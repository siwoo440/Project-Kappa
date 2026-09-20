using System.Collections.Generic; // 계단·수색 지점 목록
using ProjectK.Day16; // 지상 기본 높이 참조
using ProjectK.Day23; // 지하 Terrain 설치 결과 참조
using UnityEngine; // 월드 좌표와 런타임 검색

namespace ProjectK.Day24 // 24일차 지상·지하 이동 이름 공간
{
    public enum Map24WorldLayer // 플레이 공간 층 구분
    {
        Surface, // 지상 층
        Underground // 지하 층
    }

    [DisallowMultipleComponent] // 층 관리자 중복 방지
    public sealed class Map24LayerNavigation : MonoBehaviour // 지상·지하 판정과 계단 경로 제공
    {
        private static Map24LayerNavigation instance; // 현재 층 관리자 인스턴스
        private readonly List<Transform> stairSteps = new List<Transform>(); // 지상에서 지하 순서의 계단 지점
        private readonly List<Vector3> undergroundSearchPoints = new List<Vector3>(); // 지하 분산 수색 지점
        private Map23CityExpansionMarker marker; // Day23 지하 설치 표식
        private Terrain undergroundTerrain; // 실제 지하 Terrain
        private Transform stairRoot; // 실제 지하철 계단 루트
        private Vector3 surfaceAnchor; // 계단 지상 시작 위치
        private Vector3 undergroundAnchor; // 계단 지하 종료 위치
        private Bounds undergroundBounds; // 지하 Terrain XZ 범위
        private float layerSplitY = MapTerrainMath.Ground - 5f; // 지상·지하 판정 기준 높이
        private float nextResolveTime; // 다음 씬 참조 복구 시각
        private bool ready; // 지하 연결 준비 여부

        public static Map24LayerNavigation Instance => instance; // 현재 층 관리자 조회
        public bool Ready => ready; // 계단·Terrain 준비 상태 조회
        public Terrain UndergroundTerrain => undergroundTerrain; // 지하 Terrain 조회
        public IReadOnlyList<Transform> StairSteps => stairSteps; // 계단 순서 목록 조회
        public Vector3 SurfaceAnchor => surfaceAnchor; // 지상 계단 입구 조회
        public Vector3 UndergroundAnchor => undergroundAnchor; // 지하 계단 출구 조회
        public float LayerSplitY => layerSplitY; // 층 판정 높이 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 시 정적 상태 초기화
        {
            instance = null; // 이전 플레이 인스턴스 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 층 관리자 생성
        {
            if (instance != null) // 기존 인스턴스 존재 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day24] Layer Navigation"); // 자동 층 관리자 오브젝트 생성
            instance = owner.AddComponent<Map24LayerNavigation>(); // 층 관리자 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 뒤에도 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 인스턴스 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 인스턴스 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 씬 참조 연결
        {
            ResolveSceneReferences(); // Day23 지하 구조 즉시 검색
        }

        private void Update() // 생성 순서와 씬 전환 참조 복구
        {
            if (ready && marker != null && undergroundTerrain != null) // 정상 참조 유지 여부 확인
            {
                return; // 추가 검색 생략
            }

            if (Time.unscaledTime < nextResolveTime) // 재검색 시각 확인
            {
                return; // 검색 간격 유지
            }

            nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
            ResolveSceneReferences(); // 현재 씬 지하 구조 재검색
        }

        public void ResolveSceneReferences() // Day23 지하 Terrain과 계단 자동 연결
        {
            ready = false; // 새 참조 검사 전 준비 상태 해제
            stairSteps.Clear(); // 이전 계단 지점 제거
            undergroundSearchPoints.Clear(); // 이전 수색 지점 제거

            Map23CityExpansionMarker[] markers = Object.FindObjectsByType<Map23CityExpansionMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None); // Day23 설치 표식 전체 조회
            marker = markers.Length > 0 ? markers[0] : null; // 첫 설치 표식 연결
            undergroundTerrain = marker != null ? marker.UndergroundTerrain : null; // 실제 지하 Terrain 연결

            if (marker == null || undergroundTerrain == null || undergroundTerrain.terrainData == null) // 필수 지하 구조 확인
            {
                return; // 아직 Day23 지하 구조가 없음
            }

            stairRoot = FindChildRecursive(marker.transform, "Subway_MainStair"); // 지상·지하 실제 계단 루트 검색
            if (stairRoot == null) // 계단 존재 확인
            {
                return; // 실제 연결 경로가 없으면 준비 중단
            }

            Transform[] descendants = stairRoot.GetComponentsInChildren<Transform>(true); // 계단 전체 하위 Transform 조회
            foreach (Transform candidate in descendants) // 계단 자식 순회
            {
                if (candidate == null || !candidate.name.StartsWith("Step_")) // 시각 계단 단 이름 확인
                {
                    continue; // 다른 계단 구조 제외
                }

                stairSteps.Add(candidate); // 실제 계단 단을 이동 지점으로 등록
            }

            stairSteps.Sort(CompareStepNames); // Step_00부터 마지막 단까지 순서 정렬
            if (stairSteps.Count < 2) // 최소 계단 지점 확인
            {
                stairSteps.Clear(); // 불완전한 목록 제거
                return; // 연결 준비 중단
            }

            surfaceAnchor = stairSteps[0].position; // 첫 단을 지상 입구로 저장
            undergroundAnchor = stairSteps[stairSteps.Count - 1].position; // 마지막 단을 지하 출구로 저장
            layerSplitY = (surfaceAnchor.y + undergroundAnchor.y) * 0.5f; // 계단 중간 높이를 층 판정 기준으로 저장

            Vector3 terrainPosition = undergroundTerrain.transform.position; // 지하 Terrain 남서 기준 위치 조회
            Vector3 terrainSize = undergroundTerrain.terrainData.size; // 지하 Terrain 실제 크기 조회
            Vector3 boundsCenter = terrainPosition + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f); // Terrain 전체 Bounds 중심 계산
            undergroundBounds = new Bounds(boundsCenter, terrainSize); // 지하 Terrain 월드 범위 저장
            BuildUndergroundSearchPoints(); // 플랫폼·서비스 통로용 분산 수색 지점 생성
            ready = true; // 지상·지하 연결 준비 완료
        }

