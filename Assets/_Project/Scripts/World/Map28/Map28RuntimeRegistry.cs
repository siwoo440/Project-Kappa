using System.Collections.Generic; // 공용 목록과 공간 셀 자료구조
using ProjectK.Day20; // 시민·차량 타입 참조
using ProjectK.Day21; // 수배 경비 타입 참조
using ProjectK.Day25; // 감시 드론 타입 참조
using ProjectK.Day26; // 시민 신고 상태 타입 참조
using UnityEngine; // 런타임 검색과 좌표 처리
using UnityEngine.SceneManagement; // 씬 변경 감지

namespace ProjectK.Day28 // 28일차 성능 최적화 이름 공간
{
    [DisallowMultipleComponent] // 공용 Registry 중복 방지
    public sealed class Map28RuntimeRegistry : MonoBehaviour // 반복 전역 검색을 줄이는 도시 런타임 공용 Registry
    {
        private sealed class SpatialCell // 차량·시민 공간 분할 셀
        {
            public int Stamp; // 현재 공간 갱신 세대
            public readonly List<MapTrafficVehicle> Vehicles = new List<MapTrafficVehicle>(8); // 현재 셀 차량 목록
            public readonly List<MapCitizenAgent> Citizens = new List<MapCitizenAgent>(16); // 현재 셀 시민 목록
        }

        private static readonly DetectionSensor[] emptySensors = new DetectionSensor[0]; // Registry 미준비 센서 대체 목록
        private static readonly MapCitizenAgent[] emptyCitizens = new MapCitizenAgent[0]; // Registry 미준비 시민 대체 목록
        private static readonly MapTrafficVehicle[] emptyVehicles = new MapTrafficVehicle[0]; // Registry 미준비 차량 대체 목록
        private static readonly MapWantedGuardAgent[] emptyGuards = new MapWantedGuardAgent[0]; // Registry 미준비 경비 대체 목록
        private static readonly Map25SurveillanceDrone[] emptyDrones = new Map25SurveillanceDrone[0]; // Registry 미준비 드론 대체 목록
        private static readonly Map26CitizenWitness[] emptyWitnesses = new Map26CitizenWitness[0]; // Registry 미준비 시민 신고 상태 대체 목록
        private static Map28RuntimeRegistry instance; // 현재 Registry 인스턴스

        [SerializeField, Min(0.5f)] private float discoveryInterval = 3f; // 신규 런타임 개체 전체 검색 간격
        [SerializeField, Min(0.05f)] private float activeRefreshInterval = 0.25f; // 활성 상태 목록 갱신 간격
        [SerializeField, Min(0.05f)] private float spatialRefreshInterval = 0.15f; // 차량·시민 공간 셀 갱신 간격
        [SerializeField, Min(8f)] private float spatialCellSize = 28f; // 교통 공간 셀 한 변 길이

        private readonly List<DetectionSensor> allSensors = new List<DetectionSensor>(128); // 활성·비활성 전체 센서
        private readonly List<MapCitizenAgent> allCitizens = new List<MapCitizenAgent>(128); // 활성·비활성 전체 시민
        private readonly List<MapTrafficVehicle> allVehicles = new List<MapTrafficVehicle>(64); // 활성·비활성 전체 차량
        private readonly List<MapWantedGuardAgent> allGuards = new List<MapWantedGuardAgent>(64); // 활성·비활성 전체 수배 경비
        private readonly List<Map25SurveillanceDrone> allDrones = new List<Map25SurveillanceDrone>(16); // 활성·비활성 전체 드론
        private readonly List<Map26CitizenWitness> allWitnesses = new List<Map26CitizenWitness>(96); // 활성·비활성 전체 시민 신고 상태
        private readonly List<DetectionSensor> activeSensors = new List<DetectionSensor>(96); // 현재 활성 센서
        private readonly List<MapCitizenAgent> activeCitizens = new List<MapCitizenAgent>(96); // 현재 활성 시민
        private readonly List<MapTrafficVehicle> activeVehicles = new List<MapTrafficVehicle>(48); // 현재 활성 차량
        private readonly List<MapWantedGuardAgent> activeGuards = new List<MapWantedGuardAgent>(48); // 현재 활성 경비
        private readonly List<Map25SurveillanceDrone> activeDrones = new List<Map25SurveillanceDrone>(8); // 현재 활성 드론
        private readonly List<Map26CitizenWitness> activeWitnesses = new List<Map26CitizenWitness>(32); // 현재 신고 상태 컴포넌트
        private readonly Dictionary<long, SpatialCell> spatialCells = new Dictionary<long, SpatialCell>(256); // 월드 공간 셀 캐시

