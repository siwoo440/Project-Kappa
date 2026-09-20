using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 전체 지도 상태 참조
using ProjectK.Day31; // Day31 실제 MissionManager 수락·재시도 연동
using UnityEngine; // IMGUI 기반 전체 임무 창
using UnityEngine.InputSystem; // Tab·ESC 입력 처리

namespace ProjectK.Day30 // 30일차 전체 임무 창 이름 공간
{
    [DisallowMultipleComponent] // 전체 임무 창 중복 방지
    public sealed class Map30MissionWindow : MonoBehaviour // Tab으로 열고 닫는 보유 임무 전체 화면
    {
        private enum MissionFilter // 임무 목록 필터
        {
            All, // 전체
            Main, // 메인
            Side // 서브
        }

        private static readonly Color background = Map30UITheme.BackgroundStrong; // 전체 화면 짙은 배경
        private static readonly Color panel = new Color(0.014f, 0.040f, 0.068f, 0.97f); // 기본 패널 배경
        private static readonly Color panelAlt = new Color(0.020f, 0.075f, 0.110f, 0.90f); // 선택·정보 패널 배경
        private static readonly Color accent = Map30UITheme.Cyan; // 참고 이미지 기반 주 적색
        private static readonly Color accentSoft = Map30UITheme.Soft; // 보조 적색선
        private static readonly Color cyan = Map30UITheme.Cyan; // Day29 단말기 연동 청록색
        private static readonly Color amber = Map30UITheme.Amber; // 수락 가능·선택 강조
        private static readonly Color green = Map30UITheme.Green; // 완료 상태
        private static readonly Color text = Map30UITheme.Text; // 기본 텍스트
        private static readonly Color muted = Map30UITheme.Muted; // 보조 텍스트

        private static Map30MissionWindow instance; // 현재 전체 임무 창
        private MapWorldRoot world; // 본편 월드
        private MapNavigationUI navigationUI; // 전체 지도 UI
        private PlayerMovement movement; // 플레이어 이동
        private ThirdPersonCamera cameraController; // 카메라 입력
        private PlayerFirearmController firearm; // 총기 상태
        private PlayerHealth health; // 사망 상태
        private bool open; // 현재 임무 창 표시 여부
        private bool previousMovementEnabled = true; // 창 열기 전 이동 상태
        private bool previousCameraEnabled = true; // 창 열기 전 카메라 상태
        private bool previousNavigationEnabled = true; // 창 열기 전 지도 UI 상태
        private float previousTimeScale = 1f; // 창 열기 전 시간 배율
        private CursorLockMode previousCursorLock; // 창 열기 전 커서 잠금
        private bool previousCursorVisible; // 창 열기 전 커서 표시
        private MissionFilter filter = MissionFilter.All; // 현재 목록 필터
        private Vector2 missionScroll; // 왼쪽 임무 목록 스크롤
        private Vector2 detailScroll; // 오른쪽 상세 내용 스크롤
        private float nextResolveTime; // 씬 참조 복구 시각
        private float styledScale = -1f; // 스타일 생성 배율
        private GUIStyle titleStyle; // 최상단 제목 스타일
        private GUIStyle tabStyle; // 상단 탭 스타일
        private GUIStyle listTitleStyle; // 임무 목록 제목
        private GUIStyle listItemStyle; // 임무 목록 기본 항목
        private GUIStyle listItemSelectedStyle; // 선택 임무 항목
        private GUIStyle missionTitleStyle; // 상세 임무 제목
        private GUIStyle bodyStyle; // 상세 본문
        private GUIStyle smallStyle; // 작은 정보
        private GUIStyle objectiveStyle; // 목표 목록
        private GUIStyle statusStyle; // 상태 배지 텍스트

        public static Map30MissionWindow Instance => instance; // 현재 창 조회
        public static bool IsOpen => instance != null && instance.open; // 다른 HUD가 사용할 전체 임무 창 상태

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 창 참조 제거
        }

