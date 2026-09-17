using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// ColorSkillController (색 스킬 — 붓 스킬)
    /// [[02 플레이어 시스템]] "붓 스킬(색별)" 중 v0.2 범위인 3종을 구현합니다.
    /// 초록(되살림)/보라(왜곡)/검정(먹물)은 아직 없습니다 — 나중에 같은 패턴(Skill4~6 액션 추가)으로 넣으면 됩니다.
    ///
    /// 키 1/2/3을 각 색에 고정 매핑했습니다. 원안은 "1~6 슬롯"이 FIFO로 계속 바뀌는 구조지만,
    /// 슬롯을 화면에 보여줄 UI가 아직 없어서(v0.1엔 UI 자체가 없음) 지금은 "그 색 구슬을
    /// 보유하고 있으면 그 키로 바로 쓴다"로 단순화했습니다. UI가 생기면 실제 슬롯 매핑으로 교체.
    ///
    /// 대미지 배율/쿨타임은 [[14 밸런스 수치 시트]] "플레이어 스킬 대미지", 공통 캐스팅 Startup은
    /// [[27 전투 프레임 데이터]] "색 스킬(1~6) 캐스팅" 기준(10f)입니다. 콤보 중 스킬 캔슬은 아직
    /// 구현 안 함 — v0.2 후속으로 미룸 (지금은 BrushWeapon 상태와 무관하게 독립적으로 동작).
    ///
    /// ⚠️ 2026-09-17 추가 — 큐브 면 프로토타입([[CubeSurfaceWalker]])에도 붙일 수 있도록
    /// `[RequireComponent(typeof(CharacterController))]`/`[RequireComponent(typeof(PlayerController))]`를
    /// 뗐습니다. [[BrushWeapon]]에서 이미 검증된 것과 동일한 패턴 — PlayerController가 있으면
    /// 그 InputActions를 공유(기존과 동일), 없으면 자체 인스턴스를 만들어 씀. 강타(빨강)의 대시는
    /// CharacterController가 있으면 `cc.Move()`(기존과 동일), 없고 CubeSurfaceWalker가 있으면
    /// `CubeSurfaceWalker.ExternalStep()`(면 접선에 투영 + 표면 재클램프)으로 대신 이동시킵니다.
    /// 대미지 판정도 BrushWeapon과 동일하게 [[ICubeFaceMob]] 대상에 한해 같은 면인지 검사합니다 —
    /// 평지 던전(둘 다 없음)에서는 이 모든 분기가 기존 경로 그대로라 동작 변화 없습니다.
    /// </summary>
    public class ColorSkillController : MonoBehaviour
    {
        [Header("빨강 — 강타 (전방 대시 + 강타, 14 밸런스 수치 시트: ×2.0 / 8초)")]
        public float strikeCooldown = 8f;
        public float strikeDamageMultiplier = 2.0f;
        public float strikeDashDistance = 5f;
        public float strikeDashDuration = 0.15f;
        public float strikeHitRadius = 1.5f;

        [Header("파랑 — 흐름 (전방 직선 관통, 14 밸런스 수치 시트: ×1.5 / 6초)")]
        public float flowCooldown = 6f;
        public float flowDamageMultiplier = 1.5f;
        public float flowLength = 6f;
        public float flowWidth = 2f;

        [Header("노랑 — 번쩍 (주변 범위 폭발 + 넉백, 14 밸런스 수치 시트: ×1.0 / 10초)")]
        public float flashCooldown = 10f;
        public float flashDamageMultiplier = 1.0f;
        public float flashRadius = 4f;
        public float flashKnockbackForce = 12f;

        // 27 전투 프레임 데이터 - 색 스킬 공통 Startup (10f ≈ 0.167초, 60fps 환산)
        private const float StartupSeconds = 10f / 60f;

        public bool IsDashing { get; private set; } // 강타의 대시 구간 동안 true (PlayerController가 이동을 양보하도록)

        private CharacterController cc; // 평지 던전 — 있으면 강타 대시를 cc.Move()로 처리
        private CubeSurfaceWalker cubeWalker; // 큐브 면 프로토타입 — 있으면 강타 대시를 ExternalStep()으로 처리
        private BrushWeapon brushWeapon; // 스킬 대미지의 기준이 되는 "붓 공격력"을 여기서 읽어옴 (단일 출처 유지)
        private LockOnController lockOn; // 락온 중이면 스킬 조준을 여기 맞춤 (2026-09-02, 아래 GetAimDirection 참고)
        private PlayerInputActions inputActions; // PlayerController가 있으면 그걸 공유, 없으면 자체 소유
        private bool ownsInputActions; // 자체 소유일 때만 true — OnDestroy에서 직접 정리해야 함

        private float strikeCooldownTimer;
        private float flowCooldownTimer;
        private float flashCooldownTimer;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            cubeWalker = GetComponent<CubeSurfaceWalker>();
            brushWeapon = GetComponent<BrushWeapon>();
            lockOn = GetComponent<LockOnController>();
        }

        /// <summary>
        /// 2026-09-02: 강타/흐름이 `transform.forward`(순간 이동 방향/카메라 방향)만 보고 조준하다
        /// 보니, 실측 중 구슬 소모하고 스킬은 나가는데 보스한테 "0대상"으로 빗맞는 게 반복 확인됨
        /// (기본 콤보는 관대한 구체 판정이라 잘 맞는데, 강타는 5m 고정 거리 대시 끝 지점에서만,
        /// 흐름은 정면 좁은 박스에서만 판정해서 순간 방향이 살짝만 틀어져도 허공을 침). 락온
        /// 중이면([[LockOnController]]) 타겟 방향으로 조준을 강제해서 이 문제를 줄입니다 —
        /// 락온 없이 쓰면 기존처럼 `transform.forward` 그대로.
        /// </summary>
        private Vector3 GetAimDirection()
        {
            if (lockOn != null && lockOn.IsLockedOn)
            {
                Vector3 toTarget = lockOn.CurrentTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
            }
            return transform.forward;
        }

        private void Start()
        {
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                // PlayerController가 Awake()에서 만들어 Enable()까지 해둔 인스턴스를 공유해서 씁니다.
                inputActions = playerController.InputActions;
            }
            else
            {
                // 큐브 면 프로토타입처럼 PlayerController가 없는 경우 — 자체 인스턴스를 만들어 씁니다.
                inputActions = new PlayerInputActions();
                inputActions.Enable();
                ownsInputActions = true;
            }

            inputActions.Player.Skill1.performed += _ => TryCast(OrbColor.Red);
            inputActions.Player.Skill2.performed += _ => TryCast(OrbColor.Blue);
            inputActions.Player.Skill3.performed += _ => TryCast(OrbColor.Yellow);
        }

        private void OnDestroy()
        {
            // PlayerController.OnDestroy()/BrushWeapon.OnDestroy()와 동일하게, Enable()과 반드시
            // 짝을 맞춰 Disable()을 먼저 호출해야 "leak and performance issues" 경고가 안 뜹니다.
            if (ownsInputActions)
            {
                inputActions?.Disable();
                inputActions?.Dispose();
            }
        }

        private void Update()
        {
            if (strikeCooldownTimer > 0f) strikeCooldownTimer -= Time.deltaTime;
            if (flowCooldownTimer > 0f) flowCooldownTimer -= Time.deltaTime;
            if (flashCooldownTimer > 0f) flashCooldownTimer -= Time.deltaTime;
        }

        /// <summary>
        /// 기본 공격력(붓 공격력)입니다. BrushWeapon이 없으면 14 밸런스 수치 시트 기준값(10)을 씁니다.
        /// </summary>
        private float BaseAttackPower => brushWeapon != null ? brushWeapon.attackPower : 10f;

        private void TryCast(OrbColor color)
        {
            float cooldownTimer = GetCooldownTimer(color);
            if (cooldownTimer > 0f)
            {
                Debug.Log($"[ColorSkillController] {color} 스킬 쿨타임 중 (남은 {cooldownTimer:F1}초)");
                return;
            }

            if (ColorSystemManager.Instance == null || !ColorSystemManager.Instance.TryConsumeOrb(color))
            {
                Debug.Log($"[ColorSkillController] {color} 구슬이 없어서 스킬을 쓸 수 없음");
                return;
            }

            SetCooldownTimer(color, GetCooldownDuration(color));

            switch (color)
            {
                case OrbColor.Red:
                    StartCoroutine(CastStrike());
                    break;
                case OrbColor.Blue:
                    StartCoroutine(CastFlow());
                    break;
                case OrbColor.Yellow:
                    StartCoroutine(CastFlash());
                    break;
            }
        }

        // 2026-09-17 추가 — private → public. 스킬 HUD(구슬 슬롯 쿨타임 웨지)가 이 값들을
        // 그대로 읽어서 진행률(GetCooldownTimer/GetCooldownDuration)을 표시함. 순수 조회
        // 메서드라 외부 공개해도 안전(부작용 없음).
        public float GetCooldownTimer(OrbColor color) => color switch
        {
            OrbColor.Red => strikeCooldownTimer,
            OrbColor.Blue => flowCooldownTimer,
            OrbColor.Yellow => flashCooldownTimer,
            _ => 0f,
        };

        public float GetCooldownDuration(OrbColor color) => color switch
        {
            OrbColor.Red => strikeCooldown,
            OrbColor.Blue => flowCooldown,
            OrbColor.Yellow => flashCooldown,
            _ => 0f,
        };

        private void SetCooldownTimer(OrbColor color, float value)
        {
            switch (color)
            {
                case OrbColor.Red: strikeCooldownTimer = value; break;
                case OrbColor.Blue: flowCooldownTimer = value; break;
                case OrbColor.Yellow: flashCooldownTimer = value; break;
            }
        }

        /// <summary>
        /// 강타(빨강): Startup 후 전방으로 짧게 대시하고, 도착 지점에서 강력한 한 방을 때립니다.
        /// </summary>
        private IEnumerator CastStrike()
        {
            Debug.Log("[ColorSkillController] 강타 시전!");
            yield return new WaitForSeconds(StartupSeconds);

            IsDashing = true;
            Vector3 direction = GetAimDirection();
            float speed = strikeDashDistance / strikeDashDuration;

            float elapsed = 0f;
            while (elapsed < strikeDashDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                Vector3 step = direction * speed * dt;
                // 2026-09-17 — 평지 던전은 기존대로 cc.Move(), 큐브 면 프로토타입은 CharacterController가
                // 없으므로 CubeSurfaceWalker.ExternalStep()으로 대신 이동(면 접선 투영 + 표면 재클램프).
                if (cc != null)
                    cc.Move(step);
                else if (cubeWalker != null)
                    cubeWalker.ExternalStep(step);
                yield return null;
            }
            IsDashing = false;

            float damage = BaseAttackPower * strikeDamageMultiplier;
            Collider[] hits = Physics.OverlapSphere(transform.position, strikeHitRadius);
            int hitCount = DamageAll(hits, damage);
            Debug.Log($"[ColorSkillController] 강타 적중! 대미지 {damage} x {hitCount}대상");
        }

        /// <summary>
        /// 흐름(파랑): Startup 후 전방 직선 범위(박스) 안의 모든 대상을 관통 히트합니다.
        /// </summary>
        private IEnumerator CastFlow()
        {
            Debug.Log("[ColorSkillController] 흐름 시전!");
            yield return new WaitForSeconds(StartupSeconds);

            float damage = BaseAttackPower * flowDamageMultiplier;
            Vector3 aimDir = GetAimDirection();
            // 2026-09-17 — 큐브 면 위에서는 "위"가 항상 월드 Vector3.up이 아니라 그 면의 법선입니다.
            // 기본값(Vector3.up)으로 LookRotation하면 옆면/윗면 등에서 박스가 비뚤어져서 판정이
            // 어긋날 수 있어 cubeWalker가 있으면 실제 표면 법선을 up으로 씀 — 평지 던전은 그대로.
            Vector3 up = cubeWalker != null ? cubeWalker.CurrentSurfaceNormal : Vector3.up;
            Quaternion aimRot = Quaternion.LookRotation(aimDir, up);
            Vector3 center = transform.position + aimDir * (flowLength / 2f);
            Vector3 halfExtents = new Vector3(flowWidth / 2f, 1f, flowLength / 2f);

            Collider[] hits = Physics.OverlapBox(center, halfExtents, aimRot);
            int hitCount = DamageAll(hits, damage);
            Debug.Log($"[ColorSkillController] 흐름 적중! 대미지 {damage} x {hitCount}대상");
        }

        /// <summary>
        /// 번쩍(노랑): Startup 후 주변 반경 전체에 대미지 + 넉백을 줍니다.
        /// </summary>
        private IEnumerator CastFlash()
        {
            Debug.Log("[ColorSkillController] 번쩍 시전!");
            yield return new WaitForSeconds(StartupSeconds);

            float damage = BaseAttackPower * flashDamageMultiplier;
            Collider[] hits = Physics.OverlapSphere(transform.position, flashRadius);

            // 2026-09-16 — BrushWeapon.PerformHit()과 같은 이유로 중복 방어([[MonsterPaintParts]]
            // 도입 이후 "한 번의 판정으로 부위가 2개씩 칠해지는" 형태로 드러날 수 있음).
            var alreadyHit = new HashSet<GameObject>();

            int hitCount = 0;
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) continue; // 자기 자신 제외

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) continue;

                if (!alreadyHit.Add(hit.gameObject)) continue;

                // 2026-09-17 — [[BrushWeapon]]과 동일한 이유로, 큐브 면 위(cubeWalker != null)에서만
                // 같은 면인지 검사합니다. 평지 던전/면 개념 없는 대상은 그대로 통과.
                if (cubeWalker != null && damageable is ICubeFaceMob mob && !mob.IsSameFaceAs(cubeWalker.CurrentSurfaceNormal))
                    continue;

                damageable.TakeDamage(damage);
                hitCount++;

                var knockbackable = hit.GetComponent<IKnockbackable>();
                if (knockbackable != null)
                {
                    Vector3 dir = hit.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
                    knockbackable.ApplyKnockback(dir.normalized, flashKnockbackForce);
                }
            }

            Debug.Log($"[ColorSkillController] 번쩍 적중! 대미지 {damage} x {hitCount}대상 (넉백 포함)");
        }

        /// <summary>
        /// hits 중 자기 자신을 제외한 IDamageable에게 전부 같은 대미지를 줍니다. 맞은 수를 반환합니다.
        /// </summary>
        private int DamageAll(Collider[] hits, float damage)
        {
            // 2026-09-16 — BrushWeapon.PerformHit()과 같은 이유로 중복 방어([[MonsterPaintParts]]
            // 도입 이후 "한 번의 판정으로 부위가 2개씩 칠해지는" 형태로 드러날 수 있음).
            var alreadyHit = new HashSet<GameObject>();
            int count = 0;
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) continue; // 자기 자신 제외

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) continue;

                if (!alreadyHit.Add(hit.gameObject)) continue;

                // 2026-09-17 — 위 CastFlash와 동일한 같은 면 검사(강타/흐름 공용 경로).
                if (cubeWalker != null && damageable is ICubeFaceMob mob && !mob.IsSameFaceAs(cubeWalker.CurrentSurfaceNormal))
                    continue;

                damageable.TakeDamage(damage);
                count++;
            }
            return count;
        }

        // 에디터에서 판정 범위를 눈으로 확인하기 위한 기즈모입니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.forward * strikeDashDistance, strikeHitRadius);

            Gizmos.color = Color.blue;
            Vector3 flowCenter = transform.position + transform.forward * (flowLength / 2f);
            Gizmos.matrix = Matrix4x4.TRS(flowCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(flowWidth, 2f, flowLength));
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, flashRadius);
        }
    }
}
