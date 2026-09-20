using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[RequireComponent(typeof(Camera))] // 카메라 컴포넌트 필수 지정
public sealed class ThirdPersonCamera : MonoBehaviour // 3인칭 카메라 관리자
{
    [Header("Target")] // 대상 설정 구분
    [SerializeField] private Transform target; // 추적 대상
    [SerializeField] private PlayerInput playerInput; // 플레이어 입력 참조
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f); // 카메라 중심 높이

    [Header("Orbit")] // 회전 설정 구분
    [SerializeField] private float distance = 5f; // 기본 카메라 거리
    [SerializeField] private float mouseSensitivity = 0.12f; // 마우스 감도
    [SerializeField] private float gamepadLookSpeed = 140f; // 게임패드 회전 속도
    [SerializeField] private bool invertY; // Day36 사용자 Y축 반전 설정
    [SerializeField] private float minPitch = -35f; // 최소 상하 각도
    [SerializeField] private float maxPitch = 70f; // 최대 상하 각도
    [SerializeField] private float distanceSmoothTime = 0.04f; // 거리 복귀 보간 시간
    [SerializeField] private float rollSmoothSpeed = 8f; // 롤 보간 속도

    [Header("Collision")] // 충돌 설정 구분
    [SerializeField] private float collisionRadius = 0.25f; // 카메라 충돌 반경
    [SerializeField] private float collisionPadding = 0.12f; // 벽 여유 거리
    [SerializeField] private float minimumDistance = 0.65f; // 최소 카메라 거리
    [SerializeField] private LayerMask collisionMask = ~0; // 카메라 충돌 마스크

    private Vector2 shotRecoil; // 입력 각도와 분리된 사격 반동
    private float shotRecoilRate = 2f; // 초당 반동 복귀 도수
    private float recoilRecoveryAt; // 반동 복귀 시작 시각
    private float lookMultiplier = 1f; // 조준 감도 비율
    public Vector2 ShotRecoil => shotRecoil; // 반동 검사 자료

    private InputAction lookAction; // 시점 입력 액션
    private float yaw; // 현재 좌우 각도
    private float pitch = 15f; // 현재 상하 각도
    private float currentDistance; // 현재 카메라 거리
    private float distanceVelocity; // 거리 보간 속도
    private float targetRoll; // 목표 롤 각도
    private float currentRoll; // 현재 롤 각도

    private void Awake() // 초기 카메라 설정
    {
        currentDistance = distance; // 초기 거리 설정
        ResolveLookAction(); // 시점 입력 연결

        if (target != null) // 추적 대상 확인
        {
            yaw = target.eulerAngles.y; // 대상 방향으로 초기화
        }
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveLookAction(); // 시점 입력 재연결
        LockCursor(); // 마우스 커서 잠금
    }

    private void OnDisable() // 비활성화 처리
    {
        ResetShotRecoil(); // 비활성화 시 반동 잔류 제거
        SetLookMultiplier(1f); // 조준 감도 복원
        UnlockCursor(); // 마우스 커서 해제
    }

    private void Update() // 입력 처리
    {
        HandleCursorState(); // 커서 상태 처리
        HandleLookInput(); // 카메라 회전 입력 처리
    }

    private void LateUpdate() // 대상 이동 후 카메라 배치
    {
        if (target == null) // 추적 대상 확인
        {
            return; // 카메라 갱신 중단
        }

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rollSmoothSpeed); // 롤 보간 적용
        if (Time.time >= recoilRecoveryAt) // 반동 복귀 지연 종료 확인
        {
            float elapsed = Mathf.Min(Time.deltaTime, Time.time - recoilRecoveryAt); // 지연 경계 프레임 보정
            shotRecoil.x = FirearmHandlingMath.Recover(shotRecoil.x, elapsed, shotRecoilRate); // 위쪽 반동 회복
            shotRecoil.y = FirearmHandlingMath.Recover(shotRecoil.y, elapsed, shotRecoilRate); // 좌우 반동 회복
        }

        float renderPitch = Mathf.Clamp(pitch - shotRecoil.x, minPitch, maxPitch); // 기본 시점에만 반동 보정 추가
        Quaternion orbitRotation = Quaternion.Euler(renderPitch, yaw + shotRecoil.y, currentRoll); // 벽 달리기 롤을 유지한 최종 시점
        Vector3 pivotPosition = target.position + pivotOffset; // 카메라 중심 위치 계산
        Vector3 backwardDirection = orbitRotation * Vector3.back; // 카메라 후방 방향 계산
        float collisionDistance = GetCollisionDistance(pivotPosition, backwardDirection); // 충돌 보정 거리 계산
        currentDistance = Mathf.SmoothDamp(currentDistance, collisionDistance, ref distanceVelocity, distanceSmoothTime); // 카메라 거리 부드럽게 보정
        Vector3 cameraPosition = pivotPosition + backwardDirection * currentDistance; // 최종 카메라 위치 계산
        transform.SetPositionAndRotation(cameraPosition, orbitRotation); // 카메라 위치 회전 적용
    }

    public void Configure(Transform newTarget, PlayerInput newPlayerInput) // 카메라 대상 설정
    {
        target = newTarget; // 추적 대상 저장
        playerInput = newPlayerInput; // 입력 참조 저장
        yaw = target != null ? target.eulerAngles.y : yaw; // 초기 좌우 각도 갱신
        currentDistance = distance; // 현재 거리 초기화
        ResolveLookAction(); // 시점 입력 연결
    }

    public void SetAdditionalRoll(float roll) // 카메라 추가 롤 지정
    {
        targetRoll = roll; // 목표 롤 저장
    }

    public void AddShotRecoil(Vector2 kick, Vector2 limits, float rate, float delay) // 실제 발사 시 조준 반동 누적
    {
        shotRecoil.x = Mathf.Clamp(shotRecoil.x + kick.x, 0f, Mathf.Max(0f, limits.x)); // 수직 반동 한도 적용
        shotRecoil.y = Mathf.Clamp(shotRecoil.y + kick.y, -Mathf.Max(0f, limits.y), Mathf.Max(0f, limits.y)); // 좌우 반동 한도 적용
        shotRecoilRate = Mathf.Max(0f, rate); // 현재 총기 복귀 속도 저장
        recoilRecoveryAt = Time.time + Mathf.Max(0f, delay); // 연속 사격 시 복귀 대기 갱신
    }

    public void ResetShotRecoil() // 장착 해제와 특수행동의 반동 정리
    {
        shotRecoil = Vector2.zero; // 사격 보정만 초기화
        recoilRecoveryAt = 0f; // 복귀 대기 해제
    }

    public void SetLookMultiplier(float multiplier) // 기본 감도를 바꾸지 않는 조준 감도 적용
    {
        lookMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f); // 유효한 감도 비율 저장
    }

    private void ResolveLookAction() // 시점 입력 연결
    {
        if (playerInput == null || playerInput.actions == null) // 입력 참조 확인
        {
            lookAction = null; // 입력 액션 초기화
            return; // 연결 중단
        }

        InputActionMap actionMap = playerInput.actions.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        lookAction = actionMap != null ? actionMap.FindAction("Look", false) : null; // 시점 액션 조회
    }

    private void HandleLookInput() // 시점 입력 처리
    {
        if (lookAction == null || Cursor.lockState != CursorLockMode.Locked) // 입력 가능 상태 확인
        {
            return; // 회전 처리 중단
        }

        Vector2 lookInput = lookAction.ReadValue<Vector2>(); // 시점 입력값 조회
        bool gamepadInput = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad; // 게임패드 입력 확인
        float verticalSign = invertY ? 1f : -1f; // 기본 시점과 Y축 반전 방향 부호 계산

        if (gamepadInput) // 게임패드 입력 처리
        {
            yaw += lookInput.x * gamepadLookSpeed * lookMultiplier * Time.deltaTime; // 게임패드 좌우 회전 적용
            pitch += lookInput.y * gamepadLookSpeed * lookMultiplier * Time.deltaTime * verticalSign; // 게임패드 Y축 설정 적용
        }
        else // 마우스 입력 처리
        {
            yaw += lookInput.x * mouseSensitivity * lookMultiplier; // 마우스 좌우 회전 적용
            pitch += lookInput.y * mouseSensitivity * lookMultiplier * verticalSign; // 마우스 Y축 설정 적용
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch); // 상하 회전 범위 제한
    }

    private float GetCollisionDistance(Vector3 pivotPosition, Vector3 direction) // 카메라 충돌 거리 계산
    {
        RaycastHit[] hits = Physics.SphereCastAll(pivotPosition, collisionRadius, direction, distance, collisionMask, QueryTriggerInteraction.Ignore); // 카메라 경로 충돌 조회
        float nearestDistance = distance; // 기본 거리 설정

        for (int i = 0; i < hits.Length; i++) // 충돌 결과 순회
        {
            Collider hitCollider = hits[i].collider; // 충돌 콜라이더 조회
            if (hitCollider == null) // 콜라이더 누락 확인
            {
                continue; // 누락 결과 제외
            }

            Transform hitTransform = hitCollider.transform; // 충돌 트랜스폼 조회
            if (target != null && (hitTransform == target || hitTransform.IsChildOf(target))) // 플레이어 자기 충돌 확인
            {
                continue; // 플레이어 충돌 제외
            }

            nearestDistance = Mathf.Min(nearestDistance, hits[i].distance); // 가장 가까운 충돌 거리 갱신
        }

        if (nearestDistance >= distance) // 충돌 없음 확인
        {
            return distance; // 기본 거리 반환
        }

        return Mathf.Clamp(nearestDistance - collisionPadding, minimumDistance, distance); // 벽 앞 보정 거리 반환
    }

    private void HandleCursorState() // 커서 잠금 상태 처리
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) // ESC 입력 확인
        {
            UnlockCursor(); // 커서 잠금 해제
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) // 화면 클릭 확인
        {
            LockCursor(); // 커서 다시 잠금
        }
    }

    private static void LockCursor() // 커서 잠금 처리
    {
        Cursor.lockState = CursorLockMode.Locked; // 커서 위치 잠금
        Cursor.visible = false; // 커서 숨김
    }

    private static void UnlockCursor() // 커서 해제 처리
    {
        Cursor.lockState = CursorLockMode.None; // 커서 위치 해제
        Cursor.visible = true; // 커서 표시
    }
}
