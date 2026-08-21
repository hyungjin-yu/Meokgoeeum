using UnityEngine;

/// <summary>
/// PlayerHealth (플레이어 체력)
/// 먹괴음의 공격이 실제로 의미가 있으려면 플레이어도 맞을 대상이 있어야 해서
/// ③ 먹괴음 구현과 함께 최소한으로 추가했습니다.
///
/// 2026-08-20: [[GameOverManager]] 추가하면서 `OnDeath` 이벤트 연결 — [[17 게임오버 & 리트라이]]
/// 최소 버전(원 기획의 "처음부터 재시작" 대신 지금은 "죽은 층에서 다시 시작"으로 구현. 이유는
/// GameOverManager 주석 참고).
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력 (14 밸런스 수치 시트)")]
    public float maxHP = 100f;

    private float currentHP;
    private bool isInvulnerable; // [[PlayerDodge]]의 구르기 무적 프레임(i-frame) 동안 true
    private bool isDead;

    /// <summary>HP가 0이 됐을 때 딱 한 번 발동합니다. [[GameOverManager]]가 구독합니다.</summary>
    public event System.Action OnDeath;

    public float CurrentHP => currentHP;

    private void Awake()
    {
        currentHP = maxHP;
    }

    /// <summary>
    /// 체력을 최대치로 되돌리고 사망 상태를 해제합니다. [[GameOverManager]]가 리스폰 직후 호출합니다.
    /// </summary>
    public void ResetHealth()
    {
        currentHP = maxHP;
        isDead = false;
    }

    /// <summary>
    /// 무적 여부를 켜고 끕니다. [[PlayerDodge]]가 구르기 i-frame 구간에서 호출합니다.
    /// </summary>
    public void SetInvulnerable(bool value)
    {
        if (isInvulnerable == value) return;
        isInvulnerable = value;
        Debug.Log(isInvulnerable ? "플레이어 무적 시작! (구르기 i-frame)" : "플레이어 무적 종료.");
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return; // 게임 오버 처리 중 추가 피격 무시 (중복 OnDeath 방지)

        if (isInvulnerable)
        {
            Debug.Log("플레이어 무적 중이라 피격 무시됨!");
            return;
        }

        currentHP = Mathf.Max(0f, currentHP - amount);
        // 2026-08-19: 피격마다 찍히는 로그가 다른 디버깅(EncounterSpawner 등) 콘솔을 뒤덮어서 제거.

        if (currentHP <= 0f)
        {
            isDead = true;
            Debug.Log("[PlayerHealth] 플레이어 사망!");
            OnDeath?.Invoke();
        }
    }
}
