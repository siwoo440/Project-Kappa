#if UNITY_EDITOR // 편집기 전용 11일차 설정
using System; // 오류와 백업 시각 처리
using System.Collections.Generic; // 연결된 총기 목록 관리
using System.IO; // 원본 파일 백업
using System.Text; // 입력 JSON 한글 보존
using UnityEditor; // 데이터와 입력 편집
using UnityEditor.SceneManagement; // 씬 저장과 추가 로드
using UnityEngine; // 플레이어와 카메라 참조
using UnityEngine.InputSystem; // 활성 원본과 분리된 입력 편집
using UnityEngine.SceneManagement; // 씬 객체 조회

public static class ProjectKDay11Setup // 기존 총기를 확장하는 수동 설정
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 기존 훈련장
    public const string InputPath = "Assets/InputSystem_Actions.inputactions"; // 기존 입력 에셋
    public const string DataFolder = "Assets/_Project/Data/Day11"; // 사격 조정 자료 폴더
    private static bool applying; // 같은 설정의 중복 실행 방지

    [MenuItem("Project K/Day 11/Setup Recoil Spread And Suppressor")] // 11일차 적용 메뉴
    public static void ApplySetup() // 사격 설정과 추가 훈련장 연결
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Day11: Play 중지와 컴파일 완료 후 실행하세요."); // 실행 불가 안내
            return; // 실행 중 씬 변경 금지
        }

        if (!File.Exists(ScenePath) || !File.Exists(InputPath)) // 이전 일차 필수 자료 확인
        {
            Debug.LogError("Day11: Test 씬과 InputSystem_Actions.inputactions를 확인하세요."); // 누락 자료 안내
            return; // 빈 맵 임의 생성 금지
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 현재 훈련장 로드 상태
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 자신이 여는 씬 여부
        if (!openedHere && scene.isDirty) // 저장하지 않은 수동 편집 확인
        {
            Debug.LogWarning("Day11: Test 씬을 Ctrl+S로 저장한 뒤 다시 실행하세요."); // 편집 내용 보호 안내
            return; // 수동 편집 덮어쓰기 차단
        }

        applying = true; // 설정 재진입 잠금
        try // 실패 시에도 임시 씬과 잠금 정리
        {
            if (openedHere) // 다른 씬 작업 중인지 확인
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); // 기존 작업 씬 유지
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어 확인
            PlayerFirearmController firearm = player != null ? player.GetComponent<PlayerFirearmController>() : null; // 10일차 총기 기반 확인
            if (firearm == null || FindCamera(scene) == null) // 총기와 기존 카메라 필수 확인
            {
                throw new InvalidOperationException("Day11: 10일차 PlayerFirearmController와 ThirdPersonCamera 설정이 필요합니다."); // 부분 설치 방지
            }

            List<FirearmDefinition> definitions = GetLoadout(firearm); // 현재 장비 목록 그대로 조회
            if (definitions.Count == 0 || ProjectKDay9Setup.FindNamed(scene, "CombatPreviewZone") == null) // 기존 무기와 배치 기준 검사
            {
                throw new InvalidOperationException("Day11: 연결된 권총과 CombatPreviewZone을 확인하세요."); // 임의로 기존 무기 변경 금지
            }

            string backup = "Library/ProjectKDay11Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"); // 실행별 백업 폴더
            Directory.CreateDirectory(backup); // 백업 폴더 확보
            File.Copy(ScenePath, Path.Combine(backup, "Test.unity")); // 씬 변경 전 백업
            File.Copy(InputPath, Path.Combine(backup, "InputSystem_Actions.inputactions")); // 입력 원본 백업
            ProjectKDay10ModelFactory.EnsureFolder(DataFolder); // 새 조정 데이터 폴더 확보

            foreach (FirearmDefinition definition in definitions) // 기존에 연결된 총기만 확장
            {
                string sourcePath = AssetDatabase.GetAssetPath(definition); // 수정할 총기 정의 경로
                if (string.IsNullOrEmpty(sourcePath) || !definition.IsValid) // 저장된 정상 에셋 확인
                {
                    throw new InvalidOperationException("Day11: 저장된 정상 FirearmDefinition이 필요합니다."); // 미완성 설정 중단
                }

                File.Copy(sourcePath, Path.Combine(backup, AssetDatabase.AssetPathToGUID(sourcePath) + ".asset")); // 총기 원본 설정 백업
                FirearmHandlingProfile profile = definition.Handling; // 이전 조정 수치 유지
                if (profile == null) // 처음 연결하는 총기 설정 확인
                {
                    string profilePath = DataFolder + "/" + definition.name + "_Handling.asset"; // 총기별 사격 조정 경로
                    profile = AssetDatabase.LoadAssetAtPath<FirearmHandlingProfile>(profilePath); // 기존 조정 자료 조회
                    if (profile == null) // 신규 조정 자료 확인
                    {
                        profile = ScriptableObject.CreateInstance<FirearmHandlingProfile>(); // 명시된 테스트 기본값 생성
                        AssetDatabase.CreateAsset(profile, profilePath); // 독립 조정 에셋 저장
                    }
                }

                GameObject model = ProjectKDay11RangeFactory.SuppressorPrefab(definition); // 기존 총기의 별도 소음기 지원 복제본
                FirearmView view = model != null ? model.GetComponent<FirearmView>() : null; // 완성 모형 검사
                if (view == null || view.Muzzle == null || !view.HasSuppressor) // 필수 총구 참조 확인
                {
                    throw new InvalidOperationException("Day11: 소음기 모형의 FirearmView 참조를 확인하세요."); // 잘못된 모형 연결 방지
                }

                definition.ConfigureHandling(profile, model); // 피해와 탄약과 재장전 수치는 그대로 유지
                EditorUtility.SetDirty(definition); // 연결 변경 저장 표시
            }

            ConfigureInput(); // 비활성 사본에 B 입력만 추가
            ProjectKDay11RangeFactory.BuildRange(scene, player.transform); // 기존 구간을 지우지 않는 옆 사격장 배치
            EditorSceneManager.MarkSceneDirty(scene); // 실제 씬 변경 표시
            if (!EditorSceneManager.SaveScene(scene)) // 저장 성공 여부 확인
            {
                throw new IOException("Day11: Test 씬 저장 실패"); // 완료로 오인하지 않도록 오류 전달
            }

            AssetDatabase.SaveAssets(); // 연결한 프로필과 프리팹 저장
            Debug.Log("Day11 설정 완료: 5 권총 / B 소음기 / T 재장전. Combat 구역 오른쪽 사격장. 원본 백업: " + backup); // 적용 결과와 백업 안내
        }
        catch (Exception exception) // 실제 설정 실패 보고
        {
            Debug.LogException(exception); // 실패 위치와 원인 출력
        }
        finally // 원래 편집 환경 복구
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 자신이 열었던 씬만 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 기존 작업 씬으로 복귀
            }

            applying = false; // 수정 후 재시도 가능
        }
    }

    public static List<FirearmDefinition> GetLoadout(PlayerFirearmController firearm) // 원래 총기 목록 읽기
    {
        List<FirearmDefinition> result = new List<FirearmDefinition>(); // 고유한 유효 에셋 목록
        SerializedObject serialized = new SerializedObject(firearm); // 기존 필드를 읽기만 사용
        SerializedProperty loadout = serialized.FindProperty("loadout"); // 10일차 장비 목록
        for (int i = 0; loadout != null && i < loadout.arraySize; i++) // 기존 슬롯 순회
        {
            FirearmDefinition definition = loadout.GetArrayElementAtIndex(i).objectReferenceValue as FirearmDefinition; // 연결된 총기 참조
            if (definition != null && !result.Contains(definition)) // 중복 참조 제외
            {
                result.Add(definition); // 순서와 장비 구성 보존
            }
        }

        return result; // 읽은 목록 반환
    }

    public static ThirdPersonCamera FindCamera(Scene scene) // 해당 씬의 실제 메인 카메라 조회
    {
        foreach (GameObject root in scene.GetRootGameObjects()) // 대상 씬만 검색
        {
            foreach (ThirdPersonCamera camera in root.GetComponentsInChildren<ThirdPersonCamera>(true)) // 기존 카메라 스크립트 조회
            {
                if (camera.CompareTag("MainCamera")) // 총기와 동일한 기준 카메라 확인
                {
                    return camera; // 실제 반동 연결 대상 반환
                }
            }
        }

        return null; // 필요한 카메라 없음 반환
    }

    private static void ConfigureInput() // 원본을 활성화하지 않는 입력 편집
    {
        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(InputPath)); // 기존 ID와 사용자 바인딩 보존
        try // 임시 자료 수명 정리
        {
            copy.Disable(); // 전체 사본 액션 비활성화
            InputActionMap map = copy.FindActionMap("Player", false); // 기존 플레이어 입력 확인
            if (map == null) // 필수 맵 검사
            {
                throw new InvalidOperationException("Day11: Player 입력 맵이 없습니다."); // 다른 맵 생성 금지
            }

            InputAction action = map.FindAction("ToggleSuppressor", false); // 기존 사용자 소음기 입력 조회
            bool changed = false; // 실제 변경 여부
            if (action == null) // 처음 추가하는 입력 확인
            {
                foreach (InputBinding binding in map.bindings) // 기존 B키 충돌 검사
                {
                    if (binding.path == "<Keyboard>/b") // 다른 기능의 B키 사용 확인
                    {
                        throw new InvalidOperationException("Day11: B키가 이미 사용 중입니다. ToggleSuppressor 액션에 다른 키를 연결하세요."); // 사용자 조작을 몰래 변경하지 않음
                    }
                }

                action = map.AddAction("ToggleSuppressor", InputActionType.Button); // 비활성 사본에만 액션 추가
                changed = true; // 새 입력 변경 기록
            }

            if (action.bindings.Count == 0) // 사용자가 정한 기존 키가 없는 경우만 확인
            {
                action.AddBinding("<Keyboard>/b").WithGroup("Keyboard&Mouse"); // 소음기 테스트 전환키 추가
                changed = true; // 바인딩 변경 기록
            }

            if (changed) // 실제 바뀐 입력만 저장
            {
                File.WriteAllText(InputPath, copy.ToJson(), new UTF8Encoding(false)); // 기존 키와 ID를 유지한 JSON 저장
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate); // 변경 파일만 다시 임포트
            }
        }
        finally // 임시 액션 자료 정리
        {
            UnityEngine.Object.DestroyImmediate(copy); // 활성 원본과 분리된 사본 해제
        }
    }
}
#endif // 런타임 빌드에서 편집기 코드 제외
