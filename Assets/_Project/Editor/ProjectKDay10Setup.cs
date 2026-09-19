#if UNITY_EDITOR // 에디터 전용 설정 도구
using System; // 오류와 백업 시각 기능
using System.Collections.Generic; // 기존 총기 목록 보존
using System.IO; // 씬과 입력 파일 백업
using System.Text; // UTF-8 저장 기능
using UnityEditor; // 에셋과 컴포넌트 편집
using UnityEditor.SceneManagement; // 훈련장 저장 기능
using UnityEngine; // 장착 오브젝트 구성
using UnityEngine.InputSystem; // 비활성 입력 사본 편집
using UnityEngine.SceneManagement; // 특정 씬 참조

public static class ProjectKDay10Setup // 기존 훈련장을 유지하는 권총 설정
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 기존 테스트 씬
    public const string DataFolder = "Assets/_Project/Data/Day10"; // 총기 데이터 폴더
    public const string DefinitionPath = DataFolder + "/TestPistol_Definition.asset"; // 총기 설정 에셋
    public const string StatsPath = DataFolder + "/TestPistol_Stats.asset"; // 기존 WeaponData 형식 에셋
    public const string InputPath = "Assets/InputSystem_Actions.inputactions"; // 기존 입력 파일
    private static bool applying; // 설정 재진입 방지

    [MenuItem("Project K/Day 10/Setup Pistol And Reload")] // 10일차 수동 적용 메뉴
    public static void ApplySetup() // 권총과 입력과 보급대 연결
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Day10: Play를 중지하고 컴파일과 임포트가 끝난 뒤 실행하세요."); // 실행 조건 안내
            return; // 실행 중 씬과 입력 수정 금지
        }

        if (!File.Exists(ScenePath) || !File.Exists(InputPath)) // 선행 훈련장과 입력 존재 확인
        {
            Debug.LogError("Day10: 기존 Test 씬과 InputSystem_Actions.inputactions가 필요합니다."); // 누락 기반 안내
            return; // 새 프로젝트 임의 생성 금지
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 기존 씬 로드 상태
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 임시 로드 여부
        if (!openedHere && scene.isDirty) // 저장하지 않은 작업 보호
        {
            Debug.LogWarning("Day10: Test 씬을 Ctrl+S로 저장한 뒤 다시 실행하세요."); // 저장 안내
            return; // 사용자 편집 덮어쓰기 방지
        }

        applying = true; // 재진입 차단
        try // 설정 오류 시 씬과 잠금 정리
        {
            string backup = BackupInputs(); // 원본 씬과 입력 백업
            if (openedHere) // 다른 작업 씬이 열려 있는 경우
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); // 기존 열린 씬을 유지한 추가 로드
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어 확인
            if (player == null || player.GetComponent<PlayerEquipmentManager>() == null || player.GetComponent<PlayerHealth>() == null) // 9일차 장비 기반 확인
            {
                throw new InvalidOperationException("Day10: 9일차 Player 장비 설정이 없습니다."); // 선행 단계 누락 명시
            }

            PlayerInput playerInput = player.GetComponent<PlayerInput>(); // 입력 연결 확인
            InputActionAsset expectedInput = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath); // 설정 대상 입력 에셋
            if (playerInput == null || expectedInput == null || playerInput.actions != expectedInput) // 다른 입력 에셋 임의 변경 방지
            {
                throw new InvalidOperationException("Day10: PlayerInput이 Assets/InputSystem_Actions.inputactions를 사용해야 합니다."); // 대상 에셋 불일치 안내
            }

            FirearmDefinition definition = BuildDefinition(); // 조정값을 유지한 권총 데이터 생성
            ConfigureInput(); // 비활성 JSON 사본에만 입력 추가
            ConfigurePlayer(player, definition); // 플레이어 장착 관리자 연결
            AddStationDisplay(scene, definition.ModelPrefab); // 기존 보급대에 진열만 추가
            EditorSceneManager.MarkSceneDirty(scene); // 실제 변경 표시
            if (!EditorSceneManager.SaveScene(scene)) // 씬 저장 결과 확인
            {
                throw new IOException("Day10: Test 씬 저장 실패"); // 저장 오류 안내
            }

            AssetDatabase.SaveAssets(); // 생성 에셋 저장
            Debug.Log("Day10 설정 완료: 5 권총 / LMB 발사 / RMB 조준 / T 재장전 / R 마비침 유지. 백업: " + backup); // 적용 결과 안내
        }
        catch (Exception exception) // 설정 오류 보고
        {
            Debug.LogException(exception); // 실제 실패 원인 출력
        }
        finally // 임시 작업 정리
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 자신이 연 씬만 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 원래 작업 씬으로 복귀
            }

            applying = false; // 재시도 허용
        }
    }

    private static FirearmDefinition BuildDefinition() // 검증용 수치와 프리팹 연결
    {
        ProjectKDay10ModelFactory.EnsureFolder(DataFolder); // 신규 데이터 폴더 확보
        WeaponData stats = AssetDatabase.LoadAssetAtPath<WeaponData>(StatsPath); // 기존 조정 수치 검색
        if (stats == null) // 최초 생성 확인
        {
            stats = ScriptableObject.CreateInstance<WeaponData>(); // 기존 공통 수치 형식 재사용
            SerializedObject edit = new SerializedObject(stats); // 비공개 수치의 에디터 편집
            edit.FindProperty("id").stringValue = "D10-TEST-PISTOL"; // 스토리 무기와 구분한 검증용 식별자
            edit.FindProperty("displayName").stringValue = "검증용 권총"; // 테스트 장비 이름
            edit.FindProperty("category").enumValueIndex = (int)WeaponCategory.Sidearm; // 권총 분류
            edit.FindProperty("healthDamage").floatValue = 32f; // 검증용 체력 피해
            edit.FindProperty("postureDamage").floatValue = 18f; // 검증용 자세 피해
            edit.FindProperty("effectiveRange").floatValue = 25f; // 검증용 유효 거리
            edit.FindProperty("falloffEndRange").floatValue = 40f; // 검증용 감쇠 종료 거리
            edit.FindProperty("noiseRadius").floatValue = 25f; // 검증용 총성 반경
            edit.FindProperty("magazineSize").intValue = 6; // 검증용 탄창 용량
            edit.FindProperty("reserveAmmo").intValue = 24; // 훈련용 예비탄 지급량
            edit.FindProperty("fireInterval").floatValue = 0.65f; // 검증용 단발 간격
            edit.ApplyModifiedPropertiesWithoutUndo(); // 새 에셋 값 적용
            AssetDatabase.CreateAsset(stats, StatsPath); // 공통 무기 수치 저장
        }

        FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(DefinitionPath); // 기존 총기 조정값 확인
        if (definition == null) // 최초 연결 확인
        {
            EquipmentTuning tuning = AssetDatabase.LoadAssetAtPath<EquipmentTuning>("Assets/_Project/Data/Day9/EquipmentTuning.asset"); // 검증된 공용 효과 재질 조회
            if (tuning == null || tuning.effectMaterial == null) // 궤적 재질 존재 확인
            {
                throw new InvalidOperationException("Day10: 9일차 EquipmentTuning의 effectMaterial을 확인하세요."); // 잘못된 효과 재질 생성 방지
            }

            definition = ScriptableObject.CreateInstance<FirearmDefinition>(); // 총기 동작 정의 생성
            definition.Configure(stats, ProjectKDay10ModelFactory.GetPistolPrefab(), tuning.effectMaterial); // 모형과 수치와 궤적 재질 연결
            AssetDatabase.CreateAsset(definition, DefinitionPath); // 동작 정의 저장
        }

        if (!definition.IsValid || definition.ModelPrefab.GetComponent<FirearmView>() == null || definition.ModelPrefab.GetComponent<FirearmView>().Muzzle == null) // 기존 수동 수정 이후에도 필수 참조 확인
        {
            throw new InvalidOperationException("Day10: 권총 정의의 Stats / ModelPrefab / FirearmView.Muzzle 참조를 확인하세요."); // 참조 누락 안내
        }

        return definition; // 기존 값 보존한 총기 정의 반환
    }

    private static void ConfigurePlayer(GameObject player, FirearmDefinition definition) // 기존 무기를 건드리지 않는 총기 추가
    {
        Transform socket = player.transform.Find("FirearmSocket_Day10"); // 기존 총기 장착 위치 확인
        if (socket == null) // 최초 위치 생성 확인
        {
            socket = new GameObject("FirearmSocket_Day10").transform; // 총기 전용 소켓 생성
            socket.SetParent(player.transform, false); // 플레이어 하위 연결
            CharacterController controller = player.GetComponent<CharacterController>(); // 몸 중심 확인
            socket.localPosition = (controller != null ? controller.center : Vector3.zero) + new Vector3(0.38f, 0.12f, 0.32f); // 오른손 부근의 총기 위치
        }

        PlayerFirearmController firearm = player.GetComponent<PlayerFirearmController>(); // 기존 총기 관리자 확인
        if (firearm == null) // 관리자 최초 추가 확인
        {
            firearm = player.AddComponent<PlayerFirearmController>(); // 새 총기 관리자 한 개만 추가
        }

        SerializedObject existing = new SerializedObject(firearm); // 기존 장비 목록 보존
        SerializedProperty list = existing.FindProperty("loadout"); // 저장된 총기 목록
        List<FirearmDefinition> definitions = new List<FirearmDefinition>(); // 재사용할 총기 목록
        for (int i = 0; i < list.arraySize; i++) // 기존 정의 순회
        {
            FirearmDefinition entry = list.GetArrayElementAtIndex(i).objectReferenceValue as FirearmDefinition; // 총기 참조 조회
            if (entry != null && !definitions.Contains(entry)) // 유효한 고유 총기 확인
            {
                definitions.Add(entry); // 기존 총기 유지
            }
        }

        if (!definitions.Contains(definition)) // 테스트 권총 연결 여부 확인
        {
            definitions.Add(definition); // 누락된 권총만 추가
        }

        firearm.Configure(definitions.ToArray(), socket); // 모형 위치와 정의 목록 연결
        EditorUtility.SetDirty(firearm); // 씬 직렬화 변경 표시
        if (player.GetComponent<PlayerEquipmentHUD>() == null) // HUD 존재 확인
        {
            player.AddComponent<PlayerEquipmentHUD>(); // 누락된 경우에만 기존 통합 HUD 추가
        }
    }

    private static void ConfigureInput() // 활성 원본을 건드리지 않는 입력 추가
    {
        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(InputPath)); // 기존 ID를 보존한 비활성 입력 사본
        try // 임시 입력 수명 보호
        {
            copy.Disable(); // 사본의 액션 전체 비활성화
            InputActionMap map = copy.FindActionMap("Player", false); // 기존 맵 검색
            if (map == null || map.FindAction("Attack", false) == null || map.FindAction("Defense", false) == null) // 기존 공격과 방어 입력 확인
            {
                throw new InvalidOperationException("Day10: Player / Attack / Defense 입력을 확인하세요."); // 다른 맵 임의 생성 방지
            }

            bool changed = EnsureBinding(map, "Equip5", "<Keyboard>/5"); // 권총 선택 키 추가
            changed |= EnsureBinding(map, "Reload", "<Keyboard>/t"); // R 마비침을 보존한 재장전 키 추가
            if (changed) // 실제 추가가 있는 경우만 저장
            {
                File.WriteAllText(InputPath, copy.ToJson(), new UTF8Encoding(false)); // 기존 액션 ID와 바인딩을 유지한 JSON 저장
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate); // 변경된 입력만 재임포트
            }
        }
        finally // 원본 입력 활성 상태와 분리된 사본 정리
        {
            UnityEngine.Object.DestroyImmediate(copy); // 임시 입력 해제
        }
    }

    private static bool EnsureBinding(InputActionMap map, string name, string path) // 중복 없는 단추 입력 생성
    {
        bool changed = false; // 실제 입력 변경 상태
        InputAction action = map.FindAction(name, false); // 기존 액션 조회
        if (action == null) // 신규 액션 확인
        {
            action = map.AddAction(name, InputActionType.Button); // 비활성 맵에만 액션 추가
            changed = true; // 새 액션 기록
        }

        foreach (InputBinding binding in action.bindings) // 기존 단축키 조회
        {
            if (binding.path == path) // 동일 단축키 존재 확인
            {
                return changed; // 바인딩 중복 생성 방지
            }
        }

        action.AddBinding(path).WithGroup("Keyboard&Mouse"); // 키보드 단축키 연결
        return true; // 입력 변경 기록
    }

    private static void AddStationDisplay(Scene scene, GameObject prefab) // 기존 보급대 옆 작은 권총 진열
    {
        GameObject station = ProjectKDay9Setup.FindNamed(scene, "Day9_EquipmentStation"); // 기존 보급대 검색
        if (station == null || station.transform.Find("Day10_PistolDisplay") != null) // 보급대 존재와 중복 확인
        {
            return; // 기존 지도와 진열 배치 보존
        }

        Transform display = new GameObject("Day10_PistolDisplay").transform; // 새 진열 루트
        display.SetParent(station.transform, false); // 기존 보급대 하위 연결
        display.localPosition = new Vector3(3.6f, 0f, 0f); // 기존 무기를 가리지 않는 옆 위치
        Material material = ProjectKDay10ModelFactory.MaterialAsset("DisplayMetal", new Color(0.16f, 0.22f, 0.28f), false); // 진열 받침 재질
        ProjectKDay10ModelFactory.Part(display, "Pedestal", PrimitiveType.Cube, new Vector3(0f, 0.64f, 0f), new Vector3(1.15f, 1.28f, 0.95f), material, Vector3.zero); // 작은 받침대 외형
        GameObject sample = UnityEngine.Object.Instantiate(prefab, display); // 권총 진열 모형
        sample.name = "PistolDisplayModel"; // 진열 모형 식별
        sample.transform.localPosition = new Vector3(0f, 1.73f, 0f); // 받침대 위 표시
        sample.transform.localRotation = Quaternion.Euler(0f, 70f, 0f); // 측면 형태를 볼 수 있는 회전
        sample.transform.localScale = Vector3.one * 1.25f; // 진열용 크기
        UnityEngine.Object.DestroyImmediate(sample.GetComponent<FirearmView>()); // 진열품의 발사 효과 실행 방지
        ProjectKDay9ModelFactory.Label(display, "5 : TEST PISTOL", new Vector3(0f, 1.16f, -0.50f), 0.03f); // 새 장비 선택 번호
        ProjectKDay9ModelFactory.Label(display, "T : RELOAD", new Vector3(0f, 0.96f, -0.50f), 0.03f); // 재장전 키 안내
    }

    private static string BackupInputs() // 씬과 입력 원본 백업
    {
        string folder = "Library/ProjectKDay10Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"); // 실행별 백업 경로
        Directory.CreateDirectory(folder); // 백업 폴더 생성
        File.Copy(ScenePath, Path.Combine(folder, "Test.unity"), false); // 씬 변경 전 원본 저장
        File.Copy(InputPath, Path.Combine(folder, "InputSystem_Actions.inputactions"), false); // 입력 변경 전 원본 저장
        return folder; // 백업 위치 반환
    }
}
#endif // 에디터 전용 코드 제외
