using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyPyeong (먹괴음 - 평, 기본형)
/// 플레이어를 감지하면 다가가서, 사거리 안에 들어오면 근접 공격합니다.
///
/// [[13 먹괴음 AI 설계]]의 BT_Enemy_Pyeong(Selector: 사거리 안이면 공격, 아니면 추격)을
/// C# 상태머신으로 구현했습니다 — 문서에도 "Unity Behavior 패키지 불안정 시
/// 상태머신으로 대체 가능하게 설계" 라는 폴백이 이미 명시돼 있어서 그대로 따랐습니다.
/// 공격 타이밍은 [[27 전투 프레임 데이터]]의 텔레그래프 프레임 수치 기준입니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyPyeong : MonoBehaviour
{
    [Header("스탯 (14 밸런스 수치 시트)")]
    public float attackPower = 8f;
    public float moveSpeed = 3f;

    [Header("감지/판정 (13 AI 설계, 27 프레임 데이터)")]
    [Tooltip("이 반경 안에 들어오면 플레이어를 인지합니다.")]
    public float sightRadius = 6f;

    [Tooltip("이 거리 이하면 추격을 멈추고 공격을 시작합니다.")]
    public float attackRange = 1.5f;

    [Tooltip("공격이 실제로 맞는 판정 반경입니다.")]
    public float attackHitRadius = 1f;

    [Tooltip("퍼셉션(플레이어 탐지)과 이동 목적지 갱신 주기입니다. 매 프레임 안 하고 이 간격으로 쉬어갑니다 — 적이 여러 마리일 때 부하를 줄이기 위함 (최적화 원칙).")]
    public float perceptionInterval = 0.2f;

    // 텔레그래프 연출 훅 — VFX/애니메이션은 나중에 이 이벤트를 구독해서 붙이면 됩니다. 지금은 로직만.
    public event System.Action OnAttackWindupStart;
    public event System.Action OnAttackHit;

    private enum State { Idle, Chase, AttackWindup, AttackActive, AttackRecovery }
    private State state = State.Idle;
    private float stateTimer;
    private float perceptionTimer;

    private NavMeshAgent agent;
    private Transform player;
    private float distanceToPlayer = float.MaxValue;

    // 27 전투 프레임 데이터 - 먹괴음 평 (60fps 기준 초 단위 환산)
    private const float WindupSeconds = 15f / 60f;
    private const float ActiveSeconds = 4f / 60f;
    private const float RecoverySeconds = 14f / 60f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }

    private void Update()
    {
        UpdatePerception();

        switch (state)
        {
            case State.Idle:
            case State.Chase:
                UpdateChase();
                break;

            case State.AttackWindup:
                stateTimer += Time.deltaTime;
                if (stateTimer >= WindupSeconds)
                    EnterAttackActive();
                break;

            case State.AttackActive:
                stateTimer += Time.deltaTime;
                if (stateTimer >= ActiveSeconds)
                    EnterAttackRecovery();
                break;

            case State.AttackRecovery:
                stateTimer += Time.deltaTime;
                if (stateTimer >= RecoverySeconds)
                    EnterChaseOrIdle();
                break;
        }
    }

    /// <summary>
    /// 매 프레임이 아니라 perceptionInterval마다 플레이어를 찾고 이동 목적지를 갱신합니다.
    /// (EnemyPerception 역할. Tag == "Player" 기준으로 찾습니다 — Layer 설정을 따로
    /// 안 해도 되게 하려고 태그 방식을 선택했습니다.)
    /// </summary>
    private void UpdatePerception()
    {
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer < perceptionInterval) return;
        perceptionTimer = 0f;

        player = FindPlayerInSight();
        distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;

        // 공격 중이 아닐 때만 이동 목적지를 갱신합니다 (공격 도중엔 agent가 멈춰있음).
        if (player != null && IsChasingState())
            agent.SetDestination(player.position);
    }

    private Transform FindPlayerInSight()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sightRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
                return hit.transform;
        }
        return null;
    }

    private bool IsChasingState() => state == State.Idle || state == State.Chase;

    private void UpdateChase()
    {
        if (player == null)
        {
            state = State.Idle;
            return;
        }

        if (distanceToPlayer <= attackRange)
        {
            EnterAttackWindup();
            return;
        }

        state = State.Chase;
    }

    private void EnterAttackWindup()
    {
        state = State.AttackWindup;
        stateTimer = 0f;
        agent.isStopped = true;
        OnAttackWindupStart?.Invoke();
    }

    private void EnterAttackActive()
    {
        state = State.AttackActive;
        stateTimer = 0f;
        PerformAttack();
    }

    private void EnterAttackRecovery()
    {
        state = State.AttackRecovery;
        stateTimer = 0f;
    }

    private void EnterChaseOrIdle()
    {
        agent.isStopped = false;
        state = player != null ? State.Chase : State.Idle;
        stateTimer = 0f;
    }

    /// <summary>
    /// Active 프레임 진입 시 1회만 판정합니다. (BrushWeapon과 동일한 이유 — 다단히트 방지)
    /// </summary>
    private void PerformAttack()
    {
        OnAttackHit?.Invoke();

        Collider[] hits = Physics.OverlapSphere(transform.position, attackHitRadius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            var damageable = hit.GetComponent<IDamageable>();
            damageable?.TakeDamage(attackPower);
        }
    }

    // 에디터에서 감지/공격 범위를 눈으로 확인하기 위한 기즈모입니다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
