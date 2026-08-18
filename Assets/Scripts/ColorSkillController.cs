using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ColorSkillController (색 스킬 — 붓 스킬)
/// [[02 플레이어 시스템]] "붓 스킬(색별)" 중 v0.2 범위인 3종을 구현합니다.
/// 초록(되살림)/보라(왜곡)/검정(먹물)은 아직 없습니다 — 나중에 같은 패턴(Skill4~6 액션 추가)으로 넣으면 됩니다.
///
/// 키 1/2/3을 각 색에 고정 매핑했습니다. 원안은 "1~6 슬롯"이 FIFO로 계속 바뀌는 구조지만,
/// 슬롯을 화면에 보여줄 UI가 아직 없어서(v0.1엔 UI 자체가 없음) 지금은 "그 색 구슬을
/// 보유하고 있으면 그 키로 바로 쓴다"로 단순화했습니다. UI가 생기면 실제 슬롯 매핑으로 교체.
///
/// 대미지 배율/쿨타임은 [[14 밸런스 수치 시트]] "플레이어 스킬 대미지", 공통 캐스팅 Startup은
/// [[27 전투 프레임 데이터]] "색 스킬(1~6) 캐스팅" 기준(10f)입니다. 콤보 중 스킬 캔슬은 아직
/// 구현 안 함 — v0.2 후속으로 미룸 (지금은 BrushWeapon 상태와 무관하게 독립적으로 동작).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ColorSkillController : MonoBehaviour
{
    [Header("빨강 — 강타 (전방 대시 + 강타, 14 밸런스 수치 시트: ×2.0 / 8초)")]
    public float strikeCooldown = 8f;
    public float strikeDamageMultiplier = 2.0f;
    public float strikeDashDistance = 5f;
    public float strikeDashDuration = 0.15f;
    public float strikeHitRadius = 1.5f;

    [Header("파랑 — 흐름 (전방 직선 관통, 14 밸런스 수치 시트: ×1.5 / 6초)")]
    public float flowCooldown = 6f;
    public float flowDamageMultiplier = 1.5f;
    public float flowLength = 6f;
    public float flowWidth = 2f;

    [Header("노랑 — 번쩍 (주변 범위 폭발 + 넉백, 14 밸런스 수치 시트: ×1.0 / 10초)")]
    public float flashCooldown = 10f;
    public float flashDamageMultiplier = 1.0f;
    public float flashRadius = 4f;
    public float flashKnockbackForce = 12f;

    // 27 전투 프레임 데이터 - 색 스킬 공통 Startup (10f ≈ 0.167초, 60fps 환산)
    private const float StartupSeconds = 10f / 60f;

    public bool IsDashing { get; private set; } // 강타의 대시 구간 동안 true (PlayerController가 이동을 양보하도록)

    private CharacterController cc;
    private BrushWeapon brushWeapon; // 스킬 대미지의 기준이 되는 "붓 공격력"을 여기서 읽어옴 (단일 출처 유지)
    private PlayerInputActions inputActions;

    private float strikeCooldownTimer;
    private float flowCooldownTimer;
    private float flashCooldownTimer;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        brushWeapon = GetComponent<BrushWeapon>();
    }

    private void Start()
    {
        inputActions = new PlayerInputActions();
        inputActions.Player.Skill1.performed += _ => TryCast(OrbColor.Red);
        inputActions.Player.Skill2.performed += _ => TryCast(OrbColor.Blue);
        inputActions.Player.Skill3.performed += _ => TryCast(OrbColor.Yellow);
        inputActions.Enable();
    }

    private void OnDestroy()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        if (strikeCooldownTimer > 0f) strikeCooldownTimer -= Time.deltaTime;
        if (flowCooldownTimer > 0f) flowCooldownTimer -= Time.deltaTime;
        if (flashCooldownTimer > 0f) flashCooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 기본 공격력(붓 공격력)입니다. BrushWeapon이 없으면 14 밸런스 수치 시트 기준값(10)을 씁니다.
    /// </summary>
    private float BaseAttackPower => brushWeapon != null ? brushWeapon.attackPower : 10f;

    private void TryCast(OrbColor color)
    {
        float cooldownTimer = GetCooldownTimer(color);
        if (cooldownTimer > 0f)
        {
            Debug.Log($"[ColorSkillController] {color} 스킬 쿨타임 중 (남은 {cooldownTimer:F1}초)");
            return;
        }

        if (ColorSystemManager.Instance == null || !ColorSystemManager.Instance.TryConsumeOrb(color))
        {
            Debug.Log($"[ColorSkillController] {color} 구슬이 없어서 스킬을 쓸 수 없음");
            return;
        }

        SetCooldownTimer(color, GetCooldownDuration(color));

        switch (color)
        {
            case OrbColor.Red:
                StartCoroutine(CastStrike());
                break;
            case OrbColor.Blue:
                StartCoroutine(CastFlow());
                break;
            case OrbColor.Yellow:
                StartCoroutine(CastFlash());
                break;
        }
    }

    private float GetCooldownTimer(OrbColor color) => color switch
    {
        OrbColor.Red => strikeCooldownTimer,
        OrbColor.Blue => flowCooldownTimer,
        OrbColor.Yellow => flashCooldownTimer,
        _ => 0f,
    };

    private float GetCooldownDuration(OrbColor color) => color switch
    {
        OrbColor.Red => strikeCooldown,
        OrbColor.Blue => flowCooldown,
        OrbColor.Yellow => flashCooldown,
        _ => 0f,
    };

    private void SetCooldownTimer(OrbColor color, float value)
    {
        switch (color)
        {
            case OrbColor.Red: strikeCooldownTimer = value; break;
            case OrbColor.Blue: flowCooldownTimer = value; break;
            case OrbColor.Yellow: flashCooldownTimer = value; break;
        }
    }

    /// <summary>
    /// 강타(빨강): Startup 후 전방으로 짧게 대시하고, 도착 지점에서 강력한 한 방을 때립니다.
    /// </summary>
    private IEnumerator CastStrike()
    {
        Debug.Log("[ColorSkillController] 강타 시전!");
        yield return new WaitForSeconds(StartupSeconds);

        IsDashing = true;
        Vector3 direction = transform.forward;
        float speed = strikeDashDistance / strikeDashDuration;

        float elapsed = 0f;
        while (elapsed < strikeDashDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            cc.Move(direction * speed * dt);
            yield return null;
        }
        IsDashing = false;

        float damage = BaseAttackPower * strikeDamageMultiplier;
        Collider[] hits = Physics.OverlapSphere(transform.position, strikeHitRadius);
        int hitCount = DamageAll(hits, damage);
        Debug.Log($"[ColorSkillController] 강타 적중! 대미지 {damage} x {hitCount}대상");
    }

    /// <summary>
    /// 흐름(파랑): Startup 후 전방 직선 범위(박스) 안의 모든 대상을 관통 히트합니다.
    /// </summary>
    private IEnumerator CastFlow()
    {
        Debug.Log("[ColorSkillController] 흐름 시전!");
        yield return new WaitForSeconds(StartupSeconds);

        float damage = BaseAttackPower * flowDamageMultiplier;
        Vector3 center = transform.position + transform.forward * (flowLength / 2f);
        Vector3 halfExtents = new Vector3(flowWidth / 2f, 1f, flowLength / 2f);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation);
        int hitCount = DamageAll(hits, damage);
        Debug.Log($"[ColorSkillController] 흐름 적중! 대미지 {damage} x {hitCount}대상");
    }

    /// <summary>
    /// 번쩍(노랑): Startup 후 주변 반경 전체에 대미지 + 넉백을 줍니다.
    /// </summary>
    private IEnumerator CastFlash()
    {
        Debug.Log("[ColorSkillController] 번쩍 시전!");
        yield return new WaitForSeconds(StartupSeconds);

        float damage = BaseAttackPower * flashDamageMultiplier;
        Collider[] hits = Physics.OverlapSphere(transform.position, flashRadius);

        int hitCount = 0;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player")) continue; // 자기 자신 제외

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(damage);
            hitCount++;

            var knockbackable = hit.GetComponent<IKnockbackable>();
            if (knockbackable != null)
            {
                Vector3 dir = hit.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
                knockbackable.ApplyKnockback(dir.normalized, flashKnockbackForce);
            }
        }

        Debug.Log($"[ColorSkillController] 번쩍 적중! 대미지 {damage} x {hitCount}대상 (넉백 포함)");
    }

    /// <summary>
    /// hits 중 자기 자신을 제외한 IDamageable에게 전부 같은 대미지를 줍니다. 맞은 수를 반환합니다.
    /// </summary>
    private int DamageAll(Collider[] hits, float damage)
    {
        int count = 0;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player")) continue; // 자기 자신 제외

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(damage);
            count++;
        }
        return count;
    }

    // 에디터에서 판정 범위를 눈으로 확인하기 위한 기즈모입니다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * strikeDashDistance, strikeHitRadius);

        Gizmos.color = Color.blue;
        Vector3 flowCenter = transform.position + transform.forward * (flowLength / 2f);
        Gizmos.matrix = Matrix4x4.TRS(flowCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(flowWidth, 2f, flowLength));
        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, flashRadius);
    }
}
