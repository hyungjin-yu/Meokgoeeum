using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeSurfaceCamera (큐브 표면 추적 카메라 — 프로토타입)
    /// [[CubeSurfaceWalker]]와 짝을 이루는 카메라. 에버플래닛의 "글로브 뷰"처럼, 카메라의
    /// up 벡터를 월드 고정 Vector3.up이 아니라 "플레이어가 지금 서있는 면의 법선"에 맞춰서
    /// 매 순간 다시 계산합니다.
    ///
    /// ⚠️ 2026-09-12 재설계 — 예전엔 이 카메라가 마우스로 독립적인 궤도 방향을 갖고 돌았는데,
    /// 사용자가 원한 건 그게 아니었다("면이 바뀔 때마다 카메라를 다시 돌려줘야 해서 불편하다" →
    /// "어느 면으로 이동하든 W를 누르면 캐릭터 기준으로 앞으로, 카메라랑 같이 움직였으면 좋겠다").
    /// 마우스 좌우 회전을 [[CubeSurfaceWalker]](캐릭터 자체)로 옮기고, 이 카메라는 이제 순수하게
    /// "캐릭터 뒤를 따라가기만" 합니다 — 독자적인 방향을 전혀 안 가짐.
    /// </summary>
    public class CubeSurfaceCamera : MonoBehaviour
    {
        [Tooltip("따라갈 대상입니다.")]
        public CubeSurfaceWalker target;

        [Tooltip("대상 뒤쪽으로 얼마나 떨어질지입니다(줌 1배 기준값).")]
        public float distance = 6f;

        [Tooltip("대상 표면 법선(up) 방향으로 얼마나 띄울지입니다(줌 1배 기준값).")]
        public float height = 3f;

        [Tooltip("카메라가 목표 위치/회전으로 따라가는 속도입니다.")]
        public float followSpeed = 6f;

        [Tooltip("모서리를 넘어 표면 법선이 바뀔 때, 카메라가 새 방향으로 얼마나 부드럽게 돌지입니다.")]
        public float normalSmoothSpeed = 2.5f;

        [Tooltip("시작할 때 마우스 커서를 잠글지 여부입니다. Esc로 잠금을 풀 수 있습니다.")]
        public bool lockCursor = true;

        [Header("줌")]
        [Tooltip("마우스 휠 1틱당 줌 배율이 얼마나 변하는지입니다.")]
        public float zoomSpeed = 0.15f;

        [Tooltip("가장 가까이 줌인했을 때의 배율입니다(distance/height에 곱해짐). 1보다 작아야 줌인이 됩니다 — " +
                 "사용자 리포트(\"플레이어 키가 작아서 나무가 안 보인다\")처럼 캐릭터를 크게 당겨봐야 할 때를 대비.")]
        public float minZoom = 0.4f;

        [Tooltip("가장 멀리 줌아웃했을 때의 배율입니다 — 주변 가로수처럼 화면 밖으로 벗어나는 오브젝트를 보려면 키웁니다.")]
        public float maxZoom = 3f;

        private Vector3 smoothedUp = Vector3.up;
        private float zoom = 1f; // distance/height에 곱해지는 배율. 마우스 휠로 조절.

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (target != null)
                smoothedUp = target.CurrentSurfaceNormal;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 up = target.CurrentSurfaceNormal;

            // 모서리를 넘는 순간 법선 자체는 90도 뚝 바뀌지만(큐브라 당연함), 화면에 보이는
            // 기울기는 smoothedUp을 거쳐서 서서히 돌게 만듭니다.
            smoothedUp = Vector3.Slerp(smoothedUp, up, normalSmoothSpeed * Time.deltaTime).normalized;

            // Esc로 커서 잠금을 풀 수 있게 함(에디터 Game 뷰에서 자동으로 안 풀리는 경우가 있어서 안전장치로 추가).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            else if (lockCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;

            // 마우스 휠 줌 — 사용자 리포트("플레이어 키가 작아서 나무가 안 보인다")에 대한 대응.
            // distance/height에 곱해지는 배율만 바꿔서, 줌아웃하면 캐릭터 주변(가로수 등)까지
            // 넓게 보이고 줌인하면 캐릭터를 크게 당겨볼 수 있게 함.
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
                zoom = Mathf.Clamp(zoom - Mathf.Sign(scroll) * zoomSpeed, minZoom, maxZoom);

            // 캐릭터 forward를 그대로 따라감 — 카메라는 독자적인 방향이 없음.
            Vector3 desiredPos = target.transform.position + smoothedUp * (height * zoom) - target.transform.forward * (distance * zoom);
            transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);

            Quaternion desiredRot = Quaternion.LookRotation(target.transform.position - transform.position, smoothedUp);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, followSpeed * Time.deltaTime);
        }
    }
}
