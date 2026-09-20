#if UNITY_EDITOR // 에디터 전용 검사
using System.Collections.Generic; // 검사 오류 목록
using UnityEditor; // 에셋 조회 기능
using UnityEditor.SceneManagement; // 테스트 씬 조회
using UnityEngine; // 검사 대상 컴포넌트
using UnityEngine.InputSystem; // 입력 바인딩 검사
using UnityEngine.SceneManagement; // 씬 조회 기능

public static class ProjectKDay9Validation // 장비 설정과 계산 검사
{
    // 수동 검사 메뉴
    public static void ValidateSetup() // 데이터와 씬 연결 검사
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) // 실행 상태 확인
        {
            Debug.LogWarning("Day9 검사: Play를 중지하고 컴파일 완료 후 실행하세요."); // 검사 실행 안내
            return; // 실행 중 씬 변경 방지
        }

        List<string> errors = new List<string>(); // 검사 오류 목록 생성
        Scene scene = SceneManager.GetSceneByPath(ProjectKDay9Setup.ScenePath); // 현재 테스트 씬 조회
        bool temporary = !scene.IsValid() || !scene.isLoaded; // 임시 로드 여부
        try // 임시 씬 수명 보호
        {
            if (temporary) // 다른 작업 씬 확인
            {
                scene = EditorSceneManager.OpenScene(ProjectKDay9Setup.ScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
            }

            CheckRules(errors); // 장비 계산 경계값 검사
            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어 검색
            Check(player != null, "Player 오브젝트 누락", errors); // 플레이어 존재 검사
            if (player != null) // 플레이어 연결 확인
            {
                CheckPlayer(player, errors); // 장비 컴포넌트와 데이터 검사
            }

            Check(ProjectKDay9Setup.FindNamed(scene, "Day9_EquipmentStation") != null, "Day9 보급대 누락", errors); // 보급대 구성 검사
            Check(Shader.Find("ProjectK/Day9/SoftEffect") != null, "Day9 효과 셰이더 누락", errors); // 효과 셰이더 검사
            CheckInputs(errors); // 새 조작과 기존 조작 검사
            foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
            {
                foreach (EnemyActor actor in root.GetComponentsInChildren<EnemyActor>(true)) // 기존 적 순회
                {
                    Check(actor.GetComponent<EnemyStatusController>() != null, actor.name + " 마비 상태 누락", errors); // 적 상태 연결 검사
                    EnemyMeleeCombat melee = actor.GetComponent<EnemyMeleeCombat>(); // 적 근접 공격 조회
                    if (melee != null) // 근접 공격 가능 적 확인
                    {
                        CheckReference(new SerializedObject(melee), "weaponSocket", actor.name + " 검 소켓 저장 누락", errors); // 재실행 무기 연결 검사
                    }

                    PatrolGuardAI patrol = actor.GetComponent<PatrolGuardAI>(); // 순찰 AI 조회
                    if (patrol != null) // 경비 순찰 가능 여부
                    {
                        SerializedObject data = new SerializedObject(patrol); // 저장된 순찰 정보 조회
                        CheckReference(data, "target", actor.name + " 추격 대상 저장 누락", errors); // 추격 대상 검사
                        SerializedProperty points = data.FindProperty("patrolPoints"); // 순찰 지점 목록 조회
                        Check(points != null && points.arraySize > 0, actor.name + " 순찰 지점 누락", errors); // 순찰 경로 검사
                    }
                }
            }
        }
        catch (System.Exception exception) // 검사 도중 오류 확인
        {
            errors.Add(exception.Message); // 실제 오류 기록
        }
        finally // 임시 로드 씬 정리
        {
            if (temporary && scene.IsValid() && scene.isLoaded) // 검사 전 열리지 않은 씬 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 다른 작업 씬 유지
            }
        }

        if (errors.Count == 0) // 전체 설정 검사 결과
        {
            Debug.Log("Day9 설정 검사 통과: 무기 데이터·입력·장비 참조·회복과 연막 계산. 실제 조작은 Play에서 확인하세요."); // 검사 범위를 구분한 결과 안내
            return; // 검사 종료
        }

        foreach (string error in errors) // 발견한 오류 순회
        {
            Debug.LogError("Day9 검사: " + error); // 수정할 설정 위치 안내
        }
    }

    private static void CheckPlayer(GameObject player, List<string> errors) // 플레이어 장비 검사
    {
        PlayerEquipmentManager manager = player.GetComponent<PlayerEquipmentManager>(); // 장비 관리자 조회
        Check(manager != null, "PlayerEquipmentManager 누락", errors); // 장비 관리자 확인
        Check(player.GetComponent<SupportEquipmentController>() != null, "마비침 컴포넌트 누락", errors); // 보조장비 확인
        Check(player.GetComponent<ConsumableController>() != null, "소모품 컴포넌트 누락", errors); // 소모품 확인
        Check(player.GetComponent<PlayerEquipmentHUD>() != null, "장비 HUD 누락", errors); // HUD 확인
        Check(player.GetComponent<PlayerHealth>() != null, "PlayerHealth 누락", errors); // 회복 대상 확인
        Check(player.GetComponents<PlayerEquipmentManager>().Length == 1, "장비 관리자 중복", errors); // 중복 관리자 검사
        if (manager == null) // 장비 관리자 존재 확인
        {
            return; // 하위 참조 검사 생략
        }

        SerializedObject data = new SerializedObject(manager); // 저장된 장비 설정 조회
        CheckReference(data, "weaponSocket", "Player 근접 무기 소켓 누락", errors); // 소켓 참조 확인
        SerializedProperty weapons = data.FindProperty("weapons"); // 무기 목록 조회
        Check(weapons != null && weapons.arraySize == 4, "테스트 근접 무기 4종 연결 필요", errors); // 무기 수 확인
        if (weapons != null) // 무기 목록 확인
        {
            for (int i = 0; i < weapons.arraySize; i++) // 각 무기 검사
            {
                MeleeWeaponDefinition weapon = weapons.GetArrayElementAtIndex(i).objectReferenceValue as MeleeWeaponDefinition; // 무기 정의 조회
                Check(weapon != null && weapon.Stats != null && weapon.ModelPrefab != null, "무기 " + (i + 1) + " 데이터 또는 프리팹 누락", errors); // 기본 참조 확인
                if (weapon != null && weapon.Stats != null) // 검사 가능한 무기 확인
                {
                    Check(weapon.HitTime > 0f && weapon.HitTime <= weapon.AnimationDuration, weapon.name + " 타격 시점 범위 오류", errors); // 타격 시점 검사
                    Check(weapon.Stats.FireInterval >= weapon.AnimationDuration, weapon.name + " 공격 간격 범위 오류", errors); // 공격 간격 검사
                    Check(weapon.Stats.HealthDamage >= 0f && weapon.Stats.PostureDamage >= 0f, weapon.name + " 피해량 범위 오류", errors); // 음수 피해 검사
                }
            }
        }

        EquipmentTuning settings = manager.Tuning; // 장비 설정 조회
        Check(settings != null, "EquipmentTuning 연결 누락", errors); // 설정 존재 검사
        if (settings != null) // 설정 참조 확인
        {
            Check(settings.dartModel != null && settings.injectorModel != null && settings.lureModel != null && settings.smokeModel != null, "장비 모형 프리팹 누락", errors); // 장비 모형 확인
            Check(settings.effectMaterial != null && settings.smokeMaterial != null, "장비 효과 재질 누락", errors); // 재질 참조 확인
            Check(settings.maxDarts > 0 && settings.maxConsumables > 0, "초기 장비 수량 확인 필요", errors); // 소지 수량 확인
            Check(settings.stunDuration > 0f && settings.smokeDuration > 0f && settings.lureDuration > 0f, "효과 지속 시간 확인 필요", errors); // 지속 시간 확인
        }
    }

    private static void CheckInputs(List<string> errors) // 장비 조작 검사
    {
        InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"); // 저장된 입력 에셋 조회
        InputActionMap map = input != null ? input.FindActionMap("Player", false) : null; // 플레이어 입력 맵 조회
        Check(map != null, "Player 입력 맵 누락", errors); // 입력 맵 확인
        if (map == null) // 입력 맵 참조 확인
        {
            return; // 하위 입력 검사 중단
        }

        for (int i = 1; i <= 4; i++) // 직접 선택 입력 검사
        {
            CheckBinding(map, "Equip" + i, "<Keyboard>/" + i, errors); // 숫자 선택 키 확인
        }

        CheckBinding(map, "UseSupport", "<Keyboard>/r", errors); // 마비침 키 확인
        CheckBinding(map, "CycleItem", "<Keyboard>/v", errors); // 소모품 선택 키 확인
        CheckBinding(map, "UseItem", "<Keyboard>/g", errors); // 소모품 사용 키 확인
        CheckBinding(map, "Interact", "<Keyboard>/f", errors); // 기존 상호작용 키 보존 확인
        CheckBinding(map, "Attack", "<Mouse>/leftButton", errors); // 기존 검 공격 키 확인
        CheckBinding(map, "Defense", "<Mouse>/rightButton", errors); // 기존 방어 키 확인
    }

    private static void CheckBinding(InputActionMap map, string actionName, string path, List<string> errors) // 특정 바인딩 검사
    {
        InputAction action = map.FindAction(actionName, false); // 검사할 액션 조회
        bool found = false; // 바인딩 검색 상태
        if (action != null) // 액션 존재 확인
        {
            foreach (InputBinding binding in action.bindings) // 바인딩 목록 검사
            {
                found |= string.Equals(binding.path, path, System.StringComparison.OrdinalIgnoreCase); // 대상 키 검색
            }
        }

        Check(found, actionName + " : " + path + " 연결 누락", errors); // 누락 조작 기록
    }

    private static void CheckRules(List<string> errors) // 실제 C# 계산 경계값 검사
    {
        Check(EquipmentRules.WrapIndex(-1, 4) == 3, "이전 무기 순환 오류", errors); // 음수 번호 검사
        Check(EquipmentRules.WrapIndex(4, 4) == 0, "다음 무기 순환 오류", errors); // 끝 번호 검사
        Check(EquipmentRules.WrapIndex(1, 0) == -1, "빈 무기 목록 처리 오류", errors); // 빈 목록 검사
        Check(Mathf.Approximately(EquipmentRules.ClampedHeal(80f, 100f, 40f, false), 20f), "최대 체력 초과 회복 오류", errors); // 회복 상한 검사
        Check(EquipmentRules.ClampedHeal(100f, 100f, 40f, false) == 0f, "최대 체력에서 회복 소모 오류", errors); // 불필요한 회복 검사
        Check(EquipmentRules.ClampedHeal(0f, 100f, 40f, true) == 0f, "사망 후 회복 오류", errors); // 부활 방지 검사
        Check(EquipmentRules.SegmentIntersectsSphere(Vector3.left * 5f, Vector3.right * 5f, Vector3.zero, 1f), "연막 관통 차단 오류", errors); // 연막 관통 검사
        Check(EquipmentRules.SegmentIntersectsSphere(Vector3.zero, Vector3.forward * 5f, Vector3.zero, 1f), "연막 내부 시야 차단 오류", errors); // 내부 출발 검사
        Check(!EquipmentRules.SegmentIntersectsSphere(Vector3.up * 2f, Vector3.up * 2f + Vector3.forward, Vector3.zero, 1f), "연막 밖 시야 차단 오류", errors); // 연막 바깥 검사
        Check(EquipmentRules.SegmentIntersectsSphere(Vector3.zero, Vector3.zero, Vector3.zero, 1f), "같은 위치 시야 검사 오류", errors); // 길이 없는 구간 검사
    }

    private static void CheckReference(SerializedObject data, string property, string message, List<string> errors) // 저장 참조 검사
    {
        SerializedProperty field = data.FindProperty(property); // 참조 필드 조회
        Check(field != null && field.objectReferenceValue != null, message, errors); // 누락 참조 기록
    }

    private static void Check(bool success, string message, List<string> errors) // 검사 실패 기록
    {
        if (!success) // 실패 여부 확인
        {
            errors.Add(message); // 실패 설명 추가
        }
    }
}
#endif // 에디터 검사 종료
