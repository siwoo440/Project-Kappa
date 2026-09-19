using ProjectK.Day16; // 기존 본편 월드 참조
using UnityEngine; // 거리 표시 기능

namespace ProjectK.Day17 // 17일차 도시 디테일 전용 이름 공간
{
    [DisallowMultipleComponent] // 거리 묶음 중복 방지
    public sealed class MapDetailCluster : MonoBehaviour // 먼 장식 묶음의 간단한 표시 관리
    {
        [SerializeField] private MapWorldRoot world; // 플레이어를 가진 본편 월드
        [SerializeField] private GameObject content; // 실제 표시를 끌 장식 자식
        [SerializeField, Min(40f)] private float visibleDistance = 420f; // 표시 유지 거리
        [SerializeField, Min(0.1f)] private float checkInterval = 0.45f; // 거리 확인 간격
        private float nextCheck; // 다음 거리 검사 시각
        public GameObject Content => content; // 검증용 표시 자식
        public float VisibleDistance => visibleDistance; // 검증용 표시 거리
        public MapWorldRoot World => world; // 검증용 월드 참조

        public void Configure(MapWorldRoot owner, GameObject target, float distance) // 편집기에서 묶음 연결
        {
            world = owner; // 본편 월드 저장
            content = target; // 표시 자식 저장
            visibleDistance = Mathf.Max(40f, distance); // 최소 표시 거리 보장
        }

        private void Start() // 첫 프레임 표시 상태 적용
        {
            Refresh(true); // 즉시 거리 검사 실행
        }

        private void Update() // 저빈도 거리 검사
        {
            if (Time.unscaledTime < nextCheck) // 검사 간격 이전 확인
            {
                return; // 반복 계산 생략
            }
            nextCheck = Time.unscaledTime + checkInterval; // 다음 검사 시각 지정
            Refresh(false); // 일반 거리 검사 실행
        }

        private void Refresh(bool force) // 플레이어 거리로 장식 표시 결정
        {
            if (world == null || world.Player == null || content == null) // 필수 참조 확인
            {
                return; // 불완전한 묶음 중단
            }
            float distance = Vector3.Distance(world.Player.transform.position, transform.position); // 플레이어와 묶음 거리 계산
            bool visible = distance <= visibleDistance; // 표시 거리 안 여부 계산
            if (force || content.activeSelf != visible) // 실제 상태 변화 확인
            {
                content.SetActive(visible); // 장식 자식만 활성 상태 변경
            }
        }

        private void OnDisable() // 편집과 재시작을 위한 표시 복구
        {
            if (content != null) // 표시 자식 존재 확인
            {
                content.SetActive(true); // 다음 실행의 숨김 잔류 방지
            }
        }
    }
}
