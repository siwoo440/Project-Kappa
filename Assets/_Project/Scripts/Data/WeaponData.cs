using UnityEngine; // 유니티 기본 기능

public enum WeaponCategory // 무기 분류
{
    Melee, // 근접 무기
    Sidearm, // 권총 계열
    SubmachineGun, // 기관단총 계열
    AssaultRifle, // 돌격소총 계열
    Shotgun, // 산탄총 계열
    PrecisionSpecial // 정밀 특수화기 계열
}

[CreateAssetMenu(fileName = "WeaponData", menuName = "Project K/Data/Weapon")] // 무기 데이터 생성 메뉴
public sealed class WeaponData : BaseGameData // 무기 데이터 정의
{
    [SerializeField] private WeaponCategory category = WeaponCategory.Melee; // 무기 분류
    [SerializeField] private float healthDamage = 0f; // 체력 피해
    [SerializeField] private float postureDamage = 0f; // 자세 피해
    [SerializeField] private float effectiveRange = 0f; // 유효 거리
    [SerializeField] private float falloffEndRange = 0f; // 감쇠 종료 거리
    [SerializeField] private float noiseRadius = 0f; // 소음 반경
    [SerializeField, Range(0f, 1f)] private float armorPenetration = 0f; // 방어 관통률
    [SerializeField] private int magazineSize = 0; // 탄창 크기
    [SerializeField] private int reserveAmmo = 0; // 예비 탄약
    [SerializeField] private float fireInterval = 0f; // 공격 발사 간격

    public WeaponCategory Category => category; // 무기 분류 읽기
    public float HealthDamage => healthDamage; // 체력 피해 읽기
    public float PostureDamage => postureDamage; // 자세 피해 읽기
    public float EffectiveRange => effectiveRange; // 유효 거리 읽기
    public float FalloffEndRange => falloffEndRange; // 감쇠 종료 거리 읽기
    public float NoiseRadius => noiseRadius; // 소음 반경 읽기
    public float ArmorPenetration => armorPenetration; // 방어 관통률 읽기
    public int MagazineSize => magazineSize; // 탄창 크기 읽기
    public int ReserveAmmo => reserveAmmo; // 예비 탄약 읽기
    public float FireInterval => fireInterval; // 공격 발사 간격 읽기
}
