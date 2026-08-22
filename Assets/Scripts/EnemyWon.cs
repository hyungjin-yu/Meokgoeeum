using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyWon (먹괴음 - 원, 원거리형)
/// 일정 거리(2~5f)를 유지하며 먹물을 던집니다. 너무 가까이 붙으면 물러납니다.
///
/// [[13 먹괴음 AI 설계]] BT_Enemy_Won(Selector: 사거리 안이면 투척, 너무 가까우면 후퇴,
/// 아니면 추격)을 [[EnemyPyeong]]과 같은 C# 상태머신 방식으로 구현했습니다.
/// 공격 타이밍은 [[27 전투 프레임 데이터]] 텔레그래프 기준입니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyWon : MonoBehaviour, IKnockbackable
{
    [Header("스탯 (14 밸런스 수치 시트)")]
    public float attackPower = 10f;
    public float moveSpeed = 2f;

    [Header("감지/판정 (13 AI 설계, 27 프레임 데이터)")]
    [Tooltip("이 반경 안에 들어오면 플레이어를 인지합니다.")]
    public float sightRadius = 6f;

    [Tooltip("이 거리 범위 안이면 투척 공격을 합니다.")]
    public float attackMinRange = 2f;
    public float attackMaxRange = 5f;

    [Tooltip("이 거리 이하로 붙으면 후퇴합니다.")]
    public float retreatTriggerRange = 1.5f;

    [Tooltip("후퇴할 때 확보하려는 거리입니다.")]
    public float retreatTargetDistance = 2f;

    [Tooltip("퍼셉션(플레이어 탐지)과 이동 목적지 갱신 주기입니다. (최적화 원칙 — EnemyPyeong과 동일)")]
    public float perceptionInterval = 0.2f;

    [Header("투사체")]
    public float projectileSpeed = 12f;

    public event System.Action OnAttackWindupStart;
    public event System.Action OnAttackHit;

    private enum State { Idle, Chase, Retreat, AttackWindup, AttackRecovery }
    private State state = State.Idle;
    private float stateTimer;
    private float perceptionTimer;
    private bool isKnockedBack;

    private NavMeshAgent agent;
    private Transform player;
    private float distanceToPlayer = float.MaxValue;

    // 27 전투 프레임 데이터 - 먹괴음 원 (60fps 기준 초 단위 환산)
    private const float WindupSeconds = 20f / 60f;
    private const float RecoverySeconds = 16f / 60f;
    private const float KnockbackDuration = 0.25f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }

    private void Update()
    {
        if (isKnockedBack) return; // 넉백 중엔 AI 로직을 통째로 쉰다 (agent를 꺼둔 상태)

        UpdatePerception();

        switch (state)
        {
            case State.Idle:
            case State.Chase:
            case State.Retreat:
                UpdateDecision();
                break;

            case State.AttackWindup:
                stateTimer += Time.deltaTime;
                if (stateTimer >= WindupSeconds)
                    EnterAttackRecovery(); // 투척 자체는 Windup 끝나는 순간 1회 실행
                break;

            case State.AttackRecovery:
                stateTimer += Time.deltaTime;
                if (stateTimer >= RecoverySeconds)
                    EnterChaseOrIdle();
                break;
        }
    }

    /// <summary>매 프레임이 아니라 perceptionInterval마다 플레이어를 찾고 이동 목적지를 갱신합니다.</summary>
    private void UpdatePerception()
    {
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer < perceptionInterval) return;
        perceptionTimer = 0f;

        player = FindPlayerInSight();
        distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;

        if (player == null) return;

        if (state == State.Chase)
            agent.SetDestination(player.position);
        else if (state == State.Retreat)
        {
            Vector3 away = (transform.position - player.position).normalized;
            agent.SetDestination(transform.position + away * retreatTargetDistance);
        }
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

    /// <summary>
    /// BT_Enemy_Won의 Selector: 사거리 안이면 투척, 너무 가까우면 후퇴, 아니면 추격.
    /// </summary>
    private void UpdateDecision()
    {
        if (player == null)
        {
            state = State.Idle;
            return;
        }

        if (distanceToPlayer <= retreatTriggerRange)
        {
            state = State.Retreat;
            return;
        }

        if (distanceToPlayer >= attackMinRange && distanceToPlayer <= attackMaxRange)
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

    private void EnterAttackRecovery()
    {
        state = State.AttackRecovery;
        stateTimer = 0f;
        ThrowInk();
    }

    private void EnterChaseOrIdle()
    {
        agent.isStopped = false;
        state = player != null ? State.Chase : State.Idle;
        stateTimer = 0f;
    }

    /// <summary>
    /// Windup이 끝나는 순간 1회만 실행됩니다. (BrushWeapon/EnemyPyeong과 동일한 이유 — 다단히트 방지)
    /// 먹물 투사체를 런타임 프리미티브로 생성해서 플레이어 방향으로 던집니다.
    /// </summary>
    private void ThrowInk()
    {
        OnAttackHit?.Invoke();
        if (player == null) return;

        GameObject projectileObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObj.name = "InkProjectile";
        projectileObj.transform.position = transform.position + Vector3.up * 1f + transform.forward * 0.5f;
        projectileObj.transform.localScale = Vector3.one * 0.3f;

        var col = projectileObj.GetComponent<SphereCollider>();
        col.isTrigger = true;

        var rb = projectileObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var rendererComp = projectileObj.GetComponent<Renderer>();
        rendererComp.material.color = Color.black; // 먹물

        var projectile = projectileObj.AddComponent<InkProjectile>();
        projectile.speed = projectileSpeed;
        Vector3 direction = player.position - projectileObj.transform.position;
        projectile.Launch(direction, attackPower);

        // 2026-08-21: 원거리 공격마다 찍히는 로그가 콘솔을 도배해서 제거 (피격/회복 로그와 같은 이유)
    }

    /// <summary>IKnockbackable 구현. [[EnemyPyeong]]과 동일한 방식입니다.</summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(direction.normalized, force));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        Debug.Log($"[EnemyWon] {name} 넉백당함! 방향: {direction}, 힘: {force}");

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

        Debug.Log($"[EnemyWon] {name} 넉백 종료.");
    }

    // 에디터에서 감지/판정 범위를 눈으로 확인하기 위한 기즈모입니다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackMinRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackMaxRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatTriggerRange);
    }
}
