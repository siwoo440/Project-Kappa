using UnityEngine; // 훈련장 보급과 이동 단말기

[DisallowMultipleComponent] // 중복 단말기 방지
public sealed class TrainingCampusStation : MonoBehaviour, IInteractable // 기존 F 상호작용 재사용
{
    [SerializeField] private Transform targetRoot; // 초기화할 확장 훈련장
    [SerializeField] private Transform travelDestination; // 선택적 이동 도착점
    [SerializeField] private string label = "표적 초기화 · 탄약 보충"; // 안내 이름
    public string InteractionLabel => label; // 기존 HUD에 표시할 문구
    public void Configure(Transform campus, Transform destination, string text) // 에디터 설정 연결
    {
        targetRoot = campus; // 표적 구역 저장
        travelDestination = destination; // 이동 지점 저장
        label = text; // 사용자 안내 저장
    }
    public bool CanInteract(GameObject user) // 특수행동 중 사용 제한
    {
        PlayerEquipmentManager equipment = user != null ? user.GetComponent<PlayerEquipmentManager>() : null; // 사용자 상태 조회
        return equipment != null && equipment.CanUseEquipment(); // 기존 행동 제한 유지
    }
    public void Interact(GameObject user) // 표적 보급 또는 빠른 이동
    {
        if (!CanInteract(user)) // 실행 순간 상태 재확인
        {
            return; // 사용 불가 상태 중단
        }
        PlayerEquipmentManager equipment = user.GetComponent<PlayerEquipmentManager>(); // 검증한 장비 참조
        if (travelDestination != null) // 이동 단말기 여부
        {
            equipment.Firearm?.Interrupt(); // 이동 순간 사격과 조준 중단
            PlayerMovement movement = user.GetComponent<PlayerMovement>(); // 이동 상태 연결
            CharacterController body = user.GetComponent<CharacterController>(); // 충돌체 위치 갱신 준비
            bool enabledBefore = body != null && body.enabled; // 기존 충돌 상태 보존
            if (body != null) // 플레이어 충돌체 확인
            {
                body.enabled = false; // 순간 이동 중 충돌 보정 방지
            }
            user.transform.SetPositionAndRotation(travelDestination.position, travelDestination.rotation); // 지정된 안전 지점으로 이동
            if (movement != null) // 이동 참조 확인
            {
                movement.SetHorizontalVelocity(Vector3.zero); // 이전 수평 속도 제거
                movement.VerticalVelocity = 0f; // 이전 낙하 속도 제거
            }
            if (body != null) // 기존 충돌체 확인
            {
                body.enabled = enabledBefore; // 이전 활성 상태 복구
            }
            Physics.SyncTransforms(); // 도착 위치 충돌 갱신
            equipment.Notify("훈련 구역 이동 완료"); // 한 번만 안내
            return; // 이동과 초기화 동시 실행 금지
        }
        equipment.Firearm?.RefillAll(); // 무기별 훈련 탄약 보충
        equipment.Firearm?.ResetPracticeStatistics(); // 사격 계측 초기화
        user.GetComponent<PlayerHealth>()?.Heal(10000f); // 훈련용 체력 회복
        if (targetRoot != null) // 연결된 표적 구역 확인
        {
            foreach (TrainingReactiveTarget target in targetRoot.GetComponentsInChildren<TrainingReactiveTarget>(true)) // 확장 구역 표적 조회
            {
                target.ResetTarget(); // 위치와 힌지와 기록 초기화
            }
        }
        equipment.Notify("표적 복귀 · 탄약 보충 · 체력 회복 완료"); // 보급 결과 안내
    }
}
