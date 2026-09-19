#if UNITY_EDITOR // 편집기 전용 시설 생성
using System; // 필수 에셋 오류 보고
using System.Collections.Generic; // 재질 재사용 목록
using UnityEditor; // 에셋 생성과 편집
using UnityEngine; // 기본 모형 구성
using UnityEngine.Rendering; // 장식 그림자 설정

public static class TrainingCenterGeometry // 통일된 시설 부품과 재질
{
    public const string Art = "Assets/_Project/Art/TrainingCenter"; // 제공한 표면과 안내판 자료
    public const string Materials = "Assets/_Project/Materials/TrainingCenter"; // 생성할 공용 재질
    private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>(); // 중복 재질 생성 방지
    public static Material Concrete => Mat("Concrete", new Color(0.54f, 0.58f, 0.60f), "Concrete"); // 낡은 콘크리트
    public static Material Steel => Mat("Steel", new Color(0.18f, 0.24f, 0.29f)); // 공용 구조 금속
    public static Material LightSteel => Mat("LightSteel", new Color(0.50f, 0.57f, 0.61f)); // 외장과 프레임 금속
    public static Material Dark => Mat("Rubber", new Color(0.10f, 0.13f, 0.16f), "Rubber"); // 탄도 배경과 고무
    public static Material White => Mat("TargetWhite", new Color(0.83f, 0.86f, 0.84f)); // 읽기 쉬운 밝은 표적
    public static Material Cyan => Mat("Cyan", new Color(0.09f, 0.70f, 0.76f), null, true); // 안전 구역 안내
    public static Material Amber => Mat("Amber", new Color(0.96f, 0.51f, 0.12f), null, true); // 사격 구역 안내
    public static Material Violet => Mat("Violet", new Color(0.50f, 0.39f, 0.83f), null, true); // 이동 훈련 안내
    public static Material Red => Mat("Red", new Color(0.82f, 0.15f, 0.16f), null, true); // 실제 적 구역 안내
    public static Material Marks => Mat("Marks", Color.white); // 기존 탄착 시스템 공용 재질

