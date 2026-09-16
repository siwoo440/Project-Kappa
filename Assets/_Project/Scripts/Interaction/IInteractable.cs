using UnityEngine; // 유니티 기본 기능

public interface IInteractable // 공통 상호작용 규칙
{
    string InteractionLabel { get; } // 상호작용 표시 이름
    bool CanInteract(GameObject interactor); // 상호작용 가능 여부 확인
    void Interact(GameObject interactor); // 상호작용 실행
}