        private float nextDiscoveryTime; // 다음 전체 개체 검색 시각
        private float nextActiveRefreshTime; // 다음 활성 목록 갱신 시각
        private float nextSpatialRefreshTime; // 다음 공간 셀 갱신 시각
        private int spatialStamp; // 현재 공간 셀 세대
        private bool ready; // 최초 검색 완료 여부

        public static Map28RuntimeRegistry Instance => instance; // 현재 Registry 조회
        public static bool Ready => instance != null && instance.ready; // Registry 준비 상태 조회
        public static IReadOnlyList<DetectionSensor> AllSensors => instance != null ? instance.allSensors : emptySensors; // 전체 센서 목록 조회
        public static IReadOnlyList<MapCitizenAgent> AllCitizens => instance != null ? instance.allCitizens : emptyCitizens; // 전체 시민 목록 조회
        public static IReadOnlyList<MapTrafficVehicle> AllVehicles => instance != null ? instance.allVehicles : emptyVehicles; // 전체 차량 목록 조회
        public static IReadOnlyList<MapWantedGuardAgent> AllGuards => instance != null ? instance.allGuards : emptyGuards; // 전체 경비 목록 조회
        public static IReadOnlyList<Map25SurveillanceDrone> AllDrones => instance != null ? instance.allDrones : emptyDrones; // 전체 드론 목록 조회
        public static IReadOnlyList<Map26CitizenWitness> AllCitizenWitnesses => instance != null ? instance.allWitnesses : emptyWitnesses; // 전체 시민 신고 상태 목록 조회
        public static IReadOnlyList<DetectionSensor> ActiveSensors => instance != null ? instance.activeSensors : emptySensors; // 활성 센서 목록 조회
        public static IReadOnlyList<MapCitizenAgent> ActiveCitizens => instance != null ? instance.activeCitizens : emptyCitizens; // 활성 시민 목록 조회
        public static IReadOnlyList<MapTrafficVehicle> ActiveVehicles => instance != null ? instance.activeVehicles : emptyVehicles; // 활성 차량 목록 조회
        public static IReadOnlyList<MapWantedGuardAgent> ActiveGuards => instance != null ? instance.activeGuards : emptyGuards; // 활성 경비 목록 조회
        public static IReadOnlyList<Map25SurveillanceDrone> ActiveDrones => instance != null ? instance.activeDrones : emptyDrones; // 활성 드론 목록 조회
        public static IReadOnlyList<Map26CitizenWitness> ActiveCitizenWitnesses => instance != null ? instance.activeWitnesses : emptyWitnesses; // 활성 시민 신고 상태 목록 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 정적 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 Registry 참조 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 Registry 생성
        {
            if (instance != null) // 기존 Registry 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day28] Runtime Registry"); // Registry 오브젝트 생성
            instance = owner.AddComponent<Map28RuntimeRegistry>(); // Registry 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }

        private void Awake() // 단일 인스턴스 등록과 최초 검색
        {
            if (instance != null && instance != this) // 다른 Registry 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 Registry 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            DiscoverAll(); // 현재 씬 전체 도시 개체 최초 검색
            RebuildActiveLists(); // 최초 활성 목록 생성
            RebuildSpatialIndex(); // 최초 교통 공간 인덱스 생성
            ready = true; // Registry 준비 완료
        }

        private void OnEnable() // 씬 전환 이벤트 연결
        {
            SceneManager.activeSceneChanged += HandleSceneChanged; // 활성 씬 변경 감지
        }

        private void OnDisable() // 씬 전환 이벤트 해제
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged; // 정적 이벤트 참조 해제
        }

