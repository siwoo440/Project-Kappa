using UnityEngine; // 목표 안내 HUD 자동 생성

namespace ProjectK.Day33 // 33일차 추가 임무 안내 기능 이름 공간
{
    public static class Map33MissionObjectiveArrowBootstrap // 별도 씬 편집 없이 G 목표 화살표 자동 설치
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // 플레이 세션 목표 안내 HUD 구성
        {
            if (Map33MissionObjectiveArrowHUD.Instance != null) // 기존 HUD 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day33] Mission Objective Arrow HUD"); // 목표 안내 HUD 오브젝트 생성
            owner.AddComponent<Map33MissionObjectiveArrowHUD>(); // G 목표 화살표 기능 연결
            Object.DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
