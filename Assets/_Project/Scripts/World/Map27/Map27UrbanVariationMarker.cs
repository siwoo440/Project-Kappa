using UnityEngine; // 씬 저장용 표식 컴포넌트

namespace ProjectK.Day27 // 27일차 통합 QA 이름 공간
{
    [DisallowMultipleComponent] // 도시 다양화 표식 중복 방지
    public sealed class Map27UrbanVariationMarker : MonoBehaviour // 현재 Map에 Day27 도시 재배치가 적용됐는지 저장
    {
        [SerializeField] private int seed; // 재현 가능한 도시 배치 시드
        [SerializeField] private int variedBlocks; // 재배치한 일반 블록 수
        [SerializeField] private int buildingCount; // 최종 일반 건물 수
        [SerializeField] private int highRiseCount; // 고층 건물 수
        [SerializeField] private int warnings; // 자동 검수 경고 수
        [SerializeField] private string sourceCommit; // 적용 기준 커밋
        [SerializeField] private bool completed; // 적용 완료 여부

        public int Seed => seed; // 배치 시드 조회
        public int VariedBlocks => variedBlocks; // 재배치 블록 수 조회
        public int BuildingCount => buildingCount; // 건물 수 조회
        public int HighRiseCount => highRiseCount; // 고층 수 조회
        public int Warnings => warnings; // 검수 경고 수 조회
        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public bool Completed => completed; // 적용 완료 여부 조회

        public void Configure(int newSeed, int blocks, int buildings, int highRises, int warningCount, string commit) // 에디터 자동 배치 결과 저장
        {
            seed = newSeed; // 사용한 시드 저장
            variedBlocks = blocks; // 재배치 블록 수 저장
            buildingCount = buildings; // 최종 건물 수 저장
            highRiseCount = highRises; // 고층 건물 수 저장
            warnings = warningCount; // 검수 경고 수 저장
            sourceCommit = commit; // 기준 커밋 저장
            completed = true; // 적용 완료 표시
        }
    }

    [DisallowMultipleComponent] // 건물 태그 중복 방지
    public sealed class Map27BuildingTag : MonoBehaviour // Day27에서 재배치한 개별 건물 정보
    {
        [SerializeField] private int blockX; // 도시 블록 X 번호
        [SerializeField] private int blockZ; // 도시 블록 Z 번호
        [SerializeField] private int buildingIndex; // 블록 안 건물 순번
        [SerializeField] private float targetHeight; // 목표 건물 높이
        [SerializeField] private bool highRise; // 고층 분류 여부

        public int BlockX => blockX; // 블록 X 조회
        public int BlockZ => blockZ; // 블록 Z 조회
        public int BuildingIndex => buildingIndex; // 건물 순번 조회
        public float TargetHeight => targetHeight; // 목표 높이 조회
        public bool HighRise => highRise; // 고층 여부 조회

        public void Configure(int x, int z, int index, float height, bool isHighRise) // 에디터 배치 정보 연결
        {
            blockX = x; // 블록 X 저장
            blockZ = z; // 블록 Z 저장
            buildingIndex = index; // 순번 저장
            targetHeight = height; // 목표 높이 저장
            highRise = isHighRise; // 고층 여부 저장
        }
    }
}
