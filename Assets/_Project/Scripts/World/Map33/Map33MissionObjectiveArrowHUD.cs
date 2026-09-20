using ProjectK.Day29; // 현재 임무 목표 Provider
using ProjectK.Day30; // 공통 청록·파랑 UI 테마와 메뉴 숨김 상태
using ProjectK.Day31; // 목표 없음 안내 Toast
using UnityEngine; // 화면 좌표·IMGUI 처리
using UnityEngine.InputSystem; // G 키 입력 처리

namespace ProjectK.Day33 // 33일차 추가 임무 안내 기능 이름 공간
{
    [DisallowMultipleComponent] // 임무 안내 HUD 중복 방지
    public sealed class Map33MissionObjectiveArrowHUD : MonoBehaviour // G 키로 일정 시간 현재 임무 목표 방향과 남은 거리만 표시
    {
        private static readonly Color panelColor = new Color(0.015f, 0.055f, 0.080f, 0.92f); // 최소 화살표 패널 배경
        private static readonly Color borderColor = new Color(0.10f, 0.88f, 0.98f, 0.92f); // 청록 외곽선
        private static readonly Color textMain = new Color(0.86f, 0.97f, 1f, 1f); // 주요 텍스트 색상
        private static readonly Color textMuted = new Color(0.48f, 0.76f, 0.82f, 1f); // 보조 텍스트 색상

        private static Map33MissionObjectiveArrowHUD instance; // 현재 목표 화살표 HUD
        [SerializeField] private float visibleDuration = 4.0f; // G 입력 후 표시 시간
        [SerializeField] private float screenMargin = 72f; // 화면 밖 목표 화살표 가장자리 여백
        [SerializeField] private Vector3 targetWorldOffset = new Vector3(0f, 1.8f, 0f); // 화면 안 목표 화살표 높이 보정

        private Camera targetCamera; // 목표 화면 좌표 계산 카메라
        private float visibleUntil; // 화살표 표시 종료 시각
        private GUIStyle arrowStyle; // 큰 방향 화살표 스타일
        private GUIStyle distanceStyle; // 거리 표시 스타일
        private GUIStyle smallDistanceStyle; // 화면 안 거리 표시 스타일

        public static Map33MissionObjectiveArrowHUD Instance => instance; // 현재 HUD 조회
        public bool IsVisible => Time.unscaledTime < visibleUntil; // 현재 표시 상태 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 참조 초기화
        {
            instance = null; // 이전 HUD 참조 제거
        }

