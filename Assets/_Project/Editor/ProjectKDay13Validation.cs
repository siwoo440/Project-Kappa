#if UNITY_EDITOR // 실제 상태 규칙 검사 메뉴
using System; // 실패한 검사 보고
using UnityEditor; // 사용자 검사 메뉴
using UnityEditor.SceneManagement; // 임시 검사 공간
using UnityEngine; // 실제 표적 구성
using UnityEngine.InputSystem; // 실제 추가된 무기 입력 검사
using UnityEngine.SceneManagement; // 원래 씬 보존

public static class ProjectKDay13Validation // 산탄 장전 표적 배치 검사
{
    private static void Check(bool result, string name, ref int checks) // 검사 결과 누적
    {
        if (!result) // 실패 조건 확인
        {
            throw new InvalidOperationException("Day13 검사 실패: " + name); // 정확한 항목 보고
        }
        checks++; // 성공 항목 기록
    }
    private static bool CanTest() // 안전한 검사 시점
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating; // 실행 중 사용자 장면 변경 금지
    }

    // 외부 테스트 패키지 없는 규칙 검사
    public static void TestRules() // 실제 런타임 상태 클래스 검사
    {
        if (!CanTest()) // 검사 시점 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 조건 안내
            return; // 검증 중단
        }
        int checks = 0; // 통과 항목 수
        FirearmRuntimeState shotgun = new FirearmRuntimeState(6, 30, 0.8f); // 실제 산탄총 상태
        shotgun.ConfigureMechanism(true, 0.8); // 한 발 장전과 펌프 준비
        Check(shotgun.TryFireScheduled(0), "산탄 한 발 발사", ref checks); // 최초 사격
        Check(shotgun.Rounds == 5 && shotgun.Reserve == 30, "여덟 펠릿에 탄약 한 발 소모", ref checks); // 펠릿 수와 탄수 분리
        Check(!shotgun.TryFireScheduled(0.4), "펌프 진행 중 재발사 차단", ref checks); // 준비 시간 우회 방지
        Check(!shotgun.TryBeginReload(0.4f, 0.72f), "펌프 중 장전 시작 차단", ref checks); // 기구 상태 충돌 방지
        Check(shotgun.TryFireScheduled(0.81), "펌프 완료 뒤 재발사", ref checks); // 완료 경계
        Check(shotgun.TryBeginReload(2f, 0.72f), "한 발씩 장전 시작", ref checks); // 삽입 시작
        Check(!shotgun.TickReload(2.7f) && shotgun.Rounds == 4, "미완료 탄약 이동 없음", ref checks); // 중도 취소 보존
        Check(shotgun.TickReload(2.73f) && shotgun.Rounds == 5 && shotgun.Reserve == 29, "삽입 한 발 완료", ref checks); // 실제 탄약 보충
        shotgun.CancelReload(); // 다음 탄 삽입 취소
        Check(shotgun.Rounds == 5 && shotgun.Reserve == 29 && !shotgun.IsReloading, "완료한 탄약은 취소 후 유지", ref checks); // 탄약 보존
        Check(shotgun.TryBeginReload(3f, 0.72f), "남은 한 발 재장전", ref checks); // 재개
        shotgun.TickReload(20f); // 긴 프레임 지연 모사
        Check(shotgun.Rounds == 6 && shotgun.Reserve == 28 && !shotgun.IsReloading, "긴 지연에도 탄창 한도 유지", ref checks); // 탄약 복제 방지
        FirearmRuntimeState rifle = new FirearmRuntimeState(30, 120, 0.1f); // 기존 탄창형 소총
        rifle.TryFireScheduled(0); // 시험 탄약 소모
        rifle.TryFireScheduled(0.2); // 추가 탄약 소모
        rifle.TryBeginReload(1f, 2.3f); // 기존 탄창 재장전
        Check(!rifle.TickReload(3f) && rifle.Rounds == 28, "기존 재장전 완료 전 보존", ref checks); // 이전 일차 호환
        Check(rifle.TickReload(3.4f) && rifle.Rounds == 30 && rifle.Reserve == 118, "기존 탄창 일괄 보충 유지", ref checks); // 이전 일차 완료 동작
        TrainingTargetCycle cycle = new TrainingTargetCycle(0.24f, 2.5f, 0.55f); // 실제 표적 상태
        Check(cycle.AcceptsHit && cycle.Hit(), "서 있는 표적 피격", ref checks); // 피격 시작
        Check(!cycle.Hit(), "넘어짐 중 중복 피격 거부", ref checks); // 반복 피해 방지
        cycle.Tick(0.12f); // 넘어짐 절반 경과
        Check(cycle.State == TrainingTargetCycle.Phase.Falling && cycle.Tilt > 0.4f && cycle.Tilt < 0.6f, "부드러운 넘어짐 중간 상태", ref checks); // 시각 연결 값
        cycle.Tick(0.2f); // 넘어짐 완료
        Check(cycle.State == TrainingTargetCycle.Phase.Down, "누운 상태 유지", ref checks); // 정지 구간
        cycle.Tick(3f); // 전체 복귀 완료
        Check(cycle.AcceptsHit && cycle.Tilt == 0f, "자동으로 다시 일어남", ref checks); // 반복 훈련
        Check(Math.Abs(FirearmDamageMath.Resolve(72f / 8f, 1f, 0f, 0f) * 8f - 72f) < 0.001f, "전체 산탄 피해 보존", ref checks); // 피해 여덟 배 오류 방지
        Check(Math.Abs(FirearmDamageMath.Resolve(72f / 8f, 1f, 0f, 0f) * 4f - 36f) < 0.001f, "반만 적중한 산탄 피해", ref checks); // 부분 명중
        Debug.Log("Day13 규칙 검사 통과: " + checks + "항목"); // 실행한 검사 결과만 표시
    }

    // 실제 표적 회전과 충돌 검사
    public static void TestTargetMotion() // 저장하지 않는 검사 전용 씬
    {
        if (!CanTest()) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 실행 조건 안내
            return; // 실행 중 씬 변경 차단
        }
        Scene previous = SceneManager.GetActiveScene(); // 원래 작업 씬 보존
        Scene temporary = default; // 임시 검사 씬
        int checks = 0; // 실제 검사 항목 수
        try // 오류에도 임시 객체 정리
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 저장 없는 빈 검사 공간
            SceneManager.SetActiveScene(temporary); // 생성 객체의 소속 지정
            GameObject root = new GameObject("Day13_TargetMotionTest"); // 사용자 표적과 분리한 객체
            root.transform.position = new Vector3(10000f, 0f, 10000f); // 기존 맵과 물리 겹침 방지
            Transform carriage = ProjectKDay13Geometry.Node(root.transform, "Carriage", Vector3.zero); // 실제 이동 받침
            Transform hinge = ProjectKDay13Geometry.Node(carriage, "Hinge", new Vector3(0f, 0.4f, 0f)); // 실제 회전축
            BoxCollider plate = ProjectKDay13Geometry.Node(hinge, "Plate", Vector3.up).gameObject.AddComponent<BoxCollider>(); // 실제 피격 상자
            plate.size = new Vector3(1f, 1f, 0.1f); // 검사 가능한 표적 크기
            TrainingReactiveTarget target = root.AddComponent<TrainingReactiveTarget>(); // 게임과 같은 표적 동작
            Collider[] colliders = new Collider[1]; // 검사 피격 영역
            colliders[0] = plate; // 상자 참조 저장
            target.Configure(hinge, carriage, colliders, null, 6f, 2f); // 한 번 피격하는 이동 표적 설정
            target.ResetTarget(); // 최초 위치와 충돌 상태 준비
            target.Advance(0.5f); // 실제 이동 함수 실행
            Check(target.AcceptsHit && plate.enabled, "서 있는 표적 피격 가능", ref checks); // 초기 충돌 확인
            Check(Mathf.Abs(carriage.localPosition.x - 1f) < 0.01f, "레일 속도 2m/s 이동", ref checks); // 실제 위치 확인
            Physics.SyncTransforms(); // 이동한 상자의 물리 위치 갱신
            Vector3 rayStart = plate.bounds.center + Vector3.back * 4f; // 표적 정면 검사 위치
            Check(Physics.Raycast(rayStart, Vector3.forward, out RaycastHit hit, 5f) && hit.collider == plate, "이동 후 실제 충돌 위치", ref checks); // 화면뿐인 가짜 이동 방지
            Vector3 stopped = carriage.localPosition; // 피격 순간 받침 위치 보존
            Check(target.ReceiveImpact(1f, Vector3.forward), "피해에 의한 넘어짐 시작", ref checks); // 실제 피해 API 검사
            Check(!plate.enabled && !target.AcceptsHit, "넘어지는 표적 재피격 차단", ref checks); // 중복 명중 차단
            target.Advance(0.12f); // 넘어짐 동작 중간
            Check(Quaternion.Angle(Quaternion.identity, hinge.localRotation) > 35f, "표적판 뒤로 회전", ref checks); // 실제 Transform 검사
            Check(Vector3.Distance(stopped, carriage.localPosition) < 0.001f, "넘어짐 중 레일 정지", ref checks); // 이동과 회전 동기화
            target.Advance(0.15f); // 넘어진 상태 도착
            Check(target.State == TrainingTargetCycle.Phase.Down, "바닥 쪽으로 젖힌 자세 유지", ref checks); // 정지 구간 확인
            target.Advance(3.1f); // 자동 복귀 시간 경과
            Check(target.AcceptsHit && plate.enabled && Quaternion.Angle(Quaternion.identity, hinge.localRotation) < 0.01f, "완전 복귀 후 충돌 재활성화", ref checks); // 자동 재사용 검사
            target.ResetTarget(); // F 초기화와 같은 처리
            Check(carriage.localPosition == Vector3.zero && target.Knockdowns == 0, "초기화 시 위치와 기록 복구", ref checks); // 사용자 반복 테스트 보장
            Debug.Log("Day13 실제 표적 검사 통과: " + checks + "항목"); // 실행한 검사 결과
        }
        finally // 사용자 작업 씬으로 복귀
        {
            if (previous.IsValid() && previous.isLoaded) // 기존 씬 유효성 확인
            {
                SceneManager.SetActiveScene(previous); // 원래 편집 씬 복구
            }
            if (temporary.IsValid() && temporary.isLoaded) // 임시 씬 생성 확인
            {
                EditorSceneManager.CloseScene(temporary, true); // 테스트 객체를 저장하지 않고 제거
            }
        }
    }

    // 씬 참조와 안전거리 검사
    public static void ValidateScene() // 현재 저장된 훈련장 확인
    {
        if (!CanTest()) // 안전한 편집 상태 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 조건 안내
            return; // 실행 중 검사 중단
        }
        Scene scene = SceneManager.GetSceneByPath(ProjectKDay13Setup.ScenePath); // 대상 씬 조회
        bool opened = !scene.IsValid() || !scene.isLoaded; // 자신이 열었는지 기록
        try // 로드한 씬 정리
        {
            if (opened) // 대상 씬 미로드 상태
            {
                scene = EditorSceneManager.OpenScene(ProjectKDay13Setup.ScenePath, OpenSceneMode.Additive); // 다른 작업 씬 유지
            }
            int checks = 0; // 검사 수
            TrainingCampusMarker[] markers = ProjectKDay13Campus.Components<TrainingCampusMarker>(scene); // 실제 구역 표식
            Check(markers.Length == 1 && markers[0].Completed, "완료된 확장 구역 하나", ref checks); // 중복 생성 검사
            GameObject player = ProjectKDay9Setup.FindNamed(scene, "Player"); // 기존 플레이어
            Check(player != null && Vector3.Distance(player.transform.position, markers[0].OriginalSpawn) < 0.1f, "원래 스폰 위치 보존", ref checks); // 사용자 스폰 유지
            foreach (EnemyActor actor in ProjectKDay13Campus.Components<EnemyActor>(scene)) // 모든 실제 적 확인
            {
                Check(Vector3.Distance(actor.transform.position, markers[0].OriginalSpawn) >= 100f, "스폰으로부터 적 안전거리: " + actor.name, ref checks); // 초기 교전 방지
                PatrolGuardAI patrol = actor.GetComponent<PatrolGuardAI>(); // 순찰 경비 경로 검사
                if (patrol != null) // 순찰하는 적 확인
                {
                    SerializedProperty route = new SerializedObject(patrol).FindProperty("patrolPoints"); // 저장된 실제 경로
                    Check(route != null && route.arraySize >= 2, "순찰 경로 연결", ref checks); // 경로 누락 방지
                    for (int i = 0; i < route.arraySize; i++) // 각 경로 거리
                    {
                        Transform point = route.GetArrayElementAtIndex(i).objectReferenceValue as Transform; // 실제 순찰 지점
                        Check(point != null && point.IsChildOf(markers[0].transform) && Vector3.Distance(point.position, markers[0].OriginalSpawn) >= 100f, "옛 스폰으로 돌아가지 않는 경로", ref checks); // 경로 참조까지 이동 확인
                    }
                }
            }
            PlayerFirearmController firearm = player.GetComponent<PlayerFirearmController>(); // 실제 장비 참조
            Check(firearm != null, "기존 총기 관리자 유지", ref checks); // 필수 컴포넌트 검사
            System.Collections.Generic.List<FirearmDefinition> loadout = ProjectKDay11Setup.GetLoadout(firearm); // 저장된 무기 선택 목록
            Check(loadout.Count >= 5 && loadout[3] == AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay13Weapons.PathFor(0)) && loadout[4] == AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay13Weapons.PathFor(1)), "8번 문지기와 9번 천리안 장착 연결", ref checks); // 기존 5~7번 유지 확인
            InputActionAsset actions = player.GetComponent<PlayerInput>()?.actions; // 실제 입력 자료
            Check(actions != null && actions.FindAction("Player/Equip8", false) != null && actions.FindAction("Player/Equip9", false) != null, "추가 무기 선택 입력", ref checks); // 새 선택 동작 검사
            TrainingReactiveTarget[] targets = markers[0].GetComponentsInChildren<TrainingReactiveTarget>(true); // 새 표적 목록
            Check(targets.Length >= 10, "고정 이동 장거리 계측 표적", ref checks); // 계획된 표적 수
            int moving = 0; // 이동 표적 수
            foreach (TrainingReactiveTarget target in targets) // 표적 구성 순회
            {
                Check(target.Hinge != null, "표적 회전축 참조", ref checks); // 넘어짐 기준 확인
                moving += target.Travel > 0f ? 1 : 0; // 이동 표적 집계
            }
            Check(moving >= 3, "이동 표적 세 개 이상", ref checks); // 움직임 테스트 대상 확인
            for (int i = 0; i < 2; i++) // 신규 총기 자료 검증
            {
                FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(ProjectKDay13Weapons.PathFor(i)); // 저장된 자료
                Check(definition != null && definition.IsValid && definition.SingleRoundReload, "산탄 저격 정의와 한 발 장전", ref checks); // 참조 검증
                Check(definition.PelletCount == (i == 0 ? 8 : 1), "총기별 탄환 수", ref checks); // 탄도 모드 검증
            }
            Debug.Log("Day13 배치 검사 통과: " + checks + "항목"); // 실제 검사 결과
        }
        finally // 자기 로드만 되돌리기
        {
            if (opened && scene.IsValid() && scene.isLoaded) // 임시로 연 씬 확인
            {
                EditorSceneManager.CloseScene(scene, true); // 저장하지 않고 닫기
            }
        }
    }
}
#endif // 런타임에서 검사 메뉴 제외
