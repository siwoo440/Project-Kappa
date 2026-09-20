using System.Collections; // ESC 메뉴 진입 전 화면 캡처 코루틴
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 지도 UI 입력 잠금
using ProjectK.Day30; // 기존 청록·파랑 UI 테마와 임무 창 상태
using ProjectK.Day32; // 결과 화면 충돌 방지
using UnityEngine; // IMGUI·화면 캡처·블러 처리
using UnityEngine.InputSystem; // ESC 키 입력

namespace ProjectK.Day35 // 35일차 저장·불러오기 이름 공간
{
    [DisallowMultipleComponent] // ESC 메뉴 중복 방지
    public sealed class Map35PauseMenuHUD : MonoBehaviour // 왼쪽 메뉴바와 오른쪽 블러 기반 페이지 전환 UI
    {
        private enum MenuPage // 오른쪽 콘텐츠 페이지 종류
        {
            None, // 메뉴 기본 화면
            Save, // 저장 슬롯
            Load, // 불러오기 슬롯
            Settings // 추후 설정 화면 연결
        }

        private enum ConfirmationAction // 별도 경고창에서 확인할 위험 동작 종류
        {
            None, // 경고창 없음
            LoadSlot, // 저장 슬롯 불러오기 확인
            ExitGame // 게임 종료 확인
        }

        private static Map35PauseMenuHUD instance; // 현재 ESC 메뉴 인스턴스
        private bool open; // 메뉴 열린 상태
        private bool opening; // 깨끗한 플레이 화면 캡처 중 상태
        private MenuPage page; // 현재 오른쪽 페이지
        private ConfirmationAction confirmationAction; // 현재 표시 중인 별도 경고창 종류
        private int selectedSlot = 1; // 현재 선택 저장 슬롯
        private bool confirmOverwrite; // 기존 슬롯 덮어쓰기 재확인 상태
        private bool confirmDelete; // 슬롯 삭제 재확인 상태
        private string statusMessage = string.Empty; // 하단 작업 결과 문구
        private float statusUntil; // 결과 문구 종료 시각
        private Texture2D blurTexture; // 오른쪽 배경용 저해상도 블러 화면
        private Texture2D previewTexture; // 저장 슬롯 PNG용 깨끗한 플레이 화면

        private MapWorldRoot world; // 현재 본편 월드
        private PlayerMovement movement; // 플레이어 이동
        private ThirdPersonCamera cameraController; // 카메라 입력
        private MapNavigationUI navigationUI; // 지도 UI
        private Map30MissionWindow missionWindow; // Tab 임무 창 입력 차단 참조
        private float previousTimeScale = 1f; // 메뉴 열기 전 시간 배율
        private bool previousMovementEnabled = true; // 메뉴 열기 전 이동 상태
        private bool previousCameraEnabled = true; // 메뉴 열기 전 카메라 상태
        private bool previousNavigationEnabled = true; // 메뉴 열기 전 지도 상태
        private bool previousMissionWindowEnabled = true; // 메뉴 열기 전 Tab 임무 창 상태
        private CursorLockMode previousCursorLock; // 메뉴 열기 전 커서 잠금
        private bool previousCursorVisible; // 메뉴 열기 전 커서 표시

        private GUIStyle systemStyle; // 작은 시스템 라벨 스타일
        private GUIStyle menuTitleStyle; // 왼쪽 메뉴 제목 스타일
        private GUIStyle menuButtonStyle; // 왼쪽 메뉴 버튼 스타일
        private GUIStyle pageTitleStyle; // SAVE·LOAD 큰 제목 스타일
        private GUIStyle cardSlotStyle; // 슬롯 번호 스타일
        private GUIStyle cardTitleStyle; // 카드 임무 제목 스타일
        private GUIStyle cardBodyStyle; // 카드 메타 정보 스타일
        private GUIStyle noDataStyle; // 빈 슬롯 NO DATA 스타일
        private GUIStyle footerStyle; // 하단 상태 스타일
        private GUIStyle actionStyle; // 하단 액션 버튼 스타일
        private GUIStyle slotSystemStyle; // SAVE·LOAD 설명 2배 크기 스타일
        private GUIStyle confirmTitleStyle; // 경고창 제목 스타일
        private GUIStyle confirmBodyStyle; // 경고창 본문 스타일
        private GUIStyle confirmButtonStyle; // 경고창 버튼 스타일
        private GUIStyle centerStyle; // 일시 정지 기본 화면 스타일

        public static Map35PauseMenuHUD Instance => instance; // 현재 ESC 메뉴 조회
        public static bool IsOpen => instance != null && instance.open; // 다른 UI 입력 차단용 상태

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 메뉴 참조 제거
        }

        private void Awake() // 단일 ESC 메뉴 등록
        {
            if (instance != null && instance != this) // 기존 메뉴 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 메뉴 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Update() // ESC 열기·닫기와 경고창 취소 입력 처리
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 조회
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) // ESC 신규 입력 확인
            {
                return; // 입력 없음 처리
            }

            if (open && confirmationAction != ConfirmationAction.None) // 별도 경고창이 열린 상태 확인
            {
                confirmationAction = ConfirmationAction.None; // ESC로 경고창만 취소
                return; // 메뉴 자체는 유지
            }

            if (open) // 현재 ESC 메뉴 열린 상태 확인
            {
                CloseMenu(); // ESC로 메뉴 닫기
                return; // 같은 프레임 추가 처리 방지
            }

            if (opening) // 이미 화면 캡처 중인지 확인
            {
                return; // 중복 열기 차단
            }

            if (Map30MissionWindow.IsOpen) // Tab 임무 창이 ESC를 처리할 상태 확인
            {
                return; // 임무 창의 ESC 닫기 우선
            }

