using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[RequireComponent(typeof(PlayerMovement))] // 이동 스크립트 필수 지정
[RequireComponent(typeof(MovementEnergy))] // 에너지 스크립트 필수 지정
public sealed class PlayerParkour : MonoBehaviour // 파쿠르 동작 관리자
{
    private enum ParkourState // 파쿠르 상태
    {
        Normal, // 일반 상태
        WallRunLeft, // 좌측 벽 달리기 상태
        WallRunRight, // 우측 벽 달리기 상태
        WallClimb, // 벽 오르기 상태
        LedgeHang // 난간 매달리기 상태
    }

    [Header("Mask")] // 마스크 설정 구분
    [SerializeField] private LayerMask parkourMask = ~0; // 파쿠르 표면 마스크

    [Header("Wall Detection")] // 벽 감지 설정 구분
    [SerializeField] private float sideWallCheckDistance = 0.9f; // 측면 벽 감지 거리
    [SerializeField] private float frontWallCheckDistance = 0.9f; // 정면 벽 감지 거리
    [SerializeField] private float wallCheckHeight = 1.2f; // 벽 감지 높이
    [SerializeField] private float reattachCooldown = 0.25f; // 같은 벽 재진입 제한 시간

    [Header("Wall Run")] // 벽 달리기 설정 구분
    [SerializeField] private float wallRunSpeed = 8.5f; // 벽 달리기 속도
    [SerializeField] private float wallRunGravity = -3f; // 벽 달리기 하강 속도
    [SerializeField] private float wallRunEnergyCost = 20f; // 벽 달리기 초당 에너지 소모
    [SerializeField] private float wallRunCameraTilt = 12f; // 벽 달리기 카메라 기울기

    [Header("Wall Climb")] // 벽 오르기 설정 구분
    [SerializeField] private float wallClimbUpSpeed = 4.5f; // 벽 오르기 상승 속도
    [SerializeField] private float wallClimbIntoWallSpeed = 1f; // 벽 오르기 벽 부착 속도
    [SerializeField] private float wallClimbEnergyCost = 30f; // 벽 오르기 초당 에너지 소모

    [Header("Wall Jump")] // 벽 점프 설정 구분
    [SerializeField] private float wallJumpHorizontalSpeed = 6.5f; // 벽 점프 수평 속도
    [SerializeField] private float wallJumpVerticalSpeed = 7f; // 벽 점프 수직 속도

    [Header("Ledge")] // 난간 설정 구분
    [SerializeField] private float ledgeLowerRayHeight = 1f; // 낮은 레이 높이
    [SerializeField] private float ledgeUpperRayHeight = 2f; // 높은 레이 높이
    [SerializeField] private float ledgeForwardDistance = 0.75f; // 난간 전방 감지 거리
    [SerializeField] private float ledgeTopSearchHeight = 2.5f; // 난간 상단 탐색 높이
    [SerializeField] private float ledgeTopSearchDistance = 4f; // 난간 상단 탐색 거리
    [SerializeField] private float ledgeGrabBackOffset = 0.45f; // 난간 매달리기 후방 보정
    [SerializeField] private float ledgeGrabDownOffset = 1.05f; // 난간 매달리기 하방 보정
    [SerializeField] private float ledgeSideMoveSpeed = 2f; // 난간 좌우 이동 속도
    [SerializeField] private float ledgeClimbForwardOffset = 0.7f; // 난간 올라가기 전방 보정
    [SerializeField] private float ledgeClimbUpOffset = 1.2f; // 난간 올라가기 상방 보정

    private PlayerMovement movement; // 기본 이동 참조
    private MovementEnergy energy; // 이동 에너지 참조
    private CharacterController controller; // 캐릭터 컨트롤러 참조
    private PlayerInput playerInput; // 플레이어 입력 참조
    private ThirdPersonCamera cameraController; // 카메라 스크립트 참조
    private InputAction moveAction; // 이동 입력 액션
    private InputAction jumpAction; // 점프 입력 액션
    private InputAction crouchAction; // 앉기 입력 액션
    private ParkourState currentState; // 현재 파쿠르 상태
    private RaycastHit currentWallHit; // 현재 벽 충돌 정보
    private Collider blockedWallCollider; // 재진입 제한 벽 정보
    private float blockedWallTimer; // 재진입 제한 남은 시간
    private Vector3 ledgeHangPosition; // 난간 매달리기 위치
    private Vector3 ledgeTopPosition; // 난간 상단 위치
    private Vector3 ledgeWallNormal; // 난간 벽 노말
    private Vector3 ledgeRightDirection; // 난간 우측 방향

