using ProjectK.Day16; // 기존 본편 월드 참조
using UnityEngine; // 유니티 기본 기능

namespace ProjectK.Day17 // 17일차 도시 디테일 전용 이름 공간
{
    [DisallowMultipleComponent] // 디테일 표식 중복 방지
    public sealed class MapCyberpunkDetailMarker : MonoBehaviour // 사이버펑크 디테일 설치 결과
    {
        public const string RootName = "Day17_CyberpunkCityDetail"; // 반복 설치 기준 이름
        [SerializeField] private MapWorldRoot world; // 연결된 본편 월드
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private int neonSignCount; // 생성한 네온 간판 수
        [SerializeField] private int propCount; // 생성한 세부 소품 수
        [SerializeField] private int lightCount; // 생성한 실제 광원 수
        [SerializeField] private int clusterCount; // 거리 표시 묶음 수
        [SerializeField] private bool completed; // 구성 완료 여부
        public MapWorldRoot World => world; // 검사 메뉴용 월드 참조
        public string SourceCommit => sourceCommit; // 제작 기준 조회
        public int NeonSignCount => neonSignCount; // 네온 간판 수 조회
        public int PropCount => propCount; // 세부 소품 수 조회
        public int LightCount => lightCount; // 실제 광원 수 조회
        public int ClusterCount => clusterCount; // 표시 묶음 수 조회
        public bool Completed => completed; // 구성 완료 상태 조회

        public void Configure(MapWorldRoot owner, string commit, int signs, int props, int lights, int clusters) // 편집기 생성 결과 저장
        {
            world = owner; // 본편 월드 연결
            sourceCommit = commit; // 기준 커밋 저장
            neonSignCount = Mathf.Max(0, signs); // 음수 간판 수 방지
            propCount = Mathf.Max(0, props); // 음수 소품 수 방지
            lightCount = Mathf.Max(0, lights); // 음수 광원 수 방지
            clusterCount = Mathf.Max(0, clusters); // 음수 묶음 수 방지
            completed = true; // 정상 구성 완료 표시
        }
    }
}
