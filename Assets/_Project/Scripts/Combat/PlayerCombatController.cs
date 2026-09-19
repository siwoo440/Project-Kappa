using System.Collections.Generic; // 중복 피격 방지 집합
using ProjectK.Day21; // 시민·차량 공통 피해 연결
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

    [SerializeField] private MeleeWeaponDefinition weaponDefinition; // 장착 근접 무기 데이터
    private PlayerEquipmentManager equipment; // 장비 행동 상태
    private PlayerHealth health; // 생존 상태
    private PlayerAssassination assassination; // 암살 상태
    private PlayerInput playerInput; // 플레이어 입력 참조
    private InputAction attackAction; // 공격 입력 액션
    private PlayerDirectionIndicator directionIndicator; // 방향 표시 참조
    private PlayerDefenseController defenseController; // 방어 관리자 참조
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
        defenseController = GetComponent<PlayerDefenseController>(); // 방어 관리자 조회
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 관리자 연결
        health = GetComponent<PlayerHealth>(); // 생존 상태 연결
        assassination = GetComponent<PlayerAssassination>(); // 암살 상태 연결
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
        bool blocked = (health != null && (health.IsDead || health.IsPostureBroken)) || (assassination != null && assassination.IsAssassinating) || (equipment != null && (equipment.IsBusy || equipment.IsFirearmEquipped)); // 총기 장착 중 검 공격 차단
        if (blocked) // 공격 중단 조건 확인
        {
            if (isAttacking) // 진행 중 공격 확인
            {
                EndAttack(); // 공격 취소와 검 위치 복구
            }

            return; // 새 공격 입력 금지
        }

        if (isAttacking) // 공격 진행 상태 확인
        {
            UpdateAttack(); // 공격 동작 갱신
            return; // 신규 공격 처리 중단
        }

        if (externalLock || attackAction == null) // 공격 잠금과 입력 확인
        {
            return; // 공격 처리 중단
        }

        if (defenseController != null && defenseController.IsDefending) // 방어 상태 확인
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

    public void ApplyDefinition(MeleeWeaponDefinition definition) // 현재 무기 데이터 적용
    {
        if (isAttacking || definition == null || definition.Stats == null) // 진행 중 공격과 비어 있는 설정 보호
        {
            return; // 공격 도중 수치 교체 방지
        }

        weaponDefinition = definition; // 장착 정의 저장
        healthDamage = definition.Stats.HealthDamage; // 체력 피해 적용
        postureDamage = definition.Stats.PostureDamage; // 자세 피해 적용
        attackReach = Mathf.Max(0.1f, definition.Stats.EffectiveRange); // 무기 사거리 적용
        attackRadius = definition.HitRadius; // 무기 타격 반경 적용
        attackDuration = definition.AnimationDuration; // 휘두르기 시간 적용
        hitTime = definition.HitTime; // 타격 시점 적용
        attackCooldown = Mathf.Max(attackDuration, definition.Stats.FireInterval); // 공격 간격 적용
    }

    private void OnDisable() // 비활성 상태 정리
    {
        if (isAttacking) // 진행 중 공격 확인
        {
            EndAttack(); // 중단된 공격 표시 복구
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
        ApplyDefinition(weaponDefinition); // 이번 공격에 사용할 무기 수치 확정
        isAttacking = true; // 공격 상태 저장
        attackTimer = 0f; // 공격 시간 초기화
        hitApplied = false; // 피해 적용 상태 초기화
        nextAttackTime = Time.time + attackCooldown; // 다음 공격 가능 시간 저장
        RotateTowardCameraForward(); // 카메라 방향으로 회전

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(true); // 방향 표시 숨김
        }

        Debug.Log((weaponDefinition != null ? weaponDefinition.DisplayName : "절선") + " 기본 공격"); // 공격 로그 출력
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

        if (directionIndicator != null && (defenseController == null || !defenseController.IsDefending)) // 방향 표시 복구 조건 확인
        {
            directionIndicator.SetSuppressed(false); // 방향 표시 복구
        }
    }

    private void PerformHit() // 공격 피해 판정
    {
        Vector3 center = EquipmentTargeting.BodyCenter(transform) + transform.forward * attackReach; // 공격 중심 위치 계산
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, targetMask, QueryTriggerInteraction.Collide); // 공격 범위 충돌 조회
        HashSet<TrainingReactiveTarget> damagedTargets = new HashSet<TrainingReactiveTarget>(); // 표적의 여러 충돌체 중복 방지
        HashSet<EnemyActor> damagedEnemies = new HashSet<EnemyActor>(); // 중복 피해 방지 집합 생성
        HashSet<WorldDamageReceiver> damagedWorld = new HashSet<WorldDamageReceiver>(); // 시민·차량 중복 피해 방지 집합 생성

        for (int i = 0; i < hits.Length; i++) // 충돌 대상 순회
        {
            TrainingReactiveTarget target = hits[i].GetComponentInParent<TrainingReactiveTarget>(); // 검으로 칠 수 있는 훈련 표적
            if (target != null) // 실전 적과 표적 구분
            {
                Vector3 toTarget = hits[i].bounds.center - EquipmentTargeting.BodyCenter(transform); // 실제 표적 방향
                if (target.AcceptsHit && !damagedTargets.Contains(target) && Vector3.Dot(transform.forward, toTarget.normalized) >= -0.1f && EquipmentTargeting.HasClearPath(EquipmentTargeting.BodyCenter(transform), hits[i].bounds.center, transform, target.transform)) // 전방과 엄폐와 중복 피해 검사
                {
                    damagedTargets.Add(target); // 이번 공격에 처리한 표적 기록
                    target.ReceiveImpact(healthDamage, toTarget); // 피격 시 뒤로 넘어짐
                }
                continue; // 표적을 실전 적으로 중복 처리하지 않음
            }
            WorldDamageReceiver worldTarget = hits[i].GetComponentInParent<WorldDamageReceiver>(); // 시민·차량 공통 피해 대상 조회
            if (worldTarget != null && worldTarget.AcceptsHit && !damagedWorld.Contains(worldTarget)) // 살아 있는 월드 대상과 중복 여부 확인
            {
                Vector3 worldDirection = worldTarget.transform.position - transform.position; // 대상 방향 계산
                worldDirection.y = 0f; // 수직 성분 제거
                if (worldDirection.sqrMagnitude > 0.0001f && Vector3.Dot(transform.forward, worldDirection.normalized) >= -0.10f && EquipmentTargeting.HasClearPath(EquipmentTargeting.BodyCenter(transform), hits[i].bounds.center, transform, worldTarget.transform)) // 전방과 엄폐 조건 확인
                {
                    damagedWorld.Add(worldTarget); // 이번 공격 처리 대상 기록
                    worldTarget.ApplyDamage(healthDamage, gameObject, WorldDamageType.Melee); // 검 체력 피해 적용
                }
                continue; // 시민·차량을 적 체력 로직으로 중복 처리하지 않음
            }
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

            Vector3 targetPoint = hits[i].bounds.center; // 대상 충돌체 중심 조회
            if (!EquipmentTargeting.HasClearPath(EquipmentTargeting.BodyCenter(transform), targetPoint, transform, enemy.transform)) // 벽을 통과하는 검 공격 검사
            {
                continue; // 엄폐물 뒤의 적 제외
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
        if (!showCombatHint || equipment != null) // 새 장비 HUD의 중복 안내 방지
        {
            return; // UI 표시 중단
        }

        GUIStyle style = new GUIStyle(GUI.skin.box); // 안내 스타일 생성
        style.alignment = TextAnchor.MiddleCenter; // 중앙 정렬 적용
        style.fontSize = 16; // 글자 크기 적용
        style.normal.textColor = Color.white; // 글자 색상 적용
        Rect rect = new Rect(18f, Screen.height - 62f, 310f, 38f); // 안내 위치 계산
        GUI.Box(rect, "[LMB] 공격  [RMB] 방어/받아치기", style); // 공격 방어 안내 표시
    }

    private void OnDrawGizmosSelected() // 공격 범위 에디터 표시
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f); // 기즈모 색상 지정
        Vector3 center = EquipmentTargeting.BodyCenter(transform) + transform.forward * attackReach; // 공격 중심 계산
        Gizmos.DrawWireSphere(center, attackRadius); // 공격 범위 표시
    }
}
