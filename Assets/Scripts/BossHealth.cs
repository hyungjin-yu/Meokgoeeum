using UnityEngine;

/// <summary>
/// BossHealth (보스 체력)
/// [[06 보스 설계]] "포(鯆)" 전용 체력 컴포넌트입니다. [[EnemyHealth]]와 따로 만든 이유:
/// 보스는 일반 먹괴음과 달리 구슬을 안 드랍하고(별도 보상 시스템, v0.3 범위 밖) 페이즈
/// 전환 이벤트가 필요해서, 이미 검증된 EnemyHealth를 건드리는 대신 독립적으로 구현했습니다.
/// </summary>
public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("체력 (14 밸런스 수치 시트 — 1페이즈 500, 2페이즈부터 연속)")]
    public float maxHP = 500f;

    [Tooltip("이 비율 밑으로 떨어지면 2페이즈 전환 이벤트가 발생합니다.")]
    [Range(0f, 1f)]
    public float phase2Threshold = 0.5f;

    private float currentHP;
    private bool isDead;
    private bool phase2Triggered;
    private bool isInvulnerable;

    public float CurrentHP => currentHP;
    public float HpRatio => currentHP / maxHP;

    /// <summary>HP가 phase2Threshold 밑으로 떨어지는 순간 1회 발생합니다.</summary>
    public event System.Action OnPhase2;

    /// <summary>보스가 죽으면 발생합니다. (처치 연출/보상은 v0.3 범위 밖이라 훅만 열어둠)</summary>
    public event System.Action OnDeath;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        if (isInvulnerable)
        {
            Debug.Log($"[BossHealth] {name} 무적 중이라 피격 무시됨!");
            return;
        }

        currentHP -= amount;
        Debug.Log($"[BossHealth] {name} 피격! 남은 HP: {Mathf.Max(0f, currentHP)}/{maxHP} ({HpRatio:P0})");

        if (!phase2Triggered && currentHP <= maxHP * phase2Threshold)
        {
            phase2Triggered = true;
            Debug.Log("[BossHealth] 2페이즈 전환!");
            OnPhase2?.Invoke();
        }

        if (currentHP <= 0f)
            Die();
    }

    /// <summary>
    /// 무적 여부를 켜고 끕니다. [[BossPo]]가 2페이즈 진입 시 "위치 교란"이 끝날 때까지
    /// (숨바꼭질 하듯 자리를 옮기는 동안은 못 맞음) 켜두는 용도로 씁니다.
    /// [[27 전투 프레임 데이터]] "페이즈 전환 무적" 스펙을 고정 시간 대신 실제 연출
    /// 길이에 맞춰 구현한 버전입니다.
    /// </summary>
    public void SetInvulnerable(bool value)
    {
        if (isInvulnerable == value) return;
        isInvulnerable = value;
        Debug.Log(isInvulnerable ? $"[BossHealth] {name} 무적 시작!" : $"[BossHealth] {name} 무적 종료.");
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        Debug.Log($"[BossHealth] {name} 회복! 남은 HP: {currentHP}/{maxHP}");
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"[BossHealth] {name} 처치! (3페이즈/처치 연출/보상은 v0.3 범위 밖 — 다음 마일스톤)");
        OnDeath?.Invoke();

        SaveManager.Instance?.Save(); // [[18 세이브 & 로드 기획]] "보스 처치 후 자동 저장"
    }
}
