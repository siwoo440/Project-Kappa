using System.Collections.Generic; // 플레이어별 재진입 대기 저장
using UnityEngine; // 트리거와 순간 이동 처리

namespace ProjectK.Day23 // 23일차 도시 확장 이름 공간
{
    [DisallowMultipleComponent] // 포털 중복 방지
    [RequireComponent(typeof(BoxCollider))] // 진입 트리거 필수
    public sealed class Map23SubwayPortal : MonoBehaviour // 지상과 지하 공간을 연결하는 자동 이동 장치
    {
        [SerializeField] private Transform destination; // 이동 도착 지점
        [SerializeField, Min(0.2f)] private float cooldown = 1.2f; // 연속 왕복 방지 시간
        private static readonly Dictionary<int, float> nextUseTime = new Dictionary<int, float>(); // 플레이어별 다음 사용 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 진입 시 정적 상태 초기화
        {
            nextUseTime.Clear(); // 이전 플레이의 포털 대기 제거
        }

        public void Configure(Transform target) // 에디터 생성 시 도착점 연결
        {
            destination = target; // 도착 지점 저장
            BoxCollider trigger = GetComponent<BoxCollider>(); // 포털 충돌체 조회
            trigger.isTrigger = true; // 물리 벽 대신 진입 감지로 설정
        }

        private void OnTriggerEnter(Collider other) // 플레이어 진입 감지
        {
            if (destination == null || other == null) // 필수 참조 확인
            {
                return; // 이동 중단
            }

            PlayerMovement movement = other.GetComponentInParent<PlayerMovement>(); // 플레이어 이동 컴포넌트 조회
            if (movement == null) // 플레이어가 아닌 개체 확인
            {
                return; // 다른 개체 이동 제외
            }

            GameObject player = movement.gameObject; // 실제 플레이어 루트 저장
            int key = player.GetInstanceID(); // 플레이어 식별값 계산
            if (nextUseTime.TryGetValue(key, out float allowedAt) && Time.unscaledTime < allowedAt) // 재진입 대기 확인
            {
                return; // 즉시 왕복 이동 방지
            }

            nextUseTime[key] = Time.unscaledTime + cooldown; // 다음 포털 사용 가능 시각 저장
            CharacterController body = player.GetComponent<CharacterController>(); // 플레이어 충돌체 조회
            bool bodyEnabled = body != null && body.enabled; // 기존 충돌 상태 저장
            player.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 사격과 조준 정리

            if (body != null) // 충돌체 존재 확인
            {
                body.enabled = false; // 순간 이동 중 충돌 보정 중지
            }

            player.transform.SetPositionAndRotation(destination.position, destination.rotation); // 지상 또는 지하 도착점으로 이동
            movement.SetHorizontalVelocity(Vector3.zero); // 수평 관성 제거
            movement.VerticalVelocity = 0f; // 낙하 속도 제거

            if (body != null) // 충돌체 존재 확인
            {
                body.enabled = bodyEnabled; // 기존 충돌 상태 복원
            }

            Physics.SyncTransforms(); // 순간 이동 결과 물리 반영
        }
    }
}
