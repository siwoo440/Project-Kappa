#if UNITY_EDITOR // 에디터 전용 검증
using System; // 검증 실패 보고
using System.IO; // 입력 파일 확인
using UnityEditor; // 에셋과 직렬화 검증
using UnityEditor.SceneManagement; // 검증용 씬 로드
using UnityEngine; // 설정 참조 검증
using UnityEngine.InputSystem; // 입력 바인딩 검증
using UnityEngine.SceneManagement; // 특정 씬 조회

public static class ProjectKDay10Validation // 설정과 탄약 상태 검증 메뉴
{
    private static int assertions; // 이번 실행 검사 수

    [MenuItem("Project K/Day 10/Validate Pistol Setup")] // 설정 검증 메뉴
    public static void ValidateSetup() // 씬 변경 없이 참조와 탄약 규칙 검사
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) // 안정된 에디터 상태 확인
        {
            Debug.LogWarning("Day10 검증: Play 중지와 컴파일 완료 후 실행하세요."); // 검증 실행 조건 안내
            return; // 실행 중 씬 검증 중단
        }

        Scene scene = SceneManager.GetSceneByPath(ProjectKDay10Setup.ScenePath); // 대상 씬 조회
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 임시 로드 여부 기록
        assertions = 0; // 이번 검사 초기화
        try // 검사 실패 원인 보고
        {
            RunAmmoChecks(); // 실제 C# 탄약 상태 전이 검사
            FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay10Setup.DefinitionPath); // 생성한 총기 정의
            Check(definition != null && definition.IsValid, "총기 정의와 기본 수치 연결"); // 필수 에셋 검사
            FirearmView view = definition.ModelPrefab.GetComponent<FirearmView>(); // 프리팹의 총구 제어
            Check(view != null && view.Muzzle != null, "프리팹의 실제 총구 참조"); // 명중 기준 검사
            Check(definition.ModelPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "총기 장식 콜라이더 제거"); // 자기 충돌 검사
            Check(definition.TracerMaterial != null, "총알 궤적 재질"); // 효과 누락 검사
            Check(File.Exists(ProjectKDay10Setup.ScenePath), "Test 씬 존재"); // 씬 경로 검사
            if (openedHere) // 다른 씬을 보고 있는 경우
            {
                scene = EditorSceneManager.OpenScene(ProjectKDay10Setup.ScenePath, OpenSceneMode.Additive); // 읽기용 씬 추가 로드
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 검증할 플레이어
            Check(player != null, "Player 존재"); // 플레이어 검색 검사
            Check(player.GetComponents<PlayerFirearmController>().Length == 1, "총기 관리자 한 개"); // 중복 컴포넌트 검사
            Check(player.GetComponents<PlayerEquipmentHUD>().Length == 1, "통합 장비 HUD 한 개"); // 중복 HUD 검사
            Check(player.GetComponent<PlayerEquipmentManager>() != null && player.GetComponent<PlayerCombatController>() != null && player.GetComponent<PlayerDefenseController>() != null, "기존 근접 장비와 방어 유지"); // 이전 일차 참조 검사
            Check(player.GetComponent<PlayerAssassination>() != null && player.GetComponent<SupportEquipmentController>() != null && player.GetComponent<ConsumableController>() != null, "암살과 마비침과 소모품 유지"); // 기존 기능 보존 검사
            SerializedObject firearm = new SerializedObject(player.GetComponent<PlayerFirearmController>()); // 저장된 장착 참조 조회
            Check(firearm.FindProperty("firearmSocket").objectReferenceValue != null, "총기 장착 소켓 저장"); // 소켓 직렬화 검사
            SerializedProperty loadout = firearm.FindProperty("loadout"); // 총기 목록 확인
            bool linked = false; // 검증용 총기 연결 여부
            for (int i = 0; i < loadout.arraySize; i++) // 연결 총기 순회
            {
                linked |= loadout.GetArrayElementAtIndex(i).objectReferenceValue == definition; // 실제 생성 정의 연결 검사
            }

            Check(linked, "생성한 권총 정의의 플레이어 연결"); // 잘못된 에셋 참조 검사
            ValidateInput(); // 실제 입력 JSON 검사
            Debug.Log("Day10 설정 및 탄약 규칙 검사 " + assertions + "건 통과. 실제 명중·엄폐·조준 화면은 Play에서 확인하세요."); // 검증 범위를 구분한 결과 출력
        }
        catch (Exception exception) // 실패 검사 출력
        {
            Debug.LogError("Day10 검증 실패: " + exception.Message); // 실패 원인 안내
        }
        finally // 읽기용으로 연 씬만 정리
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 임시 로드 씬 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 저장 없이 원래 씬으로 복귀
            }
        }
    }

    [MenuItem("Project K/Day 10/Test Ammo Rules")] // 씬 없이 실행 가능한 탄약 검사
    public static void TestAmmoRules() // 순수 C# 상태 규칙 검사
    {
        assertions = 0; // 검사 수 초기화
        try // 검증 실패 구분
        {
            RunAmmoChecks(); // 탄약 상태 전이 실행
            Debug.Log("Day10 탄약 규칙 " + assertions + "건 통과"); // 순수 상태 검사 결과
        }
        catch (Exception exception) // 실제 실패 출력
        {
            Debug.LogError("Day10 탄약 검사 실패: " + exception.Message); // 실패 이유 안내
        }
    }

    private static void RunAmmoChecks() // 실제 구현 클래스의 회귀 검사
    {
        FirearmRuntimeState gun = new FirearmRuntimeState(6, 24, 0.65f); // 검증용 새 권총
        Check(gun.Rounds == 6 && gun.Reserve == 24, "최초 탄약 지급"); // 초기값 검사
        Check(gun.TryFire(0f) && gun.Rounds == 5, "한 발에 탄약 하나 소모"); // 정상 발사 검사
        Check(!gun.TryFire(0.1f) && gun.Rounds == 5, "발사 간격 중 중복 소모 금지"); // 연사 제한 검사
        Check(gun.TryFire(1f) && gun.Rounds == 4, "대기 뒤 발사 가능"); // 재사용 검사
        Check(gun.TryBeginReload(2f, 1.4f), "빈 탄창 재장전 시작"); // 정상 재장전 검사
        Check(!gun.TryBeginReload(2.1f, 1.4f), "중복 재장전 금지"); // 중복 입력 검사
        Check(!gun.TryFire(2.2f) && gun.Rounds == 4, "재장전 중 발사 차단"); // 재장전과 공격 충돌 검사
        Check(!gun.TickReload(2.5f) && gun.Reserve == 24, "완료 전 예비탄 보존"); // 중간 상태 검사
        Check(gun.ReloadProgress(2.7f) > 0f && gun.ReloadProgress(2.7f) < 1f, "재장전 게이지 범위"); // 진행도 검사
        gun.CancelReload(); // 무기 교체 상황 재현
        Check(!gun.IsReloading && gun.Rounds == 4 && gun.Reserve == 24, "취소 시 장탄수와 예비탄 보존"); // 취소 불변성 검사
        Check(!gun.TickReload(100f), "취소된 재장전의 나중 보충 방지"); // 예약 잔류 검사
        Check(gun.TryBeginReload(101f, 1f) && gun.TickReload(102f), "취소 뒤 새 재장전 가능"); // 재시도 검사
        Check(gun.Rounds == 6 && gun.Reserve == 22, "빈 공간만 보충하고 같은 양 차감"); // 탄약 보존 검사
        Check(!gun.TickReload(103f) && gun.Reserve == 22, "완료 재장전 중복 차감 방지"); // 완료 중복 검사
        Check(!gun.TryBeginReload(104f, 1f), "가득 찬 탄창 재장전 차단"); // 불필요한 재장전 검사

        FirearmRuntimeState low = new FirearmRuntimeState(3, 1, 0.1f); // 예비탄 부족 사례
        low.TryFire(0f); // 첫 발 소모
        low.TryFire(1f); // 둘째 발 소모
        low.TryFire(2f); // 마지막 발 소모
        Check(!low.TryFire(3f) && low.Rounds == 0, "빈 탄창 발사 차단"); // 음수 탄약 검사
        Check(low.TryBeginReload(4f, 1f) && low.TickReload(5f), "적은 예비탄으로 재장전"); // 부분 보충 검사
        Check(low.Rounds == 1 && low.Reserve == 0, "부족한 예비탄만큼만 보충"); // 보충량 검사
        Check(!low.TryBeginReload(6f, 1f), "예비탄 없음 처리"); // 보급 필요 상태 검사
        low.TryFire(7f); // 남은 한 발 소모
        Check(low.Rounds == 0 && low.Reserve == 0, "총 탄약 완전 소진"); // 바닥값 검사
        low.Refill(); // 훈련장 보급 실행
        Check(low.Rounds == 3 && low.Reserve == 1, "보급대에서 원래 최대치 복구"); // 보급 검사
        Check(gun.Rounds == 6 && gun.Reserve == 22, "서로 다른 총기 상태 독립"); // 무기별 탄약 공유 방지
    }

    private static void ValidateInput() // 저장한 입력 파일 검사
    {
        InputActionAsset copy = InputActionAsset.FromJson(File.ReadAllText(ProjectKDay10Setup.InputPath)); // 원본을 건드리지 않는 JSON 조회
        try // 임시 입력 정리 보장
        {
            InputActionMap map = copy.FindActionMap("Player", false); // 실제 플레이어 맵 검색
            Check(map != null, "Player 입력 맵"); // 맵 존재 검사
            Check(CountBinding(map, "Equip5", "<Keyboard>/5") == 1, "5번 총기 바인딩 한 개"); // 중복 키 검사
            Check(CountBinding(map, "Reload", "<Keyboard>/t") == 1, "T 재장전 바인딩 한 개"); // 중복 재장전 검사
            Check(CountBinding(map, "UseSupport", "<Keyboard>/r") >= 1, "R 마비침 유지"); // 기존 입력 보존 검사
            Check(CountBinding(map, "Interact", "<Keyboard>/f") >= 1, "F 암살과 상호작용 유지"); // 기존 핵심 조작 검사
            Check(CountBinding(map, "Attack", "<Mouse>/leftButton") >= 1 && CountBinding(map, "Defense", "<Mouse>/rightButton") >= 1, "기존 LMB와 RMB 재사용"); // 무기별 입력 분기 전제 검사
        }
        finally // 임시 입력 제거
        {
            UnityEngine.Object.DestroyImmediate(copy); // 에디터 입력 메모리 정리
        }
    }

    private static int CountBinding(InputActionMap map, string name, string path) // 특정 키 연결 개수
    {
        InputAction action = map.FindAction(name, false); // 이름으로 액션 조회
        int count = 0; // 연결 수 초기화
        if (action != null) // 액션 존재 확인
        {
            foreach (InputBinding binding in action.bindings) // 액션 단축키 순회
            {
                if (binding.path == path) // 대상 키 확인
                {
                    count++; // 일치 연결 수 누적
                }
            }
        }

        return count; // 연결 개수 반환
    }

    private static void Check(bool condition, string label) // 구체적 실패 원인 검증
    {
        if (!condition) // 검사 실패 확인
        {
            throw new InvalidOperationException(label); // 실패 항목 보고
        }

        assertions++; // 통과 항목 기록
    }
}
#endif // 에디터 전용 코드 제외
