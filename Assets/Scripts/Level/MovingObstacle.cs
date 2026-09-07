using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// MovingObstacle (느리게 움직이는 장애물)
    /// [[15 튜토리얼 설계]] 2단계 — "좁은 통로 + 느리게 움직이는 장애물, 장애물을 피해야 통과
    /// 가능 → Space 구르기 자연스럽게 학습"의 구현체입니다. [[28 레벨 디자인 - 인카운터 페이싱과
    /// 블록아웃]] 1층 "입장" 노드에 배치됩니다.
    ///
    /// 대미지나 넉백 로직은 일부러 안 넣었습니다 — 튜토리얼 초입에서 곧바로 패널티를 주면
    /// "환경으로 자연스럽게 유도"(문서 상단 원칙)와 어긋나고, 인카운터 체크리스트 5번
    /// "처음 겪는 실수를 용서하는 여유가 있는가"에도 안 맞습니다. 그냥 통로 폭을 막는 단단한
    /// Collider(Is Trigger 꺼짐)라서, CharacterController가 물리적으로 못 지나가고 밀려나는
    /// 것만으로 충분히 "피해야 한다"는 감각을 줍니다 — 타이밍 맞춰 지나가거나 구르기로 더
    /// 여유 있게 통과하면 됨.
    /// </summary>
    public class MovingObstacle : MonoBehaviour
    {
        [Tooltip("장애물이 좌우(자기 기준 Right축)로 오가는 폭입니다.")]
        public float travelDistance = 3f;

        [Tooltip("왕복 한 번에 걸리는 시간(초)입니다. '느리게'라는 기획 의도에 맞게 넉넉하게 잡으세요.")]
        public float period = 4f;

        private Vector3 startPos;

        private void Awake()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            // 0→1→0으로 왕복하는 값 (PingPong)
            float t = Mathf.PingPong(Time.time / period * 2f, 1f);
            float offset = Mathf.Lerp(-travelDistance * 0.5f, travelDistance * 0.5f, t);
            transform.position = startPos + transform.right * offset;
        }
    }
}
