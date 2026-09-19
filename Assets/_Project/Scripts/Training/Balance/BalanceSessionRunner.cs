using System; // 시험 고유 시각과 오류 처리
using System.Collections.Generic; // 무기별 시험 결과 목록
using UnityEngine; // 기존 훈련센터와 계측 연결

public enum BalanceStance // 동일한 발사 조건 선택
{
    StandingAim, // 서서 정지 조준
    StandingHip, // 서서 정지 비조준
    CrouchedAim, // 앉아서 정지 조준
    StrafeAim // 사격선에서 좌우 이동 조준
}

public enum BalanceHitPolicy // 허용할 명중 부위 선택
{
    Any, // 머리와 몸통 자유 사격
    BodyOnly, // 몸통 적중만 허용
    HeadOnly // 머리 적중만 허용
}

[DefaultExecutionOrder(1000)] // 실제 사격과 피해 처리 이후 검증
[DisallowMultipleComponent] // 계측 관리자 중복 방지
public sealed class BalanceSessionRunner : MonoBehaviour // 훈련센터의 통제된 총기 비교 시험
{
    public const string BaseCommit = "1c6a2b18e4aee8188793dddb9e31d78cf9f33861"; // 제작 기준 커밋
    [SerializeField] private TrainingCenterRoot center; // 기존 시설 참조
    [SerializeField] private TrainingCenterLane[] lanes; // 기존 사격선 참조
    [SerializeField] private FirearmDefinition[] weapons; // 기존 총기 장착 순서
    [SerializeField, Min(5f)] private float maximumSeconds = 60f; // 실제 첫 발사 이후 시험 제한
    private readonly BalanceTrialRig rig = new BalanceTrialRig(); // 원본 보존용 임시 표적
    private BalanceTrialMetrics metrics; // 이번 시험의 수치 집계
    private PlayerFirearmController firearm; // 실제 총기 상태
    private PlayerEquipmentManager equipment; // 실제 장착 관리자
    private PlayerMovement movement; // 실제 이동 조건
    private PlayerHealth health; // 시험 중 생존 확인
    private TrainingCenterLane activeLane; // 지정된 사격선
    private FirearmDefinition activeWeapon; // 시험 중 고정한 무기
    private BalanceHitPolicy activePolicy; // 이번 시험의 명중 부위 규칙
    private BalanceStance activeStance; // 시험 중 고정한 자세
    private float expectedHealth; // 기록된 표적 체력
    private int initialTotalAmmo; // 보급 오염 검사 기준
    private double armedAt; // 무발사 대기 제한 시각
    private double previousFrame; // 장전 시간 관측 기준
    private bool wasReloading; // 이전 프레임 장전 상태
    private bool finalized; // 결과 중복 저장 방지
    private double cleanupAt = -1; // 넘어짐 연출 후 원본 복원 시각
    private string snapshot; // 시험 시작 수치 자료
    private List<BalanceTrialRecord> history = new List<BalanceTrialRecord>(); // 최근 저장 결과
    public TrainingCenterRoot Center => center; // UI 시설 참조
    public TrainingCenterLane[] Lanes => lanes; // 선택할 사격선
    public FirearmDefinition[] Weapons => weapons; // 선택할 총기
    public IReadOnlyList<BalanceTrialRecord> History => history; // 비교용 최근 기록
    public BalanceTrialRecord Current => metrics != null ? metrics.Record : null; // 현재 결과 조회
    public bool IsActive => metrics != null && !metrics.IsFinished; // 진행 중 시험 확인
    private string messageValue = "F8 또는 로비 계측 단말기 F"; // 사용자 상태 안내
    public string Message // 사용자 상태 안내
    {
        get => messageValue; // 현재 값 조회
        private set => messageValue = value; // 내부 상태 갱신
    }
    private string lastSavedPathValue = ""; // 실제 저장 위치
    public string LastSavedPath // 실제 저장 위치
    {
        get => lastSavedPathValue; // 현재 값 조회
        private set => lastSavedPathValue = value; // 내부 상태 갱신
    }