        public Map24WorldLayer GetLayer(Vector3 worldPosition) // 월드 위치의 지상·지하 층 판정
        {
            if (!ready) // 지하 구조 준비 여부 확인
            {
                return worldPosition.y < MapTerrainMath.Ground - 5f ? Map24WorldLayer.Underground : Map24WorldLayer.Surface; // 기본 높이 기준 대체 판정
            }

            bool insideHorizontal = worldPosition.x >= undergroundBounds.min.x && worldPosition.x <= undergroundBounds.max.x && worldPosition.z >= undergroundBounds.min.z && worldPosition.z <= undergroundBounds.max.z; // 지하 Terrain XZ 범위 확인
            bool belowSplit = worldPosition.y <= layerSplitY; // 계단 중간보다 아래인지 확인
            return insideHorizontal && belowSplit ? Map24WorldLayer.Underground : Map24WorldLayer.Surface; // 범위와 높이 기반 층 반환
        }

        public bool SameLayer(Vector3 first, Vector3 second) // 두 위치가 같은 층인지 확인
        {
            return GetLayer(first) == GetLayer(second); // 층 비교 결과 반환
        }

        public Vector3 GetUndergroundSearchPoint(int guardSeed, int searchIndex) // 지하 경비별 분산 수색 위치 조회
        {
            if (undergroundSearchPoints.Count == 0) // 지하 수색 지점 존재 확인
            {
                return undergroundAnchor; // 계단 하단을 대체 수색점으로 사용
            }

            int safeSeed = Mathf.Abs(guardSeed); // 음수 인스턴스 값 보정
            int index = Mathf.Abs(safeSeed * 5 + searchIndex * 7) % undergroundSearchPoints.Count; // 경비와 수색 순번별 다른 지점 계산
            return undergroundSearchPoints[index]; // 선택된 지하 수색 위치 반환
        }

