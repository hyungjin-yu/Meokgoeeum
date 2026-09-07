using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerDodge (구르기 회피)
/// [[27 전투 프레임 데이터]] "플레이어 - 구르기 회피(Space)" 스펙 구현입니다.
///
/// 배경: 2026-08-18 밸런스 이슈("먹괴음-평에게 한 교전에 HP 100→65") 조사 결과,
/// attackPower는 이미 기획서 기준값(8)과 일치했고 진짜 원인은 "평의 텔레그래프
/// (Windup 15f≈0.25초)를 피할 유일한 방어 수단인 구르기가 v0.1 코드에 아예
/// 없었다"는 것이었습니다. 그래서 숫자를 깎는 대신 원안대로 구르기를 구현합니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerController))] // Start()에서 PlayerController.InputActions를 공유해서 씀
public class PlayerDodge : MonoBehaviour
{
    [Header("설정 (27 전투 프레임 데이터, 14 밸런스 수치 시트)")]
    [Tooltip("구르기로 이동하는 거리입니다.")]
    public float dodgeDistance = 4f;

    [Tooltip("구르기 재사용 대기시간입니다. (14 밸런스 수치 시트 기준 1.0초)")]
    public float dodgeCooldown = 1.0f;

    [Header("넉백 (32 QA 리뷰 SYS-4 — 흡 회복 저지 수단)")]
    [Tooltip("구르는 동안 스치는 적을 밀어내는 판정 반경입니다.")]
    public float knockbackRadius = 1.5f;

    [Tooltip("구르기가 적에게 주는 넉백 힘입니다. (다른 넉백 소스들과 동일 범위 — 번쩍 12, 보스 AoE 10)")]
    public float dodgeKnockbackForce = 10f;

    [Tooltip("이 레이어만 판정합니다. 비워두면(Everything) 전부 검사하되, IKnockbackable이 없는 대상은 자동으로 무시됩니다.")]
    public LayerMask knockbackLayers = ~0;

    [Header("연결 (References)")]
    [Tooltip("이동 방향 계산 기준 카메라입니다. 비워두면 Start()에서 Camera.main을 자동으로 찾습니다.")]
    public Transform cameraTransform;

    public bool IsDodging { get; private set; }

    // 27 문서 기준 프레임 데이터 (60fps 환산, 초 단위로 미리 계산 — 매 프레임 나눗셈 피함)
    private const float StartupSeconds = 3f / 60f;       // 무적 아님 (선입력 대비 구간)
    private const float InvulnerableSeconds = 12f / 60f; // 0.2초 무적(i-frame)
    private const float RecoverySeconds = 9f / 60f;      // 행동 불가
    private const float TotalDuration = StartupSeconds + InvulnerableSeconds + RecoverySeconds; // 24f = 0.4초

    private CharacterController cc;
    private PlayerHealth playerHealth;
    private PlayerInputActions inputActions; // PlayerController가 소유 — 여기선 구독/조회만 함
    private bool dodgeQueued;
    private float cooldownTimer;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // PlayerController가 Awake()에서 만들어 Enable()까지 해둔 인스턴스를 공유해서 씁니다.
        inputActions = GetComponent<PlayerController>().InputActions;
        inputActions.Player.Dodge.performed += _ => dodgeQueued = true;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (dodgeQueued)
        {
            dodgeQueued = false;
            if (!IsDodging && cooldownTimer <= 0f)
                StartCoroutine(DodgeRoutine());
        }
    }

    private IEnumerator DodgeRoutine()
    {
        IsDodging = true;
        cooldownTimer = dodgeCooldown;

        Vector3 direction = GetDodgeDirection();
        float speed = dodgeDistance / TotalDuration;
        Debug.Log($"[PlayerDodge] 구르기 시작! 방향: {direction}");

        float elapsed = 0f;
        while (elapsed < TotalDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            bool shouldBeInvulnerable = elapsed >= StartupSeconds && elapsed < StartupSeconds + InvulnerableSeconds;
            if (playerHealth != null)
                playerHealth.SetInvulnerable(shouldBeInvulnerable);

            cc.Move(direction * speed * dt);
            ApplyKnockbackToNearbyEnemies(direction);
            yield return null;
        }

        if (playerHealth != null)
            playerHealth.SetInvulnerable(false);

        IsDodging = false;
        Debug.Log("[PlayerDodge] 구르기 종료.");
    }

    /// <summary>
    /// 2026-09-04 — [[32 QA 리뷰 - 기획자 3인]] SYS-4: "흡(EnemyHeup)의 회복 후퇴를 저지할
    /// 수단이 없다"는 지적에 대한 최소 픽스. 구르기로 스치면 밀려나게 해서, 회복 구역으로
    /// 도망가는 흡을 구르기로 쳐내 저지할 수 있게 합니다. 이미 있는 IKnockbackable을
    /// 그대로 재사용 — 새 시스템 없이 기존 넉백 파이프라인(EnemyHeup 등)에 얹었습니다.
    /// [[BrushWeapon]]과 같은 방식(OverlapSphere + IKnockbackable)이라 매 프레임 불러도
    /// 이미 넉백 중인 대상은 각 Enemy의 ApplyKnockback이 자체적으로 무시합니다.
    /// </summary>
    private void ApplyKnockbackToNearbyEnemies(Vector3 dodgeDirection)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, knockbackRadius, knockbackLayers);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var knockbackable = hit.GetComponent<IKnockbackable>();
            knockbackable?.ApplyKnockback(dodgeDirection, dodgeKnockbackForce);
        }
    }

    /// <summary>
    /// 이동 입력이 있으면 그 방향(카메라 기준)으로, 없으면 캐릭터가 보고 있는 방향으로 구릅니다.
    /// PlayerController.Move()와 동일한 카메라 기준 변환 방식을 씁니다.
    /// </summary>
    private Vector3 GetDodgeDirection()
    {
        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude < 0.0001f)
            return transform.forward;

        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * moveInput.y + camRight * moveInput.x).normalized;
    }
}
