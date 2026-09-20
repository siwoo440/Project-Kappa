using ProjectK.Day30; // Day30 통합 HUD 테마와 Tab 임무 창 상태 참조
using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class PlayerHealth : MonoBehaviour // 플레이어 체력 자세 관리자
{
    [Header("Vitals")] // 생명 수치 설정 구분
    [SerializeField] private float maxHealth = 100f; // 최대 체력
    [SerializeField] private float maxPosture = 100f; // 최대 자세

    [Header("Posture Break")] // 자세 붕괴 설정 구분
    [SerializeField] private float postureBreakDuration = 1.1f; // 플레이어 자세 붕괴 시간
    [SerializeField] private float postureRecoveryRatio = 0.65f; // 자세 회복 비율
    [SerializeField] private float postureRegenDelay = 2f; // 자세 자연 회복 대기
    [SerializeField] private float postureRegenPerSecond = 18f; // 자세 자연 회복 속도

    private PlayerMovement movement; // 이동 관리자 참조
    private PlayerDefenseController defense; // 방어 관리자 참조
    private float currentHealth; // 현재 체력
    private float currentPosture; // 현재 자세
    private float breakTimer; // 자세 붕괴 남은 시간
    private float regenDelayTimer; // 자세 회복 대기 시간
    private bool postureBroken; // 자세 붕괴 상태
    private bool dead; // 사망 상태
    private bool resumeMovementAfterBreak; // 자세 붕괴 전에 이동 가능했던 상태

    public float CurrentHealth => currentHealth; // 현재 체력 읽기
    public float MaxHealth => maxHealth; // 최대 체력 읽기
    public float CurrentPosture => currentPosture; // 현재 자세 읽기
    public float MaxPosture => maxPosture; // 최대 자세 읽기
    public bool IsPostureBroken => postureBroken; // 자세 붕괴 상태 읽기
    public bool IsDead => dead; // 사망 상태 읽기

    private void Awake() // 초기 참조 설정
    {
        movement = GetComponent<PlayerMovement>(); // 이동 관리자 조회
        defense = GetComponent<PlayerDefenseController>(); // 방어 관리자 조회
        currentHealth = maxHealth; // 현재 체력 초기화
        currentPosture = maxPosture; // 현재 자세 초기화
    }

    private void Update() // 매 프레임 상태 갱신
    {
        if (dead) // 사망 상태 확인
        {
            return; // 갱신 중단
        }

        if (postureBroken) // 자세 붕괴 상태 확인
        {
            breakTimer -= Time.deltaTime; // 붕괴 시간 감소

            if (breakTimer <= 0f) // 붕괴 종료 확인
            {
                postureBroken = false; // 자세 붕괴 해제
                currentPosture = Mathf.Max(1f, maxPosture * postureRecoveryRatio); // 자세 일부 회복

                if (movement != null && resumeMovementAfterBreak) // 자신이 잠근 이동만 복구
                {
                    movement.SetMovementEnabled(true); // 이동 복구
                }

                if (defense != null) // 방어 관리자 확인
                {
                    defense.SetExternalLock(false); // 방어 잠금 해제
                }
            }

            return; // 자연 회복 처리 생략
        }

        if (regenDelayTimer > 0f) // 회복 대기 확인
        {
            regenDelayTimer -= Time.deltaTime; // 회복 대기 감소
            return; // 자연 회복 처리 대기
        }

        currentPosture = Mathf.MoveTowards(currentPosture, maxPosture, postureRegenPerSecond * Time.deltaTime); // 자세 자연 회복
    }

    public float Heal(float amount) // 회복 주입기 체력 회복
    {
        float restored = EquipmentRules.ClampedHeal(currentHealth, maxHealth, amount, dead); // 실제 회복 가능량 계산
        currentHealth += restored; // 최대 체력 이내 회복 적용
        return restored; // 소모 여부 판단용 실제 회복량
    }

    public void TakeDamage(float healthDamage, float postureDamage, GameObject attacker) // 피해 적용
    {
        if (dead) // 사망 후 중복 피해만 차단
        {
            return; // 피해 처리 중단
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, healthDamage)); // 체력 피해 적용
        if (!postureBroken) // 붕괴 시간 재시작 없이 체력 피해만 허용
        {
            currentPosture = Mathf.Max(0f, currentPosture - Mathf.Max(0f, postureDamage)); // 정상 상태의 자세 피해 적용
        }
        regenDelayTimer = postureRegenDelay; // 자세 회복 대기 초기화
        Debug.Log($"Player 피해 {healthDamage:0} / HP {currentHealth:0}/{maxHealth:0} / 자세 {currentPosture:0}/{maxPosture:0}"); // 피해 로그 출력

        if (currentHealth <= 0f) // 체력 소진 확인
        {
            dead = true; // 사망 상태 저장
            postureBroken = false; // 사망 후 자세 회복 방지
            GetComponent<PlayerFirearmController>()?.Interrupt(); // 재장전과 발사 예약 즉시 중단
            GetComponent<PlayerCombatController>()?.SetExternalLock(true); // 사망 직후 검 공격 중단
            defense?.SetExternalLock(true); // 사망 직후 방어 중단

            if (movement != null) // 이동 관리자 확인
            {
                movement.SetMovementEnabled(false); // 이동 비활성화
            }

            Debug.Log("Player 사망"); // 사망 로그 출력
            return; // 추가 처리 중단
        }

        if (!postureBroken && currentPosture <= 0f) // 연속 피격으로 붕괴 시간을 다시 시작하지 않도록 제한
        {
            BreakPosture(); // 자세 붕괴 처리
        }
    }

    private void BreakPosture() // 플레이어 자세 붕괴 처리
    {
        resumeMovementAfterBreak = movement != null && movement.MovementEnabled; // 기존 이동 가능 여부 보존
        GetComponent<PlayerFirearmController>()?.Interrupt(); // 자세 붕괴 즉시 재장전 취소
        postureBroken = true; // 자세 붕괴 상태 저장
        breakTimer = postureBreakDuration; // 붕괴 시간 저장
        currentPosture = 0f; // 자세 수치 고정

        if (movement != null) // 이동 관리자 확인
        {
            movement.SetMovementEnabled(false); // 이동 비활성화
        }

        if (defense != null) // 방어 관리자 확인
        {
            defense.SetExternalLock(true); // 방어 잠금 적용
        }

        Debug.Log("Player 자세 붕괴"); // 자세 붕괴 로그 출력
    }

    private void OnGUI() // 플레이어 전투 수치 표시
    {
        if (Map30UITheme.HideGameplayHUD) // Tab 전체 임무 창 상태 확인
        {
            return; // 임무 창 위 일반 전투 HUD 숨김
        }

        Rect rect = new Rect(14f, 14f, 180f, 70f); // 좌측 상단 생존 정보 영역
        Map30UITheme.DrawPanel(rect); // 공통 청록 패널 출력

        GUIStyle title = new GUIStyle(GUI.skin.label); // 작은 시스템 제목 스타일
        title.fontSize = 10; // 헤더 글자 크기
        title.fontStyle = FontStyle.Bold; // 헤더 강조
        title.normal.textColor = Map30UITheme.Muted; // 보조 청록색 적용

        GUIStyle value = new GUIStyle(GUI.skin.label); // 실제 수치 스타일
        value.fontSize = 12; // 수치 글자 크기
        value.fontStyle = FontStyle.Bold; // 수치 강조
        value.normal.textColor = Map30UITheme.Text; // 밝은 청백색 적용

        string stateText = dead ? "DEAD" : postureBroken ? "POSTURE BREAK" : "READY"; // 현재 상태 문구 계산
        GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, 82f, 18f), "VITAL STATUS", title); // 패널 헤더
        GUI.Label(new Rect(rect.x + 100f, rect.y + 6f, 68f, 18f), stateText, title); // 현재 상태 표시
        GUI.Label(new Rect(rect.x + 10f, rect.y + 25f, 62f, 18f), "HP " + currentHealth.ToString("0") + "/" + maxHealth.ToString("0"), value); // HP 수치
        Map30UITheme.DrawBar(new Rect(rect.x + 76f, rect.y + 30f, 92f, 7f), maxHealth > 0f ? currentHealth / maxHealth : 0f, Map30UITheme.Cyan); // HP 게이지
        GUI.Label(new Rect(rect.x + 10f, rect.y + 46f, 68f, 18f), "POST " + currentPosture.ToString("0") + "/" + maxPosture.ToString("0"), value); // 자세 수치
        Map30UITheme.DrawBar(new Rect(rect.x + 82f, rect.y + 51f, 86f, 7f), maxPosture > 0f ? currentPosture / maxPosture : 0f, Map30UITheme.Blue); // 자세 게이지
    }
}
