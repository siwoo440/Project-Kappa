#if UNITY_EDITOR // 17일차 도시 디테일 배치 전용
using System; // 안정적인 해시와 오류 처리
using System.Collections.Generic; // 구역별 묶음 목록
using ProjectK.Day16; // 기존 Map 월드 참조
using ProjectK.Day17; // 디테일 런타임 컴포넌트 참조
using UnityEditor; // 편집기 저장과 오브젝트 표시
using UnityEngine; // 도시 배치와 조명 설정
using UnityEngine.Rendering; // 환경광 모드 설정

public static class MapCyberpunkDetailBuilder // 기존 Map 위에 네온·기계 세부 요소를 추가하는 편집기 생성기
{
    public const string SourceCommit = "00f150414912873a5d7c4e7df3eef21f409cf205"; // 16일차 Map 기준 커밋

    public static MapCyberpunkDetailMarker Build(MapWorldRoot world) // 전체 사이버펑크 디테일 레이어 생성
    {
        if (world == null) // 본편 월드 참조 확인
        {
            throw new ArgumentNullException(nameof(world)); // 잘못된 생성 호출 보고
        }
        Transform existing = Find(world.transform, MapCyberpunkDetailMarker.RootName); // 기존 디테일 설치 여부 확인
        if (existing != null) // 반복 설치 감지
        {
            MapCyberpunkDetailMarker old = existing.GetComponent<MapCyberpunkDetailMarker>(); // 기존 완료 표식 조회
            if (old != null && old.Completed) // 정상 완료 설치 확인
            {
                return old; // 수동 편집을 덮지 않고 기존 결과 반환
            }
            throw new InvalidOperationException("완료되지 않은 Day17 디테일 루트가 있습니다. 씬 백업을 확인한 뒤 정리하세요."); // 부분 설치 위 중복 생성 방지
        }
        GameObject rootObject = new GameObject(MapCyberpunkDetailMarker.RootName); // 단일 삭제가 가능한 디테일 루트 생성
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Day17 Cyberpunk City Detail"); // 한 번의 실행 취소 연결
        rootObject.transform.SetParent(world.transform, false); // 기존 Map 월드에만 연결
        MapCyberpunkDetailMarker marker = rootObject.AddComponent<MapCyberpunkDetailMarker>(); // 완료와 집계 표식 추가
        MapCyberpunkGeometry g = new MapCyberpunkGeometry(rootObject.transform, world); // 공통 모형 도구 준비
        ApplyAtmosphere(world); // 네온이 읽히는 저녁 도시 환경 적용
        BuildGenericBuildings(world, g); // 일반 건물의 옥상과 외벽 세부 요소 추가
        BuildStreetFurniture(world, g); // 도로와 골목의 생활 소품 추가
        BuildLin(world, g); // 린의 거점 네온 정비소 보강
        BuildGyeopgil(world, g); // 겹길 시장의 밀집 네온 보강
        BuildJeoryu(world, g); // 저류 산업 배관과 경고 조명 보강
        BuildYurigwan(world, g); // 유리관 기업 외벽과 수직 조명 보강
        BuildCheomtap(world, g); // 첨탑 랜드마크 수직 네온 보강
        marker.Configure(world, SourceCommit, g.SignCount, g.PropCount, g.LightCount, g.ClusterCount); // 생성 결과를 씬에 기록
        EditorUtility.SetDirty(marker); // 변경 저장 대상으로 표시
        return marker; // 설정 메뉴와 검증에서 사용할 결과 반환
    }

