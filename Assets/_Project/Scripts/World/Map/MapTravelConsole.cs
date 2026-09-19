using UnityEngine; // 본편 안내 단말기

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    [DisallowMultipleComponent] // 중복 상호작용 방지
    public sealed class MapTravelConsole : MonoBehaviour, IInteractable // 기존 F키로 도시 지점 이동
    {
        [SerializeField] private MapWorldRoot world; // 본편 월드 연결
        [SerializeField] private MapPoint destination; // 안전한 도착 장소
        public string InteractionLabel => destination != null ? destination.DisplayName + " 이동" : "도시 안내"; // 기존 화면 안내 사용

        public void Configure(MapWorldRoot owner, MapPoint place) // 에디터에서 단말기 연결
        {
            world = owner; // 현재 Map 관리자 저장
            destination = place; // 방문할 장소 저장
        }

        public bool CanInteract(GameObject user) // 기존 전투와 입력 제한 유지
        {
            return world != null && world.Player == user && destination != null && user.GetComponent<PlayerEquipmentManager>() != null && user.GetComponent<PlayerEquipmentManager>().CanUseEquipment(); // 정상 상태에서만 이동 허용
        }

        public void Interact(GameObject user) // F키 사용 결과 적용
        {
            if (CanInteract(user)) // 실행 시점 상태 재확인
            {
                world.Travel(destination, user); // 실제 도착점으로 이동
            }
        }
    }
}
