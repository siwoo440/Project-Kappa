#if UNITY_EDITOR // 20일차 교통·시민 설치 메뉴
using System; // 백업 이름과 오류 처리
using System.IO; // 씬 백업 처리
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day20; // 도시 생활 관리자 참조
using UnityEditor; // 메뉴와 Undo 처리
using UnityEditor.SceneManagement; // 씬 저장과 백업
using UnityEngine; // 루트와 컴포넌트 생성
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay20CityLifeSetup // Map 씬에 차량과 시민 생활 시스템 설치
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 본편 Map 씬 경로
    public const string RootName = "Day20_CityLife"; // 단일 제거 가능한 도시 생활 루트
    public const string SourceCommit = "010a637b837f0743dd5851d34c942a93975453ad"; // 19일차 완료 기준 최신 커밋
    private static bool applying; // 중복 설치 방지

    [MenuItem("Project K/Day 20/Setup Traffic And Citizens")] // 실제 설치 메뉴
    public static void Apply() // 프리팹 생성 후 Map 씬에 관리자 연결
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 잘못된 시점 변경 방지
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 중단
        }
        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 단일 본편 월드 조회
        if (world == null || world.Player == null || world.Tiles == null || world.Tiles.Length != 9) // 교통 생성에 필요한 월드 확인
        {
            Debug.LogError("MapWorldRoot의 플레이어와 3x3 Terrain 구성을 확인하세요."); // 선행 Map 구성 안내
            return; // 설치 중단
        }
        Transform old = world.transform.Find(RootName); // 기존 도시 생활 루트 확인
        if (old != null) // 중복 설치 확인
        {
            Debug.LogWarning("이미 Day20_CityLife가 있습니다. 기존 수동 편집을 보존하며 다시 만들지 않습니다."); // 재설치 방지 안내
            Selection.activeGameObject = old.gameObject; // 기존 결과 선택
            return; // 반복 설치 중단
        }
        applying = true; // 메뉴 재진입 차단
        int group = -1; // Undo 그룹 초기값
        string backup = string.Empty; // 오류 복구용 백업 경로
        try // 백업 이후 실제 설치
        {
            string folder = "Assets/_Project/Backups/Day20"; // 20일차 백업 폴더
            EnsureFolder(folder); // 백업 폴더 확보
            backup = folder + "/Map_before_citylife_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 경로 생성
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 현재 Map 씬 복사 백업
            {
                throw new IOException("Day20 도시 생활 적용 전 Map 백업에 실패했습니다."); // 백업 없는 변경 차단
            }
            Undo.IncrementCurrentGroup(); // 전체 설치 Undo 그룹 시작
            group = Undo.GetCurrentGroup(); // 현재 그룹 번호 저장
            Undo.SetCurrentGroupName("Day20 Setup Traffic And Citizens"); // 실행 취소 이름 지정
            EditorUtility.DisplayProgressBar("Project K / Day 20", "차량·시민 프리팹 생성", 0.20f); // 에셋 생성 진행 표시
            MapCityLifeAssets.AssetSet assets = MapCityLifeAssets.CreateOrLoad(); // 차량 세 종과 시민 세 유형 준비
            GameObject rootObject = new GameObject(RootName); // 도시 생활 단일 루트 생성
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Day20 City Life"); // 새 루트 Undo 연결
            rootObject.transform.SetParent(world.transform, false); // 본편 월드에 생활 시스템 연결
            MapCityLifeManager manager = rootObject.AddComponent<MapCityLifeManager>(); // 교통·시민 풀 관리자 추가
            manager.Configure(world, SourceCommit, assets.CivilianVehicle, assets.DeliveryVehicle, assets.CargoVehicle, assets.HumanCitizen, assets.AndroidCitizen, assets.MechanicalCitizen); // 생성 프리팹과 월드 연결
            EditorUtility.SetDirty(manager); // 씬 저장 대상 표시
            EditorUtility.DisplayProgressBar("Project K / Day 20", "교통 그래프·보행 그래프 검사", 0.75f); // 저장 전 검사 표시
            int checks = ProjectKDay20CityLifeValidation.Validate(manager, true); // 정적 구성과 경계 규칙 검사
            AssetDatabase.SaveAssets(); // 프리팹과 재질 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 변경 상태 표시
            if (!EditorSceneManager.SaveScene(scene)) // 원래 Map 씬 저장
            {
                throw new IOException("Day20 도시 생활 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = rootObject; // 생성된 생활 루트 선택
            SceneView.lastActiveSceneView?.FrameSelected(); // 편집기 시점을 생성 루트로 이동
            Debug.Log("Day20 교통·시민 설정 완료 · 검사 " + checks + "항목 · 차량 목표 " + manager.TargetVehicleCount + " · 시민 목표 " + manager.TargetCitizenCount + " · 백업: " + backup); // 생성 결과 안내
        }
        catch (Exception error) // 부분 설치 실패 처리
        {
            if (group >= 0) // 되돌릴 작업 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 씬 변경 되돌리기
            }
            Debug.LogException(error); // 구체적인 실패 원인 출력
            Debug.LogError("Day20 도시 생활 설정 중단 · 백업 씬: " + backup); // 복구 경로 안내
        }
        finally // 편집기 상태 정리
        {
            EditorUtility.ClearProgressBar(); // 진행 창 닫기
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
