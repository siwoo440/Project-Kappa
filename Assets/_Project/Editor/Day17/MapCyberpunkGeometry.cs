#if UNITY_EDITOR // 17일차 도시 세부 모형 편집 전용
using System; // 문자열과 오류 처리
using System.Collections.Generic; // 재질 캐시 관리
using ProjectK.Day16; // 기존 본편 월드 참조
using ProjectK.Day17; // 런타임 네온 컴포넌트 참조
using UnityEditor; // 생성 에셋 저장
using UnityEngine; // 모형과 재질 생성
using UnityEngine.Rendering; // 렌더 파이프라인 확인

public sealed class MapCyberpunkGeometry // 사이버펑크 세부 모형 공통 생성기
{
    public const string MaterialFolder = "Assets/_Project/Materials/Map17"; // 고정 네온 재질 폴더
    private readonly Transform root; // 전체 디테일 부모
    private readonly MapWorldRoot world; // 거리 표시용 본편 월드
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(); // 재질 중복 생성 방지
    public int SignCount { get; private set; } // 생성한 간판 수
    public int PropCount { get; private set; } // 생성한 소품 수
    public int LightCount { get; private set; } // 생성한 실제 광원 수
    public int ClusterCount { get; private set; } // 생성한 거리 묶음 수

    public MapCyberpunkGeometry(Transform parent, MapWorldRoot owner) // 공통 루트와 월드 연결
    {
        root = parent; // 디테일 루트 저장
        world = owner; // 본편 월드 저장
        EnsureFolder(MaterialFolder); // 재질 저장 폴더 확보
        PrepareMaterials(); // 공통 재질 생성 또는 로드
    }

    public Transform Cluster(string name, Vector3 center, float distance) // 거리 단위 디테일 묶음 생성
    {
        Transform cluster = Node(root, name, center); // 거리 계산 기준 루트 생성
        Transform content = Node(cluster, "Content", Vector3.zero); // 실제 표시 자식 생성
        MapDetailCluster culler = cluster.gameObject.AddComponent<MapDetailCluster>(); // 간단한 거리 표시 관리자 추가
        culler.Configure(world, content.gameObject, distance); // 플레이어와 표시 자식 연결
        ClusterCount++; // 거리 묶음 집계 증가
        return content; // 세부 모형 부모 반환
    }

    public Transform Node(Transform parent, string name, Vector3 localPosition) // 빈 구조 노드 생성
    {
        GameObject item = new GameObject(name); // 새 계층 객체 생성
        item.transform.SetParent(parent, false); // 지정 부모에 로컬 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        return item.transform; // 자식 생성용 트랜스폼 반환
    }

