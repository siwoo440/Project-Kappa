#if UNITY_EDITOR // 훈련장 모형 생성 전용
using System; // 생성 오류 보고
using UnityEditor; // 에셋 생성
using UnityEngine; // 기본 도형과 글자

public static class ProjectKDay13Geometry // 훈련장 공용 부품
{
    public static Transform Node(Transform parent, string name, Vector3 position) // 빈 부품 기준 생성
    {
        GameObject node = new GameObject(name); // 지정 이름의 기준 객체
        node.transform.SetParent(parent, false); // 부모 연결
        node.transform.localPosition = position; // 위치 적용
        return node.transform; // 기준 반환
    }

    public static Material Mat(string name, Color color, bool glow = false) // 공용 재질 확보
    {
        string folder = "Assets/_Project/Materials/Generated/Day13"; // 이번 훈련장 재질 위치
        ProjectKDay10ModelFactory.EnsureFolder(folder); // 폴더 확보
        string path = folder + "/" + name + ".mat"; // 재질 파일 경로
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 이미 편집한 재질 확인
        if (material != null) // 기존 재질 보존
        {
            return material; // 재실행 시 덮어쓰기 생략
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 실제 프로젝트 렌더링 사용
        shader = shader != null ? shader : Shader.Find("Standard"); // 기본 셰이더 대체
        if (shader == null) // 셰이더 누락 확인
        {
            throw new InvalidOperationException("Day13: Lit 셰이더 누락"); // 잘못된 재질 생성 중단
        }
        material = new Material(shader); // 공유 재질 생성
        material.name = name; // 식별 이름
        material.enableInstancing = true; // 반복 부품 렌더링 지원
        if (material.HasProperty("_BaseColor")) // URP 색상 지원 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }
        if (material.HasProperty("_Color")) // 기본 색상 지원 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }
        if (glow && material.HasProperty("_EmissionColor")) // 발광 표시 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 기능 사용
            material.SetColor("_EmissionColor", color * 1.5f); // 네온 과노출 제한
        }
        AssetDatabase.CreateAsset(material, path); // 재질 저장
        return material; // 생성된 재질 반환
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid = false) // 모형과 충돌 분리 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 실제 메시가 있는 부품 생성
        part.name = name; // 부품 이름
        part.transform.SetParent(parent, false); // 지정 구역 연결
        part.transform.localPosition = position; // 로컬 위치 적용
        part.transform.localScale = scale; // 크기 적용
        part.GetComponent<Renderer>().sharedMaterial = material; // 공용 재질 사용
        Collider collider = part.GetComponent<Collider>(); // 자동 충돌체 조회
        if (!solid && collider != null) // 장식 부품 확인
        {
            UnityEngine.Object.DestroyImmediate(collider); // 장식이 사격을 가리지 않도록 제거
        }
        return part; // 생성 결과
    }

    public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = false) // 각진 부품 생성
    {
        return Part(parent, name, PrimitiveType.Cube, position, size, material, solid); // 상자 도형 재사용
    }

    public static GameObject Tube(Transform parent, string name, Vector3 position, float radius, float length, Material material) // 앞쪽으로 뻗은 원통
    {
        GameObject tube = Part(parent, name, PrimitiveType.Cylinder, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material); // 높이 두 배 기본 메시 보정
        tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Z축 방향 정렬
        return tube; // 부품 반환
    }

    public static TextMesh Label(Transform parent, string text, Vector3 position, float size = 0.06f) // 단일 월드 글자 생성
    {
        Transform node = Node(parent, "Label", position); // 글자 위치 생성
        TextMesh label = node.gameObject.AddComponent<TextMesh>(); // 별도 캔버스 없는 글자
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 6 내장 글꼴
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material; // 실제 글자 재질 연결
        label.fontSize = 56; // 충분한 글자 해상도
        label.characterSize = size; // 월드 표시 크기
        label.anchor = TextAnchor.MiddleCenter; // 중앙 기준 정렬
        label.alignment = TextAlignment.Center; // 여러 줄 정렬
        label.color = Color.white; // 어두운 패널 대비
        label.text = text; // 안내 문구
        return label; // 상태 갱신용 참조
    }

    public static void Panel(Transform parent, string name, Vector3 position, string text, Material metal, Material accent) // 표지판과 프레임
    {
        Transform sign = Node(parent, name, position); // 표지판 기준
        Box(sign, "Panel", Vector3.zero, new Vector3(5f, 1.2f, 0.16f), metal); // 뒷면 판
        Box(sign, "Accent", new Vector3(0f, -0.52f, -0.092f), new Vector3(4.75f, 0.05f, 0.02f), accent); // 하단 강조선
        Label(sign, text, new Vector3(0f, 0f, -0.1f), 0.039f); // 읽기 쉬운 구역 이름
    }
}
#endif // 게임 실행 빌드에서 생성 도구 제외
