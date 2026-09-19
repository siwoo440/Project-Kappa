using UnityEngine; // 실행 중 사격 상태 수치

public sealed class FirearmHandlingState // 무기마다 유지하는 누적 분산과 소음기 상태
{
    public float Bloom // 현재 추가 분산
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public bool Suppressed // 현재 소음기 장착 상태
    {
        get; // 현재 상태 조회
        set; // 장착 상태 저장
    }
    private float lastShotAt = float.NegativeInfinity; // 마지막 실제 발사 시각
    private float lastTickAt; // 마지막 분산 갱신 시각

    public FirearmHandlingState(float now) // 최초 사격 상태 생성
    {
        lastTickAt = now; // 첫 갱신 기준 저장
    }

    public void Tick(float now, FirearmHandlingProfile profile) // 비장착 시간까지 반영한 분산 복구
    {
        if (profile != null) // 조정 설정 확인
        {
            float begin = Mathf.Max(lastTickAt, lastShotAt + profile.BloomRecoveryDelay); // 회복 가능한 구간 시작
            Bloom = FirearmHandlingMath.Recover(Bloom, now - begin, profile.BloomRecovery); // 지연 이후의 시간만 회복 적용
        }

        lastTickAt = now; // 갱신 시각 저장
    }

    public void RegisterShot(float now, FirearmHandlingProfile profile) // 실제 발사 성공 시에만 분산 누적
    {
        Tick(now, profile); // 이전 발사 이후 회복 반영
        if (profile != null) // 설정 유효성 확인
        {
            Bloom = Mathf.Min(profile.MaximumBloom, Bloom + profile.BloomPerShot); // 누적 분산 한도 적용
        }

        lastShotAt = now; // 회복 지연 재시작
    }
}