    public void Configure(TrainingCenterRoot owner, TrainingCenterLane[] ranges, FirearmDefinition[] definitions) // 에디터 연결
    {
        center = owner; // 기존 시설 연결
        lanes = ranges; // 기존 레인 연결
        weapons = definitions; // 기존 장착 자료 연결
    }

    private void OnEnable() // 새 Play 계측 연결
    {
        BalanceTelemetry.ShotResolved -= OnShot; // 중복 구독 제거
        BalanceTelemetry.ShotResolved += OnShot; // 실제 성공한 발사 구독
    }

    private void Start() // 이전 결과를 비교 목록으로 읽기
    {
        history = BalanceReportStore.LoadRecent(250); // 최근 결과만 메모리에 유지
    }

    public string ProfileKey(int laneIndex, float hp, float armor, BalanceStance stance, BalanceHitPolicy policy = BalanceHitPolicy.Any) // 같은 조건끼리 결과 분리
    {
        if (lanes == null || laneIndex < 0 || laneIndex >= lanes.Length || lanes[laneIndex] == null || lanes[laneIndex].Target == null) // 올바른 레인 확인
        {
            return ""; // 선택 오류 표시
        }
        TrainingCenterLane lane = lanes[laneIndex]; // 비교할 레인
        return "v1|" + lane.name + "|" + BalanceReportStore.Number(lane.Distance) + "m|HP" + BalanceReportStore.Number(hp) + "|armor" + BalanceReportStore.Number(armor) + "|travel" + BalanceReportStore.Number(lane.Target.Travel) + "|speed2|" + stance + "|" + policy + "|shape" + BalanceReportStore.TargetFingerprint(lane) + "|limit" + BalanceReportStore.Number(maximumSeconds); // 시험 조건 전체 구분
    }

