using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// BossHealth (보스 체력)
    /// [[06 보스 설계]] "포(鯆)" 전용 체력 컴포넌트입니다. [[EnemyHealth]]와 따로 만든 이유:
    /// 보스는 일반 먹괴음과 달리 구슬을 안 드랍하고(별도 보상 시스템, v0.3 범위 밖) 페이즈
    /// 전환 이벤트가 필요해서, 이미 검증된 EnemyHealth를 건드리는 대신 독립적으로 구현했습니다.
    /// </summary>
    public class BossHealth : MonoBehaviour, IDamageable
    {
        public static BossHealth Instance { get; private set; }

        [Header("체력 (14 밸런스 수치 시트 — 1페이즈 500, 2페이즈부터 연속)")]
        public float maxHP = 500f;

        [Tooltip("이 비율 밑으로 떨어지면 2페이즈 전환 이벤트가 발생합니다.")]
        [Range(0f, 1f)]
        public float phase2Threshold = 0.5f;

        /// <summary>
        /// [[14 밸런스 수치 시트]] "보스 DPS 역산 검증"의 순수 콤보 DPS(붓 3타 풀콤보 23댐 ÷ 1.22초).
        /// [[BrushWeapon]]의 attackPower(10) × DamageMultiplier(0.6/0.7/1.0 = 23) ÷ 프레임 합산(73f/60=1.22초)과
        /// 정확히 일치하는 값 — 코드가 바뀌면 이 상수도 같이 갱신해야 아래 실측 로그가 의미 있음.
        /// </summary>
        private const float RawComboDpsAssumption = 18.9f;

        private float currentHP;
        private bool isDead;

        /// <summary>죽었는지 여부입니다. 이 스크립트는 처치 연출(v0.3 범위 밖) 예정이라 죽어도 오브젝트를
        /// Destroy하지 않고 남겨두는데, [[LockOnController]]가 시체를 계속 타겟팅하지 않게 이걸로 걸러냅니다.</summary>
        public bool IsDead => isDead;

        private bool phase2Triggered;
        private bool isInvulnerable;
        private float fightStartTime = -1f; // 첫 피격 시각 — [[30 플레이테스트 & 밸런싱 검증 계획]] "실효 교전 비율 40%" 가정 실측용

        public float CurrentHP => currentHP;
        public float HpRatio => currentHP / maxHP;

        /// <summary>HP가 phase2Threshold 밑으로 떨어지는 순간 1회 발생합니다.</summary>
        public event System.Action OnPhase2;

        /// <summary>보스가 죽으면 발생합니다. (처치 연출/보상은 v0.3 범위 밖이라 훅만 열어둠)</summary>
        public event System.Action OnDeath;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[BossHealth] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

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

            if (fightStartTime < 0f)
            {
                fightStartTime = Time.time;
                Debug.Log($"[BossHealth] {name} 전투 시작(첫 피격) — 실효 DPS 실측 타이머 시작.");
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
            LogDpsMeasurement();
            OnDeath?.Invoke();

            SaveManager.Instance?.Save(); // [[18 세이브 & 로드 기획]] "보스 처치 후 자동 저장"
        }

        /// <summary>
        /// [[14 밸런스 수치 시트]] "보스 DPS 역산 검증"의 "실효 교전 비율 40%" 가정을 실측으로 검증합니다.
        /// 첫 피격~처치까지 걸린 실제 시간으로 실효 DPS를 역산하고, 이론상 순수 콤보 DPS(18.9/초) 대비
        /// 비율을 구해 문서가 가정한 40%와 비교합니다. 스킬 사용은 포함된 실측치라 문서의 "66초는
        /// 상한에 가까운 보수적 추정치" 서술과 자연스럽게 비교 가능.
        /// </summary>
        private void LogDpsMeasurement()
        {
            if (fightStartTime < 0f)
            {
                Debug.LogWarning("[BossHealth] DPS 실측 실패 — 첫 피격 시각이 기록 안 됨(전투 시작 전에 즉사시켰거나 디버그 커맨드 등).");
                return;
            }

            float elapsed = Time.time - fightStartTime;
            if (elapsed <= 0f) return;

            float measuredDps = maxHP / elapsed;
            float impliedEngagementRatio = measuredDps / RawComboDpsAssumption;

            Debug.Log(
                $"[BossHealth] === 보스 DPS 실측 결과 (14 밸런스 수치 시트 '보스 DPS 역산 검증' 대응) ===\n" +
                $"  전투 시간: {elapsed:F1}초 (문서 목표: 60~90초)\n" +
                $"  실측 실효 DPS: {measuredDps:F1}/초 (문서 가정: 7.6/초)\n" +
                $"  역산된 실효 교전 비율: {impliedEngagementRatio:P0} (문서 가정: 40%)\n" +
                $"  → 이 비율이 40%와 크게 다르면 [[14 밸런스 수치 시트]]의 가정을 이 실측값으로 갱신할 것."
            );
        }
    }
}
