using UnityEngine; // 런타임 전체 임무 창 자동 생성

namespace ProjectK.Day30 // 30일차 전체 임무 창 이름 공간
{
    public static class Map30MissionArchiveBootstrap // 별도 씬 수정 없이 Mission Journal과 Tab 전체 임무 창 자동 설치
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // 현재 플레이 세션 임무 아카이브 설치
        {
            if (Map30MissionJournal.Instance == null) // Mission Journal 존재 확인
            {
                GameObject journalObject = new GameObject("[Day30] Mission Journal"); // Journal 오브젝트 생성
                journalObject.AddComponent<Map30MissionJournal>(); // 보유 임무 Journal 연결
                Object.DontDestroyOnLoad(journalObject); // 씬 전환 유지
            }

            if (Map30MissionWindow.Instance != null) // 기존 전체 임무 창 확인
            {
                return; // 중복 창 생성 방지
            }

            GameObject windowObject = new GameObject("[Day30] Mission Archive"); // 전체 임무 창 오브젝트 생성
            windowObject.AddComponent<Map30MissionWindow>(); // Tab 임무 창 연결
            Object.DontDestroyOnLoad(windowObject); // 씬 전환 유지
        }
    }
}
