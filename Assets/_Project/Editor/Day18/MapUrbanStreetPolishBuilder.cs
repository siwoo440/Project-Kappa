#if UNITY_EDITOR // 18일차 거리 가독성·보행 디테일 보정 전용
using System; // 문자열과 오류 처리
using ProjectK.Day16; // 기존 본편 월드 참조
using ProjectK.Day18; // 18일차 밀도와 거리 보정 참조
using UnityEditor; // 편집기 생성과 되돌리기
using UnityEngine; // 도시 좌표와 거리 묶음
using UnityEngine.Rendering; // 글자 그림자 옵션 설정

public static class MapUrbanStreetPolishBuilder // 간판 가독성 수정과 길거리 디테일 보강
{
    public const string SourceCommit = "84428023baa1d65f831c3c17158ac51eee5ab112"; // 수정 기준 최신 커밋

    public static MapUrbanStreetPolishMarker Build(MapWorldRoot world, MapUrbanDensityMarker day18) // 전체 거리 디테일 보강 생성
    {
        if (world == null || day18 == null || !day18.Completed) // 선행 작업 확인
        {
            throw new InvalidOperationException("Day18 도시 밀도 레이어가 먼저 필요합니다."); // 선행 설치 누락 보고
        }
        Transform existing = Find(world.transform, MapUrbanStreetPolishMarker.RootName); // 기존 18일차 레이어 확인
        if (existing != null) // 반복 설치 감지
        {
            MapUrbanStreetPolishMarker old = existing.GetComponent<MapUrbanStreetPolishMarker>(); // 기존 표식 조회
            if (old != null && old.Completed) // 완료 설치 확인
            {
                return old; // 수동 편집 보존
            }
            throw new InvalidOperationException("완료되지 않은 Day18 거리 보정 레이어가 있습니다."); // 부분 설치 중복 방지
        }
        int fixedSigns = FixExistingNeonSigns(world.transform); // Day17·Day18 전체 간판의 크기와 깊이 우선 수정
        GameObject rootObject = new GameObject(MapUrbanStreetPolishMarker.RootName); // 새 거리 디테일 루트 생성
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Day18 Street Polish"); // 전체 Undo 연결
        rootObject.transform.SetParent(world.transform, false); // 본편 월드에 연결
        MapUrbanStreetPolishMarker marker = rootObject.AddComponent<MapUrbanStreetPolishMarker>(); // 결과 표식 추가
        MapUrbanDensityGeometry geometry = new MapUrbanDensityGeometry(rootObject.transform, world); // 기존 도시 생성기 재사용
        Transform[] tileClusters = CreateTileClusters(world, geometry); // 3x3 타일별 거리 묶음 생성
        Transform blocks = Find(world.transform, "Blocks_12x12"); // 기존 블록 루트 조회
        if (blocks == null) // 도시 블록 존재 확인
        {
            throw new InvalidOperationException("Blocks_12x12 도시 루트를 찾지 못했습니다."); // 잘못된 Map 씬 보고
        }
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlock(block.name, out int x, out int z)) // 좌표 이름 확인
            {
                continue; // 다른 구조 제외
            }
            Transform content = ClusterFor(world, tileClusters, block.position); // 같은 Terrain 묶음 선택
            BuildStreetPolish(world, geometry, content, block, x, z); // 보도 블럭과 전봇대 생성
        }
        BuildDistrictStreetProps(world, geometry); // 핵심 구역 추가 디테일 생성
        marker.Configure(world, SourceCommit, fixedSigns, geometry.PoleCount, geometry.SidewalkCount, geometry.LightCount, geometry.ClusterCount); // 생성 결과 저장
        EditorUtility.SetDirty(marker); // 씬 저장 대상으로 표시
        return marker; // 검사와 선택에 사용할 표식 반환
    }

    public static int FixExistingNeonSigns(Transform root) // 기존 간판 글자 크기와 3D 깊이 관통을 함께 보정
    {
        int fixedCount = 0; // 수정 간판 집계 시작
        foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true)) // 전체 월드 문자 순회
        {
            if (text == null || text.transform.parent == null) // 안전한 문자 부모 확인
            {
                continue; // 잘못된 문자 제외
            }
            Transform sign = text.transform.parent; // 현재 문자 부모 조회
            Transform frame = sign.Find("Frame"); // 네온 간판 프레임 조회
            Transform face = sign.Find("NeonFace"); // 네온 간판 전면 조회
            if (frame == null || face == null) // 실제 네온 간판 구조 여부 확인
            {
                continue; // 포스터와 HUD 문자 제외
            }
            float width = Mathf.Max(0.8f, frame.localScale.x - 0.35f); // 실제 간판 내부 폭 계산
            float height = Mathf.Max(0.55f, frame.localScale.y - 0.35f); // 실제 간판 내부 높이 계산
            MapCyberpunkGeometry.ConfigureSignText(text, new Vector3(width, height, 0f), text.color); // 실제 렌더 바운드와 깊이 셰이더 적용
            face.localPosition = new Vector3(0f, 0f, -0.12f); // 전면 패널 위치를 고정해 문자와 분리
            fixedCount++; // 수정 간판 집계 증가
        }
        return fixedCount; // 총 수정 수 반환
    }

    private static Transform[] CreateTileClusters(MapWorldRoot world, MapUrbanDensityGeometry geometry) // Terrain 타일별 거리 묶음 생성
    {
        Transform[] result = new Transform[9]; // 아홉 타일 묶음 준비
        for (int z = 0; z < 3; z++) // 세로 Terrain 순회
        {
            for (int x = 0; x < 3; x++) // 가로 Terrain 순회
            {
                Terrain terrain = world.Tile(x, z); // 현재 Terrain 조회
                if (terrain == null) // Terrain 누락 확인
                {
                    throw new InvalidOperationException("Map Terrain 3x3 구성이 필요합니다."); // 잘못된 맵 보고
                }
                Vector3 center = terrain.transform.position + new Vector3(world.TileSize * 0.5f, 24f, world.TileSize * 0.5f); // 타일 중심 계산
                result[z * 3 + x] = geometry.Cluster("Polish_Tile_" + x + "_" + z, world.transform.InverseTransformPoint(center), world.TileSize * 1.12f); // 타일별 거리 묶음 생성
            }
        }
        return result; // 블록 배치용 묶음 반환
    }

    private static void BuildStreetPolish(MapWorldRoot world, MapUrbanDensityGeometry geometry, Transform content, Transform block, int x, int z) // 일반 블록의 보도와 전봇대 보강
    {
        float pitch = world.WorldSize * 0.84f / 12f; // 기존 도시 블록 간격 계산
        float roadWidth = Mathf.Clamp(pitch * 0.13f, 7f, 14f); // 기존 도로 폭 계산
        float halfLot = (pitch - roadWidth) * 0.5f; // 보행 부지 반폭 계산
        float ground = MapTerrainMath.Ground + 0.10f; // 도로 표면 위 높이 기준
        Vector3 center = block.position; // 현재 블록 중심 조회
        geometry.SidewalkStrip(content, new Vector3(center.x - content.position.x, ground - content.position.y, center.z - halfLot + 0.95f - content.position.z), pitch - roadWidth - 1.85f, 1.75f, 7, 2); // 블록 전면 보도 블럭 추가
        if ((x + z) % 2 == 0) // 절반 블록에 보조 보도 추가
        {
            geometry.SidewalkStrip(content, new Vector3(center.x - pitch * 0.35f - content.position.x, ground - content.position.y, center.z - halfLot + 2.45f - content.position.z), 4.2f, 1.10f, 4, 2); // 상점 앞 대기 구간 추가
        }
        if ((x + z) % 3 == 0) // 일정 간격의 전봇대 배치 조건 확인
        {
            geometry.UtilityPole(content, new Vector3(center.x - pitch * 0.34f - content.position.x, ground - content.position.y, center.z - halfLot + 0.12f - content.position.z), 6.2f, (x + z) % 4 == 0 ? "Cyan" : "Amber", true); // 남쪽 인도 전봇대 추가
        }
        if ((x + z) % 4 == 0) // 추가 전력 기둥 배치 조건 확인
        {
            geometry.UtilityPole(content, new Vector3(center.x + pitch * 0.34f - content.position.x, ground - content.position.y, center.z - halfLot + 0.18f - content.position.z), 5.8f, (x + z) % 5 == 0 ? "Magenta" : "Amber", false); // 보조 전봇대 추가
        }
    }

    private static void BuildDistrictStreetProps(MapWorldRoot world, MapUrbanDensityGeometry geometry) // 주요 구역 중심로 추가 보강
    {
        AddDistrictAccent(world, geometry, "GYEOPGIL_Market", 420f, -28f, 28f, "Magenta"); // 시장 구역 네온 보도와 조명 보강
        AddDistrictAccent(world, geometry, "JEORYU_Industrial", 460f, -32f, 32f, "Amber"); // 산업 구역 전봇대 보강
        AddDistrictAccent(world, geometry, "YURIGWAN_Corporate", 500f, -30f, 30f, "Cyan"); // 기업 구역 정돈된 조명 보강
    }

    private static void AddDistrictAccent(MapWorldRoot world, MapUrbanDensityGeometry geometry, string nodeName, float distance, float start, float end, string neon) // 핵심 구역 직선 보행로 보강
    {
        Transform site = Find(world.transform, nodeName); // 랜드마크 조회
        if (site == null) // 랜드마크 누락 확인
        {
            return; // 선택 구역이 없으면 조용히 생략
        }
        Transform content = geometry.Cluster(nodeName + "_StreetAccent", world.transform.InverseTransformPoint(site.position), distance); // 구역 전용 거리 묶음 생성
        geometry.SidewalkStrip(content, new Vector3(0f, 0.02f, 0f), 28f, 2.2f, 10, 2); // 중심 보행 띠 추가
        for (int i = 0; i < 5; i++) // 중심로 전봇대 반복
        {
            float x = Mathf.Lerp(start, end, i / 4f); // 균등한 배치 위치 계산
            geometry.UtilityPole(content, new Vector3(x, 0f, -1.6f), 6.5f, neon, true); // 핵심 구역 가로등 추가
        }
    }

    private static Transform ClusterFor(MapWorldRoot world, Transform[] clusters, Vector3 worldPosition) // 월드 위치에 맞는 Terrain 묶음 선택
    {
        float half = world.WorldSize * 0.5f; // 전체 월드 반경 계산
        int x = Mathf.Clamp(Mathf.FloorToInt((worldPosition.x + half) / world.TileSize), 0, 2); // 가로 타일 번호 계산
        int z = Mathf.Clamp(Mathf.FloorToInt((worldPosition.z + half) / world.TileSize), 0, 2); // 세로 타일 번호 계산
        return clusters[z * 3 + x]; // 해당 타일 묶음 반환
    }

    public static Transform Find(Transform root, string name) // 계층에서 이름으로 자식 찾기
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 모든 자식 Transform 순회
        {
            if (child.name == name) // 동일한 이름 확인
            {
                return child; // 찾은 구조 반환
            }
        }
        return null; // 찾지 못한 경우 null 반환
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
}
#endif
