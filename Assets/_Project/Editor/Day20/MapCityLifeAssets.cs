#if UNITY_EDITOR // 20일차 차량·시민 프리팹 생성 전용
using System; // 생성 오류 처리
using ProjectK.Day20; // 차량과 시민 런타임 컴포넌트 참조
using UnityEditor; // 재질과 프리팹 저장
using UnityEngine; // 기본 도형과 재질 생성
using UnityEngine.Rendering; // 렌더 파이프라인 확인

public static class MapCityLifeAssets // 프로시저럴 차량과 시민 프리팹 제작
{
    public const string RootFolder = "Assets/_Project/Generated/Map20"; // 생성 에셋 루트
    public const string MaterialFolder = RootFolder + "/Materials"; // 도시 생활 재질 폴더
    public const string PrefabFolder = RootFolder + "/Prefabs"; // 차량과 시민 프리팹 폴더

    public readonly struct AssetSet // 설치 메뉴에 전달할 여섯 프리팹 묶음
    {
        public readonly MapTrafficVehicle CivilianVehicle; // 일반 승용차 프리팹
        public readonly MapTrafficVehicle DeliveryVehicle; // 배달 차량 프리팹
        public readonly MapTrafficVehicle CargoVehicle; // 화물 차량 프리팹
        public readonly MapCitizenAgent HumanCitizen; // 인간 시민 프리팹
        public readonly MapCitizenAgent AndroidCitizen; // 안드로이드 시민 프리팹
        public readonly MapCitizenAgent MechanicalCitizen; // 기계화 시민 프리팹

        public AssetSet(MapTrafficVehicle civilian, MapTrafficVehicle delivery, MapTrafficVehicle cargo, MapCitizenAgent human, MapCitizenAgent android, MapCitizenAgent mechanical) // 프리팹 묶음 생성
        {
            CivilianVehicle = civilian; // 일반 차량 저장
            DeliveryVehicle = delivery; // 배달 차량 저장
            CargoVehicle = cargo; // 화물 차량 저장
            HumanCitizen = human; // 인간 시민 저장
            AndroidCitizen = android; // 안드로이드 시민 저장
            MechanicalCitizen = mechanical; // 기계화 시민 저장
        }
    }

    public static AssetSet CreateOrLoad() // 필요한 재질과 프리팹 준비
    {
        EnsureFolder(RootFolder); // 생성 루트 확보
        EnsureFolder(MaterialFolder); // 재질 폴더 확보
        EnsureFolder(PrefabFolder); // 프리팹 폴더 확보
        Material dark = MaterialAsset("CityLife_Dark", new Color(0.035f, 0.045f, 0.060f), 0.25f, false); // 차량과 기계 기본 재질
        Material steel = MaterialAsset("CityLife_Steel", new Color(0.19f, 0.23f, 0.28f), 0.45f, false); // 금속 구조 재질
        Material glass = MaterialAsset("CityLife_Glass", new Color(0.08f, 0.16f, 0.21f), 0.75f, false); // 어두운 유리 재질
        Material cyan = MaterialAsset("CityLife_Cyan", new Color(0.02f, 0.65f, 0.82f), 0.35f, true); // 청록 네온 재질
        Material magenta = MaterialAsset("CityLife_Magenta", new Color(0.85f, 0.05f, 0.45f), 0.30f, true); // 자홍 네온 재질
        Material amber = MaterialAsset("CityLife_Amber", new Color(0.95f, 0.42f, 0.05f), 0.30f, true); // 주황 네온 재질
        Material skin = MaterialAsset("CityLife_Skin", new Color(0.48f, 0.29f, 0.23f), 0.25f, false); // 임시 인간 피부 재질
        MapTrafficVehicle civilian = LoadOrCreateVehicle("Vehicle_Civilian", MapVehicleKind.Civilian, new Vector3(4.2f, 1.5f, 1.82f), dark, steel, glass, cyan); // 일반 승용차 준비
        MapTrafficVehicle delivery = LoadOrCreateVehicle("Vehicle_Delivery", MapVehicleKind.Delivery, new Vector3(4.9f, 2.1f, 1.95f), dark, steel, glass, magenta); // 배달 차량 준비
        MapTrafficVehicle cargo = LoadOrCreateVehicle("Vehicle_Cargo", MapVehicleKind.Cargo, new Vector3(6.4f, 2.5f, 2.20f), dark, steel, glass, amber); // 화물 차량 준비
        MapCitizenAgent human = LoadOrCreateCitizen("Citizen_Human", MapCitizenKind.Human, skin, dark, cyan); // 인간 시민 준비
        MapCitizenAgent android = LoadOrCreateCitizen("Citizen_Android", MapCitizenKind.Android, steel, dark, cyan); // 안드로이드 시민 준비
        MapCitizenAgent mechanical = LoadOrCreateCitizen("Citizen_Mechanical", MapCitizenKind.Mechanical, dark, steel, magenta); // 기계화 시민 준비
        AssetDatabase.SaveAssets(); // 생성된 에셋 저장
        AssetDatabase.Refresh(); // 프로젝트 창에 생성 결과 반영
        return new AssetSet(civilian, delivery, cargo, human, android, mechanical); // 설치 메뉴용 프리팹 반환
    }

