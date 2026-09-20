#if UNITY_EDITOR // 19일차 지도 UI 설치 메뉴
using System; // 백업 이름과 오류 처리
using System.IO; // 씬 백업 처리
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 지도 UI 참조
using UnityEditor; // 메뉴와 Undo 처리
using UnityEditor.SceneManagement; // 씬 저장과 백업
using UnityEngine; // 컴포넌트 생성
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay19MapUISetup // Map 씬에 전체 지도와 미니맵 연결
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 본편 Map 씬 경로
    public const string SourceCommit = "ea1158cc0bc0d1828d66c48ae9d18876a37418f2"; // 18일차 기준 최신 커밋
    private static bool applying; // 중복 실행 방지

    // 실제 설치 메뉴
    public static void Apply() // MapWorldRoot에 지도 UI 추가
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 잘못된 시점 변경 차단
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 중단
        }
        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 단일 본편 월드 조회
        if (world == null || world.Player == null || world.PlayCamera == null) // 지도 UI 필수 참조 확인
        {
            Debug.LogError("MapWorldRoot의 플레이어와 카메라 구성을 확인하세요."); // 누락 참조 안내
            return; // 설치 중단
        }
        applying = true; // 메뉴 재진입 차단
        int group = -1; // Undo 그룹 초기값
        string backup = string.Empty; // 오류 복구용 백업 경로
        try // 백업 이후 실제 변경
        {
            string folder = "Assets/_Project/Backups/Day19"; // 19일차 백업 폴더
            EnsureFolder(folder); // 백업 폴더 생성
            backup = folder + "/Map_before_mapui_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 이름 생성
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 현재 Map 복사 백업
            {
                throw new IOException("Day19 Map UI 적용 전 백업에 실패했습니다."); // 백업 없는 변경 차단
            }
            Undo.IncrementCurrentGroup(); // 지도 UI Undo 그룹 시작
            group = Undo.GetCurrentGroup(); // 현재 그룹 번호 저장
            Undo.SetCurrentGroupName("Day19 Setup World Map And Minimap"); // 실행 취소 이름 지정
            MapNavigationUI ui = world.GetComponent<MapNavigationUI>(); // 기존 지도 UI 조회
            if (ui == null) // 최초 설치 여부 확인
            {
                ui = Undo.AddComponent<MapNavigationUI>(world.gameObject); // 월드 루트에 지도 UI 추가
            }
            ui.Configure(world, SourceCommit); // 월드와 기준 커밋 연결
            EditorUtility.SetDirty(ui); // 변경된 컴포넌트 저장 표시
            int checks = ProjectKDay19MapUIValidation.Validate(ui, true); // 저장 전 구성 검사
            EditorSceneManager.MarkSceneDirty(scene); // 현재 Map 변경 표시
            if (!EditorSceneManager.SaveScene(scene)) // 원래 Map 씬 저장
            {
                throw new IOException("Day19 지도 UI Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = world.gameObject; // 지도 UI가 붙은 월드 선택
            Debug.Log("Day19 지도 UI 설정 완료 · 검사 " + checks + "항목 · M 전체 지도 · N 미니맵 · Shift+N 크기 · 백업: " + backup); // 설치 결과 안내
        }
        catch (Exception error) // 부분 설치 실패 처리
        {
            if (group >= 0) // 되돌릴 작업 확인
            {
                Undo.RevertAllDownToGroup(group); // 부분 씬 변경 되돌리기
            }
            Debug.LogException(error); // 실제 실패 원인 출력
            Debug.LogError("Day19 지도 UI 설정 중단 · 백업 씬: " + backup); // 복구 경로 안내
        }
        finally // 편집기 상태 정리
        {
            applying = false; // 다음 실행 허용
        }
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 지정 씬 단일 컴포넌트 조회
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
        return found; // 단일 결과 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 조각 분리
        string current = parts[0]; // Assets 시작 경로
        for (int i = 1; i < parts.Length; i++) // 나머지 폴더 순회
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
