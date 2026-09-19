using UnityEngine; // 유니티 수학 기능

public static class EquipmentRules // 장비 계산 공통 규칙
{
    public static int WrapIndex(int index, int count) // 순환 선택 번호 계산
    {
        return count <= 0 ? -1 : ((index % count) + count) % count; // 음수와 빈 목록 처리
    }

    public static float ClampedHeal(float current, float maximum, float amount, bool dead) // 초과 회복과 부활 방지
    {
        if (dead || amount <= 0f || current >= maximum) // 회복 불가 조건
        {
            return 0f; // 회복량 없음
        }

        return Mathf.Min(amount, Mathf.Max(0f, maximum - current)); // 최대 체력 이내 회복량
    }

    public static bool SegmentIntersectsSphere(Vector3 start, Vector3 end, Vector3 center, float radius) // 시선 전체와 연막 교차 검사
    {
        Vector3 segment = end - start; // 시선 구간 방향
        float lengthSquared = segment.sqrMagnitude; // 시선 길이 제곱
        float t = lengthSquared > 0.000001f ? Mathf.Clamp01(Vector3.Dot(center - start, segment) / lengthSquared) : 0f; // 구간 안의 최근접 비율
        Vector3 closest = start + segment * t; // 구간 안의 최근접 위치
        float safeRadius = Mathf.Max(0f, radius); // 음수 반경 보정
        return (closest - center).sqrMagnitude <= safeRadius * safeRadius; // 내부와 관통 모두 차단
    }
}