    private static MapTrafficVehicle LoadOrCreateVehicle(string name, MapVehicleKind kind, Vector3 size, Material dark, Material steel, Material glass, Material accent) // 차량 프리팹 생성 또는 기존 로드
    {
        string path = PrefabFolder + "/" + name + ".prefab"; // 차량 프리팹 경로 계산
        GameObject existingObject = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 차량 프리팹 루트 조회
        MapTrafficVehicle existing = existingObject != null ? existingObject.GetComponent<MapTrafficVehicle>() : null; // 기존 차량 AI 조회
        if (existing != null) // 이미 생성된 차량 확인
        {
            return existing; // 수동 편집된 기존 프리팹 보존
        }
        GameObject root = new GameObject(name); // 차량 루트 생성
        try // 임시 오브젝트 정리를 보장하는 생성 처리
        {
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 차량 물리 충돌체 추가
            collider.center = new Vector3(0f, size.y * 0.48f, 0f); // 차량 차체 중심 적용
            collider.size = new Vector3(size.z * 0.92f, size.y * 0.88f, size.x * 0.92f); // 전방 Z축 기준 차량 충돌 범위 적용
            Rigidbody body = root.AddComponent<Rigidbody>(); // 움직이는 기동 충돌체 추가
            body.isKinematic = true; // 경로 AI가 직접 위치를 제어
            body.useGravity = false; // 도로 높이를 코드로 유지
            MapTrafficVehicle agent = root.AddComponent<MapTrafficVehicle>(); // 차량 주행 AI 추가
            AddBox(root.transform, "LowerBody", new Vector3(0f, size.y * 0.38f, 0f), new Vector3(size.z, size.y * 0.56f, size.x), dark); // 전방 Z축 기준 차량 하부 차체 생성
            float cabinHeight = kind == MapVehicleKind.Civilian ? size.y * 0.52f : size.y * 0.68f; // 차량 유형별 객실 높이 계산
            AddBox(root.transform, "Cabin", new Vector3(0f, size.y * 0.78f, kind == MapVehicleKind.Cargo ? 0.65f : 0.25f), new Vector3(size.z * 0.87f, cabinHeight, size.x * (kind == MapVehicleKind.Cargo ? 0.32f : 0.52f)), steel); // 전방 쪽 객실 또는 운전석 생성
            AddBox(root.transform, "Windshield", new Vector3(0f, size.y * 0.86f, size.x * 0.20f), new Vector3(size.z * 0.70f, size.y * 0.38f, 0.08f), glass); // 전방 유리 생성
            if (kind != MapVehicleKind.Civilian) // 배달·화물 적재함 확인
            {
                AddBox(root.transform, "CargoBody", new Vector3(0f, size.y * 0.70f, -size.x * 0.18f), new Vector3(size.z * 0.92f, size.y * 0.62f, size.x * (kind == MapVehicleKind.Cargo ? 0.64f : 0.48f)), dark); // 후방 적재함 생성
            }
            for (int x = -1; x <= 1; x += 2) // 좌우 바퀴 위치 순회
            {
                for (int z = -1; z <= 1; z += 2) // 앞뒤 바퀴 위치 순회
                {
                    AddCylinder(root.transform, "Wheel", new Vector3(x * size.z * 0.50f, 0.34f, z * size.x * 0.31f), new Vector3(0.35f, 0.13f, 0.35f), dark, Quaternion.Euler(0f, 0f, 90f)); // 전방 Z축 기준 네 바퀴 생성
                }
            }
            AddBox(root.transform, "FrontGlow", new Vector3(0f, size.y * 0.45f, size.x * 0.505f), new Vector3(size.z * 0.65f, 0.12f, 0.04f), accent); // 전면 차량 식별 조명 생성
            AddBox(root.transform, "TailGlow", new Vector3(0f, size.y * 0.45f, -size.x * 0.505f), new Vector3(size.z * 0.58f, 0.13f, 0.04f), accent); // 후면 차량 식별 조명 생성
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); // 차량 프리팹 프로젝트 저장
            return saved.GetComponent<MapTrafficVehicle>(); // 저장된 차량 AI 참조 반환
        }
        finally // 임시 씬 오브젝트 정리
        {
            UnityEngine.Object.DestroyImmediate(root); // 프리팹 생성용 임시 루트 제거
        }
    }

    private static MapCitizenAgent LoadOrCreateCitizen(string name, MapCitizenKind kind, Material bodyMaterial, Material secondary, Material accent) // 시민 프리팹 생성 또는 기존 로드
    {
        string path = PrefabFolder + "/" + name + ".prefab"; // 시민 프리팹 경로 계산
        GameObject existingObject = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 시민 프리팹 루트 조회
        MapCitizenAgent existing = existingObject != null ? existingObject.GetComponent<MapCitizenAgent>() : null; // 기존 시민 AI 조회
        if (existing != null) // 이미 생성된 시민 확인
        {
            return existing; // 수동 편집된 프리팹 보존
        }
        GameObject root = new GameObject(name); // 시민 루트 생성
        try // 임시 오브젝트 정리를 보장하는 생성 처리
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>(); // 시민 물리 충돌체 추가
            collider.center = new Vector3(0f, 0.90f, 0f); // 시민 몸 중심 적용
            collider.height = 1.75f; // 임시 성인 높이 적용
            collider.radius = 0.32f; // 보행 충돌 반경 적용
            Rigidbody body = root.AddComponent<Rigidbody>(); // 이동 충돌 안정용 강체 추가
            body.isKinematic = true; // 보행 AI가 직접 이동 제어
            body.useGravity = false; // 보도 높이를 그래프로 유지
            root.AddComponent<MapCitizenAgent>(); // 시민 생활 AI 추가
            AddCapsule(root.transform, "Torso", new Vector3(0f, 1.02f, 0f), new Vector3(0.42f, 0.60f, 0.34f), secondary); // 몸통 실루엣 생성
            AddSphere(root.transform, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.34f, 0.34f, 0.34f), bodyMaterial); // 머리 실루엣 생성
            for (int side = -1; side <= 1; side += 2) // 양팔 생성
            {
                AddCylinder(root.transform, "Arm", new Vector3(side * 0.31f, 1.05f, 0f), new Vector3(0.09f, 0.32f, 0.09f), bodyMaterial, Quaternion.identity); // 팔 모형 생성
                AddCylinder(root.transform, "Leg", new Vector3(side * 0.14f, 0.38f, 0f), new Vector3(0.11f, 0.38f, 0.11f), secondary, Quaternion.identity); // 다리 모형 생성
            }
            if (kind == MapCitizenKind.Android) // 안드로이드 식별 표현 확인
            {
                AddBox(root.transform, "FaceGlow", new Vector3(0f, 1.73f, 0.31f), new Vector3(0.28f, 0.06f, 0.025f), accent); // 안드로이드 눈 네온 생성
            }
            else if (kind == MapCitizenKind.Mechanical) // 기계화 시민 식별 표현 확인
            {
                AddBox(root.transform, "ChestCore", new Vector3(0f, 1.08f, 0.29f), new Vector3(0.20f, 0.20f, 0.025f), accent); // 기계화 코어 표시 생성
                AddBox(root.transform, "HeadPlate", new Vector3(0f, 1.73f, 0.28f), new Vector3(0.36f, 0.22f, 0.05f), secondary); // 금속 얼굴판 생성
            }
            else // 인간 시민 생활 액세서리
            {
                AddBox(root.transform, "Bag", new Vector3(-0.30f, 0.98f, -0.12f), new Vector3(0.20f, 0.42f, 0.24f), accent); // 작은 배송 가방 생성
            }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); // 시민 프리팹 프로젝트 저장
            return saved.GetComponent<MapCitizenAgent>(); // 저장된 시민 AI 참조 반환
        }
        finally // 임시 시민 루트 정리
        {
            UnityEngine.Object.DestroyImmediate(root); // 생성용 임시 오브젝트 제거
        }
    }

    private static Material MaterialAsset(string name, Color color, float smoothness, bool emission) // URP 기본 재질 생성 또는 로드
    {
        string path = MaterialFolder + "/" + name + ".mat"; // 재질 저장 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회
        if (material != null) // 기존 재질 존재 확인
        {
            return material; // 수동 조정값 보존
        }
        bool urp = GraphicsSettings.currentRenderPipeline != null; // 현재 렌더 파이프라인 확인
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard"); // 프로젝트 기본 Lit 셰이더 조회
        if (shader == null) // 셰이더 누락 확인
        {
            throw new InvalidOperationException("차량·시민용 Lit 셰이더를 찾지 못했습니다."); // 생성 중단
        }
        material = new Material(shader); // 새 공유 재질 생성
        material.name = name; // 재질 이름 지정
        if (material.HasProperty("_BaseColor")) // URP 기본 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // 기본 색상 적용
        }
        else // Built-in 기본 셰이더 처리
        {
            material.color = color; // 기본 색상 적용
        }
        if (material.HasProperty("_Smoothness")) // 표면 매끄러움 속성 확인
        {
            material.SetFloat("_Smoothness", smoothness); // 표면 반사도 적용
        }
        if (emission) // 네온 발광 재질 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
            if (material.HasProperty("_EmissionColor")) // 발광 색 속성 확인
            {
                material.SetColor("_EmissionColor", color * 2.8f); // 네온 발광 강도 적용
            }
        }
        AssetDatabase.CreateAsset(material, path); // 재질 프로젝트 저장
        return material; // 생성 재질 반환
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material) // 충돌 없는 큐브 외형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 큐브 생성
        item.name = name; // 오브젝트 이름 지정
        item.transform.SetParent(parent, false); // 프리팹 루트에 연결
        item.transform.localPosition = position; // 로컬 위치 적용
        item.transform.localScale = scale; // 로컬 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = material; // 공유 재질 연결
        UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 그래픽 자식의 중복 충돌체 제거
        return item; // 후속 편집용 반환
    }

    private static GameObject AddCylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation) // 충돌 없는 원통 외형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 기본 원통 생성
        item.name = name; // 오브젝트 이름 지정
        item.transform.SetParent(parent, false); // 프리팹 루트에 연결
        item.transform.localPosition = position; // 로컬 위치 적용
        item.transform.localRotation = rotation; // 로컬 회전 적용
        item.transform.localScale = scale; // 로컬 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = material; // 공유 재질 연결
        UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 그래픽 자식의 중복 충돌체 제거
        return item; // 생성 외형 반환
    }

    private static GameObject AddCapsule(Transform parent, string name, Vector3 position, Vector3 scale, Material material) // 충돌 없는 캡슐 외형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 기본 캡슐 생성
        item.name = name; // 오브젝트 이름 지정
        item.transform.SetParent(parent, false); // 시민 루트 연결
        item.transform.localPosition = position; // 로컬 위치 적용
        item.transform.localScale = scale; // 로컬 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = material; // 공유 재질 연결
        UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 자식 충돌체 제거
        return item; // 생성 외형 반환
    }

    private static GameObject AddSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material) // 충돌 없는 구형 외형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 기본 구 생성
        item.name = name; // 오브젝트 이름 지정
        item.transform.SetParent(parent, false); // 시민 루트 연결
        item.transform.localPosition = position; // 로컬 위치 적용
        item.transform.localScale = scale; // 로컬 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = material; // 공유 재질 연결
        UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 자식 충돌체 제거
        return item; // 생성 외형 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 조각 분리
        string current = parts[0]; // Assets 시작 경로
        for (int i = 1; i < parts.Length; i++) // 나머지 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 전체 경로 생성
            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            }
            current = next; // 다음 부모 경로 갱신
        }
    }
}
#endif
