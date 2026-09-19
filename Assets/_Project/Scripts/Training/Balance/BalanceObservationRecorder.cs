using System; // 실제 관측 시각 저장
using System.Collections.Generic; // 경비 반응 기록 목록
using UnityEngine; // 기존 구역의 실행 센서 관찰

[Serializable] // 실제 교전 관측 파일
public sealed class BalanceObservationRecord // 통제 사격 결과와 분리된 실전 로그
{
    public int schema = 1; // 결과 형식 버전
    public string id; // 관측 고유 번호
    public string createdUtc; // 관측 시작 표준 시각
    public string zone; // 실제 실행 구역
    public string kind = "LiveObservation"; // 통제 실험과 구분하는 종류
    public string reason; // 기록 종료 이유
    public double startedAt; // 게임 시계 시작점
    public int droppedEvents; // 기록 상한으로 제외한 이벤트
    public List<string> weaponSnapshots = new List<string>(); // 사용한 총기 설정 자료
    public List<BalanceObservationEvent> events = new List<BalanceObservationEvent>(); // 실제 발생 순서 기록
}

[Serializable] // 경비 사건의 순수 값 자료
public sealed class BalanceObservationEvent // 시야와 청각을 섞지 않는 관측 항목
{
    public string kind; // 발사와 청취와 탐지 변화 종류
    public double seconds; // 관측 시작 이후 시간
    public string actor; // 센서 또는 적 식별 이름
    public int instanceId; // 같은 이름의 실행 적 구분
    public string weapon; // 해당 시점의 실제 장착 총기
    public string noiseType; // 총성과 발소리 등 구분
    public string state; // 센서의 실제 탐지 상태
    public bool targetVisible; // 직접 시야 판정 결과
    public float progress; // 실제 탐지 진행도
    public float distance; // 사건 시점의 발생 위치 거리
    public float hearingRadius; // 실제 센서 청각 한계
    public float emittedRadius; // 실제 소음 전달 반경
    public Vector3 position; // 발사 또는 청취 위치
    public Vector3 sensorPosition; // 센서 사건 위치
    public int pellets; // 발사 또는 대상 적중 펠릿
    public float healthDamage; // 해당 대상의 실제 체력 감소
    public bool killed; // 이번 발사로 적 제압
}

[DefaultExecutionOrder(1600)] // 기존 AI와 사격 처리 후 종료 확인
[DisallowMultipleComponent] // 같은 구역 기록 중복 방지
public sealed class BalanceObservationRecorder : MonoBehaviour // 실제 시험 구역을 읽기만 하는 기록기
{
    [SerializeField] private TrainingCenterRoot center; // 기존 시설 참조
    private TrainingCenterZone activeZone; // 현재 실행 시험 구역
    private BalanceObservationRecord record; // 이번 실전 관측
    private readonly Dictionary<int, bool> visibility = new Dictionary<int, bool>(); // 직접 시야 변화 추적
    private readonly HashSet<string> snapshots = new HashSet<string>(); // 같은 총기 설정 중복 저장 방지
    private string messageValue = "잠입·실전 시험 중 경비 반응을 자동 기록"; // 실제 기록 상태 안내
    public string Message // 실제 기록 상태 안내
    {
        get => messageValue; // 현재 값 조회
        private set => messageValue = value; // 내부 상태 갱신
    }

    public void Configure(TrainingCenterRoot owner) // 에디터 참조 연결
    {
        center = owner; // 기존 구역 관리자 연결
    }

    private void OnEnable() // 계측 이벤트 구독
    {
        BalanceTelemetry.ShotResolved -= OnShot; // 중복 발사 구독 제거
        BalanceTelemetry.ShotResolved += OnShot; // 실제 발사 관찰
        BalanceTelemetry.NoiseHeard -= OnHeard; // 중복 청각 구독 제거
        BalanceTelemetry.NoiseHeard += OnHeard; // 실제 청취 관찰
        BalanceTelemetry.DetectionChanged -= OnDetection; // 중복 상태 구독 제거
        BalanceTelemetry.DetectionChanged += OnDetection; // 실제 상태 전환 관찰
    }

    private bool Ensure(TrainingCenterZone zone) // 현재 실제 가동 구역의 기록 확보
    {
        if (center == null || zone == null || !zone.IsRunning || !zone.transform.IsChildOf(center.transform)) // 이 시설의 실행 구역 확인
        {
            return false; // 일반 센서와 원본 센서 제외
        }
        if (record != null && activeZone != zone) // 다른 구역으로 시험 변경
        {
            Save("다른 구역 시작"); // 이전 관측을 별도 종료
        }
        if (record == null) // 새 관측 시작
        {
            activeZone = zone; // 실제 실행 구역 저장
            record = new BalanceObservationRecord(); // 새로운 관측 기록
            record.id = BalanceReportStore.NewId(); // 고유 이름 확보
            record.createdUtc = DateTime.UtcNow.ToString("o"); // 실제 시작 시각
            record.zone = zone.ZoneName; // 현재 구역 이름
            record.startedAt = Time.timeAsDouble; // 게임 경과 기준
            visibility.Clear(); // 이전 시야 상태 제거
            snapshots.Clear(); // 이전 총기 기록 제거
            Message = zone.ZoneName + " 실제 경비 반응 기록 중"; // 가동 안내
        }
        return true; // 기록 가능 상태
    }

