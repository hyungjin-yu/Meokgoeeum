using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyHeup (먹괴음 - 흡, 흡수형)
/// HP가 절반(`lowHpThreshold`) 밑으로 떨어지면 가장 가까운 색 복원 구역을 찾아가 흡수해서
/// 회복하고, 회복이 다 끝날 때까지는(100%) 전투를 걸지 않습니다. 반대로 **HP가 50% 이상이면
/// 평범한 근접 공격 유닛처럼 플레이어를 공격**합니다 — 즉 "많이 다치면 도망만 가고, 어느 정도
/// 버틸 만하면 맞서 싸우는" 하이브리드 행동입니다.
///
/// [[13 먹괴음 AI 설계]] BT_Enemy_Heup 원안(HP&lt;50% → FindNearestColorRestoredArea + Absorb,
/// 아니면 MoveToPlayer)에서 두 가지를 사용자 피드백에 맞춰 바꿨습니다:
/// 1. (2026-08-20) 회복 시작 후 HP가 50%를 살짝 넘자마자 멈추던 문제 → 한 번 시작하면
///    100%까지 계속 회복하는 히스테리시스(`isHealingCommitted`) 추가
/// 2. (2026-08-20) [[27 전투 프레임 데이터]]의 "비공격 유닛" 스펙에서 벗어나, 50% 이상일 때는
///    [[EnemyPyeong]]과 동일한 근접 공격(윈드업/액티브/리커버리)을 하도록 확장
///
/// 단순화: 원안은 색마다 회복 폭이 다르지만(빨강 크게, 보라 작게), 구역별 색 정보까지
/// 등록소에 저장하는 건 범위를 넘어서서 **모든 구역 동일 회복량**으로 단순화했습니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHeup : MonoBehaviour, IKnockbackable
{
    [Header("스탯 (14 밸런스 수치 시트)")]
    public float moveSpeed = 2.5f;

    [Header("흡수 (13 먹괴음 AI 설계)")]
    [Tooltip("이 비율 밑으로 HP가 떨어지면 색 복원 구역을 찾아 회복하러 갑니다. 이 비율 이상이면 공격도 가능해집니다.")]
    [Range(0f, 1f)]
    public float lowHpThreshold = 0.5f;

    [Tooltip("구역에 이만큼 가까워지면 흡수(회복)를 시작합니다.")]
    public float absorbRadius = 1.5f;

    [Tooltip("초당 회복량입니다. (구역 안에 있는 동안 계속 적용)")]
    public float healPerSecond = 5f;

    [Header("공격 (2026-08-20 추가 — HP 50% 이상일 때만)")]
    public float attackPower = 6f;
    public float attackRange = 1.5f;
    public float attackHitRadius = 1f;

    [Header("감지 (13 AI 설계)")]
    public float sightRadius = 6f;
    public float perceptionInterval = 0.2f;

    /// <summary>회복 구역에 도달해서 흡수를 막 시작한 순간(딱 한 번) 발동합니다. [[WallExplosionHazard]]가 구독.</summary>
    public event System.Action OnAbsorbStart;

    // 텔레그래프 연출 훅 — [[EnemyPyeong]]과 동일한 용도.
    public event System.Action OnAttackWindupStart;
    public event System.Action OnAttackHit;

    private enum State { Idle, Chase, SeekHealArea, Absorbing, AttackWindup, AttackActive, AttackRecovery }
    private State state = State.Idle;
    private float perceptionTimer;
    private float stateTimer;
    private bool isKnockedBack;
    private bool isHealingCommitted; // 한 번 회복 시작하면 100% 찰 때까지 true

    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform player;
    private float distanceToPlayer = float.MaxValue;
    private Vector3 healTargetPos;
    private bool hasHealTarget;

    private const float KnockbackDuration = 0.25f;

    // 27 전투 프레임 데이터에 흡 전용 수치가 없어서(원래 비공격 유닛), 평과 동일한 프레임을 재사용.
    private const float WindupSeconds = 15f / 60f;
    private const float ActiveSeconds = 4f / 60f;
    private const float RecoverySeconds = 14f / 60f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        health = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (isKnockedBack) return;

        // 퍼셉션 갱신 + 의사결정은 매 프레임 안 하고 perceptionInterval마다만 (최적화 원칙).
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer >= perceptionInterval)
        {
            perceptionTimer = 0f;
            player = FindPlayerInSight();
            distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

            if (!IsAttacking()) // 공격 시퀀스 중엔 판단을 바꾸지 않음 (EnemyPyeong과 동일 이유)
                UpdateDecision();
        }

        // 회복은 상태가 유지되는 동안 매 프레임 스무스하게 적용합니다.
        if (state == State.Absorbing)
            health.Heal(healPerSecond * Time.deltaTime);

        // 공격 시퀀스는 매 프레임 진행해야 하므로(윈드업→액티브→리커버리) perceptionInterval과 별개로 처리.
        switch (state)
        {
            case State.AttackWindup:
                stateTimer += Time.deltaTime;
                if (stateTimer >= WindupSeconds) EnterAttackActive();
                break;

            case State.AttackActive:
                stateTimer += Time.deltaTime;
                if (stateTimer >= ActiveSeconds) EnterAttackRecovery();
                break;

            case State.AttackRecovery:
                stateTimer += Time.deltaTime;
                if (stateTimer >= RecoverySeconds) EnterChaseOrIdle();
                break;
        }
    }

    private bool IsAttacking() => state == State.AttackWindup || state == State.AttackActive || state == State.AttackRecovery;

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

    /// <summary>
    /// BT_Enemy_Heup 확장판 Selector: HP 50% 미만이면 회복만(공격 없음), 그 이상이면 사거리 안일
    /// 때 공격, 아니면 추격.
    /// </summary>
    private void UpdateDecision()
    {
        bool isLowHp = health.CurrentHP < health.maxHP * lowHpThreshold;
        if (isLowHp) isHealingCommitted = true;

        if (isHealingCommitted && health.CurrentHP >= health.maxHP)
            isHealingCommitted = false; // 완전히 다 찼으면 회복 종료

        bool canFight = health.CurrentHP >= health.maxHP * lowHpThreshold; // 50% 이상 = 공격 가능

        if (canFight && player != null && distanceToPlayer <= attackRange)
        {
            EnterAttackWindup();
            return;
        }

        if (isHealingCommitted)
        {
            UpdateSeekHealArea();
            return;
        }

        hasHealTarget = false; // 회복 완전히 끝났으면 다음에 다시 낮아졌을 때 새로 탐색
        state = player != null ? State.Chase : State.Idle;

        if (state == State.Chase)
            agent.SetDestination(player.position);
    }

    private void UpdateSeekHealArea()
    {
        if (!hasHealTarget)
        {
            hasHealTarget = RestoredAreaRegistry.TryFindNearest(transform.position, out healTargetPos);
            if (!hasHealTarget)
            {
                // 등록된 색 복원 구역이 하나도 없으면 할 수 있는 게 없어서 그냥 대기
                state = State.Idle;
                return;
            }
        }

        float distToHealArea = Vector3.Distance(transform.position, healTargetPos);
        if (distToHealArea <= absorbRadius)
        {
            if (state != State.Absorbing) // 상태 전이 시점에만 1회 발동 (매 퍼셉션 틱마다 X)
            {
                state = State.Absorbing;
                agent.isStopped = true;
                OnAbsorbStart?.Invoke();
            }
        }
        else
        {
            state = State.SeekHealArea;
            agent.isStopped = false;
            agent.SetDestination(healTargetPos);
        }
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

    /// <summary>Active 프레임 진입 시 1회만 판정합니다. ([[EnemyPyeong]]과 동일한 패턴)</summary>
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

    /// <summary>IKnockbackable 구현. [[EnemyPyeong]]과 동일한 방식입니다.</summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(direction.normalized, force));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        Debug.Log($"[EnemyHeup] {name} 넉백당함! 방향: {direction}, 힘: {force}");

        isKnockedBack = true;
        bool wasAgentEnabled = agent.enabled;
        if (wasAgentEnabled) agent.enabled = false;

        float elapsed = 0f;
        while (elapsed < KnockbackDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float speed = force * (1f - elapsed / KnockbackDuration);
            transform.position += direction * speed * dt;
            yield return null;
        }

        if (wasAgentEnabled)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.isStopped = false;
        }

        // 공격 중이었더라도 넉백당하면 리셋 — 맞고도 태연히 공격을 이어가면 안 맞은 것처럼 느껴짐
        // (isHealingCommitted는 그대로 유지 — 회복 목표 자체는 넉백당했다고 취소되지 않음)
        state = player != null ? State.Chase : State.Idle;
        stateTimer = 0f;
        isKnockedBack = false;

        Debug.Log($"[EnemyHeup] {name} 넉백 종료.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, absorbRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
