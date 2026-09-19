#if UNITY_EDITOR // 편집기에서 실행하는 검증 기능
using System; // 검증 실패 전달
using System.Collections.Generic; // 장비 목록 조회
using System.Reflection; // 실제 청각 처리의 분리 검사
using UnityEditor; // 데이터와 검사 메뉴
using UnityEditor.SceneManagement; // 임시 조회 씬 관리
using UnityEngine; // Unity 실제 수학 함수 검사
using UnityEngine.InputSystem; // 입력 연결 검사
using UnityEngine.SceneManagement; // 기존 테스트 씬 조회

public static class ProjectKDay11Validation // 정적 연결과 실제 계산 검사 메뉴
{
    [MenuItem("Project K/Day 11/Validate Recoil And Hearing Setup")] // 설정 연결 검사 메뉴
    public static void ValidateSetup() // 에셋과 씬을 변경하지 않는 연결 확인
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) // 검사 가능한 임포트 상태 확인
        {
            Debug.LogWarning("Day11: 컴파일과 임포트 완료 후 검사하세요."); // 대기 안내
            return; // 참조 생성 중 검사 방지
        }

        Scene scene = SceneManager.GetSceneByPath(ProjectKDay11Setup.ScenePath); // 대상 씬 확인
        bool openedHere = !scene.IsValid() || !scene.isLoaded; // 임시 조회 필요 여부
        if (openedHere && EditorApplication.isPlaying) // 실행 중 다른 씬 자동 로드 방지
        {
            Debug.LogWarning("Day11: Test 씬에서 검사하거나 Play를 중지하세요."); // 대상 씬 안내
            return; // 실행 중 환경 변경 방지
        }

        int checks = 0; // 검사 항목 수
        try // 오류가 있어도 조회 씬 정리
        {
            if (openedHere) // 아직 열리지 않은 씬 확인
            {
                scene = EditorSceneManager.OpenScene(ProjectKDay11Setup.ScenePath, OpenSceneMode.Additive); // 읽기용 씬 추가
            }

            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 실제 플레이어 조회
            Check(player != null, "Player 누락", ref checks); // 필수 플레이어 검사
            PlayerFirearmController firearm = player.GetComponent<PlayerFirearmController>(); // 실제 총기 참조
            Check(firearm != null, "PlayerFirearmController 누락", ref checks); // 총기 기반 검사
            Check(ProjectKDay11Setup.FindCamera(scene) != null, "ThirdPersonCamera 누락", ref checks); // 반동 카메라 검사
            List<FirearmDefinition> definitions = ProjectKDay11Setup.GetLoadout(firearm); // 실제 장비 목록 조회
            Check(definitions.Count > 0, "총기 목록 비어 있음", ref checks); // 장착 대상 검사
            foreach (FirearmDefinition definition in definitions) // 모든 연결 총기 검사
            {
                Check(definition.IsValid, "총기 기본 수치 또는 모형 누락", ref checks); // 원래 무기 정의 검사
                Check(definition.Handling != null, "FirearmHandlingProfile 누락", ref checks); // 새로운 조정 자료 검사
                FirearmView view = definition.ModelPrefab.GetComponent<FirearmView>(); // 모형의 실제 표시 참조
                Check(view != null && view.Muzzle != null && (!definition.Handling.SupportsSuppressor || view.HasSuppressor), "소음기 또는 총구 연결 누락", ref checks); // 두 총구 연결 검사
                Check(definition.Handling.AimSpread <= definition.Handling.HipSpread, "조준 분산이 비조준보다 큼", ref checks); // 기본 정확도 비교
            }

            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectKDay11Setup.InputPath); // 입력은 읽기만 사용
            Check(input != null, "입력 에셋 누락", ref checks); // 입력 파일 검사
            InputActionMap map = input.FindActionMap("Player", false); // 기존 맵 조회
            Check(map != null, "Player 입력 맵 누락", ref checks); // 필수 맵 검사
            Check(map.FindAction("ToggleSuppressor", false) != null, "소음기 전환 입력 누락", ref checks); // B 기능 연결 검사
            Check(map.FindAction("Reload", false) != null && map.FindAction("UseSupport", false) != null, "기존 T 재장전 또는 R 마비침 누락", ref checks); // 기존 조작 보존 검사
            GameObject range = ProjectKDay9Setup.FindNamed(scene, "Day11_ShootingRange"); // 옆 사격장 조회
            Check(range != null, "11일차 사격장 누락", ref checks); // 배치 검사
            Check(range.GetComponentsInChildren<FirearmPracticeTarget>(true).Length == 3, "거리별 표적은 3개여야 함", ref checks); // 중복 생성 검사
            Check(range.GetComponentsInChildren<FirearmHearingProbe>(true).Length == 2, "청각 검사 장치는 2개여야 함", ref checks); // 청각 표시 검사
            Check(range.GetComponentsInChildren<FirearmPracticeStation>(true).Length == 1, "보급 단말기 중복 또는 누락", ref checks); // 상호작용 대상 검사
            Debug.Log("Day11 설정 참조 검사 통과: " + checks + "항목. 실제 사격 화면과 이동 중 동작은 Play에서 확인하세요."); // 검사 범위와 결과 구분
        }
        catch (Exception exception) // 실패 원인 전달
        {
            Debug.LogException(exception); // 실패 항목 출력
        }
        finally // 읽기용 씬 정리
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) // 검사 때문에 연 씬만 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 기존 작업 씬 유지
            }
        }
    }

    [MenuItem("Project K/Day 11/Test Handling Rules")] // Unity 계산 규칙 검사 메뉴
    public static void TestRules() // 실제 프로덕션 함수를 사용하는 회귀 검사
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) // 테스트용 객체가 플레이에 간섭하지 않도록 제한
        {
            Debug.LogWarning("Day11 규칙 검사는 Play를 중지한 뒤 실행하세요."); // 검사 시점 안내
            return; // 플레이 데이터 변경 방지
        }

        int checks = 0; // 실행한 단언 개수
        FirearmHandlingProfile profile = ScriptableObject.CreateInstance<FirearmHandlingProfile>(); // 기본값 검사 자료
        GameObject probe = null; // 실제 청각 처리 검사 객체
        GameObject cameraObject = null; // 실제 카메라 반동 검사 객체
        try // 실패 시에도 임시 자료 해제
        {
            float hip = FirearmHandlingMath.SpreadAngle(profile, false, true, false, 0f, 0f); // 정지 비조준 계산
            float aim = FirearmHandlingMath.SpreadAngle(profile, true, true, false, 0f, 0f); // 정지 조준 계산
            Check(aim < hip, "조준 정확도 구분", ref checks); // 조준 이득 검사
            Check(FirearmHandlingMath.SpreadAngle(profile, false, true, false, 5f, 0f) > hip, "이동 분산 증가", ref checks); // 이동 보정 검사
            Check(FirearmHandlingMath.SpreadAngle(profile, false, true, true, 0f, 0f) < hip, "앉기 정확도 증가", ref checks); // 지상 앉기 검사
            float air = FirearmHandlingMath.SpreadAngle(profile, false, false, false, 0f, 0f); // 공중 비조준 계산
            Check(air > hip, "공중 분산 증가", ref checks); // 공중 보정 검사
            Check(Near(air, FirearmHandlingMath.SpreadAngle(profile, false, false, true, 0f, 0f)), "공중 앉기 보너스 금지", ref checks); // 공중 악용 방지
            Check(FirearmHandlingMath.SpreadAngle(profile, true, true, false, 0f, 2f) > aim, "연속 발사 분산 증가", ref checks); // 누적 효과 검사
            Check(FirearmHandlingMath.SpreadAngle(null, false, false, false, 5f, 1f) == 0f, "이전 정의 호환", ref checks); // 이전 에셋 동작 검사
            Check(FirearmHandlingMath.ReticlePixels(0f, 60f, 1080f) == 0f, "분산 0 투영", ref checks); // 영점 계산 검사
            float pixels = FirearmHandlingMath.ReticlePixels(2f, 60f, 1080f); // 실제 화면 분산 반경
            Vector2 edge = FirearmHandlingMath.ViewportSample(Vector2.right, 2f, 60f, 1920f, 1080f); // 오른쪽 끝 탄도 표본
            Check(Near(edge.x * 1920f, pixels), "HUD와 가로 탄도 일치", ref checks); // 수평 투영 일치 검사
            Vector2 upper = FirearmHandlingMath.ViewportSample(Vector2.up, 2f, 60f, 1920f, 1080f); // 위쪽 끝 탄도 표본
            Check(Near(upper.y * 1080f, pixels), "HUD와 세로 탄도 일치", ref checks); // 수직 투영 일치 검사
            Check(FirearmHandlingMath.ViewportSample(Vector2.zero, 2f, 60f, 1920f, 1080f) == Vector2.zero, "정중앙 표본", ref checks); // 중앙 탄도 검사
            Vector2 clamped = FirearmHandlingMath.ViewportSample(new Vector2(5f, 0f), 2f, 60f, 1920f, 1080f); // 잘못된 표본 입력
            Check(Near(clamped.x, edge.x), "분산 범위 외 표본 제한", ref checks); // 원형 범위 제한 검사
            Check(Near(FirearmHandlingMath.Recover(-1f, 1f, 2f), 0f), "음수 좌우 반동 회복", ref checks); // 부호 반동 회복 검사
            Check(Near(FirearmHandlingMath.Recover(1f, -1f, 2f), 1f), "음수 시간 회복 금지", ref checks); // 시간 역행 보호

            FirearmHandlingState handling = new FirearmHandlingState(0f); // 새 총기 정확도 상태
            handling.RegisterShot(0f, profile); // 실제 발사 등록
            float initial = handling.Bloom; // 발사 직후 분산 기록
            handling.Tick(0.1f, profile); // 회복 지연 내부 시각
            Check(Near(initial, handling.Bloom), "분산 회복 지연", ref checks); // 즉시 회복 방지
            handling.Tick(0.4f, profile); // 회복 가능한 시각
            Check(handling.Bloom < initial, "분산 시간 기반 회복", ref checks); // 시간에 따른 복귀
            for (int i = 0; i < 50; i++) // 상한보다 많은 발사 누적
            {
                handling.RegisterShot(1f, profile); // 같은 시각 누적 검사
            }

            Check(Near(handling.Bloom, profile.MaximumBloom), "분산 누적 상한", ref checks); // 무한 분산 방지
            FirearmHandlingState repeated = new FirearmHandlingState(0f); // 현재 권총 간격 누적 검사
            repeated.RegisterShot(0f, profile); // 첫 번째 실제 발사
            float firstBloom = repeated.Bloom; // 첫 사격 이후 추가 분산
            repeated.RegisterShot(0.65f, profile); // 기존 권총 간격의 두 번째 발사
            Check(repeated.Bloom > firstBloom, "현재 권총 연속 사격 분산 누적", ref checks); // 회복이 지나치게 빨라지는 설정 방지
            handling.Suppressed = true; // 무기별 소음기 설정
            handling.Tick(100f, profile); // 오랜 비장착 시간 적용
            Check(Near(handling.Bloom, 0f) && handling.Suppressed, "교체 중 분산 회복과 소음기 보존", ref checks); // 상태 보존 검사
            Check(!new FirearmHandlingState(0f).Suppressed, "다른 총기 소음기 독립", ref checks); // 무기별 분리 검사

            Check(FirearmHandlingMath.CanHear(8f, 10f, 25f), "일반 총성 범위", ref checks); // 일반 총성 청취 검사
            Check(!FirearmHandlingMath.CanHear(8f, 10f, 5f), "먼 소음기 총성 제외", ref checks); // 소음기 비교 검사
            Check(FirearmHandlingMath.CanHear(3f, 10f, 5f), "가까운 소음기 총성 허용", ref checks); // 완전 무음 오인 방지
            Check(!FirearmHandlingMath.CanHear(15f, 10f, 25f), "경비 청각 한도", ref checks); // 청각 한도 검사
            Check(!FirearmHandlingMath.CanHear(0f, 10f, 0f), "반경 0 소음 제외", ref checks); // 비활성 소음 검사

            FirearmRuntimeState ammo = new FirearmRuntimeState(6, 4, 0.65f); // 기존 탄약 코드 직접 검사
            Check(ammo.TryFire(0f) && ammo.Rounds == 5, "한 발 탄수 소모", ref checks); // 기본 사격 회귀 검사
            Check(!ammo.TryFire(0.1f) && ammo.Rounds == 5, "발사 간격 중 탄수 유지", ref checks); // 빠른 입력 보호
            Check(ammo.TryBeginReload(1f, 1.4f), "기존 재장전 시작", ref checks); // 재장전 호환 검사
            Check(!ammo.TickReload(2f) && ammo.Rounds == 5 && ammo.Reserve == 4, "완료 전 탄약 이동 금지", ref checks); // 원자적 재장전 검사
            ammo.CancelReload(); // 교체 취소 재현
            Check(!ammo.TickReload(4f) && ammo.Rounds == 5 && ammo.Reserve == 4, "취소 후 탄약 유지", ref checks); // 취소 복제 방지
            Check(ammo.TryBeginReload(5f, 1f) && ammo.TickReload(6f) && ammo.Rounds == 6 && ammo.Reserve == 3, "완료 시 정확한 탄약 이동", ref checks); // 기존 탄수 보존 검사

            probe = EditorUtility.CreateGameObjectWithHideFlags("Day11_TestProbe", HideFlags.HideAndDontSave, typeof(DetectionSensor)); // 저장되지 않는 실제 센서
            probe.transform.position = new Vector3(5000f, 0f, 5000f); // 실제 훈련장과 떨어진 검사 위치
            DetectionSensor sensor = probe.GetComponent<DetectionSensor>(); // 실제 프로덕션 센서 참조
            MethodInfo receive = typeof(DetectionSensor).GetMethod("HandleNoise", BindingFlags.Instance | BindingFlags.NonPublic); // 이벤트 처리 함수 직접 조회
            Check(receive != null, "청각 이벤트 함수 존재", ref checks); // 잘못된 검사 대상을 구분
            Vector3 origin = probe.transform.position + Vector3.right * 4f; // 센서 가까운 총성 위치
            DeliverNoise(receive, sensor, new NoiseEvent(origin, 5f, NoiseType.Gunshot, null)); // 실제 소음기 이벤트 처리
            Check(sensor.State == DetectionState.Suspicious && sensor.GunshotsHeard == 1, "청각만으로 발견 상태 금지", ref checks); // 상태 전이 검사
            Check(sensor.LastKnownPosition == origin, "조사 위치는 발사 시점 위치", ref checks); // 위치 추적 오인 방지
            Vector3 distant = probe.transform.position + Vector3.right * 8f; // 소음기 범위 밖 총성
            DeliverNoise(receive, sensor, new NoiseEvent(distant, 5f, NoiseType.Gunshot, null)); // 청취 불가 소음 전달
            Check(sensor.GunshotsHeard == 1 && sensor.LastKnownPosition == origin, "못 들은 소음의 위치 덮어쓰기 방지", ref checks); // 상태 보존 검사
            DeliverNoise(receive, sensor, new NoiseEvent(distant, 25f, NoiseType.Gunshot, null)); // 같은 거리의 일반 총성
            Check(sensor.GunshotsHeard == 2 && sensor.LastKnownPosition == distant, "일반 총성의 조사 위치 갱신", ref checks); // 실제 차등 반응 검사

            cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Day11_TestCamera", HideFlags.HideAndDontSave, typeof(Camera), typeof(ThirdPersonCamera)); // 저장되지 않는 카메라 검사 객체
            ThirdPersonCamera camera = cameraObject.GetComponent<ThirdPersonCamera>(); // 실제 카메라 코드 참조
            camera.AddShotRecoil(new Vector2(20f, -20f), new Vector2(8f, 3f), 2f, 0f); // 반동 한도 초과 입력
            Check(camera.ShotRecoil == new Vector2(8f, -3f), "카메라 반동 상한", ref checks); // 실제 카메라 보정 제한 검사
            camera.ResetShotRecoil(); // 장착 해제 동작 재현
            Check(camera.ShotRecoil == Vector2.zero, "카메라 반동 초기화", ref checks); // 잔류 반동 제거 검사
            Debug.Log("Day11 사격 규칙 검사 통과: " + checks + "항목. 씬에서의 사격 화면 검증은 별도입니다."); // 검사 결과와 범위 출력
        }
        catch (Exception exception) // 실패 항목 전달
        {
            Debug.LogException(exception); // 실제 실패 원인 출력
        }
        finally // 검사 자료의 메모리 정리
        {
            UnityEngine.Object.DestroyImmediate(profile); // 임시 조정 자료 해제
            if (probe != null) // 생성된 검사 센서 확인
            {
                UnityEngine.Object.DestroyImmediate(probe); // 임시 센서와 이벤트 정리
            }

            if (cameraObject != null) // 생성된 카메라 확인
            {
                UnityEngine.Object.DestroyImmediate(cameraObject); // 임시 카메라 해제
            }
        }
    }

    private static void DeliverNoise(MethodInfo receive, DetectionSensor sensor, NoiseEvent noise) // 실제 센서 함수에 고정 소음 전달
    {
        object[] parameters = new object[1]; // 하나의 소음 인자 준비
        parameters[0] = noise; // 발사 시점 위치 자료 저장
        receive.Invoke(sensor, parameters); // 실제 청각 이벤트 코드 검사
    }

    private static bool Near(float actual, float expected) // 부동소수 오차 허용 비교
    {
        return Mathf.Abs(actual - expected) < 0.001f; // 충분히 작은 계산 오차만 허용
    }

    private static void Check(bool passed, string name, ref int count) // 실패 즉시 중단하는 단언
    {
        count++; // 실행한 검사항목 기록
        if (!passed) // 검사 실패 확인
        {
            throw new InvalidOperationException("Day11 검사 실패: " + name); // 통과로 오인하지 않도록 명시
        }
    }
}
#endif // 에디터 외 빌드에서 제외
