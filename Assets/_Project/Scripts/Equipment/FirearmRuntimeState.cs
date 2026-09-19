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
    private double nextShotAt; // 오래 실행해도 정밀한 다음 발사 시각
    public float NextShotAt => (float)nextShotAt; // 기존 코드용 발사 시각 조회
    public double NextShotTime => nextShotAt; // 자동 연사용 정밀 시각 조회
    private readonly double fireInterval; // 한 발 사이의 최소 간격
    private float reloadStartedAt; // 재장전 시작 시각
    private float reloadEndsAt; // 재장전 완료 시각
    private float reloadStep; // 한 발 삽입 시간
    private double cycleStartedAt; // 펌프와 볼트 시작 시각
    private double cycleDuration; // 동작 준비 시간
    public double CycleReadyAt // 교체해도 유지되는 사격 준비 시각
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    } = double.NegativeInfinity;
    public bool SingleRoundReload // 보충 방식
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }

    public void ConfigureMechanism(bool singleRound, double cycleSeconds) // 무기별 장전과 준비 규칙
    {
        SingleRoundReload = singleRound; // 한 발 보충 여부 저장
        cycleDuration = double.IsNaN(cycleSeconds) || double.IsInfinity(cycleSeconds) ? 0 : Math.Max(0, cycleSeconds); // 유효한 동작 시간
    }

    public bool IsCycling(double now) // 펌프와 볼트 진행 상태
    {
        return now < CycleReadyAt; // 대기 시각 확인
    }

    public float CycleProgress(double now) // 동작 표시 진행도
    {
        return cycleDuration > 0 ? (float)Math.Max(0, Math.Min(1, (now - cycleStartedAt) / cycleDuration)) : 1f; // 영점 시간 안전 처리
    }

    public FirearmRuntimeState(int capacity, int initialReserve, float interval) // 새 총기 상태 초기화
    {
        Capacity = Math.Max(1, capacity); // 유효한 탄창 용량 확보
        ReserveLimit = Math.Max(0, initialReserve); // 예비탄 음수 방지
        Rounds = Capacity; // 최초 장탄수 지급
        Reserve = ReserveLimit; // 최초 예비탄 지급
        fireInterval = Math.Max(0.02f, interval); // 연사 간격 최소값 확보
        nextShotAt = double.NegativeInfinity; // 시작 직후 발사 허용
    }

    public bool TryFire(float now) // 이전 일차의 단발 호출 호환
    {
        return TryFireScheduled(now); // 공통 탄약 차감으로 연결
    }

    public bool TryFireScheduled(double now) // 실제 예약 시각 기준 한 발 발사
    {
        if (double.IsNaN(now) || double.IsInfinity(now) || IsReloading || Rounds <= 0 || now + 0.000001 < nextShotAt || now + 0.000001 < CycleReadyAt) // 시간과 탄수와 발사 간격 검사
        {
            return false; // 실패 시 탄약 보존
        }

        cycleStartedAt = now; // 탄약을 소모한 동작만 시작
        CycleReadyAt = now + cycleDuration; // 무기를 바꿔도 준비 시간 유지
        Rounds--; // 실제 발사 한 발 소모
        nextShotAt = now + fireInterval; // 다음 발사 시각 갱신
        return true; // 발사 성공 반환
    }

    public bool TryBeginReload(float now, float duration) // 재장전 시작 시도
    {
        if (IsReloading || Rounds >= Capacity || Reserve <= 0 || IsCycling(now)) // 중복 재장전과 보충 가능량 검사
        {
            return false; // 탄약 이동 없는 실패
        }

        IsReloading = true; // 재장전 상태 저장
        reloadStartedAt = now; // 시작 시각 저장
        reloadStep = Math.Max(0.05f, duration); // 한 번 보충하는 시간
        reloadEndsAt = now + reloadStep; // 완료 시각 저장
        return true; // 재장전 시작 성공
    }

    public bool TickReload(float now) // 재장전 완료 시점의 탄약 이동
    {
        if (!IsReloading || now < reloadEndsAt) // 재장전 완료 여부 확인
        {
            return false; // 완료 전 탄약 보존
        }

        if (SingleRoundReload) // 한 발 삽입 방식
        {
            int elapsedSteps = 1 + (int)Math.Min(Capacity, Math.Max(0, Math.Floor((now - reloadEndsAt) / reloadStep))); // 지연 프레임의 완료 횟수 제한
            int inserted = Math.Min(elapsedSteps, Math.Min(Capacity - Rounds, Reserve)); // 탄수 한도와 완료된 삽입만 사용
            Rounds += inserted; // 완료한 탄약만 보충
            Reserve -= inserted; // 같은 수량 예비탄 차감
            reloadStartedAt = reloadEndsAt + (inserted - 1) * reloadStep; // 다음 삽입 시작점
            reloadEndsAt += inserted * reloadStep; // 다음 완료 시각
            if (Rounds >= Capacity || Reserve <= 0) // 더 넣을 탄약 또는 공간 확인
            {
                CancelReload(); // 자동 반복 종료
            }
            return inserted > 0; // 실제 보충 여부
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
        nextShotAt = double.NegativeInfinity; // 보급 후 발사 대기 해제
        CycleReadyAt = double.NegativeInfinity; // 보급 이후 동작 대기 해제
        cycleStartedAt = double.NegativeInfinity; // 이전 동작 표시 제거
    }
}
