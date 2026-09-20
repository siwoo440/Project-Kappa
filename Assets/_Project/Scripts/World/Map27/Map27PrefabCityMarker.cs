using UnityEngine; // Prefab 도시 자료형과 씬 표식

namespace ProjectK.Day27 // 27일차 Prefab 도시 이름 공간
{
    public enum Map27PrefabElementKind // 반복 도시 Prefab 종류
    {
        Building, // 일반 건물
        StreetLamp, // Day16 도로 가로등
        RoadStreetLight, // Day23 추가 가로등
        ServiceFurniture, // 블록 서비스 설비 묶음
        VendingMachine, // 자동판매기
        Dumpster, // 쓰레기 수거함
        UtilityPole, // 전봇대
        Bollard, // 보행 경계봉
        Landmark, // 주요 랜드마크
        Other // 기타 반복 요소
    }

    public enum Map27BuildingClass // 건물 실루엣 분류
    {
        None, // 건물 아님
        Low, // 저층
        Mid, // 중층
        High, // 고층
        Tower // 초고층
    }

    [DisallowMultipleComponent] // Prefab 정보 중복 방지
    public sealed class Map27PrefabElement : MonoBehaviour // 생성된 Prefab의 크기와 분류 정보
    {
        [SerializeField] private Map27PrefabElementKind kind; // 도시 요소 종류
        [SerializeField] private Map27BuildingClass buildingClass; // 건물 높이 분류
        [SerializeField] private float width; // 실제 배치 폭
        [SerializeField] private float depth; // 실제 배치 깊이
        [SerializeField] private float height; // 실제 높이
        [SerializeField] private string sourceName; // 원본 씬 오브젝트 이름

        public Map27PrefabElementKind Kind => kind; // 요소 종류 조회
        public Map27BuildingClass BuildingClass => buildingClass; // 건물 분류 조회
        public float Width => width; // 배치 폭 조회
        public float Depth => depth; // 배치 깊이 조회
        public float Height => height; // 실제 높이 조회
        public string SourceName => sourceName; // 원본 이름 조회

        public void Configure(Map27PrefabElementKind elementKind, Map27BuildingClass classification, float elementWidth, float elementDepth, float elementHeight, string originalName) // 에디터 생성 자료 저장
        {
            kind = elementKind; // 요소 종류 저장
            buildingClass = classification; // 건물 분류 저장
            width = Mathf.Max(0.1f, elementWidth); // 안전한 폭 저장
            depth = Mathf.Max(0.1f, elementDepth); // 안전한 깊이 저장
            height = Mathf.Max(0.1f, elementHeight); // 안전한 높이 저장
            sourceName = originalName; // 원본 이름 저장
        }
    }

    [DisallowMultipleComponent] // Prefab 도시 적용 표식 중복 방지
    public sealed class Map27PrefabCityMarker : MonoBehaviour // Map 전체 Prefab 전환 완료 상태 저장
    {
        [SerializeField] private string sourceCommit; // 적용 기준 커밋
        [SerializeField] private int buildingPrefabCount; // 생성 건물 Prefab 수
        [SerializeField] private int propPrefabCount; // 생성 소품 Prefab 수
        [SerializeField] private int landmarkPrefabCount; // 연결 랜드마크 Prefab 수
        [SerializeField] private int buildingInstanceCount; // 최종 건물 인스턴스 수
        [SerializeField] private int propInstanceCount; // 최종 소품 인스턴스 수
        [SerializeField] private int validationWarnings; // 자동 검수 경고 수
        [SerializeField] private bool completed; // 적용 완료 여부

        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public int BuildingPrefabCount => buildingPrefabCount; // 건물 Prefab 수 조회
        public int PropPrefabCount => propPrefabCount; // 소품 Prefab 수 조회
        public int LandmarkPrefabCount => landmarkPrefabCount; // 랜드마크 Prefab 수 조회
        public int BuildingInstanceCount => buildingInstanceCount; // 건물 인스턴스 수 조회
        public int PropInstanceCount => propInstanceCount; // 소품 인스턴스 수 조회
        public int ValidationWarnings => validationWarnings; // 경고 수 조회
        public bool Completed => completed; // 적용 완료 상태 조회

        public void Configure(string commit, int buildingPrefabs, int propPrefabs, int landmarkPrefabs, int buildings, int props, int warnings) // 에디터 적용 결과 저장
        {
            sourceCommit = commit; // 적용 기준 저장
            buildingPrefabCount = Mathf.Max(0, buildingPrefabs); // 건물 Prefab 집계 저장
            propPrefabCount = Mathf.Max(0, propPrefabs); // 소품 Prefab 집계 저장
            landmarkPrefabCount = Mathf.Max(0, landmarkPrefabs); // 랜드마크 Prefab 집계 저장
            buildingInstanceCount = Mathf.Max(0, buildings); // 건물 인스턴스 집계 저장
            propInstanceCount = Mathf.Max(0, props); // 소품 인스턴스 집계 저장
            validationWarnings = Mathf.Max(0, warnings); // 검수 경고 저장
            completed = true; // 적용 완료 표시
        }
    }
}
