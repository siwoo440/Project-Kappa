using UnityEngine; // 유니티 기본 기능

[CreateAssetMenu(menuName = "Project K/Day 9/Melee Definition")] // 근접 무기 설정 메뉴
public sealed class MeleeWeaponDefinition : ScriptableObject // 근접 무기 동작과 외형 설정
{
    [SerializeField] private WeaponData stats; // 기존 공통 무기 데이터
    [SerializeField] private GameObject modelPrefab; // 손에 장착할 무기 모형
    [SerializeField, Min(0.01f)] private float hitTime = 0.16f; // 공격 타격 시점
    [SerializeField, Min(0.02f)] private float animationDuration = 0.42f; // 검 휘두르기 시간
    [SerializeField, Min(0.1f)] private float hitRadius = 0.8f; // 공격 판정 반경

    public WeaponData Stats => stats; // 공통 무기 수치 조회
    public GameObject ModelPrefab => modelPrefab; // 무기 모형 조회
    public float HitTime => hitTime; // 타격 시점 조회
    public float AnimationDuration => animationDuration; // 동작 시간 조회
    public float HitRadius => hitRadius; // 타격 반경 조회
    public string DisplayName => stats != null ? stats.DisplayName : name; // 표시 이름 조회

    public void Configure(WeaponData data, GameObject model, float hit, float duration, float radius) // 제작 도구 설정
    {
        stats = data; // 무기 데이터 연결
        modelPrefab = model; // 무기 모형 연결
        animationDuration = Mathf.Max(0.02f, duration); // 동작 시간 보정
        hitTime = Mathf.Clamp(hit, 0.01f, animationDuration); // 타격 시점 보정
        hitRadius = Mathf.Max(0.1f, radius); // 타격 반경 보정
    }
}
