using UnityEngine; // 유니티 기본 기능

public enum MissionType // 미션 유형
{
    Main, // 메인 미션
    Side, // 서브 의뢰
    Delivery, // 배달 미션
    Assassination, // 암살 미션
    Investigation // 조사 미션
}

[CreateAssetMenu(fileName = "MissionData", menuName = "Project K/Data/Mission")] // 미션 데이터 생성 메뉴
public sealed class MissionData : BaseGameData // 미션 데이터 정의
{
    [SerializeField] private MissionType missionType = MissionType.Main; // 미션 유형
    [SerializeField] private string startScene = ProjectKConstants.HubScene; // 시작 씬
    [SerializeField] private string[] prerequisiteMissionIds = new string[0]; // 선행 미션 목록
    [SerializeField] private int creditReward = 0; // 크레딧 보상

    public MissionType MissionType => missionType; // 미션 유형 읽기
    public string StartScene => startScene; // 시작 씬 읽기
    public string[] PrerequisiteMissionIds => prerequisiteMissionIds; // 선행 미션 읽기
    public int CreditReward => creditReward; // 보상 읽기
}
