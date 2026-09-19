using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
[RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
[RequireComponent(typeof(DetectionSensor))] // 탐지 센서 필수 지정
[RequireComponent(typeof(EnemyActor))] // 적 생명 관리자 필수 지정
[RequireComponent(typeof(EnemyMeleeCombat))] // 적 근접 전투 필수 지정
public sealed class E02SwordGuardAI : MonoBehaviour // E-02 검술 호위병 AI
{
    [Header("Movement")] // 이동 설정 구분
    [SerializeField] private float patrolSpeed = 3.8f; // 일반 이동 속도
    [SerializeField] private float chaseSpeed = 5.5f; // 추격 속도
    [SerializeField] private float rotationSpeed = 9f; // 회전 속도
    [SerializeField] private float attackDistance = 2.25f; // 공격 시작 거리
    [SerializeField] private float stopDistance = 1.75f; // 추격 정지 거리
    [SerializeField] private float gravity = -20f; // 중력 값

    private CharacterController controller; // 캐릭터 컨트롤러 참조
    private DetectionSensor sensor; // 탐지 센서 참조
    private EnemyStatusController status; // 마비 상태 참조
    private EnemyActor actor; // 적 생명 관리자 참조
    private EnemyMeleeCombat melee; // 근접 전투 참조
    [SerializeField] private Transform target; // 플레이어 대상 참조
    private PlayerHealth targetHealth; // 플레이어 체력 참조
    private float verticalVelocity; // 수직 속도

    private void Awake() // 초기 참조 설정
    {
        controller = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        sensor = GetComponent<DetectionSensor>(); // 탐지 센서 조회
        actor = GetComponent<EnemyActor>(); // 적 생명 관리자 조회
        status = GetComponent<EnemyStatusController>(); // 마비 상태 연결
        melee = GetComponent<EnemyMeleeCombat>(); // 적 근접 전투 조회
    }

    private void Update() // 매 프레임 행동 처리
    {
        status = status != null ? status : GetComponent<EnemyStatusController>(); // 마비 참조 재확인
        target = target != null ? target : sensor != null ? sensor.Target : null; // 저장된 센서 대상 재사용
        targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null; // 재실행 후 플레이어 체력 연결

        if (actor == null || actor.IsDead) // 사망 상태 확인
        {
            return; // 행동 중단
        }

        if (actor.IsPostureBroken || (status != null && status.IsStunned)) // 자세 붕괴 확인
        {
            if (melee != null && melee.IsBusy) // 공격 진행 확인
            {
                melee.CancelAttack(); // 공격 취소
            }

            ApplyGravity(); // 중력 유지
            return; // 행동 중단
        }

        if (target == null || targetHealth == null || targetHealth.IsDead) // 플레이어 대상 확인
        {
            ApplyGravity(); // 중력 처리
            return; // 행동 중단
        }

        float distance = PlanarDistance(transform.position, target.position); // 플레이어 거리 계산
        bool detected = sensor != null && sensor.State == DetectionState.Detected && sensor.TargetVisible; // 완전 탐지 상태 계산
        bool suspicious = sensor != null && sensor.HasLastKnownPosition && sensor.State != DetectionState.Idle; // 의심 상태 계산

        if (detected) // 전투 상태 확인
        {
            RotateTowards(target.position - transform.position); // 플레이어 방향 회전

            if (distance <= attackDistance) // 공격 거리 확인
            {
                if (melee != null && !melee.IsBusy) // 공격 가능 상태 확인
                {
                    melee.TryStartAttack(targetHealth); // 검 공격 시작
                }
            }
            else if (melee == null || !melee.IsBusy) // 공격 중 아님 확인
            {
                MoveTowards(target.position, chaseSpeed, stopDistance); // 플레이어 추격
            }
        }
        else if (suspicious && (melee == null || !melee.IsBusy)) // 조사 상태 확인
        {
            MoveTowards(sensor.LastKnownPosition, patrolSpeed, 0.4f); // 마지막 위치 조사
        }

        ApplyGravity(); // 중력 처리
    }

    public void Configure(Transform playerTarget, float newPatrolSpeed, float newChaseSpeed, float newAttackDistance) // 외부 설정 적용
    {
        target = playerTarget; // 플레이어 대상 저장
        targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null; // 플레이어 체력 저장
        patrolSpeed = newPatrolSpeed; // 일반 속도 저장
        chaseSpeed = newChaseSpeed; // 추격 속도 저장
        attackDistance = newAttackDistance; // 공격 거리 저장
    }

    private void MoveTowards(Vector3 destination, float speed, float minimumDistance) // 목적지 이동
    {
        Vector3 direction = destination - transform.position; // 목적지 방향 계산
        direction.y = 0f; // 수직 성분 제거
        float distance = direction.magnitude; // 목적지 거리 계산

        if (distance <= minimumDistance || direction.sqrMagnitude <= 0.0001f) // 이동 정지 조건 확인
        {
            return; // 이동 중단
        }

        direction.Normalize(); // 이동 방향 정규화
        controller.Move(direction * speed * Time.deltaTime); // 수평 이동 적용
        RotateTowards(direction); // 이동 방향 회전
    }

    private void RotateTowards(Vector3 direction) // 목표 방향 회전
    {
        direction.y = 0f; // 수직 성분 제거

        if (direction.sqrMagnitude <= 0.0001f) // 방향 유효성 확인
        {
            return; // 회전 중단
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 회전 계산
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime); // 부드러운 회전 적용
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

    private static float PlanarDistance(Vector3 first, Vector3 second) // 평면 거리 계산
    {
        Vector2 a = new Vector2(first.x, first.z); // 첫 위치 평면 변환
        Vector2 b = new Vector2(second.x, second.z); // 둘째 위치 평면 변환
        return Vector2.Distance(a, b); // 평면 거리 반환
    }
}
