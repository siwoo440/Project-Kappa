#if UNITY_EDITOR // 17일차 Map 보강 편집 전용
using System; // 백업 파일 이름과 오류 처리
using System.IO; // 씬 백업 경로 처리
using ProjectK.Day16; // 기존 Map 월드 참조
using ProjectK.Day17; // 설치 결과 표식 참조
using UnityEditor; // 메뉴와 에셋 저장
using UnityEditor.SceneManagement; // Map 씬 백업과 저장
using UnityEngine; // 씬 객체 조회
using UnityEngine.SceneManagement; // 활성 씬 검사

public static class ProjectKDay17CyberpunkSetup // 16일차 Map 위에 사이버펑크 디테일을 설치하는 메뉴
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 대상 본편 씬 경로
    private static bool applying; // 중복 메뉴 실행 방지

    // 17일차 실제 설치 메뉴
    public static void Enhance() // 백업 후 네온 도시 디테일 추가
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 불가 상태 안내
            return; // 씬 변경 중단
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 편집 중인 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 여부 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 활성 씬으로 선택하세요."); // 올바른 작업 순서 안내
            return; // 다른 씬 수정 방지
        }
        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 16일차 월드 관리자 조회
        if (world == null) // Map 생성 여부 확인
        {
            Debug.LogError("MapWorldRoot가 없습니다. 16일차 Map 씬을 먼저 생성하세요."); // 선행 작업 누락 안내
            return; // 잘못된 씬 변경 방지
        }
        Transform existing = MapCyberpunkDetailBuilder.Find(world.transform, MapCyberpunkDetailMarker.RootName); // 기존 설치 여부 확인
        if (existing != null) // 이미 디테일 루트가 존재하는 경우
        {
            Debug.LogWarning("이미 Day17 사이버펑크 디테일이 있습니다. 수동 편집을 보존하며 재설치하지 않습니다."); // 반복 설치 방지 안내
            Selection.activeTransform = existing; // 기존 디테일 루트 선택
            return; // 중복 장식 생성 중단
        }
        applying = true; // 메뉴 재진입 방지
        int group = -1; // 실행 취소 범위 초기화
        string backup = string.Empty; // 오류 안내용 백업 경로
        try // 백업 뒤에만 실제 씬 변경
        {
            string folder = "Assets/_Project/Backups/Day17"; // Map 원본 백업 폴더
            MapCyberpunkGeometry.EnsureFolder(folder); // 백업 폴더 생성
            backup = folder + "/Map_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 덮어쓰지 않는 백업 이름
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 기존 Map을 복사 형태로 저장
            {
                throw new IOException("Map 씬 백업에 실패했습니다."); // 백업 없는 변경 금지
            }
            Undo.IncrementCurrentGroup(); // 전체 변경의 실행 취소 범위 시작
            group = Undo.GetCurrentGroup(); // 현재 실행 취소 번호 저장
            Undo.SetCurrentGroupName("Day17 Cyberpunk City Detail"); // 실행 취소 메뉴 이름 지정
            EditorUtility.DisplayProgressBar("Project K / Day 17", "네온 재질과 도시 세부 구조 생성", 0.20f); // 긴 생성 과정 표시
            MapCyberpunkDetailMarker marker = MapCyberpunkDetailBuilder.Build(world); // 실제 도시 디테일 생성
            EditorUtility.DisplayProgressBar("Project K / Day 17", "구역별 디테일 참조 검증", 0.82f); // 저장 전 검사 단계 표시
            int checks = ProjectKDay17Validation.Validate(marker, true); // 실제 씬 구조와 성능 제한 검사
            AssetDatabase.SaveAssets(); // 새 재질과 에셋 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 씬 변경 표시
            if (!EditorSceneManager.SaveScene(scene)) // 같은 Map 씬 경로에 최종 결과 저장
            {
                throw new IOException("Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = marker.gameObject; // 생성된 디테일 루트 선택
            SceneView.lastActiveSceneView?.FrameSelected(); // 편집기 시점을 생성 루트로 이동
            Debug.Log("Day17 사이버펑크 도시 보강 저장 완료 · 검사 " + checks + "항목 · 백업: " + backup + " · 네온 간판 " + marker.NeonSignCount + " · 세부 소품 " + marker.PropCount + " · 실제 광원 " + marker.LightCount); // 실제 생성 집계 안내
        }
        catch (Exception error) // 부분 생성 실패 처리
        {
            if (group >= 0) // 실행 취소 가능한 변경 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 씬 오브젝트 변경 되돌리기
            }
            Debug.LogException(error); // 구체적인 오류 출력
            Debug.LogError("Day17 구성 중단 · 백업 씬: " + backup); // 복구 가능한 백업 위치 안내
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
        foreach (GameObject root in scene.GetRootGameObjects()) // 활성 씬 루트만 순회
        {
            foreach (T item in root.GetComponentsInChildren<T>(true)) // 비활성 자식까지 조회
            {
                if (found != null) // 두 번째 결과 확인
                {
                    throw new InvalidOperationException(typeof(T).Name + "가 Map 씬에 두 개 이상 있습니다."); // 잘못된 중복 구성 보고
                }
                found = item; // 첫 결과 저장
            }
        }
        return found; // 단일 결과 또는 없음 반환
    }
}
#endif
