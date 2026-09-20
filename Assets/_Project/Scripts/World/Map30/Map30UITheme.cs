using UnityEngine; // 공통 HUD 색상과 패널 그리기

namespace ProjectK.Day30 // 30일차 UI 통합 테마 이름 공간
{
    public static class Map30UITheme // 일반 HUD와 Tab 임무 창이 공유하는 청록·파란색 테마
    {
        public static readonly Color Background = new Color(0.012f, 0.030f, 0.055f, 0.94f); // 기본 짙은 남청색 배경
        public static readonly Color BackgroundStrong = new Color(0.010f, 0.024f, 0.045f, 0.985f); // 전체 화면용 진한 배경
        public static readonly Color Inner = new Color(0.020f, 0.070f, 0.105f, 0.82f); // 내부 데이터 영역
        public static readonly Color Cyan = new Color(0.06f, 0.84f, 0.98f, 1f); // 주 청록 네온
        public static readonly Color Blue = new Color(0.12f, 0.46f, 0.82f, 1f); // 보조 파랑
        public static readonly Color Soft = new Color(0.10f, 0.38f, 0.58f, 0.72f); // 얇은 보조선
        public static readonly Color Text = new Color(0.88f, 0.96f, 1f, 1f); // 주요 글자
        public static readonly Color Muted = new Color(0.48f, 0.72f, 0.82f, 1f); // 보조 글자
        public static readonly Color Green = new Color(0.28f, 1f, 0.68f, 1f); // 완료·정상 상태
        public static readonly Color Amber = new Color(1f, 0.68f, 0.20f, 1f); // 경고·보유 상태
        public static readonly Color Danger = new Color(1f, 0.30f, 0.34f, 1f); // 위험 상태만 유지할 적색

        public static bool HideGameplayHUD => Map30MissionWindow.IsOpen || ProjectK.Day32.Map32MissionResultScreen.IsOpen || ProjectK.Day35.Map35PauseMenuHUD.IsOpen; // Tab 임무 창·결과 화면·ESC 메뉴 동안 일반 플레이 HUD 숨김

        public static void DrawPanel(Rect rect, bool strong = false) // 공통 청록 패널 출력
        {
            DrawSolid(rect, strong ? BackgroundStrong : Background); // 기본 남청색 배경
            DrawSolid(new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(0f, rect.width - 8f), Mathf.Max(0f, rect.height - 8f)), Inner); // 내부 데이터 배경
            DrawBorder(rect, Cyan, 2f); // 주 청록 외곽선
            DrawBorder(new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(0f, rect.width - 8f), Mathf.Max(0f, rect.height - 8f)), Soft, 1f); // 이중 프레임
            DrawSolid(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Min(58f, rect.width * 0.28f), 3f), Cyan); // 좌측 상단 강조선
            DrawSolid(new Rect(rect.xMax - Mathf.Min(78f, rect.width * 0.32f), rect.y + 1f, Mathf.Min(77f, rect.width * 0.31f), 3f), Blue); // 우측 상단 강조선
        }

        public static void DrawDivider(Rect rect) // 공통 데이터 구분선
        {
            DrawSolid(rect, Soft); // 보조 청록선 출력
        }

        public static void DrawBar(Rect rect, float normalized, Color fill) // 공통 수치 게이지 출력
        {
            DrawSolid(rect, new Color(0.01f, 0.05f, 0.08f, 0.95f)); // 게이지 바탕
            DrawSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(normalized), rect.height), fill); // 현재 수치 채움
            DrawBorder(rect, Soft, 1f); // 얇은 게이지 외곽선
        }

        public static void DrawSolid(Rect rect, Color color) // 단색 사각형 출력
        {
            Color previous = GUI.color; // 기존 GUI 색상 보존
            GUI.color = color; // 요청 색상 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 흰 텍스처 단색 출력
            GUI.color = previous; // 기존 색상 복원
        }

        public static void DrawBorder(Rect rect, Color color, float thickness) // 사각형 외곽선 출력
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color); // 상단 선
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // 하단 선
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color); // 좌측 선
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // 우측 선
        }
    }
}
