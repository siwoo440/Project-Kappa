using UnityEngine; // 유니티 기능

public sealed class MovementEnergy : MonoBehaviour // 이동 에너지 관리자
{
    [SerializeField] private float maxEnergy = 100f; // 최대 에너지
    [SerializeField] private float recoveryRate = 35f; // 초당 회복량
    [SerializeField] private float startingEnergy = 100f; // 시작 에너지

    private float currentEnergy; // 현재 에너지
    private bool autoRecoveryEnabled = true; // 자동 회복 상태

    public float MaxEnergy => maxEnergy; // 최대 에너지 읽기
    public float CurrentEnergy => currentEnergy; // 현재 에너지 읽기
    public float NormalizedEnergy => maxEnergy <= 0f ? 0f : currentEnergy / maxEnergy; // 정규화 에너지 읽기
    public bool AutoRecoveryEnabled => autoRecoveryEnabled; // 자동 회복 상태 읽기

    private void Awake() // 초기 값 설정
    {
        maxEnergy = Mathf.Max(1f, maxEnergy); // 최대 에너지 보정
        currentEnergy = Mathf.Clamp(startingEnergy, 0f, maxEnergy); // 시작 에너지 적용
    }

    private void Update() // 매 프레임 회복 처리
    {
        if (!autoRecoveryEnabled) // 자동 회복 상태 확인
        {
            return; // 회복 처리 중단
        }

        Recover(recoveryRate * Time.deltaTime); // 에너지 회복 적용
    }

    public void SetAutoRecoveryEnabled(bool enabled) // 자동 회복 상태 변경
    {
        autoRecoveryEnabled = enabled; // 자동 회복 상태 저장
    }

    public bool CanConsume(float amount) // 소모 가능 여부 확인
    {
        return currentEnergy >= amount; // 에너지 보유 여부 반환
    }

    public bool Consume(float amount) // 에너지 소모 처리
    {
        if (amount <= 0f) // 소모량 확인
        {
            return true; // 소모 성공 반환
        }

        if (currentEnergy <= 0f) // 잔량 확인
        {
            currentEnergy = 0f; // 음수 방지
            return false; // 소모 실패 반환
        }

        currentEnergy = Mathf.Max(0f, currentEnergy - amount); // 에너지 감소 적용
        return currentEnergy > 0f; // 남은 에너지 여부 반환
    }

    public void Recover(float amount) // 에너지 회복 처리
    {
        if (amount <= 0f) // 회복량 확인
        {
            return; // 회복 처리 중단
        }

        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount); // 에너지 회복 적용
    }

    public void ResetEnergy() // 에너지 최대치 복구
    {
        currentEnergy = maxEnergy; // 최대치 적용
    }

    public void SetCurrentEnergy(float value) // 현재 에너지 강제 설정
    {
        currentEnergy = Mathf.Clamp(value, 0f, maxEnergy); // 현재 에너지 보정 저장
    }
}
