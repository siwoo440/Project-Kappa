#if UNITY_EDITOR // 실제 씬과 임시 시험 검사
using System; // 단언 실패 표시
using System.IO; // 원본 백업 검사
using UnityEditor; // 검사 메뉴
using UnityEditor.SceneManagement; // 임시 씬 수명 관리
using UnityEngine; // 실제 물리와 구역 처리
using UnityEngine.SceneManagement; // 작업 씬 보존

public static class TrainingCenterValidation // 참조와 사격선과 시험 초기화 검증
{
    private static void Check(bool value, string message, ref int count) // 실행한 검사만 집계
    {
        if (!value) // 실패 조건 확인
        {
            throw new InvalidOperationException("Training Center 검사 실패: " + message); // 정확한 문제 위치 보고
        }
        count++; // 성공한 항목 추가
    }

    public static int Validate(TrainingCenterRoot center, bool physics) // 실제 Test 씬 검증
    {
        int checks = 0; // 실행 항목 수
        Check(center != null && center.Completed, "센터 구성 완료", ref checks); // 부분 설치 제외
        Check(center.Player != null && center.Spawn != null, "플레이어와 새 스폰", ref checks); // 필수 참조
        Check(center.Zones != null && center.Zones.Length == 7, "일곱 구역 구성", ref checks); // 배치 구조 확인
        Check(File.Exists(center.BackupScene), "이전 씬 백업 존재", ref checks); // 원본 보관 확인
        Check(Vector3.Distance(center.Player.transform.position, center.Spawn.position) < 0.3f, "플레이어의 로비 스폰", ref checks); // 실제 시작 위치
        Check(center.Zones[0].Contains(center.Spawn.position), "안전 로비 내부 스폰", ref checks); // 경계 일치
        Check(center.Player.GetComponent<PlayerFirearmController>() != null && center.Player.GetComponent<PlayerCombatController>() != null, "검과 총 기존 시스템 유지", ref checks); // 핵심 전투 보존
        Physics.SyncTransforms(); // 시작점의 새 시설 충돌 위치 반영
        if (physics) // 물리 배치 검사 선택
        {
            AssertPlacement(center.Player, center.Spawn.position, "새 로비 스폰", ref checks); // 바닥과 벽 내부 시작 방지
        }
        foreach (TrainingCenterZone zone in center.Zones) // 구역별 검사
        {
            Check(zone != null && zone.Entry != null && zone.RuntimeRoot != null, "구역 기본 참조", ref checks); // 이동과 시험 부모
            Check(zone.Contains(zone.Entry.position), zone.ZoneName + " 내부 도착점", ref checks); // 이동 시 즉시 이탈 방지
            if (physics) // 실제 플레이어 크기의 도착 여유 검사
            {
                AssertPlacement(center.Player, zone.Entry.position, zone.ZoneName + " 도착점", ref checks); // 단말기 이동 후 끼임 방지
            }
            foreach (GameObject template in zone.Templates ?? new GameObject[0]) // 보존한 적 원본
            {
                Check(template != null && !template.activeInHierarchy && !template.activeSelf, "비활성 원본 저장", ref checks); // 초기 적 활성화 방지
                Check(zone.Contains(template.transform.position), "적 원본의 해당 구역 배치", ref checks); // 벽 밖 원본 제외
                Check(Vector3.Distance(template.transform.position, center.Spawn.position) >= 100f, "스폰에서 적 안전거리", ref checks); // 원래 요청 유지
                if (physics) // 적 시작 위치의 실제 캡슐 검사
                {
                    AssertPlacement(template, template.transform.position, template.name + " 시작점", ref checks); // 지형 내부 원본 생성 방지
                }
                PatrolGuardAI patrol = template.GetComponent<PatrolGuardAI>(); // 순찰 참조 검사
                if (patrol != null) // 실제 순찰하는 적만 확인
                {
                    SerializedProperty route = new SerializedObject(patrol).FindProperty("patrolPoints"); // 저장된 경로
                    Check(route != null && route.arraySize >= 2, "순찰 경로 존재", ref checks); // 경로 누락 검사
                    for (int i = 0; i < route.arraySize; i++) // 실제 경로 지점 확인
                    {
                        Transform point = route.GetArrayElementAtIndex(i).objectReferenceValue as Transform; // 지점 참조
                        Check(point != null && zone.Contains(point.position) && point.IsChildOf(zone.transform), "새 구역 내부 순찰 경로", ref checks); // 이전 맵으로 이동 방지
                        if (physics) // 순찰 목적지 도착 가능 여부
                        {
                            AssertPlacement(template, point.position, template.name + " 순찰 지점", ref checks); // 막힌 목적지로 계속 이동하는 문제 방지
                        }
                    }
                }
            }
        }
        TrainingCenterConsole[] consoles = center.GetComponentsInChildren<TrainingCenterConsole>(true); // 조작 장치 목록
        Check(consoles.Length >= 15, "보급과 구역별 제어 장치", ref checks); // 주요 기능 장치 확인
        foreach (TrainingCenterConsole console in consoles) // 장치별 부모 참조 확인
        {
            Check(console.Center == center, "단말기의 통합 센터 연결", ref checks); // 예전 시설 참조 방지
            Check(console.GetComponentInChildren<Collider>() != null, "F 상호작용 충돌체", ref checks); // 화면만 있는 단말기 방지
        }
        TrainingCenterLane[] lanes = center.GetComponentsInChildren<TrainingCenterLane>(true); // 실제 사격선
        Check(lanes.Length == 9, "고정 4 · 이동 3 · 정밀 2 사격선", ref checks); // 독립 표적 수
        Physics.SyncTransforms(); // 위치 변경한 콜라이더 정보 갱신
        foreach (TrainingCenterLane lane in lanes) // 각 기준점 거리 검사
        {
            Check(lane.FiringPoint != null && lane.Target != null, "사격선 참조", ref checks); // 누락된 표적 확인
            Vector3 delta = lane.Target.transform.position - lane.FiringPoint.position; // 초기 표적까지의 거리
            delta.y = 0f; // 수평 표시 거리 사용
            Check(Mathf.Abs(delta.magnitude - lane.Distance) < 0.02f, "표시 거리와 실제 표적 위치: " + lane.name, ref checks); // 잘못된 거리 표기 방지
            if (physics) // 사용자 요청한 실제 사격선 검사
            {
                Transform plate = lane.Target.Hinge.Find("BodyHit"); // 기존 표적의 실제 몸통 콜라이더
                Collider collider = plate != null ? plate.GetComponent<Collider>() : null; // 피격 면 조회
                Check(collider != null, "표적 몸통 피격 영역", ref checks); // 핵심 물리 참조
                Vector3 origin = lane.FiringPoint.position; // 지정 발사선
                origin.y = collider.bounds.center.y; // 표적 몸통 높이의 시야 검사
                Vector3 direction = collider.bounds.center - origin; // 실제 목표 방향
                bool hit = Physics.Raycast(origin, direction.normalized, out RaycastHit result, direction.magnitude + 0.3f, ~0, QueryTriggerInteraction.Ignore); // 첫 장애물 확인
                Check(hit && result.collider.GetComponentInParent<TrainingReactiveTarget>() == lane.Target, "부스와 구조물에 막히지 않는 사격선: " + lane.name, ref checks); // 눈에만 보이는 표적 문제 검사
            }
        }
        foreach (GameObject root in center.gameObject.scene.GetRootGameObjects()) // 활성 맵 루트 확인
        {
            bool preserved = Array.IndexOf(center.StandaloneSystems ?? new GameObject[0], root) >= 0; // 기존 전역 관리자만 별도 허용
            Check(root == center.gameObject || preserved, "새 시설과 기존 공통 시스템만 남는 씬: " + root.name, ref checks); // 옛 확장 맵 제외
        }
        return checks; // 실제 통과 수 반환
    }