    public bool TryStart(int weaponIndex, int laneIndex, float hp, float armor, BalanceStance stance, BalanceHitPolicy policy = BalanceHitPolicy.Any) // 선택한 무기와 조건으로 시험 시작
    {
        if (IsActive || center == null || !Application.isPlaying || !TrainingCenterRoot.CanUse(center.Player)) // 실행 상태와 기존 행동 제한 확인
        {
            Message = "진행 중인 행동을 끝낸 뒤 시험을 시작하세요."; // 시작 제한 안내
            return false; // 기존 동작 보존
        }
        if (weapons == null || weaponIndex < 0 || weaponIndex >= weapons.Length || weapons[weaponIndex] == null || !weapons[weaponIndex].IsValid || lanes == null || laneIndex < 0 || laneIndex >= lanes.Length || lanes[laneIndex] == null || lanes[laneIndex].Target == null || lanes[laneIndex].FiringPoint == null) // 저장된 시험 참조 확인
        {
            Message = "Day 15 설정 메뉴로 총기와 레인 참조를 확인하세요."; // 연결 오류 안내
            return false; // 불완전한 시험 방지
        }
        if (!BalanceTrialMetrics.Finite(hp) || !BalanceTrialMetrics.Finite(armor) || hp <= 0 || armor < 0 || armor > 1) // 시험 수치 범위 확인
        {
            Message = "시험 체력과 방어율을 확인하세요."; // 조건 오류 안내
            return false; // 잘못된 수치 제외
        }
        if (metrics != null && metrics.IsFinished && !finalized) // 이전 종료 결과 저장 대기 확인
        {
            FinalizeRecord(); // 새 시험이 이전 중단 기록을 덮지 않도록 처리
        }
        CleanupRig(); // 이전 결과의 복사본 제거
        GameObject user = center.Player; // 기존 플레이어 사용
        firearm = user.GetComponent<PlayerFirearmController>(); // 실제 사격 연결
        equipment = user.GetComponent<PlayerEquipmentManager>(); // 실제 장비 연결
        movement = user.GetComponent<PlayerMovement>(); // 실제 자세 연결
        health = user.GetComponent<PlayerHealth>(); // 실제 생존 연결
        activeLane = lanes[laneIndex]; // 이번 표적 선택
        activeWeapon = weapons[weaponIndex]; // 이번 총기 선택
        activePolicy = policy; // 이번 명중 부위 규칙 저장
        activeStance = stance; // 이번 자세 선택
        try // 부분 시험 생성 시 원본 복구
        {
            center.StopTrials(); // 실제 적 시험과 통제 계측 분리
            TrainingCenterRoot.Supply(user); // 시작 장비와 체력 보충
            TrainingCenterRoot.Teleport(user, activeLane.FiringPoint); // 같은 발사 기준점으로 이동
            if (equipment == null || firearm == null || movement == null || !equipment.TryEquipFirearm(weaponIndex) || firearm.Definition != activeWeapon || firearm.State == null) // 기존 장비 관리자를 통한 실제 장착 확인
            {
                throw new InvalidOperationException("현재 장비 목록과 시험 총기 순서가 일치하지 않습니다."); // 잘못된 무기 측정 방지
            }
            firearm.RefillAll(); // 첫 장착한 무기도 전체 탄약으로 시작
            rig.Create(activeLane, transform, hp, armor); // 원본 모형을 그대로 사용하는 임시 표적
            snapshot = BalanceReportStore.Snapshot(activeWeapon, firearm.IsSuppressed); // 시작 수치를 순수 값으로 저장
            BalanceTrialRecord record = new BalanceTrialRecord(); // 이번 시험 기록 생성
            record.id = BalanceReportStore.NewId(); // 고유 결과 번호
            record.createdUtc = DateTime.UtcNow.ToString("o"); // 표준 시각 기록
            record.baseCommit = BaseCommit; // 기준 소스 버전
            record.weaponId = activeWeapon.Stats.Id; // 실제 무기 식별자
            record.weaponName = activeWeapon.DisplayName; // 실제 무기 이름
            record.weaponSnapshot = snapshot; // 사용한 설정 자료
            record.weaponFingerprint = BalanceReportStore.Hash(snapshot); // 수치 버전 구분
            record.profileKey = ProfileKey(laneIndex, hp, armor, stance, policy); // 같은 조건 구분자
            record.laneName = activeLane.name; // 레인 식별자
            record.hitPolicy = policy.ToString(); // 머리와 몸통 시험 구분
            record.stance = stance.ToString(); // 요구한 자세
            record.nominalDistance = activeLane.Distance; // 레인 수평 거리
            record.targetHealth = hp; // 표적 시작 체력
            record.targetArmor = armor; // 표적 방어율
            record.targetTravel = activeLane.Target.Travel; // 표적 좌우 범위
            record.targetSpeed = 2f; // 동일한 레일 이동 속도
            record.initialRounds = firearm.State.Rounds; // 실제 시작 장탄수
            record.initialReserve = firearm.State.Reserve; // 실제 시작 예비탄
            metrics = new BalanceTrialMetrics(record); // 결과 집계 시작
            expectedHealth = hp; // 외부 피해 검사 기준
            initialTotalAmmo = record.initialRounds + record.initialReserve; // 보급 검사 기준
            armedAt = Time.timeAsDouble; // 대기 시간 시작
            previousFrame = armedAt; // 프레임 관측 시작
            wasReloading = false; // 새 시험 장전 상태
            finalized = false; // 결과 저장 대기
            Message = "시험 준비 · 선택한 자세에서 사격 · F8은 시험 중단"; // 실제 조작 안내
            return true; // 시험 시작 완료
        }
        catch (Exception error) // 초기화 실패 정리
        {
            CleanupRig(); // 원본 표적 복원
            metrics = null; // 불완전한 시험 제거
            Message = "시험 시작 실패: " + error.Message; // 실패 이유 표시
            Debug.LogException(error); // 자세한 오류 보고
            return false; // 실행 실패 반환
        }
    }

