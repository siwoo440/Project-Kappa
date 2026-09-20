using ProjectK.Day30; // 기존 청록·파랑 UI 테마
using UnityEngine; // IMGUI 설정 페이지

namespace ProjectK.Day36 // 36일차 설정 시스템 이름 공간
{
    public static class Map36SettingsPage // Day35 ESC 메뉴 오른쪽 영역에서 사용하는 상단 탭형 설정 페이지
    {
        private enum SettingsTab // 설정 상단 소형 탭
        {
            Graphics, // 그래픽
            Sound, // 사운드
            Controls, // 조작
            Gameplay // 게임플레이
        }

        private static SettingsTab selectedTab = SettingsTab.Graphics; // 처음 열 때 그래픽 탭 선택
        private static int lastDrawFrame = -100; // 설정 페이지 재진입 감지용 마지막 그리기 프레임
        private static string statusMessage = string.Empty; // 적용·취소 결과 문구
        private static float statusUntil; // 결과 문구 표시 종료 시각
        private static GUIStyle pageTitleStyle; // SETTINGS 제목 스타일
        private static GUIStyle systemStyle; // 작은 시스템 문구 스타일
        private static GUIStyle tabStyle; // 상단 탭 기본 스타일
        private static GUIStyle tabSelectedStyle; // 상단 탭 선택 스타일
        private static GUIStyle sectionStyle; // 섹션 제목 스타일
        private static GUIStyle labelStyle; // 옵션 이름 스타일
        private static GUIStyle valueStyle; // 옵션 값 스타일
        private static GUIStyle smallStyle; // 보조 설명 스타일
        private static GUIStyle buttonStyle; // 하단 버튼 스타일

        private static readonly int[] FpsOptions = { -1, 30, 60, 120, 144 }; // FPS 제한 선택값

        private const float UiScale = 2f; // 요청한 설정 UI 전체 2배 확대 배율
        private static Vector2 scrollPosition; // 확대된 설정 내용이 작은 화면에서 잘리지 않도록 스크롤 위치


        public static void Draw(Rect right) // Day35 오른쪽 콘텐츠 영역에 2배 크기 설정 페이지 출력
        {
            Matrix4x4 previousMatrix = GUI.matrix; // Day35 메뉴의 기존 GUI 변환 보존
            Color previousColor = GUI.color; // 기존 GUI 색상 보존
            Rect virtualRight = new Rect(right.x / UiScale, right.y / UiScale, right.width / UiScale, right.height / UiScale); // 실제 영역을 절반 좌표계로 변환
            GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f)); // 설정 페이지 전체를 실제 화면에서 2배로 확대

