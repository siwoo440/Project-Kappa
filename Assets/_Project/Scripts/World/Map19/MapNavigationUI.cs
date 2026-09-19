using System; // 문자열과 상태 보존
using ProjectK.Day16; // 본편 월드와 장소 참조
using UnityEngine; // 지도 UI와 월드 좌표
using UnityEngine.InputSystem; // M N 키와 마우스 입력
using UnityEngine.InputSystem.Controls; // 대괄호 키 참조

namespace ProjectK.Day19 // 19일차 월드 지도 이름 공간
{
    [DisallowMultipleComponent] // 지도 UI 중복 방지
    public sealed class MapNavigationUI : MonoBehaviour // M 전체 지도와 N 미니맵 런타임 화면
    {
        [SerializeField] private MapWorldRoot world; // 연결된 본편 월드
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private bool minimapVisible = true; // 미니맵 기본 표시
        [SerializeField, Range(0, 2)] private int minimapSizeLevel = 1; // 미니맵 소형 중형 대형 단계
        [SerializeField] private float minimapWorldSpan = 280f; // 미니맵에 보이는 실제 월드 거리
        [SerializeField] private float fullMapZoom = 1f; // 전체 지도 확대 배율
        [SerializeField] private Vector2 fullMapCenter; // 전체 지도 이동 중심
        private bool fullMapOpen; // 전체 지도 표시 상태
        private bool draggingMap; // 마우스 지도 이동 상태
        private Vector2 lastDragPosition; // 이전 드래그 화면 좌표
        private float previousTimeScale = 1f; // 지도 열기 전 시간 배율
        private bool previousMovementEnabled = true; // 지도 열기 전 이동 상태
        private bool previousCameraEnabled = true; // 지도 열기 전 카메라 상태
        private CursorLockMode previousCursorLock; // 지도 열기 전 커서 잠금
        private bool previousCursorVisible; // 지도 열기 전 커서 표시
        private PlayerMovement movement; // 이동 중지와 복원 참조
        private ThirdPersonCamera cameraController; // 지도 사용 중 시점 입력 차단
        private PlayerFirearmController firearm; // 지도 열기 전 사격 상태 정리
        private GUIStyle titleStyle; // 전체 지도 제목 스타일
        private GUIStyle labelStyle; // 일반 안내 스타일
        private GUIStyle smallStyle; // 작은 조작 안내 스타일
        private GUIStyle placeStyle; // 주요 구역 이름 스타일
        private const string MiniVisibleKey = "ProjectK.Map.MinimapVisible"; // 미니맵 표시 저장 키
        private const string MiniSizeKey = "ProjectK.Map.MinimapSize"; // 미니맵 크기 저장 키
        private const string MiniSpanKey = "ProjectK.Map.MinimapSpan"; // 미니맵 거리 저장 키
        public MapWorldRoot World => world; // 에디터 검사 월드 참조
        public string SourceCommit => sourceCommit; // 에디터 검사 기준 커밋
        public bool MinimapVisible => minimapVisible; // 현재 미니맵 표시 상태
        public int MinimapSizeLevel => minimapSizeLevel; // 현재 미니맵 크기 단계
        public float MiniMapWorldSpan => minimapWorldSpan; // 현재 미니맵 월드 범위
        public bool FullMapOpen => fullMapOpen; // 현재 전체 지도 표시 상태

        public void Configure(MapWorldRoot owner, string commit) // 에디터에서 Map 월드와 기준 커밋 연결
        {
            world = owner; // 본편 월드 참조 저장
            sourceCommit = commit; // 기준 커밋 저장
            ResolveReferences(); // 플레이어와 카메라 즉시 조회
        }

        private void Awake() // 런타임 초기 참조 준비
        {
            if (world == null) // 직렬화 참조 누락 확인
            {
                world = GetComponent<MapWorldRoot>(); // 같은 월드 루트에서 자동 복구
            }
            LoadPreferences(); // 사용자의 미니맵 설정 불러오기
            ResolveReferences(); // 플레이어와 카메라 연결
            ResetFullMapView(); // 전체 지도 기본 중심과 배율 적용
        }

