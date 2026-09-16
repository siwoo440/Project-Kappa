using UnityEngine; // 유니티 기본 기능

public sealed class DetectionSensor : MonoBehaviour // 시야 청각 탐지 센서
{
    [Header("Target")] // 대상 설정 구분
    [SerializeField] private Transform target; // 탐지 대상
    [SerializeField] private Vector3 visionOriginOffset = new Vector3(0f, 1.5f, 0f); // 시야 원점 보정
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f); // 대상 중심 보정

    [Header("Vision")] // 시야 설정 구분
    [SerializeField] private float visionDistance = 18f; // 시야 거리
    [SerializeField, Range(1f, 180f)] private float visionAngle = 90f; // 시야 각도
    [SerializeField] private LayerMask visionMask = ~0; // 시야 충돌 마스크
    [SerializeField] private float detectionGainPerSecond = 0.9f; // 초당 탐지 증가량
    [SerializeField] private float detectionDecayPerSecond = 0.45f; // 초당 탐지 감소량

    [Header("Hearing")] // 청각 설정 구분
    [SerializeField] private float hearingRadius = 10f; // 최대 청각 거리
    [SerializeField] private float hearingSuspicionAmount = 0.35f; // 소음 의심 증가량
    [SerializeField] private float suspicionHoldTime = 2.5f; // 의심 유지 시간

    [Header("Search")] // 수색 설정 구분
    [SerializeField] private float searchDuration = 4f; // 수색 유지 시간

    private DetectionState state; // 현재 탐지 상태
    private float detectionProgress; // 현재 탐지 진행도
    private float suspicionTimer; // 의심 유지 시간
    private float searchTimer; // 수색 유지 시간
    private Vector3 lastKnownPosition; // 마지막 확인 위치
    private bool hasLastKnownPosition; // 마지막 위치 존재 여부
    private bool targetVisible; // 현재 시야 확인 상태

    public DetectionState State => state; // 탐지 상태 읽기
    public float DetectionProgress => detectionProgress; // 탐지 진행도 읽기
    public Vector3 LastKnownPosition => lastKnownPosition; // 마지막 위치 읽기
    public bool HasLastKnownPosition => hasLastKnownPosition; // 마지막 위치 존재 여부 읽기
    public bool TargetVisible => targetVisible; // 현재 시야 상태 읽기

    private void OnEnable() // 활성화 처리
    {
        NoiseSystem.NoiseEmitted += HandleNoise; // 소음 이벤트 구독
    }

    private void OnDisable() // 비활성화 처리
    {
        NoiseSystem.NoiseEmitted -= HandleNoise; // 소음 이벤트 구독 해제
    }

    private void Update() // 매 프레임 탐지 처리
    {
        if (target == null) // 탐지 대상 확인
        {
            targetVisible = false; // 시야 상태 초기화
            DecayDetection(); // 탐지 감소 처리
            UpdateStateTimers(); // 상태 시간 갱신
            return; // 탐지 처리 중단
        }

        targetVisible = IsTargetVisible(); // 시야 확인
        if (targetVisible) // 대상 시야 확인
        {
            lastKnownPosition = target.position; // 마지막 위치 갱신
            hasLastKnownPosition = true; // 마지막 위치 존재 설정
            detectionProgress = Mathf.Clamp01(detectionProgress + detectionGainPerSecond * Time.deltaTime); // 탐지 진행도 증가
            suspicionTimer = suspicionHoldTime; // 의심 유지 시간 갱신

            if (detectionProgress >= 1f) // 완전 탐지 확인
            {
                SetState(DetectionState.Detected); // 발견 상태 적용
            }
            else // 부분 탐지 처리
            {
                SetState(DetectionState.Suspicious); // 의심 상태 적용
            }

            return; // 시야 탐지 처리 종료
        }

        DecayDetection(); // 탐지 진행도 감소
        UpdateStateTimers(); // 상태 시간 갱신
    }

    public void Configure(Transform newTarget, float distance, float angle, float hearing, LayerMask mask) // 센서 설정 적용
    {
        target = newTarget; // 탐지 대상 저장
        visionDistance = Mathf.Max(0.1f, distance); // 시야 거리 저장
        visionAngle = Mathf.Clamp(angle, 1f, 180f); // 시야 각도 저장
        hearingRadius = Mathf.Max(0f, hearing); // 청각 거리 저장
        visionMask = mask; // 시야 마스크 저장
    }

    private bool IsTargetVisible() // 대상 시야 확인
    {
        Vector3 origin = transform.TransformPoint(visionOriginOffset) + transform.forward * 0.05f; // 시야 원점 계산
        Vector3 targetPosition = target.position + targetOffset; // 대상 확인 위치 계산
        Vector3 toTarget = targetPosition - origin; // 대상 방향 계산
        float distance = toTarget.magnitude; // 대상 거리 계산

        if (distance > visionDistance || distance <= 0.001f) // 거리 조건 확인
        {
            return false; // 시야 실패 반환
        }

        Vector3 direction = toTarget / distance; // 대상 방향 정규화
        float angle = Vector3.Angle(transform.forward, direction); // 대상 각도 계산
        if (angle > visionAngle * 0.5f) // 시야 각도 확인
        {
            return false; // 시야 실패 반환
        }

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance + 0.2f, visionMask, QueryTriggerInteraction.Ignore)) // 시야 레이 충돌 확인
        {
            return true; // 장애물 없음 반환
        }

        Transform hitTransform = hit.transform; // 충돌 트랜스폼 조회
        if (hitTransform == target || hitTransform.IsChildOf(target)) // 대상 직접 충돌 확인
        {
            return true; // 시야 성공 반환
        }

        return false; // 장애물 차단 반환
    }

    private void HandleNoise(NoiseEvent noiseEvent) // 소음 이벤트 처리
    {
        float distance = Vector3.Distance(transform.position, noiseEvent.Position); // 소음 거리 계산
        float audibleDistance = Mathf.Min(hearingRadius, noiseEvent.Radius); // 실제 청취 거리 계산
        if (distance > audibleDistance) // 청취 범위 확인
        {
            return; // 소음 처리 중단
        }

        lastKnownPosition = noiseEvent.Position; // 마지막 위치 갱신
        hasLastKnownPosition = true; // 마지막 위치 존재 설정
        suspicionTimer = suspicionHoldTime; // 의심 유지 시간 설정
        detectionProgress = Mathf.Max(detectionProgress, hearingSuspicionAmount); // 탐지 진행도 최소 의심치 적용

        if (state != DetectionState.Detected) // 발견 상태 여부 확인
        {
            SetState(DetectionState.Suspicious); // 의심 상태 적용
        }
    }

    private void DecayDetection() // 탐지 진행도 감소
    {
        detectionProgress = Mathf.MoveTowards(detectionProgress, 0f, detectionDecayPerSecond * Time.deltaTime); // 탐지 진행도 감소 적용
    }

    private void UpdateStateTimers() // 탐지 상태 시간 갱신
    {
        if (state == DetectionState.Detected) // 발견 상태 확인
        {
            searchTimer = searchDuration; // 수색 시간 설정
            SetState(DetectionState.Searching); // 수색 상태 전환
            return; // 상태 처리 종료
        }

        if (state == DetectionState.Searching) // 수색 상태 확인
        {
            searchTimer -= Time.deltaTime; // 수색 시간 감소
            if (searchTimer <= 0f) // 수색 종료 확인
            {
                SetState(detectionProgress > 0f ? DetectionState.Suspicious : DetectionState.Idle); // 탐지량 기준 상태 전환
            }
            return; // 상태 처리 종료
        }

        if (state == DetectionState.Suspicious) // 의심 상태 확인
        {
            suspicionTimer -= Time.deltaTime; // 의심 시간 감소
            if (suspicionTimer <= 0f && detectionProgress <= 0.01f) // 의심 종료 조건 확인
            {
                SetState(DetectionState.Idle); // 평상 상태 복귀
            }
        }
    }

    private void SetState(DetectionState newState) // 탐지 상태 변경
    {
        if (state == newState) // 동일 상태 확인
        {
            return; // 변경 처리 중단
        }

        state = newState; // 새 상태 저장
        Debug.Log($"{name} 탐지 상태: {state}"); // 상태 변경 로그 출력
    }

    private void OnDrawGizmosSelected() // 선택 시 탐지 범위 표시
    {
        Vector3 origin = transform.TransformPoint(visionOriginOffset); // 시야 원점 계산
        Gizmos.DrawWireSphere(transform.position, hearingRadius); // 청각 범위 표시
        Gizmos.DrawLine(origin, origin + transform.forward * visionDistance); // 시야 중심선 표시

        Quaternion leftRotation = Quaternion.AngleAxis(-visionAngle * 0.5f, Vector3.up); // 좌측 시야 회전 계산
        Quaternion rightRotation = Quaternion.AngleAxis(visionAngle * 0.5f, Vector3.up); // 우측 시야 회전 계산
        Gizmos.DrawLine(origin, origin + leftRotation * transform.forward * visionDistance); // 좌측 시야 경계 표시
        Gizmos.DrawLine(origin, origin + rightRotation * transform.forward * visionDistance); // 우측 시야 경계 표시

        if (hasLastKnownPosition) // 마지막 위치 존재 확인
        {
            Gizmos.DrawWireSphere(lastKnownPosition, 0.35f); // 마지막 확인 위치 표시
        }
    }
}
