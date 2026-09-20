#if UNITY_EDITOR // Day27 V3 도시 복구 전용
using System; // 날짜·난수·오류 처리
using System.Collections.Generic; // Prefab·점유 영역 목록
using System.IO; // 씬 백업 처리
using ProjectK.Day16; // MapWorldRoot 참조
using ProjectK.Day27; // Day27 복구 표식 참조
using UnityEditor; // PrefabUtility·SerializedObject·Undo 사용
using UnityEditor.SceneManagement; // 씬 열기·저장 이벤트 처리
using UnityEngine; // 건물 모델과 Bounds 처리
using UnityEngine.Rendering; // URP 재질 셰이더 확인
using UnityEngine.SceneManagement; // 활성 씬 조회

[InitializeOnLoad] // Unity가 안전해질 때까지 update에서 복구 시도
public static class ProjectKDay27CityRecovery // 기존 건물이 사라진 Map을 완성형 V3 Prefab 건물로 안전 복구
{
    private enum BuildingBand // 에디터 내부 건물 높이 분류
    {
        Low, // 저층
        Mid, // 중층
        High, // 고층
        Tower // 초고층
    }

    private sealed class BuildingAsset // 생성된 V3 Prefab과 실제 배치 크기
    {
        public GameObject Prefab; // 실제 Prefab 에셋
        public BuildingBand Band; // 높이 분류
        public float Width; // 배치 폭
        public float Depth; // 배치 깊이
        public float Height; // 건물 높이
    }

    private struct Spec // 직접 생성할 건물 규격
    {
        public string Name; // 에셋 이름
        public BuildingBand Band; // 높이 분류
        public float Width; // 폭
        public float Depth; // 깊이
        public float Height; // 높이
        public int Style; // 외형 패턴
        public string Body; // 본체 재질 키
        public string Accent; // 네온 포인트 키

        public Spec(string name, BuildingBand band, float width, float depth, float height, int style, string body, string accent) // 규격 생성
        {
            Name = name; // 이름 저장
            Band = band; // 분류 저장
            Width = width; // 폭 저장
            Depth = depth; // 깊이 저장
            Height = height; // 높이 저장
            Style = style; // 스타일 저장
            Body = body; // 본체 재질 저장
            Accent = accent; // 포인트 재질 저장
        }
    }

    private const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 본편 씬
    private const string BackupFolder = "Assets/_Project/Backups/Day27CityRecovery"; // 복구 전 백업 폴더
    private const string GeneratedRoot = "Assets/_Project/Generated/Map27/V3"; // V3 생성 자료 루트
    private const string BuildingFolder = GeneratedRoot + "/Buildings"; // V3 건물 Prefab 폴더
    private const string MaterialFolder = GeneratedRoot + "/Materials"; // V3 재질 폴더
    private const string SourceCommit = "3780e7b71506febdbe7b021f73d0ed7cea5d8d4d"; // 최신 원격 기준 커밋
    private const int Revision = 3; // 현재 복구 버전
    private const int Seed = 27331; // 재현 가능한 배치 시드
    private const float LotMargin = 3f; // 블록 경계 내부 여유
    private const float BuildingGap = 4.5f; // 건물 사이 최소 간격
    private static bool applying; // 현재 복구 실행 여부
    private static bool sessionFinished; // 이번 Editor 세션 처리 완료 여부
    private static double nextCheck; // 다음 안전 상태 검사 시각

    static ProjectKDay27CityRecovery() // Editor 시작 시 지속 검사 등록
    {
        EditorApplication.update -= Tick; // 중복 update 등록 방지
        EditorApplication.update += Tick; // Unity가 안전해질 때까지 반복 검사
        EditorSceneManager.sceneOpened -= OnSceneOpened; // 중복 씬 이벤트 제거
        EditorSceneManager.sceneOpened += OnSceneOpened; // Map 재열기 시 다시 검사
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) // Map 씬 재열기 처리
    {
        if (scene.IsValid() && scene.path == MapScenePath) // 대상 Map 확인
        {
            sessionFinished = false; // 실제 도시 상태를 다시 검사하도록 허용
            nextCheck = 0d; // 즉시 검사 예약
        }
    }

    private static void Tick() // 컴파일·임포트가 끝날 때까지 포기하지 않고 대기
    {
        if (sessionFinished || applying) // 이미 정상 완료됐거나 현재 실행 중인지 확인
        {
            return; // 중복 처리 방지
        }

        if (EditorApplication.timeSinceStartup < nextCheck) // 검사 간격 확인
        {
            return; // 다음 검사 대기
        }

        nextCheck = EditorApplication.timeSinceStartup + 0.5d; // 다음 검사 예약

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 편집 가능한 안전 상태 확인
        {
            return; // 기존 방식과 달리 update 연결을 유지하고 다음 프레임에 다시 검사
        }

        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // Map 씬인지 확인
        {
            return; // 다른 씬에서는 대기
        }

        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 본편 월드 조회
        if (world == null) // 월드 구성 확인
        {
            return; // 씬 로딩이 끝나지 않았으면 대기
        }

        Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 일반 도시 블록 루트 조회
        if (blocks == null) // 도시 구성 확인
        {
            Debug.LogError("[Day27 V3] Blocks_12x12를 찾지 못했습니다."); // 구성 오류 보고
            sessionFinished = true; // 반복 로그 방지
            return; // 처리 중단
        }

        if (!NeedsRecovery(world, blocks)) // 실제 보이는 건물 수와 구형 장식 상태 확인
        {
            sessionFinished = true; // 현재 Map은 이미 정상 상태
            return; // 아무 수정도 하지 않음
        }

        try // 실제 복구 실행
        {
            Apply(scene, world, blocks); // 안전한 V3 도시 복구
            sessionFinished = true; // 정상 완료 후 세션 검사 종료
        }
        catch (Exception error) // 복구 실패 처리
        {
            Debug.LogException(error); // 실제 실패 원인 출력
            Debug.LogError("[Day27 V3] 자동 도시 복구에 실패했습니다. 기존 Map은 Undo로 되돌렸습니다."); // 안전 상태 안내
            sessionFinished = true; // 같은 세션에서 반복 파괴 방지
        }
    }

    private static bool NeedsRecovery(MapWorldRoot world, Transform blocks) // 현재 도시가 실제로 복구가 필요한지 판정
    {
        int normalBlocks = 0; // 일반 블록 수
        int healthyBlocks = 0; // 본체가 보이는 건물이 두 동 이상인 블록 수

        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlockCoordinate(block.name, out int x, out int z) || Reserved(x, z)) // 일반 블록 여부 확인
            {
                continue; // 랜드마크 예약지 제외
            }

