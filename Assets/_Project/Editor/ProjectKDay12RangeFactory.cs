#if UNITY_EDITOR // 대표 총기 비교장 생성 전용
using System; // 필수 구역 누락 처리
using UnityEditor; // 생성 객체 저장 지원
using UnityEngine; // 비교장 모형과 기록 장치
using UnityEngine.SceneManagement; // 지정 씬만 편집

public static class ProjectKDay12RangeFactory // 기존 11일차 사격장 옆 비교 구간
{
    public static void Build(Scene scene, FirearmDefinition[] guns) // 기존 구조를 지우지 않는 옆 확장
    {
        if (ProjectKDay9Setup.FindNamed(scene, "Day12_FirearmComparison") != null) // 이전 적용 여부 확인
        {
            return; // 사용자가 수정한 비교장 보존
        }

        GameObject previous = ProjectKDay9Setup.FindNamed(scene, "Day11_ShootingRange"); // 이전 사격장 기준
        if (previous == null) // 연결 통로 기준 존재 확인
        {
            throw new InvalidOperationException("Day12: Day11_ShootingRange가 필요합니다."); // 임의 위치 생성 금지
        }

        Transform root = ProjectKDay12ModelFactory.Node(previous.transform.parent, "Day12_FirearmComparison", Vector3.zero); // 전투 구역의 새 비교장
        root.position = previous.transform.TransformPoint(new Vector3(20f, 0f, 0f)); // 기존 사격장 오른쪽 배치
        root.rotation = previous.transform.rotation; // 기존 레인 방향 유지
        Material floor = ProjectKDay12ModelFactory.Material("RangeFloor", new Color(0.085f, 0.12f, 0.16f)); // 비교장 바닥
        Material metal = ProjectKDay12ModelFactory.Material("RangeMetal", new Color(0.24f, 0.31f, 0.38f)); // 표적과 전시 프레임
        Material light = ProjectKDay12ModelFactory.Material("RangePaper", new Color(0.86f, 0.89f, 0.88f)); // 탄착 확인 바탕
        Material orange = ProjectKDay12ModelFactory.Material("RangeAccent", new Color(1f, 0.56f, 0.12f)); // 몸통 표적 강조
        Material cyan = ProjectKDay12ModelFactory.Material("RangeHead", new Color(0.08f, 0.82f, 0.95f)); // 머리 표적 강조
        Material marks = ProjectKDay12ModelFactory.Material("HitMarks", Color.white); // 기존 탄착 기능용 재질
        Box(root, "Floor", new Vector3(0f, -0.1f, 36f), new Vector3(12f, 0.2f, 78f), floor, true); // 70m까지 이어진 바닥
        Box(root, "Bridge", new Vector3(-9.5f, -0.1f, 0f), new Vector3(7f, 0.2f, 3f), floor, true); // 기존 사격장 오른쪽 가장자리 연결
        Box(root, "Backstop", new Vector3(0f, 2.2f, 74.5f), new Vector3(12f, 4.4f, 0.3f), metal, true); // 사격장 끝 정지벽
        Label(root, "DAY 12 / THREE FIREARMS", new Vector3(0f, 3.5f, -1.7f), 0.045f); // 입구 식별 표식
        FirearmDamageProbe[] probes = new FirearmDamageProbe[2]; // 부위와 방어율 비교 장치
        for (int i = 0; i < 2; i++) // 무방어와 테스트 장갑 표적
        {
            Transform target = ProjectKDay12ModelFactory.Node(root, "DamageProbe_" + i, new Vector3(i == 0 ? -2f : 2f, 0f, 7f)); // 짧은 비교 거리 배치
            Box(target, "BodyModel", new Vector3(0f, 1.0f, 0f), new Vector3(0.8f, 1.35f, 0.30f), orange); // 몸통 모형
            Box(target, "HeadModel", new Vector3(0f, 1.9f, 0f), new Vector3(0.5f, 0.4f, 0.34f), cyan); // 머리 모형
            Box(target, "Stand", new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.3f, 0.6f), metal); // 모형 받침
            TextMesh label = Label(target, "HEAD / BODY", new Vector3(0f, 2.65f, -0.3f), 0.018f); // 단일 결과판
            probes[i] = target.gameObject.AddComponent<FirearmDamageProbe>(); // 적 AI 없는 기록 장치
            probes[i].Configure(i == 0 ? 0f : 0.5f, label); // 실제 적 수치와 구분한 테스트 방어율
            ProjectKDay12HitboxSetup.Zone(target, "BodyZone", new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1.35f, 0.32f), FirearmHitRegion.Body, null, probes[i]); // 몸통 피해 부위
            ProjectKDay12HitboxSetup.Zone(target, "HeadZone", new Vector3(0f, 1.9f, 0f), new Vector3(0.5f, 0.4f, 0.36f), FirearmHitRegion.Head, null, probes[i]); // 머리 피해 부위
        }

        FirearmPracticeTarget[] targets = new FirearmPracticeTarget[2]; // 소총 거리 비교 표적
        float[] distances = new float[] // 청룡선 유효 거리 안팎 비교
        {
            40f, // 검사 기준값
            70f // 검사 기준값
        };
        for (int i = 0; i < targets.Length; i++) // 거리별 탄착 표적
        {
            float x = i == 0 ? -4.5f : 4.5f; // 비교용 분리 레인
            Transform target = ProjectKDay12ModelFactory.Node(root, "Target_" + distances[i] + "m", new Vector3(x, 0f, distances[i])); // 원거리 표적 기준
            Box(target, "Board", new Vector3(0f, 1.9f, 0f), new Vector3(2.3f, 2.4f, 0.12f), light); // 넓은 표적 판
            Box(target, "Center", new Vector3(0f, 1.9f, -0.069f), new Vector3(0.38f, 0.38f, 0.01f), orange); // 적중 중심 표시
            BoxCollider collider = target.gameObject.AddComponent<BoxCollider>(); // 실제 충돌 판
            collider.center = new Vector3(0f, 1.9f, -0.09f); // 장식 앞 피격 면
            collider.size = new Vector3(2.3f, 2.4f, 0.025f); // 표적 면적
            targets[i] = target.gameObject.AddComponent<FirearmPracticeTarget>(); // 기존 탄착 기록 사용
            targets[i].Configure(marks); // 공용 재질 연결
            Label(target, distances[i] + " m", new Vector3(0f, 3.35f, -0.15f), 0.055f); // 레인 거리 안내
            Box(root, "FirePad_" + i, new Vector3(x, 0.02f, 0f), new Vector3(1.1f, 0.012f, 1.1f), cyan); // 해당 표적의 발사 기준 위치
        }

        Transform station = ProjectKDay12ModelFactory.Node(root, "CatalogStation", new Vector3(-4.4f, 0f, -1.6f)); // 출입구 보급 단말기
        Box(station, "Console", new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 1.4f, 0.7f), metal, true); // F 상호작용 충돌체
        station.gameObject.AddComponent<FirearmCatalogStation>().Configure(probes, targets); // 새 구간 기록과 보급 연결
        Label(station, "F : AMMO / RESET", new Vector3(0f, 1.65f, -0.37f), 0.027f); // 단말기 사용 안내
        for (int i = 0; i < guns.Length && i < 3; i++) // 대표 총기만 진열
        {
            Transform display = ProjectKDay12ModelFactory.Node(root, "Display_" + ProjectKDay12Catalog.Ids[i], new Vector3(-1.7f + i * 1.8f, 0f, -1.9f)); // 전시 위치
            Box(display, "Pedestal", new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 1.1f, 0.7f), metal, true); // 진열대 받침
            GameObject sample = UnityEngine.Object.Instantiate(guns[i].ModelPrefab, display); // 장착 프리팹 진열 복제
            sample.name = "DisplayOnly"; // 실제 장착 객체와 구분
            sample.transform.localPosition = new Vector3(0f, 1.6f, 0f); // 받침 위 진열
            sample.transform.localRotation = Quaternion.Euler(0f, 70f, 0f); // 측면 외형 확인 방향
            sample.transform.localScale = Vector3.one * 1.2f; // 모형 읽기 쉬운 크기
            UnityEngine.Object.DestroyImmediate(sample.GetComponent<FirearmView>()); // 진열품의 런타임 발사와 소리 제거
            Label(display, (5 + i) + " : " + ProjectKDay12Catalog.Ids[i], new Vector3(0f, 0.95f, -0.37f), 0.023f); // 장비 번호와 식별자 안내
        }
    }

    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = false) // 공통 모형 생성 전달
    {
        return ProjectKDay12ModelFactory.Box(parent, name, position, size, material, solid); // 단일 부품 생성 기능 재사용
    }

    private static TextMesh Label(Transform parent, string text, Vector3 position, float size) // 별도 Canvas 없는 결과 글자
    {
        Transform node = ProjectKDay12ModelFactory.Node(parent, "Label", position); // 글자 위치 기준
        node.localRotation = Quaternion.identity; // 앞쪽 카메라에서 읽을 수 있는 글자 방향
        TextMesh label = node.gameObject.AddComponent<TextMesh>(); // 간단한 월드 글자
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 현재 프로젝트의 유효 내장 글꼴
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material; // 글꼴 재질 연결
        label.fontSize = 48; // 글자 해상도
        label.characterSize = size; // 월드 표시 크기
        label.anchor = TextAnchor.MiddleCenter; // 중앙 기준
        label.alignment = TextAlignment.Center; // 여러 줄 중앙 정렬
        label.color = Color.white; // 어두운 배경 대비
        label.text = text; // 표시 문자열
        return label; // 기록 갱신용 참조 반환
    }
}
#endif // 게임 빌드에서 비교장 생성 도구 제외
