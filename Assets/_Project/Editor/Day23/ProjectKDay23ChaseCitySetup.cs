#if UNITY_EDITOR // 23일차 도시 배치 교정 전용
using System; // 오류와 날짜 처리
using System.Collections.Generic; // 건물과 재질 목록
using System.IO; // 씬과 Terrain 백업 처리
using ProjectK.Day16; // 본편 월드와 지면 높이 참조
using ProjectK.Day23; // 23일차 설치 표식 참조
using UnityEditor; // 에셋과 Undo 처리
using UnityEditor.SceneManagement; // 씬 저장 처리
using UnityEngine; // 도시 구조와 Terrain 생성
using UnityEngine.Rendering; // 렌더 파이프라인 확인
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay23ChaseCitySetup // Day23 배치 교정과 실제 지하 Terrain 생성
{
    private const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 본편 Map 씬
    private const string GeneratedFolder = "Assets/_Project/Generated/Map23"; // 생성 에셋 루트
    private const string MaterialFolder = GeneratedFolder + "/Materials"; // 공통 도시 재질 폴더
    private const string TerrainFolder = GeneratedFolder + "/UndergroundTerrain"; // 지하 Terrain 에셋 폴더
    private const string BackupFolder = "Assets/_Project/Backups/Day23"; // Map과 Terrain 백업 폴더
    private const string RootName = "Day23_UrbanChaseExpansion"; // 씬 확장 루트 이름
    private const string SourceCommit = "bd0f2be58c198e3da0a126267b2767b57d488fc7"; // 교정 기준 최신 커밋
    private const float UndergroundFloorY = 1.75f; // 지하 Terrain 실제 표면 높이
    private const float UndergroundWidth = 220f; // 지하 Terrain 가로 길이
    private const float UndergroundDepth = 150f; // 지하 Terrain 세로 길이
    private const float StairWidth = 8.0f; // 지상과 지하를 잇는 계단 폭
    private const float StairLength = 34f; // 지상 Terrain 개구부의 계단 길이
    private const int StairVisualSteps = 56; // 보이는 계단 단 수
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(); // 생성 재질 캐시
    private static int highRiseCount; // 안전 배치 고층 집계
    private static int alleyCount; // 안전 배치 골목 집계
    private static int subwayPropCount; // 지하 공간 소품 집계

    [MenuItem("Project K/Day 23/Fix Layout And Build Terrain Subway")] // 실제 교정 메뉴
    public static void Apply() // 현재 Day23 확장을 제거하고 안전 배치로 다시 생성
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 잘못된 시점 변경 차단
        }

        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 Day23 교정 메뉴를 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 방지
        }

        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 본편 월드 조회
        if (world == null || world.Player == null || world.Tiles == null || world.Tiles.Length != 9) // 필수 월드 구성 확인
        {
            Debug.LogError("MapWorldRoot의 플레이어와 3x3 Terrain 구성을 확인하세요."); // 누락 구성 보고
            return; // 설치 중단
        }

        EnsureFolder(BackupFolder); // 백업 폴더 준비
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"); // 이번 교정 작업 고유 시각 생성
        string sceneBackup = BackupFolder + "/Map_before_day23_layout_fix_" + stamp + ".unity"; // 현재 Map 백업 경로 생성

        if (!EditorSceneManager.SaveScene(scene, sceneBackup, true)) // 씬 변경 전 복사 백업
        {
            Debug.LogError("Day23 배치 교정 전 Map 씬 백업에 실패했습니다."); // 백업 실패 보고
            return; // 안전하지 않은 변경 중단
        }

        EnsureFolder(GeneratedFolder); // 생성 자료 루트 준비
        EnsureFolder(MaterialFolder); // 재질 폴더 준비
        EnsureFolder(TerrainFolder); // 지하 Terrain 폴더 준비
        materials.Clear(); // 이전 편집 세션 캐시 초기화
        highRiseCount = 0; // 고층 집계 초기화
        alleyCount = 0; // 골목 집계 초기화
        subwayPropCount = 0; // 지하 소품 집계 초기화

        Undo.IncrementCurrentGroup(); // 전체 교정 Undo 그룹 시작
        int undoGroup = Undo.GetCurrentGroup(); // Undo 그룹 번호 저장
        Undo.SetCurrentGroupName("Day23 Fix Layout And Terrain Subway"); // 실행 취소 이름 설정

        try // 배치 교정과 지하 생성 실행
        {
            Transform oldRoot = FindByName(world.transform, RootName); // 기존 Day23 확장 루트 조회
            if (oldRoot != null) // 이전 확장 존재 확인
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject); // 잘못 배치된 Day23 요소 전체 제거
            }

            GameObject rootObject = new GameObject(RootName); // 새 확장 루트 생성
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Corrected Day23 Expansion"); // 새 확장 루트 Undo 등록
            rootObject.transform.SetParent(world.transform, false); // 본편 월드 아래 연결
            rootObject.transform.localPosition = Vector3.zero; // 월드 기준 좌표 유지
            Map23CityExpansionMarker marker = rootObject.AddComponent<Map23CityExpansionMarker>(); // 새 설치 표식 추가

            BuildSafeHighRises(world, rootObject.transform); // 기업·중층 구역 기존 옥상 위에만 고층 증축
            BuildSafeAlleys(world, rootObject.transform); // 실제 건물 사이 빈 공간 기준 골목 소품 배치
            BuildFillBuildings(world, rootObject.transform); // 비어 보이는 블록에 저층 보조 건물 추가
            BuildRoadStreetLights(world, rootObject.transform); // 주요 도로 전체에 가로등 라인 추가
            SubwayBuildResult subway = BuildTerrainSubway(world, rootObject.transform, stamp); // Terrain 구멍·계단·지하 Terrain·역사 생성

            marker.Configure(world, subway.Terrain, SourceCommit, highRiseCount, alleyCount, 1, subwayPropCount, subway.StairsConnected); // 교정 결과 저장
            EditorUtility.SetDirty(marker); // 표식 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 표시

            if (!EditorSceneManager.SaveScene(scene)) // 교정된 원래 Map 씬 저장
            {
                throw new IOException("교정된 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }

            AssetDatabase.SaveAssets(); // Terrain·재질 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트에 생성 자료 반영
            Undo.CollapseUndoOperations(undoGroup); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = rootObject; // 교정된 확장 루트 선택
            Debug.Log("Day23 배치 교정 완료 · 고층 " + highRiseCount + "개 · 골목 " + alleyCount + "곳 · 지하 Terrain 1개 · 실제 계단 연결 " + subway.StairsConnected + " · 백업: " + sceneBackup); // 교정 결과 출력
        }
        catch (Exception error) // 부분 교정 실패 처리
        {
            Undo.RevertAllDownToGroup(undoGroup); // 씬 오브젝트와 Terrain Undo 복구
            Debug.LogException(error); // 실제 실패 원인 출력
            Debug.LogError("Day23 배치 교정 중단 · 씬 백업: " + sceneBackup); // 복구 경로 안내
        }
        finally // 편집기 상태 정리
        {
            EditorUtility.ClearProgressBar(); // 남은 진행 표시 제거
        }
    }

    private static void BuildSafeHighRises(MapWorldRoot world, Transform root) // 도시 성격에 맞는 구역만 고층화
    {
        Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 기존 도시 블록 루트 조회
        if (blocks == null) // 블록 루트 존재 확인
        {
            throw new InvalidOperationException("Blocks_12x12 도시 루트를 찾지 못했습니다."); // 잘못된 Map 구성 보고
        }

        for (int i = 0; i < blocks.childCount; i++) // 전체 도시 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlockCoordinate(block.name, out int x, out int z)) // Block_xx_zz 이름 확인
            {
                continue; // 다른 구조 노드 제외
            }

            bool corporateBand = x >= 8 && z >= 3 && z <= 9; // 동쪽 기업형 고층 구역 판정
            bool mixedBand = x >= 5 && x <= 7 && z >= 7 && z <= 9; // 중앙 북부 혼합 고층 구역 판정
            if (!corporateBand && !mixedBand) // 고층화 대상 구역 확인
            {
                continue; // 저류·시장 저층 실루엣 보존
            }

            BoxCollider[] buildingColliders = FindBuildingColliders(block); // 실제 건물 본체 조회
            if (buildingColliders.Length == 0) // 일반 건물 없는 예약 부지 확인
            {
                continue; // 랜드마크 부지 제외
            }

            foreach (BoxCollider building in buildingColliders) // 블록의 기존 건물 순회
            {
                if (building == null || building.bounds.size.x < 8f || building.bounds.size.z < 8f) // 고층 증축 가능한 옥상 크기 확인
                {
                    continue; // 너무 작은 건물 제외
                }

                int key = Mathf.Abs(StableHash(block.name + "_" + building.transform.parent.name)); // 건물별 안정적인 형태 키 계산
                bool useThisBuilding = corporateBand || key % 3 == 0; // 기업 구역은 적극적, 혼합 구역은 일부만 증축
                if (!useThisBuilding) // 현재 건물 증축 여부 확인
                {
                    continue; // 기존 높이 유지
                }

                BuildHighRiseOnBuilding(root, block, building, key, corporateBand); // 기존 옥상 중심을 기준으로 정확하게 증축
                highRiseCount++; // 고층 집계 증가

                if (highRiseCount >= 24) // 도시 실루엣 과밀 방지
                {
                    return; // 목표 고층 수 도달
                }
            }
        }
    }

    private static void BuildHighRiseOnBuilding(Transform root, Transform block, BoxCollider building, int key, bool corporate) // 기존 옥상 바로 위 고층 구조 생성
    {
        Bounds bounds = building.bounds; // 기존 건물 월드 범위 조회
        Vector3 baseCenter = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z); // 정확한 옥상 중심 계산
        Transform tower = CreateWorldNode(root, "HighRise_" + block.name + "_" + building.transform.parent.name, baseCenter); // 옥상 중심 증축 루트 생성
        float height = corporate ? 42f + key % 5 * 9f : 30f + key % 4 * 7f; // 구역별 상부 높이 계산
        float width = Mathf.Clamp(bounds.size.x * 0.58f, 8f, 20f); // 기존 옥상보다 좁은 상부 폭 계산
        float depth = Mathf.Clamp(bounds.size.z * 0.58f, 8f, 21f); // 기존 옥상보다 좁은 상부 깊이 계산
        float offsetX = ((key >> 2) & 1) == 0 ? -bounds.size.x * 0.05f : bounds.size.x * 0.05f; // 옥상 가장자리 통행 공간 확보
        float offsetZ = ((key >> 4) & 1) == 0 ? -bounds.size.z * 0.05f : bounds.size.z * 0.05f; // 옥상 가장자리 통행 공간 확보
        string bodyMaterial = corporate ? "TowerBlue" : key % 2 == 0 ? "TowerSteel" : "TowerDark"; // 구역에 맞는 외장 재질 선택
        CreateBox(tower, "TowerBody", new Vector3(offsetX, height * 0.5f + 0.08f, offsetZ), new Vector3(width, height, depth), bodyMaterial, true); // 기존 옥상 위에 본체 생성
        int bands = Mathf.Clamp(Mathf.RoundToInt(height / 8f), 4, 9); // 창문 띠 개수 계산

        for (int floor = 0; floor < bands; floor++) // 외벽 창문 띠 반복
        {
            float y = 4f + floor * (height - 7f) / Mathf.Max(1, bands - 1); // 현재 층 높이 계산
            string stripMaterial = floor % 3 == 0 ? "NeonCyan" : floor % 3 == 1 ? "WindowBlue" : "NeonMagenta"; // 층별 빛 색상 선택
            CreateBox(tower, "WindowSouth_" + floor, new Vector3(offsetX, y, offsetZ - depth * 0.5f - 0.05f), new Vector3(width * 0.70f, 0.32f, 0.08f), stripMaterial, false); // 남쪽 창문 띠 생성
            CreateBox(tower, "WindowNorth_" + floor, new Vector3(offsetX, y, offsetZ + depth * 0.5f + 0.05f), new Vector3(width * 0.70f, 0.32f, 0.08f), stripMaterial, false); // 북쪽 창문 띠 생성
        }

        CreateBox(tower, "RoofPlant", new Vector3(offsetX + width * 0.18f, height + 1.4f, offsetZ), new Vector3(width * 0.30f, 2.8f, depth * 0.30f), "Steel", true); // 옥상 기계실 생성
        CreateCylinder(tower, "Antenna", new Vector3(offsetX - width * 0.18f, height + 5.5f, offsetZ), new Vector3(0.28f, 2.8f, 0.28f), "Steel", false); // 옥상 안테나 생성
        CreateBox(tower, "CrownLight", new Vector3(offsetX, height + 0.25f, offsetZ - depth * 0.5f - 0.08f), new Vector3(width * 0.82f, 0.22f, 0.10f), corporate ? "NeonCyan" : "NeonMagenta", false); // 상단 네온 실루엣 생성
    }

    private static void BuildSafeAlleys(MapWorldRoot world, Transform root) // 실제 두 건물 사이 빈 공간 기준 골목 디테일 생성
    {
        Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 기존 블록 루트 조회
        if (blocks == null) // 블록 루트 존재 확인
        {
            throw new InvalidOperationException("Blocks_12x12 도시 루트를 찾지 못했습니다."); // 잘못된 Map 구성 보고
        }

        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlockCoordinate(block.name, out int x, out int z)) // 실제 블록 이름 확인
            {
                continue; // 다른 노드 제외
            }

            bool alleyZone = (x >= 3 && x <= 8 && z >= 3 && z <= 9) && ((x + z) % 2 == 0); // 시장·중층 중심 골목 대상 판정
            if (!alleyZone) // 골목 보강 구역 확인
            {
                continue; // 외곽 산업·랜드마크 과밀 방지
            }

            BoxCollider[] buildings = FindBuildingColliders(block); // 현재 블록의 실제 건물 본체 조회
            if (buildings.Length < 2) // 두 건물 사이 골목 존재 여부 확인
            {
                continue; // 예약지와 단독 건물 제외
            }

            Array.Sort(buildings, (a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x)); // 서쪽에서 동쪽 순으로 정렬
            Bounds west = buildings[0].bounds; // 서쪽 건물 범위 저장
            Bounds east = buildings[buildings.Length - 1].bounds; // 동쪽 건물 범위 저장
            float gapMinX = west.max.x; // 골목 서쪽 경계 계산
            float gapMaxX = east.min.x; // 골목 동쪽 경계 계산
            float gapWidth = gapMaxX - gapMinX; // 실제 비어 있는 골목 폭 계산
            float overlapMinZ = Mathf.Max(west.min.z, east.min.z); // 두 건물이 함께 존재하는 남쪽 범위 계산
            float overlapMaxZ = Mathf.Min(west.max.z, east.max.z); // 두 건물이 함께 존재하는 북쪽 범위 계산

            if (gapWidth < 7.5f || overlapMaxZ - overlapMinZ < 14f) // 안전한 이동 골목 최소 크기 확인
            {
                continue; // 좁거나 짧은 틈 제외
            }

            float alleyCenterX = (gapMinX + gapMaxX) * 0.5f; // 실제 골목 중심 X 계산
            float alleyCenterZ = (overlapMinZ + overlapMaxZ) * 0.5f; // 실제 골목 중심 Z 계산
            Transform alley = CreateWorldNode(root, "Alley_" + block.name, new Vector3(alleyCenterX, MapTerrainMath.Ground + 0.24f, alleyCenterZ)); // 실제 빈 공간 중심에 골목 루트 생성
            float halfGap = gapWidth * 0.5f; // 골목 반폭 계산
            float halfLength = (overlapMaxZ - overlapMinZ) * 0.5f; // 골목 반길이 계산
            int key = Mathf.Abs(StableHash(block.name)); // 블록별 장식 선택 키 계산

            CreateBox(alley, "WetPatch", new Vector3(0f, 0.03f, -halfLength * 0.30f), new Vector3(Mathf.Min(4.8f, gapWidth * 0.48f), 0.04f, 2.1f), "Wet", false); // 중앙을 막지 않는 젖은 바닥 생성
            CreateBox(alley, "Dumpster", new Vector3(-halfGap + 1.25f, 0.70f, -halfLength * 0.55f), new Vector3(2.1f, 1.4f, 1.35f), "Steel", true); // 서쪽 벽 쪽 쓰레기통 배치
            CreateBox(alley, "CrateA", new Vector3(halfGap - 0.85f, 0.48f, halfLength * 0.48f), new Vector3(1.0f, 0.96f, 1.0f), "Rust", true); // 동쪽 벽 쪽 화물 상자 배치
            CreateBox(alley, "CrateB", new Vector3(halfGap - 1.75f, 0.32f, halfLength * 0.52f), new Vector3(0.72f, 0.64f, 0.72f), "Rust", true); // 벽 쪽 보조 상자 배치
            CreateBox(alley, "OverheadCableFrame", new Vector3(0f, 4.5f, 0f), new Vector3(gapWidth - 1.4f, 0.16f, 0.16f), "Steel", false); // 머리 위 정비 프레임 생성
            CreateBox(alley, "NeonWall", new Vector3(-halfGap + 0.10f, 3.0f, 1.5f), new Vector3(0.10f, 2.2f, Mathf.Min(4.8f, halfLength * 0.55f)), key % 2 == 0 ? "NeonCyan" : "NeonMagenta", false); // 서쪽 벽면 네온 생성
            CreateCylinder(alley, "PipeA", new Vector3(halfGap - 0.16f, 2.6f, -1.7f), new Vector3(0.18f, 2.6f, 0.18f), "Pipe", false); // 동쪽 벽면 배관 생성
            CreateCylinder(alley, "PipeB", new Vector3(halfGap - 0.42f, 2.2f, -1.7f), new Vector3(0.12f, 2.2f, 0.12f), "Pipe", false); // 보조 배관 생성
            alleyCount++; // 안전 골목 집계 증가

            if (alleyCount >= 14) // 소품 과밀 방지
            {
                return; // 목표 골목 수 도달
            }
        }
    }

    private static SubwayBuildResult BuildTerrainSubway(MapWorldRoot world, Transform root, string stamp) // 지상 Terrain과 실제 계단으로 연결되는 별도 지하 Terrain 생성
    {
        Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 도시 블록 루트 조회
        Transform marketBlock = blocks != null ? FindByName(blocks, "Block_06_05") : null; // 겹길 예약 부지 블록 조회
        Transform pavedLot = marketBlock != null ? FindByName(marketBlock, "PavedLot") : null; // 예약 부지 기존 평탄 바닥 조회

        if (marketBlock == null || pavedLot == null) // 지하철 입구 기반 부지 확인
        {
            throw new InvalidOperationException("Block_06_05의 PavedLot을 찾지 못했습니다."); // 잘못된 Map 구성 보고
        }

        BoxCollider lotCollider = pavedLot.GetComponent<BoxCollider>(); // 기존 부지 범위 충돌체 조회
        if (lotCollider == null) // 부지 충돌체 존재 확인
        {
            throw new InvalidOperationException("겹길 예약 부지 PavedLot의 BoxCollider가 필요합니다."); // 지상 개구부 계산 불가 보고
        }

        Bounds lotBounds = lotCollider.bounds; // 실제 예약 부지 월드 범위 조회
        float openingCenterX = lotBounds.min.x + Mathf.Max(14f, lotBounds.size.x * 0.18f); // 시장 핵심 구조를 피한 서쪽 입구 X 계산
        float openingCenterZ = lotBounds.center.z - lotBounds.size.z * 0.08f; // 시장 중앙에서 약간 남쪽 입구 Z 계산
        Vector3 openingCenter = new Vector3(openingCenterX, lotBounds.max.y, openingCenterZ); // 실제 지상 계단 개구부 중심 계산
        Rect openingRect = new Rect(openingCenterX - StairWidth * 0.5f - 0.6f, openingCenterZ - StairLength * 0.5f - 0.6f, StairWidth + 1.2f, StairLength + 1.2f); // Terrain 구멍 월드 XZ 범위 생성

        Undo.RecordObject(pavedLot.gameObject, "Disable Original Market Paved Lot"); // 기존 부지 활성 상태 Undo 등록
        pavedLot.gameObject.SetActive(false); // 통짜 바닥을 숨겨 실제 계단 개구부 확보
        BuildLotPanelsAroundOpening(root, lotBounds, openingRect); // 기존 부지를 네 장의 안전한 바닥으로 재구성
        Terrain surfaceTerrain = FindTerrainForRect(world, openingRect); // 계단 개구부가 속한 지상 Terrain 조회

        if (surfaceTerrain == null) // Terrain 개구부 대상 확인
        {
            throw new InvalidOperationException("지하철 계단 개구부가 하나의 지상 Terrain 안에 들어오지 않습니다."); // 경계 걸침 오류 보고
        }

        BackupTerrainData(surfaceTerrain, stamp); // TerrainData 변경 전 원본 에셋 복사
        CutTerrainHole(surfaceTerrain, openingRect); // 지상 Terrain에 실제 통과 가능한 구멍 생성
        Terrain undergroundTerrain = CreateUndergroundTerrain(root, new Vector3(lotBounds.center.x, UndergroundFloorY, lotBounds.center.z)); // 지상 아래 별도 Terrain 생성
        BuildPhysicalStairwell(root, openingCenter, lotBounds.max.y + 0.05f, UndergroundFloorY + 0.18f); // 순간 이동 없이 걸어 내려가는 실제 계단 생성
        BuildUndergroundStation(root, undergroundTerrain); // 새 지하 Terrain 위 역·플랫폼·선로·통로 생성

        SubwayBuildResult result = new SubwayBuildResult(); // 설치 결과 구조 생성
        result.Terrain = undergroundTerrain; // 생성한 지하 Terrain 저장
        result.StairsConnected = true; // 실제 계단 연결 성공 표시
        return result; // 설치 결과 반환
    }

    private static void BuildLotPanelsAroundOpening(Transform root, Bounds lotBounds, Rect opening) // 통짜 PavedLot 대신 계단 구멍을 남긴 지상 바닥 재구성
    {
        float y = lotBounds.center.y; // 기존 부지 중심 높이 사용
        float thickness = Mathf.Max(0.20f, lotBounds.size.y); // 기존 부지 두께 유지
        float minX = lotBounds.min.x; // 부지 서쪽 경계 저장
        float maxX = lotBounds.max.x; // 부지 동쪽 경계 저장
        float minZ = lotBounds.min.z; // 부지 남쪽 경계 저장
        float maxZ = lotBounds.max.z; // 부지 북쪽 경계 저장
        float openMinX = Mathf.Clamp(opening.xMin, minX + 1f, maxX - 1f); // 개구부 서쪽 경계 보정
        float openMaxX = Mathf.Clamp(opening.xMax, minX + 1f, maxX - 1f); // 개구부 동쪽 경계 보정
        float openMinZ = Mathf.Clamp(opening.yMin, minZ + 1f, maxZ - 1f); // 개구부 남쪽 경계 보정
        float openMaxZ = Mathf.Clamp(opening.yMax, minZ + 1f, maxZ - 1f); // 개구부 북쪽 경계 보정
        Transform rebuilt = CreateWorldNode(root, "MarketLot_RebuiltAroundSubway", Vector3.zero); // 재구성 부지 루트 생성

        CreateWorldBox(rebuilt, "LotWest", new Vector3((minX + openMinX) * 0.5f, y, lotBounds.center.z), new Vector3(openMinX - minX, thickness, lotBounds.size.z), "Concrete", true); // 개구부 서쪽 바닥 생성
        CreateWorldBox(rebuilt, "LotEast", new Vector3((openMaxX + maxX) * 0.5f, y, lotBounds.center.z), new Vector3(maxX - openMaxX, thickness, lotBounds.size.z), "Concrete", true); // 개구부 동쪽 바닥 생성
        CreateWorldBox(rebuilt, "LotSouth", new Vector3((openMinX + openMaxX) * 0.5f, y, (minZ + openMinZ) * 0.5f), new Vector3(openMaxX - openMinX, thickness, openMinZ - minZ), "Concrete", true); // 개구부 남쪽 바닥 생성
        CreateWorldBox(rebuilt, "LotNorth", new Vector3((openMinX + openMaxX) * 0.5f, y, (openMaxZ + maxZ) * 0.5f), new Vector3(openMaxX - openMinX, thickness, maxZ - openMaxZ), "Concrete", true); // 개구부 북쪽 바닥 생성
    }

    private static Terrain FindTerrainForRect(MapWorldRoot world, Rect worldRect) // 개구부 전체를 포함하는 지상 Terrain 검색
    {
        foreach (Terrain terrain in world.Tiles) // 기존 아홉 Terrain 순회
        {
            if (terrain == null || terrain.terrainData == null) // 누락 Terrain 확인
            {
                continue; // 다음 Terrain 처리
            }

            Vector3 position = terrain.transform.position; // Terrain 남서 기준 위치 조회
            Vector3 size = terrain.terrainData.size; // Terrain 실제 크기 조회
            bool contains = worldRect.xMin >= position.x && worldRect.xMax <= position.x + size.x && worldRect.yMin >= position.z && worldRect.yMax <= position.z + size.z; // 개구부 전체 포함 여부 계산
            if (contains) // 대상 Terrain 확인
            {
                return terrain; // 개구부를 포함하는 Terrain 반환
            }
        }

        return null; // Terrain 경계 걸침 반환
    }

    private static void BackupTerrainData(Terrain terrain, string stamp) // 지상 TerrainData 수정 전 에셋 백업
    {
        string sourcePath = AssetDatabase.GetAssetPath(terrain.terrainData); // 현재 TerrainData 에셋 경로 조회
        if (string.IsNullOrEmpty(sourcePath)) // 저장된 TerrainData 여부 확인
        {
            return; // 임시 데이터는 복사 생략
        }

        string backupPath = BackupFolder + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_before_subway_" + stamp + ".asset"; // 고유 TerrainData 백업 경로 생성
        AssetDatabase.CopyAsset(sourcePath, backupPath); // 원본 TerrainData 에셋 복사
    }

    private static void CutTerrainHole(Terrain terrain, Rect worldRect) // 지상 Terrain 충돌과 표면에 실제 계단 구멍 생성
    {
        TerrainData data = terrain.terrainData; // 대상 TerrainData 조회
        int resolution = data.holesResolution; // Terrain 홀 격자 해상도 조회
        bool[,] holes = data.GetHoles(0, 0, resolution, resolution); // 현재 홀 상태 전체 읽기
        Vector3 origin = terrain.transform.position; // Terrain 남서 기준 위치 조회
        Vector3 size = data.size; // Terrain 실제 크기 조회
        int minX = Mathf.Clamp(Mathf.FloorToInt((worldRect.xMin - origin.x) / size.x * resolution), 0, resolution - 1); // 홀 서쪽 격자 계산
        int maxX = Mathf.Clamp(Mathf.CeilToInt((worldRect.xMax - origin.x) / size.x * resolution), 0, resolution - 1); // 홀 동쪽 격자 계산
        int minZ = Mathf.Clamp(Mathf.FloorToInt((worldRect.yMin - origin.z) / size.z * resolution), 0, resolution - 1); // 홀 남쪽 격자 계산
        int maxZ = Mathf.Clamp(Mathf.CeilToInt((worldRect.yMax - origin.z) / size.z * resolution), 0, resolution - 1); // 홀 북쪽 격자 계산
        Undo.RegisterCompleteObjectUndo(data, "Cut Subway Opening In Surface Terrain"); // TerrainData 변경 Undo 등록

        for (int z = minZ; z <= maxZ; z++) // 개구부 세로 홀 격자 순회
        {
            for (int x = minX; x <= maxX; x++) // 개구부 가로 홀 격자 순회
            {
                holes[z, x] = false; // Terrain 렌더와 충돌을 실제로 제거
            }
        }

        data.SetHoles(0, 0, holes); // 변경한 홀 상태 Terrain에 반영
        EditorUtility.SetDirty(data); // TerrainData 에셋 저장 대상으로 표시
    }

    private static Terrain CreateUndergroundTerrain(Transform root, Vector3 center) // 지상 아래 독립 TerrainData와 Terrain 생성
    {
        string dataPath = TerrainFolder + "/UndergroundTerrainData.asset"; // 지하 TerrainData 경로 생성
        string texturePath = TerrainFolder + "/UndergroundGroundTexture.asset"; // 지하 바닥 텍스처 경로 생성
        string layerPath = TerrainFolder + "/UndergroundGround.terrainlayer"; // 지하 TerrainLayer 경로 생성
        string materialPath = TerrainFolder + "/UndergroundTerrain.mat"; // 지하 Terrain 전용 재질 경로 생성

        DeleteGeneratedAsset(dataPath); // 이전 교정 TerrainData 제거
        DeleteGeneratedAsset(texturePath); // 이전 지하 텍스처 제거
        DeleteGeneratedAsset(layerPath); // 이전 TerrainLayer 제거
        DeleteGeneratedAsset(materialPath); // 이전 지하 Terrain 재질 제거

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false); // 단순 지하 콘크리트 텍스처 생성
        texture.name = "UndergroundGroundTexture"; // 텍스처 에셋 이름 설정
        Color32[] pixels = new Color32[16]; // 4x4 픽셀 배열 생성

        for (int i = 0; i < pixels.Length; i++) // 모든 텍스처 픽셀 순회
        {
            byte value = (byte)(i % 3 == 0 ? 58 : 48); // 미세한 명도 변화 생성
            pixels[i] = new Color32(value, (byte)(value + 3), (byte)(value + 5), 255); // 어두운 콘크리트 색상 저장
        }

        texture.SetPixels32(pixels); // 생성 픽셀 적용
        texture.Apply(false, false); // 텍스처 GPU 반영
        AssetDatabase.CreateAsset(texture, texturePath); // 텍스처 영구 에셋 저장

        TerrainLayer layer = new TerrainLayer(); // 지하 전용 TerrainLayer 생성
        layer.name = "UndergroundGround"; // TerrainLayer 이름 설정
        layer.diffuseTexture = texture; // 생성 콘크리트 텍스처 연결
        layer.tileSize = new Vector2(7f, 7f); // 지하 바닥 반복 크기 설정
        layer.smoothness = 0.06f; // 거친 콘크리트 표면 설정
        AssetDatabase.CreateAsset(layer, layerPath); // TerrainLayer 에셋 저장

        TerrainData data = new TerrainData(); // 새 지하 TerrainData 생성
        data.name = "UndergroundTerrainData"; // TerrainData 이름 설정
        data.heightmapResolution = 129; // 지하 평면 편집용 높이 해상도 설정
        data.size = new Vector3(UndergroundWidth, 24f, UndergroundDepth); // 지하 공간 실제 크기 설정
        data.alphamapResolution = 64; // 지하 표면 페인트 해상도 설정
        data.baseMapResolution = 128; // 원거리 바닥 표시 해상도 설정
        data.terrainLayers = new TerrainLayer[] { layer }; // 단일 콘크리트 레이어 연결
        float[,] heights = new float[data.heightmapResolution, data.heightmapResolution]; // 완전 평탄 지하 높이 배열 생성
        data.SetHeights(0, 0, heights); // 지하 Terrain을 평면으로 설정
        float[,,] alpha = new float[data.alphamapResolution, data.alphamapResolution, 1]; // 단일 표면 가중치 배열 생성

        for (int z = 0; z < data.alphamapResolution; z++) // 표면 세로 격자 순회
        {
            for (int x = 0; x < data.alphamapResolution; x++) // 표면 가로 격자 순회
            {
                alpha[z, x, 0] = 1f; // 전체 지하 Terrain에 콘크리트 레이어 적용
            }
        }

        data.SetAlphamaps(0, 0, alpha); // 표면 가중치 적용
        AssetDatabase.CreateAsset(data, dataPath); // TerrainData 에셋 저장

        Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Terrain/Lit" : "Nature/Terrain/Standard"); // 현재 파이프라인 Terrain 셰이더 조회
        if (shader == null) // Terrain 셰이더 존재 확인
        {
            throw new InvalidOperationException("지하 Terrain용 Terrain/Lit 셰이더를 찾지 못했습니다."); // 렌더 오류 방지
        }

        Material material = new Material(shader); // 지하 Terrain 전용 재질 생성
        material.name = "UndergroundTerrain"; // 재질 이름 설정
        material.enableInstancing = true; // Terrain 인스턴싱 활성화
        AssetDatabase.CreateAsset(material, materialPath); // Terrain 재질 에셋 저장

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data); // 지하 Terrain과 TerrainCollider 생성
        terrainObject.name = "UndergroundTerrain"; // 지하 Terrain 식별 이름 적용
        Undo.RegisterCreatedObjectUndo(terrainObject, "Create Underground Terrain"); // 지하 Terrain 생성 Undo 등록
        terrainObject.transform.SetParent(root, true); // Day23 확장 루트에 연결
        terrainObject.transform.position = new Vector3(center.x - UndergroundWidth * 0.5f, UndergroundFloorY, center.z - UndergroundDepth * 0.5f); // 지상 아래 독립 위치에 Terrain 배치
        Terrain terrain = terrainObject.GetComponent<Terrain>(); // 지하 Terrain 컴포넌트 조회
        terrain.materialTemplate = material; // 전용 Terrain 재질 연결
        terrain.drawInstanced = true; // Terrain 인스턴싱 활성화
        terrain.heightmapPixelError = 4f; // 지하 평면 표시 정밀도 설정
        terrain.basemapDistance = UndergroundWidth; // 지하 공간 전체 표면 품질 유지
        terrain.allowAutoConnect = false; // 지상 Terrain과 자동 이웃 연결 방지
        terrain.groupingID = 2323; // 지상 Terrain과 다른 그룹 식별
        terrain.detailObjectDistance = 0f; // 사용하지 않는 Terrain 디테일 비활성화
        return terrain; // 생성한 지하 Terrain 반환
    }

    private static void BuildPhysicalStairwell(Transform root, Vector3 openingCenter, float topY, float bottomY) // 지상에서 지하까지 직접 걸어가는 계단 생성
    {
        Transform stairs = CreateWorldNode(root, "Subway_MainStair", Vector3.zero); // 실제 계단 루트 생성
        float topZ = openingCenter.z - StairLength * 0.5f + 1.0f; // 지상 계단 첫 단 위치 계산
        float bottomZ = openingCenter.z + StairLength * 0.5f - 1.0f; // 지하 계단 마지막 단 위치 계산
        float usableLength = bottomZ - topZ; // 실제 계단 수평 길이 계산
        float drop = topY - bottomY; // 지상과 지하 높이 차 계산
        float tread = usableLength / StairVisualSteps; // 한 계단의 수평 깊이 계산
        float riser = drop / StairVisualSteps; // 한 계단의 수직 높이 계산

        for (int step = 0; step < StairVisualSteps; step++) // 보이는 계단 단 반복
        {
            float t = step / (float)(StairVisualSteps - 1); // 계단 진행률 계산
            float z = Mathf.Lerp(topZ, bottomZ, t); // 현재 계단 Z 위치 계산
            float y = Mathf.Lerp(topY, bottomY, t); // 현재 계단 표면 높이 계산
            CreateWorldBox(stairs, "Step_" + step.ToString("D2"), new Vector3(openingCenter.x, y - 0.09f, z), new Vector3(StairWidth - 0.7f, 0.18f, tread + 0.08f), "Concrete", false); // 시각용 계단 단 생성
        }

        float slopeLength = Mathf.Sqrt(usableLength * usableLength + drop * drop); // 부드러운 실제 보행 경사 길이 계산
        float slopeAngle = Mathf.Atan2(drop, usableLength) * Mathf.Rad2Deg; // 계단 아래 보행 경사 각도 계산
        GameObject walkRamp = CreateWorldBox(stairs, "WalkCollider", new Vector3(openingCenter.x, (topY + bottomY) * 0.5f - 0.16f, (topZ + bottomZ) * 0.5f), new Vector3(StairWidth - 1.0f, 0.32f, slopeLength), "Concrete", true); // 계단 아래 연속 보행 충돌면 생성
        walkRamp.transform.rotation = Quaternion.Euler(slopeAngle, 0f, 0f); // 지상에서 지하로 내려가는 경사 적용
        Renderer rampRenderer = walkRamp.GetComponent<Renderer>(); // 보행 경사 렌더러 조회
        if (rampRenderer != null) // 렌더러 존재 확인
        {
            rampRenderer.enabled = false; // 플레이어는 계단 모형만 보고 충돌은 매끄러운 경사 사용
        }

        float wallHeight = drop + 4.0f; // 계단 측벽 전체 높이 계산
        float wallCenterY = bottomY + wallHeight * 0.5f - 0.3f; // 측벽 중심 높이 계산
        CreateWorldBox(stairs, "WallWest", new Vector3(openingCenter.x - StairWidth * 0.5f - 0.25f, wallCenterY, openingCenter.z), new Vector3(0.5f, wallHeight, StairLength + 1.5f), "Concrete", true); // 서쪽 계단 측벽 생성
        CreateWorldBox(stairs, "WallEast", new Vector3(openingCenter.x + StairWidth * 0.5f + 0.25f, wallCenterY, openingCenter.z), new Vector3(0.5f, wallHeight, StairLength + 1.5f), "Concrete", true); // 동쪽 계단 측벽 생성
        CreateWorldBox(stairs, "EntranceHeader", new Vector3(openingCenter.x, topY + 3.4f, topZ - 0.8f), new Vector3(StairWidth + 1.2f, 0.35f, 1.0f), "NeonCyan", false); // 지상 입구 네온 헤더 생성
        CreateWorldBox(stairs, "UndergroundHeader", new Vector3(openingCenter.x, bottomY + 3.0f, bottomZ + 0.5f), new Vector3(StairWidth + 0.4f, 0.30f, 0.8f), "Warning", false); // 지하 진입 경고 헤더 생성
    }

    private static void BuildUndergroundStation(Transform root, Terrain undergroundTerrain) // 새 지하 Terrain 위 실제 역과 통로 모델링
    {
        Vector3 terrainOrigin = undergroundTerrain.transform.position; // 지하 Terrain 남서 기준 위치 조회
        Vector3 terrainSize = undergroundTerrain.terrainData.size; // 지하 Terrain 실제 크기 조회
        Vector3 center = terrainOrigin + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f); // 지하 Terrain 월드 중심 계산
        float floorY = UndergroundFloorY + 0.12f; // Terrain 표면 바로 위 역 바닥 기준
        float ceilingY = 12.4f; // 지상 Terrain 아래 충분한 천장 높이 설정
        Transform station = CreateWorldNode(root, "Subway_Station_OnTerrain", Vector3.zero); // 지하역 전체 루트 생성

        CreateWorldBox(station, "Ceiling", new Vector3(center.x, ceilingY, center.z), new Vector3(190f, 0.45f, 92f), "Steel", true); // 지하역 천장 생성
        CreateWorldBox(station, "WallNorth", new Vector3(center.x, (floorY + ceilingY) * 0.5f, center.z + 46f), new Vector3(190f, ceilingY - floorY, 0.5f), "Concrete", true); // 북쪽 외벽 생성
        CreateWorldBox(station, "WallSouth", new Vector3(center.x, (floorY + ceilingY) * 0.5f, center.z - 46f), new Vector3(190f, ceilingY - floorY, 0.5f), "Concrete", true); // 남쪽 외벽 생성
        CreateWorldBox(station, "WallWest", new Vector3(center.x - 95f, (floorY + ceilingY) * 0.5f, center.z), new Vector3(0.5f, ceilingY - floorY, 92f), "Concrete", true); // 서쪽 외벽 생성
        CreateWorldBox(station, "WallEast", new Vector3(center.x + 95f, (floorY + ceilingY) * 0.5f, center.z), new Vector3(0.5f, ceilingY - floorY, 92f), "Concrete", true); // 동쪽 외벽 생성

        CreateWorldBox(station, "PlatformNorth", new Vector3(center.x, floorY + 0.22f, center.z + 10.5f), new Vector3(170f, 0.44f, 13f), "Concrete", true); // 북쪽 승강장 생성
        CreateWorldBox(station, "PlatformSouth", new Vector3(center.x, floorY + 0.22f, center.z - 10.5f), new Vector3(170f, 0.44f, 13f), "Concrete", true); // 남쪽 승강장 생성
        CreateWorldBox(station, "TrackBed", new Vector3(center.x, floorY + 0.06f, center.z), new Vector3(172f, 0.10f, 7.0f), "Dark", false); // 중앙 선로 바닥 생성
        CreateWorldBox(station, "RailNorth", new Vector3(center.x, floorY + 0.28f, center.z + 1.65f), new Vector3(170f, 0.16f, 0.18f), "Steel", true); // 북쪽 레일 생성
        CreateWorldBox(station, "RailSouth", new Vector3(center.x, floorY + 0.28f, center.z - 1.65f), new Vector3(170f, 0.16f, 0.18f), "Steel", true); // 남쪽 레일 생성

        for (int i = -20; i <= 20; i++) // 선로 침목 반복
        {
            CreateWorldBox(station, "Sleeper_" + i, new Vector3(center.x + i * 4f, floorY + 0.18f, center.z), new Vector3(1.45f, 0.12f, 5.4f), "Rust", false); // 선로 침목 생성
            subwayPropCount++; // 지하 소품 집계 증가
        }

        for (int i = -8; i <= 8; i++) // 플랫폼 기둥과 천장 조명 반복
        {
            if (i == 0) // 계단 진입과 중앙 통로 공간 확보
            {
                continue; // 중앙 기둥 제외
            }

            float x = center.x + i * 10f; // 현재 기둥 X 위치 계산
            CreateWorldBox(station, "PillarNorth_" + i, new Vector3(x, 7.0f, center.z + 17f), new Vector3(0.65f, 10f, 0.65f), "Steel", true); // 북쪽 승강장 기둥 생성
            CreateWorldBox(station, "PillarSouth_" + i, new Vector3(x, 7.0f, center.z - 17f), new Vector3(0.65f, 10f, 0.65f), "Steel", true); // 남쪽 승강장 기둥 생성
            CreateWorldBox(station, "LightNorth_" + i, new Vector3(x, ceilingY - 0.35f, center.z + 17f), new Vector3(4.8f, 0.10f, 0.32f), "NeonCyan", false); // 북쪽 천장 조명 생성
            CreateWorldBox(station, "LightSouth_" + i, new Vector3(x, ceilingY - 0.35f, center.z - 17f), new Vector3(4.8f, 0.10f, 0.32f), "NeonCyan", false); // 남쪽 천장 조명 생성
            subwayPropCount += 4; // 기둥과 조명 집계 증가
        }

        BuildUndergroundTrain(station, center, floorY); // 정차 지하철 차량 생성
        BuildUndergroundServiceAlley(station, center, floorY, ceilingY); // 지하 서비스 골목과 설비 통로 생성
    }

    private static void BuildUndergroundTrain(Transform station, Vector3 center, float floorY) // 선로 위 정차 지하철 모델링
    {
        Transform train = CreateWorldNode(station, "StationaryTrain", Vector3.zero); // 열차 루트 생성

        for (int car = -1; car <= 1; car++) // 세 량 차량 반복
        {
            float x = center.x + car * 25f + 20f; // 차량별 X 위치 계산
            CreateWorldBox(train, "CarBody_" + car, new Vector3(x, floorY + 2.35f, center.z), new Vector3(23f, 4.3f, 5.0f), "Train", true); // 열차 차체 생성
            CreateWorldBox(train, "WindowNorth_" + car, new Vector3(x, floorY + 2.9f, center.z + 2.54f), new Vector3(15f, 1.05f, 0.08f), "WindowBlue", false); // 북쪽 창문 생성
            CreateWorldBox(train, "WindowSouth_" + car, new Vector3(x, floorY + 2.9f, center.z - 2.54f), new Vector3(15f, 1.05f, 0.08f), "WindowBlue", false); // 남쪽 창문 생성
            CreateWorldBox(train, "NeonStripe_" + car, new Vector3(x, floorY + 1.1f, center.z - 2.62f), new Vector3(20f, 0.18f, 0.08f), car == 0 ? "NeonMagenta" : "NeonCyan", false); // 차량 네온 띠 생성
            subwayPropCount += 4; // 열차 시각 요소 집계
        }
    }

    private static void BuildUndergroundServiceAlley(Transform station, Vector3 center, float floorY, float ceilingY) // 플랫폼 옆 지하 골목형 서비스 통로 생성
    {
        float corridorX = center.x - 72f; // 서비스 통로 서쪽 위치 계산
        CreateWorldBox(station, "ServiceFloor", new Vector3(corridorX, floorY + 0.08f, center.z + 31f), new Vector3(16f, 0.16f, 52f), "Concrete", true); // 서비스 통로 바닥 생성
        CreateWorldBox(station, "ServiceCeiling", new Vector3(corridorX, ceilingY - 0.1f, center.z + 31f), new Vector3(16f, 0.20f, 52f), "Steel", true); // 서비스 통로 천장 생성
        CreateWorldBox(station, "ServiceWestWall", new Vector3(corridorX - 8f, 7.0f, center.z + 31f), new Vector3(0.4f, 10f, 52f), "Concrete", true); // 서비스 통로 서쪽 벽 생성
        CreateWorldBox(station, "ServiceEastWall", new Vector3(corridorX + 8f, 7.0f, center.z + 31f), new Vector3(0.4f, 10f, 52f), "Concrete", true); // 서비스 통로 동쪽 벽 생성

        for (int i = 0; i < 7; i++) // 배관·경고등·화물 반복
        {
            float z = center.z + 11f + i * 7f; // 현재 서비스 소품 Z 위치 계산
            CreateCylinder(station, "ServicePipe_" + i, new Vector3(corridorX - 7.4f, 9.0f, z), new Vector3(0.16f, 2.4f, 0.16f), "Pipe", false); // 벽면 수직 배관 생성
            CreateWorldBox(station, "ServiceLight_" + i, new Vector3(corridorX, ceilingY - 0.35f, z), new Vector3(3.4f, 0.10f, 0.28f), i % 2 == 0 ? "Warning" : "NeonCyan", false); // 천장 경고 조명 생성
            subwayPropCount += 2; // 서비스 설비 집계 증가
        }

        CreateWorldBox(station, "ServiceCrateA", new Vector3(corridorX + 5.5f, floorY + 0.60f, center.z + 42f), new Vector3(1.3f, 1.2f, 1.3f), "Rust", true); // 서비스 화물 상자 생성
        CreateWorldBox(station, "ServiceCrateB", new Vector3(corridorX + 4.0f, floorY + 0.42f, center.z + 44f), new Vector3(0.9f, 0.84f, 0.9f), "Rust", true); // 보조 화물 생성
        subwayPropCount += 2; // 서비스 화물 집계 증가
    }


    private static void BuildFillBuildings(MapWorldRoot world, Transform root) // 빈 공간을 저층 보조 건물과 점포로 채우기
    {
        Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 기존 도시 블록 루트 조회
        if (blocks == null) // 블록 루트 존재 확인
        {
            throw new InvalidOperationException("Blocks_12x12 도시 루트를 찾지 못했습니다."); // 잘못된 Map 구성 보고
        }

        int created = 0; // 생성된 채움 건물 묶음 수
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlockCoordinate(block.name, out int x, out int z)) // 블록 좌표 확인
            {
                continue; // 다른 노드 제외
            }

            bool targetZone = x >= 2 && x <= 9 && z >= 2 && z <= 9; // 플레이 중심 시가지 구역 확인
            bool reservedSubway = x == 6 && z == 5; // 지하철 입구 예약 부지 확인
            if (!targetZone || reservedSubway) // 생성 허용 구역 확인
            {
                continue; // 외곽과 지하철 입구 부지 제외
            }

            BoxCollider[] existingBuildings = FindBuildingColliders(block); // 기존 건물 본체 조회
            if (existingBuildings.Length >= 2) // 이미 충분히 채워진 블록 확인
            {
                continue; // 기존 건물 밀도 유지
            }

            Vector3 center = block.position; // 블록 중심 위치 저장
            float surfaceY = SampleSurfaceHeight(world, center.x, center.z, center.y); // 블록 위치의 실제 지면 높이 계산
            center.y = surfaceY + 0.24f; // 기존 PavedLot 보행면 높이에 맞춘 루트 높이 설정
            int seed = Mathf.Abs(StableHash(block.name)); // 블록별 형태 시드 계산
            Transform cluster = CreateWorldNode(root, "FillCluster_" + block.name, center); // 실제 지면에 맞춘 채움 건물 루트 생성
            float frontShift = (seed % 2 == 0 ? -1f : 1f) * 3.5f; // 건물 묶음 앞뒤 이동 값 계산

            CreateFillBuilding(cluster, "Primary", new Vector3(-10f, 0f, frontShift), new Vector3(16f, 12f + (seed % 3) * 4f, 14f), seed % 2 == 0 ? "TowerDark" : "TowerSteel", seed % 2 == 0); // 메인 저층 건물을 루트 바닥에서 시작
            CreateFillBuilding(cluster, "Shop", new Vector3(8.5f, 0f, -frontShift * 0.6f), new Vector3(11f, 6.5f, 10f), "TowerBlue", true); // 보조 점포 건물을 루트 바닥에서 시작

            if (existingBuildings.Length == 0 || seed % 3 == 0) // 완전 공터 또는 일부 블록 추가 장식 확인
            {
                CreateFillBuilding(cluster, "Storage", new Vector3(6f, 0f, 10f), new Vector3(8f, 5.2f, 7f), "Rust", false); // 작은 창고 건물을 지면에서 시작
            }

            CreateBox(cluster, "Awning_" + block.name, new Vector3(8.5f, 3.0f, -frontShift * 0.6f + 5.2f), new Vector3(8f, 0.18f, 2.2f), "NeonMagenta", false); // 점포 전면 차양을 로컬 높이로 생성
            CreateBox(cluster, "Sign_" + block.name, new Vector3(8.5f, 4.8f, -frontShift * 0.6f + 5.5f), new Vector3(6.2f, 1.1f, 0.10f), "NeonCyan", false); // 점포 전면 간판을 로컬 높이로 생성
            CreateBox(cluster, "UtilityBox_" + block.name, new Vector3(-14f, 0.8f, 9f), new Vector3(1.1f, 1.6f, 0.8f), "Steel", true); // 전기 박스를 지면 위에 배치
            CreateBox(cluster, "Crate_" + block.name, new Vector3(-12.8f, 0.4f, 9.6f), new Vector3(0.8f, 0.8f, 0.8f), "Rust", true); // 화물 상자를 지면 위에 배치
            created++; // 채움 건물 묶음 수 증가

            if (created >= 18) // 과도한 블록 충전 방지
            {
                return; // 목표 채움 건물 수 도달
            }
        }
    }

    private static void CreateFillBuilding(Transform cluster, string name, Vector3 center, Vector3 size, string materialKey, bool addWindows) // 공터 채움용 단순 건물 생성
    {
        Vector3 bodyCenter = center + new Vector3(0f, size.y * 0.5f, 0f); // 건물 본체 중심 위치 계산
        CreateBox(cluster, name + "_Body", bodyCenter, size, materialKey, true); // 본체 생성
        CreateBox(cluster, name + "_RoofCap", center + new Vector3(0f, size.y + 0.18f, 0f), new Vector3(size.x * 0.92f, 0.26f, size.z * 0.92f), "Steel", true); // 옥상 마감 생성

        if (!addWindows) // 창문 띠 필요 여부 확인
        {
            return; // 단순 창고 형태 유지
        }

        int rows = Mathf.Clamp(Mathf.RoundToInt(size.y / 3.6f), 1, 5); // 창문 층 수 계산
        for (int row = 0; row < rows; row++) // 창문 띠 반복
        {
            float y = center.y + 2.0f + row * Mathf.Max(1.9f, (size.y - 3.8f) / Mathf.Max(1, rows - 1)); // 현재 창문 높이 계산
            CreateBox(cluster, name + "_WindowNorth_" + row, new Vector3(center.x, y, center.z + size.z * 0.5f + 0.05f), new Vector3(size.x * 0.72f, 0.30f, 0.08f), "WindowBlue", false); // 북쪽 창문 띠 생성
            CreateBox(cluster, name + "_WindowSouth_" + row, new Vector3(center.x, y, center.z - size.z * 0.5f - 0.05f), new Vector3(size.x * 0.72f, 0.30f, 0.08f), "WindowBlue", false); // 남쪽 창문 띠 생성
        }
    }

    private static void BuildRoadStreetLights(MapWorldRoot world, Transform root) // 주요 도로 전체에 가로등 라인 배치
    {
        Transform lightsRoot = CreateWorldNode(root, "RoadStreetLights", Vector3.zero); // 가로등 전체 루트 생성
        float[] xLines = new float[] { -168f, -112f, -56f, 0f, 56f, 112f, 168f }; // 세로 주요 도로 기준선 정의
        float[] zLines = new float[] { -168f, -112f, -56f, 0f, 56f, 112f, 168f }; // 가로 주요 도로 기준선 정의

        foreach (float xLine in xLines) // 세로 도로 라인 순회
        {
            for (float z = -220f; z <= 220f; z += 32f) // 일정 간격 위치 순회
            {
                float westY = SampleSurfaceHeight(world, xLine - 10f, z, MapTerrainMath.Ground) + 0.08f; // 서쪽 가로등 실제 지면 높이 계산
                float eastY = SampleSurfaceHeight(world, xLine + 10f, z + 16f, MapTerrainMath.Ground) + 0.08f; // 동쪽 가로등 실제 지면 높이 계산
                CreateStreetLight(lightsRoot, new Vector3(xLine - 10f, westY, z), "LampPole"); // 서쪽 보도 가로등 생성
                CreateStreetLight(lightsRoot, new Vector3(xLine + 10f, eastY, z + 16f), "LampPole"); // 동쪽 보도 가로등 생성
            }
        }

        foreach (float zLine in zLines) // 가로 도로 라인 순회
        {
            for (float x = -220f; x <= 220f; x += 32f) // 일정 간격 위치 순회
            {
                float southY = SampleSurfaceHeight(world, x + 16f, zLine - 10f, MapTerrainMath.Ground) + 0.08f; // 남쪽 가로등 실제 지면 높이 계산
                float northY = SampleSurfaceHeight(world, x, zLine + 10f, MapTerrainMath.Ground) + 0.08f; // 북쪽 가로등 실제 지면 높이 계산
                CreateStreetLight(lightsRoot, new Vector3(x + 16f, southY, zLine - 10f), "LampPole"); // 남쪽 보도 가로등 생성
                CreateStreetLight(lightsRoot, new Vector3(x, northY, zLine + 10f), "LampPole"); // 북쪽 보도 가로등 생성
            }
        }
    }

    private static void CreateStreetLight(Transform root, Vector3 basePosition, string poleMaterial) // 단순 도로 가로등 생성
    {
        Transform lamp = CreateWorldNode(root, "StreetLight", basePosition); // 실제 지면을 기준으로 가로등 루트 생성
        CreateBox(lamp, "Base", new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.70f, 0.9f), "Concrete", true); // 받침대 생성
        CreateLocalCylinder(lamp, "Pole", new Vector3(0f, 4.8f, 0f), new Vector3(0.14f, 4.8f, 0.14f), poleMaterial, false); // 기둥을 루트 기준 로컬 높이에 생성
        CreateBox(lamp, "Arm", new Vector3(1.05f, 9.25f, 0f), new Vector3(2.2f, 0.16f, 0.16f), poleMaterial, false); // 가로 지지대를 로컬 높이에 생성
        CreateBox(lamp, "LampHead", new Vector3(2.0f, 8.8f, 0f), new Vector3(0.8f, 0.28f, 0.45f), "Steel", true); // 램프 머리를 로컬 높이에 생성
        CreateBox(lamp, "LampGlow", new Vector3(2.02f, 8.55f, 0f), new Vector3(0.65f, 0.08f, 0.38f), "WarmLight", false); // 발광부를 로컬 높이에 생성
    }

    private static float SampleSurfaceHeight(MapWorldRoot world, float worldX, float worldZ, float fallback) // 월드 XZ 위치의 실제 지상 Terrain 높이 계산
    {
        if (world == null || world.Tiles == null) // Terrain 목록 존재 확인
        {
            return fallback; // 기본 높이 반환
        }

        foreach (Terrain terrain in world.Tiles) // 기존 지상 Terrain 순회
        {
            if (terrain == null || terrain.terrainData == null) // 유효한 Terrain 확인
            {
                continue; // 다음 Terrain 검사
            }

            Vector3 origin = terrain.transform.position; // Terrain 남서 기준 위치 조회
            Vector3 size = terrain.terrainData.size; // Terrain 실제 크기 조회
            bool inside = worldX >= origin.x && worldX <= origin.x + size.x && worldZ >= origin.z && worldZ <= origin.z + size.z; // 현재 위치가 Terrain 안인지 확인
            if (!inside) // 현재 Terrain 범위 밖 확인
            {
                continue; // 다음 Terrain 검사
            }

            return terrain.SampleHeight(new Vector3(worldX, origin.y, worldZ)) + origin.y; // 실제 Terrain 표면 월드 Y 반환
        }

        return fallback; // Terrain을 찾지 못한 경우 기존 높이 사용
    }

    private static GameObject CreateLocalCylinder(Transform parent, string name, Vector3 localPosition, Vector3 scale, string materialKey, bool solid) // 부모 기준 로컬 좌표 원통 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 원통 형상 생성
        item.name = name; // 용도 이름 적용
        item.transform.SetParent(parent, false); // 부모 로컬 좌표 기준 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        item.transform.localScale = scale; // 원통 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = MaterialFor(materialKey); // 공유 재질 연결

        if (!solid) // 장식 전용 여부 확인
        {
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 불필요한 충돌 제거
        }

        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // 정적 구조 최적화 플래그 적용
        return item; // 생성 원통 반환
    }

    private static BoxCollider[] FindBuildingColliders(Transform block) // 블록 안 실제 건물 MainCollision 조회
    {
        List<BoxCollider> result = new List<BoxCollider>(); // 결과 목록 생성
        BoxCollider[] colliders = block.GetComponentsInChildren<BoxCollider>(true); // 블록 하위 BoxCollider 전체 조회

        foreach (BoxCollider collider in colliders) // 모든 충돌체 순회
        {
            if (collider != null && collider.name == "MainCollision") // 건물 본체 충돌체 확인
            {
                result.Add(collider); // 실제 건물 목록에 추가
            }
        }

        return result.ToArray(); // 안전한 배열 반환
    }

    private static bool TryBlockCoordinate(string name, out int x, out int z) // Block_00_00 이름에서 좌표 추출
    {
        x = -1; // 실패 기본 X 값 설정
        z = -1; // 실패 기본 Z 값 설정
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Block_")) // 블록 이름 형식 확인
        {
            return false; // 다른 노드 제외
        }

        string[] parts = name.Split('_'); // 이름 요소 분리
        return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z); // X Z 숫자 변환 결과 반환
    }

    private static void DeleteGeneratedAsset(string path) // 이전 자동 생성 에셋 제거
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) // 기존 에셋 존재 확인
        {
            AssetDatabase.DeleteAsset(path); // 재생성 가능한 자동 생성 에셋 제거
        }
    }

    private static Transform CreateWorldNode(Transform parent, string name, Vector3 worldPosition) // 월드 좌표 기준 빈 노드 생성
    {
        GameObject node = new GameObject(name); // 새 빈 오브젝트 생성
        node.transform.SetParent(parent, true); // 월드 좌표를 유지하며 부모 연결
        node.transform.position = worldPosition; // 정확한 월드 위치 적용
        return node.transform; // 생성 Transform 반환
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, string materialKey, bool solid) // 로컬 좌표 상자 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); // 큐브 형상 생성
        item.name = name; // 용도 이름 적용
        item.transform.SetParent(parent, false); // 부모 로컬 좌표 연결
        item.transform.localPosition = localPosition; // 로컬 위치 적용
        item.transform.localScale = size; // 실제 미터 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = MaterialFor(materialKey); // 공유 재질 연결

        if (!solid) // 장식 전용 여부 확인
        {
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 불필요한 충돌 제거
        }

        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // 정적 구조 최적화 플래그 적용
        return item; // 생성 오브젝트 반환
    }

    private static GameObject CreateWorldBox(Transform parent, string name, Vector3 worldPosition, Vector3 size, string materialKey, bool solid) // 월드 좌표 상자 생성
    {
        GameObject item = CreateBox(parent, name, Vector3.zero, size, materialKey, solid); // 공통 큐브 생성
        item.transform.position = worldPosition; // 월드 위치 덮어쓰기
        return item; // 생성 오브젝트 반환
    }

    private static GameObject CreateCylinder(Transform parent, string name, Vector3 worldPosition, Vector3 scale, string materialKey, bool solid) // 월드 좌표 원통 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 원통 형상 생성
        item.name = name; // 용도 이름 적용
        item.transform.SetParent(parent, true); // 월드 좌표 유지 부모 연결
        item.transform.position = worldPosition; // 월드 위치 적용
        item.transform.localScale = scale; // 원통 크기 적용
        item.GetComponent<Renderer>().sharedMaterial = MaterialFor(materialKey); // 공유 재질 연결

        if (!solid) // 장식 전용 여부 확인
        {
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 불필요한 충돌 제거
        }

        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // 정적 구조 최적화 플래그 적용
        return item; // 생성 원통 반환
    }

    private static Material MaterialFor(string key) // 공통 도시 재질 조회 또는 생성
    {
        if (materials.TryGetValue(key, out Material cached) && cached != null) // 현재 세션 재질 캐시 확인
        {
            return cached; // 기존 캐시 재사용
        }

        string path = MaterialFolder + "/Day23_" + key + ".mat"; // 재질 에셋 경로 계산
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 생성 재질 조회
        if (existing != null) // 기존 재질 존재 확인
        {
            materials[key] = existing; // 캐시에 등록
            return existing; // 기존 재질 재사용
        }

        Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 렌더 파이프라인 Lit 셰이더 조회
        if (shader == null) // 셰이더 존재 확인
        {
            throw new InvalidOperationException("Day23 도시 재질용 Lit 셰이더를 찾지 못했습니다."); // 렌더 오류 방지
        }

        Material material = new Material(shader); // 새 공유 재질 생성
        material.name = "Day23_" + key; // 재질 이름 설정
        Color color = ColorFor(key); // 재질 색상 조회

        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // URP 기본 색상 적용
        }

        if (material.HasProperty("_Color")) // Standard 색상 속성 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }

        if (IsEmission(key)) // 발광 재질 여부 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
            if (material.HasProperty("_EmissionColor")) // 발광 색상 속성 확인
            {
                material.SetColor("_EmissionColor", color * 3.1f); // 네온 발광 색상 적용
            }
        }

        material.enableInstancing = true; // 반복 소품 GPU 인스턴싱 허용
        AssetDatabase.CreateAsset(material, path); // 재질 영구 에셋 저장
        materials[key] = material; // 현재 세션 캐시에 등록
        return material; // 생성 재질 반환
    }

    private static Color ColorFor(string key) // 재질 이름별 색상 정의
    {
        switch (key) // 재질 이름 분기
        {
            case "TowerDark": return new Color(0.07f, 0.09f, 0.12f, 1f); // 어두운 고층 외장
            case "TowerSteel": return new Color(0.18f, 0.22f, 0.27f, 1f); // 금속 고층 외장
            case "TowerBlue": return new Color(0.10f, 0.17f, 0.24f, 1f); // 기업형 청색 외장
            case "Concrete": return new Color(0.20f, 0.21f, 0.22f, 1f); // 콘크리트 구조
            case "Steel": return new Color(0.25f, 0.29f, 0.33f, 1f); // 일반 금속
            case "Rust": return new Color(0.34f, 0.18f, 0.10f, 1f); // 녹슨 화물과 설비
            case "Pipe": return new Color(0.18f, 0.24f, 0.25f, 1f); // 배관 금속
            case "Wet": return new Color(0.03f, 0.08f, 0.10f, 1f); // 젖은 바닥
            case "Dark": return new Color(0.025f, 0.03f, 0.04f, 1f); // 선로 바닥
            case "Train": return new Color(0.13f, 0.16f, 0.19f, 1f); // 지하철 차체
            case "NeonCyan": return new Color(0.04f, 0.95f, 1f, 1f); // 청록 네온
            case "NeonMagenta": return new Color(1f, 0.08f, 0.52f, 1f); // 자홍 네온
            case "WindowBlue": return new Color(0.08f, 0.42f, 0.75f, 1f); // 야간 창문
            case "Warning": return new Color(1f, 0.48f, 0.04f, 1f); // 경고 조명
            case "LampPole": return new Color(0.22f, 0.25f, 0.29f, 1f); // 가로등 기둥
            case "WarmLight": return new Color(1.00f, 0.92f, 0.68f, 1f); // 가로등 조명
            default: return new Color(0.45f, 0.45f, 0.45f, 1f); // 알 수 없는 재질 기본값
        }
    }

    private static bool IsEmission(string key) // 발광 재질 여부 판정
    {
        return key == "NeonCyan" || key == "NeonMagenta" || key == "WindowBlue" || key == "Warning" || key == "WarmLight"; // 네온과 창문과 가로등 발광 사용
    }

    private static int StableHash(string value) // 실행마다 같은 문자열 해시 계산
    {
        unchecked // 정수 오버플로 허용
        {
            int hash = 23; // 초기 해시값 설정

            foreach (char character in value ?? string.Empty) // 문자열 문자 순회
            {
                hash = hash * 31 + character; // 안정적인 해시 누적
            }

            return hash; // 최종 해시 반환
        }
    }

    private static Transform FindByName(Transform root, string targetName) // 하위 계층 이름 검색
    {
        if (root == null) // 검색 루트 확인
        {
            return null; // 검색 실패 반환
        }

        if (root.name == targetName) // 현재 오브젝트 이름 확인
        {
            return root; // 현재 결과 반환
        }

        for (int i = 0; i < root.childCount; i++) // 자식 순회
        {
            Transform result = FindByName(root.GetChild(i), targetName); // 재귀 검색
            if (result != null) // 검색 성공 확인
            {
                return result; // 첫 결과 반환
            }
        }

        return null; // 전체 검색 실패 반환
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 대상 씬 단일 컴포넌트 검색
    {
        T found = null; // 첫 결과 저장

        foreach (GameObject rootObject in scene.GetRootGameObjects()) // 씬 루트 오브젝트 순회
        {
            foreach (T item in rootObject.GetComponentsInChildren<T>(true)) // 비활성 자식까지 검색
            {
                if (found != null) // 두 번째 결과 확인
                {
                    throw new InvalidOperationException(typeof(T).Name + "가 두 개 이상 있습니다."); // 중복 구성 보고
                }

                found = item; // 첫 결과 저장
            }
        }

        return found; // 단일 결과 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 요소 분리
        string current = parts[0]; // Assets 시작 경로 설정

        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 경로 생성

            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            }

            current = next; // 다음 부모 경로 갱신
        }
    }

    private sealed class SubwayBuildResult // 지하철 생성 결과 묶음
    {
        public Terrain Terrain; // 생성한 지하 Terrain
        public bool StairsConnected; // 실제 계단 연결 여부
    }
}
#endif
