using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 유니티 기본 기능

namespace ProjectK.Day18 // 18일차 거리 가독성 보정 이름 공간
{
    [DisallowMultipleComponent] // 표식 중복 방지
    public sealed class MapUrbanStreetPolishMarker : MonoBehaviour // 간판·전봇대·보도 보정 결과
    {
        public const string RootName = "Day18_StreetPolishLayer"; // 현재 일차 기준 보정 루트 이름
        [SerializeField] private MapWorldRoot world; // 연결된 본편 월드
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private int fixedSignCount; // 크기와 깊이를 수정한 간판 수
        [SerializeField] private int poleCount; // 추가 전봇대 수
        [SerializeField] private int sidewalkCount; // 추가 보도 구간 수
        [SerializeField] private int lightCount; // 추가 거리 광원 수
        [SerializeField] private int clusterCount; // 거리 표시 묶음 수
        [SerializeField] private bool completed; // 완료 상태
        public MapWorldRoot World => world; // 월드 참조 조회
        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public int FixedSignCount => fixedSignCount; // 수정 간판 수 조회
        public int PoleCount => poleCount; // 전봇대 수 조회
        public int SidewalkCount => sidewalkCount; // 보도 수 조회
        public int LightCount => lightCount; // 광원 수 조회
        public int ClusterCount => clusterCount; // 묶음 수 조회
        public bool Completed => completed; // 완료 상태 조회

        public void Configure(MapWorldRoot owner, string commit, int signs, int poles, int sidewalks, int lights, int clusters) // 생성 결과 저장
        {
            world = owner; // 본편 월드 연결
            sourceCommit = commit; // 기준 커밋 저장
            fixedSignCount = Mathf.Max(0, signs); // 간판 집계 보정
            poleCount = Mathf.Max(0, poles); // 전봇대 집계 보정
            sidewalkCount = Mathf.Max(0, sidewalks); // 보도 집계 보정
            lightCount = Mathf.Max(0, lights); // 광원 집계 보정
            clusterCount = Mathf.Max(0, clusters); // 묶음 집계 보정
            completed = true; // 완료 상태 저장
        }
    }
}
