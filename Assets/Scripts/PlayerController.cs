using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private CharacterController cc;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;

    private float verticalVelocity;
    private float gravity = -20f;

    private void Start()
    {
        cc = GetComponent<CharacterController>();

        // 인스턴스 생성
        inputActions = new PlayerInputActions();

        // 키를 떼는 순간 moveInput에 값 저장
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();

        // 키를 떼는 순간 moveInput을 0으로 초기화
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // 입력 감지 시작
        inputActions.Enable();
    }

    private void Update()
    {
        // 입력된 방향 벡터 계산 (2D 입력을 3D로 변환)
        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        cc.Move(dir * moveSpeed * Time.deltaTime);

        // 중력 적용
        if (cc.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        cc.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
    }
}