        private void OnEnable() // 씬 재활성화 처리
        {
            LoadPreferences(); // 최근 미니맵 설정 복원
            ResolveReferences(); // 플레이어 참조 복구
        }

        private void OnDisable() // 컴포넌트 비활성화 시 입력 상태 복원
        {
            if (fullMapOpen) // 열린 전체 지도 확인
            {
                CloseFullMap(); // 시간과 카메라 상태 복원
            }
        }

        private void Update() // 키보드 기반 지도 입력 처리
        {
            if (world == null || world.Player == null) // 필수 월드와 플레이어 확인
            {
                ResolveReferences(); // 생성 순서 차이 보정
            }
            Keyboard keyboard = Keyboard.current; // 현재 키보드 조회
            if (keyboard == null) // 키보드 미연결 확인
            {
                return; // 입력 처리 중단
            }
            if (keyboard.mKey.wasPressedThisFrame) // M 전체 지도 입력 확인
            {
                ToggleFullMap(); // 전체 지도 열기 또는 닫기
            }
            bool shiftKey = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed; // Shift 보조키 확인
            if (keyboard.nKey.wasPressedThisFrame) // N 미니맵 입력 확인
            {
                if (shiftKey) // Shift+N 크기 변경 확인
                {
                    CycleMiniMapSize(); // 소형 중형 대형 순환
                }
                else // 일반 N 입력 처리
                {
                    ToggleMiniMap(); // 미니맵 표시 상태 변경
                }
            }
            KeyControl bracketLeftKey = keyboard.leftBracketKey; // 미니맵 줌 아웃 키 참조
            KeyControl bracketRightKey = keyboard.rightBracketKey; // 미니맵 줌 인 키 참조
            if (bracketLeftKey.wasPressedThisFrame) // 왼쪽 대괄호 입력 확인
            {
                minimapWorldSpan = MapNavigationMath.ClampMiniMapSpan(minimapWorldSpan * 1.15f); // 더 넓은 범위 표시
                SavePreferences(); // 거리 설정 저장
            }
            if (bracketRightKey.wasPressedThisFrame) // 오른쪽 대괄호 입력 확인
            {
                minimapWorldSpan = MapNavigationMath.ClampMiniMapSpan(minimapWorldSpan / 1.15f); // 더 가까운 범위 표시
                SavePreferences(); // 거리 설정 저장
            }
            if (fullMapOpen) // 전체 지도 전용 입력 확인
            {
                HandleFullMapKeyboard(keyboard); // 이동·초기화·닫기 처리
                HandleFullMapScroll(); // 마우스 휠 확대 축소 처리
            }
        }

        private void HandleFullMapKeyboard(Keyboard keyboard) // 전체 지도 키 입력 처리
        {
            if (keyboard.escapeKey.wasPressedThisFrame) // ESC 닫기 입력 확인
            {
                CloseFullMap(); // 전체 지도 종료
                return; // 같은 프레임 추가 입력 방지
            }
            if (keyboard.rKey.wasPressedThisFrame) // 지도 보기 초기화 입력 확인
            {
                ResetFullMapView(); // 중앙과 확대 배율 초기화
            }
            float span = MapNavigationMath.FullMapSpan(world.WorldSize, fullMapZoom); // 현재 표시 월드 범위 계산
            float speed = span * 0.65f * Time.unscaledDeltaTime; // 확대 상태에 맞춘 지도 이동 속도
            Vector2 delta = Vector2.zero; // 현재 프레임 지도 이동량
            if (keyboard.leftArrowKey.isPressed) // 왼쪽 지도 이동 확인
            {
                delta.x -= speed; // 지도 중심 서쪽 이동
            }
            if (keyboard.rightArrowKey.isPressed) // 오른쪽 지도 이동 확인
            {
                delta.x += speed; // 지도 중심 동쪽 이동
            }
            if (keyboard.upArrowKey.isPressed) // 위쪽 지도 이동 확인
            {
                delta.y += speed; // 지도 중심 북쪽 이동
            }
            if (keyboard.downArrowKey.isPressed) // 아래쪽 지도 이동 확인
            {
                delta.y -= speed; // 지도 중심 남쪽 이동
            }
            fullMapCenter = MapNavigationMath.ClampCenter(fullMapCenter + delta, span, world.WorldSize); // 월드 밖 이동 차단
        }

