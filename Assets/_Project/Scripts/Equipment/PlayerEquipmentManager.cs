using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 기존 입력 시스템 기능

[DefaultExecutionOrder(-30)] // 공격보다 먼저 장비 입력 처리
[DisallowMultipleComponent] // 중복 관리자 방지
[RequireComponent(typeof(PlayerInput))] // 입력 컴포넌트 확보
public sealed class PlayerEquipmentManager : MonoBehaviour // 근접 무기와 장비 사용 관리자
{
    [SerializeField] private MeleeWeaponDefinition[] weapons; // 해금된 테스트 근접 무기
    [SerializeField] private Transform weaponSocket; // 근접 무기 장착 위치
    [SerializeField] private EquipmentTuning tuning; // 보조장비와 소모품 수치
    [SerializeField, Min(0f)] private float switchDuration = 0.25f; // 무기 교체 대기 시간
    private PlayerFirearmController firearm; // 총기 장착과 재장전 상태
    private PlayerInput input; // 입력 컴포넌트 참조
    private PlayerCombatController combat; // 검 공격 참조
    private PlayerDefenseController defense; // 방어 참조
    private PlayerAssassination assassination; // 암살 참조
    private PlayerHealth health; // 생존 상태 참조
    private PlayerMovement movement; // 이동 상태 참조
    private SupportEquipmentController support; // 보조장비 참조
    private ConsumableController consumables; // 소모품 참조
    private InputActionMap actionMap; // 장비 입력 맵
    private GameObject[] models; // 무기별 표시 모형
    private int selectedIndex; // 현재 근접 무기 번호
    private float busyUntil; // 장비 행동 종료 시각
    private float messageUntil; // 안내 표시 종료 시각
    private string message = string.Empty; // 최근 장비 안내
    private bool ready; // 장비 초기 구성 상태

    public EquipmentTuning Tuning => tuning; // 보조장비 설정 조회
    public MeleeWeaponDefinition CurrentWeapon => weapons != null && selectedIndex >= 0 && selectedIndex < weapons.Length ? weapons[selectedIndex] : null; // 현재 무기 조회
    public int WeaponCount => weapons != null ? weapons.Length : 0; // 무기 개수 조회
    public PlayerFirearmController Firearm => firearm; // 총기 관리자 조회
    public bool IsFirearmEquipped => firearm != null && firearm.IsEquipped; // 현재 장착 종류 조회
    public int TotalSlotCount => WeaponCount + (firearm != null ? firearm.Count : 0); // 근접과 총기의 전체 슬롯
    public int CurrentSlot => IsFirearmEquipped ? WeaponCount + firearm.SelectedIndex : selectedIndex; // 현재 전체 슬롯 번호
    public int SelectedIndex => selectedIndex; // 선택 번호 조회
    public bool IsBusy => Time.time < busyUntil; // 장비 행동 잠금 조회
    public string Message => Time.unscaledTime < messageUntil ? message : string.Empty; // 유효한 안내 조회

    private void Awake() // 런타임 참조 준비
    {
        ResolveReferences(); // 필요한 컴포넌트 연결
    }

    private void Start() // 모든 초기화 이후 장비 준비
    {
        InitializeEquipment(); // 무기 모형과 초기 무기 구성
    }

    private void Update() // 장비 입력 처리
    {
        if (!ready) // 장비 초기화 확인
        {
            InitializeEquipment(); // 참조가 늦게 준비된 경우 재시도
        }

        if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked) // 편집기와 메뉴의 숫자 입력 차단
        {
            return; // 게임 창 밖 장비 사용 방지
        }

        if (!ready || !CanSwitchWeapon()) // 재장전 취소용 교체 입력은 별도 허용
        {
            return; // 장비 입력 중단
        }

        if (actionMap == null || !actionMap.enabled) // 활성 입력 맵 확인
        {
            actionMap = input != null && input.actions != null ? input.actions.FindActionMap("Player", false) : null; // 입력 맵 재연결
            return; // 다음 프레임부터 입력 처리
        }

        for (int i = 0; i < TotalSlotCount; i++) // 근접과 총기 숫자 선택 순회
        {
            if (Pressed("Equip" + (i + 1))) // 숫자 키 선택 확인
            {
                TryEquipSlot(i); // 선택 종류의 무기 장착
                return; // 한 프레임 장비 행동 하나로 제한
            }
        }

