using System.Collections.Generic; // 총기별 상태 보관 기능
using UnityEngine; // 총기 전투 기본 기능
using UnityEngine.InputSystem; // 기존 입력 액션 기능

[DefaultExecutionOrder(20)] // 이동과 카메라 처리 뒤 총구 조준
[DisallowMultipleComponent] // 총기 관리자 중복 방지
[RequireComponent(typeof(PlayerInput))] // 기존 입력 참조 확보
public sealed class PlayerFirearmController : MonoBehaviour // 장착 총기의 조준 발사 재장전
{
    [SerializeField] private FirearmDefinition[] loadout; // 사용할 총기 정의 목록
    [SerializeField] private Transform firearmSocket; // 총기 장착 위치
    [SerializeField] private LayerMask hitMask = ~0; // 총알 충돌 마스크
    private readonly Dictionary<FirearmDefinition, FirearmRuntimeState> states = new Dictionary<FirearmDefinition, FirearmRuntimeState>(); // 무기 교체 후에도 유지할 탄약
    private readonly Dictionary<FirearmDefinition, FirearmView> views = new Dictionary<FirearmDefinition, FirearmView>(); // 중복 생성 없는 총기 모형
    private readonly Dictionary<FirearmDefinition, FirearmHandlingState> handlingStates = new Dictionary<FirearmDefinition, FirearmHandlingState>(); // 총기별 분산과 소음기 보존
    private FirearmHandlingState currentHandling; // 현재 무기의 사격 감각 상태
    private ThirdPersonCamera aimRig; // 마우스 입력과 독립된 반동 카메라
    private float currentSpread; // 실제 발사와 HUD에 사용할 반각
    private int practiceShots; // 이번 훈련의 실제 발사 수
    private int practiceHits; // 이번 훈련의 표적 적중 수
    private FirearmHandlingProfile Handling => currentDefinition != null ? currentDefinition.Handling : null; // 현재 무기 조정 자료
    public bool IsSuppressed => currentHandling != null && currentHandling.Suppressed && Handling != null && Handling.SupportsSuppressor && currentView != null && currentView.HasSuppressor; // 실제 적용된 소음기 상태
    public float SpreadDegrees => currentSpread; // HUD 분산 반각 조회
    public float ReticleRadiusPixels => aimCamera != null ? FirearmHandlingMath.ReticlePixels(currentSpread, aimCamera.fieldOfView, aimCamera.pixelHeight) : 0f; // 같은 탄도 범위의 조준점 반경
    public float EffectiveNoiseRadius => currentDefinition != null ? currentDefinition.Stats.NoiseRadius * (IsSuppressed ? Handling.SuppressedNoiseRatio : 1f) : 0f; // AI에 전달할 실제 총성 반경
    public int PracticeShots => practiceShots; // 발사 수 조회
    public int PracticeHits => practiceHits; // 표적 적중 수 조회
    public float PracticeAccuracy => practiceShots > 0 ? 100f * practiceHits / practiceShots : 0f; // 표적 명중률 조회

    private PlayerEquipmentManager equipment; // 공통 장비 상태
    private PlayerInput input; // 플레이어 입력 참조
    private PlayerHealth health; // 생존과 자세 상태
    private PlayerMovement movement; // 이동 잠금 상태
    private PlayerAssassination assassination; // 암살 진행 상태
    private PlayerCombatController melee; // 기존 검술 상태
    private Camera aimCamera; // 조준 카메라
    private InputActionMap map; // 활성 입력 맵
    private FirearmDefinition currentDefinition; // 현재 총기 정의
    private FirearmRuntimeState currentState; // 현재 총기의 보존 상태
    private FirearmView currentView; // 현재 총기 표시
    private float originalFov; // 장착 전 카메라 시야각
    private bool ownsFov; // 시야각 복구 책임 상태
    private bool pendingShot; // 카메라 갱신 후 처리할 발사
    private readonly FirearmTriggerState triggerState = new FirearmTriggerState(); // 프레임 독립 발사 예약
    private readonly double[] scheduledShots = new double[4]; // 긴 멈춤 뒤 한 프레임 최대 네 발
    private int pendingShotCount; // 현재 유효 발사 예약 수
    private bool waitForAttackRelease; // 행동 중단 뒤 버튼 해제 대기
    private double equipReadyAt; // 무기별 장착 완료 시각
    private float aimProgress; // 조준 전환 진행도
    public float AimProgress => aimProgress; // 화면과 분산에 공통 적용할 조준 비율
    public bool IsEquipping => IsEquipped && Time.timeAsDouble < equipReadyAt; // 장착 지연 상태
    public bool SupportsSuppressor => Handling != null && Handling.SupportsSuppressor && currentView != null && currentView.HasSuppressor; // 실제 B 전환 지원 여부
    public float MovementSpeedMultiplier => IsEquipped && isActiveAndEnabled ? currentDefinition.MovementMultiplier * Mathf.Lerp(1f, 0.82f, aimProgress) : 1f; // 장착 속도와 문서 조준 배율
    public bool LastHitWasHead // 마지막 적 명중 부위 기록
    {
        get; // 현재 부위 조회
        private set; // 명중 시에만 갱신
    }
    public float LastHealthDamage // 마지막 적 피해 기록
    {
        get; // 현재 피해 조회
        private set; // 실제 피해만 저장
    }
    private float shotPoseUntil; // 발사 자세 유지 시각
    private float hitMarkerUntil; // 명중 표시 종료 시각
    private float noticeUntil; // 빈 탄창 안내 반복 제한

