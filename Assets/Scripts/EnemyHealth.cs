using UnityEngine;

/// <summary>
/// EnemyHealth (적 체력)
/// IDamageable을 구현해서 BrushWeapon 등에게 맞으면 체력이 깎입니다.
/// 모든 먹괴음 타입(평/원/흡/분/광)과 보스가 공통으로 쓸 수 있도록 분리해뒀습니다.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("체력 (14 밸런스 수치 시트)")]
    public float maxHP = 20f; // 2026-08-16: 30 -> 20. 붓 3타 콤보(6+7+10=23)로 정확히 3클릭째 처치되도록 QA용 조정

    private float currentHP;
    private bool isDead;

    /// <summary>처치됐을 때 호출됩니다. (④ 색 구슬 드랍 등을 구독해서 처리하면 됩니다)</summary>
    public event System.Action<EnemyHealth> OnDeath;

    public float CurrentHP => currentHP;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHP -= amount;
        if (currentHP <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke(this);

        // TODO(④ 색 구슬 시스템): 여기서 색 구슬 드랍 처리 (현재는 그냥 제거만 함)
        Debug.Log($"{name} 정화 완료!");
        Destroy(gameObject);
    }
}
