#if UNITY_EDITOR // 에디터 전용 에셋 생성
using System.IO; // 에셋 폴더 경로 기능
using UnityEditor; // 에셋과 프리팹 저장
using UnityEditor.SceneManagement; // 임시 프리뷰 씬 정리
using UnityEngine; // 모형 부품 생성
using UnityEngine.SceneManagement; // 임시 씬으로 모형 이동

public static class ProjectKDay10ModelFactory // 검증용 권총 프리팹 생성
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Day10/TestPistol.prefab"; // 총기 프리팹 경로
    public const string MaterialFolder = "Assets/_Project/Materials/Generated/Day10"; // 전용 재질 저장 경로

    public static GameObject GetPistolPrefab() // 기존 조정 프리팹을 보존하는 권총 확보
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); // 기존 프리팹 조회
        if (existing != null) // 생성 완료된 총기 확인
        {
            return existing; // 재실행 시 수동 모델링 보존
        }

        EnsureFolder("Assets/_Project/Prefabs/Day10"); // 프리팹 폴더 확보
        Material dark = MaterialAsset("PistolFrame", new Color(0.075f, 0.10f, 0.14f), false); // 총기 프레임 재질
        Material metal = MaterialAsset("PistolMetal", new Color(0.47f, 0.56f, 0.63f), false); // 슬라이드와 부속 금속
        Material glow = MaterialAsset("PistolPulse", new Color(1f, 0.48f, 0.08f), true); // 주황 펄스 발광
        Material cyan = MaterialAsset("PistolSight", new Color(0.10f, 0.87f, 0.93f), true); // 청록 조준기 발광
        Scene preview = EditorSceneManager.NewPreviewScene(); // 저장되지 않는 제작 씬
        try // 임시 모형 정리 보장
        {
            GameObject root = new GameObject("D10_TestPistol"); // 총기 프리팹 루트
            SceneManager.MoveGameObjectToScene(root, preview); // 훈련장과 분리한 제작 공간
            Transform parts = Child(root.transform, "MovingParts", Vector3.zero); // 반동 외형 루트
            Part(parts, "Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.06f), new Vector3(0.18f, 0.15f, 0.57f), dark, Vector3.zero); // 총기 하부 프레임
            Part(parts, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0.075f), new Vector3(0.165f, 0.11f, 0.54f), metal, Vector3.zero); // 상부 슬라이드
            Part(parts, "RearCap", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.235f), new Vector3(0.19f, 0.19f, 0.055f), dark, Vector3.zero); // 후면 장갑
            Part(parts, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.17f, -0.095f), new Vector3(0.14f, 0.29f, 0.155f), dark, new Vector3(-14f, 0f, 0f)); // 기울어진 손잡이
            Part(parts, "GripPanelL", PrimitiveType.Cube, new Vector3(-0.074f, -0.17f, -0.09f), new Vector3(0.012f, 0.20f, 0.12f), metal, new Vector3(-14f, 0f, 0f)); // 왼쪽 손잡이 패널
            Part(parts, "GripPanelR", PrimitiveType.Cube, new Vector3(0.074f, -0.17f, -0.09f), new Vector3(0.012f, 0.20f, 0.12f), metal, new Vector3(-14f, 0f, 0f)); // 오른쪽 손잡이 패널
            Part(parts, "TriggerFront", PrimitiveType.Cube, new Vector3(0f, -0.12f, 0.115f), new Vector3(0.06f, 0.19f, 0.03f), dark, Vector3.zero); // 방아쇠 보호대 앞부분
            Part(parts, "TriggerBottom", PrimitiveType.Cube, new Vector3(0f, -0.21f, 0.04f), new Vector3(0.06f, 0.025f, 0.17f), dark, Vector3.zero); // 방아쇠 보호대 하단
            Part(parts, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.03f), new Vector3(0.035f, 0.075f, 0.025f), metal, new Vector3(20f, 0f, 0f)); // 방아쇠 외형
            Part(parts, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0.36f), new Vector3(0.12f, 0.075f, 0.12f), dark, new Vector3(90f, 0f, 0f)); // 전면 총열 하우징
            Part(parts, "MuzzleRing", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0.422f), new Vector3(0.13f, 0.018f, 0.13f), metal, new Vector3(90f, 0f, 0f)); // 총구 테두리
            Part(parts, "Emitter", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0.443f), new Vector3(0.070f, 0.004f, 0.070f), glow, new Vector3(90f, 0f, 0f)); // 펄스 방출구
            Part(parts, "RearSightL", PrimitiveType.Cube, new Vector3(-0.045f, 0.18f, -0.12f), new Vector3(0.027f, 0.050f, 0.035f), cyan, Vector3.zero); // 후방 조준점 왼쪽
            Part(parts, "RearSightR", PrimitiveType.Cube, new Vector3(0.045f, 0.18f, -0.12f), new Vector3(0.027f, 0.050f, 0.035f), cyan, Vector3.zero); // 후방 조준점 오른쪽
            Part(parts, "FrontSight", PrimitiveType.Cube, new Vector3(0f, 0.175f, 0.29f), new Vector3(0.025f, 0.045f, 0.035f), glow, Vector3.zero); // 전방 조준점
            Part(parts, "PulseStripL", PrimitiveType.Cube, new Vector3(-0.088f, 0.045f, 0.14f), new Vector3(0.011f, 0.025f, 0.25f), glow, Vector3.zero); // 좌측 발광 표시
            Part(parts, "PulseStripR", PrimitiveType.Cube, new Vector3(0.088f, 0.045f, 0.14f), new Vector3(0.011f, 0.025f, 0.25f), glow, Vector3.zero); // 우측 발광 표시
            for (int i = 0; i < 4; i++) // 냉각 핀 반복 생성
            {
                float z = -0.14f + i * 0.045f; // 냉각 홈 간격
                Part(parts, "VentL_" + i, PrimitiveType.Cube, new Vector3(-0.09f, 0.09f, z), new Vector3(0.012f, 0.07f, 0.015f), dark, Vector3.zero); // 왼쪽 냉각 홈
                Part(parts, "VentR_" + i, PrimitiveType.Cube, new Vector3(0.09f, 0.09f, z), new Vector3(0.012f, 0.07f, 0.015f), dark, Vector3.zero); // 오른쪽 냉각 홈
            }

            Transform magazine = Part(root.transform, "Magazine", PrimitiveType.Cube, new Vector3(0f, -0.30f, -0.12f), new Vector3(0.15f, 0.065f, 0.17f), metal, Vector3.zero).transform; // 별도로 움직이는 탄창 바닥
            Transform muzzle = Child(parts, "Muzzle", new Vector3(0f, 0.055f, 0.48f)); // 판정에 사용할 총구 앞 위치
            GameObject flash = Part(muzzle, "MuzzleFlash", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.045f), new Vector3(0.15f, 0.15f, 0.23f), glow, Vector3.zero); // 일시적 총구 발광 외형
            flash.SetActive(false); // 발사 전 효과 숨김
            root.AddComponent<FirearmView>().Configure(muzzle, parts, magazine, flash); // 발사 표시와 실제 총구 연결
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved); // 프리팹 에셋 저장
            if (!saved || prefab == null) // 저장 결과 확인
            {
                throw new IOException("Day10: 권총 프리팹 저장 실패"); // 불완전 설정 중단
            }

            return prefab; // 저장된 프리팹 반환
        }
        finally // 임시 제작 씬 종료
        {
            EditorSceneManager.ClosePreviewScene(preview); // 임시 모형과 씬 정리
        }
    }

    public static Material MaterialAsset(string name, Color color, bool emissive) // 조정한 재질을 유지하는 생성
    {
        EnsureFolder(MaterialFolder); // 전용 재질 폴더 확보
        string path = MaterialFolder + "/" + name + ".mat"; // 재질 경로
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 확인
        if (material != null) // 이미 조정한 재질 확인
        {
            return material; // 기존 재질 유지
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 프로젝트 URP 재질
        shader = shader != null ? shader : Shader.Find("Standard"); // 다른 파이프라인의 기본 대안
        if (shader == null) // 지원 셰이더 확인
        {
            throw new IOException("Day10: Lit 또는 Standard 셰이더가 없습니다."); // 분홍 재질 생성 방지
        }

        material = new Material(shader); // 새 재질 생성
        material.name = name; // 재질 이름 지정
        if (material.HasProperty("_BaseColor")) // URP 색상 지원 확인
        {
            material.SetColor("_BaseColor", color); // URP 기본 색상
        }

        if (material.HasProperty("_Color")) // 일반 색상 지원 확인
        {
            material.SetColor("_Color", color); // 기본 색상
        }

        if (material.HasProperty("_Metallic")) // 금속 속성 지원 확인
        {
            material.SetFloat("_Metallic", emissive ? 0.1f : 0.65f); // 총기 금속 느낌
        }

        if (emissive && material.HasProperty("_EmissionColor")) // 발광 설정 지원 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 기능 활성화
            material.SetColor("_EmissionColor", color * 1.7f); // 약한 발광 적용
        }

        AssetDatabase.CreateAsset(material, path); // 실제 재질 에셋 저장
        return material; // 완성 재질 반환
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 rotation) // 충돌 없는 표시 부품
    {
        GameObject part = GameObject.CreatePrimitive(type); // 기초 부품 생성
        part.name = name; // 부품 식별 이름
        part.transform.SetParent(parent, false); // 모형 하위 연결
        part.transform.localPosition = position; // 설계 위치 적용
        part.transform.localRotation = Quaternion.Euler(rotation); // 부품 회전 적용
        part.transform.localScale = scale; // 부품 크기 적용
        part.GetComponent<Renderer>().sharedMaterial = material; // 재질 연결
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); // 총기 장식의 자기 충돌 방지
        return part; // 생성 부품 반환
    }

    private static Transform Child(Transform parent, string name, Vector3 position) // 비어 있는 장착 기준 생성
    {
        Transform child = new GameObject(name).transform; // 기준 오브젝트 생성
        child.SetParent(parent, false); // 부모 연결
        child.localPosition = position; // 기준 위치 지정
        return child; // 생성 기준 반환
    }

    public static void EnsureFolder(string path) // 중첩 에셋 폴더 생성
    {
        if (AssetDatabase.IsValidFolder(path)) // 기존 폴더 확인
        {
            return; // 기존 폴더 유지
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); // 에셋 상위 경로
        EnsureFolder(parent); // 상위 폴더 먼저 확보
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); // 필요한 폴더만 추가
    }
}
#endif // 에디터 전용 코드 제외
