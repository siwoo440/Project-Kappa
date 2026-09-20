using UnityEngine; // Day33 런타임 자동 설치

namespace ProjectK.Day33 // 33일차 M-02 잠입 암살 이름 공간
{
    public static class Map33MissionBootstrap // 별도 씬 편집 없이 M-02 월드 컨트롤러 자동 생성
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // Day33 M-02 런타임 구성
        {
            if (Map33MissionWorldController.Instance != null) // 기존 컨트롤러 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day33] M-02 Mission World"); // M-02 월드 관리자 생성
            owner.AddComponent<Map33MissionWorldController>(); // 런타임 배치·상태 동기화 연결
            Object.DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
