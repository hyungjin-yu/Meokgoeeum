using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyHeup (먹괴음 - 흡, 큐브 면 버전)
    /// 원본 [[EnemyHeup]]과 동일하게 **비공격 유닛**입니다. HP가 절반 밑으로 떨어지면 가장 가까운
    /// 색 복원 구역([[RestoredAreaRegistry]] — 월드 좌표 기반이라 큐브 면이든 평지든 그대로 재사용
    /// 가능)을 찾아가 흡수해서 회복하고, 그렇지 않을 땐 플레이어 쪽으로 다가만 옵니다.
    ///
    /// 원본의 "회복 히스테리시스"(한 번 시작하면 100%까지 계속 회복)도 그대로 가져왔습니다.
    /// [[CubeEnemyBase]]의 Busy 상태를 "흡수 중(제자리 정지 + 회복)"으로 씁니다 — 원래 원본에서도
    /// Absorbing 상태에서 agent.isStopped=true였던 것과 같은 발상입니다.
    /// </summary>
    public class CubeEnemyHeup : CubeEnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트)")]
        public float moveSpeed = 2.5f;

        [Header("흡수 (13 먹괴음 AI 설계)")]
        [Tooltip("이 비율 밑으로 HP가 떨어지면 색 복원 구역을 찾아 회복하러 갑니다.")]
        [Range(0f, 1f)]
        public float lowHpThreshold = 0.5f;

        [Tooltip("구역에 이만큼 가까워지면 흡수(회복)를 시작합니다.")]
        public float absorbRadius = 1.5f;

        [Tooltip("초당 회복량입니다(흡수 중에 한해). CubeEnemyBase.returnRegenPerSecond와는 별도입니다.")]
        public float healPerSecond = 5f;

        /// <summary>회복 구역에 도달해서 흡수를 막 시작한 순간(딱 한 번) 발동합니다.</summary>
        public event System.Action OnAbsorbStart;

        private bool isHealingCommitted; // 한 번 회복 시작하면 100% 찰 때까지 true
        private Vector3 healTargetPos;
        private bool hasHealTarget;
        private bool wasAbsorbing;

        protected override float MoveSpeed => moveSpeed;

        /// <summary>
        /// 원본 BT_Enemy_Heup의 Selector: HP 낮으면 회복 구역 탐색/흡수, 아니면 플레이어 쪽으로
        /// 이동만(공격 없음). 원본과 동일하게 히스테리시스 적용 — 트리거는 50% 밑, 해제는 100%.
        /// </summary>
        protected override void OnChasingTick()
        {
            bool isLowHp = currentHP < maxHP * lowHpThreshold;
            if (isLowHp) isHealingCommitted = true;

            if (isHealingCommitted)
            {
                if (currentHP >= maxHP)
                {
                    isHealingCommitted = false; // 완전히 다 찼으면 회복 종료, 정상 행동으로 복귀
                }
                else
                {
                    UpdateSeekHealArea();
                    return;
                }
            }

            hasHealTarget = false; // 회복 완전히 끝났으면 다음에 다시 낮아졌을 때 새로 탐색
            // 흡은 공격 판정이 없어서 사거리로 자연히 멈추질 않으므로, minApproachToPlayer로
            // 플레이어에게 완전히 파고들지 않게 막습니다([[changelog/2026-09-14_몹겹침-최소거리분리]]).
            MoveToward(target.transform.position, arriveThreshold: minApproachToPlayer);
        }

        private void UpdateSeekHealArea()
        {
            if (!hasHealTarget)
            {
                hasHealTarget = RestoredAreaRegistry.TryFindNearest(transform.position, out healTargetPos);
                if (!hasHealTarget) return; // 등록된 구역이 하나도 없으면 할 수 있는 게 없어서 그냥 대기
            }

            float distToHealArea = Vector3.Distance(transform.position, healTargetPos);
            if (distToHealArea <= absorbRadius)
            {
                if (!wasAbsorbing) OnAbsorbStart?.Invoke(); // 상태 전이 시점에만 1회 발동
                wasAbsorbing = true;
                EnterBusy(); // 제자리 정지 — OnBusyTick에서 회복
            }
            else
            {
                wasAbsorbing = false;
                MoveToward(healTargetPos);
            }
        }

        /// <summary>흡수 중(Busy) 매 프레임 회복. 다 차거나 회복 목표를 잃으면 다시 추격으로.</summary>
        protected override void OnBusyTick()
        {
            Heal(healPerSecond * Time.deltaTime);
            if (currentHP >= maxHP)
            {
                isHealingCommitted = false;
                hasHealTarget = false;
                wasAbsorbing = false;
                EnterChasing();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, absorbRadius);
        }
    }
}
