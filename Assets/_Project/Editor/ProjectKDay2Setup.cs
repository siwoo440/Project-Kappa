#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능
using UnityEngine.SceneManagement; // 씬 구조 기능

public static class ProjectKDay2Setup // 2일차 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions"; // 입력 에셋 경로
    private const string SessionKey = "ProjectK.Day2Setup.SessionApplied"; // 세션 적용 확인 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 구성 예약
    {
        if (System.IO.File.Exists("Assets/_Project/Editor/ProjectKDay9Setup.cs")) // 9일차 이후 자동 재생성 방지
        {
            return; // 기존 테스트 씬과 장비 배치 보존
        }

        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 적용 여부 확인
        {
            return; // 중복 자동 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplyDay2Setup; // 컴파일 종료 후 적용 예약
    }

    // 수동 구성 메뉴
    public static void ApplyDay2Setup() // 2일차 구성 적용
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath); // 입력 에셋 조회
        if (inputAsset == null) // 입력 에셋 누락 확인
        {
            Debug.LogError("Project K Day 2 setup failed: InputSystem_Actions.inputactions not found."); // 입력 에셋 오류 출력
            return; // 구성 중단
        }

        Scene testScene = SceneManager.GetSceneByPath(TestScenePath); // 기존 테스트 씬 조회
        bool sceneWasLoaded = testScene.IsValid() && testScene.isLoaded; // 기존 로드 상태 확인

        if (!sceneWasLoaded) // 테스트 씬 미로드 확인
        {
            testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(testScene, "Player"); // 플레이어 객체 조회
        GameObject cameraObject = FindObjectByName(testScene, "Main Camera"); // 메인 카메라 객체 조회

        if (player == null || cameraObject == null) // 필수 객체 누락 확인
        {
            Debug.LogError("Project K Day 2 setup failed: Player or Main Camera was not found in Test scene."); // 필수 객체 오류 출력

            if (!sceneWasLoaded) // 임시 로드 상태 확인
            {
                EditorSceneManager.CloseScene(testScene, true); // 임시 테스트 씬 닫기
            }

            return; // 구성 중단
        }

        CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>(); // 기존 캡슐 콜라이더 조회
        if (capsuleCollider != null) // 기존 콜라이더 확인
        {
            Object.DestroyImmediate(capsuleCollider); // 중복 물리 콜라이더 제거
        }

        CharacterController controller = player.GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        if (controller == null) // 컨트롤러 누락 확인
        {
            controller = player.AddComponent<CharacterController>(); // 캐릭터 컨트롤러 추가
        }

        controller.height = 2f; // 캐릭터 높이 설정
        controller.radius = 0.45f; // 캐릭터 반경 설정
        controller.center = Vector3.zero; // 캐릭터 중심 설정
        controller.stepOffset = 0.3f; // 계단 오르기 높이 설정
        controller.slopeLimit = 50f; // 경사 이동 한계 설정
        EditorUtility.SetDirty(controller); // 컨트롤러 변경 표시

        PlayerInput playerInput = player.GetComponent<PlayerInput>(); // 플레이어 입력 컴포넌트 조회
        if (playerInput == null) // 입력 컴포넌트 누락 확인
        {
            playerInput = player.AddComponent<PlayerInput>(); // 플레이어 입력 컴포넌트 추가
        }

        playerInput.actions = inputAsset; // 입력 액션 에셋 지정
        playerInput.defaultActionMap = "Player"; // 기본 액션 맵 지정
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents; // 입력 알림 방식 지정
        EditorUtility.SetDirty(playerInput); // 입력 컴포넌트 변경 표시

        Camera cameraComponent = cameraObject.GetComponent<Camera>(); // 카메라 컴포넌트 조회
        if (cameraComponent == null) // 카메라 컴포넌트 누락 확인
        {
            cameraComponent = cameraObject.AddComponent<Camera>(); // 카메라 컴포넌트 추가
        }

        cameraObject.tag = "MainCamera"; // 메인 카메라 태그 지정
        EditorUtility.SetDirty(cameraObject); // 카메라 객체 변경 표시

        PlayerMovement movement = player.GetComponent<PlayerMovement>(); // 이동 스크립트 조회
        if (movement == null) // 이동 스크립트 누락 확인
        {
            movement = player.AddComponent<PlayerMovement>(); // 이동 스크립트 추가
        }

        movement.Configure(cameraComponent); // 이동 기준 카메라 연결
        movement.SetSpawnPoint(player.transform.position, player.transform.rotation); // 시작 복귀 위치 지정
        EditorUtility.SetDirty(movement); // 이동 스크립트 변경 표시

        ThirdPersonCamera thirdPersonCamera = cameraObject.GetComponent<ThirdPersonCamera>(); // 3인칭 카메라 스크립트 조회
        if (thirdPersonCamera == null) // 카메라 스크립트 누락 확인
        {
            thirdPersonCamera = cameraObject.AddComponent<ThirdPersonCamera>(); // 3인칭 카메라 스크립트 추가
        }

        thirdPersonCamera.Configure(player.transform, playerInput); // 카메라 대상과 입력 연결
        EditorUtility.SetDirty(thirdPersonCamera); // 카메라 스크립트 변경 표시

        EditorSceneManager.MarkSceneDirty(testScene); // 테스트 씬 변경 표시
        EditorSceneManager.SaveScene(testScene); // 테스트 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 변경 저장
        AssetDatabase.Refresh(); // 에셋 새로고침

        if (!sceneWasLoaded) // 임시 로드 상태 확인
        {
            EditorSceneManager.CloseScene(testScene, true); // 임시 테스트 씬 닫기
        }

        Debug.Log("Project K Day 2 movement and camera setup complete."); // 완료 로그 출력
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 씬 내부 이름 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회

        for (int i = 0; i < roots.Length; i++) // 루트 객체 순회
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName); // 하위 객체 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static Transform FindChildRecursive(Transform current, string objectName) // 하위 객체 재귀 검색
    {
        if (current.name == objectName) // 현재 객체 이름 확인
        {
            return current; // 현재 객체 반환
        }

        for (int i = 0; i < current.childCount; i++) // 자식 객체 순회
        {
            Transform found = FindChildRecursive(current.GetChild(i), objectName); // 자식 객체 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }
}
#endif // 에디터 전용 컴파일 종료
