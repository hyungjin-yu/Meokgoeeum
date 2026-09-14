using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceMobUtils (면 기반 몹 겹침 방지 공용 유틸)
    /// [[CubeFaceLockedMob]]과 [[CubeEnemyBase]] 둘 다 [[ICubeFaceMob]]을 구현하지만 상속
    /// 관계는 아니라서, "다른 몹과 너무 가까우면 밀어낸다"는 로직을 정적 유틸로 뽑아 공유합니다.
    ///
    /// 사용자가 실제 Play 중 여러 몹(원/분/흡/광/평 + 프로토타입 테스트 몹)이 플레이어 한 점으로
    /// 몰려서 서로 겹치는 것을 스크린샷/로그로 리포트("몹들간의 겹침은 있으면 안돼") — 그 대응으로
    /// 추가했습니다.
    /// </summary>
    public static class CubeFaceMobUtils
    {
        /// <summary>
        /// self를 제외한 같은 면 위 모든 ICubeFaceMob 중 separationDistance보다 가까운 것들로부터
        /// 밀어내는 방향/세기를 합산해서 반환합니다. 그대로 transform.position에 더하고 나서
        /// 반드시 면 경계로 다시 clamp하세요(면 밖으로 밀려날 수 있음).
        /// </summary>
        public static Vector3 ComputeSeparation(MonoBehaviour self, Vector3 faceNormal, float separationDistance, float strength)
        {
            if (separationDistance <= 0f) return Vector3.zero;

            Vector3 totalOffset = Vector3.zero;
            var candidates = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var candidate in candidates)
            {
                if (candidate == self) continue;
                if (candidate is not ICubeFaceMob otherMob) continue;
                if (!otherMob.IsSameFaceAs(faceNormal)) continue; // 다른 면 몹은 애초에 안 가까워짐(무시)

                Vector3 delta = self.transform.position - candidate.transform.position;
                delta = Vector3.ProjectOnPlane(delta, faceNormal); // 면 접선 성분만
                float dist = delta.magnitude;

                if (dist >= separationDistance) continue;

                Vector3 pushDir;
                if (dist < 0.001f)
                {
                    // 완전히 겹친 경우 — 임의의 접선 방향으로 밀어냄(0으로 나누기 방지).
                    pushDir = Vector3.ProjectOnPlane(Random.insideUnitSphere, faceNormal).normalized;
                }
                else
                {
                    pushDir = delta / dist;
                }

                totalOffset += pushDir * (separationDistance - dist) * strength;
            }
            return totalOffset;
        }
    }
}
