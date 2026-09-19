using UnityEngine; // 훈련장 구성 기록

[DisallowMultipleComponent] // 훈련장 기록 중복 방지
public sealed class TrainingCampusMarker : MonoBehaviour // 재실행 시 배치를 보존하는 표식
{
    [SerializeField] private Vector3 originalSpawn; // 이동하지 않은 플레이어 시작점
    [SerializeField] private int relocatedActors; // 이동한 실제 적 수
    [SerializeField] private bool completed; // 최초 배치 완료 상태
    public Vector3 OriginalSpawn => originalSpawn; // 안전거리 검사 기준
    public int RelocatedActors => relocatedActors; // 배치된 적 수 조회
    public bool Completed => completed; // 완료된 배치 여부
    public void Configure(Vector3 spawn, int count) // 설치 완료 기록
    {
        originalSpawn = spawn; // 스폰 기준 저장
        relocatedActors = count; // 실제 이동 결과 저장
        completed = true; // 중복 생성 방지 상태
    }
}
