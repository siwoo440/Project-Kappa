#if UNITY_EDITOR // 대표 총기 수동 설치 도구
using System; // 오류와 백업 시각 기능
using System.Collections.Generic; // 사용자 총기 목록 보존
using System.IO; // 원본 파일 백업
using System.Text; // 한글 입력 자료 저장
using UnityEditor; // 에셋과 저장 필드 편집
using UnityEditor.SceneManagement; // 훈련장 저장
using UnityEngine; // 플레이어와 모형 연결
using UnityEngine.InputSystem; // 활성 원본과 분리한 입력 설정
using UnityEngine.SceneManagement; // 대상 씬 조회

public static class ProjectKDay12Setup // 기존 일차를 유지하는 대표 총기 설치
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 기존 훈련장 경로
    public const string InputPath = "Assets/InputSystem_Actions.inputactions"; // 기존 입력 에셋 경로
    private static bool applying; // 중복 메뉴 실행 잠금

    [MenuItem("Project K/Day 12/Setup Three Representative Firearms")] // 12일차 수동 설정 메뉴
    public static void ApplySetup() // 세 총기와 입력과 테스트 구역 구성
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Day12: Play를 중지하고 컴파일·임포트 완료 후 실행하세요."); // 사용 조건 안내
            return; // 실행 중 게임 구조 변경 금지
        }

        if (!File.Exists(ScenePath) || !File.Exists(InputPath)) // 기존 프로젝트 기반 확인
        {
            Debug.LogError("Day12: Test 씬과 InputSystem_Actions.inputactions가 필요합니다."); // 필수 경로 안내
            return; // 새 훈련장 임의 생성 금지
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 대상 씬 로드 상태
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 자신이 여는 씬 기록
        if (!openedHere && scene.isDirty) // 저장하지 않은 사용자 작업 확인
        {
            Debug.LogWarning("Day12: Test 씬을 Ctrl+S로 저장한 뒤 실행하세요."); // 기존 작업 보호
            return; // 저장되지 않은 수정 덮어쓰기 방지
        }

        applying = true; // 설정 재진입 방지
        string backup = string.Empty; // 실패 시 안내할 백업 경로
        try // 실패 시에도 로드한 씬과 잠금 정리
        {
            if (openedHere) // 다른 씬에서 메뉴 실행 확인
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); // 기존 작업 씬 유지
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어 조회
            PlayerEquipmentManager equipment = player != null ? player.GetComponent<PlayerEquipmentManager>() : null; // 선행 장비 시스템 확인
            PlayerFirearmController firearm = player != null ? player.GetComponent<PlayerFirearmController>() : null; // 선행 총기 기능 확인
            if (equipment == null || firearm == null || equipment.WeaponCount != 4 || equipment.Tuning == null || equipment.Tuning.effectMaterial == null || ProjectKDay11Setup.FindCamera(scene) == null) // 기반 참조와 표준 슬롯 구성 확인
            {
                throw new InvalidOperationException("Day12: 9~11일차 장비·카메라·근접 무기 4개 구성을 확인하세요."); // 임의로 다른 장비 구성을 변경하지 않음
            }

            if (ProjectKDay9Setup.FindNamed(scene, "Day11_ShootingRange") == null) // 옆 구간 연결 기준 확인
            {
                throw new InvalidOperationException("Day12: 11일차 사격장 배치가 필요합니다."); // 잘못된 위치 생성 방지
            }

            PlayerInput playerInput = player.GetComponent<PlayerInput>(); // 실제 입력 에셋 사용자 확인
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath); // 설정할 입력 원본 조회
            if (playerInput == null || input == null || playerInput.actions != input) // 다른 입력 파일을 사용하는 경우 확인
            {
                throw new InvalidOperationException("Day12: PlayerInput의 입력 에셋 경로를 확인하세요."); // 엉뚱한 입력 파일 편집 방지
            }

            string inputJson = BuildInputJson(); // 에셋 변경 전에 기존 키 충돌 검사
            backup = "Library/ProjectKDay12Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"); // 실행별 고유 백업 폴더
            Directory.CreateDirectory(backup); // 백업 위치 확보
            File.Copy(ScenePath, Path.Combine(backup, "Test.unity")); // 원본 훈련장 백업
            File.Copy(InputPath, Path.Combine(backup, "InputSystem_Actions.inputactions")); // 입력 원본 백업
            List<FirearmDefinition> previous = ProjectKDay11Setup.GetLoadout(firearm); // 사용자의 기존 총기 목록 보존
            FirearmDefinition[] guns = ProjectKDay12Catalog.Build(equipment.Tuning.effectMaterial); // 세 무기의 독립 에셋 생성
            List<FirearmDefinition> loadout = new List<FirearmDefinition>(guns); // 기본 순서 유령손·골목비·청룡선
            foreach (FirearmDefinition definition in previous) // 사용자 추가 총기 보존
            {
                if (definition != null && !loadout.Contains(definition) && AssetDatabase.GetAssetPath(definition) != ProjectKDay10Setup.DefinitionPath) // 기존 검증용 권총만 기본 목록에서 제외
                {
                    loadout.Add(definition); // 별도로 추가한 다른 총기는 뒤에 유지
                }
            }

            SerializedObject edit = new SerializedObject(firearm); // 기존 총기 장착 소켓 보존
            SerializedProperty slots = edit.FindProperty("loadout"); // 변경할 장착 목록만 조회
            if (slots == null) // 필드 이름과 새 코드 일치 확인
            {
                throw new InvalidOperationException("Day12: PlayerFirearmController.loadout 필드가 없습니다."); // 부분 연결 방지
            }

            Undo.RecordObject(firearm, "Day12 Firearm Loadout"); // 목록 변경 되돌리기 기록
            slots.arraySize = loadout.Count; // 새 총기 개수 적용
            for (int i = 0; i < loadout.Count; i++) // 총기 슬롯 참조 저장
            {
                slots.GetArrayElementAtIndex(i).objectReferenceValue = loadout[i]; // 기존 탄약 구조를 사용하는 정의 연결
            }

            edit.ApplyModifiedProperties(); // 목록 변경 적용
            int actors = ProjectKDay12HitboxSetup.ConfigureScene(scene); // 기존 적 부위 판정 연결
            ProjectKDay12RangeFactory.Build(scene, guns); // 기존 구간 옆 비교장 추가
            GameObject oldDisplay = ProjectKDay9Setup.FindNamed(scene, "Day10_PistolDisplay"); // 옛 5번 권총 안내 진열 조회
            if (oldDisplay != null && oldDisplay.activeSelf) // 새 슬롯과 충돌하는 옛 안내 확인
            {
                Undo.RecordObject(oldDisplay, "Day12 Hide Old Pistol Display"); // 재활성화 가능한 변경 기록
                oldDisplay.SetActive(false); // 에셋과 객체는 남기고 옛 진열만 숨김
            }

            if (inputJson != null) // 새 키를 추가한 경우만 저장
            {
                File.WriteAllText(InputPath, inputJson, new UTF8Encoding(false)); // 비활성 사본의 변경 내용만 저장
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate); // 입력 에셋 재임포트
            }

            AssetDatabase.SaveAssets(); // 프리팹과 데이터 먼저 저장
            EditorSceneManager.MarkSceneDirty(scene); // 실제 변경 씬 저장 표시
            if (!EditorSceneManager.SaveScene(scene)) // 파일 저장 성공 확인
            {
                throw new IOException("Day12: Test 씬 저장 실패"); // 잘못된 완료 안내 방지
            }

            Debug.Log("Day12 설정 완료: 5 유령손 / 6 골목비 / 7 청룡선 / T 재장전. 부위 연결 " + actors + "개. 추가 소음기 배율은 미적용. 백업: " + backup); // 적용 범위와 백업 위치 안내
        }
        catch (Exception exception) // 실패 원인과 복구 자료 안내
        {
            Debug.LogException(exception); // 정확한 실패 위치 출력
            if (!string.IsNullOrEmpty(backup)) // 생성된 백업 존재 확인
            {
                Debug.LogWarning("Day12 원본 백업: " + backup + ". 실패 후에는 Test 씬을 저장하지 않고 다시 여세요."); // 부분 변경 저장 방지 안내
            }
        }
        finally // 기존 편집 환경 복구
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 자신이 연 씬만 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 다른 작업 씬 유지
            }

            applying = false; // 실패 후 재실행 가능 상태
        }
    }

    private static string BuildInputJson() // 활성 InputActionAsset을 수정하지 않는 키 추가
    {
        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(InputPath)); // 기존 ID를 보존한 비활성 JSON 사본
        try // 임시 입력 사본 정리 보장
        {
            copy.Disable(); // 사본 전체 액션 비활성화
            InputActionMap map = copy.FindActionMap("Player", false); // 기존 플레이어 맵 조회
            if (map == null || map.FindAction("Attack", false) == null || map.FindAction("Defense", false) == null || map.FindAction("Reload", false) == null) // 선행 사격 입력 확인
            {
                throw new InvalidOperationException("Day12: Player / Attack / Defense / Reload 액션을 확인하세요."); // 다른 맵 임의 생성 금지
            }

            bool changed = false; // 실제 추가 내용 존재 여부
            for (int slot = 5; slot <= 7; slot++) // 대표 총기 숫자 키 구성
            {
                string name = "Equip" + slot; // 기존 장비 관리자 액션 이름
                string key = "<Keyboard>/" + slot; // 숫자 키 바인딩
                InputAction action = map.FindAction(name, false); // 기존 사용자 설정 액션 조회
                if (action != null && action.bindings.Count > 0) // 이미 정의한 사용자 단축키 확인
                {
                    continue; // 기존 키와 ID 보존
                }

                foreach (InputBinding binding in map.bindings) // 다른 기능의 같은 숫자 키 확인
                {
                    if (binding.effectivePath == key && binding.action != name && (action == null || binding.action != action.id.ToString())) // 실제 키 충돌 검사
                    {
                        throw new InvalidOperationException("Day12: " + key + "가 다른 기능에 사용 중입니다. " + name + " 액션에 원하는 키를 먼저 연결하세요."); // 기존 키를 몰래 변경하지 않음
                    }
                }

                action = action ?? map.AddAction(name, InputActionType.Button); // 사본에만 새 장비 액션 추가
                action.AddBinding(key).WithGroup("Keyboard&Mouse"); // 숫자 선택 키 연결
                changed = true; // 입력 변경 기록
            }

            return changed ? copy.ToJson() : null; // 변화가 없으면 원본 재저장 생략
        }
        finally // 복제 입력 수명 정리
        {
            UnityEngine.Object.DestroyImmediate(copy); // 원본 활성 상태를 건드리지 않는 정리
        }
    }
}
#endif // 런타임에서 수동 설치 도구 제외
