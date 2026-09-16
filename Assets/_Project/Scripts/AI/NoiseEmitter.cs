using UnityEngine; // 유니티 기본 기능

[RequireComponent(typeof(PlayerMovement))] // 플레이어 이동 필수 지정
public sealed class NoiseEmitter : MonoBehaviour // 플레이어 게임플레이 소음 발생기
{
    [Header("Footstep")] // 발소리 설정 구분
    [SerializeField] private float walkRadius = 4.5f; // 일반 이동 소음 반경
    [SerializeField] private float sprintRadius = 9f; // 달리기 소음 반경
    [SerializeField] private float crouchRadius = 2f; // 앉기 이동 소음 반경
    [SerializeField] private float walkInterval = 0.5f; // 일반 발소리 간격
    [SerializeField] private float sprintInterval = 0.32f; // 달리기 발소리 간격
    [SerializeField] private float crouchInterval = 0.7f; // 앉기 발소리 간격

    [Header("Landing")] // 착지 설정 구분
    [SerializeField] private float minimumLandingSpeed = 3f; // 착지 소음 최소 낙하 속도
    [SerializeField] private float hardLandingSpeed = 13f; // 강한 착지 기준 속도
    [SerializeField] private float minimumLandingRadius = 4f; // 최소 착지 소음 반경
    [SerializeField] private float maximumLandingRadius = 14f; // 최대 착지 소음 반경

    private PlayerMovement movement; // 플레이어 이동 참조
    private float footstepTimer; // 발소리 남은 시간
    private bool hasGroundSample; // 지면 샘플 존재 여부
    private bool wasGrounded; // 이전 지면 상태
    private float lowestAirVerticalSpeed; // 공중 최저 수직 속도

    private void Awake() // 초기 참조 설정
    {
        movement = GetComponent<PlayerMovement>(); // 플레이어 이동 조회
    }

    private void LateUpdate() // 이동 처리 후 소음 갱신
    {
        if (movement == null) // 이동 참조 확인
        {
            return; // 소음 갱신 중단
        }

        bool grounded = movement.IsGrounded; // 현재 지면 상태 조회

        if (!hasGroundSample) // 초기 지면 샘플 확인
        {
            hasGroundSample = true; // 초기 샘플 저장
            wasGrounded = grounded; // 현재 지면 상태 저장
            lowestAirVerticalSpeed = movement.VerticalVelocity; // 현재 수직 속도 저장
            return; // 첫 프레임 소음 방지
        }

        if (!grounded) // 공중 상태 확인
        {
            lowestAirVerticalSpeed = Mathf.Min(lowestAirVerticalSpeed, movement.VerticalVelocity); // 최저 낙하 속도 저장
        }

        if (grounded && !wasGrounded) // 착지 전환 확인
        {
            EmitLandingNoise(); // 착지 소음 발생
            lowestAirVerticalSpeed = 0f; // 낙하 속도 초기화
        }

        UpdateFootstepNoise(grounded); // 발소리 갱신
        wasGrounded = grounded; // 지면 상태 저장
    }

    private void UpdateFootstepNoise(bool grounded) // 발소리 갱신
    {
        float horizontalSpeed = movement.HorizontalVelocity.magnitude; // 현재 수평 속도 조회
        if (!grounded || horizontalSpeed < 0.35f) // 발소리 발생 조건 확인
        {
            footstepTimer = Mathf.Min(footstepTimer, 0.1f); // 다음 발소리 빠른 재개 준비
            return; // 발소리 처리 중단
        }

        footstepTimer -= Time.deltaTime; // 발소리 시간 감소
        if (footstepTimer > 0f) // 발소리 대기 확인
        {
            return; // 발소리 발생 중단
        }

        if (movement.IsCrouching) // 앉기 이동 확인
        {
            NoiseSystem.Emit(transform.position, crouchRadius, NoiseType.CrouchStep, gameObject); // 앉기 발소리 발생
            footstepTimer = crouchInterval; // 앉기 발소리 간격 적용
            return; // 처리 종료
        }

        if (horizontalSpeed >= 6.25f) // 달리기 속도 확인
        {
            NoiseSystem.Emit(transform.position, sprintRadius, NoiseType.Sprint, gameObject); // 달리기 발소리 발생
            footstepTimer = sprintInterval; // 달리기 발소리 간격 적용
            return; // 처리 종료
        }

        NoiseSystem.Emit(transform.position, walkRadius, NoiseType.Footstep, gameObject); // 일반 발소리 발생
        footstepTimer = walkInterval; // 일반 발소리 간격 적용
    }

    private void EmitLandingNoise() // 착지 소음 발생
    {
        float landingSpeed = Mathf.Abs(Mathf.Min(lowestAirVerticalSpeed, 0f)); // 낙하 속도 절댓값 계산
        if (landingSpeed < minimumLandingSpeed) // 최소 착지 속도 확인
        {
            return; // 착지 소음 생략
        }

        float landingRatio = Mathf.InverseLerp(minimumLandingSpeed, hardLandingSpeed, landingSpeed); // 착지 강도 비율 계산
        float radius = Mathf.Lerp(minimumLandingRadius, maximumLandingRadius, landingRatio); // 착지 소음 반경 계산
        NoiseSystem.Emit(transform.position, radius, NoiseType.Landing, gameObject); // 착지 소음 발생
    }
}
