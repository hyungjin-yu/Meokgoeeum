using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// LockOnController (Tab 락온 — 간소화 버전)
    /// [[02 플레이어 시스템]]/[[16 조작 설계]] "Tab → Lock-On (적 잠금)". 타겟 감지/전환 + 플레이어가
    /// 타겟을 바라보도록 회전 + 화면 마커까지가 스코프입니다.
    ///
    /// ⚠️ 2026-08-31: 카메라 프레이밍(`CinemachineTargetGroup` + 전용 카메라, 기획서 스펙)도
    /// 같은 날 시도했지만 포기했습니다 — 마우스를 끄면 좁은 방A에서 벽에 낀 채 못 빠져나오고,
    /// 켜두면 마우스가 다 덮어써서 "고정"이 의미가 없고, Deoccluder(벽 회피)를 붙이면 이번엔
    /// 좁은 방 구조상 거의 항상 "막혔다" 판단해서 카메라가 과하게 확대돼 플레이가 안 되는 등,
    /// 실제로 보면서 튜닝해야만 풀리는 문제가 연속으로 나와서 시간 대비 효율이 안 남. 그래서
    /// 카메라는 원래 시점(`CinemachineCamera` 기본)을 그대로 쓰고, 이 스크립트는 카메라를
    /// 아예 건드리지 않습니다. `LockOnCamera`/`LockOnTargetGroup`/`CinemachineDeoccluder`
    /// 오브젝트는 씬에 그대로 남겨뒀습니다(Priority -10이라 절대 안 뜸) — 나중에 시간 내서
    /// 직접 플레이하며 튜닝할 사람을 위한 밑작업으로.
    ///
    /// [[27 전투 프레임 데이터]] "타겟 전환 쿨다운: 10f(60fps 환산 ≈0.167초)" — Tab 연타로 인한
    /// "카메라 멀미"를 막기 위한 값(카메라를 안 건드리는 지금 버전에서도 타겟이 매 프레임
    /// 휙휙 바뀌는 걸 막는 용도로 재사용).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class LockOnController : MonoBehaviour
    {
        public static LockOnController Instance { get; private set; }

        [Header("타겟 탐지")]
        [Tooltip("이 반경 안의 적만 락온 후보로 고려합니다.")]
        public float lockOnRadius = 3.5f;

        [Tooltip("락온 상태에서 타겟이 이 거리보다 멀어지면 자동 해제합니다 (lockOnRadius보다 커야 함 — 경계에서 계속 붙었다 떨어지는 걸 방지).")]
        public float releaseRadius = 5f;

        // ⚠️ 2026-09-10: 720(0.25초면 반바퀴)이었다가 300으로 낮춤 — 락온을 거는 순간 이전 방향과
        // 타겟 방향의 차이가 크면(예: 뒤돌아있다가 락온) 거의 순간 이동처럼 보인다는 리포트로
        // 실측 확인(40프레임 동안 position은 전혀 안 변하고 rotation만 0→302→244→187→183도로
        // 수렴 — 즉 "순간 이동"의 정체는 과도하게 빠른 회전이었음, [[changelog/2026-09-10_플레이어-대시중회전스냅-수정]]).
        [Tooltip("락온 중 타겟을 바라보는 회전 속도입니다. (초당 각도)")]
        public float rotationSpeed = 300f;

        // 27 전투 프레임 데이터 - 타겟 전환 쿨다운 10f(60fps 환산), 연타 스팸으로 인한 카메라 멀미 방지
        private const float SwitchCooldownSeconds = 10f / 60f;

        public Transform CurrentTarget { get; private set; }
        public bool IsLockedOn => CurrentTarget != null;

        private float switchCooldownTimer;

        // [[changelog/2026-09-10_플레이어-대시중회전스냅-수정]] 강타(빨강) 대시 중엔 이동/회전 둘 다
        // ColorSkillController가 전담해야 하는데, 이 스크립트는 그걸 모르고 매 프레임 계속
        // FaceTarget()을 돌렸음 — 대시로 타겟 근접을 스쳐 지나가면 상대 각도가 급격히 바뀌어서
        // "방향이 홱 돌아간다"는 리포트로 이어짐. PlayerController가 이미 IsDashing을 참조하는
        // 것과 같은 패턴으로 여기도 참조해서 대시 중엔 회전을 양보합니다.
        private ColorSkillController skills;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[LockOnController] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            skills = GetComponent<ColorSkillController>(); // 없어도(색 스킬 미부착) 동작은 그대로 — null 체크로 방어
        }

        private void Update()
        {
            if (switchCooldownTimer > 0f)
                switchCooldownTimer -= Time.deltaTime;

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                HandleTabPress();

            if (IsLockedOn)
            {
                ValidateCurrentTarget();
                bool isDashing = skills != null && skills.IsDashing;
                if (IsLockedOn && !isDashing) FaceTarget();
            }
        }

        private void HandleTabPress()
        {
            if (switchCooldownTimer > 0f) return;

            Transform next = FindNextTarget();
            switchCooldownTimer = SwitchCooldownSeconds;

            if (next == null)
            {
                if (IsLockedOn) ReleaseLock();
                return;
            }

            SetCurrentTarget(next);
            Debug.Log($"[LockOnController] 락온: {next.name}");
        }

        /// <summary>가장 가까운 유효 타겟을 찾습니다. 이미 락온 중이면 "현재 타겟이 아닌 것" 중에서 찾아서 Tab을 다시 누르면 순환되게 합니다.</summary>
        private Transform FindNextTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRadius);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!IsValidTarget(hit.transform)) continue;
                if (hit.transform == CurrentTarget) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = hit.transform;
                }
            }

            // 락온 중인데 후보가 하나도 없다면(주변에 다른 적이 없음) 지금 타겟 유지
            if (best == null && IsLockedOn && IsValidTarget(CurrentTarget))
                return CurrentTarget;

            return best;
        }

        private bool IsValidTarget(Transform t)
        {
            if (t == null) return false;

            var enemyHealth = t.GetComponent<EnemyHealth>();
            if (enemyHealth != null) return !enemyHealth.IsDead;

            var bossHealth = t.GetComponent<BossHealth>();
            if (bossHealth != null) return !bossHealth.IsDead;

            return false;
        }

        private void ValidateCurrentTarget()
        {
            if (!IsValidTarget(CurrentTarget))
            {
                ReleaseLock();
                return;
            }

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);
            if (dist > releaseRadius)
                ReleaseLock();
        }

        private void ReleaseLock()
        {
            Debug.Log("[LockOnController] 락온 해제.");
            SetCurrentTarget(null);
        }

        private void SetCurrentTarget(Transform newTarget)
        {
            CurrentTarget = newTarget;
        }

        private void FaceTarget()
        {
            Vector3 dir = CurrentTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
