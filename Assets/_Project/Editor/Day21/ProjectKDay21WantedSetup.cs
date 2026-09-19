#if UNITY_EDITOR // 21일차 피해·수배·증원 설치 메뉴
using System; // 백업 이름과 오류 처리
using System.IO; // 씬 백업과 소스 확인
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day20; // Day20 차량·시민 프리팹 참조
using ProjectK.Day21; // 피해·수배 런타임 참조
using UnityEditor; // 메뉴·프리팹·Undo 처리
using UnityEditor.SceneManagement; // Map 씬 저장과 백업
using UnityEngine; // 루트·컴포넌트 생성
using UnityEngine.SceneManagement; // 활성 씬 확인

public static class ProjectKDay21WantedSetup // 시민·차량 체력과 GTA식 수배 시스템 설치
{
    public const string MapScenePath = "Assets/_Project/Scenes/Map.unity"; // 본편 Map 씬 경로
    public const string RootName = "Day21_CrimeWanted"; // 단일 제거 가능한 수배 시스템 루트
    public const string SourceCommit = "7f709c72d3eaf842face0955362efd1d9a6babc8"; // 20일차 완료 기준 최신 커밋
    private static bool applying; // 중복 실행 방지

    [MenuItem("Project K/Day 21/Setup Damage Wanted And Response")] // 실제 설치 메뉴
    public static void Apply() // 피해·Heat·별·증원 시스템 전체 설치
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 잘못된 시점 변경 방지
        }
        if (!ProjectKDay21CombatSourcePatch.IsPatched()) // 기존 총기·검 피해 경로 패치 확인
        {
            ProjectKDay21CombatSourcePatch.ApplyNow(); // 자동 패치 재시도
            Debug.LogWarning("총기·검 피해 경로를 수정했습니다. Unity 재컴파일이 끝난 뒤 같은 Setup 메뉴를 다시 실행하세요."); // 재컴파일 뒤 실행 안내
            return; // 컴파일 전 타입 연결 중단
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != MapScenePath) // 정확한 Map 씬 확인
        {
            Debug.LogWarning("Assets/_Project/Scenes/Map.unity를 열고 실행하세요."); // 대상 씬 안내
            return; // 다른 씬 변경 중단
        }
        MapWorldRoot world = FindSingle<MapWorldRoot>(scene); // 본편 월드 조회
        MapCityLifeManager cityLife = FindSingle<MapCityLifeManager>(scene); // Day20 도시 생활 관리자 조회
        if (world == null || world.Player == null || cityLife == null) // 선행 월드·생활 시스템 확인
        {
            Debug.LogError("Day20 Traffic And Citizens 적용이 먼저 필요합니다."); // 선행 작업 안내
            return; // 설치 중단
        }
        Transform old = world.transform.Find(RootName); // 기존 Day21 수배 루트 조회
        if (old != null) // 반복 설치 확인
        {
            Debug.LogWarning("이미 Day21_CrimeWanted가 있습니다. 기존 수동 편집을 보존하며 다시 만들지 않습니다."); // 중복 생성 방지
            Selection.activeGameObject = old.gameObject; // 기존 결과 선택
            return; // 설치 중단
        }
        applying = true; // 메뉴 재진입 차단
        int group = -1; // Undo 그룹 초기값
        string backup = string.Empty; // 오류 복구용 백업 경로
        try // 백업 이후 실제 변경
        {
            string folder = "Assets/_Project/Backups/Day21"; // 21일차 백업 폴더
            EnsureFolder(folder); // 백업 폴더 생성
            backup = folder + "/Map_before_wanted_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 고유 백업 이름 생성
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 현재 Map 복사 백업
            {
                throw new IOException("Day21 수배 시스템 적용 전 Map 백업에 실패했습니다."); // 백업 없는 변경 차단
            }
            Undo.IncrementCurrentGroup(); // 전체 설치 Undo 그룹 시작
            group = Undo.GetCurrentGroup(); // 현재 그룹 번호 저장
            Undo.SetCurrentGroupName("Day21 Setup Damage Wanted And Response"); // 실행 취소 이름 지정
            EditorUtility.DisplayProgressBar("Project K / Day 21", "Day20 시민·차량 피해 컴포넌트 연결", 0.18f); // 프리팹 패치 진행 표시
            PatchCityLifePrefabs(); // 기존 Day20 프리팹에 체력·피격 연결 추가
            EditorUtility.DisplayProgressBar("Project K / Day 21", "수배 경비 프리팹 생성", 0.42f); // 경비 프리팹 생성 진행 표시
            MapWantedAssets.GuardAssets guards = MapWantedAssets.CreateOrLoad(); // E-01/E-02 대응 경비 준비
            GameObject rootObject = new GameObject(RootName); // 수배 시스템 단일 루트 생성
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Day21 Crime Wanted"); // 생성 루트 Undo 연결
            rootObject.transform.SetParent(world.transform, false); // 본편 월드 아래 연결
            MapWantedSystem wanted = rootObject.AddComponent<MapWantedSystem>(); // Heat·별·감소 관리자 추가
            wanted.Configure(world, SourceCommit); // 월드와 기준 커밋 연결
            MapWantedResponseManager response = rootObject.AddComponent<MapWantedResponseManager>(); // 별 단계별 증원 관리자 추가
            response.Configure(world, wanted, guards.Regular, guards.Elite); // 경비 프리팹과 수배 관리자 연결
            EditorUtility.SetDirty(wanted); // 수배 관리자 저장 표시
            EditorUtility.SetDirty(response); // 증원 관리자 저장 표시
            EditorUtility.DisplayProgressBar("Project K / Day 21", "Heat·별·피해·프리팹 검사", 0.82f); // 저장 전 검사 표시
            int checks = ProjectKDay21WantedValidation.Validate(wanted, response, true); // 전체 구성 검사
            AssetDatabase.SaveAssets(); // 수정된 프리팹과 생성 경비 저장
            EditorSceneManager.MarkSceneDirty(scene); // Map 변경 상태 표시
            if (!EditorSceneManager.SaveScene(scene)) // 원래 Map 씬 저장
            {
                throw new IOException("Day21 수배 시스템 Map 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 Undo 단위로 결합
            Selection.activeGameObject = rootObject; // 새 수배 루트 선택
            Debug.Log("Day21 피해·수배·증원 설정 완료 · 검사 " + checks + "항목 · 별 최대 5 · 경비 풀 " + response.GuardPoolSize + " · 백업: " + backup); // 설치 결과 안내
        }
        catch (Exception error) // 부분 설치 실패 처리
        {
            if (group >= 0) // 되돌릴 작업 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 씬 변경 되돌리기
            }
            Debug.LogException(error); // 구체적인 실패 원인 출력
            Debug.LogError("Day21 수배 시스템 설정 중단 · 백업 씬: " + backup); // 복구 경로 안내
        }
        finally // 편집기 상태 정리
        {
            EditorUtility.ClearProgressBar(); // 진행 창 닫기
            applying = false; // 다음 실행 허용
        }
    }

    public static void PatchCityLifePrefabs() // Day20 차량·시민 프리팹에 피해 컴포넌트 추가
    {
        PatchCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Human.prefab", MapCitizenKind.Human); // 인간 시민 피해 연결
        PatchCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Android.prefab", MapCitizenKind.Android); // 안드로이드 시민 피해 연결
        PatchCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Mechanical.prefab", MapCitizenKind.Mechanical); // 기계화 시민 피해 연결
        PatchVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Civilian.prefab", MapVehicleKind.Civilian); // 일반 차량 피해 연결
        PatchVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Delivery.prefab", MapVehicleKind.Delivery); // 배달 차량 피해 연결
        PatchVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Cargo.prefab", MapVehicleKind.Cargo); // 화물 차량 피해 연결
        AssetDatabase.SaveAssets(); // 수정한 여섯 프리팹 저장
    }

    private static void PatchCitizen(string path, MapCitizenKind kind) // 시민 프리팹 체력과 공통 피격 연결
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) // Day20 프리팹 존재 확인
        {
            throw new FileNotFoundException("Day20 시민 프리팹을 찾지 못했습니다.", path); // 선행 설치 누락 보고
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path); // 프리팹 편집용 임시 루트 로드
        try // 임시 프리팹 정리 보장
        {
            MapCitizenVitals vitals = root.GetComponent<MapCitizenVitals>(); // 기존 시민 생명 관리자 조회
            if (vitals == null) // 최초 패치 여부 확인
            {
                vitals = root.AddComponent<MapCitizenVitals>(); // 시민 체력·사망 컴포넌트 추가
            }
            vitals.Configure(kind); // 유형별 HP·방어율 적용
            if (root.GetComponent<WorldDamageReceiver>() == null) // 공통 피격 연결 존재 확인
            {
                root.AddComponent<WorldDamageReceiver>(); // 총기·검·폭발 공통 피격 연결 추가
            }
            PrefabUtility.SaveAsPrefabAsset(root, path); // 같은 GUID의 기존 프리팹에 덮어쓰기
        }
        finally // 프리팹 편집 루트 정리
        {
            PrefabUtility.UnloadPrefabContents(root); // 임시 프리팹 스테이지 해제
        }
    }

    private static void PatchVehicle(string path, MapVehicleKind kind) // 차량 프리팹 내구도와 공통 피격 연결
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) // Day20 프리팹 존재 확인
        {
            throw new FileNotFoundException("Day20 차량 프리팹을 찾지 못했습니다.", path); // 선행 설치 누락 보고
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path); // 프리팹 편집용 임시 루트 로드
        try // 임시 프리팹 정리 보장
        {
            MapVehicleVitals vitals = root.GetComponent<MapVehicleVitals>(); // 기존 차량 생명 관리자 조회
            if (vitals == null) // 최초 패치 여부 확인
            {
                vitals = root.AddComponent<MapVehicleVitals>(); // 차량 내구도·폭발 컴포넌트 추가
            }
            vitals.Configure(kind); // 유형별 HP·방어율 적용
            if (root.GetComponent<WorldDamageReceiver>() == null) // 공통 피격 연결 존재 확인
            {
                root.AddComponent<WorldDamageReceiver>(); // 총기·검·폭발 공통 피격 연결 추가
            }
            PrefabUtility.SaveAsPrefabAsset(root, path); // 같은 GUID의 기존 차량 프리팹에 덮어쓰기
        }
        finally // 프리팹 편집 루트 정리
        {
            PrefabUtility.UnloadPrefabContents(root); // 임시 프리팹 스테이지 해제
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
        return found; // 단일 결과 반환
    }

    private static void EnsureFolder(string path) // Unity 에셋 폴더 재귀 생성
    {
        string[] parts = path.Split('/'); // 경로 조각 분리
        string current = parts[0]; // Assets 시작 경로
        for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
        {
            string next = current + "/" + parts[i]; // 다음 전체 경로 생성
            if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 확인
            {
                AssetDatabase.CreateFolder(current, parts[i]); // 누락 폴더 생성
            }
            current = next; // 다음 부모 경로 갱신
        }
    }
}
#endif
