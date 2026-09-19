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

    [SerializeField] private FirearmHandlingProfile handling; // 11일차 분산 반동 소음기 설정

    [SerializeField] private FirearmFireMode fireMode; // 12일차 반자동과 자동 구분
    [SerializeField, Min(0f)] private float equipDuration = 0.25f; // 장착 후 사용 대기
    [SerializeField, Min(0.01f)] private float aimDuration = 0.18f; // 완전 조준까지 걸리는 시간
    [SerializeField, Range(0.1f, 1.2f)] private float movementMultiplier = 1f; // 기본 이동 대비 장착 속도
    [SerializeField, Min(0f)] private float headDamage; // 영점이면 기존 몸통 피해 사용
    [SerializeField, Range(1, 16)] private int pelletCount = 1; // 한 발에서 검사할 탄환 수
    [SerializeField] private bool singleRoundReload; // 한 발씩 보충하는 재장전
    [SerializeField, Min(0f)] private float cycleDuration; // 펌프 또는 볼트 준비 시간
    [SerializeField] private bool boltAction; // 볼트와 펌프 표시 구분
    [SerializeField] private bool scopeEnabled; // 저격 전용 조준 표시
    [SerializeField, Range(0.1f, 1f)] private float scopeFovRatio = 0.3f; // 저격용 시야각 배율
    public int PelletCount => Mathf.Clamp(pelletCount, 1, 16); // 기존 총기는 한 발 유지
    public bool SingleRoundReload => singleRoundReload; // 장전 방식 조회
    public float CycleDuration => Mathf.Max(0f, cycleDuration); // 다음 사격 준비 시간
    public bool BoltAction => boltAction; // 볼트 동작 여부
    public bool ScopeEnabled => scopeEnabled; // 저격 표시 여부
    public FirearmFireMode FireMode => fireMode; // 방아쇠 방식 조회
    public float EquipDuration => Mathf.Max(0f, equipDuration); // 총기별 장착 지연 조회
    public float AimDuration => Mathf.Max(0.01f, aimDuration); // 조준 전환 시간 조회
    public float MovementMultiplier => Mathf.Clamp(movementMultiplier, 0.1f, 1.2f); // 이동 보정 조회
    public float HeadDamage => headDamage > 0f ? headDamage : stats != null ? stats.HealthDamage : 0f; // 기존 권총 부위 피해 호환
    public string FireModeLabel => cycleDuration > 0f ? (boltAction ? "볼트" : "펌프") : fireMode == FirearmFireMode.Automatic ? "자동" : "반자동"; // HUD 발사 방식 이름

    public FirearmHandlingProfile Handling => handling; // 공통 사격 감각 설정 조회
    public WeaponData Stats => stats; // 공통 수치 조회
    public GameObject ModelPrefab => modelPrefab; // 프리팹 조회
    public string DisplayName => stats != null ? stats.DisplayName : "검증용 권총"; // 표시 이름 조회
    public float ReloadDuration => Mathf.Max(0.05f, reloadDuration); // 재장전 시간 조회
    public float MaximumRange => Mathf.Max(1f, maximumRange); // 최대 사거리 조회
    public float AimFovRatio => scopeEnabled ? Mathf.Clamp(scopeFovRatio, 0.1f, 1f) : Mathf.Clamp(aimFovRatio, 0.5f, 1f); // 조준 배율 조회
    public Material TracerMaterial => tracerMaterial; // 궤적 재질 조회
    public bool IsValid => stats != null && stats.Category != WeaponCategory.Melee && stats.MagazineSize > 0 && modelPrefab != null; // 발사 가능한 정의 검사

    public void Configure(WeaponData data, GameObject prefab, Material tracer) // 최초 에셋 참조 설정
    {
        stats = data; // 공통 무기 연결
        modelPrefab = prefab; // 표시 모형 연결
        tracerMaterial = tracer; // 효과 재질 연결
    }

    public void ConfigureHandling(FirearmHandlingProfile profile, GameObject prefab) // 사격 설정과 개선 모형 연결
    {
        handling = profile; // 기존 무기 수치는 유지한 조정 자료 연결
        modelPrefab = prefab; // 소음기 지원 모형 연결
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
