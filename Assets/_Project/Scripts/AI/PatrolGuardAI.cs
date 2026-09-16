using UnityEngine; // 유니티 기본 기능

[RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
[RequireComponent(typeof(DetectionSensor))] // 탐지 센서 필수 지정
public sealed class PatrolGuardAI : MonoBehaviour // E-01 순찰 경비 행동 관리자
{
    private enum GuardState // 경비 행동 상태
    {
        Patrol, // 순찰 상태
        Investigate, // 조사 상태
        Chase, // 추격 상태
        Search // 수색 상태
    }

    [Header("Movement")] // 이동 설정 구분
    [SerializeField] private float patrolSpeed = 3.5f; // 순찰 속도
    [SerializeField] private float chaseSpeed = 5f; // 추격 속도
    [SerializeField] private float rotationSpeed = 8f; // 회전 보간 속도
    [SerializeField] private float patrolWaitTime = 1.2f; // 순찰 지점 대기 시간
    [SerializeField] private float arrivalDistance = 0.35f; // 목적지 도착 거리
    [SerializeField] private float gravity = -20f; // 중력 값

    private CharacterController controller; // 캐릭터 컨트롤러 참조
    private DetectionSensor sensor; // 탐지 센서 참조
    private Transform target; // 플레이어 대상 참조
    private Transform[] patrolPoints; // 순찰 지점 배열
    private GuardState state; // 현재 경비 상태
    private int patrolIndex; // 현재 순찰 지점 번호
    private float waitTimer; // 순찰 대기 시간
    private float searchYaw; // 수색 회전 누적값
    private float verticalVelocity; // 수직 속도

    private void Awake() // 초기 참조 설정
    {
        controller = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        sensor = GetComponent<DetectionSensor>(); // 탐지 센서 조회
    }

    private void Update() // 매 프레임 행동 처리
    {
        UpdateStateFromSensor(); // 탐지 상태 기반 행동 갱신

        switch (state) // 행동 상태 분기
        {
            case GuardState.Chase: // 추격 상태 처리
                UpdateChase(); // 추격 행동 실행
                break; // 분기 종료
            case GuardState.Investigate: // 조사 상태 처리
                UpdateInvestigate(); // 조사 행동 실행
                break; // 분기 종료
            case GuardState.Search: // 수색 상태 처리
                UpdateSearch(); // 수색 행동 실행
                break; // 분기 종료
            default: // 순찰 상태 처리
                UpdatePatrol(); // 순찰 행동 실행
                break; // 분기 종료
        }

        ApplyGravity(); // 중력 처리
    }

    public void Configure(Transform playerTarget, Transform[] points, float newPatrolSpeed, float newChaseSpeed) // 경비 설정 적용
    {
        target = playerTarget; // 플레이어 대상 저장
        patrolPoints = points; // 순찰 지점 저장
        patrolSpeed = newPatrolSpeed; // 순찰 속도 저장
        chaseSpeed = newChaseSpeed; // 추격 속도 저장
    }

    private void UpdateStateFromSensor() // 탐지 상태 기반 행동 갱신
    {
        if (sensor == null) // 센서 참조 확인
        {
            state = GuardState.Patrol; // 순찰 상태 적용
            return; // 상태 갱신 중단
        }

        if (sensor.State == DetectionState.Detected && sensor.TargetVisible) // 완전 발견 상태 확인
        {
            state = GuardState.Chase; // 추격 상태 적용
            return; // 상태 갱신 종료
        }

        if (sensor.State == DetectionState.Searching && sensor.HasLastKnownPosition) // 수색 상태 확인
        {
            state = GuardState.Search; // 수색 상태 적용
            return; // 상태 갱신 종료
        }

        if (sensor.State == DetectionState.Suspicious && sensor.HasLastKnownPosition) // 의심 상태 확인
        {
            state = GuardState.Investigate; // 조사 상태 적용
            return; // 상태 갱신 종료
        }

        state = GuardState.Patrol; // 순찰 상태 적용
    }

    private void UpdatePatrol() // 순찰 행동 처리
    {
        if (patrolPoints == null || patrolPoints.Length == 0) // 순찰 지점 확인
        {
            return; // 순찰 처리 중단
        }

        Transform patrolPoint = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)]; // 현재 순찰 지점 조회
        Vector3 targetPosition = patrolPoint.position; // 순찰 목적지 저장
        float planarDistance = GetPlanarDistance(transform.position, targetPosition); // 평면 거리 계산

        if (planarDistance <= arrivalDistance) // 도착 여부 확인
        {
            waitTimer += Time.deltaTime; // 대기 시간 누적
            RotateTowards(patrolPoint.forward); // 순찰 지점 방향 정렬

            if (waitTimer >= patrolWaitTime) // 대기 완료 확인
            {
                waitTimer = 0f; // 대기 시간 초기화
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length; // 다음 순찰 지점 선택
            }

            return; // 순찰 이동 종료
        }

        waitTimer = 0f; // 이동 중 대기 시간 초기화
        MoveTowards(targetPosition, patrolSpeed); // 순찰 이동 적용
    }

    private void UpdateChase() // 추격 행동 처리
    {
        if (target == null) // 플레이어 대상 확인
        {
            return; // 추격 중단
        }

        MoveTowards(target.position, chaseSpeed); // 플레이어 방향 추격
    }

    private void UpdateInvestigate() // 조사 행동 처리
    {
        if (!sensor.HasLastKnownPosition) // 조사 위치 확인
        {
            return; // 조사 중단
        }

        Vector3 investigatePosition = sensor.LastKnownPosition; // 조사 위치 저장
        float distance = GetPlanarDistance(transform.position, investigatePosition); // 조사 위치 거리 계산
        if (distance > arrivalDistance) // 조사 지점 도착 여부 확인
        {
            MoveTowards(investigatePosition, patrolSpeed); // 조사 위치 이동
            return; // 조사 이동 종료
        }

        RotateSearchPattern(35f); // 도착 후 주변 확인
    }

    private void UpdateSearch() // 수색 행동 처리
    {
        if (sensor.HasLastKnownPosition) // 마지막 위치 확인
        {
            float distance = GetPlanarDistance(transform.position, sensor.LastKnownPosition); // 마지막 위치 거리 계산
            if (distance > arrivalDistance) // 마지막 위치 도착 여부 확인
            {
                MoveTowards(sensor.LastKnownPosition, patrolSpeed); // 마지막 위치 이동
                return; // 수색 이동 종료
            }
        }

        RotateSearchPattern(70f); // 주변 수색 회전
    }

    private void MoveTowards(Vector3 targetPosition, float speed) // 목적지 이동 처리
    {
        Vector3 planarTarget = new Vector3(targetPosition.x, transform.position.y, targetPosition.z); // 평면 목적지 계산
        Vector3 direction = planarTarget - transform.position; // 이동 방향 계산
        direction.y = 0f; // 수직 성분 제거

        if (direction.sqrMagnitude <= 0.0001f) // 이동 방향 유효성 확인
        {
            return; // 이동 중단
        }

        direction.Normalize(); // 이동 방향 정규화
        controller.Move(direction * speed * Time.deltaTime); // 수평 이동 적용
        RotateTowards(direction); // 이동 방향 회전 적용
    }

    private void RotateTowards(Vector3 direction) // 방향 회전 처리
    {
        direction.y = 0f; // 수직 성분 제거
        if (direction.sqrMagnitude <= 0.0001f) // 방향 유효성 확인
        {
            return; // 회전 중단
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 회전 계산
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime); // 회전 보간 적용
    }

    private void RotateSearchPattern(float degreesPerSecond) // 수색 회전 처리
    {
        searchYaw += degreesPerSecond * Time.deltaTime; // 수색 회전 누적
        Vector3 direction = Quaternion.Euler(0f, searchYaw, 0f) * Vector3.forward; // 수색 방향 계산
        RotateTowards(direction); // 수색 방향 회전 적용
    }

    private void ApplyGravity() // 중력 처리
    {
        if (controller.isGrounded && verticalVelocity < 0f) // 지면 상태 확인
        {
            verticalVelocity = -2f; // 지면 고정 중력 적용
        }
        else // 공중 상태 처리
        {
            verticalVelocity += gravity * Time.deltaTime; // 중력 누적
        }

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime); // 수직 이동 적용
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b) // 평면 거리 계산
    {
        Vector2 first = new Vector2(a.x, a.z); // 첫 위치 평면 변환
        Vector2 second = new Vector2(b.x, b.z); // 둘째 위치 평면 변환
        return Vector2.Distance(first, second); // 평면 거리 반환
    }
}
