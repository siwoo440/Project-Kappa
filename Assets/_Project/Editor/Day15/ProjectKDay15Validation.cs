#if UNITY_EDITOR // 실제 클래스와 임시 객체 검사 메뉴
using System; // 실패 항목 보고
using UnityEditor; // 패키지 없는 검사 실행
using UnityEditor.SceneManagement; // 저장하지 않는 테스트 공간
using UnityEngine; // 실제 표적과 JSON 검사
using UnityEngine.SceneManagement; // 사용자 작업 씬 복원

public static class ProjectKDay15Validation // 계측 수치와 원본 복구 검사
{
    private static void Check(bool pass, string name, ref int count) // 실제 검사 통과 집계
    {
        if (!pass) // 실패한 조건 확인
        {
            throw new InvalidOperationException("Day 15 검사 실패: " + name); // 정확한 실패 원인
        }
        count++; // 통과한 검사 수
    }

    private static bool Safe() // 검증 가능한 편집 시점
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating; // 실행 중 씬 변경 금지
    }

    // 실제 수치 집계와 저장 형식 검사
    public static void TestMetrics() // 외부 테스트 패키지 없는 기능 검사
    {
        if (!Safe()) // 실행 상태 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 안전한 검사 시점
            return; // 실행 중 검사 중단
        }
        int checks = 0; // 통과 항목 수
        BalanceTrialRecord r = new BalanceTrialRecord(); // 실제 결과 구조
        BalanceTrialMetrics metrics = new BalanceTrialMetrics(r); // 실제 계측 클래스
        Check(metrics.AddShot(10, Sample(8, 0, 0, 0)), "첫 발이 빗나가도 발사 집계", ref checks); // 최초 명중 편향 방지
        Check(r.firstShotAt == 10 && r.shots == 1 && r.hitShots == 0, "발사부터 시간 시작", ref checks); // 시작 시계 확인
        Check(metrics.AddShot(11, Sample(8, 4, 1, 36)), "산탄 일부 적중 수용", ref checks); // 부분 명중 확인
        Check(r.shots == 2 && r.hitShots == 1 && r.pellets == 16 && r.hitPellets == 4, "발사와 펠릿 분리", ref checks); // 여러 배 집계 방지
        Check(BalanceTrialMetrics.Percent(r.hitShots, r.shots) == 50 && BalanceTrialMetrics.Percent(r.hitPellets, r.pellets) == 25, "명중률 분모 구분", ref checks); // 발사와 산탄 정확도 구분
        Check(r.headPellets == 1 && r.actualDamage == 36, "머리와 실제 피해 합산", ref checks); // 값 보존
        Check(!metrics.AddShot(12, Sample(8, 9, 0, 1)), "펠릿보다 많은 적중 거부", ref checks); // 잘못된 자료 제외
        Check(!metrics.AddShot(12, Sample(8, 1, 2, 1)), "적중보다 많은 머리 판정 거부", ref checks); // 부위 중복 방지
        Check(!metrics.AddShot(double.NaN, Sample(1, 1, 0, 1)), "무효 시각 거부", ref checks); // NaN 기록 방지
        Check(!metrics.AddShot(9, Sample(1, 1, 0, 1)), "첫 발 이전 시각 거부", ref checks); // 음수 시간 방지
        Check(!metrics.AddShot(10.5, Sample(1, 0, 0, 0)), "마지막 발사보다 이른 시각 거부", ref checks); // 중간 시각 역행 차단
        metrics.Finish("Completed", "test", 12); // 실제 종료 상태 적용
        Check(r.status == "Completed" && r.ttk == 2, "빗나감 포함 TTK", ref checks); // 완료 시간 확인
        Check(!metrics.AddShot(13, Sample(1, 1, 0, 1)), "완료 뒤 추가 집계 차단", ref checks); // 자동 연사 잔여 집계 방지
        metrics.Finish("Invalid", "late", 14); // 중복 종료 호출
        Check(r.status == "Completed" && r.ttk == 2, "완료 기록 중복 변경 차단", ref checks); // 저장 안정성
        BalanceTrialRecord aborted = new BalanceTrialRecord(); // 미완료 시험 자료
        BalanceTrialMetrics other = new BalanceTrialMetrics(aborted); // 별도 시험
        other.AddShot(4, Sample(1, 0, 0, 0)); // 빗나간 한 발
        other.Finish("Timeout", "limit", 64); // 시간초과
        Check(aborted.ttk < 0 && aborted.status == "Timeout", "미완료를 영초 처치로 기록하지 않음", ref checks); // 평균 오염 방지
        BalanceTrialMetrics empty = new BalanceTrialMetrics(new BalanceTrialRecord()); // 무발사 시험
        empty.Finish("Completed", "bad", 5); // 잘못된 완료 시도
        Check(empty.Record.status == "Invalid" && empty.Record.ttk < 0, "무발사 완료 거부", ref checks); // 공짜 완료 방지
        BalanceTrialMetrics lethal = new BalanceTrialMetrics(new BalanceTrialRecord()); // 첫 발 제압 시험
        lethal.AddShot(3, Sample(1, 1, 1, 150)); // 실제 즉시 제압
        lethal.Finish("Completed", "kill", 3); // 같은 프레임 완료
        Check(lethal.Record.ttk == 0 && lethal.Record.status == "Completed", "첫 발 제압은 실제 영초", ref checks); // 무발사와 즉시 처치 구분
        Check(BalanceTrialMetrics.Percent(0, 0) == 0, "영점 분모 안전 처리", ref checks); // 빈 기록 안전성
        Check(BalanceReportStore.Cell("a,\"b\"\n") == "\"a,\"\"b\"\"\n\"", "CSV 문자열 보존", ref checks); // 쉼표와 따옴표 줄바꿈
        Check(BalanceReportStore.Number(1.25) == "1.25", "숫자 소수점 통일", ref checks); // 운영체제 지역 독립
        string json = JsonUtility.ToJson(r); // 실제 유니티 저장 처리
        BalanceTrialRecord restored = JsonUtility.FromJson<BalanceTrialRecord>(json); // 실제 파일 복원 처리
        Check(restored.shots == 2 && restored.samples.Count == 2 && restored.ttk == 2, "JSON 왕복 보존", ref checks); // 실제 저장 형식 확인
        Check(BalanceReportStore.Hash("A") == BalanceReportStore.Hash("A") && BalanceReportStore.Hash("A") != BalanceReportStore.Hash("B"), "설정 버전 구분", ref checks); // 다른 수치 평균 분리 기준
        Debug.Log("Day 15 실제 계측 규칙 검사 통과: " + checks + "항목"); // 사용자가 실행한 결과만 표시
    }

    private static BalanceShotSample Sample(int pellets, int hits, int heads, float damage) // 실제 샘플 준비
    {
        BalanceShotSample sample = new BalanceShotSample(); // 새 발사 자료
        sample.pellets = pellets; // 발사 펠릿
        sample.hits = hits; // 적중 펠릿
        sample.heads = heads; // 머리 펠릿
        sample.damage = damage; // 실제 감소 피해
        return sample; // 검증용 자료 반환
    }

    // 기존 센터와 신규 계측 참조 확인
    public static void Validate() // 사용자 편집 씬 검사
    {
        if (!Safe()) // 안전한 검사 시점 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 검사 조건 안내
            return; // 실행 중 변경 방지
        }
        Scene scene = SceneManager.GetActiveScene(); // 실제 작업 씬
        TrainingCenterRoot[] centers = TrainingCenterMigration.Components<TrainingCenterRoot>(scene); // 씬 내부 시설 확인
        int checks = 0; // 성공 검사 수
        Check(centers.Length == 1, "통합 센터 한 개", ref checks); // 중복 시설 확인
        TrainingCenterRoot center = centers[0]; // 검증할 시설
        checks += TrainingCenterValidation.Validate(center, true); // 기존 스폰과 사격선 회귀 검사
        BalanceSessionRunner[] runners = center.GetComponentsInChildren<BalanceSessionRunner>(true); // 신규 계측 개수
        Check(runners.Length == 1 && runners[0].Center == center, "계측 관리자 한 개", ref checks); // 다른 센터 연결 방지
        BalanceSessionRunner runner = runners[0]; // 실제 계측 설정
        Check(runner.GetComponent<BalancePanel>() != null && runner.GetComponent<BalanceObservationRecorder>() != null, "화면과 실전 기록기 연결", ref checks); // 기능 컴포넌트 확인
        Check(runner.Lanes != null && runner.Lanes.Length == 9, "기존 아홉 사격선 재사용", ref checks); // 맵 새로 생성하지 않는 기준
        foreach (TrainingCenterLane lane in runner.Lanes) // 현재 레인 참조 순회
        {
            Check(lane != null && lane.Target != null && lane.FiringPoint != null && lane.Target.Hinge != null, "표적과 기준점 연결", ref checks); // 필수 참조 확인
            Check(lane.Target.GetComponent<BalanceTargetTag>() == null, "원본 표적 방어율 미변경", ref checks); // 임시 객체와 원본 분리
        }
        PlayerFirearmController gun = center.Player.GetComponent<PlayerFirearmController>(); // 실제 장착 목록
        SerializedProperty loadout = new SerializedObject(gun).FindProperty("loadout"); // 에디터에서 원본 참조 조회
        Check(loadout != null && loadout.arraySize == runner.Weapons.Length, "실제 총기 개수 일치", ref checks); // 인덱스 불일치 방지
        for (int i = 0; i < runner.Weapons.Length; i++) // 모든 무기 순서 확인
        {
            Check(runner.Weapons[i] != null && runner.Weapons[i] == loadout.GetArrayElementAtIndex(i).objectReferenceValue, "총기 슬롯 순서 보존", ref checks); // 다른 무기 측정 방지
        }
        Debug.Log("Day 15 실제 설정 검사 통과: " + checks + "항목"); // 실행 검사 결과 표시
    }

    // 원본 표적 보호와 복사본 수명 검사
    public static void TestRestore() // 저장하지 않는 임시 씬 검증
    {
        if (!Safe()) // 사용자 Play 확인
        {
            Debug.LogWarning("Play를 중지한 뒤 검사하세요."); // 검사 시점 안내
            return; // 실행 중 씬 변경 금지
        }
        Scene previous = SceneManager.GetActiveScene(); // 사용자 씬 보존
        Scene temporary = default; // 임시 씬 참조
        BalanceTrialRig rig = new BalanceTrialRig(); // 실제 게임 시험과 같은 복사 도구
        int checks = 0; // 실제 검증 개수
        try // 실패해도 원본 씬 복원
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 빈 검증 공간
            SceneManager.SetActiveScene(temporary); // 생성 객체 소속 지정
            GameObject root = new GameObject("BalanceRestoreTest"); // 독립 검사 루트
            root.transform.position = new Vector3(10000f, 0f, 10000f); // 기존 물리 세계와 거리 확보
            Transform targetRoot = Child(root.transform, "Original", new Vector3(0f, 0f, 10f)); // 원본 표적 위치
            Transform carriage = Child(targetRoot, "Carriage", Vector3.zero); // 이동 받침
            Transform hinge = Child(carriage, "Hinge", Vector3.up * 0.4f); // 회전 기준
            BoxCollider box = Child(hinge, "Body", Vector3.up).gameObject.AddComponent<BoxCollider>(); // 실제 피격 영역
            TrainingReactiveTarget target = targetRoot.gameObject.AddComponent<TrainingReactiveTarget>(); // 원래 동작
            Collider[] hits = new Collider[1]; // 검사할 피격 영역 목록
            hits[0] = box; // 실제 상자 연결
            target.Configure(hinge, carriage, hits, null, 0f, 2f, 1f); // 원본은 한 발 표적
            target.ResetTarget(); // 실제 초기화
            TrainingCenterLane lane = Child(root.transform, "Firing", Vector3.zero).gameObject.AddComponent<TrainingCenterLane>(); // 실제 사격선 자료
            lane.Configure(lane.transform, target, 10f); // 원본 참조 설정
            rig.Create(lane, root.transform, 150f, 0.5f); // 실제 계측 복사와 임시 조건
            Check(!target.gameObject.activeSelf && rig.Target != target, "원본 비활성 및 별도 복사본", ref checks); // 중복 피격 방지
            Check(rig.Target.RemainingHealth == 150f && target.RemainingHealth == 1f, "원본 체력 보존", ref checks); // 에셋 오염 방지
            Check(rig.Target.GetComponent<BalanceTargetTag>().Armor == 0.5f && target.GetComponent<BalanceTargetTag>() == null, "임시 방어율만 적용", ref checks); // 원본 수치 보호
            Check(rig.Target.ReceiveImpact(40f, Vector3.forward) && rig.Target.RemainingHealth == 110f, "임시 표적 실제 피해", ref checks); // 살아 있는 시험 표적 확인
            rig.Dispose(); // 게임과 같은 시험 종료
            Check(target.gameObject.activeSelf && target.RemainingHealth == 1f && target.AcceptsHit, "종료 후 원본 복원", ref checks); // 일반 사격 재개
            Check(rig.Target == null, "임시 참조 제거", ref checks); // 다음 시험 누수 방지
            Debug.Log("Day 15 표적 원본 복원 검사 통과: " + checks + "항목"); // 실행된 검사 보고
        }
        finally // 임시 씬과 복사본 정리
        {
            rig.Dispose(); // 남은 시험 원본 보호
            if (previous.IsValid() && previous.isLoaded) // 사용자 씬 확인
            {
                SceneManager.SetActiveScene(previous); // 기존 편집 상태 복귀
            }
            if (temporary.IsValid() && temporary.isLoaded) // 생성된 검사 공간 확인
            {
                EditorSceneManager.CloseScene(temporary, true); // 저장 없이 삭제
            }
        }
    }

    private static Transform Child(Transform parent, string name, Vector3 point) // 임시 검사 부품 생성
    {
        GameObject item = new GameObject(name); // 검사 객체
        item.transform.SetParent(parent, false); // 같은 임시 씬 계층
        item.transform.localPosition = point; // 검사 위치
        return item.transform; // 동작 연결용 반환
    }
}
#endif
