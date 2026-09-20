#if UNITY_EDITOR // 18일차 도시 밀도 설치 메뉴
using System; // 백업 이름과 오류 처리
using System.IO; // 씬 백업 확인
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day17; // 17일차 디테일 참조
using ProjectK.Day18; // 18일차 밀도 표식 참조
using UnityEditor; // 메뉴와 선택 처리
using UnityEditor.SceneManagement; // Map 씬 저장과 복사
using UnityEngine; // 편집기 오브젝트 처리
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay18DensitySetup // 기존 도시를 유지한 밀도 레이어 설치
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 본편 씬 경로
    private static bool applying; // 메뉴 중복 실행 방지

    // 실제 도시 밀도 보강 메뉴
    public static void Apply() // 백업 후 밀도 레이어 생성
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 제한 안내
            return; // 잘못된 시점 변경 방지
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 중단
        }
        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 단일 본편 월드 조회
        MapCyberpunkDetailMarker day17 = FindSingle<MapCyberpunkDetailMarker>(scene); // 17일차 디테일 조회
        if (world == null || day17 == null || !day17.Completed) // 선행 설치 확인
        {
            Debug.LogError("Day16 Map과 Day17 Enhance Map Cyberpunk City 적용이 먼저 필요합니다."); // 선행 작업 안내
            return; // 불완전한 월드 변경 중단
        }
        MapUrbanDensityMarker old = FindSingle<MapUrbanDensityMarker>(scene); // 기존 18일차 설치 조회
        if (old != null) // 중복 설치 확인
        {
            Debug.LogWarning("이미 Day18 도시 밀도 레이어가 있습니다. 수동 편집을 보존하며 재설치하지 않습니다."); // 중복 생성 방지
            Selection.activeGameObject = old.gameObject; // 기존 결과 선택
            return; // 반복 설치 중단
        }
        applying = true; // 메뉴 재진입 방지
        int group = -1; // 실행 취소 범위 초기화
        string backup = string.Empty; // 오류 안내용 백업 경로
        try // 백업 이후에만 실제 변경
        {
            string folder = "Assets/_Project/Backups/Day18"; // 18일차 Map 백업 폴더
            MapCyberpunkGeometry.EnsureFolder(folder); // 백업 경로 확보
            backup = folder + "/Map_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 이름 생성
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 현재 Map을 복사 형태로 백업
            {
                throw new IOException("Day18 Map 백업에 실패했습니다."); // 백업 없는 변경 차단
            }
            Undo.IncrementCurrentGroup(); // 전체 작업 Undo 범위 시작
            group = Undo.GetCurrentGroup(); // 현재 Undo 번호 저장
            Undo.SetCurrentGroupName("Day18 Increase Urban Density"); // 실행 취소 이름 지정
            EditorUtility.DisplayProgressBar("Project K / Day 18", "도로·상점·옥상·차량 밀도 보강", 0.20f); // 생성 진행 상태 표시
            MapUrbanDensityMarker marker = MapUrbanDensityBuilder.Build(world, day17); // 실제 밀도 레이어 생성
            EditorUtility.DisplayProgressBar("Project K / Day 18", "도시 밀도와 참조 검증", 0.86f); // 저장 전 검사 단계 표시
            int checks = ProjectKDay18Validation.Validate(marker, true); // 생성 결과 검사
            AssetDatabase.SaveAssets(); // 사용한 재질 변경 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 변경 상태 표시
            if (!EditorSceneManager.SaveScene(scene)) // 원래 Map 경로에 최종 결과 저장
            {
                throw new IOException("Day18 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = marker.gameObject; // 새 밀도 루트 선택
            SceneView.lastActiveSceneView?.FrameSelected(); // 편집기 시점을 결과로 이동
            Debug.Log("Day18 도시 밀도 보강 저장 완료 · 검사 " + checks + "항목 · 상점 " + marker.StorefrontCount + " · 차량 " + marker.VehicleCount + " · 거리 소품 " + marker.StreetPropCount + " · 공중 통로 " + marker.BridgeCount + " · 백업: " + backup); // 생성 집계 안내
        }
        catch (Exception error) // 부분 생성 실패 처리
        {
            if (group >= 0) // 되돌릴 작업 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 씬 오브젝트 변경 되돌리기
            }
            Debug.LogException(error); // 구체적인 실패 원인 출력
            Debug.LogError("Day18 구성 중단 · 백업 씬: " + backup); // 복구 위치 안내
        }
        finally // 편집기 상태 정리
        {
            EditorUtility.ClearProgressBar(); // 진행 창 닫기
            applying = false; // 다음 실행 허용
        }
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 지정 씬의 단일 컴포넌트 조회
    {
        T found = null; // 첫 결과 저장 변수
        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
        {
            foreach (T item in root.GetComponentsInChildren<T>(true)) // 비활성 자식까지 조회
            {
                if (found != null) // 두 번째 결과 확인
                {
                    throw new InvalidOperationException(typeof(T).Name + "가 두 개 이상 있습니다."); // 중복 구성 보고
                }
                found = item; // 첫 결과 저장
            }
        }
        return found; // 단일 결과 또는 없음 반환
    }
}
#endif
