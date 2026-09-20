using UnityEngine; // Day27 도시 복구 결과 저장

namespace ProjectK.Day27 // 27일차 통합 QA 이름 공간
{
    [DisallowMultipleComponent] // 복구 표식 중복 방지
    public sealed class Map27CityRecoveryMarker : MonoBehaviour // V3 도시 복구 적용 상태 저장
    {
        [SerializeField] private int revision; // 복구 버전
        [SerializeField] private string sourceCommit; // 적용 기준 커밋
        [SerializeField] private int repairedBlocks; // 복구한 일반 블록 수
        [SerializeField] private int buildingInstances; // 생성한 건물 인스턴스 수
        [SerializeField] private int buildingPrefabs; // 생성한 V3 건물 Prefab 수
        [SerializeField] private int removedLegacyDetails; // 제거한 구형 장식 묶음 수
        [SerializeField] private int validationWarnings; // 자동 검수 경고 수

        public int Revision => revision; // 복구 버전 조회
        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public int RepairedBlocks => repairedBlocks; // 복구 블록 수 조회
        public int BuildingInstances => buildingInstances; // 건물 인스턴스 수 조회
        public int BuildingPrefabs => buildingPrefabs; // 건물 Prefab 수 조회
        public int RemovedLegacyDetails => removedLegacyDetails; // 구형 장식 제거 수 조회
        public int ValidationWarnings => validationWarnings; // 경고 수 조회

        public void Configure(int newRevision, string commit, int blocks, int instances, int prefabs, int removedDetails, int warnings) // 에디터 복구 결과 연결
        {
            revision = Mathf.Max(0, newRevision); // 버전 저장
            sourceCommit = commit; // 기준 커밋 저장
            repairedBlocks = Mathf.Max(0, blocks); // 블록 수 저장
            buildingInstances = Mathf.Max(0, instances); // 인스턴스 수 저장
            buildingPrefabs = Mathf.Max(0, prefabs); // Prefab 수 저장
            removedLegacyDetails = Mathf.Max(0, removedDetails); // 제거 장식 수 저장
            validationWarnings = Mathf.Max(0, warnings); // 경고 수 저장
        }
    }
}