        if (Pressed("Next")) // 다음 무기 입력 확인
        {
            TryEquipSlot(EquipmentRules.WrapIndex(CurrentSlot + 1, TotalSlotCount)); // 총기를 포함한 다음 슬롯 장착
            return; // 교체 프레임의 다른 장비 사용 방지
        }
        else if (Pressed("Previous")) // 이전 무기 입력 확인
        {
            TryEquipSlot(EquipmentRules.WrapIndex(CurrentSlot - 1, TotalSlotCount)); // 총기를 포함한 이전 슬롯 장착
            return; // 교체 프레임의 다른 장비 사용 방지
        }

        if (!CanUseEquipment()) // 재장전 중 보조장비와 소모품 제한
        {
            return; // 교체 이외 장비 행동 차단
        }

        if (Pressed("CycleItem")) // 소모품 선택 입력 확인
        {
            consumables.Cycle(); // 다음 소모품 선택
        }
        else if (Pressed("UseSupport")) // 보조장비 입력 확인
        {
            support.TryFire(); // 마비침 발사 시도
        }
        else if (Pressed("UseItem")) // 소모품 사용 입력 확인
        {
            consumables.TryUse(); // 선택 소모품 사용 시도
        }
    }

    public void Configure(MeleeWeaponDefinition[] definitions, Transform socket, EquipmentTuning settings) // 에디터 장비 연결
    {
        weapons = definitions; // 근접 무기 목록 저장
        weaponSocket = socket; // 장착 위치 저장
        tuning = settings; // 장비 수치 저장
    }

    public bool CanUseEquipment() // 보조장비와 소모품 사용 제한
    {
        return CanSwitchWeapon() && (firearm == null || !firearm.IsReloading); // 재장전 도중 중복 장비 사용 차단
    }

    public bool CanSwitchWeapon() // 재장전 취소를 허용하는 장착 제한
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || IsBusy || health == null || health.IsDead || health.IsPostureBroken) // 생존과 장비 잠금 확인
        {
            return false; // 장비 사용 금지
        }

        if (combat != null && (combat.IsAttacking || combat.IsLocked)) // 검술 상태 확인
        {
            return false; // 검술 중 장비 사용 금지
        }

        if (defense != null && defense.IsDefending) // 방어 상태 확인
        {
            return false; // 방어 중 장비 사용 금지
        }

        if (assassination != null && assassination.IsAssassinating) // 암살 상태 확인
        {
            return false; // 암살 중 장비 사용 금지
        }

        return movement == null || movement.MovementEnabled; // 벽 이동과 외부 이동 잠금 확인
    }

    public bool TryEquip(int index) // 근접 무기 교체 시도
    {
        if (!ready || !CanSwitchWeapon() || index < 0 || index >= WeaponCount || weapons[index] == null) // 유효한 교체 조건 확인
        {
            return false; // 무기 교체 실패
        }

        if (index == selectedIndex && !IsFirearmEquipped) // 같은 근접 무기 선택 확인
        {
            return true; // 불필요한 교체 생략
        }

        ApplyWeapon(index); // 선택 모형과 공격 수치 적용
        BeginUse(switchDuration); // 교체 대기 적용
        Notify(CurrentWeapon.DisplayName + " 장착"); // 장착 안내
        return true; // 교체 성공
    }

    public bool TryEquipSlot(int slot) // 전체 장비 슬롯 선택
    {
        return slot < WeaponCount ? TryEquip(slot) : TryEquipFirearm(slot - WeaponCount); // 근접과 총기 장착 분기
    }

    public bool TryEquipFirearm(int index) // 총기 선택과 기존 검 숨김
    {
        if (!ready || !CanSwitchWeapon() || firearm == null) // 교체 가능한 실행 상태 확인
        {
            return false; // 장착 실패 반환
        }

        if (IsFirearmEquipped && firearm.SelectedIndex == index) // 현재 총기 재선택 확인
        {
            return true; // 재장전과 탄약을 초기화하지 않고 유지
        }

        if (!firearm.Equip(index)) // 총기 데이터와 모형 연결 확인
        {
            Notify("총기 설정을 확인하세요 - Day 10 설정 메뉴"); // 미완성 장착 안내
            return false; // 이전 근접 무기 유지
        }

        for (int i = 0; i < models.Length; i++) // 기존 근접 모형 순회
        {
            if (models[i] != null) // 존재하는 모형 확인
            {
                models[i].SetActive(false); // 총과 검의 동시 표시 방지
            }
        }

        BeginUse(firearm.Definition.EquipDuration); // 총기별 장착 전환 대기
        Notify(firearm.Definition.DisplayName + " 장착"); // 총기 장착 안내
        return true; // 장착 완료
    }

    public void BeginUse(float duration) // 장비 행동 잠금 시작
    {
        busyUntil = Mathf.Max(busyUntil, Time.time + Mathf.Max(0f, duration)); // 겹친 잠금의 긴 종료 시각 유지
    }

    public void Notify(string text) // 장비 안내 표시
    {
        message = text; // 안내 문구 저장
        messageUntil = Time.unscaledTime + 2f; // 안내 유지 시간 설정
    }

    private bool Pressed(string name) // 장비 입력 확인
    {
        InputAction action = actionMap != null ? actionMap.FindAction(name, false) : null; // 입력 액션 검색
        return action != null && action.enabled && action.WasPressedThisFrame(); // 활성 입력만 사용
    }

    private void ResolveReferences() // 관련 컴포넌트 조회
    {
        firearm = GetComponent<PlayerFirearmController>(); // 총기 관리자 연결
        input = GetComponent<PlayerInput>(); // 입력 연결
        combat = GetComponent<PlayerCombatController>(); // 공격 연결
        defense = GetComponent<PlayerDefenseController>(); // 방어 연결
        assassination = GetComponent<PlayerAssassination>(); // 암살 연결
        health = GetComponent<PlayerHealth>(); // 생존 상태 연결
        movement = GetComponent<PlayerMovement>(); // 이동 상태 연결
        support = GetComponent<SupportEquipmentController>(); // 보조장비 연결
        consumables = GetComponent<ConsumableController>(); // 소모품 연결
        actionMap = input != null && input.actions != null ? input.actions.FindActionMap("Player", false) : null; // 플레이어 입력 맵 연결
    }

    private void InitializeEquipment() // 저장된 설정으로 무기 재구성
    {
        if (ready || !Application.isPlaying) // 중복 초기화와 에디터 실행 방지
        {
            return; // 초기화 생략
        }

        ResolveReferences(); // 늦게 준비된 컴포넌트 재조회
        if (weaponSocket == null || weapons == null || weapons.Length == 0 || tuning == null || combat == null || support == null || consumables == null) // 설정 완료 여부 확인
        {
            return; // 에디터 설정 완료 대기
        }

        models = new GameObject[weapons.Length]; // 무기 표시 목록 생성
        for (int i = 0; i < weapons.Length; i++) // 무기 모형 구성
        {
            if (weapons[i] == null || weapons[i].ModelPrefab == null) // 무기 모형 확인
            {
                continue; // 비어 있는 슬롯 제외
            }

            models[i] = Instantiate(weapons[i].ModelPrefab, weaponSocket); // 장착 모형 생성
            models[i].name = "Equipped_" + weapons[i].name; // 모형 이름 지정
            models[i].SetActive(false); // 선택 전 모형 숨김
        }

        Transform legacySocket = transform.Find("WeaponSocket_Day7"); // 이전 고정 검 조회
        if (legacySocket != null) // 이전 검 존재 확인
        {
            legacySocket.gameObject.SetActive(false); // 두 검의 동시 표시 방지
        }

        ready = true; // 초기화 완료 기록
        ApplyWeapon(Mathf.Clamp(selectedIndex, 0, weapons.Length - 1)); // 시작 무기 적용
    }

    private void ApplyWeapon(int index) // 무기 외형과 수치 동시 적용
    {
        if (firearm != null) // 총기 장착 상태 확인
        {
            firearm.Unequip(); // 총기를 숨기고 미완료 재장전 취소
        }

        selectedIndex = index; // 현재 무기 번호 저장
        for (int i = 0; i < models.Length; i++) // 무기 모형 순회
        {
            if (models[i] != null) // 모형 존재 확인
            {
                models[i].SetActive(i == selectedIndex); // 선택 무기 하나만 표시
            }
        }

        combat.ConfigureWeapon(weaponSocket); // 공격 회전 소켓 연결
        combat.ApplyDefinition(CurrentWeapon); // 무기별 공격 수치 적용
    }
}
