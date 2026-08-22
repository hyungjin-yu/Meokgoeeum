using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BrushWeapon (붓 무기 — 기본 3타 콤보)
/// 마우스 좌클릭으로 3타 콤보를 수행합니다.
///
/// 프레임 수치(Startup/Active/Recovery)와 대미지 배율은 [[27 전투 프레임 데이터]],
/// [[14 밸런스 수치 시트]] "플레이어 기본 콤보 대미지" 기준 (60fps 환산).
///
/// 아직 캐릭터 모델/애니메이션이 없어서(Player = 캡슐) Animation Event 대신
/// 타이머로 각 단계를 구현했습니다. 나중에 실제 모델이 생기면 이 타이머 값을
/// Animation Event 호출로 교체하면 되고, 바깥에서 보이는 동작(콤보 흐름, 판정
/// 타이밍)은 그대로 유지되도록 상태머신 구조를 잡아뒀습니다.
/// </summary>
public class BrushWeapon : MonoBehaviour
{
    [Header("공격력 (14 밸런스 수치 시트)")]
    [Tooltip("기본 공격력입니다. 각 타는 이 값에 배율을 곱해서 대미지를 냅니다.")]
    public float attackPower = 10f;

    [Header("판정 (27 전투 프레임 데이터)")]
    [Tooltip("공격 판정 반경입니다.")]
    public float attackRange = 2f;

    [Tooltip("판정 중심을 캐릭터 앞쪽으로 얼마나 띄울지입니다. (정면의 적을 더 잘 맞히기 위함)")]
    public float hitOffset = 1f;

    [Tooltip("이 레이어만 판정합니다. 비워두면(Everything) 전부 검사하되, IDamageable이 없는 대상은 자동으로 무시됩니다.")]
    public LayerMask hittableLayers = ~0;

    [Header("연출 (선택 — 없으면 생략)")]
    public ParticleSystem slashEffect;

    // 내부 상태
    private enum ComboPhase { Idle, Startup, Active, Recovery }
    private ComboPhase phase = ComboPhase.Idle;
    private int comboIndex; // 0 = 없음, 1~3 = 몇 타째인지
    private float phaseTimer;
    private bool attackQueued;      // 클릭 입력을 여기 담아뒀다가 매 프레임 소비
    private bool nextComboBuffered; // Recovery 버퍼 구간에서 다음 콤보 확정 여부

    private PlayerInputActions inputActions;

    // 프레임 데이터 (60fps 기준 초 단위로 미리 환산 — 매 프레임 나눗셈 피함)
    // 인덱스: 0 = 1타, 1 = 2타, 2 = 3타
    private static readonly float[] StartupSeconds = { 6f / 60f, 5f / 60f, 8f / 60f };
    private static readonly float[] ActiveSeconds = { 4f / 60f, 4f / 60f, 6f / 60f };
    private static readonly float[] RecoverySeconds = { 10f / 60f, 12f / 60f, 18f / 60f };
    private static readonly float[] DamageMultiplier = { 0.6f, 0.7f, 1.0f };

    // Recovery 구간 중 "다음 콤보 입력을 받아주는" 마지막 구간 길이 (6프레임 ≈ 0.1초)
    // 27 문서 기준: 1타는 Recovery 4~10f(=끝) 구간에 입력, 즉 Recovery 마지막 6프레임.
    // 구현 단순화: Startup 시작 이후 언제 클릭하든 입력을 큐에 담아뒀다가,
    // 이 버퍼 구간에 들어오는 순간 콤보를 확정한다 (엄격한 4~10f 윈도우보다 살짝 관대함 —
    // 놓친 입력이 씹히는 것보다 살짝 관대한 쪽이 액션 게임에서 체감이 낫다고 판단).
    private const float ComboBufferSeconds = 6f / 60f;

    private void Start()
    {
        inputActions = new PlayerInputActions();
        inputActions.Player.Attack.performed += _ => attackQueued = true;
        inputActions.Enable();
    }

