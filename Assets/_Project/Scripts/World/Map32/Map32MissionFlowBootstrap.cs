using UnityEngine; // 런타임 Day32 자동 설치

namespace ProjectK.Day32 // 32일차 체크포인트·재시도 이름 공간
{
    public static class Map32MissionFlowBootstrap // 별도 씬 설정 없이 체크포인트·결과 화면 관리자 자동 생성
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // Day32 런타임 시스템 구성
        {
            if (Map32MissionCheckpointSystem.Instance == null) // 체크포인트 관리자 존재 확인
            {
                GameObject checkpointObject = new GameObject("[Day32] Mission Checkpoint"); // 체크포인트 오브젝트 생성
                checkpointObject.AddComponent<Map32MissionCheckpointSystem>(); // 체크포인트 시스템 연결
                Object.DontDestroyOnLoad(checkpointObject); // 씬 전환 유지
            }

            if (Map32MissionResultScreen.Instance == null) // 결과 화면 관리자 존재 확인
            {
                GameObject resultObject = new GameObject("[Day32] Mission Result Screen"); // 결과 UI 오브젝트 생성
                resultObject.AddComponent<Map32MissionResultScreen>(); // 결과 화면 연결
                Object.DontDestroyOnLoad(resultObject); // 씬 전환 유지
            }
        }
    }
}
