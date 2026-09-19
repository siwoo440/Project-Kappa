#if UNITY_EDITOR // 에디터 전용 기능
using System; // 예외와 문자열 검사
using System.Collections.Generic; // 목록 관리
using System.IO; // 원본 파일과 백업 저장
using System.Text; // UTF-8 저장 설정
using UnityEditor; // 에셋 편집 기능
using UnityEditor.SceneManagement; // 씬 저장 기능
using UnityEngine; // 오브젝트 구성 기능
using UnityEngine.InputSystem; // 입력 액션 편집 기능
using UnityEngine.SceneManagement; // 씬 객체 조회

public static class ProjectKDay9Setup // 9일차 장비 자동 구성 도구
{
    public const string ScenePath = "Assets/_Project/Scenes/Test.unity"; // 기존 훈련장 경로
    public const string DataFolder = "Assets/_Project/Data/Day9"; // 새 장비 설정 경로
    private const string InputPath = "Assets/InputSystem_Actions.inputactions"; // 입력 에셋 경로
    private static double automaticTime; // 자동 적용 예약 시각
    private static bool applying; // 중복 설정 방지 상태
    private static string SetupKey => "ProjectK.Day9.Equipment.V1." + Application.dataPath; // 프로젝트별 설정 성공 기록

    [InitializeOnLoadMethod] // 컴파일 후 자동 구성 예약
    private static void Schedule() // 이전 일차 이후 설정 예약
    {
        if (File.Exists("Assets/_Project/Data/Day10/TestPistol_Definition.asset")) // 후속 일차 구성 존재 확인
        {
            return; // 10일차 장착 참조를 이전 자동 설정으로 덮어쓰지 않도록 보호
        }

        if (EditorPrefs.GetBool(SetupKey, false)) // 현재 프로젝트의 적용 성공 확인
        {
            return; // 에디터 재시작 시 씬 재생성 방지
        }

        automaticTime = EditorApplication.timeSinceStartup + 1.5d; // 임포트 완료 대기
        EditorApplication.update -= TryAutomaticSetup; // 중복 예약 제거
        EditorApplication.update += TryAutomaticSetup; // 자동 적용 예약
    }

