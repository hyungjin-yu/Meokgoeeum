using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// EnemyHeup (먹괴음 - 흡, 흡수형)
    /// **비공격 유닛입니다** — [[27 전투 프레임 데이터]] "텔레그래프 불필요(비공격 유닛)" 명시대로
    /// 플레이어를 직접 때리진 않습니다. HP가 절반 밑으로 떨어지면 플레이어에게 달라붙어 보유
    /// 색 구슬을 빼앗아 회복하고, 그렇지 않을 때는 플레이어 쪽으로 다가만 옵니다.
    ///
    /// ⚠️ 2026-09-16 재설계 — 원래는 "가장 가까운 색 복원 구역([[RestoredAreaRegistry]])을
    /// 찾아가 흡수"하는 방식이었는데, 사용자 요청으로 "플레이어한테 붙어서 플레이어의 색을
    /// 흡수하는 느낌"으로 바꿨습니다 — 회복 대상이 고정된 환경 구역이 아니라 플레이어 자신이
    /// 됨. [[ColorSystemManager.TryDrainRandomOrb]]로 보유 구슬을 하나 빼앗고, 그게 성공했을
    /// 때만 그만큼 회복합니다(플레이어에게 구슬이 하나도 없으면 빼앗을 것도 회복할 것도 없음 —
    /// "색을 빼앗아 회복한다"는 새 설정을 기계적으로도 일관되게 유지).
    ///
    /// [[13 먹괴음 AI 설계]] BT_Enemy_Heup의 골격(HP&lt;50% → 흡수, 아니면 MoveToPlayer)은 그대로
    /// 두고 "흡수 대상"만 바꾼 것 — 단순화: 원안은 색마다 회복 폭이 다르지만(빨강 크게, 보라
    /// 작게), 지금은 어떤 색을 빼앗든 회복량 동일로 단순화했습니다.
    ///
    /// 2026-08-20: HP 50% 이상일 때 공격도 하도록 확장했다가, 실제로 공격이 들어오는지 체감이
    /// 잘 안 된다는 피드백으로 **다시 원래대로 비공격 유닛으로 되돌림**. 회복 히스테리시스(한 번
    /// 시작하면 100%까지 계속 회복)만 남기고 공격 관련 코드는 제거했습니다.
    ///
    /// 시야 퍼셉션/넉백/에이전트 세팅은 [[EnemyBase]] 공통 구현을 씁니다. 다른 4종과 달리 넉백
    /// 종료 시 상태 리셋을 하지 않는데, 이건 기존 동작을 그대로 보존한 것입니다(EnemyBase의
    /// OnKnockbackEnd 기본 주석 참고) — 애초에 공격 상태가 없어서 리셋할 게 없었던 것으로 추정.
    /// </summary>
    public class EnemyHeup : EnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트)")]
        public float moveSpeed = 2.5f;

        [Header("흡수 (13 먹괴음 AI 설계, 2026-09-16 대상을 플레이어로 재설계)")]
        [Tooltip("이 비율 밑으로 HP가 떨어지면 플레이어에게 붙어 흡수하러 갑니다.")]
        [Range(0f, 1f)]
        public float lowHpThreshold = 0.5f;

        [Tooltip("플레이어와 이만큼 가까워지면 달라붙어 흡수(회복)를 시작합니다.")]
        public float absorbRadius = 1.5f;

        [Tooltip("초당 흡수 시도 횟수입니다. 한 번 흡수에 성공할 때마다(플레이어에게 구슬이 있을 때만) 1만큼 회복합니다.")]
        public float healPerSecond = 5f;

        /// <summary>
        /// 플레이어에게 달라붙어 흡수를 막 시작한 순간(딱 한 번) 발동합니다.
        /// 2026-08-20: 4층 [[WallExplosionHazard]]가 구독해서 "제때 못 끊으면" 페널티를 겁니다 —
        /// 2026-09-16 재설계로 트리거가 "환경 구역 도달"에서 "플레이어에게 달라붙음"으로 바뀜.
        /// </summary>
        public event System.Action OnAbsorbStart;

        private enum State { Idle, Chase, SeekPlayerToAbsorb, Absorbing }
        private State state = State.Idle;
        private bool isHealingCommitted; // 한 번 회복 시작하면 100% 찰 때까지 true

        private EnemyHealth health;

        /// <summary>
        /// 2026-09-16 추가 — [[MonsterPaintParts]](부위 색칠 처치 시스템)가 붙어있으면 숫자
        /// 체력 대신 그쪽 기준으로 저체력/회복 판정을 합니다. 없으면(아직 이 시스템을 안 붙인
        /// 다른 상황) 기존 숫자 체력 그대로 동작.
        ///
        /// ⚠️ 2026-09-16 발견 — paintParts가 붙어있는데도 이 판정을 `health.CurrentHP`/
        /// `health.maxHP`(숫자)로 그대로 했더니, `EnemyHealth.CurrentHP`는 이미 "남은 부위 수"로
        /// 재해석돼있지만 `maxHP`는 그대로 20 같은 숫자라서 단위가 안 맞아 "저체력" 판정이
        /// 스폰 직후부터 영원히 참으로 고정되는 버그가 있었음(사용자가 "몹이 다 똑같은데?"로
        /// 리포트한 것과 별개로 발견 — 흡이 플레이어를 아예 안 쫓아오고 계속 회복 구역만
        /// 찾아다니는 증상으로 나타남). `MonsterPaintParts.HealthFraction`(0~1, 같은 체계 안에서
        /// 일관된 비율)을 쓰도록 고침.
        /// </summary>
        private MonsterPaintParts paintParts;

        private float healPartTimer; // "다음 흡수 시도까지" 누적 시간

        protected override float MoveSpeed => moveSpeed;

        protected override void Awake()
        {
            base.Awake();
            health = GetComponent<EnemyHealth>();
            paintParts = GetComponent<MonsterPaintParts>();
        }

        /// <summary>0(전부 칠해짐/체력없음)~1(멀쩡함) 사이의 정규화된 체력 비율입니다.</summary>
        private float HealthFraction() => paintParts != null
            ? paintParts.HealthFraction
            : (health.maxHP > 0f ? health.CurrentHP / health.maxHP : 1f);

        private void Update()
        {
            if (isKnockedBack) return;

            // 퍼셉션 갱신 + 목적지 재계산(SetDestination)은 매 프레임 안 하고 perceptionInterval마다만
            // 합니다 (최적화 원칙 — EnemyPyeong/EnemyWon과 동일한 이유).
            TickPerception();

            if (state != State.Absorbing) return;

            // 플레이어가 흡수 도중 멀어지면 즉시 끊깁니다 — 고정된 환경 구역과 달리 플레이어는
            // 움직이므로 매 프레임 거리 재확인이 필요합니다.
            if (player == null || Vector3.Distance(transform.position, player.position) > absorbRadius)
            {
                state = State.SeekPlayerToAbsorb;
                agent.isStopped = false;
                animator?.SetBool("Special", false);
                return;
            }

            healPartTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.01f, healPerSecond);
            while (healPartTimer >= interval)
            {
                healPartTimer -= interval;
                TryAbsorbFromPlayer();
            }
        }

        /// <summary>
        /// 플레이어의 보유 색 구슬을 하나 빼앗아 그만큼 회복합니다. 플레이어에게 구슬이 하나도
        /// 없으면 이번 시도는 그냥 아무 일도 안 합니다(빼앗을 색이 없으니 회복도 없음 — 새
        /// "색을 빼앗아 회복한다"는 설정을 기계적으로도 일관되게 유지).
        /// </summary>
        private void TryAbsorbFromPlayer()
        {
            if (ColorSystemManager.Instance == null || !ColorSystemManager.Instance.TryDrainRandomOrb(out _))
                return;

            if (paintParts != null) paintParts.HealRandomPart();
            else health.Heal(1f);
        }

        protected override void OnPerceptionUpdated()
        {
            UpdateDecision();
        }

        /// <summary>
        /// BT_Enemy_Heup의 Selector: HP 낮으면 플레이어에게 붙어 흡수, 아니면 플레이어 쪽으로
        /// 이동만.
        ///
        /// 2026-08-20: [[13 먹괴음 AI 설계]] 원안은 "HP&lt;50%" 조건을 매 틱 재검사하는 순수
        /// Selector라서, 회복 중 HP가 50%를 살짝 넘는 순간 곧바로 멈춰버리는 문제가 있었습니다
        /// (사용자 피드백: "왜 절반까지만 회복해?"). 그래서 히스테리시스를 추가했습니다 —
        /// 한 번 회복이 시작되면(`isHealingCommitted`) HP가 완전히 꽉 찰 때까지는 멈추지 않습니다.
        /// 트리거 조건(50% 밑에서 시작)은 기획서 그대로 유지, "언제 멈추는지"만 다르게 해석.
        /// </summary>
        private void UpdateDecision()
        {
            bool isLowHp = HealthFraction() < lowHpThreshold;
            if (isLowHp) isHealingCommitted = true;

            if (isHealingCommitted)
            {
                if (HealthFraction() >= 1f)
                {
                    isHealingCommitted = false; // 완전히 다 찼으면 회복 종료, 정상 행동으로 복귀
                    animator?.SetBool("Special", false); // [[changelog/2026-09-10_먹괴음5종-애니메이터컨트롤러]]
                }
                else
                {
                    UpdateSeekPlayerToAbsorb();
                    return;
                }
            }

            state = player != null ? State.Chase : State.Idle;

            if (state == State.Chase)
                agent.SetDestination(player.position);
        }

        private void UpdateSeekPlayerToAbsorb()
        {
            if (player == null)
            {
                state = State.Idle;
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer <= absorbRadius)
            {
                if (state != State.Absorbing) // 상태 전이 시점에만 1회 발동 (매 퍼셉션 틱마다 X)
                {
                    state = State.Absorbing;
                    agent.isStopped = true;
                    animator?.SetBool("Special", true); // [[changelog/2026-09-10_먹괴음5종-애니메이터컨트롤러]]
                    OnAbsorbStart?.Invoke();
                }
            }
            else
            {
                state = State.SeekPlayerToAbsorb;
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, absorbRadius);
        }
    }
}
