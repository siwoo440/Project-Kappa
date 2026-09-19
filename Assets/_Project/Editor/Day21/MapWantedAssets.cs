#if UNITY_EDITOR // 21일차 수배 경비 프리팹 생성 전용
using System; // 생성 오류 처리
using ProjectK.Day21; // 수배 경비 런타임 컴포넌트
using UnityEditor; // 프리팹·재질 저장
using UnityEngine; // 경비 모델 생성
using UnityEngine.Rendering; // 렌더 파이프라인 확인

public static class MapWantedAssets // E-01 대응 경비와 E-02 정예 경비 생성
{
    public const string RootFolder = "Assets/_Project/Generated/Map21"; // 21일차 생성 에셋 루트
    public const string MaterialFolder = RootFolder + "/Materials"; // 경비 재질 폴더
    public const string PrefabFolder = RootFolder + "/Prefabs"; // 경비 프리팹 폴더

    public readonly struct GuardAssets // 수배 대응 경비 프리팹 묶음
    {
        public readonly MapWantedGuardAgent Regular; // E-01 일반 대응 경비
        public readonly MapWantedGuardAgent Elite; // E-02 정예 대응 경비

        public GuardAssets(MapWantedGuardAgent regular, MapWantedGuardAgent elite) // 경비 프리팹 묶음 생성
        {
            Regular = regular; // 일반 경비 저장
            Elite = elite; // 정예 경비 저장
        }
    }

    public static GuardAssets CreateOrLoad() // 필요한 경비 재질과 프리팹 준비
    {
        EnsureFolder(RootFolder); // 생성 루트 확보
        EnsureFolder(MaterialFolder); // 재질 폴더 확보
        EnsureFolder(PrefabFolder); // 프리팹 폴더 확보
        Material dark = MaterialAsset("Wanted_Dark", new Color(0.035f, 0.050f, 0.065f), false); // 경비 어두운 장갑 재질
        Material steel = MaterialAsset("Wanted_Steel", new Color(0.24f, 0.30f, 0.36f), false); // 경비 금속판 재질
        Material cyan = MaterialAsset("Wanted_Cyan", new Color(0.03f, 0.75f, 0.95f), true); // E-01 청록 신호 재질
        Material orange = MaterialAsset("Wanted_Orange", new Color(1f, 0.32f, 0.05f), true); // E-02 주황 신호 재질
        Material red = MaterialAsset("Wanted_Red", new Color(0.95f, 0.05f, 0.08f), true); // 적색 바이저 재질
        MapWantedGuardAgent regular = LoadOrCreateGuard("Wanted_E01_Response", false, dark, steel, cyan, red); // 일반 E-01 프리팹 준비
        MapWantedGuardAgent elite = LoadOrCreateGuard("Wanted_E02_Elite", true, dark, steel, orange, red); // 정예 E-02 프리팹 준비
        AssetDatabase.SaveAssets(); // 생성 에셋 저장
        AssetDatabase.Refresh(); // 프로젝트 창 갱신
        return new GuardAssets(regular, elite); // 증원 관리자용 프리팹 반환
    }

