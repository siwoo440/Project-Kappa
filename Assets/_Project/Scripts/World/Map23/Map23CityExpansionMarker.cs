using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 설치 결과 직렬화

namespace ProjectK.Day23 // 23일차 도시 확장 이름 공간
{
    [DisallowMultipleComponent] // 설치 표식 중복 방지
    public sealed class Map23CityExpansionMarker : MonoBehaviour // 23일차 도시 확장 결과 기록
    {
        [SerializeField] private MapWorldRoot world; // 연결된 본편 월드
        [SerializeField] private Terrain undergroundTerrain; // 새 지하 전용 Terrain
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private int highRiseCount; // 추가 고층 건물 수
        [SerializeField] private int alleyCount; // 보강 골목 수
        [SerializeField] private int subwayEntranceCount; // 지하철 출입구 수
        [SerializeField] private int subwayPropCount; // 지하 공간 소품 수
        [SerializeField] private bool physicalStairAccess; // 실제 계단 연결 여부
        [SerializeField] private bool completed; // 설치 완료 여부

        public bool Completed => completed; // 설치 완료 상태 조회
        public Terrain UndergroundTerrain => undergroundTerrain; // 지하 Terrain 조회
        public int HighRiseCount => highRiseCount; // 고층 건물 수 조회
        public int AlleyCount => alleyCount; // 골목 수 조회
        public int SubwayEntranceCount => subwayEntranceCount; // 지하철 출입구 수 조회
        public int SubwayPropCount => subwayPropCount; // 지하 소품 수 조회
        public bool PhysicalStairAccess => physicalStairAccess; // 실제 계단 연결 상태 조회

        public void Configure(MapWorldRoot owner, Terrain subwayTerrain, string commit, int towers, int alleys, int entrances, int subwayProps, bool stairsConnected) // 설치 결과 저장
        {
            world = owner; // 본편 월드 저장
            undergroundTerrain = subwayTerrain; // 지하 Terrain 저장
            sourceCommit = commit; // 기준 커밋 저장
            highRiseCount = Mathf.Max(0, towers); // 고층 건물 수 저장
            alleyCount = Mathf.Max(0, alleys); // 골목 수 저장
            subwayEntranceCount = Mathf.Max(0, entrances); // 출입구 수 저장
            subwayPropCount = Mathf.Max(0, subwayProps); // 지하 소품 수 저장
            physicalStairAccess = stairsConnected; // 실제 계단 연결 결과 저장
            completed = true; // 설치 완료 표시
        }
    }
}