        private void Awake() // 단일 창 등록
        {
            if (instance != null && instance != this) // 다른 창 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 창 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 플레이어 참조 연결
        {
            ResolveReferences(); // 본편 월드·플레이어·지도 UI 연결
        }

        private void Update() // Tab·ESC 전체 임무 창 입력 처리
        {
            Keyboard keyboard = Keyboard.current; // 현재 키보드 조회
            if (keyboard == null) // 키보드 미연결 확인
            {
                return; // 입력 처리 생략
            }

            if (keyboard.tabKey.wasPressedThisFrame) // Tab 열기·닫기 입력 확인
            {
                Toggle(); // 전체 임무 창 상태 전환
                return; // 같은 프레임 다른 메뉴 입력 방지
            }

            if (open && keyboard.escapeKey.wasPressedThisFrame) // 열린 상태 ESC 입력 확인
            {
                Close(); // 임무 창 닫기
                return; // 현재 프레임 처리 종료
            }

            if ((world == null || world.Player == null) && Time.unscaledTime >= nextResolveTime) // 씬 전환·생성 순서 참조 복구 확인
            {
                nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 예약
                ResolveReferences(); // 현재 씬 참조 갱신
            }
        }

        private void OnDisable() // 컴포넌트 비활성 시 게임 상태 복원
        {
            if (open) // 열린 임무 창 확인
            {
                Close(); // 시간·입력·커서 복구
            }
        }

        private void OnDestroy() // 관리자 파괴 시 정적 참조와 입력 상태 정리
        {
            if (open) // 열린 상태 확인
            {
                Close(); // 게임 상태 복원
            }

            if (instance == this) // 현재 인스턴스 확인
            {
                instance = null; // 정적 참조 해제
            }
        }

        public void Toggle() // Tab 입력용 전체 임무 창 상태 전환
        {
            if (open) // 현재 열린 상태 확인
            {
                Close(); // 닫기
            }
            else // 현재 닫힌 상태
            {
                Open(); // 열기
            }
        }

        public void Open() // 전체 임무 창 열기와 플레이 입력 잠금
        {
            if (open) // 중복 열기 확인
            {
                return; // 처리 생략
            }

            ResolveReferences(); // 최신 플레이어 상태 연결

            if (navigationUI != null && navigationUI.FullMapOpen) // 기존 M 전체 지도 화면 확인
            {
                return; // 두 전체 화면 UI 동시 표시 방지
            }

            if (health != null && health.IsDead) // 사망 상태 확인
            {
                return; // 사망 중 임무 창 열기 방지
            }

            previousTimeScale = Time.timeScale; // 기존 시간 배율 저장
            previousCursorLock = Cursor.lockState; // 기존 커서 잠금 저장
            previousCursorVisible = Cursor.visible; // 기존 커서 표시 저장
            previousMovementEnabled = movement == null || movement.MovementEnabled; // 기존 이동 상태 저장
            previousCameraEnabled = cameraController == null || cameraController.enabled; // 기존 카메라 상태 저장
            previousNavigationEnabled = navigationUI == null || navigationUI.enabled; // 기존 지도 UI 상태 저장
            open = true; // 임무 창 표시 상태 적용

            firearm?.Interrupt(); // 조준·사격·재장전 진행 상태 안전 중단
            movement?.SetMovementEnabled(false); // 메뉴 중 플레이어 이동 차단

            if (navigationUI != null) // 지도 UI 존재 확인
            {
                navigationUI.enabled = false; // M·N 지도 입력과 미니맵 표시 임시 중지
            }

            if (cameraController != null) // 카메라 입력 존재 확인
            {
                cameraController.enabled = false; // 마우스 시점 입력 중지와 커서 해제
            }

            Time.timeScale = 0f; // 임무 확인 중 도시·전투 시간 정지
            Cursor.lockState = CursorLockMode.None; // 임무 선택용 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시
        }

        public void Close() // 전체 임무 창 닫기와 이전 게임 상태 복원
        {
            if (!open) // 이미 닫힌 상태 확인
            {
                return; // 처리 생략
            }

            open = false; // UI 표시 먼저 해제
            Time.timeScale = previousTimeScale; // 기존 시간 배율 복원

            if (movement != null) // 플레이어 이동 참조 확인
            {
                movement.SetMovementEnabled(previousMovementEnabled); // 창 열기 전 이동 상태 복원
            }

            if (navigationUI != null) // 지도 UI 참조 확인
            {
                navigationUI.enabled = previousNavigationEnabled; // 창 열기 전 지도 UI 상태 복원
            }

            if (cameraController != null) // 카메라 참조 확인
            {
                cameraController.enabled = previousCameraEnabled; // 창 열기 전 카메라 상태 복원
            }

            Cursor.lockState = previousCursorLock; // 기존 커서 잠금 복원
            Cursor.visible = previousCursorVisible; // 기존 커서 표시 복원
        }

        private void ResolveReferences() // 현재 Map 플레이어와 UI 참조 연결
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 본편 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결

            if (world == null) // Map 씬이 아닌 경우 확인
            {
                movement = null; // 이전 플레이어 이동 참조 제거
                cameraController = null; // 이전 카메라 참조 제거
                firearm = null; // 이전 총기 참조 제거
                health = null; // 이전 생존 참조 제거
                navigationUI = null; // 이전 지도 UI 참조 제거
                return; // 검색 종료
            }

            navigationUI = world.GetComponent<MapNavigationUI>(); // Day19 지도 UI 연결
            GameObject player = world.Player; // 현재 플레이어 조회
            movement = player != null ? player.GetComponent<PlayerMovement>() : null; // 이동 시스템 연결
            firearm = player != null ? player.GetComponent<PlayerFirearmController>() : null; // 총기 시스템 연결
            health = player != null ? player.GetComponent<PlayerHealth>() : null; // 생존 상태 연결
            Camera mainCamera = Camera.main; // 현재 메인 카메라 조회
            cameraController = mainCamera != null ? mainCamera.GetComponent<ThirdPersonCamera>() : null; // 카메라 입력 연결
        }

