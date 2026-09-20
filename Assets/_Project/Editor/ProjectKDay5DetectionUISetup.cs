#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 구조 기능

public static class ProjectKDay5DetectionUISetup // 탐지 UI 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day5.DetectionUI.V2"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 구성 예약
    {
        if (System.IO.File.Exists("Assets/_Project/Editor/ProjectKDay9Setup.cs")) // 9일차 이후 자동 재생성 방지
        {
            return; // 기존 테스트 씬과 장비 배치 보존
        }

        if (SessionState.GetBool(SessionKey, false)) // 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }
        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplySetup; // 지연 적용 예약
    }

    // 메뉴 항목 등록
    public static void ApplySetup() // 탐지 UI 구성 적용
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인
        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        Camera mainCamera = FindMainCamera(scene); // 메인 카메라 조회
        DetectionSensor[] sensors = Object.FindObjectsByType<DetectionSensor>(FindObjectsSortMode.None); // 전체 센서 조회
        for (int i = 0; i < sensors.Length; i++) // 센서 순회
        {
            if (sensors[i] == null || sensors[i].gameObject.scene.path != TestScenePath) // 테스트 씬 센서 확인
            {
                continue; // 다른 센서 제외
            }

            DetectionBillboardUI billboard = sensors[i].GetComponent<DetectionBillboardUI>(); // 기존 UI 조회
            if (billboard == null) // UI 누락 확인
            {
                billboard = sensors[i].gameObject.AddComponent<DetectionBillboardUI>(); // UI 추가
            }

            Vector3 offset = sensors[i].name.StartsWith("E01_") ? new Vector3(0f, 2.75f, 0f) : new Vector3(0f, 2.4f, 0f); // 대상별 높이 계산
            billboard.Configure(sensors[i], mainCamera, offset); // 탐지 UI 설정 적용
            EditorUtility.SetDirty(billboard); // UI 변경 표시
        }

        EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        if (!wasLoaded) // 임시 로드 여부 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 테스트 씬 닫기
        }
        Debug.Log("Project K Day 5 detection billboard UI setup complete."); // 완료 로그 출력
    }

    private static Camera FindMainCamera(Scene scene) // 메인 카메라 조회
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 목록 조회
        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true); // 하위 카메라 조회
            for (int j = 0; j < cameras.Length; j++) // 카메라 순회
            {
                if (cameras[j].CompareTag("MainCamera")) // 메인 카메라 태그 확인
                {
                    return cameras[j]; // 메인 카메라 반환
                }
            }
        }
        return Camera.main; // 대체 메인 카메라 반환
    }
}
#endif // 에디터 전용 기능 종료