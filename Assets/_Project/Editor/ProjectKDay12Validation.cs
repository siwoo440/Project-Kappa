#if UNITY_EDITOR // 대표 총기 검사 전용
using System; // 단언 실패 보고
using System.Collections.Generic; // 실제 장착 목록 검사
using UnityEditor; // 검사 메뉴와 에셋 조회
using UnityEditor.SceneManagement; // 검사 전용 씬 분리
using UnityEngine; // 실제 Unity 충돌 검사
using UnityEngine.InputSystem; // 저장된 입력 확인
using UnityEngine.SceneManagement; // 대상 씬 접근

public static class ProjectKDay12Validation // 설정·발사 규칙·부위 판정 검사
{
    // 설치 참조 검사 메뉴
    public static void ValidateSetup() // 파일과 씬 연결 확인
    {
        if (!CanTest()) // 안전한 에디터 검사 시점 확인
        {
            return; // 실행 중 씬 검사 변경 금지
        }

        Scene scene = SceneManager.GetSceneByPath(ProjectKDay12Setup.ScenePath); // 현재 훈련장 상태
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 읽기용 추가 로드 여부
        int checks = 0; // 통과한 단언 수
        try // 조회 씬 정리 보장
        {
            if (openedHere) // 아직 열리지 않은 훈련장 확인
            {
                scene = EditorSceneManager.OpenScene(ProjectKDay12Setup.ScenePath, OpenSceneMode.Additive); // 저장된 훈련장 읽기
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 실제 플레이어 조회
            Check(player != null, "Player 누락", ref checks); // 필수 객체 검사
            PlayerFirearmController controller = player.GetComponent<PlayerFirearmController>(); // 실제 총기 관리자
            Check(controller != null, "총기 관리자 누락", ref checks); // 선행 기반 검사
            List<FirearmDefinition> guns = ProjectKDay11Setup.GetLoadout(controller); // 저장된 장착 순서 조회
            Check(guns.Count >= 3, "대표 총기 3개 누락", ref checks); // 장착 목록 검사
            for (int i = 0; i < 3; i++) // 대표 무기별 참조 검사
            {
                FirearmDefinition expected = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay12Catalog.DefinitionPath(i)); // 생성된 대표 에셋 조회
                Check(expected != null && guns[i] == expected, "5~7번 총기 순서 불일치", ref checks); // 슬롯 연결 검사
                Check(expected.IsValid && expected.Handling != null, "총기 수치·모형·분산 설정 누락", ref checks); // 필수 자료 검사
                Check(expected.ModelPrefab.GetComponent<FirearmView>()?.Muzzle != null, "총구 참조 누락", ref checks); // 실제 발사 원점 검사
                Check(expected.FireMode == (i == 0 ? FirearmFireMode.SemiAutomatic : FirearmFireMode.Automatic), "발사 모드 불일치", ref checks); // 반자동과 자동 검사
                Check(expected.HeadDamage > expected.Stats.HealthDamage, "머리 피해 구분 누락", ref checks); // 부위 피해 검사
                Check(expected.EquipDuration > 0f && expected.AimDuration > 0f && expected.Stats.FireInterval > 0f, "시간 설정 오류", ref checks); // 장착과 조준과 사격 시간 검사
                Check(expected.MaximumRange >= expected.Stats.FalloffEndRange, "검사 거리보다 감쇠 종료가 큼", ref checks); // 원거리 검사 범위
            }

            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectKDay12Setup.InputPath); // 저장 입력 조회
            Check(input != null, "입력 파일 누락", ref checks); // 입력 에셋 검사
            for (int slot = 5; slot <= 7; slot++) // 대표 무기 단축키 확인
            {
                InputAction action = input.FindAction("Player/Equip" + slot, false); // 기존 플레이어 맵의 장착 입력
                Check(action != null && action.bindings.Count > 0, "Equip" + slot + " 바인딩 누락", ref checks); // 실제 선택 입력 검사
            }

            GameObject range = ProjectKDay9Setup.FindNamed(scene, "Day12_FirearmComparison"); // 비교 구역 조회
            Check(range != null, "12일차 비교장 누락", ref checks); // 구역 설치 검사
            Check(range.GetComponentsInChildren<FirearmDamageProbe>(true).Length == 2, "피해 표적 중복 또는 누락", ref checks); // 두 테스트 방어율 표적 검사
            Check(range.GetComponentsInChildren<FirearmPracticeTarget>(true).Length == 2, "40m·70m 표적 누락", ref checks); // 원거리 표적 검사
            Check(range.GetComponentsInChildren<FirearmCatalogStation>(true).Length == 1, "보급 단말기 중복 또는 누락", ref checks); // 상호작용 단일 구성
            Check(AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay10Setup.DefinitionPath) != null, "기존 검증용 권총 에셋 삭제", ref checks); // 기존 에셋 보존 확인
            Debug.Log("Day12 설정 참조 검사 통과: " + checks + "항목. 실제 화면은 Play에서 확인하세요."); // 검사 범위 구분
        }
        catch (Exception exception) // 검사 실패 보고
        {
            Debug.LogException(exception); // 실패 항목 표시
        }
        finally // 읽기용 씬 정리
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 자신이 열었던 씬만 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 사용자 편집 씬 유지
            }
        }
    }

    // 실제 발사 계산 회귀 검사 메뉴
    public static void TestRules() // 코드의 실제 사격 규칙 검증
    {
        if (!CanTest()) // 검사 가능 시점 확인
        {
            return; // 실행 중 데이터 간섭 방지
        }

        int checks = 0; // 통과 단언 수
        try // 실패를 완료로 표시하지 않는 검사
        {
            double[] shots = new double[4]; // 실제 프레임 보정과 같은 최대 예약
            FirearmTriggerState semi = new FirearmTriggerState(); // 반자동 입력 상태
            Check(semi.Collect(0, true, true, FirearmFireMode.SemiAutomatic, 1d / 6d, double.NegativeInfinity, shots) == 1, "첫 클릭 한 발", ref checks); // 단발 시작 검사
            Check(semi.Collect(1, false, true, FirearmFireMode.SemiAutomatic, 1d / 6d, 0.2, shots) == 0, "누르기 유지로 반자동 연사 금지", ref checks); // 반자동 유지 입력 검사
            Check(semi.Collect(0.1, true, true, FirearmFireMode.SemiAutomatic, 0.2, 0.2, shots) == 0, "쿨타임 중 클릭 예약 금지", ref checks); // 부당한 발사 예약 방지
            Check(semi.Collect(0.3, false, false, FirearmFireMode.SemiAutomatic, 0.2, 0.2, shots) == 0, "해제 뒤 밀린 클릭 발사 금지", ref checks); // 지연 클릭 제거
            int[] frameRates = new int[] // 비교할 프레임 수
            {
                15, // 검사 기준값
                30, // 검사 기준값
                60, // 검사 기준값
                144 // 검사 기준값
            };
            foreach (int fps in frameRates) // 프레임 속도별 연사 검사
            {
                Check(CountAutomatic(780f, fps) == 130, "골목비 10초 연사 수 " + fps, ref checks); // 목표 연사 간격 검사
                Check(CountAutomatic(600f, fps) == 100, "청룡선 10초 연사 수 " + fps, ref checks); // 소총 연사 간격 검사
            }

            FirearmTriggerState automatic = new FirearmTriggerState(); // 중단 상황 검사 상태
            Check(automatic.Collect(0, true, true, FirearmFireMode.Automatic, 0.1, 0, shots) == 1, "자동 사격 시작", ref checks); // 첫 발 검사
            Check(automatic.Collect(10, false, true, FirearmFireMode.Automatic, 0.1, 0.1, shots) == 4, "멈춤 뒤 예약 상한", ref checks); // 프레임 폭주 방지
            Check(automatic.Collect(10.001, false, true, FirearmFireMode.Automatic, 0.1, 0.4, shots) == 0, "멈춤 뒤 남은 예약 폐기", ref checks); // 장시간 밀린 탄약 발사 차단
            Check(automatic.Collect(10.2, false, false, FirearmFireMode.Automatic, 0.1, 0.4, shots) == 0, "버튼 해제 즉시 정지", ref checks); // 자동 연사 중단
            Check(automatic.Collect(20, true, true, FirearmFireMode.Automatic, 0.1, 0.4, shots) == 1, "재입력은 한 발로 재시작", ref checks); // 대기 시간 몰아쏘기 방지
            automatic.Reset(); // 무기 교체 모사
            Check(automatic.Collect(21, false, false, FirearmFireMode.Automatic, 0.1, 0.4, shots) == 0, "교체 후 예약 잔류 없음", ref checks); // 다른 무기로 예약 전파 방지
            FirearmRuntimeState ammo = new FirearmRuntimeState(10, 60, 0.1f); // 독립 탄약 상태
            Check(ammo.TryFireScheduled(0), "발사 시 탄약 감소", ref checks); // 최초 발사 검사
            Check(!ammo.TryFireScheduled(0.05), "발사 간격 유지", ref checks); // 너무 빠른 입력 차단
            Check(ammo.Rounds == 9 && ammo.Reserve == 60, "미발사 탄약 보존", ref checks); // 실패 차감 방지
            Check(ammo.TryBeginReload(0.2f, 1f), "재장전 시작", ref checks); // 기존 재장전 연결
            ammo.CancelReload(); // 장비 교체의 재장전 취소 모사
            Check(!ammo.TickReload(2f) && ammo.Rounds == 9 && ammo.Reserve == 60, "취소한 장전의 탄약 이동 금지", ref checks); // 원래 탄약 보존
            Check(ammo.TryBeginReload(3f, 1f) && ammo.TickReload(4f), "정상 재장전 완료", ref checks); // 재시도 처리
            Check(ammo.Rounds == 10 && ammo.Reserve == 59, "탄약 보충량 일치", ref checks); // 탄약 보존 법칙
            FirearmRuntimeState other = new FirearmRuntimeState(30, 120, 0.1f); // 다른 무기 상태
            Check(other.Rounds == 30 && ammo.Rounds == 10, "다른 총기 탄약 독립", ref checks); // 공유 탄창 오류 검사
            Check(Near(FirearmDamageMath.Resolve(24f, 1f, 0f, 0.05f), 24f), "유령손 몸통", ref checks); // 기본 피해 검사
            Check(Near(FirearmDamageMath.Resolve(43f, 1f, 0f, 0.05f), 43f), "유령손 머리", ref checks); // 기획 머리 피해 검사
            Check(Near(FirearmDamageMath.Resolve(27f, 1f, 0.5f, 0.12f), 15.12f), "테스트 장갑 관통", ref checks); // 남은 방어율 계산 검사
            Check(Near(FirearmDamageMath.Resolve(27f, 0.55f, 0f, 0.12f), 14.85f), "거리 감쇠 비율", ref checks); // 문서 끝거리 피해 검사
            Check(Near(FirearmDamageMath.Resolve(10f, 1f, 1f, 1f), 10f), "전체 관통 한도", ref checks); // 백 퍼센트 관통 검사
            Check(FirearmDamageMath.Resolve(float.NaN, 1f, 0f, 0f) == 0f, "잘못된 피해 무효화", ref checks); // 수치 전파 방지
            Debug.Log("Day12 실제 계산 검사 통과: " + checks + "항목. 입력·물리·화면은 별도 검사 대상입니다."); // 실제 실행한 범위 명시
        }
        catch (Exception exception) // 단언 실패 전달
        {
            Debug.LogException(exception); // 실패 규칙 표시
        }
    }

    private static int CountAutomatic(float rpm, int fps) // 실제 자동 사격과 탄약 함수 통합 검사
    {
        FirearmTriggerState trigger = new FirearmTriggerState(); // 새 연사 상태
        FirearmRuntimeState ammo = new FirearmRuntimeState(1000, 0, 60f / rpm); // 검사 시간에 충분한 탄창
        double[] shots = new double[4]; // 프레임 발사 예약
        int total = 0; // 실제 발사 수
        for (int frame = 0; frame < fps * 10; frame++) // 열 초 구간의 각 프레임
        {
            double now = frame / (double)fps; // 프레임 시작 시각
            int count = trigger.Collect(now, frame == 0, true, FirearmFireMode.Automatic, 60f / rpm, ammo.NextShotTime, shots); // 프로덕션 예약 함수 사용
            for (int i = 0; i < count; i++) // 실제 예정 발사 처리
            {
                if (ammo.TryFireScheduled(shots[i])) // 프로덕션 탄약 함수 사용
                {
                    total++; // 탄약 소모 성공만 집계
                }
            }
        }

        return total; // 실제 발사 수 반환
    }

    // 실제 부위 충돌 회귀 검사 메뉴
    public static void TestPhysics() // 별도 임시 씬에서 발사 경로 검사
    {
        if (!CanTest()) // 편집 모드 검사 시점 확인
        {
            return; // 실행 중 월드 변경 방지
        }

        Scene previous = SceneManager.GetActiveScene(); // 원래 편집 씬 보존
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 저장하지 않을 검사 전용 씬
        SceneManager.SetActiveScene(testScene); // 모든 검사 객체를 임시 씬에만 생성
        int checks = 0; // 물리 검사 개수
        try // 검사 실패 시에도 생성 씬 정리
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f); // 기존 훈련장과 떨어진 검사 위치
            GameObject owner = new GameObject("D12_TestPlayer"); // 임시 플레이어 기준
            owner.transform.position = origin; // 검사 원점 배치
            CharacterController body = owner.AddComponent<CharacterController>(); // 실제 플레이어 충돌 중심 사용
            body.center = Vector3.up; // 몸통 중심 높이
            body.height = 2f; // 플레이어 높이
            body.radius = 0.3f; // 플레이어 반경
            GameObject cameraObject = new GameObject("D12_TestCamera"); // 명시적으로 전달할 조준 카메라
            Camera camera = cameraObject.AddComponent<Camera>(); // 메인 태그 없는 카메라
            camera.enabled = false; // 검사 중 렌더링 금지
            camera.transform.position = origin + new Vector3(0f, 1.8f, -1f); // 머리 높이 조준
            GameObject enemy = new GameObject("D12_TestEnemy"); // 부위 검사 대상
            enemy.transform.position = origin + Vector3.forward * 5f; // 앞쪽 적 배치
            EnemyActor actor = enemy.AddComponent<EnemyActor>(); // 기존 적 타입 사용
            CapsuleCollider physical = enemy.AddComponent<CapsuleCollider>(); // 머리 앞을 가리던 이동 충돌체
            physical.center = Vector3.up; // 이동용 중심
            physical.height = 2.2f; // 머리까지 덮는 캡슐
            physical.radius = 0.4f; // 이동용 폭
            BoxCollider torso = ProjectKDay12HitboxSetup.Zone(enemy.transform, "Body", Vector3.up * 0.75f, new Vector3(0.7f, 1.5f, 0.7f), FirearmHitRegion.Body, actor, null); // 아래쪽 몸통 부위
            BoxCollider head = ProjectKDay12HitboxSetup.Zone(enemy.transform, "Head", Vector3.up * 1.8f, Vector3.one * 0.45f, FirearmHitRegion.Head, actor, null); // 위쪽 머리 부위
            enemy.AddComponent<EnemyFirearmHitboxes>().Configure(torso, head); // 이동 캡슐 대체 활성화
            Vector3 muzzle = origin + new Vector3(0f, 1.8f, 0.6f); // 머리 높이의 총구
            Physics.SyncTransforms(); // 생성한 충돌체의 실제 위치 반영
            bool found = FirearmTargeting.CastShot(camera, owner.transform, muzzle, 10f, ~0, out RaycastHit hit, out Vector3 endpoint, out bool blocked); // 프로덕션 총구 판정 실행
            Check(found && !blocked && hit.collider == head, "이동 캡슐 대신 머리 명중", ref checks); // 핵심 부위 회귀 검사
            camera.transform.position = origin + new Vector3(0f, 1f, -1f); // 몸통 높이로 조준 변경
            muzzle.y = origin.y + 1f; // 몸통 높이 총구 설정
            Physics.SyncTransforms(); // 조준 위치 반영
            found = FirearmTargeting.CastShot(camera, owner.transform, muzzle, 10f, ~0, out hit, out endpoint, out blocked); // 실제 몸통 판정 실행
            Check(found && !blocked && hit.collider == torso, "몸통 명중", ref checks); // 부위 분리 검사
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube); // 실제 엄폐물 추가
            wall.transform.position = origin + new Vector3(0f, 1f, 2.5f); // 총구와 적 사이 배치
            wall.transform.localScale = new Vector3(2f, 3f, 0.2f); // 통과할 수 없는 벽 크기
            Physics.SyncTransforms(); // 벽 충돌 위치 반영
            found = FirearmTargeting.CastShot(camera, owner.transform, muzzle, 10f, ~0, out hit, out endpoint, out blocked); // 엄폐물 포함 판정
            Check(found && hit.collider == wall.GetComponent<Collider>(), "부위 레이가 벽을 통과하지 않음", ref checks); // 벽 관통 방지
            wall.GetComponent<Collider>().isTrigger = true; // 일반 상호작용 영역 모사
            Physics.SyncTransforms(); // 트리거 변경 반영
            found = FirearmTargeting.CastShot(camera, owner.transform, muzzle, 10f, ~0, out hit, out endpoint, out blocked); // 일반 트리거 제외 검사
            Check(found && !blocked && hit.collider == torso, "일반 트리거는 탄도를 막지 않음", ref checks); // 새 피격 트리거만 허용
            wall.GetComponent<Collider>().isTrigger = false; // 실제 벽 복구
            wall.transform.position = muzzle; // 총구 내부 벽 상황
            wall.transform.localScale = Vector3.one * 0.5f; // 원점을 감싸는 벽 크기
            Physics.SyncTransforms(); // 내부 충돌 위치 반영
            FirearmTargeting.CastShot(camera, owner.transform, muzzle, 10f, ~0, out hit, out endpoint, out blocked); // 총구 내부 검사
            Check(blocked, "총구 내부 벽 피해 차단", ref checks); // 기존 근접 벽 보호 검사
            Debug.Log("Day12 물리 경로 검사 통과: " + checks + "항목. 실제 모델 부위 크기는 Play에서 추가 확인하세요."); // 검사 결과 구분
        }
        catch (Exception exception) // 실제 물리 검사 실패 전달
        {
            Debug.LogException(exception); // 실패 판정 표시
        }
        finally // 검사 객체 전부 정리
        {
            EditorSceneManager.CloseScene(testScene, true); // 저장하지 않고 임시 객체 삭제
            if (previous.IsValid() && previous.isLoaded) // 원래 편집 씬 확인
            {
                SceneManager.SetActiveScene(previous); // 편집 기준 씬 복구
            }
        }
    }

    private static bool CanTest() // 실행 중 검사 제한
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 검사 시점 확인
        {
            Debug.LogWarning("Day12 검사는 Play 중지와 컴파일 완료 후 실행하세요."); // 검사 조건 안내
            return false; // 검사 실행 차단
        }

        return true; // 검사 허용
    }

    private static bool Near(float actual, float expected) // 수치 오차 범위 검사
    {
        return Mathf.Abs(actual - expected) < 0.0001f; // 작은 부동소수점 오차 허용
    }

    private static void Check(bool condition, string message, ref int checks) // 명시적인 검사 단언
    {
        if (!condition) // 기대 조건 미충족 확인
        {
            throw new InvalidOperationException("Day12 검사 실패: " + message); // 첫 실패를 즉시 보고
        }

        checks++; // 통과 항목 집계
    }
}
#endif // 게임 빌드에서 검증 메뉴 제외