        private void Update() // 주기별 Registry와 공간 인덱스 갱신
        {
            float now = Time.unscaledTime; // 현재 비스케일 시각 조회

            if (now >= nextDiscoveryTime) // 신규 개체 전체 검색 시각 확인
            {
                nextDiscoveryTime = now + discoveryInterval; // 다음 전체 검색 예약
                DiscoverAll(); // 풀·런타임 생성 개체 목록 갱신
            }

            if (now >= nextActiveRefreshTime) // 활성 상태 갱신 시각 확인
            {
                nextActiveRefreshTime = now + activeRefreshInterval; // 다음 활성 목록 갱신 예약
                RebuildActiveLists(); // 활성 시민·차량·경비·센서 목록 재구성
            }

            if (now >= nextSpatialRefreshTime) // 공간 인덱스 갱신 시각 확인
            {
                nextSpatialRefreshTime = now + spatialRefreshInterval; // 다음 공간 갱신 예약
                RebuildSpatialIndex(); // 차량·시민 위치 공간 셀 재구성
            }
        }

        private void HandleSceneChanged(Scene previous, Scene current) // 씬 전환 뒤 목록 즉시 재검색 예약
        {
            nextDiscoveryTime = 0f; // 전체 검색 즉시 허용
            nextActiveRefreshTime = 0f; // 활성 목록 즉시 갱신
            nextSpatialRefreshTime = 0f; // 공간 인덱스 즉시 갱신
        }