            ResolveReferences(); // 최신 지도·플레이어 상태 조회
            if (navigationUI != null && navigationUI.FullMapOpen) // 전체 지도 열린 상태 확인
            {
                return; // 기존 지도 ESC 닫기 우선
            }

            if (Map32MissionResultScreen.IsOpen || IsOptionalUiOpen("ProjectK.Day34.Map34RewardChoiceHUD")) // 결과·선택 보상 전체 화면 확인
            {
                return; // 해당 화면 위 ESC 메뉴 중첩 금지
            }

            StartCoroutine(OpenAfterCapture()); // 현재 플레이 화면 캡처 뒤 메뉴 열기
        }

        private IEnumerator OpenAfterCapture() // 메뉴 UI가 찍히지 않은 깨끗한 플레이 화면 캡처
        {
            opening = true; // 중복 ESC 차단
            yield return new WaitForEndOfFrame(); // 현재 플레이 프레임 렌더 완료 대기

            Texture2D source = null; // 원본 스크린샷 초기화
            try // 캡처 실패 시에도 메뉴 열기 보장
            {
                source = ScreenCapture.CaptureScreenshotAsTexture(); // 현재 화면 전체 캡처

                if (source != null) // 정상 캡처 확인
                {
                    ReplaceTexture(ref previewTexture, ResizeTexture(source, 320, 180)); // 슬롯 미리보기용 축소 원본 보관
                    Texture2D small = ResizeTexture(source, 160, 90); // 블러 연산용 저해상도 화면 생성

                    if (small != null) // 축소 화면 확인
                    {
                        ApplyBoxBlur(small, 2); // 두 번의 3x3 박스 블러 적용
                        ReplaceTexture(ref blurTexture, small); // 오른쪽 배경 블러 화면 보관
                    }
                }
            }
            finally // 원본 캡처 메모리 정리
            {
                if (source != null) Destroy(source); // 원본 풀해상도 텍스처 제거
            }

            OpenMenu(); // 캡처 이후 실제 메뉴 열기
            opening = false; // 캡처 상태 해제
        }

        private void OpenMenu() // 게임 정지와 왼쪽 메뉴바 표시
        {
            ResolveReferences(); // 최신 플레이어·카메라 참조 연결
            previousTimeScale = Time.timeScale; // 기존 시간 배율 저장
            previousCursorLock = Cursor.lockState; // 기존 커서 잠금 저장
            previousCursorVisible = Cursor.visible; // 기존 커서 표시 저장
            previousMovementEnabled = movement == null || movement.MovementEnabled; // 기존 이동 가능 여부 저장
            previousCameraEnabled = cameraController == null || cameraController.enabled; // 기존 카메라 활성 상태 저장
            previousNavigationEnabled = navigationUI == null || navigationUI.enabled; // 기존 지도 UI 상태 저장
            previousMissionWindowEnabled = missionWindow == null || missionWindow.enabled; // 기존 Tab 임무 창 활성 상태 저장

            open = true; // 메뉴 열린 상태 적용
            page = MenuPage.None; // 처음에는 오른쪽 블러만 표시
            selectedSlot = Mathf.Clamp(selectedSlot, 1, 10); // 선택 슬롯 안전 보정
            confirmOverwrite = false; // 덮어쓰기 확인 초기화
            confirmDelete = false; // 삭제 확인 초기화
            statusMessage = string.Empty; // 이전 작업 메시지 초기화
            Map35SaveManager.Instance?.RefreshSlots(); // 최신 저장 슬롯 파일 상태 읽기

            world?.Player?.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 사격·재장전 중지
            movement?.SetMovementEnabled(false); // 메뉴 중 플레이어 이동 차단
            if (cameraController != null) cameraController.enabled = false; // 마우스 시점 입력 차단
            if (navigationUI != null) navigationUI.enabled = false; // 지도 입력·미니맵 갱신 차단
            if (missionWindow != null) missionWindow.enabled = false; // ESC 메뉴 뒤에서 Tab 임무 창 입력 차단
            Time.timeScale = 0f; // ESC 메뉴 동안 월드 일시 정지
            Cursor.lockState = CursorLockMode.None; // UI 클릭을 위한 커서 해제
            Cursor.visible = true; // 커서 표시
        }

        public void CloseMenu() // ESC 메뉴 닫기와 기존 플레이 상태 복원
        {
            if (!open) // 이미 닫힌 상태 확인
            {
                return; // 처리 생략
            }

            open = false; // UI 먼저 닫기
            page = MenuPage.None; // 페이지 상태 초기화
            confirmOverwrite = false; // 덮어쓰기 확인 해제
            confirmDelete = false; // 삭제 확인 해제
            Time.timeScale = previousTimeScale; // 기존 시간 배율 복원

            if (movement != null) movement.SetMovementEnabled(previousMovementEnabled); // 이동 상태 복원
            if (cameraController != null) cameraController.enabled = previousCameraEnabled; // 카메라 상태 복원
            if (navigationUI != null) navigationUI.enabled = previousNavigationEnabled; // 지도 UI 상태 복원
            if (missionWindow != null) missionWindow.enabled = previousMissionWindowEnabled; // Tab 임무 창 활성 상태 복원
            Cursor.lockState = previousCursorLock; // 기존 커서 잠금 복원
            Cursor.visible = previousCursorVisible; // 기존 커서 표시 복원
        }

        private void OnDisable() // 컴포넌트 비활성 시 게임 상태 안전 복구
        {
            if (open) CloseMenu(); // 열린 메뉴 자동 종료
        }

        private void OnDestroy() // 메뉴 파괴 시 텍스처와 정적 참조 정리
        {
            if (open) CloseMenu(); // 플레이 상태 복구
            if (blurTexture != null) Destroy(blurTexture); // 블러 텍스처 정리
            if (previewTexture != null) Destroy(previewTexture); // 미리보기 텍스처 정리
            if (instance == this) instance = null; // 정적 참조 해제
        }

        private void ResolveReferences() // 현재 월드·플레이어·지도·카메라 연결
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 본편 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
            navigationUI = world != null ? world.GetComponent<MapNavigationUI>() : null; // 지도 UI 연결
            missionWindow = Map30MissionWindow.Instance; // 현재 Tab 임무 창 연결
            movement = world != null && world.Player != null ? world.Player.GetComponent<PlayerMovement>() : null; // 플레이어 이동 연결
            Camera camera = Camera.main; // 메인 카메라 조회
            cameraController = camera != null ? camera.GetComponent<ThirdPersonCamera>() : null; // 3인칭 시점 입력 연결
        }

        private void OnGUI() // 왼쪽 메뉴바·오른쪽 페이지·별도 확인창 렌더링
        {
            if (!open) // 닫힌 상태 확인
            {
                return; // UI 출력 생략
            }

            EnsureStyles(); // GUI 스타일 준비
            int previousDepth = GUI.depth; // 기존 UI 깊이 보존
            Color previousColor = GUI.color; // 기존 GUI 색 보존
            bool previousEnabled = GUI.enabled; // 기존 GUI 입력 활성 상태 보존
            GUI.depth = -3500; // 모든 기존 IMGUI 위에 표시

            try // GUI 상태 복구 보장
            {
                float sideWidth = Mathf.Clamp(Screen.width * 0.19f, 205f, 275f); // 왼쪽 메뉴바 너비
                Rect side = new Rect(0f, 0f, sideWidth, Screen.height); // 왼쪽 메뉴바 영역
                Rect right = new Rect(sideWidth, 0f, Screen.width - sideWidth, Screen.height); // 오른쪽 공통 콘텐츠 영역
                DrawBackground(side, right); // 왼쪽 고정 패널과 오른쪽 블러 출력

                bool confirmationOpen = confirmationAction != ConfirmationAction.None; // 별도 확인창 표시 여부 계산
                GUI.enabled = !confirmationOpen; // 경고창 표시 중 뒤쪽 모든 버튼 입력 차단
                DrawSidebar(side); // 왼쪽 메뉴 항목 출력
                DrawRightContent(right); // 현재 페이지 콘텐츠 출력
                GUI.enabled = previousEnabled; // 경고창 버튼 입력을 위해 기존 상태 복원

                if (confirmationOpen) // 별도 확인창 표시 확인
                {
                    DrawConfirmationDialog(); // 불러오기·종료 경고창 출력
                }
            }
            finally // 기존 UI 상태 복원
            {
                GUI.enabled = previousEnabled; // GUI 입력 활성 상태 복원
                GUI.depth = previousDepth; // UI 깊이 복원
                GUI.color = previousColor; // GUI 색상 복원
            }
        }

        private void DrawBackground(Rect side, Rect right) // 메뉴 배경과 오른쪽 게임 화면 블러 출력
        {
            Map30UITheme.DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.003f, 0.010f, 0.020f, 1f)); // 전체 안전 배경
            Map30UITheme.DrawSolid(side, new Color(0.008f, 0.025f, 0.045f, 0.995f)); // 왼쪽 진한 메뉴바 배경
            Map30UITheme.DrawSolid(new Rect(side.xMax - 2f, 0f, 2f, side.height), Map30UITheme.Cyan); // 왼쪽·오른쪽 구분 청록선

            if (blurTexture != null) // 캡처된 블러 화면 확인
            {
                GUI.DrawTexture(right, blurTexture, ScaleMode.StretchToFill, false); // 오른쪽 영역에만 블러 화면 확대 출력
            }

            Map30UITheme.DrawSolid(right, new Color(0.004f, 0.018f, 0.035f, 0.64f)); // 블러 위 남청색 반투명 필터
        }

        private void DrawSidebar(Rect side) // RESUME·SAVE·LOAD·SETTINGS·EXIT 왼쪽 메뉴바
        {
            float x = 24f; // 왼쪽 여백
            float width = side.width - 48f; // 메뉴 버튼 너비
            GUI.Label(new Rect(x, 28f, width, 18f), "TERMINAL // SYSTEM", systemStyle); // 시스템 라벨
            GUI.Label(new Rect(x, 50f, width, 44f), "PAUSE", menuTitleStyle); // 메뉴 제목
            Map30UITheme.DrawDivider(new Rect(x, 105f, width, 1f)); // 제목 구분선

            float y = 132f; // 첫 메뉴 버튼 위치
            if (DrawMenuButton(new Rect(x, y, width, 44f), "계속하기", false)) CloseMenu(); // 게임 복귀
            y += 54f; // 다음 버튼 위치
            if (DrawMenuButton(new Rect(x, y, width, 44f), "저장", page == MenuPage.Save)) ChangePage(MenuPage.Save); // 저장 페이지
            y += 54f; // 다음 버튼 위치
            if (DrawMenuButton(new Rect(x, y, width, 44f), "불러오기", page == MenuPage.Load)) ChangePage(MenuPage.Load); // 불러오기 페이지
            y += 54f; // 다음 버튼 위치
            if (DrawMenuButton(new Rect(x, y, width, 44f), "설정", page == MenuPage.Settings)) ChangePage(MenuPage.Settings); // 설정 페이지
            y += 54f; // 종료 버튼 위치
            if (DrawExitMenuButton(new Rect(x, y, width, 44f))) RequestExit(); // 게임 종료 확인창 열기

            GUI.Label(new Rect(x, side.height - 64f, width, 18f), "ESC  닫기", systemStyle); // ESC 닫기 안내
            GUI.Label(new Rect(x, side.height - 42f, width, 18f), "SAVE DATA V1", systemStyle); // 저장 버전 표시
        }

        private bool DrawMenuButton(Rect rect, string label, bool selected) // 왼쪽 공통 메뉴 버튼
        {
            Color background = selected ? new Color(0.025f, 0.130f, 0.180f, 0.96f) : new Color(0.012f, 0.045f, 0.070f, 0.92f); // 선택 여부 배경 선택
            Map30UITheme.DrawSolid(rect, background); // 버튼 배경
            Map30UITheme.DrawBorder(rect, selected ? Map30UITheme.Cyan : Map30UITheme.Soft, selected ? 2f : 1f); // 상태 외곽선
            if (selected) Map30UITheme.DrawSolid(new Rect(rect.x, rect.y, 4f, rect.height), Map30UITheme.Cyan); // 선택 왼쪽 강조선
            return GUI.Button(rect, label, menuButtonStyle); // 클릭 여부 반환
        }

        private void ChangePage(MenuPage newPage) // 오른쪽 콘텐츠 페이지 전환
        {
            page = newPage; // 선택 페이지 저장
            confirmOverwrite = false; // 덮어쓰기 확인 초기화
            confirmDelete = false; // 삭제 확인 초기화
            statusMessage = string.Empty; // 이전 결과 문구 초기화
            Map35SaveManager.Instance?.RefreshSlots(); // 저장 파일 변경 상태 재읽기
        }

        private void DrawRightContent(Rect right) // 현재 메뉴 페이지에 맞는 오른쪽 영역 출력
        {
            if (page == MenuPage.Save) // 저장 페이지 확인
            {
                DrawSlotPage(right, true); // SAVE 5x2 슬롯 출력
                return; // 다른 페이지 생략
            }

            if (page == MenuPage.Load) // 불러오기 페이지 확인
            {
                DrawSlotPage(right, false); // LOAD 5x2 슬롯 출력
                return; // 다른 페이지 생략
            }

            if (page == MenuPage.Settings) // 설정 자리 확인
            {
                DrawSettingsPlaceholder(right); // 추후 설정 모듈 자리 표시
                return; // 기본 화면 생략
            }

            return; // 기본 ESC 화면에서는 왼쪽 메뉴바만 유지하고 오른쪽 UI는 모두 비활성화
        }

        private void DrawSlotPage(Rect right, bool saveMode) // 5열×2행 저장 슬롯과 2배 글자 크기 화면
        {
            Map35SaveManager manager = Map35SaveManager.Instance; // 현재 SaveManager 조회
            if (manager == null) // 저장 시스템 준비 확인
            {
                GUI.Label(new Rect(right.x + 40f, right.y + 30f, right.width - 80f, 84f), "SAVE SYSTEM NOT READY", pageTitleStyle); // 준비 실패 안내
                return; // 슬롯 출력 중단
            }

            string title = saveMode ? "SAVE" : "LOAD"; // 현재 페이지 제목
            GUI.Label(new Rect(right.x + 38f, right.y + 14f, 260f, 84f), title, pageTitleStyle); // 2배 크기 페이지 제목
            GUI.Label(new Rect(right.x + 300f, right.y + 40f, right.width - 338f, 48f), saveMode ? "현재 플레이 상태를 슬롯에 기록합니다." : "저장된 플레이 상태를 현재 세션에 복원합니다.", slotSystemStyle); // 2배 크기 페이지 설명
            Map30UITheme.DrawDivider(new Rect(right.x + 38f, right.y + 104f, right.width - 76f, 2f)); // 확대된 상단 구분선

            float contentX = right.x + 38f; // 슬롯 영역 왼쪽
            float contentY = right.y + 122f; // 확대 제목 아래 슬롯 영역 상단
            float contentWidth = right.width - 76f; // 슬롯 전체 너비
            float footerHeight = 118f; // 확대된 하단 액션 영역 높이
            float contentHeight = right.height - contentY - footerHeight - 14f; // 슬롯 영역 높이
            const int columns = 5; // 가로 슬롯 수
            const int rows = 2; // 세로 슬롯 수
            float gap = Mathf.Clamp(contentWidth * 0.012f, 8f, 16f); // 카드 간격
            float cardWidth = (contentWidth - gap * (columns - 1)) / columns; // 카드 너비 계산
            float cardHeight = Mathf.Min((contentHeight - gap) / rows, cardWidth * 1.04f); // 확대 텍스트 공간을 확보한 카드 높이

            for (int row = 0; row < rows; row++) // 두 줄 슬롯 순회
            {
                for (int column = 0; column < columns; column++) // 한 줄 다섯 슬롯 순회
                {
                    int slot = row * columns + column + 1; // 1~10 슬롯 번호 계산
                    Rect card = new Rect(contentX + column * (cardWidth + gap), contentY + row * (cardHeight + gap), cardWidth, cardHeight); // 현재 카드 위치
                    DrawSlotCard(card, manager.SlotInfo(slot), slot == selectedSlot); // 슬롯 카드 출력
                }
            }

            DrawSlotFooter(new Rect(right.x + 38f, right.yMax - footerHeight, right.width - 76f, footerHeight - 12f), manager, saveMode); // 확대된 하단 선택 슬롯 액션
        }

        private void DrawSlotCard(Rect rect, Map35SlotInfo info, bool selected) // 2배 글자 크기의 단일 저장 카드
        {
            bool hasData = info != null && info.HasData; // 저장 데이터 존재 여부 확인
            Map30UITheme.DrawSolid(rect, new Color(0.010f, 0.035f, 0.055f, 0.96f)); // 카드 기본 배경
            Map30UITheme.DrawBorder(rect, selected ? Map30UITheme.Cyan : Map30UITheme.Soft, selected ? 2f : 1f); // 선택 카드 청록 강조

            Rect clickRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f); // 카드 전체 클릭 영역
            if (GUI.Button(clickRect, GUIContent.none, GUIStyle.none)) // 슬롯 선택 확인
            {
                selectedSlot = info != null ? info.Slot : selectedSlot; // 현재 슬롯 선택
                confirmOverwrite = false; // 다른 슬롯 선택 시 덮어쓰기 확인 해제
                confirmDelete = false; // 삭제 확인 해제
            }

            float previewHeight = rect.height * 0.48f; // 확대 텍스트 공간 확보용 미리보기 높이
            Rect preview = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, Mathf.Max(44f, previewHeight - 8f)); // 실제 미리보기 영역
            Map30UITheme.DrawSolid(preview, new Color(0.005f, 0.018f, 0.030f, 1f)); // 미리보기 바탕

            if (hasData && info.Preview != null) // 저장 이미지 존재 확인
            {
                GUI.DrawTexture(preview, info.Preview, ScaleMode.ScaleAndCrop, false); // 플레이 화면 썸네일 표시
                Map30UITheme.DrawSolid(new Rect(preview.x, preview.yMax - 28f, preview.width, 28f), new Color(0.002f, 0.015f, 0.025f, 0.72f)); // 썸네일 하단 정보 필터
            }
            else // 빈 슬롯 처리
            {
                GUI.Label(preview, "NO DATA", noDataStyle); // 2배 크기 빈 슬롯 표시
            }

            GUI.Label(new Rect(rect.x + rect.width - 68f, rect.y + 8f, 56f, 30f), (info != null ? info.Slot : 0).ToString("00"), cardSlotStyle); // 2배 크기 슬롯 번호

            if (!hasData || info.Meta == null) // 빈 슬롯 카드 확인
            {
                GUI.Label(new Rect(rect.x + 10f, preview.yMax + 8f, rect.width - 20f, 34f), "빈 저장 슬롯", cardTitleStyle); // 2배 크기 빈 슬롯 제목
                GUI.Label(new Rect(rect.x + 10f, preview.yMax + 42f, rect.width - 20f, 48f), "새 데이터를 저장할 수 있습니다.", cardBodyStyle); // 2배 크기 빈 슬롯 설명
                return; // 저장 정보 출력 생략
            }

            string missionTitle = Shorten(info.Meta.MissionTitle, Mathf.Max(7, Mathf.RoundToInt(rect.width / 14f))); // 큰 글자 기준 임무 제목 축약
            string objective = Shorten(info.Meta.ObjectiveText, Mathf.Max(8, Mathf.RoundToInt(rect.width / 12f))); // 큰 글자 기준 목표 문구 축약
            GUI.Label(new Rect(rect.x + 10f, preview.yMax + 6f, rect.width - 20f, 34f), missionTitle, cardTitleStyle); // 2배 크기 임무 제목
            GUI.Label(new Rect(rect.x + 10f, preview.yMax + 40f, rect.width - 20f, 44f), objective, cardBodyStyle); // 2배 크기 현재 목표
            GUI.Label(new Rect(rect.x + 10f, rect.yMax - 46f, rect.width - 20f, 40f), info.Meta.SavedAt + "\n" + info.Meta.Credits + " CR", cardBodyStyle); // 2배 크기 저장 시각·크레딧
        }

        private void DrawSlotFooter(Rect rect, Map35SaveManager manager, bool saveMode) // 2배 글자 크기의 선택 슬롯 액션 영역
        {
            Map30UITheme.DrawSolid(rect, new Color(0.008f, 0.030f, 0.050f, 0.92f)); // 하단 액션 배경
            Map30UITheme.DrawBorder(rect, Map30UITheme.Soft, 1f); // 액션 외곽선
            Map35SlotInfo info = manager.SlotInfo(selectedSlot); // 현재 선택 슬롯 정보
            bool hasData = info != null && info.HasData; // 선택 슬롯 데이터 존재 여부
            GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, 220f, 34f), "SLOT " + selectedSlot.ToString("00"), cardTitleStyle); // 2배 크기 선택 슬롯 표시

            string message = Time.unscaledTime < statusUntil ? statusMessage :
                             saveMode ? (hasData ? "저장 버튼을 두 번 누르면 기존 데이터를 덮어씁니다." : "현재 플레이 상태를 이 슬롯에 저장합니다.") :
                             hasData ? "선택 슬롯의 저장 데이터를 불러옵니다." : "저장 데이터가 없는 슬롯입니다."; // 현재 액션 안내
            GUI.Label(new Rect(rect.x + 16f, rect.y + 46f, rect.width * 0.48f, 52f), message, footerStyle); // 2배 크기 하단 상태 문구

            float buttonWidth = Mathf.Clamp(rect.width * 0.18f, 150f, 210f); // 확대된 하단 버튼 너비
            Rect mainButton = new Rect(rect.xMax - buttonWidth * 2f - 28f, rect.y + 26f, buttonWidth, 58f); // 확대된 저장·불러오기 버튼
            Rect deleteButton = new Rect(rect.xMax - buttonWidth - 14f, rect.y + 26f, buttonWidth, 58f); // 확대된 삭제 버튼
            string mainLabel = saveMode ? (confirmOverwrite ? "덮어쓰기 확인" : "저장") : "불러오기"; // 주 버튼 문구

            DrawActionBackground(mainButton, saveMode ? Map30UITheme.Cyan : Map30UITheme.Green); // 주 버튼 강조
            if (GUI.Button(mainButton, mainLabel, actionStyle)) // 저장·불러오기 클릭 확인
            {
                if (saveMode) SaveSelected(manager, info); // 현재 슬롯 저장 처리
                else LoadSelected(manager, info); // 불러오기 경고창 요청
            }

            DrawActionBackground(deleteButton, Map30UITheme.Danger); // 삭제 버튼 위험색
            if (GUI.Button(deleteButton, confirmDelete ? "삭제 확인" : "삭제", actionStyle)) // 슬롯 삭제 클릭 확인
            {
                DeleteSelected(manager, info); // 현재 슬롯 삭제 처리
            }
        }

        private void SaveSelected(Map35SaveManager manager, Map35SlotInfo info) // 선택 슬롯 저장·덮어쓰기 처리
        {
            bool hasData = info != null && info.HasData; // 기존 저장 데이터 확인

            if (hasData && !confirmOverwrite) // 첫 덮어쓰기 클릭 확인
            {
                confirmOverwrite = true; // 다음 클릭에서 실제 저장 허용
                confirmDelete = false; // 삭제 확인 해제
                ShowStatus("기존 데이터가 있습니다. 다시 누르면 덮어씁니다.", 3f); // 덮어쓰기 경고
                return; // 첫 클릭 저장 중단
            }

            bool success = manager.SaveSlot(selectedSlot, previewTexture); // 현재 상태와 메뉴 진입 전 미리보기 저장
            confirmOverwrite = false; // 덮어쓰기 확인 해제
            confirmDelete = false; // 삭제 확인 해제
            ShowStatus(success ? "저장이 완료되었습니다." : "저장에 실패했습니다.", 2.5f); // 저장 결과 표시
        }

        private void LoadSelected(Map35SaveManager manager, Map35SlotInfo info) // 선택 슬롯 불러오기 경고창 요청
        {
            if (info == null || !info.HasData) // 빈 슬롯 확인
            {
                ShowStatus("불러올 저장 데이터가 없습니다.", 2.5f); // 빈 슬롯 안내
                return; // 불러오기 중단
            }

            confirmationAction = ConfirmationAction.LoadSlot; // 별도 불러오기 경고창 표시
        }

        private void DeleteSelected(Map35SaveManager manager, Map35SlotInfo info) // 선택 슬롯 파일 삭제
        {
            if (info == null || !info.HasData) // 삭제 대상 존재 확인
            {
                ShowStatus("삭제할 저장 데이터가 없습니다.", 2.5f); // 빈 슬롯 안내
                return; // 삭제 중단
            }

            if (!confirmDelete) // 첫 삭제 클릭 확인
            {
                confirmDelete = true; // 다음 클릭 실제 삭제 허용
                confirmOverwrite = false; // 덮어쓰기 확인 해제
                ShowStatus("다시 누르면 이 슬롯을 삭제합니다.", 3f); // 삭제 경고
                return; // 첫 클릭 삭제 중단
            }

            bool success = manager.DeleteSlot(selectedSlot); // 실제 JSON·PNG 삭제
            confirmDelete = false; // 삭제 확인 초기화
            ShowStatus(success ? "슬롯 데이터가 삭제되었습니다." : "슬롯 삭제에 실패했습니다.", 2.5f); // 결과 표시
        }

        private bool DrawExitMenuButton(Rect rect) // 설정 아래 나가기 전용 위험 버튼
        {
            Map30UITheme.DrawSolid(rect, new Color(0.080f, 0.020f, 0.028f, 0.94f)); // 어두운 적색 종료 버튼 배경
            Map30UITheme.DrawBorder(rect, Map30UITheme.Danger, 1f); // 위험색 외곽선
            Map30UITheme.DrawSolid(new Rect(rect.x, rect.y, 4f, rect.height), Map30UITheme.Danger); // 왼쪽 위험 강조선
            return GUI.Button(rect, "나가기", menuButtonStyle); // 종료 확인창 요청 여부 반환
        }

        private void RequestExit() // 게임 종료 경고창 표시 요청
        {
            confirmationAction = ConfirmationAction.ExitGame; // 별도 종료 경고창 상태 적용
        }

        private void DrawConfirmationDialog() // 불러오기·게임 종료용 별도 확인창
        {
            Map30UITheme.DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.68f)); // 뒤쪽 메뉴 위 어두운 차단막

            float width = Mathf.Clamp(Screen.width * 0.44f, 520f, 720f); // 경고창 너비
            float height = 286f; // 경고창 높이
            Rect panel = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height); // 화면 중앙 경고창 배치
            Map30UITheme.DrawPanel(panel, true); // 기존 UI와 동일한 강한 패널
            Color accent = confirmationAction == ConfirmationAction.ExitGame ? Map30UITheme.Danger : Map30UITheme.Amber; // 동작별 강조색
            Map30UITheme.DrawSolid(new Rect(panel.x, panel.y, 5f, panel.height), accent); // 왼쪽 경고 강조선

            bool loadConfirm = confirmationAction == ConfirmationAction.LoadSlot; // 불러오기 경고 여부 확인
            string title = loadConfirm ? "저장 데이터 불러오기" : "게임 종료"; // 경고창 제목
            string body = loadConfirm ?
                "SLOT " + selectedSlot.ToString("00") + "의 저장 데이터를 불러오면 현재 저장되지 않은 진행 상황을 잃을 수 있습니다.\n정말 저장 데이터를 불러오시겠습니까?" :
                "저장하지 않은 진행 상황은 사라질 수 있습니다.\n정말 게임을 종료하시겠습니까?"; // 동작별 경고 본문

            GUI.Label(new Rect(panel.x + 26f, panel.y + 24f, panel.width - 52f, 42f), title, confirmTitleStyle); // 경고 제목 출력
            Map30UITheme.DrawDivider(new Rect(panel.x + 26f, panel.y + 76f, panel.width - 52f, 1f)); // 제목 구분선
            GUI.Label(new Rect(panel.x + 26f, panel.y + 98f, panel.width - 52f, 82f), body, confirmBodyStyle); // 경고 본문 출력

            float gap = 14f; // 두 버튼 간격
            float buttonWidth = (panel.width - 66f - gap) * 0.5f; // 확인·취소 동일 너비
            Rect cancel = new Rect(panel.x + 26f, panel.yMax - 70f, buttonWidth, 44f); // 취소 버튼
            Rect confirm = new Rect(cancel.xMax + gap, cancel.y, buttonWidth, 44f); // 위험 동작 확인 버튼
            DrawActionBackground(cancel, Map30UITheme.Soft); // 취소 버튼 프레임
            DrawActionBackground(confirm, accent); // 확인 버튼 경고 프레임

            if (GUI.Button(cancel, "취소", confirmButtonStyle)) // 취소 클릭 확인
            {
                confirmationAction = ConfirmationAction.None; // 경고창 닫기
            }

            if (GUI.Button(confirm, loadConfirm ? "불러오기" : "나가기", confirmButtonStyle)) // 실제 위험 동작 확인
            {
                if (loadConfirm) ConfirmLoadSelected(); // 슬롯 불러오기 실행
                else ConfirmExit(); // 게임 종료 실행
            }
        }

        private void ConfirmLoadSelected() // 경고창 확인 후 실제 슬롯 불러오기
        {
            Map35SaveManager manager = Map35SaveManager.Instance; // 현재 SaveManager 조회
            confirmationAction = ConfirmationAction.None; // 확인창 먼저 닫기
            bool success = manager != null && manager.LoadSlot(selectedSlot); // 선택 슬롯 JSON 상태 복원
            ShowStatus(success ? "불러오기가 완료되었습니다." : "불러오기에 실패했습니다.", 2.5f); // 결과 표시

            if (success) // 정상 불러오기 확인
            {
                CloseMenu(); // 복원된 게임 화면으로 즉시 복귀
            }
        }

        private void ConfirmExit() // 경고창 확인 후 실제 게임 종료
        {
            confirmationAction = ConfirmationAction.None; // 경고 상태 정리
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Editor에서는 Play Mode 종료
#else
            Application.Quit(); // 빌드에서는 실제 애플리케이션 종료
#endif
        }

        private void DrawSettingsPlaceholder(Rect right) // Day36 상단 탭형 실제 설정 페이지 연결
        {
            ProjectK.Day36.Map36SettingsPage.Draw(right); // 그래픽·사운드·조작·게임플레이 탭을 같은 오른쪽 영역에 출력
        }

        private static void DrawActionBackground(Rect rect, Color accent) // 하단 액션 버튼 공통 배경
        {
            Map30UITheme.DrawSolid(rect, new Color(0.012f, 0.050f, 0.075f, 0.98f)); // 버튼 바탕
            Map30UITheme.DrawBorder(rect, accent, 1f); // 상태별 외곽선
        }

        private void ShowStatus(string message, float duration) // 하단 결과 메시지 표시
        {
            statusMessage = message ?? string.Empty; // 메시지 저장
            statusUntil = Time.unscaledTime + Mathf.Max(1f, duration); // 표시 종료 시각 저장
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height) // 화면 캡처를 미리보기·블러 해상도로 축소
        {
            if (source == null) // 원본 존재 확인
            {
                return null; // 축소 실패
            }

            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32); // GPU 축소용 임시 RT 생성
            RenderTexture previous = RenderTexture.active; // 기존 활성 RT 보존

            try // RT 복구 보장
            {
                temporary.filterMode = FilterMode.Bilinear; // 축소 필터 설정
                Graphics.Blit(source, temporary); // 원본 화면 GPU 축소
                RenderTexture.active = temporary; // ReadPixels 대상 RT 지정
                Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false); // CPU 텍스처 생성
                result.name = "Day35_Capture_" + width + "x" + height; // 디버그 이름 적용
                result.filterMode = FilterMode.Bilinear; // 확대 시 부드러운 표시
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false); // 축소 화면 읽기
                result.Apply(false, false); // 텍스처 픽셀 반영
                return result; // 축소 텍스처 반환
            }
            finally // 임시 RT 정리
            {
                RenderTexture.active = previous; // 기존 활성 RT 복원
                RenderTexture.ReleaseTemporary(temporary); // 임시 RT 반환
            }
        }

        private static void ApplyBoxBlur(Texture2D texture, int passes) // 저해상도 화면에 CPU 3x3 박스 블러 적용
        {
            if (texture == null || passes <= 0) // 블러 대상 확인
            {
                return; // 처리 생략
            }

            int width = texture.width; // 텍스처 너비
            int height = texture.height; // 텍스처 높이
            Color32[] source = texture.GetPixels32(); // 원본 픽셀 배열
            Color32[] target = new Color32[source.Length]; // 블러 결과 배열

            for (int pass = 0; pass < passes; pass++) // 요청 블러 횟수 반복
            {
                for (int y = 0; y < height; y++) // 모든 행 순회
                {
                    for (int x = 0; x < width; x++) // 모든 열 순회
                    {
                        int r = 0; // 빨강 누적
                        int g = 0; // 초록 누적
                        int b = 0; // 파랑 누적
                        int count = 0; // 샘플 수

                        for (int oy = -1; oy <= 1; oy++) // 주변 3행 순회
                        {
                            int sy = Mathf.Clamp(y + oy, 0, height - 1); // 안전 Y 좌표

                            for (int ox = -1; ox <= 1; ox++) // 주변 3열 순회
                            {
                                int sx = Mathf.Clamp(x + ox, 0, width - 1); // 안전 X 좌표
                                Color32 sample = source[sy * width + sx]; // 주변 픽셀 조회
                                r += sample.r; // 빨강 누적
                                g += sample.g; // 초록 누적
                                b += sample.b; // 파랑 누적
                                count++; // 샘플 수 증가
                            }
                        }

                        target[y * width + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255); // 평균 블러 픽셀 저장
                    }
                }

                Color32[] swap = source; // 배열 교환 임시 참조
                source = target; // 결과를 다음 패스 원본으로 사용
                target = swap; // 기존 원본 배열을 다음 결과 버퍼로 재사용
            }

            texture.SetPixels32(source); // 최종 블러 픽셀 적용
            texture.Apply(false, false); // 텍스처 갱신
        }

        private static void ReplaceTexture(ref Texture2D destination, Texture2D replacement) // 메뉴 캡처 텍스처 안전 교체
        {
            if (destination != null) Destroy(destination); // 이전 텍스처 메모리 정리
            destination = replacement; // 새 텍스처 저장
        }

        private static string Shorten(string value, int maxLength) // 슬롯 카드 한 줄 문구 축약
        {
            if (string.IsNullOrWhiteSpace(value)) // 빈 문구 확인
            {
                return "---"; // 기본값 반환
            }

            string line = value.Replace("\r", " ").Replace("\n", " ").Trim(); // 줄바꿈 제거
            return line.Length <= maxLength ? line : line.Substring(0, Mathf.Max(1, maxLength - 1)) + "…"; // 말줄임 처리
        }

        private static bool IsOptionalUiOpen(string fullTypeName) // 현재 빌드에 선택 기능이 존재할 때만 열린 상태 확인
        {
            System.Type type = FindOptionalType(fullTypeName); // 선택 시스템 타입 검색
            if (type == null) // 현재 프로젝트에 타입이 없는 경우 확인
            {
                return false; // 선택 UI가 없는 빌드로 처리
            }

            System.Reflection.PropertyInfo property = type.GetProperty("IsOpen", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); // 공개 정적 IsOpen 속성 조회
            object value = property != null ? property.GetValue(null) : null; // 열린 상태 읽기
            return value is bool flag && flag; // bool 상태 반환
        }

        private static System.Type FindOptionalType(string fullTypeName) // Assembly-CSharp 내부 선택 타입 검색
        {
            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); // 현재 로드된 어셈블리 목록 조회
            for (int i = 0; i < assemblies.Length; i++) // 모든 어셈블리 순회
            {
                System.Type type = assemblies[i].GetType(fullTypeName, false); // 정확한 전체 이름으로 타입 검색
                if (type != null) // 대상 타입 발견 확인
                {
                    return type; // 타입 반환
                }
            }

            return null; // 선택 타입 없음
        }

        private void EnsureStyles() // ESC 메뉴와 확대된 저장·불러오기 텍스트 스타일 준비
        {
            if (systemStyle != null) // 기존 스타일 확인
            {
                return; // 재생성 생략
            }

            systemStyle = Style(10, FontStyle.Bold, Map30UITheme.Muted, TextAnchor.MiddleLeft); // 왼쪽 메뉴 시스템 라벨 기존 크기 유지
            menuTitleStyle = Style(31, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 왼쪽 PAUSE 제목 기존 크기 유지
            menuButtonStyle = Style(14, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 왼쪽 메뉴 버튼 기존 크기 유지
            menuButtonStyle.padding = new RectOffset(16, 8, 0, 0); // 메뉴 버튼 왼쪽 여백

            pageTitleStyle = Style(68, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // SAVE·LOAD 제목 34→68 두 배
            slotSystemStyle = Style(20, FontStyle.Bold, Map30UITheme.Muted, TextAnchor.MiddleLeft); // 페이지 설명 10→20 두 배
            cardSlotStyle = Style(20, FontStyle.Bold, Map30UITheme.Cyan, TextAnchor.MiddleRight); // 슬롯 번호 10→20 두 배
            cardTitleStyle = Style(24, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 카드 제목 12→24 두 배
            cardBodyStyle = Style(18, FontStyle.Normal, Map30UITheme.Muted, TextAnchor.UpperLeft); // 카드 메타 9→18 두 배
            cardBodyStyle.wordWrap = true; // 큰 카드 글자 자동 줄바꿈
            noDataStyle = Style(32, FontStyle.Bold, new Color(Map30UITheme.Muted.r, Map30UITheme.Muted.g, Map30UITheme.Muted.b, 0.72f), TextAnchor.MiddleCenter); // NO DATA 16→32 두 배
            footerStyle = Style(22, FontStyle.Normal, Map30UITheme.Muted, TextAnchor.UpperLeft); // 하단 상태 11→22 두 배
            footerStyle.wordWrap = true; // 긴 상태 문구 줄바꿈
            actionStyle = Style(24, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleCenter); // 저장·불러오기·삭제 버튼 12→24 두 배
            centerStyle = Style(14, FontStyle.Normal, Map30UITheme.Muted, TextAnchor.MiddleLeft); // 기타 기본 안내 기존 크기 유지

            confirmTitleStyle = Style(28, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 별도 경고창 제목
            confirmBodyStyle = Style(17, FontStyle.Normal, Map30UITheme.Text, TextAnchor.UpperLeft); // 별도 경고창 본문
            confirmBodyStyle.wordWrap = true; // 경고 본문 자동 줄바꿈
            confirmButtonStyle = Style(16, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleCenter); // 경고창 확인·취소 버튼
        }

        private static GUIStyle Style(int size, FontStyle fontStyle, Color color, TextAnchor alignment) // 공통 GUI 스타일 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 스타일 복사
            style.fontSize = size; // 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 글자 색상 적용
            style.alignment = alignment; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 기본 여백 제거
            return style; // 완성 스타일 반환
        }
    }
}
