using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 지도 UI 상태 복원
using ProjectK.Day30; // 공통 청록·파란색 UI 테마
using ProjectK.Day31; // MissionManager 연동
using UnityEngine; // 전체 화면 결과 UI와 입력 잠금

namespace ProjectK.Day32 // 32일차 체크포인트·재시도 이름 공간
{
    [DisallowMultipleComponent] // 결과 화면 중복 방지
    public sealed class Map32MissionResultScreen : MonoBehaviour // 임무 실패 재시도와 완료 결과를 표시하는 전체 화면
    {
        private enum ResultMode // 현재 결과 화면 종류
        {
            None, // 닫힘
            Failure, // 임무 실패
            Success // 임무 완료
        }

        private static Map32MissionResultScreen instance; // 현재 결과 화면 인스턴스
        private ResultMode mode; // 현재 화면 종류
        private string missionId = string.Empty; // 결과 임무 ID
        private string missionTitle = string.Empty; // 결과 임무 제목
        private string reason = string.Empty; // 실패 원인
        private string checkpointLabel = string.Empty; // 재시도 체크포인트 이름
        private string rewardText = string.Empty; // 완료 보상 설명
        private string nextMissionId = string.Empty; // 후속 임무 ID
        private bool rewardRecorded; // 이번 완료 보상 최초 기록 여부

        private MapWorldRoot world; // 현재 본편 월드
        private PlayerMovement movement; // 플레이어 이동
        private ThirdPersonCamera cameraController; // 카메라 입력
        private MapNavigationUI navigationUI; // 지도 UI
        private float previousTimeScale = 1f; // 결과 화면 전 시간 배율
        private bool previousMovementEnabled = true; // 결과 화면 전 이동 상태
        private bool previousCameraEnabled = true; // 결과 화면 전 카메라 상태
        private bool previousNavigationEnabled = true; // 결과 화면 전 지도 상태
        private CursorLockMode previousCursorLock; // 결과 화면 전 커서 잠금
        private bool previousCursorVisible; // 결과 화면 전 커서 표시

        private GUIStyle kickerStyle; // 상단 작은 시스템 제목
        private GUIStyle titleStyle; // 결과 제목
        private GUIStyle missionStyle; // 임무 이름
        private GUIStyle bodyStyle; // 본문
        private GUIStyle buttonStyle; // 버튼 텍스트
        private GUIStyle smallStyle; // 보조 정보

        public static Map32MissionResultScreen Instance => instance; // 현재 결과 UI 조회
        public static bool IsOpen => instance != null && instance.mode != ResultMode.None; // 일반 HUD·체크포인트 감지용 열림 상태

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 참조 초기화
        {
            instance = null; // 이전 결과 UI 참조 제거
        }

