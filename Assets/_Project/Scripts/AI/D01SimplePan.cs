using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class D01SimplePan : MonoBehaviour // D-01 간단 회전 제어
{
    [SerializeField] private Transform pivot; // 회전 축 참조
    [SerializeField] private float minYaw = -55f; // 최소 회전 각도
    [SerializeField] private float maxYaw = 55f; // 최대 회전 각도
    [SerializeField] private float rotationSpeed = 45f; // 회전 속도

    private float currentYaw; // 현재 회전 각도
    private float direction = 1f; // 현재 회전 방향

    public void Configure(Transform targetPivot, float minimumYaw, float maximumYaw, float speed) // 외부 설정 적용
    {
        pivot = targetPivot; // 회전 축 저장
        minYaw = minimumYaw; // 최소 각도 저장
        maxYaw = maximumYaw; // 최대 각도 저장
        rotationSpeed = speed; // 회전 속도 저장
        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw); // 현재 각도 보정
        ApplyRotation(); // 초기 회전 적용
    }

    private void Awake() // 초기화 처리
    {
        if (pivot == null) // 회전 축 확인
        {
            Transform found = transform.Find("RotationPivot"); // 기본 회전 축 검색
            if (found != null) // 검색 결과 확인
            {
                pivot = found; // 회전 축 저장
            }
        }

        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw); // 초기 각도 보정
        ApplyRotation(); // 초기 회전 적용
    }

    private void Update() // 매 프레임 회전 처리
    {
        if (pivot == null) // 회전 축 확인
        {
            return; // 처리 중단
        }

        currentYaw += direction * rotationSpeed * Time.deltaTime; // 회전 각도 누적

        if (currentYaw >= maxYaw) // 최대 각도 도달 여부 확인
        {
            currentYaw = maxYaw; // 최대 각도 고정
            direction = -1f; // 역방향 전환
        }
        else if (currentYaw <= minYaw) // 최소 각도 도달 여부 확인
        {
            currentYaw = minYaw; // 최소 각도 고정
            direction = 1f; // 정방향 전환
        }

        ApplyRotation(); // 회전 적용
    }

    private void ApplyRotation() // 실제 회전 적용
    {
        if (pivot == null) // 회전 축 확인
        {
            return; // 처리 중단
        }

        pivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f); // 로컬 회전 적용
    }
}
