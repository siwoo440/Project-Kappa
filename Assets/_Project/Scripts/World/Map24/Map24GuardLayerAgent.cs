using ProjectK.Day21; // 기존 수배 경비와 수배 시스템 참조
using ProjectK.Day23; // Day23 정체 복구 기능 참조
using UnityEngine; // CharacterController 이동 처리

namespace ProjectK.Day24 // 24일차 지상·지하 이동 이름 공간
{
    [DisallowMultipleComponent] // 경비 층 이동 제어 중복 방지
    public sealed class Map24GuardLayerAgent : MonoBehaviour // 수배 경비의 계단 추격과 지하 수색 제어
    {
        [SerializeField, Min(0.5f)] private float waypointReachDistance = 1.15f; // 계단 지점 도착 판정 거리
        [SerializeField, Min(0.5f)] private float searchReachDistance = 1.6f; // 지하 수색 지점 도착 거리
        [SerializeField, Min(0.1f)] private float regularTravelSpeed = 5.2f; // E-01 계단 이동 속도
        [SerializeField, Min(0.1f)] private float eliteTravelSpeed = 6.1f; // E-02 계단 이동 속도
        [SerializeField, Min(0.1f)] private float heavyTravelSpeed = 3.6f; // E-03 계단 이동 속도
        [SerializeField, Min(0.1f)] private float undergroundSearchSpeed = 4.2f; // 일반 지하 수색 속도
        [SerializeField, Min(0.1f)] private float heavyUndergroundSearchSpeed = 3.2f; // E-03 지하 수색 속도
        [SerializeField, Min(0.1f)] private float searchDwellTime = 1.3f; // 지하 수색 지점 확인 시간
        private MapWantedGuardAgent guard; // 기존 수배 경비 AI 참조
        private CharacterController controller; // 실제 경비 이동 충돌체
        private Map23GuardChaseStability stability; // Day23 정체 복구 참조
        private DetectionSensor sensor; // 경비 탐지 센서 참조
        private bool layerTravelActive; // 현재 계단 층 이동 여부
        private bool descending; // 지하 방향 이동 여부
        private int stairIndex; // 현재 목표 계단 인덱스
        private int undergroundSearchIndex; // 지하 수색 순번
        private Vector3 undergroundSearchTarget; // 현재 지하 수색 목적지
        private bool hasUndergroundSearchTarget; // 지하 수색 목적지 존재 여부
        private float searchDwellUntil; // 현재 수색 지점 대기 종료 시각
        private float verticalVelocity; // 보조 이동 중 중력 속도

        private void Awake() // 필수 경비 컴포넌트 연결
        {
            guard = GetComponent<MapWantedGuardAgent>(); // 기존 수배 경비 조회
            controller = guard != null ? guard.Controller : GetComponent<CharacterController>(); // 경비 CharacterController 조회
            stability = GetComponent<Map23GuardChaseStability>(); // Day23 정체 복구 기능 조회
            sensor = guard != null ? guard.Sensor : GetComponent<DetectionSensor>(); // 실제 시야 센서 조회
        }

        private void OnEnable() // 풀 활성화 시 층 이동 상태 초기화
        {
            layerTravelActive = false; // 이전 계단 이동 상태 제거
            descending = false; // 이전 방향 상태 제거
            stairIndex = -1; // 계단 인덱스 초기화
            undergroundSearchIndex = 0; // 지하 수색 순번 초기화
            hasUndergroundSearchTarget = false; // 이전 수색 목적지 제거
            searchDwellUntil = float.NegativeInfinity; // 이전 대기 상태 제거
            verticalVelocity = 0f; // 보조 중력 상태 초기화
        }

