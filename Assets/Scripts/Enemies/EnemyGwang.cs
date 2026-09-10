using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// EnemyGwang (먹괴음 - 광, 광역형)
    /// 플레이어를 쫓아다니다가 쿨다운이 차면 그 자리에 멈춰서 자기 주변 전체를 공격합니다.
    /// [[03 먹괴음 - 적 설계]] 5종 중 마지막 구현체 — [[19 층별 상세 설계]] 7층("고요한 광장")에서
    /// 첫 등장(계단 조건: 광 처치, "광역 공격 피하기 유도").
    ///
    /// [[13 먹괴음 AI 설계]]의 BT_Enemy_Gwang(Selector: 쿨다운 다 찼으면 AOE 공격, 아니면 추격)을
    /// EnemyPyeong과 동일한 방식으로 C# 상태머신으로 구현했습니다. Pyeong과의 차이: Pyeong은
    /// "사거리 안"이라는 조건으로 공격을 트리거하지만, 광은 문서에 그런 조건이 없고 오직 쿨다운으로만
    /// 트리거합니다(평보다 단순한 BT라고 문서에 명시됨) — 그동안 추격이 계속 거리를 좁혀왔을 테니
    /// 실질적으로는 근접한 상태에서 터질 때가 많을 것이라는 전제입니다.
    ///
    /// 텔레그래프/판정/후딜 프레임은 [[27 전투 프레임 데이터]] 기준(25f/8f/20f) — 5종 중 텔레그래프가
    /// 가장 길어서 "가장 위협적"으로 문서화돼있고, "바닥에 원형 경고 범위 인디케이터"가 특징입니다.
    ///
    /// 시야 퍼셉션/넉백/에이전트 세팅은 [[EnemyBase]] 공통 구현을 씁니다.
    /// </summary>
    public class EnemyGwang : EnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트 - 먹괴음 스탯표: HP 60/공격력 15/이동속도 1.5f/s)")]
        public float attackPower = 15f;
        public float moveSpeed = 1.5f;

        [Header("판정 (13 AI 설계)")]
        [Tooltip("AOE 공격 판정 반경입니다. (13 AI 설계: Physics.OverlapSphere 반경 3f)")]
        public float aoeRadius = 3f;

        [Tooltip("AOE 공격 사이 쿨다운입니다. 프레임 데이터 문서엔 값이 없어서 임의값(3초) — 실전 테스트로 조정 필요.")]
        public float aoeCooldown = 3f;

        // 텔레그래프 연출 훅 — VFX/애니메이션은 나중에 이 이벤트를 구독해서 붙이면 됩니다. 지금은 로직만.
        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum State { Idle, Chase, AttackWindup, AttackActive, AttackRecovery }
        private State state = State.Idle;
        private float stateTimer;
        private float cooldownTimer;
        private GameObject warningIndicator; // Windup 동안만 보이는 바닥 경고 범위

        // 27 전투 프레임 데이터 - 먹괴음 광 (60fps 기준 초 단위 환산, 5종 중 텔레그래프 최장)
        private const float WindupSeconds = 25f / 60f;
        private const float ActiveSeconds = 8f / 60f;
        private const float RecoverySeconds = 20f / 60f;

        protected override float MoveSpeed => moveSpeed;

        protected override void Awake()
        {
            base.Awake();
            cooldownTimer = aoeCooldown; // 스폰 직후 바로 시야에 플레이어가 있어도 즉시 공격하지 않도록

            // EnemyHealth 기본값(20)은 평/원 기준 — 광은 14 밸런스 수치 시트에 HP 60으로 명시돼있어서
            // EnemyBun이 분열 미니언 HP를 맞출 때와 같은 방식(ConfigureMaxHP)으로 여기서 직접 맞춥니다.
            // (Inspector 기본값에만 의존하면 나중에 프리팹 만들 때 깜빡 잊고 20으로 남을 수 있음)
            GetComponent<EnemyHealth>().ConfigureMaxHP(60f);
        }

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

        /// <summary>공격 중이 아닐 때만 이동 목적지를 갱신합니다 (공격 도중엔 agent가 멈춰있음).</summary>
        protected override void OnPerceptionUpdated()
        {
            if (player != null && IsChasingState())
                agent.SetDestination(player.position);
        }

        private bool IsChasingState() => state == State.Idle || state == State.Chase;

        /// <summary>
        /// BT_Enemy_Gwang의 Selector: 쿨다운이 다 찼으면(사거리 무관) AOE 공격, 아니면 추격.
        /// </summary>
        private void UpdateChase()
        {
            if (player == null)
            {
                state = State.Idle;
                return;
            }

            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
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
            SpawnWarningIndicator();
            animator?.SetTrigger("Attack"); // [[changelog/2026-09-10_먹괴음5종-애니메이터컨트롤러]]
            OnAttackWindupStart?.Invoke();
        }

        private void EnterAttackActive()
        {
            state = State.AttackActive;
            stateTimer = 0f;
            DespawnWarningIndicator();
            PerformAttack();
        }

        private void EnterAttackRecovery()
        {
            state = State.AttackRecovery;
            stateTimer = 0f;
            cooldownTimer = aoeCooldown; // 다음 공격까지 쿨다운은 여기서부터 다시 셈
        }

        private void EnterChaseOrIdle()
        {
            agent.isStopped = false;
            state = player != null ? State.Chase : State.Idle;
            stateTimer = 0f;
        }

        /// <summary>
        /// Active 프레임 진입 시 1회만 판정합니다. (EnemyPyeong/BrushWeapon과 동일한 이유 — 다단히트 방지)
        /// </summary>
        private void PerformAttack()
        {
            OnAttackHit?.Invoke();

            Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                damageable?.TakeDamage(attackPower);
            }
        }

        /// <summary>
        /// Windup 동안만 보이는 바닥 경고 범위입니다. [[DamageZone]]의 "런타임 프리미티브로 만든
        /// 납작한 실린더" 패턴을 그대로 따릅니다 — 다만 DamageZone과 달리 이건 순수 시각 경고라서
        /// 콜라이더는 만들자마자 지웁니다(실제 피해 판정은 EnterAttackActive의 PerformAttack에서
        /// OverlapSphere로 따로 계산 — 콜라이더를 살려두면 플레이어가 물리적으로 밀려나는 부작용이 생김).
        /// </summary>
        private void SpawnWarningIndicator()
        {
            warningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warningIndicator.name = "GwangWarningIndicator";
            Destroy(warningIndicator.GetComponent<Collider>());
            warningIndicator.transform.position = transform.position;
            warningIndicator.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);
            // ⚠️ 2026-08-25: CreatePrimitive는 parent 인자가 없어 기본적으로 "지금 활성 씬"에 생성됨
            // ([[changelog/2026-08-24_재방문-랜덤인카운터]] 참고). 인디케이터는 광의 이동을 안 따라가야
            // 하므로(위치 고정, windup 중엔 광도 안 움직이지만 혹시 몰라 transform.parent로는 안 붙임)
            // 부모 지정 대신 씬 소속만 광 본인 기준으로 맞춤.
            SceneManager.MoveGameObjectToScene(warningIndicator, gameObject.scene);

            var renderer = warningIndicator.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 0.15f, 0.15f, 0.6f); // 붉은 경고색 (반투명)
        }

        private void DespawnWarningIndicator()
        {
            if (warningIndicator != null)
                Destroy(warningIndicator);
        }

        /// <summary>공격 준비 중 넉백당하면 경고 표시도 같이 취소.</summary>
        protected override void OnKnockbackStart()
        {
            DespawnWarningIndicator();
        }

        /// <summary>공격 중이었더라도 넉백당하면 리셋 — 맞고도 태연히 공격을 이어가면 안 맞은 것처럼 느껴짐.</summary>
        protected override void OnKnockbackEnd()
        {
            state = player != null ? State.Chase : State.Idle;
            stateTimer = 0f;
        }

        // 공격 준비 도중(경고 인디케이터가 떠있는 채로) 처치돼도 인디케이터가 안 남게 정리합니다.
        private void OnDestroy()
        {
            DespawnWarningIndicator();
        }

        // 에디터에서 감지/공격 범위를 눈으로 확인하기 위한 기즈모입니다.
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}