    private void Add(BalanceObservationEvent value) // 무제한 기록 크기 방지
    {
        if (record == null) // 관측 존재 확인
        {
            return; // 구역 밖 사건 제외
        }
        value.seconds = Math.Max(0, Time.timeAsDouble - record.startedAt); // 실제 관측 시간
        if (record.events.Count < 6000) // 파일과 메모리 한도
        {
            record.events.Add(value); // 실제 사건 추가
        }
        else // 기록 한도 도달
        {
            record.droppedEvents++; // 누락 여부를 결과에 명시
        }
    }

    private BalanceObservationEvent SensorEvent(DetectionSensor sensor, string kind) // 공통 센서 스냅샷
    {
        BalanceObservationEvent value = new BalanceObservationEvent(); // 순수 값 생성
        value.kind = kind; // 실제 사건 종류
        value.actor = sensor.name; // 관측 대상 이름
        value.instanceId = sensor.GetInstanceID(); // 같은 종류 적 구분
        value.state = sensor.State.ToString(); // 직접 읽은 상태
        value.targetVisible = sensor.TargetVisible; // 직접 시야 확보 여부
        value.progress = sensor.DetectionProgress; // 실제 의심량
        value.hearingRadius = sensor.HearingRadius; // 실제 청각 반경
        value.sensorPosition = sensor.transform.position; // 해당 센서 위치
        value.position = sensor.LastHeardPosition; // 마지막 실제 청취 위치
        return value; // 저장 자료 반환
    }

    private void OnHeard(DetectionSensor sensor, NoiseEvent noise) // 실제 청취 성공 이벤트
    {
        TrainingCenterZone zone = sensor != null ? sensor.GetComponentInParent<TrainingCenterZone>() : null; // 실행 센서의 소속
        if (!Ensure(zone) || !sensor.isActiveAndEnabled) // 실제 가동 센서 확인
        {
            return; // 원본과 비활성 센서 제외
        }
        BalanceObservationEvent value = SensorEvent(sensor, "Heard"); // 청취 사실만 기록
        value.noiseType = noise.Type.ToString(); // 총성과 발소리 구분
        value.position = noise.Position; // 소리 발생 당시 위치
        value.distance = Vector3.Distance(sensor.transform.position, noise.Position); // 당시 청취 거리
        value.emittedRadius = noise.Radius; // 실제 전달 반경
        PlayerFirearmController gun = noise.Source != null ? noise.Source.GetComponent<PlayerFirearmController>() : null; // 소음 발생자의 무기
        value.weapon = noise.Type == NoiseType.Gunshot && gun != null && gun.Definition != null ? gun.Definition.Stats.Id : ""; // 발소리에 총기 반응을 붙이지 않음
        Add(value); // 청취 기록 추가
    }

    private void OnDetection(DetectionSensor sensor) // 실제 상태 변경 관측
    {
        TrainingCenterZone zone = sensor != null ? sensor.GetComponentInParent<TrainingCenterZone>() : null; // 센서 시험 구역
        if (Ensure(zone) && sensor.isActiveAndEnabled) // 실제 가동 센서 확인
        {
            Add(SensorEvent(sensor, "DetectionState")); // 전역 경보로 추정하지 않고 센서 상태만 기록
        }
    }