        private void OnGUI() // 전체 화면 임무 아카이브 렌더링
        {
            if (!open) // 닫힌 상태 확인
            {
                return; // UI 출력 생략
            }

            Map30MissionJournal journal = Map30MissionJournal.Instance; // 현재 보유 임무 Journal 조회
            if (journal == null) // Journal 생성 순서 확인
            {
                return; // 첫 프레임 출력 생략
            }

            int previousDepth = GUI.depth; // 기존 IMGUI 깊이 보존
            Matrix4x4 previousMatrix = GUI.matrix; // 기존 화면 변환 보존
            Color previousColor = GUI.color; // 기존 GUI 색상 보존
            GUI.depth = -1000; // 기존 HP·단말기·미니맵보다 위에 렌더링

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), 0.58f, 1.5f); // 1600x900 기준 전체 화면 스케일
            EnsureStyles(scale); // 현재 해상도용 텍스트 스타일 준비
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale); // 가상 해상도 스케일 적용

            float width = Screen.width / scale; // 스케일 보정 화면 너비
            float height = Screen.height / scale; // 스케일 보정 화면 높이

            try // 다른 IMGUI 상태 복구 보장
            {
                DrawBackground(width, height); // 전체 배경과 프레임 출력
                DrawTopBar(width); // 제목·필터·조작 안내 출력
                DrawMissionList(journal, width, height); // 왼쪽 보유 임무 목록 출력
                DrawMissionDetails(journal, width, height); // 오른쪽 선택 임무 상세 출력
            }
            finally // 기존 HUD 렌더링 상태 복원
            {
                GUI.matrix = previousMatrix; // 화면 변환 복원
                GUI.color = previousColor; // GUI 색상 복원
                GUI.depth = previousDepth; // 기존 GUI 깊이 복원
            }
        }

        private void DrawBackground(float width, float height) // 참고 이미지 스타일 전체 화면 프레임
        {
            DrawSolid(new Rect(0f, 0f, width, height), background); // 거의 불투명한 메뉴 배경
            DrawSolid(new Rect(18f, 18f, width - 36f, height - 36f), new Color(0.012f, 0.035f, 0.060f, 0.97f)); // 내부 화면 배경
            DrawBorder(new Rect(18f, 18f, width - 36f, height - 36f), accentSoft, 2f); // 외곽 적색 프레임
            DrawSolid(new Rect(18f, 18f, width - 36f, 4f), accent); // 상단 강조선
            DrawSolid(new Rect(18f, height - 22f, width - 36f, 2f), accentSoft); // 하단 상태선
            DrawSolid(new Rect(36f, 82f, width - 72f, 1f), accentSoft); // 상단 메뉴 구분선
            DrawSolid(new Rect(36f, 86f, 210f, 2f), cyan); // 좌측 메뉴 청록 강조선
            DrawSolid(new Rect(width - 260f, 86f, 224f, 2f), Map30UITheme.Blue); // 우측 메뉴 파랑 강조선
        }

        private void DrawTopBar(float width) // 제목과 필터 탭
        {
            GUI.Label(new Rect(42f, 30f, 440f, 38f), "TERMINAL // MISSION ARCHIVE", titleStyle); // 전체 임무 창 제목
            GUI.Label(new Rect(width - 410f, 35f, 360f, 24f), "보유 임무 전체 보기   [TAB] 닫기   [ESC] 닫기", smallStyle); // 조작 안내

            float tabX = 510f; // 첫 필터 탭 X 위치
            DrawFilterButton(new Rect(tabX, 32f, 110f, 34f), "전체", MissionFilter.All); // 전체 임무 탭
            DrawFilterButton(new Rect(tabX + 118f, 32f, 110f, 34f), "메인", MissionFilter.Main); // 메인 탭
            DrawFilterButton(new Rect(tabX + 236f, 32f, 110f, 34f), "서브", MissionFilter.Side); // 서브 탭
        }

        private void DrawFilterButton(Rect rect, string label, MissionFilter targetFilter) // 상단 임무 분류 탭 버튼
        {
            bool selected = filter == targetFilter; // 현재 선택 필터 확인
            DrawSolid(rect, selected ? new Color(accent.r, accent.g, accent.b, 0.24f) : new Color(0.018f, 0.060f, 0.090f, 0.88f)); // 필터 배경
            DrawBorder(rect, selected ? accent : accentSoft, selected ? 2f : 1f); // 선택 필터 강조선

            if (GUI.Button(rect, label, tabStyle)) // 필터 클릭 확인
            {
                filter = targetFilter; // 새 필터 적용
                missionScroll = Vector2.zero; // 목록 스크롤 상단으로 복귀
            }
        }

        private void DrawMissionList(Map30MissionJournal journal, float width, float height) // 왼쪽 보유 임무 목록
        {
            Rect area = new Rect(40f, 103f, Mathf.Clamp(width * 0.34f, 390f, 540f), height - 145f); // 왼쪽 목록 전체 영역
            DrawSolid(area, panel); // 목록 배경
            DrawBorder(area, accentSoft, 1f); // 목록 외곽선
            GUI.Label(new Rect(area.x + 16f, area.y + 12f, area.width - 32f, 28f), "임무 목록", listTitleStyle); // 목록 제목

            int visibleCount = CountVisible(journal); // 현재 필터 임무 수 계산
            GUI.Label(new Rect(area.x + area.width - 120f, area.y + 14f, 100f, 22f), visibleCount + "건", smallStyle); // 필터 결과 수 표시

            Rect scrollRect = new Rect(area.x + 10f, area.y + 50f, area.width - 20f, area.height - 62f); // 실제 임무 스크롤 영역
            float contentHeight = Mathf.Max(scrollRect.height - 1f, visibleCount * 72f + 6f); // 임무 수에 맞는 콘텐츠 높이
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, contentHeight); // 스크롤 콘텐츠 영역
            missionScroll = GUI.BeginScrollView(scrollRect, missionScroll, viewRect, false, true); // 세로 임무 스크롤 시작

            float y = 2f; // 첫 임무 항목 Y 위치
            for (int i = 0; i < journal.Missions.Count; i++) // 전체 보유 임무 순회
            {
                Map30MissionEntry entry = journal.Missions[i]; // 현재 임무 조회
                if (entry == null || !MatchesFilter(entry)) // 필터 대상 확인
                {
                    continue; // 다음 임무 처리
                }

                Rect item = new Rect(2f, y, viewRect.width - 4f, 62f); // 단일 임무 카드 영역
                bool selected = journal.SelectedMissionId == entry.MissionId; // 현재 선택 임무 확인
                DrawMissionListItem(journal, entry, item, selected); // 임무 카드 출력
                y += 72f; // 다음 카드 위치 이동
            }

            GUI.EndScrollView(); // 임무 목록 스크롤 종료
        }

        private void DrawMissionListItem(Map30MissionJournal journal, Map30MissionEntry entry, Rect rect, bool selected) // 임무 목록 단일 카드
        {
            Color backgroundColor = selected ? new Color(0.025f, 0.110f, 0.150f, 0.95f) : new Color(0.015f, 0.047f, 0.075f, 0.94f); // 선택 여부 배경
            DrawSolid(rect, backgroundColor); // 카드 배경
            DrawBorder(rect, selected ? accent : new Color(0.08f, 0.30f, 0.46f, 0.90f), selected ? 2f : 1f); // 카드 외곽선
            DrawSolid(new Rect(rect.x, rect.y, 4f, rect.height), selected ? accent : StatusColor(entry.Status)); // 왼쪽 상태 표시선

            Rect buttonRect = new Rect(rect.x + 5f, rect.y + 1f, rect.width - 6f, rect.height - 2f); // 클릭 영역

            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none)) // 임무 선택 클릭 확인
            {
                journal.SelectMission(entry.MissionId); // 상세 패널 선택 임무 갱신
                detailScroll = Vector2.zero; // 상세 스크롤 상단으로 복귀
            }

            GUI.Label(new Rect(rect.x + 15f, rect.y + 8f, 76f, 20f), entry.MissionId, selected ? listItemSelectedStyle : listItemStyle); // 임무 ID
            GUI.Label(new Rect(rect.x + 93f, rect.y + 8f, rect.width - 108f, 22f), entry.Title, selected ? listItemSelectedStyle : listItemStyle); // 임무 제목

            Color oldColor = GUI.color; // 상태 색상 적용 전 보존
            GUI.color = StatusColor(entry.Status); // 상태별 색상 적용
            GUI.Label(new Rect(rect.x + 15f, rect.y + 35f, 110f, 18f), StatusText(entry.Status), smallStyle); // 임무 상태
            GUI.color = oldColor; // 기본 색상 복원

            GUI.Label(new Rect(rect.x + 135f, rect.y + 35f, rect.width - 150f, 18f), entry.Region + "  /  " + entry.TypeLabel, smallStyle); // 지역과 유형 표시
        }

        private void DrawMissionDetails(Map30MissionJournal journal, float width, float height) // 오른쪽 선택 임무 상세
        {
            float leftWidth = Mathf.Clamp(width * 0.34f, 390f, 540f); // 왼쪽 목록 너비와 동일 계산
            Rect area = new Rect(56f + leftWidth, 103f, width - leftWidth - 96f, height - 145f); // 오른쪽 상세 영역
            DrawSolid(area, new Color(0.025f, 0.025f, 0.035f, 0.96f)); // 상세 배경
            DrawBorder(area, new Color(0.08f, 0.28f, 0.42f, 0.86f), 1f); // 상세 외곽선

            Map30MissionEntry entry = journal.SelectedMission(); // 현재 선택 임무 조회
            if (entry == null) // 보유 임무 없음 확인
            {
                GUI.Label(new Rect(area.x + 30f, area.y + 35f, area.width - 60f, 40f), "보유한 임무가 없습니다.", missionTitleStyle); // 빈 목록 안내
                return; // 상세 처리 종료
            }

            Rect scrollRect = new Rect(area.x + 12f, area.y + 12f, area.width - 24f, area.height - 92f); // 하단 임무 액션 영역을 제외한 상세 스크롤 화면
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, Mathf.Max(scrollRect.height - 1f, 700f)); // 상세 콘텐츠 가상 영역
            detailScroll = GUI.BeginScrollView(scrollRect, detailScroll, viewRect, false, true); // 상세 세로 스크롤 시작

            float x = 18f; // 상세 내부 X 여백
            float contentWidth = viewRect.width - 36f; // 상세 실제 너비
            float y = 12f; // 상세 시작 Y

            GUI.Label(new Rect(x, y, 100f, 25f), entry.MissionId, statusStyle); // 임무 ID
            Color oldColor = GUI.color; // 상태 색상 보존
            GUI.color = StatusColor(entry.Status); // 현재 임무 상태 색상
            GUI.Label(new Rect(contentWidth - 170f, y, 170f, 25f), StatusText(entry.Status), statusStyle); // 우측 상태 표시
            GUI.color = oldColor; // 기본 색상 복원
            y += 34f; // 다음 줄 이동

            GUI.Label(new Rect(x, y, contentWidth, 38f), entry.Title, missionTitleStyle); // 임무 제목
            y += 48f; // 다음 정보 영역 이동

            DrawInfoRow(x, ref y, contentWidth, "유형", entry.TypeLabel); // 임무 유형
            DrawInfoRow(x, ref y, contentWidth, "의뢰인", entry.Client); // 의뢰인
            DrawInfoRow(x, ref y, contentWidth, "지역", entry.Region); // 주요 지역
            DrawInfoRow(x, ref y, contentWidth, "보상", entry.Reward); // 보상
            y += 8f; // 설명 전 여백

            DrawSolid(new Rect(x, y, contentWidth, 1f), accentSoft); // 설명 구분선
            y += 18f; // 설명 위치 이동
            GUI.Label(new Rect(x, y, contentWidth, 24f), "의뢰 정보", listTitleStyle); // 설명 제목
            y += 31f; // 설명 본문 위치 이동

            float summaryHeight = bodyStyle.CalcHeight(new GUIContent(entry.Summary), contentWidth); // 설명 자동 높이 계산
            GUI.Label(new Rect(x, y, contentWidth, summaryHeight), entry.Summary, bodyStyle); // 임무 설명
            y += summaryHeight + 22f; // 목표 목록 전 여백

            DrawSolid(new Rect(x, y, contentWidth, 1f), accentSoft); // 목표 목록 구분선
            y += 18f; // 목표 제목 위치 이동
            GUI.Label(new Rect(x, y, contentWidth, 24f), "진행 목표", listTitleStyle); // 목표 목록 제목
            y += 34f; // 첫 목표 위치 이동

            if (entry.Objectives.Count == 0) // 목표 데이터 없음 확인
            {
                GUI.Label(new Rect(x, y, contentWidth, 24f), "등록된 목표가 없습니다.", bodyStyle); // 빈 목표 안내
                y += 32f; // 다음 영역 이동
            }
            else // 목표 데이터 존재
            {
                for (int i = 0; i < entry.Objectives.Count; i++) // 모든 목표 순회
                {
                    bool current = entry.Status == Map30MissionStatus.Tracking && i == entry.CurrentObjectiveIndex; // 현재 진행 목표 확인
                    bool completed = entry.Status == Map30MissionStatus.Completed || i < entry.CurrentObjectiveIndex; // 이전 완료 목표 판단
                    string prefix = completed ? "✓" : current ? "◆" : "◇"; // 목표 상태 기호 선택
                    Color itemColor = completed ? green : current ? cyan : text; // 목표 상태 색상 선택
                    DrawSolid(new Rect(x, y + 3f, 3f, 26f), itemColor); // 목표 왼쪽 인디케이터
                    Color previous = GUI.color; // 목표 색상 보존
                    GUI.color = itemColor; // 목표 상태 색상 적용
                    GUI.Label(new Rect(x + 12f, y, 28f, 30f), prefix, objectiveStyle); // 목표 상태 기호
                    GUI.Label(new Rect(x + 42f, y, contentWidth - 42f, 34f), entry.Objectives[i], objectiveStyle); // 목표 문구
                    GUI.color = previous; // 기본 색상 복원
                    y += 39f; // 다음 목표 위치 이동
                }
            }

            y += 10f; // 하단 정보 전 여백
            if (entry.DevelopmentSeed) // MissionManager 전 임시 UI 데이터 확인
            {
                DrawSolid(new Rect(x, y, contentWidth, 52f), new Color(0.12f, 0.07f, 0.02f, 0.70f)); // 개발 데이터 안내 배경
                DrawBorder(new Rect(x, y, contentWidth, 52f), new Color(amber.r, amber.g, amber.b, 0.50f), 1f); // 개발 데이터 안내 외곽선
                GUI.Label(new Rect(x + 12f, y + 8f, contentWidth - 24f, 36f), "현재는 임무 창 검증용 보유 데이터입니다. 이후 MissionManager가 같은 ID를 실제 임무 데이터로 교체합니다.", smallStyle); // 개발 단계 안내
            }

            GUI.EndScrollView(); // 상세 스크롤 종료
            DrawMissionAction(entry, area); // 하단 수락·진행·재시도 액션 표시
        }

        private void DrawMissionAction(Map30MissionEntry entry, Rect area) // Day31 MissionManager와 연결된 하단 임무 액션
        {
            Rect action = new Rect(area.x + 12f, area.yMax - 68f, area.width - 24f, 54f); // 상세 패널 하단 액션 영역
            DrawSolid(action, new Color(0.015f, 0.060f, 0.090f, 0.96f)); // 공통 파란색 액션 배경
            DrawBorder(action, cyan, 1f); // 청록색 액션 외곽선

            Map31MissionManager manager = Map31MissionManager.Instance; // 실제 MissionManager 조회
            if (manager == null || !manager.SupportsMission(entry.MissionId)) // 아직 실제 진행 시스템이 없는 임무 확인
            {
                GUI.Label(new Rect(action.x + 14f, action.y + 15f, action.width - 28f, 24f), "진행 시스템 연결 대기 · 이후 MissionData로 교체", smallStyle); // M-02·S-01 개발 단계 안내
                return; // 액션 버튼 생략
            }

            string label = string.Empty; // 현재 상태별 액션 문구
            bool clickable = false; // 실제 버튼 활성 여부

            if (entry.Status == Map30MissionStatus.Available) // 수락 가능 임무 확인
            {
                clickable = manager.CanAcceptMission(entry.MissionId); // 현재 다른 진행 임무 여부 확인
                label = clickable ? "임무 수락 및 추적" : "다른 임무 진행 중"; // 수락 가능 상태 문구
            }
            else if (entry.Status == Map30MissionStatus.Tracking) // 현재 진행 중 임무 확인
            {
                label = "현재 목표 추적 중 · Tab을 닫고 진행"; // 진행 상태 안내
            }
            else if (entry.Status == Map30MissionStatus.Completed) // 완료 임무 확인
            {
                label = "임무 완료"; // 완료 상태 표시
            }
            else // 실패 임무 처리
            {
                clickable = true; // 프로토타입 임무 재시도 허용
                label = "임무 처음부터 재시도"; // 재시도 버튼 문구
            }

            Rect button = new Rect(action.x + 8f, action.y + 8f, action.width - 16f, action.height - 16f); // 실제 버튼 영역

            if (clickable) // 클릭 가능한 상태 확인
            {
                if (GUI.Button(button, label, tabStyle)) // 수락 또는 재시도 클릭 확인
                {
                    if (entry.Status == Map30MissionStatus.Available) // 신규 수락 확인
                    {
                        manager.AcceptMission(entry.MissionId); // 실제 MissionManager에 수락 전달
                    }
                    else if (entry.Status == Map30MissionStatus.Failed) // 실패 임무 재시도 확인
                    {
                        manager.RestartMission(entry.MissionId); // 첫 목표부터 재시작
                    }
                }

                return; // 상태 라벨 중복 출력 방지
            }

            GUI.Label(button, label, tabStyle); // 클릭 불가 진행·완료 상태 문구 출력
        }

        private void DrawInfoRow(float x, ref float y, float width, string label, string value) // 임무 상세 한 줄 정보
        {
            DrawSolid(new Rect(x, y, width, 34f), panelAlt); // 정보 행 배경
            DrawBorder(new Rect(x, y, width, 34f), new Color(0.08f, 0.30f, 0.44f, 0.78f), 1f); // 정보 행 외곽선
            GUI.Label(new Rect(x + 10f, y + 7f, 86f, 20f), label, smallStyle); // 정보 이름
            GUI.Label(new Rect(x + 102f, y + 6f, width - 112f, 22f), value, bodyStyle); // 실제 정보 값
            y += 40f; // 다음 정보 행 이동
        }

        private int CountVisible(Map30MissionJournal journal) // 현재 필터 임무 수 계산
        {
            int count = 0; // 집계 초기화

            for (int i = 0; i < journal.Missions.Count; i++) // 전체 임무 순회
            {
                Map30MissionEntry entry = journal.Missions[i]; // 현재 임무 조회
                if (entry != null && MatchesFilter(entry)) // 현재 필터 일치 확인
                {
                    count++; // 표시 임무 수 증가
                }
            }

            return count; // 필터 결과 반환
        }

        private bool MatchesFilter(Map30MissionEntry entry) // 현재 상단 탭과 임무 분류 일치 여부
        {
            if (filter == MissionFilter.All) // 전체 탭 확인
            {
                return true; // 모든 임무 표시
            }

            if (filter == MissionFilter.Main) // 메인 탭 확인
            {
                return entry.Category == Map30MissionCategory.Main; // 메인 임무만 표시
            }

            return entry.Category == Map30MissionCategory.Side; // 서브 의뢰만 표시
        }

        private static string StatusText(Map30MissionStatus status) // 임무 상태 한글 표시
        {
            switch (status) // 현재 상태 분기
            {
                case Map30MissionStatus.Tracking: return "추적 중"; // 현재 추적 임무
                case Map30MissionStatus.Completed: return "완료"; // 완료 임무
                case Map30MissionStatus.Failed: return "실패"; // 실패 임무
                default: return "보유"; // 수락 가능·현재 보유
            }
        }

        private static Color StatusColor(Map30MissionStatus status) // 상태별 표시 색상
        {
            switch (status) // 현재 상태 분기
            {
                case Map30MissionStatus.Tracking: return cyan; // 추적 청록
                case Map30MissionStatus.Completed: return green; // 완료 녹색
                case Map30MissionStatus.Failed: return Map30UITheme.Danger; // 실패 적색
                default: return amber; // 보유 주황색
            }
        }

        private void EnsureStyles(float scale) // 현재 해상도용 GUI 스타일 준비
        {
            if (titleStyle != null && Mathf.Abs(styledScale - scale) < 0.01f) // 동일 배율 스타일 존재 확인
            {
                return; // 재생성 생략
            }

            styledScale = scale; // 현재 배율 저장
            titleStyle = Style(24, FontStyle.Bold, text, TextAnchor.MiddleLeft); // 최상단 제목
            tabStyle = Style(13, FontStyle.Bold, text, TextAnchor.MiddleCenter); // 필터 탭
            listTitleStyle = Style(15, FontStyle.Bold, text, TextAnchor.MiddleLeft); // 구역 제목
            listItemStyle = Style(13, FontStyle.Normal, text, TextAnchor.MiddleLeft); // 목록 기본
            listItemSelectedStyle = Style(13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft); // 목록 선택
            missionTitleStyle = Style(23, FontStyle.Bold, text, TextAnchor.MiddleLeft); // 상세 임무 제목
            bodyStyle = Style(13, FontStyle.Normal, text, TextAnchor.UpperLeft); // 상세 본문
            bodyStyle.wordWrap = true; // 긴 본문 자동 줄바꿈
            smallStyle = Style(11, FontStyle.Normal, muted, TextAnchor.MiddleLeft); // 작은 정보
            smallStyle.wordWrap = true; // 개발 안내 자동 줄바꿈
            objectiveStyle = Style(13, FontStyle.Normal, text, TextAnchor.MiddleLeft); // 목표 목록
            objectiveStyle.wordWrap = true; // 긴 목표 자동 줄바꿈
            statusStyle = Style(13, FontStyle.Bold, cyan, TextAnchor.MiddleLeft); // 상태·ID 배지
        }

        private static GUIStyle Style(int size, FontStyle fontStyle, Color color, TextAnchor anchor) // 공통 IMGUI 스타일 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 스타일 복사
            style.fontSize = size; // 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 기본 글자 색상
            style.alignment = anchor; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 불필요한 여백 제거
            return style; // 완성 스타일 반환
        }

        private static void DrawSolid(Rect rect, Color color) // 단색 사각형 출력
        {
            Color previous = GUI.color; // 기존 GUI 색상 보존
            GUI.color = color; // 요청 색 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 흰 텍스처 단색 출력
            GUI.color = previous; // 기존 색 복원
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
