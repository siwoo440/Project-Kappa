using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 상태 관리자 방지
[RequireComponent(typeof(EnemyActor))] // 생존 상태 확보
public sealed class EnemyStatusController : MonoBehaviour // 적 마비 상태 관리자
{
    private EnemyActor actor; // 적 생존 상태 참조
    private float stunnedUntil; // 마비 종료 시각
    private float nextEffect; // 다음 마비 효과 시각
    private Material effectMaterial; // 마비 표시 재질
    public bool IsStunned => isActiveAndEnabled && actor != null && !actor.IsDead && Time.time < stunnedUntil; // 실제 마비 상태 조회
    public float RemainingDuration => IsStunned ? stunnedUntil - Time.time : 0f; // 남은 마비 시간 조회

    private void Awake() // 적 상태 연결
    {
        actor = GetComponent<EnemyActor>(); // 생명 관리자 연결
    }

    public void ApplyStun(float duration, Material material) // 마비 적용
    {
        actor = actor != null ? actor : GetComponent<EnemyActor>(); // 참조 보정
        if (actor == null || actor.IsDead || duration <= 0f) // 마비 가능 조건 확인
        {
            return; // 사망한 적에게 적용 방지
        }

        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration); // 효과 시간을 합산하지 않고 긴 쪽 유지
        effectMaterial = material; // 마비 효과 재질 저장
        EnemyMeleeCombat melee = GetComponent<EnemyMeleeCombat>(); // 현재 적 공격 조회
        if (melee != null) // 공격 기능 확인
        {
            melee.CancelAttack(); // 마비 순간 진행 중 공격 취소
        }
    }

    private void Update() // 마비 표시 갱신
    {
        if (IsStunned && Time.time >= nextEffect) // 마비 중 효과 주기 확인
        {
            EquipmentTransientEffect.ShowRing(transform.position + Vector3.up * 1.5f, 0.55f, effectMaterial, new Color(0.72f, 0.35f, 1f), 0.22f); // 보라색 마비 링 표시
            nextEffect = Time.time + 0.25f; // 다음 효과 예약
        }
    }

    private void OnDisable() // 비활성 상태 정리
    {
        stunnedUntil = 0f; // 남은 마비 시간 초기화
    }
}
