using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyGwang (먹괴음 - 광, 큐브 면 버전)
    /// 원본 [[EnemyGwang]]과 텔레그래프/판정/후딜 프레임(25f/8f/20f, 5종 중 최장) 및 "쿨다운만으로
    /// 트리거되는 AOE 공격, 바닥 경고 인디케이터"는 완전히 동일합니다. Pyeong과 차이: 사거리 조건이
    /// 없고 오직 쿨다운으로만 공격이 트리거됩니다.
    /// </summary>
    public class CubeEnemyGwang : CubeEnemyBase
    {
        [Header("스탯 (14 밸런스 수치 시트 - HP 60/공격력 15/이동속도 1.5f/s)")]
        public float attackPower = 15f;
        public float moveSpeed = 1.5f;

        [Header("판정 (13 AI 설계)")]
        [Tooltip("AOE 공격 판정 반경입니다.")]
        public float aoeRadius = 3f;

        [Tooltip("AOE 공격 사이 쿨다운입니다.")]
        public float aoeCooldown = 3f;

        public event System.Action OnAttackWindupStart;
        public event System.Action OnAttackHit;

        private enum AttackPhase { Windup, Active, Recovery }
        private AttackPhase phase;
        private float phaseTimer;
        private float cooldownTimer;
        private GameObject warningIndicator; // Windup 동안만 보이는 바닥 경고 범위

        // 27 전투 프레임 데이터 - 먹괴음 광 (60fps 기준 초 단위 환산, 5종 중 텔레그래프 최장) — 원본과 동일
        private const float WindupSeconds = 25f / 60f;
        private const float ActiveSeconds = 8f / 60f;
        private const float RecoverySeconds = 20f / 60f;

        protected override float MoveSpeed => moveSpeed;

        protected override void Start()
        {
            base.Start();
            cooldownTimer = aoeCooldown; // 스폰 직후 바로 시야에 플레이어가 있어도 즉시 공격하지 않도록
            maxHP = 60f; // 14 밸런스 수치 시트 — 평/원 기본값(100)과 다름
            currentHP = maxHP;
        }

        /// <summary>원본 BT_Enemy_Gwang의 Selector: 쿨다운이 다 찼으면(사거리 무관) AOE 공격, 아니면 추격.</summary>
        protected override void OnChasingTick()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
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
            SpawnWarningIndicator();
            OnAttackWindupStart?.Invoke();
        }

        private void EnterAttackActive()
        {
            phase = AttackPhase.Active;
            phaseTimer = 0f;
            DespawnWarningIndicator();
            PerformAttack();
        }

        private void EnterAttackRecovery()
        {
            phase = AttackPhase.Recovery;
            phaseTimer = 0f;
            cooldownTimer = aoeCooldown; // 다음 공격까지 쿨다운은 여기서부터 다시 셈
        }

        /// <summary>Active 프레임 진입 시 1회만 판정합니다(다단히트 방지 — 원본과 동일).</summary>
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

        /// <summary>[[DamageZone]] 패턴과 동일 — 순수 시각 경고라 콜라이더는 만들자마자 지웁니다.</summary>
        private void SpawnWarningIndicator()
        {
            warningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warningIndicator.name = "GwangWarningIndicator";
            Destroy(warningIndicator.GetComponent<Collider>());
            warningIndicator.transform.position = transform.position;
            // ⚠️ 2026-09-14 발견 — 원본 EnemyGwang은 항상 평평한 바닥(월드 Y-up)에서만 쓰여서
            // 회전을 안 줘도 원판이 자연히 바닥에 눕지만, 큐브 옆면(faceNormal이 월드 Y가 아닌
            // 면)에서는 실린더의 기본 자세(로컬 up=월드 Y)가 그 면과 안 맞아서 경고 범위가
            // 바닥과 수직으로 튀어나와 보임(사용자 리포트: "공격 범위가 땅에 수직으로 나온다") —
            // faceNormal을 up으로 삼아 면에 눕도록 회전을 맞춰줍니다.
            warningIndicator.transform.rotation = Quaternion.FromToRotation(Vector3.up, faceNormal);
            warningIndicator.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);
            SceneManager.MoveGameObjectToScene(warningIndicator, gameObject.scene);

            var rendererComp = warningIndicator.GetComponent<Renderer>();
            rendererComp.material.color = new Color(1f, 0.15f, 0.15f, 0.6f); // 붉은 경고색(반투명)
        }

        private void DespawnWarningIndicator()
        {
            if (warningIndicator != null) Destroy(warningIndicator);
        }

        private void OnDestroy()
        {
            DespawnWarningIndicator(); // 공격 준비 중(경고 인디케이터가 떠있는 채로) 처치돼도 안 남게 정리
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}
