using UnityEngine; // 임무 상호작용 오브젝트

namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    [DisallowMultipleComponent] // 임무 상호작용 중복 방지
    public sealed class Map31MissionInteractable : MonoBehaviour, IInteractable // 기존 F 상호작용 시스템과 연결되는 임무 목표
    {
        [SerializeField] private string missionId; // 연결 임무 ID
        [SerializeField] private string objectiveId; // 연결 목표 ID
        [SerializeField] private string interactionLabel = "임무 대상 확인"; // F 안내 문구
        [SerializeField] private string grantItemId; // 상호작용 완료 시 지급 물품
        [SerializeField] private string requiredItemId; // 상호작용에 필요한 물품
        [SerializeField] private string deliverItemId; // 상호작용 시 인계할 물품

        public string InteractionLabel => interactionLabel; // 기존 PlayerInteraction 표시 문구

        public void Configure(string newMissionId, string newObjectiveId, string label, string grantItem = null, string requiredItem = null, string deliverItem = null) // 런타임 임무 단말기 구성
        {
            missionId = newMissionId ?? string.Empty; // 임무 ID 저장
            objectiveId = newObjectiveId ?? string.Empty; // 목표 ID 저장
            interactionLabel = string.IsNullOrWhiteSpace(label) ? "임무 대상 확인" : label; // F 안내 문구 저장
            grantItemId = grantItem ?? string.Empty; // 지급 물품 저장
            requiredItemId = requiredItem ?? string.Empty; // 필요 물품 저장
            deliverItemId = deliverItem ?? string.Empty; // 인계 물품 저장
        }

        public bool CanInteract(GameObject interactor) // 현재 목표일 때만 F 상호작용 허용
        {
            Map31MissionManager manager = Map31MissionManager.Instance; // 현재 MissionManager 조회
            return manager != null && manager.CanInteractObjective(missionId, objectiveId, requiredItemId); // 실제 임무 상태와 물품 조건 확인
        }

        public void Interact(GameObject interactor) // F 입력으로 현재 임무 목표 완료 요청
        {
            Map31MissionManager manager = Map31MissionManager.Instance; // 현재 MissionManager 조회
            if (manager == null) // MissionManager 존재 확인
            {
                return; // 상호작용 중단
            }

            manager.TryInteractObjective(missionId, objectiveId, interactor, grantItemId, requiredItemId, deliverItemId); // 공통 MissionManager에 상호작용 전달
        }
    }
}
