using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 전체 지도 상태 참조
using ProjectK.Day30; // 공통 HUD 숨김 상태 참조
using UnityEngine; // 최소 단말기 HUD 렌더링

namespace ProjectK.Day29 // 29일차 플레이어 단말기 이름 공간
{
    [DisallowMultipleComponent] // 단말기 HUD 중복 방지
    public sealed class Map29TerminalHUD : MonoBehaviour // 좌측 HUD 스택 하단의 최소 임무 단말기
    {
        private static readonly Color panelColor = new Color(0.014f, 0.048f, 0.068f, 0.92f); // 단말기 배경
        private static readonly Color innerColor = new Color(0.020f, 0.085f, 0.110f, 0.44f); // 내부 배경
        private static readonly Color cyan = new Color(0.08f, 0.88f, 0.98f, 1f); // 주 청록색
        private static readonly Color cyanSoft = new Color(0.10f, 0.58f, 0.70f, 0.72f); // 보조 외곽선
        private static readonly Color textMain = new Color(0.86f, 0.97f, 1f, 1f); // 주요 텍스트
        private static readonly Color textMuted = new Color(0.45f, 0.74f, 0.80f, 1f); // 보조 텍스트
        private static readonly Color green = new Color(0.28f, 1f, 0.67f, 1f); // 완료 상태
        private static readonly Color amber = new Color(1f, 0.68f, 0.18f, 1f); // 대기 상태
        private static readonly Color red = new Color(1f, 0.30f, 0.34f, 1f); // 실패 상태

        private const float PanelWidth = 232f; // 최소 패널 너비
        private const float PanelHeight = 92f; // 최소 패널 높이
        private const float LeftColumnX = 14f; // HP·QA와 동일한 좌측 열
        private const float QaBottom = 242f; // 현재 HP·QA 고정 배치의 마지막 Y
        private const float ColumnGap = 8f; // 좌측 HUD 사이 여백

        private MapWorldRoot world; // 현재 본편 월드
        private MapNavigationUI navigationUI; // 전체 지도 UI
        private PlayerHealth playerHealth; // 플레이어 생존 상태
        private GUIStyle headerStyle; // 상단 시스템 글자
        private GUIStyle missionStyle; // 임무 제목 글자
        private GUIStyle objectiveStyle; // 현재 목표 글자
        private GUIStyle distanceStyle; // 거리 글자
        private float nextResolveTime; // 다음 참조 복구 시각

        private void Start() // 첫 씬 참조 연결
        {
            ResolveReferences(); // 월드·지도·플레이어 연결
        }

        private void Update() // 씬 전환 뒤 참조 복구
        {
            if (world != null && world.Player != null) // 정상 참조 확인
            {
                return; // 재검색 생략
            }

            if (Time.unscaledTime < nextResolveTime) // 재검색 간격 확인
            {
                return; // 대기
            }

            nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 예약
            ResolveReferences(); // 참조 복구
        }

        private void ResolveReferences() // Provider 우선으로 현재 월드 연결
        {
            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 목표 Provider 조회
            world = provider != null ? provider.World : null; // Provider 월드 우선 사용

            if (world == null) // Provider 월드 미준비 확인
            {
                MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 본편 월드 대체 검색
                world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
            }

            navigationUI = world != null ? world.GetComponent<MapNavigationUI>() : null; // 지도 UI 연결
            playerHealth = world != null && world.Player != null ? world.Player.GetComponent<PlayerHealth>() : null; // 플레이어 체력 연결
        }

        private void OnGUI() // 좌측 스택형 최소 임무 HUD 출력
        {
            if (Map30UITheme.HideGameplayHUD) // Tab·결과 화면 상태 확인
            {
                return; // 전체 메뉴 위 단말기 숨김
            }

            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 목표 Provider 조회
            if (provider == null) // Provider 준비 확인
            {
                return; // 초기 프레임 표시 생략
            }

            if (navigationUI != null && navigationUI.FullMapOpen) // 전체 지도 화면 확인
            {
                return; // 지도 위 단말기 숨김
            }

            if (playerHealth != null && playerHealth.IsDead) // 사망 상태 확인
            {
                return; // 사망 중 단말기 숨김
            }

            Map29TerminalObjectiveSnapshot snapshot = provider.Snapshot(); // 현재 목표 데이터 조회
            MapWorldRoot currentWorld = provider.World != null ? provider.World : world; // 최신 월드 선택
            Transform player = currentWorld != null && currentWorld.Player != null ? currentWorld.Player.transform : null; // 플레이어 Transform 조회
            Rect panel = ResolvePanelRect(); // 현재 해상도에서 겹치지 않는 패널 위치 계산

            EnsureStyles(); // 텍스트 스타일 준비
            DrawFrame(panel, snapshot); // 최소 프레임 출력
            DrawContent(panel, snapshot, player); // 최소 정보 출력
        }

        private static Rect ResolvePanelRect() // HP·QA와 겹치지 않는 반응형 좌측 배치
        {
            float x = LeftColumnX; // 기본 좌측 열 X
            float y = QaBottom + ColumnGap; // QA 바로 아래 배치

            if (Screen.height < QaBottom + ColumnGap + PanelHeight + 12f) // 세로 공간이 부족한 극저해상도 확인
            {
                x = Mathf.Clamp(204f, LeftColumnX, Mathf.Max(LeftColumnX, Screen.width - PanelWidth - 14f)); // HP·QA 오른쪽으로 이동
                y = 14f; // 상단 배치
            }

            y = Mathf.Clamp(y, 14f, Mathf.Max(14f, Screen.height - PanelHeight - 12f)); // 화면 아래 넘침 방지
            return new Rect(x, y, PanelWidth, PanelHeight); // 최종 패널 반환
        }

