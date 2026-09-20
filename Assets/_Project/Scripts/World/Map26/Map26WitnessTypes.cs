namespace ProjectK.Day26 // 26일차 목격·신고 이름 공간
{
    public enum Map26WitnessState // 시민 목격·신고 진행 상태
    {
        None, // 평상 상태
        Witnessed, // 범죄를 목격하거나 큰 소리를 들은 직후
        Reporting, // 신고 진행 중
        Reported // 신고 완료
    }

    public enum Map26ReportSource // 범죄 신고 출처
    {
        CitizenDirect, // 시민 직접 목격
        CitizenHeard, // 시민이 큰 소리만 들음
        SecurityDirect, // 경비·카메라 직접 시야
        SecurityHeard // 경비·보안 센서 청취
    }
}
