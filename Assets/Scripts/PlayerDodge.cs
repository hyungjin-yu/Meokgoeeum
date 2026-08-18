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
public class PlayerDodge : MonoBehaviour
{
    [Header("설정 (27 전투 프레임 데이터, 14 밸런스 수치 시트)")]
    [Tooltip("구르기로 이동하는 거리입니다.")]
    public float dodgeDistance = 4f;

    [Tooltip("구르기 재사용 대기시간입니다. (14 밸런스 수치 시트 기준 1.0초)")]
    public float dodgeCooldown = 1.0f;

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
    private PlayerInputActions inputActions;
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

        inputActions = new PlayerInputActions();
        inputActions.Player.Dodge.performed += _ => dodgeQueued = true;
        inputActions.Enable();
    }

    private void OnDestroy()
    {
        inputActions?.Disable();
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
            yield return null;
        }

        if (playerHealth != null)
            playerHealth.SetInvulnerable(false);

        IsDodging = false;
        Debug.Log("[PlayerDodge] 구르기 종료.");
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
