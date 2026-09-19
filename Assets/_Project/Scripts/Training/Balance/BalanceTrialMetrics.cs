using System; // 유효한 수치와 시간 검사
using System.Collections.Generic; // 발사 기록 보관

[Serializable] // 결과 파일 저장 대상
public sealed class BalanceTrialRecord // 시험 한 회의 조건과 실제 측정 결과
{
    public int schema = 1; // 결과 형식 버전
    public string id; // 시험 고유 번호
    public string createdUtc; // 시험 시작 표준 시각
    public string baseCommit; // 제작 기준 커밋
    public string weaponId; // 실제 무기 식별자
    public string weaponName; // 실제 무기 표시 이름
    public string weaponFingerprint; // 무기 설정 비교용 지문
    public string weaponSnapshot; // 수치 변경 추적용 원본 설정
    public string profileKey; // 같은 시험 조건 구분자
    public string laneName; // 지정한 사격선
    public string hitPolicy; // 허용한 명중 부위
    public string stance; // 요구한 조준과 이동 조건
    public float nominalDistance; // 레인 기준 수평 거리
    public float targetHealth; // 고정한 표적 체력
    public float targetArmor; // 고정한 표적 방어율
    public float targetTravel; // 표적 전체 이동 거리
    public float targetSpeed; // 표적 이동 속도
    public int initialRounds; // 시작 장탄수
    public int initialReserve; // 시작 예비탄
    public string status = "Armed"; // 대기와 완료와 중단 구분
    public string reason = ""; // 중단 또는 완료 이유
    public double firstShotAt = -1; // 첫 발사 게임 시각
    public double lastShotAt = -1; // 마지막 발사 시각 역행 검사
    public double ttk = -1; // 미완료 시험은 음수 유지
    public double elapsed; // 첫 발사 이후 경과 시간
    public int shots; // 실제 탄약을 쓴 발사 횟수
    public int hitShots; // 지정 표적에 한 번 이상 맞은 발사
    public int pellets; // 발사한 전체 펠릿 수
    public int hitPellets; // 지정 표적에 맞은 펠릿 수
    public int headPellets; // 지정 표적 머리에 맞은 펠릿 수
    public double actualDamage; // 남은 체력에서 실제로 줄어든 피해
    public int reloads; // 관측된 재장전 시작 횟수
    public double reloadSeconds; // 재장전 상태 관측 시간
    public double maximumFrameSeconds; // 측정 중 가장 긴 프레임 간격
    public List<BalanceShotSample> samples = new List<BalanceShotSample>(); // 발사별 조건 기록
}

[Serializable] // 발사별 실제 조건 저장
public sealed class BalanceShotSample // 한 번 발사의 표적 적중 결과
{
    public double seconds; // 첫 발사 기준 경과 시간
    public int pellets; // 이번 발사의 전체 펠릿
    public int hits; // 지정 표적에 적중한 펠릿
    public int heads; // 머리에 적중한 펠릿
    public float damage; // 지정 표적의 실제 체력 감소
    public float remaining; // 피격 이후 표적 체력
    public float distance; // 총구부터 표적 중심까지 거리
    public float speed; // 발사 순간 실제 수평 속도
    public float aimProgress; // 발사 순간 조준 진행도
    public float spread; // 발사 순간 분산 반각
    public bool blocked; // 총구 가림 판정
}

public sealed class BalanceTrialMetrics // 유니티에 의존하지 않는 발사 계측 규칙
{
    public BalanceTrialRecord Record // 현재 시험 결과
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public bool IsFinished => Record.status != "Armed" && Record.status != "Running"; // 완료 후 추가 집계 방지
    public static double Percent(int numerator, int denominator) // 영점 분모 안전 비율
    {
        return denominator > 0 ? 100.0 * numerator / denominator : 0; // 실제 표본 비율
    }

    public BalanceTrialMetrics(BalanceTrialRecord record) // 하나의 시험 결과에 연결
    {
        if (record == null) // 필수 결과 자료 확인
        {
            throw new ArgumentNullException(nameof(record)); // 잘못된 초기화 보고
        }
        Record = record; // 외부 저장 객체와 같은 결과 유지
    }

    public bool AddShot(double now, BalanceShotSample sample) // 성공한 발사만 한 번 집계
    {
        if (IsFinished || sample == null || !Finite(now) || now < 0 || sample.pellets <= 0 || sample.hits < 0 || sample.hits > sample.pellets || sample.heads < 0 || sample.heads > sample.hits || !Finite(sample.damage) || sample.damage < 0) // 시간과 적중 범위 검사
        {
            return false; // 오염된 계측 제외
        }
        if (Record.lastShotAt >= 0 && now < Record.lastShotAt) // 시계 역행 확인
        {
            return false; // 음수 측정 방지
        }
        if (Record.firstShotAt < 0) // 최초 유효 발사 확인
        {
            Record.firstShotAt = now; // 명중이 아닌 발사부터 시간 측정
        }
        Record.lastShotAt = now; // 마지막 발사 시각 저장
        sample.seconds = now - Record.firstShotAt; // 해당 발사의 경과 시간
        Record.status = "Running"; // 실제 사격 시작 표시
        Record.elapsed = sample.seconds; // 최신 경과 시간 저장
        Record.shots++; // 펠릿과 무관하게 발사 한 번
        Record.hitShots += sample.hits > 0 ? 1 : 0; // 여러 펠릿 적중도 한 번만 증가
        Record.pellets += sample.pellets; // 실제 발사 펠릿 집계
        Record.hitPellets += sample.hits; // 지정 표적 적중만 집계
        Record.headPellets += sample.heads; // 실제 머리 펠릿 집계
        Record.actualDamage += sample.damage; // 과잉 피해를 제외한 감소량
        Record.samples.Add(sample); // 세부 조건 보관
        return true; // 정상 계측 수용
    }

    public void Finish(string status, string reason, double now) // 완료와 중단을 서로 다른 결과로 보존
    {
        if (IsFinished) // 이미 저장한 상태 확인
        {
            return; // 중복 완료 차단
        }
        if (status == "Armed" || status == "Running" || string.IsNullOrEmpty(status)) // 잘못된 종료 상태 확인
        {
            throw new ArgumentException("종료 상태가 필요합니다."); // 잘못된 호출 보고
        }
        bool completed = status == "Completed" && Record.shots > 0 && Record.firstShotAt >= 0 && Finite(now) && now >= Record.lastShotAt; // 완료 시각 검사
        Record.status = status == "Completed" && !completed ? "Invalid" : status; // 무발사 완료 방지
        Record.reason = reason ?? ""; // 종료 사유 저장
        Record.elapsed = Record.firstShotAt >= 0 && Finite(now) ? Math.Max(0, now - Record.firstShotAt) : 0; // 중단 시험도 경과 시간 보존
        Record.ttk = completed ? Record.elapsed : -1; // 취소와 시간초과를 영초 처치로 계산하지 않음
    }

    public static bool Finite(double value) // 비정상 수치 검사
    {
        return !double.IsNaN(value) && !double.IsInfinity(value); // 저장 가능한 실수 확인
    }
}
