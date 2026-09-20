#if UNITY_EDITOR // 18일차 간판·거리 보정 설치 메뉴
using System; // 백업 이름과 오류 처리
using System.IO; // 씬 백업 확인
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day18; // 도시 밀도와 거리 보정 참조
using UnityEditor; // 메뉴·선택·구형 코드 정리
using UnityEditor.SceneManagement; // Map 씬 저장과 복사
using UnityEngine; // 편집기 오브젝트 처리
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay18StreetPolishFix // 잘못된 Day19 표기와 간판 관통을 18일차로 통합
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 본편 씬 경로
    private const string LegacyRoot = "Day19_UrbanPolishLayer"; // 이전 잘못된 일차의 씬 루트
    private static bool applying; // 메뉴 중복 실행 방지

    // 현재 18일차 보정 메뉴
    public static void Apply() // 백업 후 기존 19일차 루트를 교체하고 거리 보정
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
        MapUrbanDensityMarker density = FindSingle<MapUrbanDensityMarker>(scene); // 18일차 밀도 레이어 조회
        if (world == null || density == null || !density.Completed) // 선행 설치 확인
        {
            Debug.LogError("Day18 Increase Urban Density 적용이 먼저 필요합니다."); // 선행 작업 안내
            return; // 불완전한 월드 변경 중단
        }
        applying = true; // 메뉴 재진입 방지
        int group = -1; // Undo 범위 초기화
        string backup = string.Empty; // 오류 안내용 백업 경로
        try // 백업 이후 실제 변경 수행
        {
            string folder = "Assets/_Project/Backups/Day18"; // 현재 일차 백업 폴더
            MapCyberpunkGeometry.EnsureFolder(folder); // 백업 경로 확보
            backup = folder + "/Map_before_signfix_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 이름 생성
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 현재 Map 복사 백업
            {
                throw new IOException("Day18 Map 보정 전 백업에 실패했습니다."); // 백업 없는 변경 차단
            }
            Undo.IncrementCurrentGroup(); // 전체 작업 Undo 범위 시작
            group = Undo.GetCurrentGroup(); // 현재 Undo 번호 저장
            Undo.SetCurrentGroupName("Day18 Repair Signs And Streetscape"); // 실행 취소 이름 지정
            RemoveSceneRoot(world.transform, LegacyRoot); // 이전 Day19 씬 레이어 제거
            RemoveSceneRoot(world.transform, MapUrbanStreetPolishMarker.RootName); // 이전 수정본이 있으면 교체
            EditorUtility.DisplayProgressBar("Project K / Day 18", "간판 크기·깊이와 거리 구조 보정", 0.30f); // 진행 상태 표시
            MapUrbanStreetPolishMarker marker = MapUrbanStreetPolishBuilder.Build(world, density); // 현재 18일차 보정 레이어 생성
            EditorUtility.DisplayProgressBar("Project K / Day 18", "간판 바운드·전봇대·보도 검사", 0.84f); // 저장 전 검사 단계 표시
            int checks = ProjectKDay18StreetPolishValidation.Validate(marker, true); // 실제 렌더 크기와 구조 검사
            AssetDatabase.SaveAssets(); // 생성한 문자 재질 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 변경 상태 표시
            if (!EditorSceneManager.SaveScene(scene)) // 원래 Map 경로에 최종 저장
            {
                throw new IOException("Day18 보정 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = marker.gameObject; // 새 결과 선택
            SceneView.lastActiveSceneView?.FrameSelected(); // 편집기 시점을 결과로 이동
            Debug.Log("Day18 간판·거리 보정 저장 완료 · 검사 " + checks + "항목 · 간판 " + marker.FixedSignCount + " · 전봇대 " + marker.PoleCount + " · 보도 " + marker.SidewalkCount + " · 광원 " + marker.LightCount + " · 백업: " + backup); // 실제 결과 안내
            ScheduleLegacyDay19Cleanup(); // 저장 완료 뒤 잘못된 Day19 코드 폴더 정리 예약
        }
        catch (Exception error) // 부분 생성 실패 처리
        {
            if (group >= 0) // 되돌릴 작업 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 씬 변경 되돌리기
            }
            Debug.LogException(error); // 구체적인 실패 원인 출력
            Debug.LogError("Day18 보정 중단 · 백업 씬: " + backup); // 복구 위치 안내
        }
        finally // 편집기 상태 정리
        {
            EditorUtility.ClearProgressBar(); // 진행 창 닫기
            applying = false; // 다음 실행 허용
        }
    }

    // 자동 정리가 실패했을 때 사용할 수동 메뉴
    public static void CleanupLegacyDay19Code() // 잘못된 일차 코드 폴더만 제거
    {
        CleanupLegacyDay19(); // 공통 정리 실행
    }

    private static void RemoveSceneRoot(Transform root, string name) // 이전 거리 보정 루트 제거
    {
        Transform target = MapUrbanStreetPolishBuilder.Find(root, name); // 이름으로 기존 루트 조회
        if (target != null) // 제거할 루트 확인
        {
            Undo.DestroyObjectImmediate(target.gameObject); // 현재 씬에서 안전하게 제거
        }
    }

    private static void ScheduleLegacyDay19Cleanup() // 현재 메뉴 종료 뒤 구형 코드 정리 예약
    {
        EditorApplication.delayCall += CleanupLegacyDay19; // 컴파일 중단 없이 다음 편집기 틱에서 삭제
    }

    private static void CleanupLegacyDay19() // 잘못된 Day19 코드와 스크립트 폴더 삭제
    {
        bool changed = false; // 실제 삭제 여부 기록
        changed |= DeleteAsset("Assets/_Project/Editor/Day19"); // 구형 에디터 코드 삭제
        changed |= DeleteAsset("Assets/_Project/Scripts/World/Map19"); // 구형 런타임 표식 코드 삭제
        if (changed) // 파일 변경이 있었는지 확인
        {
            AssetDatabase.Refresh(); // 삭제 결과를 Unity 프로젝트에 반영
            Debug.Log("잘못된 Day19 코드 폴더를 제거하고 18일차 구조로 통합했습니다. Day19 백업 씬은 안전을 위해 보존합니다."); // 보존 범위 안내
        }
    }

    private static bool DeleteAsset(string path) // 존재하는 구형 에셋만 삭제
    {
        if (!AssetDatabase.IsValidFolder(path) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null) // 경로 존재 여부 확인
        {
            return false; // 없는 경로는 변경 없음
        }
        return AssetDatabase.DeleteAsset(path); // Unity 메타와 함께 안전하게 삭제
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
