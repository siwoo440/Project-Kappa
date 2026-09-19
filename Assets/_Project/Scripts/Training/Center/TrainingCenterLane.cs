using UnityEngine; // 사격 기준점과 실제 거리 연결

public sealed class TrainingCenterLane : MonoBehaviour // 사격선의 물리 검증 자료
{
    [SerializeField] private Transform firingPoint; // 화면 사격 기준점
    [SerializeField] private TrainingReactiveTarget target; // 해당 사격선 표적
    [SerializeField] private float distance; // 수평 기준 사격 거리
    public Transform FiringPoint => firingPoint; // 검증용 출발점
    public TrainingReactiveTarget Target => target; // 검증용 목표
    public float Distance => distance; // 표시 거리 조회

    public void Configure(Transform origin, TrainingReactiveTarget destination, float meters) // 편집기 사격선 연결
    {
        firingPoint = origin; // 기준점 저장
        target = destination; // 표적 저장
        distance = meters; // 설정 거리 저장
    }
}
