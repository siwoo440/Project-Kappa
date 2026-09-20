using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 전체 지도 열린 상태 참조
using ProjectK.Day24; // 지상·지하 층 판정 참조
using UnityEngine; // IMGUI 기반 단말기 HUD 렌더링

namespace ProjectK.Day29 // 29일차 플레이어 단말기 이름 공간
{
    [DisallowMultipleComponent] // 단말기 HUD 중복 방지
    public sealed class Map29TerminalHUD : MonoBehaviour // 왼쪽 상단 사이버펑크 단말기 형태의 현재 목표·거리 HUD
    {
        private static readonly Color panelColor = new Color(0.018f, 0.055f, 0.075f, 0.92f); // 단말기 짙은 청색 배경
        private static readonly Color panelInnerColor = new Color(0.025f, 0.090f, 0.115f, 0.55f); // 내부 보조 패널 배경
        private static readonly Color cyan = new Color(0.08f, 0.88f, 0.98f, 1f); // 주 청록 네온
        private static readonly Color cyanSoft = new Color(0.10f, 0.58f, 0.70f, 0.72f); // 보조 청록선
        private static readonly Color blueSoft = new Color(0.14f, 0.32f, 0.48f, 0.80f); // 내부 데이터 선
        private static readonly Color textMain = new Color(0.84f, 0.96f, 1f, 1f); // 주요 텍스트
        private static readonly Color textMuted = new Color(0.45f, 0.72f, 0.78f, 1f); // 보조 텍스트
        private static readonly Color green = new Color(0.28f, 1f, 0.67f, 1f); // 완료 상태
        private static readonly Color amber = new Color(1f, 0.68f, 0.18f, 1f); // 일시 정지·시험 상태
        private static readonly Color red = new Color(1f, 0.30f, 0.34f, 1f); // 실패 상태

        private MapWorldRoot world; // 현재 본편 월드
        private MapNavigationUI navigationUI; // 전체 지도 열림 상태 참조
        private PlayerHealth playerHealth; // 사망 상태 참조
        private GUIStyle headerStyle; // 상단 시스템 제목 스타일
        private GUIStyle missionStyle; // 미션 제목 스타일
        private GUIStyle objectiveStyle; // 목표 문구 스타일
        private GUIStyle dataLabelStyle; // 거리·층 라벨 스타일
        private GUIStyle dataValueStyle; // 거리·층 값 스타일
        private GUIStyle smallStyle; // 작은 시스템 텍스트 스타일
        private GUIStyle badgeStyle; // 미션 ID 배지 스타일
        private float styledScale = -1f; // 현재 스타일 해상도 배율
        private float nextResolveTime; // 다음 월드 참조 복구 시각

        private void Start() // 첫 월드와 기존 지도 UI 연결
        {
            ResolveReferences(); // HUD에 필요한 현재 씬 참조 검색
        }

        private void Update() // 씬 생성 순서와 전환에 따른 참조 복구
        {
            if (world != null && world.Player != null) // 정상 참조 유지 확인
            {
                return; // 추가 검색 생략
            }

            if (Time.unscaledTime < nextResolveTime) // 재검색 간격 확인
            {
                return; // 다음 검색 시각까지 대기
            }

            nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 예약
            ResolveReferences(); // 현재 씬 참조 복구
        }

        private void ResolveReferences() // Provider와 Map 씬을 이용해 플레이어·지도 UI 참조 연결
        {
            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 목표 Provider 조회
            world = provider != null ? provider.World : null; // Provider가 알고 있는 본편 월드 우선 사용

            if (world == null) // Provider 월드가 아직 준비되지 않았는지 확인
            {
                MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 MapWorldRoot 대체 검색
                world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
            }

            navigationUI = world != null ? world.GetComponent<MapNavigationUI>() : null; // 같은 월드 루트의 지도 UI 연결
            playerHealth = world != null && world.Player != null ? world.Player.GetComponent<PlayerHealth>() : null; // 플레이어 생존 상태 연결
        }