    private static MapWantedGuardAgent LoadOrCreateGuard(string name, bool elite, Material dark, Material steel, Material accent, Material red) // 경비 프리팹 생성 또는 기존 로드
    {
        string path = PrefabFolder + "/" + name + ".prefab"; // 경비 프리팹 경로 계산
        GameObject existingObject = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 프리팹 루트 조회
        MapWantedGuardAgent existing = existingObject != null ? existingObject.GetComponent<MapWantedGuardAgent>() : null; // 기존 경비 AI 조회
        if (existing != null) // 이미 생성된 경비 확인
        {
            return existing; // 수동 편집한 기존 프리팹 보존
        }
        GameObject root = new GameObject(name); // 경비 루트 생성
        try // 임시 오브젝트 정리 보장
        {
            CharacterController controller = root.AddComponent<CharacterController>(); // 경비 이동 충돌체 추가
            controller.height = 1.95f; // 경비 키 적용
            controller.radius = elite ? 0.39f : 0.34f; // 정예 경비 체격 증가
            controller.center = new Vector3(0f, 0.98f, 0f); // 충돌체 중심 적용
            controller.stepOffset = 0.25f; // 낮은 보도 턱 이동 허용
            controller.slopeLimit = 45f; // 경사 한계 적용
            EnemyActor actor = root.AddComponent<EnemyActor>(); // 기존 적 체력 관리자 추가
            actor.Configure(elite ? 160f : 100f, elite ? 140f : 100f, true); // 등급별 체력·자세 설정
            root.AddComponent<EnemyStatusController>(); // 기존 마비 상태 관리자 추가
            DetectionSensor sensor = root.AddComponent<DetectionSensor>(); // 기존 시야·청각 센서 추가
            sensor.Configure(null, elite ? 25f : 20f, elite ? 105f : 90f, elite ? 16f : 12f, ~0); // 런타임 플레이어 연결 전 탐지 수치 설정
            EnemyMeleeCombat melee = root.AddComponent<EnemyMeleeCombat>(); // 기존 근접 공격 관리자 추가
            root.AddComponent<MapGuardCrimeTag>(); // 경비 공격·처치 범죄 태그 추가
            root.AddComponent<MapWantedGuardAgent>(); // 수배 추적 AI 추가
            Transform model = new GameObject("Model").transform; // 경비 모델 루트 생성
            model.SetParent(root.transform, false); // 경비 루트 연결
            BuildModel(model, elite, dark, steel, accent, red); // E-01/E-02 외형 생성
            Transform swordSocket = model.Find("SwordSocket"); // 생성한 검 소켓 조회
            melee.Configure(swordSocket, elite ? 28f : 18f, elite ? 42f : 30f); // 등급별 근접 피해 연결
            EnemyFirearmHitboxes boxes = root.AddComponent<EnemyFirearmHitboxes>(); // 총기 몸통·머리 판정 관리자 추가
            BoxCollider body = CreateHitZone(root.transform, "BodyHit", new Vector3(0f, 1.18f, 0f), new Vector3(0.78f, 1.55f, 0.68f), FirearmHitRegion.Body, actor); // 몸통 총기 피격 영역 생성
            BoxCollider head = CreateHitZone(root.transform, "HeadHit", new Vector3(0f, 2.10f, 0f), new Vector3(0.55f, 0.50f, 0.55f), FirearmHitRegion.Head, actor); // 머리 총기 피격 영역 생성
            boxes.Configure(body, head); // 기존 총기 판정 시스템에 피격 영역 연결
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); // 경비 프리팹 프로젝트 저장
            return saved.GetComponent<MapWantedGuardAgent>(); // 저장된 경비 AI 반환
        }
        finally // 임시 루트 정리
        {
            UnityEngine.Object.DestroyImmediate(root); // 생성용 임시 경비 제거
        }
    }

    private static void BuildModel(Transform root, bool elite, Material dark, Material steel, Material accent, Material red) // 프리미티브 기반 경비 외형 생성
    {
        AddPart(root, "Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(0.58f, 0.28f, 0.36f), dark); // 골반 장갑 생성
        AddPart(root, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.38f, 0f), new Vector3(elite ? 0.58f : 0.52f, 0.56f, elite ? 0.42f : 0.36f), dark); // 몸통 생성
        AddPart(root, "ChestPlate", PrimitiveType.Cube, new Vector3(0f, 1.48f, 0.24f), new Vector3(elite ? 0.72f : 0.62f, 0.48f, 0.10f), steel); // 흉부 장갑 생성
        AddPart(root, "ChestGlow", PrimitiveType.Cube, new Vector3(0f, 1.52f, 0.305f), new Vector3(0.34f, 0.06f, 0.025f), accent); // 수배 등급 신호등 생성
        AddPart(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.10f, 0f), new Vector3(0.42f, 0.38f, 0.40f), dark); // 머리 생성
        AddPart(root, "FacePlate", PrimitiveType.Cube, new Vector3(0f, 2.08f, 0.22f), new Vector3(0.40f, 0.24f, 0.08f), steel); // 얼굴 장갑 생성
        AddPart(root, "Visor", PrimitiveType.Cube, new Vector3(0f, 2.13f, 0.27f), new Vector3(0.30f, 0.055f, 0.025f), red); // 적색 바이저 생성
        for (int side = -1; side <= 1; side += 2) // 좌우 팔과 다리 생성
        {
            AddPart(root, "Arm", PrimitiveType.Cylinder, new Vector3(0.50f * side, 1.20f, 0f), new Vector3(0.11f, 0.42f, 0.11f), dark); // 팔 생성
            AddPart(root, "Leg", PrimitiveType.Cylinder, new Vector3(0.18f * side, 0.48f, 0f), new Vector3(0.13f, 0.45f, 0.13f), steel); // 다리 생성
        }
        Transform swordSocket = new GameObject("SwordSocket").transform; // 검 휘두르기 기준 소켓 생성
        swordSocket.SetParent(root, false); // 모델 루트에 연결
        swordSocket.localPosition = new Vector3(0.50f, 1.08f, 0.18f); // 오른손 부근 소켓 위치 적용
        AddPart(swordSocket, "SwordBlade", PrimitiveType.Cube, new Vector3(0f, 0.48f, 0f), new Vector3(0.08f, elite ? 1.20f : 1.0f, 0.06f), accent); // 경비 검 생성
        AddPart(swordSocket, "SwordGrip", PrimitiveType.Cylinder, new Vector3(0f, -0.18f, 0f), new Vector3(0.05f, 0.18f, 0.05f), dark); // 검 손잡이 생성
        GameObject labelObject = new GameObject("UnitLabel"); // 경비 ID 라벨 생성
        labelObject.transform.SetParent(root, false); // 모델 루트 연결
        labelObject.transform.localPosition = new Vector3(0f, 1.62f, 0.34f); // 가슴 앞 라벨 위치
        TextMesh label = labelObject.AddComponent<TextMesh>(); // 월드 텍스트 추가
        label.text = elite ? "E-02" : "E-01"; // 경비 등급 표시
        label.fontSize = 32; // 라벨 폰트 해상도
        label.characterSize = 0.055f; // 실제 라벨 크기
        label.anchor = TextAnchor.MiddleCenter; // 중앙 기준
        label.alignment = TextAlignment.Center; // 중앙 정렬
        label.color = accent.color; // 등급별 신호 색상 사용
    }

    private static BoxCollider CreateHitZone(Transform parent, string name, Vector3 position, Vector3 size, FirearmHitRegion region, EnemyActor actor) // 총기 피격 트리거 생성
    {
        GameObject zoneObject = new GameObject(name); // 피격 영역 오브젝트 생성
        zoneObject.transform.SetParent(parent, false); // 경비 루트 연결
        zoneObject.transform.localPosition = position; // 피격 영역 위치 적용
        BoxCollider collider = zoneObject.AddComponent<BoxCollider>(); // 박스 피격 영역 추가
        collider.size = size; // 실제 피격 영역 크기 적용
        collider.isTrigger = true; // 이동 충돌과 분리
        FirearmHitZone zone = zoneObject.AddComponent<FirearmHitZone>(); // 기존 총기 부위 표식 추가
        zone.Configure(region, actor, null); // 몸통·머리와 적 생명 관리자 연결
        return collider; // EnemyFirearmHitboxes 연결용 반환
    }

    private static GameObject AddPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material) // 충돌 없는 모델 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 기본 프리미티브 생성
        part.name = name; // 부품 이름 적용
        part.transform.SetParent(parent, false); // 지정 부모 연결
        part.transform.localPosition = position; // 로컬 위치 적용
        part.transform.localScale = scale; // 로컬 크기 적용
        Renderer renderer = part.GetComponent<Renderer>(); // 부품 렌더러 조회
        if (renderer != null) // 렌더러 존재 확인
        {
            renderer.sharedMaterial = material; // 공유 재질 연결
        }
        Collider collider = part.GetComponent<Collider>(); // 기본 충돌체 조회
        if (collider != null) // 장식 충돌체 존재 확인
        {
            UnityEngine.Object.DestroyImmediate(collider); // 루트 CharacterController와 피격 트리거만 사용
        }
        return part; // 생성 부품 반환
    }

    private static Material MaterialAsset(string name, Color color, bool emission) // URP 경비 재질 생성 또는 로드
    {
        string path = MaterialFolder + "/" + name + ".mat"; // 재질 저장 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회
        if (material != null) // 기존 재질 확인
        {
            return material; // 수동 조정값 보존
        }
        bool urp = GraphicsSettings.currentRenderPipeline != null; // 현재 렌더 파이프라인 확인
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard"); // 기본 Lit 셰이더 조회
        if (shader == null) // 셰이더 누락 확인
        {
            throw new InvalidOperationException("Day21 경비용 Lit 셰이더를 찾지 못했습니다."); // 생성 중단
        }
        material = new Material(shader); // 새 재질 생성
        material.name = name; // 재질 이름 적용
        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // 기본 색상 적용
        }
        else // Built-in 색상 처리
        {
            material.color = color; // 기본 색상 적용
        }
        if (material.HasProperty("_Smoothness")) // 매끄러움 속성 확인
        {
            material.SetFloat("_Smoothness", 0.38f); // 금속 도시 장비 반사도 적용
        }
        if (emission) // 네온 발광 여부 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
            if (material.HasProperty("_EmissionColor")) // 발광 색 속성 확인
            {
                material.SetColor("_EmissionColor", color * 3f); // 경비 신호등 발광 강도 적용
            }
        }
        AssetDatabase.CreateAsset(material, path); // 프로젝트 재질 에셋 저장
        return material; // 생성 재질 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 조각 분리
        string current = parts[0]; // Assets 시작 경로
        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 전체 경로 생성
            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            }
            current = next; // 다음 부모 경로 갱신
        }
    }
}
#endif
