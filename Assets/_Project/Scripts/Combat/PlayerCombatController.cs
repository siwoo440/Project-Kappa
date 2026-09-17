using System.Collections.Generic; // 중복 피격 방지 집합
using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[DisallowMultipleComponent] // 중복 부착 방지
[RequireComponent(typeof(PlayerInput))] // 플레이어 입력 필수 지정
public sealed class PlayerCombatController : MonoBehaviour // 플레이어 기본 검술 관리자
{
    [Header("Weapon")] // 무기 설정 구분
    [SerializeField] private Transform weaponSocket; // 무기 회전 기준
    [SerializeField] private float healthDamage = 28f; // 체력 피해
    [SerializeField] private float postureDamage = 20f; // 자세 피해

    [Header("Attack")] // 공격 설정 구분
    [SerializeField] private float attackDuration = 0.42f; // 공격 동작 시간
    [SerializeField] private float hitTime = 0.16f; // 실제 타격 시점
    [SerializeField] private float attackCooldown = 0.55f; // 공격 간격
    [SerializeField] private float attackReach = 1.35f; // 공격 중심 거리
    [SerializeField] private float attackRadius = 1.05f; // 공격 범위 반경
    [SerializeField] private LayerMask targetMask = ~0; // 공격 대상 마스크

    [Header("Debug UI")] // 테스트 UI 설정 구분
    [SerializeField] private bool showCombatHint = true; // 전투 안내 표시 여부

    private PlayerInput playerInput; // 플레이어 입력 참조
    private InputAction attackAction; // 공격 입력 액션
    private PlayerDirectionIndicator directionIndicator; // 방향 표시 참조
    private Quaternion weaponBaseRotation; // 무기 기본 회전
    private float attackTimer; // 현재 공격 시간
    private float nextAttackTime; // 다음 공격 가능 시간
    private bool hitApplied; // 현재 공격 피해 적용 여부
    private bool isAttacking; // 현재 공격 상태
    private bool externalLock; // 외부 공격 잠금

    public bool IsAttacking => isAttacking; // 공격 상태 읽기
    public bool IsLocked => externalLock; // 외부 잠금 상태 읽기

    private void Awake() // 초기 참조 설정
    {
        playerInput = GetComponent<PlayerInput>(); // 플레이어 입력 조회
        directionIndicator = GetComponent<PlayerDirectionIndicator>(); // 방향 표시 조회
        ResolveAttackAction(); // 공격 액션 연결

        if (weaponSocket != null) // 무기 소켓 확인
        {
            weaponBaseRotation = weaponSocket.localRotation; // 기본 무기 회전 저장
        }
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveAttackAction(); // 공격 액션 재연결
    }

    private void Update() // 매 프레임 전투 처리
    {
        if (isAttacking) // 공격 진행 상태 확인
        {
            UpdateAttack(); // 공격 동작 갱신
            return; // 신규 공격 처리 중단
        }

        if (externalLock || attackAction == null) // 공격 잠금과 입력 확인
        {
            return; // 공격 처리 중단
        }

        if (Time.time < nextAttackTime) // 공격 재사용 시간 확인
        {
            return; // 공격 처리 중단
        }

        if (attackAction.WasPressedThisFrame()) // 공격 입력 확인
        {
            BeginAttack(); // 기본 공격 시작
        }
    }

    public void ConfigureWeapon(Transform socket) // 무기 소켓 설정
    {
        weaponSocket = socket; // 무기 소켓 저장
        weaponBaseRotation = weaponSocket != null ? weaponSocket.localRotation : Quaternion.identity; // 기본 회전 저장
    }

    public void SetExternalLock(bool value) // 외부 입력 잠금 설정
    {
        externalLock = value; // 잠금 상태 저장

        if (externalLock && isAttacking) // 공격 중 잠금 확인
        {
            EndAttack(); // 현재 공격 종료
        }
    }

    private void ResolveAttackAction() // 공격 입력 액션 연결
    {
        if (playerInput == null || playerInput.actions == null) // 입력 에셋 확인
        {
            attackAction = null; // 공격 액션 초기화
            return; // 연결 중단
        }

        InputActionMap actionMap = playerInput.actions.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        attackAction = actionMap != null ? actionMap.FindAction("Attack", false) : null; // 공격 액션 조회
    }

    private void BeginAttack() // 기본 공격 시작
    {
        isAttacking = true; // 공격 상태 저장
        attackTimer = 0f; // 공격 시간 초기화
        hitApplied = false; // 피해 적용 상태 초기화
        nextAttackTime = Time.time + attackCooldown; // 다음 공격 가능 시간 저장
        RotateTowardCameraForward(); // 카메라 방향으로 회전

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(true); // 방향 표시 숨김
        }