        private void OnGUI() // 참고 이미지의 얇은 네온 프레임을 IMGUI로 구성
        {
            Map29TerminalObjectiveProvider provider = Map29TerminalObjectiveProvider.Instance; // 현재 목표 Provider 조회
            if (provider == null) // Provider 준비 확인
            {
                return; // 초기 생성 프레임 표시 생략
            }

            if (navigationUI != null && navigationUI.FullMapOpen) // M 전체 지도 화면 확인
            {
                return; // 전체 지도 위에는 작은 단말기 HUD 숨김
            }

            if (playerHealth != null && playerHealth.IsDead) // 플레이어 사망 상태 확인
            {
                return; // 사망 중 HUD 숨김
            }

            Map29TerminalObjectiveSnapshot snapshot = provider.Snapshot(); // 현재 목표 불변 복사본 조회
            MapWorldRoot currentWorld = provider.World != null ? provider.World : world; // 최신 본편 월드 선택
            Transform player = currentWorld != null && currentWorld.Player != null ? currentWorld.Player.transform : null; // 플레이어 Transform 조회

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.62f, 1.45f); // 1280x720 기준 해상도 배율 계산
            EnsureStyles(scale); // 현재 배율용 GUI 스타일 준비

            Matrix4x4 previousMatrix = GUI.matrix; // 다른 IMGUI 화면 변환 보존
            Color previousColor = GUI.color; // 다른 IMGUI 색상 보존
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale); // 단말기 UI에만 해상도 스케일 적용

