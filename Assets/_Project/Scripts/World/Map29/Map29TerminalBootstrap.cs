using UnityEngine; // 런타임 단말기 자동 생성

namespace ProjectK.Day29 // 29일차 플레이어 단말기 이름 공간
{
    public static class Map29TerminalBootstrap // 별도 씬 수정 없이 단말기 Provider와 HUD 자동 생성
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // 현재 플레이 세션 단말기 설치
        {
            if (Map29TerminalObjectiveProvider.Instance == null) // 목표 Provider 존재 확인
            {
                GameObject providerObject = new GameObject("[Day29] Terminal Objective Provider"); // Provider 오브젝트 생성
                providerObject.AddComponent<Map29TerminalObjectiveProvider>(); // 목표 Provider 연결
                Object.DontDestroyOnLoad(providerObject); // 씬 전환 유지
            }

            Map29TerminalHUD[] huds = Object.FindObjectsByType<Map29TerminalHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 기존 HUD 존재 확인
            if (huds.Length > 0) // 기존 HUD 확인
            {
                return; // 중복 HUD 생성 방지
            }

            GameObject hudObject = new GameObject("[Day29] Terminal HUD"); // 단말기 HUD 오브젝트 생성
            hudObject.AddComponent<Map29TerminalHUD>(); // 단말기 HUD 연결
            Object.DontDestroyOnLoad(hudObject); // 씬 전환 유지
        }
    }
}
