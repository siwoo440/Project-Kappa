using UnityEngine; // 유니티 기본 기능

public abstract class BaseGameData : ScriptableObject // 공통 게임 데이터 기반
{
    [SerializeField] private string id = "NEW-ID"; // 고유 식별자
    [SerializeField] private string displayName = "새 데이터"; // 표시 이름

    public string Id => id; // 식별자 읽기
    public string DisplayName => displayName; // 표시 이름 읽기
}
