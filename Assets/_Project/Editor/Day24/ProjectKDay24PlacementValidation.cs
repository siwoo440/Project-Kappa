#if UNITY_EDITOR // Day24 Map 배치 검수 전용
using System; // 날짜와 오류 처리
using System.IO; // 백업 경로 처리
using ProjectK.Day16; // 본편 Map 월드 참조
using ProjectK.Day23; // Day23 확장 루트 참조
using UnityEditor; // Undo와 메뉴 처리
using UnityEditor.SceneManagement; // 씬 저장 처리
using UnityEngine; // Terrain 높이와 Transform 처리
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay24PlacementValidation // Day23 지상 요소 높이 자동 검수·보정
{
    private const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 Map 씬 경로
    private const string BackupFolder = "Assets/_Project/Backups/Day24"; // Day24 검수 전 백업 폴더

    [MenuItem("Project K/Day 24/Validate And Fix Surface Placement")] // 지상 자동 보정 메뉴
    public static void ValidateAndFix() // 공중 건물·가로등·골목 루트 Terrain 높이 보정
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 잘못된 시점 변경 차단
        }

        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 Day24 검수 메뉴를 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 방지
        }

        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 본편 MapWorldRoot 조회
        Map23CityExpansionMarker marker = FindSingle<Map23CityExpansionMarker>(scene); // Day23 확장 표식 조회
        if (world == null || marker == null || world.Tiles == null || world.Tiles.Length != 9) // 필수 참조 확인
        {
            Debug.LogError("MapWorldRoot 3x3 Terrain과 Day23 확장이 필요합니다."); // 누락 구성 보고
            return; // 검수 중단
        }

        EnsureFolder(BackupFolder); // Day24 백업 폴더 준비
        string backupPath = BackupFolder + "/Map_before_day24_placement_fix_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 씬 경로 생성
        if (!EditorSceneManager.SaveScene(scene, backupPath, true)) // 현재 Map 씬 복사 백업
        {
            Debug.LogError("Day24 배치 보정 전 Map 씬 백업에 실패했습니다."); // 백업 실패 보고
            return; // 안전하지 않은 변경 중단
        }

        Undo.IncrementCurrentGroup(); // 전체 보정 Undo 그룹 시작
        int undoGroup = Undo.GetCurrentGroup(); // 현재 Undo 그룹 번호 저장
        Undo.SetCurrentGroupName("Day24 Validate And Fix Surface Placement"); // Undo 작업 이름 설정
        int fixedCount = 0; // 자동 보정 개수 초기화
        int warningCount = 0; // 남은 이상 배치 경고 개수 초기화

        try // 자동 높이 보정 실행
        {
            Transform root = marker.transform; // Day23 확장 루트 저장
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true); // Day23 전체 하위 Transform 조회

            foreach (Transform item in transforms) // 모든 Day23 요소 순회
            {
                if (item == null || item == root) // 루트와 누락 Transform 제외
                {
                    continue; // 다음 요소 처리
                }

                if (item.name.StartsWith("FillCluster_")) // 저층 채움 건물 묶음 확인
                {
                    fixedCount += SnapRootToTerrain(world, item, 0.24f); // 채움 건물 루트를 실제 지면 보행 높이에 부착
                    continue; // 자식 개별 보정 생략
                }

                if (item.name.StartsWith("Alley_")) // 골목 디테일 루트 확인
                {
                    fixedCount += SnapRootToTerrain(world, item, 0.24f); // 골목 루트를 실제 지면 높이에 부착
                    continue; // 자식 개별 보정 생략
                }

                if (item.name == "StreetLight" && item.parent != null && item.parent.name == "RoadStreetLights") // Day23 가로등 루트 확인
                {
                    fixedCount += SnapRootToTerrain(world, item, 0.08f); // 가로등 받침대를 실제 Terrain에 부착
                }
            }

            warningCount = CountRemainingFloatingObjects(world, root); // 자동 보정 후 남은 공중 요소 검사
            EditorSceneManager.MarkSceneDirty(scene); // 현재 씬 변경 표시

            if (!EditorSceneManager.SaveScene(scene)) // 보정된 Map 씬 저장
            {
                throw new IOException("Day24 배치 보정 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }

            Undo.CollapseUndoOperations(undoGroup); // 한 번의 Undo 단위로 결합
            Debug.Log("Day24 배치 보정 완료 · 자동 수정 " + fixedCount + "개 · 추가 확인 경고 " + warningCount + "개 · 백업: " + backupPath); // 검수 결과 출력
        }
        catch (Exception error) // 자동 보정 실패 처리
        {
            Undo.RevertAllDownToGroup(undoGroup); // 부분 변경 되돌리기
            Debug.LogException(error); // 실제 실패 원인 출력
            Debug.LogError("Day24 배치 보정 중단 · 백업 씬: " + backupPath); // 복구 위치 안내
        }
    }

    private static int SnapRootToTerrain(MapWorldRoot world, Transform item, float offset) // 지정 루트의 Y를 실제 Terrain 높이에 맞춤
    {
        float surfaceY = SampleSurfaceHeight(world, item.position.x, item.position.z, item.position.y - offset); // 현재 XZ 위치의 실제 Terrain 높이 계산
        float targetY = surfaceY + offset; // 요소별 지면 오프셋 적용
        if (Mathf.Abs(item.position.y - targetY) <= 0.04f) // 이미 올바른 높이인지 확인
        {
            return 0; // 수정 없음 반환
        }

        Undo.RecordObject(item, "Snap Day24 Surface Object"); // Transform 변경 Undo 등록
        Vector3 position = item.position; // 현재 월드 위치 복사
        position.y = targetY; // 실제 Terrain 높이 적용
        item.position = position; // 보정된 월드 위치 저장
        EditorUtility.SetDirty(item); // 씬 저장 대상으로 표시
        return 1; // 수정 한 건 반환
    }

    private static int CountRemainingFloatingObjects(MapWorldRoot world, Transform root) // 자동 보정 뒤 비정상 높이 후보 검사
    {
        int warnings = 0; // 경고 수 초기화
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true); // Day23 전체 Transform 조회

        foreach (Transform item in transforms) // 모든 확장 요소 순회
        {
            if (item == null || item == root) // 루트와 누락 요소 제외
            {
                continue; // 다음 요소 처리
            }

            bool target = item.name.StartsWith("FillCluster_") || item.name.StartsWith("Alley_") || (item.name == "StreetLight" && item.parent != null && item.parent.name == "RoadStreetLights"); // 지면 부착 대상 여부 판정
            if (!target) // 자동 검수 대상 확인
            {
                continue; // 고층·지하·계단 같은 의도적 높이 요소 제외
            }

            float surfaceY = SampleSurfaceHeight(world, item.position.x, item.position.z, item.position.y); // 실제 Terrain 높이 조회
            float allowed = item.name == "StreetLight" ? 0.9f : 1.2f; // 요소별 허용 높이 차 설정
            if (Mathf.Abs(item.position.y - surfaceY) <= allowed) // 정상 범위 확인
            {
                continue; // 경고 생략
            }

            warnings++; // 비정상 높이 후보 집계
            Debug.LogWarning("[Day24] 지면 높이 추가 확인 필요 · " + item.name + " · 차이 " + (item.position.y - surfaceY).ToString("0.00") + "m", item); // Console에 개별 경고 출력
        }

        return warnings; // 남은 경고 수 반환
    }

    private static float SampleSurfaceHeight(MapWorldRoot world, float worldX, float worldZ, float fallback) // 월드 XZ의 지상 Terrain 높이 계산
    {
        foreach (Terrain terrain in world.Tiles) // 지상 아홉 Terrain 순회
        {
            if (terrain == null || terrain.terrainData == null) // 유효 Terrain 확인
            {
                continue; // 다음 Terrain 검사
            }

            Vector3 origin = terrain.transform.position; // Terrain 남서 기준 위치 조회
            Vector3 size = terrain.terrainData.size; // Terrain 실제 크기 조회
            bool inside = worldX >= origin.x && worldX <= origin.x + size.x && worldZ >= origin.z && worldZ <= origin.z + size.z; // 현재 XZ가 Terrain 안인지 확인
            if (!inside) // Terrain 범위 밖 확인
            {
                continue; // 다음 Terrain 검사
            }

            return terrain.SampleHeight(new Vector3(worldX, origin.y, worldZ)) + origin.y; // 실제 지상 Terrain 월드 Y 반환
        }

        return fallback; // Terrain을 찾지 못한 경우 기존 높이 유지
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 대상 씬의 단일 컴포넌트 검색
    {
        T found = null; // 첫 검색 결과 저장

        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 오브젝트 순회
        {
            foreach (T item in root.GetComponentsInChildren<T>(true)) // 비활성 자식까지 검색
            {
                if (found != null) // 두 번째 결과 확인
                {
                    throw new InvalidOperationException(typeof(T).Name + "가 두 개 이상 있습니다."); // 중복 구성 보고
                }

                found = item; // 첫 결과 저장
            }
        }

        return found; // 단일 검색 결과 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 요소 분리
        string current = parts[0]; // Assets 시작 경로 설정

        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 전체 경로 생성

            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 여부 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            }

            current = next; // 다음 부모 경로 갱신
        }
    }
}
#endif