        private void HandleFullMapScroll() // 전체 지도 마우스 휠 확대 처리
        {
            if (Mouse.current == null) // 마우스 미연결 확인
            {
                return; // 확대 처리 중단
            }
            float wheel = Mouse.current.scroll.ReadValue().y; // 현재 휠 이동량 읽기
            if (Mathf.Abs(wheel) < 0.01f) // 실제 휠 입력 여부 확인
            {
                return; // 변화 없음 처리
            }
            fullMapZoom = Mathf.Clamp(fullMapZoom * (wheel > 0f ? 1.15f : 0.87f), 1f, 4f); // 1배에서 4배 확대 제한
            float span = MapNavigationMath.FullMapSpan(world.WorldSize, fullMapZoom); // 새 지도 표시 범위 계산
            fullMapCenter = MapNavigationMath.ClampCenter(fullMapCenter, span, world.WorldSize); // 확대 후 중심 범위 보정
        }

        public void ToggleMiniMap() // N 미니맵 표시 상태 변경
        {
            minimapVisible = !minimapVisible; // 표시 여부 반전
            SavePreferences(); // 사용자 선택 저장
        }

        public void CycleMiniMapSize() // Shift+N 미니맵 크기 순환
        {
            minimapSizeLevel = (minimapSizeLevel + 1) % 3; // 소형 중형 대형 다음 단계 선택
            SavePreferences(); // 사용자 선택 저장
        }

        public void ToggleFullMap() // M 전체 지도 상태 전환
        {
            if (fullMapOpen) // 현재 열린 상태 확인
            {
                CloseFullMap(); // 지도 닫기
            }
            else // 현재 닫힌 상태 처리
            {
                OpenFullMap(); // 지도 열기
            }
        }

        private void OpenFullMap() // 전체 지도 진입과 게임 입력 잠금
        {
            ResolveReferences(); // 최신 플레이어 상태 확인
            if (world == null || world.Player == null) // 필수 월드 확인
            {
                return; // 잘못된 씬에서 열기 중단
            }
            previousTimeScale = Time.timeScale; // 기존 시간 배율 저장
            previousCursorLock = Cursor.lockState; // 기존 커서 잠금 저장
            previousCursorVisible = Cursor.visible; // 기존 커서 표시 저장
            previousMovementEnabled = movement == null || movement.MovementEnabled; // 기존 플레이어 이동 상태 저장
            previousCameraEnabled = cameraController == null || cameraController.enabled; // 기존 카메라 활성 상태 저장
            fullMapOpen = true; // 지도 열린 상태 먼저 적용
            firearm?.Interrupt(); // 사격·조준 진행 상태 정리
            movement?.SetMovementEnabled(false); // 지도 중 플레이어 이동 정지
            if (cameraController != null) // 실제 카메라 관리자 확인
            {
                cameraController.enabled = false; // 마우스 시점 입력과 자동 재잠금 차단
            }
            Time.timeScale = 0f; // 전체 지도 동안 월드 일시 정지
            Cursor.lockState = CursorLockMode.None; // 지도 드래그용 커서 해제
            Cursor.visible = true; // 지도 조작 커서 표시
        }

        private void CloseFullMap() // 전체 지도 종료와 이전 상태 복원
        {
            fullMapOpen = false; // UI 상태 먼저 종료
            draggingMap = false; // 진행 중 드래그 해제
            Time.timeScale = previousTimeScale; // 기존 게임 시간 복원
            movement?.SetMovementEnabled(previousMovementEnabled); // 기존 이동 상태 복원
            if (cameraController != null) // 실제 카메라 관리자 확인
            {
                cameraController.enabled = previousCameraEnabled; // 이전 카메라 활성 상태 복원
            }
            Cursor.lockState = previousCursorLock; // 지도 열기 전 커서 잠금 복원
            Cursor.visible = previousCursorVisible; // 지도 열기 전 커서 표시 복원
        }

        private void ResetFullMapView() // 전체 지도 초기 보기 설정
        {
            fullMapZoom = 1f; // 전체 월드 표시 배율 적용
            fullMapCenter = Vector2.zero; // 월드 중심으로 이동
        }

