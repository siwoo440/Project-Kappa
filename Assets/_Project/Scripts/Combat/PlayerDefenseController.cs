using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[DisallowMultipleComponent] // 중복 부착 방지
[RequireComponent(typeof(PlayerInput))] // 플레이어 입력 필수 지정
[RequireComponent(typeof(PlayerHealth))] // 플레이어 체력 필수 지정
public sealed class PlayerDefenseController : MonoBehaviour // 플레이어 방어 받아치기 관리자
{
    [Header("Defense")] // 방어 설정 구분
    [SerializeField] private float parryWindow = 0.22f; // 받아치기 판정 시간
    [SerializeField] private float guardedHealthMultiplier = 0.35f; // 방어 체력 피해 비율
    [SerializeField] private float guardedPostureMultiplier = 0.75f; // 방어 자세 피해 비율
    [SerializeField] private float parryPostureDamage = 42f; // 받아치기 적 자세 피해

    [Header("Feedback")] // 피드백 설정 구분
    [SerializeField] private float parryMessageDuration = 0.55f; // 받아치기 메시지 시간

    private PlayerEquipmentManager equipment; // 장비 행동 상태
    private PlayerInput playerInput; // 플레이어 입력 참조
    private InputAction defenseAction; // 방어 입력 액션
    private PlayerHealth health; // 플레이어 체력 참조
    private PlayerCombatController combat; // 플레이어 공격 참조
    private PlayerDirectionIndicator directionIndicator; // 방향 표시 참조
    private float defenseStartTime; // 방어 시작 시간
    private float parryMessageTimer; // 받아치기 메시지 남은 시간
    private bool isDefending; // 방어 상태
    private bool externalLock; // 외부 잠금 상태

    public bool IsDefending => isDefending; // 방어 상태 읽기
    public bool IsParryWindow => isDefending && Time.time - defenseStartTime <= parryWindow; // 받아치기 창 읽기

    private void Awake() // 초기 참조 설정
    {
        playerInput = GetComponent<PlayerInput>(); // 플레이어 입력 조회
        health = GetComponent<PlayerHealth>(); // 플레이어 체력 조회
        combat = GetComponent<PlayerCombatController>(); // 플레이어 공격 조회
        directionIndicator = GetComponent<PlayerDirectionIndicator>(); // 방향 표시 조회
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 관리자 연결
        ResolveDefenseAction(); // 방어 액션 연결
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveDefenseAction(); // 방어 액션 재연결
    }

    private void Update() // 매 프레임 방어 처리
    {
        if (parryMessageTimer > 0f) // 받아치기 메시지 확인
        {
            parryMessageTimer -= Time.deltaTime; // 메시지 시간 감소
        }

        if (externalLock || health == null || health.IsDead || health.IsPostureBroken || (combat != null && combat.IsLocked) || (equipment != null && equipment.IsBusy)) // 방어 불가 상태 확인
        {
            EndDefense(); // 방어 종료
            return; // 처리 중단
        }

        bool pressedThisFrame = WasDefensePressedThisFrame(); // 방어 시작 입력 확인
        bool held = IsDefenseHeld(); // 방어 유지 입력 확인

        if (pressedThisFrame && (combat == null || !combat.IsAttacking)) // 방어 시작 조건 확인
        {
            BeginDefense(); // 방어 시작
        }

        if (isDefending && !held) // 방어 해제 입력 확인
        {
            EndDefense(); // 방어 종료
        }
    }

    public bool ResolveIncomingAttack(EnemyActor attacker, float healthDamage, float postureDamage) // 적 공격 방어 판정
    {
        if (health == null || health.IsDead) // 플레이어 상태 확인
        {
            return false; // 처리 실패 반환
        }

        if (IsParryWindow) // 받아치기 창 확인
        {
            parryMessageTimer = parryMessageDuration; // 받아치기 메시지 활성화

            if (attacker != null) // 공격자 확인
            {
                attacker.ApplyPostureDamage(parryPostureDamage, gameObject); // 적 자세 큰 피해 적용
            }

            Debug.Log("PARRY 성공"); // 받아치기 성공 로그 출력
            return true; // 공격 완전 방어 반환
        }

        if (isDefending) // 일반 방어 확인
        {
            health.TakeDamage(healthDamage * guardedHealthMultiplier, postureDamage * guardedPostureMultiplier, attacker != null ? attacker.gameObject : null); // 감소 피해 적용
            Debug.Log("GUARD 성공"); // 방어 성공 로그 출력
            return true; // 공격 처리 반환
        }

        health.TakeDamage(healthDamage, postureDamage, attacker != null ? attacker.gameObject : null); // 일반 피해 적용
        return false; // 일반 피격 반환
    }

    public void SetExternalLock(bool value) // 외부 방어 잠금 설정
    {
        externalLock = value; // 외부 잠금 상태 저장

        if (externalLock) // 잠금 활성 확인
        {
            EndDefense(); // 방어 즉시 종료
        }
    }

    private void OnDisable() // 비활성 방어 정리
    {
        EndDefense(); // 방향 표시와 방어 상태 복구
    }

    private void BeginDefense() // 방어 시작
    {
        isDefending = true; // 방어 상태 저장
        defenseStartTime = Time.time; // 방어 시작 시간 저장

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(true); // 방향 표시 숨김
        }

        Debug.Log("방어 시작"); // 방어 로그 출력
    }

    private void EndDefense() // 방어 종료
    {
        if (!isDefending) // 기존 방어 상태 확인
        {
            return; // 중복 종료 방지
        }

        isDefending = false; // 방어 상태 해제

        if (directionIndicator != null && (combat == null || !combat.IsAttacking)) // 방향 표시 복구 조건 확인
        {
            directionIndicator.SetSuppressed(false); // 방향 표시 복구
        }
    }

    private void ResolveDefenseAction() // 방어 입력 액션 연결
    {
        if (playerInput == null || playerInput.actions == null) // 입력 에셋 확인
        {
            defenseAction = null; // 방어 액션 초기화
            return; // 연결 중단
        }

        InputActionMap map = playerInput.actions.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        defenseAction = map != null ? map.FindAction("Defense", false) : null; // 방어 액션 조회
    }

    private bool WasDefensePressedThisFrame() // 방어 시작 입력 확인
    {
        if (defenseAction != null) // 방어 액션 확인
        {
            return defenseAction.WasPressedThisFrame(); // 액션 입력 반환
        }

        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame; // 마우스 대체 입력 반환
    }

    private bool IsDefenseHeld() // 방어 유지 입력 확인
    {
        if (defenseAction != null) // 방어 액션 확인
        {
            return defenseAction.IsPressed(); // 액션 유지 반환
        }

        return Mouse.current != null && Mouse.current.rightButton.isPressed; // 마우스 대체 입력 반환
    }

    private void OnGUI() // 받아치기 피드백 표시
    {
        if (parryMessageTimer <= 0f) // 메시지 활성 확인
        {
            return; // UI 표시 중단
        }

        GUIStyle style = new GUIStyle(GUI.skin.box); // UI 스타일 생성
        style.alignment = TextAnchor.MiddleCenter; // 중앙 정렬 적용
        style.fontSize = 28; // 글자 크기 적용
        style.fontStyle = FontStyle.Bold; // 굵은 글자 적용
        style.normal.textColor = new Color(0.25f, 0.95f, 1f); // 청록 글자 적용
        Rect rect = new Rect((Screen.width - 180f) * 0.5f, Screen.height * 0.36f, 180f, 52f); // UI 위치 계산
        GUI.Box(rect, "PARRY", style); // 받아치기 메시지 표시
    }
}
