using System.Collections; // 코루틴 기능
using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class PlayerAssassination : MonoBehaviour // 플레이어 암살 관리자
{
    [Header("Targeting")] // 대상 설정 구분
    [SerializeField] private float assassinationDistance = 2.1f; // 암살 가능 거리
    [SerializeField] private float rearDotThreshold = 0.30f; // 후방 판정 기준
    [SerializeField] private LayerMask targetMask = ~0; // 대상 감지 마스크

    [Header("Timing")] // 암살 시간 설정 구분
    [SerializeField] private float strikeDelay = 0.22f; // 처치 적용 지연
    [SerializeField] private float recoveryTime = 0.28f; // 처치 후 복귀 시간

    [Header("Prompt")] // 안내 UI 설정 구분
    [SerializeField] private bool showPrompt = true; // 암살 안내 표시 여부

    private PlayerEquipmentManager equipment; // 장비 행동 참조
    private PlayerHealth health; // 생존 상태 참조
    private PlayerDefenseController defense; // 방어 상태 참조
    private PlayerMovement movement; // 이동 관리자 참조
    private PlayerDirectionIndicator directionIndicator; // 방향 표시 참조
    private PlayerCombatController combatController; // 전투 관리자 참조
    private EnemyActor currentTarget; // 현재 암살 대상
    private bool isAssassinating; // 암살 진행 상태

    public bool HasTarget => currentTarget != null; // 암살 대상 존재 여부 읽기
    public bool IsAssassinating => isAssassinating; // 암살 진행 상태 읽기

    private void Awake() // 초기 참조 설정
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 관리자 연결
        health = GetComponent<PlayerHealth>(); // 생존 상태 연결
        defense = GetComponent<PlayerDefenseController>(); // 방어 상태 연결
        movement = GetComponent<PlayerMovement>(); // 이동 관리자 조회
        directionIndicator = GetComponent<PlayerDirectionIndicator>(); // 방향 표시 조회
        combatController = GetComponent<PlayerCombatController>(); // 전투 관리자 조회
    }

    private void Update() // 매 프레임 대상 탐색
    {
        if (isAssassinating) // 암살 진행 상태 확인
        {
            currentTarget = null; // 현재 대상 초기화
            return; // 대상 탐색 중단
        }

        if (combatController != null && combatController.IsAttacking) // 공격 중 상태 확인
        {
            currentTarget = null; // 현재 대상 초기화
            return; // 대상 탐색 중단
        }

        currentTarget = ActionBlocked() ? null : FindBestTarget(); // 사용 가능한 상태에서만 암살 표시
    }

    public bool TryAssassinate() // 암살 시도
    {
        if (ActionBlocked()) // 장비와 생존 상태 확인
        {
            return false; // 다른 행동 중 암살 금지
        }

        if (isAssassinating) // 암살 진행 상태 확인
        {
            return false; // 암살 실패 반환
        }

        EnemyActor target = currentTarget != null ? currentTarget : FindBestTarget(); // 현재 또는 신규 대상 선택

        if (target == null) // 대상 존재 확인
        {
            return false; // 암살 실패 반환
        }

        StartCoroutine(AssassinationRoutine(target)); // 암살 코루틴 시작
        return true; // 암살 입력 소비 반환
    }

    private bool ActionBlocked() // 암살과 장비 충돌 검사
    {
        return (equipment != null && (equipment.IsBusy || (equipment.Firearm != null && equipment.Firearm.SuppressesDirection))) || (health != null && (health.IsDead || health.IsPostureBroken)) || (defense != null && defense.IsDefending) || (combatController != null && (combatController.IsAttacking || combatController.IsLocked)); // 행동 잠금 통합
    }

    private EnemyActor FindBestTarget() // 최적 암살 대상 검색
    {
        Collider[] overlaps = Physics.OverlapSphere(transform.position, assassinationDistance, targetMask, QueryTriggerInteraction.Collide); // 주변 충돌 대상 조회
        EnemyActor bestTarget = null; // 최적 대상 초기화
        float bestDistance = float.MaxValue; // 최적 거리 초기화

        for (int i = 0; i < overlaps.Length; i++) // 주변 대상 순회
        {
            Collider candidateCollider = overlaps[i]; // 현재 충돌체 저장

            if (candidateCollider == null) // 충돌체 존재 확인
            {
                continue; // 누락 대상 제외
            }

            EnemyActor enemy = candidateCollider.GetComponentInParent<EnemyActor>(); // 적 생명 관리자 조회

            if (enemy == null || !enemy.CanBeAssassinated) // 암살 가능 적 확인
            {
                continue; // 암살 불가 대상 제외
            }

            if (!IsBehindTarget(enemy.transform)) // 후방 위치 확인
            {
                continue; // 전방 또는 측면 대상 제외
            }

            DetectionSensor sensor = enemy.GetComponent<DetectionSensor>(); // 적 탐지 센서 조회

            if (sensor != null && sensor.enabled && sensor.State == DetectionState.Detected) // 완전 탐지 상태 확인
            {
                continue; // 발견된 적 암살 제외
            }

            if (!EquipmentTargeting.HasClearPath(EquipmentTargeting.BodyCenter(transform), candidateCollider.bounds.center, transform, enemy.transform)) // 벽 너머 암살 방지
            {
                continue; // 차단된 대상 제외
            }

            float distance = Vector3.Distance(transform.position, enemy.transform.position); // 대상 거리 계산

            if (distance >= bestDistance) // 기존 대상보다 먼 거리 확인
            {
                continue; // 먼 대상 제외
            }

            bestDistance = distance; // 최적 거리 저장
            bestTarget = enemy; // 최적 대상 저장
        }

        return bestTarget; // 최적 대상 반환
    }

    private bool IsBehindTarget(Transform target) // 적 후방 판정
    {
        Vector3 targetToPlayer = transform.position - target.position; // 적에서 플레이어 방향 계산
        targetToPlayer.y = 0f; // 수직 성분 제거

        if (targetToPlayer.sqrMagnitude <= 0.0001f) // 방향 길이 확인
        {
            return false; // 후방 판정 실패
        }

        targetToPlayer.Normalize(); // 방향 정규화
        Vector3 targetForward = target.forward; // 적 전방 저장
        targetForward.y = 0f; // 전방 수직 성분 제거
        targetForward.Normalize(); // 적 전방 정규화
        float rearDot = Vector3.Dot(targetForward, targetToPlayer); // 후방 내적 계산
        return rearDot <= -rearDotThreshold; // 후방 여부 반환
    }

    private IEnumerator AssassinationRoutine(EnemyActor target) // 암살 실행 코루틴
    {
        isAssassinating = true; // 암살 진행 상태 저장
        currentTarget = null; // 현재 대상 초기화

        if (movement != null) // 이동 관리자 확인
        {
            movement.SetMovementEnabled(false); // 이동 비활성화
        }

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(true); // 방향 표시 숨김
        }

        if (combatController != null) // 전투 관리자 확인
        {
            combatController.SetExternalLock(true); // 공격 입력 잠금
        }

        FaceTarget(target.transform); // 암살 대상 바라보기
        yield return new WaitForSeconds(strikeDelay); // 암살 타격 시간 대기

        if (target != null && !target.IsDead && (health == null || (!health.IsDead && !health.IsPostureBroken))) // 타격 전 대상과 플레이어 생존 재검사
        {
            target.Assassinate(gameObject); // 즉시 암살 처리
        }

        yield return new WaitForSeconds(recoveryTime); // 암살 후 복귀 대기

        if (movement != null) // 이동 관리자 확인
        {
            movement.SetMovementEnabled(health == null || (!health.IsDead && !health.IsPostureBroken)); // 생존 상태에서만 이동 복구
        }

        if (directionIndicator != null) // 방향 표시 확인
        {
            directionIndicator.SetSuppressed(false); // 방향 표시 복구
        }

        if (combatController != null) // 전투 관리자 확인
        {
            combatController.SetExternalLock(false); // 공격 입력 잠금 해제
        }

        isAssassinating = false; // 암살 진행 상태 해제
    }

    private void FaceTarget(Transform target) // 암살 대상 바라보기
    {
        if (target == null) // 대상 확인
        {
            return; // 회전 처리 중단
        }

        Vector3 direction = target.position - transform.position; // 대상 방향 계산
        direction.y = 0f; // 수직 성분 제거

        if (direction.sqrMagnitude <= 0.0001f) // 방향 길이 확인
        {
            return; // 회전 처리 중단
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 대상 방향 회전 적용
    }

    private void OnGUI() // 암살 안내 UI 표시
    {
        if (!showPrompt || currentTarget == null || isAssassinating) // 표시 조건 확인
        {
            return; // UI 표시 중단
        }

        GUIStyle style = new GUIStyle(GUI.skin.box); // 안내 스타일 생성
        style.alignment = TextAnchor.MiddleCenter; // 중앙 정렬 적용
        style.fontSize = 22; // 글자 크기 적용
        style.fontStyle = FontStyle.Bold; // 굵은 글자 적용
        style.normal.textColor = Color.white; // 글자 색상 적용
        float width = 180f; // 안내 너비 설정
        float height = 42f; // 안내 높이 설정
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, height); // 안내 위치 계산
        GUI.Box(rect, "[F] 암살", style); // 암살 안내 표시
    }
}
