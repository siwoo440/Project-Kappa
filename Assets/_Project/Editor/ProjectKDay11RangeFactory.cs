#if UNITY_EDITOR // 편집기 전용 모형 생성
using System; // 생성 오류 전달
using UnityEditor; // 프리팹과 재질 저장
using UnityEditor.SceneManagement; // 임시 제작 씬 관리
using UnityEngine; // 훈련장 부품 구성
using UnityEngine.SceneManagement; // 대상 씬 지정

public static class ProjectKDay11RangeFactory // 기존 훈련장을 보존한 사격장과 소음기 모형
{
    private const string Folder = "Assets/_Project/Materials/Generated/Day11"; // 이번 일차 재질 폴더

    public static GameObject SuppressorPrefab(FirearmDefinition definition) // 기존 권총을 복제해 소음기 추가
    {
        GameObject source = definition.ModelPrefab; // 기존 사용자 모형 참조
        FirearmView sourceView = source.GetComponent<FirearmView>(); // 기존 총구 참조
        if (sourceView != null && sourceView.HasSuppressor) // 이미 완성된 소음기 모형 확인
        {
            return source; // 반복 실행 시 수동 수정 유지
        }

        string path = "Assets/_Project/Prefabs/Day11/" + definition.name + "_Suppressor.prefab"; // 별도 저장 프리팹 경로
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 이전 설정 결과 조회
        if (existing != null) // 기존 생성 모형 확인
        {
            return existing; // 임의로 프리팹을 덮어쓰지 않음
        }

        ProjectKDay10ModelFactory.EnsureFolder("Assets/_Project/Prefabs/Day11"); // 새 모형 폴더 확보
        Scene preview = EditorSceneManager.NewPreviewScene(); // 실제 맵과 분리된 제작 공간
        try // 제작 후 임시 객체 정리 보장
        {
            GameObject model = UnityEngine.Object.Instantiate(source); // 원본 프리팹은 유지한 복제
            SceneManager.MoveGameObjectToScene(model, preview); // 임시 제작 씬으로 이동
            model.name = "D11_" + source.name; // 파생 모델 식별
            FirearmView view = model.GetComponent<FirearmView>(); // 복제본 총구 참조
            if (view == null || view.Muzzle == null) // 실제 총구 필수 검사
            {
                throw new InvalidOperationException("Day11: 기존 권총의 FirearmView와 Muzzle을 확인하세요."); // 미완성 모형 차단
            }

            Transform suppressor = Child(view.Muzzle, "Suppressor_Day11", Vector3.zero); // 기존 총구 앞의 소음기 기준
            Material dark = Material("SuppressorDark", new Color(0.07f, 0.09f, 0.12f)); // 소음기 무광 외형
            Material metal = Material("SuppressorMetal", new Color(0.32f, 0.42f, 0.48f)); // 조임 링 외형
            Part(suppressor, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.14f), new Vector3(0.14f, 0.14f, 0.14f), dark, new Vector3(90f, 0f, 0f)); // 소음기 원통 외형
            Part(suppressor, "MountRing", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.012f), new Vector3(0.17f, 0.016f, 0.17f), metal, new Vector3(90f, 0f, 0f)); // 총열 연결 링
            Part(suppressor, "EndRing", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.28f), new Vector3(0.15f, 0.015f, 0.15f), metal, new Vector3(90f, 0f, 0f)); // 끝단 보호 링
            for (int i = 0; i < 3; i++) // 외형 구분용 방열 링 생성
            {
                Part(suppressor, "Fin_" + i, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.07f + i * 0.055f), new Vector3(0.155f, 0.008f, 0.155f), metal, new Vector3(90f, 0f, 0f)); // 얇은 외곽 링
            }

            Transform endpoint = Child(suppressor, "SuppressedMuzzle", new Vector3(0f, 0f, 0.32f)); // 늘어난 총열 끝 실제 판정점
            view.ConfigureSuppressor(suppressor.gameObject, endpoint); // 소음기 외형과 총구 연결
            suppressor.gameObject.SetActive(false); // 최초 장착은 소음기 해제 상태
            GameObject result = PrefabUtility.SaveAsPrefabAsset(model, path, out bool saved); // 원본과 다른 경로에 저장
            if (!saved || result == null) // 프리팹 저장 확인
            {
                throw new InvalidOperationException("Day11: 소음기 프리팹 저장 실패"); // 실패를 완료로 기록하지 않음
            }

            return result; // 완성한 파생 프리팹 반환
        }
        finally // 실제 씬에 임시 모형을 남기지 않음
        {
            EditorSceneManager.ClosePreviewScene(preview); // 임시 씬과 모형 정리
        }
    }

    public static void BuildRange(Scene scene, Transform player) // 기존 전투 구역 오른쪽의 사격장 추가
    {
        if (ProjectKDay9Setup.FindNamed(scene, "Day11_ShootingRange") != null) // 이미 배치된 사격장 확인
        {
            return; // 기존 위치와 사용자의 모델링 유지
        }

        GameObject combat = ProjectKDay9Setup.FindNamed(scene, "CombatPreviewZone"); // 기존 전투 구역 조회
        if (combat == null) // 기준 구역 필수 확인
        {
            throw new InvalidOperationException("Day11: CombatPreviewZone이 없습니다."); // 임의 위치 배치 중단
        }

        Transform root = Child(combat.transform, "Day11_ShootingRange", new Vector3(20f, 0.15f, 0f)); // 옆 확장 구간 배치
        Transform combatFloor = combat.transform.Find("CombatFloor"); // 실제 전투 구역 바닥 조회
        Renderer floorRenderer = combatFloor != null ? combatFloor.GetComponent<Renderer>() : null; // 실제 바닥 높이 조회
        if (floorRenderer != null) // 기존 바닥 높이 사용 가능 확인
        {
            Vector3 position = root.position; // 새 구간 기준 위치
            position.y = floorRenderer.bounds.max.y; // 기존 바닥 상면과 높이 일치
            root.position = position; // 접합 높이 적용
        }

        Material floor = Material("RangeFloor", new Color(0.11f, 0.15f, 0.19f)); // 사격장 바닥 재질
        Material rim = Material("RangeFrame", new Color(0.22f, 0.32f, 0.4f)); // 표적과 안내판 프레임
        Material paper = Material("TargetPaper", new Color(0.86f, 0.9f, 0.9f)); // 탄착을 읽기 쉬운 표적 바탕
        Material dark = Material("TargetRing", new Color(0.045f, 0.10f, 0.13f)); // 표적 구분 링
        Material center = Material("TargetCenter", new Color(1f, 0.60f, 0.13f)); // 중심 표적 색상
        Material marks = Material("HitMarks", Color.white); // 개별 탄착 색상용 공용 재질
        Solid(root, "RangeFloor", new Vector3(0f, -0.1f, 12.5f), new Vector3(14f, 0.2f, 31f), floor); // 별도 충돌 바닥 생성
        Solid(root, "AccessBridge", new Vector3(-11f, -0.1f, 0f), new Vector3(8f, 0.2f, 3f), floor); // 기존 전투 구역과 연결
        Solid(root, "Backstop", new Vector3(0f, 1.9f, 27.5f), new Vector3(14f, 3.8f, 0.3f), rim); // 사격장 끝 탄도 차단벽
        Label(root, "DAY 11 / RECOIL & SPREAD", new Vector3(0f, 3.4f, 27.25f), 0.06f); // 멀리서 보이는 구간 표식

        float[] lanes = new float[] // 좌우 표적 간격
        {
            -4f, // 가까운 표적 구간
            0f, // 중간 표적 구간
            4f // 먼 표적 구간
        };
        float[] distances = new float[] // 기준 패드부터 표적 중심까지 거리
        {
            5f, // 근거리 검사
            15f, // 중거리 검사
            25f // 원거리 검사
        };
        FirearmPracticeTarget[] targets = new FirearmPracticeTarget[3]; // 사격대 관리 표적 목록
        for (int i = 0; i < distances.Length; i++) // 거리별 표적 생성
        {
            Transform target = Child(root, "Target_" + distances[i] + "m", new Vector3(lanes[i], 0f, distances[i])); // 각 레인 표적 기준
            Part(target, "Board", PrimitiveType.Cube, new Vector3(0f, 1.8f, 0f), new Vector3(2.2f, 2.2f, 0.12f), paper, Vector3.zero); // 넓은 탄착 확인판
            Part(target, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.07f), new Vector3(0.12f, 0.9f, 0.16f), rim, Vector3.zero); // 표적 지지대
            Part(target, "RingOuter", PrimitiveType.Cylinder, new Vector3(0f, 1.8f, -0.08f), new Vector3(1.45f, 0.004f, 1.45f), dark, new Vector3(90f, 0f, 0f)); // 외곽 조준 원
            Part(target, "RingInner", PrimitiveType.Cylinder, new Vector3(0f, 1.8f, -0.09f), new Vector3(1.05f, 0.004f, 1.05f), paper, new Vector3(90f, 0f, 0f)); // 표적 내부 흰색 원
            Part(target, "Bullseye", PrimitiveType.Cylinder, new Vector3(0f, 1.8f, -0.10f), new Vector3(0.32f, 0.004f, 0.32f), center, new Vector3(90f, 0f, 0f)); // 중앙 적중 기준
            BoxCollider collider = target.gameObject.AddComponent<BoxCollider>(); // 장식 앞쪽의 실제 표적 판정
            collider.center = new Vector3(0f, 1.8f, -0.14f); // 모든 장식보다 앞쪽에 탄착 배치
            collider.size = new Vector3(2.2f, 2.2f, 0.025f); // 표적 판정 면적
            targets[i] = target.gameObject.AddComponent<FirearmPracticeTarget>(); // 탄착 기록 연결
            targets[i].Configure(marks); // 공용 탄착 재질 설정
            Label(target, distances[i] + " m", new Vector3(0f, 3.2f, -0.16f), 0.06f); // 실제 레인 거리 안내
            Part(root, "ShootPad_" + i, PrimitiveType.Cube, new Vector3(lanes[i], 0.018f, 0f), new Vector3(1.4f, 0.012f, 1.2f), center, Vector3.zero); // 겹침 없는 발사 기준 패드
        }

        FirearmHearingProbe[] probes = new FirearmHearingProbe[2]; // 비교용 청취 센서
        probes[0] = BuildProbe(root, player, "NEAR", new Vector3(-2f, 0f, -1f), rim); // 중앙 패드 근처 청각 센서
        probes[1] = BuildProbe(root, player, "FAR", new Vector3(6f, 0f, 6f), rim); // 소음기 반경 밖 비교 센서
        Transform station = Child(root, "Day11_PracticeStation", new Vector3(-5.5f, 0f, -1.8f)); // 출입구 보급 단말기
        Solid(station, "Console", new Vector3(0f, 0.7f, 0f), new Vector3(1.05f, 1.4f, 0.6f), rim); // F 상호작용 충돌체
        station.gameObject.AddComponent<FirearmPracticeStation>().Configure(targets, probes); // 보급과 초기화 대상 연결
        Label(station, "F : RESET + AMMO", new Vector3(0f, 1.55f, -0.32f), 0.025f); // 사격대 사용 안내
        Label(root, "B : SUPPRESSOR   T : RELOAD", new Vector3(0f, 2.7f, -2f), 0.035f); // 기존 키를 유지한 시험 조작
    }

    private static FirearmHearingProbe BuildProbe(Transform parent, Transform player, string name, Vector3 position, Material material) // 실제 청각 센서 비교 장치
    {
        Transform root = Child(parent, "Hearing_" + name, position); // 장치 루트 생성
        Part(root, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), material, Vector3.zero); // 표시등 기둥
        GameObject lamp = Part(root, "Lamp", PrimitiveType.Sphere, new Vector3(0f, 1.2f, 0f), Vector3.one * 0.28f, material, Vector3.zero); // 청취 직후 표시등
        DetectionSensor sensor = root.gameObject.AddComponent<DetectionSensor>(); // 기존 게임 센서 그대로 사용
        sensor.Configure(player, 0.1f, 30f, 10f, ~0); // 시야를 제한하고 청각만 비교
        TextMesh label = Label(root, "HEARD 0", new Vector3(0f, 1.62f, 0f), 0.028f); // 총성 청취 수 표시
        Label(root, name, new Vector3(0f, 1.95f, 0f), 0.035f); // 근거리 원거리 센서 구분
        FirearmHearingProbe probe = root.gameObject.AddComponent<FirearmHearingProbe>(); // HP UI 없는 전용 표시 연결
        probe.Configure(sensor, lamp.GetComponent<Renderer>(), label); // 센서와 표시등 연결
        return probe; // 초기화 대상 등록
    }

    private static Transform Child(Transform parent, string name, Vector3 position) // 소유 범위가 명확한 새 기준 생성
    {
        Transform result = new GameObject(name).transform; // 기준 객체 생성
        result.SetParent(parent, false); // 지정한 구간 하위 연결
        result.localPosition = position; // 구간 기준 위치 지정
        return result; // 생성 결과 반환
    }

    private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, Vector3 rotation) // 충돌 없는 장식 부품
    {
        return ProjectKDay10ModelFactory.Part(parent, name, shape, position, scale, material, rotation); // 검증된 부품 생성기 재사용
    }

    private static void Solid(Transform parent, string name, Vector3 position, Vector3 scale, Material material) // 실제 바닥과 엄폐물 생성
    {
        GameObject part = Part(parent, name, PrimitiveType.Cube, position, scale, material, Vector3.zero); // 표시 부품 생성
        part.AddComponent<BoxCollider>(); // 해당 부품의 실제 충돌만 추가
    }

    private static TextMesh Label(Transform parent, string text, Vector3 position, float size) // 고정 훈련장 안내 글자
    {
        Transform child = Child(parent, "Label_" + text.Replace(' ', '_'), position); // 안내 객체 생성
        child.localRotation = Quaternion.Euler(0f, 180f, 0f); // 입구 쪽에서 읽는 글자 방향
        TextMesh label = child.gameObject.AddComponent<TextMesh>(); // 단일 월드 글자 생성
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 현재 Unity 내장 글꼴 사용
        child.GetComponent<MeshRenderer>().sharedMaterial = label.font.material; // 글꼴 아틀라스 연결
        label.text = text; // 안내 문구 지정
        label.fontSize = 40; // 글꼴 선명도 지정
        label.characterSize = size; // 실제 월드 크기 지정
        label.anchor = TextAnchor.MiddleCenter; // 중앙 기준 위치
        label.alignment = TextAlignment.Center; // 중앙 정렬
        label.color = Color.white; // 안내 글자 대비
        return label; // 동적 청취 문구용 참조 반환
    }

    private static Material Material(string name, Color color) // 전용 폴더의 반복 가능한 재질 생성
    {
        ProjectKDay10ModelFactory.EnsureFolder(Folder); // 이번 일차 재질 폴더 확보
        string path = Folder + "/" + name + ".mat"; // 재질 경로
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); // 사용자 조정 재질 조회
        if (existing != null) // 이미 만들어진 재질 확인
        {
            return existing; // 기존 색상 보존
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 현재 URP 셰이더 확인
        shader = shader != null ? shader : Shader.Find("Standard"); // 기본 파이프라인 대안
        if (shader == null) // 지원 셰이더 확인
        {
            throw new InvalidOperationException("Day11: Lit 셰이더가 없습니다."); // 잘못된 재질 생성 중단
        }

        Material result = new Material(shader); // 새 재질 생성
        result.name = name; // 재질 식별자
        if (result.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            result.SetColor("_BaseColor", color); // URP 바탕색 적용
        }

        if (result.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            result.SetColor("_Color", color); // 기본 바탕색 적용
        }

        AssetDatabase.CreateAsset(result, path); // 실제 에셋으로 저장
        return result; // 재사용할 재질 반환
    }
}
#endif // 에디터 외 빌드에서 제외
