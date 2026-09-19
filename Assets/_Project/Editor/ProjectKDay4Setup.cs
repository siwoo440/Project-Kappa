#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능
using UnityEngine.SceneManagement; // 씬 구조 기능

public static class ProjectKDay4Setup // 4일차 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions"; // 입력 에셋 경로
    private const string SessionKey = "ProjectK.Day4Setup.V1"; // 세션 적용 확인 키

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
        EditorApplication.delayCall += ApplyDay4Setup; // 컴파일 종료 후 적용 예약
    }

    [MenuItem("Project K/Day 4/Setup Interaction Noise Detection")] // 수동 구성 메뉴
    public static void ApplyDay4Setup() // 4일차 구성 적용
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath); // 입력 에셋 조회
        if (inputAsset == null) // 입력 에셋 확인
        {
            Debug.LogError("Project K Day 4 setup failed: InputSystem_Actions.inputactions not found."); // 입력 에셋 오류 출력
            return; // 구성 중단
        }

        SetInteractKeyToF(inputAsset); // 상호작용 키 F 적용
        EnsureLeftCtrlCrouch(inputAsset); // Left Ctrl 앉기 유지

        Scene testScene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool sceneWasLoaded = testScene.IsValid() && testScene.isLoaded; // 기존 로드 상태 확인
        if (!sceneWasLoaded) // 테스트 씬 미로드 확인
        {
            testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(testScene, "Player"); // 플레이어 객체 조회
        GameObject cameraObject = FindObjectByName(testScene, "Main Camera"); // 메인 카메라 객체 조회
        if (player == null || cameraObject == null) // 필수 객체 확인
        {
            Debug.LogError("Project K Day 4 setup failed: Player or Main Camera was not found in Test scene."); // 필수 객체 오류 출력
            CloseSceneIfNeeded(testScene, sceneWasLoaded); // 임시 씬 종료
            return; // 구성 중단
        }

        ConfigurePlayer(player, cameraObject); // 플레이어 상호작용 소음 구성
        ConfigureTrainingInteractables(testScene); // 훈련장 상호작용 구성
        ConfigureDetectionZone(testScene, player.transform); // 탐지 테스트 구역 구성

        EditorSceneManager.MarkSceneDirty(testScene); // 테스트 씬 변경 표시
        EditorSceneManager.SaveScene(testScene); // 테스트 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 변경 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(testScene, sceneWasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 4 interaction, noise and detection setup complete. Interact key = F"); // 완료 로그 출력
    }

    private static void SetInteractKeyToF(InputActionAsset inputAsset) // 상호작용 F 키 적용
    {
        InputActionMap actionMap = inputAsset.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        InputAction interactAction = actionMap != null ? actionMap.FindAction("Interact", false) : null; // 상호작용 액션 조회
        if (interactAction == null) // 상호작용 액션 확인
        {
            return; // 키 변경 중단
        }

        bool keyboardBindingFound = false; // 키보드 바인딩 존재 여부
        for (int i = 0; i < interactAction.bindings.Count; i++) // 바인딩 순회
        {
            InputBinding binding = interactAction.bindings[i]; // 현재 바인딩 조회
            if (!binding.path.StartsWith("<Keyboard>/")) // 키보드 바인딩 확인
            {
                continue; // 다른 입력 제외
            }

            if (!keyboardBindingFound) // 첫 키보드 바인딩 확인
            {
                interactAction.ChangeBinding(i).WithPath("<Keyboard>/f"); // 기존 키를 F로 변경
                keyboardBindingFound = true; // 키보드 바인딩 존재 저장
            }
            else // 추가 키보드 바인딩 처리
            {
                interactAction.ChangeBinding(i).Erase(); // 중복 키보드 바인딩 제거
            }
        }

        if (!keyboardBindingFound) // 키보드 바인딩 누락 확인
        {
            interactAction.AddBinding("<Keyboard>/f").WithGroup("Keyboard&Mouse"); // F 키 바인딩 추가
        }

        EditorUtility.SetDirty(inputAsset); // 입력 에셋 변경 표시
    }

    private static void EnsureLeftCtrlCrouch(InputActionAsset inputAsset) // Left Ctrl 앉기 유지
    {
        InputActionMap actionMap = inputAsset.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        InputAction crouchAction = actionMap != null ? actionMap.FindAction("Crouch", false) : null; // 앉기 액션 조회
        if (crouchAction == null) // 앉기 액션 확인
        {
            return; // 보정 중단
        }

        for (int i = 0; i < crouchAction.bindings.Count; i++) // 바인딩 순회
        {
            if (crouchAction.bindings[i].path == "<Keyboard>/leftCtrl") // Left Ctrl 확인
            {
                return; // 기존 바인딩 유지
            }
        }

        crouchAction.AddBinding("<Keyboard>/leftCtrl").WithGroup("Keyboard&Mouse"); // Left Ctrl 바인딩 추가
        EditorUtility.SetDirty(inputAsset); // 입력 에셋 변경 표시
    }

    private static void ConfigurePlayer(GameObject player, GameObject cameraObject) // 플레이어 구성 적용
    {
        Camera cameraComponent = cameraObject.GetComponent<Camera>(); // 카메라 컴포넌트 조회
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>(); // 상호작용 스크립트 조회
        if (interaction == null) // 상호작용 스크립트 누락 확인
        {
            interaction = player.AddComponent<PlayerInteraction>(); // 상호작용 스크립트 추가
        }
        interaction.Configure(cameraComponent); // 상호작용 카메라 연결

        NoiseEmitter noiseEmitter = player.GetComponent<NoiseEmitter>(); // 소음 발생기 조회
        if (noiseEmitter == null) // 소음 발생기 누락 확인
        {
            noiseEmitter = player.AddComponent<NoiseEmitter>(); // 소음 발생기 추가
        }

        EditorUtility.SetDirty(interaction); // 상호작용 변경 표시
        EditorUtility.SetDirty(noiseEmitter); // 소음 발생기 변경 표시
    }

    private static void ConfigureTrainingInteractables(Scene scene) // 훈련장 상호작용 구성
    {
        AddInteractable(scene, "MissionTerminal", "미션 단말기", "미션 단말기 테스트", true, 4f, NoiseType.Interaction); // 미션 단말기 구성
        AddInteractable(scene, "SaveStation", "저장 장치", "저장 장치 테스트", false, 0f, NoiseType.Interaction); // 저장 장치 구성
        AddInteractable(scene, "ShopKiosk", "상점 키오스크", "상점 키오스크 테스트", true, 3f, NoiseType.Interaction); // 상점 키오스크 구성
    }

    private static void AddInteractable(Scene scene, string objectName, string label, string message, bool emitNoise, float radius, NoiseType type) // 상호작용 대상 추가
    {
        GameObject target = FindObjectByName(scene, objectName); // 대상 객체 조회
        if (target == null) // 대상 누락 확인
        {
            return; // 구성 생략
        }

        TestInteractable interactable = target.GetComponent<TestInteractable>(); // 테스트 상호작용 조회
        if (interactable == null) // 컴포넌트 누락 확인
        {
            interactable = target.AddComponent<TestInteractable>(); // 테스트 상호작용 추가
        }

        interactable.Configure(label, message, emitNoise, radius, type); // 상호작용 설정 적용
        EditorUtility.SetDirty(interactable); // 컴포넌트 변경 표시
    }

    private static void ConfigureDetectionZone(Scene scene, Transform player) // 탐지 구역 구성
    {
        GameObject stealthZone = FindObjectByName(scene, "StealthPreviewZone"); // 잠입 구역 조회
        if (stealthZone == null) // 잠입 구역 누락 확인
        {
            stealthZone = new GameObject("StealthPreviewZone"); // 잠입 구역 생성
            SceneManager.MoveGameObjectToScene(stealthZone, scene); // 테스트 씬으로 이동
            stealthZone.transform.position = new Vector3(-12f, 0f, -10f); // 잠입 구역 위치 지정
        }

        Material sensorMaterial = GetOrCreateMaterial("DetectionSensor_Mat", new Color(0.95f, 0.72f, 0.12f)); // 센서 재질 확보
        Material noiseMaterial = GetOrCreateMaterial("NoiseTest_Mat", new Color(0.65f, 0.20f, 0.85f)); // 소음 장치 재질 확보

        GameObject guardA = GetOrCreatePrimitive(stealthZone.transform, "DetectionGuard_A", PrimitiveType.Capsule, new Vector3(0f, 1f, 7f), new Vector3(1f, 1f, 1f), sensorMaterial); // 표준 센서 생성
        guardA.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 표준 센서 방향 적용
        ConfigureSensor(guardA, player, 18f, 90f, 10f); // 표준 센서 설정

        GameObject guardB = GetOrCreatePrimitive(stealthZone.transform, "DetectionGuard_B", PrimitiveType.Capsule, new Vector3(-3.6f, 1f, 3.5f), new Vector3(1f, 1f, 1f), sensorMaterial); // 보조 센서 생성
        guardB.transform.localRotation = Quaternion.Euler(0f, 150f, 0f); // 보조 센서 방향 적용
        ConfigureSensor(guardB, player, 14f, 115f, 7f); // 보조 센서 설정

        GameObject noiseButton = GetOrCreatePrimitive(stealthZone.transform, "NoiseTestEmitter", PrimitiveType.Cube, new Vector3(3.5f, 0.65f, -5f), new Vector3(1.2f, 1.3f, 1.2f), noiseMaterial); // 소음 테스트 장치 생성
        TestInteractable noiseInteractable = noiseButton.GetComponent<TestInteractable>(); // 소음 테스트 상호작용 조회
        if (noiseInteractable == null) // 컴포넌트 누락 확인
        {
            noiseInteractable = noiseButton.AddComponent<TestInteractable>(); // 소음 테스트 상호작용 추가
        }
        noiseInteractable.Configure("소음 테스트 장치", "큰 유인 소음 발생", true, 12f, NoiseType.Lure); // 소음 장치 설정 적용
        EditorUtility.SetDirty(noiseInteractable); // 소음 장치 변경 표시
    }

    private static void ConfigureSensor(GameObject sensorObject, Transform player, float distance, float angle, float hearing) // 탐지 센서 설정
    {
        DetectionSensor sensor = sensorObject.GetComponent<DetectionSensor>(); // 탐지 센서 조회
        if (sensor == null) // 탐지 센서 누락 확인
        {
            sensor = sensorObject.AddComponent<DetectionSensor>(); // 탐지 센서 추가
        }
        sensor.Configure(player, distance, angle, hearing, ~0); // 탐지 센서 설정 적용
        EditorUtility.SetDirty(sensor); // 센서 변경 표시
    }

    private static GameObject GetOrCreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material) // 테스트 프리미티브 확보
    {
        Transform existing = FindChildRecursive(parent, objectName); // 기존 객체 조회
        GameObject target = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitiveType); // 기존 또는 새 객체 선택
        if (existing == null) // 새 객체 확인
        {
            target.name = objectName; // 객체 이름 지정
            target.transform.SetParent(parent, false); // 부모 연결
        }
        target.transform.localPosition = localPosition; // 로컬 위치 적용
        target.transform.localScale = localScale; // 로컬 크기 적용
        Renderer renderer = target.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null && material != null) // 재질 적용 조건 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }
        return target; // 객체 반환
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        EnsureGeneratedMaterialFolder(); // 생성 재질 폴더 확보
        string assetPath = $"Assets/_Project/Materials/Generated/{fileName}.mat"; // 재질 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 재질 조회
        if (material != null) // 기존 재질 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 셰이더 조회
        shader = shader != null ? shader : Shader.Find("Standard"); // 대체 셰이더 적용
        material = new Material(shader); // 재질 생성
        material.name = fileName; // 재질 이름 지정
        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }
        if (material.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }
        AssetDatabase.CreateAsset(material, assetPath); // 재질 에셋 저장
        return material; // 재질 반환
    }

    private static void EnsureGeneratedMaterialFolder() // 생성 재질 폴더 확보
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) // 머티리얼 폴더 확인
        {
            AssetDatabase.CreateFolder("Assets/_Project", "Materials"); // 머티리얼 폴더 생성
        }
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials/Generated")) // 생성 재질 폴더 확인
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Generated"); // 생성 재질 폴더 생성
        }
    }

    private static void CloseSceneIfNeeded(Scene scene, bool sceneWasLoaded) // 임시 씬 종료 처리
    {
        if (!sceneWasLoaded) // 기존 로드 상태 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 임시 씬 닫기
        }
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
#endif // 에디터 전용 기능 종료