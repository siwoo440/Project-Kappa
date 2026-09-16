#if UNITY_EDITOR // 에디터 전용 컴파일
using System.Collections.Generic; // 목록 기능
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 구조 기능

public static class ProjectKDay1Setup // 1일차 자동 구성 도구
{
    private const string RootFolder = "Assets/_Project"; // 프로젝트 루트 폴더
    private static readonly string[] ScenePaths = // 기본 씬 경로 목록
    {
        "Assets/_Project/Scenes/Bootstrap.unity", // 부트스트랩 씬 경로
        "Assets/_Project/Scenes/MainMenu.unity", // 메인 메뉴 씬 경로
        "Assets/_Project/Scenes/Hub.unity", // 거점 씬 경로
        "Assets/_Project/Scenes/Test.unity" // 테스트 씬 경로
    }; // 기본 씬 경로 목록 종료

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 구성 예약
    {
        EditorApplication.delayCall += AutoSetup; // 컴파일 종료 후 실행 예약
    }

    [MenuItem("Project K/Day 1/Setup Project")] // 수동 구성 메뉴
    public static void SetupProject() // 전체 프로젝트 구성
    {
        EnsureFolderStructure(); // 폴더 구조 생성
        CreateMissingScenes(); // 누락 씬 생성
        UpdateBuildSettings(); // 빌드 씬 목록 갱신
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        Debug.Log("Project K Day 1 setup complete."); // 완료 로그
    }

    private static void AutoSetup() // 자동 구성 실행
    {
        if (AllScenesExist()) // 기존 구성 확인
        {
            return; // 중복 구성 방지
        }

        SetupProject(); // 자동 프로젝트 구성
    }