    private void OnShot(BalanceShotReport shot) // 실제 탄약 소모 후 명중 결과 수신
    {
        if (!IsActive || center == null || shot.Shooter != center.Player.transform) // 다른 플레이어나 일반 사격 제외
        {
            return; // 현재 시험만 집계
        }
        if (shot.Definition != activeWeapon || rig.Target == null) // 무기 교체와 표적 삭제 확인
        {
            Abort("시험 도중 무기 또는 표적 변경"); // 다른 무기 결과 혼합 방지
            return; // 잘못된 결과 제외
        }
        BalanceImpactReport impact = shot.Impacts.Find(value => value.Target == rig.Target); // 지정 표적의 합산 결과만 선택
        bool foreignDamage = impact != null && Mathf.Abs(impact.Before - expectedHealth) > 0.02f; // 검과 기타 외부 피해 확인
        BalanceShotSample sample = new BalanceShotSample(); // 한 발의 실제 조건
        sample.pellets = shot.Definition.PelletCount; // 발사한 전체 펠릿
        sample.hits = impact != null ? impact.Pellets : 0; // 지정 표적 적중
        sample.heads = impact != null ? impact.Heads : 0; // 지정 표적 머리 적중
        sample.damage = impact != null ? impact.AppliedDamage : 0f; // 실제 체력 감소
        sample.remaining = rig.Target.RemainingHealth; // 실제 남은 체력
        sample.distance = Vector3.Distance(shot.Muzzle, rig.Target.Hinge.position + Vector3.up); // 실제 발사 원점 거리 기록
        sample.speed = HorizontalSpeed(); // 실제 이동 속도
        sample.aimProgress = firearm.AimProgress; // 실제 조준 완성도
        sample.spread = shot.Spread; // 실제 사격 분산
        sample.blocked = shot.Blocked; // 총구 가림 여부
        if (!metrics.AddShot(shot.Time, sample)) // 무효한 관측 자료 확인
        {
            Abort("발사 계측값 오류"); // 잘못된 결과 분리
            return; // 추가 집계 중단
        }
        expectedHealth = sample.remaining; // 다음 외부 피해 검사 기준
        string violation = ShotConditionViolation(sample); // 선택 조건과 실제 사격 비교
        if (foreignDamage || !string.IsNullOrEmpty(violation)) // 시험 조건 오염 확인
        {
            Abort(foreignDamage ? "총기 이외의 피해 또는 표적 초기화 감지" : violation); // 이유를 남긴 무효 시험
        }
        else if (impact != null && impact.Killed) // 지정 표적의 실제 제압 확인
        {
            metrics.Finish("Completed", "지정 표적 제압", shot.Time); // 첫 발사부터 마지막 피해까지 고정
            firearm.Interrupt(); // 같은 프레임에 남은 연사 예약도 중단
        }
    }