            float designWidth = Screen.width / scale; // 스케일 보정된 가상 화면 너비
            float panelX = designWidth >= 980f ? 204f : 14f; // 기존 HP·QA HUD와 겹치지 않는 왼쪽 상단 위치
            const float panelY = 16f; // 상단 여백
            const float panelWidth = 356f; // 단말기 패널 너비
            const float panelHeight = 236f; // 단말기 패널 높이
            Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight); // 전체 단말기 영역

            try // GUI 상태 복구 보장
            {
                DrawTerminalFrame(panel, snapshot); // 배경·네온 프레임·장식 라인 출력
                DrawTerminalContent(panel, snapshot, player); // 미션·목표·거리·층·방향 정보 출력
            }
            finally // 다른 기존 HUD에 영향 방지
            {
                GUI.matrix = previousMatrix; // 기존 화면 변환 복원
                GUI.color = previousColor; // 기존 색상 복원
            }
        }

        private void DrawTerminalFrame(Rect panel, Map29TerminalObjectiveSnapshot snapshot) // 참고 이미지 스타일의 프레임과 데이터 장식 출력
        {
            DrawSolid(panel, panelColor); // 기본 반투명 짙은 배경
            DrawSolid(new Rect(panel.x + 7f, panel.y + 38f, panel.width - 14f, panel.height - 47f), panelInnerColor); // 내부 데이터 패널
            DrawBorder(panel, cyan, 2f); // 주 청록 외곽선
            DrawBorder(new Rect(panel.x + 5f, panel.y + 5f, panel.width - 10f, panel.height - 10f), cyanSoft, 1f); // 이중 얇은 프레임

            DrawSolid(new Rect(panel.x + 1f, panel.y + 1f, 76f, 3f), cyan); // 좌측 상단 강조선
            DrawSolid(new Rect(panel.x + panel.width - 108f, panel.y + 1f, 107f, 3f), cyan); // 우측 상단 강조선
            DrawSolid(new Rect(panel.x + 1f, panel.y + panel.height - 4f, 54f, 3f), cyan); // 좌측 하단 강조선
            DrawSolid(new Rect(panel.x + panel.width - 82f, panel.y + panel.height - 4f, 81f, 3f), cyan); // 우측 하단 강조선

            DrawLine(new Vector2(panel.x, panel.y + 18f), new Vector2(panel.x + 18f, panel.y), 2f, cyan); // 좌측 상단 절단 모서리
            DrawLine(new Vector2(panel.x + panel.width - 18f, panel.y), new Vector2(panel.x + panel.width, panel.y + 18f), 2f, cyan); // 우측 상단 절단 모서리
            DrawLine(new Vector2(panel.x, panel.y + panel.height - 16f), new Vector2(panel.x + 16f, panel.y + panel.height), 2f, cyanSoft); // 좌측 하단 절단 모서리
            DrawLine(new Vector2(panel.x + panel.width - 16f, panel.y + panel.height), new Vector2(panel.x + panel.width, panel.y + panel.height - 16f), 2f, cyanSoft); // 우측 하단 절단 모서리

            DrawSolid(new Rect(panel.x + 9f, panel.y + 34f, panel.width - 18f, 1f), blueSoft); // 헤더 구분선
            DrawSolid(new Rect(panel.x + 9f, panel.y + 111f, panel.width - 18f, 1f), blueSoft); // 목표와 데이터 구분선
            DrawSolid(new Rect(panel.x + 9f, panel.y + 184f, panel.width - 18f, 1f), blueSoft); // 상태 하단 구분선

            for (int i = 0; i < 7; i++) // 참고 이미지의 짧은 진행 화살표 장식
            {
                float x = panel.x + 207f + i * 12f; // 각 장식 X 위치
                DrawLine(new Vector2(x, panel.y + 21f), new Vector2(x + 5f, panel.y + 25f), 1f, cyanSoft); // 상단 사선
                DrawLine(new Vector2(x + 5f, panel.y + 25f), new Vector2(x, panel.y + 29f), 1f, cyanSoft); // 하단 사선
            }

            float scanRange = panel.height - 60f; // 내부 스캔선 이동 범위
            float scanOffset = Mathf.Repeat(Time.unscaledTime * 24f, scanRange); // 비스케일 시간 기반 스캔 위치
            DrawSolid(new Rect(panel.x + 10f, panel.y + 42f + scanOffset, panel.width - 20f, 1f), new Color(cyan.r, cyan.g, cyan.b, 0.10f)); // 느린 청록 스캔 라인

            Color statusColor = StatusColor(snapshot.Status, snapshot.IsDemo); // 현재 상태 색상 조회
            float pulse = 0.55f + Mathf.PingPong(Time.unscaledTime * 0.85f, 0.45f); // 통신 연결 점멸값
            DrawSolid(new Rect(panel.x + panel.width - 20f, panel.y + 13f, 7f, 7f), new Color(statusColor.r, statusColor.g, statusColor.b, pulse)); // 우측 상단 상태 LED
        }

        private void DrawTerminalContent(Rect panel, Map29TerminalObjectiveSnapshot snapshot, Transform player) // 실제 목표 정보 텍스트와 데이터 블록 출력
        {
            GUI.Label(new Rect(panel.x + 15f, panel.y + 8f, 178f, 25f), "TERMINAL // YEONMU LINK", headerStyle); // 단말기 시스템 제목

            string badge = string.IsNullOrEmpty(snapshot.MissionId) ? "NO LINK" : snapshot.MissionId; // 미션 ID 또는 대기 표시
            Rect badgeRect = new Rect(panel.x + 15f, panel.y + 46f, 82f, 21f); // 미션 ID 배지 영역
            DrawSolid(badgeRect, new Color(0.04f, 0.22f, 0.28f, 0.90f)); // 미션 ID 배지 배경
            DrawBorder(badgeRect, cyanSoft, 1f); // 미션 ID 배지 외곽선
            GUI.Label(badgeRect, badge, badgeStyle); // 왼쪽 ID 배지

            string stateText = StatusText(snapshot.Status, snapshot.IsDemo); // 상태 표시 문구 생성
            Color oldColor = GUI.color; // 상태 텍스트 색상 변경 전 보존
            GUI.color = StatusColor(snapshot.Status, snapshot.IsDemo); // 상태 색상 적용
            GUI.Label(new Rect(panel.x + 104f, panel.y + 46f, panel.width - 119f, 21f), stateText, dataLabelStyle); // 우측 상태 라벨
            GUI.color = oldColor; // 일반 텍스트 색상 복원

            GUI.Label(new Rect(panel.x + 15f, panel.y + 70f, panel.width - 30f, 28f), snapshot.MissionTitle, missionStyle); // 현재 미션 이름
            GUI.Label(new Rect(panel.x + 15f, panel.y + 94f, panel.width - 30f, 42f), snapshot.ObjectiveText, objectiveStyle); // 현재 목표 한 줄 설명

            float distance = 0f; // 현재 목표 거리 초기화
            string distanceText = "---"; // 목표 없을 때 거리 표시
            string layerText = "---"; // 목표 없을 때 층 표시
            string directionText = "---"; // 목표 없을 때 방향 표시

            if (snapshot.HasTarget && player != null) // 거리와 방향 계산 가능한 목표 확인
            {
                distance = Vector3.Distance(player.position, snapshot.TargetPosition); // 실제 3차원 거리 계산
                distanceText = FormatDistance(distance); // m 또는 km 표시 변환
                layerText = LayerText(player.position, snapshot.TargetPosition); // 지상·지하·상하층 표시
                directionText = DirectionText(player, snapshot.TargetPosition); // 플레이어 기준 목표 방향 표시
            }

            Rect dataArea = new Rect(panel.x + 15f, panel.y + 126f, panel.width - 30f, 48f); // 세 개 데이터 블록 영역
            float column = (dataArea.width - 12f) / 3f; // 데이터 블록 한 칸 너비

            DrawDataBlock(new Rect(dataArea.x, dataArea.y, column, dataArea.height), "DIST", distanceText); // 거리 데이터 출력
            DrawDataBlock(new Rect(dataArea.x + column + 6f, dataArea.y, column, dataArea.height), "LAYER", layerText); // 층 데이터 출력
            DrawDataBlock(new Rect(dataArea.x + (column + 6f) * 2f, dataArea.y, column, dataArea.height), "VECTOR", directionText); // 방향 데이터 출력

            string targetText = snapshot.HasTarget ? "TARGET // " + snapshot.TargetLabel : "TARGET // ---"; // 목표 라벨 생성
            GUI.Label(new Rect(panel.x + 15f, panel.y + 192f, panel.width - 30f, 20f), targetText, smallStyle); // 현재 목표 장소 표시

            string footer = snapshot.IsDemo ? "LOCAL NAV TEST / MISSION LINK READY" : snapshot.Status == Map29TerminalObjectiveStatus.Idle ? "NETWORK STANDBY" : "OBJECTIVE DATA SYNC"; // 하단 시스템 상태 문구
            GUI.Label(new Rect(panel.x + 15f, panel.y + 211f, panel.width - 30f, 18f), footer, smallStyle); // 하단 상태 표시
        }

        private void DrawDataBlock(Rect rect, string label, string value) // 거리·층·방향 공통 데이터 박스
        {
            DrawSolid(rect, new Color(0.02f, 0.13f, 0.17f, 0.72f)); // 데이터 박스 배경
            DrawBorder(rect, blueSoft, 1f); // 데이터 박스 외곽선
            DrawSolid(new Rect(rect.x, rect.y, 3f, rect.height), cyanSoft); // 왼쪽 청록 인디케이터
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 12f, 16f), label, dataLabelStyle); // 작은 데이터 라벨
            GUI.Label(new Rect(rect.x + 8f, rect.y + 20f, rect.width - 12f, 23f), value, dataValueStyle); // 실제 데이터 값
        }

        private static string FormatDistance(float distance) // 목표 거리 표시 형식
        {
            if (distance >= 1000f) // 1km 이상 확인
            {
                return (distance / 1000f).ToString("0.0") + " km"; // km 단위 표시
            }

            return Mathf.RoundToInt(distance) + " m"; // 미터 정수 표시
        }

        private static string LayerText(Vector3 playerPosition, Vector3 targetPosition) // Day24 층 판정과 높이 차이 기반 목표 층 표시
        {
            Map24LayerNavigation layerNavigation = Map24LayerNavigation.Instance; // 현재 지상·지하 관리자 조회

            if (layerNavigation != null) // Day24 시스템 준비 확인
            {
                Map24WorldLayer playerLayer = layerNavigation.GetLayer(playerPosition); // 플레이어 층 조회
                Map24WorldLayer targetLayer = layerNavigation.GetLayer(targetPosition); // 목표 층 조회

                if (playerLayer != targetLayer) // 지상·지하 층 차이 확인
                {
                    return targetLayer == Map24WorldLayer.Underground ? "지하 ↓" : "지상 ↑"; // 목표 층 방향 표시
                }
            }

            float vertical = targetPosition.y - playerPosition.y; // 같은 지상·지하 안의 높이 차이 계산

            if (vertical > 6f) // 위쪽 목표 확인
            {
                return "상층 ↑"; // 높은 위치 표시
            }

            if (vertical < -6f) // 아래쪽 목표 확인
            {
                return "하층 ↓"; // 낮은 위치 표시
            }

            return "동일 층"; // 큰 높이 차이 없음
        }

        private static string DirectionText(Transform player, Vector3 targetPosition) // 플레이어 전방 기준 목표 수평 방향 표시
        {
            Vector3 toTarget = targetPosition - player.position; // 목표 방향 벡터 계산
            toTarget.y = 0f; // 수평 방향만 사용

            if (toTarget.sqrMagnitude <= 0.01f) // 목표와 거의 같은 위치 확인
            {
                return "도착"; // 도착 표시
            }

            Vector3 forward = player.forward; // 플레이어 전방 복사
            forward.y = 0f; // 수평 전방만 사용

            if (forward.sqrMagnitude <= 0.001f) // 비정상 전방 확인
            {
                forward = Vector3.forward; // 기본 북쪽 방향 사용
            }

            float angle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up); // 좌우 부호가 있는 각도 계산
            float absolute = Mathf.Abs(angle); // 절대 방향 각도 계산

            if (absolute <= 18f) // 거의 정면 확인
            {
                return "전방"; // 전방 표시
            }

            if (absolute >= 155f) // 거의 후방 확인
            {
                return "후방"; // 후방 표시
            }

            return angle < 0f ? "좌 " + Mathf.RoundToInt(absolute) + "°" : "우 " + Mathf.RoundToInt(absolute) + "°"; // 좌우 각도 표시
        }

        private static string StatusText(Map29TerminalObjectiveStatus status, bool demo) // 단말기 상태 문구 변환
        {
            if (demo && status == Map29TerminalObjectiveStatus.Tracking) // 임시 추적 연결 확인
            {
                return "LOCAL TRACK / 시험 추적"; // 정식 미션 전 시험 표시
            }

            switch (status) // 현재 목표 상태 분기
            {
                case Map29TerminalObjectiveStatus.Tracking: return "TRACKING / 추적 중"; // 추적 상태
                case Map29TerminalObjectiveStatus.Completed: return "COMPLETE / 완료"; // 완료 상태
                case Map29TerminalObjectiveStatus.Failed: return "FAILED / 실패"; // 실패 상태
                case Map29TerminalObjectiveStatus.Paused: return "PAUSED / 대기"; // 일시 정지 상태
                default: return "STANDBY / 연결 대기"; // 목표 없음
            }
        }

        private static Color StatusColor(Map29TerminalObjectiveStatus status, bool demo) // 상태별 네온 색상
        {
            if (demo && status == Map29TerminalObjectiveStatus.Tracking) // 시험 추적 확인
            {
                return amber; // 시험 상태 주황색
            }

            switch (status) // 현재 상태 분기
            {
                case Map29TerminalObjectiveStatus.Completed: return green; // 완료 녹색
                case Map29TerminalObjectiveStatus.Failed: return red; // 실패 적색
                case Map29TerminalObjectiveStatus.Paused: return amber; // 대기 주황색
                default: return cyan; // 추적·기본 청록색
            }
        }

        private void EnsureStyles(float scale) // 현재 해상도 배율에 맞는 IMGUI 텍스트 스타일 준비
        {
            if (headerStyle != null && Mathf.Abs(styledScale - scale) < 0.01f) // 기존 스타일과 동일 배율 확인
            {
                return; // 재생성 생략
            }

            styledScale = scale; // 현재 배율 저장
            headerStyle = CreateStyle(11, FontStyle.Bold, textMuted, TextAnchor.MiddleLeft); // 시스템 제목
            missionStyle = CreateStyle(16, FontStyle.Bold, textMain, TextAnchor.MiddleLeft); // 미션 제목
            objectiveStyle = CreateStyle(13, FontStyle.Normal, textMain, TextAnchor.UpperLeft); // 목표 문구
            objectiveStyle.wordWrap = true; // 긴 목표 문구 자동 줄바꿈
            dataLabelStyle = CreateStyle(9, FontStyle.Bold, textMuted, TextAnchor.MiddleLeft); // 작은 데이터 라벨
            dataValueStyle = CreateStyle(13, FontStyle.Bold, textMain, TextAnchor.MiddleLeft); // 데이터 값
            smallStyle = CreateStyle(9, FontStyle.Normal, textMuted, TextAnchor.MiddleLeft); // 하단 작은 상태 글자
            badgeStyle = CreateStyle(10, FontStyle.Bold, cyan, TextAnchor.MiddleCenter); // 미션 ID 배지
            badgeStyle.normal.background = Texture2D.whiteTexture; // 배지 배경용 흰 텍스처 지정
        }

        private static GUIStyle CreateStyle(int size, FontStyle fontStyle, Color color, TextAnchor alignment) // 공통 단말기 텍스트 스타일 생성
        {
            GUIStyle style = new GUIStyle(GUI.skin.label); // 기본 Label 스타일 복사
            style.fontSize = size; // 가상 화면 기준 글자 크기 적용
            style.fontStyle = fontStyle; // 굵기 적용
            style.normal.textColor = color; // 기본 글자 색상 적용
            style.alignment = alignment; // 정렬 적용
            style.padding = new RectOffset(0, 0, 0, 0); // 불필요한 기본 여백 제거
            return style; // 완성 스타일 반환
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

        private static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color) // 절단형 모서리용 회전 선 출력
        {
            Vector2 delta = end - start; // 선 방향 벡터 계산
            float length = delta.magnitude; // 선 길이 계산

            if (length <= 0.01f) // 실제 선 길이 확인
            {
                return; // 출력 생략
            }

            Matrix4x4 previousMatrix = GUI.matrix; // 현재 스케일·화면 변환 보존
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; // 화면 좌표 기준 회전각 계산
            GUIUtility.RotateAroundPivot(angle, start); // 시작점을 중심으로 선 회전
            DrawSolid(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), color); // 회전된 얇은 사각형 선 출력
            GUI.matrix = previousMatrix; // 기존 GUI 변환 복원
        }
    }
}
