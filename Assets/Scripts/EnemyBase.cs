using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyGwang/Bun/Won/Pyeong/Heup 5종 몹의 공통 기반 클래스.
/// [[먹괴음 리팩토링 계획]] 1단계 — 전투 상태머신(공격 판정/텔레그래프)은 종별로 달라서
/// (Won: AttackActive 상태가 없고 Recovery 진입 시 바로 투척, Heup: 아예 비공격, Gwang: 사거리
/// 대신 쿨다운 기반 트리거 + 경고 인디케이터) 여기서는 묶지 않았습니다. 대신 5종 전부에서
/// 100% 동일했던 인프라만 추출했습니다: NavMeshAgent 세팅, 시야 퍼셉션(FindPlayerInSight),
/// 넉백(IKnockbackable 구현 전체), 기본 기즈모.
///
/// EnemyBun.cs에 있던 기존 주석("지금 규모에서 상속/공유 베이스 클래스로 묶는 것보다 단순
/// 복제가 유지보수하기 더 쉽다고 판단")은 "전투 행동 트리" 로직에 대한 판단이었고, 여긴 그
/// 판단과 무관한 순수 인프라 코드라서 상충하지 않습니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public abstract class EnemyBase : MonoBehaviour, IKnockbackable
{
    [Header("감지 (13 AI 설계)")]
    [Tooltip("이 반경 안에 들어오면 플레이어를 인지합니다.")]
    public float sightRadius = 6f;

    [Tooltip("퍼셉션(플레이어 탐지)과 이동 목적지 갱신 주기입니다. 매 프레임 안 하고 이 간격으로 쉬어갑니다 — 적이 여러 마리일 때 부하를 줄이기 위함 (최적화 원칙).")]
    public float perceptionInterval = 0.2f;

    protected NavMeshAgent agent;
    protected Transform player;
    protected float perceptionTimer;
    protected bool isKnockedBack; // [[번쩍(노랑) 스킬]] 등 IKnockbackable 호출로 넉백당하는 동안 true

    // 27 전투 프레임 데이터 — 넉백 자체는 문서에 값이 없어서 임의값(0.25f), 5종 전부 동일하게 써왔음
    protected const float KnockbackDuration = 0.25f;

    /// <summary>종마다 다른 이동속도 기본값(14 밸런스 수치 시트)을 서브클래스가 제공합니다.</summary>
    protected abstract float MoveSpeed { get; }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = MoveSpeed;
    }

    /// <summary>
    /// 매 프레임이 아니라 perceptionInterval마다 플레이어를 찾습니다. 이동 목적지 갱신처럼
    /// 종별로 다른 후속 처리는 서브클래스가 OnPerceptionUpdated에서 이어서 합니다.
    /// </summary>
    protected void TickPerception()
    {
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer < perceptionInterval) return;
        perceptionTimer = 0f;

        player = FindPlayerInSight();
        OnPerceptionUpdated();
    }

    protected virtual void OnPerceptionUpdated() { }

    protected Transform FindPlayerInSight()
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
    /// IKnockbackable 구현. [[02 플레이어 시스템]] "번쩍(노랑)" 스킬 등에서 호출합니다.
    /// 5종 전부 동일한 패턴 — 넉백 중엔 agent를 끄고 직접 밀어낸 뒤 NavMesh 위로 재동기화.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (isKnockedBack) return; // 이미 넉백 중이면 중첩 무시
        StartCoroutine(KnockbackRoutine(direction.normalized, force));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        Debug.Log($"[{GetType().Name}] {name} 넉백당함! 방향: {direction}, 힘: {force}");

        isKnockedBack = true;
        OnKnockbackStart();
        bool wasAgentEnabled = agent.enabled;
        if (wasAgentEnabled) agent.enabled = false; // 켜진 채로는 매 프레임 경로 이동이 넉백 이동을 덮어씀

        float elapsed = 0f;
        while (elapsed < KnockbackDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float speed = force * (1f - elapsed / KnockbackDuration); // 선형 감쇠
            transform.position += direction * speed * dt;
            yield return null;
        }

        if (wasAgentEnabled)
        {
            agent.enabled = true;
            agent.Warp(transform.position); // 넉백으로 밀려난 위치를 NavMesh 위로 재동기화
            agent.isStopped = false;
        }

        isKnockedBack = false;
        OnKnockbackEnd();

        Debug.Log($"[{GetType().Name}] {name} 넉백 종료.");
    }

    /// <summary>넉백 "시작" 시 종별 추가 처리 훅 (예: Gwang의 경고 인디케이터 취소).</summary>
    protected virtual void OnKnockbackStart() { }

    /// <summary>
    /// 넉백 "종료" 시 종별 추가 처리 훅. 기본 구현은 아무것도 안 함.
    /// ⚠️ 기존 코드 그대로: Gwang/Bun/Won/Pyeong 4종은 넉백이 끝나면 공격 상태를 Chase/Idle로
    /// 리셋했지만(맞고도 태연히 공격을 이어가면 안 맞은 것처럼 느껴져서), Heup은 애초에 그
    /// 리셋이 없었습니다(공격 상태 자체가 없어서로 추정). 동작을 바꾸지 않으려고 리셋은 각
    /// 서브클래스가 여기 오버라이드해서 직접 하고, Heup은 오버라이드하지 않습니다.
    /// </summary>
    protected virtual void OnKnockbackEnd() { }

    /// <summary>
    /// 에디터에서 감지 범위를 눈으로 확인하기 위한 기본 기즈모입니다.
    /// 서브클래스는 override 시 base.OnDrawGizmosSelected()를 부르고 자기 범위를 추가로 그립니다.
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}