            try // 다른 ESC 메뉴 UI에 스케일이 전파되지 않도록 보호
            {
                DrawScaledCore(virtualRight); // 2배 확대용 가상 영역에 설정 UI 출력
            }
            finally // Day35 왼쪽 메뉴바와 저장 UI 상태 복원
            {
                GUI.matrix = previousMatrix; // 기존 GUI 변환 복원
                GUI.color = previousColor; // 기존 색상 복원
            }
        }

        private static void DrawScaledCore(Rect right) // 2배 스케일용 가상 오른쪽 영역에 실제 설정 페이지 출력
        {
            Map36SettingsManager manager = Map36SettingsManager.Instance; // 현재 설정 관리자 조회
            if (manager == null) // 관리자 준비 확인
            {
                GUI.Label(new Rect(right.x + 38f, right.y + 28f, right.width - 76f, 48f), "SETTINGS NOT READY", GUI.skin.label); // 준비 실패 표시
                return; // 설정 출력 중단
            }

            EnsureStyles(); // 공통 스타일 준비

            if (lastDrawFrame < Time.frameCount - 1) // 다른 페이지에서 설정으로 새로 진입했는지 확인
            {
                manager.BeginEdit(); // 실제 적용값 기준 새 편집 세션 시작
                statusMessage = string.Empty; // 이전 상태 문구 초기화
            }

            lastDrawFrame = Time.frameCount; // 현재 설정 페이지 그리기 프레임 기록
            Map36SettingsData draft = manager.Draft; // 현재 편집 설정 조회
            if (draft == null) // 편집 데이터 확인
            {
                return; // 출력 생략
            }

            GUI.Label(new Rect(right.x + 38f, right.y + 25f, 240f, 46f), "SETTINGS", pageTitleStyle); // 페이지 제목
            GUI.Label(new Rect(right.x + 215f, right.y + 39f, right.width - 255f, 20f), "설정 영역을 상단 탭으로 나누어 관리합니다.", systemStyle); // 페이지 설명
            Map30UITheme.DrawDivider(new Rect(right.x + 38f, right.y + 76f, right.width - 76f, 1f)); // 제목 하단 구분선

            Rect tabArea = new Rect(right.x + 38f, right.y + 90f, right.width - 76f, 36f); // 상단 소형 탭 영역
            DrawTabs(tabArea); // 그래픽·사운드·조작·게임플레이 버튼 출력

            Rect content = new Rect(right.x + 38f, right.y + 142f, right.width - 76f, Mathf.Max(86f, right.height - 238f)); // 실제 설정 내용 영역
            Map30UITheme.DrawPanel(content); // 기존 UI와 동일한 설정 패널 출력

            Rect viewport = new Rect(content.x + 6f, content.y + 6f, Mathf.Max(80f, content.width - 12f), Mathf.Max(72f, content.height - 12f)); // 확대 UI 스크롤 표시 영역
            float canvasHeight = selectedTab == SettingsTab.Graphics ? 390f : selectedTab == SettingsTab.Sound ? 250f : selectedTab == SettingsTab.Controls ? 330f : 300f; // 탭별 필요한 가상 높이
            Rect canvas = new Rect(0f, 0f, Mathf.Max(80f, viewport.width - 22f), canvasHeight); // 스크롤 내부 가상 설정 영역
            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, canvas); // 작은 해상도에서도 2배 UI 조작 가능

            if (selectedTab == SettingsTab.Graphics) DrawGraphics(canvas, manager, draft); // 그래픽 설정 출력
            else if (selectedTab == SettingsTab.Sound) DrawSound(canvas, draft); // 사운드 설정 출력
            else if (selectedTab == SettingsTab.Controls) DrawControls(canvas, draft); // 조작 설정 출력
            else DrawGameplay(canvas, draft); // 게임플레이 설정 출력

            GUI.EndScrollView(); // 설정 내용 스크롤 종료

            Rect footer = new Rect(right.x + 38f, right.yMax - 78f, right.width - 76f, 58f); // 공통 하단 적용 영역
            DrawFooter(footer, manager); // 기본값·취소·적용 버튼 출력
        }

        private static void DrawTabs(Rect area) // 설정 창 위쪽에 작은 탭 버튼 나열
        {
            string[] labels = { "그래픽", "사운드", "조작", "게임플레이" }; // 탭 표시 이름
            float gap = 8f; // 탭 사이 간격
            float buttonWidth = Mathf.Clamp((area.width - gap * 3f) / 4f, 92f, 158f); // 화면 크기 기반 탭 너비

            for (int i = 0; i < labels.Length; i++) // 네 개 탭 순회
            {
                Rect button = new Rect(area.x + i * (buttonWidth + gap), area.y, buttonWidth, area.height); // 현재 탭 위치
                bool selected = (int)selectedTab == i; // 현재 선택 여부
                Color background = selected ? new Color(0.025f, 0.125f, 0.175f, 0.98f) : new Color(0.010f, 0.042f, 0.066f, 0.94f); // 상태별 배경색
                Map30UITheme.DrawSolid(button, background); // 탭 배경
                Map30UITheme.DrawBorder(button, selected ? Map30UITheme.Cyan : Map30UITheme.Soft, selected ? 2f : 1f); // 선택 탭 청록 강조

                if (selected) // 현재 탭 강조선 확인
                {
                    Map30UITheme.DrawSolid(new Rect(button.x + 1f, button.yMax - 3f, button.width - 2f, 3f), Map30UITheme.Cyan); // 탭 하단 강조선
                }

                if (GUI.Button(button, labels[i], selected ? tabSelectedStyle : tabStyle)) // 탭 클릭 확인
                {
                    selectedTab = (SettingsTab)i; // 선택 탭 변경
                    scrollPosition = Vector2.zero; // 새 탭은 항상 위쪽부터 표시
                    statusMessage = string.Empty; // 이전 상태 문구 정리
                }
            }
        }

        private static void DrawGraphics(Rect panel, Map36SettingsManager manager, Map36SettingsData draft) // 그래픽 설정 탭
        {
            float x = panel.x + 24f; // 옵션 왼쪽 여백
            float width = panel.width - 48f; // 옵션 사용 가능 너비
            float y = panel.y + 18f; // 첫 옵션 위치
            GUI.Label(new Rect(x, y, width, 28f), "그래픽", sectionStyle); // 섹션 제목
            y += 42f; // 첫 옵션으로 이동

            int resolutionIndex = manager.ResolutionIndex(); // 현재 해상도 목록 위치
            Vector2Int resolution = manager.Resolutions.Count > 0 ? manager.Resolutions[resolutionIndex] : new Vector2Int(draft.ResolutionWidth, draft.ResolutionHeight); // 현재 해상도 표시값
            DrawCycleRow(new Rect(x, y, width, 44f), "해상도", resolution.x + " × " + resolution.y,
                () => manager.SetResolutionIndex(resolutionIndex - 1),
                () => manager.SetResolutionIndex(resolutionIndex + 1)); // 해상도 좌우 선택
            y += 54f; // 다음 옵션

            DrawCycleRow(new Rect(x, y, width, 44f), "화면 모드", WindowModeName(draft.WindowMode),
                () => draft.WindowMode = Wrap(draft.WindowMode - 1, 3),
                () => draft.WindowMode = Wrap(draft.WindowMode + 1, 3)); // 전체 창모드·독점·창모드 선택
            y += 54f; // 다음 옵션

            string[] qualities = QualitySettings.names; // 프로젝트 품질 이름 조회
            string qualityName = qualities.Length > 0 ? qualities[Mathf.Clamp(draft.QualityLevel, 0, qualities.Length - 1)] : "기본"; // 현재 품질 표시
            DrawCycleRow(new Rect(x, y, width, 44f), "그래픽 품질", qualityName,
                () => draft.QualityLevel = Mathf.Max(0, draft.QualityLevel - 1),
                () => draft.QualityLevel = qualities.Length > 0 ? Mathf.Min(qualities.Length - 1, draft.QualityLevel + 1) : 0); // 품질 단계 선택
            y += 54f; // 다음 옵션

            DrawToggleRow(new Rect(x, y, width, 44f), "수직 동기화", draft.VSync, value => draft.VSync = value); // VSync 켜기·끄기
            y += 54f; // 다음 옵션

            int fpsIndex = FpsIndex(draft.FrameRateLimit); // 현재 FPS 제한 인덱스
            DrawCycleRow(new Rect(x, y, width, 44f), "FPS 제한", FpsText(FpsOptions[fpsIndex]),
                () => draft.FrameRateLimit = FpsOptions[Wrap(fpsIndex - 1, FpsOptions.Length)],
                () => draft.FrameRateLimit = FpsOptions[Wrap(fpsIndex + 1, FpsOptions.Length)]); // FPS 제한 선택
            y += 62f; // 설명 영역 이동

            GUI.Label(new Rect(x, y, width, 48f), "해상도와 화면 모드는 [적용 및 저장]을 누를 때 실제 화면에 반영됩니다.", smallStyle); // 적용 방식 안내
        }

        private static void DrawSound(Rect panel, Map36SettingsData draft) // 사운드 설정 탭
        {
            float x = panel.x + 24f; // 옵션 왼쪽 여백
            float width = panel.width - 48f; // 옵션 사용 너비
            float y = panel.y + 18f; // 첫 옵션 위치
            GUI.Label(new Rect(x, y, width, 28f), "사운드", sectionStyle); // 섹션 제목
            y += 48f; // 첫 옵션 이동

            DrawSliderRow(new Rect(x, y, width, 60f), "전체 음량", draft.MasterVolume, 0f, 1f, value => draft.MasterVolume = value, Mathf.RoundToInt(draft.MasterVolume * 100f) + "%"); // Master 볼륨
            y += 72f; // 다음 옵션

            DrawToggleRow(new Rect(x, y, width, 44f), "전체 음소거", draft.MasterMute, value => draft.MasterMute = value); // 전체 음소거
            y += 64f; // 설명 영역 이동

            GUI.Label(new Rect(x, y, width, 64f), "현재는 프로젝트 전체 AudioListener 음량을 제어합니다.\n이후 BGM·효과음·음성 AudioMixer가 분리되면 같은 탭에 항목을 추가할 수 있습니다.", smallStyle); // 현재 오디오 범위 설명
        }

        private static void DrawControls(Rect panel, Map36SettingsData draft) // 조작 설정 탭
        {
            float x = panel.x + 24f; // 옵션 왼쪽 여백
            float width = panel.width - 48f; // 옵션 너비
            float y = panel.y + 18f; // 첫 옵션 위치
            GUI.Label(new Rect(x, y, width, 28f), "조작", sectionStyle); // 섹션 제목
            y += 48f; // 첫 옵션 이동

            DrawSliderRow(new Rect(x, y, width, 60f), "마우스 감도", draft.MouseSensitivity, 0.03f, 0.40f, value => draft.MouseSensitivity = value, draft.MouseSensitivity.ToString("0.00")); // 마우스 감도
            y += 72f; // 다음 옵션

            DrawSliderRow(new Rect(x, y, width, 60f), "게임패드 감도", draft.GamepadLookSpeed, 60f, 300f, value => draft.GamepadLookSpeed = value, Mathf.RoundToInt(draft.GamepadLookSpeed).ToString()); // 게임패드 감도
            y += 72f; // 다음 옵션

            DrawToggleRow(new Rect(x, y, width, 44f), "Y축 반전", draft.InvertY, value => draft.InvertY = value); // Y축 반전
            y += 64f; // 설명 영역 이동

            GUI.Label(new Rect(x, y, width, 52f), "카메라 감도는 조준 배율과 별도로 적용되며 기존 총기 조준 감도 보정은 유지됩니다.", smallStyle); // 카메라 설정 설명
        }

        private static void DrawGameplay(Rect panel, Map36SettingsData draft) // 게임플레이 설정 탭
        {
            float x = panel.x + 24f; // 옵션 왼쪽 여백
            float width = panel.width - 48f; // 옵션 너비
            float y = panel.y + 18f; // 첫 옵션 위치
            GUI.Label(new Rect(x, y, width, 28f), "게임플레이", sectionStyle); // 섹션 제목
            y += 48f; // 첫 옵션 이동

            DrawToggleRow(new Rect(x, y, width, 44f), "미니맵 표시", draft.MinimapVisible, value => draft.MinimapVisible = value); // 미니맵 표시 설정
            y += 54f; // 다음 옵션

            string[] sizes = { "소형", "중형", "대형" }; // 미니맵 크기 표시 이름
            DrawCycleRow(new Rect(x, y, width, 44f), "미니맵 크기", sizes[Mathf.Clamp(draft.MinimapSizeLevel, 0, 2)],
                () => draft.MinimapSizeLevel = Wrap(draft.MinimapSizeLevel - 1, 3),
                () => draft.MinimapSizeLevel = Wrap(draft.MinimapSizeLevel + 1, 3)); // 미니맵 크기 선택
            y += 62f; // 다음 옵션

            DrawSliderRow(new Rect(x, y, width, 60f), "G 목표 안내 시간", draft.MissionGuideDuration, 2f, 8f, value => draft.MissionGuideDuration = value, draft.MissionGuideDuration.ToString("0.0") + "초"); // 목표 안내 표시 시간
            y += 78f; // 설명 영역 이동

            GUI.Label(new Rect(x, y, width, 64f), "게임플레이 설정은 진행 세이브 슬롯과 분리되어 모든 저장 슬롯에 공통으로 적용됩니다.", smallStyle); // 설정 저장 범위 안내
        }

        private static void DrawFooter(Rect rect, Map36SettingsManager manager) // 모든 탭이 공유하는 기본값·취소·적용 영역
        {
            Map30UITheme.DrawSolid(rect, new Color(0.008f, 0.030f, 0.050f, 0.95f)); // 하단 액션 배경
            Map30UITheme.DrawBorder(rect, manager.HasUnsavedChanges ? Map30UITheme.Cyan : Map30UITheme.Soft, 1f); // 변경 존재 시 청록 강조

            string message = Time.unscaledTime < statusUntil ? statusMessage :
                             manager.HasUnsavedChanges ? "변경 사항이 있습니다. 적용 및 저장을 눌러 반영하십시오." :
                             "현재 설정이 적용되어 있습니다."; // 하단 상태 문구
            GUI.Label(new Rect(rect.x + 14f, rect.y + 17f, Mathf.Max(180f, rect.width * 0.42f), 24f), message, smallStyle); // 상태 문구 출력

            float gap = 10f; // 버튼 간격
            float buttonWidth = Mathf.Clamp(rect.width * 0.15f, 112f, 162f); // 버튼 너비
            float right = rect.xMax - 12f; // 오른쪽 끝 기준
            Rect apply = new Rect(right - buttonWidth, rect.y + 8f, buttonWidth, 42f); // 적용 버튼
            Rect cancel = new Rect(apply.x - gap - buttonWidth, apply.y, buttonWidth, 42f); // 변경 취소 버튼
            Rect defaults = new Rect(cancel.x - gap - buttonWidth, apply.y, buttonWidth, 42f); // 기본값 복원 버튼

            DrawFooterButton(defaults, "기본값 복원", Map30UITheme.Amber); // 기본값 버튼
            if (GUI.Button(defaults, "기본값 복원", buttonStyle)) // 기본값 클릭 확인
            {
                manager.ResetDraftToDefaults(); // 편집값만 기본값으로 변경
                ShowStatus("기본값을 편집 상태에 불러왔습니다.", 2.5f); // 아직 미적용 안내
            }

            DrawFooterButton(cancel, "변경 취소", Map30UITheme.Soft); // 취소 버튼
            if (GUI.Button(cancel, "변경 취소", buttonStyle)) // 취소 클릭 확인
            {
                manager.CancelEdit(); // 적용값 기준으로 편집값 복원
                ShowStatus("적용하지 않은 변경을 취소했습니다.", 2.2f); // 취소 결과 표시
            }

            DrawFooterButton(apply, "적용 및 저장", manager.HasUnsavedChanges ? Map30UITheme.Cyan : Map30UITheme.Green); // 적용 버튼
            if (GUI.Button(apply, "적용 및 저장", buttonStyle)) // 적용 클릭 확인
            {
                bool success = manager.ApplyAndSave(); // 실제 설정 적용과 settings.json 저장
                ShowStatus(success ? "설정이 적용·저장되었습니다." : "설정 저장에 실패했습니다.", 2.8f); // 결과 표시
            }
        }

        private static void DrawCycleRow(Rect rect, string label, string value, System.Action previous, System.Action next) // 좌우 화살표 선택형 옵션 행
        {
            DrawRowBackground(rect); // 옵션 배경
            GUI.Label(new Rect(rect.x + 14f, rect.y + 11f, rect.width * 0.36f, 22f), label, labelStyle); // 옵션 이름
            float controlsX = rect.x + rect.width * 0.48f; // 값 영역 시작
            float controlsWidth = rect.width * 0.48f - 14f; // 값 영역 너비
            Rect left = new Rect(controlsX, rect.y + 7f, 36f, 30f); // 왼쪽 버튼
            Rect right = new Rect(controlsX + controlsWidth - 36f, rect.y + 7f, 36f, 30f); // 오른쪽 버튼
            Rect valueRect = new Rect(left.xMax + 6f, rect.y + 8f, Mathf.Max(60f, right.x - left.xMax - 12f), 28f); // 중앙 값
            Map30UITheme.DrawBorder(left, Map30UITheme.Soft, 1f); // 왼쪽 버튼 외곽선
            Map30UITheme.DrawBorder(right, Map30UITheme.Soft, 1f); // 오른쪽 버튼 외곽선
            if (GUI.Button(left, "‹", valueStyle)) previous?.Invoke(); // 이전 값 선택
            if (GUI.Button(right, "›", valueStyle)) next?.Invoke(); // 다음 값 선택
            GUI.Label(valueRect, value, valueStyle); // 현재 값 출력
        }

        private static void DrawToggleRow(Rect rect, string label, bool value, System.Action<bool> setter) // 켜짐·꺼짐 토글 옵션 행
        {
            DrawRowBackground(rect); // 옵션 배경
            GUI.Label(new Rect(rect.x + 14f, rect.y + 11f, rect.width * 0.48f, 22f), label, labelStyle); // 옵션 이름
            Rect button = new Rect(rect.xMax - 142f, rect.y + 7f, 128f, 30f); // 토글 버튼
            Color accent = value ? Map30UITheme.Cyan : Map30UITheme.Soft; // 상태 색상
            Map30UITheme.DrawBorder(button, accent, 1f); // 토글 외곽선

            if (GUI.Button(button, value ? "켜짐" : "꺼짐", valueStyle)) // 토글 클릭 확인
            {
                setter?.Invoke(!value); // 상태 반전
            }
        }

        private static void DrawSliderRow(Rect rect, string label, float value, float min, float max, System.Action<float> setter, string display) // 슬라이더 기반 옵션 행
        {
            DrawRowBackground(rect); // 옵션 배경
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width * 0.34f, 22f), label, labelStyle); // 옵션 이름
            GUI.Label(new Rect(rect.xMax - 86f, rect.y + 8f, 72f, 22f), display, valueStyle); // 현재 숫자 값
            Rect slider = new Rect(rect.x + rect.width * 0.38f, rect.y + 36f, rect.width * 0.52f, 18f); // 슬라이더 영역
            float changed = GUI.HorizontalSlider(slider, value, min, max); // IMGUI 슬라이더 입력
            if (!Mathf.Approximately(changed, value)) setter?.Invoke(changed); // 실제 변경 시 편집값 갱신
            Map30UITheme.DrawSolid(new Rect(slider.x, slider.y + 8f, slider.width, 1f), Map30UITheme.Soft); // 청록 기준선 보조 표시
        }

        private static void DrawRowBackground(Rect rect) // 옵션 한 행 공통 배경
        {
            Map30UITheme.DrawSolid(rect, new Color(0.010f, 0.045f, 0.068f, 0.78f)); // 남청색 행 배경
            Map30UITheme.DrawBorder(rect, new Color(Map30UITheme.Soft.r, Map30UITheme.Soft.g, Map30UITheme.Soft.b, 0.58f), 1f); // 얇은 보조 외곽선
        }

        private static void DrawFooterButton(Rect rect, string label, Color accent) // 하단 버튼 공통 프레임
        {
            Map30UITheme.DrawSolid(rect, new Color(0.012f, 0.050f, 0.075f, 0.98f)); // 버튼 배경
            Map30UITheme.DrawBorder(rect, accent, 1f); // 상태 외곽선
        }

        private static string WindowModeName(int mode) // 화면 모드 표시 이름
        {
            if (mode == 1) return "독점 전체 화면"; // Exclusive Fullscreen
            if (mode == 2) return "창 모드"; // Windowed
            return "전체 창 모드"; // FullScreenWindow
        }

        private static int FpsIndex(int fps) // 현재 FPS 제한의 선택 목록 인덱스
        {
            for (int i = 0; i < FpsOptions.Length; i++) // 전체 FPS 옵션 순회
            {
                if (FpsOptions[i] == fps) return i; // 정확한 값 발견
            }

            return 2; // 기본 60 FPS 인덱스
        }

        private static string FpsText(int fps) // FPS 제한 표시 문구
        {
            return fps <= 0 ? "무제한" : fps + " FPS"; // 무제한 또는 숫자 표시
        }

        private static int Wrap(int value, int count) // 선택 인덱스 순환
        {
            if (count <= 0) return 0; // 빈 목록 보호
            int wrapped = value % count; // 나머지 계산
            return wrapped < 0 ? wrapped + count : wrapped; // 음수 보정
        }

        private static void ShowStatus(string message, float duration) // 설정 하단 결과 문구 표시
        {
            statusMessage = message ?? string.Empty; // 상태 문구 저장
            statusUntil = Time.unscaledTime + Mathf.Max(1f, duration); // 표시 종료 시각 저장
        }

        private static void EnsureStyles() // 설정 페이지 전용 텍스트 스타일 준비
        {
            if (pageTitleStyle != null) // 기존 스타일 확인
            {
                return; // 재생성 생략
            }

            pageTitleStyle = Style(34, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // SETTINGS 제목
            systemStyle = Style(10, FontStyle.Bold, Map30UITheme.Muted, TextAnchor.MiddleLeft); // 시스템 설명
            tabStyle = Style(11, FontStyle.Bold, Map30UITheme.Muted, TextAnchor.MiddleCenter); // 비선택 탭
            tabSelectedStyle = Style(11, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleCenter); // 선택 탭
            sectionStyle = Style(19, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 섹션 제목
            labelStyle = Style(12, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 옵션 이름
            valueStyle = Style(11, FontStyle.Bold, Map30UITheme.Cyan, TextAnchor.MiddleCenter); // 옵션 값
            smallStyle = Style(10, FontStyle.Normal, Map30UITheme.Muted, TextAnchor.UpperLeft); // 설명 문구
            smallStyle.wordWrap = true; // 설명 줄바꿈
            buttonStyle = Style(11, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleCenter); // 하단 버튼
        }

        private static GUIStyle Style(int size, FontStyle fontStyle, Color color, TextAnchor alignment) // 공통 GUIStyle 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 복사
            style.fontSize = size; // 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 글자 색상 적용
            style.alignment = alignment; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 기본 여백 제거
            return style; // 완성 스타일 반환
        }
    }
}
