#if UNITY_EDITOR // 수동 설치 메뉴 전용
using System; // 오류와 백업 시간
using System.Collections.Generic; // 장착 목록 보존
using System.IO; // 원본 씬과 입력 백업
using System.Text; // 한글 입력 파일 저장
using UnityEditor; // 에셋 편집
using UnityEditor.SceneManagement; // 지정 씬 저장
using UnityEngine; // 실제 플레이어 참조
using UnityEngine.InputSystem; // 비활성 입력 사본
using UnityEngine.SceneManagement; // 씬 로드 상태

public static class ProjectKDay13Setup // 이전 훈련장을 지우지 않는 확장 설치
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 실제 테스트 씬
    private const string InputPath = "Assets/InputSystem_Actions.inputactions"; // 기존 입력 파일
    private static bool applying; // 메뉴 중복 실행 잠금

    // 이번 일차 수동 설치
    public static void ApplySetup() // 총기와 확장 맵 동시 구성
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 상태 확인
        {
            Debug.LogWarning("Day13: Play를 중지하고 컴파일 완료 후 실행하세요."); // 작업 조건 안내
            return; // 실행 중 데이터 변경 금지
        }
        if (!File.Exists(ScenePath) || !File.Exists(InputPath)) // 이전 일차 기반 확인
        {
            Debug.LogError("Day13: Test 씬과 입력 파일이 필요합니다."); // 미완성 프로젝트 안내
            return; // 임의의 새 프로젝트 생성 금지
        }
        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 대상 씬 조회
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 자신이 여는 씬 기록
        if (!openedHere && scene.isDirty) // 사용자 미저장 변경 확인
        {
            Debug.LogWarning("Day13: Test 씬을 Ctrl+S로 저장한 뒤 실행하세요."); // 기존 작업 보호
            return; // 미저장 작업 자동 저장 금지
        }
        applying = true; // 재진입 방지
        string backup = string.Empty; // 오류 시 복구 경로
        try // 실패 시 기존 편집 상태 복구
        {
            if (openedHere) // 다른 씬에서 메뉴 실행
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); // 기존 열린 씬 유지
            }
            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어 조회
            PlayerEquipmentManager equipment = player != null ? player.GetComponent<PlayerEquipmentManager>() : null; // 이전 장비 시스템
            PlayerFirearmController firearm = player != null ? player.GetComponent<PlayerFirearmController>() : null; // 이전 사격 시스템
            if (equipment == null || firearm == null || equipment.WeaponCount != 4 || equipment.Tuning == null || equipment.Tuning.effectMaterial == null) // 호환 가능한 기반 확인
            {
                throw new InvalidOperationException("Day13: 12일차까지의 플레이어와 장비 구성을 확인하세요."); // 누락된 기반 안내
            }
            List<FirearmDefinition> retained = ProjectKDay11Setup.GetLoadout(firearm); // 사용자 총기 목록 보존
            List<FirearmDefinition> loadout = new List<FirearmDefinition>(); // 새 슬롯 순서 준비
            for (int i = 0; i < 3; i++) // 앞 세 대표 총기 유지
            {
                FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay12Catalog.DefinitionPath(i)); // 기존 총기 자료 조회
                if (definition == null) // 선행 무기 정의 확인
                {
                    throw new InvalidOperationException("Day13: 12일차 대표 총기 자료 누락"); // 초기값으로 기존 무기 대체 금지
                }
                loadout.Add(definition); // 기존 5~7번 슬롯 유지
            }
            PlayerInput input = player.GetComponent<PlayerInput>(); // 실제 입력 사용자
            if (input == null || input.actions != AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath)) // 잘못된 입력 경로 확인
            {
                throw new InvalidOperationException("Day13: PlayerInput의 입력 파일 경로를 확인하세요."); // 다른 입력 에셋을 조용히 변경하지 않음
            }
            string json = BuildInputJson(); // 파일 변경 전 입력 충돌 검사
            backup = "Library/ProjectKDay13Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"); // 실행별 고유 백업
            Directory.CreateDirectory(backup); // 백업 경로 확보
            File.Copy(ScenePath, Path.Combine(backup, "Test.unity")); // 실제 기존 씬 백업
            File.Copy(InputPath, Path.Combine(backup, "InputSystem_Actions.inputactions")); // 기존 키와 ID 백업
            FirearmDefinition[] newGuns = ProjectKDay13Weapons.Build(equipment.Tuning.effectMaterial); // 문지기와 천리안 생성
            loadout.AddRange(newGuns); // 8번과 9번 선택 슬롯
            foreach (FirearmDefinition extra in retained) // 사용자가 추가한 장비 유지
            {
                if (extra != null && !loadout.Contains(extra)) // 중복된 정의 제외
                {
                    loadout.Add(extra); // 추가 장비는 뒤쪽에 보존
                }
            }
            SerializedObject edit = new SerializedObject(firearm); // 장착 목록만 수정
            SerializedProperty slots = edit.FindProperty("loadout"); // 실제 총기 배열
            slots.arraySize = loadout.Count; // 사용할 총기 수
            for (int i = 0; i < loadout.Count; i++) // 참조 순서 저장
            {
                slots.GetArrayElementAtIndex(i).objectReferenceValue = loadout[i]; // 무기별 보존 탄약 구조 연결
            }
            edit.ApplyModifiedProperties(); // 배열 변경 적용
            TrainingCampusMarker campus = ProjectKDay13Campus.Build(scene, player, newGuns); // 넓은 맵과 적 재배치
            if (json != null) // 실제 키 추가가 발생한 경우
            {
                File.WriteAllText(InputPath, json, new UTF8Encoding(false)); // 원본 활성 액션을 건드리지 않는 파일 저장
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate); // 입력 재임포트
            }
            AssetDatabase.SaveAssets(); // 새 자료와 프리팹 먼저 저장
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 씬 저장 표시
            if (!EditorSceneManager.SaveScene(scene)) // 실제 저장 결과 확인
            {
                throw new IOException("Day13: Test 씬 저장 실패"); // 완료 메시지 오표시 방지
            }
            Selection.activeGameObject = campus.gameObject; // 새 구역을 Hierarchy에서 찾기 쉽게 선택
            Debug.Log("Day13 구성 완료: 8 문지기 / 9 천리안. 확장 구역 140x212m, 기존 적 " + campus.RelocatedActors + "명 이동. 스폰 옆 F 이동 단말기 이용. 백업: " + backup); // 실제 설치 결과 안내
        }
        catch (Exception error) // 오류 발생 시 복구 경로 표시
        {
            Debug.LogException(error); // 정확한 실패 원인 출력
            Debug.LogWarning("Day13 설치 실패: Test 씬을 저장하지 않고 다시 여세요. 백업: " + backup); // 부분 변경 저장 방지
        }
        finally // 자신이 열었던 씬만 정리
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 원래 닫혀 있던 씬 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 기존 작업 씬은 유지
            }
            applying = false; // 재시도 가능 상태
        }
    }

    private static string BuildInputJson() // 활성 액션 구조 변경 예외 방지
    {
        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(InputPath)); // 비활성 입력 사본
        try // 임시 객체 정리 보장
        {
            copy.Disable(); // 사본 액션 전부 비활성화
            InputActionMap map = copy.FindActionMap("Player", false); // 기존 플레이어 맵 조회
            if (map == null) // 기반 맵 확인
            {
                throw new InvalidOperationException("Day13: Player 입력 맵 누락"); // 잘못된 입력 구조 수정 중단
            }
            bool changed = false; // 변경 여부
            for (int slot = 8; slot <= 9; slot++) // 새 무기 선택 키만 추가
            {
                string actionName = "Equip" + slot; // 기존 장비 관리자 이름 규칙
                string key = "<Keyboard>/" + slot; // 숫자 키 경로
                InputAction action = map.FindAction(actionName, false); // 기존 사용자 단축키 조회
                if (action != null && action.bindings.Count > 0) // 사용자 설정 보존
                {
                    continue; // 기존 키 덮어쓰기 금지
                }
                foreach (InputBinding binding in map.bindings) // 다른 동작의 키 중복 검사
                {
                    if (binding.effectivePath == key && binding.action != actionName && (action == null || binding.action != action.id.ToString())) // 실제 키 충돌
                    {
                        throw new InvalidOperationException("Day13: 숫자 " + slot + "키가 사용 중입니다. " + actionName + "에 다른 키를 먼저 지정하세요."); // 임의 키 변경 방지
                    }
                }
                action = action ?? map.AddAction(actionName, InputActionType.Button); // 사본에만 입력 추가
                action.AddBinding(key).WithGroup("Keyboard&Mouse"); // 선택 키 연결
                changed = true; // 새 입력 기록
            }
            return changed ? copy.ToJson() : null; // 변경 없으면 원본 저장 생략
        }
        finally // 입력 사본 수명 정리
        {
            UnityEngine.Object.DestroyImmediate(copy); // 원본 액션 활성 상태 유지
        }
    }
}
#endif // 플레이어 빌드에서 설치 도구 제외
