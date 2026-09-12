using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceLockedMob (면 고정 몹 — 프로토타입)
    /// 사용자 요구사항: "몹들은 면과 면을 이동할 수 없고, 캐릭터도 다른 면에 있는 몹은 공격이
    /// 불가능하다." 이 컴포넌트는 그 첫 번째 절반(몹의 면 이동 금지)을 담당합니다.
    ///
    /// [[CubeSurfaceWalker]]와 달리 <see cref="faceNormal"/>은 스폰 시점에 한 번 고정되고
    /// 절대 바뀌지 않습니다 — [[CubeFaceZone]] 트리거를 아예 구독하지 않아서, 구조적으로
    /// "다른 면으로 넘어갈 방법이 없는" 상태입니다(AI 판단으로 안 넘어가는 게 아니라,애초에
    /// 넘어갈 수단 자체가 없음).
    ///
    /// 플레이어가 같은 면에 있을 때만 그 방향으로 접근하되, 자기 면의 경계(±cubeHalfExtent)를
    /// 넘지 않도록 접선 축을 clamp합니다 — [[CubeSurfaceWalker]]가 표면에 붙어있는 방식과
    /// 동일한 clamp 패턴을 재사용했습니다.
    /// </summary>
    public class CubeFaceLockedMob : MonoBehaviour
    {
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

        [Tooltip("접근 속도입니다.")]
        public float moveSpeed = 3f;

        /// <summary>다른 스크립트(공격 판정 등)가 "같은 면인지"를 물을 때 씁니다.</summary>
        public bool IsSameFaceAs(Vector3 otherNormal) => Vector3.Dot(faceNormal, otherNormal) > 0.999f;

        private void Update()
        {
            if (target == null) return;

            // 플레이어가 내 면에 없으면 — 쫓아갈 수단 자체가 없으므로 가만히 있습니다.
            // (AI가 "안 쫓기로 판단"한 게 아니라, faceNormal이 안 바뀌니 구조적으로 못 쫓아감)
            if (!IsSameFaceAs(target.CurrentSurfaceNormal)) return;

            Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
            Vector3 myLocal = transform.position - center;
            Vector3 targetLocal = target.transform.position - center;

            int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);

            Vector3 toTarget = targetLocal - myLocal;
            toTarget[axis] = 0f; // 법선 축 성분은 제거 — 면 위 접선 방향으로만 이동
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 moveDir = toTarget.normalized;
                myLocal += moveDir * moveSpeed * Time.deltaTime;
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
        }
    }
}
