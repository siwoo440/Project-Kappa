using UnityEngine; // 공통 무기 에셋 기능

[CreateAssetMenu(fileName = "FirearmDefinition", menuName = "Project K/Data/Firearm Definition")] // 총기 정의 생성 메뉴
public sealed class FirearmDefinition : ScriptableObject // 총기 고정 설정과 모형 연결
{
    [SerializeField] private WeaponData stats; // 기존 공통 무기 수치
    [SerializeField] private GameObject modelPrefab; // 총기 모형 프리팹
    [SerializeField, Min(0.05f)] private float reloadDuration = 1.4f; // 검증용 재장전 시간
    [SerializeField, Min(1f)] private float maximumRange = 40f; // 실제 발사 최대 거리
    [SerializeField, Range(0f, 1f)] private float minimumDamageRatio = 0.5f; // 사거리 끝 피해 비율
    [SerializeField, Range(0.5f, 1f)] private float aimFovRatio = 0.8f; // 조준 중 시야각 비율
    [SerializeField] private Material tracerMaterial; // 궤적과 명중 효과 재질

    public WeaponData Stats => stats; // 공통 수치 조회
    public GameObject ModelPrefab => modelPrefab; // 프리팹 조회
    public string DisplayName => stats != null ? stats.DisplayName : "검증용 권총"; // 표시 이름 조회
    public float ReloadDuration => Mathf.Max(0.05f, reloadDuration); // 재장전 시간 조회
    public float MaximumRange => Mathf.Max(1f, maximumRange); // 최대 사거리 조회
    public float AimFovRatio => Mathf.Clamp(aimFovRatio, 0.5f, 1f); // 조준 배율 조회
    public Material TracerMaterial => tracerMaterial; // 궤적 재질 조회
    public bool IsValid => stats != null && stats.Category != WeaponCategory.Melee && stats.MagazineSize > 0 && modelPrefab != null; // 발사 가능한 정의 검사

    public void Configure(WeaponData data, GameObject prefab, Material tracer) // 최초 에셋 참조 설정
    {
        stats = data; // 공통 무기 연결
        modelPrefab = prefab; // 표시 모형 연결
        tracerMaterial = tracer; // 효과 재질 연결
    }

    public float DamageMultiplier(float distance) // 거리에 따른 피해 비율
    {
        float effective = stats != null ? Mathf.Max(0f, stats.EffectiveRange) : MaximumRange; // 유효 사거리 확인
        float end = stats != null && stats.FalloffEndRange > effective ? stats.FalloffEndRange : MaximumRange; // 감쇠 끝점 선택
        if (end <= effective || distance <= effective) // 감쇠 없는 구간 확인
        {
            return 1f; // 기본 피해 유지
        }

        return Mathf.Lerp(1f, Mathf.Clamp01(minimumDamageRatio), Mathf.InverseLerp(effective, end, distance)); // 거리 감쇠 적용
    }
}
