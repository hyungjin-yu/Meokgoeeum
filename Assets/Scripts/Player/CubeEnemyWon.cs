using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyWon (먹괴음 - 원, 큐브 면 버전)
    /// 원본 [[EnemyWon]]과 전투 타이밍·판정 반경·투사체 로직은 완전히 동일합니다. 이동만
    /// [[CubeEnemyBase]]의 직선+clamp 방식으로 교체했습니다.
    ///
    /// 원본은 "추격/후퇴/투척"을 각각 별도 top-level 상태로 뒀지만, 여기선 후퇴도 결국
    /// "같은 면 위에서 플레이어와 반대 방향으로 이동"일 뿐이라 [[CubeEnemyBase]]의 Chasing
    /// 매크로 상태 하나로 흡수했습니다 — 실제로 정지 상태가 필요한 건 투척 윈드업/리커버리뿐이라
    /// 그것만 Busy로 분리했습니다.
    /// </summary>
    public class CubeEnemyWon : CubeEnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트)")]
        public float attackPower = 10f;
        public float moveSpeed = 2f;

        [Header("판정 (13 AI 설계, 27 프레임 데이터)")]
        [Tooltip("이 거리 범위 안이면 투척 공격을 합니다. ⚠️ retreatTriggerRange보다 크거나 같게 " +
                 "유지하세요 — 사이에 틈이 생기면 그 구간에서 후퇴도 공격도 안 하고 다시 다가가는 " +
                 "버그가 생깁니다([[changelog/2026-09-14_원거리몹-후퇴버그수정]]).")]
        public float attackMinRange = 1.5f;
        public float attackMaxRange = 5f;

        [Tooltip("이 거리 이하로 붙으면 후퇴합니다. attackMinRange와 같거나 그보다 커야 틈이 안 생깁니다.")]
        public float retreatTriggerRange = 1.5f;

        [Tooltip("후퇴할 때 확보하려는 거리입니다.")]
        public float retreatTargetDistance = 2f;

        [Tooltip("후퇴 중에만 적용되는 이동속도 배율입니다. 플레이어가 원(Won)보다 훨씬 빠르면 " +
                 "일반 이동속도로는 아예 거리를 못 벌리므로(2026-09-14 실측: 플레이어 6 vs 원 2), " +
                 "후퇴할 때만 순간적으로 더 빨리 움직이게 합니다.")]
        public float retreatSpeedMultiplier = 1.5f;

        [Header("투사체")]
        public float projectileSpeed = 12f;

        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum AttackPhase { Windup, Recovery }
        private AttackPhase phase;
        private float phaseTimer;

        // 27 전투 프레임 데이터 - 먹괴음 원 (60fps 기준 초 단위 환산) — 원본과 동일
        private const float WindupSeconds = 20f / 60f;
        private const float RecoverySeconds = 16f / 60f;

        protected override float MoveSpeed => moveSpeed;

        /// <summary>원본 BT_Enemy_Won의 Selector: 너무 가까우면 후퇴, 사거리 안이면 투척, 아니면 추격.</summary>
        protected override void OnChasingTick()
        {
            if (distanceToPlayer <= retreatTriggerRange)
            {
                Vector3 away = (transform.position - target.transform.position).normalized;
                MoveToward(transform.position + away * retreatTargetDistance, speedOverride: moveSpeed * retreatSpeedMultiplier);
                return;
            }

            if (distanceToPlayer >= attackMinRange && distanceToPlayer <= attackMaxRange)
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
                    if (phaseTimer >= WindupSeconds) EnterAttackRecovery(); // 투척 자체는 Windup 끝나는 순간 1회 실행
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

        private void EnterAttackRecovery()
        {
            phase = AttackPhase.Recovery;
            phaseTimer = 0f;
            ThrowInk();
        }

        /// <summary>Windup이 끝나는 순간 1회만 실행됩니다(원본과 동일 — 다단 실행 방지).</summary>
        private void ThrowInk()
        {
            OnAttackHit?.Invoke();
            if (target == null) return;

            GameObject projectileObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObj.name = "InkProjectile";
            projectileObj.transform.position = transform.position + faceNormal * 1f + transform.forward * 0.5f;
            projectileObj.transform.localScale = Vector3.one * 0.3f;
            SceneManager.MoveGameObjectToScene(projectileObj, gameObject.scene);

            var col = projectileObj.GetComponent<SphereCollider>();
            col.isTrigger = true;

            var rb = projectileObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var rendererComp = projectileObj.GetComponent<Renderer>();
            rendererComp.material.color = Color.black; // 먹물

            var projectile = projectileObj.AddComponent<InkProjectile>();
            projectile.speed = projectileSpeed;
            Vector3 direction = target.transform.position - projectileObj.transform.position;
            projectile.Launch(direction, attackPower);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackMinRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, attackMaxRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, retreatTriggerRange);
        }
    }
}