    private void Awake() // 초기 참조 설정
    {
        movement = GetComponent<PlayerMovement>(); // 이동 스크립트 조회
        energy = GetComponent<MovementEnergy>(); // 에너지 스크립트 조회
        controller = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        playerInput = GetComponent<PlayerInput>(); // 플레이어 입력 조회
        cameraController = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null; // 카메라 스크립트 조회
        ResolveActions(); // 입력 액션 연결
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveActions(); // 입력 액션 재연결
    }

    private void Update() // 매 프레임 파쿠르 처리
    {
        blockedWallTimer -= Time.deltaTime; // 벽 재진입 제한 시간 감소

        if (cameraController == null && Camera.main != null) // 카메라 참조 확인
        {
            cameraController = Camera.main.GetComponent<ThirdPersonCamera>(); // 카메라 스크립트 재조회
        }

        switch (currentState) // 현재 상태 분기
        {
            case ParkourState.Normal: // 일반 상태 처리
                UpdateNormalState(); // 일반 상태 갱신
                break; // 분기 종료
            case ParkourState.WallRunLeft: // 좌측 벽 달리기 처리
            case ParkourState.WallRunRight: // 우측 벽 달리기 처리
                UpdateWallRunState(); // 벽 달리기 갱신
                break; // 분기 종료
            case ParkourState.WallClimb: // 벽 오르기 처리
                UpdateWallClimbState(); // 벽 오르기 갱신
                break; // 분기 종료
            case ParkourState.LedgeHang: // 난간 매달리기 처리
                UpdateLedgeHangState(); // 난간 매달리기 갱신
                break; // 분기 종료
        }
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
    }

    private void UpdateNormalState() // 일반 상태 갱신
    {
        if (cameraController != null) // 카메라 참조 확인
        {
            cameraController.SetAdditionalRoll(0f); // 카메라 기울기 원복
        }

        if (movement != null && !movement.MovementEnabled) // 기본 이동 활성 확인
        {
            movement.SetMovementEnabled(true); // 이동 활성화
        }

        energy.SetAutoRecoveryEnabled(true); // 자동 회복 활성화

        if (controller.isGrounded) // 지면 상태 확인
        {
            return; // 공중 파쿠르 탐색 중단
        }

        if (TryStartLedgeHang()) // 난간 매달리기 시작 확인
        {
            return; // 상태 전환 완료
        }

        if (TryStartWallClimb()) // 벽 오르기 시작 확인
        {
            return; // 상태 전환 완료
        }

        TryStartWallRun(); // 벽 달리기 시작 시도
    }

    private void TryStartWallRun() // 벽 달리기 시작 시도
    {
        Vector2 moveInput = GetMoveInput(); // 이동 입력 조회
        if (moveInput.y <= 0.1f) // 전진 입력 확인
        {
            return; // 벽 달리기 시작 중단
        }

        if (!energy.CanConsume(1f)) // 최소 에너지 확인
        {
            return; // 벽 달리기 시작 중단
        }

        Vector3 origin = transform.position + Vector3.up * wallCheckHeight; // 측면 감지 원점 계산
        if (TryGetSideWall(origin, -transform.right, out RaycastHit leftHit) && CanUseWall(leftHit.collider)) // 좌측 벽 확인
        {
            StartWallRun(ParkourState.WallRunLeft, leftHit); // 좌측 벽 달리기 시작
            return; // 시작 완료
        }

        if (TryGetSideWall(origin, transform.right, out RaycastHit rightHit) && CanUseWall(rightHit.collider)) // 우측 벽 확인
        {
            StartWallRun(ParkourState.WallRunRight, rightHit); // 우측 벽 달리기 시작
        }
    }

    private void StartWallRun(ParkourState wallRunState, RaycastHit hit) // 벽 달리기 시작
    {
        currentState = wallRunState; // 현재 상태 저장
        currentWallHit = hit; // 벽 충돌 정보 저장
        movement.SetMovementEnabled(false); // 기본 이동 비활성화
        movement.VerticalVelocity = 0f; // 수직 속도 초기화
        movement.SetHorizontalVelocity(Vector3.zero); // 수평 속도 초기화
        energy.SetAutoRecoveryEnabled(false); // 자동 회복 비활성화

        if (cameraController != null) // 카메라 참조 확인
        {
            float tilt = wallRunState == ParkourState.WallRunLeft ? -wallRunCameraTilt : wallRunCameraTilt; // 상태별 기울기 계산
            cameraController.SetAdditionalRoll(tilt); // 카메라 기울기 적용
        }
    }

