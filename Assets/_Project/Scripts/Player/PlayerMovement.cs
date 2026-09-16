using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
[RequireComponent(typeof(PlayerInput))] // 플레이어 입력 필수 지정
public sealed class PlayerMovement : MonoBehaviour // 플레이어 이동 관리자
{
    [Header("Movement")] // 이동 설정 구분
    [SerializeField] private float walkSpeed = 5f; // 기본 이동 속도
    [SerializeField] private float sprintSpeed = 8f; // 달리기 속도
    [SerializeField] private float crouchSpeed = 2.5f; // 앉기 이동 속도
    [SerializeField] private float acceleration = 24f; // 이동 가속도
    [SerializeField] private float deceleration = 30f; // 이동 감속도
    [SerializeField] private float airControl = 0.5f; // 공중 제어 비율
    [SerializeField] private float rotationSmoothTime = 0.08f; // 회전 보간 시간

    [Header("Jump")] // 점프 설정 구분
    [SerializeField] private float jumpHeight = 1.6f; // 점프 높이
    [SerializeField] private float gravity = -25f; // 중력 가속도
    [SerializeField] private float groundedGravity = -2f; // 지면 고정 중력
    [SerializeField] private float coyoteTime = 0.12f; // 코요테 시간
    [SerializeField] private float jumpBufferTime = 0.12f; // 점프 입력 저장 시간

    [Header("Crouch")] // 앉기 설정 구분
    [SerializeField] private float standingHeight = 2f; // 서기 높이
    [SerializeField] private float crouchingHeight = 1.2f; // 앉기 높이
    [SerializeField] private LayerMask standingObstructionMask = ~0; // 일어서기 장애물 마스크

    [Header("Recovery")] // 복귀 설정 구분
    [SerializeField] private float fallRespawnY = -20f; // 추락 복귀 높이

    private CharacterController controller; // 캐릭터 컨트롤러 참조
    private PlayerInput playerInput; // 플레이어 입력 참조
    private Camera movementCamera; // 이동 기준 카메라
    private InputAction moveAction; // 이동 입력 액션
    private InputAction jumpAction; // 점프 입력 액션
    private InputAction crouchAction; // 앉기 입력 액션
    private InputAction sprintAction; // 달리기 입력 액션
    private Vector3 horizontalVelocity; // 현재 수평 속도
    private float verticalVelocity; // 현재 수직 속도
    private float rotationVelocity; // 현재 회전 보간값
    private float coyoteTimer; // 코요테 남은 시간
    private float jumpBufferTimer; // 점프 입력 남은 시간
    private Vector3 standingCenter; // 서기 중심 위치
    private Vector3 spawnPosition; // 시작 위치
    private Quaternion spawnRotation; // 시작 회전
    private bool isCrouching; // 현재 앉기 상태
    private bool movementEnabled = true; // 이동 활성 상태

    public bool IsGrounded => controller != null && controller.isGrounded; // 지면 상태 읽기
    public bool IsCrouching => isCrouching; // 앉기 상태 읽기
    public bool MovementEnabled => movementEnabled; // 이동 활성 상태 읽기
    public Vector3 HorizontalVelocity => horizontalVelocity; // 수평 속도 읽기
    public float VerticalVelocity // 수직 속도 읽기쓰기
    {
        get => verticalVelocity; // 수직 속도 반환
        set => verticalVelocity = value; // 수직 속도 저장
    }
    public PlayerInput PlayerInput => playerInput; // 입력 참조 읽기
    public Camera MovementCamera => movementCamera; // 이동 카메라 읽기
    public CharacterController Controller => controller; // 캐릭터 컨트롤러 읽기

    private void Awake() // 초기 참조 설정
    {
        controller = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        playerInput = GetComponent<PlayerInput>(); // 플레이어 입력 조회
        movementCamera = Camera.main; // 메인 카메라 조회
        standingCenter = controller.center; // 기본 중심 저장
        standingHeight = Mathf.Max(standingHeight, controller.radius * 2f); // 서기 높이 보정
        crouchingHeight = Mathf.Clamp(crouchingHeight, controller.radius * 2f, standingHeight); // 앉기 높이 보정
        controller.height = standingHeight; // 초기 높이 적용
        controller.center = standingCenter; // 초기 중심 적용
        spawnPosition = transform.position; // 시작 위치 저장
        spawnRotation = transform.rotation; // 시작 회전 저장
        ResolveActions(); // 입력 액션 연결
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveActions(); // 입력 액션 재연결
    }

