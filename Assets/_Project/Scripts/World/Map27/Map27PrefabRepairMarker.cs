using UnityEngine; // Day27 Prefab 복구 적용 상태 저장

namespace ProjectK.Day27 // 27일차 Prefab 도시 이름 공간
{
    [DisallowMultipleComponent] // 복구 표식 중복 방지
    public sealed class Map27PrefabRepairMarker : MonoBehaviour // 최신 Prefab 본체 복구 적용 버전 저장
    {
        [SerializeField] private int revision; // 복구 적용 버전
        [SerializeField] private string sourceCommit; // 적용 기준 커밋
        [SerializeField] private int buildingPrefabCount; // 새 V2 건물 Prefab 수
        [SerializeField] private int buildingInstanceCount; // 새 V2 건물 인스턴스 수
        [SerializeField] private int removedDetailVisualCount; // 제거한 옛 DetailVisuals 수
        [SerializeField] private int roadMarkingInstanceCount; // 새 도로 표시 Prefab 인스턴스 수
        [SerializeField] private int validationWarnings; // 자동 검수 경고 수

        public int Revision => revision; // 현재 복구 버전 조회
        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public int BuildingPrefabCount => buildingPrefabCount; // 건물 Prefab 수 조회
        public int BuildingInstanceCount => buildingInstanceCount; // 건물 인스턴스 수 조회
        public int RemovedDetailVisualCount => removedDetailVisualCount; // 제거 장식 수 조회
        public int RoadMarkingInstanceCount => roadMarkingInstanceCount; // 도로 표시 수 조회
        public int ValidationWarnings => validationWarnings; // 경고 수 조회

        public void Configure(int newRevision, string commit, int prefabs, int buildings, int removedDetails, int roadMarkings, int warnings) // 에디터 복구 결과 저장
        {
            revision = Mathf.Max(0, newRevision); // 안전한 버전 저장
            sourceCommit = commit; // 기준 커밋 저장
            buildingPrefabCount = Mathf.Max(0, prefabs); // 건물 Prefab 수 저장
            buildingInstanceCount = Mathf.Max(0, buildings); // 건물 인스턴스 수 저장
            removedDetailVisualCount = Mathf.Max(0, removedDetails); // 제거 장식 수 저장
            roadMarkingInstanceCount = Mathf.Max(0, roadMarkings); // 도로 표시 수 저장
            validationWarnings = Mathf.Max(0, warnings); // 검수 경고 저장
        }
    }
}
