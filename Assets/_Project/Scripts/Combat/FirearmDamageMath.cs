using System; // 피해 수치 제한 기능

public static class FirearmDamageMath // 부위 피해와 방어 관통의 공통 계산
{
    public static float Resolve(float baseDamage, float distanceRatio, float armor, float penetration) // 최종 체력 피해 계산
    {
        float damage = SafePositive(baseDamage); // 음수와 무효 피해 제외
        float distance = Unit(distanceRatio); // 거리 피해 비율 제한
        float remainingArmor = Unit(armor) * (1f - Unit(penetration)); // 관통하지 못한 방어율 계산
        return damage * distance * (1f - remainingArmor); // 체력 피해에 거리와 방어 적용
    }

    private static float Unit(float value) // 영점부터 일 사이 비율 보정
    {
        return Math.Min(1f, SafePositive(value)); // 유효한 비율 반환
    }

    private static float SafePositive(float value) // 유효한 양수 수치 보정
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value); // 잘못된 수치의 피해 전파 방지
    }
}