    private void Update() // 매 프레임 이동 처리
    {
        if (!movementEnabled) // 이동 활성 상태 확인
        {
            HandleFallRespawn(); // 추락 복귀 처리
            return; // 일반 이동 처리 중단
        }

        UpdateGroundTimers(); // 지면 관련 시간 갱신
        UpdateJumpBuffer(); // 점프 입력 저장 갱신
        HandleCrouch(); // 앉기 처리
        HandleHorizontalMovement(); // 수평 이동 처리
        HandleJump(); // 점프 처리
        ApplyGravity(); // 중력 처리
        MoveCharacter(); // 최종 캐릭터 이동
        HandleFallRespawn(); // 추락 복귀 처리
    }

    public void Configure(Camera targetCamera) // 이동 기준 카메라 지정
    {
        movementCamera = targetCamera; // 카메라 참조 저장
    }

    public void SetSpawnPoint(Vector3 position, Quaternion rotation) // 복귀 위치 갱신
    {
        spawnPosition = position; // 복귀 위치 저장
        spawnRotation = rotation; // 복귀 회전 저장
    }

    public void SetMovementEnabled(bool enabled) // 이동 활성 상태 변경
    {
        movementEnabled = enabled; // 이동 활성 상태 저장

        if (!enabled) // 이동 비활성 확인
        {
            horizontalVelocity = Vector3.zero; // 수평 속도 초기화
            jumpBufferTimer = 0f; // 점프 저장 초기화
        }
    }

    public void SetHorizontalVelocity(Vector3 velocity) // 수평 속도 강제 지정
    {
        horizontalVelocity = velocity; // 수평 속도 저장
    }

    public Vector2 GetMoveInput() // 현재 이동 입력 조회
    {
        return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero; // 이동 입력 반환
    }

    public bool WasJumpPressedThisFrame() // 점프 입력 확인
    {
        return jumpAction != null && jumpAction.WasPressedThisFrame(); // 점프 입력 여부 반환
    }

    public bool IsCrouchHeld() // 앉기 입력 확인
    {
        return crouchAction != null && crouchAction.IsPressed(); // 앉기 입력 여부 반환
    }

    private void ResolveActions() // 입력 액션 연결
    {
        if (playerInput == null || playerInput.actions == null) // 입력 에셋 확인
        {
            return; // 연결 중단
        }

        InputActionMap actionMap = playerInput.actions.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        if (actionMap == null) // 액션 맵 누락 확인
        {
            return; // 연결 중단
        }

        moveAction = actionMap.FindAction("Move", false); // 이동 액션 조회
        jumpAction = actionMap.FindAction("Jump", false); // 점프 액션 조회
        crouchAction = actionMap.FindAction("Crouch", false); // 앉기 액션 조회
        sprintAction = actionMap.FindAction("Sprint", false); // 달리기 액션 조회
    }

    private void UpdateGroundTimers() // 지면 관련 시간 갱신
    {
        if (controller.isGrounded) // 지면 접촉 확인
        {
            coyoteTimer = coyoteTime; // 코요테 시간 초기화

            if (verticalVelocity < 0f) // 하강 속도 확인
            {
                verticalVelocity = groundedGravity; // 지면 고정 중력 적용
            }
        }
        else // 공중 상태 처리
        {
            coyoteTimer -= Time.deltaTime; // 코요테 시간 감소
        }
    }

    private void UpdateJumpBuffer() // 점프 입력 저장 갱신
    {
        if (jumpAction != null && jumpAction.WasPressedThisFrame()) // 점프 입력 확인
        {
            jumpBufferTimer = jumpBufferTime; // 점프 입력 시간 저장
        }
        else // 신규 입력 없음 처리
        {
            jumpBufferTimer -= Time.deltaTime; // 저장 시간 감소
        }
    }

    private void HandleHorizontalMovement() // 수평 이동 처리
    {
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero; // 이동 입력 조회
        input = Vector2.ClampMagnitude(input, 1f); // 대각선 입력 보정

        Vector3 cameraForward = movementCamera != null ? movementCamera.transform.forward : transform.forward; // 카메라 전방 조회
        Vector3 cameraRight = movementCamera != null ? movementCamera.transform.right : transform.right; // 카메라 우측 조회
        cameraForward.y = 0f; // 전방 수직 성분 제거
        cameraRight.y = 0f; // 우측 수직 성분 제거
        cameraForward.Normalize(); // 전방 벡터 정규화
        cameraRight.Normalize(); // 우측 벡터 정규화

        Vector3 desiredDirection = cameraForward * input.y + cameraRight * input.x; // 카메라 기준 이동 방향 계산
        desiredDirection = desiredDirection.sqrMagnitude > 1f ? desiredDirection.normalized : desiredDirection; // 이동 방향 크기 보정

        bool sprintHeld = sprintAction != null && sprintAction.IsPressed(); // 달리기 입력 확인
        float targetSpeed = isCrouching ? crouchSpeed : sprintHeld ? sprintSpeed : walkSpeed; // 현재 목표 속도 결정
        Vector3 desiredVelocity = desiredDirection * targetSpeed; // 목표 수평 속도 계산

        float controlMultiplier = controller.isGrounded ? 1f : airControl; // 지상 공중 제어 비율 선택
        float rate = desiredDirection.sqrMagnitude > 0.001f ? acceleration : deceleration; // 가속 감속 비율 선택
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * controlMultiplier * Time.deltaTime); // 수평 속도 보간

