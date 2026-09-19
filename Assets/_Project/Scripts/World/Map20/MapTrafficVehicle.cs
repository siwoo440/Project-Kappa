using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 차량 이동과 충돌 처리

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    [DisallowMultipleComponent] // 차량 AI 중복 방지
    public sealed class MapTrafficVehicle : MonoBehaviour // 도로 차선을 따라 움직이는 단일 차량
    {
        [SerializeField] private MapVehicleKind kind; // 차량 유형
        [SerializeField] private float cruiseSpeed = 11f; // 일반 주행 속도
        [SerializeField] private float acceleration = 5.5f; // 가속 속도
        [SerializeField] private float braking = 11f; // 감속 속도
        [SerializeField] private float turnSpeed = 8f; // 회전 보간 속도
        private MapCityLifeManager manager; // 교통 관리자 참조
        private int currentX; // 현재 교차로 가로 번호
        private int currentZ; // 현재 교차로 세로 번호
        private int targetX; // 다음 교차로 가로 번호
        private int targetZ; // 다음 교차로 세로 번호
        private MapTrafficDirection direction; // 현재 진행 방향
        private MapTrafficDirection pendingDirection; // 회전 이후 진행 방향
        private Vector3 targetPosition; // 현재 이동 목표 위치
        private float currentSpeed; // 현재 실제 이동 속도
        private float stuckTimer; // 장시간 정지 감지 시간
        private float targetDistanceLastFrame; // 이전 목표 거리
        private bool turning; // 교차로 안 회전 중 여부
        private int reservedIntersection = -1; // 현재 예약한 교차로 번호
        private bool initialized; // 런타임 초기화 여부
        public MapVehicleKind Kind => kind; // 차량 유형 조회
        public MapTrafficDirection Direction => direction; // 현재 진행 방향 조회
        public float CurrentSpeed => currentSpeed; // 현재 속도 조회
        public int CurrentX => currentX; // 현재 교차로 가로 번호 조회
        public int CurrentZ => currentZ; // 현재 교차로 세로 번호 조회

        public void Initialize(MapCityLifeManager owner, MapVehicleKind vehicleKind) // 풀 생성 뒤 차량 초기화
        {
            manager = owner; // 교통 관리자 저장
            kind = vehicleKind; // 차량 유형 저장
            cruiseSpeed = kind == MapVehicleKind.Cargo ? 8.5f : kind == MapVehicleKind.Delivery ? 10f : 12f; // 유형별 기본 속도 적용
            initialized = true; // 초기화 완료 상태 저장
        }

        public void ActivateAt(MapTrafficSpawn spawn) // 풀 차량을 실제 차선에 활성화
        {
            if (!initialized || manager == null) // 관리자 연결 확인
            {
                return; // 잘못된 활성화 중단
            }
            currentX = spawn.X; // 시작 교차로 가로 번호 저장
            currentZ = spawn.Z; // 시작 교차로 세로 번호 저장
            direction = spawn.Direction; // 시작 방향 저장
            pendingDirection = direction; // 회전 대기 방향 초기화
            turning = false; // 회전 상태 초기화
            reservedIntersection = -1; // 교차로 예약 초기화
            currentSpeed = Mathf.Min(cruiseSpeed, 5f); // 자연스러운 초기 속도 적용
            stuckTimer = 0f; // 막힘 시간 초기화
            float y = manager.RoadHeight; // 도로 표면 높이 조회
            transform.SetPositionAndRotation(MapTrafficMath.LanePoint(currentX, currentZ, direction, manager.World.WorldSize, y), Quaternion.LookRotation(MapTrafficMath.DirectionVector(direction), Vector3.up)); // 시작 차선 위치와 방향 적용
            SetNextIntersectionTarget(); // 첫 다음 교차로 목표 계산
            targetDistanceLastFrame = Vector3.Distance(transform.position, targetPosition); // 막힘 비교 거리 초기화
            gameObject.SetActive(true); // 풀 차량 표시
        }

        private void Update() // 차량 주행 상태 갱신
        {
            if (manager == null || manager.World == null || manager.World.Player == null) // 필수 관리자와 플레이어 확인
            {
                return; // 주행 처리 중단
            }
            if ((transform.position - manager.World.Player.transform.position).sqrMagnitude > manager.DespawnDistance * manager.DespawnDistance) // 플레이어와 지나치게 먼 차량 확인
            {
                manager.RecycleVehicle(this); // 멀어진 차량 풀 회수
                return; // 현재 프레임 처리 종료
            }
            float distance = Vector3.Distance(transform.position, targetPosition); // 현재 목표까지 거리 계산
            bool intersectionBlocked = !turning && distance < manager.IntersectionReserveDistance && !ReserveTargetIntersection(); // 교차로 진입 예약 상태 확인
            float clearance = manager.VehicleClearance(this, manager.VehicleScanDistance); // 앞차·플레이어·시민까지 남은 거리 조회
            float desiredSpeed = DesiredSpeed(clearance, intersectionBlocked); // 현재 교통 상태 목표 속도 계산
            float rate = desiredSpeed < currentSpeed ? braking : acceleration; // 가속과 감속 속도 선택
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * Time.deltaTime); // 실제 속도 부드럽게 조정
            MoveToTarget(distance); // 목표 방향 이동과 회전 처리
            UpdateStuckState(distance, desiredSpeed); // 장시간 교통 막힘 감지
        }

        private float DesiredSpeed(float clearance, bool intersectionBlocked) // 앞 장애물과 교차로를 고려한 목표 속도 계산
        {
            if (intersectionBlocked) // 교차로 예약 실패 확인
            {
                return 0f; // 교차로 앞 완전 정지
            }
            float stop = manager.VehicleStopDistance; // 완전 정지 안전 거리 조회
            float slow = manager.VehicleSlowDistance; // 감속 시작 거리 조회
            if (clearance <= stop) // 장애물이 너무 가까운지 확인
            {
                return 0f; // 충돌 방지 정지
            }
            if (clearance < slow) // 감속 구간 확인
            {
                return cruiseSpeed * Mathf.InverseLerp(stop, slow, clearance); // 거리에 비례해 감속
            }
            return cruiseSpeed; // 전방이 비어 있으면 정상 속도
        }

        private void MoveToTarget(float distance) // 목표 지점으로 차량 이동
        {
            if (distance <= 0.35f) // 현재 목표 도착 확인
            {
                ArriveTarget(); // 교차로 또는 회전점 도착 처리
                return; // 새 목표는 다음 프레임 이동
            }
            Vector3 delta = targetPosition - transform.position; // 목표 방향 벡터 계산
            delta.y = 0f; // 도로 평면 회전만 사용
            if (delta.sqrMagnitude > 0.001f) // 실제 회전 필요 여부 확인
            {
                Quaternion targetRotation = Quaternion.LookRotation(delta.normalized, Vector3.up); // 목표 방향 회전 계산
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime); // 회전 부드럽게 적용
            }
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime); // 목표까지 실제 좌표 이동
        }

        private void ArriveTarget() // 교차로와 회전점 도착 처리
        {
            if (turning) // 교차로 회전점 도착 확인
            {
                direction = pendingDirection; // 회전한 새 진행 방향 적용
                turning = false; // 회전 상태 종료
                ReleaseReservation(); // 교차로 점유 해제
                SetNextIntersectionTarget(); // 새 방향 다음 교차로 설정
                return; // 처리 종료
            }
            currentX = targetX; // 도착 교차로 가로 번호 적용
            currentZ = targetZ; // 도착 교차로 세로 번호 적용
            MapTrafficDirection next = manager.ChooseNextDirection(currentX, currentZ, direction); // 다음 진행 방향 선택
            if (next != direction) // 좌회전 또는 우회전 여부 확인
            {
                BeginTurn(next); // 교차로 안 회전점 이동 시작
                return; // 회전 완료 뒤 다음 도로 설정
            }
            ReleaseReservation(); // 직진 차량 교차로 점유 해제
            SetNextIntersectionTarget(); // 다음 직진 교차로 목표 설정
        }

        private void BeginTurn(MapTrafficDirection next) // 현재 교차로에서 새 차선으로 회전 시작
        {
            turning = true; // 교차로 회전 상태 적용
            pendingDirection = next; // 회전 후 진행 방향 저장
            targetPosition = MapTrafficMath.LanePoint(currentX, currentZ, next, manager.World.WorldSize, manager.RoadHeight); // 같은 교차로의 새 차선 지점 목표 설정
        }

        private void SetNextIntersectionTarget() // 현재 방향의 다음 교차로 목표 설정
        {
            if (!MapTrafficMath.CanAdvance(currentX, currentZ, direction)) // 현재 방향이 도시 밖으로 향하는지 확인
            {
                direction = manager.ChooseNextDirection(currentX, currentZ, direction); // 경계 안쪽 새 방향 선택
            }
            Vector2Int next = MapTrafficMath.Step(currentX, currentZ, direction); // 다음 교차로 번호 계산
            targetX = next.x; // 목표 가로 번호 저장
            targetZ = next.y; // 목표 세로 번호 저장
            targetPosition = MapTrafficMath.LanePoint(targetX, targetZ, direction, manager.World.WorldSize, manager.RoadHeight); // 다음 교차로 차선 위치 저장
        }

        private bool ReserveTargetIntersection() // 다음 교차로 점유 요청
        {
            int id = MapTrafficMath.IntersectionId(targetX, targetZ); // 목표 교차로 고유 번호 계산
            if (reservedIntersection == id) // 이미 같은 교차로 예약 여부 확인
            {
                return true; // 기존 예약 유지
            }
            if (reservedIntersection >= 0) // 이전 예약이 남아 있는지 확인
            {
                manager.ReleaseIntersection(reservedIntersection, this); // 이전 교차로 점유 해제
                reservedIntersection = -1; // 로컬 예약 초기화
            }
            if (!manager.TryReserveIntersection(id, this)) // 새 교차로 예약 시도
            {
                return false; // 다른 차량 점유 상태 반환
            }
            reservedIntersection = id; // 성공한 예약 번호 저장
            return true; // 진입 가능 반환
        }

        private void ReleaseReservation() // 현재 교차로 예약 안전 해제
        {
            if (reservedIntersection < 0) // 예약 없음 확인
            {
                return; // 해제 생략
            }
            manager.ReleaseIntersection(reservedIntersection, this); // 관리자 예약 해제
            reservedIntersection = -1; // 로컬 예약 초기화
        }

        private void UpdateStuckState(float distance, float desiredSpeed) // 교통 막힘과 위치 정체 감지
        {
            bool progressing = distance < targetDistanceLastFrame - 0.03f; // 목표 방향 실제 이동 여부 확인
            targetDistanceLastFrame = distance; // 다음 프레임 거리 저장
            if (desiredSpeed > 1f && currentSpeed < 0.25f && !progressing) // 이동해야 하지만 멈춰 있는 상태 확인
            {
                stuckTimer += Time.deltaTime; // 정체 시간 누적
            }
            else // 정상 대기 또는 이동 상태
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime * 2f); // 정체 시간 빠르게 해소
            }
            if (stuckTimer >= manager.StuckRecoverSeconds) // 장시간 막힘 기준 초과 확인
            {
                RecoverStuck(); // 풀 회수 후 다른 도로 재배치
            }
        }

        public void RecoverStuck() // 외부 검사에서도 호출 가능한 막힘 복구
        {
            ReleaseReservation(); // 교차로 점유가 남지 않도록 해제
            stuckTimer = 0f; // 정체 시간 초기화
            manager.RecoverVehicle(this); // 관리자에게 재배치 요청
        }

        private void OnDisable() // 풀 회수 시 교차로 점유 정리
        {
            if (manager != null) // 관리자 존재 확인
            {
                ReleaseReservation(); // 예약 교차로 해제
            }
            currentSpeed = 0f; // 비활성 차량 속도 초기화
            stuckTimer = 0f; // 비활성 차량 정체 시간 초기화
        }
    }
}
