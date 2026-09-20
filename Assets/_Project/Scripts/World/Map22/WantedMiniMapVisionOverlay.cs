using ProjectK.Day19; // 기존 미니맵 UI 참조
using ProjectK.Day21; // 수배 경비와 수배 상태 참조
using ProjectK.Day24; // 지상·지하 층 판정 참조
using UnityEngine; // 미니맵 GUI와 좌표 처리

namespace ProjectK.Day22 // 22일차 미니맵 적 방향 표시 이름 공간
{
    [DisallowMultipleComponent] // 미니맵 적 표시 중복 방지
    public sealed class WantedMiniMapVisionOverlay : MonoBehaviour // 수배 경비 위치와 진행 방향 표시
    {
        [SerializeField, Min(2f)] private float markerSize = 7f; // 적 중심 표식 크기
        [SerializeField, Min(4f)] private float ringRadius = 10f; // 적 주변 고리 반경
        [SerializeField, Min(0.5f)] private float ringThickness = 1.4f; // 기본 고리 두께
        [SerializeField, Range(20f, 140f)] private float directionArcAngle = 70f; // 방향 강조 고리 각도
        [SerializeField, Min(1f)] private float directionThickness = 4.2f; // 방향 강조 고리 두께
        [SerializeField, Range(8, 48)] private int ringSegments = 28; // 기본 고리 분할 수
        [SerializeField, Range(3, 24)] private int directionSegments = 9; // 방향 강조 구간 분할 수
        [SerializeField] private Color markerColor = new Color(1f, 0.12f, 0.10f, 1f); // 같은 층 적 위치 색상
        [SerializeField] private Color ringColor = new Color(1f, 0.28f, 0.24f, 0.46f); // 같은 층 적 기본 고리 색상
        [SerializeField] private Color directionColor = new Color(1f, 0.08f, 0.05f, 1f); // 같은 층 진행 방향 색상
        [SerializeField] private Color otherFloorColor = new Color(1f, 0.32f, 0.26f, 0.34f); // 다른 층 적 흐린 표시 색상
        private MapNavigationUI navigation; // 현재 미니맵 UI 참조
        private GUIStyle floorStyle; // 층 방향 화살표 스타일
        private float nextNavigationSearch; // 다음 미니맵 검색 시각

        private void Awake() // 초기 미니맵 참조 준비
        {
            ResolveNavigation(); // 현재 씬 미니맵 검색
        }

        private void OnEnable() // 재활성화 시 미니맵 참조 복구
        {
            ResolveNavigation(); // 미니맵 UI 연결 시도
        }

        private void Update() // 씬 전환과 생성 순서 차이 보정
        {
            if (navigation != null) // 현재 미니맵 참조 확인
            {
                return; // 정상 참조 유지
            }

            if (Time.unscaledTime < nextNavigationSearch) // 검색 간격 확인
            {
                return; // 아직 검색하지 않음
            }

            nextNavigationSearch = Time.unscaledTime + 0.5f; // 다음 검색 시각 예약
            ResolveNavigation(); // 새 씬 미니맵 UI 검색
        }

        private void ResolveNavigation() // 활성 미니맵 UI 검색
        {
            MapNavigationUI[] candidates = Object.FindObjectsByType<MapNavigationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전체 지도 UI 조회
            navigation = candidates.Length > 0 ? candidates[0] : null; // 첫 지도 UI 연결
        }

        private void OnGUI() // 기존 미니맵 위에 적 위치와 방향 표시
        {
            if (navigation == null || navigation.World == null || navigation.World.Player == null) // 필수 미니맵 참조 확인
            {
                return; // 표시 중단
            }

            if (!navigation.MinimapVisible || navigation.FullMapOpen) // 미니맵 숨김 또는 전체 지도 상태 확인
            {
                return; // 미니맵 오버레이 숨김
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null || wanted.Stars <= 0) // 수배 상태 확인
            {
                return; // 평상시 적 표시 없음
            }

            int previousDepth = GUI.depth; // 기존 GUI 깊이 저장
            GUI.depth = -1000; // 기존 미니맵보다 앞쪽에 표시
            DrawWantedGuards(); // 수배 경비 위치와 층 방향 출력
            GUI.depth = previousDepth; // 기존 GUI 깊이 복원
        }

