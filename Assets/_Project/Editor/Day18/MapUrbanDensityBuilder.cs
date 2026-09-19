#if UNITY_EDITOR // 18일차 도시 밀도 배치 전용
using System; // 이름 분석과 오류 처리
using System.Collections.Generic; // 건물 충돌체 목록
using ProjectK.Day16; // 기존 본편 월드 참조
using ProjectK.Day17; // 17일차 네온 디테일 참조
using ProjectK.Day18; // 18일차 설치 표식 참조
using UnityEditor; // 편집기 생성과 되돌리기
using UnityEngine; // 도시 좌표와 충돌 범위

public static class MapUrbanDensityBuilder // 기존 Map 위 중간·소형 도시 구성요소 추가
{
    public const string SourceCommit = "fe896773797c3666949a159206831e417fa33f2d"; // 17일차 도시 기준 커밋

    public static MapUrbanDensityMarker Build(MapWorldRoot world, MapCyberpunkDetailMarker day17) // 전체 도시 밀도 레이어 생성
    {
        if (world == null || day17 == null || !day17.Completed) // 선행 월드와 네온 도시 확인
        {
            throw new InvalidOperationException("Day16 Map과 Day17 사이버펑크 디테일이 필요합니다."); // 선행 작업 누락 보고
        }
        Transform existing = MapCyberpunkDetailBuilder.Find(world.transform, MapUrbanDensityMarker.RootName); // 기존 설치 여부 확인
        if (existing != null) // 반복 설치 감지
        {
            MapUrbanDensityMarker old = existing.GetComponent<MapUrbanDensityMarker>(); // 기존 표식 조회
            if (old != null && old.Completed) // 정상 완료 설치 확인
            {
                return old; // 수동 편집 보존
            }
            throw new InvalidOperationException("완료되지 않은 Day18 도시 밀도 루트가 있습니다."); // 부분 설치 중복 방지
        }
        GameObject rootObject = new GameObject(MapUrbanDensityMarker.RootName); // 단일 삭제 가능한 밀도 루트 생성
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Day18 Urban Density"); // 전체 Undo 연결
        rootObject.transform.SetParent(world.transform, false); // 본편 월드에 밀도 레이어 연결
        MapUrbanDensityMarker marker = rootObject.AddComponent<MapUrbanDensityMarker>(); // 설치 결과 표식 추가
        MapUrbanDensityGeometry density = new MapUrbanDensityGeometry(rootObject.transform, world); // 공통 도시 모형 도구 준비
        Transform[] tileClusters = CreateTileClusters(world, density); // 3x3 표시 거리 묶음 생성
        Transform blocks = MapCyberpunkDetailBuilder.Find(world.transform, "Blocks_12x12"); // 기존 도시 블록 루트 조회
        if (blocks == null) // 도시 블록 누락 확인
        {
            throw new InvalidOperationException("Blocks_12x12 도시 루트를 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlock(block.name, out int x, out int z)) // 블록 좌표 이름 확인
            {
                continue; // 다른 구조 노드 제외
            }
            Transform content = ClusterFor(world, tileClusters, block.position); // 같은 Terrain 타일의 밀도 부모 선택
            BuildBlockDensity(world, density, content, block, x, z); // 건물 전면과 도로 가장자리 보강
        }
        BuildLinDensity(world, density); // 린 거점 생활 밀도 보강
        BuildGyeopgilDensity(world, density); // 겹길 시장 최고 밀도 보강
        BuildJeoryuDensity(world, density); // 저류 물류·산업 밀도 보강
        BuildYurigwanDensity(world, density); // 유리관 기업형 밀도 보강
        BuildSpireDensity(world, density); // 첨탑 접근 광장 밀도 보강
        marker.Configure(world, SourceCommit, density.StorefrontCount, density.VehicleCount, density.StreetPropCount, density.RooftopCount, density.BridgeCount, density.CableCount, density.NeonSignCount, density.ClusterCount); // 실제 생성 집계 저장
        EditorUtility.SetDirty(marker); // 씬 저장 대상으로 표시
        return marker; // 설정과 검증에서 사용할 표식 반환
    }

    private static Transform[] CreateTileClusters(MapWorldRoot world, MapUrbanDensityGeometry density) // Terrain 타일별 거리 표시 묶음 생성
    {
        Transform[] result = new Transform[9]; // 아홉 타일 묶음 준비
        for (int z = 0; z < 3; z++) // 세로 Terrain 순회
        {
            for (int x = 0; x < 3; x++) // 가로 Terrain 순회
            {
                Terrain terrain = world.Tile(x, z); // 현재 Terrain 조회
                if (terrain == null) // 누락 Terrain 확인
                {
                    throw new InvalidOperationException("Map Terrain 3x3 구성이 필요합니다."); // 잘못된 Map 씬 보고
                }
                Vector3 center = terrain.transform.position + new Vector3(world.TileSize * 0.5f, 28f, world.TileSize * 0.5f); // 타일 중심 계산
                result[z * 3 + x] = density.Cluster("Density_Tile_" + x + "_" + z, world.transform.InverseTransformPoint(center), world.TileSize * 1.12f); // 근처 타일에서만 소형 오브젝트 표시
            }
        }
        return result; // 블록 배치용 묶음 반환
    }

    public static void BuildBlockDensity(MapWorldRoot world, MapUrbanDensityGeometry density, Transform content, Transform block, int x, int z) // 일반 블록의 중간·소형 요소 보강
    {
        int level = DistrictDensity(x, z); // 구역별 목표 밀도 계산
        int hash = StableHash(block.name); // 재현 가능한 선택값 계산
        BoxCollider[] colliders = block.GetComponentsInChildren<BoxCollider>(true); // 기존 건물 충돌체 목록 조회
        List<BoxCollider> buildings = new List<BoxCollider>(); // 실제 건물 본체 목록 준비
        foreach (BoxCollider collider in colliders) // 모든 블록 충돌체 순회
        {
            if (collider.name == "MainCollision") // 일반 건물 본체 확인
            {
                buildings.Add(collider); // 건물 목록에 추가
            }
        }
        foreach (BoxCollider building in buildings) // 블록의 건물 전면 순회
        {
            BuildFacadeDensity(density, content, building, x, z, level, hash); // 1층·옥상·외벽 중간 밀도 추가
            hash += 17; // 같은 블록 두 건물 선택값 분리
        }
        if (buildings.Count >= 2 && level >= 2 && hash % 3 == 0) // 건물 사이 공중 연결 후보 확인
        {
            buildings.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x)); // 서쪽과 동쪽 건물 순서 정렬
            BoxCollider west = buildings[0]; // 서쪽 건물 선택
            BoxCollider east = buildings[buildings.Count - 1]; // 동쪽 건물 선택
            float y = Mathf.Min(west.bounds.max.y, east.bounds.max.y) - Mathf.Clamp(2.4f + level, 4.2f, 6.0f); // 접근 가능한 중간 높이 계산
            if (east.bounds.min.x - west.bounds.max.x > 3f) // 실제 건물 사이 간격 확인
            {
                Vector3 a = new Vector3(west.bounds.max.x + 0.15f, y, (west.bounds.center.z + east.bounds.center.z) * 0.5f) - content.position; // 서쪽 연결점 계산
                Vector3 b = new Vector3(east.bounds.min.x - 0.15f, y, (west.bounds.center.z + east.bounds.center.z) * 0.5f) - content.position; // 동쪽 연결점 계산
                density.ServiceBridge(content, a, b, level >= 3 ? "Magenta" : "Cyan"); // 공중 정비 통로 추가
            }
        }
        BuildStreetEdge(world, density, content, block, x, z, level, hash); // 도로 가장자리 생활 밀도 추가
        if (buildings.Count >= 2 && level >= 2) // 양쪽 건물 사이 케이블 배치 확인
        {
            BoxCollider aBuilding = buildings[0]; // 첫 건물 선택
            BoxCollider bBuilding = buildings[1]; // 둘째 건물 선택
            float cableY = Mathf.Min(aBuilding.bounds.max.y, bBuilding.bounds.max.y) - 1.5f; // 옥상 아래 케이블 높이 계산
            for (int c = 0; c < level; c++) // 구역 밀도에 따른 케이블 수 생성
            {
                Vector3 start = new Vector3(aBuilding.bounds.center.x, cableY - c * 0.35f, aBuilding.bounds.center.z - 1f + c) - content.position; // 첫 건물 고정점 계산
                Vector3 end = new Vector3(bBuilding.bounds.center.x, cableY - c * 0.25f, bBuilding.bounds.center.z + 1f - c) - content.position; // 둘째 건물 고정점 계산
                density.HangingCable(content, start, end); // 공중 전력 케이블 추가
            }
        }
    }

    private static void BuildFacadeDensity(MapUrbanDensityGeometry density, Transform content, BoxCollider building, int x, int z, int level, int hash) // 건물 1층과 옥상 실루엣 보강
    {
        Bounds b = building.bounds; // 실제 건물 범위 조회
        Vector3 center = b.center - content.position; // 거리 묶음 기준 중심 계산
        float front = b.min.z - content.position.z - 0.22f; // 남쪽 외벽 전면 위치 계산
        int storefronts = level >= 3 ? 2 : level == 2 ? 1 : hash % 3 == 0 ? 1 : 0; // 구역별 상점 수 결정
        string[] labels = LabelsFor(x, z); // 구역별 상점 문구 목록 조회
        string[] neons = new string[] { "Cyan", "Magenta", "Amber", "Violet", "Green" }; // 도시 네온 색상 목록
        for (int i = 0; i < storefronts; i++) // 상점 전면 반복 생성
        {
            float offset = storefronts == 1 ? 0f : (i == 0 ? -b.size.x * 0.22f : b.size.x * 0.22f); // 두 상점 좌우 위치 분리
            string neon = neons[(hash + i + x + z) % neons.Length]; // 재현 가능한 네온 색상 선택
            string label = labels[(hash + i) % labels.Length]; // 구역별 상점 이름 선택
            density.Storefront(content, new Vector3(center.x + offset, b.min.y - content.position.y + 0.02f, front), Mathf.Min(8f, b.size.x * 0.34f), label, neon, ((hash + i) & 3) == 0); // 기존 벽 앞 1층 상점 추가
        }
        if ((hash % 5) < Mathf.Max(1, level - 1)) // 구역 밀도에 따른 옥상 증축 확인
        {
            string neon = level >= 3 ? "Magenta" : "Cyan"; // 구역별 정비 네온 선택
            density.RooftopAnnex(content, new Vector3(center.x + b.extents.x * 0.18f, b.max.y - content.position.y, center.z - b.extents.z * 0.12f), new Vector3(Mathf.Min(7.5f, b.size.x * 0.32f), 3.2f + level * 0.35f, Mathf.Min(6.2f, b.size.z * 0.34f)), neon); // 지붕 기계실 추가
        }
        if (level >= 2 && hash % 4 == 0 && b.size.y > 12f) // 외벽 비상 구조 후보 확인
        {
            density.FireEscape(content, new Vector3(center.x - b.extents.x * 0.30f, b.min.y - content.position.y, front - 0.62f), Mathf.Clamp(b.size.x * 0.26f, 3.2f, 5.2f), b.size.y); // 외벽 발판과 사다리 추가
        }
        if ((level >= 3 && hash % 2 == 0) || (level == 2 && hash % 5 == 0)) // 중대형 광고판 후보 확인
        {
            string neon = hash % 3 == 0 ? "Magenta" : hash % 3 == 1 ? "Cyan" : "Amber"; // 광고판 색상 선택
            float signY = Mathf.Min(b.max.y - 2f, b.min.y + b.size.y * 0.58f) - content.position.y; // 외벽 중상단 위치 계산
            string label = level >= 3 ? "YEONMU//LIVE" : "GRID//SERVICE"; // 구역 밀도에 맞춘 광고 문구 선택
            density.Billboard(content, new Vector3(center.x, signY, front - 0.14f), new Vector3(Mathf.Min(9f, b.size.x * 0.52f), 2.1f, 0f), label, neon); // 중대형 외벽 광고판 추가
        }
    }

    private static void BuildStreetEdge(MapWorldRoot world, MapUrbanDensityGeometry density, Transform content, Transform block, int x, int z, int level, int hash) // 블록과 도로 사이 밀도 보강
    {
        float pitch = world.WorldSize * 0.84f / 12f; // Day16 도시 블록 간격 재사용
        float roadWidth = Mathf.Clamp(pitch * 0.13f, 7f, 14f); // Day16 도로 폭 재사용
        float halfLot = (pitch - roadWidth) * 0.5f; // 보행 부지 반폭 계산
        Vector3 center = block.position; // 블록 월드 중심 조회
        float ground = MapTerrainMath.Ground + 0.10f; // 도로 표면 위 높이 기준
        Vector3 southWalk = new Vector3(center.x, ground, center.z - halfLot + 1.4f); // 남쪽 인도 안쪽 위치 계산
        density.Curb(content, new Vector3(center.x - content.position.x, ground - content.position.y, center.z - halfLot - content.position.z), new Vector3(pitch - roadWidth - 1.8f, 0.18f, 0.28f)); // 남쪽 도로 경계 턱 추가
        density.SidewalkStrip(content, new Vector3(center.x - content.position.x, ground - content.position.y, center.z - halfLot + 0.92f - content.position.z), pitch - roadWidth - 2.1f, 1.65f, 6, 2); // 상점 앞 보도 블럭 띠 추가
        if ((x + z + hash) % 3 == 0) // 블록 일부에 전봇대와 가로등 배치
        {
            density.UtilityPole(content, southWalk - content.position + new Vector3(-pitch * 0.32f, 0f, -0.28f), 6.4f, level >= 3 ? "Magenta" : "Amber", true); // 길거리 전봇대와 조명 추가
        }
        if ((x + z) % 2 == 0) // 절반 블록의 보행 소품 배치
        {
            density.CargoStack(content, southWalk - content.position + new Vector3(-pitch * 0.23f, 0f, 1.6f), level >= 3 ? "Magenta" : "Amber"); // 배송 화물 묶음 추가
            density.TrafficPylon(content, southWalk - content.position + new Vector3(pitch * 0.30f, 0f, -0.6f), level >= 3 ? "Cyan" : "Amber"); // 보행 경계 신호 기둥 추가
        }
        if (hash % 3 == 0) // 정차 차량 배치 후보 확인
        {
            Vector3 car = new Vector3(center.x - pitch * 0.18f, ground, center.z - pitch * 0.5f + roadWidth * 0.28f); // 남쪽 도로 가장자리 위치 계산
            density.ParkedCar(content, car - content.position, 90f, level >= 3 ? "Magenta" : "Cyan"); // 정차 차량 추가
            density.Puddle(content, car - content.position + new Vector3(2.6f, -0.02f, 1.2f), new Vector2(4.8f, 1.5f)); // 차량 옆 젖은 도로 패치 추가
        }
        if (hash % 4 == 0) // 배달 바이크 후보 확인
        {
            Vector3 bike = new Vector3(center.x + pitch * 0.28f, ground, center.z - halfLot + 2.4f); // 상점 앞 바이크 위치 계산
            density.DeliveryBike(content, bike - content.position, 0f, level >= 3 ? "Green" : "Amber"); // 배달 바이크 추가
        }
        if (level >= 2 && hash % 3 != 1) // 도시 포스터 배치 후보 확인
        {
            Vector3 poster = new Vector3(center.x + pitch * 0.12f, ground + 1.65f, center.z - halfLot + 0.85f); // 인도 벽면 가상 위치 계산
            density.PosterPanel(content, poster - content.position, level >= 3 ? "NO ID\nNO ENTRY" : "DELIVERY\nNODE", level >= 3 ? "Magenta" : "Cyan"); // 도시 생활 문구 추가
        }
    }

    public static void BuildGyeopgilDensity(MapWorldRoot world, MapUrbanDensityGeometry density) // 겹길 최고 밀도 시장 보강
    {
        Transform site = MapCyberpunkDetailBuilder.Find(world.transform, "GYEOPGIL_Market"); // 기존 겹길 랜드마크 조회
        if (site == null) // 랜드마크 누락 확인
        {
            throw new InvalidOperationException("GYEOPGIL_Market을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = density.Cluster("GYEOPGIL_Infill", world.transform.InverseTransformPoint(site.position), 480f); // 시장 전용 고밀도 묶음 생성
        string[] labels = new string[] { "RAMEN//24", "BYTE//BAR", "MED//PATCH", "SYNTH//SHOP", "DATA//FIX", "OPEN//LATE" }; // 시장 상점 문구 목록
        string[] colors = new string[] { "Magenta", "Cyan", "Amber", "Violet", "Green" }; // 시장 네온 팔레트
        for (int i = 0; i < 12; i++) // 시장 가장자리 소형 상점 반복
        {
            float x = -30f + (i % 6) * 12f; // 가로 시장 위치 계산
            float z = i < 6 ? -18f : 18f; // 남북 두 줄 배치
            density.Storefront(content, new Vector3(x, 0.05f, z), 6.2f, labels[i % labels.Length], colors[i % colors.Length], i % 4 == 0); // 독립 시장 전면 추가
            if (i % 3 == 0) // 일부 상점 앞 바이크 추가
            {
                density.DeliveryBike(content, new Vector3(x + 3.8f, 0.15f, z + (z < 0f ? 3f : -3f)), z < 0f ? 0f : 180f, colors[(i + 2) % colors.Length]); // 시장 배달 바이크 추가
            }
        }
        for (int i = 0; i < 8; i++) // 골목 물류와 생활 소품 반복
        {
            density.CargoStack(content, new Vector3(-28f + i * 8f, 0.15f, 27f), i % 2 == 0 ? "Amber" : "Green"); // 시장 후방 배송 화물 추가
            density.Puddle(content, new Vector3(-25f + i * 7f, 0.16f, -27f), new Vector2(3.5f + i % 2, 1.2f)); // 시장 바닥 젖은 패치 추가
        }
    }

    public static void BuildJeoryuDensity(MapWorldRoot world, MapUrbanDensityGeometry density) // 저류 물류·산업 밀도 보강
    {
        Transform site = MapCyberpunkDetailBuilder.Find(world.transform, "JEORYU_Industrial"); // 기존 저류 랜드마크 조회
        if (site == null) // 랜드마크 누락 확인
        {
            throw new InvalidOperationException("JEORYU_Industrial을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = density.Cluster("JEORYU_Infill", world.transform.InverseTransformPoint(site.position), 520f); // 산업 전용 밀도 묶음 생성
        for (int i = 0; i < 10; i++) // 산업 화물 적재 구역 반복
        {
            float x = -34f + (i % 5) * 17f; // 적재 구역 가로 위치 계산
            float z = i < 5 ? -30f : 30f; // 남북 두 줄 배치
            density.CargoStack(content, new Vector3(x, 0.15f, z), i % 2 == 0 ? "Amber" : "Green"); // 산업 화물 팔레트 추가
            if (i % 2 == 0) // 절반 위치의 서비스 차량 배치
            {
                density.ParkedCar(content, new Vector3(x + 5f, 0.15f, z + (z < 0f ? 6f : -6f)), 90f, "Amber"); // 산업 서비스 차량 추가
            }
        }
        for (int i = 0; i < 5; i++) // 정비 라인 포스터와 경고 구조 반복
        {
            density.TrafficPylon(content, new Vector3(-24f + i * 12f, 0.15f, -16f), "Amber"); // 산업 안전 파일런 추가
            density.PosterPanel(content, new Vector3(-24f + i * 12f, 2.0f, 18f), "LOAD\nZONE " + (i + 1), "Amber"); // 적재 구역 번호 표시 추가
        }
    }

    public static void BuildYurigwanDensity(MapWorldRoot world, MapUrbanDensityGeometry density) // 유리관 정돈된 기업 거리 밀도 보강
    {
        Transform site = MapCyberpunkDetailBuilder.Find(world.transform, "YURIGWAN_Corporate"); // 기존 유리관 랜드마크 조회
        if (site == null) // 랜드마크 누락 확인
        {
            throw new InvalidOperationException("YURIGWAN_Corporate을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = density.Cluster("YURIGWAN_Infill", world.transform.InverseTransformPoint(site.position), 560f); // 기업 전용 밀도 묶음 생성
        for (int i = -4; i <= 4; i++) // 기업 광장 정렬된 구조 반복
        {
            string color = i % 2 == 0 ? "Cyan" : "Violet"; // 기업 네온 색상 선택
            density.TrafficPylon(content, new Vector3(i * 7f, 0.15f, -25f), color); // 광장 보안 파일런 추가
            if ((i & 1) == 0) // 간격을 둔 차량 배치
            {
                density.ParkedCar(content, new Vector3(i * 7f, 0.15f, -34f), 90f, color); // 기업 서비스 차량 추가
            }
        }
        for (int i = 0; i < 4; i++) // 기업형 안내와 배송 지점 반복
        {
            density.Storefront(content, new Vector3(-21f + i * 14f, 0.05f, 24f), 8f, i % 2 == 0 ? "CORP//ACCESS" : "DATA//LOBBY", i % 2 == 0 ? "Cyan" : "Violet", false); // 기업 로비 전면 추가
            density.PosterPanel(content, new Vector3(-21f + i * 14f, 2.0f, 28f), "AUTHORIZED\nONLY", i % 2 == 0 ? "Cyan" : "Violet", 180f); // 기업 접근 경고 추가
        }
    }

    public static void BuildLinDensity(MapWorldRoot world, MapUrbanDensityGeometry density) // 린 거점 생활 흔적 보강
    {
        Transform site = MapCyberpunkDetailBuilder.Find(world.transform, "LIN_RooftopWorkshop"); // 기존 린 거점 조회
        if (site == null) // 거점 누락 확인
        {
            throw new InvalidOperationException("LIN_RooftopWorkshop을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = density.Cluster("LIN_Infill", world.transform.InverseTransformPoint(site.position), 420f); // 거점 전용 소품 묶음 생성
        density.CargoStack(content, new Vector3(-9f, 8.15f, 7f), "Cyan"); // 거점 배송 화물 추가
        density.CargoStack(content, new Vector3(8f, 8.15f, 6f), "Amber"); // 반대편 장비 상자 추가
        density.DeliveryBike(content, new Vector3(7f, 8.18f, -5f), 90f, "Cyan"); // 린의 옥상 배달 바이크 추가
        density.Puddle(content, new Vector3(-4f, 8.16f, -7f), new Vector2(4f, 1.5f)); // 옥상 젖은 바닥 패치 추가
        density.PosterPanel(content, new Vector3(-10f, 10.2f, -7f), "NIGHT\nROUTE", "Cyan"); // 작업실 생활 포스터 추가
    }

    public static void BuildSpireDensity(MapWorldRoot world, MapUrbanDensityGeometry density) // 첨탑 진입 광장 중간 밀도 보강
    {
        Transform site = MapCyberpunkDetailBuilder.Find(world.transform, "CHEOMTAP_Spire"); // 기존 첨탑 조회
        if (site == null) // 첨탑 누락 확인
        {
            throw new InvalidOperationException("CHEOMTAP_Spire을 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        Transform content = density.Cluster("CHEOMTAP_Infill", world.transform.InverseTransformPoint(site.position), 680f); // 첨탑 전용 광장 묶음 생성
        for (int i = -3; i <= 3; i++) // 진입 광장 서비스 구조 반복
        {
            string color = i % 2 == 0 ? "Cyan" : "Magenta"; // 첨탑 네온 색상 선택
            density.TrafficPylon(content, new Vector3(i * 8f, 0.15f, -34f), color); // 접근 파일런 추가
            if (i % 3 == 0) // 제한된 보안 차량 배치
            {
                density.ParkedCar(content, new Vector3(i * 8f, 0.15f, -43f), 90f, color); // 첨탑 서비스 차량 추가
            }
        }
        density.Storefront(content, new Vector3(-16f, 0.05f, -20f), 8f, "SEC//CHECK", "Cyan", true); // 첨탑 보안 부스 외형 추가
        density.Storefront(content, new Vector3(16f, 0.05f, -20f), 8f, "VISITOR//ID", "Magenta", true); // 방문 인증 부스 외형 추가
    }

    private static int DistrictDensity(int x, int z) // 블록 좌표별 도시 밀도 등급 계산
    {
        if (x >= 4 && x <= 8 && z >= 3 && z <= 7) // 겹길 중심권 확인
        {
            return 3; // 최고 밀도 반환
        }
        if ((x <= 3 && z <= 4) || (x >= 8 && z >= 4 && z <= 8)) // 저류와 유리관 권역 확인
        {
            return 2; // 중간 밀도 반환
        }
        return 1; // 외곽과 첨탑 주변 낮은 밀도 반환
    }

    private static string[] LabelsFor(int x, int z) // 구역별 1층 상점 문구 목록 반환
    {
        if (x <= 3 && z <= 4) // 저류 산업권 확인
        {
            return new string[] { "MECH//PARTS", "COOLANT", "CARGO//07", "PUMP//FIX" }; // 산업 상점 문구 반환
        }
        if (x >= 8 && z >= 4 && z <= 8) // 유리관 기업권 확인
        {
            return new string[] { "CORP//ACCESS", "DATA//NODE", "SECURE//LOBBY", "MEMBER//ONLY" }; // 기업 상점 문구 반환
        }
        return new string[] { "RAMEN//24", "BYTE//BAR", "DATA//FIX", "MED//PATCH", "DELIVERY//NODE", "OPEN//LATE" }; // 시장과 주거권 문구 반환
    }

    private static Transform ClusterFor(MapWorldRoot world, Transform[] clusters, Vector3 worldPosition) // 월드 위치에 맞는 Terrain 묶음 선택
    {
        float half = world.WorldSize * 0.5f; // 전체 월드 반경 계산
        int x = Mathf.Clamp(Mathf.FloorToInt((worldPosition.x + half) / world.TileSize), 0, 2); // 가로 타일 번호 계산
        int z = Mathf.Clamp(Mathf.FloorToInt((worldPosition.z + half) / world.TileSize), 0, 2); // 세로 타일 번호 계산
        return clusters[z * 3 + x]; // 해당 타일 표시 자식 반환
    }

    private static bool TryBlock(string name, out int x, out int z) // Block_XX_ZZ 이름에서 좌표 분석
    {
        x = -1; // 실패 기본 가로 값
        z = -1; // 실패 기본 세로 값
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Block_", StringComparison.Ordinal)) // 블록 접두어 확인
        {
            return false; // 다른 이름 제외
        }
        string[] parts = name.Split('_'); // 좌표 문자열 분리
        return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z); // 두 좌표 분석 결과 반환
    }

    private static int StableHash(string value) // 실행 환경과 무관한 선택 해시
    {
        unchecked // 정수 오버플로 허용
        {
            int hash = 29; // 고정 시작값 지정
            foreach (char c in value ?? string.Empty) // 이름 문자 순회
            {
                hash = hash * 31 + c; // 고정 곱셈 해시 누적
            }
            return Mathf.Abs(hash == int.MinValue ? int.MaxValue : hash); // 양수 선택값 반환
        }
    }
}
#endif