    public bool IsEquipped => currentDefinition != null && currentView != null; // 실제 총기 장착 여부
    public bool IsCycling => IsEquipped && currentState != null && currentState.IsCycling(Time.timeAsDouble); // 펌프와 볼트 준비 상태
    public float CycleProgress => currentState != null ? currentState.CycleProgress(Time.timeAsDouble) : 1f; // 기구 동작 표시 비율
    public int LastPelletHits // 마지막 발사의 적중 펠릿 수
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public float LastPracticeTime // 마지막 표적 제압 시간
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    } = -1f;
    public bool IsReloading => IsEquipped && currentState != null && currentState.IsReloading; // 현재 총기 재장전 상태
    public bool IsAiming // 총기 조준 상태
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public bool HasHitMarker => Time.time < hitMarkerUntil; // 짧은 명중 표시 상태
    public bool SuppressesDirection => IsEquipped && (IsAiming || IsReloading || Time.time < shotPoseUntil); // 발밑 표시 제한 상태
    public int Count => loadout != null ? loadout.Length : 0; // 총기 슬롯 개수
    public int SelectedIndex // 장착 총기 번호
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    public FirearmDefinition Definition => currentDefinition; // 현재 정의 조회
    public FirearmRuntimeState State => currentState; // 읽기용 탄약 상태 조회
    public float ReloadProgress => IsReloading ? currentState.ReloadProgress(Time.time) : 0f; // HUD 재장전 진행도

    private void Awake() // 참조 준비
    {
        ResolveReferences(); // 기존 플레이어 시스템 연결
    }

    public void Configure(FirearmDefinition[] definitions, Transform socket) // 에디터 총기 구성
    {
        loadout = definitions; // 총기 목록 저장
        firearmSocket = socket; // 장착 위치 저장
    }

    public bool Equip(int index) // 장비 관리자의 총기 장착 요청
    {
        if (!Application.isPlaying || loadout == null || index < 0 || index >= loadout.Length || firearmSocket == null) // 유효한 실행 중 슬롯 확인
        {
            return false; // 잘못된 장착 방지
        }

        FirearmDefinition definition = loadout[index]; // 장착할 총기 선택
        if (definition == null || !definition.IsValid || definition.ModelPrefab.GetComponent<FirearmView>() == null) // 데이터와 총구 표시 구조 검사
        {
            return false; // 미완성 총기 제외
        }

        ResolveReferences(); // 늦게 준비된 컴포넌트 연결
        if (!views.TryGetValue(definition, out FirearmView view) || view == null) // 모형 최초 생성 여부 확인
        {
            GameObject instance = Instantiate(definition.ModelPrefab, firearmSocket); // 정의별 모형 한 번만 생성
            instance.name = "Equipped_D10_" + definition.name; // 실행 모형 이름 지정
            view = instance.GetComponent<FirearmView>(); // 모형 총구와 반동 참조
            views[definition] = view; // 모형 재사용 기록
            instance.SetActive(false); // 선택 확정 전 모형 숨김
        }

        if (view.Muzzle == null) // 실제 총구 존재 확인
        {
            return false; // 잘못된 프리팹 장착 중단
        }

        Unequip(); // 이전 총기와 진행 중 재장전 정리
        if (!states.TryGetValue(definition, out currentState)) // 총기 상태 최초 생성 확인
        {
            currentState = new FirearmRuntimeState(definition.Stats.MagazineSize, definition.Stats.ReserveAmmo, definition.Stats.FireInterval); // 최초 지급 탄약 생성
            states.Add(definition, currentState); // 교체 시 초기화하지 않을 상태 보관
        }

        if (!handlingStates.TryGetValue(definition, out currentHandling)) // 처음 선택한 사격 설정 확인
        {
            currentHandling = new FirearmHandlingState(Time.time); // 새 무기 분산과 소음기 상태
            handlingStates.Add(definition, currentHandling); // 교체 시 상태 유지
        }

        currentState.ConfigureMechanism(definition.SingleRoundReload, definition.CycleDuration); // 기존 탄약을 유지한 기구 설정
        currentHandling.Tick(Time.time, definition.Handling); // 비장착 동안의 회복 반영
        currentDefinition = definition; // 선택 총기 저장
        SelectedIndex = index; // 총기 슬롯 번호 저장
        equipReadyAt = Time.timeAsDouble + definition.EquipDuration; // 무기별 사용 시작 시각
        waitForAttackRelease = AttackIsHeld(); // 누른 채 교체한 입력의 자동 발사 방지
        triggerState.Reset(); // 이전 총기의 예약 제거
        currentView = view; // 현재 모형 저장
        currentView.gameObject.SetActive(true); // 선택한 총기만 표시
        currentView.SetSuppressed(IsSuppressed); // 저장한 소음기 상태 복구
        CaptureCamera(); // 카메라 원래 시야각 보존
        return true; // 장착 완료
    }

    public void Unequip() // 검이나 다른 총기로 전환
    {
        Interrupt(); // 탄약을 이동하지 않고 재장전 취소
        if (currentView != null) // 현재 모형 확인
        {
            currentView.gameObject.SetActive(false); // 이전 총기 숨김
        }

        currentDefinition = null; // 장착 정의 해제
        currentState = null; // 보존 목록은 유지하고 현재 참조 해제
        currentView = null; // 모형 참조 해제
        currentHandling = null; // 저장 목록을 유지한 현재 상태 해제
        currentSpread = 0f; // 미장착 분산 표시 초기화
    }

    public void Interrupt() // 사망과 특수행동의 즉시 총기 중단
    {
        pendingShot = false; // 예약된 발사 취소
        pendingShotCount = 0; // 이번 프레임 연사 예약 전부 취소
        triggerState.Reset(); // 다음 프레임에 밀린 연사 방지
        waitForAttackRelease = true; // 초점 복귀를 포함해 버튼 해제 후 새 입력만 허용
        aimProgress = 0f; // 정지한 조준 진행도 복구
        IsAiming = false; // 조준 종료
        shotPoseUntil = 0f; // 발사 자세 종료
        if (currentState != null) // 총기 상태 확인
        {
            currentState.CancelReload(); // 보충 전 탄수 유지
        }

        if (currentView != null) // 모형 참조 확인
        {
            currentView.ShowReload(0f, false); // 탄창 외형 복구
        }

        RestoreCamera(); // 조준 배율과 반동 원복
    }

    private void Update() // 입력과 재장전 상태 처리
    {
        pendingShot = false; // 이전 프레임 예약 제거
        pendingShotCount = 0; // 이전 연사 예약 제거
        if (!IsEquipped) // 총기 장착 여부 확인
        {
            return; // 검 장착 중 총기 입력 금지
        }

        if (!CanOperate() || IsEquipping) // 실행과 행동과 장착 지연 확인
        {
            Interrupt(); // 잠금 중 발사와 재장전 중단
            return; // 입력 처리 중단
        }

        currentHandling?.Tick(Time.time, Handling); // 사격하지 않는 동안 누적 분산 복구
        if (Pressed("ToggleSuppressor") && !IsReloading) // 테스트 소음기 전환 입력 확인
        {
            TryToggleSuppressor(); // 탄수를 건드리지 않는 부착 전환
            return; // 같은 프레임 발사와 장착을 함께 처리하지 않음
        }

        bool held = AttackIsHeld(); // 장착 중인 총기의 방아쇠 유지 상태
        if (IsReloading && held) // 장전 도중 시작한 누르기까지 확인
        {
            waitForAttackRelease = true; // 장전 완료 직후 의도하지 않은 연사 차단
        }
        if (!held) // 중단 이후 손을 뗀 입력 확인
        {
            waitForAttackRelease = false; // 다음 새 사격 허용
        }

        if (currentState.TickReload(Time.time)) // 재장전 완료 처리
        {
            equipment.Notify(currentState.IsReloading ? "탄약 한 발 보충" : "재장전 완료"); // 탄약 이동 완료 안내
        }

        if (IsReloading && currentDefinition.SingleRoundReload && (Pressed("Attack") || Pressed("Reload"))) // 삽입식 장전 중 취소 입력
        {
            currentState.CancelReload(); // 완료한 탄약은 남기고 다음 삽입만 취소
            pendingShotCount = 0; // 취소와 발사가 같은 순간 겹치지 않도록 제한
            triggerState.Reset(); // 연사 예약 정리
            waitForAttackRelease = held; // 취소 후 새 클릭부터 사격
            currentView.ShowReload(0f, false); // 삽입 모형 원복
            equipment.Notify("삽입 중단 - 넣은 탄약 유지"); // 완료된 보충 안내
            return; // 이번 프레임 발사 없음
        }

        if (Pressed("Reload")) // T 재장전 입력 확인
        {
            TryReload(); // 재장전 우선 처리
        }

        InputAction aim = map.FindAction("Defense", false); // 무기별로 분기할 기존 RMB 입력
        IsAiming = !IsReloading && !IsCycling && aim != null && aim.enabled && aim.IsPressed(); // 총 장착 중 RMB는 조준만 처리
        aimProgress = Mathf.MoveTowards(aimProgress, IsAiming ? 1f : 0f, Time.deltaTime / currentDefinition.AimDuration); // 실제 무기 조준 시간 적용
        if (!IsReloading && !IsCycling && !waitForAttackRelease) // 재장전과 중단 입력의 자동 발사 차단
        {
            pendingShotCount = triggerState.Collect(Time.timeAsDouble, Pressed("Attack"), held, currentDefinition.FireMode, currentDefinition.Stats.FireInterval, currentState.NextShotTime, scheduledShots); // 반자동과 자동을 같은 피해 함수로 예약
        }
        else // 재장전 중 예약 정리
        {
            triggerState.Reset(); // 대기 시간에 발사가 쌓이는 문제 차단
        }
        pendingShot = pendingShotCount > 0; // 기존 발사 자세와 연결
        if (aimRig != null) // 카메라 입력 감도 참조 확인
        {
            aimRig.SetLookMultiplier(Handling != null ? Mathf.Lerp(1f, Handling.AimSensitivityRatio, aimProgress) : 1f); // 기본 감도는 보존한 조준 보정
        }
        currentView.ShowReload(ReloadProgress, IsReloading); // 재장전 탄창 모션 적용
    }

    private void LateUpdate() // 카메라 이동 후 실제 발사와 조준
    {
        if (!IsEquipped || !CanOperate() || IsEquipping) // 최종 발사와 장착 상태 재검사
        {
            Interrupt(); // 마지막 시점의 행동 변경도 모든 예약과 조준 정리
            return; // 총구 처리 중단
        }

        CaptureCamera(); // 변경된 카메라 참조 보정
        Vector3 forward = aimCamera != null ? aimCamera.transform.forward : transform.forward; // 실제 카메라 조준 방향
        if (IsAiming || pendingShot || Time.time < shotPoseUntil) // 조준 또는 발사 자세 확인
        {
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up); // 플레이어 수평 방향
            if (flat.sqrMagnitude > 0.0001f) // 수평 방향 확인
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up); // 사격 방향으로 몸 회전
            }
        }

        firearmSocket.rotation = Quaternion.LookRotation(forward, Vector3.up); // 총구를 실제 조준 방향에 정렬
        if (ownsFov && aimCamera != null) // 조준 카메라 설정 확인
        {
            float targetFov = Mathf.Lerp(originalFov, originalFov * currentDefinition.AimFovRatio, aimProgress); // 현재 목표 시야각
            aimCamera.fieldOfView = targetFov; // 부드러운 조준 확대
        }

        UpdateSpread(); // 이동과 공중 상태를 반영한 실제 분산 갱신
        if (pendingShot) // 단발 예약 확인
        {
            pendingShot = false; // 같은 프레임 중복 발사 방지
            Physics.SyncTransforms(); // 이번 프레임 이동한 적 부위와 총구 충돌 위치 반영
            for (int i = 0; i < pendingShotCount; i++) // 프레임 보정된 실제 예약 순서 처리
            {
                if (!FireScheduled(scheduledShots[i])) // 한 발마다 탄약과 행동 상태 검사
                {
                    triggerState.Reset(); // 빈 탄창과 행동 중단의 나머지 예약 폐기
                    break; // 남은 연사 중단
                }
            }
            pendingShotCount = 0; // 처리된 예약 수 초기화
        }
    }

    public bool TryToggleSuppressor() // 소음기 부착과 해제
    {
        if (!IsEquipped || !CanOperate() || IsReloading || Handling == null || !Handling.SupportsSuppressor || !currentView.HasSuppressor || currentHandling == null) // 전환 가능한 완성 총기 확인
        {
            return false; // 사용 불가 상태 유지
        }

        currentHandling.Suppressed = !currentHandling.Suppressed; // 해당 총기 소음기 상태 변경
        currentView.SetSuppressed(IsSuppressed); // 총구 길이와 외형 동기화
        Interrupt(); // 전환 중 조준과 발사 예약 정리
        equipment.BeginUse(0.2f); // 테스트 부착 동작 대기
        equipment.Notify(IsSuppressed ? "소음기 장착 - 가까운 경비는 들을 수 있음" : "소음기 해제"); // 실제 기능 안내
        return true; // 전환 완료
    }

    public void ResetPracticeStatistics() // 보급대의 훈련 기록 초기화
    {
        practiceShots = 0; // 발사 수 초기화
        practiceHits = 0; // 표적 적중 수 초기화
        LastPracticeTime = -1f; // 제압 기록 초기화
        LastPelletHits = 0; // 펠릿 기록 초기화
    }

    private void UpdateSpread() // 현재 사격 상태와 분산 계산
    {
        bool grounded = movement == null || movement.IsGrounded; // 지면 접촉 확인
        bool crouching = movement != null && movement.IsCrouching; // 실제 앉기 상태 확인
        CharacterController body = movement != null ? movement.Controller : null; // 실제 이동을 수행한 충돌체 확인
        float speed = body != null && body.enabled ? Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude : 0f; // 벽에 막힌 이동을 제외한 실제 수평 속도
        float bloom = currentHandling != null ? currentHandling.Bloom : 0f; // 현재 누적 분산
        float hip = FirearmHandlingMath.SpreadAngle(Handling, false, grounded, crouching, speed, bloom); // 비조준 최종 분산
        float aimed = FirearmHandlingMath.SpreadAngle(Handling, true, grounded, crouching, speed, bloom); // 완전 조준 최종 분산
        currentSpread = Mathf.Lerp(hip, aimed, aimProgress); // 공통 공식으로 현재 분산 계산
    }

    private void ApplyShotRecoil() // 성공한 발사만 조준 방향에 반영
    {
        if (aimRig == null || Handling == null) // 카메라와 사격 설정 확인
        {
            return; // 기존 조준 카메라가 없는 경우 직접 회전 덮어쓰기 금지
        }

        float ratio = Mathf.Lerp(1f, Handling.AimRecoilRatio, aimProgress); // 조준 상태 반동 비율
        Vector2 kick = new Vector2(Handling.PitchKick, UnityEngine.Random.Range(-Handling.YawKick, Handling.YawKick)) * ratio; // 무작위 좌우 반동과 수직 들림
        Vector2 limits = new Vector2(Handling.MaximumPitchKick, Handling.MaximumYawKick); // 과도한 흔들림 상한
        aimRig.AddShotRecoil(kick, limits, Handling.RecoilRecovery, Handling.RecoilRecoveryDelay); // 기본 마우스 시점에 보정만 누적
    }

    public bool TryReload() // 현재 총기 재장전 시도
    {
        if (!IsEquipped || !CanOperate() || currentState == null || IsEquipping || IsCycling) // 총기와 장착 상태 검사
        {
            return false; // 잘못된 재장전 차단
        }

        if (!currentState.TryBeginReload(Time.time, currentDefinition.ReloadDuration)) // 빈 공간과 예비탄과 중복 상태 검사
        {
            equipment.Notify(currentState.Reserve <= 0 ? "예비탄 부족 - 보급대에서 F" : "재장전 불필요 또는 진행 중"); // 재장전 실패 안내
            return false; // 탄약 변화 없음
        }

        IsAiming = false; // 재장전 중 조준 종료
        triggerState.Reset(); // 재장전 중 연사 시간 누적 방지
        waitForAttackRelease = AttackIsHeld(); // 장전 후 방아쇠를 다시 누르도록 제한
        pendingShotCount = 0; // 재장전 프레임 예약 제거
        pendingShot = false; // 같은 프레임 발사 차단
        equipment.Notify("재장전 시작 - 무기 교체 시 취소"); // 재장전 안내
        return true; // 재장전 시작 완료
    }

    public bool TryFire() // 외부의 단발 호출 호환
    {
        Physics.SyncTransforms(); // 외부 호출의 최신 충돌 위치 반영
        return FireScheduled(Time.timeAsDouble); // 현재 시각 한 발 처리
    }

    private bool FireScheduled(double shotTime) // 단발과 연사의 공통 명중 처리
    {
        if (!IsEquipped || !CanOperate() || currentState == null || IsReloading || IsEquipping || waitForAttackRelease) // 실제 한 발 직전 잠금 검사
        {
            return false; // 발사 불가 반환
        }

        if (!currentState.TryFireScheduled(shotTime)) // 실제 예정 간격과 잔탄 검사
        {
            if (currentState.Rounds <= 0 && Time.unscaledTime >= noticeUntil) // 빈 탄창 안내 반복 제한
            {
                equipment.Notify("탄창이 비었습니다 - T 재장전"); // 탄약 부족 안내
                noticeUntil = Time.unscaledTime + 0.5f; // 안내 재사용 시간
            }

            return false; // 실패 시 탄약과 피해 없음
        }

        UpdateSpread(); // 실제 사격 상태의 분산 확정
        Vector3 muzzle = currentView.Muzzle.position; // 반동 이전 사격 원점
        TrainingShotResult result = TrainingShotResolver.Resolve(aimCamera, transform, muzzle, currentDefinition, hitMask, currentSpread, IsSuppressed); // 단발과 여덟 펠릿 공통 판정
        bool blocked = result.Blocked; // 기존 총구 막힘 안내 연결
        currentView.ShowShot(IsSuppressed ? Handling.SuppressedAudioRatio : 1f); // 한 발에 한 번만 발사 효과
        practiceShots++; // 펠릿 수가 아닌 사용한 탄약 수
        if (result.HitPractice) // 실제 훈련 표적에 적중한 발사 확인
        {
            practiceHits++; // 여러 표적에 맞아도 한 번만 증가
        }
        LastPelletHits = result.PelletsHit; // 마지막 산탄 적중 수 표시
        LastHitWasHead = result.Head; // 기존 머리 명중 표시 유지
        LastHealthDamage = result.HealthDamage; // 이번 발사의 합산 피해
        if (result.TrialSeconds >= 0f) // 표적 제압 완료 확인
        {
            LastPracticeTime = result.TrialSeconds; // 첫 발사부터 제압까지 기록
        }
        if (result.HitAnything) // 살아 있는 적 또는 훈련 표적 확인
        {
            hitMarkerUntil = Time.time + 0.16f; // 기존 명중 피드백 재사용
        }
        shotPoseUntil = Time.time + 0.18f; // 발사 자세 유지
        NoiseSystem.Emit(EquipmentTargeting.BodyCenter(transform), EffectiveNoiseRadius, NoiseType.Gunshot, gameObject); // 펠릿 수와 무관한 총성 한 번

        currentHandling?.RegisterShot(Time.time, Handling); // 빗나가거나 벽에 맞아도 실제 발사는 분산 누적
        ApplyShotRecoil(); // 탄약 소모 성공 후에만 반동 적용
        UpdateSpread(); // 다음 발사의 분산을 조준점에 반영
        if (blocked) // 몸이나 총구 앞 벽 확인
        {
            equipment.Notify("총구 앞이 막혀 있습니다"); // 벽 명중 안내
        }

        return true; // 발사와 탄약 소모 완료
    }

    public void RefillAll() // 기존 보급대에서 모든 총기 탄약 보충
    {
        foreach (FirearmRuntimeState state in states.Values) // 생성된 총기별 탄약 순회
        {
            state.Refill(); // 무기별 훈련 탄약 보충
        }
    }

    private bool CanOperate() // 총기와 다른 행동의 공통 제한
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) // 일시정지와 창 초점과 커서 상태 확인
        {
            return false; // 메뉴와 외부 창에서 사격 금지
        }

        if (health == null || health.IsDead || health.IsPostureBroken || equipment == null || !equipment.isActiveAndEnabled || equipment.IsBusy) // 생존과 장비 사용 상태 확인
        {
            return false; // 특수행동 중 총기 금지
        }

        if ((assassination != null && assassination.IsAssassinating) || (melee != null && (melee.IsAttacking || melee.IsLocked)) || (movement != null && !movement.MovementEnabled)) // 암살과 검술과 벽 이동 잠금
        {
            return false; // 다른 동작 중 발사 차단
        }

        map = input != null && input.isActiveAndEnabled && input.actions != null ? input.actions.FindActionMap("Player", false) : null; // 현재 플레이어 맵 확인
        return map != null && map.enabled; // 활성 게임 입력만 처리
    }

    private bool AttackIsHeld() // 입력 중단과 장착 중 버튼 유지 확인
    {
        PlayerInput source = input != null ? input : GetComponent<PlayerInput>(); // 늦은 초기화 입력 참조 보정
        InputAction action = source != null && source.isActiveAndEnabled && source.actions != null ? source.actions.FindAction("Player/Attack", false) : null; // 다른 액션 맵과 구분
        return action != null && action.enabled && action.IsPressed(); // 실제 유지 입력만 반환
    }

    private bool Pressed(string actionName) // 기존 액션의 단발 입력 확인
    {
        InputAction action = map != null ? map.FindAction(actionName, false) : null; // 액션 검색
        return action != null && action.enabled && action.WasPressedThisFrame(); // 활성 입력만 수용
    }

    private void ResolveReferences() // 기존 컴포넌트 연결
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 관리자 연결
        input = GetComponent<PlayerInput>(); // 입력 연결
        health = GetComponent<PlayerHealth>(); // 생존 상태 연결
        movement = GetComponent<PlayerMovement>(); // 이동 연결
        assassination = GetComponent<PlayerAssassination>(); // 암살 연결
        melee = GetComponent<PlayerCombatController>(); // 검술 연결
    }

    private void CaptureCamera() // 조준 시야각 소유권 확보
    {
        Camera current = Camera.main; // 현재 메인 카메라
        if (ownsFov && current == aimCamera) // 같은 카메라 설정 확인
        {
            return; // 원래 시야각 반복 덮어쓰기 방지
        }

        RestoreCamera(); // 이전 카메라 시야 복구
        aimCamera = current; // 새 카메라 연결
        aimRig = aimCamera != null ? aimCamera.GetComponent<ThirdPersonCamera>() : null; // 기존 카메라 반동 인터페이스 연결
        if (aimCamera != null) // 카메라 존재 확인
        {
            originalFov = aimCamera.fieldOfView; // 장착 전 시야각 저장
            ownsFov = true; // 시야각 복구 책임 기록
        }
    }

    private void RestoreCamera() // 조준 배율 완전 복구
    {
        if (ownsFov && aimCamera != null) // 변경한 카메라 확인
        {
            aimCamera.fieldOfView = originalFov; // 원래 시야각 복원
        }

        if (aimRig != null) // 자신이 제어하던 카메라 확인
        {
            aimRig.ResetShotRecoil(); // 총기 해제 시 누적 반동 제거
            aimRig.SetLookMultiplier(1f); // 원래 마우스 감도 복구
        }

        ownsFov = false; // 카메라 설정 소유권 해제
    }

    private void OnDisable() // 스크립트 비활성 시 총기 정리
    {
        Unequip(); // 조준과 재장전과 표시 종료
    }

    private void OnDestroy() // 생성한 모형 정리
    {
        RestoreCamera(); // 카메라 값 잔류 방지
        foreach (FirearmView view in views.Values) // 자신이 생성한 모형 순회
        {
            if (view != null) // 남은 모형 확인
            {
                Destroy(view.gameObject); // 다른 장착 모형은 유지하고 총기만 제거
            }
        }
    }
}
