using UnityEngine; // 표적 전투·탐지 제어

namespace ProjectK.Day33 // 33일차 M-02 잠입 암살 이름 공간
{
    [DisallowMultipleComponent] // 표적 AI 중복 방지
    [RequireComponent(typeof(EnemyActor), typeof(DetectionSensor), typeof(EnemyMeleeCombat))] // 표적 기본 전투 구성
    public sealed class Map33MissionTargetAI : MonoBehaviour // 고정형 임무 표적의 탐지·근접 대응 AI
    {
        private EnemyActor actor; // 표적 생명 상태
        private DetectionSensor sensor; // 플레이어 탐지 센서
        private EnemyMeleeCombat melee; // 근접 반격 시스템
        private Transform player; // 현재 플레이어
        private float turnSpeed = 220f; // 발각 뒤 회전 속도

        private void Awake() // 표적 기본 참조 연결
        {
            actor = GetComponent<EnemyActor>(); // 생명 관리자 연결
            sensor = GetComponent<DetectionSensor>(); // 탐지 센서 연결
            melee = GetComponent<EnemyMeleeCombat>(); // 근접 전투 연결
        }

        public void Configure(Transform target) // 런타임 플레이어 연결
        {
            player = target; // 추적 대상 저장
            sensor = sensor != null ? sensor : GetComponent<DetectionSensor>(); // 센서 참조 보정

            if (sensor != null && player != null) // 탐지 구성 가능 여부
            {
                sensor.Configure(player, 16f, 90f, 7f, ~0); // 중거리 시야·청각 설정
            }
        }

        private void Update() // 발각 뒤 플레이어 방향 대응
        {
            if (actor == null || actor.IsDead || player == null || sensor == null) // 동작 가능 상태 확인
            {
                return; // AI 갱신 중단
            }

            if (sensor.State != DetectionState.Suspicious && sensor.State != DetectionState.Detected) // 의심·발각 여부 확인
            {
                return; // 미인지 상태 유지
            }

            Vector3 direction = player.position - transform.position; // 플레이어 방향 계산
            direction.y = 0f; // 수평 방향만 사용

            if (direction.sqrMagnitude > 0.001f) // 유효 회전 방향 확인
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 회전 생성
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime); // 천천히 플레이어 방향 전환
            }

            if (sensor.State != DetectionState.Detected || melee == null) // 완전 발각 여부 확인
            {
                return; // 공격 처리 생략
            }

            if (direction.sqrMagnitude <= 2.6f * 2.6f) // 근접 반격 거리 확인
            {
                PlayerHealth targetHealth = player.GetComponent<PlayerHealth>(); // 플레이어 체력 조회
                melee.TryStartAttack(targetHealth); // 기존 근접 공격 시스템 사용
            }
        }
    }
}
