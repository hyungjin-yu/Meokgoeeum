using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BossPo (보스 — 포/鯆)
/// [[06 보스 설계]] 1~2페이즈 구현. 6가지 색 공격을 무작위로 시전하고, HP가 50% 밑으로
/// 떨어지면 2페이즈로 전환되어 "위치 교란" 공격이 추가됩니다.
///
/// v0.3 범위 밖으로 명시적으로 뺀 것들 (다음 마일스톤 몫):
/// - 3페이즈(역방향 색 복원 파동, 스탯 상승, 구슬 소멸)
/// - 특정 신체 부위 순서 공격 시 추가 피해(퍼즐 요소) — 부위별 콜라이더/메쉬가 아직 없음
/// - 검정(먹물) 스킬 2배 대미지 약점 — 먹물 스킬 자체가 아직 미구현([[ColorSkillController]] 참고)
/// - 처치 연출(큐브 분해 컷씬), 보상(보스 구슬/목걸이), 보스 전용 BGM
///
/// 넉백에 면역입니다(IKnockbackable 미구현) — 보스는 안 밀려나는 게 일반적인 설계라 의도적으로 뺐습니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossHealth))]
public class BossPo : MonoBehaviour
{
    private enum Phase { Phase1, Phase2 }
    private enum AttackType { Strike, Line, AoE, Heal, Debuff, Zone, Displace }

    [Header("스탯 (14 밸런스 수치 시트 — 1페이즈 공격력 20, 2페이즈 25)")]
    public float phase1AttackPower = 20f;
    public float phase2AttackPower = 25f;
    public float moveSpeed = 3.5f;

    [Header("감지")]
    public float perceptionInterval = 0.2f;

    [Header("강타(빨강) 판정")]
    public float strikeDashDistance = 4f;
    public float strikeDashDuration = 0.2f;
    public float strikeHitRadius = 2f;

    [Header("흐름(파랑) 판정")]
    public float lineLength = 8f;
    public float lineWidth = 3f;

    [Header("번쩍(노랑) 판정")]
    public float aoeRadius = 5f;
    public float aoeKnockback = 10f;

    [Header("되살림(초록)")]
    public float healAmount = 30f;

    [Header("왜곡(보라)")]
    public float debuffSpeedMultiplier = 0.5f;
    public float debuffDuration = 3f;

    [Header("먹물(검정) 장판")]
    public float zoneRadius = 3f;
    public float zoneDamagePerSecond = 8f;
    public float zoneLifetime = 4f;

    [Header("페이즈 2 전용 — 위치 교란")]
    public float displaceRadius = 8f;

    [Tooltip("공격 사이 추격 대기 시간입니다.")]
    public float attackCooldown = 1.5f;

    // 텔레그래프 연출 훅 — VFX/애니메이션은 나중에 이 이벤트를 구독해서 붙이면 됩니다.
    public event System.Action OnAttackWindupStart;
    public event System.Action OnAttackHit;

    // 27 전투 프레임 데이터 - 보스 텔레그래프 20~30f 기준 (60fps 환산)
    private const float WindupSeconds = 25f / 60f;
    private const float RecoverySeconds = 20f / 60f;