    public static void Folder(string path) // 필요한 에셋 폴더 확보
    {
        if (AssetDatabase.IsValidFolder(path)) // 기존 폴더 확인
        {
            return; // 중복 생성 방지
        }
        int cut = path.LastIndexOf('/'); // 상위 폴더 구분
        if (cut < 0) // 잘못된 최상위 경로 확인
        {
            throw new InvalidOperationException("에셋 폴더 경로 오류: " + path); // 잘못된 설치 중단
        }
        Folder(path.Substring(0, cut)); // 상위 폴더 선행 생성
        AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1)); // 현재 폴더 생성
    }

    public static Material Mat(string name, Color color, string texture = null, bool glow = false) // 기존 재질을 보존하는 공용 설정
    {
        if (cache.TryGetValue(name, out Material known) && known != null) // 이번 구성에서 사용한 재질 확인
        {
            return known; // 동일 재질 재사용
        }
        Folder(Materials); // 저장 경로 확보
        string path = Materials + "/" + name + ".mat"; // 고정 에셋 경로
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 사용자가 편집한 재질 조회
        if (material == null) // 새 재질만 생성
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); // 현재 렌더러에 맞춘 기본 셰이더
            if (shader == null) // 표시 가능한 셰이더 확인
            {
                throw new InvalidOperationException("URP Lit 또는 Standard 셰이더가 필요합니다."); // 분홍색 모형 생성 방지
            }
            material = new Material(shader); // 공유 재질 생성
            material.name = name; // 식별 이름 적용
            material.color = color; // 기본 색상 적용
            material.enableInstancing = true; // 반복 부품 재질 공유
            if (material.HasProperty("_Smoothness")) // URP 광택 설정 확인
            {
                material.SetFloat("_Smoothness", 0.22f); // 눈부심 적은 표면
            }
            if (texture != null) // 제공한 표면 자료 확인
            {
                Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + texture + ".png"); // 실제 래스터 표면 조회
                material.mainTexture = image; // 바닥과 방호판 질감 연결
                if (material.HasProperty("_BaseMap")) // URP 표면 연결 확인
                {
                    material.SetTexture("_BaseMap", image); // 렌더러별 주 텍스처 적용
                }
            }
            if (glow) // 안내용 발광 재질 확인
            {
                material.EnableKeyword("_EMISSION"); // 제한적인 안내 발광 사용
                material.SetColor("_EmissionColor", color * 0.7f); // 밝은 낮에도 읽히는 강조
            }
            AssetDatabase.CreateAsset(material, path); // 재사용 가능한 재질 저장
        }
        cache[name] = material; // 이번 구성 참조 저장
        return material; // 공용 재질 반환
    }

    public static Transform Node(Transform parent, string name, Vector3 position) // 단위 크기의 기능별 부모
    {
        GameObject node = new GameObject(name); // 정리 가능한 계층 생성
        node.transform.SetParent(parent, false); // 새 시설 부모 연결
        node.transform.localPosition = position; // 부모 기준 위치 저장
        return node.transform; // 하위 부품 연결용 반환
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 point, Vector3 size, Material material, bool collision = false) // 장식과 충돌을 분리한 부품
    {
        GameObject part = GameObject.CreatePrimitive(shape); // 공유 기본 메시 생성
        part.name = name; // 기능별 이름 적용
        part.transform.SetParent(parent, false); // 모형 부모 연결
        part.transform.localPosition = point; // 정확한 배치 위치
        part.transform.localScale = size; // 부품 규격 적용
        Renderer renderer = part.GetComponent<Renderer>(); // 표시 컴포넌트 조회
        renderer.sharedMaterial = material; // 개별 재질 복제 방지
        Collider collider = part.GetComponent<Collider>(); // 기본 충돌체 조회
        if (!collision && collider != null) // 장식 부품의 걸림 방지
        {
            UnityEngine.Object.DestroyImmediate(collider); // 실제 기능이 없는 충돌 제거
        }
        if (!collision) // 작은 장식 그림자 확인
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off; // 반복 소형 장식의 그림자 부하 감소
        }
        return part; // 추가 조립용 객체 반환
    }

    public static GameObject Box(Transform parent, string name, Vector3 point, Vector3 size, Material material, bool collision = false) // 사각 구조물 생성
    {
        return Part(parent, name, PrimitiveType.Cube, point, size, material, collision); // 공통 생성 경로 사용
    }

    public static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material material, bool collision = false) // 두 점을 잇는 배관과 지지대
    {
        Vector3 delta = b - a; // 연결 방향 계산
        if (delta.sqrMagnitude < 0.00001f) // 영점 길이 확인
        {
            return; // 잘못된 배관 제외
        }
        GameObject pipe = Part(parent, name, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(radius * 2f, delta.magnitude * 0.5f, radius * 2f), material, collision); // 기본 실린더 길이 보정
        pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized); // 연결 방향 정렬
    }

    public static void WallX(Transform parent, string name, float x1, float x2, float z, float height = 4.6f) // 가로 방호벽
    {
        Box(parent, name, new Vector3((x1 + x2) * 0.5f, height * 0.5f, z), new Vector3(x2 - x1, height, 0.45f), Concrete, true); // 보이는 면과 충돌면 일치
        Box(parent, name + "_Cap", new Vector3((x1 + x2) * 0.5f, height + 0.07f, z), new Vector3(x2 - x1 + 0.1f, 0.14f, 0.56f), Steel); // 벽 상부 보호판
        for (float x = x1 + 0.25f; x < x2; x += 8f) // 반복 구조 기둥
        {
            Box(parent, name + "_Rib", new Vector3(x, height * 0.5f, z), new Vector3(0.48f, height + 0.15f, 0.70f), Steel); // 벽의 구조 깊이 표현
        }
    }

    public static void WallZ(Transform parent, string name, float x, float z1, float z2, float height = 4.6f) // 세로 구획벽
    {
        Transform wall = Node(parent, name, new Vector3(x, 0f, z1)); // 세로 벽 기준
        wall.localRotation = Quaternion.Euler(0f, -90f, 0f); // 가로 벽의 세로 배치
        WallX(wall, "Structure", 0f, z2 - z1, 0f, height); // 동일 규격 재사용
    }

    public static void Stripe(Transform parent, string name, Vector3 point, Vector3 size, Material color) // 바닥 위 얇은 안내 표시
    {
        point.y = 0.014f; // 기본 바닥과 떨어진 표시 높이
        size.y = 0.012f; // 평면 겹침이 없는 표시 두께
        Box(parent, name, point, size, color); // 이동을 방해하지 않는 안내선
    }

    public static void Sign(Transform parent, string key, Vector3 point, float width, float height, float yaw = 0f) // 한글 래스터 안내판
    {
        string name = "Sign_" + key; // 안내판 재질 식별자
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat"); // 기존 안내판 재질
        if (material == null) // 최초 안내판 생성
        {
            Folder(Materials); // 재질 폴더 확보
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Signs/" + key + ".png"); // 제공된 한글 안내 이미지
            if (texture == null) // 필수 안내 이미지 확인
            {
                throw new InvalidOperationException("안내판 이미지 누락: " + key); // 잘못된 설치 즉시 중단
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"); // 조명과 독립된 안내 가독성
            if (shader == null) // 안내판 셰이더 누락 확인
            {
                throw new InvalidOperationException("URP Unlit 또는 Unlit/Texture 셰이더가 필요합니다."); // 표시 불가능한 안내판 생성 방지
            }
            material = new Material(shader); // 안내판 공용 재질
            material.mainTexture = texture; // 표기 이미지 적용
            if (material.HasProperty("_BaseMap")) // URP 텍스처 지원
            {
                material.SetTexture("_BaseMap", texture); // URP 안내판 이미지 연결
            }
            AssetDatabase.CreateAsset(material, Materials + "/" + name + ".mat"); // 재사용 에셋 저장
        }
        Transform board = Node(parent, name, point); // 안내판 위치
        board.localRotation = Quaternion.Euler(0f, yaw, 0f); // 접근 방향을 향한 표기
        Box(board, "Housing", Vector3.zero, new Vector3(width + 0.12f, height + 0.12f, 0.10f), Steel); // 실제 두께의 테두리
        Part(board, "Lettering", PrimitiveType.Quad, new Vector3(0f, 0f, -0.058f), new Vector3(width, height, 1f), material); // 앞쪽 한글 표면
    }
}
#endif