    private static void ApplyAtmosphere(MapWorldRoot world) // 밝기와 안개만 조절하는 저녁 사이버펑크 환경
    {
        RenderSettings.fog = true; // 먼 도시 실루엣을 위한 안개 활성화
        RenderSettings.fogMode = FogMode.Linear; // 거리 기반 단순 안개 사용
        RenderSettings.fogColor = new Color(0.018f, 0.030f, 0.060f); // 짙은 청색 도시 안개 적용
        RenderSettings.fogStartDistance = Mathf.Max(240f, world.TileSize * 0.65f); // 가까운 플레이 공간 가독성 보존
        RenderSettings.fogEndDistance = Mathf.Max(900f, world.WorldSize * 0.82f); // 먼 타일과 첨탑 실루엣 유지
        RenderSettings.ambientMode = AmbientMode.Flat; // 예측 가능한 저녁 환경광 사용
        RenderSettings.ambientLight = new Color(0.075f, 0.095f, 0.15f); // 완전한 암흑을 피한 청색 환경광 적용
        if (world.PlayCamera != null) // 본편 플레이 카메라 확인
        {
            world.PlayCamera.clearFlags = CameraClearFlags.SolidColor; // 별도 스카이박스 없이 저녁 하늘 색 사용
            world.PlayCamera.backgroundColor = new Color(0.012f, 0.020f, 0.050f); // 짙은 남청색 야간 하늘 적용
            world.PlayCamera.farClipPlane = Mathf.Max(world.PlayCamera.farClipPlane, world.WorldSize * 1.25f); // 전체 3x3 월드 랜드마크 가시거리 확보
            EditorUtility.SetDirty(world.PlayCamera); // 씬 저장 대상으로 표시
        }
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // 현재 씬 광원 조회
        {
            if (light.gameObject.scene != world.gameObject.scene || light.type != LightType.Directional) // 다른 씬과 점광원 제외
            {
                continue; // 다음 광원 확인
            }
            light.color = new Color(0.55f, 0.67f, 0.86f); // 푸른 저녁 주광색 적용
            light.intensity = 0.38f; // 네온이 읽히는 낮은 주광 밝기 적용
            light.transform.rotation = Quaternion.Euler(32f, -28f, 0f); // 긴 그림자 방향 지정
            EditorUtility.SetDirty(light); // 씬 저장 대상으로 표시
        }
    }