    private void UpdateWallRunState() // 벽 달리기 상태 갱신
    {
        Vector2 moveInput = GetMoveInput(); // 이동 입력 조회
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight; // 측면 감지 원점 계산
        Vector3 direction = currentState == ParkourState.WallRunLeft ? -transform.right : transform.right; // 벽 감지 방향 계산

        if (!TryGetSideWall(origin, direction, out RaycastHit wallHit)) // 측면 벽 유지 확인
        {
            StopParkour(false); // 벽 달리기 종료
            return; // 상태 갱신 중단
        }

        currentWallHit = wallHit; // 최신 벽 정보 저장

        if (moveInput.y <= 0.1f || controller.isGrounded) // 유지 조건 확인
        {
            StopParkour(false); // 벽 달리기 종료
            return; // 상태 갱신 중단
        }

        if (!energy.Consume(wallRunEnergyCost * Time.deltaTime)) // 에너지 소모 적용
        {
            StopParkour(true); // 에너지 부족 종료
            return; // 상태 갱신 중단
        }

        if (WasJumpPressed()) // 벽 점프 입력 확인
        {
            ExecuteWallJump(currentWallHit.normal); // 벽 점프 실행
            return; // 점프 처리 종료
        }

        Vector3 wallForward = Vector3.Cross(currentWallHit.normal, Vector3.up); // 벽 평행 방향 계산
        if (Vector3.Dot(wallForward, transform.forward) < 0f) // 진행 방향 확인
        {
            wallForward = -wallForward; // 진행 방향 반전
        }

        Vector3 motion = wallForward * wallRunSpeed + Vector3.up * wallRunGravity; // 벽 달리기 이동량 계산
        controller.Move(motion * Time.deltaTime); // 벽 달리기 이동 적용
        controller.Move(-currentWallHit.normal * 0.03f); // 벽 부착 보정 적용
        AlignToDirection(wallForward); // 진행 방향 정렬
    }

    private bool TryStartWallClimb() // 벽 오르기 시작 시도
    {
        Vector2 moveInput = GetMoveInput(); // 이동 입력 조회
        if (moveInput.y <= 0.1f) // 전진 입력 확인
        {
            return false; // 시작 실패 반환
        }

        if (!energy.CanConsume(1f)) // 최소 에너지 확인
        {
            return false; // 시작 실패 반환
        }

        Vector3 origin = transform.position + Vector3.up * wallCheckHeight; // 정면 감지 원점 계산
        if (!Physics.Raycast(origin, transform.forward, out RaycastHit frontHit, frontWallCheckDistance, parkourMask, QueryTriggerInteraction.Ignore)) // 정면 벽 감지
        {
            return false; // 시작 실패 반환
        }

        if (!CanUseWall(frontHit.collider)) // 재진입 제한 벽 확인
        {
            return false; // 시작 실패 반환
        }

        currentState = ParkourState.WallClimb; // 현재 상태 저장
        currentWallHit = frontHit; // 벽 정보 저장
        movement.SetMovementEnabled(false); // 기본 이동 비활성화
        movement.VerticalVelocity = 0f; // 수직 속도 초기화
        movement.SetHorizontalVelocity(Vector3.zero); // 수평 속도 초기화
        energy.SetAutoRecoveryEnabled(false); // 자동 회복 비활성화
        return true; // 시작 성공 반환
    }

    private void UpdateWallClimbState() // 벽 오르기 상태 갱신
    {
        Vector2 moveInput = GetMoveInput(); // 이동 입력 조회
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight; // 정면 감지 원점 계산

        if (moveInput.y <= 0.1f) // 유지 입력 확인
        {
            StopParkour(false); // 벽 오르기 종료
            return; // 상태 갱신 중단
        }

        if (!Physics.Raycast(origin, transform.forward, out RaycastHit frontHit, frontWallCheckDistance, parkourMask, QueryTriggerInteraction.Ignore)) // 정면 벽 유지 확인
        {
            StopParkour(false); // 벽 오르기 종료
            return; // 상태 갱신 중단
        }

        currentWallHit = frontHit; // 최신 벽 정보 저장

        if (TryStartLedgeHang()) // 난간 전환 시도
        {
            return; // 난간 전환 완료
        }

        if (!energy.Consume(wallClimbEnergyCost * Time.deltaTime)) // 에너지 소모 적용
        {
            StopParkour(true); // 에너지 부족 종료
            return; // 상태 갱신 중단
        }

        if (WasJumpPressed()) // 벽 점프 입력 확인
        {
            ExecuteWallJump(currentWallHit.normal); // 벽 점프 실행
            return; // 점프 처리 종료
        }

        Vector3 motion = Vector3.up * wallClimbUpSpeed - currentWallHit.normal * wallClimbIntoWallSpeed; // 벽 오르기 이동량 계산
        controller.Move(motion * Time.deltaTime); // 벽 오르기 이동 적용
        AlignToDirection(-currentWallHit.normal); // 캐릭터 방향 정렬
    }

