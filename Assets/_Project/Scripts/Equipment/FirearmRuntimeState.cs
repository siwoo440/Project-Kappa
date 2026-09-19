using System; // 탄약 수치 계산 기능

public sealed class FirearmRuntimeState // 총기 한 자루의 실행 중 탄약 상태
{
    public int Capacity // 탄창 최대 용량
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public int Rounds // 현재 장탄수
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public int Reserve // 현재 예비탄
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public int ReserveLimit // 훈련 보급 예비탄 한도
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public bool IsReloading // 재장전 진행 상태
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public float NextShotAt // 다음 발사 가능 시각
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    private readonly float fireInterval; // 한 발 사이의 최소 간격
    private float reloadStartedAt; // 재장전 시작 시각
    private float reloadEndsAt; // 재장전 완료 시각

    public FirearmRuntimeState(int capacity, int initialReserve, float interval) // 새 총기 상태 초기화
    {
        Capacity = Math.Max(1, capacity); // 유효한 탄창 용량 확보
        ReserveLimit = Math.Max(0, initialReserve); // 예비탄 음수 방지
        Rounds = Capacity; // 최초 장탄수 지급
        Reserve = ReserveLimit; // 최초 예비탄 지급
        fireInterval = Math.Max(0.02f, interval); // 연사 간격 최소값 확보
        NextShotAt = float.NegativeInfinity; // 시작 직후 발사 허용
    }

    public bool TryFire(float now) // 한 발 발사와 탄수 소모
    {
        if (IsReloading || Rounds <= 0 || now < NextShotAt) // 재장전과 탄수와 발사 간격 검사
        {
            return false; // 실패 시 탄약 보존
        }

        Rounds--; // 실제 발사 한 발 소모
        NextShotAt = now + fireInterval; // 다음 발사 시각 갱신
        return true; // 발사 성공 반환
    }

    public bool TryBeginReload(float now, float duration) // 재장전 시작 시도
    {
        if (IsReloading || Rounds >= Capacity || Reserve <= 0) // 중복 재장전과 보충 가능량 검사
        {
            return false; // 탄약 이동 없는 실패
        }

        IsReloading = true; // 재장전 상태 저장
        reloadStartedAt = now; // 시작 시각 저장
        reloadEndsAt = now + Math.Max(0.05f, duration); // 완료 시각 저장
        return true; // 재장전 시작 성공
    }

    public bool TickReload(float now) // 재장전 완료 시점의 탄약 이동
    {
        if (!IsReloading || now < reloadEndsAt) // 재장전 완료 여부 확인
        {
            return false; // 완료 전 탄약 보존
        }

        int transfer = Math.Min(Capacity - Rounds, Reserve); // 빈 탄창과 예비탄 중 작은 값 계산
        Rounds += transfer; // 탄창에 실제 보충량 추가
        Reserve -= transfer; // 같은 양의 예비탄 차감
        CancelReload(); // 재장전 상태 종료
        return true; // 보충 완료 반환
    }

    public void CancelReload() // 탄약 이동 없이 재장전 취소
    {
        IsReloading = false; // 재장전 상태 해제
        reloadStartedAt = 0f; // 시작 시각 초기화
        reloadEndsAt = 0f; // 완료 시각 초기화
    }

    public float ReloadProgress(float now) // 재장전 게이지 비율 계산
    {
        if (!IsReloading) // 재장전 여부 확인
        {
            return 0f; // 대기 상태 게이지 숨김 값
        }

        float duration = Math.Max(0.05f, reloadEndsAt - reloadStartedAt); // 나눗셈 시간 보정
        return Math.Max(0f, Math.Min(1f, (now - reloadStartedAt) / duration)); // 진행도 범위 보정
    }

    public void Refill() // 훈련장 전용 보급
    {
        CancelReload(); // 보급 중 재장전 중복 차감 방지
        Rounds = Capacity; // 탄창 보충
        Reserve = ReserveLimit; // 예비탄 보충
        NextShotAt = float.NegativeInfinity; // 보급 후 발사 대기 해제
    }
}
