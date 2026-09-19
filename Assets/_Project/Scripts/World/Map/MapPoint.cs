using UnityEngine; // 월드 장소 참조

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    [DisallowMultipleComponent] // 장소 표식 중복 방지
    public sealed class MapPoint : MonoBehaviour // 미션과 분리된 도시 방문 지점
    {
        [SerializeField] private string placeId; // 향후 배치 연결용 고정 식별자
        [SerializeField] private string displayName; // 장소의 한글 이름
        [SerializeField] private Transform arrival; // 안전한 실제 도착점
        public string PlaceId => placeId; // 식별자 조회
        public string DisplayName => displayName; // 장소 이름 조회
        public Transform Arrival => arrival; // 이동 지점 조회

        public void Configure(string id, string label, Transform point) // 에디터에서 장소 연결
        {
            placeId = id; // 고정된 이름 저장
            displayName = label; // 한글 안내 저장
            arrival = point; // 지면과 분리된 도착점 저장
        }
    }
}
