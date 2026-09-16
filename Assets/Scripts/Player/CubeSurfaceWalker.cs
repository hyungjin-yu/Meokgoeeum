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

        [Tooltip("캐릭터 피벗이 수학적 표면(cubeHalfExtent)에서 법선 방향으로 얼마나 더 떠있어야 하는지입니다. " +
                 "캡슐 피벗은 중심에 있으므로, 보통 캡슐 높이의 절반을 넣어야 발이 표면에 닿습니다. " +
                 "⚠️ 2026-09-12 발견 — 이 값 없이 그냥 cubeHalfExtent로 클램프했더니 캐릭터 피벗이 수학적 " +
                 "표면(도로 메쉬가 놓인 실제 위치)에 그대로 박혀서, 캡슐 절반이 큐브 속에 파묻힌 반구 모양으로만 " +
                 "보였음(\"플레이어 키가 작아서 나무가 안 보인다\" 리포트의 진짜 원인 — 스케일 문제가 아니라 매 " +
                 "프레임 파묻히는 클램프 버그였음).")]
        public float surfaceOffset = 1f;

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

        [Tooltip("벽/문 막음 큐브에 막히는지 검사할 때 쓰는 반경입니다. 2026-09-15 추가 — " +
            "WouldCollide() 참고.")]
        public float collisionCheckRadius = 0.5f;

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
                facingForward = AnyTangentTo(CurrentSurfaceNormal);

            // ⚠️ 2026-09-16 발견 — 여기서 facingForward/CurrentSurfaceNormal 같은 "내부 추적용"
            // 상태만 계산하고 실제 transform.rotation은 안 건드리고 있었음. 캡슐(대칭 메쉬)일 땐
            // 안 움직인 채로 스폰돼도 회전이 잘못돼있는 게 안 보였는데, 유니티짱(비대칭 캐릭터
            // 모델)을 자식으로 붙이고 나서야 스폰 직후(첫 이동 전) 캐릭터가 면 기준으로 옆으로
            // 누운 것처럼 서있는 게 실제로 드러남 — Update()의 Slerp 보정은 "이동을 시작해야"
            // 걸리므로 가만히 서있는 스폰 순간엔 적용 전 상태(에디터에 저장된 회전값, 보통
            // identity)가 그대로 노출됨. 스폰 즉시 올바른 회전으로 맞춰서 첫 이동 전에도 정상적으로
            // 면 위에 서있는 것처럼 보이게 함.
            transform.rotation = Quaternion.LookRotation(facingForward, CurrentSurfaceNormal);
        }

        /// <summary>
        /// ⚠️ 2026-09-14 발견 — "면이 +Z(=Vector3.forward)일 때 facingForward가 제자리에서 굳어서
        /// 캐릭터가 아예 회전/이동을 못 하는" 버그. 원인: 비상 대체 벡터로 항상 Vector3.forward를
        /// 쓰고 있었는데, 하필 큐브 면 법선이 정확히 forward인 면(+Z 면)에서는
        /// ProjectOnPlane(Vector3.forward, normal)이 정확히 0벡터가 됨(자기 자신을 자기 자신의
        /// 법선에 투영하니 완전히 상쇄됨) — 그 뒤로 facingForward가 계속 0벡터에 갇혀서 회전도
        /// 이동도 전부 죽어버림(moveDir/facingRight 둘 다 0이 됨). 두 후보(up, right) 중 normal과
        /// 평행하지 않은 쪽을 골라 안전하게 접선 벡터를 만듭니다 — 축 정렬된 법선은 up/right
        /// 둘 다에 동시에 평행할 수 없으므로 항상 성공합니다.
        /// </summary>
        private static Vector3 AnyTangentTo(Vector3 normal)
        {
            Vector3 candidate = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (candidate.sqrMagnitude < 0.0001f)
                candidate = Vector3.ProjectOnPlane(Vector3.right, normal);
            return candidate.normalized;
        }

        /// <summary>[[CubeFaceZone]]이 플레이어가 자기 존에 들어왔을 때 호출합니다.</summary>
        public void SetCurrentFaceNormal(Vector3 normal) => CurrentSurfaceNormal = normal;

        /// <summary>
        /// 모서리를 자연스럽게 넘어가는 게 아니라(그건 [[CubeFaceZone]]이 SetCurrentFaceNormal로
        /// 처리) 완전히 다른 위치/면으로 순간이동시킬 때 씁니다 — 예: 층 전환
        /// ([[CubeDungeonProgressionManager]]). 2026-09-15 추가 — SetCurrentFaceNormal만 부르면
        /// facingForward/lastNormal은 옛 면 기준 그대로 남아있어서, 바로 다음 프레임에 Update()의
        /// parallel-transport 로직이 "옛 면 → 새 면으로 갑자기 확 꺾인 것"으로 오인해 그 차이만큼
        /// facingForward를 한 프레임 만에 억지로 돌려버림 — 클리어 순간 보고 있던 방향에 따라
        /// 카메라가 크게 홱 도는 걸로 체감됨(사용자 리포트: "화면이 흔들리더니"). 여기서
        /// facingForward/lastNormal을 새 면 기준으로 바로 맞춰두면 그 보정 자체가 필요 없어짐.
        /// </summary>
        public void WarpToFace(Vector3 normal)
        {
            CurrentSurfaceNormal = normal;
            lastNormal = normal;
            facingForward = AnyTangentTo(normal);
        }

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
                facingForward = AnyTangentTo(normal);
            else
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
            float distanceFromSurface = localPos[axis] * Mathf.Sign(normal[axis]) - (cubeHalfExtent + surfaceOffset);
            bool grounded = distanceFromSurface <= 0.05f;

            if (grounded && verticalSpeed < 0f)
                verticalSpeed = 0f;
            else
                verticalSpeed -= gravity * Time.deltaTime;

            // 4. 이동 = 캐릭터가 보는 방향(facingForward/facingRight) 기준 — 카메라 방향과 무관.
            Vector3 moveDir = facingForward * moveInput.y + facingRight * moveInput.x;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // ⚠️ 2026-09-15 추가 — 지금까지 이 워커는 벽/문 막음 큐브([[CubeFaceRoomBuilder]]/
            // [[CubeDungeonRoomKit]]이 짓는 것들)를 전혀 막지 않고 그냥 통과했습니다("방A부터 몹을
            // 안 잡고 게이트를 뚫고 D까지 갈 수 있어" — 사용자 리포트로 발견). 원인은 바로 위 3번
            // 주석 그대로 — 이 프로토타입이 콜라이더 충돌 자체를 안 씀. 접지/면 경계는 좌표
            // 계산으로 흉내냈지만, 벽 차단은 그런 대체 로직이 아예 없었던 것. 완전한 collide-and-
            // slide 대신, "이번 프레임 접선 이동(WASD) 목적지가 막혀있으면 그 성분만 취소"하는
            // 단순한 방식으로 막습니다 — 수직(중력/면 고정) 성분은 그대로 둬서 표면에서 안 떨어짐.
            Vector3 tangentialMotion = moveDir * moveSpeed * Time.deltaTime;
            Vector3 verticalMotion = normal * verticalSpeed * Time.deltaTime;

            if (tangentialMotion.sqrMagnitude > 0.0001f && WouldCollide(transform.position + tangentialMotion))
                tangentialMotion = Vector3.zero;

            transform.position += tangentialMotion + verticalMotion;

            // 콜라이더가 없으므로, 표면 밑으로 파고들거나 면 경계를 벗어나면 직접 되돌려 고정합니다.
            Vector3 afterLocal = transform.position - CubeCenterPos();
            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                afterLocal[a] = Mathf.Clamp(afterLocal[a], -cubeHalfExtent, cubeHalfExtent);
            }
            afterLocal[axis] = (cubeHalfExtent + surfaceOffset) * Mathf.Sign(normal[axis]);
            transform.position = CubeCenterPos() + afterLocal;

            // 5. 캐릭터의 실제 시각적 회전 — 항상 facingForward를 보도록(입력 여부와 무관하게,
            // 마우스로 방금 돌렸을 수 있으므로). up 정렬도 이 한 번의 LookRotation에 같이 반영됨.
            Quaternion lookRot = Quaternion.LookRotation(facingForward, normal);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, reorientSpeed * Time.deltaTime);
        }

        /// <summary>
        /// candidatePos 지점에 이 캐릭터 말고 트리거가 아닌(=단단한) 콜라이더가 있는지 검사합니다.
        /// [[CubeFaceZone]] 같은 면 전환 트리거는 isTrigger라서 걸리지 않고 그대로 통과됩니다 —
        /// 여기서 막는 건 벽/문 막음 큐브처럼 실제로 단단한 지오메트리뿐입니다.
        /// </summary>
        private bool WouldCollide(Vector3 candidatePos)
        {
            var hits = Physics.OverlapSphere(candidatePos, collisionCheckRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (hit.isTrigger) continue;
                return true;
            }
            return false;
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