    private void OnShot(BalanceShotReport shot) // 실전 시험 중 총기 사용 기록
    {
        if (center == null || center.Player == null || shot.Shooter != center.Player.transform) // 사용자 사격 확인
        {
            return; // 다른 시설과 주체 제외
        }
        TrainingCenterZone zone = null; // 현재 실제 시험 구역
        foreach (TrainingCenterZone candidate in center.Zones) // 연결된 구역 순회
        {
            if (candidate != null && candidate.IsRunning) // 가동 중 구역 확인
            {
                zone = candidate; // 실제 가동 구역 선택
                break; // 하나의 시험만 기록
            }
        }
        if (!Ensure(zone)) // 일반 사격장과 통제 시험 제외
        {
            return; // 통제 결과에 실전 기록을 섞지 않음
        }
        string snapshot = BalanceReportStore.Snapshot(shot.Definition, shot.Suppressed); // 실제 사용한 총기 설정
        if (snapshots.Add(snapshot)) // 처음 사용한 설정 확인
        {
            record.weaponSnapshots.Add(snapshot); // 이 관측의 수치 근거 보존
        }
        BalanceObservationEvent fired = new BalanceObservationEvent(); // 발사 한 번 기록
        fired.kind = "Shot"; // 실제 탄약 소모 사건
        fired.weapon = shot.Definition.Stats.Id; // 실제 무기 식별
        fired.position = shot.Muzzle; // 실제 총구 위치
        fired.pellets = shot.Definition.PelletCount; // 산탄과 단발 구분
        PlayerFirearmController firingGun = center.Player.GetComponent<PlayerFirearmController>(); // 실제 발사 소음 설정
        fired.emittedRadius = firingGun != null ? firingGun.EffectiveNoiseRadius : 0f; // 게임에 전달할 실제 소음 반경
        Add(fired); // 발사 기록 추가
        foreach (DetectionSensor sensor in zone.RuntimeRoot.GetComponentsInChildren<DetectionSensor>()) // 실제 가동 센서의 사격 직전 조건
        {
            if (!sensor.isActiveAndEnabled) // 정지한 센서 제외
            {
                continue; // 비활성 센서를 청취 실패로 세지 않음
            }
            BalanceObservationEvent baseline = SensorEvent(sensor, "SensorAtShot"); // 청취 성공을 추정하지 않는 기준 자료
            baseline.weapon = fired.weapon; // 해당 총기 식별
            baseline.distance = Vector3.Distance(sensor.transform.position, EquipmentTargeting.BodyCenter(center.Player.transform)); // 실제 소음 원점까지 거리
            baseline.emittedRadius = fired.emittedRadius; // 해당 발사의 소음 한계
            Add(baseline); // 청취 콜백과 대조할 조건 보존
        }
        foreach (BalanceImpactReport impact in shot.Impacts) // 대상별 결과 순회
        {
            if (!(impact.Target is EnemyActor)) // 실제 적 피해만 선택
            {
                continue; // 다른 연습 표적 제외
            }
            BalanceObservationEvent hit = new BalanceObservationEvent(); // 실제 적 피격 사건
            hit.kind = "EnemyDamage"; // 피해와 청각 구분
            hit.actor = impact.Target.name; // 적 이름
            hit.instanceId = impact.Target.GetInstanceID(); // 실제 적 인스턴스
            hit.weapon = fired.weapon; // 해당 사격 무기
            hit.pellets = impact.Pellets; // 해당 적의 적중 펠릿
            hit.healthDamage = impact.AppliedDamage; // 실제 체력 감소
            hit.killed = impact.Killed; // 실제 사망 확인
            Add(hit); // 피해 결과 추가
        }
    }

    private void LateUpdate() // 실제 시험의 시작과 종료 관찰
    {
        if (center == null || center.Zones == null) // 구성 여부 확인
        {
            return; // 미설정 상태 생략
        }
        foreach (TrainingCenterZone zone in center.Zones) // 이 센터의 실제 가동 확인
        {
            if (zone == null || !zone.IsRunning || !Ensure(zone)) // 실행되지 않은 구역 제외
            {
                continue; // 다음 구역 확인
            }
            foreach (DetectionSensor sensor in zone.RuntimeRoot.GetComponentsInChildren<DetectionSensor>()) // 실행 복사본의 센서만 확인
            {
                bool visible = sensor.TargetVisible; // 센서의 실제 시야 판정
                if (!visibility.TryGetValue(sensor.GetInstanceID(), out bool old) || old != visible) // 시야 확보와 소실 변화 확인
                {
                    visibility[sensor.GetInstanceID()] = visible; // 새 직접 시야 상태 저장
                    Add(SensorEvent(sensor, "VisionSample")); // 프레임 관측임을 구분해 기록
                }
            }
        }
        if (record != null && (activeZone == null || !activeZone.IsRunning)) // 기존 시험 종료 확인
        {
            Save("구역 시험 종료"); // 관측 파일 확정
        }
    }

    private void Save(string reason) // 통제 실험과 별도 파일 작성
    {
        if (record == null) // 저장할 기록 확인
        {
            return; // 중복 저장 방지
        }
        record.reason = reason; // 실제 종료 이유 저장
        try // 결과 경로 오류 분리
        {
            string path = BalanceReportStore.SaveObservation(record); // 실제 발생한 사건만 저장
            Message = "경비 반응 저장: " + path; // 저장 성공 위치 표시
        }
        catch (Exception error) // 저장 실패 보고
        {
            Message = "경비 반응 저장 실패: " + error.Message; // 성공으로 표시하지 않음
            Debug.LogException(error); // 구체적 원인 출력
        }
        record = null; // 종료 기록 참조 해제
        activeZone = null; // 가동 구역 초기화
    }

    private void OnDisable() // 구독과 남은 관측 종료
    {
        BalanceTelemetry.ShotResolved -= OnShot; // 발사 구독 제거
        BalanceTelemetry.NoiseHeard -= OnHeard; // 청각 구독 제거
        BalanceTelemetry.DetectionChanged -= OnDetection; // 상태 구독 제거
        Save("관측 종료 또는 씬 닫힘"); // 마지막 관측 저장
    }
}
