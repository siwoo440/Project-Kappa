using ProjectK.Day20; // 시민·차량 동적 개체 참조
using ProjectK.Day21; // 수배 경비 동적 개체 참조
using ProjectK.Day25; // E-04 드론 참조
using UnityEngine; // SphereCast와 회피 이동 처리

namespace ProjectK.Day27 // 27일차 통합 QA 이름 공간
{
    [DisallowMultipleComponent] // 드론 회피 중복 방지
    [RequireComponent(typeof(Map25SurveillanceDrone))] // E-04 드론에서만 사용
    public sealed class Map27DroneCollisionAvoidance : MonoBehaviour // E-04가 건물 내부를 통과하는 현상 완화
    {
        [SerializeField, Min(0.2f)] private float probeRadius = 0.72f; // 드론 본체 충돌 예상 반경
        [SerializeField, Min(0.5f)] private float probeDistance = 3.2f; // 이동 방향 사전 검사 거리
        [SerializeField, Min(0.1f)] private float avoidanceSpeed = 5.2f; // 장애물 발견 시 측면·상단 회피 속도
        private Vector3 previousPosition; // 직전 프레임 위치
        private int repeatedBlocks; // 연속 장애물 감지 횟수
        private float nextHotspotLog; // 반복 장애물 로그 제한 시각

        private void OnEnable() // 풀 활성화 시 이동 기록 초기화
        {
            previousPosition = transform.position; // 현재 위치를 첫 기준으로 저장
            repeatedBlocks = 0; // 장애물 반복 횟수 초기화
            nextHotspotLog = 0f; // 로그 제한 초기화
        }

        private void LateUpdate() // 원래 드론 이동이 끝난 뒤 고정 구조물 관통 보정
        {
            Vector3 current = transform.position; // 현재 드론 위치 조회
            Vector3 movement = current - previousPosition; // 이번 프레임 원래 이동량 계산
            float distance = movement.magnitude; // 이동 거리 계산

            if (distance <= 0.002f) // 실제 이동이 거의 없는지 확인
            {
                previousPosition = current; // 위치 기록만 갱신
                return; // 충돌 검사 생략
            }

            Vector3 direction = movement / distance; // 원래 이동 방향 정규화
            if (!TryFindBlockingHit(previousPosition, direction, distance + probeDistance, out RaycastHit hit)) // 앞쪽 고정 구조물 검사
            {
                repeatedBlocks = 0; // 정상 이동 시 반복 충돌 횟수 초기화
                previousPosition = current; // 정상 위치 저장
                return; // 보정 생략
            }

            repeatedBlocks++; // 연속 장애물 감지 증가
            float sign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 두 드론 회피 방향 분산
            Vector3 planarNormal = hit.normal; // 충돌면 법선 복사
            planarNormal.y = 0f; // 수평 회피 방향 계산용 높이 제거
            Vector3 side = planarNormal.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, planarNormal.normalized) * sign : Vector3.right * sign; // 벽을 따라 이동할 측면 방향 계산
            Vector3 avoidance = (side * 0.82f + Vector3.up * 0.36f).normalized; // 측면 우회에 약간의 상승 결합

            if (TryFindBlockingHit(previousPosition, avoidance, probeDistance * 0.8f, out _)) // 첫 회피 방향도 막혔는지 확인
            {
                avoidance = (-side * 0.76f + Vector3.up * 0.48f).normalized; // 반대 측면과 상승 방향으로 전환
            }

            transform.position = previousPosition + avoidance * avoidanceSpeed * Time.deltaTime; // 원래 관통 이동 대신 회피 위치 적용
            previousPosition = transform.position; // 보정된 위치를 다음 검사 기준으로 저장

            if (repeatedBlocks >= 12 && Time.unscaledTime >= nextHotspotLog) // 약 여러 프레임 같은 장애물 반복 확인
            {
                nextHotspotLog = Time.unscaledTime + 8f; // 같은 장소 반복 로그 제한
                repeatedBlocks = 0; // 반복 횟수 초기화
                Map27QAMonitor.Instance?.ReportHotspot("Drone", transform.position, this); // QA 모니터에 드론 병목 지점 기록
            }
        }

        private bool TryFindBlockingHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit blocking) // 이동 방향 고정 구조물 검사
        {
            blocking = default; // 결과 초기화
            RaycastHit[] hits = Physics.SphereCastAll(origin, probeRadius, direction.normalized, Mathf.Max(0.1f, distance), ~0, QueryTriggerInteraction.Ignore); // 드론 앞 장애물 전체 조회
            float nearest = float.PositiveInfinity; // 최근접 고정 장애물 거리 초기화

            foreach (RaycastHit hit in hits) // 충돌 후보 순회
            {
                Collider collider = hit.collider; // 현재 충돌체 조회
                if (collider == null || EquipmentTargeting.IsOwnCollider(collider, transform)) // 자기 몸 제외
                {
                    continue; // 다음 후보 검사
                }

                if (collider is TerrainCollider) // 지형 표면 확인
                {
                    continue; // 공중 드론의 일반 지면은 벽 회피 대상에서 제외
                }

                if (collider.GetComponentInParent<MapWantedGuardAgent>() != null) // 경비 확인
                {
                    continue; // 동적 경비는 고정 벽 취급하지 않음
                }

                if (collider.GetComponentInParent<MapCitizenAgent>() != null) // 시민 확인
                {
                    continue; // 동적 시민 제외
                }

                if (collider.GetComponentInParent<MapTrafficVehicle>() != null) // 차량 확인
                {
                    continue; // 이동 차량 제외
                }

                if (hit.distance >= nearest) // 현재 최근접 장애물보다 먼지 확인
                {
                    continue; // 더 먼 후보 제외
                }

                nearest = hit.distance; // 최근접 거리 갱신
                blocking = hit; // 고정 장애물 결과 저장
            }

            return !float.IsPositiveInfinity(nearest); // 고정 장애물 존재 여부 반환
        }
    }
}
