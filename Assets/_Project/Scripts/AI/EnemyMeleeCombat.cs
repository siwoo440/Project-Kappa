using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
[RequireComponent(typeof(EnemyActor))] // 적 생명 관리자 필수 지정
public sealed class EnemyMeleeCombat : MonoBehaviour // 적 근접 검술 관리자
{
    [Header("Damage")] // 공격 피해 설정 구분
    [SerializeField] private float basicDamage = 22f; // 기본 공격 체력 피해
    [SerializeField] private float basicPostureDamage = 18f; // 기본 공격 자세 피해
    [SerializeField] private float strongDamage = 36f; // 강 공격 체력 피해
    [SerializeField] private float strongPostureDamage = 30f; // 강 공격 자세 피해

    [Header("Timing")] // 공격 시간 설정 구분
    [SerializeField] private float basicWindup = 0.34f; // 기본 공격 예고 시간
    [SerializeField] private float strongWindup = 0.68f; // 강 공격 예고 시간
    [SerializeField] private float basicRecovery = 0.48f; // 기본 공격 후딜
    [SerializeField] private float strongRecovery = 0.78f; // 강 공격 후딜
    [SerializeField] private float hitRange = 2.45f; // 실제 타격 거리

    private EnemyActor actor; // 적 생명 관리자 참조
    private Transform weaponSocket; // 적 무기 회전 기준
    private PlayerHealth targetHealth; // 공격 대상 체력
    private PlayerDefenseController targetDefense; // 공격 대상 방어
    private Quaternion weaponBaseRotation; // 무기 기본 회전
    private float timer; // 현재 공격 시간
    private float windup; // 현재 예고 시간
    private float recovery; // 현재 후딜 시간
    private float damage; // 현재 체력 피해
    private float postureDamage; // 현재 자세 피해
    private int attackCount; // 공격 횟수
    private bool busy; // 공격 진행 상태
    private bool hitApplied; // 피해 적용 상태
    private bool strongAttack; // 강 공격 상태

    public bool IsBusy => busy; // 공격 진행 상태 읽기
    public bool IsStrongAttack => strongAttack; // 강 공격 상태 읽기
    public bool IsWindupPhase => busy && !hitApplied && timer < windup; // 예고 구간 상태 읽기
    public float AttackGaugeNormalized => windup > 0f ? Mathf.Clamp01(timer / windup) : 0f; // 예고 게이지 비율 읽기

    private void Awake() // 초기 참조 설정
    {
        actor = GetComponent<EnemyActor>(); // 적 생명 관리자 조회
    }

    private void Update() // 매 프레임 공격 처리
    {
        if (!busy) // 공격 진행 여부 확인
        {
            return; // 처리 중단
        }

        if (actor == null || actor.IsDead || actor.IsPostureBroken) // 공격 중단 상태 확인
        {
            CancelAttack(); // 공격 취소
            return; // 처리 중단
        }

        timer += Time.deltaTime; // 공격 시간 누적
        UpdateWeaponSwing(); // 검 휘두르기 갱신

        if (!hitApplied && timer >= windup) // 타격 시점 확인
        {
            hitApplied = true; // 피해 적용 상태 저장
            ApplyHit(); // 플레이어 타격 적용
        }

        if (timer >= windup + recovery) // 공격 종료 시간 확인
        {
            CancelAttack(); // 공격 종료
        }
    }

    public void Configure(Transform socket, float newBasicDamage, float newStrongDamage) // 외부 공격 설정
    {
        weaponSocket = socket; // 무기 소켓 저장
        basicDamage = newBasicDamage; // 기본 공격 피해 저장
        strongDamage = newStrongDamage; // 강 공격 피해 저장
        weaponBaseRotation = weaponSocket != null ? weaponSocket.localRotation : Quaternion.identity; // 무기 기본 회전 저장
    }

    public bool TryStartAttack(PlayerHealth target) // 공격 시작 시도
    {
        if (busy || target == null || actor == null || actor.IsDead || actor.IsPostureBroken) // 공격 가능 조건 확인
        {
            return false; // 공격 실패 반환
        }

        targetHealth = target; // 공격 대상 체력 저장
        targetDefense = target.GetComponent<PlayerDefenseController>(); // 공격 대상 방어 조회
        attackCount++; // 공격 횟수 증가
        strongAttack = attackCount % 3 == 0; // 세 번째 공격 강 공격 선택
        windup = strongAttack ? strongWindup : basicWindup; // 현재 예고 시간 선택
        recovery = strongAttack ? strongRecovery : basicRecovery; // 현재 후딜 시간 선택
        damage = strongAttack ? strongDamage : basicDamage; // 현재 체력 피해 선택
        postureDamage = strongAttack ? strongPostureDamage : basicPostureDamage; // 현재 자세 피해 선택
        timer = 0f; // 공격 시간 초기화
        hitApplied = false; // 피해 적용 상태 초기화
        busy = true; // 공격 진행 상태 저장
        Debug.Log(strongAttack ? $"{name} 강 공격 준비" : $"{name} 기본 공격 준비"); // 공격 예고 로그 출력
        return true; // 공격 시작 성공 반환
    }

    public void CancelAttack() // 공격 취소 또는 종료
    {
        busy = false; // 공격 상태 해제
        timer = 0f; // 공격 시간 초기화
        hitApplied = false; // 피해 적용 상태 초기화
        targetHealth = null; // 공격 대상 초기화
        targetDefense = null; // 방어 대상 초기화

        if (weaponSocket != null) // 무기 소켓 확인
        {
            weaponSocket.localRotation = weaponBaseRotation; // 무기 기본 회전 복구
        }
    }

    private void ApplyHit() // 플레이어 타격 적용
    {
        if (targetHealth == null || targetHealth.IsDead) // 공격 대상 확인
        {
            return; // 타격 처리 중단
        }

        Vector3 direction = targetHealth.transform.position - transform.position; // 대상 방향 계산
        direction.y = 0f; // 수직 성분 제거

        if (direction.magnitude > hitRange) // 타격 거리 확인
        {
            return; // 거리 밖 대상 제외
        }

        if (direction.sqrMagnitude > 0.0001f && Vector3.Dot(transform.forward, direction.normalized) < 0.15f) // 전방 판정 확인
        {
            return; // 전방 밖 대상 제외
        }

        if (targetDefense != null) // 방어 관리자 확인
        {
            targetDefense.ResolveIncomingAttack(actor, damage, postureDamage); // 방어 포함 피해 판정
        }
        else // 방어 관리자 누락 처리
        {
            targetHealth.TakeDamage(damage, postureDamage, gameObject); // 일반 피해 적용
        }
    }

    private void UpdateWeaponSwing() // 적 검 회전 연출
    {
        if (weaponSocket == null) // 무기 소켓 확인
        {
            return; // 연출 중단
        }

        float totalDuration = Mathf.Max(0.01f, windup + recovery); // 전체 공격 시간 계산
        float normalized = Mathf.Clamp01(timer / totalDuration); // 공격 진행도 계산
        float startAngle = strongAttack ? -115f : -75f; // 시작 회전 각도 선택
        float endAngle = strongAttack ? 120f : 90f; // 종료 회전 각도 선택
        float swing = Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, normalized)); // 휘두르기 각도 계산
        weaponSocket.localRotation = weaponBaseRotation * Quaternion.Euler(0f, swing, strongAttack ? -35f : -18f); // 무기 회전 적용
    }
}
