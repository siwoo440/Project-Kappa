using UnityEngine; // 유니티 기본 기능

public sealed class EquipmentTrainingStation : MonoBehaviour, IInteractable // 훈련장 전용 보급대
{
    public string InteractionLabel => "훈련용 장비 보충 / 체력 회복"; // 보급 안내 이름

    public bool CanInteract(GameObject interactor) // 보급 가능 상태 확인
    {
        if (interactor == null) // 상호작용 대상 확인
        {
            return false; // 대상 없는 보급 차단
        }

        PlayerHealth health = interactor.GetComponent<PlayerHealth>(); // 플레이어 생존 상태 조회
        PlayerEquipmentManager equipment = interactor.GetComponent<PlayerEquipmentManager>(); // 장비 관리자 조회
        return health != null && !health.IsDead && equipment != null && equipment.CanUseEquipment(); // 살아 있는 일반 행동 중 보급 허용
    }

    public void Interact(GameObject interactor) // 보급대 사용
    {
        if (!CanInteract(interactor)) // 보급 조건 재확인
        {
            return; // 비정상 보급 방지
        }

        PlayerHealth health = interactor.GetComponent<PlayerHealth>(); // 회복 대상 조회
        SupportEquipmentController support = interactor.GetComponent<SupportEquipmentController>(); // 보조장비 조회
        ConsumableController consumables = interactor.GetComponent<ConsumableController>(); // 소모품 조회
        health.Heal(health.MaxHealth); // 훈련용 체력 회복
        if (support != null) // 보조장비 보유 확인
        {
            support.Refill(); // 마비침 보충
        }

        if (consumables != null) // 소모품 보유 확인
        {
            consumables.Refill(); // 테스트 소모품 보충
        }

        interactor.GetComponent<PlayerEquipmentManager>().Notify("훈련용 장비 보충 완료"); // 보급 완료 안내
    }
}
