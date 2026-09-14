using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyPyeong (먹괴음 - 평, 큐브 면 버전)
    /// 원본 [[EnemyPyeong]](NavMeshAgent 기반, 평지 던전용)과 전투 상태머신(Windup/Active/
    /// Recovery, 타이밍 수치)은 완전히 동일합니다 — 이동만 [[CubeEnemyBase]]의 직선+clamp
    /// 방식으로 갈아끼운 것입니다. [[changelog/2026-09-14_NavMesh-면단위-장애물회피검증]] 참고.
    /// </summary>
    public class CubeEnemyPyeong : CubeEnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트)")]
        public float attackPower = 8f;
        public float moveSpeed = 3f;

        [Header("판정 (13 AI 설계, 27 프레임 데이터)")]
        [Tooltip("이 거리 이하면 추격을 멈추고 공격을 시작합니다.")]
        public float attackRange = 1.5f;

        [Tooltip("공격이 실제로 맞는 판정 반경입니다.")]
        public float attackHitRadius = 1f;

        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum AttackPhase { Windup, Active, Recovery }
        private AttackPhase phase;
        private float phaseTimer;

        // 27 전투 프레임 데이터 - 먹괴음 평 (60fps 기준 초 단위 환산) — 원본과 동일
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

        /// <summary>Active 프레임 진입 시 1회만 판정합니다(다단히트 방지 — 원본과 동일).</summary>
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