    public GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 scale, string material, bool collider = false) // 직육면체 세부 모형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 큐브 모형 생성
        item.name = name; // 계층 식별 이름 적용
        item.transform.SetParent(parent, false); // 지정 부모 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        item.transform.localScale = scale; // 실제 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = Mat(material); // 공유 재질 연결
        if (!collider) // 장식 전용 여부 확인
        {
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 불필요한 충돌체 제거
        }
        PropCount++; // 세부 소품 집계 증가
        return item; // 후속 구성용 객체 반환
    }

    public GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 scale, string material, Quaternion localRotation, bool collider = false) // 원통형 세부 모형 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 기본 원통 생성
        item.name = name; // 계층 식별 이름 적용
        item.transform.SetParent(parent, false); // 지정 부모 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        item.transform.localRotation = localRotation; // 로컬 회전 적용
        item.transform.localScale = scale; // 실제 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = Mat(material); // 공유 재질 연결
        if (!collider) // 장식 전용 여부 확인
        {
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 불필요한 충돌체 제거
        }
        PropCount++; // 세부 소품 집계 증가
        return item; // 후속 구성용 객체 반환
    }

    public GameObject NeonStrip(Transform parent, string name, Vector3 localPosition, Vector3 scale, string material, Color color, float speed, float phase) // 맥동하는 발광 띠 생성
    {
        GameObject strip = Box(parent, name, localPosition, scale, material, false); // 얇은 발광 상자 생성
        MapNeonPulse pulse = strip.AddComponent<MapNeonPulse>(); // 공유 재질 보존형 맥동 추가
        pulse.Configure(strip.GetComponent<Renderer>(), null, color, speed, phase); // 색상과 주기 연결
        return strip; // 후속 광원 연결용 반환
    }

    public Transform NeonSign(Transform parent, string name, Vector3 localPosition, Vector3 size, string label, string material, Color color, Quaternion rotation) // 받침과 문자로 구성된 입체 네온 간판
    {
        Transform sign = Node(parent, name, localPosition); // 간판 기준 노드 생성
        sign.localRotation = rotation; // 지정 방향 적용
        Box(sign, "Frame", Vector3.zero, new Vector3(size.x + 0.35f, size.y + 0.35f, 0.18f), "DarkMetal", false); // 두꺼운 금속 테두리 생성
        GameObject face = Box(sign, "NeonFace", new Vector3(0f, 0f, -0.11f), new Vector3(size.x, size.y, 0.055f), material, false); // 발광 전면 패널 생성
        MapNeonPulse pulse = face.AddComponent<MapNeonPulse>(); // 패널 맥동 효과 추가
        pulse.Configure(face.GetComponent<Renderer>(), null, color, 0.8f + SignCount * 0.03f, SignCount * 0.7f); // 간판별 다른 주기 적용
        GameObject textObject = new GameObject("Label"); // 네온 문자 객체 생성
        textObject.transform.SetParent(sign, false); // 간판에 문자 연결
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.16f); // 전면 패널보다 조금 앞으로 배치
        textObject.transform.localRotation = Quaternion.identity; // 패널 방향과 동일하게 정렬
        TextMesh text = textObject.AddComponent<TextMesh>(); // 기본 글자 모형 추가
        text.text = label; // 간판 문구 적용
        text.characterSize = Mathf.Clamp(size.y * 0.19f, 0.24f, 0.62f); // 간판 크기에 맞춘 문자 크기
        text.fontSize = 72; // 선명한 글자 메시 생성
        text.fontStyle = FontStyle.Bold; // 굵은 사이버펑크 문자 강조
        text.anchor = TextAnchor.MiddleCenter; // 중앙 기준 정렬
        text.alignment = TextAlignment.Center; // 중앙 문장 정렬
        text.color = color; // 패널과 같은 네온 색상 적용
        SignCount++; // 네온 간판 집계 증가
        return sign; // 추가 지지대 연결용 반환
    }

    public GameObject PointLight(Transform parent, string name, Vector3 localPosition, Color color, float range, float intensity) // 제한된 실제 네온 주변광 생성
    {
        GameObject item = new GameObject(name); // 광원 객체 생성
        item.transform.SetParent(parent, false); // 지정 부모 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        Light light = item.AddComponent<Light>(); // 실제 광원 추가
        light.type = LightType.Point; // 전방향 주변광 사용
        light.color = color; // 네온 색상 연결
        light.range = Mathf.Clamp(range, 3f, 18f); // 과도한 범위 제한
        light.intensity = Mathf.Clamp(intensity, 0.5f, 6f); // 과도한 밝기 제한
        light.shadows = LightShadows.None; // 많은 네온 광원의 그림자 비용 제거
        LightCount++; // 실제 광원 집계 증가
        return item; // 맥동 연결용 객체 반환
    }

    public GameObject Cable(Transform parent, string name, Vector3 start, Vector3 end, float radius = 0.035f) // 두 점 사이 케이블 생성
    {
        return Segment(parent, name, start, end, radius, "Cable", false); // 얇은 비충돌 케이블 생성
    }

    public GameObject Pipe(Transform parent, string name, Vector3 start, Vector3 end, float radius, string material, bool collider) // 두 점 사이 산업 배관 생성
    {
        return Segment(parent, name, start, end, radius, material, collider); // 큰 원통형 배관 생성
    }

    private GameObject Segment(Transform parent, string name, Vector3 start, Vector3 end, float radius, string material, bool collider) // 두 점 원통 공통 생성
    {
        Vector3 delta = end - start; // 두 점 방향과 길이 계산
        float length = Mathf.Max(0.01f, delta.magnitude); // 영 길이 원통 방지
        GameObject item = Cylinder(parent, name, (start + end) * 0.5f, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, Quaternion.FromToRotation(Vector3.up, delta.normalized), collider); // 기본 원통을 두 점 사이로 회전
        return item; // 생성한 선형 구조 반환
    }

    public void AirUnit(Transform parent, Vector3 localPosition, float scale = 1f) // 옥상 실외기 세트 생성
    {
        Transform unit = Node(parent, "RoofAirUnit", localPosition); // 실외기 기준 노드 생성
        Box(unit, "Body", new Vector3(0f, 0.55f * scale, 0f), new Vector3(2.8f, 1.1f, 2.1f) * scale, "Steel", true); // 실제 올라설 수 있는 본체
        Box(unit, "Top", new Vector3(0f, 1.15f * scale, 0f), new Vector3(2.95f, 0.12f, 2.25f) * scale, "DarkMetal", false); // 상부 덮개 추가
        for (int i = -2; i <= 2; i++) // 정면 통풍 슬롯 반복
        {
            Box(unit, "Vent", new Vector3(i * 0.45f * scale, 0.6f * scale, -1.08f * scale), new Vector3(0.18f, 0.65f, 0.05f) * scale, "DarkMetal", false); // 얇은 통풍구 표현
        }
    }

    public void Antenna(Transform parent, Vector3 localPosition, float height, string neon) // 옥상 통신 안테나 생성
    {
        Transform antenna = Node(parent, "AntennaMast", localPosition); // 안테나 기준 노드 생성
        Cylinder(antenna, "Mast", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.10f, height * 0.5f, 0.10f), "Steel", Quaternion.identity, false); // 얇은 금속 기둥 생성
        Box(antenna, "Cross", new Vector3(0f, height * 0.75f, 0f), new Vector3(2.4f, 0.08f, 0.08f), "Steel", false); // 통신 안테나 가로대 생성
        NeonStrip(antenna, "Beacon", new Vector3(0f, height + 0.1f, 0f), new Vector3(0.25f, 0.25f, 0.25f), neon, ColorFor(neon), 1.7f, height); // 상단 위치 표시 네온 추가
    }

    public void VendingMachine(Transform parent, Vector3 localPosition, string neon) // 골목 자동판매기 생성
    {
        Transform machine = Node(parent, "VendingMachine", localPosition); // 판매기 기준 노드 생성
        Box(machine, "Body", new Vector3(0f, 1.2f, 0f), new Vector3(1.25f, 2.4f, 0.75f), "DarkMetal", true); // 실제 엄폐 가능한 본체
        NeonStrip(machine, "Screen", new Vector3(0f, 1.45f, -0.39f), new Vector3(0.84f, 0.9f, 0.035f), neon, ColorFor(neon), 1.0f, localPosition.x); // 발광 상품 화면 추가
        Box(machine, "Slot", new Vector3(0f, 0.65f, -0.40f), new Vector3(0.72f, 0.20f, 0.04f), "Steel", false); // 배출구 표현
    }

    public void Dumpster(Transform parent, Vector3 localPosition) // 골목 수거함 생성
    {
        Transform bin = Node(parent, "WasteContainer", localPosition); // 수거함 기준 노드 생성
        Box(bin, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(2.5f, 1.3f, 1.3f), "DarkMetal", true); // 낮은 엄폐 본체 생성
        Box(bin, "Lid", new Vector3(0f, 1.36f, 0f), new Vector3(2.6f, 0.14f, 1.38f), "Steel", false); // 분리 뚜껑 표현
        Box(bin, "Hazard", new Vector3(0f, 0.75f, -0.67f), new Vector3(0.9f, 0.18f, 0.03f), "Amber", false); // 위험 표시 띠 추가
    }

    public void ServiceCabinet(Transform parent, Vector3 localPosition, string neon) // 벽면 전력함 생성
    {
        Transform cabinet = Node(parent, "ServiceCabinet", localPosition); // 전력함 기준 노드 생성
        Box(cabinet, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(1.5f, 1.8f, 0.65f), "Steel", true); // 실제 엄폐 가능한 전력함 생성
        NeonStrip(cabinet, "Status", new Vector3(0f, 1.25f, -0.34f), new Vector3(0.85f, 0.12f, 0.035f), neon, ColorFor(neon), 1.4f, localPosition.z); // 상태 발광 띠 추가
        for (int i = 0; i < 4; i++) // 전력함 환기 구멍 반복
        {
            Box(cabinet, "Slot", new Vector3(-0.45f + i * 0.30f, 0.62f, -0.34f), new Vector3(0.14f, 0.32f, 0.025f), "DarkMetal", false); // 통풍 슬롯 표현
        }
    }

    public void Awning(Transform parent, Vector3 localPosition, Vector3 scale, string neon) // 시장 차양과 조명 생성
    {
        Transform awning = Node(parent, "MarketAwning", localPosition); // 차양 기준 노드 생성
        Box(awning, "Roof", new Vector3(0f, 2.35f, 0f), scale, "DarkMetal", false); // 얇은 금속 차양 생성
        for (int side = -1; side <= 1; side += 2) // 양쪽 지지 기둥 생성
        {
            Box(awning, "Post", new Vector3(side * scale.x * 0.42f, 1.15f, 0f), new Vector3(0.09f, 2.3f, 0.09f), "Steel", true); // 실제 충돌 지지대 생성
        }
        NeonStrip(awning, "EdgeLight", new Vector3(0f, 2.25f, -scale.z * 0.52f), new Vector3(scale.x * 0.92f, 0.08f, 0.06f), neon, ColorFor(neon), 0.9f, localPosition.x + localPosition.z); // 전면 네온 띠 추가
    }

    public void Bollard(Transform parent, Vector3 localPosition, string neon) // 보행 경계 봉 생성
    {
        Transform bollard = Node(parent, "NeonBollard", localPosition); // 경계 봉 기준 노드 생성
        Cylinder(bollard, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.09f, 0.45f, 0.09f), "Steel", Quaternion.identity, true); // 실제 작은 충돌 기둥 생성
        NeonStrip(bollard, "Cap", new Vector3(0f, 0.92f, 0f), new Vector3(0.22f, 0.10f, 0.22f), neon, ColorFor(neon), 1.2f, localPosition.x); // 상단 발광 표시 추가
    }

    public Material Mat(string key) // 이름 기반 공유 재질 조회
    {
        if (materials.TryGetValue(key, out Material material) && material != null) // 캐시된 재질 확인
        {
            return material; // 기존 재질 반환
        }
        throw new InvalidOperationException("Map17 재질 누락: " + key); // 생성 순서 오류 보고
    }

    public static Color ColorFor(string key) // 네온 재질 이름을 색상으로 변환
    {
        switch (key) // 고정 네온 팔레트 선택
        {
            case "Cyan": return new Color(0.05f, 0.88f, 1f); // 청록 네온 색상
            case "Magenta": return new Color(1f, 0.04f, 0.52f); // 자홍 네온 색상
            case "Violet": return new Color(0.56f, 0.16f, 1f); // 보라 네온 색상
            case "Amber": return new Color(1f, 0.44f, 0.04f); // 주황 네온 색상
            case "Green": return new Color(0.15f, 1f, 0.52f); // 녹색 네온 색상
            default: return Color.white; // 미지정 안전 색상
        }
    }

    private void PrepareMaterials() // 도시 공통 재질 준비
    {
        CreateLit("DarkMetal", new Color(0.025f, 0.04f, 0.06f), 0.78f, 0.68f, false); // 어두운 금속 구조 재질
        CreateLit("Steel", new Color(0.12f, 0.18f, 0.22f), 0.72f, 0.52f, false); // 일반 금속 구조 재질
        CreateLit("WetConcrete", new Color(0.055f, 0.07f, 0.085f), 0.08f, 0.72f, false); // 젖은 콘크리트 재질
        CreateLit("Cable", new Color(0.012f, 0.018f, 0.022f), 0.3f, 0.25f, false); // 전력 케이블 재질
        CreateLit("Cyan", ColorFor("Cyan"), 0.12f, 0.62f, true); // 청록 네온 재질
        CreateLit("Magenta", ColorFor("Magenta"), 0.12f, 0.62f, true); // 자홍 네온 재질
        CreateLit("Violet", ColorFor("Violet"), 0.12f, 0.62f, true); // 보라 네온 재질
        CreateLit("Amber", ColorFor("Amber"), 0.12f, 0.62f, true); // 주황 네온 재질
        CreateLit("Green", ColorFor("Green"), 0.12f, 0.62f, true); // 녹색 네온 재질
    }

    private void CreateLit(string key, Color color, float metallic, float smoothness, bool emission) // URP 공용 재질 생성 또는 로드
    {
        string path = MaterialFolder + "/" + key + ".mat"; // 고정 재질 에셋 경로
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 생성 재질 우선 조회
        if (material == null) // 최초 생성 여부 확인
        {
            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 렌더 파이프라인용 기본 셰이더
            if (shader == null) // 셰이더 존재 확인
            {
                throw new InvalidOperationException("Map17용 Lit 셰이더를 찾지 못했습니다."); // 잘못된 재질 생성 방지
            }
            material = new Material(shader); // 새 공유 재질 생성
            material.name = key; // 재질 식별 이름 적용
            AssetDatabase.CreateAsset(material, path); // 프로젝트 에셋으로 저장
        }
        if (material.HasProperty("_BaseColor")) // URP 기본 색상 속성 확인
        {
            material.SetColor("_BaseColor", emission ? color * 0.34f : color); // URP 기본 색상 적용
        }
        if (material.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            material.SetColor("_Color", emission ? color * 0.34f : color); // 기본 파이프라인 색상 적용
        }
        if (material.HasProperty("_Metallic")) // 금속성 속성 확인
        {
            material.SetFloat("_Metallic", metallic); // 금속성 설정
        }
        if (material.HasProperty("_Smoothness")) // 매끄러움 속성 확인
        {
            material.SetFloat("_Smoothness", smoothness); // 표면 매끄러움 설정
        }
        if (emission && material.HasProperty("_EmissionColor")) // 발광 지원 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
            material.SetColor("_EmissionColor", color * 4.2f); // HDR 네온 발광값 적용
        }
        EditorUtility.SetDirty(material); // 재질 변경 저장 표시
        materials[key] = material; // 빠른 재사용 캐시 저장
    }

    public static void EnsureFolder(string path) // 중첩 에셋 폴더 생성
    {
        string[] parts = path.Split('/'); // 폴더 경로 조각 분리
        string current = parts[0]; // Assets 루트에서 시작
        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순차 확인
        {
            string next = current + "/" + parts[i]; // 다음 전체 경로 계산
            if (!AssetDatabase.IsValidFolder(next)) // 폴더 누락 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 실제 프로젝트 폴더 생성
            }
            current = next; // 다음 단계 부모 갱신
        }
    }
}
#endif
