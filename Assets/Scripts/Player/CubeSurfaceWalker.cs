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
    /// ⚠️ 2026-09-12 재설계 — 처음엔 "카메라가 독립적인 궤도 방향을 갖고 마우스로 도는" 방식
    /// ([[16 조작 설계]] 스타일)이었는데, 사용자가 원한 건 그게 아니라 **"마우스가 캐릭터 자체를
    /// 돌리고, W는 항상 그 방향으로 전진, 카메라는 그냥 캐릭터 뒤를 따라가기만 하는"** 훨씬 단순한
    /// 3인칭 방식이었다("면이 바뀔 때마다 카메라를 다시 돌려줘야 해서 불편하다"는 리포트).
    /// 그래서 마우스 좌우 회전을 카메라([[CubeSurfaceCamera]])가 아니라 여기(캐릭터 자체의
    /// facingForward)로 옮겼다 — 이제 카메라는 순수하게 캐릭터를 뒤따라가기만 한다.
    ///
    /// ⚠️ 2026-09-12 발견 ① — 처음엔 CharacterController.Move()를 썼는데, 면을 넘어가는 순간
    /// (transform.up이 (0,1,0)→(0,0,1)로 바뀌는 순간) 플레이어가 의도와 다른 축(월드 Y)으로
    /// 계속 미끄러지는 버그를 리플렉션 스텝 테스트로 실측 확인했다. CharacterController는
    /// 슬로프/스텝 처리를 위해 내부적으로 "자기 컴포넌트의 위 방향"에 대한 가정을 갖고 있는데,
    /// 이 프로토타입처럼 매 프레임 임의 축으로 재정렬하는 용도는 지원하지 않는 것으로 보인다
    /// (Unity 커뮤니티에서도 잘 알려진 한계). 그래서 프로토타입 단계에서는 CharacterController
    /// 없이 transform.position을 직접 옮기는 방식으로 우회했다.
    ///
    /// ⚠️ 2026-09-12 발견 ② — "지금 어느 면 위에 있는지"를 매 프레임 좌표 계산으로 판정했더니
    /// 같은 면 위에서도 판정이 흔들리는 문제가 있었다. 실제 면마다 [[CubeFaceZone]] 트리거를
    /// 배치해서, "그 존에 실제로 들어온 순간"에만 면이 바뀌는 이산적 이벤트 기반으로 바꿨다.
    ///
    /// ⚠️ 2026-09-12 발견 ③ — 면과 면 사이를 빠르게 왕복하면 캐릭터가 큐브에서 떨어져 나가는
    /// 버그(접선 두 축을 전혀 clamp 안 해서 코너 근처에서 면 경계를 넘어버림) — 접선 축도
    /// 항상 면 범위 안으로 clamp해서 해결.
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

        [Tooltip("모서리를 넘을 때 자세(up 벡터)가 새 면 방향으로 돌아가는 속도입니다.")]
        public float reorientSpeed = 4f;

        [Tooltip("마우스 좌우로 캐릭터를 돌리는 감도입니다. 마우스 델타(픽셀)에 직접 곱해지므로 값이 작아야 정상입니다.")]
        public float mouseSensitivity = 0.15f;

        [Tooltip("마우스 입력을 얼마나 부드럽게(저역 통과) 만들지입니다.")]
        public float mouseSmoothing = 15f;

        [Tooltip("카메라가 참조할 '지금 이 순간의 표면 법선(up 방향)'입니다. [[CubeFaceZone]] 트리거를 " +
                 "지날 때만 바뀝니다 — 매 프레임 좌표로 재계산하지 않습니다.")]
        public Vector3 CurrentSurfaceNormal { get; private set; } = Vector3.up;

        private Vector2 moveInput;
        private float verticalSpeed; // 표면 법선 방향 기준 속도(음수=표면 쪽으로 떨어지는 중)
        private Vector3 facingForward; // 캐릭터가 실제로 바라보는 접선 방향 — 마우스로 회전시킴
        private Vector3 lastNormal; // 면이 바뀌었는지 감지하기 위한 직전 프레임 법선
        private float smoothedMouseX;

        private void Start()
        {
            CurrentSurfaceNormal = EstimateSurfaceNormalFromPosition();
            lastNormal = CurrentSurfaceNormal;
            facingForward = Vector3.ProjectOnPlane(transform.forward, CurrentSurfaceNormal).normalized;
            if (facingForward.sqrMagnitude < 0.0001f)
                facingForward = Vector3.ProjectOnPlane(Vector3.forward, CurrentSurfaceNormal).normalized;
        }

        /// <summary>[[CubeFaceZone]]이 플레이어가 자기 존에 들어왔을 때 호출합니다.</summary>
        public void SetCurrentFaceNormal(Vector3 normal) => CurrentSurfaceNormal = normal;

        /// <summary>리플렉션 스텝 테스트에서 값을 직접 주입할 때 씁니다.</summary>
        public void SetMoveInput(Vector2 input) => moveInput = input;

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

            // 1. 면이 바뀐 순간엔 facingForward를 "평면에 납작하게 투영"하지 않고, 옛 법선에서
            // 새 법선으로 가는 회전을 facingForward에도 똑같이 적용해서 옮깁니다(parallel
            // transport) — 이렇게 해야 모서리를 곧장 가로질러 걸을 때 방향이 왜곡되지 않고
            // "쭉 걸어온 방향 그대로" 이어집니다.
            // ⚠️ 2026-09-12 발견 — 처음엔 매 프레임 ProjectOnPlane으로 재투영했는데, 이건 벡터를
            // 새 평면에 납작하게 눌러버리는 것뿐이라 모서리에서 걷는 방향이 은근히 틀어질 수 있음
            // (사용자 리포트: "카메라를 다시 돌려줘야 한다" — 실제로는 재설계 전/후 둘 다 이
            // 문제가 있었음, 방향을 소유하는 주체를 바꾼 것만으론 안 고쳐졌던 것).
            if (Vector3.Dot(normal, lastNormal) < 0.999f)
            {
                Quaternion transport = Quaternion.FromToRotation(lastNormal, normal);
                facingForward = transport * facingForward;
                lastNormal = normal;
            }
            facingForward = Vector3.ProjectOnPlane(facingForward, normal); // 부동소수점 오차만 가볍게 보정
            if (facingForward.sqrMagnitude < 0.0001f)
                facingForward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            facingForward.Normalize();

            // 2. 마우스 좌우로 캐릭터 자체를 돌림 — 카메라가 아니라 캐릭터가 도는 거라서,
            // W를 누르면 항상 "지금 보고 있는 방향"으로 전진하고, 카메라는 그냥 뒤따라오기만 하면 됨.
            float rawMouseX = Mouse.current != null ? Mouse.current.delta.x.ReadValue() : 0f;
            smoothedMouseX = Mathf.Lerp(smoothedMouseX, rawMouseX, mouseSmoothing * Time.deltaTime);
            facingForward = Quaternion.AngleAxis(smoothedMouseX * mouseSensitivity, normal) * facingForward;

            Vector3 facingRight = Vector3.Cross(normal, facingForward);

            // 3. 접지 판정 — 콜라이더 충돌 없이(이 프로토타입은 CharacterController를 안 씀)
            // "표면까지 남은 거리"를 직접 계산해서 판정합니다.
            Vector3 localPos = transform.position - CubeCenterPos();
            int axis = Mathf.Abs(normal.x) > 0.5f ? 0 : (Mathf.Abs(normal.y) > 0.5f ? 1 : 2);
            float distanceFromSurface = localPos[axis] * Mathf.Sign(normal[axis]) - cubeHalfExtent;
            bool grounded = distanceFromSurface <= 0.05f;

            if (grounded && verticalSpeed < 0f)
                verticalSpeed = 0f;
            else
                verticalSpeed -= gravity * Time.deltaTime;

            // 4. 이동 = 캐릭터가 보는 방향(facingForward/facingRight) 기준 — 카메라 방향과 무관.
            Vector3 moveDir = facingForward * moveInput.y + facingRight * moveInput.x;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            Vector3 motion = moveDir * moveSpeed + normal * verticalSpeed;
            transform.position += motion * Time.deltaTime;

            // 콜라이더가 없으므로, 표면 밑으로 파고들거나 면 경계를 벗어나면 직접 되돌려 고정합니다.
            Vector3 afterLocal = transform.position - CubeCenterPos();
            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                afterLocal[a] = Mathf.Clamp(afterLocal[a], -cubeHalfExtent, cubeHalfExtent);
            }
            afterLocal[axis] = cubeHalfExtent * Mathf.Sign(normal[axis]);
            transform.position = CubeCenterPos() + afterLocal;

            // 5. 캐릭터의 실제 시각적 회전 — 항상 facingForward를 보도록(입력 여부와 무관하게,
            // 마우스로 방금 돌렸을 수 있으므로). up 정렬도 이 한 번의 LookRotation에 같이 반영됨.
            Quaternion lookRot = Quaternion.LookRotation(facingForward, normal);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, reorientSpeed * Time.deltaTime);
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
