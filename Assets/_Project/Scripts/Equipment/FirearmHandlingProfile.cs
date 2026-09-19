using UnityEngine; // 총기 조정 데이터 기능

[CreateAssetMenu(fileName = "FirearmHandling", menuName = "Project K/Data/Firearm Handling")] // 사격 조정 데이터 생성
public sealed class FirearmHandlingProfile : ScriptableObject // 조준 분산 반동 소음기 설정
{
    [SerializeField, Min(0f)] private float hipSpread = 2.4f; // 비조준 반각 도수
    [SerializeField, Min(0f)] private float aimSpread = 0.4f; // 조준 반각 도수
    [SerializeField, Min(0f)] private float movingPenalty = 1.2f; // 이동 시 추가 분산
    [SerializeField, Min(0f)] private float airbornePenalty = 3f; // 공중 추가 분산
    [SerializeField, Min(0.1f)] private float referenceMoveSpeed = 5f; // 이동 보정 기준 속도
    [SerializeField, Range(0.1f, 1f)] private float crouchSpreadRatio = 0.7f; // 지상 앉기 분산 비율
    [SerializeField, Min(0f)] private float bloomPerShot = 0.6f; // 사격마다 누적 분산
    [SerializeField, Min(0f)] private float maximumBloom = 3f; // 누적 분산 한도
    [SerializeField, Min(0f)] private float bloomRecovery = 1f; // 초당 분산 회복 도수
    [SerializeField, Min(0f)] private float bloomRecoveryDelay = 0.2f; // 분산 회복 시작 지연
    [SerializeField, Min(0f)] private float pitchKick = 1.5f; // 한 발의 위쪽 조준 반동
    [SerializeField, Min(0f)] private float yawKick = 0.3f; // 한 발의 좌우 반동 범위
    [SerializeField, Min(0f)] private float maximumPitchKick = 8f; // 위쪽 누적 반동 한도
    [SerializeField, Min(0f)] private float maximumYawKick = 3f; // 좌우 누적 반동 한도
    [SerializeField, Min(0f)] private float recoilRecovery = 2f; // 초당 반동 복귀 도수
    [SerializeField, Min(0f)] private float recoilRecoveryDelay = 0.25f; // 반동 복귀 시작 지연
    [SerializeField, Range(0.1f, 1f)] private float aimRecoilRatio = 0.7f; // 조준 시 반동 비율
    [SerializeField, Range(0.1f, 1f)] private float aimSensitivityRatio = 0.75f; // 조준 중 마우스 감도 비율
    [SerializeField] private bool supportsSuppressor = true; // 테스트 소음기 사용 허용
    [SerializeField, Range(0f, 1f)] private float suppressedNoiseRatio = 0.2f; // AI 총성 반경 비율
    [SerializeField, Range(0f, 1f)] private float suppressedAudioRatio = 0.25f; // 효과음 음량 비율

    public float HipSpread => Mathf.Max(0f, hipSpread); // 비조준 분산 조회
    public float AimSpread => Mathf.Max(0f, aimSpread); // 조준 분산 조회
    public float MovingPenalty => Mathf.Max(0f, movingPenalty); // 이동 분산 조회
    public float AirbornePenalty => Mathf.Max(0f, airbornePenalty); // 공중 분산 조회
    public float ReferenceMoveSpeed => Mathf.Max(0.1f, referenceMoveSpeed); // 기준 속도 조회
    public float CrouchSpreadRatio => Mathf.Clamp(crouchSpreadRatio, 0.1f, 1f); // 앉기 보정 조회
    public float BloomPerShot => Mathf.Max(0f, bloomPerShot); // 누적 분산 조회
    public float MaximumBloom => Mathf.Max(0f, maximumBloom); // 누적 한도 조회
    public float BloomRecovery => Mathf.Max(0f, bloomRecovery); // 회복 속도 조회
    public float BloomRecoveryDelay => Mathf.Max(0f, bloomRecoveryDelay); // 회복 지연 조회
    public float PitchKick => Mathf.Max(0f, pitchKick); // 위쪽 반동 조회
    public float YawKick => Mathf.Max(0f, yawKick); // 좌우 반동 조회
    public float MaximumPitchKick => Mathf.Max(0f, maximumPitchKick); // 수직 반동 한도 조회
    public float MaximumYawKick => Mathf.Max(0f, maximumYawKick); // 수평 반동 한도 조회
    public float RecoilRecovery => Mathf.Max(0f, recoilRecovery); // 반동 복귀 속도 조회
    public float RecoilRecoveryDelay => Mathf.Max(0f, recoilRecoveryDelay); // 반동 복귀 지연 조회
    public float AimRecoilRatio => Mathf.Clamp(aimRecoilRatio, 0.1f, 1f); // 조준 반동 비율 조회
    public float AimSensitivityRatio => Mathf.Clamp(aimSensitivityRatio, 0.1f, 1f); // 조준 감도 조회
    public bool SupportsSuppressor => supportsSuppressor; // 소음기 허용 조회
    public float SuppressedNoiseRatio => Mathf.Clamp01(suppressedNoiseRatio); // 총성 반경 보정 조회
    public float SuppressedAudioRatio => Mathf.Clamp01(suppressedAudioRatio); // 효과음 보정 조회
}
