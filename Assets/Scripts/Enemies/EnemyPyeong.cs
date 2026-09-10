using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// EnemyPyeong (먹괴음 - 평, 기본형)
    /// 플레이어를 감지하면 다가가서, 사거리 안에 들어오면 근접 공격합니다.
    ///
    /// [[13 먹괴음 AI 설계]]의 BT_Enemy_Pyeong(Selector: 사거리 안이면 공격, 아니면 추격)을
    /// C# 상태머신으로 구현했습니다 — 문서에도 "Unity Behavior 패키지 불안정 시
    /// 상태머신으로 대체 가능하게 설계" 라는 폴백이 이미 명시돼 있어서 그대로 따랐습니다.
    /// 공격 타이밍은 [[27 전투 프레임 데이터]]의 텔레그래프 프레임 수치 기준입니다.
    ///
    /// 시야 퍼셉션/넉백/에이전트 세팅은 [[EnemyBase]] 공통 구현을 씁니다 — 여긴 이 종만의
    /// 공격 상태머신(Windup/Active/Recovery)만 남아있습니다.
    /// </summary>
    public class EnemyPyeong : EnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트)")]
        public float attackPower = 8f;
        public float moveSpeed = 3f;

        [Header("판정 (13 AI 설계, 27 프레임 데이터)")]
        [Tooltip("이 거리 이하면 추격을 멈추고 공격을 시작합니다.")]
        public float attackRange = 1.5f;

        [Tooltip("공격이 실제로 맞는 판정 반경입니다.")]
        public float attackHitRadius = 1f;

        // 텔레그래프 연출 훅 — VFX/애니메이션은 나중에 이 이벤트를 구독해서 붙이면 됩니다. 지금은 로직만.
        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum State { Idle, Chase, AttackWindup, AttackActive, AttackRecovery }
        private State state = State.Idle;
        private float stateTimer;
        private float distanceToPlayer = float.MaxValue;

        // 27 전투 프레임 데이터 - 먹괴음 평 (60fps 기준 초 단위 환산)
        private const float WindupSeconds = 15f / 60f;
        private const float ActiveSeconds = 4f / 60f;
        private const float RecoverySeconds = 14f / 60f;

        protected override float MoveSpeed => moveSpeed;

        private void Update()
        {
            if (isKnockedBack) return; // 넉백 중엔 AI 로직을 통째로 쉰다 (agent를 꺼둔 상태라 이동 관련 호출이 위험함)

            TickPerception();

            switch (state)
            {
                case State.Idle:
                case State.Chase:
                    UpdateChase();
                    break;

                case State.AttackWindup:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= WindupSeconds)
                        EnterAttackActive();
                    break;

                case State.AttackActive:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= ActiveSeconds)
                        EnterAttackRecovery();
                    break;

                case State.AttackRecovery:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= RecoverySeconds)
                        EnterChaseOrIdle();
                    break;
            }
        }

        /// <summary>
        /// TickPerception이 player를 갱신한 직후 호출됩니다. 거리 갱신 + (공격 중이 아닐 때만)
        /// 이동 목적지 갱신 — 공격 도중엔 agent가 멈춰있습니다.
        /// </summary>
        protected override void OnPerceptionUpdated()
        {
            distanceToPlayer = player != null
                ? Vector3.Distance(transform.position, player.position)
                : float.MaxValue;

            if (player != null && IsChasingState())
                agent.SetDestination(player.position);
        }

        private bool IsChasingState() => state == State.Idle || state == State.Chase;

        private void UpdateChase()
        {
            if (player == null)
            {
                state = State.Idle;
                return;
            }

            if (distanceToPlayer <= attackRange)
            {
                EnterAttackWindup();
                return;
            }

            state = State.Chase;
        }

        private void EnterAttackWindup()
        {
            state = State.AttackWindup;
            stateTimer = 0f;
            agent.isStopped = true;
            animator?.SetTrigger("Attack"); // [[changelog/2026-09-10_먹괴음5종-애니메이터컨트롤러]]
            OnAttackWindupStart?.Invoke();
        }

        private void EnterAttackActive()
        {
            state = State.AttackActive;
            stateTimer = 0f;
            PerformAttack();
        }

        private void EnterAttackRecovery()
        {
            state = State.AttackRecovery;
            stateTimer = 0f;
        }

        private void EnterChaseOrIdle()
        {
            agent.isStopped = false;
            state = player != null ? State.Chase : State.Idle;
            stateTimer = 0f;
        }

        /// <summary>
        /// Active 프레임 진입 시 1회만 판정합니다. (BrushWeapon과 동일한 이유 — 다단히트 방지)
        /// </summary>
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

        /// <summary>공격 중이었더라도 넉백당하면 리셋 — 맞고도 태연히 공격을 이어가면 안 맞은 것처럼 느껴짐.</summary>
        protected override void OnKnockbackEnd()
        {
            state = player != null ? State.Chase : State.Idle;
            stateTimer = 0f;
        }

        // 에디터에서 감지/공격 범위를 눈으로 확인하기 위한 기즈모입니다.
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
