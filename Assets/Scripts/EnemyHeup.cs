using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyHeup (먹괴음 - 흡, 흡수형)
/// **비공격 유닛입니다** — [[27 전투 프레임 데이터]] "텔레그래프 불필요(비공격 유닛)" 명시대로
/// 플레이어를 절대 공격하지 않습니다. HP가 절반 밑으로 떨어지면 가장 가까운 색 복원 구역을
/// 찾아가 흡수해서 회복하고, 그렇지 않을 때는 플레이어 쪽으로 다가만 옵니다(공격 없이).
///
/// [[13 먹괴음 AI 설계]] BT_Enemy_Heup 그대로: HP&lt;50% → FindNearestColorRestoredArea + Absorb,
/// 아니면 MoveToPlayer. "구역 찾기"는 [[RestoredAreaRegistry]] 정적 유틸로 구현.
///
/// 단순화: 원안은 색마다 회복 폭이 다르지만(빨강 크게, 보라 작게 — 가시광선 스펙트럼 기준),
/// 구역별 색 정보까지 등록소에 저장하는 건 지금 범위를 넘어서서 **모든 구역 동일 회복량**으로
/// 단순화했습니다. 나중에 [[RestoredAreaRegistry]]가 색까지 같이 저장하게 확장하면 됩니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHeup : MonoBehaviour, IKnockbackable
{
    [Header("스탯 (14 밸런스 수치 시트)")]
    public float moveSpeed = 2.5f;

    [Header("흡수 (13 먹괴음 AI 설계)")]
    [Tooltip("이 비율 밑으로 HP가 떨어지면 색 복원 구역을 찾아 회복하러 갑니다.")]
    [Range(0f, 1f)]
    public float lowHpThreshold = 0.5f;

    [Tooltip("구역에 이만큼 가까워지면 흡수(회복)를 시작합니다.")]
    public float absorbRadius = 1.5f;

    [Tooltip("초당 회복량입니다. (구역 안에 있는 동안 계속 적용)")]
    public float healPerSecond = 5f;

    [Header("감지 (13 AI 설계)")]
    public float sightRadius = 6f;
    public float perceptionInterval = 0.2f;

    /// <summary>
    /// 회복 구역에 도달해서 흡수를 막 시작한 순간(딱 한 번) 발동합니다.
    /// 2026-08-20: 4층 [[WallExplosionHazard]]가 구독해서 "접근 전에 처치" 페널티를 겁니다.
    /// </summary>
    public event System.Action OnAbsorbStart;

    private enum State { Idle, Chase, SeekHealArea, Absorbing }
    private State state = State.Idle;
    private float perceptionTimer;
    private bool isKnockedBack;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform player;
    private Vector3 healTargetPos;
    private bool hasHealTarget;

    private const float KnockbackDuration = 0.25f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        health = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (isKnockedBack) return;

        // 퍼셉션 갱신 + 목적지 재계산(SetDestination)은 매 프레임 안 하고 perceptionInterval마다만
        // 합니다 (최적화 원칙 — EnemyPyeong/EnemyWon과 동일한 이유).
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer >= perceptionInterval)
        {
            perceptionTimer = 0f;
            player = FindPlayerInSight();
            UpdateDecision();
        }

        // 회복은 상태가 유지되는 동안 매 프레임 스무스하게 적용합니다.
        if (state == State.Absorbing)
            health.Heal(healPerSecond * Time.deltaTime);
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
    /// BT_Enemy_Heup의 Selector: HP 낮으면 회복 구역 탐색/흡수, 아니면 플레이어 쪽으로 이동만.
    /// </summary>
    private void UpdateDecision()
    {
        bool isLowHp = health.CurrentHP < health.maxHP * lowHpThreshold;

        if (isLowHp)
        {
            UpdateSeekHealArea();
            return;
        }

        hasHealTarget = false; // HP 회복돼서 정상으로 돌아오면 다음에 다시 낮아졌을 때 새로 탐색
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

        isKnockedBack = false;
        Debug.Log($"[EnemyHeup] {name} 넉백 종료.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, absorbRadius);
    }
}
