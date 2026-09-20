#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay5EnemySetup // E-01 경비 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day5.EnemySetup.V1"; // 세션 적용 키

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
        EditorApplication.delayCall += ApplySetup; // 지연 실행 예약
    }

    // 메뉴 항목 등록
    public static void ApplySetup() // E-01 경비 구성 적용
    {
        Scene testScene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = testScene.IsValid() && testScene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject stealthZone = FindObjectByName(testScene, "StealthPreviewZone"); // 잠입 구역 조회
        GameObject player = FindObjectByName(testScene, "Player"); // 플레이어 조회
        GameObject cameraObject = FindObjectByName(testScene, "Main Camera"); // 메인 카메라 조회

        if (stealthZone == null || player == null) // 필수 요소 확인
        {
            Debug.LogError("Project K Day 5 enemy setup failed: StealthPreviewZone or Player not found."); // 오류 로그 출력
            CloseSceneIfNeeded(testScene, wasLoaded); // 임시 씬 종료
            return; // 구성 중단
        }

        RemoveOldPlaceholder(stealthZone.transform, "DetectionGuard_A"); // 기존 센서 A 제거
        RemoveOldPlaceholder(stealthZone.transform, "DetectionGuard_B"); // 기존 센서 B 제거
        RemoveOldPlaceholder(stealthZone.transform, "E01_Guard_A"); // 기존 실제 경비 A 제거
        RemoveOldPlaceholder(stealthZone.transform, "E01_Guard_B"); // 기존 실제 경비 B 제거
        RemoveOldPlaceholder(stealthZone.transform, "E01_PatrolPoints"); // 기존 순찰 지점 제거

        Material armorMaterial = GetOrCreateMaterial("E01_Armor_Dark", new Color(0.09f, 0.12f, 0.16f)); // 어두운 방어구 재질 확보
        Material plateMaterial = GetOrCreateMaterial("E01_Armor_Plate", new Color(0.24f, 0.32f, 0.38f)); // 장갑판 재질 확보
        Material cyanMaterial = GetOrCreateMaterial("E01_Cyan_Light", new Color(0.10f, 0.82f, 0.95f)); // 청록 발광 재질 확보
        Material redMaterial = GetOrCreateMaterial("E01_Red_Sensor", new Color(0.95f, 0.18f, 0.22f)); // 적색 센서 재질 확보
        Material metalMaterial = GetOrCreateMaterial("E01_Metal", new Color(0.50f, 0.56f, 0.62f)); // 금속 재질 확보

        Transform patrolRoot = new GameObject("E01_PatrolPoints").transform; // 순찰 지점 루트 생성
        patrolRoot.SetParent(stealthZone.transform, false); // 잠입 구역에 연결

        Transform[] routeA = CreateRoute(patrolRoot, "A", new Vector3[] // 경비 A 순찰 지점 생성
        {
            new Vector3(-4f, 0.05f, -6.5f), // A1 위치
            new Vector3(-4f, 0.05f, 6.5f), // A2 위치
            new Vector3(-1f, 0.05f, 6.5f), // A3 위치
            new Vector3(-1f, 0.05f, -6.5f) // A4 위치
        });

        Transform[] routeB = CreateRoute(patrolRoot, "B", new Vector3[] // 경비 B 순찰 지점 생성
        {
            new Vector3(4f, 0.05f, 6.5f), // B1 위치
            new Vector3(4f, 0.05f, -6.5f), // B2 위치
            new Vector3(1f, 0.05f, -6.5f), // B3 위치
            new Vector3(1f, 0.05f, 6.5f) // B4 위치
        });

        Camera mainCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main; // 메인 카메라 참조 조회
        CreateGuard(stealthZone.transform, "E01_Guard_A", new Vector3(-4f, 0.05f, -6.5f), 0f, player.transform, mainCamera, routeA, armorMaterial, plateMaterial, cyanMaterial, redMaterial, metalMaterial); // 경비 A 생성
        CreateGuard(stealthZone.transform, "E01_Guard_B", new Vector3(4f, 0.05f, 6.5f), 180f, player.transform, mainCamera, routeB, armorMaterial, plateMaterial, cyanMaterial, redMaterial, metalMaterial); // 경비 B 생성

        EditorSceneManager.MarkSceneDirty(testScene); // 씬 변경 표시
        EditorSceneManager.SaveScene(testScene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(testScene, wasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 5 E-01 patrol guards setup complete."); // 완료 로그 출력
    }

    private static void CreateGuard(Transform parent, string objectName, Vector3 localPosition, float yaw, Transform player, Camera mainCamera, Transform[] patrolRoute, Material armorMaterial, Material plateMaterial, Material cyanMaterial, Material redMaterial, Material metalMaterial) // E-01 경비 생성
    {
        GameObject guard = new GameObject(objectName); // 경비 루트 생성
        guard.transform.SetParent(parent, false); // 부모 연결
        guard.transform.localPosition = localPosition; // 시작 위치 적용
        guard.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 시작 회전 적용

        CharacterController controller = guard.AddComponent<CharacterController>(); // 캐릭터 컨트롤러 추가
        controller.height = 1.9f; // 컨트롤러 높이 적용
        controller.radius = 0.34f; // 컨트롤러 반경 적용
        controller.center = new Vector3(0f, 0.95f, 0f); // 컨트롤러 중심 적용
        controller.stepOffset = 0.25f; // 계단 높이 적용
        controller.slopeLimit = 45f; // 경사 한계 적용

        DetectionSensor sensor = guard.AddComponent<DetectionSensor>(); // 탐지 센서 추가
        sensor.Configure(player, 18f, 90f, 10f, ~0); // E-01 탐지 수치 적용

        PatrolGuardAI patrolAI = guard.AddComponent<PatrolGuardAI>(); // 순찰 AI 추가
        patrolAI.Configure(player, patrolRoute, 3.5f, 5f); // E-01 이동 수치 적용

        DetectionBillboardUI billboard = guard.AddComponent<DetectionBillboardUI>(); // 탐지 UI 추가
        billboard.Configure(sensor, mainCamera, new Vector3(0f, 2.75f, 0f)); // 머리 위 UI 위치 설정

        Transform modelRoot = new GameObject("Model").transform; // 모델 루트 생성
        modelRoot.SetParent(guard.transform, false); // 경비 루트 연결
        BuildGuardModel(modelRoot, armorMaterial, plateMaterial, cyanMaterial, redMaterial, metalMaterial); // 상세 경비 모델 생성
    }

    private static void BuildGuardModel(Transform root, Material armorMaterial, Material plateMaterial, Material cyanMaterial, Material redMaterial, Material metalMaterial) // E-01 외형 생성
    {
        CreatePart("Pelvis", PrimitiveType.Cube, root, new Vector3(0f, 0.92f, 0f), new Vector3(0.55f, 0.28f, 0.34f), armorMaterial); // 골반 생성
        CreatePart("Torso", PrimitiveType.Capsule, root, new Vector3(0f, 1.38f, 0f), new Vector3(0.52f, 0.55f, 0.36f), armorMaterial); // 몸통 생성
        CreatePart("ChestPlate", PrimitiveType.Cube, root, new Vector3(0f, 1.48f, 0.24f), new Vector3(0.62f, 0.48f, 0.09f), plateMaterial, new Vector3(-4f, 0f, 0f)); // 흉부 장갑 생성
        CreatePart("ChestLight", PrimitiveType.Cube, root, new Vector3(0f, 1.52f, 0.295f), new Vector3(0.30f, 0.05f, 0.025f), cyanMaterial); // 흉부 발광선 생성
        CreatePart("Backpack", PrimitiveType.Cube, root, new Vector3(0f, 1.43f, -0.27f), new Vector3(0.46f, 0.56f, 0.18f), plateMaterial); // 등 장비 생성
        CreatePart("Neck", PrimitiveType.Cylinder, root, new Vector3(0f, 1.86f, 0f), new Vector3(0.12f, 0.10f, 0.12f), metalMaterial); // 목 연결부 생성
        CreatePart("Head", PrimitiveType.Sphere, root, new Vector3(0f, 2.12f, 0f), new Vector3(0.42f, 0.38f, 0.40f), armorMaterial); // 머리 생성
        CreatePart("FacePlate", PrimitiveType.Cube, root, new Vector3(0f, 2.10f, 0.22f), new Vector3(0.38f, 0.22f, 0.08f), plateMaterial); // 얼굴 장갑 생성
        CreatePart("Visor", PrimitiveType.Cube, root, new Vector3(0f, 2.14f, 0.27f), new Vector3(0.29f, 0.055f, 0.025f), redMaterial); // 바이저 생성
        CreatePart("Antenna", PrimitiveType.Cylinder, root, new Vector3(0.17f, 2.50f, -0.02f), new Vector3(0.025f, 0.16f, 0.025f), metalMaterial); // 안테나 생성
        CreatePart("AntennaLight", PrimitiveType.Sphere, root, new Vector3(0.17f, 2.70f, -0.02f), new Vector3(0.08f, 0.08f, 0.08f), cyanMaterial); // 안테나 신호등 생성

        CreateLimb(root, "LeftArm", -1f, armorMaterial, plateMaterial, metalMaterial); // 좌측 팔 생성
        CreateLimb(root, "RightArm", 1f, armorMaterial, plateMaterial, metalMaterial); // 우측 팔 생성
        CreateLeg(root, "LeftLeg", -1f, armorMaterial, plateMaterial, metalMaterial); // 좌측 다리 생성
        CreateLeg(root, "RightLeg", 1f, armorMaterial, plateMaterial, metalMaterial); // 우측 다리 생성

        CreatePart("SwordSheath", PrimitiveType.Cube, root, new Vector3(0.42f, 1.05f, -0.08f), new Vector3(0.10f, 0.72f, 0.12f), plateMaterial, new Vector3(10f, 0f, -8f)); // 검집 생성
        CreatePart("SwordHandle", PrimitiveType.Cylinder, root, new Vector3(0.43f, 1.78f, -0.06f), new Vector3(0.045f, 0.18f, 0.045f), cyanMaterial, new Vector3(10f, 0f, -8f)); // 검 손잡이 생성

        GameObject labelObject = new GameObject("E01_Label"); // 가슴 라벨 객체 생성
        labelObject.transform.SetParent(root, false); // 모델 루트 연결
        labelObject.transform.localPosition = new Vector3(0f, 1.60f, 0.345f); // 라벨 위치 적용
        labelObject.transform.localRotation = Quaternion.identity; // 라벨 회전 초기화
        TextMesh text = labelObject.AddComponent<TextMesh>(); // 텍스트 메쉬 추가
        text.text = "E-01"; // 경비 ID 표시
        text.fontSize = 36; // 글꼴 크기 적용
        text.characterSize = 0.055f; // 문자 크기 적용
        text.anchor = TextAnchor.MiddleCenter; // 텍스트 기준점 적용
        text.alignment = TextAlignment.Center; // 텍스트 정렬 적용
        text.color = cyanMaterial.color; // 텍스트 색상 적용
    }

    private static void CreateLimb(Transform root, string prefix, float side, Material armorMaterial, Material plateMaterial, Material metalMaterial) // 팔 모델 생성
    {
        CreatePart(prefix + "Shoulder", PrimitiveType.Sphere, root, new Vector3(0.42f * side, 1.63f, 0f), new Vector3(0.26f, 0.24f, 0.27f), plateMaterial); // 어깨 장갑 생성
        CreatePart(prefix + "Upper", PrimitiveType.Cylinder, root, new Vector3(0.52f * side, 1.38f, 0f), new Vector3(0.10f, 0.26f, 0.10f), armorMaterial, new Vector3(0f, 0f, 6f * side)); // 위팔 생성
        CreatePart(prefix + "Elbow", PrimitiveType.Sphere, root, new Vector3(0.56f * side, 1.12f, 0f), new Vector3(0.16f, 0.14f, 0.16f), metalMaterial); // 팔꿈치 생성
        CreatePart(prefix + "Lower", PrimitiveType.Cylinder, root, new Vector3(0.58f * side, 0.90f, 0f), new Vector3(0.09f, 0.22f, 0.09f), armorMaterial, new Vector3(0f, 0f, 3f * side)); // 아래팔 생성
        CreatePart(prefix + "Hand", PrimitiveType.Sphere, root, new Vector3(0.59f * side, 0.66f, 0.02f), new Vector3(0.14f, 0.13f, 0.14f), metalMaterial); // 손 생성
    }

    private static void CreateLeg(Transform root, string prefix, float side, Material armorMaterial, Material plateMaterial, Material metalMaterial) // 다리 모델 생성
    {
        CreatePart(prefix + "Thigh", PrimitiveType.Cylinder, root, new Vector3(0.18f * side, 0.65f, 0f), new Vector3(0.13f, 0.27f, 0.13f), armorMaterial); // 허벅지 생성
        CreatePart(prefix + "Knee", PrimitiveType.Sphere, root, new Vector3(0.18f * side, 0.37f, 0.03f), new Vector3(0.18f, 0.14f, 0.18f), plateMaterial); // 무릎 장갑 생성
        CreatePart(prefix + "Shin", PrimitiveType.Cylinder, root, new Vector3(0.18f * side, 0.18f, 0f), new Vector3(0.12f, 0.20f, 0.12f), metalMaterial); // 정강이 생성
        CreatePart(prefix + "Boot", PrimitiveType.Cube, root, new Vector3(0.18f * side, 0.06f, 0.10f), new Vector3(0.28f, 0.12f, 0.42f), armorMaterial); // 부츠 생성
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material) // 모델 부품 생성
    {
        return CreatePart(name, type, parent, position, scale, material, Vector3.zero); // 기본 회전 생성 호출
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 rotation) // 모델 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 프리미티브 부품 생성
        part.name = name; // 부품 이름 적용
        part.transform.SetParent(parent, false); // 모델 루트 연결
        part.transform.localPosition = position; // 부품 위치 적용
        part.transform.localRotation = Quaternion.Euler(rotation); // 부품 회전 적용
        part.transform.localScale = scale; // 부품 크기 적용
        Renderer renderer = part.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null) // 렌더러 확인
        {
            renderer.sharedMaterial = material; // 부품 재질 적용
        }
        Collider collider = part.GetComponent<Collider>(); // 부품 콜라이더 조회
        if (collider != null) // 부품 콜라이더 확인
        {
            Object.DestroyImmediate(collider); // 장식 콜라이더 제거
        }
        return part; // 생성 부품 반환
    }

    private static Transform[] CreateRoute(Transform parent, string prefix, Vector3[] points) // 순찰 지점 생성
    {
        Transform[] route = new Transform[points.Length]; // 순찰 배열 생성
        for (int i = 0; i < points.Length; i++) // 지점 순회
        {
            GameObject point = new GameObject("Patrol_" + prefix + (i + 1)); // 순찰 지점 객체 생성
            point.transform.SetParent(parent, false); // 순찰 루트 연결
            point.transform.localPosition = points[i]; // 순찰 지점 위치 적용
            Vector3 next = points[(i + 1) % points.Length]; // 다음 지점 위치 조회
            Vector3 forward = next - points[i]; // 지점 전방 방향 계산
            forward.y = 0f; // 수직 성분 제거
            point.transform.localRotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity; // 지점 방향 적용
            route[i] = point.transform; // 순찰 배열 저장
        }
        return route; // 순찰 배열 반환
    }

    private static void RemoveOldPlaceholder(Transform parent, string objectName) // 기존 오브젝트 제거
    {
        Transform existing = FindChildRecursive(parent, objectName); // 기존 오브젝트 조회
        if (existing != null) // 기존 오브젝트 확인
        {
            Object.DestroyImmediate(existing.gameObject); // 기존 오브젝트 삭제
        }
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        EnsureGeneratedMaterialFolder(); // 생성 재질 폴더 확보
        string assetPath = "Assets/_Project/Materials/Generated/" + fileName + ".mat"; // 재질 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 재질 조회
        if (material != null) // 기존 재질 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 셰이더 조회
        shader = shader != null ? shader : Shader.Find("Standard"); // 기본 셰이더 대체
        material = new Material(shader); // 재질 생성
        material.name = fileName; // 재질 이름 적용
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
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials/Generated")) // 생성 폴더 확인
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Generated"); // 생성 폴더 생성
        }
    }

    private static void CloseSceneIfNeeded(Scene scene, bool wasLoaded) // 임시 씬 종료
    {
        if (!wasLoaded) // 기존 로드 여부 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 임시 씬 닫기
        }
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 씬 내부 오브젝트 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 목록 조회
        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName); // 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 오브젝트 반환
            }
        }
        return null; // 검색 실패 반환
    }

    private static Transform FindChildRecursive(Transform current, string objectName) // 자식 재귀 검색
    {
        if (current.name == objectName) // 현재 이름 확인
        {
            return current; // 현재 트랜스폼 반환
        }
        for (int i = 0; i < current.childCount; i++) // 자식 순회
        {
            Transform found = FindChildRecursive(current.GetChild(i), objectName); // 자식 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 결과 반환
            }
        }
        return null; // 검색 실패 반환
    }
}
#endif // 에디터 전용 기능 종료