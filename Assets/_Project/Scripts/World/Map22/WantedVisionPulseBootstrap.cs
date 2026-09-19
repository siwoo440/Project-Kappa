using UnityEngine; // 런타임 자동 생성 기능

namespace ProjectK.Day22 // 22일차 미니맵 시야 표시 이름 공간
{
    [DisallowMultipleComponent] // 자동 부트스트랩 중복 방지
    public sealed class WantedVisionPulseBootstrap : MonoBehaviour // 미니맵 시야 오버레이 자동 생성
    {
        private static WantedVisionPulseBootstrap instance; // 현재 자동 생성 관리자
        private WantedMiniMapVisionOverlay overlay; // 미니맵 시야 오버레이 참조

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경의 정적 상태 초기화
        private static void ResetStatics() // 플레이 진입마다 정적 참조 초기화
        {
            instance = null; // 이전 플레이 인스턴스 참조 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 첫 씬 준비 뒤 자동 생성
        private static void EnsureBootstrap() // 별도 씬 설정 없이 관리자 생성
        {
            if (instance != null) // 이미 관리자 존재 여부 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day22] Wanted Minimap Vision Bootstrap"); // 자동 관리자 오브젝트 생성
            instance = owner.AddComponent<WantedVisionPulseBootstrap>(); // 관리자 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 뒤에도 유지
        }

        private void Awake() // 중복 관리자 정리와 오버레이 준비
        {
            if (instance != null && instance != this) // 다른 관리자 존재 여부 확인
            {
                Destroy(gameObject); // 중복 관리자 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            overlay = GetComponent<WantedMiniMapVisionOverlay>(); // 기존 오버레이 확인
            if (overlay == null) // 오버레이 누락 확인
            {
                overlay = gameObject.AddComponent<WantedMiniMapVisionOverlay>(); // 미니맵 오버레이 자동 추가
            }
        }
    }
}
