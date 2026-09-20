using System; // 난수와 배열 처리
using System.Collections.Generic; // 풀과 교차로 예약 관리
using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 런타임 차량·시민 관리

using ProjectK.Day28; // Day28 교통 공간 인덱스 참조
namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    [DisallowMultipleComponent] // 도시 생활 관리자 중복 방지
    public sealed class MapCityLifeManager : MonoBehaviour // 차량과 시민 풀링·교통 흐름 관리자
    {
        [Header("World")] // 월드 참조 구분
        [SerializeField] private MapWorldRoot world; // 본편 월드
        [SerializeField] private string sourceCommit; // 제작 기준 커밋

        [Header("Vehicle Prefabs")] // 차량 프리팹 구분
        [SerializeField] private MapTrafficVehicle civilianVehiclePrefab; // 일반 승용차 프리팹
        [SerializeField] private MapTrafficVehicle deliveryVehiclePrefab; // 배달 차량 프리팹
        [SerializeField] private MapTrafficVehicle cargoVehiclePrefab; // 화물 차량 프리팹

        [Header("Citizen Prefabs")] // 시민 프리팹 구분
        [SerializeField] private MapCitizenAgent humanCitizenPrefab; // 인간 시민 프리팹
        [SerializeField] private MapCitizenAgent androidCitizenPrefab; // 안드로이드 시민 프리팹
        [SerializeField] private MapCitizenAgent mechanicalCitizenPrefab; // 기계화 시민 프리팹

        [Header("Traffic")] // 교통 밀도 설정
        [SerializeField, Range(4, 32)] private int targetVehicleCount = 18; // 동시에 활성화할 차량 목표 수
        [SerializeField, Range(8, 64)] private int vehiclePoolSize = 24; // 생성해 둘 차량 풀 크기
        [SerializeField] private float vehicleSpawnMinimum = 45f; // 플레이어 근처 생성 금지 거리
        [SerializeField] private float vehicleSpawnMaximum = 230f; // 차량 생성 후보 최대 거리
        [SerializeField] private float vehicleDespawnDistance = 330f; // 차량 풀 회수 거리
        [SerializeField] private float vehicleScanDistance = 18f; // 차량 전방 검사 거리
        [SerializeField] private float vehicleSlowDistance = 11f; // 차량 감속 시작 거리
        [SerializeField] private float vehicleStopDistance = 4.8f; // 차량 완전 정지 거리
        [SerializeField] private float intersectionReserveDistance = 10f; // 교차로 예약 시작 거리
        [SerializeField] private float stuckRecoverSeconds = 7f; // 막힌 차량 재배치 시간

        [Header("Citizens")] // 시민 밀도 설정
        [SerializeField, Range(8, 64)] private int targetCitizenCount = 32; // 동시에 활성화할 시민 목표 수
        [SerializeField, Range(16, 96)] private int citizenPoolSize = 48; // 생성해 둘 시민 풀 크기
        [SerializeField] private float citizenSpawnMinimum = 20f; // 플레이어 바로 앞 시민 생성 금지 거리
        [SerializeField] private float citizenSpawnMaximum = 180f; // 시민 생성 후보 최대 거리
        [SerializeField] private float citizenDespawnDistance = 250f; // 시민 풀 회수 거리
        [SerializeField] private float crosswalkSafetyRadius = 12f; // 횡단보도 차량 확인 거리

        [Header("Runtime")] // 런타임 갱신 설정
        [SerializeField] private float maintenanceInterval = 0.75f; // 풀 유지 검사 간격
        private readonly List<MapTrafficVehicle> vehiclePool = new List<MapTrafficVehicle>(); // 모든 차량 풀
        private readonly List<MapCitizenAgent> citizenPool = new List<MapCitizenAgent>(); // 모든 시민 풀
        private readonly Dictionary<int, MapTrafficVehicle> intersectionReservations = new Dictionary<int, MapTrafficVehicle>(); // 교차로 점유 차량
        private System.Random random; // 재현 가능한 런타임 난수
        private MapPedestrianGraph pedestrianGraph; // 보도·횡단보도 그래프
        private float nextMaintenance; // 다음 풀 유지 시각
        private bool initialized; // 풀 초기화 완료 여부
        public MapWorldRoot World => world; // 차량과 시민이 참조할 본편 월드
        public string SourceCommit => sourceCommit; // 제작 기준 커밋 조회
        public MapPedestrianGraph PedestrianGraph => pedestrianGraph; // 시민 보행 그래프 조회
        public float RoadHeight => MapTerrainMath.Ground + 0.20f; // 도로 표면 위 차량 높이
        public float DespawnDistance => vehicleDespawnDistance; // 차량 회수 거리 조회
        public float CitizenDespawnDistance => citizenDespawnDistance; // 시민 회수 거리 조회
        public float VehicleScanDistance => vehicleScanDistance; // 차량 전방 검사 거리 조회
        public float VehicleSlowDistance => vehicleSlowDistance; // 차량 감속 거리 조회
        public float VehicleStopDistance => vehicleStopDistance; // 차량 정지 거리 조회
        public float IntersectionReserveDistance => intersectionReserveDistance; // 교차로 예약 거리 조회
        public float StuckRecoverSeconds => stuckRecoverSeconds; // 차량 막힘 복구 시간 조회
        public int TargetVehicleCount => targetVehicleCount; // 검사 메뉴용 차량 목표 수
        public int TargetCitizenCount => targetCitizenCount; // 검사 메뉴용 시민 목표 수
        public int VehiclePoolSize => vehiclePoolSize; // 검사 메뉴용 차량 풀 크기
        public int CitizenPoolSize => citizenPoolSize; // 검사 메뉴용 시민 풀 크기

        public void Configure(MapWorldRoot owner, string commit, MapTrafficVehicle civilian, MapTrafficVehicle delivery, MapTrafficVehicle cargo, MapCitizenAgent human, MapCitizenAgent android, MapCitizenAgent mechanical) // 에디터 설치 시 전체 프리팹과 월드 연결
        {
            world = owner; // 본편 월드 저장
            sourceCommit = commit; // 기준 커밋 저장
            civilianVehiclePrefab = civilian; // 일반 차량 프리팹 저장
            deliveryVehiclePrefab = delivery; // 배달 차량 프리팹 저장
            cargoVehiclePrefab = cargo; // 화물 차량 프리팹 저장
            humanCitizenPrefab = human; // 인간 시민 프리팹 저장
            androidCitizenPrefab = android; // 안드로이드 시민 프리팹 저장
            mechanicalCitizenPrefab = mechanical; // 기계화 시민 프리팹 저장
        }

        private void Awake() // 런타임 그래프와 난수 초기화
        {
            if (world == null) // 직렬화 월드 누락 확인
            {
                world = GetComponentInParent<MapWorldRoot>(); // 부모 월드에서 자동 복구
            }
            int seed = sourceCommit != null ? sourceCommit.GetHashCode() : 2020; // 기준 커밋 기반 난수 시드 계산
            random = new System.Random(seed); // 차량·시민 분산용 난수 생성
            float pedestrianY = MapTerrainMath.Ground + 0.24f; // 보도 표면 위 시민 발 위치 계산
            pedestrianGraph = MapPedestrianGraph.Build(world != null ? world.WorldSize : 1536f, pedestrianY); // 전체 보행 그래프 생성
        }

        private void Start() // 풀 생성과 초기 도시 생활 시작
        {
            InitializePools(); // 차량과 시민 풀 최초 생성
            MaintainPopulation(true); // 시작 프레임 주변 생활 인구 배치
        }

        private void Update() // 일정 간격의 활성 개체 유지
        {
            if (!initialized || world == null || world.Player == null || Time.unscaledTime < nextMaintenance) // 유지 검사 시점 확인
            {
                return; // 다음 검사까지 대기
            }
            nextMaintenance = Time.unscaledTime + maintenanceInterval; // 다음 검사 시각 예약
            MaintainPopulation(false); // 차량·시민 목표 수 유지
        }

        private void InitializePools() // 차량과 시민 프리팹을 미리 생성
        {
            if (initialized) // 중복 초기화 확인
            {
                return; // 기존 풀 유지
            }
            Transform vehicleRoot = new GameObject("VehiclePool").transform; // 차량 풀 부모 생성
            vehicleRoot.SetParent(transform, false); // 도시 생활 루트에 연결
            for (int i = 0; i < vehiclePoolSize; i++) // 차량 풀 크기만큼 생성
            {
                MapVehicleKind kind = VehicleKindForIndex(i); // 인덱스별 차량 종류 선택
                MapTrafficVehicle prefab = VehiclePrefab(kind); // 해당 차량 프리팹 조회
                MapTrafficVehicle item = Instantiate(prefab, vehicleRoot); // 차량 프리팹 인스턴스 생성
                item.name = "Traffic_" + kind + "_" + i.ToString("D2"); // 풀 식별 이름 지정
                item.Initialize(this, kind); // 차량 AI 관리자 연결
                item.gameObject.SetActive(false); // 첫 배치 전 비활성화
                vehiclePool.Add(item); // 풀 목록 저장
            }
            Transform citizenRoot = new GameObject("CitizenPool").transform; // 시민 풀 부모 생성
            citizenRoot.SetParent(transform, false); // 도시 생활 루트에 연결
            for (int i = 0; i < citizenPoolSize; i++) // 시민 풀 크기만큼 생성
            {
                MapCitizenKind kind = CitizenKindForIndex(i); // 인덱스별 시민 유형 선택
                MapCitizenAgent prefab = CitizenPrefab(kind); // 해당 시민 프리팹 조회
                MapCitizenAgent item = Instantiate(prefab, citizenRoot); // 시민 프리팹 인스턴스 생성
                item.name = "Citizen_" + kind + "_" + i.ToString("D2"); // 풀 식별 이름 지정
                item.Initialize(this, kind); // 시민 AI 관리자 연결
                item.gameObject.SetActive(false); // 첫 배치 전 비활성화
                citizenPool.Add(item); // 풀 목록 저장
            }
            initialized = true; // 풀 초기화 완료
        }

        private void MaintainPopulation(bool fillImmediately) // 목표 차량·시민 수 유지
        {
            int activeVehicles = CountActiveVehicles(); // 현재 활성 차량 수 조회
            int activeCitizens = CountActiveCitizens(); // 현재 활성 시민 수 조회
            int vehicleBudget = fillImmediately ? targetVehicleCount : Mathf.Min(3, targetVehicleCount - activeVehicles); // 프레임당 차량 생성 제한
            int citizenBudget = fillImmediately ? targetCitizenCount : Mathf.Min(5, targetCitizenCount - activeCitizens); // 프레임당 시민 생성 제한
            for (int i = 0; i < vehicleBudget && activeVehicles < targetVehicleCount; i++) // 부족 차량 배치
            {
                if (TrySpawnVehicle()) // 차량 한 대 배치 성공 확인
                {
                    activeVehicles++; // 활성 수 갱신
                }
            }
            for (int i = 0; i < citizenBudget && activeCitizens < targetCitizenCount; i++) // 부족 시민 배치
            {
                if (TrySpawnCitizen()) // 시민 한 명 배치 성공 확인
                {
                    activeCitizens++; // 활성 수 갱신
                }
            }
        }

        private bool TrySpawnVehicle() // 플레이어 주변 보이지 않는 차선에 차량 배치
        {
            MapTrafficVehicle item = FirstInactiveVehicle(); // 사용 가능한 풀 차량 조회
            if (item == null || world == null || world.Player == null) // 풀 여유와 플레이어 확인
            {
                return false; // 차량 생성 실패
            }
            Vector3 player = world.Player.transform.position; // 현재 플레이어 위치 조회
            for (int attempt = 0; attempt < 48; attempt++) // 적절한 도로 후보 반복 탐색
            {
                int x = random.Next(1, MapTrafficMath.RoadCount - 1); // 외곽을 제외한 교차로 가로 번호 선택
                int z = random.Next(1, MapTrafficMath.RoadCount - 1); // 외곽을 제외한 교차로 세로 번호 선택
                MapTrafficDirection direction = (MapTrafficDirection)random.Next(0, 4); // 시작 방향 무작위 선택
                if (!MapTrafficMath.CanAdvance(x, z, direction)) // 다음 차선 존재 확인
                {
                    continue; // 도시 밖 방향 제외
                }
                Vector3 point = MapTrafficMath.LanePoint(x, z, direction, world.WorldSize, RoadHeight); // 후보 차선 위치 계산
                float distance = HorizontalDistance(player, point); // 플레이어와 수평 거리 계산
                if (distance < vehicleSpawnMinimum || distance > vehicleSpawnMaximum) // 생성 가능 거리 범위 확인
                {
                    continue; // 너무 가깝거나 먼 후보 제외
                }
                if (HasVehicleNear(point, 9f)) // 기존 차량과 겹치는지 확인
                {
                    continue; // 중복 차량 배치 방지
                }
                item.ActivateAt(new MapTrafficSpawn(x, z, direction)); // 선택 차선에 차량 활성화
                return true; // 차량 생성 성공
            }
            return false; // 적절한 차선 후보 없음
        }

        private bool TrySpawnCitizen() // 플레이어 주변 보도에 시민 배치
        {
            MapCitizenAgent item = FirstInactiveCitizen(); // 사용 가능한 시민 풀 조회
            if (item == null || world == null || world.Player == null || pedestrianGraph == null) // 필수 참조 확인
            {
                return false; // 시민 생성 실패
            }
            Vector3 player = world.Player.transform.position; // 현재 플레이어 위치 조회
            for (int attempt = 0; attempt < 64; attempt++) // 보도 후보 반복 탐색
            {
                int node = random.Next(0, pedestrianGraph.Count); // 무작위 보행 노드 선택
                Vector3 point = pedestrianGraph.Get(node).Position; // 후보 시민 위치 조회
                float distance = HorizontalDistance(player, point); // 플레이어와 수평 거리 계산
                if (distance < citizenSpawnMinimum || distance > citizenSpawnMaximum) // 시민 생성 거리 확인
                {
                    continue; // 화면 바로 앞과 먼 후보 제외
                }
                if (HasCitizenNear(point, 2.2f)) // 다른 시민과 겹침 확인
                {
                    continue; // 시민 겹침 방지
                }
                item.ActivateAt(node); // 선택 보도 노드에 시민 활성화
                return true; // 시민 생성 성공
            }
            return false; // 적절한 보도 후보 없음
        }

        public bool TryReserveIntersection(int id, MapTrafficVehicle vehicle) // 차량 교차로 점유 요청
        {
            if (vehicle == null) // 요청 차량 확인
            {
                return false; // 잘못된 예약 거부
            }
            if (intersectionReservations.TryGetValue(id, out MapTrafficVehicle owner)) // 현재 점유 차량 확인
            {
                if (owner == null || !owner.gameObject.activeInHierarchy) // 회수된 차량의 오래된 예약 확인
                {
                    intersectionReservations[id] = vehicle; // 새 차량으로 점유 교체
                    return true; // 예약 성공
                }
                return owner == vehicle; // 같은 차량의 중복 예약만 허용
            }
            intersectionReservations.Add(id, vehicle); // 비어 있는 교차로 점유
            return true; // 예약 성공
        }

        public void ReleaseIntersection(int id, MapTrafficVehicle vehicle) // 차량 교차로 예약 해제
        {
            if (!intersectionReservations.TryGetValue(id, out MapTrafficVehicle owner)) // 현재 예약 존재 확인
            {
                return; // 해제할 예약 없음
            }
            if (owner == vehicle || owner == null) // 요청 차량 소유 또는 오래된 예약 확인
            {
                intersectionReservations.Remove(id); // 교차로 예약 제거
            }
        }

        public MapTrafficDirection ChooseNextDirection(int x, int z, MapTrafficDirection current) // 교차로에서 직진 우선 다음 방향 선택
        {
            List<MapTrafficDirection> options = new List<MapTrafficDirection>(3); // 유효 방향 후보 준비
            MapTrafficDirection opposite = MapTrafficMath.Opposite(current); // 유턴 방향 조회
            foreach (MapTrafficDirection candidate in Enum.GetValues(typeof(MapTrafficDirection))) // 네 방향 순회
            {
                if (candidate == opposite || !MapTrafficMath.CanAdvance(x, z, candidate)) // 유턴과 도시 밖 방향 확인
                {
                    continue; // 후보 제외
                }
                options.Add(candidate); // 유효 방향 추가
            }
            if (options.Count == 0) // 유턴 외 선택이 없는 특수 경계 확인
            {
                return opposite; // 도시 안쪽 유턴으로 복구
            }
            if (options.Contains(current) && random.NextDouble() < 0.58) // 직진 가능할 때 우선 확률 적용
            {
                return current; // 직진 선택
            }
            return options[random.Next(0, options.Count)]; // 좌우회전 후보 무작위 선택
        }

        public float VehicleClearance(MapTrafficVehicle vehicle, float maxDistance) // Day28 공간 인덱스 기반 차량 전방 장애물 거리
        {
            Transform player = world != null && world.Player != null ? world.Player.transform : null; // 플레이어 Transform 조회
            return Map28RuntimeRegistry.VehicleClearance(vehicle, maxDistance, player); // 주변 공간 셀만 검사한 전방 거리 반환
        }

        private static float ForwardClearance(Vector3 origin, Vector3 forward, Vector3 candidate, float laneRadius, float maxDistance) // 전방 원뿔 대신 간단한 차선 거리 검사
        {
            Vector3 delta = candidate - origin; // 대상까지 벡터 계산
            delta.y = 0f; // 수평 교통만 검사
            float forwardDistance = Vector3.Dot(delta, forward); // 차량 전방 거리 계산
            if (forwardDistance <= 0f || forwardDistance > maxDistance) // 뒤쪽 또는 검사 범위 밖 확인
            {
                return maxDistance; // 장애물로 사용하지 않음
            }
            Vector3 lateral = delta - forward * forwardDistance; // 차선 옆 방향 거리 계산
            if (lateral.sqrMagnitude > laneRadius * laneRadius) // 다른 차선 또는 인도 대상 확인
            {
                return maxDistance; // 현재 차선 장애물 제외
            }
            return forwardDistance; // 실제 전방 거리 반환
        }

        public bool IsCrosswalkSafe(Vector3 position) // Day28 공간 인덱스 기반 횡단 차량 안전 확인
        {
            return Map28RuntimeRegistry.IsCrosswalkSafe(position, crosswalkSafetyRadius); // 주변 셀 이동 차량만 검사
        }

        public void RecycleVehicle(MapTrafficVehicle vehicle) // 멀어진 차량 풀 회수
        {
            if (vehicle == null) // 유효 차량 확인
            {
                return; // 회수 생략
            }
            vehicle.gameObject.SetActive(false); // 풀 비활성 상태 전환
        }

        public void RecoverVehicle(MapTrafficVehicle vehicle) // 막힌 차량 안전 재배치
        {
            RecycleVehicle(vehicle); // 현재 막힌 차량 먼저 회수
            nextMaintenance = Mathf.Min(nextMaintenance, Time.unscaledTime + 0.05f); // 다음 유지 검사에서 빠르게 새 차량 생성
        }

        public void RecycleCitizen(MapCitizenAgent citizen) // 멀어진 시민 풀 회수
        {
            if (citizen == null) // 유효 시민 확인
            {
                return; // 회수 생략
            }
            citizen.gameObject.SetActive(false); // 시민 풀 비활성 상태 전환
        }

        public int RandomIndex(int count) // 시민 이웃 선택용 안전 난수 인덱스
        {
            return count <= 1 ? 0 : random.Next(0, count); // 범위 안 난수 반환
        }

        public float RandomRange(float minimum, float maximum) // 시민 대기 시간용 난수 실수
        {
            float t = (float)random.NextDouble(); // 영에서 일 난수 생성
            return Mathf.Lerp(minimum, maximum, t); // 요청 범위 실수 반환
        }

        private int CountActiveVehicles() // 활성 차량 수 집계
        {
            int count = 0; // 집계 초기화
            foreach (MapTrafficVehicle vehicle in vehiclePool) // 전체 차량 풀 순회
            {
                if (vehicle != null && vehicle.gameObject.activeInHierarchy) // 활성 차량 확인
                {
                    count++; // 활성 수 증가
                }
            }
            return count; // 최종 활성 차량 수 반환
        }

        private int CountActiveCitizens() // 활성 시민 수 집계
        {
            int count = 0; // 집계 초기화
            foreach (MapCitizenAgent citizen in citizenPool) // 전체 시민 풀 순회
            {
                if (citizen != null && citizen.gameObject.activeInHierarchy) // 활성 시민 확인
                {
                    count++; // 활성 수 증가
                }
            }
            return count; // 최종 활성 시민 수 반환
        }

        private MapTrafficVehicle FirstInactiveVehicle() // 사용 가능한 차량 풀 항목 조회
        {
            foreach (MapTrafficVehicle vehicle in vehiclePool) // 전체 차량 풀 순회
            {
                if (vehicle != null && !vehicle.gameObject.activeSelf) // 비활성 차량 확인
                {
                    return vehicle; // 첫 사용 가능 차량 반환
                }
            }
            return null; // 풀 여유 없음
        }

        private MapCitizenAgent FirstInactiveCitizen() // 사용 가능한 시민 풀 항목 조회
        {
            foreach (MapCitizenAgent citizen in citizenPool) // 전체 시민 풀 순회
            {
                if (citizen != null && !citizen.gameObject.activeSelf) // 비활성 시민 확인
                {
                    return citizen; // 첫 사용 가능 시민 반환
                }
            }
            return null; // 풀 여유 없음
        }

        private bool HasVehicleNear(Vector3 point, float radius) // 차량 생성 위치 겹침 검사
        {
            float radiusSqr = radius * radius; // 검사 반경 제곱 계산
            foreach (MapTrafficVehicle vehicle in vehiclePool) // 차량 풀 순회
            {
                if (vehicle != null && vehicle.gameObject.activeInHierarchy && (vehicle.transform.position - point).sqrMagnitude < radiusSqr) // 가까운 활성 차량 확인
                {
                    return true; // 겹침 존재 반환
                }
            }
            return false; // 생성 가능 위치 반환
        }

        private bool HasCitizenNear(Vector3 point, float radius) // 시민 생성 위치 겹침 검사
        {
            float radiusSqr = radius * radius; // 검사 반경 제곱 계산
            foreach (MapCitizenAgent citizen in citizenPool) // 시민 풀 순회
            {
                if (citizen != null && citizen.gameObject.activeInHierarchy && (citizen.transform.position - point).sqrMagnitude < radiusSqr) // 가까운 활성 시민 확인
                {
                    return true; // 겹침 존재 반환
                }
            }
            return false; // 생성 가능 위치 반환
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) // 높이를 제외한 플레이 거리 계산
        {
            Vector2 delta = new Vector2(a.x - b.x, a.z - b.z); // 수평 좌표 차이 계산
            return delta.magnitude; // 실제 수평 거리 반환
        }

        private MapVehicleKind VehicleKindForIndex(int index) // 차량 풀에 종류를 일정 비율로 분산
        {
            if (index % 7 == 0) // 화물차 낮은 비율 확인
            {
                return MapVehicleKind.Cargo; // 산업 화물차 선택
            }
            if (index % 3 == 0) // 배달 차량 중간 비율 확인
            {
                return MapVehicleKind.Delivery; // 배달 차량 선택
            }
            return MapVehicleKind.Civilian; // 일반 승용차 선택
        }

        private MapCitizenKind CitizenKindForIndex(int index) // 시민 유형을 일정 비율로 분산
        {
            if (index % 7 == 0) // 완전 기계화 낮은 비율 확인
            {
                return MapCitizenKind.Mechanical; // 기계화 시민 선택
            }
            if (index % 3 == 0) // 안드로이드 중간 비율 확인
            {
                return MapCitizenKind.Android; // 안드로이드 선택
            }
            return MapCitizenKind.Human; // 일반 인간 시민 선택
        }

        private MapTrafficVehicle VehiclePrefab(MapVehicleKind kind) // 차량 종류별 프리팹 조회
        {
            if (kind == MapVehicleKind.Cargo) // 화물 차량 확인
            {
                return cargoVehiclePrefab; // 화물차 프리팹 반환
            }
            if (kind == MapVehicleKind.Delivery) // 배달 차량 확인
            {
                return deliveryVehiclePrefab; // 배달차 프리팹 반환
            }
            return civilianVehiclePrefab; // 일반 승용차 프리팹 반환
        }

        private MapCitizenAgent CitizenPrefab(MapCitizenKind kind) // 시민 유형별 프리팹 조회
        {
            if (kind == MapCitizenKind.Mechanical) // 기계화 시민 확인
            {
                return mechanicalCitizenPrefab; // 기계화 프리팹 반환
            }
            if (kind == MapCitizenKind.Android) // 안드로이드 시민 확인
            {
                return androidCitizenPrefab; // 안드로이드 프리팹 반환
            }
            return humanCitizenPrefab; // 인간 시민 프리팹 반환
        }

#if UNITY_EDITOR // 에디터 검사용 프리팹 참조 노출
        public bool HasAllPrefabs() // 설치 검증용 프리팹 완성 여부
        {
            return civilianVehiclePrefab != null && deliveryVehiclePrefab != null && cargoVehiclePrefab != null && humanCitizenPrefab != null && androidCitizenPrefab != null && mechanicalCitizenPrefab != null; // 여섯 프리팹 존재 여부 반환
        }
#endif
    }
}