        private void Update() // 층 차이와 지하 수색 상태에 따라 기존 AI 제어
        {
            if (guard == null || controller == null || guard.IsDead) // 필수 경비 상태 확인
            {
                ReleaseOriginalAI(); // 가능한 경우 기존 AI 복구
                return; // Day24 이동 중단
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            Map24LayerNavigation navigation = Map24LayerNavigation.Instance; // 현재 지상·지하 관리자 조회
            if (wanted == null || wanted.Player == null || wanted.Stars <= 0 || navigation == null || !navigation.Ready) // 수배와 지하 구조 준비 확인
            {
                layerTravelActive = false; // 계단 이동 상태 해제
                hasUndergroundSearchTarget = false; // 지하 수색 상태 해제
                ReleaseOriginalAI(); // 기존 수배 AI 복구
                return; // Day24 처리 중단
            }

            Map24WorldLayer guardLayer = navigation.GetLayer(transform.position); // 현재 경비 층 판정
            Map24WorldLayer playerLayer = navigation.GetLayer(wanted.Player.transform.position); // 현재 플레이어 층 판정

            if (guardLayer != playerLayer || layerTravelActive) // 서로 다른 층 또는 계단 이동 진행 확인
            {
                BeginOrContinueLayerTravel(navigation, playerLayer); // 실제 계단 층 이동 처리
                return; // 기존 AI 처리 생략
            }

            if (guardLayer == Map24WorldLayer.Underground && ShouldUseUndergroundSearch(wanted)) // 지하 분산 수색 조건 확인
            {
                UpdateUndergroundSearch(navigation); // 지하 수색 처리
                return; // 기존 평면 수색 생략
            }

            hasUndergroundSearchTarget = false; // 지하 수색 목적지 해제
            searchDwellUntil = float.NegativeInfinity; // 수색 대기 해제
            ReleaseOriginalAI(); // 같은 층 직접 추격 복구
        }

        private bool ShouldUseUndergroundSearch(MapWantedSystem wanted) // 지하 분산 수색 사용 여부 판정
        {
            bool visible = sensor != null && sensor.TargetVisible; // 센서 직접 시야 조회
            return !visible && !wanted.HasSharedTarget; // 직접 시야와 공유 위치가 없을 때 사용
        }

        private void BeginOrContinueLayerTravel(Map24LayerNavigation navigation, Map24WorldLayer targetLayer) // 계단 이동 시작·계속 처리
        {
            if (!layerTravelActive) // 새 층 이동 시작 확인
            {
                descending = targetLayer == Map24WorldLayer.Underground; // 목표 층 이동 방향 저장
                stairIndex = navigation.FindClosestStairStep(transform.position); // 최근접 계단 단 검색

                if (stairIndex < 0) // 계단 검색 실패 확인
                {
                    ReleaseOriginalAI(); // 기존 AI 복귀
                    return; // 층 이동 포기
                }

                if (descending && PlanarDistance(transform.position, navigation.SurfaceAnchor) > 3.2f) // 지상 입구까지 거리 확인
                {
                    stairIndex = 0; // 첫 계단 단 우선 이동
                }
                else if (!descending && PlanarDistance(transform.position, navigation.UndergroundAnchor) > 3.2f) // 지하 출구까지 거리 확인
                {
                    stairIndex = navigation.StairSteps.Count - 1; // 마지막 계단 단 우선 이동
                }

                layerTravelActive = true; // 계단 이동 상태 활성화
            }

            TakeOriginalAIControl(); // 기존 평면 추격 일시 중지
            if (navigation.StairSteps.Count == 0) // 계단 목록 확인
            {
                layerTravelActive = false; // 이동 상태 해제
                ReleaseOriginalAI(); // 기존 AI 복구
                return; // 처리 중단
            }

            stairIndex = Mathf.Clamp(stairIndex, 0, navigation.StairSteps.Count - 1); // 계단 인덱스 보정
            Vector3 waypoint = navigation.StairSteps[stairIndex].position; // 현재 목표 계단 위치 조회
            MoveToward(waypoint, TravelSpeedForGuard()); // 병력별 계단 이동 속도 적용
            ApplyGravity(); // 경사면 지면 밀착

            if (PlanarDistance(transform.position, waypoint) > waypointReachDistance) // 현재 계단 단 도착 여부 확인
            {
                return; // 다음 프레임 계속 이동
            }

            if (descending) // 지하 방향 이동 확인
            {
                if (stairIndex < navigation.StairSteps.Count - 1) // 마지막 단 미도착 확인
                {
                    stairIndex = Mathf.Min(navigation.StairSteps.Count - 1, stairIndex + 3); // 여러 단씩 다음 목표 진행
                    return; // 다음 계단 이동 준비
                }
            }
            else // 지상 방향 이동 처리
            {
                if (stairIndex > 0) // 첫 단 미도착 확인
                {
                    stairIndex = Mathf.Max(0, stairIndex - 3); // 여러 단씩 이전 목표 진행
                    return; // 다음 계단 이동 준비
                }
            }

            Map24WorldLayer currentLayer = navigation.GetLayer(transform.position); // 계단 끝 현재 층 재판정
            if (currentLayer == targetLayer || PlanarDistance(transform.position, descending ? navigation.UndergroundAnchor : navigation.SurfaceAnchor) <= 1.8f) // 목표 층 도착 확인
            {
                layerTravelActive = false; // 계단 이동 종료
                hasUndergroundSearchTarget = false; // 이전 수색 목적지 제거
                verticalVelocity = 0f; // 보조 중력 초기화
                ReleaseOriginalAI(); // 기존 추격 AI 복구
            }
        }

        private void UpdateUndergroundSearch(Map24LayerNavigation navigation) // 지하 플랫폼·서비스 통로 분산 수색
        {
            TakeOriginalAIControl(); // 기존 자유 수색 일시 중지

            if (!hasUndergroundSearchTarget) // 현재 지하 수색 지점 확인
            {
                undergroundSearchTarget = navigation.GetUndergroundSearchPoint(GetInstanceID(), undergroundSearchIndex); // 경비별 수색 지점 선택
                undergroundSearchIndex++; // 다음 수색 순번 증가
                hasUndergroundSearchTarget = true; // 현재 수색 목적지 활성화
                searchDwellUntil = float.NegativeInfinity; // 도착 전 대기 초기화
            }

            float distance = PlanarDistance(transform.position, undergroundSearchTarget); // 수색 지점 거리 계산
            if (distance > searchReachDistance) // 미도착 확인
            {
                MoveToward(undergroundSearchTarget, UndergroundSearchSpeedForGuard()); // 병력별 지하 수색 이동
                ApplyGravity(); // 지하 Terrain 표면 유지
                return; // 이동 중 대기 생략
            }

            RotateSearchLook(); // 주변 방향 확인
            ApplyGravity(); // 지면 밀착 유지

            if (float.IsNegativeInfinity(searchDwellUntil)) // 첫 도착 확인
            {
                float variance = Mathf.Abs(GetInstanceID() % 5) * 0.10f; // 경비 동시 이동 방지
                float dwell = guard.UnitKind == MapWantedUnitKind.Heavy ? searchDwellTime + 0.45f : searchDwellTime; // E-03 느린 확인 시간 적용
                searchDwellUntil = Time.time + dwell + variance; // 수색 종료 시각 설정
            }

            if (Time.time >= searchDwellUntil) // 현재 수색 완료 확인
            {
                hasUndergroundSearchTarget = false; // 다음 지점 선택 허용
                searchDwellUntil = float.NegativeInfinity; // 대기 초기화
            }
        }

        private float TravelSpeedForGuard() // 병력별 계단 이동 속도
        {
            if (guard.UnitKind == MapWantedUnitKind.Heavy) return heavyTravelSpeed; // E-03 느린 계단 이동
            if (guard.UnitKind == MapWantedUnitKind.Elite) return eliteTravelSpeed; // E-02 빠른 계단 이동
            return regularTravelSpeed; // E-01 기본 계단 이동
        }

        private float UndergroundSearchSpeedForGuard() // 병력별 지하 수색 속도
        {
            return guard.UnitKind == MapWantedUnitKind.Heavy ? heavyUndergroundSearchSpeed : undergroundSearchSpeed; // E-03 느린 수색 적용
        }

        private void TakeOriginalAIControl() // Day24가 이동을 담당할 때 기존 AI 중지
        {
            if (guard != null && guard.enabled) // 기존 수배 AI 활성 확인
            {
                guard.enabled = false; // 직접 플레이어 추격 일시 중지
            }

            if (stability != null && stability.enabled) // Day23 정체 복구 활성 확인
            {
                stability.enabled = false; // 계단 이동 중 측면 보정 비활성화
            }
        }

        private void ReleaseOriginalAI() // 같은 층 추격 상태에서 기존 AI 복구
        {
            if (guard != null && !guard.enabled) // 기존 수배 AI 비활성 확인
            {
                guard.enabled = true; // Day21 수배 추격 AI 복구
            }

            if (stability != null && !stability.enabled) // Day23 정체 복구 비활성 확인
            {
                stability.enabled = true; // Day23 정체 복구 기능 복구
            }
        }

        private void MoveToward(Vector3 destination, float speed) // CharacterController 기반 보조 이동
        {
            Vector3 delta = destination - transform.position; // 목표 방향 계산
            delta.y = 0f; // 경사 충돌면을 사용하도록 수평 이동
            if (delta.sqrMagnitude <= 0.01f) // 이동 가능 방향 확인
            {
                return; // 이동 생략
            }

            Vector3 direction = delta.normalized; // 이동 방향 정규화
            controller.Move(direction * Mathf.Max(0f, speed) * Time.deltaTime); // 목표 방향 이동 적용
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up); // 목표 회전 계산
            float turnSpeed = guard.UnitKind == MapWantedUnitKind.Heavy ? 6f : 10f; // E-03 느린 계단 회전 적용
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime); // 이동 방향 회전
        }

        private void ApplyGravity() // CharacterController 지면 밀착 중력 적용
        {
            if (controller == null) // 컨트롤러 확인
            {
                return; // 중력 처리 생략
            }

            if (controller.isGrounded && verticalVelocity < 0f) // 지면 접촉 확인
            {
                verticalVelocity = -2f; // 지면 밀착 속도 적용
            }
            else // 공중 또는 경사 이동
            {
                verticalVelocity += -20f * Time.deltaTime; // 기본 중력 누적
            }

            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime); // 수직 이동 적용
        }

        private void RotateSearchLook() // 지하 수색 지점 주변 확인 회전
        {
            float sign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 경비별 회전 방향 분산
            float speed = guard != null && guard.UnitKind == MapWantedUnitKind.Heavy ? 70f : guard != null && guard.IsElite ? 120f : 95f; // 병력별 회전 속도 선택
            transform.Rotate(0f, sign * speed * Time.deltaTime, 0f, Space.World); // 수색 방향 회전 적용
        }

        private static float PlanarDistance(Vector3 first, Vector3 second) // XZ 수평 거리 계산
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)); // 수평 거리 반환
        }
    }
}
