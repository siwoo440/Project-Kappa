#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay7DirectionIndicatorSetup // 방향 표시 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day7.DirectionIndicator.V1"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 설정 예약
    {
        if (System.IO.File.Exists("Assets/_Project/Editor/ProjectKDay9Setup.cs")) // 9일차 이후 자동 재생성 방지
        {
            return; // 기존 테스트 씬과 장비 배치 보존
        }

        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplySetup; // 컴파일 종료 후 설정 예약
    }

    [MenuItem("Project K/Day 7/Setup Player Direction Indicator")] // 수동 설정 메뉴
    public static void ApplySetup() // 방향 표시 설정 적용
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(scene, "Player"); // 플레이어 조회

        if (player == null) // 플레이어 누락 확인
        {
            Debug.LogError("Project K Day 7 direction indicator setup failed: Player not found."); // 오류 로그 출력
            CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
            return; // 설정 중단
        }

        PlayerDirectionIndicator indicator = player.GetComponent<PlayerDirectionIndicator>(); // 기존 방향 표시 조회

        if (indicator == null) // 방향 표시 누락 확인
        {
            indicator = player.AddComponent<PlayerDirectionIndicator>(); // 방향 표시 추가
        }

        EditorUtility.SetDirty(indicator); // 컴포넌트 변경 표시
        EditorSceneManager.MarkSceneDirty(scene); // 테스트 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 테스트 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 7 player direction indicator setup complete."); // 완료 로그 출력
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 이름 기반 객체 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회

        for (int i = 0; i < roots.Length; i++) // 루트 객체 순회
        {
            Transform found = FindRecursive(roots[i].transform, objectName); // 하위 객체 재귀 검색

            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static Transform FindRecursive(Transform current, string objectName) // 하위 객체 재귀 검색
    {
        if (current.name == objectName) // 현재 이름 확인
        {
            return current; // 현재 객체 반환
        }

        for (int i = 0; i < current.childCount; i++) // 자식 객체 순회
        {
            Transform found = FindRecursive(current.GetChild(i), objectName); // 자식 객체 재귀 검색

            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static void CloseSceneIfNeeded(Scene scene, bool wasLoaded) // 임시 씬 종료
    {
        if (!wasLoaded) // 임시 로드 상태 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 테스트 씬 종료
        }
    }
}
#endif // 에디터 전용 컴파일 종료