    private string ShotConditionViolation(BalanceShotSample sample) // 동일 조건 사격 검사
    {
        if ((activePolicy == BalanceHitPolicy.BodyOnly && sample.heads > 0) || (activePolicy == BalanceHitPolicy.HeadOnly && sample.hits > sample.heads)) // 명중 부위 시험 조건 검사
        {
            return "선택한 머리 또는 몸통 명중 조건과 불일치"; // 다른 부위 결과 혼합 방지
        }
        if (BalanceReportStore.Snapshot(activeWeapon, firearm.IsSuppressed) != snapshot) // 시험 중 에셋 또는 부착 상태 변경
        {
            return "시험 도중 총기 설정 또는 소음기 변경"; // 다른 설정으로 분리할 사유
        }
        if (!movement.IsGrounded) // 공중 사격 여부 확인
        {
            return "선택한 지상 시험에서 공중 사격"; // 지상 자료 오염 방지
        }
        bool crouch = activeStance == BalanceStance.CrouchedAim; // 요구한 앉기 상태
        if (movement.IsCrouching != crouch) // 실제 자세 비교
        {
            return "선택한 서기 또는 앉기 조건과 불일치"; // 자세 불일치 표시
        }
        if (activeStance == BalanceStance.StandingHip ? sample.aimProgress > 0.01f : sample.aimProgress < 0.99f) // 조준 완성도 검사
        {
            return "선택한 완전 조준 또는 비조준 조건과 불일치"; // 확대 중 발사를 다른 자료로 분리
        }
        if (activeStance == BalanceStance.StrafeAim ? sample.speed < 0.25f : sample.speed > 0.20f) // 정지와 이동 조건 비교
        {
            return "선택한 정지 또는 좌우 이동 조건과 불일치"; // 다른 정확도 조건 혼합 방지
        }
        return ""; // 정상 시험 조건
    }

    private float HorizontalSpeed() // 의도한 입력이 아닌 실제 이동 측정
    {
        CharacterController body = movement != null ? movement.Controller : null; // 플레이어 실제 이동체
        return body != null && body.enabled ? Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude : 0f; // 벽에 막힌 입력 제외
    }

    private void LateUpdate() // 해당 프레임의 모든 사격 이후 확인
    {
        if (metrics == null) // 시험 생성 여부 확인
        {
            return; // 대기 중 계산 생략
        }
        double now = Time.timeAsDouble; // 같은 게임 시계 사용
        if (IsActive) // 진행 중 시험만 검사
        {
            double delta = Math.Max(0, now - previousFrame); // 유효한 프레임 간격
            metrics.Record.maximumFrameSeconds = Math.Max(metrics.Record.maximumFrameSeconds, delta); // 샘플 시간 오차 기록
            if (wasReloading && metrics.Record.firstShotAt >= 0) // 이전 프레임 장전 관측
            {
                metrics.Record.reloadSeconds += delta; // 장전 시간은 프레임 단위로 기록
            }
            bool reloading = firearm != null && firearm.IsReloading; // 이번 프레임 장전 상태
            if (reloading && !wasReloading) // 새로운 장전 진입 확인
            {
                metrics.Record.reloads++; // 관측된 장전 횟수 증가
            }
            wasReloading = reloading; // 다음 프레임 기준 저장
            previousFrame = now; // 시간 기준 갱신
            ValidateRunning(now); // 참조와 시험 오염 확인
        }
        if (metrics.IsFinished && !finalized) // 새 종료 결과 확인
        {
            FinalizeRecord(); // 한 번만 파일 저장
            cleanupAt = now + 3.3; // 넘어짐 연출 이후 원본 복구
        }
        if (!IsActive && cleanupAt >= 0 && now >= cleanupAt) // 결과 표적의 복구 시점 확인
        {
            CleanupRig(); // 원본 사격 기능 복원
        }
    }

