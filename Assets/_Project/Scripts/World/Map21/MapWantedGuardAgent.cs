using ProjectK.Day20; // 도시 생활 개체와 보도 시작점 참조
using UnityEngine; // 자유 추격·수색·전투 처리

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 수배 경비 AI 중복 방지
    [RequireComponent(typeof(CharacterController))] // 경비 이동 충돌체 필수
    [RequireComponent(typeof(DetectionSensor))] // 플레이어 시야 센서 필수
    [RequireComponent(typeof(EnemyActor))] // 기존 적 체력 필수
    [RequireComponent(typeof(EnemyMeleeCombat))] // 기존 근접 공격 필수
    public sealed class MapWantedGuardAgent : MonoBehaviour // 횡단보도 그래프 없이 자유 추격·수색하는 수배 경비
    {
        private const float regularVisionDistance = 65f; // E-01 확장 시야 거리
        private const float eliteVisionDistance = 85f; // E-02 확장 시야 거리
        private const float regularVisionAngle = 160f; // E-01 확장 시야각
        private const float eliteVisionAngle = 180f; // E-02 확장 시야각
        private const float proximityDetectionDistance = 8f; // 근거리 전방향 즉시 인지 거리
        private const float movementProbeDistance = 2.8f; // 자유 이동 장애물 검사 거리
        private const float movementProbeRadius = 0.30f; // 자유 이동 장애물 검사 반경

        private MapWantedResponseManager manager; // 증원·수색 관리자
        private MapWantedSystem wanted; // 현재 수배 상태
        private CharacterController controller; // 경비 이동 충돌체
        private DetectionSensor sensor; // 기존 시야·청각 센서
        private EnemyActor actor; // 기존 적 체력 관리자
        private EnemyMeleeCombat melee; // 기존 적 근접 공격
        private PlayerHealth playerHealth; // 플레이어 체력 참조
        private int searchStep; // 개체별 수색 지점 순번
        private bool elite; // E-02 정예 여부
        private bool hasSearchDestination; // 현재 자유 수색 목표 존재 여부
        private bool wasSharedPursuit; // 직전 공유 추적 상태
        private float moveSpeed; // 현재 추적 이동 속도
        private float attackDistance; // 근접 공격 시작 거리
        private float deadAt = float.PositiveInfinity; // 사망 시각
        private float verticalVelocity; // CharacterController 중력 속도
        private float nextSearchChange = float.PositiveInfinity; // 다음 수색 위치 전환 시각
        private float searchLookYaw; // 수색 중 주변 확인 회전값
        private Vector3 searchDestination; // 각 경비마다 다른 자유 수색 목적지

        public bool IsElite => elite; // 정예 여부 조회
        public bool IsDead => actor != null && actor.IsDead; // 사망 여부 조회
        public DetectionSensor Sensor => sensor; // 수배 관리자의 시야 검사 참조
        public CharacterController Controller => controller; // 경비끼리 충돌 무시 연결용 컨트롤러
        public bool ReadyToRecycle => IsDead && Time.time - deadAt >= 12f; // 시체 유지 후 풀 복귀 가능 여부

        private void Awake() // 기존 전투 컴포넌트 연결
        {
            controller = GetComponent<CharacterController>(); // 이동 충돌체 조회
            sensor = GetComponent<DetectionSensor>(); // 탐지 센서 조회
            actor = GetComponent<EnemyActor>(); // 적 생명 관리자 조회
            melee = GetComponent<EnemyMeleeCombat>(); // 근접 공격 관리자 조회
        }

        public void Initialize(MapWantedResponseManager owner, bool isElite) // 풀 생성 시 경비 종류와 관리자 연결
        {
            manager = owner; // 증원 관리자 저장
            wanted = owner != null ? owner.Wanted : null; // 수배 관리자 저장
            elite = isElite; // 경비 등급 저장
            moveSpeed = elite ? 6.4f : 5.4f; // 자유 추격 속도 설정
            attackDistance = elite ? 2.6f : 2.3f; // 근접 공격 거리 설정
        }

        public void ActivateAt(int nodeIndex) // 풀 경비를 보도 시작점에 배치
        {
            if (manager == null || manager.Graph == null || wanted == null || wanted.Player == null) // 필수 참조 확인
            {
                return; // 잘못된 활성화 중단
            }
            searchStep = 0; // 수색 순번 초기화
            hasSearchDestination = false; // 이전 수색 목표 제거
            wasSharedPursuit = false; // 공유 추적 상태 초기화
            nextSearchChange = float.PositiveInfinity; // 수색 전환 시각 초기화
            searchLookYaw = Mathf.Abs(GetInstanceID() % 360); // 개체별 다른 탐색 시작 방향
            transform.position = manager.Graph.Get(nodeIndex).Position; // 생성 위치만 기존 보행 노드 사용
            transform.rotation = Quaternion.identity; // 사망 기울기 제거
            playerHealth = wanted.Player.GetComponent<PlayerHealth>(); // 플레이어 체력 연결
            if (actor != null) // 기존 적 체력 관리자 확인
            {
                actor.enabled = true; // 풀 재사용 적 체력 기능 활성화
                actor.Configure(elite ? 160f : 100f, elite ? 140f : 100f, true); // E-01/E-02 체력·자세 설정
            }
            if (sensor != null) // 탐지 센서 확인
            {
                sensor.enabled = true; // 사망 시 꺼졌던 센서 복구
                sensor.Configure(wanted.Player.transform, elite ? eliteVisionDistance : regularVisionDistance, elite ? eliteVisionAngle : regularVisionAngle, elite ? 38f : 32f, ~0); // 대폭 확장한 시야·청각 적용
            }
            if (melee != null) // 근접 전투 확인
            {
                melee.enabled = true; // 사망 시 꺼졌던 전투 기능 복구
                Transform sword = transform.Find("Model/SwordSocket"); // 생성 프리팹 검 소켓 조회
                melee.Configure(sword, elite ? 28f : 18f, elite ? 42f : 30f); // 정예 공격력 강화
            }
            MapGuardCrimeTag tag = GetComponent<MapGuardCrimeTag>(); // 경비 범죄 태그 조회
            if (tag != null) // 태그 존재 확인
            {
                tag.enabled = true; // 풀 재사용 신고 상태 복구
            }
            deadAt = float.PositiveInfinity; // 사망 시각 초기화
            verticalVelocity = 0f; // 중력 상태 초기화
            gameObject.SetActive(true); // 실제 경비 활성화
        }

        private void Update() // 직접 발견·공유 추적·자유 수색·전투 갱신
        {
            if (manager == null || wanted == null || wanted.Player == null || actor == null) // 필수 참조 확인
            {
                return; // 행동 처리 중단
            }
            if (actor.IsDead) // 경비 사망 확인
            {
                if (float.IsPositiveInfinity(deadAt)) // 최초 사망 프레임 확인
                {
                    deadAt = Time.time; // 시체 유지 시작 시각 저장
                }
                return; // 사망 후 이동 중단
            }
            if (wanted.Stars <= 0) // 수배 해제 확인
            {
                ApplyGravity(); // 지면 고정만 유지
                return; // 추적 행동 중단
            }

            float playerDistance = PlanarDistance(transform.position, wanted.Player.transform.position); // 플레이어 거리 계산
            bool sensorVisible = sensor != null && sensor.TargetVisible; // 기존 센서 시야 확인
            bool wideVisible = HasWideLineOfSight(playerDistance); // 확장된 시야 거리·각도 확인
            bool visible = sensorVisible || wideVisible; // 기존 센서와 확장 감지를 통합

            if (visible) // 한 경비가 플레이어를 직접 찾은 상태
            {
                wanted.BroadcastDetection(wanted.Player.transform.position); // 전체 수배 경비에 정확한 위치를 5초 공유
                hasSearchDestination = false; // 자유 수색 목표 해제
                wasSharedPursuit = true; // 직접 발견도 공유 추적 흐름으로 표시
                if (playerHealth != null && !playerHealth.IsDead && playerDistance <= attackDistance) // 근접 공격 거리 확인
                {
                    RotateTowards(wanted.Player.transform.position - transform.position); // 플레이어 방향 회전
                    if (melee != null && !melee.IsBusy) // 공격 중복 확인
                    {
                        melee.TryStartAttack(playerHealth); // 기존 적 검 공격 실행
                    }
                    ApplyGravity(); // 공격 중 지면 유지
                    return; // 이동 처리 생략
                }
                MoveFreely(wanted.Player.transform.position, moveSpeed); // 횡단보도 없이 플레이어를 직접 추격
                ApplyGravity(); // CharacterController 지면 유지
                return; // 직접 추격 처리 종료
            }

            if (wanted.HasSharedTarget) // 다른 경비가 최근 발견한 정확한 위치 공유 여부 확인
            {
                wasSharedPursuit = true; // 공유 추적 상태 저장
                hasSearchDestination = false; // 공유 중 자유 수색 목표 해제
                MoveFreely(wanted.SharedTargetPosition, moveSpeed * 0.96f); // 횡단보도 없이 공유 위치로 직접 이동
                ApplyGravity(); // CharacterController 지면 유지
                return; // 공유 추적 처리 종료
            }

            if (wasSharedPursuit) // 정확한 위치 공유 시간이 막 끝났는지 확인
            {
                hasSearchDestination = false; // 각자 새 자유 수색 위치를 선택하도록 초기화
                wasSharedPursuit = false; // 분산 수색 상태로 전환
            }

            SearchAroundLastKnown(); // 마지막 위치 주변을 횡단보도 제약 없이 자유 수색
            ApplyGravity(); // CharacterController 지면 유지
        }

        private bool HasWideLineOfSight(float playerDistance) // 넓어진 거리·각도와 근거리 360도 인지
        {
            if (wanted == null || wanted.Player == null) // 플레이어 참조 확인
            {
                return false; // 시야 실패
            }
            float visionDistance = elite ? eliteVisionDistance : regularVisionDistance; // 등급별 최대 시야 거리 선택
            if (playerDistance > visionDistance) // 최대 거리 확인
            {
                return false; // 시야 거리 밖
            }
            Vector3 start = transform.position + Vector3.up * 1.50f; // 경비 눈높이 근사 위치
            Vector3 end = wanted.Player.transform.position + Vector3.up * 1.0f; // 플레이어 몸 중심 근사 위치
            Vector3 direction = end - start; // 플레이어 방향 계산
            Vector3 planar = direction; // 수평 각도 계산용 복사
            planar.y = 0f; // 높이 차이를 시야각에서 제외
            if (playerDistance > proximityDetectionDistance && planar.sqrMagnitude > 0.001f) // 근거리 360도 밖에서만 시야각 검사
            {
                float angle = Vector3.Angle(transform.forward, planar.normalized); // 경비 전방과 플레이어 각도 계산
                float limit = (elite ? eliteVisionAngle : regularVisionAngle) * 0.5f; // 실제 좌우 반각 계산
                if (angle > limit) // 넓어진 시야각 바깥 확인
                {
                    return false; // 시야 실패
                }
            }
            return HasClearSightIgnoringDynamicActors(start, end); // 벽·건물만 시야를 막도록 검사
        }

        private bool HasClearSightIgnoringDynamicActors(Vector3 start, Vector3 end) // 경비·시민·차량 때문에 서로 시야를 가리지 않도록 검사
        {
            Vector3 delta = end - start; // 검사 구간 계산
            float distance = delta.magnitude; // 실제 시야 거리 계산
            if (distance <= 0.01f) // 동일 위치 확인
            {
                return true; // 즉시 인지
            }
            RaycastHit[] hits = Physics.RaycastAll(start, delta / distance, distance - 0.05f, ~0, QueryTriggerInteraction.Ignore); // 플레이어 앞까지 장애물 수집
            foreach (RaycastHit hit in hits) // 시야 장애물 후보 순회
            {
                Collider collider = hit.collider; // 현재 충돌체 조회
                if (collider == null || EquipmentTargeting.IsOwnCollider(collider, transform)) // 자기 몸 제외
                {
                    continue; // 다음 충돌체 검사
                }
                Transform hitTransform = collider.transform; // 충돌체 트랜스폼 조회
                if (hitTransform == wanted.Player.transform || hitTransform.IsChildOf(wanted.Player.transform)) // 플레이어 몸 확인
                {
                    continue; // 목표 플레이어는 장애물에서 제외
                }
                if (IsSightIgnorableDynamic(collider)) // 다른 생활 개체·수배 경비 확인
                {
                    continue; // 동적 군중 때문에 시야를 잃지 않음
                }
                return false; // 건물·벽·고정 구조물에 시야가 막힘
            }
            return true; // 직접 시야 확보
        }

        private static bool IsSightIgnorableDynamic(Collider collider) // 시야에서 무시할 동적 도시 개체 판정
        {
            if (collider == null) // 충돌체 확인
            {
                return false; // 무시 대상 아님
            }
            if (collider.GetComponentInParent<MapWantedGuardAgent>() != null) // 다른 수배 경비 확인
            {
                return true; // 동료 경비는 시야를 가리지 않음
            }
            if (collider.GetComponentInParent<MapCitizenAgent>() != null) // 시민 확인
            {
                return true; // 군중은 시야를 완전히 차단하지 않음
            }
            return collider.GetComponentInParent<MapTrafficVehicle>() != null; // 이동 차량도 시야 완전 차단에서 제외
        }

        private void SearchAroundLastKnown() // 공유 위치가 끝난 뒤 주변 자유 수색
        {
            if (!hasSearchDestination) // 현재 수색 목적지 존재 확인
            {
                AssignSearchDestination(); // 개체별 자유 수색 지점 선택
            }
            float distance = PlanarDistance(transform.position, searchDestination); // 현재 수색 지점까지 거리 계산
            if (distance > 1.5f) // 수색 지점에 아직 도착하지 않았는지 확인
            {
                MoveFreely(searchDestination, moveSpeed * 0.80f); // 횡단보도 없이 직접 수색 지점으로 이동
                return; // 이동 중 회전 수색 생략
            }
            RotateSearchPattern(); // 제자리에서 주변 방향을 훑어 시야각 변경
            if (float.IsPositiveInfinity(nextSearchChange)) // 이번 지점에 처음 도착했는지 확인
            {
                float dwell = elite ? 1.1f : 1.5f; // 정예 경비는 더 빠르게 다음 지점 확인
                float variance = Mathf.Abs(GetInstanceID() % 7) * 0.08f; // 모든 경비의 동시 이동 방지
                nextSearchChange = Time.time + dwell + variance; // 현재 지점 수색 종료 시각 예약
            }
            if (Time.time >= nextSearchChange) // 현재 수색 지점 확인 완료 여부
            {
                AssignSearchDestination(); // 다음 자유 수색 지점 선택
            }
        }

        private void AssignSearchDestination() // 마지막 위치 주변 개체별 자유 수색 슬롯 선택
        {
            searchDestination = manager.GetSearchDestination(this, wanted.LastKnownPosition, searchStep); // 그래프 스냅 없는 수색 위치 조회
            searchStep++; // 다음 수색 순번 증가
            hasSearchDestination = true; // 현재 수색 목적지 활성화
            nextSearchChange = float.PositiveInfinity; // 목적지 도착 전에는 다시 바꾸지 않음
        }

        private void RotateSearchPattern() // 수색 지점에서 넓은 시야를 계속 회전
        {
            float directionSign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 절반은 시계·절반은 반시계 방향
            searchLookYaw += directionSign * (elite ? 130f : 105f) * Time.deltaTime; // 넓은 시야와 함께 빠르게 주변 수색
            Vector3 direction = Quaternion.Euler(0f, searchLookYaw, 0f) * Vector3.forward; // 현재 수색 방향 계산
            RotateTowards(direction); // 경비 몸과 시야 방향 회전
        }

        private void MoveFreely(Vector3 destination, float speed) // 횡단보도·보행 노드 없이 도로와 보도를 직접 가로질러 이동
        {
            Vector3 delta = destination - transform.position; // 목적지 방향 계산
            delta.y = 0f; // 평면 자유 이동만 사용
            if (delta.sqrMagnitude <= 0.20f) // 목적지에 충분히 가까운지 확인
            {
                return; // 이동 중단
            }
            Vector3 desired = delta.normalized; // 가장 빠른 직접 방향 계산
            Vector3 direction = FindFreeDirection(desired); // 건물·고정 구조물만 간단히 우회
            controller.Move(direction * Mathf.Max(0f, speed) * Time.deltaTime); // 횡단보도 제약 없는 직접 이동 적용
            RotateTowards(direction); // 실제 이동 방향으로 회전
        }

        private Vector3 FindFreeDirection(Vector3 desired) // 앞이 막히면 좌우로 간단히 틀어 자유 이동 유지
        {
            if (IsMoveDirectionOpen(desired)) // 정면 이동 가능 확인
            {
                return desired; // 가장 빠른 직접 방향 사용
            }
            float sign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 경비마다 우회 우선 방향 분산
            float[] angles = { 32f, -32f, 62f, -62f, 95f, -95f }; // 단계별 우회 각도 후보
            foreach (float raw in angles) // 우회 후보 순회
            {
                float angle = raw * sign; // 개체별 우선 방향 반영
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desired; // 우회 방향 계산
                if (IsMoveDirectionOpen(candidate)) // 해당 방향 이동 가능 확인
                {
                    return candidate.normalized; // 첫 통과 가능한 방향 사용
                }
            }
            return desired; // 완전히 막힌 경우 CharacterController 슬라이딩에 맡김
        }

        private bool IsMoveDirectionOpen(Vector3 direction) // 자유 이동 앞쪽의 고정 장애물 검사
        {
            Vector3 origin = transform.position + Vector3.up * 0.85f; // 허리 높이 검사 원점
            RaycastHit[] hits = Physics.SphereCastAll(origin, movementProbeRadius, direction.normalized, movementProbeDistance, ~0, QueryTriggerInteraction.Ignore); // 전방 짧은 범위 장애물 조회
            foreach (RaycastHit hit in hits) // 장애물 후보 순회
            {
                Collider collider = hit.collider; // 현재 충돌체 조회
                if (collider == null || EquipmentTargeting.IsOwnCollider(collider, transform)) // 자기 몸 제외
                {
                    continue; // 다음 충돌체 검사
                }
                if (IsSightIgnorableDynamic(collider)) // 경비·시민·차량 같은 동적 개체 확인
                {
                    continue; // 이동 경로 결정에서는 군중을 고정 벽처럼 보지 않음
                }
                if (wanted != null && wanted.Player != null) // 플레이어 참조 확인
                {
                    Transform hitTransform = collider.transform; // 충돌체 트랜스폼 조회
                    if (hitTransform == wanted.Player.transform || hitTransform.IsChildOf(wanted.Player.transform)) // 목표 플레이어 확인
                    {
                        continue; // 추격 대상은 장애물에서 제외
                    }
                }
                return false; // 고정 구조물에 막힌 방향
            }
            return true; // 자유 이동 가능한 방향
        }

        private void RotateTowards(Vector3 direction) // 경비 진행 방향 회전
        {
            direction.y = 0f; // 수직 성분 제거
            if (direction.sqrMagnitude <= 0.0001f) // 유효 방향 확인
            {
                return; // 회전 생략
            }
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 회전 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 11f * Time.deltaTime); // 빠른 추격 회전 적용
        }

        private void ApplyGravity() // CharacterController 지면 유지
        {
            if (controller == null) // 컨트롤러 확인
            {
                return; // 중력 처리 생략
            }
            if (controller.isGrounded && verticalVelocity < 0f) // 지면 접촉 확인
            {
                verticalVelocity = -2f; // 지면 밀착 속도 적용
            }
            else // 공중 상태 처리
            {
                verticalVelocity += -20f * Time.deltaTime; // 기본 중력 누적
            }
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime); // 수직 이동 적용
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) // 평면 거리 계산
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)); // XZ 거리 반환
        }
    }
}
