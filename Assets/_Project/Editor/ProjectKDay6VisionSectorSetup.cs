#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay6VisionSectorSetup // 감시 영역 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day6.VisionSector.SessionApplied"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 적용 예약
    {
        if (SessionState.GetBool(SessionKey, false)) // 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplySetup; // 지연 실행 등록
    }

    [MenuItem("Project K/Day 6/Setup Vision Sector Visuals")] // 메뉴 항목 등록
    public static void ApplySetup() // 감시 영역 적용
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        DetectionSensor[] sensors = Object.FindObjectsByType<DetectionSensor>(FindObjectsSortMode.None); // 전체 탐지 센서 조회
        Color faintRed = new Color(1f, 0.25f, 0.25f, 0.10f); // 시야 색상 정의

        for (int i = 0; i < sensors.Length; i++) // 센서 순회
        {
            DetectionSensor sensor = sensors[i]; // 현재 센서 저장
            if (sensor == null) // 센서 확인
            {
                continue; // 누락 센서 제외
            }

            if (sensor.gameObject.scene.path != TestScenePath) // 테스트 씬 소속 확인
            {
                continue; // 다른 씬 제외
            }

            string lowerName = sensor.name.ToLowerInvariant(); // 소문자 이름 저장
            VisionSectorVisual visual = sensor.GetComponent<VisionSectorVisual>(); // 기존 시각화 조회
            if (visual == null) // 시각화 존재 여부 확인
            {
                visual = sensor.gameObject.AddComponent<VisionSectorVisual>(); // 시각화 추가
            }

            if (lowerName.Contains("d01") || lowerName.Contains("camera")) // 카메라 여부 확인
            {
                Transform yawSource = sensor.transform.Find("RotationPivot"); // 회전 축 조회
                if (yawSource == null) // 회전 축 확인
                {
                    yawSource = sensor.transform; // 기본 회전축 대체
                }

                visual.Configure(yawSource, 20f, 70f, 0.04f, faintRed); // 카메라 시야 적용
            }
            else // 경비 처리
            {
                visual.Configure(sensor.transform, 18f, 90f, 0.04f, faintRed); // 경비 시야 적용
            }

            EditorUtility.SetDirty(sensor.gameObject); // 변경 표시
        }

        EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침

        if (!wasLoaded) // 임시 로드 여부 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 테스트 씬 닫기
        }

        Debug.Log("Project K Day 6 vision sector visuals setup complete."); // 완료 로그 출력
    }
}
#endif