    private bool TryStartLedgeHang() // 난간 매달리기 시작 시도
    {
        Vector3 lowerOrigin = transform.position + Vector3.up * ledgeLowerRayHeight; // 낮은 레이 원점 계산
        Vector3 upperOrigin = transform.position + Vector3.up * ledgeUpperRayHeight; // 높은 레이 원점 계산

        if (!Physics.Raycast(lowerOrigin, transform.forward, out RaycastHit lowerHit, ledgeForwardDistance, parkourMask, QueryTriggerInteraction.Ignore)) // 낮은 레이 충돌 확인
        {
            return false; // 시작 실패 반환
        }

        if (Physics.Raycast(upperOrigin, transform.forward, ledgeForwardDistance, parkourMask, QueryTriggerInteraction.Ignore)) // 높은 레이 공간 확인
        {
            return false; // 시작 실패 반환
        }

        Vector3 topSearchOrigin = lowerHit.point + Vector3.up * ledgeTopSearchHeight - lowerHit.normal * 0.2f; // 상단 탐색 원점 계산
        if (!Physics.Raycast(topSearchOrigin, Vector3.down, out RaycastHit topHit, ledgeTopSearchDistance, parkourMask, QueryTriggerInteraction.Ignore)) // 상단 탐색 확인
        {
            return false; // 시작 실패 반환
        }

        ledgeWallNormal = lowerHit.normal; // 난간 벽 노말 저장
        ledgeRightDirection = Vector3.Cross(Vector3.up, ledgeWallNormal).normalized; // 난간 우측 방향 계산
        ledgeTopPosition = topHit.point + Vector3.up * 0.05f - ledgeWallNormal * 0.1f; // 난간 상단 위치 계산
        ledgeHangPosition = topHit.point - ledgeWallNormal * ledgeGrabBackOffset - Vector3.up * ledgeGrabDownOffset; // 난간 매달리기 위치 계산
        StartLedgeHang(); // 난간 매달리기 시작
        return true; // 시작 성공 반환
    }

    private void StartLedgeHang() // 난간 매달리기 시작
    {
        currentState = ParkourState.LedgeHang; // 현재 상태 저장
        movement.SetMovementEnabled(false); // 기본 이동 비활성화
        movement.SetHorizontalVelocity(Vector3.zero); // 수평 속도 초기화
        movement.VerticalVelocity = 0f; // 수직 속도 초기화
        energy.SetAutoRecoveryEnabled(true); // 에너지 회복 활성화

        if (cameraController != null) // 카메라 참조 확인
        {
            cameraController.SetAdditionalRoll(0f); // 카메라 기울기 초기화
        }

        controller.enabled = false; // 컨트롤러 비활성화
        transform.position = ledgeHangPosition; // 매달리기 위치 적용
        transform.rotation = Quaternion.LookRotation(-ledgeWallNormal, Vector3.up); // 매달리기 방향 적용
        controller.enabled = true; // 컨트롤러 재활성화
    }

    private void UpdateLedgeHangState() // 난간 매달리기 상태 갱신
    {
        Vector2 moveInput = GetMoveInput(); // 이동 입력 조회

        controller.enabled = false; // 위치 고정용 비활성화
        transform.position += ledgeRightDirection * moveInput.x * ledgeSideMoveSpeed * Time.deltaTime; // 좌우 이동 적용
        controller.enabled = true; // 컨트롤러 재활성화

        if (WasJumpPressed() || moveInput.y > 0.5f) // 올라가기 입력 확인
        {
            ClimbUpFromLedge(); // 난간 올라가기 실행
            return; // 처리 종료
        }

        if ((crouchAction != null && crouchAction.IsPressed()) || moveInput.y < -0.5f) // 내려가기 입력 확인
        {
            DropFromLedge(); // 난간 내려가기 실행
        }
    }