        if (desiredDirection.sqrMagnitude > 0.001f) // 이동 방향 존재 확인
        {
            float targetAngle = Mathf.Atan2(desiredDirection.x, desiredDirection.z) * Mathf.Rad2Deg; // 목표 회전각 계산
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime); // 회전각 부드럽게 계산
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f); // 캐릭터 회전 적용
        }
    }

    private void HandleJump() // 점프 처리
    {
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f || isCrouching) // 점프 가능 조건 확인
        {
            return; // 점프 처리 중단
        }

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); // 점프 초기 속도 계산
        jumpBufferTimer = 0f; // 점프 입력 저장 소모
        coyoteTimer = 0f; // 코요테 시간 소모
    }

    private void ApplyGravity() // 중력 처리
    {
        if (!controller.isGrounded || verticalVelocity > 0f) // 공중 또는 상승 상태 확인
        {
            verticalVelocity += gravity * Time.deltaTime; // 중력 누적
        }
    }

    private void MoveCharacter() // 최종 이동 처리
    {
        Vector3 frameVelocity = horizontalVelocity + Vector3.up * verticalVelocity; // 전체 이동 속도 결합
        controller.Move(frameVelocity * Time.deltaTime); // 캐릭터 컨트롤러 이동
    }

    private void HandleCrouch() // 앉기 처리
    {
        bool crouchHeld = crouchAction != null && crouchAction.IsPressed(); // 앉기 입력 확인

        if (crouchHeld && !isCrouching) // 앉기 시작 확인
        {
            SetCrouchState(true); // 앉기 상태 적용
            return; // 추가 처리 중단
        }

        if (!crouchHeld && isCrouching && CanStand()) // 일어서기 가능 확인
        {
            SetCrouchState(false); // 서기 상태 적용
        }
    }

    private void SetCrouchState(bool crouching) // 앉기 상태 변경
    {
        isCrouching = crouching; // 현재 앉기 상태 저장
        controller.height = crouching ? crouchingHeight : standingHeight; // 컨트롤러 높이 적용
        float heightDifference = standingHeight - controller.height; // 높이 차이 계산
        controller.center = standingCenter + Vector3.down * (heightDifference * 0.5f); // 컨트롤러 중심 보정
    }

    private bool CanStand() // 일어서기 공간 확인
    {
        float capsuleHalf = Mathf.Max(standingHeight * 0.5f - controller.radius, 0f); // 캡슐 중심 구간 계산
        Vector3 worldCenter = transform.TransformPoint(standingCenter); // 서기 중심 월드 위치 계산
        Vector3 bottom = worldCenter + Vector3.down * capsuleHalf; // 캡슐 하단 중심 계산
        Vector3 top = worldCenter + Vector3.up * capsuleHalf; // 캡슐 상단 중심 계산
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, controller.radius * 0.95f, standingObstructionMask, QueryTriggerInteraction.Ignore); // 서기 공간 충돌 조회

        for (int i = 0; i < overlaps.Length; i++) // 충돌 객체 순회
        {
            Transform hitTransform = overlaps[i].transform; // 충돌 객체 트랜스폼 조회
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) // 자기 자신 충돌 확인
            {
                continue; // 자기 충돌 제외
            }

            return false; // 장애물 존재 반환
        }

        return true; // 서기 가능 반환
    }

    private void HandleFallRespawn() // 추락 복귀 처리
    {
        if (transform.position.y >= fallRespawnY) // 추락 높이 확인
        {
            return; // 복귀 처리 중단
        }

        controller.enabled = false; // 컨트롤러 임시 비활성
        transform.SetPositionAndRotation(spawnPosition, spawnRotation); // 시작 위치 복귀
        controller.enabled = true; // 컨트롤러 재활성
        horizontalVelocity = Vector3.zero; // 수평 속도 초기화
        verticalVelocity = 0f; // 수직 속도 초기화
        coyoteTimer = 0f; // 코요테 시간 초기화
        jumpBufferTimer = 0f; // 점프 저장 초기화
    }
}
