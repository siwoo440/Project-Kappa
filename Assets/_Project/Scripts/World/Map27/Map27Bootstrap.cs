using ProjectK.Day25; // E-04 감시 드론 참조
using UnityEngine; // 런타임 자동 연결

namespace ProjectK.Day27 // 27일차 통합 QA 이름 공간
{
    [DisallowMultipleComponent] // 자동 연결 관리자 중복 방지
    public sealed class Map27Bootstrap : MonoBehaviour // Day27 런타임 보조 기능 자동 연결
    {
        private static Map27Bootstrap instance; // 현재 부트스트랩 인스턴스
        private float nextScanTime; // 다음 드론 검색 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 시 정적 참조 초기화
        {
            instance = null; // 이전 플레이 인스턴스 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 로드 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 Day27 부트스트랩 생성
        {
            if (instance != null) // 기존 인스턴스 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day27] Runtime Bootstrap"); // 자동 연결 관리자 생성
            instance = owner.AddComponent<Map27Bootstrap>(); // 부트스트랩 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 인스턴스 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 인스턴스 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 자동 연결
        {
            AttachDroneAvoidance(); // 현재 E-04에 충돌 회피 연결
            nextScanTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
        }

        private void Update() // 풀에서 새로 활성화되는 드론 보조 기능 유지
        {
            if (Time.unscaledTime < nextScanTime) // 검색 간격 확인
            {
                return; // 다음 검사 대기
            }

            nextScanTime = Time.unscaledTime + 0.75f; // 다음 검색 예약
            AttachDroneAvoidance(); // 모든 E-04에 회피 기능 보장
        }

        private static void AttachDroneAvoidance() // E-04 드론에 건물 관통 회피 자동 부착
        {
            Map25SurveillanceDrone[] drones = Object.FindObjectsByType<Map25SurveillanceDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 활성·비활성 드론 전체 조회
            foreach (Map25SurveillanceDrone drone in drones) // 드론 순회
            {
                if (drone == null || drone.GetComponent<Map27DroneCollisionAvoidance>() != null) // 누락·기존 연결 확인
                {
                    continue; // 다음 드론 처리
                }

                drone.gameObject.AddComponent<Map27DroneCollisionAvoidance>(); // Day27 고정 구조물 회피 추가
            }
        }
    }
}
