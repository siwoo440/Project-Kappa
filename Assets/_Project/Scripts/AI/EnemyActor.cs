using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class EnemyActor : MonoBehaviour // 적 공통 생명 관리자
{
    [Header("Stats")] // 능력치 설정 구분
    [SerializeField] private float maxHealth = 100f; // 최대 체력
    [SerializeField] private float maxPosture = 100f; // 최대 자세
    [SerializeField] private bool canBeAssassinated = true; // 암살 가능 여부

    [Header("Posture Break")] // 자세 붕괴 설정 구분
    [SerializeField] private float postureBreakDuration = 2f; // 자세 붕괴 지속 시간
    [SerializeField] private float postureRecoveryRatio = 0.55f; // 붕괴 후 자세 회복 비율

    [Header("Death")] // 사망 설정 구분
    [SerializeField] private bool tiltOnDeath = true; // 사망 기울기 사용 여부
    [SerializeField] private float deathTiltAngle = 72f; // 사망 기울기 각도

    private float currentHealth; // 현재 체력
    private float currentPosture; // 현재 자세
    private float postureBreakTimer; // 자세 붕괴 남은 시간
    private bool isDead; // 사망 상태
    private bool isPostureBroken; // 자세 붕괴 상태

    public float CurrentHealth => currentHealth; // 현재 체력 읽기
    public float MaxHealth => maxHealth; // 최대 체력 읽기
    public float CurrentPosture => currentPosture; // 현재 자세 읽기
    public float MaxPosture => maxPosture; // 최대 자세 읽기
    public bool IsDead => isDead; // 사망 상태 읽기
    public bool IsPostureBroken => isPostureBroken; // 자세 붕괴 상태 읽기
    public bool CanBeAssassinated => canBeAssassinated && !isDead; // 암살 가능 상태 읽기
    public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f; // 체력 비율 읽기
    public float PostureNormalized => maxPosture > 0f ? currentPosture / maxPosture : 0f; // 자세 비율 읽기

    private void Awake() // 초기 상태 설정
    {
        ResetVitals(); // 체력과 자세 초기화
    }

    private void Update() // 매 프레임 상태 갱신
    {
        if (!isPostureBroken || isDead) // 자세 붕괴 상태 확인
        {
            return; // 갱신 중단
        }

        postureBreakTimer -= Time.deltaTime; // 자세 붕괴 시간 감소

        if (postureBreakTimer > 0f) // 붕괴 시간 확인
        {
            return; // 회복 처리 대기
        }

        isPostureBroken = false; // 자세 붕괴 해제
        currentPosture = Mathf.Max(1f, maxPosture * postureRecoveryRatio); // 자세 일부 회복
        Debug.Log($"{name} 자세 회복"); // 자세 회복 로그
    }

    public void Configure(float health, float posture, bool assassinationAllowed) // 외부 능력치 설정
    {
        maxHealth = Mathf.Max(1f, health); // 최대 체력 보정
        maxPosture = Mathf.Max(1f, posture); // 최대 자세 보정
        canBeAssassinated = assassinationAllowed; // 암살 가능 여부 저장
        ResetVitals(); // 현재 수치 초기화
    }

    public void TakeDamage(float healthDamage, float postureDamage, GameObject instigator) // 피해 적용
    {
        if (isDead) // 사망 상태 확인
        {
            return; // 피해 처리 중단
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, healthDamage)); // 체력 피해 적용

        if (!isPostureBroken) // 자세 붕괴 여부 확인
        {
            currentPosture = Mathf.Max(0f, currentPosture - Mathf.Max(0f, postureDamage)); // 자세 피해 적용
        }

        Debug.Log($"{name} 피해 {healthDamage:0} / HP {currentHealth:0}/{maxHealth:0} / 자세 {currentPosture:0}/{maxPosture:0}"); // 피해 로그 출력

        if (currentHealth <= 0f) // 체력 소진 확인
        {
            Die(instigator, false); // 일반 사망 처리
            return; // 추가 처리 중단
        }

        if (!isPostureBroken && currentPosture <= 0f) // 자세 소진 확인
        {
            BreakPosture(); // 자세 붕괴 처리
        }
    }

    public void ApplyPostureDamage(float postureDamage, GameObject instigator) // 자세 전용 피해 적용
    {
        if (isDead || isPostureBroken) // 적용 가능 상태 확인
        {
            return; // 자세 피해 처리 중단
        }

        currentPosture = Mathf.Max(0f, currentPosture - Mathf.Max(0f, postureDamage)); // 자세 피해 적용
        Debug.Log($"{name} 자세 피해 {postureDamage:0} / {currentPosture:0}/{maxPosture:0}"); // 자세 피해 로그

        if (currentPosture <= 0f) // 자세 소진 확인
        {
            BreakPosture(); // 자세 붕괴 처리
        }
    }

    public void Assassinate(GameObject instigator) // 암살 처리
    {
        if (!CanBeAssassinated) // 암살 가능 상태 확인
        {
            return; // 암살 처리 중단
        }

        currentHealth = 0f; // 체력 즉시 소진
        Die(instigator, true); // 암살 사망 처리
    }

    private void BreakPosture() // 자세 붕괴 처리
    {
        isPostureBroken = true; // 자세 붕괴 상태 저장
        postureBreakTimer = postureBreakDuration; // 자세 붕괴 시간 저장
        currentPosture = 0f; // 자세 수치 고정
        Debug.Log($"{name} 자세 붕괴"); // 자세 붕괴 로그
    }

    private void ResetVitals() // 생명 수치 초기화
    {
        currentHealth = maxHealth; // 현재 체력 초기화
        currentPosture = maxPosture; // 현재 자세 초기화
        postureBreakTimer = 0f; // 붕괴 시간 초기화
        isDead = false; // 사망 상태 초기화
        isPostureBroken = false; // 자세 붕괴 상태 초기화
    }

    private void Die(GameObject instigator, bool assassinated) // 사망 처리
    {
        if (isDead) // 중복 사망 확인
        {
            return; // 사망 처리 중단
        }

        isDead = true; // 사망 상태 저장
        isPostureBroken = false; // 자세 붕괴 상태 해제
        DisableGameplayComponents(); // 적 기능 비활성화
        Debug.Log(assassinated ? $"{name} 암살 성공" : $"{name} 처치"); // 사망 로그 출력

        if (tiltOnDeath) // 기울기 연출 확인
        {
            transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, deathTiltAngle); // 사망 기울기 적용
        }
    }

    private void DisableGameplayComponents() // 적 기능 비활성화
    {
        DetectionSensor sensor = GetComponent<DetectionSensor>(); // 탐지 센서 조회
        PatrolGuardAI patrol = GetComponent<PatrolGuardAI>(); // 순찰 AI 조회
        VisionSectorVisual vision = GetComponent<VisionSectorVisual>(); // 시야 시각화 조회
        DetectionBillboardUI billboard = GetComponent<DetectionBillboardUI>(); // 탐지 UI 조회
        E02SwordGuardAI swordGuard = GetComponent<E02SwordGuardAI>(); // E-02 AI 조회
        EnemyMeleeCombat meleeCombat = GetComponent<EnemyMeleeCombat>(); // 적 근접 전투 조회
        EnemyPostureBillboardUI postureUI = GetComponent<EnemyPostureBillboardUI>(); // 자세 UI 조회
        EnemyCombatBillboardUI combatUI = GetComponent<EnemyCombatBillboardUI>(); // 전투 통합 UI 조회

        if (sensor != null) // 탐지 센서 존재 확인
        {
            sensor.enabled = false; // 탐지 센서 비활성화
        }

        if (patrol != null) // 순찰 AI 존재 확인
        {
            patrol.enabled = false; // 순찰 AI 비활성화
        }

        if (vision != null) // 시야 시각화 존재 확인
        {
            vision.enabled = false; // 시야 시각화 비활성화
        }

        if (billboard != null) // 탐지 UI 존재 확인
        {
            billboard.enabled = false; // 탐지 UI 비활성화
        }

        if (swordGuard != null) // E-02 AI 존재 확인
        {
            swordGuard.enabled = false; // E-02 AI 비활성화
        }

        if (meleeCombat != null) // 적 전투 존재 확인
        {
            meleeCombat.enabled = false; // 적 전투 비활성화
        }

        if (postureUI != null) // 자세 UI 존재 확인
        {
            postureUI.enabled = false; // 자세 UI 비활성화
        }

        if (combatUI != null) // 통합 UI 존재 확인
        {
            combatUI.enabled = false; // 통합 UI 비활성화
        }

        Canvas[] ownedCanvases = GetComponentsInChildren<Canvas>(true); // 남아 있는 적 소유 UI 확인
        for (int i = 0; i < ownedCanvases.Length; i++) // 적의 자식 캔버스 순회
        {
            if (ownedCanvases[i].GetComponentInParent<EnemyActor>() == this) // 다른 적의 UI와 구분
            {
                ownedCanvases[i].enabled = false; // 갱신 중단 전에 남은 화면도 숨김
            }
        }

        Transform sector = transform.Find("__VisionSector"); // 자신의 감시 영역 모형 조회
        if (sector != null) // 생성된 감시 영역 확인
        {
            sector.gameObject.SetActive(false); // 사망 후 시야 부채꼴 잔류 방지
        }
    }
}
