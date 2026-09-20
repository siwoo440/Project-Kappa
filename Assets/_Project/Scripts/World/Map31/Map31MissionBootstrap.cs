using UnityEngine; // MissionManager·Inventory·Toast 자동 설치

namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    public static class Map31MissionBootstrap // 별도 씬 작업 없이 공통 MissionManager 자동 설치
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // 현재 플레이 세션 MissionManager 구성
        {
            if (Map31MissionManager.Instance == null) // MissionManager 존재 확인
            {
                GameObject managerObject = new GameObject("[Day31] Mission Manager"); // 중앙 임무 관리자 오브젝트 생성
                managerObject.AddComponent<Map31MissionInventory>(); // 일반 아이템과 분리된 임무 물품 슬롯 추가
                managerObject.AddComponent<Map31MissionManager>(); // 공통 MissionManager 연결
                Object.DontDestroyOnLoad(managerObject); // 씬 전환 유지
            }

            if (Map31MissionToastHUD.Instance == null) // 목표 알림 HUD 존재 확인
            {
                GameObject toastObject = new GameObject("[Day31] Mission Toast HUD"); // 목표 알림 오브젝트 생성
                toastObject.AddComponent<Map31MissionToastHUD>(); // 알림 HUD 연결
                Object.DontDestroyOnLoad(toastObject); // 씬 전환 유지
            }
        }
    }
}
