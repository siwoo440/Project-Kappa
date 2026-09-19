using System.Collections.Generic; // 경비 풀 관리
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day20; // 보행 그래프 재사용
using UnityEngine; // 경비 생성·거리 계산

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 증원 관리자 중복 방지
    public sealed class MapWantedResponseManager : MonoBehaviour // 별 단계에 따라 E-01/E-02 추적 병력을 풀링
    {
        [SerializeField] private MapWorldRoot world; // 본편 월드
        [SerializeField] private MapWantedSystem wanted; // 수배 관리자
        [SerializeField] private MapWantedGuardAgent regularPrefab; // E-01 일반 대응 경비
        [SerializeField] private MapWantedGuardAgent elitePrefab; // E-02 정예 대응 경비
        [SerializeField, Range(8, 32)] private int guardPoolSize = 22; // 최대 추적 경비 풀 크기
        [SerializeField] private float spawnMinimumDistance = 55f; // 화면 가까이 생성 금지 거리
        [SerializeField] private float spawnMaximumDistance = 180f; // 기본 증원 최대 거리
        [SerializeField] private float maintenanceInterval = 0.8f; // 증원 유지 검사 간격
        [SerializeField] private float searchRadius = 34f; // 마지막 목격 위치 주변 분산 수색 최대 반경
        private readonly List<MapWantedGuardAgent> guardPool = new List<MapWantedGuardAgent>(); // 모든 수배 경비 풀
        private MapPedestrianGraph graph; // 보도 기반 추적 그래프
        private System.Random random; // 증원 위치·등급 난수
        private float nextMaintenance; // 다음 증원 검사 시각
        public MapWorldRoot World => world; // 본편 월드 조회
        public MapWantedSystem Wanted => wanted; // 수배 관리자 조회
        public MapPedestrianGraph Graph => graph; // 경비 이동 그래프 조회
        public int GuardPoolSize => guardPoolSize; // 검사 메뉴용 풀 크기 조회
        public float SearchRadius => searchRadius; // 검사 메뉴용 분산 수색 반경 조회

        public void Configure(MapWorldRoot owner, MapWantedSystem wantedSystem, MapWantedGuardAgent regular, MapWantedGuardAgent elite) // 에디터 설치 참조 연결
        {
            world = owner; // 본편 월드 저장
            wanted = wantedSystem; // 수배 관리자 저장
            regularPrefab = regular; // 일반 경비 프리팹 저장
            elitePrefab = elite; // 정예 경비 프리팹 저장
        }

        private void Awake() // 그래프와 난수 준비
        {
            if (world == null) // 월드 참조 누락 확인
            {
                world = GetComponentInParent<MapWorldRoot>(); // 부모 월드에서 복구
            }
            if (wanted == null) // 수배 관리자 누락 확인
            {
                wanted = GetComponentInParent<MapWantedSystem>(); // 부모에서 수배 관리자 조회
            }
            graph = MapPedestrianGraph.Build(world != null ? world.WorldSize : 1536f, MapTerrainMath.Ground + 0.24f); // 시민과 같은 보도 그래프 생성
            random = new System.Random(21021); // 재현 가능한 증원 난수 생성
        }

        private void Start() // 경비 풀 사전 생성
        {
            InitializePool(); // E-01/E-02 풀 생성
        }

        private void Update() // 별 단계에 맞는 증원 병력 유지
        {
            if (wanted == null || world == null || world.Player == null || Time.unscaledTime < nextMaintenance) // 필수 참조와 검사 시점 확인
            {
                return; // 다음 유지 시각까지 대기
            }
            nextMaintenance = Time.unscaledTime + maintenanceInterval; // 다음 증원 검사 예약
            RecycleDeadGuards(); // 사망 시체 유지가 끝난 경비 회수
            int target = wanted.GuardTargetCount; // 현재 별 단계 병력 목표 조회
            int active = CountActiveLiving(); // 현재 살아 있는 활성 경비 수 조회
            if (active < target) // 증원 필요 여부 확인
            {
                int budget = Mathf.Min(3, target - active); // 한 검사에서 최대 세 명만 투입
                for (int i = 0; i < budget; i++) // 증원 예산만큼 배치
                {
                    TrySpawnGuard(); // 수배 단계에 맞는 경비 생성
                }
            }
            else if (active > target) // 수배 하락 후 과잉 병력 확인
            {
                RecycleExcess(active - target); // 멀고 시야가 없는 경비부터 회수
            }
        }

        private void InitializePool() // 일반·정예 경비를 절반씩 사전 생성
        {
            if (guardPool.Count > 0 || regularPrefab == null || elitePrefab == null) // 이미 생성됐거나 프리팹 누락 확인
            {
                return; // 중복 생성 방지
            }
            Transform poolRoot = new GameObject("WantedGuardPool").transform; // 경비 풀 부모 생성
            poolRoot.SetParent(transform, false); // Day21 루트에 연결
            for (int i = 0; i < guardPoolSize; i++) // 전체 풀 크기 순회
            {
                bool elite = i >= guardPoolSize / 2; // 후반 절반을 정예 풀로 배정
                MapWantedGuardAgent prefab = elite ? elitePrefab : regularPrefab; // 등급별 프리팹 선택
                MapWantedGuardAgent guard = Instantiate(prefab, poolRoot); // 경비 프리팹 생성
                guard.name = elite ? "Wanted_E02_" + i.ToString("D2") : "Wanted_E01_" + i.ToString("D2"); // 수배 경비 식별 이름 적용
                guard.Initialize(this, elite); // 증원 관리자와 등급 연결
                guard.gameObject.SetActive(false); // 실제 투입 전 풀 대기
                guardPool.Add(guard); // 풀 목록 저장
            }
        }

        private bool TrySpawnGuard() // 플레이어 화면 근처를 피한 보도 노드에서 경비 투입
        {
            if (wanted.Stars <= 0 || graph == null) // 수배와 그래프 확인
            {
                return false; // 증원 불필요
            }
            bool wantElite = wanted.Stars >= 3 && random.NextDouble() < MapWantedRules.EliteRatio(wanted.Stars); // 별 단계별 정예 확률 적용
            MapWantedGuardAgent guard = FindInactiveGuard(wantElite); // 요청 등급의 대기 경비 조회
            if (guard == null && wantElite) // 정예 풀 부족 확인
            {
                guard = FindInactiveGuard(false); // 일반 경비로 대체
            }
            if (guard == null) // 전체 풀 여유 확인
            {
                return false; // 증원 실패
            }
            Vector3 player = world.Player.transform.position; // 플레이어 위치 조회
            float maximum = Mathf.Max(spawnMinimumDistance + 5f, Mathf.Min(spawnMaximumDistance, wanted.PursuitRadius * 0.95f)); // 현재 추적 반경 안 생성 상한 계산
            for (int attempt = 0; attempt < 80; attempt++) // 적절한 보도 노드 탐색
            {
                int node = random.Next(0, graph.Count); // 무작위 보도 노드 선택
                Vector3 point = graph.Get(node).Position; // 후보 위치 조회
                float distance = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(point.x, point.z)); // 플레이어 수평 거리 계산
                if (distance < spawnMinimumDistance || distance > maximum) // 화면 근거리·추적 범위 밖 확인
                {
                    continue; // 후보 제외
                }
                if (HasActiveGuardNear(point, 6f)) // 같은 위치 증원 겹침 확인
                {
                    continue; // 후보 제외
                }
                guard.ActivateAt(node); // 경비 실제 투입
                IgnoreGuardCollisions(guard); // 자유 추격 중 동료 경비가 서로 막지 않도록 충돌 무시
                return true; // 증원 성공
            }
            return false; // 적절한 생성 위치 없음
        }

        public Vector3 GetSearchDestination(MapWantedGuardAgent guard, Vector3 center, int searchIndex) // 그래프 스냅 없이 마지막 위치 주변 자유 수색 슬롯 계산
        {
            int guardSeed = guard != null ? Mathf.Abs(guard.GetInstanceID()) : 0; // 개체별 안정적인 수색 시드 계산
            int ring = Mathf.Abs(searchIndex) % 4; // 수색 거리를 네 단계로 반복
            float radius = Mathf.Lerp(12f, Mathf.Max(18f, searchRadius), ring / 3f); // 수색 중심에서 12m~설정 반경으로 분산
            float angleDegrees = (guardSeed * 47 + searchIndex * 97) % 360; // 개체와 수색 순번마다 다른 방향 계산
            float angle = angleDegrees * Mathf.Deg2Rad; // 삼각함수용 라디안 변환
            return new Vector3(center.x + Mathf.Cos(angle) * radius, center.y, center.z + Mathf.Sin(angle) * radius); // 횡단보도·보행 노드와 무관한 자유 수색 위치 반환
        }

        private void IgnoreGuardCollisions(MapWantedGuardAgent activated) // 수배 경비끼리 한 지점에서 서로 밀고 막는 현상 방지
        {
            if (activated == null || activated.Controller == null) // 새 경비 컨트롤러 확인
            {
                return; // 충돌 설정 생략
            }
            foreach (MapWantedGuardAgent other in guardPool) // 기존 경비 풀 순회
            {
                if (other == null || other == activated || other.Controller == null || !other.gameObject.activeSelf) // 자기 자신·비활성 경비 제외
                {
                    continue; // 다음 경비 검사
                }
                Physics.IgnoreCollision(activated.Controller, other.Controller, true); // 동료 경비끼리 직접 통과 가능하게 설정
            }
        }

        private void RecycleDeadGuards() // 시체 유지 시간이 끝난 경비 풀 회수
        {
            foreach (MapWantedGuardAgent guard in guardPool) // 전체 경비 풀 순회
            {
                if (guard != null && guard.gameObject.activeSelf && guard.ReadyToRecycle) // 사망 유지 완료 경비 확인
                {
                    guard.gameObject.SetActive(false); // 풀 대기 상태로 전환
                }
            }
        }

        private void RecycleExcess(int count) // 수배 하락 시 과잉 경비 정리
        {
            if (count <= 0) // 회수 필요 수 확인
            {
                return; // 처리 생략
            }
            Vector3 player = world.Player.transform.position; // 현재 플레이어 위치
            for (int i = guardPool.Count - 1; i >= 0 && count > 0; i--) // 풀 뒤쪽부터 정리
            {
                MapWantedGuardAgent guard = guardPool[i]; // 현재 경비 조회
                if (guard == null || !guard.gameObject.activeSelf || guard.IsDead) // 비활성·사망 경비 제외
                {
                    continue; // 다음 경비 확인
                }
                if (guard.Sensor != null && guard.Sensor.TargetVisible) // 플레이어를 직접 보고 있는 경비 확인
                {
                    continue; // 눈앞 추격 병력은 갑자기 제거하지 않음
                }
                float distance = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(guard.transform.position.x, guard.transform.position.z)); // 플레이어와 수평 거리 계산
                if (distance < 45f) // 가까운 경비 확인
                {
                    continue; // 화면 가까운 병력 유지
                }
                guard.gameObject.SetActive(false); // 멀리 있는 과잉 병력 풀 회수
                count--; // 남은 회수 수 감소
            }
        }

        private int CountActiveLiving() // 살아 있는 활성 경비 수 집계
        {
            int count = 0; // 집계 초기화
            foreach (MapWantedGuardAgent guard in guardPool) // 전체 풀 순회
            {
                if (guard != null && guard.gameObject.activeSelf && !guard.IsDead) // 활성 생존 경비 확인
                {
                    count++; // 병력 수 증가
                }
            }
            return count; // 활성 병력 수 반환
        }

        private MapWantedGuardAgent FindInactiveGuard(bool elite) // 원하는 등급의 비활성 경비 조회
        {
            foreach (MapWantedGuardAgent guard in guardPool) // 전체 풀 순회
            {
                if (guard != null && !guard.gameObject.activeSelf && guard.IsElite == elite) // 비활성·등급 일치 확인
                {
                    return guard; // 사용 가능한 경비 반환
                }
            }
            return null; // 해당 등급 풀 없음
        }

        private bool HasActiveGuardNear(Vector3 point, float radius) // 증원 위치 중복 검사
        {
            float radiusSqr = radius * radius; // 반경 제곱 계산
            foreach (MapWantedGuardAgent guard in guardPool) // 전체 경비 순회
            {
                if (guard != null && guard.gameObject.activeSelf && (guard.transform.position - point).sqrMagnitude < radiusSqr) // 근처 활성 경비 확인
                {
                    return true; // 겹침 존재
                }
            }
            return false; // 배치 가능
        }

#if UNITY_EDITOR // 에디터 설치 검사 전용
        public bool HasPrefabs() // 경비 프리팹 두 종류 연결 여부
        {
            return regularPrefab != null && elitePrefab != null; // 일반·정예 프리팹 존재 반환
        }
#endif
    }
}