        private void ResolveReferences() // 플레이어 관련 런타임 참조 조회
        {
            if (world == null) // 월드 참조 확인
            {
                world = GetComponent<MapWorldRoot>(); // 같은 오브젝트에서 월드 조회
            }
            if (world == null || world.Player == null) // 플레이어 생성 여부 확인
            {
                return; // 다음 프레임 재시도
            }
            movement = world.Player.GetComponent<PlayerMovement>(); // 이동 관리자 조회
            firearm = world.Player.GetComponent<PlayerFirearmController>(); // 총기 관리자 조회
            Camera camera = world.PlayCamera; // 본편 플레이 카메라 조회
            cameraController = camera != null ? camera.GetComponent<ThirdPersonCamera>() : null; // 3인칭 시점 관리자 조회
        }

        private void LoadPreferences() // 이전 실행의 미니맵 설정 불러오기
        {
            minimapVisible = PlayerPrefs.GetInt(MiniVisibleKey, minimapVisible ? 1 : 0) != 0; // 표시 여부 복원
            minimapSizeLevel = Mathf.Clamp(PlayerPrefs.GetInt(MiniSizeKey, minimapSizeLevel), 0, 2); // 크기 단계 복원
            minimapWorldSpan = MapNavigationMath.ClampMiniMapSpan(PlayerPrefs.GetFloat(MiniSpanKey, minimapWorldSpan)); // 표시 거리 복원
        }

        private void SavePreferences() // 미니맵 설정 저장
        {
            PlayerPrefs.SetInt(MiniVisibleKey, minimapVisible ? 1 : 0); // 표시 여부 저장
            PlayerPrefs.SetInt(MiniSizeKey, minimapSizeLevel); // 크기 단계 저장
            PlayerPrefs.SetFloat(MiniSpanKey, minimapWorldSpan); // 표시 거리 저장
            PlayerPrefs.Save(); // 설정 즉시 기록
        }

        private void OnGUI() // 미니맵과 전체 지도 그리기
        {
            if (world == null || world.Player == null) // 월드 표시 준비 확인
            {
                return; // UI 그리기 중단
            }
            EnsureStyles(); // 현재 해상도용 글자 스타일 준비
            if (fullMapOpen) // 전체 지도 우선 표시
            {
                DrawFullMap(); // 화면 대부분을 사용하는 전체 지도 출력
            }
            else if (minimapVisible) // 일반 플레이 미니맵 표시 확인
            {
                DrawMiniMap(); // 우측 상단 미니맵 출력
            }
            DrawPersistentHint(); // 기본 조작 안내 출력
        }

        private void DrawMiniMap() // 플레이어 중심 미니맵 그리기
        {
            float size = MapNavigationMath.MiniMapPixelSize(minimapSizeLevel, Screen.width, Screen.height); // 현재 단계 실제 픽셀 크기 계산
            Rect outer = new Rect(Screen.width - size - 18f, 18f, size, size); // 우측 상단 미니맵 위치
            DrawRect(new Rect(outer.x - 3f, outer.y - 3f, outer.width + 6f, outer.height + 6f), new Color(0.05f, 0.75f, 0.85f, 0.78f)); // 청록 외곽선 표시
            DrawRect(outer, new Color(0.015f, 0.025f, 0.045f, 0.88f)); // 반투명 지도 배경
            Vector3 playerPosition = world.Player.transform.position; // 플레이어 월드 좌표 조회
            Vector2 center = new Vector2(playerPosition.x, playerPosition.z); // 미니맵 중심 좌표 구성
            GUI.BeginGroup(outer); // 미니맵 외부 그리기 차단
            Rect local = new Rect(0f, 0f, outer.width, outer.height); // 그룹 내부 좌표 정의
            DrawMapContents(local, center, minimapWorldSpan, false); // 근거리 도로·구역·플레이어 표시
            GUI.EndGroup(); // 미니맵 클리핑 종료
            string area = NearestPlaceName(playerPosition); // 현재 가장 가까운 구역 이름 조회
            GUI.Label(new Rect(outer.x + 10f, outer.y + 7f, outer.width - 20f, 24f), area, placeStyle); // 미니맵 현재 지역 표시
            GUI.Label(new Rect(outer.x + 9f, outer.y + outer.height - 25f, outer.width - 18f, 20f), "N 숨김 · Shift+N 크기 · [ ] 거리", smallStyle); // 미니맵 조작 안내
        }

