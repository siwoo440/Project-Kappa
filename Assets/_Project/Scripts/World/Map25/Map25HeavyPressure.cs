using ProjectK.Day21; // E-03 수배 경비 상태 참조
using UnityEngine; // 충격파 타이밍과 피해 처리

namespace ProjectK.Day25 // 25일차 수배 병력 차별화 이름 공간
{
    [DisallowMultipleComponent] // E-03 압박 공격 중복 방지
    [RequireComponent(typeof(MapWantedGuardAgent))] // E-03 경비 AI 필수
    [RequireComponent(typeof(EnemyActor))] // 생존 상태 필수
    public sealed class Map25HeavyPressure : MonoBehaviour // E-03 느린 내려찍기·충격파 압박 행동
    {
        [SerializeField, Min(2f)] private float triggerDistance = 4.6f; // 충격파 시작 최대 거리
        [SerializeField, Min(0.1f)] private float minimumDistance = 2.7f; // 일반 검 공격과 겹치지 않는 최소 거리
        [SerializeField, Min(0.1f)] private float windup = 0.85f; // 내려찍기 예고 시간
        [SerializeField, Min(0.1f)] private float cooldown = 6.5f; // 충격파 재사용 대기
        [SerializeField, Min(0.1f)] private float healthDamage = 48f; // 기획 강공격 체력 피해
        [SerializeField, Min(0.1f)] private float postureDamage = 36f; // 충격파 자세 피해
        private MapWantedGuardAgent guard; // E-03 경비 AI 참조
        private EnemyActor actor; // E-03 생명 상태 참조
        private EnemyMeleeCombat melee; // 기존 검 공격 참조
        private EnemyStatusController status; // 마비 상태 참조
        private PlayerHealth playerHealth; // 플레이어 체력 참조
        private float nextReadyTime; // 다음 충격파 가능 시각
        private float attackAt; // 현재 충격파 타격 시각
        private bool windingUp; // 내려찍기 준비 상태

        private void Awake() // E-03 전투 참조 연결
        {
            guard = GetComponent<MapWantedGuardAgent>(); // 수배 경비 AI 조회
            actor = GetComponent<EnemyActor>(); // 적 생명 관리자 조회
            melee = GetComponent<EnemyMeleeCombat>(); // 기존 검 공격 조회
            status = GetComponent<EnemyStatusController>(); // 마비 상태 조회
        }

        private void OnEnable() // 풀 재사용 공격 상태 초기화
        {
            nextReadyTime = Time.time + 2.5f; // 생성 직후 즉시 충격파 방지
            attackAt = float.PositiveInfinity; // 타격 시각 초기화
            windingUp = false; // 준비 상태 초기화
            playerHealth = null; // 플레이어 참조 초기화
        }

        private void Update() // 충격파 준비·타격 갱신
        {
            if (guard == null || guard.UnitKind != MapWantedUnitKind.Heavy || actor == null || actor.IsDead || !guard.enabled) // E-03 활성 상태 확인
            {
                windingUp = false; // 잘못된 준비 상태 해제
                return; // 행동 중단
            }

            if (actor.IsPostureBroken || (status != null && status.IsStunned)) // 자세 붕괴·마비 상태 확인
            {
                windingUp = false; // 충격파 준비 취소
                return; // 행동 중단
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 상태 조회
            if (wanted == null || wanted.Player == null || wanted.Stars < 4) // E-03 대응 단계 확인
            {
                windingUp = false; // 준비 상태 해제
                return; // 충격파 비활성화
            }

            playerHealth = playerHealth != null ? playerHealth : wanted.Player.GetComponent<PlayerHealth>(); // 플레이어 체력 참조 보정
            if (playerHealth == null || playerHealth.IsDead) // 플레이어 생존 확인
            {
                windingUp = false; // 준비 상태 해제
                return; // 행동 중단
            }

            if (windingUp) // 현재 내려찍기 예고 중 확인
            {
                RotateTowardsPlayer(wanted.Player.transform.position); // 준비 중 플레이어 방향 유지
                if (Time.time >= attackAt) // 실제 충격파 타격 시각 확인
                {
                    ApplyShockwave(wanted.Player.transform); // 범위 충격파 적용
                    windingUp = false; // 준비 상태 종료
                    nextReadyTime = Time.time + cooldown; // 다음 공격 시각 예약
                }
                return; // 준비 중 신규 공격 검사 생략
            }

            if (Time.time < nextReadyTime || (melee != null && melee.IsBusy)) // 재사용 대기와 기존 검 공격 확인
            {
                return; // 충격파 시작 대기
            }

            float distance = PlanarDistance(transform.position, wanted.Player.transform.position); // 플레이어 수평 거리 계산
            if (distance < minimumDistance || distance > triggerDistance) // 충격파 사용 거리 확인
            {
                return; // 적절한 거리까지 대기
            }

            windingUp = true; // 내려찍기 준비 시작
            attackAt = Time.time + windup; // 실제 타격 시각 예약
            if (melee != null) // 기존 검 공격 존재 확인
            {
                melee.CancelAttack(); // 충격파 준비 중 기존 검 공격 취소
            }
        }

        private void ApplyShockwave(Transform player) // 충격파 범위 피해 적용
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) // 플레이어 상태 확인
            {
                return; // 피해 처리 중단
            }

            float distance = PlanarDistance(transform.position, player.position); // 타격 시점 거리 재확인
            if (distance > triggerDistance + 0.35f) // 충격파 최대 범위 확인
            {
                return; // 범위 밖 피해 제외
            }

            Vector3 start = transform.position + Vector3.up * 1.1f; // E-03 몸 중심 검사 위치
            Vector3 end = EquipmentTargeting.BodyCenter(player); // 플레이어 몸 중심 위치
            if (!EquipmentTargeting.HasClearPath(start, end, transform, player)) // 벽 너머 충격파 피해 방지
            {
                return; // 차단된 피해 제외
            }

            playerHealth.TakeDamage(healthDamage, postureDamage, gameObject); // 기획 강공격 수준 충격파 피해 적용
        }

        private void RotateTowardsPlayer(Vector3 playerPosition) // 내려찍기 준비 중 플레이어 방향 회전
        {
            Vector3 direction = playerPosition - transform.position; // 플레이어 방향 계산
            direction.y = 0f; // 수평 회전만 사용
            if (direction.sqrMagnitude <= 0.001f) // 유효 방향 확인
            {
                return; // 회전 생략
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 회전 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 4.5f * Time.deltaTime); // 중장갑의 느린 방향 전환 적용
        }

        private static float PlanarDistance(Vector3 first, Vector3 second) // XZ 평면 거리 계산
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)); // 수평 거리 반환
        }
    }
}
