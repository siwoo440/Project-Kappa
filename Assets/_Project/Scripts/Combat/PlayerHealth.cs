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

                if (movement != null) // 이동 관리자 확인
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
        if (dead || postureBroken) // 피해 가능 상태 확인
        {
            return; // 피해 처리 중단
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, healthDamage)); // 체력 피해 적용
        currentPosture = Mathf.Max(0f, currentPosture - Mathf.Max(0f, postureDamage)); // 자세 피해 적용
        regenDelayTimer = postureRegenDelay; // 자세 회복 대기 초기화
        Debug.Log($"Player 피해 {healthDamage:0} / HP {currentHealth:0}/{maxHealth:0} / 자세 {currentPosture:0}/{maxPosture:0}"); // 피해 로그 출력

        if (currentHealth <= 0f) // 체력 소진 확인
        {
            dead = true; // 사망 상태 저장

            if (movement != null) // 이동 관리자 확인
            {
                movement.SetMovementEnabled(false); // 이동 비활성화
            }

            Debug.Log("Player 사망"); // 사망 로그 출력
            return; // 추가 처리 중단
        }

        if (currentPosture <= 0f) // 자세 소진 확인
        {
            BreakPosture(); // 자세 붕괴 처리
        }
    }

    private void BreakPosture() // 플레이어 자세 붕괴 처리
    {
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
        GUIStyle style = new GUIStyle(GUI.skin.box); // UI 스타일 생성
        style.alignment = TextAnchor.MiddleLeft; // 좌측 정렬 적용
        style.fontSize = 15; // 글자 크기 적용
        style.normal.textColor = Color.white; // 글자 색상 적용
        string stateText = dead ? "DEAD" : postureBroken ? "POSTURE BREAK" : "READY"; // 상태 문자열 계산
        Rect rect = new Rect(18f, 18f, 270f, 70f); // UI 위치 계산
        GUI.Box(rect, $" HP  {currentHealth:0}/{maxHealth:0}\n POSTURE  {currentPosture:0}/{maxPosture:0}   {stateText}", style); // 전투 수치 표시
    }
}
