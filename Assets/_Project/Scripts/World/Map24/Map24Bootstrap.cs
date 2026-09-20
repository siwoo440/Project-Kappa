using ProjectK.Day21; // 수배 경비 타입 참조
using UnityEngine; // 런타임 자동 연결

namespace ProjectK.Day24 // 24일차 지상·지하 이동 이름 공간
{
    [DisallowMultipleComponent] // 자동 연결 관리자 중복 방지
    public sealed class Map24Bootstrap : MonoBehaviour // 모든 수배 경비에 층 이동 기능 자동 연결
    {
        private static Map24Bootstrap instance; // 현재 부트스트랩 인스턴스
        private float nextScanTime; // 다음 경비 검색 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 시 정적 상태 초기화
        {
            instance = null; // 이전 플레이 인스턴스 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 부트스트랩 생성
        {
            if (instance != null) // 기존 인스턴스 존재 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day24] Underground Pursuit Bootstrap"); // 자동 연결 관리자 오브젝트 생성
            instance = owner.AddComponent<Map24Bootstrap>(); // 부트스트랩 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 뒤에도 유지
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

        private void Start() // 첫 경비 자동 연결
        {
            AttachLayerAgents(); // 현재 경비 풀에 Day24 기능 연결
            nextScanTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
        }

        private void Update() // 런타임 생성 경비 추가 연결
        {
            if (Time.unscaledTime < nextScanTime) // 검색 간격 확인
            {
                return; // 아직 검색하지 않음
            }

            nextScanTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
            AttachLayerAgents(); // 신규·풀 경비에 기능 연결
        }

        private static void AttachLayerAgents() // 모든 활성·비활성 수배 경비 처리
        {
            MapWantedGuardAgent[] guards = Object.FindObjectsByType<MapWantedGuardAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 전체 수배 경비 조회

            foreach (MapWantedGuardAgent guard in guards) // 경비 목록 순회
            {
                if (guard == null || guard.GetComponent<Map24GuardLayerAgent>() != null) // 누락 또는 기존 연결 확인
                {
                    continue; // 다음 경비 처리
                }

                guard.gameObject.AddComponent<Map24GuardLayerAgent>(); // 계단 추격·지하 수색 기능 자동 추가
            }
        }
    }
}
