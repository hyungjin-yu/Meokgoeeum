using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyBun (먹괴음 - 분, 큐브 면 버전)
    /// 원본 [[EnemyBun]]과 전투 상태머신(윈드업/액티브/리커버리, 평과 동일 BT)은 완전히 동일하고,
    /// [[CubeEnemyPyeong]]과 사실상 같은 코드입니다 — 원본도 "평과 완전히 같은데 복제로 남겨둠"
    /// 판단을 그대로 유지했습니다. 차이는 딱 하나, 처치 시 분열입니다.
    ///
    /// [[CubeEnemyBase]]는 [[EnemyHealth]] 컴포넌트를 안 쓰고 자체 currentHP를 들고 있어서,
    /// 죽음 감지는 `OnDeath()` 훅으로 처리합니다(원본의 `health.OnDeath += ...` 대신).
    /// </summary>
    public class CubeEnemyBun : CubeEnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트 — 분(원본) 기준)")]
        public float attackPower = 7f;
        public float moveSpeed = 3f;

        [Header("분열 (13 먹괴음 AI 설계 — OnDeath: 미니언 2마리)")]
        [Tooltip("분열로 생성된 미니언인지 여부입니다. 미니언은 죽어도 다시 분열하지 않습니다.")]
        public bool isMinor;

        [Tooltip("미니언의 체력 배율입니다. (원본 HP × 이 값)")]
        public float minorHpMultiplier = 0.5f;

        [Tooltip("미니언의 크기 배율입니다.")]
        public float minorScaleMultiplier = 0.6f;

        [Header("판정 (13 AI 설계, 27 프레임 데이터)")]
        public float attackRange = 1.5f;
        public float attackHitRadius = 1f;

        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum AttackPhase { Windup, Active, Recovery }
        private AttackPhase phase;
        private float phaseTimer;

        // 27 전투 프레임 데이터 - 먹괴음 평과 동일 텔레그래프(분도 "평과 동일 BT") — 원본과 동일
        private const float WindupSeconds = 15f / 60f;
        private const float ActiveSeconds = 4f / 60f;
        private const float RecoverySeconds = 14f / 60f;

        protected override float MoveSpeed => moveSpeed;

        protected override void OnChasingTick()
        {
            if (distanceToPlayer <= attackRange)
            {
                EnterAttackWindup();
                return;
            }
            MoveToward(target.transform.position);
        }

        protected override void OnBusyTick()
        {
            phaseTimer += Time.deltaTime;
            switch (phase)
            {
                case AttackPhase.Windup:
                    if (phaseTimer >= WindupSeconds) EnterAttackActive();
                    break;
                case AttackPhase.Active:
                    if (phaseTimer >= ActiveSeconds) EnterAttackRecovery();
                    break;
                case AttackPhase.Recovery:
                    if (phaseTimer >= RecoverySeconds) EnterChasing();
                    break;
            }
        }

        private void EnterAttackWindup()
        {
            EnterBusy();
            phase = AttackPhase.Windup;
            phaseTimer = 0f;
            OnAttackWindupStart?.Invoke();
        }

        private void EnterAttackActive()
        {
            phase = AttackPhase.Active;
            phaseTimer = 0f;
            PerformAttack();
        }

        private void EnterAttackRecovery()
        {
            phase = AttackPhase.Recovery;
            phaseTimer = 0f;
        }

        private void PerformAttack()
        {
            OnAttackHit?.Invoke();

            Collider[] hits = Physics.OverlapSphere(transform.position, attackHitRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;
                var damageable = hit.GetComponent<IDamageable>();
                damageable?.TakeDamage(attackPower);
            }
        }

        /// <summary>
        /// 원본과 동일 — 이미 검증된 자기 자신을 복제해서 체력/크기만 줄이는 방식(별도 미니언
        /// 프리팹 없이). 큐브 면 버전이라 미니언도 같은 faceNormal/cubeCenter를 그대로 물려받고,
        /// 스폰 위치도 접선 평면 위에서만 흩어지도록 법선 축 성분은 제거합니다.
        /// </summary>
        protected override void OnDeath()
        {
            if (isMinor) return; // 미니언은 또 분열하지 않음 (무한 분열 방지)

            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 1f;
                int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);
                offset[axis] = 0f; // 면 접선 방향으로만 흩어짐
                Vector3 spawnPos = transform.position + offset;

                GameObject clone = Instantiate(gameObject, spawnPos, transform.rotation, transform.parent);
                clone.transform.localScale = transform.localScale * minorScaleMultiplier;

                var cloneBun = clone.GetComponent<CubeEnemyBun>();
                cloneBun.isMinor = true;
                cloneBun.maxHP = maxHP * minorHpMultiplier;
                cloneBun.currentHP = cloneBun.maxHP;

                Debug.Log($"[CubeEnemyBun] 분열! {clone.name} 생성 (미니언, HP {cloneBun.currentHP})");
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
