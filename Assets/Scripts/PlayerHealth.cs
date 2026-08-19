using UnityEngine;

/// <summary>
/// PlayerHealth (플레이어 체력)
/// 먹괴음의 공격이 실제로 의미가 있으려면 플레이어도 맞을 대상이 있어야 해서
/// ③ 먹괴음 구현과 함께 최소한으로 추가했습니다.
///
/// 게임오버 처리는 아직 없습니다 — 그건 [[17 게임오버 & 리트라이]] 몫이라
/// 지금은 로그만 남기고 죽지 않게 해뒀습니다 (HP가 0 밑으로 안 내려감).
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력 (14 밸런스 수치 시트)")]
    public float maxHP = 100f;

    private float currentHP;
    private bool isInvulnerable; // [[PlayerDodge]]의 구르기 무적 프레임(i-frame) 동안 true

    public float CurrentHP => currentHP;

    private void Awake()
    {
        currentHP = maxHP;
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
        if (isInvulnerable)
        {
            Debug.Log("플레이어 무적 중이라 피격 무시됨!");
            return;
        }

        currentHP = Mathf.Max(0f, currentHP - amount);
        // 2026-08-19: 피격마다 찍히는 로그가 다른 디버깅(EncounterSpawner 등) 콘솔을 뒤덮어서 제거.

        // TODO(17 게임오버 & 리트라이): currentHP <= 0일 때 게임오버 처리
    }
}
