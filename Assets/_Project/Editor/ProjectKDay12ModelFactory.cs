#if UNITY_EDITOR // 대표 총기 모형 생성 전용
using System; // 프리팹 생성 실패 처리
using UnityEditor; // 재질과 프리팹 저장
using UnityEditor.SceneManagement; // 임시 모형 씬 관리
using UnityEngine; // 모형 부품 생성
using UnityEngine.SceneManagement; // 임시 씬 분리

public static class ProjectKDay12ModelFactory // 크기와 실루엣이 다른 대표 총기 생성
{
    private const string Prefabs = "Assets/_Project/Prefabs/Day12"; // 이번 일차 프리팹 폴더
    private const string Materials = "Assets/_Project/Materials/Generated/Day12"; // 이번 일차 재질 폴더

    public static GameObject Build(int index) // 선택 총기 프리팹 확보
    {
        ProjectKDay10ModelFactory.EnsureFolder(Prefabs); // 프리팹 저장 폴더 확보
        string path = Prefabs + "/" + ProjectKDay12Catalog.Ids[index] + ".prefab"; // 고정 무기 식별자 경로
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 사용자가 수정한 기존 모형 조회
        if (existing != null) // 재실행 시 기존 모형 보존
        {
            return existing; // 기존 에셋 반환
        }

        Scene preview = EditorSceneManager.NewPreviewScene(); // 사용자 작업 씬과 분리된 모형 공간
        GameObject root = null; // 임시 모형 정리 참조
        try // 프리팹 실패 시 임시 객체 정리
        {
            root = new GameObject(ProjectKDay12Catalog.Ids[index]); // 무기 루트 생성
            SceneManager.MoveGameObjectToScene(root, preview); // 임시 공간으로 이동
            Transform moving = Node(root.transform, "Mechanism", Vector3.zero); // 공통 반동 부품 루트
            Material shell = Material("Graphite", new Color(0.055f, 0.07f, 0.095f)); // 무광 외장
            Material steel = Material("Steel", new Color(0.35f, 0.43f, 0.50f)); // 금속 기구
            Material rubber = Material("Grip", new Color(0.025f, 0.032f, 0.040f)); // 고무 손잡이
            Color accent = index == 0 ? new Color(0.08f, 0.8f, 0.9f) : index == 1 ? new Color(1f, 0.48f, 0.08f) : new Color(0.08f, 0.6f, 1f); // 무기별 식별 색상
            Material glow = Material("Accent_" + index, accent, true); // 식별 발광 재질
            Transform magazine = null; // 재장전 탄창 참조
            Transform muzzle = null; // 실제 총구 참조
            if (index == 0) // 유령손 권총 외형
            {
                Box(moving, "LowerFrame", new Vector3(0f, 0.025f, 0.12f), new Vector3(0.11f, 0.12f, 0.32f), shell); // 작은 권총 프레임
                Box(moving, "Slide", new Vector3(0f, 0.115f, 0.15f), new Vector3(0.115f, 0.11f, 0.40f), steel); // 길게 뻗은 슬라이드
                Box(moving, "QuietCoilShroud", new Vector3(0f, 0.105f, 0.39f), new Vector3(0.12f, 0.12f, 0.17f), shell); // 기획 저소음 코일 외장
                Tube(moving, "BarrelRim", new Vector3(0f, 0.105f, 0.49f), 0.027f, 0.07f, steel); // 금속 총구 테두리
                Tube(moving, "Bore", new Vector3(0f, 0.105f, 0.527f), 0.018f, 0.004f, rubber); // 어두운 총열 입구
                GameObject grip = Box(moving, "AngledGrip", new Vector3(0f, -0.13f, -0.045f), new Vector3(0.095f, 0.23f, 0.105f), rubber); // 권총 손잡이
                grip.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f); // 손잡이 기울기
                Guard(moving, new Vector3(0f, -0.045f, 0.1f), steel); // 방아쇠 보호대
                magazine = Node(moving, "Magazine", new Vector3(0f, -0.18f, -0.03f)); // 하부 탄창 기준
                Box(magazine, "MagazineBase", new Vector3(0f, -0.07f, 0f), new Vector3(0.10f, 0.025f, 0.12f), steel); // 탄창 하단 부품
                muzzle = Node(moving, "Muzzle", new Vector3(0f, 0.105f, 0.55f)); // 권총 실제 사격 원점
            }
            else // 기관단총과 소총 구분 외형
            {
                bool rifle = index == 2; // 청룡선 여부
                float length = rifle ? 0.65f : 0.43f; // 몸체 길이 구분
                Box(moving, "Receiver", new Vector3(0f, 0.065f, 0.12f), new Vector3(0.145f, 0.18f, length), shell); // 주 몸체
                Box(moving, "UpperHousing", new Vector3(0f, 0.19f, 0.15f), new Vector3(0.12f, 0.07f, length - 0.04f), steel); // 상부 기구
                Box(moving, "PistolGrip", new Vector3(0f, -0.15f, -0.10f), new Vector3(0.09f, 0.23f, 0.11f), rubber); // 뒤쪽 손잡이
                Guard(moving, new Vector3(0f, -0.035f, 0f), steel); // 방아쇠 보호대
                magazine = Node(moving, "Magazine", new Vector3(0f, -0.11f, 0.18f)); // 분리되는 탄창 기준
                GameObject mag = Box(magazine, "MagazineShell", new Vector3(0f, -0.115f, 0f), new Vector3(0.09f, rifle ? 0.29f : 0.25f, 0.135f), steel); // 큰 탄창 부품
                mag.transform.localRotation = Quaternion.Euler(rifle ? -14f : -6f, 0f, 0f); // 총기별 탄창 각도
                for (int i = 0; i < 5; i++) // 탄창 외부 홈
                {
                    Box(magazine, "MagazineGroove_" + i, new Vector3(0.047f, -0.04f - i * 0.04f, 0f), new Vector3(0.008f, 0.012f, 0.105f), rubber); // 탄창 측면 식별 홈
                }

                Box(moving, "StockRod", new Vector3(0f, 0.11f, -0.30f), new Vector3(0.055f, 0.07f, rifle ? 0.32f : 0.24f), steel); // 개머리판 연결부
                Box(moving, rifle ? "RifleStock" : "FoldingStock", new Vector3(0f, 0.045f, rifle ? -0.48f : -0.39f), new Vector3(0.11f, 0.20f, 0.075f), rubber); // 총기별 개머리판
                float muzzleZ = rifle ? 0.98f : 0.59f; // 길이가 다른 총열 끝
                Tube(moving, "Barrel", new Vector3(0f, 0.11f, rifle ? 0.68f : 0.43f), 0.025f, rifle ? 0.48f : 0.23f, steel); // 노출된 총열
                Tube(moving, "MuzzleBrake", new Vector3(0f, 0.11f, muzzleZ - 0.065f), 0.038f, 0.11f, shell); // 총구 보호부
                Tube(moving, "Bore", new Vector3(0f, 0.11f, muzzleZ - 0.007f), 0.022f, 0.004f, rubber); // 어두운 총구 구멍
                if (rifle) // 소총 추가 부품
                {
                    Box(moving, "Handguard", new Vector3(0f, 0.09f, 0.53f), new Vector3(0.17f, 0.17f, 0.28f), shell); // 긴 총열 보호대
                    Box(moving, "Foregrip", new Vector3(0f, -0.075f, 0.49f), new Vector3(0.07f, 0.18f, 0.08f), rubber); // 전방 보조 손잡이
                    Box(moving, "OpticBase", new Vector3(0f, 0.24f, 0.08f), new Vector3(0.10f, 0.035f, 0.16f), steel); // 소형 조준경 받침
                    Box(moving, "OpticHood", new Vector3(0f, 0.295f, 0.08f), new Vector3(0.075f, 0.08f, 0.12f), shell); // 조준경 외장
                    Box(moving, "OpticLens", new Vector3(0f, 0.295f, 0.143f), new Vector3(0.05f, 0.04f, 0.006f), glow); // 조준경 렌즈
                }
                else // 골목비 접이식 구조
                {
                    Box(moving, "CompactCoil", new Vector3(0.09f, 0.075f, 0.12f), new Vector3(0.045f, 0.09f, 0.24f), steel); // 짧은 외장 코일
                    Box(moving, "FrontGrip", new Vector3(0f, -0.015f, 0.34f), new Vector3(0.075f, 0.14f, 0.095f), rubber); // 짧은 전방 그립
                }

                muzzle = Node(moving, "Muzzle", new Vector3(0f, 0.11f, muzzleZ)); // 장총 실제 총구
            }

            for (int i = 0; i < 7; i++) // 몸체 상부 레일과 냉각 홈
            {
                Box(moving, "Rail_" + i, new Vector3(0f, index == 0 ? 0.18f : 0.245f, -0.03f + i * 0.044f), new Vector3(0.12f, 0.013f, 0.019f), shell); // 상부 레일 홈
                Box(moving, "Vent_" + i, new Vector3(index == 0 ? 0.061f : 0.08f, 0.10f, 0.08f + i * 0.038f), new Vector3(0.006f, 0.026f, 0.014f), rubber); // 측면 냉각 구멍
            }

            Box(moving, "EnergyStrip", new Vector3(index == 0 ? -0.061f : -0.079f, 0.09f, 0.18f), new Vector3(0.008f, 0.022f, index == 2 ? 0.36f : 0.22f), glow); // 총기 구분 발광선
            Box(moving, "RearSight", new Vector3(0f, index == 0 ? 0.195f : 0.26f, -0.045f), new Vector3(0.07f, 0.035f, 0.035f), glow); // 후방 조준 표시
            GameObject flash = Box(muzzle, "MuzzleFlash", new Vector3(0f, 0f, 0.055f), new Vector3(0.04f, 0.04f, 0.10f), Material("Flash", new Color(1f, 0.72f, 0.17f), true)); // 짧은 총구 효과
            flash.SetActive(false); // 발사 전 섬광 숨김
            root.AddComponent<FirearmView>().Configure(muzzle, moving, magazine, flash); // 기존 발사와 재장전 연출 재사용
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 프리팹 저장
            if (prefab == null) // 저장 결과 확인
            {
                throw new InvalidOperationException("Day12 모형 저장 실패: " + path); // 실패 경로 보고
            }

            return prefab; // 저장된 무기 반환
        }
        finally // 임시 모형과 씬 해제
        {
            if (root != null) // 남은 임시 루트 확인
            {
                UnityEngine.Object.DestroyImmediate(root); // 임시 모형만 제거
            }

            EditorSceneManager.ClosePreviewScene(preview); // 사용자 씬을 건드리지 않고 임시 공간 닫기
        }
    }

    public static Material Material(string name, Color color, bool emissive = false) // 공용 재질 확보
    {
        ProjectKDay10ModelFactory.EnsureFolder(Materials); // 재질 폴더 확보
        string path = Materials + "/" + name + ".mat"; // 재질 저장 경로
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 조정 재질 확인
        if (material != null) // 수동 편집 재질 보존
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 프로젝트 렌더링 셰이더 확인
        shader = shader != null ? shader : Shader.Find("Standard"); // 기본 렌더링 대체
        if (shader == null) // 사용 가능한 셰이더 확인
        {
            throw new InvalidOperationException("Day12: Lit 셰이더를 확인하세요."); // 분홍색 미완성 재질 방지
        }

        material = new Material(shader); // 새로운 공용 재질
        material.name = name; // 재질 식별 이름
        if (material.HasProperty("_BaseColor")) // URP 기본 색상 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }

        if (material.HasProperty("_Color")) // 기본 셰이더 색상 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }

        if (emissive && material.HasProperty("_EmissionColor")) // 발광 재질 여부
        {
            material.EnableKeyword("_EMISSION"); // 발광 셰이더 기능
            material.SetColor("_EmissionColor", color * 1.5f); // 약한 식별 발광
        }

        AssetDatabase.CreateAsset(material, path); // 재질 에셋 저장
        return material; // 생성 재질 반환
    }

    public static Transform Node(Transform parent, string name, Vector3 position) // 비어 있는 부품 기준 생성
    {
        Transform child = new GameObject(name).transform; // 기준 객체 생성
        child.SetParent(parent, false); // 부모 씬과 좌표 연결
        child.localPosition = position; // 기준 위치 설정
        return child; // 부품 기준 반환
    }

    public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid = false) // 상자 부품 생성
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상자 기본 모형
        Finish(part, parent, name, position, scale, material, solid); // 공통 부품 설정
        return part; // 생성 부품 반환
    }

    private static void Tube(Transform parent, string name, Vector3 position, float radius, float length, Material material) // 총열 방향 원통 생성
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 원통 기본 모형
        Finish(part, parent, name, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, false); // 기본 높이 두 배를 고려한 원통 크기
        part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 전방 총열 방향 정렬
    }

    private static void Guard(Transform parent, Vector3 center, Material material) // 방아쇠와 보호대 부품
    {
        Box(parent, "TriggerGuardBottom", center + new Vector3(0f, -0.065f, 0f), new Vector3(0.045f, 0.017f, 0.13f), material); // 보호대 아랫부분
        Box(parent, "TriggerGuardFront", center + new Vector3(0f, -0.025f, 0.06f), new Vector3(0.045f, 0.09f, 0.017f), material); // 보호대 앞부분
        Box(parent, "Trigger", center + new Vector3(0f, -0.02f, -0.02f), new Vector3(0.015f, 0.045f, 0.018f), material); // 방아쇠 모형
    }

    private static void Finish(GameObject part, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool solid) // 부품 공통 설정
    {
        part.name = name; // 부품 이름 적용
        part.transform.SetParent(parent, false); // 부품 연결
        part.transform.localPosition = position; // 위치 적용
        part.transform.localScale = scale; // 크기 적용
        part.GetComponent<Renderer>().sharedMaterial = material; // 공용 재질 연결
        if (!solid) // 장식과 무기의 이동 충돌 제외
        {
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>()); // 불필요한 충돌체 제거
        }
    }
}
#endif // 런타임 모형 생성 도구 제외
