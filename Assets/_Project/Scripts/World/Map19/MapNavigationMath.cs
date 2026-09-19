using UnityEngine; // 지도 좌표와 화면 크기 계산

namespace ProjectK.Day19 // 19일차 월드 지도 이름 공간
{
    public static class MapNavigationMath // 런타임과 검사에서 공유하는 지도 계산
    {
        public static Vector2 WorldToViewport(Vector3 worldPosition, Vector2 center, float worldSpan, Rect rect) // 월드 좌표를 지도 화면 좌표로 변환
        {
            float span = Mathf.Max(1f, worldSpan); // 영점 범위 방지
            float left = center.x - span * 0.5f; // 지도 왼쪽 월드 좌표
            float bottom = center.y - span * 0.5f; // 지도 아래 월드 좌표
            float u = (worldPosition.x - left) / span; // 가로 정규화 좌표
            float v = (worldPosition.z - bottom) / span; // 세로 정규화 좌표
            return new Vector2(rect.x + u * rect.width, rect.y + (1f - v) * rect.height); // 화면 좌표 반환
        }

        public static bool IsInside(Vector3 worldPosition, Vector2 center, float worldSpan) // 현재 지도 범위 안쪽 여부 계산
        {
            float half = Mathf.Max(1f, worldSpan) * 0.5f; // 지도 반경 계산
            return Mathf.Abs(worldPosition.x - center.x) <= half && Mathf.Abs(worldPosition.z - center.y) <= half; // 가로 세로 범위 검사
        }

        public static Vector2 ClampCenter(Vector2 center, float worldSpan, float worldSize) // 확대 지도 중심을 월드 안쪽으로 제한
        {
            float size = Mathf.Max(1f, worldSize); // 전체 월드 크기 보정
            float span = Mathf.Clamp(worldSpan, 1f, size); // 현재 표시 범위 보정
            float allowance = Mathf.Max(0f, (size - span) * 0.5f); // 이동 가능한 중심 범위 계산
            return new Vector2(Mathf.Clamp(center.x, -allowance, allowance), Mathf.Clamp(center.y, -allowance, allowance)); // 제한된 중심 반환
        }

        public static float FullMapSpan(float worldSize, float zoom) // 전체 지도 확대 배율을 실제 월드 범위로 변환
        {
            return Mathf.Max(1f, worldSize) / Mathf.Clamp(zoom, 1f, 4f); // 1배에서 4배 확대 범위 반환
        }

        public static float MiniMapPixelSize(int level, float screenWidth, float screenHeight) // 화면 해상도에 맞는 미니맵 표시 크기 계산
        {
            float shortSide = Mathf.Max(320f, Mathf.Min(screenWidth, screenHeight)); // 짧은 화면 축 기준
            float baseSize = Mathf.Clamp(shortSide * 0.30f, 186f, 300f); // 중간 단계 기본 크기
            int safeLevel = Mathf.Clamp(level, 0, 2); // 세 단계 범위 제한
            float multiplier = safeLevel == 0 ? 0.78f : safeLevel == 1 ? 1f : 1.30f; // 소형 중형 대형 배율
            return Mathf.Clamp(baseSize * multiplier, 170f, 420f); // 실제 화면 크기 제한
        }

        public static float ClampMiniMapSpan(float value) // 미니맵 확대 범위 제한
        {
            return Mathf.Clamp(value, 120f, 600f); // 너무 좁거나 넓은 표시 범위 방지
        }
    }
}