        private void DiscoverAll() // 전역 검색을 Registry 한 곳에서만 낮은 빈도로 수행
        {
            CopyObjects(Object.FindObjectsByType<DetectionSensor>(FindObjectsInactive.Include, FindObjectsSortMode.None), allSensors); // 전체 센서 목록 갱신
            CopyObjects(Object.FindObjectsByType<MapCitizenAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None), allCitizens); // 전체 시민 목록 갱신
            CopyObjects(Object.FindObjectsByType<MapTrafficVehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None), allVehicles); // 전체 차량 목록 갱신
            CopyObjects(Object.FindObjectsByType<MapWantedGuardAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None), allGuards); // 전체 경비 목록 갱신
            CopyObjects(Object.FindObjectsByType<Map25SurveillanceDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None), allDrones); // 전체 드론 목록 갱신
            CopyObjects(Object.FindObjectsByType<Map26CitizenWitness>(FindObjectsInactive.Include, FindObjectsSortMode.None), allWitnesses); // 전체 시민 신고 상태 목록 갱신
        }

        private static void CopyObjects<T>(T[] source, List<T> destination) where T : Object // 검색 배열을 재사용 목록으로 복사
        {
            destination.Clear(); // 이전 검색 결과 제거

            if (source == null) // 검색 결과 존재 확인
            {
                return; // 빈 목록 유지
            }

            for (int i = 0; i < source.Length; i++) // 검색 결과 순회
            {
                T item = source[i]; // 현재 개체 조회
                if (item != null) // 파괴되지 않은 개체 확인
                {
                    destination.Add(item); // 전체 목록 등록
                }
            }
        }

        private void RebuildActiveLists() // 활성 상태만 별도 목록으로 캐시
        {
            activeSensors.Clear(); // 이전 활성 센서 제거
            activeCitizens.Clear(); // 이전 활성 시민 제거
            activeVehicles.Clear(); // 이전 활성 차량 제거
            activeGuards.Clear(); // 이전 활성 경비 제거
            activeDrones.Clear(); // 이전 활성 드론 제거
            activeWitnesses.Clear(); // 이전 활성 시민 신고 상태 제거

            for (int i = 0; i < allSensors.Count; i++) // 전체 센서 순회
            {
                DetectionSensor sensor = allSensors[i]; // 현재 센서 조회
                if (sensor != null && sensor.isActiveAndEnabled) // 실제 활성 센서 확인
                {
                    activeSensors.Add(sensor); // 활성 센서 등록
                }
            }

            for (int i = 0; i < allCitizens.Count; i++) // 전체 시민 순회
            {
                MapCitizenAgent citizen = allCitizens[i]; // 현재 시민 조회
                if (citizen != null && citizen.isActiveAndEnabled) // 실제 활성 시민 확인
                {
                    activeCitizens.Add(citizen); // 활성 시민 등록
                }
            }

            for (int i = 0; i < allVehicles.Count; i++) // 전체 차량 순회
            {
                MapTrafficVehicle vehicle = allVehicles[i]; // 현재 차량 조회
                if (vehicle != null && vehicle.isActiveAndEnabled) // 실제 활성 차량 확인
                {
                    activeVehicles.Add(vehicle); // 활성 차량 등록
                }
            }

            for (int i = 0; i < allGuards.Count; i++) // 전체 경비 순회
            {
                MapWantedGuardAgent guard = allGuards[i]; // 현재 경비 조회
                if (guard != null && guard.gameObject.activeInHierarchy) // Day24가 AI 컴포넌트를 잠시 꺼도 실제 활성 경비로 유지
                {
                    activeGuards.Add(guard); // 활성 경비 등록
                }
            }

            for (int i = 0; i < allDrones.Count; i++) // 전체 드론 순회
            {
                Map25SurveillanceDrone drone = allDrones[i]; // 현재 드론 조회
                if (drone != null && drone.gameObject.activeInHierarchy) // 실제 활성 드론 확인
                {
                    activeDrones.Add(drone); // 활성 드론 등록
                }
            }

            for (int i = 0; i < allWitnesses.Count; i++) // 전체 시민 신고 상태 순회
            {
                Map26CitizenWitness witness = allWitnesses[i]; // 현재 신고 상태 조회
                if (witness != null && witness.isActiveAndEnabled) // 실제 활성 신고 상태 확인
                {
                    activeWitnesses.Add(witness); // 활성 신고 상태 등록
                }
            }
        }

        private void RebuildSpatialIndex() // 차량·시민을 월드 공간 셀에 재등록
        {
            spatialStamp++; // 새 공간 세대 증가

            if (spatialStamp == int.MaxValue) // 세대 정수 한계 확인
            {
                spatialStamp = 1; // 안전한 세대 번호 복구

                foreach (KeyValuePair<long, SpatialCell> pair in spatialCells) // 기존 공간 셀 순회
                {
                    pair.Value.Stamp = 0; // 기존 세대 무효화
                }
            }

            for (int i = 0; i < activeVehicles.Count; i++) // 활성 차량 순회
            {
                MapTrafficVehicle vehicle = activeVehicles[i]; // 현재 차량 조회
                if (vehicle == null) // 파괴 차량 확인
                {
                    continue; // 다음 차량 처리
                }

                SpatialCell cell = CellForWrite(vehicle.transform.position); // 현재 위치 공간 셀 조회
                cell.Vehicles.Add(vehicle); // 차량 셀 등록
            }

            for (int i = 0; i < activeCitizens.Count; i++) // 활성 시민 순회
            {
                MapCitizenAgent citizen = activeCitizens[i]; // 현재 시민 조회
                if (citizen == null) // 파괴 시민 확인
                {
                    continue; // 다음 시민 처리
                }

                SpatialCell cell = CellForWrite(citizen.transform.position); // 현재 위치 공간 셀 조회
                cell.Citizens.Add(citizen); // 시민 셀 등록
            }
        }

        private SpatialCell CellForWrite(Vector3 position) // 현재 위치의 공간 셀 조회·초기화
        {
            int x = Mathf.FloorToInt(position.x / spatialCellSize); // X 셀 번호 계산
            int z = Mathf.FloorToInt(position.z / spatialCellSize); // Z 셀 번호 계산
            long key = CellKey(x, z); // 셀 고유 키 계산

            if (!spatialCells.TryGetValue(key, out SpatialCell cell)) // 기존 셀 존재 확인
            {
                cell = new SpatialCell(); // 새 공간 셀 생성
                spatialCells.Add(key, cell); // 공간 사전 등록
            }

            if (cell.Stamp != spatialStamp) // 현재 세대 첫 사용 확인
            {
                cell.Stamp = spatialStamp; // 현재 세대 등록
                cell.Vehicles.Clear(); // 이전 세대 차량 제거
                cell.Citizens.Clear(); // 이전 세대 시민 제거
            }

            return cell; // 준비된 공간 셀 반환
        }

        private SpatialCell CellForRead(int x, int z) // 현재 세대 공간 셀 조회
        {
            long key = CellKey(x, z); // 셀 고유 키 계산

            if (!spatialCells.TryGetValue(key, out SpatialCell cell) || cell.Stamp != spatialStamp) // 현재 세대 셀 확인
            {
                return null; // 비어 있는 셀 반환
            }

            return cell; // 현재 세대 셀 반환
        }

        private static long CellKey(int x, int z) // 두 정수 좌표를 하나의 키로 결합
        {
            return ((long)x << 32) ^ (uint)z; // 충돌 없는 64비트 키 반환
        }

        public static float VehicleClearance(MapTrafficVehicle vehicle, float maxDistance, Transform player) // 공간 인덱스를 이용한 전방 장애물 거리 계산
        {
            if (vehicle == null || maxDistance <= 0f) // 유효 차량과 거리 확인
            {
                return Mathf.Max(0f, maxDistance); // 안전 거리 반환
            }

            Map28RuntimeRegistry registry = instance; // 현재 Registry 조회
            if (registry == null || !registry.ready) // Registry 미준비 확인
            {
                return maxDistance; // 초기 프레임은 전방 여유로 처리
            }

            Vector3 origin = vehicle.transform.position; // 검사 차량 위치 조회
            Vector3 forward = vehicle.transform.forward; // 검사 차량 전방 조회
            float nearest = maxDistance; // 기본 여유 거리 설정
            int centerX = Mathf.FloorToInt(origin.x / registry.spatialCellSize); // 현재 X 셀 계산
            int centerZ = Mathf.FloorToInt(origin.z / registry.spatialCellSize); // 현재 Z 셀 계산
            int radius = Mathf.Max(1, Mathf.CeilToInt(maxDistance / registry.spatialCellSize)); // 검사할 셀 반경 계산

            for (int dz = -radius; dz <= radius; dz++) // Z 셀 범위 순회
            {
                for (int dx = -radius; dx <= radius; dx++) // X 셀 범위 순회
                {
                    SpatialCell cell = registry.CellForRead(centerX + dx, centerZ + dz); // 주변 공간 셀 조회
                    if (cell == null) // 빈 셀 확인
                    {
                        continue; // 다음 셀 처리
                    }

                    for (int i = 0; i < cell.Vehicles.Count; i++) // 셀 차량 순회
                    {
                        MapTrafficVehicle other = cell.Vehicles[i]; // 현재 주변 차량 조회
                        if (other == null || other == vehicle || !other.gameObject.activeInHierarchy) // 자기 자신·비활성 차량 제외
                        {
                            continue; // 다음 차량 처리
                        }

                        nearest = Mathf.Min(nearest, ForwardClearance(origin, forward, other.transform.position, 2.3f, maxDistance)); // 앞차 거리 갱신
                    }

                    for (int i = 0; i < cell.Citizens.Count; i++) // 셀 시민 순회
                    {
                        MapCitizenAgent citizen = cell.Citizens[i]; // 현재 주변 시민 조회
                        if (citizen == null || !citizen.gameObject.activeInHierarchy) // 비활성 시민 제외
                        {
                            continue; // 다음 시민 처리
                        }

                        nearest = Mathf.Min(nearest, ForwardClearance(origin, forward, citizen.transform.position, 1.8f, maxDistance)); // 횡단 시민 거리 갱신
                    }
                }
            }

            if (player != null) // 플레이어 존재 확인
            {
                nearest = Mathf.Min(nearest, ForwardClearance(origin, forward, player.position, 2.0f, maxDistance)); // 플레이어 전방 거리 갱신
            }

            return nearest; // 가장 가까운 전방 장애물 거리 반환
        }

        public static bool IsCrosswalkSafe(Vector3 position, float radius) // 공간 인덱스를 이용한 횡단 차량 검사
        {
            Map28RuntimeRegistry registry = instance; // 현재 Registry 조회
            if (registry == null || !registry.ready) // Registry 미준비 확인
            {
                return true; // 초기 프레임은 횡단 허용
            }

            float radiusSqr = radius * radius; // 안전 반경 제곱 계산
            int centerX = Mathf.FloorToInt(position.x / registry.spatialCellSize); // 중심 X 셀 계산
            int centerZ = Mathf.FloorToInt(position.z / registry.spatialCellSize); // 중심 Z 셀 계산
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(radius / registry.spatialCellSize)); // 검사 셀 반경 계산

            for (int dz = -cellRadius; dz <= cellRadius; dz++) // Z 셀 범위 순회
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++) // X 셀 범위 순회
                {
                    SpatialCell cell = registry.CellForRead(centerX + dx, centerZ + dz); // 주변 셀 조회
                    if (cell == null) // 빈 셀 확인
                    {
                        continue; // 다음 셀 처리
                    }

                    for (int i = 0; i < cell.Vehicles.Count; i++) // 셀 차량 순회
                    {
                        MapTrafficVehicle vehicle = cell.Vehicles[i]; // 현재 차량 조회
                        if (vehicle == null || !vehicle.gameObject.activeInHierarchy || vehicle.CurrentSpeed <= 0.2f) // 비활성·정지 차량 제외
                        {
                            continue; // 다음 차량 처리
                        }

                        Vector3 delta = vehicle.transform.position - position; // 횡단 지점과 차량 거리 계산
                        delta.y = 0f; // 수평 거리만 사용

                        if (delta.sqrMagnitude <= radiusSqr) // 안전 반경 안 이동 차량 확인
                        {
                            return false; // 횡단 대기
                        }
                    }
                }
            }

            return true; // 주변 이동 차량 없음
        }

        private static float ForwardClearance(Vector3 origin, Vector3 forward, Vector3 candidate, float laneRadius, float maxDistance) // 전방 차선 거리 검사
        {
            Vector3 delta = candidate - origin; // 대상 방향 벡터 계산
            delta.y = 0f; // 수평 교통만 검사
            float forwardDistance = Vector3.Dot(delta, forward); // 차량 전방 거리 계산

            if (forwardDistance <= 0f || forwardDistance > maxDistance) // 뒤쪽 또는 검사 범위 밖 확인
            {
                return maxDistance; // 장애물 제외
            }

            Vector3 lateral = delta - forward * forwardDistance; // 차선 옆 방향 거리 계산
            if (lateral.sqrMagnitude > laneRadius * laneRadius) // 다른 차선 대상 확인
            {
                return maxDistance; // 현재 차선 장애물 제외
            }

            return forwardDistance; // 실제 전방 거리 반환
        }

        public static void ForceRefresh() // 테스트와 런타임 대규모 생성 후 즉시 Registry 갱신
        {
            if (instance == null) // Registry 존재 확인
            {
                return; // 갱신 생략
            }

            instance.DiscoverAll(); // 전체 개체 재검색
            instance.RebuildActiveLists(); // 활성 목록 즉시 재구성
            instance.RebuildSpatialIndex(); // 공간 셀 즉시 재구성
            instance.ready = true; // 준비 상태 유지
        }
    }
}