        Debug.Log("절선 기본 공격"); // 공격 로그 출력
    }

    private void UpdateAttack() // 공격 동작 갱신
    {
        attackTimer += Time.deltaTime; // 공격 시간 누적

        if (weaponSocket != null) // 무기 소켓 확인
        {
            float normalized = Mathf.Clamp01(attackTimer / attackDuration); // 공격 진행도 계산
            float swing = Mathf.Lerp(-75f, 95f, Mathf.SmoothStep(0f, 1f, normalized)); // 검 휘두르기 각도 계산
            weaponSocket.localRotation = weaponBaseRotation * Quaternion.Euler(0f, swing, -15f); // 검 회전 적용
        }

        if (!hitApplied && attackTimer >= hitTime) // 타격 시점 확인
        {
            hitApplied = true; // 피해 적용 상태 저장
            PerformHit(); // 공격 피해 판정
        }

        if (attackTimer >= attackDuration) // 공격 종료 시간 확인
        {
            EndAttack(); // 공격 종료
        }
    }

    private void EndAttack() // 공격 종료
    {
        isAttacking = false; // 공격 상태 해제
        attackTimer = 0f; // 공격 시간 초기화
        hitApplied = false; // 피해 적용 상태 초기화

        if (weaponSocket != null) // 무기 소켓 확인
        {
            weaponSocket.localRotation = weaponBaseRotation; // 무기 기본 회전 복구
        }

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(false); // 방향 표시 복구
        }
    }

    private void PerformHit() // 공격 피해 판정
    {
        Vector3 center = transform.position + Vector3.up * 0.95f + transform.forward * attackReach; // 공격 중심 위치 계산
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, targetMask, QueryTriggerInteraction.Collide); // 공격 범위 충돌 조회
        HashSet<EnemyActor> damagedEnemies = new HashSet<EnemyActor>(); // 중복 피해 방지 집합 생성

        for (int i = 0; i < hits.Length; i++) // 충돌 대상 순회
        {
            EnemyActor enemy = hits[i].GetComponentInParent<EnemyActor>(); // 적 생명 관리자 조회

            if (enemy == null || enemy.IsDead || damagedEnemies.Contains(enemy)) // 피해 대상 확인
            {
                continue; // 유효하지 않은 대상 제외
            }

            Vector3 direction = enemy.transform.position - transform.position; // 적 방향 계산
            direction.y = 0f; // 수직 성분 제거

            if (direction.sqrMagnitude > 0.0001f) // 방향 길이 확인
            {
                float forwardDot = Vector3.Dot(transform.forward, direction.normalized); // 전방 내적 계산

                if (forwardDot < -0.10f) // 지나친 후방 대상 확인
                {
                    continue; // 후방 대상 제외
                }
            }

            damagedEnemies.Add(enemy); // 피해 대상 저장
            enemy.TakeDamage(healthDamage, postureDamage, gameObject); // 검 피해 적용
        }
    }

    private void RotateTowardCameraForward() // 카메라 방향 회전
    {
        Camera targetCamera = Camera.main; // 메인 카메라 조회

        if (targetCamera == null) // 카메라 확인
        {
            return; // 회전 처리 중단
        }

        Vector3 forward = targetCamera.transform.forward; // 카메라 전방 조회
        forward.y = 0f; // 수직 성분 제거

        if (forward.sqrMagnitude <= 0.0001f) // 방향 길이 확인
        {
            return; // 회전 처리 중단
        }

        transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up); // 카메라 전방 회전 적용
    }

    private void OnGUI() // 전투 테스트 안내 표시
    {
        if (!showCombatHint) // 안내 표시 여부 확인
        {
            return; // UI 표시 중단
        }

        GUIStyle style = new GUIStyle(GUI.skin.box); // 안내 스타일 생성
        style.alignment = TextAnchor.MiddleCenter; // 중앙 정렬 적용
        style.fontSize = 16; // 글자 크기 적용
        style.normal.textColor = Color.white; // 글자 색상 적용
        Rect rect = new Rect(18f, Screen.height - 62f, 220f, 38f); // 안내 위치 계산
        GUI.Box(rect, "[LMB] 절선 기본 공격", style); // 공격 안내 표시
    }

    private void OnDrawGizmosSelected() // 공격 범위 에디터 표시
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f); // 기즈모 색상 지정
        Vector3 center = transform.position + Vector3.up * 0.95f + transform.forward * attackReach; // 공격 중심 계산
        Gizmos.DrawWireSphere(center, attackRadius); // 공격 범위 표시
    }
}