        public int FindClosestStairStep(Vector3 worldPosition) // 현재 위치와 가장 가까운 계단 단 검색
        {
            if (stairSteps.Count == 0) // 계단 지점 존재 확인
            {
                return -1; // 검색 실패 반환
            }

            int bestIndex = 0; // 최근접 계단 기본 인덱스
            float bestDistance = float.PositiveInfinity; // 최근접 거리 초기화

            for (int i = 0; i < stairSteps.Count; i++) // 모든 계단 단 순회
            {
                Transform step = stairSteps[i]; // 현재 계단 단 조회
                Vector2 delta = new Vector2(step.position.x - worldPosition.x, step.position.z - worldPosition.z); // 수평 거리 계산
                float distance = delta.sqrMagnitude; // 거리 제곱 계산

                if (distance < bestDistance) // 더 가까운 계단 단 확인
                {
                    bestDistance = distance; // 최소 거리 갱신
                    bestIndex = i; // 최근접 인덱스 저장
                }
            }

            return bestIndex; // 최근접 계단 인덱스 반환
        }

        private void BuildUndergroundSearchPoints() // 지하 승강장과 서비스 통로 수색점 생성
        {
            Vector3 terrainPosition = undergroundTerrain.transform.position; // 지하 Terrain 기준 위치 조회
            Vector3 terrainSize = undergroundTerrain.terrainData.size; // 지하 Terrain 크기 조회
            Vector3 center = terrainPosition + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f); // 지하역 중심 XZ 계산
            float floorY = undergroundTerrain.transform.position.y + 0.55f; // 지하 Terrain 표면 위 경비 위치 높이 계산

            undergroundSearchPoints.Add(new Vector3(center.x - 62f, floorY, center.z + 13f)); // 북서 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 30f, floorY, center.z + 13f)); // 북중 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x + 4f, floorY, center.z + 13f)); // 북중앙 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x + 42f, floorY, center.z + 13f)); // 북동 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x + 70f, floorY, center.z + 13f)); // 북동 끝 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 58f, floorY, center.z - 13f)); // 남서 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 22f, floorY, center.z - 13f)); // 남중 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x + 18f, floorY, center.z - 13f)); // 남중앙 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x + 58f, floorY, center.z - 13f)); // 남동 승강장 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 72f, floorY, center.z + 22f)); // 서비스 통로 입구 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 72f, floorY, center.z + 38f)); // 서비스 통로 중앙 수색점 추가
            undergroundSearchPoints.Add(new Vector3(center.x - 72f, floorY, center.z + 52f)); // 서비스 통로 끝 수색점 추가
            undergroundSearchPoints.Add(undergroundAnchor + Vector3.up * 0.20f); // 계단 하단 감시 지점 추가
        }

        private static int CompareStepNames(Transform first, Transform second) // Step 번호 기반 정렬
        {
            int firstIndex = ParseStepIndex(first != null ? first.name : string.Empty); // 첫 계단 번호 변환
            int secondIndex = ParseStepIndex(second != null ? second.name : string.Empty); // 둘째 계단 번호 변환
            return firstIndex.CompareTo(secondIndex); // 번호 오름차순 결과 반환
        }

        private static int ParseStepIndex(string name) // Step_00 형식에서 숫자 추출
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Step_")) // 올바른 계단 이름 확인
            {
                return int.MaxValue; // 잘못된 이름을 목록 뒤로 이동
            }

            return int.TryParse(name.Substring(5), out int result) ? result : int.MaxValue; // 숫자 변환 결과 반환
        }

        private static Transform FindChildRecursive(Transform root, string targetName) // 하위 계층 이름 검색
        {
            if (root == null) // 검색 루트 확인
            {
                return null; // 검색 실패 반환
            }

            if (root.name == targetName) // 현재 이름 확인
            {
                return root; // 일치 Transform 반환
            }

            for (int i = 0; i < root.childCount; i++) // 모든 자식 순회
            {
                Transform found = FindChildRecursive(root.GetChild(i), targetName); // 하위 계층 재귀 검색
                if (found != null) // 검색 성공 확인
                {
                    return found; // 첫 검색 결과 반환
                }
            }

            return null; // 전체 검색 실패 반환
        }
    }
}
