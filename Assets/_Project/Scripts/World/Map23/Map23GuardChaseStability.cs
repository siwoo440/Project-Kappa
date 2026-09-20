using ProjectK.Day21; // 수배 경비와 수배 상태 참조
using UnityEngine; // 이동 복구와 장애물 검사

namespace ProjectK.Day23 // 23일차 추격 안정화 이름 공간
{
    [DisallowMultipleComponent] // 경비별 안정화 기능 중복 방지
    public sealed class Map23GuardChaseStability : MonoBehaviour // 벽·골목 정체를 완화하는 보조 이동
    {
        [SerializeField, Min(0.2f)] private float sampleInterval = 0.8f; // 정체 검사 간격
        [SerializeField, Min(0.05f)] private float stuckDistance = 0.45f; // 정체로 판단할 최소 이동량
        [SerializeField, Min(0.2f)] private float recoveryDuration = 1.15f; // 우회 복구 지속 시간
        [SerializeField, Min(0.1f)] private float recoverySpeed = 2.8f; // 우회 보조 이동 속도
        [SerializeField, Min(0.5f)] private float obstacleProbeDistance = 2.2f; // 우회 방향 장애물 검사 거리
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.32f; // 우회 방향 검사 반경
        [SerializeField, Min(1f)] private float minimumTargetDistance = 4f; // 근접 전투 중 정체 판정 제외 거리
        private MapWantedGuardAgent guard; // 현재 수배 경비 참조
        private CharacterController controller; // 경비 이동 충돌체
        private Vector3 samplePosition; // 이전 정체 검사 위치
        private float nextSampleTime; // 다음 정체 검사 시각
        private float recoveryUntil; // 우회 복구 종료 시각
        private Vector3 recoveryDirection; // 현재 강제 우회 방향

        private void Awake() // 경비와 충돌체 연결
        {
            guard = GetComponent<MapWantedGuardAgent>(); // 수배 경비 조회
            controller = guard != null ? guard.Controller : GetComponent<CharacterController>(); // 이동 충돌체 조회
            samplePosition = transform.position; // 첫 정체 검사 위치 저장
        }

        private void OnEnable() // 풀 재사용 시 상태 초기화
        {
            samplePosition = transform.position; // 현재 위치를 새 기준으로 저장
            nextSampleTime = Time.unscaledTime + sampleInterval; // 첫 검사 시각 예약
            recoveryUntil = float.NegativeInfinity; // 이전 복구 상태 제거
            recoveryDirection = Vector3.zero; // 이전 우회 방향 제거
        }

        private void Update() // 일정 간격 정체 상태 확인
        {
            if (guard == null || controller == null) // 필수 참조 확인
            {
                return; // 처리 중단
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null || wanted.Stars <= 0 || wanted.Player == null || guard.IsDead) // 추격 가능 상태 확인
            {
                samplePosition = transform.position; // 다음 수배를 위해 위치 기준 갱신
                return; // 평상시 복구 기능 중단
            }

            if (Time.unscaledTime < nextSampleTime) // 정체 검사 시각 확인
            {
                return; // 다음 검사까지 대기
            }

            nextSampleTime = Time.unscaledTime + sampleInterval; // 다음 정체 검사 시각 예약
            float moved = Vector2.Distance(new Vector2(samplePosition.x, samplePosition.z), new Vector2(transform.position.x, transform.position.z)); // 수평 이동량 계산
            float targetDistance = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(wanted.Player.transform.position.x, wanted.Player.transform.position.z)); // 플레이어 수평 거리 계산
            samplePosition = transform.position; // 다음 검사 기준 위치 갱신

            if (moved >= stuckDistance || targetDistance <= minimumTargetDistance) // 정상 이동 또는 근접 전투 확인
            {
                return; // 우회 복구 불필요
            }

            recoveryDirection = ChooseRecoveryDirection(wanted.Player.transform.position); // 벽에서 벗어날 방향 선택
            if (recoveryDirection.sqrMagnitude <= 0.01f) // 유효한 우회 방향 확인
            {
                return; // 안전한 방향이 없으면 기존 AI 유지
            }

            recoveryUntil = Time.unscaledTime + recoveryDuration; // 일정 시간 우회 이동 활성화
        }

        private void LateUpdate() // 기존 수배 AI 이동 뒤 보조 우회 적용
        {
            if (controller == null || guard == null || guard.IsDead || Time.unscaledTime >= recoveryUntil) // 우회 이동 가능 상태 확인
            {
                return; // 보조 이동 생략
            }

            controller.Move(recoveryDirection * recoverySpeed * Time.deltaTime); // 기존 이동에 짧은 측면 이동 추가
            Quaternion targetRotation = Quaternion.LookRotation(recoveryDirection, Vector3.up); // 우회 방향 회전 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8f * Time.deltaTime); // 벽에서 빠져나오는 방향으로 회전
        }

        private Vector3 ChooseRecoveryDirection(Vector3 targetPosition) // 여러 각도 중 안전한 우회 방향 선택
        {
            Vector3 towardTarget = targetPosition - transform.position; // 플레이어 방향 계산
            towardTarget.y = 0f; // 수평 이동만 사용

            if (towardTarget.sqrMagnitude <= 0.01f) // 플레이어와 같은 수평 위치 확인
            {
                towardTarget = transform.forward; // 현재 진행 방향 사용
            }

            towardTarget.Normalize(); // 기준 방향 정규화
            float sign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 경비마다 좌우 우선순위 분산
            float[] angles = new float[] { 82f, -82f, 118f, -118f, 155f, -155f }; // 좁은 골목과 막다른 길용 우회 후보

            foreach (float rawAngle in angles) // 우회 후보 순회
            {
                Vector3 direction = Quaternion.Euler(0f, rawAngle * sign, 0f) * towardTarget; // 후보 방향 계산
                direction.y = 0f; // 수평 방향 유지
                direction.Normalize(); // 이동 방향 정규화

                if (IsRecoveryDirectionSafe(direction)) // 장애물과 낙하 위험 확인
                {
                    return direction; // 첫 안전 방향 사용
                }
            }

            return Vector3.zero; // 모든 후보가 막힌 경우 복구 이동 생략
        }

        private bool IsRecoveryDirectionSafe(Vector3 direction) // 장애물과 발밑 확인
        {
            Vector3 origin = transform.position + Vector3.up * 0.85f; // 허리 높이 검사 시작점
            RaycastHit[] hits = Physics.SphereCastAll(origin, obstacleProbeRadius, direction, obstacleProbeDistance, ~0, QueryTriggerInteraction.Ignore); // 전방 장애물 전체 검사

            foreach (RaycastHit hit in hits) // 충돌 후보 순회
            {
                Collider collider = hit.collider; // 현재 충돌체 조회
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) // 자기 몸 충돌 확인
                {
                    continue; // 자기 충돌 제외
                }

                if (collider.GetComponentInParent<MapWantedGuardAgent>() != null) // 다른 수배 경비 확인
                {
                    continue; // 동료 경비는 고정 벽으로 취급하지 않음
                }

                return false; // 고정 장애물이 있는 방향 제외
            }

            Vector3 groundProbe = transform.position + direction * 1.4f + Vector3.up * 1.4f; // 다음 발 위치 위쪽 계산
            if (!Physics.Raycast(groundProbe, Vector3.down, 3.6f, ~0, QueryTriggerInteraction.Ignore)) // 이동 방향의 바닥 존재 확인
            {
                return false; // 낙하 가능 방향 제외
            }

            return true; // 안전한 우회 방향 반환
        }
    }
}