        private void Awake() // 단일 결과 UI 등록
        {
            if (instance != null && instance != this) // 기존 결과 UI 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 결과 UI 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        public static void ShowFailure(string id, string title, string failureReason, string checkpoint) // 사망·임무 실패 화면 표시
        {
            EnsureInstance(); // 결과 UI 존재 보장
            instance.missionId = id ?? string.Empty; // 임무 ID 저장
            instance.missionTitle = string.IsNullOrWhiteSpace(title) ? id : title; // 임무 제목 저장
            instance.reason = string.IsNullOrWhiteSpace(failureReason) ? "임무 실패" : failureReason; // 실패 원인 저장
            instance.checkpointLabel = string.IsNullOrWhiteSpace(checkpoint) ? "체크포인트 없음" : checkpoint; // 재시도 지점 저장
            instance.rewardText = string.Empty; // 완료 보상 정보 제거
            instance.nextMissionId = string.Empty; // 후속 임무 정보 제거
            instance.rewardRecorded = false; // 보상 기록 초기화
            instance.Open(ResultMode.Failure); // 실패 화면 열기
        }

        public static void ShowSuccess(string id, string title, string reward, string nextMission) // 임무 완료 결과 화면 표시
        {
            EnsureInstance(); // 결과 UI 존재 보장
            instance.missionId = id ?? string.Empty; // 임무 ID 저장
            instance.missionTitle = string.IsNullOrWhiteSpace(title) ? id : title; // 임무 제목 저장
            instance.reason = string.Empty; // 실패 원인 제거
            instance.checkpointLabel = string.Empty; // 실패 체크포인트 제거
            instance.rewardText = string.IsNullOrWhiteSpace(reward) ? "---" : reward; // 보상 설명 저장
            instance.nextMissionId = nextMission ?? string.Empty; // 후속 임무 ID 저장
            instance.rewardRecorded = Map32MissionRewardLedger.TryMarkGranted(instance.missionId); // 세션 중복 보상 기록 방지
            Map32MissionCheckpointSystem.Instance?.ClearCheckpoint(instance.missionId); // 정상 완료 임무 재시도 체크포인트 폐기
            instance.Open(ResultMode.Success); // 완료 결과 화면 열기
        }

        private void Open(ResultMode newMode) // 결과 화면 열기와 플레이 입력 정지
        {
            if (mode != ResultMode.None) // 다른 결과 화면이 이미 열린 상태 확인
            {
                mode = newMode; // 현재 결과 정보만 교체
                return; // 입력 상태 중복 저장 방지
            }

            ResolveReferences(); // 현재 플레이어·지도 참조 연결
            previousTimeScale = Time.timeScale; // 기존 시간 배율 저장
            previousCursorLock = Cursor.lockState; // 기존 커서 잠금 저장
            previousCursorVisible = Cursor.visible; // 기존 커서 표시 저장
            previousMovementEnabled = movement == null || movement.MovementEnabled; // 기존 이동 가능 여부 저장
            previousCameraEnabled = cameraController == null || cameraController.enabled; // 기존 카메라 활성 상태 저장
            previousNavigationEnabled = navigationUI == null || navigationUI.enabled; // 기존 지도 UI 상태 저장
            mode = newMode; // 화면 종류 적용

            movement?.SetMovementEnabled(false); // 결과 확인 중 이동 중지
            if (cameraController != null) cameraController.enabled = false; // 마우스 시점 입력 중지
            if (navigationUI != null) navigationUI.enabled = false; // 지도 입력·미니맵 중지
            world?.Player?.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 사격·재장전 중지
            Time.timeScale = 0f; // 결과 화면 동안 게임 정지
            Cursor.lockState = CursorLockMode.None; // 버튼 선택을 위한 커서 해제
            Cursor.visible = true; // 커서 표시
        }

        private void Close(bool restoreMovementState) // 결과 화면 닫기와 게임 상태 복원
        {
            if (mode == ResultMode.None) // 이미 닫힌 상태 확인
            {
                return; // 처리 생략
            }

            mode = ResultMode.None; // UI 먼저 닫기
            Time.timeScale = previousTimeScale; // 기존 시간 배율 복원

            if (movement != null) // 플레이어 이동 존재 확인
            {
                movement.SetMovementEnabled(restoreMovementState ? previousMovementEnabled : movement.MovementEnabled); // 상황에 맞는 이동 상태 유지
            }

            if (cameraController != null) cameraController.enabled = previousCameraEnabled; // 카메라 상태 복원
            if (navigationUI != null) navigationUI.enabled = previousNavigationEnabled; // 지도 UI 상태 복원
            Cursor.lockState = previousCursorLock; // 커서 잠금 복원
            Cursor.visible = previousCursorVisible; // 커서 표시 복원
        }

        private void ResolveReferences() // 현재 Map 플레이어와 카메라·지도 UI 연결
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 본편 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
            navigationUI = world != null ? world.GetComponent<MapNavigationUI>() : null; // 지도 UI 연결
            movement = world != null && world.Player != null ? world.Player.GetComponent<PlayerMovement>() : null; // 플레이어 이동 연결
            Camera camera = Camera.main; // 메인 카메라 조회
            cameraController = camera != null ? camera.GetComponent<ThirdPersonCamera>() : null; // 시점 컨트롤러 연결
        }

        private void OnGUI() // 청록·파랑 전체 화면 결과 UI
        {
            if (mode == ResultMode.None) // 닫힌 상태 확인
            {
                return; // UI 출력 생략
            }

            EnsureStyles(); // 텍스트 스타일 준비
            int previousDepth = GUI.depth; // 다른 IMGUI 깊이 보존
            GUI.depth = -2200; // 모든 일반 HUD와 메뉴 위에 표시

            try // GUI 깊이 복구 보장
            {
                Map30UITheme.DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.014f, 0.026f, 0.985f)); // 전체 화면 남청색 배경

                float width = Mathf.Clamp(Screen.width * 0.52f, 560f, 860f); // 결과 패널 너비
                float height = mode == ResultMode.Failure ? 410f : 430f; // 화면 종류별 패널 높이
                Rect panel = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height); // 중앙 패널
                Map30UITheme.DrawPanel(panel, true); // 공통 강한 패널 스타일