    private NavMeshAgent agent;
    private BossHealth health;
    private Transform player;
    private Phase phase = Phase.Phase1;
    private bool isAttacking;
    private float perceptionTimer;
    private float attackCooldownTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        health = GetComponent<BossHealth>();
        health.OnPhase2 += () =>
        {
            phase = Phase.Phase2;
            Debug.Log("[BossPo] \"숨바꼭질 하자~ 내가 술래할게~\" — 2페이즈 돌입!");
            StartCoroutine(EnterPhase2Routine());
        };
    }

    /// <summary>
    /// 2페이즈 진입 연출: 곧바로 "위치 교란"을 강제로 시전하고, 그동안(자리를 옮기는 중)은
    /// 무적입니다. [[27 전투 프레임 데이터]] "페이즈 전환 무적"을 고정 시간 대신 실제 위치
    /// 교란 연출 길이에 맞춰 구현했습니다 — 사용자 요청으로 고정 1초 대신 이 방식 채택.
    /// </summary>
    private IEnumerator EnterPhase2Routine()
    {
        while (isAttacking) yield return null; // 진행 중인 공격이 있으면 끝날 때까지 대기

        isAttacking = true;
        agent.isStopped = true;
        health.SetInvulnerable(true);

        yield return CastDisplace();

        health.SetInvulnerable(false);
        attackCooldownTimer = attackCooldown;
        agent.isStopped = false;
        isAttacking = false;
    }

    private void Update()
    {
        // 퍼셉션 갱신 + 목적지 재계산(SetDestination)은 매 프레임 안 하고 perceptionInterval마다만
        // 합니다 (최적화 원칙 — EnemyPyeong 등과 동일한 이유. 보스는 1마리뿐이라 비용이 크진
        // 않지만, 프로젝트 전체 관례를 그대로 따랐습니다).
        perceptionTimer += Time.deltaTime;
        if (perceptionTimer >= perceptionInterval)
        {
            perceptionTimer = 0f;
            UpdatePerceptionAndDestination();
        }

        if (isAttacking) return; // 공격 코루틴이 흐름을 전담
        if (player == null) return;

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        StartCoroutine(PerformRandomAttack());
    }

    private void UpdatePerceptionAndDestination()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            player = found != null ? found.transform : null;
        }

        if (isAttacking || player == null)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    private IEnumerator PerformRandomAttack()
    {
        isAttacking = true;
        agent.isStopped = true;

        AttackType type = ChooseAttack();
        OnAttackWindupStart?.Invoke();
        yield return new WaitForSeconds(WindupSeconds);

        switch (type)
        {
            case AttackType.Strike: yield return CastStrike(); break;
            case AttackType.Line: CastLine(); break;
            case AttackType.AoE: CastAoE(); break;
            case AttackType.Heal: CastHeal(); break;
            case AttackType.Debuff: CastDebuff(); break;
            case AttackType.Zone: CastZone(); break;
            case AttackType.Displace: yield return CastDisplace(); break;
        }

        yield return new WaitForSeconds(RecoverySeconds);

        attackCooldownTimer = attackCooldown;
        agent.isStopped = false;
        isAttacking = false;
    }

    private AttackType ChooseAttack()
    {
        var pool = new List<AttackType>
        {
            AttackType.Strike, AttackType.Line, AttackType.AoE,
            AttackType.Heal, AttackType.Debuff, AttackType.Zone,
        };
        if (phase == Phase.Phase2) pool.Add(AttackType.Displace); // "숨바꼭질" 위치 교란은 2페이즈부터

        return pool[Random.Range(0, pool.Count)];
    }

    private float CurrentAttackPower => phase == Phase.Phase1 ? phase1AttackPower : phase2AttackPower;

    /// <summary>강타(빨강): 전방 대시 후 강력한 일격. [[ColorSkillController]] 강타와 같은 패턴.</summary>
    private IEnumerator CastStrike()
    {
        Debug.Log("[BossPo] 강타(빨강) 시전!");
        if (player == null) yield break;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        float speed = strikeDashDistance / strikeDashDuration;
        float elapsed = 0f;
        while (elapsed < strikeDashDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            transform.position += direction * speed * dt;
            yield return null;
        }

        OnAttackHit?.Invoke();
        DamageAllInRadius(transform.position, strikeHitRadius, CurrentAttackPower);
    }

    /// <summary>흐름(파랑): 전방 직선 관통 공격.</summary>
    private void CastLine()
    {
        Debug.Log("[BossPo] 흐름(파랑) 시전!");
        if (player != null) FaceTarget(player.position);

        OnAttackHit?.Invoke();
        Vector3 center = transform.position + transform.forward * (lineLength / 2f);
        Vector3 halfExtents = new Vector3(lineWidth / 2f, 2f, lineLength / 2f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation);
        DamageAll(hits, CurrentAttackPower);
    }

    /// <summary>번쩍(노랑): 주변 범위 전체 공격 + 넉백.</summary>
    private void CastAoE()
    {
        Debug.Log("[BossPo] 번쩍(노랑) 시전!");
        OnAttackHit?.Invoke();

        Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;
            damageable.TakeDamage(CurrentAttackPower);

            var knockbackable = hit.GetComponent<IKnockbackable>();
            if (knockbackable == null) continue;

            Vector3 dir = hit.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            knockbackable.ApplyKnockback(dir.normalized, aoeKnockback);
        }
    }

    /// <summary>초록: 자가 회복.</summary>
    private void CastHeal()
    {
        Debug.Log("[BossPo] 되살림(초록) 시전!");
        health.Heal(healAmount);
    }

    /// <summary>보라: 플레이어에게 슬로우 디버프.</summary>
    private void CastDebuff()
    {
        Debug.Log("[BossPo] 왜곡(보라) 시전!");
        if (player == null) return;

        var pc = player.GetComponent<PlayerController>();
        pc?.ApplySlow(debuffSpeedMultiplier, debuffDuration);
    }

    /// <summary>검정: 플레이어 위치에 장판 생성.</summary>
    private void CastZone()
    {
        Debug.Log("[BossPo] 먹물(검정) 시전!");
        Vector3 spawnPos = player != null ? player.position : transform.position;
        DamageZone.Create(spawnPos, zoneRadius, zoneDamagePerSecond, zoneLifetime);
    }

    /// <summary>
    /// 2페이즈 전용: [[06 보스 설계]] "플레이어 위치가 강제로 이동되는 교란 패턴". 페이드 아웃/인은
    /// 재사용하지만, 큐브 6면 순환([[CubeMapManager]])은 건드리지 않고 보스룸 안에서 순간이동만
    /// 시킵니다 — 보스룸은 6면 순환 밖의 별도 씬이라 실제 면 회전을 발동하면 상태가 꼬입니다.
    /// </summary>
    private IEnumerator CastDisplace()
    {
        Debug.Log("[BossPo] \"숨바꼭질~\" 위치 교란 시전!");
        if (player == null) yield break;

        // 2026-08-21: CubeMapManager/CubeRevealSequence와 같은 이유 — 화면이 페이드로
        // 가려진 동안에도 다른 공격 판정(장판 등)은 그대로 실행돼서 안 보이는 채로 맞을 수 있음.
        var playerHealth = player.GetComponent<PlayerHealth>();
        playerHealth?.SetInvulnerable(true);

        if (FadeManager.Instance != null) yield return FadeManager.Instance.FadeOut();

        Vector2 randomCircle = Random.insideUnitCircle * displaceRadius;
        Vector3 newPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = newPos;
        if (cc != null) cc.enabled = true;

        if (FadeManager.Instance != null) yield return FadeManager.Instance.FadeIn();

        playerHealth?.SetInvulnerable(false);
    }

    private void DamageAllInRadius(Vector3 center, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        DamageAll(hits, damage);
    }

    private void DamageAll(Collider[] hits, float damage)
    {
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            var damageable = hit.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
        }
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, aoeRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * strikeDashDistance, strikeHitRadius);
    }
}