        private void DrawFullMap() // 일시 정지형 전체 지도 출력
        {
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.008f, 0.018f, 0.96f)); // 전체 화면 암색 배경
            float sidePanel = Mathf.Clamp(Screen.width * 0.20f, 230f, 330f); // 왼쪽 정보 영역 폭 계산
            Rect panel = new Rect(24f, 24f, sidePanel, Screen.height - 48f); // 전체 지도 정보 패널 위치
            DrawRect(panel, new Color(0.025f, 0.045f, 0.075f, 0.96f)); // 정보 패널 배경
            GUI.Label(new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, 42f), "연무 도시 지도", titleStyle); // 지도 제목 표시
            GUI.Label(new Rect(panel.x + 18f, panel.y + 74f, panel.width - 36f, 110f), "M / ESC  닫기\n마우스 휠  확대·축소\n드래그 / 방향키  지도 이동\nR  전체 보기\nN  미니맵 표시 전환\nShift+N  미니맵 크기", labelStyle); // 전체 지도 조작 안내
            string location = NearestPlaceName(world.Player.transform.position); // 현재 구역 조회
            GUI.Label(new Rect(panel.x + 18f, panel.y + 212f, panel.width - 36f, 70f), "현재 위치\n" + location, placeStyle); // 현재 위치 정보 표시
            GUI.Label(new Rect(panel.x + 18f, panel.y + panel.height - 92f, panel.width - 36f, 74f), "청록색 점: 주요 구역\n흰색 표식: 플레이어\n가는 선: 도시 도로", smallStyle); // 지도 범례 표시
            float availableWidth = Screen.width - panel.xMax - 48f; // 지도 사용 가능 가로 길이
            float availableHeight = Screen.height - 64f; // 지도 사용 가능 세로 길이
            float mapSize = Mathf.Min(availableWidth, availableHeight); // 정사각형 지도 크기 계산
            Rect mapRect = new Rect(panel.xMax + 24f + (availableWidth - mapSize) * 0.5f, 32f + (availableHeight - mapSize) * 0.5f, mapSize, mapSize); // 전체 지도 중앙 배치
            DrawRect(new Rect(mapRect.x - 4f, mapRect.y - 4f, mapRect.width + 8f, mapRect.height + 8f), new Color(0.05f, 0.85f, 0.92f, 0.88f)); // 전체 지도 외곽선
            DrawRect(mapRect, new Color(0.010f, 0.020f, 0.035f, 1f)); // 전체 지도 배경
            HandleFullMapMouse(mapRect); // 마우스 드래그 처리
            float span = MapNavigationMath.FullMapSpan(world.WorldSize, fullMapZoom); // 현재 확대 표시 범위 계산
            fullMapCenter = MapNavigationMath.ClampCenter(fullMapCenter, span, world.WorldSize); // 지도 중심 안전 범위 적용
            GUI.BeginGroup(mapRect); // 확대 지도 외부 클리핑 시작
            DrawMapContents(new Rect(0f, 0f, mapRect.width, mapRect.height), fullMapCenter, span, true); // 전체 지도 내용 출력
            GUI.EndGroup(); // 전체 지도 클리핑 종료
            GUI.Label(new Rect(mapRect.x + 12f, mapRect.y + 10f, 60f, 25f), "N ↑", placeStyle); // 북쪽 방향 표시
            GUI.Label(new Rect(mapRect.xMax - 120f, mapRect.yMax + 7f, 120f, 24f), "ZOOM ×" + fullMapZoom.ToString("0.0"), smallStyle); // 현재 확대 배율 표시
        }

        private void HandleFullMapMouse(Rect mapRect) // 전체 지도 마우스 드래그 처리
        {
            Event current = Event.current; // 현재 IMGUI 이벤트 조회
            if (current == null) // 이벤트 누락 확인
            {
                return; // 드래그 처리 중단
            }
            if (current.type == EventType.MouseDown && current.button == 0 && mapRect.Contains(current.mousePosition)) // 지도 안 왼쪽 클릭 확인
            {
                draggingMap = true; // 드래그 시작 상태 저장
                lastDragPosition = current.mousePosition; // 시작 좌표 저장
                current.Use(); // 다른 UI 클릭 소비
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && draggingMap) // 진행 중 지도 드래그 확인
            {
                Vector2 delta = current.mousePosition - lastDragPosition; // 화면 드래그 거리 계산
                lastDragPosition = current.mousePosition; // 다음 프레임 기준 갱신
                float span = MapNavigationMath.FullMapSpan(world.WorldSize, fullMapZoom); // 현재 월드 표시 범위 계산
                fullMapCenter.x -= delta.x / Mathf.Max(1f, mapRect.width) * span; // 화면 오른쪽 드래그 시 서쪽 지도 내용 표시
                fullMapCenter.y += delta.y / Mathf.Max(1f, mapRect.height) * span; // 화면 아래 드래그 시 북쪽 지도 내용 표시
                fullMapCenter = MapNavigationMath.ClampCenter(fullMapCenter, span, world.WorldSize); // 월드 바깥 이동 차단
                current.Use(); // 드래그 이벤트 소비
            }
            else if (current.type == EventType.MouseUp && current.button == 0) // 왼쪽 버튼 해제 확인
            {
                draggingMap = false; // 드래그 종료
            }
        }

        private void DrawMapContents(Rect rect, Vector2 center, float span, bool labels) // 공통 지도 배경과 표식 그리기
        {
            DrawTerrainTiles(rect, center, span); // 3x3 Terrain 구획 표시
            DrawRoadGrid(rect, center, span); // 12x12 도시 도로 표시
            DrawPlaces(rect, center, span, labels); // 주요 구역 표식 표시
            DrawPlayerMarker(rect, center, span); // 현재 플레이어 위치와 방향 표시
        }

        private void DrawTerrainTiles(Rect rect, Vector2 center, float span) // Terrain 3x3 배경 구획 표시
        {
            float half = world.WorldSize * 0.5f; // 전체 월드 반경 계산
            for (int z = 0; z < 3; z++) // 세로 Terrain 순회
            {
                for (int x = 0; x < 3; x++) // 가로 Terrain 순회
                {
                    float minX = -half + x * world.TileSize; // 타일 서쪽 좌표
                    float maxX = minX + world.TileSize; // 타일 동쪽 좌표
                    float minZ = -half + z * world.TileSize; // 타일 남쪽 좌표
                    float maxZ = minZ + world.TileSize; // 타일 북쪽 좌표
                    Vector2 a = MapNavigationMath.WorldToViewport(new Vector3(minX, 0f, minZ), center, span, rect); // 타일 남서 화면 좌표
                    Vector2 b = MapNavigationMath.WorldToViewport(new Vector3(maxX, 0f, maxZ), center, span, rect); // 타일 북동 화면 좌표
                    Rect tileRect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)); // 화면 타일 사각형 계산
                    Color tileColor = (x + z) % 2 == 0 ? new Color(0.045f, 0.075f, 0.095f, 0.62f) : new Color(0.035f, 0.060f, 0.080f, 0.62f); // 타일 교차 색상 선택
                    DrawRect(tileRect, tileColor); // Terrain 배경 표현
                    DrawLine(new Vector2(tileRect.xMin, tileRect.yMin), new Vector2(tileRect.xMax, tileRect.yMin), new Color(0.18f, 0.45f, 0.50f, 0.45f), 1f); // 북쪽 타일 경계
                    DrawLine(new Vector2(tileRect.xMin, tileRect.yMin), new Vector2(tileRect.xMin, tileRect.yMax), new Color(0.18f, 0.45f, 0.50f, 0.45f), 1f); // 서쪽 타일 경계
                }
            }
        }

        private void DrawRoadGrid(Rect rect, Vector2 center, float span) // Day16 12x12 도시 도로를 단순 선으로 표시
        {
            float minimum = -world.WorldSize * 0.42f; // 도시 도로 남서 경계 계산
            float pitch = world.WorldSize * 0.84f / 12f; // 기존 블록 간격 계산
            float maximum = minimum + pitch * 12f; // 도시 도로 북동 경계 계산
            Color road = new Color(0.32f, 0.38f, 0.45f, 0.55f); // 도로 지도 선 색상
            for (int i = 0; i <= 12; i++) // 도시 격자선 반복
            {
                float p = minimum + pitch * i; // 현재 도로 월드 좌표
                Vector2 verticalA = MapNavigationMath.WorldToViewport(new Vector3(p, 0f, minimum), center, span, rect); // 세로 도로 남쪽 좌표
                Vector2 verticalB = MapNavigationMath.WorldToViewport(new Vector3(p, 0f, maximum), center, span, rect); // 세로 도로 북쪽 좌표
                DrawLine(verticalA, verticalB, road, 1.3f); // 남북 도로 표시
                Vector2 horizontalA = MapNavigationMath.WorldToViewport(new Vector3(minimum, 0f, p), center, span, rect); // 가로 도로 서쪽 좌표
                Vector2 horizontalB = MapNavigationMath.WorldToViewport(new Vector3(maximum, 0f, p), center, span, rect); // 가로 도로 동쪽 좌표
                DrawLine(horizontalA, horizontalB, road, 1.3f); // 동서 도로 표시
            }
        }

        private void DrawPlaces(Rect rect, Vector2 center, float span, bool labels) // 주요 장소 표식과 이름 표시
        {
            foreach (MapPoint place in world.Places ?? Array.Empty<MapPoint>()) // 모든 주요 장소 순회
            {
                if (place == null) // 누락 장소 확인
                {
                    continue; // 다음 장소 처리
                }
                Vector3 position = place.Arrival != null ? place.Arrival.position : place.transform.position; // 도착점 또는 장소 기준 위치 선택
                if (!MapNavigationMath.IsInside(position, center, span)) // 현재 지도 밖 장소 확인
                {
                    continue; // 화면 밖 표식 제외
                }
                Vector2 point = MapNavigationMath.WorldToViewport(position, center, span, rect); // 장소 화면 좌표 계산
                DrawRect(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), new Color(0.05f, 0.95f, 1f, 0.95f)); // 청록 장소 표식
                if (labels) // 전체 지도 이름 표시 여부 확인
                {
                    GUI.Label(new Rect(point.x + 8f, point.y - 12f, 170f, 26f), place.DisplayName, placeStyle); // 한글 장소 이름 표시
                }
            }
        }

        private void DrawPlayerMarker(Rect rect, Vector2 center, float span) // 플레이어 현재 위치와 바라보는 방향 표시
        {
            Vector3 position = world.Player.transform.position; // 플레이어 월드 위치 조회
            if (!MapNavigationMath.IsInside(position, center, span)) // 확대 지도 밖 플레이어 확인
            {
                return; // 화면 밖 표식 제외
            }
            Vector2 point = MapNavigationMath.WorldToViewport(position, center, span, rect); // 플레이어 화면 좌표 계산
            DrawRect(new Rect(point.x - 5f, point.y - 5f, 10f, 10f), Color.white); // 흰색 플레이어 중심 표식
            Vector3 forward = world.Player.transform.forward; // 플레이어 월드 전방 벡터 조회
            Vector2 direction = new Vector2(forward.x, -forward.z).normalized; // 화면 좌표 방향으로 변환
            DrawLine(point, point + direction * 18f, new Color(1f, 0.42f, 0.72f, 1f), 3f); // 자홍색 시선 방향 표시
        }

        private string NearestPlaceName(Vector3 playerPosition) // 현재 플레이어와 가장 가까운 주요 구역 이름 계산
        {
            float best = float.MaxValue; // 가장 가까운 거리 초기값
            string result = "연무 외곽"; // 주요 구역 밖 기본 이름
            foreach (MapPoint place in world.Places ?? Array.Empty<MapPoint>()) // 주요 장소 순회
            {
                if (place == null) // 장소 누락 확인
                {
                    continue; // 다음 장소 처리
                }
                Vector3 position = place.Arrival != null ? place.Arrival.position : place.transform.position; // 실제 장소 위치 선택
                float distance = new Vector2(position.x - playerPosition.x, position.z - playerPosition.z).sqrMagnitude; // 수평 거리 제곱 계산
                if (distance < best) // 더 가까운 장소 확인
                {
                    best = distance; // 최근 최소 거리 갱신
                    result = place.DisplayName; // 표시할 장소 이름 갱신
                }
            }
            return result; // 가장 가까운 구역 이름 반환
        }

        private void DrawPersistentHint() // 플레이 중 항상 보이는 지도 조작 안내
        {
            if (fullMapOpen) // 전체 지도에서 중복 힌트 확인
            {
                return; // 왼쪽 정보 패널만 사용
            }
            float width = 310f; // 힌트 박스 폭
            Rect hint = new Rect(Screen.width - width - 18f, minimapVisible ? MapNavigationMath.MiniMapPixelSize(minimapSizeLevel, Screen.width, Screen.height) + 28f : 18f, width, 24f); // 미니맵 아래 또는 우측 상단 위치
            GUI.Label(hint, "[M] 전체 지도   [N] 미니맵   [Shift+N] 크기", smallStyle); // 핵심 조작 안내
        }

        private void EnsureStyles() // 현재 화면 크기에 맞는 IMGUI 스타일 준비
        {
            if (titleStyle == null) // 최초 스타일 생성 여부 확인
            {
                titleStyle = new GUIStyle(GUI.skin.label); // 기본 라벨에서 제목 생성
                labelStyle = new GUIStyle(GUI.skin.label); // 일반 안내 스타일 생성
                smallStyle = new GUIStyle(GUI.skin.label); // 작은 안내 스타일 생성
                placeStyle = new GUIStyle(GUI.skin.label); // 장소 이름 스타일 생성
            }
            int baseSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 54f), 14, 22); // 해상도 기반 기본 글자 크기
            titleStyle.fontSize = baseSize + 8; // 큰 지도 제목 크기
            titleStyle.fontStyle = FontStyle.Bold; // 지도 제목 굵기
            titleStyle.normal.textColor = new Color(0.2f, 0.95f, 1f, 1f); // 제목 청록 색상
            labelStyle.fontSize = baseSize; // 일반 안내 글자 크기
            labelStyle.normal.textColor = new Color(0.84f, 0.91f, 0.96f, 1f); // 밝은 회청색 안내
            labelStyle.wordWrap = true; // 좁은 패널 줄바꿈 허용
            smallStyle.fontSize = Mathf.Max(12, baseSize - 2); // 작은 안내 글자 크기
            smallStyle.normal.textColor = new Color(0.68f, 0.77f, 0.84f, 1f); // 낮은 강조 안내색
            placeStyle.fontSize = baseSize; // 장소 글자 크기
            placeStyle.fontStyle = FontStyle.Bold; // 장소 이름 굵게 표시
            placeStyle.normal.textColor = new Color(0.42f, 1f, 0.96f, 1f); // 장소 청록 강조색
        }

        private static void DrawRect(Rect rect, Color color) // 단색 사각형 그리기
        {
            Color old = GUI.color; // 기존 GUI 색상 저장
            GUI.color = color; // 요청 색상 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 흰 텍스처를 단색 면으로 출력
            GUI.color = old; // 기존 GUI 색상 복원
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) // 회전 가능한 단색 선 그리기
        {
            Vector2 delta = end - start; // 선 방향과 길이 계산
            float length = delta.magnitude; // 실제 선 길이 계산
            if (length < 0.01f) // 너무 짧은 선 확인
            {
                return; // 그리기 생략
            }
            Matrix4x4 oldMatrix = GUI.matrix; // 기존 GUI 변환 저장
            Color oldColor = GUI.color; // 기존 GUI 색상 저장
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; // 화면 기준 회전 각도 계산
            GUIUtility.RotateAroundPivot(angle, start); // 시작점 기준 선 방향 회전
            GUI.color = color; // 선 색상 적용
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture); // 회전된 단색 선 출력
            GUI.matrix = oldMatrix; // 기존 GUI 변환 복원
            GUI.color = oldColor; // 기존 GUI 색상 복원
        }
    }
}