            normalBlocks++; // 일반 블록 수 증가
            if (CountVisibleBuildings(block) >= 2) // 실제 본체가 보이는 건물이 최소 두 동인지 확인
            {
                healthyBlocks++; // 정상 블록 수 증가
            }
        }

        bool missingBuildings = normalBlocks == 0 || healthyBlocks < normalBlocks; // 하나라도 건물이 사라진 블록이 있는지 확인
        bool legacyDetails = world.DetailVisuals != null && world.DetailVisuals.Length > 0; // 이동 전 좌표로 baked된 Day16 장식이 남아 있는지 확인
        return missingBuildings || legacyDetails; // 실제 문제가 있을 때만 복구
    }

    private static int CountVisibleBuildings(Transform block) // 블록 안 실제 본체가 표시 가능한 건물 수 집계
    {
        int count = 0; // 건물 수 초기화

        for (int i = 0; i < block.childCount; i++) // 직접 자식 순회
        {
            Transform child = block.GetChild(i); // 현재 자식 조회
            if (child == null) // 누락 자식 확인
            {
                continue; // 다음 자식
            }

            Transform main = child.Find("MainCollision"); // 건물 본체 조회
            if (main == null) // 건물 구조가 아닌 자식 확인
            {
                continue; // 제외
            }

            MeshFilter filter = main.GetComponent<MeshFilter>(); // 실제 메시 조회
            MeshRenderer renderer = main.GetComponent<MeshRenderer>(); // 실제 렌더러 조회
            if (filter != null && filter.sharedMesh != null && renderer != null && renderer.enabled && renderer.sharedMaterial != null) // 본체 표시 가능 여부 확인
            {
                count++; // 정상 건물 집계
            }
        }

        return count; // 본체가 있는 건물 수 반환
    }

    private static void Apply(Scene scene, MapWorldRoot world, Transform blocks) // 먼저 새 도시를 생성·검증한 뒤 기존 요소를 교체
    {
        EnsureFolder(BackupFolder); // 백업 폴더 준비
        EnsureFolder(GeneratedRoot); // 생성 자료 루트 준비
        EnsureFolder(BuildingFolder); // 건물 Prefab 폴더 준비
        EnsureFolder(MaterialFolder); // 재질 폴더 준비

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"); // 고유 백업 시각 생성
        string backup = BackupFolder + "/Map_before_city_recovery_v3_" + stamp + ".unity"; // 백업 경로 생성
        if (!EditorSceneManager.SaveScene(scene, backup, true)) // 수정 전 씬 복사 백업
        {
            throw new IOException("Day27 V3 복구 전 Map 백업에 실패했습니다."); // 백업 없는 변경 차단
        }

        applying = true; // 중복 실행 차단
        Undo.IncrementCurrentGroup(); // 전체 복구 Undo 범위 시작
        int undoGroup = Undo.GetCurrentGroup(); // Undo 번호 저장
        Undo.SetCurrentGroupName("Day27 Recover City V3"); // 실행 취소 이름 지정

        try // 안전한 staged 복구 실행
        {
            Dictionary<string, Material> materials = CreateMaterials(); // V3 건물 재질 준비
            List<BuildingAsset> library = CreateBuildingLibrary(materials); // 본체 포함 V3 건물 Prefab 생성·검증
            RemoveStagingBuildings(blocks); // 이전 실패에서 남은 RecoveryV3만 정리
            System.Random random = new System.Random(Seed); // 재현 가능한 배치 난수 생성
            int stagedBuildings = StageEntireCity(blocks, library, random); // 기존 건물을 건드리기 전에 V3 도시 전체 생성
            int normalBlocks = CountNormalBlocks(blocks); // 일반 블록 수 계산
            int stagedHealthy = CountHealthyStagedBlocks(blocks); // V3 건물이 두 동 이상 생성된 블록 수 확인

            if (normalBlocks <= 0 || stagedHealthy != normalBlocks) // 모든 일반 블록 복구 성공 여부 확인
            {
                throw new InvalidOperationException("V3 건물 사전 생성 검증 실패 · 일반 블록 " + normalBlocks + " / 정상 V3 블록 " + stagedHealthy); // 기존 건물 제거 전 즉시 중단
            }

            RemoveOldBuildingsAfterStaging(blocks); // 새 건물이 완성된 뒤에만 구형 건물 제거
            RenameStagedBuildings(blocks); // RecoveryV3 이름을 최종 BuildingV3 이름으로 변경
            int removedDetails = RemoveLegacyDetails(world); // 새 건물 확정 후 공중에 남는 baked 장식 제거
            RemoveDetachedLayers(world); // Day17·18·23의 이전 건물 좌표 종속 레이어 제거
            int warnings = ValidateFinalCity(world, blocks); // 최종 Prefab·본체·블록 경계 검수

            Map27CityRecoveryMarker marker = world.GetComponent<Map27CityRecoveryMarker>(); // 기존 V3 복구 표식 조회
            if (marker == null) marker = Undo.AddComponent<Map27CityRecoveryMarker>(world.gameObject); // 표식 없으면 추가
            marker.Configure(Revision, SourceCommit, normalBlocks, stagedBuildings, library.Count, removedDetails, warnings); // 복구 결과 저장
            EditorUtility.SetDirty(marker); // 씬 저장 대상으로 표시
            EditorUtility.SetDirty(world); // DetailVisuals 참조 변경 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 변경 표시
            AssetDatabase.SaveAssets(); // Prefab·재질 저장
            AssetDatabase.Refresh(); // Project 창 갱신

            if (!EditorSceneManager.SaveScene(scene)) // 최종 Map 저장
            {
                throw new IOException("Day27 V3 복구 Map 저장에 실패했습니다."); // 저장 실패 보고
            }

            Undo.CollapseUndoOperations(undoGroup); // 전체 작업을 한 번의 Undo로 결합
            Debug.Log("[Day27 V3] 도시 건물 복구 완료 · 일반 블록 " + normalBlocks + " · 건물 " + stagedBuildings + " · Prefab " + library.Count + "종 · 제거 장식 " + removedDetails + " · 경고 " + warnings + " · 백업: " + backup); // 결과 출력
        }
        catch // 오류 시 기존 Map 보존
        {
            Undo.RevertAllDownToGroup(undoGroup); // staged 건물과 부분 변경 전체 되돌리기
            throw; // 실제 원인을 상위 Tick으로 전달
        }
        finally // 실행 상태 정리
        {
            applying = false; // 다음 처리 허용
        }
    }

    private static Dictionary<string, Material> CreateMaterials() // 야간에서도 건물 본체가 읽히는 V3 재질 생성
    {
        Dictionary<string, Material> result = new Dictionary<string, Material>(); // 재질 사전 생성
        result["BodyLight"] = GetMaterial("V3_BodyLight", new Color(0.36f, 0.40f, 0.48f), false); // 밝은 본체
        result["BodyDark"] = GetMaterial("V3_BodyDark", new Color(0.16f, 0.20f, 0.28f), false); // 어두운 본체
        result["Steel"] = GetMaterial("V3_Steel", new Color(0.22f, 0.27f, 0.34f), false); // 금속 구조
        result["Glass"] = GetMaterial("V3_Glass", new Color(0.07f, 0.30f, 0.43f), false); // 청색 창
        result["Door"] = GetMaterial("V3_Door", new Color(0.035f, 0.055f, 0.075f), false); // 출입구
        result["Cyan"] = GetMaterial("V3_Cyan", new Color(0.04f, 0.82f, 1f), true); // 청록 네온
        result["Amber"] = GetMaterial("V3_Amber", new Color(1f, 0.44f, 0.06f), true); // 주황 네온
        result["Magenta"] = GetMaterial("V3_Magenta", new Color(0.92f, 0.08f, 0.56f), true); // 자홍 네온
        return result; // 재질 사전 반환
    }

    private static Material GetMaterial(string name, Color color, bool emission) // V3 재질 생성·재사용
    {
        string path = MaterialFolder + "/" + name + ".mat"; // 에셋 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 에셋 조회

        if (material == null) // 첫 생성 확인
        {
            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 파이프라인 Lit 조회
            if (shader == null) shader = Shader.Find("Standard"); // Built-in 대체
            if (shader == null) shader = Shader.Find("Unlit/Color"); // 최종 대체 셰이더
            material = new Material(shader); // 새 재질 생성
            material.name = name; // 이름 지정
            AssetDatabase.CreateAsset(material, path); // 영구 저장
        }

        material.color = color; // 기본 색상 적용
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP 색상 적용
        if (material.HasProperty("_Color")) material.SetColor("_Color", color); // Built-in 색상 적용
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", name.Contains("Glass") ? 0.62f : 0.20f); // 표면 광택 차이
        material.enableInstancing = true; // 반복 Prefab 인스턴싱 활성화

        if (emission) // 네온 재질 확인
        {
            material.EnableKeyword("_EMISSION"); // 발광 활성화
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.4f); // 발광 색 적용
        }
        else // 일반 외벽 처리
        {
            material.DisableKeyword("_EMISSION"); // 발광 제거
        }

        EditorUtility.SetDirty(material); // 에셋 저장 표시
        return material; // 준비된 재질 반환
    }

    private static List<BuildingAsset> CreateBuildingLibrary(Dictionary<string, Material> materials) // 본체·창문·문·옥상 구조가 모두 Prefab 자식인 12종 생성
    {
        Spec[] specs = new Spec[] // V3 건물 규격 정의
        {
            new Spec("BuildingV3_Low_A", BuildingBand.Low, 18f, 20f, 10f, 0, "BodyLight", "Cyan"), // 저층 A
            new Spec("BuildingV3_Low_B", BuildingBand.Low, 22f, 16f, 13f, 1, "BodyDark", "Amber"), // 저층 B
            new Spec("BuildingV3_Low_C", BuildingBand.Low, 17f, 23f, 16f, 2, "BodyLight", "Magenta"), // 저층 C
            new Spec("BuildingV3_Mid_A", BuildingBand.Mid, 21f, 23f, 24f, 0, "BodyDark", "Cyan"), // 중층 A
            new Spec("BuildingV3_Mid_B", BuildingBand.Mid, 25f, 19f, 30f, 1, "BodyLight", "Amber"), // 중층 B
            new Spec("BuildingV3_Mid_C", BuildingBand.Mid, 19f, 27f, 36f, 2, "BodyDark", "Magenta"), // 중층 C
            new Spec("BuildingV3_Mid_D", BuildingBand.Mid, 26f, 21f, 40f, 3, "BodyLight", "Cyan"), // 중층 D
            new Spec("BuildingV3_High_A", BuildingBand.High, 23f, 25f, 50f, 0, "BodyDark", "Cyan"), // 고층 A
            new Spec("BuildingV3_High_B", BuildingBand.High, 27f, 21f, 60f, 1, "BodyLight", "Amber"), // 고층 B
            new Spec("BuildingV3_High_C", BuildingBand.High, 21f, 28f, 68f, 2, "BodyDark", "Magenta"), // 고층 C
            new Spec("BuildingV3_Tower_A", BuildingBand.Tower, 23f, 23f, 82f, 3, "BodyLight", "Cyan"), // 타워 A
            new Spec("BuildingV3_Tower_B", BuildingBand.Tower, 27f, 19f, 96f, 4, "BodyDark", "Magenta") // 타워 B
        };

        List<BuildingAsset> library = new List<BuildingAsset>(); // 최종 에셋 목록

        foreach (Spec spec in specs) // 모든 건물 규격 순회
        {
            string path = BuildingFolder + "/" + spec.Name + ".prefab"; // Prefab 경로 계산
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 기존 V3 Prefab 조회

            if (prefab == null || !HasVisibleMainBody(prefab)) // 없거나 본체가 손상된 에셋 확인
            {
                if (prefab != null) AssetDatabase.DeleteAsset(path); // 손상 에셋 제거
                GameObject source = BuildBuilding(spec, materials); // 완성형 임시 건물 생성
                prefab = PrefabUtility.SaveAsPrefabAsset(source, path); // 실제 Prefab 저장
                UnityEngine.Object.DestroyImmediate(source); // 임시 오브젝트 제거
            }

            if (prefab == null || !HasVisibleMainBody(prefab)) // 저장 후 본체 재검증
            {
                throw new InvalidOperationException(spec.Name + "의 MainCollision 렌더러 생성 실패"); // 빈 건물 Prefab 차단
            }

            BuildingAsset asset = new BuildingAsset(); // 에디터 배치 정보 생성
            asset.Prefab = prefab; // Prefab 연결
            asset.Band = spec.Band; // 높이 분류 저장
            asset.Width = spec.Width + 1.6f; // 외벽 포인트와 차양을 포함한 안전 폭
            asset.Depth = spec.Depth + 2.2f; // 출입구 차양을 포함한 안전 깊이
            asset.Height = spec.Height; // 본체 높이 저장
            library.Add(asset); // 라이브러리 등록
        }

        return library; // V3 건물 목록 반환
    }

    private static GameObject BuildBuilding(Spec spec, Dictionary<string, Material> materials) // 완성형 건물 모델 직접 생성
    {
        GameObject root = new GameObject(spec.Name); // Prefab 루트 생성
        root.transform.position = Vector3.zero; // 원점 정렬
        root.transform.rotation = Quaternion.identity; // 기본 방향
        root.transform.localScale = Vector3.one; // 루트 스케일 고정
        int parkourLayer = LayerMask.NameToLayer("ParkourSurface"); // 파쿠르 레이어 조회

        GameObject body = Cube(root.transform, "MainCollision", new Vector3(0f, spec.Height * 0.5f, 0f), new Vector3(spec.Width, spec.Height, spec.Depth), materials[spec.Body], true); // 항상 보이는 본체 생성
        if (parkourLayer >= 0) body.layer = parkourLayer; // 벽 이동 가능한 본체 지정

        Cube(root.transform, "Foundation", new Vector3(0f, 0.25f, 0f), new Vector3(spec.Width + 0.8f, 0.5f, spec.Depth + 0.8f), materials["Steel"], true); // 지면 기반 생성
        Cube(root.transform, "RoofCap", new Vector3(0f, spec.Height + 0.18f, 0f), new Vector3(spec.Width + 0.35f, 0.36f, spec.Depth + 0.35f), materials["Steel"], true); // 옥상 마감 생성
        Cube(root.transform, "Entrance", new Vector3(0f, 1.55f, -spec.Depth * 0.5f - 0.09f), new Vector3(Mathf.Min(3.4f, spec.Width * 0.22f), 3.1f, 0.18f), materials["Door"], false); // 출입구 생성
        Cube(root.transform, "Awning", new Vector3(0f, 3.4f, -spec.Depth * 0.5f - 0.72f), new Vector3(Mathf.Min(6f, spec.Width * 0.38f), 0.20f, 1.35f), materials[spec.Accent], false); // 전면 차양 생성
        Cube(root.transform, "RoofPlant", new Vector3(-spec.Width * 0.22f, spec.Height + 1.05f, spec.Depth * 0.16f), new Vector3(Mathf.Clamp(spec.Width * 0.24f, 3f, 5.5f), 2.1f, Mathf.Clamp(spec.Depth * 0.22f, 3f, 5.5f)), materials["Steel"], true); // 옥상 설비 생성

        int rows = Mathf.Clamp(Mathf.RoundToInt(spec.Height / 5.5f), 2, 14); // 높이에 맞는 창문 층 수

        for (int row = 0; row < rows; row++) // 창문 반복
        {
            float t = rows <= 1 ? 0f : row / (float)(rows - 1); // 층 비율 계산
            float y = Mathf.Lerp(3.0f, spec.Height - 2.5f, t); // 현재 창 높이 계산
            float frontWidth = spec.Width * (spec.Style % 2 == 0 ? 0.72f : 0.56f); // 스타일별 창문 폭
            Cube(root.transform, "WindowFront_" + row, new Vector3(0f, y, -spec.Depth * 0.5f - 0.07f), new Vector3(frontWidth, 0.42f, 0.10f), materials["Glass"], false); // 전면 창
            Cube(root.transform, "WindowBack_" + row, new Vector3(0f, y, spec.Depth * 0.5f + 0.07f), new Vector3(frontWidth, 0.42f, 0.10f), materials["Glass"], false); // 후면 창

            if ((row + spec.Style) % 2 == 0) // 일부 층 측면 창 생성
            {
                Cube(root.transform, "WindowLeft_" + row, new Vector3(-spec.Width * 0.5f - 0.07f, y, 0f), new Vector3(0.10f, 0.40f, spec.Depth * 0.58f), materials["Glass"], false); // 좌측 창
                Cube(root.transform, "WindowRight_" + row, new Vector3(spec.Width * 0.5f + 0.07f, y, 0f), new Vector3(0.10f, 0.40f, spec.Depth * 0.58f), materials["Glass"], false); // 우측 창
            }
        }

        float accentX = spec.Style % 2 == 0 ? spec.Width * 0.34f : -spec.Width * 0.34f; // 네온 위치 결정
        Cube(root.transform, "FacadeAccent", new Vector3(accentX, spec.Height * 0.52f, -spec.Depth * 0.5f - 0.14f), new Vector3(0.20f, spec.Height * 0.72f, 0.12f), materials[spec.Accent], false); // 수직 네온 생성

        if (spec.Band == BuildingBand.High || spec.Band == BuildingBand.Tower) // 고층 상단 구조 확인
        {
            float crownHeight = spec.Band == BuildingBand.Tower ? 5.5f : 3.5f; // 상단 구조 높이
            Cube(root.transform, "Crown", new Vector3(0f, spec.Height + crownHeight * 0.5f + 0.38f, 0f), new Vector3(spec.Width * 0.55f, crownHeight, spec.Depth * 0.55f), materials["Steel"], true); // 상단 기계층
            Cube(root.transform, "CrownLight", new Vector3(0f, spec.Height + crownHeight + 0.46f, -spec.Depth * 0.18f), new Vector3(spec.Width * 0.48f, 0.28f, 0.20f), materials[spec.Accent], false); // 상단 네온
            Antenna(root.transform, spec.Height + crownHeight + 0.6f, materials["Steel"], materials[spec.Accent]); // 안테나
        }
        else if (spec.Style >= 2) // 일부 저·중층 안테나
        {
            Antenna(root.transform, spec.Height + 0.4f, materials["Steel"], materials[spec.Accent]); // 작은 통신장치
        }

        if (spec.Style == 1 || spec.Style == 4) // 일부 건물 부속동 추가
        {
            float annexHeight = Mathf.Min(spec.Height * 0.42f, 16f); // 부속동 높이
            GameObject annex = Cube(root.transform, "SideAnnex", new Vector3(spec.Width * 0.43f, annexHeight * 0.5f, spec.Depth * 0.26f), new Vector3(spec.Width * 0.34f, annexHeight, spec.Depth * 0.30f), materials["Steel"], true); // 부속 볼륨
            if (parkourLayer >= 0) annex.layer = parkourLayer; // 부속동 파쿠르 적용
        }

        return root; // 완성 건물 반환
    }

    private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool solid) // 실제 Cube 메시 부품 생성
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); // 기본 Cube 생성
        item.name = name; // 이름 적용
        item.transform.SetParent(parent, false); // Prefab 계층 연결
        item.transform.localPosition = localPosition; // 위치 적용
        item.transform.localRotation = Quaternion.identity; // 기본 회전
        item.transform.localScale = scale; // 실제 미터 크기 적용
        Renderer renderer = item.GetComponent<Renderer>(); // MeshRenderer 조회
        if (renderer != null) renderer.sharedMaterial = material; // 재질 연결
        Collider collider = item.GetComponent<Collider>(); // 기본 충돌체 조회

        if (!solid && collider != null) // 장식 요소 확인
        {
            UnityEngine.Object.DestroyImmediate(collider); // 불필요한 충돌 제거
        }

        GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // 정적 도시 요소 최적화
        return item; // 생성 부품 반환
    }

    private static void Antenna(Transform parent, float baseHeight, Material steel, Material accent) // 옥상 안테나 생성
    {
        GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 원통형 기둥 생성
        mast.name = "AntennaMast"; // 이름 적용
        mast.transform.SetParent(parent, false); // 건물 연결
        mast.transform.localPosition = new Vector3(0f, baseHeight + 2.2f, 0f); // 위치 적용
        mast.transform.localScale = new Vector3(0.18f, 2.2f, 0.18f); // 크기 적용
        Renderer renderer = mast.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null) renderer.sharedMaterial = steel; // 금속 재질 적용
        Collider collider = mast.GetComponent<Collider>(); // 기본 충돌체 조회
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider); // 얇은 안테나 충돌 제거
        Cube(parent, "AntennaLight", new Vector3(0f, baseHeight + 4.55f, 0f), new Vector3(0.42f, 0.22f, 0.42f), accent, false); // 상단 발광 표시 생성
    }

    private static bool HasVisibleMainBody(GameObject prefab) // Prefab 본체 렌더링 유효성 확인
    {
        if (prefab == null) return false; // Prefab 존재 확인
        Transform main = prefab.transform.Find("MainCollision"); // 본체 조회
        if (main == null) return false; // 본체 누락
        MeshFilter filter = main.GetComponent<MeshFilter>(); // 메시 조회
        MeshRenderer renderer = main.GetComponent<MeshRenderer>(); // 렌더러 조회
        return filter != null && filter.sharedMesh != null && renderer != null && renderer.enabled && renderer.sharedMaterial != null; // 실제 표시 가능 여부 반환
    }

    private static int StageEntireCity(Transform blocks, List<BuildingAsset> library, System.Random random) // 구형 건물을 남긴 채 V3 건물을 먼저 생성
    {
        int created = 0; // 전체 생성 수

        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록 조회
            if (!TryBlockCoordinate(block.name, out int x, out int z) || Reserved(x, z)) // 일반 블록 여부 확인
            {
                continue; // 예약지 보존
            }

            Transform lot = block.Find("PavedLot"); // 실제 부지 조회
            if (lot == null) throw new InvalidOperationException(block.name + "의 PavedLot가 없습니다."); // 필수 부지 누락 차단

            Bounds lotBounds = WorldBounds(lot); // 부지 크기 조회
            int target = BuildingCountForBlock(random, x, z); // 2~5동 목표 수 선택
            Vector2[] slots = Slots(target); // 해당 동수의 안전 슬롯 패턴 조회
            List<Rect> occupied = new List<Rect>(); // 실제 배치 영역 목록

            for (int index = 0; index < target; index++) // 목표 건물 순회
            {
                BuildingBand preferred = PreferredBand(random, x, z, target, index); // 위치와 밀도에 맞는 높이 선택
                List<BuildingAsset> candidates = Candidates(library, preferred, target, random); // 적합한 Prefab 후보 생성
                bool placed = false; // 현재 슬롯 배치 성공 여부

                foreach (BuildingAsset asset in candidates) // 후보 Prefab 순회
                {
                    if (asset == null || asset.Prefab == null) // 잘못된 에셋 제외
                    {
                        continue; // 다음 후보
                    }

                    int rotation = random.Next(0, 2); // 0도·90도 선택
                    float width = rotation == 0 ? asset.Width : asset.Depth; // 회전 후 폭
                    float depth = rotation == 0 ? asset.Depth : asset.Width; // 회전 후 깊이
                    float maxX = lotBounds.size.x * 0.5f - LotMargin - width * 0.5f; // 안전한 X 이동 범위
                    float maxZ = lotBounds.size.z * 0.5f - LotMargin - depth * 0.5f; // 안전한 Z 이동 범위

                    if (maxX <= 0f || maxZ <= 0f) // Prefab이 부지보다 큰지 확인
                    {
                        continue; // 더 작은 후보 시도
                    }

                    Vector2 slot = slots[index]; // 현재 정규화 슬롯 조회
                    Vector2 position = new Vector2(slot.x * maxX, slot.y * maxZ); // 실제 블록 로컬 위치 계산
                    Rect area = Inflated(position, width, depth, BuildingGap); // 최소 간격 포함 점유 영역
                    bool overlap = false; // 겹침 여부 초기화

                    foreach (Rect existing in occupied) // 이미 배치된 영역 검사
                    {
                        if (area.Overlaps(existing)) // 실제 겹침 확인
                        {
                            overlap = true; // 겹침 표시
                            break; // 현재 후보 실패
                        }
                    }

                    if (overlap) // 겹친 후보 확인
                    {
                        continue; // 작은 후보 또는 다른 높이 시도
                    }

                    GameObject instance = PrefabUtility.InstantiatePrefab(asset.Prefab, block) as GameObject; // V3 Prefab 인스턴스 생성
                    if (instance == null) // 생성 실패 확인
                    {
                        continue; // 다음 후보
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "Stage Recovery V3 Building"); // 생성 Undo 등록
                    instance.name = "RecoveryV3_" + index.ToString("D2"); // staged 상태 식별 이름
                    instance.transform.localPosition = new Vector3(position.x, 0f, position.y); // 블록 위치 적용
                    instance.transform.localRotation = Quaternion.Euler(0f, rotation * 90f, 0f); // 방향 적용
                    instance.transform.localScale = Vector3.one; // 모델 비율 그대로 사용
                    occupied.Add(area); // 현재 점유 영역 등록
                    created++; // 전체 생성 수 증가
                    placed = true; // 슬롯 배치 성공
                    break; // 다음 슬롯으로 이동
                }

                if (!placed) // 모든 후보 실패 확인
                {
                    throw new InvalidOperationException(block.name + "의 V3 건물 " + index + "번째 슬롯 배치 실패"); // 구형 건물을 삭제하기 전에 전체 복구 중단
                }
            }
        }

        return created; // staged 건물 수 반환
    }

    private static Vector2[] Slots(int count) // 건물 수마다 서로 다른 안전 배치 패턴
    {
        switch (count) // 목표 동수 분기
        {
            case 2:
                return new Vector2[] { new Vector2(-0.58f, -0.32f), new Vector2(0.54f, 0.38f) }; // 비대칭 2동
            case 3:
                return new Vector2[] { new Vector2(-0.72f, -0.58f), new Vector2(0.72f, -0.48f), new Vector2(0.02f, 0.72f) }; // 삼각 3동
            case 4:
                return new Vector2[] { new Vector2(-0.74f, -0.74f), new Vector2(0.74f, -0.70f), new Vector2(-0.70f, 0.74f), new Vector2(0.72f, 0.72f) }; // 네 모서리 4동
            default:
                return new Vector2[] { new Vector2(-0.88f, -0.88f), new Vector2(0.88f, -0.84f), new Vector2(-0.84f, 0.88f), new Vector2(0.88f, 0.86f), Vector2.zero }; // 네 모서리+중앙 5동
        }
    }

    private static BuildingBand PreferredBand(System.Random random, int x, int z, int count, int index) // 구역별 높이 구성 선택
    {
        int roll = random.Next(0, 100); // 분류 난수

        if (count >= 5) // 밀집 5동 블록
        {
            return index == 4 && roll < 30 ? BuildingBand.Mid : BuildingBand.Low; // 중앙만 가끔 중층, 나머지는 저층
        }

        if (x >= 8) // 동쪽 기업 구역
        {
            if (index == 0 && roll < 38) return BuildingBand.Tower; // 대표 초고층
            if (roll < 68) return BuildingBand.High; // 고층
            if (roll < 92) return BuildingBand.Mid; // 중층
            return BuildingBand.Low; // 저층
        }

        bool central = x >= 4 && x <= 7 && z >= 2 && z <= 10; // 중앙 시가지 확인
        if (central) // 중앙 혼합 지역
        {
            if (index == 0 && roll < 16) return BuildingBand.Tower; // 일부 타워
            if (roll < 40) return BuildingBand.High; // 고층
            if (roll < 78) return BuildingBand.Mid; // 중층
            return BuildingBand.Low; // 저층
        }

        if (roll < 8) return BuildingBand.High; // 외곽 드문 고층
        if (roll < 46) return BuildingBand.Mid; // 외곽 중층
        return BuildingBand.Low; // 외곽 저층
    }

    private static List<BuildingAsset> Candidates(List<BuildingAsset> library, BuildingBand preferred, int count, System.Random random) // 선호 분류와 공간 크기를 반영한 후보 순서
    {
        List<BuildingAsset> first = new List<BuildingAsset>(); // 선호 분류
        List<BuildingAsset> rest = new List<BuildingAsset>(); // 나머지 분류

        foreach (BuildingAsset asset in library) // 전체 V3 Prefab 순회
        {
            if (asset.Band == preferred) first.Add(asset); // 선호 분류 등록
            else rest.Add(asset); // 예비 후보 등록
        }

        Shuffle(first, random); // 선호 분류 선택 순서 분산
        Shuffle(rest, random); // 예비 순서 분산

        if (count >= 4) // 4~5동 밀집 블록 확인
        {
            first.Sort((a, b) => Area(a).CompareTo(Area(b))); // 작은 건물을 먼저 시도
            rest.Sort((a, b) => Area(a).CompareTo(Area(b))); // 예비도 작은 순서
        }

        first.AddRange(rest); // 최종 후보 결합
        return first; // 후보 목록 반환
    }

    private static float Area(BuildingAsset asset) // 건물 평면 면적 계산
    {
        return asset != null ? asset.Width * asset.Depth : float.PositiveInfinity; // 면적 반환
    }

    private static int BuildingCountForBlock(System.Random random, int x, int z) // 일반 블록마다 2~5동 선택
    {
        int roll = random.Next(0, 100); // 분포 난수
        int count = roll < 12 ? 2 : roll < 42 ? 3 : roll < 79 ? 4 : 5; // 3~4동 중심 분포
        if (x >= 8 && roll % 3 == 0) count = Mathf.Min(count, 3); // 기업 구역 일부는 타워 공간 확보
        return count; // 목표 동수 반환
    }

    private static int CountNormalBlocks(Transform blocks) // 일반 블록 수 계산
    {
        int count = 0; // 집계 초기화

        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            if (TryBlockCoordinate(block.name, out int x, out int z) && !Reserved(x, z)) count++; // 일반 블록 집계
        }

        return count; // 결과 반환
    }

    private static int CountHealthyStagedBlocks(Transform blocks) // staged V3가 두 동 이상인 블록 수 계산
    {
        int healthy = 0; // 정상 블록 수

        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            if (!TryBlockCoordinate(block.name, out int x, out int z) || Reserved(x, z)) continue; // 일반 블록만 검사

            int count = 0; // staged 건물 수
            for (int childIndex = 0; childIndex < block.childCount; childIndex++) // 직접 자식 순회
            {
                Transform child = block.GetChild(childIndex); // 현재 자식
                if (child != null && child.name.StartsWith("RecoveryV3_", StringComparison.Ordinal) && HasVisibleMainBody(child.gameObject)) count++; // 정상 V3 집계
            }

            if (count >= 2) healthy++; // 최소 두 동이면 정상 블록
        }

        return healthy; // 정상 staged 블록 수 반환
    }

    private static void RemoveStagingBuildings(Transform blocks) // 이전 실패에서 남은 RecoveryV3만 제거
    {
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            for (int childIndex = block.childCount - 1; childIndex >= 0; childIndex--) // 역순 자식 순회
            {
                Transform child = block.GetChild(childIndex); // 현재 자식
                if (child != null && child.name.StartsWith("RecoveryV3_", StringComparison.Ordinal)) Undo.DestroyObjectImmediate(child.gameObject); // staged 잔여 건물 제거
            }
        }
    }

    private static void RemoveOldBuildingsAfterStaging(Transform blocks) // V3 전체 생성 성공 뒤 구형 건물만 제거
    {
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            if (!TryBlockCoordinate(block.name, out int x, out int z) || Reserved(x, z)) continue; // 일반 블록만 처리

            for (int childIndex = block.childCount - 1; childIndex >= 0; childIndex--) // 역순 자식 순회
            {
                Transform child = block.GetChild(childIndex); // 현재 자식
                if (child == null || child.name.StartsWith("RecoveryV3_", StringComparison.Ordinal)) continue; // 새 V3는 보존

                bool oldBuilding = child.name.StartsWith("Building_", StringComparison.Ordinal) ||
                                   child.name.StartsWith("BuildingPrefab_", StringComparison.Ordinal) ||
                                   child.name.StartsWith("BuildingV2_", StringComparison.Ordinal) ||
                                   child.name.StartsWith("BuildingV3_", StringComparison.Ordinal); // 기존 건물 계열 확인

                if (oldBuilding && child.Find("MainCollision") != null) Undo.DestroyObjectImmediate(child.gameObject); // 구형 건물 전체 제거
            }
        }
    }

    private static void RenameStagedBuildings(Transform blocks) // staged V3 이름을 최종 이름으로 변경
    {
        for (int i = 0; i < blocks.childCount; i++) // 전체 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            int index = 0; // 최종 건물 순번

            for (int childIndex = 0; childIndex < block.childCount; childIndex++) // 직접 자식 순회
            {
                Transform child = block.GetChild(childIndex); // 현재 자식
                if (child == null || !child.name.StartsWith("RecoveryV3_", StringComparison.Ordinal)) continue; // staged V3만 처리
                Undo.RecordObject(child.gameObject, "Rename Recovery V3 Building"); // 이름 변경 Undo 등록
                child.name = "BuildingV3_" + index.ToString("D2"); // 최종 계층 이름 적용
                index++; // 순번 증가
            }
        }
    }

    private static int RemoveLegacyDetails(MapWorldRoot world) // Day16 baked 장식 제거와 MapWorldRoot 참조 해제
    {
        int removed = 0; // 제거 수 초기화
        HashSet<GameObject> targets = new HashSet<GameObject>(); // 중복 없는 제거 목록

        if (world.DetailVisuals != null) // 직렬화된 타일 장식 배열 확인
        {
            foreach (GameObject detail in world.DetailVisuals) // 배열 순회
            {
                if (detail != null) targets.Add(detail); // 유효 장식 등록
            }
        }

        foreach (Transform child in world.GetComponentsInChildren<Transform>(true)) // 배열 밖 잔여 이름 검색
        {
            if (child != null && child != world.transform && child.name.StartsWith("DetailVisuals_", StringComparison.Ordinal)) targets.Add(child.gameObject); // baked 장식 등록
        }

        foreach (GameObject target in targets) // 구형 장식 제거
        {
            if (target == null) continue; // 이미 제거된 항목 제외
            Undo.DestroyObjectImmediate(target); // 타일별 통합 창문·패널·도로 장식 제거
            removed++; // 제거 수 증가
        }

        SerializedObject serialized = new SerializedObject(world); // private 배열 직렬화 접근
        serialized.Update(); // 현재 값 읽기
        SerializedProperty property = serialized.FindProperty("detailVisuals"); // 장식 배열 속성 찾기

        if (property != null) // 속성 확인
        {
            property.arraySize = 0; // 런타임에서 제거 장식을 다시 활성화하지 않도록 배열 비움
            serialized.ApplyModifiedProperties(); // 실제 월드에 저장
        }

        return removed; // 제거 수 반환
    }

    private static void RemoveDetachedLayers(MapWorldRoot world) // 구형 건물 위치를 기준으로 따로 생성된 후속 장식 제거
    {
        Transform day17 = FindByName(world.transform, "Day17_CyberpunkCityDetail"); // Day17 루트
        if (day17 != null) RemoveDirectChildren(day17, name => name.StartsWith("Generic_", StringComparison.Ordinal) || name == "StreetLife"); // 일반 건물·거리 종속 요소 제거

        Transform day18 = FindByName(world.transform, "Day18_UrbanDensityLayer"); // Day18 밀도 루트
        if (day18 != null) RemoveDirectChildren(day18, name => name.StartsWith("Density_Tile_", StringComparison.Ordinal)); // 구형 상점·옥상 요소 제거

        Transform polish = FindByName(world.transform, "Day18_StreetPolishLayer"); // Day18 보정 루트
        if (polish != null) RemoveDirectChildren(polish, name => name.StartsWith("Polish_Tile_", StringComparison.Ordinal)); // 구형 전봇대·보도 요소 제거

        Transform day23 = FindByName(world.transform, "Day23_UrbanChaseExpansion"); // Day23 확장 루트
        if (day23 != null) RemoveDirectChildren(day23, name => name.StartsWith("HighRise_", StringComparison.Ordinal) || name.StartsWith("FillCluster_", StringComparison.Ordinal) || name.StartsWith("Alley_", StringComparison.Ordinal)); // 구형 건물 종속 요소 제거
    }

    private static int ValidateFinalCity(MapWorldRoot world, Transform blocks) // 최종 V3 도시 검수
    {
        int warnings = 0; // 경고 수 초기화

        if (world.DetailVisuals != null && world.DetailVisuals.Length > 0) // 구형 장식 참조 확인
        {
            warnings++; // 경고 증가
            Debug.LogWarning("[Day27 V3] MapWorldRoot.DetailVisuals가 비워지지 않았습니다.", world); // 잔여 참조 보고
        }

        for (int i = 0; i < blocks.childCount; i++) // 모든 블록 순회
        {
            Transform block = blocks.GetChild(i); // 현재 블록
            if (!TryBlockCoordinate(block.name, out int x, out int z) || Reserved(x, z)) continue; // 일반 블록만 검사

            Transform lot = block.Find("PavedLot"); // 부지 조회
            if (lot == null) // 부지 누락 확인
            {
                warnings++; // 경고 증가
                continue; // 현재 블록 생략
            }

            Bounds lotBounds = WorldBounds(lot); // 부지 범위
            List<Bounds> bodies = new List<Bounds>(); // 건물 본체 범위 목록
            int count = 0; // V3 건물 수

            for (int childIndex = 0; childIndex < block.childCount; childIndex++) // 직접 자식 순회
            {
                Transform child = block.GetChild(childIndex); // 현재 자식
                if (child == null || !child.name.StartsWith("BuildingV3_", StringComparison.Ordinal)) continue; // V3 건물만 검사
                count++; // 건물 수 증가

                if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject)) // Prefab 인스턴스 연결 확인
                {
                    warnings++; // 경고 증가
                    Debug.LogWarning("[Day27 V3] Prefab 연결이 아닌 건물 · " + block.name + " / " + child.name, child); // 연결 문제 보고
                }

                Transform main = child.Find("MainCollision"); // 본체 조회
                if (main == null || !HasVisibleMainBody(child.gameObject)) // 본체 렌더링 유효성 확인
                {
                    warnings++; // 경고 증가
                    Debug.LogWarning("[Day27 V3] 건물 본체 렌더링 누락 · " + block.name + " / " + child.name, child); // 본체 문제 보고
                    continue; // Bounds 검사 생략
                }

                Bounds body = WorldBounds(main); // 실제 본체 범위 조회
                bodies.Add(body); // 겹침 검사 목록 등록
                bool inside = body.min.x >= lotBounds.min.x - 0.25f && body.max.x <= lotBounds.max.x + 0.25f &&
                              body.min.z >= lotBounds.min.z - 0.25f && body.max.z <= lotBounds.max.z + 0.25f; // 블록 경계 확인

                if (!inside) // 부지 밖 건물 확인
                {
                    warnings++; // 경고 증가
                    Debug.LogWarning("[Day27 V3] 건물 블록 경계 초과 · " + block.name + " / " + child.name, child); // 경계 문제 출력
                }
            }

            if (count < 2 || count > 5) // 요청한 2~5동 범위 확인
            {
                warnings++; // 경고 증가
                Debug.LogWarning("[Day27 V3] 건물 수 확인 필요 · " + block.name + " · " + count + "동", block); // 동수 보고
            }

            for (int first = 0; first < bodies.Count; first++) // 첫 건물 순회
            {
                for (int second = first + 1; second < bodies.Count; second++) // 두 번째 건물 비교
                {
                    float overlapX = Mathf.Max(0f, Mathf.Min(bodies[first].max.x, bodies[second].max.x) - Mathf.Max(bodies[first].min.x, bodies[second].min.x)); // X 겹침
                    float overlapZ = Mathf.Max(0f, Mathf.Min(bodies[first].max.z, bodies[second].max.z) - Mathf.Max(bodies[first].min.z, bodies[second].min.z)); // Z 겹침

                    if (overlapX * overlapZ > 0.15f) // 의미 있는 본체 겹침 확인
                    {
                        warnings++; // 경고 증가
                        Debug.LogWarning("[Day27 V3] 건물 본체 겹침 · " + block.name + " · " + (overlapX * overlapZ).ToString("0.0") + "m²", block); // 겹침 보고
                    }
                }
            }
        }

        return warnings; // 전체 검수 경고 반환
    }

    private static Rect Inflated(Vector2 center, float width, float depth, float gap) // 최소 간격 포함 점유 영역
    {
        float totalWidth = width + gap; // 폭 여유 포함
        float totalDepth = depth + gap; // 깊이 여유 포함
        return new Rect(center.x - totalWidth * 0.5f, center.y - totalDepth * 0.5f, totalWidth, totalDepth); // 중심 기준 Rect 반환
    }

    private static Bounds WorldBounds(Transform target) // 실제 월드 Bounds 조회
    {
        Collider collider = target != null ? target.GetComponent<Collider>() : null; // 충돌체 우선 조회
        if (collider != null) return collider.bounds; // 충돌 Bounds 반환
        Renderer renderer = target != null ? target.GetComponent<Renderer>() : null; // 렌더러 조회
        if (renderer != null) return renderer.bounds; // 시각 Bounds 반환
        return new Bounds(target != null ? target.position : Vector3.zero, Vector3.one); // 최소 대체 범위
    }

    private static void RemoveDirectChildren(Transform parent, Predicate<string> predicate) // 조건에 맞는 직접 자식 묶음 제거
    {
        for (int i = parent.childCount - 1; i >= 0; i--) // 역순 자식 순회
        {
            Transform child = parent.GetChild(i); // 현재 자식
            if (child != null && predicate(child.name)) Undo.DestroyObjectImmediate(child.gameObject); // 조건 일치 묶음 제거
        }
    }

    private static Transform FindByName(Transform root, string name) // 하위 이름 검색
    {
        if (root == null) return null; // 검색 루트 확인
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 전체 하위 순회
        {
            if (child.name == name) return child; // 첫 일치 반환
        }
        return null; // 검색 실패
    }

    private static bool Reserved(int x, int z) // 주요 랜드마크 예약 블록
    {
        return (x == 6 && z == 2) || (x == 6 && z == 5) || (x == 1 && z == 2) || (x == 9 && z == 6) || (x == 6 && z == 10); // 린·시장·산업·기업·첨탑 보존
    }

    private static bool TryBlockCoordinate(string name, out int x, out int z) // Block_00_00 좌표 분석
    {
        x = -1; // 실패 기본값
        z = -1; // 실패 기본값
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Block_", StringComparison.Ordinal)) return false; // 형식 확인
        string[] parts = name.Split('_'); // 문자열 분리
        return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z); // 좌표 파싱
    }

    private static void Shuffle<T>(List<T> list, System.Random random) // 재현 가능한 셔플
    {
        for (int i = list.Count - 1; i > 0; i--) // 뒤에서 앞으로 순회
        {
            int j = random.Next(0, i + 1); // 교환 위치 선택
            T temp = list[i]; // 현재 값 임시 저장
            list[i] = list[j]; // 선택 값 이동
            list[j] = temp; // 임시 값 복원
        }
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 현재 씬의 단일 컴포넌트 조회
    {
        T found = null; // 첫 결과 초기화
        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
        {
            foreach (T item in root.GetComponentsInChildren<T>(true)) // 비활성 자식 포함 검색
            {
                if (found != null) throw new InvalidOperationException(typeof(T).Name + "가 두 개 이상 있습니다."); // 중복 구성 보고
                found = item; // 첫 결과 저장
            }
        }
        return found; // 단일 결과 반환
    }

    private static void EnsureFolder(string path) // Unity Assets 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 분리
        string current = parts[0]; // Assets 시작 경로
        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 경로 계산
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            current = next; // 부모 경로 갱신
        }
    }
}
#endif