        private void Awake() // 단일 HUD 등록
        {
            if (instance != null && instance != this) // 기존 HUD 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 HUD 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 카메라 참조 준비
        {
            targetCamera = Camera.main; // 현재 메인 카메라 연결
        }

        private void Update() // G 키 목표 안내 입력 처리
        {
            if (Map30UITheme.HideGameplayHUD) // Tab 임무 창·결과 화면 상태 확인
            {
                return; // 메뉴 중 목표 화살표 입력 차단
            }

            Keyboard keyboard = Keyboard.current; // 현재 키보드 조회
            if (keyboard == null || !keyboard.gKey.wasPressedThisFrame) // G 신규 입력 확인
            {
                return; // 입력 없으면 처리 생략
            }

            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 임무 목표 Provider 조회
            if (provider == null) // Provider 생성 여부 확인
            {
                Map31MissionToastHUD.Show("MISSION GUIDE", "현재 추적 중인 목표가 없습니다.", 1.4f, false, true); // 목표 없음 안내
                return; // 표시 시작 중단
            }

            Map29TerminalObjectiveSnapshot snapshot = provider.Snapshot(); // 현재 목표 복사본 조회
            if (!snapshot.HasTarget || snapshot.Status != Map29TerminalObjectiveStatus.Tracking) // 실제 추적 가능 목표 확인
            {
                Map31MissionToastHUD.Show("MISSION GUIDE", "현재 추적 중인 목표가 없습니다.", 1.4f, false, true); // 목표 없음 안내
                return; // 표시 시작 중단
            }

            visibleUntil = Time.unscaledTime + visibleDuration; // 목표 안내 표시 시간 갱신
        }

        private void OnGUI() // 현재 목표의 최소 방향 화살표 렌더링
        {
            if (!IsVisible || Map30UITheme.HideGameplayHUD) // 표시 시간과 메뉴 상태 확인
            {
                return; // 화살표 출력 생략
            }

            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 Provider 조회
            if (provider == null) // Provider 유효성 확인
            {
                return; // 출력 생략
            }

            Map29TerminalObjectiveSnapshot snapshot = provider.Snapshot(); // 현재 목표 정보 조회
            if (!snapshot.HasTarget || snapshot.Status != Map29TerminalObjectiveStatus.Tracking) // 목표가 완료·실패·해제됐는지 확인
            {
                visibleUntil = 0f; // 표시 즉시 종료
                return; // 출력 생략
            }

            targetCamera = targetCamera != null && targetCamera.isActiveAndEnabled ? targetCamera : Camera.main; // 활성 카메라 참조 보정
            if (targetCamera == null) // 카메라 존재 확인
            {
                return; // 좌표 계산 불가
            }

            EnsureStyles(); // GUI 스타일 준비

            int previousDepth = GUI.depth; // 다른 IMGUI 깊이 보존
            Matrix4x4 previousMatrix = GUI.matrix; // 다른 UI 화면 변환 보존
            Color previousColor = GUI.color; // 다른 UI 색상 보존
            GUI.depth = -1750; // 일반 HUD보다 위, 결과 화면보다 아래 표시

            try // GUI 상태 복구 보장
            {
                DrawObjectiveArrow(snapshot); // 현재 목표 화살표 출력
            }
            finally // 다른 HUD 상태 복원
            {
                GUI.depth = previousDepth; // 기존 깊이 복원
                GUI.matrix = previousMatrix; // 기존 화면 변환 복원
                GUI.color = previousColor; // 기존 색상 복원
            }
        }

        private void DrawObjectiveArrow(Map29TerminalObjectiveSnapshot snapshot) // 목표 화면 좌표와 방향 계산
        {
            Vector3 worldPoint = snapshot.TargetPosition + targetWorldOffset; // 목표 위쪽 표시 위치 계산
            Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPoint); // Unity 화면 좌표 계산
            bool behind = screenPoint.z <= 0f; // 카메라 뒤쪽 목표 여부 확인

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); // 화면 중심 계산
            Vector2 projected = new Vector2(screenPoint.x, screenPoint.y); // 화면 좌표 2D 변환
            Vector2 fromCenter = projected - center; // 중심에서 목표 화면 방향 계산

            if (behind) // 카메라 뒤쪽 목표는 방향 반전
            {
                fromCenter = -fromCenter; // 실제 방향으로 반전
            }

            bool inside = !behind &&
                          screenPoint.x >= screenMargin &&
                          screenPoint.x <= Screen.width - screenMargin &&
                          screenPoint.y >= screenMargin &&
                          screenPoint.y <= Screen.height - screenMargin; // 목표가 화면 안전 영역 안인지 확인

            float distance = Vector3.Distance(targetCamera.transform.position, snapshot.TargetPosition); // 카메라 기준 목표 거리 계산
            string distanceLabel = FormatDistance(distance); // 거리 단위 변환

            if (inside) // 화면 안 목표 표시
            {
                Vector2 guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y); // IMGUI 좌표로 Y축 변환
                DrawOnScreenMarker(guiPoint, distanceLabel); // 최소 화면 안 화살표 출력
                return; // 화면 밖 처리 생략
            }

            if (fromCenter.sqrMagnitude <= 0.001f) // 화면 중심과 방향이 겹치는 특수 상태 확인
            {
                fromCenter = Vector2.up; // 기본 위쪽 방향 사용
            }

