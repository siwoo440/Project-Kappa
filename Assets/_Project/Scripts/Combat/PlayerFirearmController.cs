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
    private float shotPoseUntil; // 발사 자세 유지 시각
    private float hitMarkerUntil; // 명중 표시 종료 시각
    private float noticeUntil; // 빈 탄창 안내 반복 제한

    public bool IsEquipped => currentDefinition != null && currentView != null; // 실제 총기 장착 여부
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

        currentDefinition = definition; // 선택 총기 저장
        SelectedIndex = index; // 총기 슬롯 번호 저장
        currentView = view; // 현재 모형 저장
        currentView.gameObject.SetActive(true); // 선택한 총기만 표시
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
    }

    public void Interrupt() // 사망과 특수행동의 즉시 총기 중단
    {
        pendingShot = false; // 예약된 발사 취소
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

        RestoreCamera(); // 조준 배율 원복
    }

    private void Update() // 입력과 재장전 상태 처리
    {
        pendingShot = false; // 이전 프레임 예약 제거
        if (!IsEquipped) // 총기 장착 여부 확인
        {
            return; // 검 장착 중 총기 입력 금지
        }

        if (!CanOperate()) // 실행과 행동 상태 확인
        {
            Interrupt(); // 잠금 중 발사와 재장전 중단
            return; // 입력 처리 중단
        }

        if (currentState.TickReload(Time.time)) // 재장전 완료 처리
        {
            equipment.Notify("재장전 완료"); // 탄약 이동 완료 안내
        }

        if (Pressed("Reload")) // T 재장전 입력 확인
        {
            TryReload(); // 재장전 우선 처리
        }

        InputAction aim = map.FindAction("Defense", false); // 무기별로 분기할 기존 RMB 입력
        IsAiming = !IsReloading && aim != null && aim.enabled && aim.IsPressed(); // 총 장착 중 RMB는 조준만 처리
        pendingShot = !IsReloading && Pressed("Attack"); // 단발 입력만 예약
        currentView.ShowReload(ReloadProgress, IsReloading); // 재장전 탄창 모션 적용
    }

    private void LateUpdate() // 카메라 이동 후 실제 발사와 조준
    {
        if (!IsEquipped || !CanOperate()) // 최종 발사 직전 상태 재검사
        {
            pendingShot = false; // 무효 예약 제거
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
            float targetFov = IsAiming ? originalFov * currentDefinition.AimFovRatio : originalFov; // 현재 목표 시야각
            aimCamera.fieldOfView = Mathf.Lerp(aimCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-14f * Time.deltaTime)); // 부드러운 조준 확대
        }

        if (pendingShot) // 단발 예약 확인
        {
            pendingShot = false; // 같은 프레임 중복 발사 방지
            TryFire(); // 총구 기준 발사 처리
        }
    }

    public bool TryReload() // 현재 총기 재장전 시도
    {
        if (!IsEquipped || !CanOperate() || currentState == null) // 총기 사용 상태 검사
        {
            return false; // 잘못된 재장전 차단
        }

        if (!currentState.TryBeginReload(Time.time, currentDefinition.ReloadDuration)) // 빈 공간과 예비탄과 중복 상태 검사
        {
            equipment.Notify(currentState.Reserve <= 0 ? "예비탄 부족 - 보급대에서 F" : "재장전 불필요 또는 진행 중"); // 재장전 실패 안내
            return false; // 탄약 변화 없음
        }

        IsAiming = false; // 재장전 중 조준 종료
        pendingShot = false; // 같은 프레임 발사 차단
        equipment.Notify("재장전 시작 - 무기 교체 시 취소"); // 재장전 안내
        return true; // 재장전 시작 완료
    }

    public bool TryFire() // 피해와 탄약을 한 번만 처리하는 단발 사격
    {
        if (!IsEquipped || !CanOperate() || currentState == null || IsReloading) // 발사 직전 잠금 검사
        {
            return false; // 발사 불가 반환
        }

        if (!currentState.TryFire(Time.time)) // 발사 간격과 잔탄 검사
        {
            if (currentState.Rounds <= 0 && Time.unscaledTime >= noticeUntil) // 빈 탄창 안내 반복 제한
            {
                equipment.Notify("탄창이 비었습니다 - T 재장전"); // 탄약 부족 안내
                noticeUntil = Time.unscaledTime + 0.5f; // 안내 재사용 시간
            }

            return false; // 실패 시 탄약과 피해 없음
        }

        Vector3 muzzle = currentView.Muzzle.position; // 반동 전 실제 총구 위치
        bool hitSomething = FirearmTargeting.CastShot(aimCamera, transform, muzzle, currentDefinition.MaximumRange, hitMask, out RaycastHit hit, out Vector3 end, out bool blocked); // 카메라와 총구의 첫 명중 계산
        currentView.ShowShot(); // 총구 반동과 효과음 실행
        shotPoseUntil = Time.time + 0.18f; // 짧은 발사 자세 유지
        NoiseSystem.Emit(EquipmentTargeting.BodyCenter(transform), currentDefinition.Stats.NoiseRadius, NoiseType.Gunshot, gameObject); // 기존 경비 청각에 총성 전달
        if (currentDefinition.TracerMaterial != null) // 궤적 재질 확인
        {
            Vector3 start = blocked ? EquipmentTargeting.BodyCenter(transform) : muzzle; // 벽 내부에서 나오는 궤적 방지
            EquipmentTransientEffect.ShowLine(start, end, currentDefinition.TracerMaterial, new Color(1f, 0.65f, 0.15f), 0.075f); // 짧은 사격 궤적
        }

        if (hitSomething && !blocked && hit.collider != null) // 총구 가림 없는 첫 명중 확인
        {
            EnemyActor enemy = hit.collider.GetComponentInParent<EnemyActor>(); // 명중한 적의 공통 생명 관리자
            if (enemy != null && !enemy.IsDead) // 살아 있는 적 확인
            {
                float multiplier = currentDefinition.DamageMultiplier(Vector3.Distance(muzzle, hit.point)); // 실제 발사 거리 피해 계산
                enemy.TakeDamage(currentDefinition.Stats.HealthDamage * multiplier, currentDefinition.Stats.PostureDamage * multiplier, gameObject); // 한 번의 명중 피해
                hitMarkerUntil = Time.time + 0.16f; // 명중 표식 표시
            }
        }

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