        private void DrawWantedGuards() // 미니맵 범위 안 수배 경비 표시
        {
            float size = MapNavigationMath.MiniMapPixelSize(navigation.MinimapSizeLevel, Screen.width, Screen.height); // 실제 미니맵 픽셀 크기 계산
            Rect outer = new Rect(Screen.width - size - 18f, 18f, size, size); // 기존 미니맵 화면 위치 계산
            Vector3 playerPosition = navigation.World.Player.transform.position; // 현재 플레이어 월드 위치 조회
            Vector2 center = new Vector2(playerPosition.x, playerPosition.z); // 미니맵 중심 월드 좌표 생성
            float span = navigation.MiniMapWorldSpan; // 현재 미니맵 표시 거리 조회
            Map24LayerNavigation layerNavigation = Map24LayerNavigation.Instance; // Day24 층 관리자 조회
            Map24WorldLayer playerLayer = layerNavigation != null ? layerNavigation.GetLayer(playerPosition) : Map24WorldLayer.Surface; // 플레이어 현재 층 판정

            GUI.BeginGroup(outer); // 미니맵 밖 적 표시 클리핑
            Rect local = new Rect(0f, 0f, outer.width, outer.height); // 그룹 내부 지도 좌표 생성
            MapWantedGuardAgent[] guards = Object.FindObjectsByType<MapWantedGuardAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 수배 경비 전체 조회

            foreach (MapWantedGuardAgent guard in guards) // 모든 활성 수배 경비 순회
            {
                if (guard == null || guard.IsDead) // 누락 또는 사망 경비 확인
                {
                    continue; // 다음 경비 처리
                }

                Vector3 guardPosition = guard.transform.position; // 경비 월드 위치 조회
                if (!MapNavigationMath.IsInside(guardPosition, center, span)) // 현재 미니맵 범위 밖 확인
                {
                    continue; // 화면 밖 경비 제외
                }

                Vector2 point = MapNavigationMath.WorldToViewport(guardPosition, center, span, local); // 경비 미니맵 좌표 계산
                Map24WorldLayer guardLayer = layerNavigation != null ? layerNavigation.GetLayer(guardPosition) : playerLayer; // 경비 현재 층 판정
                bool sameLayer = layerNavigation == null || guardLayer == playerLayer; // 플레이어와 경비 층 일치 여부 계산

                if (sameLayer) // 같은 층 경비 확인
                {
                    DrawEnemyMarker(point, markerColor); // 진한 적 중심 표식 출력
                    DrawEnemyRing(point, ringColor, ringThickness); // 기본 원형 고리 출력
                    DrawDirectionArc(guard, point); // 현재 진행 방향 고리 강조
                }
                else // 다른 층 경비 처리
                {
                    DrawEnemyMarker(point, otherFloorColor); // 흐린 적 중심 표식 출력
                    DrawEnemyRing(point, otherFloorColor, 1f); // 흐린 층 구분 고리 출력
                    DrawFloorIndicator(point, guardLayer, playerLayer); // 위·아래 층 화살표 표시
                }
            }

            GUI.EndGroup(); // 미니맵 클리핑 종료
        }

        private void DrawEnemyMarker(Vector2 point, Color color) // 적 중심 표식 표시
        {
            Rect markerRect = new Rect(point.x - markerSize * 0.5f, point.y - markerSize * 0.5f, markerSize, markerSize); // 적 표식 사각형 계산
            DrawRect(markerRect, color); // 지정 색상 적 위치 표식 출력
        }

        private void DrawEnemyRing(Vector2 point, Color color, float thickness) // 적 주변 원형 고리 표시
        {
            int segments = Mathf.Max(8, ringSegments); // 안전한 고리 분할 수 계산
            float step = 360f / segments; // 각 선분 각도 계산

            for (int i = 0; i < segments; i++) // 원형 고리 선분 순회
            {
                float angleA = i * step; // 현재 선분 시작 각도 계산
                float angleB = (i + 1) * step; // 현재 선분 종료 각도 계산
                Vector2 start = point + DirectionFromAngle(angleA) * ringRadius; // 시작점 계산
                Vector2 end = point + DirectionFromAngle(angleB) * ringRadius; // 종료점 계산
                DrawLine(start, end, color, thickness); // 지정 색상 원형 고리 출력
            }
        }