        private void DrawFrame(Rect panel, Map29TerminalObjectiveSnapshot snapshot) // 단순 프레임 출력
        {
            DrawSolid(panel, panelColor); // 기본 배경
            DrawSolid(new Rect(panel.x + 5f, panel.y + 23f, panel.width - 10f, panel.height - 28f), innerColor); // 내부 배경
            DrawBorder(panel, cyan, 2f); // 청록 외곽선
            DrawBorder(new Rect(panel.x + 4f, panel.y + 4f, panel.width - 8f, panel.height - 8f), cyanSoft, 1f); // 내부 외곽선
            DrawSolid(new Rect(panel.x + 8f, panel.y + 21f, panel.width - 16f, 1f), cyanSoft); // 헤더 구분선

            Color status = StatusColor(snapshot.Status, snapshot.IsDemo); // 상태 색상 계산
            float pulse = 0.45f + Mathf.PingPong(Time.unscaledTime * 1.1f, 0.55f); // 상태 LED 점멸 계산
            DrawSolid(new Rect(panel.x + panel.width - 14f, panel.y + 8f, 5f, 5f), new Color(status.r, status.g, status.b, pulse)); // 상태 LED
        }

        private void DrawContent(Rect panel, Map29TerminalObjectiveSnapshot snapshot, Transform player) // 임무명·목표·거리만 표시
        {
            GUI.Label(new Rect(panel.x + 9f, panel.y + 4f, 150f, 16f), "MISSION // LINK", headerStyle); // 최소 헤더

            string mission = string.IsNullOrWhiteSpace(snapshot.MissionTitle) ? "임무 대기" : snapshot.MissionTitle; // 임무 제목 보정
            string objective = string.IsNullOrWhiteSpace(snapshot.ObjectiveText) ? "새 목표 대기" : snapshot.ObjectiveText; // 목표 문구 보정
            GUI.Label(new Rect(panel.x + 9f, panel.y + 29f, panel.width - 18f, 18f), CompactText(mission, 21), missionStyle); // 임무 제목
            GUI.Label(new Rect(panel.x + 9f, panel.y + 48f, panel.width - 18f, 17f), CompactText(objective, 27), objectiveStyle); // 현재 목표

            string distance = "---"; // 기본 거리
            if (snapshot.HasTarget && player != null) // 거리 계산 가능 여부 확인
            {
                distance = FormatDistance(Vector3.Distance(player.position, snapshot.TargetPosition)); // 실제 거리 계산
            }

            GUI.Label(new Rect(panel.x + 9f, panel.y + 69f, panel.width - 18f, 18f), distance, distanceStyle); // 남은 거리만 크게 표시
        }

        private static string CompactText(string value, int maxLength) // 긴 HUD 문구 축약
        {
            if (string.IsNullOrWhiteSpace(value)) // 빈 문자열 확인
            {
                return "---"; // 기본값 반환
            }

            string line = value.Replace("\r", " ").Replace("\n", " ").Trim(); // 한 줄 변환
            return line.Length <= maxLength ? line : line.Substring(0, Mathf.Max(1, maxLength - 1)) + "…"; // 길이 제한
        }

        private static string FormatDistance(float distance) // 거리 표시 형식 변환
        {
            return distance >= 1000f ? (distance / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(distance) + " m"; // km·m 자동 선택
        }

        private static Color StatusColor(Map29TerminalObjectiveStatus status, bool demo) // 목표 상태별 LED 색상
        {
            if (demo && status == Map29TerminalObjectiveStatus.Tracking) // 시험 추적 확인
            {
                return amber; // 시험 상태 주황색
            }

            switch (status) // 상태별 색상 분기
            {
                case Map29TerminalObjectiveStatus.Completed: return green; // 완료 녹색
                case Map29TerminalObjectiveStatus.Failed: return red; // 실패 적색
                case Map29TerminalObjectiveStatus.Paused: return amber; // 일시 정지 주황색
                default: return cyan; // 추적·대기 청록색
            }
        }

        private void EnsureStyles() // 고정 픽셀 기준 최소 HUD 스타일 준비
        {
            if (headerStyle != null) // 기존 스타일 확인
            {
                return; // 재생성 생략
            }

            headerStyle = CreateStyle(9, FontStyle.Bold, textMuted, TextAnchor.MiddleLeft); // 헤더 스타일
            missionStyle = CreateStyle(13, FontStyle.Bold, textMain, TextAnchor.MiddleLeft); // 임무 제목 스타일
            objectiveStyle = CreateStyle(10, FontStyle.Normal, textMain, TextAnchor.MiddleLeft); // 목표 문구 스타일
            distanceStyle = CreateStyle(13, FontStyle.Bold, cyan, TextAnchor.MiddleLeft); // 거리 스타일
        }

        private static GUIStyle CreateStyle(int size, FontStyle fontStyle, Color color, TextAnchor alignment) // 공통 GUI 스타일 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 복사
            style.fontSize = size; // 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 글자 색상 적용
            style.alignment = alignment; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 기본 여백 제거
            return style; // 완성 스타일 반환
        }

        private static void DrawSolid(Rect rect, Color color) // 단색 사각형 출력
        {
            Color previous = GUI.color; // 기존 GUI 색상 보존
            GUI.color = color; // 요청 색상 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 단색 출력
            GUI.color = previous; // 기존 색상 복원
        }

        private static void DrawBorder(Rect rect, Color color, float thickness) // 사각 외곽선 출력
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color); // 상단
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // 하단
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color); // 좌측
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // 우측
        }
    }
}
