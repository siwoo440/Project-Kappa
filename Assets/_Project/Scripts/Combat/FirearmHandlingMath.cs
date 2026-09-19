using UnityEngine; // 공통 사격 수학 기능

public static class FirearmHandlingMath // 조준 표시와 실제 판정의 공통 계산
{
    public static float SpreadAngle(FirearmHandlingProfile profile, bool aiming, bool grounded, bool crouching, float speed, float bloom) // 자세별 분산 반각 계산
    {
        if (profile == null) // 이전 총기 설정 호환 확인
        {
            return 0f; // 조정 설정 없는 총기는 기존 정중앙 발사
        }

        float angle = aiming ? profile.AimSpread : profile.HipSpread; // 조준 상태 기본 분산
        angle += Mathf.Clamp01(Mathf.Max(0f, speed) / profile.ReferenceMoveSpeed) * profile.MovingPenalty; // 실제 수평 속도 보정
        if (!grounded) // 공중 상태 확인
        {
            angle += profile.AirbornePenalty; // 공중 정확도 저하
        }
        else if (crouching) // 지상 앉기 확인
        {
            angle *= profile.CrouchSpreadRatio; // 지상에서만 앉기 정확도 향상
        }

        return Mathf.Clamp(angle + Mathf.Max(0f, bloom), 0f, 25f); // 누적 분산과 최대 반각 제한
    }

    public static float ReticlePixels(float angle, float verticalFov, float pixelHeight) // 분산 반각을 화면 반경으로 변환
    {
        float spread = Mathf.Tan(Mathf.Clamp(angle, 0f, 25f) * Mathf.Deg2Rad); // 분산 원뿔 기울기
        float lens = Mathf.Tan(Mathf.Clamp(verticalFov, 1f, 170f) * 0.5f * Mathf.Deg2Rad); // 카메라 세로 시야 기울기
        return spread * Mathf.Max(1f, pixelHeight) / (2f * lens); // 실제 픽셀 기준 분산 반경
    }

    public static Vector2 ViewportSample(Vector2 unitDisk, float angle, float verticalFov, float pixelWidth, float pixelHeight) // 같은 분산 범위의 발사 표본 계산
    {
        Vector2 disk = Vector2.ClampMagnitude(unitDisk, 1f); // 원 밖의 표본 제한
        float pixels = ReticlePixels(angle, verticalFov, pixelHeight); // HUD와 동일한 투영 반경
        return new Vector2(disk.x * pixels / Mathf.Max(1f, pixelWidth), disk.y * pixels / Mathf.Max(1f, pixelHeight)); // 화면 비율과 종횡비 보정
    }

    public static float Recover(float value, float elapsed, float rate) // 누적 반동과 분산의 시간 기반 회복
    {
        return Mathf.MoveTowards(value, 0f, Mathf.Max(0f, elapsed) * Mathf.Max(0f, rate)); // 부호를 유지하며 영점으로 복귀
    }

    public static bool CanHear(float distance, float hearingRadius, float noiseRadius) // 청각과 소음 범위 공통 검사
    {
        float limit = Mathf.Min(Mathf.Max(0f, hearingRadius), Mathf.Max(0f, noiseRadius)); // 두 반경의 작은 값 사용
        return limit > 0f && distance >= 0f && distance <= limit; // 영점 반경과 범위 밖 소음 제외
    }
}
