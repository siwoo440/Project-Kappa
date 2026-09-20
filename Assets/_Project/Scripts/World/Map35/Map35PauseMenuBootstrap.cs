using UnityEngine; // Day35 저장·ESC 메뉴 자동 설치

namespace ProjectK.Day35 // 35일차 저장·불러오기 이름 공간
{
    public static class Map35PauseMenuBootstrap // 별도 씬 편집 없이 저장 시스템과 ESC 메뉴 자동 생성
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // Day35 런타임 구성
        {
            if (Map35SaveManager.Instance == null) // SaveManager 존재 확인
            {
                GameObject saveObject = new GameObject("[Day35] Save Manager"); // 저장 관리자 오브젝트 생성
                saveObject.AddComponent<Map35SaveManager>(); // SaveManager 연결
                Object.DontDestroyOnLoad(saveObject); // 씬 전환 유지
            }

            if (Map35PauseMenuHUD.Instance == null) // ESC 메뉴 존재 확인
            {
                GameObject menuObject = new GameObject("[Day35] Pause Menu HUD"); // ESC 메뉴 오브젝트 생성
                menuObject.AddComponent<Map35PauseMenuHUD>(); // 메뉴 HUD 연결
                Object.DontDestroyOnLoad(menuObject); // 씬 전환 유지
            }
        }
    }
}
