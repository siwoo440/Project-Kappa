using System; // 시간 범위 보정

public sealed class TrainingTargetCycle // 고정과 이동 표적의 공통 복귀 시간
{
    public enum Phase // 표적 동작 상태
    {
        Ready, // 사격 대기
        Falling, // 뒤로 넘어지는 중
        Down, // 누운 상태
        Rising // 복귀 중
    }

    private float age = -1f; // 피격 이후 경과 시간
    private readonly float fall; // 넘어지는 시간
    private readonly float down; // 누운 상태 유지 시간
    private readonly float rise; // 일어나는 시간
    public bool AcceptsHit => age < 0f; // 일어선 표적만 피격 허용
    public Phase State => age < 0f ? Phase.Ready : age < fall ? Phase.Falling : age < fall + down ? Phase.Down : Phase.Rising; // 현재 단계
    public float Tilt // 영점은 서기 상태
    {
        get // 현재 기울기 비율 조회
        {
            if (age < 0f) // 피격 전 확인
            {
                return 0f; // 정면 유지
            }
            float t = age < fall ? age / fall : age < fall + down ? 1f : 1f - (age - fall - down) / rise; // 단계별 기울기
            t = Math.Max(0f, Math.Min(1f, t)); // 유효한 범위 제한
            return t * t * (3f - 2f * t); // 부드러운 가속과 감속
        }
    }

    public TrainingTargetCycle(float fallSeconds, float downSeconds, float riseSeconds) // 표적 시간 설정
    {
        fall = Math.Max(0.05f, fallSeconds); // 너무 짧은 넘어짐 방지
        down = Math.Max(0f, downSeconds); // 음수 유지 시간 방지
        rise = Math.Max(0.05f, riseSeconds); // 너무 짧은 복귀 방지
    }

    public bool Hit() // 피격 상태 진입
    {
        if (!AcceptsHit) // 이미 넘어진 상태 확인
        {
            return false; // 중복 피격 거부
        }
        age = 0f; // 넘어짐 시작
        return true; // 피격 수락
    }

    public void Tick(float delta) // 경과 시간 반영
    {
        if (age < 0f || float.IsNaN(delta) || float.IsInfinity(delta)) // 대기 또는 무효 시간 확인
        {
            return; // 시간 갱신 생략
        }
        age += Math.Max(0f, delta); // 음수 시간 역행 방지
        if (age >= fall + down + rise) // 복귀 완료 확인
        {
            Reset(); // 다음 피격 준비
        }
    }

    public void Reset() // 단말기와 자동 복귀 공통 초기화
    {
        age = -1f; // 서기 상태 복구
    }
}
