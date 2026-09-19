using ProjectK.Day16; // 기존 본편 월드 참조
using UnityEngine; // 유니티 기본 기능

namespace ProjectK.Day18 // 18일차 도시 밀도 전용 이름 공간
{
    [DisallowMultipleComponent] // 밀도 표식 중복 방지
    public sealed class MapUrbanDensityMarker : MonoBehaviour // 도시 밀도 보강 설치 결과
    {
        public const string RootName = "Day18_UrbanDensityLayer"; // 반복 설치 기준 이름
        [SerializeField] private MapWorldRoot world; // 연결된 본편 월드
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private int storefrontCount; // 생성한 상점 전면 수
        [SerializeField] private int vehicleCount; // 생성한 차량과 바이크 수
        [SerializeField] private int streetPropCount; // 거리 생활 소품 수
        [SerializeField] private int rooftopCount; // 옥상 증축과 설비 묶음 수
        [SerializeField] private int bridgeCount; // 공중 연결 구조 수
        [SerializeField] private int cableCount; // 공중 케이블 묶음 수
        [SerializeField] private int neonSignCount; // 추가 네온 간판 수
        [SerializeField] private int clusterCount; // 거리 표시 묶음 수
        [SerializeField] private bool completed; // 구성 완료 여부
        public MapWorldRoot World => world; // 검사 메뉴용 월드 참조
        public string SourceCommit => sourceCommit; // 제작 기준 조회
        public int StorefrontCount => storefrontCount; // 상점 전면 수 조회
        public int VehicleCount => vehicleCount; // 차량 수 조회
        public int StreetPropCount => streetPropCount; // 거리 소품 수 조회
        public int RooftopCount => rooftopCount; // 옥상 구조 수 조회
        public int BridgeCount => bridgeCount; // 연결 구조 수 조회
        public int CableCount => cableCount; // 케이블 수 조회
        public int NeonSignCount => neonSignCount; // 네온 간판 수 조회
        public int ClusterCount => clusterCount; // 거리 묶음 수 조회
        public bool Completed => completed; // 구성 완료 상태 조회

        public void Configure(MapWorldRoot owner, string commit, int storefronts, int vehicles, int streetProps, int rooftops, int bridges, int cables, int signs, int clusters) // 편집기 생성 결과 저장
        {
            world = owner; // 본편 월드 연결
            sourceCommit = commit; // 기준 커밋 저장
            storefrontCount = Mathf.Max(0, storefronts); // 상점 집계 보정
            vehicleCount = Mathf.Max(0, vehicles); // 차량 집계 보정
            streetPropCount = Mathf.Max(0, streetProps); // 소품 집계 보정
            rooftopCount = Mathf.Max(0, rooftops); // 옥상 집계 보정
            bridgeCount = Mathf.Max(0, bridges); // 연결 구조 집계 보정
            cableCount = Mathf.Max(0, cables); // 케이블 집계 보정
            neonSignCount = Mathf.Max(0, signs); // 간판 집계 보정
            clusterCount = Mathf.Max(0, clusters); // 묶음 집계 보정
            completed = true; // 정상 구성 완료 표시
        }
    }
}
