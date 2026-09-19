using ProjectK.Day19; // 기존 미니맵 UI 참조
using ProjectK.Day21; // 수배 경비와 수배 상태 참조
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
        [SerializeField] private Color markerColor = new Color(1f, 0.12f, 0.10f, 1f); // 적 위치 표식 색상
        [SerializeField] private Color ringColor = new Color(1f, 0.28f, 0.24f, 0.46f); // 적 주변 기본 고리 색상
        [SerializeField] private Color directionColor = new Color(1f, 0.08f, 0.05f, 1f); // 진행 방향 강조 색상

        private MapNavigationUI navigation; // 현재 미니맵 UI 참조
        private float nextNavigationSearch; // 다음 미니맵 검색 시각

        private void Awake() // 초기 미니맵 참조 준비
        {
            ResolveNavigation(); // 현재 씬 미니맵 검색
        }

        private void OnEnable() // 재활성화 시 미니맵 참조 복구
        {
            ResolveNavigation(); // 현재 씬 미니맵 검색
        }

        private void Update() // 씬 전환과 생성 순서 차이 보정
        {
            if (navigation != null) // 현재 미니맵 참조 확인
            {
                return; // 정상 참조 유지
            }

            if (Time.unscaledTime < nextNavigationSearch) // 다음 검색 시각 확인
            {
                return; // 검색 간격 유지
            }

            nextNavigationSearch = Time.unscaledTime + 0.5f; // 다음 검색 시각 예약
            ResolveNavigation(); // 새 씬 미니맵 검색
        }

        private void ResolveNavigation() // 활성 미니맵 UI 검색
        {
            MapNavigationUI[] candidates = Object.FindObjectsByType<MapNavigationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전체 지도 UI 검색
            navigation = candidates.Length > 0 ? candidates[0] : null; // 첫 지도 UI 연결
        }

        private void OnGUI() // 기존 미니맵 위에 적 위치와 방향 표시
        {
            if (navigation == null || navigation.World == null || navigation.World.Player == null) // 필수 미니맵 참조 확인
            {
                return; // 표시 중단
            }

            if (!navigation.MinimapVisible || navigation.FullMapOpen) // 미니맵 표시 상태 확인
            {
                return; // 숨김 또는 전체 지도에서 표시 제외
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null || wanted.Stars <= 0) // 수배 상태 확인
            {
                return; // 수배가 없으면 적 표시 숨김
            }

            int previousDepth = GUI.depth; // 기존 GUI 깊이 저장
            GUI.depth = -1000; // 기존 미니맵보다 앞쪽에 표시
            DrawWantedGuards(); // 수배 경비 위치와 방향 출력
            GUI.depth = previousDepth; // 기존 GUI 깊이 복원
        }

        private void DrawWantedGuards() // 미니맵 범위 안 수배 경비 표시
        {
            float size = MapNavigationMath.MiniMapPixelSize(navigation.MinimapSizeLevel, Screen.width, Screen.height); // 실제 미니맵 픽셀 크기 계산
            Rect outer = new Rect(Screen.width - size - 18f, 18f, size, size); // 기존 미니맵 화면 위치 계산
            Vector3 playerPosition = navigation.World.Player.transform.position; // 현재 플레이어 월드 위치 조회
            Vector2 center = new Vector2(playerPosition.x, playerPosition.z); // 미니맵 중심 월드 좌표 생성
            float span = navigation.MiniMapWorldSpan; // 현재 미니맵 표시 거리 조회

            GUI.BeginGroup(outer); // 미니맵 밖 적 표시 클리핑
            Rect local = new Rect(0f, 0f, outer.width, outer.height); // 그룹 내부 지도 좌표 생성
            MapWantedGuardAgent[] guards = Object.FindObjectsByType<MapWantedGuardAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 수배 경비 전체 조회

            foreach (MapWantedGuardAgent guard in guards) // 활성 수배 경비 순회
            {
                if (guard == null || guard.IsDead) // 누락 또는 사망 경비 확인
                {
                    continue; // 다음 경비 처리
                }

                Vector3 guardPosition = guard.transform.position; // 경비 월드 위치 조회
                if (!MapNavigationMath.IsInside(guardPosition, center, span)) // 현재 미니맵 범위 확인
                {
                    continue; // 미니맵 밖 경비 제외
                }

                Vector2 point = MapNavigationMath.WorldToViewport(guardPosition, center, span, local); // 경비 미니맵 좌표 계산
                DrawEnemyMarker(point); // 적 중심 위치 표시
                DrawEnemyRing(point); // 적 주변 기본 고리 표시
                DrawDirectionArc(guard, point); // 현재 진행 방향 고리 강조
            }

            GUI.EndGroup(); // 미니맵 클리핑 종료
        }

        private void DrawEnemyMarker(Vector2 point) // 적 중심 빨간 점 표시
        {
            Rect markerRect = new Rect(point.x - markerSize * 0.5f, point.y - markerSize * 0.5f, markerSize, markerSize); // 적 표식 사각형 계산
            DrawRect(markerRect, markerColor); // 적 위치 표식 출력
        }

        private void DrawEnemyRing(Vector2 point) // 적 주변 전체 고리 표시
        {
            int segments = Mathf.Max(8, ringSegments); // 안전한 고리 분할 수 계산
            float step = 360f / segments; // 각 선분 각도 계산

            for (int i = 0; i < segments; i++) // 원형 고리 선분 순회
            {
                float angleA = i * step; // 현재 선분 시작 각도 계산
                float angleB = (i + 1) * step; // 현재 선분 종료 각도 계산
                Vector2 start = point + DirectionFromAngle(angleA) * ringRadius; // 시작점 계산
                Vector2 end = point + DirectionFromAngle(angleB) * ringRadius; // 종료점 계산
                DrawLine(start, end, ringColor, ringThickness); // 옅은 기본 고리 출력
            }
        }

        private void DrawDirectionArc(MapWantedGuardAgent guard, Vector2 point) // 적 진행 방향 고리 구간 강조
        {
            Vector3 worldForward = guard.transform.forward; // 현재 경비 이동과 시선 방향 조회
            DetectionSensor sensor = guard.Sensor; // 경비 탐지 센서 조회
            if (sensor != null && sensor.VisionSource != null) // 별도 시야 기준 존재 여부 확인
            {
                worldForward = sensor.VisionSource.forward; // 실제 시야 기준 방향 사용
            }

            Vector2 screenDirection = new Vector2(worldForward.x, -worldForward.z); // 월드 방향을 미니맵 방향으로 변환
            if (screenDirection.sqrMagnitude <= 0.0001f) // 유효한 방향 확인
            {
                return; // 방향 강조 생략
            }

            screenDirection.Normalize(); // 방향 벡터 정규화
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