    private static void TryAutomaticSetup() // 안전한 자동 구성 시점 확인
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < automaticTime) // 임포트와 컴파일 대기
        {
            return; // 다음 에디터 갱신까지 대기
        }

        EditorApplication.update -= TryAutomaticSetup; // 자동 실행 한 번으로 제한
        if (EditorApplication.isPlayingOrWillChangePlaymode) // 실행 중 씬 변경 방지
        {
            Debug.LogWarning("Day9: Play를 중지하고 Project K > Day 9 > Setup Equipment And Items 메뉴를 실행하세요."); // 수동 적용 안내
            return; // 자동 적용 중단
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 현재 훈련장 조회
        if (scene.IsValid() && scene.isLoaded && scene.isDirty) // 저장하지 않은 변경 보호
        {
            Debug.LogWarning("Day9: Test 씬을 저장한 뒤 Setup Equipment And Items 메뉴를 실행하세요."); // 저장 안내
            return; // 저장 전 씬 자동 변경 방지
        }

        ApplySetup(); // 장비 설정 실행
    }

    [MenuItem("Project K/Day 9/Setup Equipment And Items")] // 수동 설정 메뉴
    public static void ApplySetup() // 기존 씬을 유지하는 장비 구성
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) // 중복 실행과 플레이 상태 확인
        {
            Debug.LogWarning("Day9: Play 중지와 컴파일 완료 후 실행하세요."); // 실행 불가 안내
            return; // 변경 중단
        }

        if (!File.Exists(ScenePath)) // 필수 씬 확인
        {
            Debug.LogError("Day9: Assets/_Project/Scenes/Test.unity가 없습니다."); // 누락 씬 안내
            return; // 새 맵 임의 생성 방지
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath); // 씬 로드 상태 조회
        bool loadedHere = !scene.IsValid() || !scene.isLoaded; // 임시 로드 여부
        if (!loadedHere && scene.isDirty) // 저장 전 편집 내용 확인
        {
            Debug.LogWarning("Day9: Test 씬을 Ctrl+S로 저장한 뒤 다시 실행하세요."); // 편집 보호 안내
            return; // 저장 전 작업 덮어쓰기 방지
        }

        applying = true; // 재진입 잠금
        try // 적용 실패 시 잠금과 씬 정리
        {
            BackupFile(ScenePath); // 변경 전 씬 백업
            if (loadedHere) // 다른 씬을 보고 있는 상태 확인
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); // 원래 작업 씬 유지
            }

            GameObject player = FindNamed(scene, "Player"); // 기존 플레이어 검색
            if (player == null || player.GetComponent<PlayerCombatController>() == null) // 8일차 기반 확인
            {
                throw new InvalidOperationException("Day9 requires the Day8 Player and combat setup."); // 누락 기반에서 부분 설정 차단
            }

            ProjectKDay9ModelFactory.EnsureFolder(DataFolder); // 장비 설정 폴더 확보
            MeleeWeaponDefinition[] weapons = BuildWeaponData(); // 무기별 수치와 모형 확보
            EquipmentTuning tuning = BuildEquipmentData(); // 소모품과 보조장비 설정 확보
            ConfigureInput(); // 활성 원본과 분리된 입력 편집
            ConfigurePlayer(player, weapons, tuning); // 플레이어 장착 관리자 연결
            ConfigureEnemies(scene, player.transform); // 기존 적 마비와 참조 연결
            BuildStation(scene, weapons, tuning); // 기존 맵에 보급대 추가
            EditorSceneManager.MarkSceneDirty(scene); // 실제 변경 표시
            if (!EditorSceneManager.SaveScene(scene)) // 씬 저장 결과 확인
            {
                throw new IOException("Failed to save the Test scene."); // 실패 기록 중단
            }

            AssetDatabase.SaveAssets(); // 새 데이터와 프리팹 참조 저장
            EditorPrefs.SetBool(SetupKey, true); // 성공한 뒤에만 자동 적용 완료 기록
            Debug.Log("Day9 설정 완료: 1~4 무기 / Q·E 순환 / R 마비침 / V 아이템 선택 / G 사용 / F 보급대"); // 적용 결과 안내
        }
        catch (Exception exception) // 부분 설정 오류 확인
        {
            Debug.LogException(exception); // 실제 실패 원인 출력
        }
        finally // 적용 상태 복구
        {
            if (loadedHere && scene.IsValid() && scene.isLoaded) // 임시 씬 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 원래 열려 있던 씬 유지
            }

            applying = false; // 재시도 잠금 해제
        }
    }

    private static MeleeWeaponDefinition[] BuildWeaponData() // 무기 4종 수치 생성
    {
        string[] ids = new string[] // 테스트 무기 식별자
        {
            "D9-JEOLSEON", // 절선 식별자
            "D9-CHIMMUK", // 침묵 식별자
            "D9-PASEONG", // 파성 식별자
            "D9-CURRENT" // 전류봉 식별자
        };
        string[] names = new string[] // 한국어 장비 이름
        {
            "절선", // 균형형 검
            "침묵", // 고속 단검
            "파성", // 중량 대검
            "전류봉" // 자세 특화 봉
        };
        float[] hp = new float[] // 승인된 테스트 체력 피해
        {
            28f, // 1번 설정값
            18f, // 2번 설정값
            42f, // 3번 설정값
            14f // 4번 설정값
        };
        float[] posture = new float[] // 승인된 테스트 자세 피해
        {
            20f, // 1번 설정값
            10f, // 2번 설정값
            35f, // 3번 설정값
            34f // 4번 설정값
        };
        float[] intervals = new float[] // 승인된 테스트 공격 간격
        {
            0.55f, // 1번 설정값
            0.32f, // 2번 설정값
            0.90f, // 3번 설정값
            0.60f // 4번 설정값
        };
        float[] reach = new float[] // 임시 공격 중심 거리
        {
            1.35f, // 1번 설정값
            0.85f, // 2번 설정값
            1.65f, // 3번 설정값
            1.15f // 4번 설정값
        };
        float[] radius = new float[] // 임시 판정 반경
        {
            0.8f, // 1번 설정값
            0.65f, // 2번 설정값
            0.95f, // 3번 설정값
            0.75f // 4번 설정값
        };
        float[] duration = new float[] // 무기별 휘두르기 시간
        {
            0.42f, // 1번 설정값
            0.24f, // 2번 설정값
            0.72f, // 3번 설정값
            0.45f // 4번 설정값
        };
        float[] hitTimes = new float[] // 무기별 타격 시점
        {
            0.16f, // 1번 설정값
            0.09f, // 2번 설정값
            0.32f, // 3번 설정값
            0.18f // 4번 설정값
        };
        MeleeWeaponDefinition[] result = new MeleeWeaponDefinition[ids.Length]; // 완성 정의 목록

        for (int i = 0; i < ids.Length; i++) // 무기 데이터 생성
        {
            string statsPath = DataFolder + "/" + ids[i] + "_Stats.asset"; // 공통 데이터 경로
            WeaponData stats = AssetDatabase.LoadAssetAtPath<WeaponData>(statsPath); // 기존 조정 데이터 조회
            if (stats == null) // 처음 생성하는 무기 확인
            {
                stats = ScriptableObject.CreateInstance<WeaponData>(); // 기존 공통 데이터 형식 사용
                SerializedObject edit = new SerializedObject(stats); // private 필드 에디터 편집
                edit.FindProperty("id").stringValue = ids[i]; // 식별자 지정
                edit.FindProperty("displayName").stringValue = names[i]; // 표시 이름 지정
                edit.FindProperty("category").enumValueIndex = (int)WeaponCategory.Melee; // 근접 분류 지정
                edit.FindProperty("healthDamage").floatValue = hp[i]; // 체력 피해 지정
                edit.FindProperty("postureDamage").floatValue = posture[i]; // 자세 피해 지정
                edit.FindProperty("effectiveRange").floatValue = reach[i]; // 공격 중심 거리 지정
                edit.FindProperty("fireInterval").floatValue = intervals[i]; // 공격 간격 지정
                edit.ApplyModifiedPropertiesWithoutUndo(); // 새 데이터 필드 적용
                AssetDatabase.CreateAsset(stats, statsPath); // 공통 무기 데이터 저장
            }

            string definitionPath = DataFolder + "/" + ids[i] + "_Definition.asset"; // 동작 정의 경로
            result[i] = AssetDatabase.LoadAssetAtPath<MeleeWeaponDefinition>(definitionPath); // 기존 동작 정의 조회
            if (result[i] == null) // 새 동작 정의 확인
            {
                result[i] = ScriptableObject.CreateInstance<MeleeWeaponDefinition>(); // 모형과 동작 정의 생성
                result[i].Configure(stats, ProjectKDay9ModelFactory.WeaponPrefab(i), hitTimes[i], duration[i], radius[i]); // 데이터와 프리팹 연결
                AssetDatabase.CreateAsset(result[i], definitionPath); // 무기 정의 저장
            }
        }

        return result; // 무기 정의 목록 반환
    }

    private static EquipmentTuning BuildEquipmentData() // 소모품과 보조장비 설정 생성
    {
        string path = DataFolder + "/EquipmentTuning.asset"; // 장비 테스트 설정 경로
        EquipmentTuning settings = AssetDatabase.LoadAssetAtPath<EquipmentTuning>(path); // 기존 수치 조회
        if (settings == null) // 새 설정 확인
        {
            settings = ScriptableObject.CreateInstance<EquipmentTuning>(); // 기본 테스트 수치 생성
            AssetDatabase.CreateAsset(settings, path); // 장비 설정 저장
        }

        settings.dartModel = settings.dartModel != null ? settings.dartModel : ProjectKDay9ModelFactory.GadgetPrefab("DartLauncher"); // 마비침 모형 연결
        settings.injectorModel = settings.injectorModel != null ? settings.injectorModel : ProjectKDay9ModelFactory.GadgetPrefab("Injector"); // 회복 주입기 연결
        settings.lureModel = settings.lureModel != null ? settings.lureModel : ProjectKDay9ModelFactory.GadgetPrefab("NoiseLure"); // 소음 유인기 연결
        settings.smokeModel = settings.smokeModel != null ? settings.smokeModel : ProjectKDay9ModelFactory.GadgetPrefab("SmokeCapsule"); // 연막 캡슐 연결
        settings.effectMaterial = settings.effectMaterial != null ? settings.effectMaterial : ProjectKDay9ModelFactory.MaterialAsset("EffectLine", Color.white, false, false, true); // 선 효과 재질 연결
        settings.smokeMaterial = settings.smokeMaterial != null ? settings.smokeMaterial : ProjectKDay9ModelFactory.MaterialAsset("SoftSmoke", Color.white, false, true); // 연막 입자 재질 연결
        EditorUtility.SetDirty(settings); // 연결 변경 표시
        return settings; // 완성 장비 설정 반환
    }

    private static void ConfigureInput() // 원본 활성 상태와 분리된 입력 편집
    {
        if (!File.Exists(InputPath)) // 입력 원본 파일 확인
        {
            throw new FileNotFoundException("Day9 requires InputSystem_Actions.inputactions.", InputPath); // 입력 없는 부분 설치 방지
        }

        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(InputPath)); // 원본 ID를 보존한 비활성 입력 자료 생성
        try // 임시 입력 자료 수명 보호
        {
            copy.Disable(); // 편집 자료의 전체 액션 비활성화
            InputActionMap map = copy.FindActionMap("Player", false); // 플레이어 액션 맵 조회
            if (map == null) // 필수 맵 확인
            {
                throw new InvalidOperationException("Player action map is missing."); // 임의 맵 생성 방지
            }

            bool changed = false; // 실제 입력 변경 여부
            for (int i = 1; i <= 4; i++) // 직접 무기 선택 키 구성
            {
                changed |= EnsureBinding(map, "Equip" + i, "<Keyboard>/" + i); // 숫자 키 추가
            }

            changed |= EnsureBinding(map, "Previous", "<Keyboard>/q"); // 이전 무기 키 확보
            changed |= EnsureBinding(map, "Next", "<Keyboard>/e"); // 다음 무기 키 확보
            changed |= EnsureBinding(map, "UseSupport", "<Keyboard>/r"); // 마비침 사용 키 추가
            changed |= EnsureBinding(map, "CycleItem", "<Keyboard>/v"); // 소모품 선택 키 추가
            changed |= EnsureBinding(map, "UseItem", "<Keyboard>/g"); // 소모품 사용 키 추가
            if (changed) // 실제로 추가된 설정 확인
            {
                BackupFile(InputPath); // 기존 조작과 GUID 보존 백업
                File.WriteAllText(InputPath, copy.ToJson(), new UTF8Encoding(false)); // 변경된 입력 JSON만 저장
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport); // 실제 입력 파일 다시 불러오기
            }
        }
        finally // 임시 입력 객체 정리
        {
            UnityEngine.Object.DestroyImmediate(copy); // 활성 원본에 영향 없는 복사 자료 제거
        }
    }

    private static bool EnsureBinding(InputActionMap map, string name, string path) // 중복 없는 입력 바인딩 생성
    {
        InputAction action = map.FindAction(name, false); // 기존 액션 조회
        bool changed = action == null; // 신규 액션 여부
        if (action == null) // 액션 누락 확인
        {
            action = map.AddAction(name, InputActionType.Button); // 버튼 액션 생성
            action.expectedControlType = "Button"; // 버튼 입력 형식 지정
        }

        for (int i = 0; i < action.bindings.Count; i++) // 같은 바인딩 검색
        {
            if (string.Equals(action.bindings[i].path, path, StringComparison.OrdinalIgnoreCase)) // 기존 키 확인
            {
                return changed; // 중복 바인딩 생성 방지
            }
        }

        action.AddBinding(path).WithGroup("Keyboard&Mouse"); // 기존 키보드 마우스 그룹 사용
        return true; // 입력 변경 기록
    }

    private static void ConfigurePlayer(GameObject player, MeleeWeaponDefinition[] weapons, EquipmentTuning settings) // 플레이어 장비 연결
    {
        PlayerEquipmentManager manager = GetOrAdd<PlayerEquipmentManager>(player); // 장비 관리자 확보
        SupportEquipmentController support = GetOrAdd<SupportEquipmentController>(player); // 마비침 관리자 확보
        ConsumableController consumables = GetOrAdd<ConsumableController>(player); // 소모품 관리자 확보
        GetOrAdd<PlayerEquipmentHUD>(player); // 장비 HUD 하나만 연결
        Transform meleeSocket = EnsureChild(player.transform, "WeaponSocket_Day9"); // 새 무기 장착 위치 확보
        meleeSocket.localPosition = new Vector3(0.48f, 0.12f, 0.42f); // 중앙 피벗 플레이어의 손 위치
        meleeSocket.localRotation = Quaternion.Euler(20f, 0f, -22f); // 시작 검 자세 설정
        Transform supportSocket = EnsureChild(player.transform, "SupportSocket_Day9"); // 보조장비 장착 위치 확보
        supportSocket.localPosition = new Vector3(-0.36f, 0.08f, 0.4f); // 반대쪽 손목 위치
        supportSocket.localRotation = Quaternion.identity; // 전방 발사기 자세 설정
        manager.Configure(weapons, meleeSocket, settings); // 장비 정의 연결
        support.Configure(settings, supportSocket); // 보조장비 연결
        consumables.Configure(settings); // 소모품 연결
        EditorUtility.SetDirty(manager); // 장비 설정 저장 표시
        EditorUtility.SetDirty(support); // 보조장비 저장 표시
        EditorUtility.SetDirty(consumables); // 소모품 저장 표시
        Transform oldSocket = player.transform.Find("WeaponSocket_Day7"); // 이전 고정 검 조회
        if (oldSocket != null) // 이전 검 존재 확인
        {
            oldSocket.gameObject.SetActive(false); // 검 두 개 동시 표시 방지
        }
    }

    private static void ConfigureEnemies(Scene scene, Transform player) // 기존 적을 지우지 않고 상태 연결
    {
        GameObject routeRoot = FindNamed(scene, "E01_PatrolPoints"); // 기존 순찰 경로 조회
        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
        {
            foreach (EnemyActor actor in root.GetComponentsInChildren<EnemyActor>(true)) // 기존 생명 관리자 순회
            {
                GetOrAdd<EnemyStatusController>(actor.gameObject); // 공통 마비 상태 연결
                PatrolGuardAI patrol = actor.GetComponent<PatrolGuardAI>(); // 순찰 AI 조회
                if (patrol != null) // E-01 경비 확인
                {
                    string suffix = actor.name.EndsWith("_B", StringComparison.Ordinal) ? "B" : "A"; // 경비 순찰 경로 구분
                    List<Transform> points = new List<Transform>(); // 유효한 순찰 지점 목록
                    for (int i = 1; routeRoot != null && i <= 4; i++) // 기존 순찰 지점 검색
                    {
                        Transform point = routeRoot.transform.Find("Patrol_" + suffix + i); // 저장된 지점 조회
                        if (point != null) // 지점 존재 확인
                        {
                            points.Add(point); // 순찰 순서 복원
                        }
                    }

                    patrol.BindTargetAndRoute(player, points.ToArray()); // 비직렬화였던 참조 저장
                    EditorUtility.SetDirty(patrol); // 순찰 참조 저장 표시
                }

                E02SwordGuardAI swordGuard = actor.GetComponent<E02SwordGuardAI>(); // 검술 AI 조회
                if (swordGuard != null) // E-02 확인
                {
                    SerializedObject edit = new SerializedObject(swordGuard); // 이동 속도 유지한 참조 편집
                    edit.FindProperty("target").objectReferenceValue = player; // 저장 대상 연결
                    edit.ApplyModifiedPropertiesWithoutUndo(); // 대상 참조 저장
                }

                EnemyMeleeCombat melee = actor.GetComponent<EnemyMeleeCombat>(); // 공격 모형 참조 조회
                Transform socket = actor.transform.Find("Model/SwordSocket"); // 기존 E-02 검 소켓 조회
                if (melee != null && socket != null) // 저장할 검 소켓 확인
                {
                    melee.BindWeaponSocket(socket); // 공격 시각화 참조 저장
                    EditorUtility.SetDirty(melee); // 검 소켓 저장 표시
                }
            }

            foreach (DetectionSensor sensor in root.GetComponentsInChildren<DetectionSensor>(true)) // 경비와 카메라 탐지 순회
            {
                Transform pivot = sensor.transform.Find("RotationPivot"); // 카메라 실제 회전축 검색
                if (pivot != null) // D-01 카메라 확인
                {
                    sensor.ConfigureVisionSource(pivot, new Vector3(0f, 0.06f, 1.27f)); // 실제 렌즈 앞에서 감시
                    EditorUtility.SetDirty(sensor); // 시야 기준 저장 표시
                }
            }
        }
    }

    private static void BuildStation(Scene scene, MeleeWeaponDefinition[] weapons, EquipmentTuning settings) // 장비 진열 보급대 추가
    {
        if (FindNamed(scene, "Day9_EquipmentStation") != null) // 기존 보급대 확인
        {
            return; // 반복 적용 시 배치와 모형 유지
        }

        GameObject station = new GameObject("Day9_EquipmentStation"); // 보급대 루트 생성
        SceneManager.MoveGameObjectToScene(station, scene); // 훈련장 씬으로 이동
        station.transform.position = new Vector3(8f, 0f, -32f); // 시작 구역 오른쪽에 배치
        station.AddComponent<EquipmentTrainingStation>(); // F 보급 기능 연결
        BoxCollider collider = station.AddComponent<BoxCollider>(); // 보급대 상호작용 충돌체
        collider.center = new Vector3(0f, 1.2f, 0f); // 충돌체 중심 설정
        collider.size = new Vector3(5.8f, 2.4f, 1.1f); // 보급대 크기 설정
        Material dark = ProjectKDay9ModelFactory.MaterialAsset("EquipmentDark", new Color(0.08f, 0.11f, 0.15f)); // 보급대 본체 재질
        Material metal = ProjectKDay9ModelFactory.MaterialAsset("EquipmentMetal", new Color(0.58f, 0.67f, 0.73f)); // 보급대 테두리 재질
        ProjectKDay9ModelFactory.Part(station.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.52f, 0f), new Vector3(5.8f, 1.04f, 1.1f), dark); // 보급대 하부 본체
        ProjectKDay9ModelFactory.Part(station.transform, "Counter", PrimitiveType.Cube, new Vector3(0f, 1.06f, 0f), new Vector3(6f, 0.12f, 1.3f), metal); // 보급대 상판
        ProjectKDay9ModelFactory.Part(station.transform, "Header", PrimitiveType.Cube, new Vector3(0f, 2.7f, 0.15f), new Vector3(6f, 0.65f, 0.2f), dark); // 제목 패널
        ProjectKDay9ModelFactory.Label(station.transform, "DAY 9 - EQUIPMENT", new Vector3(0f, 2.78f, -0.02f), 0.06f); // 보급대 이름
        ProjectKDay9ModelFactory.Label(station.transform, "F : REFILL / HEAL", new Vector3(0f, 0.5f, -0.57f), 0.065f); // 조작 안내
        for (int i = 0; i < weapons.Length; i++) // 근접 무기 진열
        {
            GameObject sample = UnityEngine.Object.Instantiate(weapons[i].ModelPrefab, station.transform); // 전투 코드 없는 모형만 배치
            sample.name = "Display_" + i; // 진열 모형 이름 지정
            sample.transform.localPosition = new Vector3(-2.3f + i * 1.2f, 1.5f, 0f); // 진열 간격 적용
            sample.transform.localScale = Vector3.one * 0.58f; // 진열용 크기 조절
            ProjectKDay9ModelFactory.Label(station.transform, (i + 1).ToString(), new Vector3(-2.3f + i * 1.2f, 1.18f, -0.66f), 0.07f); // 무기 선택 번호 표시
        }

        GameObject[] items = new GameObject[] // 소모품 진열 목록

        {

            settings.injectorModel, // 1번 설정값

            settings.lureModel, // 2번 설정값

            settings.smokeModel // 3번 설정값

        };
        for (int i = 0; i < items.Length; i++) // 소모품 모형 진열
        {
            GameObject sample = UnityEngine.Object.Instantiate(items[i], station.transform); // 소모품 표시 모형 생성
            sample.transform.localPosition = new Vector3(2.25f, 1.35f + i * 0.37f, -0.2f); // 소모품 진열 위치
            sample.transform.localScale = Vector3.one * 0.7f; // 진열용 크기 조절
        }
    }

    public static GameObject FindNamed(Scene scene, string name) // 특정 씬 안의 이름 검색
    {
        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 비활성 객체까지 검색
            {
                if (child.name == name) // 이름 일치 확인
                {
                    return child.gameObject; // 해당 객체 반환
                }
            }
        }

        return null; // 검색 실패 반환
    }

    private static T GetOrAdd<T>(GameObject owner) where T : Component // 컴포넌트 중복 없이 확보
    {
        T component = owner.GetComponent<T>(); // 기존 컴포넌트 검색
        return component != null ? component : owner.AddComponent<T>(); // 누락된 컴포넌트만 추가
    }

    private static Transform EnsureChild(Transform parent, string name) // 장비 장착 위치 확보
    {
        Transform existing = parent.Find(name); // 기존 장착 위치 검색
        if (existing != null) // 기존 위치 확인
        {
            return existing; // 중복 장착 위치 생성 방지
        }

        GameObject child = new GameObject(name); // 새 장착 위치 생성
        child.transform.SetParent(parent, false); // 플레이어 하위 연결
        return child.transform; // 장착 위치 반환
    }

    private static void BackupFile(string path) // 원본 로컬 백업
    {
        string folder = "Library/ProjectKDay9Backups"; // 깃에 포함되지 않는 백업 경로
        Directory.CreateDirectory(folder); // 백업 폴더 생성
        string backup = Path.Combine(folder, Path.GetFileName(path) + ".before-day9"); // 최초 원본 백업 이름
        if (!File.Exists(backup) && File.Exists(path)) // 원본 백업 존재 확인
        {
            File.Copy(path, backup); // 최초 원본 파일 보존
        }
    }
}
#endif // 에디터 전용 기능 종료