            fromCenter.Normalize(); // 화면 가장자리 계산용 방향 정규화
            Vector2 edgePoint = ClampDirectionToScreenEdge(center, fromCenter, screenMargin); // 화면 가장자리 교차 지점 계산
            string arrow = ArrowForDirection(fromCenter); // 8방향 화살표 문자 선택
            Vector2 guiEdge = new Vector2(edgePoint.x, Screen.height - edgePoint.y); // IMGUI 좌표로 변환
            DrawOffScreenMarker(guiEdge, arrow, distanceLabel); // 화면 밖 목표 최소 표시
        }

        private void DrawOnScreenMarker(Vector2 point, string distanceLabel) // 목표가 화면 안에 있을 때 화살표와 거리만 표시
        {
            Rect bounds = new Rect(point.x - 42f, point.y - 52f, 84f, 50f); // 화살표·거리 전체 영역
            bounds = AvoidHudZones(bounds); // 고정 HUD와 겹칠 경우 안전 영역으로 이동
            Rect arrowRect = new Rect(bounds.x + 20f, bounds.y, 44f, 32f); // 목표 방향 화살표 위치
            Rect textRect = new Rect(bounds.x, bounds.y + 32f, bounds.width, 18f); // 거리 표시 위치

            Color previous = GUI.color; // 화살표 색상 변경 전 보존
            GUI.color = borderColor; // 청록 화살표 적용
            GUI.Label(arrowRect, "▼", arrowStyle); // 목표 위치 방향 아래 화살표
            GUI.color = previous; // 기본 색상 복원
            GUI.Label(textRect, distanceLabel, smallDistanceStyle); // 거리만 표시
        }

        private void DrawOffScreenMarker(Vector2 point, string arrow, string distanceLabel) // 화면 밖 목표를 최소 패널로 표시
        {
            const float panelWidth = 86f; // 최소 패널 너비
            const float panelHeight = 54f; // 최소 패널 높이
            Rect panel = new Rect(point.x - panelWidth * 0.5f, point.y - panelHeight * 0.5f, panelWidth, panelHeight); // 가장자리 목표 패널

            panel.x = Mathf.Clamp(panel.x, 8f, Screen.width - panel.width - 8f); // 좌우 화면 안으로 보정
            panel.y = Mathf.Clamp(panel.y, 8f, Screen.height - panel.height - 8f); // 상하 화면 안으로 보정
            panel = AvoidHudZones(panel); // 좌측 HUD·미니맵·장비 HUD와 겹치지 않도록 이동

            DrawSolid(panel, panelColor); // 최소 패널 배경
            DrawBorder(panel, borderColor, 1f); // 청록 외곽선
            GUI.Label(new Rect(panel.x, panel.y + 2f, panel.width, 28f), arrow, arrowStyle); // 방향 화살표 표시
            GUI.Label(new Rect(panel.x + 4f, panel.y + 31f, panel.width - 8f, 16f), distanceLabel, distanceStyle); // 거리만 표시
        }

        private static Rect AvoidHudZones(Rect panel) // 화면 고정 HUD 영역을 피하도록 화살표 패널 보정
        {
            Rect leftHud = new Rect(0f, 0f, 210f, Mathf.Min(Screen.height, 360f)); // HP·QA·임무 단말기 좌측 열 예약 영역
            Rect topRightHud = new Rect(Mathf.Max(0f, Screen.width - 220f), 0f, 220f, 205f); // 미니맵 예약 영역
            Rect bottomRightHud = new Rect(Mathf.Max(0f, Screen.width - 285f), Mathf.Max(0f, Screen.height - 235f), 285f, 235f); // 장비 HUD 예약 영역

            if (panel.Overlaps(leftHud)) // 좌측 HUD와 겹침 확인
            {
                panel.x = leftHud.xMax + 8f; // 좌측 열 오른쪽으로 이동
            }

            if (panel.Overlaps(topRightHud)) // 우측 상단 미니맵과 겹침 확인
            {
                panel.y = topRightHud.yMax + 8f; // 미니맵 아래로 이동
            }

            if (panel.Overlaps(bottomRightHud)) // 우측 하단 장비 HUD와 겹침 확인
            {
                panel.y = bottomRightHud.yMin - panel.height - 8f; // 장비 HUD 위로 이동
            }

            panel.x = Mathf.Clamp(panel.x, 8f, Mathf.Max(8f, Screen.width - panel.width - 8f)); // 최종 좌우 화면 안 보정
            panel.y = Mathf.Clamp(panel.y, 8f, Mathf.Max(8f, Screen.height - panel.height - 8f)); // 최종 상하 화면 안 보정
            return panel; // 겹침을 피한 최종 패널 반환
        }

        private static string FormatDistance(float distance) // 목표 거리 표시 형식
        {
            if (distance >= 1000f) // 1km 이상 확인
            {
                return (distance / 1000f).ToString("0.0") + " km"; // km 단위 표시
            }

            return Mathf.RoundToInt(distance) + " m"; // 미터 정수 표시
        }

        private static Vector2 ClampDirectionToScreenEdge(Vector2 center, Vector2 direction, float margin) // 중심에서 방향 벡터가 화면 가장자리와 만나는 지점 계산
        {
            float halfWidth = Mathf.Max(1f, Screen.width * 0.5f - margin); // 사용 가능한 화면 반너비
            float halfHeight = Mathf.Max(1f, Screen.height * 0.5f - margin); // 사용 가능한 화면 반높이
            float scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity; // 좌우 가장자리 교차 배율
            float scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity; // 상하 가장자리 교차 배율
            float scale = Mathf.Min(scaleX, scaleY); // 먼저 만나는 가장자리 배율 선택
            return center + direction * scale; // 실제 화면 가장자리 위치 반환
        }

        private static string ArrowForDirection(Vector2 direction) // 화면 방향을 8방향 화살표 문자로 변환
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; // 오른쪽 0도 기준 방향 각도 계산

            if (angle >= -22.5f && angle < 22.5f) return "▶"; // 오른쪽
            if (angle >= 22.5f && angle < 67.5f) return "↗"; // 오른쪽 위
            if (angle >= 67.5f && angle < 112.5f) return "▲"; // 위
            if (angle >= 112.5f && angle < 157.5f) return "↖"; // 왼쪽 위
            if (angle >= 157.5f || angle < -157.5f) return "◀"; // 왼쪽
            if (angle >= -157.5f && angle < -112.5f) return "↙"; // 왼쪽 아래
            if (angle >= -112.5f && angle < -67.5f) return "▼"; // 아래
            return "↘"; // 오른쪽 아래
        }

        private void EnsureStyles() // 목표 화살표 텍스트 스타일 준비
        {
            if (arrowStyle != null) // 기존 스타일 확인
            {
                return; // 재생성 생략
            }

            arrowStyle = new GUIStyle(GUI.skin.label); // 큰 화살표 스타일 생성
            arrowStyle.fontSize = 27; // 화살표 크기 적용
            arrowStyle.fontStyle = FontStyle.Bold; // 화살표 강조
            arrowStyle.alignment = TextAnchor.MiddleCenter; // 중앙 정렬
            arrowStyle.normal.textColor = borderColor; // 청록 화살표 색상

            distanceStyle = new GUIStyle(GUI.skin.label); // 화면 밖 거리 스타일 생성
            distanceStyle.fontSize = 12; // 거리 글자 크기
            distanceStyle.fontStyle = FontStyle.Bold; // 거리 강조
            distanceStyle.alignment = TextAnchor.MiddleCenter; // 중앙 정렬
            distanceStyle.normal.textColor = textMain; // 주요 글자 색상

            smallDistanceStyle = new GUIStyle(GUI.skin.label); // 화면 안 거리 스타일 생성
            smallDistanceStyle.fontSize = 11; // 작은 거리 글자 크기
            smallDistanceStyle.fontStyle = FontStyle.Bold; // 거리 강조
            smallDistanceStyle.alignment = TextAnchor.MiddleCenter; // 중앙 정렬
            smallDistanceStyle.normal.textColor = textMuted; // 보조 글자 색상
        }

        private static void DrawSolid(Rect rect, Color color) // 단색 사각형 출력
        {
            Color previous = GUI.color; // 기존 GUI 색상 보존
            GUI.color = color; // 요청 색상 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 흰 텍스처를 단색으로 출력
            GUI.color = previous; // 기존 색상 복원
        }

        private static void DrawBorder(Rect rect, Color color, float thickness) // 사각 프레임 출력
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color); // 상단 선
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // 하단 선
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color); // 좌측 선
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // 우측 선
        }
    }
}
