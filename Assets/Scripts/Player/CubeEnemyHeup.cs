using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyHeup (먹괴음 - 흡, 큐브 면 버전)
    /// 원본 [[EnemyHeup]]과 동일하게 **비공격 유닛**입니다. HP가 절반 밑으로 떨어지면 플레이어에게
    /// 달라붙어 보유 색 구슬을 빼앗아 회복하고, 그렇지 않을 땐 플레이어 쪽으로 다가만 옵니다.
    ///
    /// ⚠️ 2026-09-16 재설계 — 원래는 "가장 가까운 색 복원 구역([[RestoredAreaRegistry]])을 찾아가
    /// 흡수"하는 방식이었는데, 사용자 요청으로 "플레이어한테 붙어서 플레이어의 색을 흡수하는
    /// 느낌"으로 바꿨습니다. 원본 [[EnemyHeup]]과 동일한 이유·동일한 구현 방식.
    ///
    /// 원본의 "회복 히스테리시스"(한 번 시작하면 100%까지 계속 회복)도 그대로 가져왔습니다.
    /// [[CubeEnemyBase]]의 Busy 상태를 "흡수 중(제자리 정지 + 회복)"으로 씁니다 — 원래 원본에서도
    /// Absorbing 상태에서 agent.isStopped=true였던 것과 같은 발상입니다.
    /// </summary>
    public class CubeEnemyHeup : CubeEnemyBase
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
        /// 2026-09-16 재설계로 트리거가 "환경 구역 도달"에서 "플레이어에게 달라붙음"으로 바뀜.
        /// </summary>
        public event System.Action OnAbsorbStart;

        private bool isHealingCommitted; // 한 번 회복 시작하면 100% 찰 때까지 true
        private float healPartTimer; // "다음 흡수 시도까지" 누적 시간

        /// <summary>
        /// 2026-09-16 추가 — [[MonsterPaintParts]]가 붙어있으면 숫자 체력(currentHP/maxHP) 대신
        /// 그쪽 기준으로 저체력/회복 판정을 합니다.
        ///
        /// ⚠️ 2026-09-16 발견 — `CubeEnemyBase.TakeDamage()`는 paintParts가 있으면 currentHP를
        /// 아예 안 건드리고 리턴하는데, 여기 저체력 판정은 그 currentHP를 그대로 읽고 있어서
        /// **currentHP가 영원히 maxHP와 같은 채로 고정 → "저체력" 조건이 평생 안 걸림 → 회복
        /// 행동 자체가 완전히 사라지는 버그**가 있었음(사용자 리포트 "3층까지 왔는데 몹이 다
        /// 똑같은데?"의 원인 중 하나로 추정 — 흡이 그냥 평범하게 쫓아오기만 하는 몹이 돼버림).
        /// `MonsterPaintParts.HealthFraction`을 쓰도록 고침.
        /// </summary>
        private MonsterPaintParts paintParts;

        protected override float MoveSpeed => moveSpeed;

        protected override void Start()
        {
            base.Start();
            paintParts = GetComponent<MonsterPaintParts>();
        }

        private float HealthFraction() => paintParts != null
            ? paintParts.HealthFraction
            : (maxHP > 0f ? currentHP / maxHP : 1f);

        /// <summary>
        /// 원본 BT_Enemy_Heup의 Selector: HP 낮으면 플레이어에게 붙어 흡수, 아니면 플레이어 쪽으로
        /// 이동만(공격 없음). 원본과 동일하게 히스테리시스 적용 — 트리거는 50% 밑, 해제는 100%.
        /// </summary>
        protected override void OnChasingTick()
        {
            bool isLowHp = HealthFraction() < lowHpThreshold;
            if (isLowHp) isHealingCommitted = true;

            if (isHealingCommitted)
            {
                if (HealthFraction() >= 1f)
                {
                    isHealingCommitted = false; // 완전히 다 찼으면 회복 종료, 정상 행동으로 복귀
                }
                else
                {
                    SeekPlayerToAbsorb();
                    return;
                }
            }

            // 흡은 공격 판정이 없어서 사거리로 자연히 멈추질 않으므로, minApproachToPlayer로
            // 플레이어에게 완전히 파고들지 않게 막습니다([[changelog/2026-09-14_몹겹침-최소거리분리]]).
            MoveToward(target.transform.position, arriveThreshold: minApproachToPlayer);
        }

        private void SeekPlayerToAbsorb()
        {
            float distToPlayer = Vector3.Distance(transform.position, target.transform.position);
            if (distToPlayer <= absorbRadius)
            {
                // OnChasingTick은 Busy로 전환되면 더는 안 불리므로, 이 줄은 실제로 "막 도달한
                // 순간"에만 한 번 실행됩니다 — 별도 wasAbsorbing 플래그 없이도 1회 발동 보장.
                OnAbsorbStart?.Invoke();
                EnterBusy(); // 제자리 정지 — OnBusyTick에서 흡수/회복
            }
            else
            {
                MoveToward(target.transform.position);
            }
        }

        /// <summary>흡수 중(Busy) 매 프레임 실행. 플레이어가 멀어지면 즉시 추격으로 복귀합니다.</summary>
        protected override void OnBusyTick()
        {
            if (Vector3.Distance(transform.position, target.transform.position) > absorbRadius)
            {
                EnterChasing(); // 다음 OnChasingTick에서 저체력이면 다시 SeekPlayerToAbsorb로 재판정
                return;
            }

            healPartTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.01f, healPerSecond);
            while (healPartTimer >= interval)
            {
                healPartTimer -= interval;
                TryAbsorbFromPlayer();
            }

            if (HealthFraction() >= 1f)
            {
                isHealingCommitted = false;
                EnterChasing();
            }
        }

        /// <summary>
        /// 플레이어의 보유 색 구슬을 하나 빼앗아 그만큼 회복합니다. 플레이어에게 구슬이 하나도
        /// 없으면 이번 시도는 그냥 아무 일도 안 합니다(빼앗을 색이 없으니 회복도 없음).
        /// </summary>
        private void TryAbsorbFromPlayer()
        {
            if (ColorSystemManager.Instance == null || !ColorSystemManager.Instance.TryDrainRandomOrb(out _))
                return;

            if (paintParts != null) paintParts.HealRandomPart();
            else Heal(1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, absorbRadius);
        }
    }
}
