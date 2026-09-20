using ProjectK.Day21; // 수배 경비와 수배 시스템 참조
using UnityEngine; // 런타임 자동 연결

using ProjectK.Day28; // Day28 런타임 Registry 참조
namespace ProjectK.Day23 // 23일차 추격 안정화 이름 공간
{
    [DisallowMultipleComponent] // 부트스트랩 중복 방지
    public sealed class Map23ChaseBootstrap : MonoBehaviour // 수배 경비 안정화 자동 연결과 철수 보조
    {
        private static Map23ChaseBootstrap instance; // 현재 부트스트랩 인스턴스
        private float nextScanTime; // 다음 경비 검색 시각
        private float noWantedSince = float.PositiveInfinity; // 수배 해제 시작 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 진입마다 정적 참조 초기화
        {
            instance = null; // 이전 플레이 인스턴스 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 후 자동 생성
        private static void EnsureBootstrap() // 별도 씬 설정 없이 기능 생성
        {
            if (instance != null) // 기존 인스턴스 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day23] Chase Stability Bootstrap"); // 자동 관리자 오브젝트 생성
            instance = owner.AddComponent<Map23ChaseBootstrap>(); // 부트스트랩 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 뒤에도 유지
        }

        private void Awake() // 중복 관리자 정리
        {
            if (instance != null && instance != this) // 다른 인스턴스 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 인스턴스 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Update() // 경비 자동 연결과 수배 해제 후 철수 처리
        {
            if (Time.unscaledTime >= nextScanTime) // 검색 시각 확인
            {
                nextScanTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
                AttachStabilityComponents(); // 새로 생성된 경비에 안정화 기능 연결
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null) // 수배 시스템이 없는 씬 확인
            {
                noWantedSince = float.PositiveInfinity; // 철수 타이머 초기화
                return; // 처리 중단
            }

            if (wanted.Stars > 0) // 수배 진행 상태 확인
            {
                noWantedSince = float.PositiveInfinity; // 수배 해제 타이머 제거
                return; // 철수 처리 생략
            }

            if (float.IsPositiveInfinity(noWantedSince)) // 수배가 방금 해제됐는지 확인
            {
                noWantedSince = Time.unscaledTime; // 철수 시작 시각 저장
                return; // 짧은 여유 시간 제공
            }

            if (Time.unscaledTime - noWantedSince >= 6f) // 수배 해제 후 충분한 시간 경과 확인
            {
                RecycleRemainingGuards(); // 남아 있는 수배 경비 풀 복귀
                noWantedSince = float.PositiveInfinity; // 반복 철수 방지
            }
        }

        private static void AttachStabilityComponents() // 모든 풀 경비에 보조 안정화 기능 연결
        {
            var guards = Map28RuntimeRegistry.AllGuards; // 활성·비활성 수배 경비 조회

            foreach (MapWantedGuardAgent guard in guards) // 모든 수배 경비 순회
            {
                if (guard == null || guard.GetComponent<Map23GuardChaseStability>() != null) // 누락 또는 기존 연결 확인
                {
                    continue; // 다음 경비 처리
                }

                guard.gameObject.AddComponent<Map23GuardChaseStability>(); // 정체 복구 기능 자동 추가
            }
        }

        private static void RecycleRemainingGuards() // 수배 해제 뒤 남은 경비 정리
        {
            var guards = Map28RuntimeRegistry.ActiveGuards; // 현재 활성 수배 경비 조회

            foreach (MapWantedGuardAgent guard in guards) // 활성 경비 순회
            {
                if (guard == null || guard.IsDead) // 누락 또는 사망 경비 확인
                {
                    continue; // 사망 경비는 기존 시체 회수 규칙 유지
                }

                guard.gameObject.SetActive(false); // 살아 있는 추적 경비를 풀 대기 상태로 복귀
            }
        }
    }
}
