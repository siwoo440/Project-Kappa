using UnityEngine; // 유니티 기본 기능

[CreateAssetMenu(fileName = "ItemData", menuName = "Project K/Data/Item")] // 아이템 데이터 생성 메뉴
public sealed class ItemData : BaseGameData // 아이템 데이터 정의
{
    [SerializeField] private int maxStack = 1; // 최대 중첩 수량
    [SerializeField] private bool missionItem = false; // 임무 물품 여부
    [SerializeField] private bool canDiscard = true; // 버리기 가능 여부

    public int MaxStack => maxStack; // 최대 중첩 읽기
    public bool MissionItem => missionItem; // 임무 물품 여부 읽기
    public bool CanDiscard => canDiscard; // 버리기 가능 여부 읽기
}
