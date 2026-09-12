using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceZone (큐브 면 트리거 — 프로토타입)
    /// [[CubeSurfaceWalker]]가 "지금 어느 면 위에 있는지"를 매 프레임 좌표 계산(어느 축이
    /// 제일 큰지)으로 판정하던 걸, 실제 면마다 하나씩 배치한 트리거로 바꾼 버전입니다.
    ///
    /// ⚠️ 2026-09-12 발견 — 연속 좌표 계산 방식은 마우스로 카메라를 돌리는 것만으로도(WASD를
    /// 안 눌러도) 면 판정이 아주 가끔 흔들리는 문제가 있었음(사용자 리포트: "평면 위에서
    /// 카메라 회전만 해도 90도씩 홱 도는 것 같다"). 사용자 제안대로 "빈 오브젝트를 트리거로"
    /// 만들어서, 실제로 그 면의 트리거 존에 "들어온 순간"에만 면이 바뀌도록 이산적인
    /// 이벤트 기반으로 바꿨습니다 — 그 안에서 카메라를 아무리 돌려도 면 자체는 안 바뀝니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CubeFaceZone : MonoBehaviour
    {
        [Tooltip("이 존에 들어오면 플레이어의 표면 법선을 이 방향으로 맞춥니다.")]
        public Vector3 faceNormal;

        private void OnTriggerEnter(Collider other)
        {
            var walker = other.GetComponent<CubeSurfaceWalker>();
            if (walker != null)
                walker.SetCurrentFaceNormal(faceNormal);
        }
    }
}
