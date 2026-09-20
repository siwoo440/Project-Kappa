using UnityEngine; // Day36 설정 관리자 자동 설치

namespace ProjectK.Day36 // 36일차 설정 시스템 이름 공간
{
    public static class Map36SettingsBootstrap // 별도 씬 편집 없이 설정 관리자 자동 생성
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 설치
        private static void Install() // 설정 시스템 구성
        {
            if (Map36SettingsManager.Instance != null) // 기존 관리자 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day36] Settings Manager"); // 설정 관리자 오브젝트 생성
            owner.AddComponent<Map36SettingsManager>(); // 설정 관리자 연결
            Object.DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
