using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeSurfaceWalker (큐브 표면 보행 — 프로토타입)
    /// [[05 맵 시스템 - 큐브 구조]]의 "이 세계는 큐브 1개"를 문자 그대로 구현해보는 실험.
    /// 에버플래닛의 "글로브 뷰"(구형 행성 표면을 걷는 카메라 시점)를 구 대신 큐브에 적용한다 —
    /// 플레이어가 큐브 바깥 표면을 걷다가 모서리를 넘으면 중력이 그 면의 새 방향으로 돌아간다.
    ///
    /// 기존 PlayerController는 월드 고정 Y축 중력(CharacterController + Update에서 -Y)을
    /// 전제로 하기 때문에 이 프로토타입에는 안 맞아서, 별도 스크립트로 분리했다. 나중에
    /// 실제 게임에 붙일지는 이 프로토타입으로 "느낌"부터 검증한 뒤 결정한다.
    ///
    /// ⚠️ 2026-09-12 발견 ① — 처음엔 CharacterController.Move()를 썼는데, 면을 넘어가는 순간
    /// (transform.up이 (0,1,0)→(0,0,1)로 바뀌는 순간) 플레이어가 의도와 다른 축(월드 Y)으로
    /// 계속 미끄러지는 버그를 리플렉션 스텝 테스트로 실측 확인했다. CharacterController는
    /// 슬로프/스텝 처리를 위해 내부적으로 "자기 컴포넌트의 위 방향"에 대한 가정을 갖고 있는데,
    /// 이 프로토타입처럼 매 프레임 임의 축으로 재정렬하는 용도는 지원하지 않는 것으로 보인다
    /// (Unity 커뮤니티에서도 잘 알려진 한계). 그래서 프로토타입 단계에서는 CharacterController
    /// 없이 transform.position을 직접 옮기는 방식으로 우회했다 — 충돌 처리가 필요해지면 그때
    /// Rigidbody 기반이나 커스텀 캡슐 캐스트로 다시 검토.
    ///
    /// ⚠️ 2026-09-12 발견 ② — "지금 어느 면 위에 있는지"를 매 프레임 "어느 축 좌표가 제일
    /// 큰지"로 계산했더니, 같은 면 위에서 카메라만 돌려도(WASD 없이) 판정이 흔들리는 문제가
    /// 있었다(사용자 리포트). 사용자 제안대로 실제 면마다 [[CubeFaceZone]] 트리거를 배치해서,
    /// "그 존에 실제로 들어온 순간"에만 면이 바뀌는 이산적 이벤트 기반으로 바꿨다 — 같은 면
    /// 위에서는 무슨 짓을 해도 면 판정 자체가 안 바뀐다.
    /// </summary>
    public class CubeSurfaceWalker : MonoBehaviour
    {
        [Tooltip("걷고 있는 큐브의 중심입니다.")]
        public Transform cubeCenter;

        [Tooltip("큐브 한 변 길이의 절반입니다. 콜라이더 충돌 없이 표면에 붙어있게 하는 데 씁니다.")]
        public float cubeHalfExtent = 5f;

        [Tooltip("이동 속도입니다.")]
        public float moveSpeed = 6f;

        [Tooltip("중력 가속도입니다. (표면을 향하는 방향으로 작용)")]
        public float gravity = 20f;

        [Tooltip("모서리를 넘을 때 플레이어 자세(up 벡터)가 새 면 방향으로 돌아가는 속도입니다. " +
                 "너무 빠르면 뚝뚝 끊겨 보이고, 너무 느리면 한동안 벽을 걷는 것처럼 보입니다.")]
        public float reorientSpeed = 4f;

        [Tooltip("카메라가 참조할 '지금 이 순간의 표면 법선(up 방향)'입니다. [[CubeFaceZone]] 트리거를 " +
                 "지날 때만 바뀝니다 — 매 프레임 좌표로 재계산하지 않습니다.")]
        public Vector3 CurrentSurfaceNormal { get; private set; } = Vector3.up;

        private Vector2 moveInput;
        private float verticalSpeed; // 표면 법선 방향 기준 속도(음수=표면 쪽으로 떨어지는 중)
        private Transform cameraTransform;

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            // 트리거를 아직 하나도 안 지났을 초기 상태를 위해, 시작할 때 한 번만 좌표로 면을 추정합니다.
            CurrentSurfaceNormal = EstimateSurfaceNormalFromPosition();
        }

        /// <summary>[[CubeFaceZone]]이 플레이어가 자기 존에 들어왔을 때 호출합니다.</summary>
        public void SetCurrentFaceNormal(Vector3 normal) => CurrentSurfaceNormal = normal;

        /// <summary>
        /// 리플렉션 스텝 테스트에서 값을 직접 주입할 때 씁니다. 실제 플레이에서는 매 프레임
        /// ReadKeyboardInput()이 덮어쓰므로, 테스트 코드에서 Update() 호출 "직전"에 매번
        /// 새로 호출해야 원하는 값이 유지됩니다.
        /// </summary>
        public void SetMoveInput(Vector2 input) => moveInput = input;

        /// <summary>
        /// ⚠️ 2026-09-12 발견 — 프로토타입 첫 실행 때 실제 키보드 입력이 하나도 연결 안 되어
        /// 있어서 플레이어가 전혀 안 움직이는 문제가 있었음(리플렉션 테스트용 SetMoveInput()만
        /// 만들어두고 진짜 입력 연결을 깜빡함). 이 프로젝트는 Active Input Handling이
        /// "Input System Package (New)"뿐이라(레거시 Input 클래스는 아예 동작 안 함) WASD를
        /// Keyboard.current로 직접 읽습니다 — 프로토타입이라 별도 .inputactions 없이 최소한으로.
        /// </summary>
        private void ReadKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = 0f, y = 0f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.wKey.isPressed) y += 1f;
            if (kb.sKey.isPressed) y -= 1f;
            moveInput = new Vector2(x, y);
        }

        private void Update()
        {
            ReadKeyboardInput();

            Vector3 normal = CurrentSurfaceNormal; // 트리거로만 바뀜 — 여기서 재계산하지 않음

            // 1. 자세를 표면 법선 쪽으로 서서히 정렬 (모서리를 넘어도 뚝 끊기지 않게 보간)
            Quaternion uprightTarget = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, uprightTarget, reorientSpeed * Time.deltaTime);

            // 2. 접지 판정 — 콜라이더 충돌 없이(이 프로토타입은 CharacterController를 안 씀)
            // "표면까지 남은 거리"를 직접 계산해서 판정합니다.
            Vector3 localPos = transform.position - CubeCenterPos();
            int axis = Mathf.Abs(normal.x) > 0.5f ? 0 : (Mathf.Abs(normal.y) > 0.5f ? 1 : 2);
            float distanceFromSurface = localPos[axis] * Mathf.Sign(normal[axis]) - cubeHalfExtent;
            bool grounded = distanceFromSurface <= 0.05f;

            if (grounded && verticalSpeed < 0f)
                verticalSpeed = 0f;
            else
                verticalSpeed -= gravity * Time.deltaTime;

            // 3. 입력을 "지금 서있는 면의 접선 평면"에 투영해서 이동 방향 계산
            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : transform.right;

            Vector3 tangentForward = Vector3.ProjectOnPlane(camForward, normal).normalized;
            Vector3 tangentRight = Vector3.ProjectOnPlane(camRight, normal).normalized;

            Vector3 moveDir = (tangentForward * moveInput.y + tangentRight * moveInput.x);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            Vector3 motion = moveDir * moveSpeed + normal * verticalSpeed; // verticalSpeed는 음수(표면 쪽으로 당김)
            transform.position += motion * Time.deltaTime;

            // 콜라이더가 없으므로, 표면 밑으로 파고들면 직접 표면 위치로 되돌려 고정합니다
            // (실측 결과: 속도를 0으로만 맞추면 그 프레임에 이미 파고든 만큼은 안 돌아와서
            // 누적되면 결국 큐브를 뚫고 지나가버림 — 위치 자체를 매 프레임 clamp해야 함).
            Vector3 afterLocal = transform.position - CubeCenterPos();
            float afterDist = afterLocal[axis] * Mathf.Sign(normal[axis]);
            if (afterDist < cubeHalfExtent)
            {
                afterLocal[axis] = cubeHalfExtent * Mathf.Sign(normal[axis]);
                transform.position = CubeCenterPos() + afterLocal;
            }

            // 4. 이동 방향을 바라보도록 회전(입력이 있을 때만) — 표면에 붙은 상태를 유지하며 yaw만 갱신
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(moveDir, normal);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, reorientSpeed * Time.deltaTime);
            }
        }

        /// <summary>Start()에서 트리거를 지나기 전 초기 면을 추정하는 용도로만 씁니다(1회성).</summary>
        private Vector3 EstimateSurfaceNormalFromPosition()
        {
            Vector3 local = transform.position - CubeCenterPos();
            Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));

            if (abs.x >= abs.y && abs.x >= abs.z)
                return new Vector3(Mathf.Sign(local.x), 0f, 0f);
            if (abs.y >= abs.x && abs.y >= abs.z)
                return new Vector3(0f, Mathf.Sign(local.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(local.z));
        }

        private Vector3 CubeCenterPos() => cubeCenter != null ? cubeCenter.position : Vector3.zero;
    }
}