    private static void AssertPlacement(GameObject owner, Vector3 position, string label, ref int checks) // 비활성 원본도 실제 캡슐 크기로 배치 검사
    {
        CharacterController body = owner.GetComponent<CharacterController>(); // 이동용 캡슐 조회
        CapsuleCollider capsule = owner.GetComponent<CapsuleCollider>(); // 고정 더미용 캡슐 조회
        if (body == null && (capsule == null || capsule.direction != 1)) // 수직 캡슐이 없는 장치 확인
        {
            return; // 벽 부착 카메라의 임의 크기 가정 생략
        }
        Vector3 scale = owner.transform.lossyScale; // 원본의 실제 크기
        float radius = (body != null ? body.radius : capsule.radius) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)); // 가로 피격 폭
        float height = Mathf.Max(radius * 2f, (body != null ? body.height : capsule.height) * Mathf.Abs(scale.y)); // 수직 이동 높이
        Vector3 localCenter = body != null ? body.center : capsule.center; // 피벗에 대한 캡슐 중심
        Vector3 center = owner.transform.TransformPoint(localCenter) + position - owner.transform.position; // 검사 위치로 옮긴 중심
        Vector3 offset = Vector3.up * (height * 0.5f - radius); // 양끝 반구 중심 간격
        Collider[] overlaps = Physics.OverlapCapsule(center - offset, center + offset, radius * 0.98f, ~0, QueryTriggerInteraction.Ignore); // 미세한 접촉을 제외한 실제 겹침 검사
        foreach (Collider other in overlaps) // 시작점과 겹친 시설 조회
        {
            if (other == null || other.gameObject.scene != owner.scene || other.transform == owner.transform || other.transform.IsChildOf(owner.transform)) // 자기 부품과 다른 씬 제외
            {
                continue; // 검사 대상이 아닌 충돌 생략
            }
            Check(false, label + "이 구조물과 겹침: " + other.name, ref checks); // 자동 저장 전에 배치 오류 중단
        }
        Check(true, label + " 캡슐 여유", ref checks); // 검사한 시작점 기록
    }

    [MenuItem("Project K/Training Center/Validate Integrated Center")] // 실제 씬 검사 메뉴
    public static void ValidateCurrent() // 현재 열려 있는 Test 검사
    {
        if (!CanTest()) // 안전한 검사 시점
        {
            return; // 실행 중 검사 중단
        }
        TrainingCenterRoot[] roots = TrainingCenterMigration.Components<TrainingCenterRoot>(SceneManager.GetActiveScene()); // 활성 씬의 센터
        if (roots.Length != 1) // 중복 또는 누락 확인
        {
            Debug.LogError("통합 센터가 하나 있는 Test 씬을 열어주세요."); // 대상 안내
            return; // 잘못된 씬 검사 중단
        }
        int checks = Validate(roots[0], true); // 실제 배치와 물리 경로 확인
        Debug.Log("통합 훈련센터 검사 통과: " + checks + "항목"); // 실행 결과만 보고
    }

    [MenuItem("Project K/Training Center/Test Zone Isolation And Reset")] // 임시 씬 상태 검사
    public static void TestIsolation() // 기존 맵을 건드리지 않는 시험 수명 검증
    {
        if (!CanTest()) // 편집 시점 확인
        {
            return; // 플레이 중 검사 금지
        }
        Scene previous = SceneManager.GetActiveScene(); // 사용자 작업 씬 보존
        Scene temporary = default; // 검사 전용 씬
        int checks = 0; // 실행 항목 수
        try // 임시 자료 정리 보장
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 빈 추가 씬
            SceneManager.SetActiveScene(temporary); // 검사 객체 소속 지정
            GameObject root = new GameObject("Center_Isolation_Test"); // 사용자 센터와 분리
            TrainingCenterZone zone = root.AddComponent<TrainingCenterZone>(); // 실제 런타임 구역
            Transform entry = TrainingCenterGeometry.Node(root.transform, "Entry", new Vector3(2f, 0f, 2f)); // 내부 도착점
            Transform runtime = TrainingCenterGeometry.Node(root.transform, "Runtime", Vector3.zero); // 실행 부모
            zone.Configure("TestZone", new Vector2(10f, 10f), entry, runtime); // 실제 경계 설정
            GameObject template = new GameObject("Inactive_Original"); // 원본 객체
            template.transform.SetParent(root.transform, false); // 테스트 소속
            template.transform.localPosition = new Vector3(5f, 0f, 5f); // 유효한 시작점
            template.AddComponent<BoxCollider>(); // 실제 물리 부품 보존 검사
            GameObject[] originals = new GameObject[1]; // 테스트 원본 목록
            originals[0] = template; // 비활성화 대상 등록
            zone.SetTemplates(originals); // 비활성 원본으로 등록
            GameObject user = new GameObject("TestUser"); // 가상 사용자 위치
            user.transform.position = entry.position; // 구역 안 진입
            Check(!template.activeSelf && !zone.IsRunning, "시작 전 원본 비활성", ref checks); // 초기 안전 상태
            Check(zone.Contains(new Vector3(5f, 0f, 5f)) && !zone.Contains(new Vector3(11f, 0f, 5f)), "구역 안과 밖 구분", ref checks); // 경계 계산
            Check(zone.BeginTrial(user) && zone.RunningCount == 1, "원본에서 실행 객체 생성", ref checks); // 실제 시험 시작
            Check(!template.activeSelf && runtime.childCount == 1, "원본은 비활성 상태 유지", ref checks); // 원본 감시 중복 방지
            Check(zone.BeginTrial(user) && runtime.childCount == 1, "재시작 시 객체 중복 없음", ref checks); // 반복 실행 안정성
            zone.StopTrial(); // 실제 정지 처리
            Check(!zone.IsRunning && runtime.childCount == 0 && template != null, "정지 후 실행 객체만 제거", ref checks); // 원본 보존
            user.transform.position = new Vector3(11f, 0f, 5f); // 구역 밖 이동
            Check(!zone.BeginTrial(user), "구역 밖 시험 시작 차단", ref checks); // 스폰에서 적 자동 실행 방지
            Debug.Log("구역 격리와 초기화 검사 통과: " + checks + "항목"); // 실제 메뉴 결과
        }
        finally // 실패해도 원래 씬 복구
        {
            if (previous.IsValid() && previous.isLoaded) // 기존 씬 확인
            {
                SceneManager.SetActiveScene(previous); // 사용자 작업 씬 복귀
            }
            if (temporary.IsValid() && temporary.isLoaded) // 생성된 검사 씬 확인
            {
                EditorSceneManager.CloseScene(temporary, true); // 임시 객체 전체 제거
            }
        }
    }

    private static bool CanTest() // 안전한 편집 검사 조건
    {
        bool allowed = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating; // 실행과 임포트 중 검사 방지
        if (!allowed) // 검사 불가 상태
        {
            Debug.LogWarning("Play 중지와 컴파일 완료 후 검사하세요."); // 사용자 실행 조건
        }
        return allowed; // 메뉴 허용 여부
    }
}
#endif
