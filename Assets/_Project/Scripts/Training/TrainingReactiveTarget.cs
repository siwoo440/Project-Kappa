using UnityEngine; // 움직이는 훈련 표적 기능

[DisallowMultipleComponent] // 표적 중복 제어 방지
public sealed class TrainingReactiveTarget : MonoBehaviour // 고정과 이동 표적의 피격 넘어짐
{
    [SerializeField] private Transform hinge; // 뒤로 넘어지는 회전축
    [SerializeField] private Transform carriage; // 레일을 따라 이동하는 받침
    [SerializeField] private Collider[] hitColliders; // 서 있을 때만 사용하는 피격 영역
    [SerializeField] private TextMesh statusLabel; // 단일 표적 상태 표시
    [SerializeField, Min(0f)] private float travel = 0f; // 좌우 전체 이동 거리
    [SerializeField, Min(0f)] private float speed = 2f; // 레일 이동 속도
    [SerializeField, Min(1f)] private float healthToDrop = 1f; // 일반 표적은 한 번 피격에 넘어짐
    [SerializeField, Min(0f)] private float downSeconds = 2.5f; // 넘어진 상태 유지 시간
    private readonly TrainingTargetCycle cycle = new TrainingTargetCycle(0.24f, 2.5f, 0.55f); // 기본 자동 복귀 상태
    private TrainingTargetCycle activeCycle; // 설정 시간을 반영한 상태
    private Quaternion homeRotation; // 회전축의 초기 방향
    private Vector3 carriageHome; // 레일 중앙 위치
    private float railTime; // 이동 구간에만 흐르는 시간
    private float fallSign = 1f; // 충격 반대쪽 회전 방향
    private double trialStarted = -1; // 최초 발사 측정 시각
    private bool initialized; // 초기 위치 캡처 여부
    public bool AcceptsHit => isActiveAndEnabled && initialized && (activeCycle ?? cycle).AcceptsHit; // 실제 피격 가능 상태
    public Transform MarkParent => hinge != null ? hinge : transform; // 탄착이 표적판을 따라가는 부모
    public float RemainingHealth // 현재 훈련 체력
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public int Knockdowns // 누적 넘어짐 횟수
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public float LastTrialSeconds // 마지막 처치 시간
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    } = -1f;
    public float Travel => travel; // 검사 메뉴 이동 거리
    public Transform Hinge => hinge; // 검사 메뉴 회전축
    public TrainingTargetCycle.Phase State => (activeCycle ?? cycle).State; // 현재 표적 상태

    public void Configure(Transform pivot, Transform movingBase, Collider[] colliders, TextMesh label, float railTravel, float railSpeed, float hitPoints = 1f) // 생성 도구의 참조 연결
    {
        hinge = pivot; // 회전축 저장
        carriage = movingBase; // 이동 받침 저장
        hitColliders = colliders; // 표적 피격 영역 저장
        statusLabel = label; // 기존 결과판 저장
        travel = Mathf.Max(0f, railTravel); // 이동 범위 저장
        speed = Mathf.Max(0f, railSpeed); // 이동 속도 저장
        healthToDrop = Mathf.Max(1f, hitPoints); // 피격 또는 체력 시험 모드 저장
    }

    private void Start() // 모든 참조 생성 뒤 초기화
    {
        Initialize(); // 원래 자세 기억
    }

    private void Initialize() // 최초 한 번만 원래 위치 저장
    {
        if (initialized || hinge == null) // 중복 또는 미완성 표적 확인
        {
            return; // 초기화 대기
        }
        homeRotation = hinge.localRotation; // 회전축 기본 자세
        carriageHome = carriage != null ? carriage.localPosition : Vector3.zero; // 레일 중앙 기억
        activeCycle = new TrainingTargetCycle(0.24f, downSeconds, 0.55f); // 조정 가능한 복귀 시간
        RemainingHealth = healthToDrop; // 훈련 체력 초기화
        initialized = true; // 준비 완료 기록
        SetColliders(true); // 최초 표적 활성화
    }

    private void Update() // 레일 이동과 피격 연출
    {
        Advance(Time.deltaTime); // 실제 프레임 시간 전달
    }

    public void Advance(float delta) // 실행과 검사에 공통인 표적 갱신
    {
        if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f) // 유효한 시간 간격 확인
        {
            return; // 잘못된 시간의 위치 변경 차단
        }
        Initialize(); // 늦은 구성 대응
        if (!initialized) // 회전축 누락 확인
        {
            return; // 미완성 표적 갱신 중단
        }
        bool wasReady = activeCycle.AcceptsHit; // 이전 피격 가능 상태
        activeCycle.Tick(delta); // 프레임 시간 반영
        hinge.localRotation = homeRotation * Quaternion.Euler(88f * fallSign * activeCycle.Tilt, 0f, 0f); // 뒤로 젖혀지는 힌지 회전
        if (!wasReady && activeCycle.AcceptsHit) // 자동 복귀 완료 확인
        {
            RemainingHealth = healthToDrop; // 다음 시험 체력 복구
            trialStarted = -1; // 다음 측정 시작 대기
            GetComponent<FirearmPracticeTarget>()?.ClearMarks(); // 지난 탄착 정리
            SetColliders(true); // 완전히 일어선 뒤 피격 복구
        }
        if (carriage != null && travel > 0f && activeCycle.AcceptsHit) // 이동 표적의 서기 상태 확인
        {
            railTime += delta; // 넘어짐 동안 레일 정지
            float x = Mathf.PingPong(railTime * speed + travel * 0.5f, travel) - travel * 0.5f; // 양끝에서 왕복하는 위치
            carriage.localPosition = carriageHome + Vector3.right * x; // 레일 범위 안에서 좌우 이동
        }
    }

    public void BeginTrial(double now) // 조준한 표적에 대한 첫 발사 기록
    {
        Initialize(); // 최초 프레임 발사 대응
        if (AcceptsHit && trialStarted < 0) // 측정 중복 시작 방지
        {
            trialStarted = now; // 첫 발사 시각 저장
        }
    }

    public bool ReceiveImpact(float damage, Vector3 shotDirection) // 총기와 검의 공통 표적 피격
    {
        Initialize(); // 동적 생성 직후 피해 대응
        if (!AcceptsHit || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) // 실제 유효 피격만 수용
        {
            return false; // 넘어짐 중 피해 중복 차단
        }
        BeginTrial(Time.timeAsDouble); // 조준 외 명중도 측정 시작
        RemainingHealth = Mathf.Max(0f, RemainingHealth - damage); // 한 발의 합산 피해 적용
        if (RemainingHealth <= 0f) // 표적 제압 기준 확인
        {
            fallSign = Vector3.Dot(transform.forward, shotDirection) >= 0f ? 1f : -1f; // 들어온 방향의 반대쪽으로 넘어짐
            LastTrialSeconds = (float)System.Math.Max(0, Time.timeAsDouble - trialStarted); // 첫 발사부터 넘어짐까지 기록
            Knockdowns++; // 제압 횟수 증가
            activeCycle.Hit(); // 넘어짐 시작
            SetColliders(false); // 누운 표적을 재차 맞히지 않도록 즉시 비활성화
        }
        return true; // 유효 피격 반환
    }

    public void ResetTarget() // F 단말기의 훈련 초기화
    {
        Initialize(); // 늦은 초기화 대응
        if (!initialized) // 필수 회전축 확인
        {
            return; // 초기화 실패 시 중단
        }
        activeCycle.Reset(); // 넘어짐 시간 초기화
        hinge.localRotation = homeRotation; // 서기 자세 복구
        if (carriage != null) // 이동 받침 확인
        {
            carriage.localPosition = carriageHome; // 레일 중앙 복귀
        }
        railTime = 0f; // 왕복 시간 초기화
        trialStarted = -1; // 처치 시간 초기화
        LastTrialSeconds = -1f; // 이전 결과 제거
        Knockdowns = 0; // 누적 기록 초기화
        RemainingHealth = healthToDrop; // 체력 복구
        GetComponent<FirearmPracticeTarget>()?.ClearMarks(); // 탄착 표시 초기화
        SetColliders(true); // 표적 재활성화
    }

    private void SetColliders(bool value) // 표적 피격 상태 동기화
    {
        foreach (Collider hit in hitColliders ?? new Collider[0]) // 이 표적의 피격 영역만 조회
        {
            if (hit != null) // 남은 참조 확인
            {
                hit.enabled = value; // 바닥과 레일 충돌은 건드리지 않음
            }
        }
    }

    private void LateUpdate() // 상태 글자만 카메라 방향 정렬
    {
        if (statusLabel == null) // 결과판 참조 확인
        {
            return; // 별도 UI 생성 생략
        }
        Camera camera = Camera.main; // 사용자 카메라 조회
        if (camera != null) // 카메라 유효성 확인
        {
            statusLabel.transform.rotation = camera.transform.rotation; // 표적 회전과 글자 분리
        }
        statusLabel.text = (travel > 0f ? "MOVING" : "STATIC") + " / " + State + "\n" + (healthToDrop > 1f ? "HP " + RemainingHealth.ToString("0") + " / " + healthToDrop.ToString("0") : "HIT TO DROP") + "\nDOWN " + Knockdowns + (LastTrialSeconds >= 0f ? " / TTK " + LastTrialSeconds.ToString("0.00") + "s" : ""); // 단일 결과 표시
    }
}
