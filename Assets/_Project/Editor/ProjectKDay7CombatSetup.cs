#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay7CombatSetup // 7일차 암살 전투 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day7.CombatSetup.V2"; // 세션 적용 키

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
        EditorApplication.delayCall += ApplySetup; // 컴파일 후 구성 예약
    }

    [MenuItem("Project K/Day 7/Setup Assassination And Combat")] // 수동 구성 메뉴
    public static void ApplySetup() // 암살 전투 구성 적용
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(scene, "Player"); // 플레이어 조회
        GameObject combatZone = FindObjectByName(scene, "CombatPreviewZone"); // 전투 구역 조회

        if (combatZone == null) // 전투 구역 확인
        {
            combatZone = FindObjectByName(scene, "StealthPreviewZone"); // 대체 잠입 구역 조회
        }

        if (player == null || combatZone == null) // 필수 객체 확인
        {
            Debug.LogError("Project K Day 7 combat setup failed: Player or test zone not found."); // 오류 로그 출력
            CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
            return; // 구성 중단
        }

        ConfigurePlayerCombat(player); // 플레이어 전투 구성
        ConfigureExistingGuards(scene); // 기존 E-01 전투 구성
        ConfigureTestRobots(combatZone.transform); // 고정 테스트 로봇 구성

        EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 7 assassination and combat setup complete."); // 완료 로그 출력
    }

    private static void ConfigurePlayerCombat(GameObject player) // 플레이어 전투 구성
    {
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>(); // 전투 관리자 조회

        if (combat == null) // 전투 관리자 누락 확인
        {
            combat = player.AddComponent<PlayerCombatController>(); // 전투 관리자 추가
        }

        PlayerAssassination assassination = player.GetComponent<PlayerAssassination>(); // 암살 관리자 조회

        if (assassination == null) // 암살 관리자 누락 확인
        {
            assassination = player.AddComponent<PlayerAssassination>(); // 암살 관리자 추가
        }

        Transform oldSocket = player.transform.Find("WeaponSocket_Day7"); // 기존 무기 소켓 조회

        if (oldSocket != null) // 기존 무기 존재 확인
        {
            Object.DestroyImmediate(oldSocket.gameObject); // 기존 무기 제거
        }

        Material darkMaterial = GetOrCreateMaterial("Day7_Sword_Dark", new Color(0.08f, 0.10f, 0.13f)); // 검 손잡이 재질 확보
        Material metalMaterial = GetOrCreateMaterial("Day7_Sword_Metal", new Color(0.56f, 0.62f, 0.68f)); // 검 금속 재질 확보
        Material cyanMaterial = GetOrCreateMaterial("Day7_Sword_Cyan", new Color(0.08f, 0.88f, 0.98f)); // 검 발광 재질 확보

        Transform socket = new GameObject("WeaponSocket_Day7").transform; // 무기 소켓 생성
        socket.SetParent(player.transform, false); // 플레이어 하위 연결
        socket.localPosition = new Vector3(0.48f, 1.05f, 0.42f); // 무기 위치 적용
        socket.localRotation = Quaternion.Euler(20f, 0f, -22f); // 무기 기본 회전 적용

        CreatePart("Handle", PrimitiveType.Cylinder, socket, new Vector3(0f, -0.28f, 0f), new Vector3(0.08f, 0.30f, 0.08f), darkMaterial); // 검 손잡이 생성
        CreatePart("Pommel", PrimitiveType.Sphere, socket, new Vector3(0f, -0.60f, 0f), new Vector3(0.13f, 0.13f, 0.13f), cyanMaterial); // 손잡이 끝 생성
        CreatePart("Guard", PrimitiveType.Cube, socket, new Vector3(0f, 0.04f, 0f), new Vector3(0.52f, 0.08f, 0.12f), metalMaterial); // 검 가드 생성
        CreatePart("Blade", PrimitiveType.Cube, socket, new Vector3(0f, 0.78f, 0f), new Vector3(0.12f, 1.42f, 0.06f), metalMaterial); // 검날 생성
        CreatePart("EnergyEdge", PrimitiveType.Cube, socket, new Vector3(0.075f, 0.78f, 0f), new Vector3(0.025f, 1.42f, 0.07f), cyanMaterial); // 검날 발광선 생성

        combat.ConfigureWeapon(socket); // 전투 관리자에 무기 연결
        EditorUtility.SetDirty(combat); // 전투 관리자 변경 표시
        EditorUtility.SetDirty(assassination); // 암살 관리자 변경 표시
    }

    private static void ConfigureExistingGuards(Scene scene) // 기존 경비 전투 구성
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 씬 루트 목록 조회

        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            ConfigureGuardRecursive(roots[i].transform); // 하위 경비 재귀 구성
        }
    }

    private static void ConfigureGuardRecursive(Transform current) // 경비 재귀 구성
    {
        if (current.name.StartsWith("E01_")) // E-01 이름 확인
        {
            EnemyActor actor = current.GetComponent<EnemyActor>(); // 적 생명 관리자 조회

            if (actor == null) // 적 생명 관리자 누락 확인
            {
                actor = current.gameObject.AddComponent<EnemyActor>(); // 적 생명 관리자 추가
            }

            actor.Configure(100f, 100f, true); // E-01 기획 수치 적용
            EditorUtility.SetDirty(actor); // 적 생명 관리자 변경 표시
        }

        for (int i = 0; i < current.childCount; i++) // 자식 순회
        {
            ConfigureGuardRecursive(current.GetChild(i)); // 자식 재귀 구성
        }
    }

    private static void ConfigureTestRobots(Transform zone) // 테스트 로봇 구성
    {
        RemoveChild(zone, "AssassinationTestRobot"); // 기존 암살 로봇 제거
        RemoveChild(zone, "CombatTestRobot"); // 기존 전투 로봇 제거

        Material armorMaterial = GetOrCreateMaterial("Day7_TestRobot_Armor", new Color(0.12f, 0.15f, 0.19f)); // 로봇 장갑 재질 확보
        Material plateMaterial = GetOrCreateMaterial("Day7_TestRobot_Plate", new Color(0.30f, 0.36f, 0.42f)); // 로봇 판금 재질 확보
        Material cyanMaterial = GetOrCreateMaterial("Day7_TestRobot_Cyan", new Color(0.08f, 0.85f, 0.95f)); // 로봇 청록 재질 확보
        Material redMaterial = GetOrCreateMaterial("Day7_TestRobot_Red", new Color(0.90f, 0.18f, 0.20f)); // 로봇 적색 재질 확보

        CreateTestRobot(zone, "AssassinationTestRobot", new Vector3(-2.4f, 0.05f, 4.2f), 0f, "ASSASSINATION TEST", armorMaterial, plateMaterial, cyanMaterial, redMaterial); // 암살 연습 로봇 생성
        CreateTestRobot(zone, "CombatTestRobot", new Vector3(2.4f, 0.05f, 4.2f), 180f, "COMBAT TEST", armorMaterial, plateMaterial, cyanMaterial, redMaterial); // 전투 연습 로봇 생성
    }

    private static void CreateTestRobot(Transform parent, string objectName, Vector3 localPosition, float yaw, string label, Material armorMaterial, Material plateMaterial, Material cyanMaterial, Material redMaterial) // 고정 로봇 생성
    {
        GameObject robot = new GameObject(objectName); // 로봇 루트 생성
        robot.transform.SetParent(parent, false); // 테스트 구역 연결
        robot.transform.localPosition = localPosition; // 로봇 위치 적용
        robot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 로봇 방향 적용

        CapsuleCollider collider = robot.AddComponent<CapsuleCollider>(); // 루트 충돌체 추가
        collider.height = 1.9f; // 충돌체 높이 적용
        collider.radius = 0.34f; // 충돌체 반경 적용
        collider.center = new Vector3(0f, 0.95f, 0f); // 충돌체 중심 적용

        EnemyActor actor = robot.AddComponent<EnemyActor>(); // 적 생명 관리자 추가
        actor.Configure(100f, 100f, true); // 테스트 로봇 능력치 적용

        Transform model = new GameObject("Model").transform; // 모델 루트 생성
        model.SetParent(robot.transform, false); // 로봇 루트 연결

        CreatePart("Torso", PrimitiveType.Cube, model, new Vector3(0f, 1.18f, 0f), new Vector3(0.68f, 0.72f, 0.38f), armorMaterial); // 몸통 생성
        CreatePart("ChestPlate", PrimitiveType.Cube, model, new Vector3(0f, 1.24f, 0.22f), new Vector3(0.52f, 0.46f, 0.08f), plateMaterial); // 흉부 장갑 생성
        CreatePart("Head", PrimitiveType.Cube, model, new Vector3(0f, 1.78f, 0f), new Vector3(0.46f, 0.38f, 0.40f), armorMaterial); // 머리 생성
        CreatePart("Visor", PrimitiveType.Cube, model, new Vector3(0f, 1.80f, 0.23f), new Vector3(0.34f, 0.09f, 0.05f), redMaterial); // 바이저 생성
        CreatePart("Core", PrimitiveType.Cube, model, new Vector3(0f, 1.20f, 0.27f), new Vector3(0.16f, 0.16f, 0.04f), cyanMaterial); // 가슴 코어 생성
        CreatePart("LeftArm", PrimitiveType.Capsule, model, new Vector3(-0.48f, 1.17f, 0f), new Vector3(0.18f, 0.52f, 0.18f), armorMaterial); // 왼팔 생성
        CreatePart("RightArm", PrimitiveType.Capsule, model, new Vector3(0.48f, 1.17f, 0f), new Vector3(0.18f, 0.52f, 0.18f), armorMaterial); // 오른팔 생성
        CreatePart("LeftLeg", PrimitiveType.Capsule, model, new Vector3(-0.20f, 0.48f, 0f), new Vector3(0.20f, 0.55f, 0.20f), armorMaterial); // 왼다리 생성
        CreatePart("RightLeg", PrimitiveType.Capsule, model, new Vector3(0.20f, 0.48f, 0f), new Vector3(0.20f, 0.55f, 0.20f), armorMaterial); // 오른다리 생성
        CreateText(model, label, new Vector3(0f, 2.35f, 0f), cyanMaterial.color); // 테스트 라벨 생성
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) // 모델 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 프리미티브 생성
        part.name = name; // 부품 이름 지정
        part.transform.SetParent(parent, false); // 모델 루트 연결
        part.transform.localPosition = localPosition; // 부품 위치 적용
        part.transform.localRotation = Quaternion.identity; // 부품 회전 초기화
        part.transform.localScale = localScale; // 부품 크기 적용

        Renderer renderer = part.GetComponent<Renderer>(); // 렌더러 조회

        if (renderer != null) // 렌더러 존재 확인
        {
            renderer.sharedMaterial = material; // 부품 재질 적용
        }

        Collider collider = part.GetComponent<Collider>(); // 장식 콜라이더 조회

        if (collider != null) // 장식 콜라이더 존재 확인
        {
            Object.DestroyImmediate(collider); // 장식 콜라이더 제거
        }

        return part; // 생성 부품 반환
    }

    private static void CreateText(Transform parent, string text, Vector3 localPosition, Color color) // 테스트 라벨 생성
    {
        GameObject textObject = new GameObject("TestLabel"); // 텍스트 객체 생성
        textObject.transform.SetParent(parent, false); // 모델 루트 연결
        textObject.transform.localPosition = localPosition; // 텍스트 위치 적용
        textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 텍스트 방향 적용
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

        if (target != null) // 기존 자식 존재 확인
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
            Transform found = FindChildRecursive(parent.GetChild(i), childName); // 자식 재귀 검색

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

        if (material != null) // 기존 재질 존재 확인
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
#endif // 에디터 전용 컴파일 종료
