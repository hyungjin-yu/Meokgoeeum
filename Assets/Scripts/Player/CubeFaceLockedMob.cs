using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceLockedMob (면 고정 몹 — 프로토타입)
    /// 사용자 요구사항: "몹들은 면과 면을 이동할 수 없고, 캐릭터도 다른 면에 있는 몹은 공격이
    /// 불가능하다." [[CubeSurfaceWalker]]와 달리 <see cref="faceNormal"/>은 스폰 시점에 한 번
    /// 고정되고 절대 바뀌지 않습니다 — [[CubeFaceZone]] 트리거를 아예 구독하지 않아서, 구조적으로
    /// "다른 면으로 넘어갈 방법이 없는" 상태입니다(AI 판단으로 안 넘어가는 게 아니라, 애초에
    /// 넘어갈 수단 자체가 없음).
    ///
    /// ⚠️ 2026-09-12 추가 — 사용자 요청: "몹도 갑자기 시야에서 플레이어가 사라지면 당황하다가,
    /// 다시 보이면 쫓아가는 느낌으로." 4단계 상태 머신으로 구현:
    /// Chasing(추적) → 플레이어가 면을 떠나면 Searching(수색, 제자리에서 두리번거림, 최대
    /// <see cref="searchDuration"/>초) → 그 안에 플레이어가 돌아오면 다시 Chasing, 안 돌아오면
    /// Returning(원위치 복귀 + HP 회복) → 원위치 도착 시 Idle(대기, 계속 회복). 어느 상태에서든
    /// 플레이어가 자기 면에 다시 나타나면 즉시 Chasing으로 복귀합니다.
    /// </summary>
    public class CubeFaceLockedMob : MonoBehaviour
    {
        private enum MobState { Idle, Chasing, Searching, Returning }

        [Tooltip("이 몹이 묶여있는 면의 법선입니다. 스폰 후 절대 바뀌지 않습니다.")]
        public Vector3 faceNormal = Vector3.forward;

        [Tooltip("걷고 있는 큐브의 중심입니다.")]
        public Transform cubeCenter;

        [Tooltip("큐브 한 변 길이의 절반입니다.")]
        public float cubeHalfExtent = 10f;

        [Tooltip("피벗이 수학적 표면에서 법선 방향으로 얼마나 더 떠있어야 하는지입니다 — " +
                 "[[CubeSurfaceWalker]].surfaceOffset과 같은 이유(캡슐이 파묻혀 보이는 버그 방지)입니다.")]
        public float surfaceOffset = 1f;

        [Tooltip("추적 대상(플레이어)입니다.")]
        public CubeSurfaceWalker target;

        [Tooltip("추적/복귀 이동 속도입니다.")]
        public float moveSpeed = 3f;

        [Header("수색/복귀")]
        [Tooltip("플레이어가 면을 떠난 뒤 이 시간(초) 안에 돌아오면 다시 추적, 안 돌아오면 원위치로 복귀합니다.")]
        public float searchDuration = 5f;

        [Tooltip("수색 중 제자리에서 두리번거리는 좌우 각도입니다.")]
        public float lookAroundAngle = 45f;

        [Tooltip("두리번거리는 속도입니다.")]
        public float lookAroundSpeed = 2f;

        [Header("체력")]
        public float maxHP = 100f;
        public float currentHP = 100f;

        [Tooltip("복귀 중(및 원위치 대기 중) 초당 회복량입니다.")]
        public float returnRegenPerSecond = 25f;

        /// <summary>다른 스크립트(공격 판정 등)가 "같은 면인지"를 물을 때 씁니다.</summary>
        public bool IsSameFaceAs(Vector3 otherNormal) => Vector3.Dot(faceNormal, otherNormal) > 0.999f;

        private MobState state = MobState.Idle;
        private Vector3 spawnPosition;
        private Quaternion baseRotation;
        private float searchTimer;

        private void Start()
        {
            spawnPosition = transform.position;
            baseRotation = transform.rotation;
            currentHP = maxHP;
        }

        /// <summary>피격 시 호출합니다([[CubeFaceAttackTester]] 등에서). 죽음 처리는 아직 이 프로토타입 범위 밖입니다.</summary>
        public void TakeDamage(float amount)
        {
            currentHP = Mathf.Max(0f, currentHP - amount);
        }

        private void Heal(float amount)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        private void Update()
        {
            if (target == null) return;

            bool sameFace = IsSameFaceAs(target.CurrentSurfaceNormal);

            switch (state)
            {
                case MobState.Idle:
                    Heal(returnRegenPerSecond * Time.deltaTime);
                    if (sameFace) state = MobState.Chasing;
                    break;

                case MobState.Chasing:
                    if (!sameFace)
                    {
                        // 시야에서 놓침 — 당황해서 주변을 둘러보는 수색 상태로 전환.
                        state = MobState.Searching;
                        searchTimer = 0f;
                        break;
                    }
                    MoveToward(target.transform.position);
                    break;

                case MobState.Searching:
                    if (sameFace)
                    {
                        // 두리번거리다 다시 발견 — 즉시 추적 재개.
                        state = MobState.Chasing;
                        break;
                    }
                    searchTimer += Time.deltaTime;
                    LookAround();
                    if (searchTimer >= searchDuration)
                        state = MobState.Returning;
                    break;

                case MobState.Returning:
                    if (sameFace)
                    {
                        // 복귀 도중이라도 플레이어가 다시 보이면 추적으로 복귀.
                        state = MobState.Chasing;
                        break;
                    }
                    Heal(returnRegenPerSecond * Time.deltaTime);
                    bool arrived = MoveToward(spawnPosition, arriveThreshold: 0.15f);
                    if (arrived) state = MobState.Idle;
                    break;
            }
        }

        /// <summary>월드 좌표 목표를 향해 면 위에서 이동합니다. 도착하면 true를 반환합니다.</summary>
        private bool MoveToward(Vector3 worldTarget, float arriveThreshold = 0f)
        {
            Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
            Vector3 myLocal = transform.position - center;
            Vector3 targetLocal = worldTarget - center;

            int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);

            Vector3 toTarget = targetLocal - myLocal;
            toTarget[axis] = 0f; // 법선 축 성분은 제거 — 면 위 접선 방향으로만 이동
            bool arrived = toTarget.magnitude <= arriveThreshold;

            if (toTarget.sqrMagnitude > 0.0001f && !arrived)
            {
                Vector3 moveDir = toTarget.normalized;
                myLocal += moveDir * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(moveDir, faceNormal);
            }

            // 접선 두 축은 면 범위 안으로 clamp, 법선 축은 표면 값으로 고정
            // ([[CubeSurfaceWalker]]와 동일한 clamp 패턴 — 면 밖으로 못 나가게).
            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                myLocal[a] = Mathf.Clamp(myLocal[a], -cubeHalfExtent, cubeHalfExtent);
            }
            myLocal[axis] = (cubeHalfExtent + surfaceOffset) * Mathf.Sign(faceNormal[axis]);

            transform.position = center + myLocal;
            return arrived;
        }

        /// <summary>제자리에서 faceNormal 축을 기준으로 좌우로 두리번거립니다(당황한 느낌의 시각 피드백).</summary>
        private void LookAround()
        {
            float angle = Mathf.Sin(searchTimer * lookAroundSpeed) * lookAroundAngle;
            transform.rotation = Quaternion.AngleAxis(angle, faceNormal) * baseRotation;
        }
    }
}