    public static void BuildGenericBuildings(MapWorldRoot world, MapCyberpunkGeometry g) // 반복 건물에 선택적으로 옥상과 외벽 디테일 추가
    {
        Transform[] quadrant = new Transform[4]; // 네 사분면 표시 묶음 준비
        quadrant[0] = g.Cluster("Generic_SW", new Vector3(-world.TileSize * 0.35f, 20f, -world.TileSize * 0.35f), world.TileSize * 1.15f); // 남서쪽 장식 묶음
        quadrant[1] = g.Cluster("Generic_SE", new Vector3(world.TileSize * 0.35f, 20f, -world.TileSize * 0.35f), world.TileSize * 1.15f); // 남동쪽 장식 묶음
        quadrant[2] = g.Cluster("Generic_NW", new Vector3(-world.TileSize * 0.35f, 20f, world.TileSize * 0.35f), world.TileSize * 1.15f); // 북서쪽 장식 묶음
        quadrant[3] = g.Cluster("Generic_NE", new Vector3(world.TileSize * 0.35f, 20f, world.TileSize * 0.35f), world.TileSize * 1.15f); // 북동쪽 장식 묶음
        foreach (BoxCollider collider in world.GetComponentsInChildren<BoxCollider>(true)) // 기존 일반 건물 충돌체 탐색
        {
            if (collider.name != "MainCollision") // 건물 본체가 아닌 상자 제외
            {
                continue; // 다음 충돌체 확인
            }
            Bounds bounds = collider.bounds; // 실제 건물 월드 범위 조회
            int hash = StableHash(collider.transform.parent != null ? collider.transform.parent.name + collider.transform.position : collider.name); // 재현 가능한 선택값 계산
            int index = (bounds.center.x >= 0f ? 1 : 0) + (bounds.center.z >= 0f ? 2 : 0); // 건물 위치에 맞춘 사분면 선택
            Transform content = quadrant[index]; // 가까운 장식 묶음 부모 선택
            Vector3 basePoint = bounds.center - content.position; // 묶음 기준 로컬 위치 계산
            float top = bounds.max.y - content.position.y; // 건물 지붕 로컬 높이 계산
            if (hash % 4 == 0) // 일부 건물의 실외기 배치
            {
                g.AirUnit(content, new Vector3(basePoint.x - bounds.extents.x * 0.24f, top, basePoint.z + bounds.extents.z * 0.18f), Mathf.Clamp(bounds.size.x / 28f, 0.75f, 1.25f)); // 지붕 서비스 장비 추가
            }
            if (hash % 8 == 0) // 더 적은 건물의 안테나 배치
            {
                g.Antenna(content, new Vector3(basePoint.x + bounds.extents.x * 0.22f, top, basePoint.z), 4.5f + hash % 4, hash % 16 == 0 ? "Magenta" : "Cyan"); // 옥상 통신 실루엣 추가
            }
            if (hash % 7 == 0 && bounds.size.y > 9f) // 중층 이상 건물의 수직 네온 표시
            {
                float front = bounds.min.z - content.position.z - 0.10f; // 정면 외벽 위치 계산
                string neon = hash % 14 == 0 ? "Magenta" : "Cyan"; // 청록과 자홍 네온 분산
                g.NeonStrip(content, "FacadeNeon", new Vector3(basePoint.x + bounds.extents.x * 0.34f, basePoint.y, front), new Vector3(0.11f, bounds.size.y * 0.72f, 0.06f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.75f, hash * 0.01f); // 수직 외벽 네온 띠 추가
            }
            if (hash % 11 == 0 && bounds.size.x > 12f) // 일부 건물에 입체 광고판 배치
            {
                float front = bounds.min.z - content.position.z - 0.24f; // 정면 광고판 위치 계산
                string neon = hash % 22 == 0 ? "Violet" : "Amber"; // 광고 색상 분산
                g.NeonSign(content, "BlockNeonSign", new Vector3(basePoint.x, Mathf.Min(top - 2f, basePoint.y + bounds.extents.y * 0.4f), front), new Vector3(Mathf.Min(7f, bounds.size.x * 0.45f), 2.1f, 0f), hash % 2 == 0 ? "YEONMU//GRID" : "NIGHT//LINK", neon, MapCyberpunkGeometry.ColorFor(neon), Quaternion.identity); // 건물 정면 네온 광고 추가
            }
            if (hash % 13 == 0 && bounds.size.y > 8f) // 제한된 건물의 파쿠르용 정비 발판 배치
            {
                float front = bounds.min.z - content.position.z - 0.72f; // 외벽 앞 발판 위치 계산
                GameObject ledge = g.Box(content, "MaintenanceLedge", new Vector3(basePoint.x, Mathf.Min(top - 1.5f, basePoint.y - bounds.extents.y + 4.6f), front), new Vector3(Mathf.Min(5f, bounds.size.x * 0.35f), 0.28f, 1.1f), "Steel", true); // 실제 올라설 수 있는 정비 발판 추가
                g.NeonStrip(content, "LedgeGuide", ledge.transform.localPosition + new Vector3(0f, -0.12f, -0.58f), new Vector3(ledge.transform.localScale.x * 0.88f, 0.05f, 0.05f), "Green", MapCyberpunkGeometry.ColorFor("Green"), 1.1f, hash * 0.02f); // 이동 가능 발판 아래 녹색 안내선 추가
            }
        }
    }

    private static void BuildStreetFurniture(MapWorldRoot world, MapCyberpunkGeometry g) // 도로와 골목의 생활 디테일 추가
    {
        Transform content = g.Cluster("StreetLife", Vector3.zero, world.TileSize * 1.05f); // 중앙 도시 생활 소품 묶음 생성
        float span = world.WorldSize * 0.30f; // 주요 활동 구역 반경 설정
        for (int i = -5; i <= 5; i++) // 중앙 도로를 따라 반복 소품 배치
        {
            float axis = i * span / 5f; // 균일한 도로 위치 계산
            g.ServiceCabinet(content, new Vector3(axis, 16.15f - content.position.y, -22f), i % 2 == 0 ? "Cyan" : "Amber"); // 전력함과 상태등 추가
            g.Bollard(content, new Vector3(axis + 6f, 16.15f - content.position.y, 18f), i % 2 == 0 ? "Magenta" : "Cyan"); // 보행 경계 네온 봉 추가
            if (i % 2 == 0) // 절반 위치의 판매기와 수거함 배치
            {
                g.VendingMachine(content, new Vector3(axis - 4f, 16.15f - content.position.y, 31f), i % 4 == 0 ? "Magenta" : "Green"); // 골목 판매기 추가
                g.Dumpster(content, new Vector3(axis + 9f, 16.15f - content.position.y, -35f)); // 생활 수거함 추가
            }
        }
        for (int i = -4; i <= 4; i++) // 도로 위 공중 케이블 반복
        {
            Vector3 a = new Vector3(-span * 0.55f, 25f + (i & 1) * 3f, i * 58f); // 서쪽 전력 케이블 시작점
            Vector3 b = new Vector3(span * 0.55f, 24f + ((i + 1) & 1) * 3f, i * 58f); // 동쪽 전력 케이블 끝점
            g.Cable(content, "OverheadCable", a - content.position, b - content.position, 0.045f); // 거리감 있는 전력 케이블 추가
        }
    }

    private static void BuildLin(MapWorldRoot world, MapCyberpunkGeometry g) // 린의 옥상 작업실 디테일 보강
    {
        Transform site = Find(world.transform, "LIN_RooftopWorkshop"); // 기존 거점 랜드마크 조회
        if (site == null) // 거점 랜드마크 누락 확인
        {
            throw new InvalidOperationException("LIN_RooftopWorkshop을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = g.Cluster("LIN_Detail", world.transform.InverseTransformPoint(site.position), 360f); // 거점 전용 거리 묶음 생성
        g.NeonSign(content, "WorkshopNeon", new Vector3(-6f, 13.1f, -1.1f), new Vector3(7.5f, 1.7f, 0f), "LIN//ROOFTOP", "Cyan", MapCyberpunkGeometry.ColorFor("Cyan"), Quaternion.identity); // 거점 대표 간판 추가
        g.Antenna(content, new Vector3(8f, 8.1f, 6f), 8f, "Magenta"); // 거점 통신 안테나 추가
        g.AirUnit(content, new Vector3(6f, 8.1f, -5f), 0.95f); // 작업실 옥상 실외기 추가
        g.ServiceCabinet(content, new Vector3(-10f, 8.1f, -7f), "Green"); // 장비 전력함 추가
        g.VendingMachine(content, new Vector3(9f, 8.1f, 8f), "Cyan"); // 생활감을 위한 자동판매기 추가
        g.NeonStrip(content, "RoofGuideWest", new Vector3(-12.1f, 8.18f, 0f), new Vector3(0.08f, 0.06f, 19f), "Cyan", MapCyberpunkGeometry.ColorFor("Cyan"), 0.8f, 1f); // 옥상 가장자리 진행 네온 추가
        g.NeonStrip(content, "RoofGuideSouth", new Vector3(0f, 8.18f, -12.1f), new Vector3(20f, 0.06f, 0.08f), "Magenta", MapCyberpunkGeometry.ColorFor("Magenta"), 0.8f, 2f); // 옥상 남쪽 진행 네온 추가
        g.Cable(content, "WorkshopCableA", new Vector3(-8f, 14f, 3f), new Vector3(8f, 15.5f, 6f)); // 작업실 상부 전력 케이블 추가
        g.Cable(content, "WorkshopCableB", new Vector3(-8f, 13f, 4f), new Vector3(5f, 14.2f, -6f)); // 작업실 보조 케이블 추가
        g.PointLight(content, "LIN_CyanLight", new Vector3(-5f, 11f, -4f), MapCyberpunkGeometry.ColorFor("Cyan"), 13f, 3.2f); // 거점 청록 주변광 추가
        g.PointLight(content, "LIN_MagentaLight", new Vector3(7f, 10f, 5f), MapCyberpunkGeometry.ColorFor("Magenta"), 11f, 2.6f); // 거점 자홍 주변광 추가
    }

    private static void BuildGyeopgil(MapWorldRoot world, MapCyberpunkGeometry g) // 겹길 시장의 밀집 네온과 생활 소품 보강
    {
        Transform site = Find(world.transform, "GYEOPGIL_Market"); // 기존 시장 랜드마크 조회
        if (site == null) // 시장 랜드마크 누락 확인
        {
            throw new InvalidOperationException("GYEOPGIL_Market을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = g.Cluster("GYEOPGIL_Detail", world.transform.InverseTransformPoint(site.position), 430f); // 시장 전용 거리 묶음 생성
        string[] labels = new string[] { "NIGHT//MARKET", "RAMEN_24", "DATA//REPAIR", "BYTE//CLUB", "MED//PATCH", "SYNTH//BAR", "DELIVERY//NODE", "OPEN//LATE" }; // 시장 네온 간판 문구
        for (int i = 0; i < labels.Length; i++) // 옥상과 골목 간판 반복 배치
        {
            float side = i % 2 == 0 ? -1f : 1f; // 좌우 간판 분산
            float z = -10f + i * 3.1f; // 시장 통로를 따라 세로 분산
            string neon = i % 3 == 0 ? "Magenta" : i % 3 == 1 ? "Cyan" : "Amber"; // 세 가지 네온 색 순환
            g.NeonSign(content, "MarketSign_" + i, new Vector3(side * 13.4f, 10.2f + (i % 3) * 1.6f, z), new Vector3(5.4f, 1.35f, 0f), labels[i], neon, MapCyberpunkGeometry.ColorFor(neon), Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f)); // 건물 측면 돌출 간판 추가
        }
        for (int i = -2; i <= 2; i++) // 시장 차양과 판매기 배치
        {
            float x = i * 5.2f; // 가판대 가로 위치 계산
            string neon = i % 2 == 0 ? "Magenta" : "Cyan"; // 교차 네온 색상 선택
            g.Awning(content, new Vector3(x, 8.05f, 8.5f), new Vector3(4.4f, 0.18f, 2.6f), neon); // 옥상 시장 차양 추가
            if (i % 2 == 0) // 일부 차양의 판매기 배치
            {
                g.VendingMachine(content, new Vector3(x + 1.6f, 8.05f, -7f), neon); // 차양 반대편 판매기 추가
            }
        }
        for (int i = 0; i < 5; i++) // 시장 상부 현수 케이블과 조명 추가
        {
            float z = -8f + i * 4f; // 시장 케이블 위치 계산
            g.Cable(content, "MarketCable_" + i, new Vector3(-11f, 13f + (i % 2), z), new Vector3(11f, 12.5f + ((i + 1) % 2), z + 1.2f)); // 골목을 가로지르는 전력선 추가
            if (i < 4) // 제한된 시장 실제 광원 수 확인
            {
                g.PointLight(content, "MarketLight_" + i, new Vector3(-7f + i * 4.5f, 11.2f, z), i % 2 == 0 ? MapCyberpunkGeometry.ColorFor("Magenta") : MapCyberpunkGeometry.ColorFor("Cyan"), 10f, 2.2f); // 일부 간판 주변 실제 광원 추가
            }
        }
    }

    private static void BuildJeoryu(MapWorldRoot world, MapCyberpunkGeometry g) // 저류 산업지구의 배관과 위험 표시 보강
    {
        Transform site = Find(world.transform, "JEORYU_Industrial"); // 기존 산업 랜드마크 조회
        if (site == null) // 산업 랜드마크 누락 확인
        {
            throw new InvalidOperationException("JEORYU_Industrial을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = g.Cluster("JEORYU_Detail", world.transform.InverseTransformPoint(site.position), 480f); // 산업 구역 거리 묶음 생성
        for (int i = 0; i < 5; i++) // 대형 산업 배관 반복
        {
            float z = -15f + i * 7.5f; // 배관 세로 위치 계산
            g.Pipe(content, "ProcessPipe_" + i, new Vector3(-20f, 5f + i * 0.7f, z), new Vector3(18f, 5f + i * 0.7f, z), 0.65f + (i % 2) * 0.20f, i % 2 == 0 ? "Steel" : "DarkMetal", true); // 실제 엄폐와 파쿠르가 가능한 수평 배관 추가
            g.NeonStrip(content, "PipeHazard_" + i, new Vector3(-15f + i * 7f, 6.1f + i * 0.7f, z - 0.7f), new Vector3(3.2f, 0.10f, 0.08f), "Amber", MapCyberpunkGeometry.ColorFor("Amber"), 1.4f, i); // 배관 위험 표시 네온 추가
        }
        for (int i = 0; i < 4; i++) // 정비 발판과 전력함 추가
        {
            float x = -18f + i * 12f; // 정비 지점 가로 위치 계산
            g.Box(content, "ServiceDeck_" + i, new Vector3(x, 8.5f, 18f), new Vector3(7f, 0.32f, 3.2f), "Steel", true); // 상부 정비 발판 추가
            g.ServiceCabinet(content, new Vector3(x, 0.15f, -22f), i % 2 == 0 ? "Amber" : "Green"); // 산업 전력함 추가
        }
        g.NeonSign(content, "JeoryuHazardSign", new Vector3(0f, 9.5f, -28f), new Vector3(10f, 2f, 0f), "SUBLEVEL//PUMP", "Amber", MapCyberpunkGeometry.ColorFor("Amber"), Quaternion.identity); // 산업지구 대표 경고 간판 추가
        g.PointLight(content, "JeoryuAmberLightA", new Vector3(-16f, 7f, -8f), MapCyberpunkGeometry.ColorFor("Amber"), 14f, 2.7f); // 산업 황색 주변광 추가
        g.PointLight(content, "JeoryuAmberLightB", new Vector3(16f, 8f, 10f), MapCyberpunkGeometry.ColorFor("Amber"), 14f, 2.7f); // 반대편 산업 주변광 추가
    }

    private static void BuildYurigwan(MapWorldRoot world, MapCyberpunkGeometry g) // 유리관 기업지구의 수직 네온과 정돈된 구조 보강
    {
        Transform site = Find(world.transform, "YURIGWAN_Corporate"); // 기존 기업 랜드마크 조회
        if (site == null) // 기업 랜드마크 누락 확인
        {
            throw new InvalidOperationException("YURIGWAN_Corporate을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = g.Cluster("YURIGWAN_Detail", world.transform.InverseTransformPoint(site.position), 560f); // 기업 구역 거리 묶음 생성
        foreach (BoxCollider collider in site.GetComponentsInChildren<BoxCollider>(true)) // 기업 타워 본체 탐색
        {
            if (collider.name != "MainCollision") // 일반 외장 충돌체 제외
            {
                continue; // 다음 타워 확인
            }
            Bounds b = collider.bounds; // 타워 실제 범위 조회
            Vector3 center = b.center - content.position; // 묶음 기준 중심 위치 계산
            float front = b.min.z - content.position.z - 0.10f; // 정면 외벽 네온 위치 계산
            for (int side = -1; side <= 1; side += 2) // 양쪽 수직 네온 가장자리 생성
            {
                g.NeonStrip(content, "CorporateEdge", new Vector3(center.x + side * b.extents.x * 0.72f, center.y, front), new Vector3(0.14f, b.size.y * 0.82f, 0.07f), side < 0 ? "Cyan" : "Violet", side < 0 ? MapCyberpunkGeometry.ColorFor("Cyan") : MapCyberpunkGeometry.ColorFor("Violet"), 0.55f, b.center.y * 0.03f); // 기업 타워 수직 조명 추가
            }
        }
        g.NeonSign(content, "CorporateBoard", new Vector3(0f, 8f, -28f), new Vector3(13f, 2.5f, 0f), "GLASSWAY//CORP", "Cyan", MapCyberpunkGeometry.ColorFor("Cyan"), Quaternion.identity); // 기업지구 대표 광고판 추가
        for (int i = -2; i <= 2; i++) // 공개 광장 보안 파일런 추가
        {
            float x = i * 6f; // 파일런 가로 위치 계산
            g.Box(content, "SecurityPylon", new Vector3(x, 2.1f, -18f), new Vector3(0.85f, 4.2f, 0.85f), "DarkMetal", true); // 기업 광장 보안 기둥 추가
            g.NeonStrip(content, "PylonStatus", new Vector3(x, 2.5f, -18.44f), new Vector3(0.42f, 2.4f, 0.05f), i % 2 == 0 ? "Cyan" : "Violet", i % 2 == 0 ? MapCyberpunkGeometry.ColorFor("Cyan") : MapCyberpunkGeometry.ColorFor("Violet"), 0.7f, i); // 파일런 상태 네온 추가
        }
        g.PointLight(content, "CorporateCyanA", new Vector3(-12f, 7f, -13f), MapCyberpunkGeometry.ColorFor("Cyan"), 16f, 3.0f); // 기업 광장 청록 주변광 추가
        g.PointLight(content, "CorporateVioletB", new Vector3(12f, 8f, -8f), MapCyberpunkGeometry.ColorFor("Violet"), 16f, 2.8f); // 기업 광장 보라 주변광 추가
    }

    private static void BuildCheomtap(MapWorldRoot world, MapCyberpunkGeometry g) // 첨탑의 멀리서 읽히는 수직 네온 보강
    {
        Transform site = Find(world.transform, "CHEOMTAP_Spire"); // 기존 첨탑 랜드마크 조회
        if (site == null) // 첨탑 랜드마크 누락 확인
        {
            throw new InvalidOperationException("CHEOMTAP_Spire을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = g.Cluster("CHEOMTAP_Detail", world.transform.InverseTransformPoint(site.position), 720f); // 첨탑은 더 먼 거리에서도 표시
        foreach (BoxCollider collider in site.GetComponentsInChildren<BoxCollider>(true)) // 첨탑 세 단계 본체 탐색
        {
            if (!collider.name.StartsWith("Tower", StringComparison.Ordinal)) // 타워 외의 광장 충돌체 제외
            {
                continue; // 다음 충돌체 확인
            }
            Bounds b = collider.bounds; // 단계별 타워 범위 조회
            Vector3 center = b.center - content.position; // 묶음 기준 중심 위치 계산
            for (int side = -1; side <= 1; side += 2) // 동서 양쪽 수직 네온 추가
            {
                g.NeonStrip(content, "SpireVertical", new Vector3(center.x + side * b.extents.x * 0.92f, center.y, center.z - b.extents.z - 0.10f), new Vector3(0.18f, b.size.y * 0.88f, 0.08f), side < 0 ? "Cyan" : "Magenta", side < 0 ? MapCyberpunkGeometry.ColorFor("Cyan") : MapCyberpunkGeometry.ColorFor("Magenta"), 0.42f, center.y * 0.02f); // 장거리 수직 실루엣 강조
            }
        }
        for (int i = 0; i < 6; i++) // 첨탑 접근 광장 비콘 배치
        {
            float x = -20f + i * 8f; // 광장 비콘 가로 위치 계산
            g.Bollard(content, new Vector3(x, 0.2f, -24f), i % 2 == 0 ? "Cyan" : "Magenta"); // 진입 방향 네온 비콘 추가
        }
        g.NeonSign(content, "SpireIdentity", new Vector3(0f, 18f, -29f), new Vector3(14f, 2.8f, 0f), "SPIRE//CORE", "Magenta", MapCyberpunkGeometry.ColorFor("Magenta"), Quaternion.identity); // 첨탑 대표 식별 간판 추가
        g.PointLight(content, "SpireCyan", new Vector3(-12f, 10f, -20f), MapCyberpunkGeometry.ColorFor("Cyan"), 17f, 3.4f); // 첨탑 청록 주변광 추가
        g.PointLight(content, "SpireMagenta", new Vector3(12f, 12f, -18f), MapCyberpunkGeometry.ColorFor("Magenta"), 17f, 3.4f); // 첨탑 자홍 주변광 추가
    }

    private static int StableHash(string value) // 실행 환경과 무관한 간단한 선택 해시
    {
        unchecked // 정수 오버플로를 선택 규칙으로 허용
        {
            int hash = 23; // 고정 시작값 지정
            foreach (char c in value ?? string.Empty) // 이름 문자 순회
            {
                hash = hash * 31 + c; // 고정 곱셈 해시 누적
            }
            return Mathf.Abs(hash == int.MinValue ? int.MaxValue : hash); // 양수 선택값으로 변환
        }
    }

    public static Transform Find(Transform parent, string name) // 계층 전체 이름 검색
    {
        if (parent == null) // 부모 누락 확인
        {
            return null; // 검색 결과 없음 반환
        }
        if (parent.name == name) // 현재 객체 이름 확인
        {
            return parent; // 원하는 객체 반환
        }
        for (int i = 0; i < parent.childCount; i++) // 모든 자식 순회
        {
            Transform found = Find(parent.GetChild(i), name); // 재귀 검색 실행
            if (found != null) // 자식에서 발견 여부 확인
            {
                return found; // 첫 일치 객체 반환
            }
        }
        return null; // 전체 계층에 이름 없음 반환
    }
}
#endif