                Color accent = mode == ResultMode.Success ? Map30UITheme.Green : Map30UITheme.Danger; // 완료·실패 상태색 선택
                Map30UITheme.DrawSolid(new Rect(panel.x, panel.y, 5f, panel.height), accent); // 왼쪽 상태 강조선
                Map30UITheme.DrawSolid(new Rect(panel.x + 18f, panel.y + 74f, panel.width - 36f, 1f), Map30UITheme.Soft); // 제목 구분선

                GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 24f), "TERMINAL // MISSION RESULT", kickerStyle); // 시스템 제목

                Color previous = GUI.color; // 결과색 적용 전 GUI 색 보존
                GUI.color = accent; // 완료·실패 색 적용
                GUI.Label(new Rect(panel.x + 24f, panel.y + 42f, panel.width - 48f, 42f), mode == ResultMode.Success ? "MISSION COMPLETE" : "MISSION FAILED", titleStyle); // 결과 제목
                GUI.color = previous; // 기본 색 복원

                GUI.Label(new Rect(panel.x + 24f, panel.y + 95f, panel.width - 48f, 30f), "[" + missionId + "] " + missionTitle, missionStyle); // 임무 ID와 이름

                if (mode == ResultMode.Failure) // 실패 상세 표시
                {
                    DrawFailure(panel); // 실패 원인·체크포인트·재시도 버튼
                }
                else // 완료 상세 표시
                {
                    DrawSuccess(panel); // 보상·후속 임무·확인 버튼
                }
            }
            finally // 다른 HUD 깊이 복원
            {
                GUI.depth = previousDepth; // 기존 깊이 복원
            }
        }

        private void DrawFailure(Rect panel) // 실패 상세와 재시도·포기 버튼
        {
            GUI.Label(new Rect(panel.x + 24f, panel.y + 143f, 120f, 22f), "실패 원인", smallStyle); // 실패 원인 라벨
            GUI.Label(new Rect(panel.x + 150f, panel.y + 141f, panel.width - 174f, 26f), reason, bodyStyle); // 실패 원인 값

            GUI.Label(new Rect(panel.x + 24f, panel.y + 183f, 120f, 22f), "재시도 지점", smallStyle); // 체크포인트 라벨
            GUI.Label(new Rect(panel.x + 150f, panel.y + 181f, panel.width - 174f, 26f), checkpointLabel, bodyStyle); // 체크포인트 이름

            Rect notice = new Rect(panel.x + 24f, panel.y + 224f, panel.width - 48f, 58f); // 복원 범위 안내 영역
            Map30UITheme.DrawSolid(notice, new Color(0.02f, 0.06f, 0.09f, 0.90f)); // 안내 배경
            Map30UITheme.DrawBorder(notice, Map30UITheme.Soft, 1f); // 안내 외곽선
            GUI.Label(new Rect(notice.x + 12f, notice.y + 8f, notice.width - 24f, 42f), "재시도 시 목표 단계·플레이어 위치·체력·자세·임무 물품을 마지막 체크포인트 상태로 복원합니다.", smallStyle); // 복원 범위 안내

            float buttonWidth = (panel.width - 60f) * 0.5f; // 두 버튼 동일 너비
            Rect retry = new Rect(panel.x + 24f, panel.yMax - 78f, buttonWidth, 46f); // 재시도 버튼
            Rect abandon = new Rect(retry.xMax + 12f, retry.y, buttonWidth, 46f); // 임무 포기 버튼
            DrawButtonBackground(retry, Map30UITheme.Cyan); // 재시도 강조 배경
            DrawButtonBackground(abandon, Map30UITheme.Amber); // 포기 경고 배경

            if (GUI.Button(retry, "체크포인트 재시도", buttonStyle)) // 재시도 클릭 확인
            {
                Map32MissionCheckpointSystem checkpoint = Map32MissionCheckpointSystem.Instance; // 체크포인트 관리자 조회
                if (checkpoint != null && checkpoint.RetryCurrentCheckpoint()) // 상태 복원 성공 확인
                {
                    Close(false); // 복원된 PlayerHealth 이동 상태를 덮어쓰지 않고 결과 화면 닫기
                }
            }

            if (GUI.Button(abandon, "임무 포기", buttonStyle)) // 임무 포기 클릭 확인
            {
                Map32MissionCheckpointSystem checkpoint = Map32MissionCheckpointSystem.Instance; // 체크포인트 관리자 조회
                if (checkpoint != null && checkpoint.AbandonCurrentMission()) // 안전 위치 복귀와 임무 초기화 확인
                {
                    Close(false); // 복원된 이동 상태 유지
                }
            }
        }

        private void DrawSuccess(Rect panel) // 완료 보상과 후속 임무 표시
        {
            GUI.Label(new Rect(panel.x + 24f, panel.y + 143f, 120f, 22f), "보상", smallStyle); // 보상 라벨
            GUI.Label(new Rect(panel.x + 150f, panel.y + 141f, panel.width - 174f, 46f), rewardText, bodyStyle); // 보상 설명

            GUI.Label(new Rect(panel.x + 24f, panel.y + 201f, 120f, 22f), "후속 임무", smallStyle); // 후속 임무 라벨
            GUI.Label(new Rect(panel.x + 150f, panel.y + 199f, panel.width - 174f, 26f), string.IsNullOrEmpty(nextMissionId) ? "---" : nextMissionId + " 해금 준비", bodyStyle); // 후속 임무 정보

            GUI.Label(new Rect(panel.x + 24f, panel.y + 241f, 120f, 22f), "보상 상태", smallStyle); // 보상 기록 라벨
            GUI.Label(new Rect(panel.x + 150f, panel.y + 239f, panel.width - 174f, 26f), rewardRecorded ? "최초 지급 기록 완료" : "이미 지급된 임무 · 중복 지급 없음", bodyStyle); // 중복 방지 상태

            Rect notice = new Rect(panel.x + 24f, panel.y + 282f, panel.width - 48f, 50f); // 완료 안내 영역
            Map30UITheme.DrawSolid(notice, new Color(0.02f, 0.08f, 0.085f, 0.90f)); // 완료 안내 배경
            Map30UITheme.DrawBorder(notice, new Color(Map30UITheme.Green.r, Map30UITheme.Green.g, Map30UITheme.Green.b, 0.60f), 1f); // 완료 안내 외곽선
            GUI.Label(new Rect(notice.x + 12f, notice.y + 8f, notice.width - 24f, 34f), "임무 진행 체크포인트를 종료하고 완료 상태를 유지합니다.", smallStyle); // 완료 처리 안내

            Rect confirm = new Rect(panel.x + 24f, panel.yMax - 72f, panel.width - 48f, 42f); // 확인 버튼
            DrawButtonBackground(confirm, Map30UITheme.Green); // 완료 버튼 녹색 강조

            if (GUI.Button(confirm, "확인 · 도시로 복귀", buttonStyle)) // 결과 확인 클릭
            {
                Close(true); // 임무 완료 전 이동 가능 상태 복원
            }
        }

        private static void DrawButtonBackground(Rect rect, Color accent) // 공통 결과 화면 버튼 배경
        {
            Map30UITheme.DrawSolid(rect, new Color(0.015f, 0.055f, 0.080f, 0.96f)); // 버튼 바탕
            Map30UITheme.DrawBorder(rect, accent, 1f); // 상태색 외곽선
        }

        private void EnsureStyles() // 결과 화면 텍스트 스타일 준비
        {
            if (titleStyle != null) // 기존 스타일 확인
            {
                return; // 재생성 생략
            }

            kickerStyle = Style(11, FontStyle.Bold, Map30UITheme.Muted, TextAnchor.MiddleLeft); // 시스템 제목
            titleStyle = Style(28, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 결과 제목
            missionStyle = Style(18, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleLeft); // 임무 제목
            bodyStyle = Style(14, FontStyle.Normal, Map30UITheme.Text, TextAnchor.UpperLeft); // 본문
            bodyStyle.wordWrap = true; // 긴 보상·설명 줄바꿈
            buttonStyle = Style(14, FontStyle.Bold, Map30UITheme.Text, TextAnchor.MiddleCenter); // 버튼
            smallStyle = Style(12, FontStyle.Normal, Map30UITheme.Muted, TextAnchor.UpperLeft); // 보조 정보
            smallStyle.wordWrap = true; // 안내 줄바꿈
        }

        private static GUIStyle Style(int size, FontStyle fontStyle, Color color, TextAnchor alignment) // 공통 결과 화면 스타일 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 복사
            style.fontSize = size; // 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 글자 색상 적용
            style.alignment = alignment; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 기본 여백 제거
            return style; // 완성 스타일 반환
        }

        private static void EnsureInstance() // 외부 호출 시 결과 UI 자동 생성
        {
            if (instance != null) // 기존 결과 UI 확인
            {
                return; // 생성 생략
            }

            GameObject owner = new GameObject("[Day32] Mission Result Screen"); // 결과 UI 오브젝트 생성
            instance = owner.AddComponent<Map32MissionResultScreen>(); // 결과 화면 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