        private void DrawDirectionArc(MapWantedGuardAgent guard, Vector2 point) // 같은 층 적 진행 방향 고리 구간 강조
        {
            Vector3 worldForward = guard.transform.forward; // 현재 경비 이동과 시선 방향 조회
            DetectionSensor sensor = guard.Sensor; // 경비 실제 탐지 센서 조회
            if (sensor != null && sensor.VisionSource != null) // 별도 시야 기준 존재 여부 확인
            {
                worldForward = sensor.VisionSource.forward; // 실제 시야 기준 방향 사용
            }

            Vector2 screenDirection = new Vector2(worldForward.x, -worldForward.z); // 월드 방향을 미니맵 방향으로 변환
            if (screenDirection.sqrMagnitude <= 0.0001f) // 유효한 방향 확인
            {
                return; // 방향 표시 생략
            }

            screenDirection.Normalize(); // 화면 방향 정규화
            float centerAngle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg; // 화면 기준 중앙 방향 각도 계산
            float halfArc = directionArcAngle * 0.5f; // 강조 구간 반각 계산
            int segments = Mathf.Max(3, directionSegments); // 안전한 강조 분할 수 계산
            float step = directionArcAngle / segments; // 강조 구간 선분 각도 계산

            for (int i = 0; i < segments; i++) // 방향 강조 선분 순회
            {
                float angleA = centerAngle - halfArc + step * i; // 강조 선분 시작 각도 계산
                float angleB = centerAngle - halfArc + step * (i + 1); // 강조 선분 종료 각도 계산
                Vector2 start = point + DirectionFromAngle(angleA) * ringRadius; // 강조 시작점 계산
                Vector2 end = point + DirectionFromAngle(angleB) * ringRadius; // 강조 종료점 계산
                DrawLine(start, end, directionColor, directionThickness); // 진행 방향 구간 진한 색상 출력
            }

            Vector2 pointerEnd = point + screenDirection * (ringRadius + 4f); // 방향 보조선 끝점 계산
            DrawLine(point, pointerEnd, directionColor, 1.8f); // 적 중심에서 방향 보조선 출력
        }

        private void DrawFloorIndicator(Vector2 point, Map24WorldLayer guardLayer, Map24WorldLayer playerLayer) // 다른 층 적의 위·아래 방향 표시
        {
            EnsureFloorStyle(); // 층 화살표 GUI 스타일 준비
            string symbol = guardLayer == Map24WorldLayer.Surface && playerLayer == Map24WorldLayer.Underground ? "↑" : "↓"; // 플레이어 기준 위·아래 화살표 선택
            Rect labelRect = new Rect(point.x - 10f, point.y - ringRadius - 18f, 20f, 18f); // 적 표식 위 화살표 위치 계산
            GUI.Label(labelRect, symbol, floorStyle); // 층 방향 화살표 출력
        }

        private void EnsureFloorStyle() // 층 화살표 스타일 생성
        {
            if (floorStyle != null) // 기존 스타일 존재 확인
            {
                return; // 중복 생성 방지
            }

            floorStyle = new GUIStyle(GUI.skin.label); // 기본 라벨 스타일 복사
            floorStyle.fontSize = 15; // 작은 미니맵용 글자 크기 설정
            floorStyle.fontStyle = FontStyle.Bold; // 층 화살표 강조
            floorStyle.alignment = TextAnchor.MiddleCenter; // 표식 중앙 정렬
            floorStyle.normal.textColor = new Color(1f, 0.42f, 0.36f, 0.82f); // 흐린 빨간 화살표 색상 적용
        }

        private static Vector2 DirectionFromAngle(float angleDegrees) // 화면 각도를 방향 벡터로 변환
        {
            float radians = angleDegrees * Mathf.Deg2Rad; // 각도를 라디안으로 변환
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)); // 화면 방향 벡터 반환
        }

        private static void DrawRect(Rect rect, Color color) // 단색 사각형 출력
        {
            Color previousColor = GUI.color; // 기존 GUI 색상 저장
            GUI.color = color; // 요청 색상 적용
            GUI.DrawTexture(rect, Texture2D.whiteTexture); // 단색 적 표식 출력
            GUI.color = previousColor; // 기존 GUI 색상 복원
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) // 회전 가능한 선 출력
        {
            Vector2 delta = end - start; // 선 방향과 길이 계산
            float length = delta.magnitude; // 선 길이 계산
            if (length <= 0.01f) // 너무 짧은 선 확인
            {
                return; // 출력 생략
            }

            Matrix4x4 previousMatrix = GUI.matrix; // 기존 GUI 변환 저장
            Color previousColor = GUI.color; // 기존 GUI 색상 저장
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; // 선 회전 각도 계산
            GUIUtility.RotateAroundPivot(angle, start); // 시작점 기준 GUI 회전
            GUI.color = color; // 선 색상 적용
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture); // 회전된 선 출력
            GUI.matrix = previousMatrix; // 기존 GUI 변환 복원
            GUI.color = previousColor; // 기존 GUI 색상 복원
        }
    }
}
