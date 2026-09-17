using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class EnemyActor : MonoBehaviour // 적 공통 생명 관리자
{
    [Header("Stats")] // 능력치 설정 구분
    [SerializeField] private float maxHealth = 100f; // 최대 체력
    [SerializeField] private float maxPosture = 100f; // 최대 자세
    [SerializeField] private bool canBeAssassinated = true; // 암살 가능 여부

    [Header("Death")] // 사망 설정 구분
    [SerializeField] private bool tiltOnDeath = true; // 사망 기울기 사용 여부
    [SerializeField] private float deathTiltAngle = 72f; // 사망 기울기 각도

    private float currentHealth; // 현재 체력
    private float currentPosture; // 현재 자세
    private bool isDead; // 사망 상태

    public float CurrentHealth => currentHealth; // 현재 체력 읽기
    public float MaxHealth => maxHealth; // 최대 체력 읽기
    public float CurrentPosture => currentPosture; // 현재 자세 읽기
    public float MaxPosture => maxPosture; // 최대 자세 읽기
    public bool IsDead => isDead; // 사망 상태 읽기
    public bool CanBeAssassinated => canBeAssassinated && !isDead; // 암살 가능 상태 읽기

    private void Awake() // 초기 상태 설정
    {
        ResetVitals(); // 체력과 자세 초기화
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
        currentPosture = Mathf.Max(0f, currentPosture - Mathf.Max(0f, postureDamage)); // 자세 피해 적용
        Debug.Log($"{name} 피해 {healthDamage:0} / HP {currentHealth:0}/{maxHealth:0} / 자세 {currentPosture:0}/{maxPosture:0}"); // 피해 로그 출력

        if (currentHealth <= 0f) // 체력 소진 확인
        {
            Die(instigator, false); // 일반 사망 처리
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

    private void ResetVitals() // 생명 수치 초기화
    {
        currentHealth = maxHealth; // 현재 체력 초기화
        currentPosture = maxPosture; // 현재 자세 초기화
        isDead = false; // 사망 상태 초기화
    }

    private void Die(GameObject instigator, bool assassinated) // 사망 처리
    {
        if (isDead) // 중복 사망 확인
        {
            return; // 사망 처리 중단
        }

        isDead = true; // 사망 상태 저장
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

        if (sensor != null) // 탐지 센서 존재 확인
        {
            sensor.enabled = false; // 탐지 센서 비활성화
        }

        PatrolGuardAI patrol = GetComponent<PatrolGuardAI>(); // 순찰 AI 조회

        if (patrol != null) // 순찰 AI 존재 확인
        {
            patrol.enabled = false; // 순찰 AI 비활성화
        }

        VisionSectorVisual vision = GetComponent<VisionSectorVisual>(); // 시야 시각화 조회

        if (vision != null) // 시야 시각화 존재 확인
        {
            vision.enabled = false; // 시야 시각화 비활성화
        }

        DetectionBillboardUI billboard = GetComponent<DetectionBillboardUI>(); // 탐지 UI 조회

        if (billboard != null) // 탐지 UI 존재 확인
        {
            billboard.enabled = false; // 탐지 UI 비활성화
        }
    }
}
