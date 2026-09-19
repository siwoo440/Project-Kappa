#if UNITY_EDITOR // 에디터 전용 기능
using System; // 에셋 생성 오류 처리
using UnityEditor; // 에셋 저장 기능
using UnityEditor.SceneManagement; // 임시 제작 씬 기능
using UnityEngine; // 모형 생성 기능
using UnityEngine.SceneManagement; // 오브젝트 씬 배치

public static class ProjectKDay9ModelFactory // 장비 테스트 모형 제작 도구
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Day9"; // 장비 프리팹 경로
    public const string MaterialFolder = "Assets/_Project/Materials/Generated/Day9"; // 장비 재질 경로

    public static void EnsureFolder(string path) // 중첩 에셋 폴더 확보
    {
        string[] parts = path.Split('/'); // 경로 단계 구분
        string parent = parts[0]; // 에셋 루트 경로
        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = parent + "/" + parts[i]; // 다음 폴더 경로
            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 확인
            {
                AssetDatabase.CreateFolder(parent, parts[i]); // 필요한 폴더 생성
            }

            parent = next; // 다음 단계 기준 갱신
        }
    }

    public static Material MaterialAsset(string name, Color color, bool glow = false, bool softEffect = false, bool lineEffect = false) // 재사용 재질 생성
    {
        EnsureFolder(MaterialFolder); // 저장 폴더 확보
        string path = MaterialFolder + "/" + name + ".mat"; // 재질 파일 경로
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 편집 재질 조회
        if (existing != null) // 기존 재질 확인
        {
            return existing; // 사용자가 조정한 재질 유지
        }

        Shader shader = Shader.Find(softEffect || lineEffect ? "ProjectK/Day9/SoftEffect" : "Universal Render Pipeline/Lit"); // 효과와 장비 셰이더 선택
        if (shader == null && !softEffect && !lineEffect) // URP 없는 프로젝트 확인
        {
            shader = Shader.Find("Standard"); // 기본 렌더러 셰이더 대체
        }

        if (shader == null) // 셰이더 누락 확인
        {
            throw new InvalidOperationException("Day9 shader import is incomplete."); // 잘못된 재질 생성 차단
        }

        Material material = new Material(shader); // 재질 생성
        material.name = name; // 재질 이름 지정
        if (material.HasProperty("_BaseColor")) // URP 색상 확인
        {
            material.SetColor("_BaseColor", color); // 장비 표면 색상 적용
        }

        if (material.HasProperty("_Color")) // 기본 색상 확인
        {
            material.SetColor("_Color", color); // 기본 재질 색상 적용
        }

        if (softEffect || lineEffect) // 효과 재질 확인
        {
            material.SetFloat("_SoftParticle", softEffect ? 1f : 0f); // 연막 가장자리 설정
        }

        if (glow && material.HasProperty("_EmissionColor")) // 발광 재질 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 기능 활성화
            material.SetColor("_EmissionColor", color * 1.5f); // 낮은 발광 강도 적용
        }

        AssetDatabase.CreateAsset(material, path); // 재질 파일 저장
        return material; // 저장된 재질 반환
    }

    public static GameObject WeaponPrefab(int kind) // 종류별 근접 무기 모형
    {
        string[] names = new string[] // 근접 무기 프리팹 이름
        {
            "Jeolseon", // 절선
            "Chimmuk", // 침묵
            "Paseong", // 파성
            "CurrentBaton" // 전류봉
        };
        string path = PrefabFolder + "/" + names[kind] + ".prefab"; // 모형 저장 경로
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 프리팹 조회
        if (existing != null) // 이미 제작된 모형 확인
        {
            return existing; // 사용자의 모형 편집 유지
        }

        Material dark = MaterialAsset("EquipmentDark", new Color(0.08f, 0.11f, 0.15f)); // 손잡이 재질
        Material metal = MaterialAsset("EquipmentMetal", new Color(0.58f, 0.67f, 0.73f)); // 금속 재질
        Color[] accents = new Color[] // 무기별 구분 색상
        {
            new Color(0.12f, 0.9f, 1f), // 절선 청록색
            new Color(0.68f, 0.36f, 0.95f), // 침묵 보라색
            new Color(1f, 0.43f, 0.1f), // 파성 주황색
            new Color(0.3f, 0.78f, 1f) // 전류봉 푸른색
        };
        Material glow = MaterialAsset(names[kind] + "Glow", accents[kind], true); // 무기별 발광 재질
        Scene preview = EditorSceneManager.NewPreviewScene(); // 실제 씬과 분리된 제작 공간
        try // 제작 중 임시 공간 보호
        {
            GameObject root = new GameObject(names[kind]); // 모형 루트 생성
            SceneManager.MoveGameObjectToScene(root, preview); // 임시 제작 씬으로 이동
            Part(root.transform, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.2f, 0.09f), dark); // 손잡이 생성
            for (int i = 0; i < 5; i++) // 손잡이 감개 생성
            {
                Part(root.transform, "GripBand_" + i, PrimitiveType.Cylinder, new Vector3(0f, -0.32f + i * 0.075f, 0f), new Vector3(0.105f, 0.013f, 0.105f), metal); // 금속 감개 장식
            }

            Part(root.transform, "Pommel", PrimitiveType.Sphere, new Vector3(0f, -0.39f, 0f), Vector3.one * 0.13f, glow); // 손잡이 끝 발광부
            if (kind == 3) // 전류봉 모형 구분
            {
                Part(root.transform, "BatonCore", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 0.47f, 0.14f), dark); // 전류봉 몸체
                for (int i = 0; i < 6; i++) // 전류 링 순회
                {
                    Part(root.transform, "ElectricCoil_" + i, PrimitiveType.Cylinder, new Vector3(0f, 0.35f + i * 0.11f, 0f), new Vector3(0.19f, 0.025f, 0.19f), glow); // 전류 코일
                }

                Part(root.transform, "LeftProng", PrimitiveType.Cube, new Vector3(-0.09f, 1.0f, 0f), new Vector3(0.06f, 0.24f, 0.1f), metal); // 좌측 방전 단자
                Part(root.transform, "RightProng", PrimitiveType.Cube, new Vector3(0.09f, 1.0f, 0f), new Vector3(0.06f, 0.24f, 0.1f), metal); // 우측 방전 단자
            }
            else // 검과 단검 모형 구분
            {
                float length = kind == 1 ? 0.65f : kind == 2 ? 1.75f : 1.35f; // 무기별 검날 길이
                float width = kind == 1 ? 0.10f : kind == 2 ? 0.32f : 0.12f; // 무기별 검날 폭
                Part(root.transform, "CrossGuard", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(kind == 2 ? 0.65f : 0.38f, 0.09f, 0.16f), dark); // 손 보호대
                Part(root.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, length * 0.5f + 0.11f, 0f), new Vector3(width, length, 0.065f), metal); // 금속 검날
                Part(root.transform, "Spine", PrimitiveType.Cube, new Vector3(-width * 0.35f, length * 0.5f + 0.11f, 0f), new Vector3(width * 0.24f, length * 0.92f, 0.08f), dark); // 검날 등 부분
                Part(root.transform, "EnergyEdge", PrimitiveType.Cube, new Vector3(width * 0.58f, length * 0.5f + 0.1f, 0f), new Vector3(0.025f, length, 0.075f), glow); // 얇은 발광 날
                Part(root.transform, "Tip", PrimitiveType.Cube, new Vector3(0f, length + 0.13f, 0f), new Vector3(width * 0.72f, width * 0.72f, 0.055f), metal, new Vector3(0f, 0f, 45f)); // 끝부분 마름모 모형
                if (kind == 2) // 대검의 추가 장갑 확인
                {
                    Part(root.transform, "BladeReinforcement", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0f), new Vector3(0.46f, 0.38f, 0.11f), dark); // 대검 하부 보강판
                    Part(root.transform, "PowerCell", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0.075f), new Vector3(0.18f, 0.18f, 0.04f), glow); // 대검 동력 코어
                }
            }

            return Save(root, path); // 완성 모형 프리팹 저장
        }
        finally // 임시 제작 공간 정리
        {
            EditorSceneManager.ClosePreviewScene(preview); // 제작 중 오브젝트 일괄 제거
        }
    }

    public static GameObject GadgetPrefab(string kind) // 보조장비와 소모품 외형 생성
    {
        string path = PrefabFolder + "/" + kind + ".prefab"; // 소도구 프리팹 경로
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 모형 조회
        if (existing != null) // 기존 편집 모형 확인
        {
            return existing; // 모형 편집 유지
        }

        Material dark = MaterialAsset("EquipmentDark", new Color(0.08f, 0.11f, 0.15f)); // 본체 재질
        Material metal = MaterialAsset("EquipmentMetal", new Color(0.58f, 0.67f, 0.73f)); // 금속 재질
        Color color = kind == "Injector" ? new Color(0.25f, 1f, 0.5f) : kind == "NoiseLure" ? new Color(1f, 0.78f, 0.14f) : new Color(0.58f, 0.4f, 1f); // 도구 구분 색상
        Material glow = MaterialAsset(kind + "Glow", color, true); // 도구 상태등 재질
        Scene preview = EditorSceneManager.NewPreviewScene(); // 임시 제작 씬 생성
        try // 제작 공간 보호
        {
            GameObject root = new GameObject(kind); // 소도구 루트 생성
            SceneManager.MoveGameObjectToScene(root, preview); // 제작 공간 이동
            if (kind == "DartLauncher") // 손목형 마비침 발사기
            {
                Part(root.transform, "Housing", PrimitiveType.Cube, Vector3.zero, new Vector3(0.20f, 0.14f, 0.46f), dark); // 발사기 본체
                Part(root.transform, "ArmBand", PrimitiveType.Cube, new Vector3(0f, -0.09f, -0.1f), new Vector3(0.29f, 0.06f, 0.15f), metal); // 손목 고정 밴드
                for (int i = 0; i < 3; i++) // 마비침 총구 순회
                {
                    Part(root.transform, "DartBarrel_" + i, PrimitiveType.Cylinder, new Vector3(-0.07f + i * 0.07f, 0f, 0.27f), new Vector3(0.045f, 0.14f, 0.045f), metal, new Vector3(90f, 0f, 0f)); // 세 갈래 발사관
                }

                Part(root.transform, "ChargeIndicator", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.025f, 0.15f), glow); // 충전 상태등
            }
            else // 투척과 회복 소도구
            {
                Part(root.transform, "Canister", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.17f, 0.18f, 0.17f), dark); // 원통 본체
                Part(root.transform, "TopCap", PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.2f, 0.035f, 0.2f), metal); // 상단 금속 캡
                Part(root.transform, "BottomCap", PrimitiveType.Cylinder, new Vector3(0f, -0.18f, 0f), new Vector3(0.2f, 0.035f, 0.2f), metal); // 하단 금속 캡
                for (int i = 0; i < 3; i++) // 구분 띠 생성
                {
                    Part(root.transform, "StatusBand_" + i, PrimitiveType.Cylinder, new Vector3(0f, -0.1f + i * 0.1f, 0f), new Vector3(0.19f, 0.014f, 0.19f), glow); // 발광 구분 띠
                }

                if (kind == "Injector") // 회복 주입기 외형
                {
                    Part(root.transform, "Needle", PrimitiveType.Cylinder, new Vector3(0f, 0.31f, 0f), new Vector3(0.018f, 0.095f, 0.018f), metal); // 주입 노즐
                    Part(root.transform, "FingerGrip", PrimitiveType.Cube, new Vector3(0f, -0.16f, 0f), new Vector3(0.32f, 0.04f, 0.08f), dark); // 손가락 받침
                }
                else if (kind == "NoiseLure") // 유인기 스피커 외형
                {
                    Part(root.transform, "Speaker", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.12f), new Vector3(0.11f, 0.035f, 0.11f), metal, new Vector3(90f, 0f, 0f)); // 전면 스피커
                    Part(root.transform, "Antenna", PrimitiveType.Cylinder, new Vector3(0.06f, 0.31f, 0f), new Vector3(0.018f, 0.12f, 0.018f), glow); // 유인 신호 안테나
                }
                else // 연막 캡슐 외형
                {
                    Part(root.transform, "Trigger", PrimitiveType.Cube, new Vector3(0.07f, 0.23f, 0f), new Vector3(0.06f, 0.12f, 0.04f), glow); // 연막 작동 레버
                }
            }

            return Save(root, path); // 소도구 프리팹 저장
        }
        finally // 임시 제작 공간 정리
        {
            EditorSceneManager.ClosePreviewScene(preview); // 제작 공간 종료
        }
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 euler = default) // 장식 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(type); // 기본 도형 생성
        part.name = name; // 부품 이름 적용
        part.transform.SetParent(parent, false); // 모형 루트 연결
        part.transform.localPosition = position; // 로컬 위치 적용
        part.transform.localRotation = Quaternion.Euler(euler); // 로컬 회전 적용
        part.transform.localScale = scale; // 부품 크기 적용
        part.GetComponent<Renderer>().sharedMaterial = material; // 공유 재질 적용
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); // 장식 충돌체 제거
        return part; // 완성 부품 반환
    }

    public static void Label(Transform parent, string text, Vector3 position, float size) // 훈련대 이름표 생성
    {
        GameObject label = new GameObject("Label_" + text); // 이름표 루트 생성
        label.transform.SetParent(parent, false); // 훈련대 연결
        label.transform.localPosition = position; // 이름표 위치 설정
        label.transform.localRotation = Quaternion.identity; // 전면 기준 글자 방향
        TextMesh mesh = label.AddComponent<TextMesh>(); // 공간 글자 추가
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 지원되는 내장 글꼴 사용
        mesh.fontSize = 48; // 글꼴 해상도 설정
        mesh.characterSize = size; // 월드 문자 크기 설정
        mesh.anchor = TextAnchor.MiddleCenter; // 중앙 기준 정렬
        mesh.alignment = TextAlignment.Center; // 문자열 중앙 정렬
        mesh.text = text; // 안내 문구 적용
        mesh.color = Color.white; // 밝은 글자 색상
        label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material; // 글꼴 텍스처 재질 연결
    }

    private static GameObject Save(GameObject root, string path) // 안전한 프리팹 파일 저장
    {
        EnsureFolder(PrefabFolder); // 프리팹 폴더 확보
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); // 실제 프리팹 에셋 저장
        if (!saved || prefab == null) // 저장 결과 확인
        {
            throw new InvalidOperationException("Failed to save " + path); // 실패한 모형 연결 차단
        }

        return prefab; // 저장된 프리팹 반환
    }
}
#endif // 에디터 전용 기능 종료
