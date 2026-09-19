using System; // 정밀 사격 시각 계산

public sealed class FirearmTriggerState // 프레임 속도와 분리한 방아쇠 예약
{
    private bool automaticHeld; // 자동 사격 입력 유지 상태
    private double nextScheduledAt; // 다음 연사 예약 시각
    private const double Tolerance = 0.000001; // 시간 경계 오차 허용값

    public int Collect(double now, bool pressed, bool held, FirearmFireMode mode, double interval, double earliest, double[] output) // 이번 프레임에 처리할 발사 시각 수집
    {
        if (output == null || output.Length == 0 || double.IsNaN(now) || double.IsInfinity(now)) // 유효한 시간과 출력 공간 확인
        {
            Reset(); // 잘못된 예약 정리
            return 0; // 발사 예약 없음
        }

        interval = double.IsNaN(interval) || double.IsInfinity(interval) ? 0.1 : Math.Max(0.02, interval); // 유효한 연사 간격 확보
        if (mode == FirearmFireMode.SemiAutomatic) // 반자동 입력 처리
        {
            Reset(); // 이전 자동 연사 예약 제거
            if (!pressed || now + Tolerance < earliest) // 새 클릭과 발사 대기 확인
            {
                return 0; // 누르고 있기와 대기 중 클릭 제외
            }

            output[0] = now; // 이번 클릭 한 발 예약
            return 1; // 반자동 최대 한 발 반환
        }

        if (!held) // 자동 사격 버튼 해제 확인
        {
            Reset(); // 남은 자동 사격 즉시 취소
            return 0; // 해제 프레임 발사 없음
        }

        if (!automaticHeld) // 새로운 자동 사격 시작
        {
            nextScheduledAt = Math.Max(now, earliest); // 오래된 대기 시간을 몰아서 발사하지 않도록 보정
            automaticHeld = true; // 연사 유지 상태 저장
        }

        nextScheduledAt = Math.Max(nextScheduledAt, earliest); // 탄약 상태의 실제 발사 간격 보존
        int count = 0; // 이번 프레임 발사 수
        while (nextScheduledAt <= now + Tolerance && count < output.Length) // 제한된 개수만 누락 프레임 보정
        {
            output[count++] = nextScheduledAt; // 예정 시각 순서대로 한 발 기록
            nextScheduledAt += interval; // 프레임 끝이 아닌 예정 시각 기준 간격 누적
        }

        if (count == output.Length && nextScheduledAt <= now + Tolerance) // 긴 멈춤의 과도한 밀린 발사 확인
        {
            nextScheduledAt = now + interval; // 남은 밀린 발사는 폐기하고 정상 간격 복구
        }

        return count; // 실제 예약 개수 반환
    }

    public void Reset() // 장비 교체와 행동 중단의 예약 정리
    {
        automaticHeld = false; // 자동 사격 유지 해제
        nextScheduledAt = 0; // 이전 무기 예약 삭제
    }
}
