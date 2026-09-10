using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// PlayerController (플레이어 이동 제어)
    /// WASD로 이동하되, "카메라가 보는 방향"을 기준으로 이동 방향을 계산합니다.
    /// (16 조작 설계: Camera-relative movement)
    ///
    /// 카메라 자체의 회전(마우스 룩)은 이 스크립트가 아니라 Cinemachine이 담당합니다.
    /// 여기서는 카메라의 현재 방향을 "참고"만 해서 이동 방향을 계산할 뿐입니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("이동 설정")]
        [Tooltip("이동 속도입니다. (Unity units/s)")]
        public float moveSpeed = 5f;

        [Tooltip("캐릭터가 이동 방향을 바라보도록 회전하는 속도입니다. (초당 각도)")]
        public float rotationSpeed = 720f;

        // ⚠️ 2026-09-10: 입력이 들어오는 즉시 moveSpeed로 순간 가속/감속하던 걸(가속 곡선 없음)
        // "A/D나 락온 직후 홱 움직이는 느낌이 든다"는 리포트로 재검토 — 실측으로 position/rotation
        // 자체엔 이상이 없음을 확인했지만(회전만 함), 즉시-최고속도 이동 자체가 스냅처럼 느껴질
        // 수 있어서 목표 속도로 부드럽게 도달하도록 SmoothDamp를 추가했습니다.
        [Tooltip("목표 속도까지 부드럽게 도달하는 데 걸리는 대략적인 시간입니다. (초) 0에 가까울수록 즉시 반응")]
        public float accelerationTime = 0.1f;

        [Header("연결 (References)")]
        [Tooltip("이동 기준이 될 카메라입니다. 비워두면 Start()에서 Camera.main을 자동으로 찾습니다.")]
        public Transform cameraTransform;

        // 내부적으로 사용할 컴포넌트/상태
        private CharacterController cc;
        private PlayerInputActions inputActions;
        private Vector2 moveInput;
        private PlayerDodge dodge; // 구르기 중엔 일반 이동을 넘긴다 (PlayerDodge가 대신 이동시킴)
        private ColorSkillController skills; // 강타(빨강)의 대시 구간 중엔 일반 이동을 넘긴다
        private LockOnController lockOn; // 락온 중엔 이동 방향이 아니라 타겟을 바라봐야 해서, 회전만 넘긴다

        private float verticalVelocity;
        private const float Gravity = -20f;
        private const float GroundedStickVelocity = -2f; // 땅에 붙어있게 하는 최소 하강 속도 (완전히 0이면 살짝 뜨는 느낌이 남음)

        private Vector3 currentVelocity;     // 실제로 cc.Move에 쓰이는, 목표 속도를 서서히 따라가는 속도
        private Vector3 velocitySmoothRef;    // Vector3.SmoothDamp 내부 상태(참조용, 직접 읽지 않음)

        private float speedMultiplier = 1f; // [[BossPo]] 왜곡 공격 등 슬로우 디버프용
        private Coroutine slowRoutine;

        /// <summary>
        /// 같은 GameObject의 BrushWeapon/ColorSkillController/PlayerDodge가 공유해서 쓰는
        /// PlayerInputActions 인스턴스입니다. (예전엔 4개 스크립트가 각자 따로 만들었는데,
        /// 같은 캐릭터에 대해 인스턴스 4개를 유지할 이유가 없어서 여기 한 곳에서만 소유하도록 정리했습니다.)
        /// </summary>
        public PlayerInputActions InputActions => inputActions;

        private void Awake()
        {
            // Unity는 "모든 컴포넌트의 Awake()가 끝난 뒤에야 Start()가 실행된다"를 보장하므로,
            // 인스턴스 생성/Enable()을 Awake()에서 해두면 다른 스크립트들이 자기 Start()에서
            // InputActions를 안전하게 가져다 쓸 수 있습니다.
            inputActions = new PlayerInputActions();

            // 키를 누르는 동안 moveInput에 값 저장
            inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();

            // 키를 떼는 순간 moveInput을 0으로 초기화
            inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

            // 입력 감지 시작
            inputActions.Enable();
        }

        private void Start()
        {
            cc = GetComponent<CharacterController>();
            dodge = GetComponent<PlayerDodge>(); // 없어도(구르기 미부착) 동작은 그대로 — null 체크로 방어
            skills = GetComponent<ColorSkillController>(); // 없어도(색 스킬 미부착) 동작은 그대로 — null 체크로 방어
            lockOn = GetComponent<LockOnController>(); // 없어도(락온 미부착) 동작은 그대로 — null 체크로 방어

            // 카메라가 연결 안 되어 있으면 메인 카메라를 자동으로 찾습니다.
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnDestroy()
        {
            // Enable()과 반드시 짝을 맞춰 Disable()을 호출해야 합니다.
            // 안 그러면 콘솔에 "leak and performance issues" 경고가 뜨고,
            // 내부적으로 할당된 네이티브 리소스가 정리되지 않습니다.
            inputActions?.Disable();
        }

        private void Update()
        {
            Move();
            ApplyGravity();
        }

        /// <summary>
        /// 카메라가 보는 방향을 기준으로 이동합니다.
        /// 카메라가 위/아래를 보고 있어도 캐릭터는 항상 바닥과 평행하게 움직여야 하므로,
        /// 카메라의 앞/오른쪽 방향에서 Y축 성분을 제거(수평면에 투영)한 뒤 사용합니다.
        /// </summary>
        private void Move()
        {
            if (dodge != null && dodge.IsDodging) { currentVelocity = Vector3.zero; return; } // 구르기 중엔 PlayerDodge가 이동을 전담
            if (skills != null && skills.IsDashing) { currentVelocity = Vector3.zero; return; } // 강타 대시 중엔 ColorSkillController가 이동을 전담

            Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);
            bool hasInput = inputDir.sqrMagnitude > 0.0001f;

            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // ⚠️ 2026-09-10: 예전엔 입력이 들어오는 즉시 moveSpeed로 순간 가속하고 떼는 즉시 순간
            // 정지했습니다(가속 곡선 없음). "A/D나 락온 직후 홱 움직이는 느낌"의 원인 중 하나로
            // 보여서, 목표 속도로 부드럽게 도달하도록 SmoothDamp 추가 — 입력이 없어질 때도 서서히
            // 감속합니다(급정지 대신).
            Vector3 targetVelocity = hasInput
                ? (camForward * inputDir.z + camRight * inputDir.x).normalized * moveSpeed * speedMultiplier
                : Vector3.zero;
            currentVelocity = Vector3.SmoothDamp(currentVelocity, targetVelocity, ref velocitySmoothRef, accelerationTime);
            cc.Move(currentVelocity * Time.deltaTime);

            if (!hasInput) return; // 회전은 입력이 있을 때만 갱신 (이동 감속 자체는 위에서 이미 처리됨)

            // 락온 중엔 [[LockOnController]]가 대신 타겟 방향으로 회전시킵니다 — 여기서 이동 방향으로
            // 돌려버리면 두 스크립트가 매 프레임 회전을 다투게 됩니다. 이동(위 cc.Move)은 락온 중에도
            // 그대로 둬서 측면 스트레이프가 가능하게 합니다 ([[16 조작 설계]]).
            if (lockOn == null || !lockOn.IsLockedOn)
            {
                // ⚠️ 2026-09-10 수정: 예전엔 moveDir(이동 방향)을 바라보게 했는데, [[16 조작 설계]]
                // "3인칭 숄더뷰"+"카메라 방향 기준 이동"은 캐릭터가 항상 카메라 정면을 바라봐야
                // 하는 설계입니다. moveDir 기준이면 A/D 스트레이프만 눌러도 카메라 기준 정반대
                // 방향이라 몸이 거의 180도 순간 회전 — 실제 모델이 붙은 뒤 "방향이 홱홱
                // 돌아간다"는 리포트로 발견됨. camForward를 바라보게 해서 스트레이프 중엔
                // 캐릭터가 계속 정면을 유지한 채 옆으로만 이동하도록 수정.
                Quaternion targetRotation = Quaternion.LookRotation(camForward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 일정 시간 동안 이동 속도를 배율만큼 낮춥니다. [[BossPo]]의 "왜곡" 공격 등에서 씁니다.
        /// 이미 슬로우가 걸려있는 중에 또 걸리면 새 지속시간으로 갱신합니다(중첩 대신 갱신).
        /// </summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (slowRoutine != null) StopCoroutine(slowRoutine);
            slowRoutine = StartCoroutine(SlowRoutine(multiplier, duration));
        }

        private IEnumerator SlowRoutine(float multiplier, float duration)
        {
            speedMultiplier = multiplier;
            Debug.Log($"[PlayerController] 슬로우 적용! 배율 {multiplier}, {duration}초간");

            yield return new WaitForSeconds(duration);

            speedMultiplier = 1f;
            slowRoutine = null;
            Debug.Log("[PlayerController] 슬로우 해제.");
        }

        /// <summary>
        /// 중력을 적용합니다. CharacterController는 자체 중력이 없어서 직접 계산해야 합니다.
        /// </summary>
        private void ApplyGravity()
        {
            if (cc.isGrounded)
                verticalVelocity = GroundedStickVelocity;
            else
                verticalVelocity += Gravity * Time.deltaTime;

            cc.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
        }
    }
}
