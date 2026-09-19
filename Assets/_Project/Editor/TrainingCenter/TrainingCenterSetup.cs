#if UNITY_EDITOR // 통합 Test 씬 재구성 전용
using System; // 구성 실패 보고
using System.IO; // 원본 백업과 필수 파일 확인
using UnityEditor; // 메뉴와 편집 되돌리기
using UnityEditor.SceneManagement; // 씬 복사와 저장
using UnityEngine; // 새 시설 루트 생성
using UnityEngine.SceneManagement; // 작업 씬 범위 확인
using G = TrainingCenterGeometry; // 공용 에셋 폴더 생성

public static class TrainingCenterSetup // 별도 확장이 아닌 Test 전체 통합 도구
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 유지할 원래 씬 경로
    public const string RootName = "TrainingCenter_Integrated"; // 완료된 새 시설 식별자
    private static bool applying; // 메뉴 중복 실행 방지

    [MenuItem("Project K/Training Center/Rebuild Test As Integrated Center")] // 명시적 적용 메뉴
    public static void Rebuild() // 백업 후 통합 훈련센터 생성
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일이 끝난 뒤 실행하세요."); // 변경 불가 상태 안내
            return; // 실행 중 씬 변경 금지
        }
        Scene scene = SceneManager.GetActiveScene(); // 현재 사용자가 편집하는 씬
        if (!scene.IsValid() || scene.path != ScenePath) // 다른 씬의 실수 방지
        {
            Debug.LogWarning("Assets/_Project/Scenes/Test.unity를 열고 활성 씬으로 선택하세요."); // 정확한 대상 안내
            return; // 다른 씬을 자동으로 닫지 않음
        }
        TrainingCenterRoot[] previous = TrainingCenterMigration.Components<TrainingCenterRoot>(scene); // 반복 설치 확인
        if (previous.Length > 0) // 기존 통합 구역 존재
        {
            Debug.LogWarning("이미 통합 훈련센터가 있습니다. 수동 수정은 보존됩니다. Validate Integrated Center 메뉴로 검사하세요."); // 위치와 편집 내용 보호
            Selection.activeGameObject = previous[0].gameObject; // 기존 시설 선택
            return; // 중복 시설 생성 방지
        }
        int group = -1; // 되돌리기 범위
        string backup = string.Empty; // 오류 시 원본 위치 안내
        applying = true; // 재진입 방지
        try // 실패한 부분 구성 정리
        {
            TrainingCenterMigration.Snapshot source = TrainingCenterMigration.Inspect(scene); // 변경 전 필수 참조 확인
            ValidateArt(); // 필요한 안내판과 표면 자료 확인
            string folder = "Assets/_Project/Backups/TrainingCenter"; // 이전 맵의 별도 씬 위치
            G.Folder(folder); // 백업 경로 확보
            backup = folder + "/Test_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 덮어쓰지 않는 백업 이름
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 기존 씬을 바꾸지 않는 원본 복사
            {
                throw new IOException("원본 Test 씬 백업에 실패했습니다."); // 백업 없는 재구성 중단
            }
            Undo.IncrementCurrentGroup(); // 전체 작업 되돌리기 구분
            group = Undo.GetCurrentGroup(); // 이번 작업 범위 저장
            Undo.SetCurrentGroupName("Rebuild Integrated Training Center"); // 사용자 되돌리기 이름
            EditorUtility.DisplayProgressBar("Training Center", "통합 시설과 사격선 구성", 0.15f); // 긴 편집 과정 표시
            GameObject world = new GameObject(RootName); // 새 중심 원점
            Undo.RegisterCreatedObjectUndo(world, "Create Integrated Center"); // 새 시설 전체 복구 연결
            TrainingCenterRoot center = world.AddComponent<TrainingCenterRoot>(); // 런타임 관리자
            TrainingCenterZone[] zones = TrainingCenterSite.Build(center, source.Guns, out Transform spawn); // 새로운 고정 배치 생성
            EditorUtility.DisplayProgressBar("Training Center", "기존 기능 보존과 적 원본 이전", 0.65f); // 작업 진행 표시
            TrainingCenterMigration.Move(source, center, zones, spawn); // 백업 뒤에만 기존 배치 정리
            center.Configure(source.Player, spawn, zones, backup); // 완료된 참조 연결
            center.SetStandaloneSystems(source.Standalone.ToArray()); // 기존 전역 관리자 루트 보존
            EditorUtility.SetDirty(center); // 구성 결과 저장 대상 표시
            Physics.SyncTransforms(); // 검증할 새 콜라이더 위치 적용
            EditorUtility.DisplayProgressBar("Training Center", "사격선과 원본 참조 검증", 0.9f); // 실제 검사 단계 표시
            int checks = TrainingCenterValidation.Validate(center, true); // 저장 전 참조와 물리 사격선 검사
            AssetDatabase.SaveAssets(); // 새 공유 재질 먼저 저장
            EditorSceneManager.MarkSceneDirty(scene); // Test 변경 상태 표시
            if (!EditorSceneManager.SaveScene(scene)) // 동일 경로와 GUID의 최종 씬 저장
            {
                throw new IOException("통합 Test 씬 저장에 실패했습니다."); // 저장 실패 보고
            }
            Undo.CollapseUndoOperations(group); // 한 번의 되돌리기로 묶기
            Selection.activeGameObject = source.Player; // 새 로비의 플레이어 선택
            SceneView.lastActiveSceneView?.FrameSelected(); // 편집기 시점을 새 스폰으로 이동
            Debug.Log("통합 훈련센터 저장 완료 · 검사 " + checks + "항목 · 이전 씬: " + backup + " · 총기 수치와 입력 에셋은 변경하지 않았습니다."); // 실제 실행 결과 안내
        }
        catch (Exception error) // 부분 작업 오류 처리
        {
            if (group >= 0) // 되돌릴 작업 존재 확인
            {
                Undo.RevertAllDownToGroup(group); // 실패한 새 구역과 기존 이동 되돌리기
            }
            Debug.LogException(error); // 구체적인 실패 원인 표시
            Debug.LogError("구성 중단. 이전 씬 백업: " + backup + " · 백업을 확인하기 전 실패한 장면을 덮어 저장하지 마세요."); // 원본 복구 위치 안내
        }
        finally // 편집기 상태 복구
        {
            EditorUtility.ClearProgressBar(); // 진행 창 닫기
            applying = false; // 재시도 허용
        }
    }

    private static void ValidateArt() // 최소 필수 자료 사전 확인
    {
        string[] keys = new string[] // 주요 구역 안내판
        {
            "entry", // 시설 입구
            "overview", // 전체 배치
            "static", // 고정 사격
            "moving", // 이동 사격
            "precision", // 정밀 사격
            "parkour", // 이동 훈련
            "stealth", // 잠입 훈련
            "live" // 실전 훈련
        };
        foreach (string key in keys) // 실제 파일 확인
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(G.Art + "/Signs/" + key + ".png") == null) // 임포트된 이미지 누락
            {
                throw new FileNotFoundException("ZIP의 Assets 폴더 전체를 덮어쓰고 임포트를 기다리세요: " + key); // 부분 설치 중단
            }
        }
    }
}
#endif