    private void ClimbUpFromLedge() // 난간 올라가기 처리
    {
        controller.enabled = false; // 컨트롤러 비활성화
        transform.position = ledgeTopPosition + transform.forward * ledgeClimbForwardOffset + Vector3.up * ledgeClimbUpOffset; // 상단 위치 적용
        controller.enabled = true; // 컨트롤러 재활성화
        currentState = ParkourState.Normal; // 일반 상태 복귀
        movement.SetMovementEnabled(true); // 기본 이동 활성화
        movement.SetHorizontalVelocity(Vector3.zero); // 수평 속도 초기화
        movement.VerticalVelocity = -2f; // 지면 밀착 속도 적용
        energy.SetAutoRecoveryEnabled(true); // 자동 회복 활성화
    }

    private void DropFromLedge() // 난간 내려가기 처리
    {
        currentState = ParkourState.Normal; // 일반 상태 복귀
        movement.SetMovementEnabled(true); // 기본 이동 활성화
        movement.SetHorizontalVelocity(Vector3.zero); // 수평 속도 초기화
        movement.VerticalVelocity = -2f; // 하강 속도 적용
        energy.SetAutoRecoveryEnabled(true); // 자동 회복 활성화
    }

    private void ExecuteWallJump(Vector3 wallNormal) // 벽 점프 실행
    {
        blockedWallCollider = currentWallHit.collider; // 재진입 제한 벽 저장
        blockedWallTimer = reattachCooldown; // 재진입 제한 시간 설정
        currentState = ParkourState.Normal; // 일반 상태 복귀
        movement.SetMovementEnabled(true); // 기본 이동 활성화
        energy.SetAutoRecoveryEnabled(true); // 자동 회복 활성화

        if (cameraController != null) // 카메라 참조 확인
        {
            cameraController.SetAdditionalRoll(0f); // 카메라 기울기 초기화
        }

        Vector3 jumpDirection = (wallNormal + Vector3.up).normalized; // 점프 방향 계산
        movement.SetHorizontalVelocity(new Vector3(jumpDirection.x, 0f, jumpDirection.z) * wallJumpHorizontalSpeed); // 수평 속도 적용
        movement.VerticalVelocity = wallJumpVerticalSpeed; // 수직 속도 적용
    }

    private void StopParkour(bool forceDrop) // 파쿠르 중단 처리
    {
        currentState = ParkourState.Normal; // 일반 상태 복귀
        movement.SetMovementEnabled(true); // 기본 이동 활성화
        energy.SetAutoRecoveryEnabled(true); // 자동 회복 활성화

        if (cameraController != null) // 카메라 참조 확인
        {
            cameraController.SetAdditionalRoll(0f); // 카메라 기울기 초기화
        }

        if (forceDrop) // 강제 하강 확인
        {
            movement.VerticalVelocity = -2f; // 하강 속도 적용
        }
    }

    private bool TryGetSideWall(Vector3 origin, Vector3 direction, out RaycastHit hit) // 측면 벽 감지
    {
        return Physics.Raycast(origin, direction, out hit, sideWallCheckDistance, parkourMask, QueryTriggerInteraction.Ignore); // 측면 레이 결과 반환
    }

    private bool CanUseWall(Collider wallCollider) // 벽 사용 가능 여부 확인
    {
        if (wallCollider == null) // 콜라이더 확인
        {
            return false; // 사용 불가 반환
        }

        if (blockedWallTimer > 0f && blockedWallCollider == wallCollider) // 재진입 제한 확인
        {
            return false; // 사용 불가 반환
        }

        return true; // 사용 가능 반환
    }

    private void AlignToDirection(Vector3 direction) // 캐릭터 방향 정렬
    {
        Vector3 flattenedDirection = new Vector3(direction.x, 0f, direction.z); // 수평 방향 계산
        if (flattenedDirection.sqrMagnitude <= 0.001f) // 방향 유효성 확인
        {
            return; // 정렬 처리 중단
        }

        Quaternion targetRotation = Quaternion.LookRotation(flattenedDirection.normalized, Vector3.up); // 목표 회전 계산
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 12f); // 회전 보간 적용
    }

    private Vector2 GetMoveInput() // 현재 이동 입력 조회
    {
        return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero; // 이동 입력 반환
    }

    private bool WasJumpPressed() // 점프 입력 확인
    {
        return jumpAction != null && jumpAction.WasPressedThisFrame(); // 점프 입력 여부 반환
    }
}
