using UnityEngine; // 유니티 기본 기능

[CreateAssetMenu(fileName = "EnemyData", menuName = "Project K/Data/Enemy")] // 적 데이터 생성 메뉴
public sealed class EnemyData : BaseGameData // 적 데이터 정의
{
    [SerializeField] private float maxHealth = 100f; // 최대 체력
    [SerializeField] private float maxPosture = 100f; // 최대 자세
    [SerializeField] private float patrolSpeed = 3.5f; // 순찰 속도
    [SerializeField] private float chaseSpeed = 5f; // 추격 속도
    [SerializeField] private float visionDistance = 18f; // 시야 거리
    [SerializeField] private float visionAngle = 90f; // 시야 각도
    [SerializeField] private float hearingRadius = 10f; // 청각 반경
    [SerializeField] private bool canBeAssassinated = true; // 암살 가능 여부

    public float MaxHealth => maxHealth; // 최대 체력 읽기
    public float MaxPosture => maxPosture; // 최대 자세 읽기
    public float PatrolSpeed => patrolSpeed; // 순찰 속도 읽기
    public float ChaseSpeed => chaseSpeed; // 추격 속도 읽기
    public float VisionDistance => visionDistance; // 시야 거리 읽기
    public float VisionAngle => visionAngle; // 시야 각도 읽기
    public float HearingRadius => hearingRadius; // 청각 반경 읽기
    public bool CanBeAssassinated => canBeAssassinated; // 암살 가능 여부 읽기
}
