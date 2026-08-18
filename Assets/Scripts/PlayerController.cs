using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("연결 (References)")]
    [Tooltip("이동 기준이 될 카메라입니다. 비워두면 Start()에서 Camera.main을 자동으로 찾습니다.")]
    public Transform cameraTransform;

    // 내부적으로 사용할 컴포넌트/상태
    private CharacterController cc;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private PlayerDodge dodge; // 구르기 중엔 일반 이동을 넘긴다 (PlayerDodge가 대신 이동시킴)

    private float verticalVelocity;
    private const float Gravity = -20f;
    private const float GroundedStickVelocity = -2f; // 땅에 붙어있게 하는 최소 하강 속도 (완전히 0이면 살짝 뜨는 느낌이 남음)

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        dodge = GetComponent<PlayerDodge>(); // 없어도(구르기 미부착) 동작은 그대로 — null 체크로 방어

        // 카메라가 연결 안 되어 있으면 메인 카메라를 자동으로 찾습니다.
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // 인스턴스 생성
        inputActions = new PlayerInputActions();

        // 키를 누르는 동안 moveInput에 값 저장
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();

        // 키를 떼는 순간 moveInput을 0으로 초기화
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // 입력 감지 시작
        inputActions.Enable();
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
        if (dodge != null && dodge.IsDodging) return; // 구르기 중엔 PlayerDodge가 이동을 전담

        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);
        if (inputDir.sqrMagnitude < 0.0001f) return; // 입력이 없으면 회전도, 이동도 하지 않음

        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * inputDir.z + camRight * inputDir.x).normalized;

        cc.Move(moveDir * moveSpeed * Time.deltaTime);

        // 이동 방향을 부드럽게 바라보도록 캐릭터를 회전시킵니다.
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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
