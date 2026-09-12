using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeSurfaceCamera (큐브 표면 추적 카메라 — 프로토타입)
    /// [[CubeSurfaceWalker]]와 짝을 이루는 카메라. 에버플래닛의 "글로브 뷰"처럼, 카메라의
    /// up 벡터를 월드 고정 Vector3.up이 아니라 "플레이어가 지금 서있는 면의 법선"에 맞춰서
    /// 매 순간 다시 계산합니다 — Cinemachine은 기본적으로 월드 up이 고정이라는 전제가 강해서,
    /// 이 프로토타입에서는 검증이 끝나기 전까지 별도의 간단한 스크립트로 분리했습니다.
    ///
    /// ⚠️ 2026-09-12 추가 — 마우스 좌우 회전. 처음엔 카메라가 플레이어의 forward를 그대로
    /// 따라가기만 해서(플레이어 몸 방향 = 이동 방향이 곧 카메라 방향), 사용자가 직접 카메라를
    /// 돌릴 수 없었다. 대신 카메라가 "자기만의 궤도 방향(orbitForward)"을 따로 갖고, 마우스
    /// X 이동으로 그 방향을 표면 법선 축을 기준으로 돌린다 — [[PlayerController]]가 이미 쓰는
    /// "카메라 기준 이동"([[16 조작 설계]])과 같은 원리다: CubeSurfaceWalker는 여전히 이
    /// 카메라의 forward/right를 읽어서 이동 방향을 계산하므로, 카메라를 돌리면 이동 방향도
    /// 자연히 같이 돈다.
    ///
    /// 모서리를 넘어서 표면 법선이 바뀌면, orbitForward를 그 순간의 새 접선 평면에 다시
    /// 투영해서 방향을 유지한 채 축만 갈아탄다 — 그래서 면이 바뀌어도 카메라가 갑자기
    /// 홱 돌아가지 않는다.
    /// </summary>
    public class CubeSurfaceCamera : MonoBehaviour
    {
        [Tooltip("따라갈 대상입니다.")]
        public CubeSurfaceWalker target;

        [Tooltip("대상 뒤쪽으로 얼마나 떨어질지입니다.")]
        public float distance = 6f;

        [Tooltip("대상 표면 법선(up) 방향으로 얼마나 띄울지입니다.")]
        public float height = 3f;

        [Tooltip("카메라가 목표 위치/회전으로 따라가는 속도입니다.")]
        public float followSpeed = 6f;

        [Tooltip("마우스 좌우 회전 감도입니다. 마우스 델타(픽셀)에 직접 곱해지므로 값이 작아야 정상입니다 — 3처럼 큰 값은 프레임당 수십 도씩 홱홱 돌아서 멀미가 남.")]
        public float mouseSensitivity = 0.15f;

        [Tooltip("마우스 입력을 얼마나 부드럽게(저역 통과) 만들지입니다. 값이 클수록 즉각적이고, 작을수록 부드럽지만 약간 늦게 반응합니다.")]
        public float mouseSmoothing = 15f;

        [Tooltip("모서리를 넘어 표면 법선이 바뀔 때, 카메라가 새 방향으로 얼마나 부드럽게 돌지입니다. " +
                 "낮을수록 천천히 돌아서 '뚝' 끊기는 느낌이 줄어듭니다(대신 잠깐 기울어진 채로 이동함).")]
        public float normalSmoothSpeed = 2.5f;

        private float smoothedMouseX;
        private Vector3 smoothedUp = Vector3.up; // 카메라 위치/회전 계산에만 씀 — 궤도 방향(orbitForward) 투영은 실제 순간 법선을 그대로 씀

        [Tooltip("시작할 때 마우스 커서를 잠글지 여부입니다. 프로토타입이라 잠금 해제 키는 아직 없습니다 — 필요하면 Esc로 Play 모드를 끝내세요.")]
        public bool lockCursor = true;

        // 궤도 방향 — 플레이어 몸 방향과 무관하게 카메라가 독립적으로 소유합니다.
        private Vector3 orbitForward;

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (target != null)
                orbitForward = Vector3.ProjectOnPlane(target.transform.forward, target.CurrentSurfaceNormal).normalized;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 up = target.CurrentSurfaceNormal;

            // 카메라 위치/회전에 쓰는 "체감 위 방향"은 실제 법선을 곧바로 따라가지 않고 천천히 회전시킵니다
            // — 모서리를 넘는 순간 법선 자체는 90도 뚝 바뀌지만(큐브라 당연함), 화면에 보이는 기울기는
            // 이 smoothedUp을 거쳐서 서서히 돌게 만드는 것. 반면 orbitForward 투영은 정확도를 위해
            // 실제(raw) 법선을 그대로 씁니다.
            smoothedUp = Vector3.Slerp(smoothedUp, up, normalSmoothSpeed * Time.deltaTime).normalized;

            // 면이 바뀌었을 수 있으니, 궤도 방향을 지금 표면의 접선 평면에 다시 투영해서 정합성을 유지.
            orbitForward = Vector3.ProjectOnPlane(orbitForward, up);
            if (orbitForward.sqrMagnitude < 0.0001f) // 투영 결과가 거의 0이면(방향이 법선과 거의 평행했던 경우) 임시로 아무 접선 방향이나 잡음
                orbitForward = Vector3.ProjectOnPlane(Vector3.forward, up);
            orbitForward.Normalize();

            // Esc로 커서 잠금을 풀 수 있게 함(에디터 Game 뷰에서 자동으로 안 풀리는 경우가 있어서 안전장치로 추가).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            else if (lockCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked; // 게임 뷰 클릭하면 다시 잠금

            float rawMouseX = Mouse.current != null ? Mouse.current.delta.x.ReadValue() : 0f;
            smoothedMouseX = Mathf.Lerp(smoothedMouseX, rawMouseX, mouseSmoothing * Time.deltaTime);
            orbitForward = Quaternion.AngleAxis(smoothedMouseX * mouseSensitivity, up) * orbitForward;

            Vector3 desiredPos = target.transform.position + smoothedUp * height - orbitForward * distance;
            transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);

            Quaternion desiredRot = Quaternion.LookRotation(target.transform.position - transform.position, smoothedUp);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, followSpeed * Time.deltaTime);
        }
    }
}
