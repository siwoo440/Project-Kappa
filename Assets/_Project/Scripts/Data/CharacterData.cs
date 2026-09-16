using UnityEngine; // 유니티 기본 기능

[CreateAssetMenu(fileName = "CharacterData", menuName = "Project K/Data/Character")] // 캐릭터 데이터 생성 메뉴
public sealed class CharacterData : BaseGameData // 캐릭터 데이터 정의
{
    [SerializeField] private float maxHealth = 100f; // 최대 체력
    [SerializeField] private float maxPosture = 100f; // 최대 자세
    [SerializeField] private float moveSpeed = 5f; // 기본 이동 속도

    public float MaxHealth => maxHealth; // 최대 체력 읽기
    public float MaxPosture => maxPosture; // 최대 자세 읽기
    public float MoveSpeed => moveSpeed; // 이동 속도 읽기
}