    private static bool AllScenesExist() // 기본 씬 존재 확인
    {
        for (int i = 0; i < ScenePaths.Length; i++) // 씬 경로 순회
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths[i]) == null) // 씬 누락 확인
            {
                return false; // 누락 상태 반환
            }
        }

        return true; // 전체 존재 반환
    }

    private static void EnsureFolderStructure() // 기본 폴더 구조 생성
    {
        EnsureFolder(RootFolder); // 프로젝트 루트 생성
        EnsureFolder(RootFolder + "/Animations"); // 애니메이션 폴더 생성
        EnsureFolder(RootFolder + "/Art"); // 아트 폴더 생성
        EnsureFolder(RootFolder + "/Audio"); // 오디오 폴더 생성
        EnsureFolder(RootFolder + "/Data"); // 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Data/Weapons"); // 무기 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Data/Enemies"); // 적 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Data/Items"); // 아이템 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Data/Missions"); // 미션 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Data/Characters"); // 캐릭터 데이터 폴더 생성
        EnsureFolder(RootFolder + "/Materials"); // 머티리얼 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs"); // 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs/Characters"); // 캐릭터 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs/Enemies"); // 적 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs/Weapons"); // 무기 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs/Items"); // 아이템 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Prefabs/World"); // 월드 프리팹 폴더 생성
        EnsureFolder(RootFolder + "/Scenes"); // 씬 폴더 생성
        EnsureFolder(RootFolder + "/Scripts"); // 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Core"); // 코어 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Data"); // 데이터 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Player"); // 플레이어 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Camera"); // 카메라 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Interaction"); // 상호작용 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Combat"); // 전투 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Weapons"); // 무기 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/AI"); // AI 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Mission"); // 미션 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/UI"); // UI 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/Scripts/Save"); // 저장 스크립트 폴더 생성
        EnsureFolder(RootFolder + "/UI"); // UI 에셋 폴더 생성
        EnsureFolder(RootFolder + "/VFX"); // VFX 폴더 생성
        EnsureFolder("Assets/ThirdParty"); // 외부 에셋 폴더 생성
    }

    private static void EnsureFolder(string fullPath) // 단일 폴더 생성
    {
        if (AssetDatabase.IsValidFolder(fullPath)) // 기존 폴더 확인
        {
            return; // 중복 생성 방지
        }

        int slashIndex = fullPath.LastIndexOf('/'); // 부모 경로 구분 위치
        string parentPath = fullPath.Substring(0, slashIndex); // 부모 경로 추출
        string folderName = fullPath.Substring(slashIndex + 1); // 폴더 이름 추출
        EnsureFolder(parentPath); // 부모 폴더 선행 생성
        AssetDatabase.CreateFolder(parentPath, folderName); // 새 폴더 생성
    }

    private static void CreateMissingScenes() // 누락 씬 생성
    {
        CreateSceneIfMissing(ScenePaths[0], CreateBootstrapContents); // 부트스트랩 씬 생성
        CreateSceneIfMissing(ScenePaths[1], CreateBasicSceneContents); // 메인 메뉴 씬 생성
        CreateSceneIfMissing(ScenePaths[2], CreateHubContents); // 거점 씬 생성
        CreateSceneIfMissing(ScenePaths[3], CreateTestContents); // 테스트 씬 생성
    }

    private static void CreateSceneIfMissing(string path, System.Action contentBuilder) // 단일 씬 조건 생성
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) // 기존 씬 확인
        {
            return; // 기존 씬 보호
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 빈 추가 씬 생성
        SceneManager.SetActiveScene(scene); // 생성 씬 활성화
        contentBuilder.Invoke(); // 씬 기본 오브젝트 생성
        EditorSceneManager.SaveScene(scene, path); // 씬 파일 저장
        EditorSceneManager.CloseScene(scene, true); // 임시 씬 닫기
    }

    private static void CreateBootstrapContents() // 부트스트랩 내용 생성
    {
        GameObject bootstrapObject = new GameObject("GameBootstrap"); // 부트스트랩 객체 생성
        bootstrapObject.AddComponent<GameBootstrap>(); // 초기화 관리자 추가
    }

    private static void CreateBasicSceneContents() // 기본 씬 내용 생성
    {
        CreateCamera(); // 기본 카메라 생성
        CreateDirectionalLight(); // 기본 조명 생성
    }

    private static void CreateHubContents() // 거점 씬 내용 생성
    {
        CreateBasicSceneContents(); // 기본 씬 구성 생성
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // 임시 바닥 생성
        ground.name = "Hub_Ground"; // 바닥 이름 지정
        ground.transform.localScale = new Vector3(4f, 1f, 4f); // 바닥 크기 지정
    }

    private static void CreateTestContents() // 테스트 씬 내용 생성
    {
        CreateBasicSceneContents(); // 기본 씬 구성 생성
        GameObject root = new GameObject("TestRoot"); // 테스트 루트 생성
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // 테스트 바닥 생성
        ground.name = "Ground"; // 테스트 바닥 이름 지정
        ground.transform.localScale = new Vector3(5f, 1f, 5f); // 테스트 바닥 크기 지정
        ground.transform.SetParent(root.transform); // 테스트 루트 연결
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 임시 플레이어 생성
        player.name = "Player"; // 플레이어 이름 지정
        player.transform.position = new Vector3(0f, 1f, 0f); // 플레이어 위치 지정
        player.transform.SetParent(root.transform); // 테스트 루트 연결
        CreateObstacle(root.transform, new Vector3(3f, 1f, 3f), new Vector3(2f, 2f, 2f)); // 테스트 장애물 생성
        CreateObstacle(root.transform, new Vector3(-3f, 1.5f, 5f), new Vector3(2f, 3f, 2f)); // 테스트 장애물 생성
        CreateObstacle(root.transform, new Vector3(0f, 2f, 8f), new Vector3(6f, 4f, 1f)); // 테스트 벽 생성
    }

    private static void CreateCamera() // 기본 카메라 생성
    {
        GameObject cameraObject = new GameObject("Main Camera"); // 카메라 객체 생성
        Camera cameraComponent = cameraObject.AddComponent<Camera>(); // 카메라 컴포넌트 추가
        cameraObject.AddComponent<AudioListener>(); // 오디오 리스너 추가
        cameraObject.tag = "MainCamera"; // 메인 카메라 태그 지정
        cameraObject.transform.position = new Vector3(0f, 5f, -10f); // 카메라 위치 지정
        cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f); // 카메라 회전 지정
        cameraComponent.clearFlags = CameraClearFlags.Skybox; // 카메라 배경 방식 지정
    }

    private static void CreateDirectionalLight() // 기본 조명 생성
    {
        GameObject lightObject = new GameObject("Directional Light"); // 조명 객체 생성
        Light lightComponent = lightObject.AddComponent<Light>(); // 조명 컴포넌트 추가
        lightComponent.type = LightType.Directional; // 방향광 유형 지정
        lightComponent.intensity = 1f; // 조명 밝기 지정
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // 조명 회전 지정
    }

    private static void CreateObstacle(Transform parent, Vector3 position, Vector3 scale) // 테스트 장애물 생성
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); // 큐브 장애물 생성
        obstacle.name = "Obstacle"; // 장애물 이름 지정
        obstacle.transform.position = position; // 장애물 위치 지정
        obstacle.transform.localScale = scale; // 장애물 크기 지정
        obstacle.transform.SetParent(parent); // 테스트 루트 연결
    }

    private static void UpdateBuildSettings() // 빌드 씬 목록 갱신
    {
        List<EditorBuildSettingsScene> mergedScenes = new List<EditorBuildSettingsScene>(); // 병합 씬 목록 생성

        for (int i = 0; i < ScenePaths.Length; i++) // 프로젝트 기본 씬 순회
        {
            mergedScenes.Add(new EditorBuildSettingsScene(ScenePaths[i], true)); // 기본 씬 활성 추가
        }

        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes; // 기존 빌드 씬 조회
        for (int i = 0; i < currentScenes.Length; i++) // 기존 씬 순회
        {
            if (ContainsScenePath(currentScenes[i].path)) // 프로젝트 기본 씬 중복 확인
            {
                continue; // 기본 씬 중복 제외
            }

            mergedScenes.Add(currentScenes[i]); // 기존 사용자 씬 유지
        }

        EditorBuildSettings.scenes = mergedScenes.ToArray(); // 병합 목록 적용
    }

    private static bool ContainsScenePath(string scenePath) // 기본 씬 경로 포함 확인
    {
        for (int i = 0; i < ScenePaths.Length; i++) // 기본 씬 순회
        {
            if (ScenePaths[i] == scenePath) // 동일 경로 확인
            {
                return true; // 포함 상태 반환
            }
        }

        return false; // 미포함 상태 반환
    }
}
#endif // 에디터 전용 컴파일 종료
