#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay6CameraVisualUpgrade // D-01 카메라 시각 강화 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day6.CameraVisualUpgrade.V1"; // 세션 적용 키

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

    [MenuItem("Project K/Day 6/Upgrade D-01 Camera Visuals")] // 메뉴 항목 등록
    public static void ApplySetup() // D-01 카메라 적용
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = scene.IsValid() && scene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 씬 미로드 확인
        {
            scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject stealthZone = FindObjectByName(scene, "StealthPreviewZone"); // 잠입 구역 조회
        GameObject player = FindObjectByName(scene, "Player"); // 플레이어 조회
        Camera mainCamera = FindMainCamera(scene); // 메인 카메라 조회

        if (stealthZone == null) // 잠입 구역 확인
        {
            Debug.LogError("Project K Day 6 camera visual upgrade failed: StealthPreviewZone not found."); // 오류 로그 출력
            CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
            return; // 처리 중단
        }

        Transform root = EnsureRoot(stealthZone.transform, "D01_CameraRigRoot"); // 카메라 루트 확보
        RemoveChild(root, "D01_Camera_A"); // 기존 카메라 A 제거
        RemoveChild(root, "D01_Camera_B"); // 기존 카메라 B 제거

        Material frameMaterial = GetOrCreateMaterial("D01_Frame_Dark", new Color(0.10f, 0.13f, 0.17f)); // 프레임 재질 확보
        Material plateMaterial = GetOrCreateMaterial("D01_Plate_Mid", new Color(0.28f, 0.34f, 0.40f)); // 장갑 재질 확보
        Material glowCyanMaterial = GetOrCreateMaterial("D01_Glow_Cyan", new Color(0.10f, 0.88f, 0.95f)); // 청록 발광 재질 확보
        Material glowRedMaterial = GetOrCreateMaterial("D01_Glow_Red", new Color(0.95f, 0.20f, 0.24f)); // 적색 발광 재질 확보
        Material metalMaterial = GetOrCreateMaterial("D01_Metal_Light", new Color(0.60f, 0.66f, 0.72f)); // 금속 재질 확보
        Material yellowMaterial = GetOrCreateMaterial("D01_Label_Yellow", new Color(0.95f, 0.78f, 0.15f)); // 경고 라벨 재질 확보

        CreateCameraUnit(root, "D01_Camera_A", new Vector3(-8.5f, 0f, 0f), 35f, player != null ? player.transform : null, mainCamera, frameMaterial, plateMaterial, glowCyanMaterial, glowRedMaterial, metalMaterial, yellowMaterial); // 카메라 A 생성
        CreateCameraUnit(root, "D01_Camera_B", new Vector3(8.5f, 0f, 0f), -145f, player != null ? player.transform : null, mainCamera, frameMaterial, plateMaterial, glowCyanMaterial, glowRedMaterial, metalMaterial, yellowMaterial); // 카메라 B 생성

        EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시
        EditorSceneManager.SaveScene(scene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침
        CloseSceneIfNeeded(scene, wasLoaded); // 임시 씬 종료
        Debug.Log("Project K Day 6 D-01 camera visual upgrade complete."); // 완료 로그 출력
    }

    private static void CreateCameraUnit(Transform parent, string objectName, Vector3 localPosition, float yaw, Transform player, Camera mainCamera, Material frameMaterial, Material plateMaterial, Material glowCyanMaterial, Material glowRedMaterial, Material metalMaterial, Material yellowMaterial) // 카메라 유닛 생성
    {
        GameObject root = new GameObject(objectName); // 카메라 루트 생성
        root.transform.SetParent(parent, false); // 부모 연결
        root.transform.localPosition = localPosition; // 로컬 위치 적용
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 로컬 회전 적용

        Transform baseRoot = new GameObject("Base").transform; // 받침 루트 생성
        baseRoot.SetParent(root.transform, false); // 루트 연결
        CreatePart("FootPlate", PrimitiveType.Cylinder, baseRoot, new Vector3(0f, 0.10f, 0f), new Vector3(1.40f, 0.10f, 1.40f), frameMaterial); // 바닥 받침 생성
        CreatePart("SupportColumn", PrimitiveType.Cylinder, baseRoot, new Vector3(0f, 1.10f, 0f), new Vector3(0.26f, 0.95f, 0.26f), metalMaterial); // 지지 기둥 생성
        CreatePart("ColumnCollar", PrimitiveType.Cylinder, baseRoot, new Vector3(0f, 2.05f, 0f), new Vector3(0.42f, 0.08f, 0.42f), plateMaterial); // 상단 링 생성
        CreatePart("MaintenanceBox", PrimitiveType.Cube, baseRoot, new Vector3(0.55f, 0.52f, 0.22f), new Vector3(0.35f, 0.55f, 0.30f), frameMaterial); // 정비 박스 생성
        CreatePart("PowerCable", PrimitiveType.Cylinder, baseRoot, new Vector3(0.32f, 1.45f, 0.18f), new Vector3(0.03f, 0.60f, 0.03f), glowCyanMaterial, new Vector3(28f, 0f, 26f)); // 전원 케이블 생성

        Transform pivot = new GameObject("RotationPivot").transform; // 회전 축 생성
        pivot.SetParent(root.transform, false); // 루트 연결
        pivot.localPosition = new Vector3(0f, 2.05f, 0f); // 회전 축 위치 적용
        pivot.localRotation = Quaternion.identity; // 회전 축 회전 초기화

        D01SimplePan pan = root.AddComponent<D01SimplePan>(); // 간단 회전 스크립트 추가
        pan.Configure(pivot, -55f, 55f, 45f); // 회전 값 적용

        CreatePart("SupportArmLeft", PrimitiveType.Cube, pivot, new Vector3(-0.48f, 0.06f, 0f), new Vector3(0.95f, 0.12f, 0.16f), plateMaterial); // 좌측 지지대 생성
        CreatePart("SupportArmRight", PrimitiveType.Cube, pivot, new Vector3(0.48f, 0.06f, 0f), new Vector3(0.95f, 0.12f, 0.16f), plateMaterial); // 우측 지지대 생성
        CreatePart("CoreJoint", PrimitiveType.Sphere, pivot, new Vector3(0f, 0.04f, 0f), new Vector3(0.38f, 0.38f, 0.38f), metalMaterial); // 중앙 조인트 생성

        Transform bodyRoot = new GameObject("CameraBody").transform; // 본체 루트 생성
        bodyRoot.SetParent(pivot, false); // 회전 축 연결
        bodyRoot.localPosition = new Vector3(0f, 0.02f, 0f); // 본체 위치 적용
        bodyRoot.localRotation = Quaternion.identity; // 본체 회전 초기화

        CreatePart("MainHull", PrimitiveType.Cube, bodyRoot, new Vector3(0f, 0.12f, 0.10f), new Vector3(1.55f, 0.72f, 1.60f), frameMaterial, new Vector3(6f, 0f, 0f)); // 메인 본체 생성
        CreatePart("TopHood", PrimitiveType.Cube, bodyRoot, new Vector3(0f, 0.46f, 0.18f), new Vector3(1.68f, 0.16f, 1.30f), plateMaterial, new Vector3(12f, 0f, 0f)); // 상단 후드 생성
        CreatePart("BottomPlate", PrimitiveType.Cube, bodyRoot, new Vector3(0f, -0.30f, 0.12f), new Vector3(1.32f, 0.12f, 1.18f), metalMaterial); // 하단 플레이트 생성
        CreatePart("SideFinLeft", PrimitiveType.Cube, bodyRoot, new Vector3(-0.86f, 0.04f, 0.16f), new Vector3(0.10f, 0.60f, 1.00f), plateMaterial, new Vector3(0f, 0f, 8f)); // 좌측 핀 생성
        CreatePart("SideFinRight", PrimitiveType.Cube, bodyRoot, new Vector3(0.86f, 0.04f, 0.16f), new Vector3(0.10f, 0.60f, 1.00f), plateMaterial, new Vector3(0f, 0f, -8f)); // 우측 핀 생성
        CreatePart("RearVent", PrimitiveType.Cube, bodyRoot, new Vector3(0f, 0.08f, -0.72f), new Vector3(0.95f, 0.36f, 0.18f), metalMaterial); // 후면 벤트 생성
        CreatePart("RearPowerCore", PrimitiveType.Cylinder, bodyRoot, new Vector3(0f, 0.02f, -0.82f), new Vector3(0.18f, 0.18f, 0.18f), glowCyanMaterial, new Vector3(90f, 0f, 0f)); // 후면 코어 생성
        CreatePart("CameraVisor", PrimitiveType.Cube, bodyRoot, new Vector3(0f, 0.08f, 0.74f), new Vector3(1.20f, 0.48f, 0.18f), plateMaterial, new Vector3(-10f, 0f, 0f)); // 전면 바이저 생성
        CreatePart("LensHousing", PrimitiveType.Cylinder, bodyRoot, new Vector3(0f, 0.06f, 1.00f), new Vector3(0.35f, 0.22f, 0.35f), frameMaterial, new Vector3(90f, 0f, 0f)); // 렌즈 하우징 생성
        CreatePart("LensGlass", PrimitiveType.Cylinder, bodyRoot, new Vector3(0f, 0.06f, 1.15f), new Vector3(0.24f, 0.04f, 0.24f), glowCyanMaterial, new Vector3(90f, 0f, 0f)); // 렌즈 유리 생성
        CreatePart("FocusRing", PrimitiveType.Cylinder, bodyRoot, new Vector3(0f, 0.06f, 1.07f), new Vector3(0.28f, 0.03f, 0.28f), metalMaterial, new Vector3(90f, 0f, 0f)); // 초점 링 생성
        CreatePart("StatusLightLeft", PrimitiveType.Sphere, bodyRoot, new Vector3(-0.38f, 0.26f, 0.86f), new Vector3(0.12f, 0.12f, 0.12f), glowRedMaterial); // 좌측 경고등 생성
        CreatePart("StatusLightRight", PrimitiveType.Sphere, bodyRoot, new Vector3(0.38f, 0.26f, 0.86f), new Vector3(0.12f, 0.12f, 0.12f), glowRedMaterial); // 우측 경고등 생성
        CreatePart("UnderSensor", PrimitiveType.Cube, bodyRoot, new Vector3(0f, -0.22f, 0.66f), new Vector3(0.58f, 0.16f, 0.26f), glowCyanMaterial); // 하부 센서 생성
        CreatePart("WarningPlate", PrimitiveType.Cube, bodyRoot, new Vector3(0f, -0.12f, 0.92f), new Vector3(0.70f, 0.10f, 0.04f), yellowMaterial); // 경고 패널 생성
        CreateText("LabelText", bodyRoot, "D-01", new Vector3(0f, -0.12f, 0.95f), Color.black, 28, 0.08f); // 라벨 텍스트 생성
        CreateText("TopText", bodyRoot, "CAM", new Vector3(0f, 0.57f, 0.05f), glowCyanMaterial.color, 26, 0.06f); // 상단 텍스트 생성

        DetectionSensor sensor = root.GetComponent<DetectionSensor>(); // 기존 센서 조회
        if (sensor == null) // 센서 존재 여부 확인
        {
            sensor = root.AddComponent<DetectionSensor>(); // 탐지 센서 추가
        }

        if (player != null) // 플레이어 존재 여부 확인
        {
            sensor.Configure(player, 20f, 70f, 12f, ~0); // D-01 탐지 수치 적용
        }

        DetectionBillboardUI billboard = root.GetComponent<DetectionBillboardUI>(); // 기존 UI 조회
        if (billboard == null) // UI 존재 여부 확인
        {
            billboard = root.AddComponent<DetectionBillboardUI>(); // 탐지 UI 추가
        }

        billboard.Configure(sensor, mainCamera, new Vector3(0f, 3.10f, 0f)); // 머리 위 UI 설정 적용
    }

    private static Transform EnsureRoot(Transform parent, string rootName) // 루트 확보
    {
        Transform found = parent.Find(rootName); // 기존 루트 조회
        if (found != null) // 기존 루트 존재 여부 확인
        {
            return found; // 기존 루트 반환
        }

        Transform created = new GameObject(rootName).transform; // 새 루트 생성
        created.SetParent(parent, false); // 부모 연결
        created.localPosition = Vector3.zero; // 위치 초기화
        created.localRotation = Quaternion.identity; // 회전 초기화
        created.localScale = Vector3.one; // 크기 초기화
        return created; // 새 루트 반환
    }

    private static void RemoveChild(Transform parent, string childName) // 자식 제거
    {
        Transform target = parent.Find(childName); // 대상 자식 조회
        if (target != null) // 대상 자식 확인
        {
            Object.DestroyImmediate(target.gameObject); // 자식 삭제
        }
    }

    private static void CloseSceneIfNeeded(Scene scene, bool wasLoaded) // 임시 씬 종료
    {
        if (!wasLoaded) // 임시 로드 여부 확인
        {
            EditorSceneManager.CloseScene(scene, true); // 테스트 씬 종료
        }
    }

    private static Camera FindMainCamera(Scene scene) // 메인 카메라 조회
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회
        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true); // 카메라 목록 조회
            for (int j = 0; j < cameras.Length; j++) // 카메라 순회
            {
                if (cameras[j].CompareTag("MainCamera")) // 메인 카메라 태그 확인
                {
                    return cameras[j]; // 메인 카메라 반환
                }
            }
        }

        return Camera.main; // 대체 카메라 반환
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 이름 기반 객체 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회
        for (int i = 0; i < roots.Length; i++) // 루트 순회
        {
            Transform found = FindRecursive(roots[i].transform, objectName); // 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static Transform FindRecursive(Transform current, string objectName) // 재귀 검색
    {
        if (current.name == objectName) // 이름 일치 여부 확인
        {
            return current; // 현재 객체 반환
        }

        for (int i = 0; i < current.childCount; i++) // 자식 순회
        {
            Transform found = FindRecursive(current.GetChild(i), objectName); // 자식 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) // 부품 생성
    {
        return CreatePart(name, type, parent, localPosition, localScale, material, Vector3.zero); // 기본 회전 생성 호출
    }

    private static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler) // 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 프리미티브 생성
        part.name = name; // 이름 지정
        part.transform.SetParent(parent, false); // 부모 연결
        part.transform.localPosition = localPosition; // 위치 적용
        part.transform.localRotation = Quaternion.Euler(localEuler); // 회전 적용
        part.transform.localScale = localScale; // 크기 적용

        Renderer renderer = part.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null) // 렌더러 존재 여부 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }

        Collider collider = part.GetComponent<Collider>(); // 콜라이더 조회
        if (collider != null) // 콜라이더 존재 여부 확인
        {
            Object.DestroyImmediate(collider); // 장식 콜라이더 제거
        }

        return part; // 생성 객체 반환
    }

    private static void CreateText(string name, Transform parent, string text, Vector3 localPosition, Color color, int fontSize, float characterSize) // 텍스트 생성
    {
        GameObject textObject = new GameObject(name); // 텍스트 오브젝트 생성
        textObject.transform.SetParent(parent, false); // 부모 연결
        textObject.transform.localPosition = localPosition; // 위치 적용
        textObject.transform.localRotation = Quaternion.identity; // 회전 초기화
        textObject.transform.localScale = Vector3.one; // 크기 초기화

        TextMesh textMesh = textObject.AddComponent<TextMesh>(); // 텍스트 메쉬 추가
        textMesh.text = text; // 표시 문자열 지정
        textMesh.fontSize = fontSize; // 글자 크기 지정
        textMesh.characterSize = characterSize; // 문자 크기 지정
        textMesh.anchor = TextAnchor.MiddleCenter; // 기준점 지정
        textMesh.alignment = TextAlignment.Center; // 정렬 지정
        textMesh.color = color; // 색상 지정
        textMesh.richText = false; // 리치 텍스트 비활성화
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        string parentFolder = "Assets/_Project/Materials"; // 상위 폴더 경로
        string folderPath = "Assets/_Project/Materials/Generated"; // 재질 폴더 경로
        EnsureFolder("Assets", "_Project"); // 프로젝트 폴더 확보
        EnsureFolder("Assets/_Project", "Materials"); // 재질 폴더 확보
        EnsureFolder(parentFolder, "Generated"); // 생성 폴더 확보
        string assetPath = folderPath + "/" + fileName + ".mat"; // 에셋 경로 계산

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 재질 조회
        if (material != null) // 기존 재질 존재 여부 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 셰이더 조회
        if (shader == null) // URP 셰이더 존재 여부 확인
        {
            shader = Shader.Find("Standard"); // 기본 셰이더 조회
        }

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

        AssetDatabase.CreateAsset(material, assetPath); // 재질 에셋 생성
        return material; // 재질 반환
    }

    private static void EnsureFolder(string parentPath, string folderName) // 폴더 확보
    {
        string path = parentPath + "/" + folderName; // 대상 경로 계산
        if (!AssetDatabase.IsValidFolder(path)) // 폴더 존재 여부 확인
        {
            AssetDatabase.CreateFolder(parentPath, folderName); // 폴더 생성
        }
    }
}
#endif
