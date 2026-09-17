#if UNITY_EDITOR // 에디터 전용 컴파일
using System.IO; // 파일 저장 기능
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay8Setup // 8일차 받아치기 자세 E-02 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions"; // 입력 에셋 경로
    private const string SessionKey = "ProjectK.Day8.Setup.V4"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 구성 예약
    {
        if (SessionState.GetBool(SessionKey, false)) // 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplySetup; // 컴파일 후 구성 예약
    }

    [MenuItem("Project K/Day 8/Setup Parry Posture E-02")] // 수동 구성 메뉴
    public static void ApplySetup() // 8일차 구성 적용
    {
        ConfigureDefenseInput(); // RMB 방어 입력 구성

        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(scene, "Player"); // 플레이어 조회
        GameObject combatZone = FindObjectByName(scene, "CombatPreviewZone"); // 전투 구역 조회
        GameObject cameraObject = FindObjectByName(scene, "Main Camera"); // 메인 카메라 조회
        Camera mainCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main; // 카메라 참조 보정

        if (player == null || combatZone == null) // 필수 객체 확인
        {
            Debug.LogError("Project K Day 8 setup failed: Player or CombatPreviewZone not found."); // 오류 로그 출력
            CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
            return; // 구성 중단
        }

        ConfigurePlayer(player); // 플레이어 방어 체력 구성
        ConfigureExistingEnemies(scene, mainCamera); // 기존 적 UI 정리 및 재구성
        CreateE02Guard(combatZone.transform, player.transform, mainCamera); // E-02 생성

        EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 테스트 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 8 combat UI layout fix complete."); // 완료 로그 출력
    }

    private static void ConfigureDefenseInput() // 방어 입력 구성
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath); // 원본 입력 에셋 조회

        if (inputAsset == null) // 입력 에셋 확인
        {
            Debug.LogWarning("Project K Day 8: InputSystem_Actions.inputactions not found. RMB fallback remains available."); // 입력 에셋 경고 출력
            return; // 입력 설정 중단
        }

        InputActionAsset editableAsset = Object.Instantiate(inputAsset); // 활성 상태와 분리된 입력 에셋 복제본 생성
        editableAsset.name = inputAsset.name; // 원본 입력 에셋 이름 유지

        try // 복제본 수정 보호 구간
        {
            for (int i = 0; i < editableAsset.actionMaps.Count; i++) // 복제본 액션 맵 순회
            {
                editableAsset.actionMaps[i].Disable(); // 복제본 액션 비활성화
            }

            InputActionMap playerMap = editableAsset.FindActionMap("Player", false); // 복제본 플레이어 액션 맵 조회

            if (playerMap == null) // 액션 맵 확인
            {
                Debug.LogWarning("Project K Day 8: Player action map not found. RMB fallback remains available."); // 액션 맵 경고 출력
                return; // 입력 설정 중단
            }

            InputAction defenseAction = playerMap.FindAction("Defense", false); // 기존 방어 액션 조회

            if (defenseAction == null) // 방어 액션 누락 확인
            {
                defenseAction = playerMap.AddAction("Defense", InputActionType.Button); // 복제본에 방어 액션 추가
            }

            bool mouseBindingFound = false; // 마우스 바인딩 상태 초기화

            for (int i = 0; i < defenseAction.bindings.Count; i++) // 방어 바인딩 순회
            {
                if (defenseAction.bindings[i].path == "<Mouse>/rightButton") // RMB 바인딩 확인
                {
                    mouseBindingFound = true; // RMB 바인딩 존재 저장
                    break; // 검색 종료
                }
            }

            if (!mouseBindingFound) // RMB 바인딩 누락 확인
            {
                defenseAction.AddBinding("<Mouse>/rightButton").WithGroup("Keyboard&Mouse"); // 복제본에 RMB 바인딩 추가
            }

            string json = editableAsset.ToJson(); // 수정된 입력 에셋 JSON 생성
            File.WriteAllText(InputAssetPath, json); // 입력 액션 원본 파일에 JSON 저장
        }
        finally // 복제본 정리 구간
        {
            Object.DestroyImmediate(editableAsset); // 임시 입력 에셋 복제본 제거
        }

        AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceUpdate); // 수정된 입력 액션 파일 재임포트
        AssetDatabase.Refresh(); // 에셋 데이터 새로고침
    }

    private static void ConfigurePlayer(GameObject player) // 플레이어 전투 방어 구성
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>(); // 플레이어 체력 조회

        if (health == null) // 체력 관리자 누락 확인
        {
            health = player.AddComponent<PlayerHealth>(); // 체력 관리자 추가
        }

        PlayerDefenseController defense = player.GetComponent<PlayerDefenseController>(); // 방어 관리자 조회

        if (defense == null) // 방어 관리자 누락 확인
        {
            defense = player.AddComponent<PlayerDefenseController>(); // 방어 관리자 추가
        }

        EditorUtility.SetDirty(health); // 체력 관리자 변경 표시
        EditorUtility.SetDirty(defense); // 방어 관리자 변경 표시
    }

    private static void ConfigureExistingEnemies(Scene scene, Camera mainCamera) // 기존 적 UI 재구성
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 씬 루트 목록 조회

        for (int i = 0; i < roots.Length; i++) // 씬 루트 순회
        {
            ConfigureEnemyRecursive(roots[i].transform, mainCamera); // 하위 적 재귀 구성
        }
    }

    private static void ConfigureEnemyRecursive(Transform current, Camera mainCamera) // 적 재귀 구성
    {
        EnemyActor actor = current.GetComponent<EnemyActor>(); // 적 생명 관리자 조회

        if (actor != null) // 적 객체 확인
        {
            RebuildCombatBillboard(current.gameObject, actor, mainCamera); // 통합 UI 재구성
        }

        for (int i = 0; i < current.childCount; i++) // 자식 순회
        {
            ConfigureEnemyRecursive(current.GetChild(i), mainCamera); // 자식 재귀 구성
        }
    }

    private static void RebuildCombatBillboard(GameObject enemy, EnemyActor actor, Camera mainCamera) // 통합 UI 재구성
    {
        ClearChildCanvases(enemy.transform); // 기존 하위 캔버스 제거

        DetectionBillboardUI[] oldDetections = enemy.GetComponents<DetectionBillboardUI>(); // 기존 탐지 UI 목록 조회

        for (int i = 0; i < oldDetections.Length; i++) // 기존 탐지 UI 순회
        {
            Object.DestroyImmediate(oldDetections[i]); // 기존 탐지 UI 컴포넌트 제거
        }

        EnemyPostureBillboardUI[] oldPostures = enemy.GetComponents<EnemyPostureBillboardUI>(); // 기존 자세 UI 목록 조회

        for (int i = 0; i < oldPostures.Length; i++) // 기존 자세 UI 순회
        {
            Object.DestroyImmediate(oldPostures[i]); // 기존 자세 UI 컴포넌트 제거
        }

        EnemyCombatBillboardUI[] combatUIs = enemy.GetComponents<EnemyCombatBillboardUI>(); // 통합 UI 목록 조회
        EnemyCombatBillboardUI combatUI = combatUIs.Length > 0 ? combatUIs[0] : null; // 첫 통합 UI 선택

        for (int i = 1; i < combatUIs.Length; i++) // 중복 통합 UI 순회
        {
            Object.DestroyImmediate(combatUIs[i]); // 중복 통합 UI 컴포넌트 제거
        }

        if (combatUI == null) // 통합 UI 누락 확인
        {
            combatUI = enemy.AddComponent<EnemyCombatBillboardUI>(); // 통합 UI 추가
        }

        DetectionSensor sensor = enemy.GetComponent<DetectionSensor>(); // 탐지 센서 조회
        EnemyMeleeCombat meleeCombat = enemy.GetComponent<EnemyMeleeCombat>(); // 적 전투 조회
        combatUI.Configure(actor, sensor, meleeCombat, mainCamera, new Vector3(0f, 2.68f, 0f)); // 통합 UI 설정
        EditorUtility.SetDirty(combatUI); // 통합 UI 변경 표시
    }

    private static void ClearChildCanvases(Transform root) // 기존 하위 캔버스 정리
    {
        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true); // 하위 캔버스 목록 조회

        for (int i = 0; i < canvases.Length; i++) // 캔버스 순회
        {
            if (canvases[i] == null) // 유효성 확인
            {
                continue; // 무효 객체 제외
            }

            if (canvases[i].transform == root) // 루트 객체 확인
            {
                continue; // 루트 객체 제외
            }

            Object.DestroyImmediate(canvases[i].gameObject); // 하위 캔버스 제거
        }
    }

    private static void CreateE02Guard(Transform parent, Transform player, Camera mainCamera) // E-02 검술 호위병 생성
    {
        RemoveChild(parent, "E02_SwordGuard"); // 기존 E-02 제거

        Material armorMaterial = GetOrCreateMaterial("E02_Armor_Dark", new Color(0.10f, 0.11f, 0.15f)); // 어두운 장갑 재질 확보
        Material plateMaterial = GetOrCreateMaterial("E02_Armor_Plate", new Color(0.34f, 0.28f, 0.30f)); // 붉은 장갑판 재질 확보
        Material orangeMaterial = GetOrCreateMaterial("E02_Orange_Light", new Color(1f, 0.42f, 0.08f)); // 주황 발광 재질 확보
        Material metalMaterial = GetOrCreateMaterial("E02_Metal", new Color(0.58f, 0.60f, 0.64f)); // 금속 재질 확보

        GameObject guard = new GameObject("E02_SwordGuard"); // E-02 루트 생성
        guard.transform.SetParent(parent, false); // 전투 구역 연결
        guard.transform.localPosition = new Vector3(0f, 0.05f, 6.3f); // E-02 위치 적용
        guard.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 시작 방향 적용

        CharacterController controller = guard.AddComponent<CharacterController>(); // 캐릭터 컨트롤러 추가
        controller.height = 2f; // 컨트롤러 높이 적용
        controller.radius = 0.36f; // 컨트롤러 반경 적용
        controller.center = new Vector3(0f, 1f, 0f); // 컨트롤러 중심 적용
        controller.stepOffset = 0.25f; // 계단 높이 적용

        DetectionSensor sensor = guard.AddComponent<DetectionSensor>(); // 탐지 센서 추가
        sensor.Configure(player, 20f, 100f, 12f, ~0); // E-02 탐지 수치 적용

        EnemyActor actor = guard.AddComponent<EnemyActor>(); // 적 생명 관리자 추가
        actor.Configure(130f, 120f, true); // E-02 HP 자세 암살 설정

        EnemyMeleeCombat melee = guard.AddComponent<EnemyMeleeCombat>(); // 적 근접 전투 추가
        E02SwordGuardAI ai = guard.AddComponent<E02SwordGuardAI>(); // E-02 AI 추가
        ai.Configure(player, 3.8f, 5.5f, 2.25f); // E-02 이동 공격 수치 적용

        VisionSectorVisual vision = guard.AddComponent<VisionSectorVisual>(); // 시야 부채꼴 추가
        vision.Configure(guard.transform, 20f, 100f, 0.04f, new Color(1f, 0.25f, 0.25f, 0.10f)); // E-02 시야 표시 설정

        Transform modelRoot = new GameObject("Model").transform; // 모델 루트 생성
        modelRoot.SetParent(guard.transform, false); // E-02 루트 연결
        BuildE02Model(modelRoot, armorMaterial, plateMaterial, orangeMaterial, metalMaterial, out Transform weaponSocket); // E-02 모델 생성
        melee.Configure(weaponSocket, 22f, 36f); // E-02 공격 피해 설정
        RebuildCombatBillboard(guard, actor, mainCamera); // E-02 통합 UI 구성
    }

    private static void BuildE02Model(Transform root, Material armor, Material plate, Material glow, Material metal, out Transform weaponSocket) // E-02 외형 생성
    {
        CreatePart("Torso", PrimitiveType.Cube, root, new Vector3(0f, 1.20f, 0f), new Vector3(0.72f, 0.76f, 0.40f), armor); // 몸통 생성
        CreatePart("ChestPlate", PrimitiveType.Cube, root, new Vector3(0f, 1.27f, 0.24f), new Vector3(0.58f, 0.48f, 0.08f), plate); // 흉부 장갑 생성
        CreatePart("Head", PrimitiveType.Cube, root, new Vector3(0f, 1.83f, 0f), new Vector3(0.48f, 0.40f, 0.42f), armor); // 머리 생성
        CreatePart("Visor", PrimitiveType.Cube, root, new Vector3(0f, 1.84f, 0.235f), new Vector3(0.36f, 0.08f, 0.045f), glow); // 바이저 생성
        CreatePart("LeftShoulder", PrimitiveType.Sphere, root, new Vector3(-0.48f, 1.46f, 0f), new Vector3(0.32f, 0.22f, 0.36f), plate); // 왼쪽 어깨 장갑 생성
        CreatePart("RightShoulder", PrimitiveType.Sphere, root, new Vector3(0.48f, 1.46f, 0f), new Vector3(0.32f, 0.22f, 0.36f), plate); // 오른쪽 어깨 장갑 생성
        CreatePart("LeftArm", PrimitiveType.Capsule, root, new Vector3(-0.48f, 1.05f, 0f), new Vector3(0.18f, 0.48f, 0.18f), armor); // 왼팔 생성
        CreatePart("RightArm", PrimitiveType.Capsule, root, new Vector3(0.48f, 1.05f, 0f), new Vector3(0.18f, 0.48f, 0.18f), armor); // 오른팔 생성
        CreatePart("LeftLeg", PrimitiveType.Capsule, root, new Vector3(-0.21f, 0.48f, 0f), new Vector3(0.22f, 0.56f, 0.22f), armor); // 왼다리 생성
        CreatePart("RightLeg", PrimitiveType.Capsule, root, new Vector3(0.21f, 0.48f, 0f), new Vector3(0.22f, 0.56f, 0.22f), armor); // 오른다리 생성
        CreatePart("WaistPlate", PrimitiveType.Cube, root, new Vector3(0f, 0.77f, 0f), new Vector3(0.70f, 0.18f, 0.34f), plate); // 허리 장갑 생성
        CreatePart("BackCore", PrimitiveType.Cube, root, new Vector3(0f, 1.28f, -0.26f), new Vector3(0.30f, 0.32f, 0.12f), glow); // 후면 코어 생성

        weaponSocket = new GameObject("SwordSocket").transform; // 검 회전 소켓 생성
        weaponSocket.SetParent(root, false); // 모델 루트 연결
        weaponSocket.localPosition = new Vector3(0.52f, 1.12f, 0.26f); // 검 위치 적용
        weaponSocket.localRotation = Quaternion.Euler(18f, 0f, -24f); // 검 기본 회전 적용
        CreatePart("SwordHandle", PrimitiveType.Cylinder, weaponSocket, new Vector3(0f, -0.28f, 0f), new Vector3(0.075f, 0.30f, 0.075f), armor); // 검 손잡이 생성
        CreatePart("SwordGuard", PrimitiveType.Cube, weaponSocket, new Vector3(0f, 0.03f, 0f), new Vector3(0.48f, 0.08f, 0.12f), plate); // 검 가드 생성
        CreatePart("SwordBlade", PrimitiveType.Cube, weaponSocket, new Vector3(0f, 0.82f, 0f), new Vector3(0.12f, 1.50f, 0.06f), metal); // 검날 생성
        CreatePart("SwordEdge", PrimitiveType.Cube, weaponSocket, new Vector3(0.075f, 0.82f, 0f), new Vector3(0.025f, 1.50f, 0.07f), glow); // 발광 검날 생성

        CreateText(root, "E-02", new Vector3(0f, 2.36f, 0f), glow.color); // E-02 라벨 생성
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) // 모델 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 프리미티브 생성
        part.name = name; // 부품 이름 지정
        part.transform.SetParent(parent, false); // 부모 연결
        part.transform.localPosition = localPosition; // 위치 적용
        part.transform.localRotation = Quaternion.identity; // 회전 초기화
        part.transform.localScale = localScale; // 크기 적용

        Renderer renderer = part.GetComponent<Renderer>(); // 렌더러 조회

        if (renderer != null) // 렌더러 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }

        Collider collider = part.GetComponent<Collider>(); // 장식 콜라이더 조회

        if (collider != null) // 콜라이더 확인
        {
            Object.DestroyImmediate(collider); // 장식 콜라이더 제거
        }

        return part; // 생성 부품 반환
    }

    private static void CreateText(Transform parent, string text, Vector3 localPosition, Color color) // 라벨 생성
    {
        GameObject textObject = new GameObject("Label"); // 텍스트 객체 생성
        textObject.transform.SetParent(parent, false); // 부모 연결
        textObject.transform.localPosition = localPosition; // 위치 적용
        textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 방향 적용
        TextMesh textMesh = textObject.AddComponent<TextMesh>(); // 텍스트 메쉬 추가
        textMesh.text = text; // 표시 문자열 적용
        textMesh.fontSize = 38; // 글자 크기 적용
        textMesh.characterSize = 0.06f; // 문자 크기 적용
        textMesh.anchor = TextAnchor.MiddleCenter; // 기준점 적용
        textMesh.alignment = TextAlignment.Center; // 중앙 정렬 적용
        textMesh.color = color; // 글자 색상 적용
    }

    private static void RemoveChild(Transform parent, string childName) // 기존 자식 제거
    {
        Transform target = FindChildRecursive(parent, childName); // 기존 자식 검색

        if (target != null) // 기존 자식 확인
        {
            Object.DestroyImmediate(target.gameObject); // 기존 자식 제거
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName) // 자식 재귀 검색
    {
        if (parent.name == childName) // 현재 이름 확인
        {
            return parent; // 현재 객체 반환
        }

        for (int i = 0; i < parent.childCount; i++) // 자식 순회
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName); // 하위 검색

            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 씬 객체 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회

        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName); // 하위 객체 검색

            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        EnsureFolder("Assets/_Project", "Materials"); // 머티리얼 폴더 확보
        EnsureFolder("Assets/_Project/Materials", "Generated"); // 생성 재질 폴더 확보
        string path = "Assets/_Project/Materials/Generated/" + fileName + ".mat"; // 재질 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

        if (material != null) // 기존 재질 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit 셰이더 조회
        shader = shader != null ? shader : Shader.Find("Standard"); // 대체 셰이더 적용
        material = new Material(shader); // 새 재질 생성
        material.name = fileName; // 재질 이름 지정

        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }

        if (material.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }

        AssetDatabase.CreateAsset(material, path); // 재질 에셋 생성
        return material; // 재질 반환
    }

    private static void EnsureFolder(string parent, string folderName) // 폴더 확보
    {
        string fullPath = parent + "/" + folderName; // 전체 경로 계산

        if (!AssetDatabase.IsValidFolder(fullPath)) // 폴더 존재 확인
        {
            AssetDatabase.CreateFolder(parent, folderName); // 폴더 생성
        }
    }

    private static void CloseSceneIfNeeded(Scene scene, bool wasLoaded) // 임시 씬 종료
    {
        if (!wasLoaded) // 임시 로드 상태 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 테스트 씬 종료
        }
    }
}
#endif
