using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyBun (먹괴음 - 분, 분열형)
/// 행동 자체는 [[EnemyPyeong]](평)과 완전히 동일한 근접형입니다 — [[13 먹괴음 AI 설계]]에도
/// "평과 동일 BT"로 명시되어 있어서, 별도 추상화 없이 같은 상태머신을 그대로 복제했습니다
/// (지금 규모에서 상속/공유 베이스 클래스로 묶는 것보다 단순 복제가 유지보수하기 더 쉽다고 판단).
///
/// 차이는 딱 하나: 처치되면 자기 자신을 복제해서 체력을 절반으로 낮춘 미니언 2마리로
/// 분열합니다(`isMinor`가 true인 미니언은 다시 분열하지 않음 — 무한 분열 방지).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBun : MonoBehaviour, IKnockbackable
{
    [Header("스탯 (14 밸런스 수치 시트 — 분(원본) 기준)")]
    public float attackPower = 7f;
    public float moveSpeed = 3f;

    [Header("분열 (13 먹괴음 AI 설계 — OnDeath: 미니언 2마리)")]
    [Tooltip("분열로 생성된 미니언인지 여부입니다. 미니언은 죽어도 다시 분열하지 않습니다.")]
    public bool isMinor;

    [Tooltip("미니언의 체력 배율입니다. (원본 HP × 이 값)")]
    public float minorHpMultiplier = 0.5f;

    [Tooltip("미니언의 크기 배율입니다.")]
    public float minorScaleMultiplier = 0.6f;

    [Header("감지/판정 (13 AI 설계, 27 프레임 데이터)")]
    public float sightRadius = 6f;
    public float attackRange = 1.5f;
    public float attackHitRadius = 1f;
    public float perceptionInterval = 0.2f;

    public event System.Action OnAttackWindupStart;
    public event System.Action OnAttackHit;

    private enum State { Idle, Chase, AttackWindup, AttackActive, AttackRecovery }
    private State state = State.Idle;
    private float stateTimer;
    private float perceptionTimer;
    private bool isKnockedBack;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform player;
    private float distanceToPlayer = float.MaxValue;

    // 27 전투 프레임 데이터 - 먹괴음 평과 동일 텔레그래프(분도 "평과 동일 BT")
    private const float WindupSeconds = 15f / 60f;
    private const float ActiveSeconds = 4f / 60f;
    private const float RecoverySeconds = 14f / 60f;
    private const float KnockbackDuration = 0.25f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        health = GetComponent<EnemyHealth>();
        health.OnDeath += _ => SplitOnDeath();
    }

    private void Update()
    {
        if (isKnockedBack) return;

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

    private void UpdatePerception()
    {
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer < perceptionInterval) return;
        perceptionTimer = 0f;

        player = FindPlayerInSight();
        distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;

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

    /// <summary>
    /// [[13 먹괴음 AI 설계]] "OnDeath: Instantiate(minorEnemyPrefab) × 2 후 Destroy".
    /// 별도 미니언 프리팹 대신, 이미 검증된 자기 자신을 복제해서 체력/크기만 줄이는 방식을 씁니다
    /// (아트 에셋이 아직 없어서 새 프리팹을 만들 수 없음 — 같은 GameObject를 복제하는 게 가장
    /// 확실하게 동작하는 방법).
    /// </summary>
    private void SplitOnDeath()
    {
        if (isMinor) return; // 미니언은 또 분열하지 않음 (무한 분열 방지)

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 1f;
            offset.y = 0f;
            Vector3 spawnPos = transform.position + offset;

            GameObject clone = Instantiate(gameObject, spawnPos, transform.rotation);
            clone.transform.localScale = transform.localScale * minorScaleMultiplier;

            var cloneBun = clone.GetComponent<EnemyBun>();
            cloneBun.isMinor = true;

            var cloneHealth = clone.GetComponent<EnemyHealth>();
            cloneHealth.ConfigureMaxHP(health.maxHP * minorHpMultiplier);

            Debug.Log($"[EnemyBun] 분열! {clone.name} 생성 (미니언, HP {cloneHealth.CurrentHP})");
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
        Debug.Log($"[EnemyBun] {name} 넉백당함! 방향: {direction}, 힘: {force}");

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

        state = player != null ? State.Chase : State.Idle;
        stateTimer = 0f;
        isKnockedBack = false;

        Debug.Log($"[EnemyBun] {name} 넉백 종료.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
