using UnityEngine; // 유니티 기본 기능

public sealed class GameBootstrap : MonoBehaviour // 전역 초기화 관리자
{
    public static GameBootstrap Instance { get; private set; } // 단일 인스턴스 참조

    private void Awake() // 초기화 진입
    {
        if (Instance != null && Instance != this) // 중복 인스턴스 확인
        {
            Destroy(gameObject); // 중복 객체 제거
            return; // 중복 초기화 중단
        }

        Instance = this; // 현재 인스턴스 저장
        DontDestroyOnLoad(gameObject); // 씬 전환 유지
    }
} 