    private void OnDestroy()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        switch (phase)
        {
            case ComboPhase.Idle:
                if (attackQueued)
                {
                    attackQueued = false;
                    StartCombo(1);
                }
                break;

            case ComboPhase.Startup:
                phaseTimer += Time.deltaTime;
                if (phaseTimer >= StartupSeconds[comboIndex - 1])
                    EnterActive();
                break;

            case ComboPhase.Active:
                phaseTimer += Time.deltaTime;
                if (phaseTimer >= ActiveSeconds[comboIndex - 1])
                    EnterRecovery();
                break;

            case ComboPhase.Recovery:
                UpdateRecovery();
                break;
        }
    }

    private void UpdateRecovery()
    {
        phaseTimer += Time.deltaTime;
        float recoveryDuration = RecoverySeconds[comboIndex - 1];
        float bufferStart = recoveryDuration - ComboBufferSeconds;

        bool canChainNext = comboIndex < 3;

        if (canChainNext && attackQueued && phaseTimer >= bufferStart)
            nextComboBuffered = true;

        if (phaseTimer >= recoveryDuration)
        {
            attackQueued = false; // Recovery가 끝나면 그 이전 입력은 더 이상 유효하지 않음

            if (nextComboBuffered)
            {
                nextComboBuffered = false;
                StartCombo(comboIndex + 1);
            }
            else
            {
                EndCombo();
            }
        }
    }

    private void StartCombo(int index)
    {
        comboIndex = index;
        phase = ComboPhase.Startup;
        phaseTimer = 0f;
    }

    private void EnterActive()
    {
        phase = ComboPhase.Active;
        phaseTimer = 0f;
        PerformHit();
    }

    private void EnterRecovery()
    {
        phase = ComboPhase.Recovery;
        phaseTimer = 0f;
    }

    private void EndCombo()
    {
        phase = ComboPhase.Idle;
        comboIndex = 0;
        phaseTimer = 0f;
        nextComboBuffered = false;
    }

    /// <summary>
    /// Active 프레임에 진입하는 "그 순간" 딱 1번만 판정합니다.
    /// (Active 구간 내내 매 프레임 판정하면 한 번의 휘두르기로 여러 번 맞는 버그가 됩니다.)
    /// </summary>
    private void PerformHit()
    {
        if (slashEffect != null)
        {
            slashEffect.Stop();
            slashEffect.Play();
        }

        float damage = attackPower * DamageMultiplier[comboIndex - 1];
        Vector3 center = transform.position + transform.forward * hitOffset;

        Collider[] hits = Physics.OverlapSphere(center, attackRange, hittableLayers);
        bool didHit = false;

        foreach (var hit in hits)
        {
            // 2026-08-21: 자기 자신 무시 판정이 Rigidbody 기준이었는데, 플레이어는 Rigidbody가
            // 아니라 CharacterController를 씀 — 그래서 이 체크가 한 번도 안 걸려서 매 타격마다
            // 자기 자신도 같이 맞고 있었음(공격력을 1000으로 올렸을 때 즉사로 드러남). gameObject
            // 직접 비교를 추가해서 CharacterController 케이스도 확실히 걸러지게 함.
            if (hit.gameObject == gameObject)
                continue;
            if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == gameObject)
                continue; // 자기 자신은 무시 (Rigidbody 기반 콜라이더가 자식 오브젝트에 있는 경우 대비)

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(damage);
            didHit = true;
        }

        if (didHit)
            StartCoroutine(HitStop());
    }

    /// <summary>
    /// 히트스탑: 타격 확정 순간 0.05초(3프레임@60fps) 정지해서 타격감을 냅니다.
    /// [[27 전투 프레임 데이터]] 기준. 자주 일어나는 일이 아니라서(적중 시에만) 코루틴 사용.
    /// </summary>
    private System.Collections.IEnumerator HitStop()
    {
        float original = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.05f);
        Time.timeScale = original;
    }

    // 에디터에서 판정 범위를 눈으로 확인하기 위한 기즈모입니다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = transform.position + transform.forward * hitOffset;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