    private void ValidateRunning(double now) // 실제 실행 중단과 오염 판정
    {
        if (!Application.isFocused || Time.timeScale != 1f) // 초점 이탈과 배속 변경 확인
        {
            Abort("창 초점 또는 게임 시간 배율 변경"); // 시간 비교가 다른 시험 제외
            return; // 추가 조건 생략
        }
        if (health == null || health.IsDead || rig.Target == null || !rig.Target.gameObject.activeInHierarchy || firearm == null || !firearm.IsEquipped || firearm.Definition != activeWeapon) // 생존과 대상과 무기 변경 확인
        {
            Abort("사망 · 표적 삭제 · 무기 변경"); // 미완료 기록 분리
            return; // 참조 접근 중단
        }
        Vector3 offset = activeLane.FiringPoint.InverseTransformPoint(center.Player.transform.position); // 레인 기준 이동량
        float width = activeStance == BalanceStance.StrafeAim ? 2.7f : 0.4f; // 사격선 내 좌우 이동 폭
        if (Mathf.Abs(offset.x) > width || Mathf.Abs(offset.z) > 0.6f || Mathf.Abs(offset.y) > 1f) // 사격 거리와 레인 유지 확인
        {
            Abort("지정한 사격선 범위 이탈"); // 가까이 걸어가서 처치한 시험 제외
        }
        else if (Mathf.Abs(rig.Target.RemainingHealth - expectedHealth) > 0.02f) // 다른 공격과 표적 리셋 확인
        {
            Abort("총기 외 피해 또는 표적 초기화"); // 외부 변화로 만든 처치 시간 제외
        }
        else if (firearm.State == null || firearm.State.Rounds + firearm.State.Reserve != initialTotalAmmo - metrics.Record.shots) // 보급과 탄약 수정 확인
        {
            Abort("시험 도중 보급 또는 탄약 변경"); // 탄약 효율이 오염된 결과 제외
        }
        else if (metrics.Record.samples.Count >= 3000) // 기록 메모리 한도 확인
        {
            Abort("발사 기록 한도 도달"); // 무제한 메모리 증가 방지
        }
        else if (metrics.Record.firstShotAt < 0 && now - armedAt > 45) // 발사 없는 준비 시간 확인
        {
            metrics.Finish("NoShot", "45초 동안 발사 없음", now); // 무발사와 처치 실패 구분
        }
        else if (metrics.Record.firstShotAt >= 0 && now - metrics.Record.firstShotAt >= maximumSeconds) // 실제 사격 제한 확인
        {
            metrics.Finish("Timeout", "제한 시간 내 미제압", now); // 미제압을 성공 TTK에 넣지 않음
        }
    }

    public void Abort(string reason) // 사용자 중단과 조건 오류 공통 처리
    {
        if (IsActive) // 실제 진행 여부 확인
        {
            metrics.Finish("Invalid", reason, Time.timeAsDouble); // 중단도 이유와 함께 기록
        }
    }

    private void FinalizeRecord() // 시험 종료 결과와 저장 상태 안내
    {
        finalized = true; // 실패하더라도 중복 자동 저장 방지
        firearm?.Interrupt(); // 완료 이후 이어지는 자동 연사 차단
        history.Insert(0, metrics.Record); // 화면 비교 결과 추가
        if (history.Count > 250) // 메모리 내 결과 한도
        {
            history.RemoveAt(history.Count - 1); // 오래된 메모리 기록만 제외
        }
        try // 파일 쓰기 실패와 게임 동작 분리
        {
            LastSavedPath = BalanceReportStore.SaveTrial(metrics.Record); // 상세 JSON과 요약 CSV 저장
            Message = metrics.Record.status + " / " + metrics.Record.reason + " · 기록 저장 완료"; // 실제 저장 완료 안내
        }
        catch (Exception error) // 경로와 권한 오류 보고
        {
            Message = "결과는 메모리에 유지 · 저장 실패: " + error.Message; // 성공으로 오해하지 않도록 표시
            Debug.LogException(error); // 원인 로그 출력
        }
    }

    private void CleanupRig() // 복사본만 제거하는 원본 복구
    {
        rig.Dispose(); // 표적 활성 상태 복원
        cleanupAt = -1; // 지연 복구 예약 해제
    }

    private void OnDisable() // 씬 종료와 컴포넌트 비활성화 정리
    {
        BalanceTelemetry.ShotResolved -= OnShot; // 구독 잔류 방지
        Abort("씬 종료 또는 계측 비활성화"); // 진행 중 결과 분리
        if (metrics != null && !finalized) // 아직 저장하지 않은 결과 확인
        {
            FinalizeRecord(); // 가능한 범위에서 중단 기록 저장
        }
        CleanupRig(); // 비활성화된 원본 복구
    }
}
