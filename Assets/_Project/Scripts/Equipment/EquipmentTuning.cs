using UnityEngine; // 유니티 기본 기능

[CreateAssetMenu(menuName = "Project K/Day 9/Equipment Tuning")] // 장비 테스트 수치 메뉴
public sealed class EquipmentTuning : ScriptableObject // 보조장비와 소모품 테스트 설정
{
    [Header("Dart - Prototype")] // 마비침 테스트 수치
    [Min(0.1f)] public float dartRange = 20f; // 마비침 사거리
    [Min(0.1f)] public float stunDuration = 3f; // 마비 지속 시간
    [Min(0.1f)] public float dartCooldown = 1.2f; // 마비침 재사용 시간
    [Min(1)] public int maxDarts = 6; // 테스트 최대 마비침 수
    [Min(0f)] public float dartNoiseRadius = 2f; // 마비침 발사 소음
    public GameObject dartModel; // 보조장비 모형

    [Header("Consumables")] // 소모품 테스트 수치
    [Min(1f)] public float healingAmount = 40f; // 회복 주입기 회복량
    [Min(1)] public int maxConsumables = 3; // 종류별 테스트 지급 수
    [Min(0.05f)] public float useDuration = 0.45f; // 사용 중 장비 잠금 시간
    [Min(0.1f)] public float throwSpeed = 8f; // 투척 수평 속도
    [Min(0.1f)] public float throwLift = 3f; // 투척 상승 속도
    [Min(0.1f)] public float lureRadius = 12f; // 유인 소음 반경
    [Min(0.1f)] public float lureDuration = 6f; // 유인 소음 지속 시간
    [Min(0.1f)] public float smokeRadius = 4f; // 연막 차단 반경
    [Min(0.1f)] public float smokeDuration = 5f; // 연막 지속 시간
    public GameObject injectorModel; // 회복 주입기 모형
    public GameObject lureModel; // 소음 유인기 모형
    public GameObject smokeModel; // 연막 캡슐 모형
    public Material effectMaterial; // 선과 마비 표시 재질
    public Material smokeMaterial; // 연막 입자 재질
}
