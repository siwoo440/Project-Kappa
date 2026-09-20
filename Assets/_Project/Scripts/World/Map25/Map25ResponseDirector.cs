using System.Collections.Generic; // E-03·E-04 풀 목록
using ProjectK.Day21; // 수배 시스템·경비·그래프 참조
using ProjectK.Day23; // Day23 정체 복구 참조
using ProjectK.Day24; // 지상·지하 경비 이동 참조
using UnityEngine; // 런타임 병력 생성·배치
using UnityEngine.Rendering; // URP 재질 셰이더 확인

using ProjectK.Day28; // Day28 런타임 Registry 참조
namespace ProjectK.Day25 // 25일차 수배 병력 차별화 이름 공간
{
    [DisallowMultipleComponent] // 대응 관리자 중복 방지
    public sealed class Map25ResponseDirector : MonoBehaviour // 4~5성 E-03·E-04 추가 대응 관리자
    {
        private static Map25ResponseDirector instance; // 현재 Day25 대응 관리자
        private readonly List<MapWantedGuardAgent> heavyPool = new List<MapWantedGuardAgent>(); // E-03 중장갑 풀
        private readonly List<Map25SurveillanceDrone> dronePool = new List<Map25SurveillanceDrone>(); // E-04 감시 드론 풀
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(); // 런타임 외형 재질 캐시
        private MapWantedResponseManager responseManager; // 기존 E-01·E-02 증원 관리자
        private MapWantedSystem wanted; // 현재 수배 관리자
        private Transform poolRoot; // Day25 특수 병력 풀 루트
        private float nextResolveTime; // 참조 재검색 시각
        private float nextMaintenance; // 다음 병력 유지 검사 시각
        private int heavySpawnSerial; // E-03 배치 방향 분산 번호
        private int droneSpawnSerial; // E-04 배치 방향 분산 번호
        private const float maintenanceInterval = 0.8f; // 특수 병력 유지 검사 간격
        private const int heavyPoolSize = 3; // 최대 E-03 풀 크기
        private const int dronePoolSize = 2; // 최대 E-04 풀 크기

        public static Map25ResponseDirector Instance => instance; // 현재 대응 관리자 조회
        public IReadOnlyList<MapWantedGuardAgent> HeavyPool => heavyPool; // 미니맵·검사용 E-03 목록
        public IReadOnlyList<Map25SurveillanceDrone> DronePool => dronePool; // 미니맵·검사용 E-04 목록

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 시 정적 상태 초기화
        {
            instance = null; // 이전 플레이 관리자 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 대응 관리자 생성
        {
            if (instance != null) // 기존 인스턴스 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day25] Response Escalation"); // Day25 자동 관리자 오브젝트 생성
            instance = owner.AddComponent<Map25ResponseDirector>(); // 대응 관리자 연결
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 관리자 존재 확인
            {
                Destroy(gameObject); // 중복 관리자 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
        }

        private void OnDestroy() // 씬 종료 정적 참조 정리
        {
            if (instance == this) // 현재 인스턴스 여부 확인
            {
                instance = null; // 정적 참조 해제
            }
        }

        private void Start() // 첫 수배 시스템 연결 시도
        {
            ResolveReferences(); // 기존 증원 관리자와 수배 관리자 검색
        }

        private void Update() // 별 단계별 E-03·E-04 투입 유지
        {
            if (responseManager == null || wanted == null || responseManager.Graph == null) // 기존 수배 시스템 준비 확인
            {
                if (Time.unscaledTime >= nextResolveTime) // 참조 재검색 시각 확인
                {
                    nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 시각 예약
                    ResolveReferences(); // 현재 씬 수배 시스템 검색
                }
                return; // 준비 전 증원 처리 생략
            }

            if (Time.unscaledTime < nextMaintenance) // 다음 병력 유지 시각 확인
            {
                return; // 검사 간격 유지
            }

            nextMaintenance = Time.unscaledTime + maintenanceInterval; // 다음 유지 검사 예약
            RecycleDeadUnits(); // 파괴·사망 유지 시간이 끝난 병력 회수
            MaintainHeavyUnits(MapWantedRules.HeavyTargetCount(wanted.Stars)); // 현재 별 단계 E-03 목표 유지
            MaintainDrones(MapWantedRules.DroneTargetCount(wanted.Stars)); // 현재 별 단계 E-04 목표 유지
        }

        private void ResolveReferences() // 기존 수배 시스템 자동 연결
        {
            MapWantedResponseManager[] managers = Object.FindObjectsByType<MapWantedResponseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 증원 관리자 검색
            responseManager = managers.Length > 0 ? managers[0] : null; // 첫 증원 관리자 연결
            wanted = responseManager != null ? responseManager.Wanted : MapWantedSystem.Instance; // 수배 관리자 연결

            if (responseManager == null || wanted == null || responseManager.Graph == null || wanted.Player == null) // 필수 구성 확인
            {
                return; // 아직 Map 수배 시스템 준비 안 됨
            }

            if (poolRoot == null) // 특수 병력 풀 미생성 확인
            {
                GameObject rootObject = new GameObject("WantedSpecialResponsePool"); // 특수 병력 풀 루트 생성
                rootObject.transform.SetParent(transform, false); // Day25 관리자 아래 연결
                poolRoot = rootObject.transform; // 풀 루트 저장
                InitializePools(); // E-03·E-04 사전 생성
            }
        }

        private void InitializePools() // E-03 세 명·E-04 두 대 사전 생성
        {
            if (heavyPool.Count == 0) // E-03 풀 미생성 확인
            {
                for (int i = 0; i < heavyPoolSize; i++) // 최대 중장갑 풀 순회
                {
                    MapWantedGuardAgent heavy = CreateHeavyGuard(i); // E-03 런타임 모델·AI 생성
                    heavyPool.Add(heavy); // 중장갑 풀 등록
                }
            }

            if (dronePool.Count == 0) // E-04 풀 미생성 확인
            {
                for (int i = 0; i < dronePoolSize; i++) // 최대 감시 드론 풀 순회
                {
                    Map25SurveillanceDrone drone = CreateDrone(i); // E-04 런타임 모델·AI 생성
                    dronePool.Add(drone); // 드론 풀 등록
                }
            }
        }

        private void MaintainHeavyUnits(int targetCount) // 현재 별 단계 E-03 병력 유지
        {
            targetCount = Mathf.Clamp(targetCount, 0, heavyPoolSize); // 안전한 목표 수 보정
            int active = CountActiveHeavy(); // 현재 활성 생존 E-03 집계

            while (active > targetCount) // 별 감소로 과잉 E-03 확인
            {
                if (!RecycleOneHeavy()) // 한 명 철수 시도
                {
                    break; // 철수 가능한 병력 없음
                }
                active--; // 활성 수 감소
            }

            if (active < targetCount) // E-03 증원 필요 확인
            {
                MapWantedGuardAgent heavy = FindInactiveHeavy(); // 대기 중 E-03 조회
                if (heavy != null && TrySpawnHeavy(heavy)) // 적절한 길목에 E-03 투입
                {
                    active++; // 활성 수 증가
                }
            }
        }

        private void MaintainDrones(int targetCount) // 현재 별 단계 E-04 병력 유지
        {
            targetCount = Mathf.Clamp(targetCount, 0, dronePoolSize); // 안전한 목표 수 보정
            int active = CountActiveDrones(); // 현재 활성 생존 드론 집계

            while (active > targetCount) // 수배 감소로 과잉 드론 확인
            {
                if (!RecycleOneDrone()) // 한 대 철수 시도
                {
                    break; // 철수 가능한 드론 없음
                }
                active--; // 활성 수 감소
            }

            if (active < targetCount) // E-04 추가 투입 필요 확인
            {
                Map25SurveillanceDrone drone = FindInactiveDrone(); // 대기 드론 조회
                if (drone != null) // 사용 가능한 드론 확인
                {
                    drone.ActivateAt(GetDroneSpawnPosition(droneSpawnSerial++)); // 플레이어 주변 공중 위치에 투입
                }
            }
        }

        private bool TrySpawnHeavy(MapWantedGuardAgent heavy) // E-03을 플레이어 예상 도주 길목에 배치
        {
            if (heavy == null || responseManager == null || responseManager.Graph == null || wanted == null || wanted.Player == null) // 필수 참조 확인
            {
                return false; // 배치 실패
            }

            int node = FindHeavySpawnNode(heavySpawnSerial++); // 현재 도주 방향 기반 후보 보행 노드 선택
            if (node < 0) // 유효한 보행 노드 확인
            {
                return false; // 배치 실패
            }

            heavy.Initialize(responseManager, MapWantedUnitKind.Heavy); // E-03 종류와 기존 수배 관리자 연결
            heavy.ActivateAt(node); // 기존 수배 경비 활성화 절차 재사용
            EnsureDay23AndDay24Components(heavy); // 정체 복구·지상지하 추격 즉시 연결
            IgnoreGuardCollisions(heavy); // 기존 E-01~E-03끼리 직접 밀림 방지
            return true; // E-03 배치 성공
        }

        private int FindHeavySpawnNode(int serial) // 플레이어 앞 길목에 가까운 보행 노드 선택
        {
            Vector3 focus = wanted.Player.transform.position; // 현재 플레이어 위치 조회
            Map24LayerNavigation navigation = Map24LayerNavigation.Instance; // 지상·지하 상태 조회
            if (navigation != null && navigation.Ready && navigation.GetLayer(focus) == Map24WorldLayer.Underground) // 플레이어 지하 여부 확인
            {
                focus = navigation.SurfaceAnchor; // 지하에서는 지상 계단 입구 주변 길목에 증원
            }

            float angleDegrees = (serial * 121f + wanted.Stars * 37f) % 360f; // 중장갑마다 다른 차단 방향 계산
            float angle = angleDegrees * Mathf.Deg2Rad; // 라디안 변환
            Vector3 desired = focus + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 58f; // 약 58m 앞 길목 목표 계산
            int bestNode = -1; // 최적 노드 초기화
            float bestScore = float.PositiveInfinity; // 최적 점수 초기화

            for (int i = 0; i < responseManager.Graph.Count; i++) // 전체 보도 노드 순회
            {
                Vector3 point = responseManager.Graph.Get(i).Position; // 후보 노드 위치 조회
                float playerDistance = PlanarDistance(point, focus); // 플레이어 또는 입구 기준 거리 계산
                if (playerDistance < 42f || playerDistance > 92f) // 화면 바로 앞·너무 먼 후보 제외
                {
                    continue; // 다음 후보 검사
                }

                if (HasActiveResponseNear(point, 7f)) // 기존 경비와 같은 위치 겹침 확인
                {
                    continue; // 겹치는 후보 제외
                }

                float score = PlanarDistance(point, desired); // 목표 차단 방향과의 거리 점수 계산
                if (score < bestScore) // 더 좋은 차단 위치 확인
                {
                    bestScore = score; // 최적 점수 갱신
                    bestNode = i; // 최적 노드 저장
                }
            }

            return bestNode; // 선택된 중장갑 배치 노드 반환
        }

        private Vector3 GetDroneSpawnPosition(int serial) // E-04 공중 증원 위치 계산
        {
            Vector3 focus = wanted.Player.transform.position; // 플레이어 위치 조회
            Map24LayerNavigation navigation = Map24LayerNavigation.Instance; // 지상·지하 상태 조회
            if (navigation != null && navigation.Ready && navigation.GetLayer(focus) == Map24WorldLayer.Underground) // 플레이어 지하 여부 확인
            {
                focus = navigation.SurfaceAnchor; // 지하에서는 계단 지상 입구 중심으로 드론 투입
            }

            float angle = ((serial * 180f + 45f) % 360f) * Mathf.Deg2Rad; // 두 드론 반대 방향 생성
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 34f; // 화면 바로 앞을 피한 수평 거리 적용
            return focus + offset + Vector3.up * 14f; // 기획 비행 높이로 생성
        }

        private MapWantedGuardAgent CreateHeavyGuard(int index) // E-03 중장갑 런타임 오브젝트 생성
        {
            GameObject root = new GameObject("Wanted_E03_Heavy_" + index.ToString("D2")); // E-03 루트 생성
            root.transform.SetParent(poolRoot, false); // 특수 병력 풀 아래 연결
            CharacterController controller = root.AddComponent<CharacterController>(); // 중장갑 이동 충돌체 추가
            controller.height = 2.15f; // E-03 큰 체격 높이 적용
            controller.radius = 0.48f; // E-03 넓은 체격 적용
            controller.center = new Vector3(0f, 1.08f, 0f); // 충돌체 중심 설정
            controller.stepOffset = 0.28f; // 낮은 보도·계단 이동 허용
            controller.slopeLimit = 45f; // 기본 경사 이동 한계 적용
            EnemyActor actor = root.AddComponent<EnemyActor>(); // 기존 적 생명 관리자 추가
            actor.Configure(180f, 160f, false); // 기획 체력180·자세160·암살 불가 적용
            root.AddComponent<EnemyStatusController>(); // EMP·마비 상태 관리자 추가
            DetectionSensor sensor = root.AddComponent<DetectionSensor>(); // E-03 시야·청각 센서 추가
            sensor.Configure(null, 16f, 85f, 14f, ~0); // 기획 시야16m·85도·청각14m 적용
            EnemyMeleeCombat melee = root.AddComponent<EnemyMeleeCombat>(); // 기존 근접 검술 추가
            root.AddComponent<MapGuardCrimeTag>(); // 법 집행 경비 공격·처치 Heat 연결
            MapWantedGuardAgent guard = root.AddComponent<MapWantedGuardAgent>(); // 기존 자유 추격·수색 AI 추가
            guard.Initialize(responseManager, MapWantedUnitKind.Heavy); // E-03 종류 연결
            Transform model = BuildHeavyModel(root.transform); // E-03 중장갑 외형 생성
            Transform swordSocket = model.Find("SwordSocket"); // 중장갑 무기 소켓 조회
            melee.Configure(swordSocket, 30f, 48f); // 기획 기본30·강48 피해 적용
            CreateHitZone(root.transform, "BodyHit", new Vector3(0f, 1.26f, 0f), new Vector3(1.05f, 1.72f, 0.86f), FirearmHitRegion.Body, actor); // 큰 몸통 총기 피격 영역 생성
            CreateHitZone(root.transform, "HeadHit", new Vector3(0f, 2.28f, 0f), new Vector3(0.62f, 0.54f, 0.62f), FirearmHitRegion.Head, actor); // 머리 총기 피격 영역 생성
            root.AddComponent<Map25HeavyPressure>(); // 느린 내려찍기·충격파 행동 추가
            root.AddComponent<Map23GuardChaseStability>(); // Day23 정체 복구 즉시 연결
            root.AddComponent<Map24GuardLayerAgent>(); // Day24 지상·지하 계단 추격 즉시 연결
            root.SetActive(false); // 실제 투입 전 풀 대기
            return guard; // 생성 E-03 반환
        }

        private Map25SurveillanceDrone CreateDrone(int index) // E-04 감시 드론 런타임 생성
        {
            GameObject root = new GameObject("Wanted_E04_Drone_" + index.ToString("D2")); // E-04 루트 생성
            root.transform.SetParent(poolRoot, false); // 특수 병력 풀 아래 연결
            EnemyActor actor = root.AddComponent<EnemyActor>(); // 드론 내구도 관리자 추가
            actor.Configure(60f, 1f, false); // 기획 내구도60·암살 불가 적용
            root.AddComponent<EnemyStatusController>(); // EMP·마비 상태 관리자 추가
            DetectionSensor sensor = root.AddComponent<DetectionSensor>(); // 공중 감시 센서 추가
            sensor.Configure(null, 22f, 110f, 0f, ~0); // 기획 시야22m·110도 적용
            root.AddComponent<MapGuardCrimeTag>(); // 법 집행 드론 공격·파괴 Heat 연결
            BuildDroneModel(root.transform); // E-04 공중 외형 생성
            CreateHitZone(root.transform, "BodyHit", Vector3.zero, new Vector3(1.55f, 0.70f, 1.30f), FirearmHitRegion.Body, actor); // 드론 본체 총기 피격 영역 생성
            Map25SurveillanceDrone drone = root.AddComponent<Map25SurveillanceDrone>(); // 공중 감시 AI 추가
            drone.Initialize(index); // 드론별 순찰 위상 분산
            root.SetActive(false); // 오성 이전 풀 대기
            return drone; // 생성 E-04 반환
        }

        private Transform BuildHeavyModel(Transform parent) // E-03 중장갑 프리미티브 모델 생성
        {
            Transform model = new GameObject("Model").transform; // 모델 루트 생성
            model.SetParent(parent, false); // E-03 루트 연결
            Material dark = MaterialFor("HeavyDark", new Color(0.055f, 0.060f, 0.070f), false); // 중장갑 어두운 재질
            Material steel = MaterialFor("HeavySteel", new Color(0.28f, 0.31f, 0.34f), false); // 두꺼운 금속 재질
            Material amber = MaterialFor("HeavyAmber", new Color(1f, 0.42f, 0.04f), true); // E-03 경고 발광 재질
            Material red = MaterialFor("HeavyRed", new Color(0.95f, 0.05f, 0.06f), true); // 적색 바이저 재질
            AddPart(model, "Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(0.76f, 0.34f, 0.52f), dark); // 큰 골반 장갑 생성
            AddPart(model, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.48f, 0f), new Vector3(0.72f, 0.68f, 0.58f), dark); // 중장갑 몸통 생성
            AddPart(model, "ChestPlate", PrimitiveType.Cube, new Vector3(0f, 1.52f, 0.31f), new Vector3(0.96f, 0.62f, 0.16f), steel); // 두꺼운 전면 흉갑 생성
            AddPart(model, "ChestWarning", PrimitiveType.Cube, new Vector3(0f, 1.56f, 0.405f), new Vector3(0.42f, 0.08f, 0.025f), amber); // E-03 경고등 생성
            AddPart(model, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.28f, 0f), new Vector3(0.48f, 0.42f, 0.45f), dark); // 중장갑 머리 생성
            AddPart(model, "Visor", PrimitiveType.Cube, new Vector3(0f, 2.30f, 0.27f), new Vector3(0.36f, 0.07f, 0.03f), red); // 적색 바이저 생성

            for (int side = -1; side <= 1; side += 2) // 좌우 팔다리와 어깨 장갑 생성
            {
                AddPart(model, "Shoulder", PrimitiveType.Cube, new Vector3(0.61f * side, 1.66f, 0f), new Vector3(0.34f, 0.34f, 0.50f), steel); // 어깨 장갑 생성
                AddPart(model, "Arm", PrimitiveType.Cylinder, new Vector3(0.61f * side, 1.20f, 0f), new Vector3(0.15f, 0.46f, 0.15f), dark); // 팔 생성
                AddPart(model, "Leg", PrimitiveType.Cylinder, new Vector3(0.22f * side, 0.49f, 0f), new Vector3(0.17f, 0.47f, 0.17f), steel); // 다리 생성
            }

            Transform swordSocket = new GameObject("SwordSocket").transform; // 중장갑 무기 소켓 생성
            swordSocket.SetParent(model, false); // 모델 루트 연결
            swordSocket.localPosition = new Vector3(0.60f, 1.12f, 0.22f); // 오른손 부근 무기 위치 적용
            AddPart(swordSocket, "HeavyBlade", PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f), new Vector3(0.13f, 1.48f, 0.11f), amber); // 긴 중장갑 검 생성
            AddPart(swordSocket, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.20f, 0f), new Vector3(0.07f, 0.22f, 0.07f), dark); // 무기 손잡이 생성
            AddUnitLabel(model, "E-03", amber, new Vector3(0f, 1.70f, 0.42f)); // 중장갑 ID 라벨 생성
            return model; // 생성 모델 반환
        }

        private void BuildDroneModel(Transform parent) // E-04 공중 감시 드론 프리미티브 모델 생성
        {
            Material dark = MaterialFor("DroneDark", new Color(0.055f, 0.075f, 0.095f), false); // 드론 본체 재질
            Material steel = MaterialFor("DroneSteel", new Color(0.24f, 0.30f, 0.36f), false); // 드론 프레임 재질
            Material cyan = MaterialFor("DroneCyan", new Color(0.04f, 0.82f, 1f), true); // 드론 추진·표식 발광
            Material red = MaterialFor("DroneRed", new Color(1f, 0.05f, 0.08f), true); // 감시 렌즈 발광
            AddPart(parent, "Core", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.20f, 0.48f, 0.95f), dark); // 드론 중앙 몸체 생성
            AddPart(parent, "FrontLens", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.58f), new Vector3(0.38f, 0.26f, 0.18f), red); // 전방 감시 렌즈 생성
            AddPart(parent, "LeftWing", PrimitiveType.Cube, new Vector3(-0.92f, 0f, 0f), new Vector3(0.90f, 0.12f, 0.36f), steel); // 왼쪽 날개 생성
            AddPart(parent, "RightWing", PrimitiveType.Cube, new Vector3(0.92f, 0f, 0f), new Vector3(0.90f, 0.12f, 0.36f), steel); // 오른쪽 날개 생성
            AddPart(parent, "LeftThruster", PrimitiveType.Cylinder, new Vector3(-1.28f, -0.10f, 0f), new Vector3(0.18f, 0.16f, 0.18f), cyan); // 왼쪽 추진기 생성
            AddPart(parent, "RightThruster", PrimitiveType.Cylinder, new Vector3(1.28f, -0.10f, 0f), new Vector3(0.18f, 0.16f, 0.18f), cyan); // 오른쪽 추진기 생성
            AddUnitLabel(parent, "E-04", cyan, new Vector3(0f, 0.72f, 0f)); // 감시 드론 ID 라벨 생성
        }

        private void AddUnitLabel(Transform parent, string text, Material accent, Vector3 position) // 런타임 병력 ID 라벨 생성
        {
            GameObject labelObject = new GameObject("UnitLabel"); // 라벨 오브젝트 생성
            labelObject.transform.SetParent(parent, false); // 병력 모델에 연결
            labelObject.transform.localPosition = position; // 병력별 라벨 위치 적용
            TextMesh label = labelObject.AddComponent<TextMesh>(); // 월드 텍스트 추가
            label.text = text; // E-03·E-04 ID 적용
            label.fontSize = 32; // 폰트 해상도 설정
            label.characterSize = 0.055f; // 실제 표시 크기 설정
            label.anchor = TextAnchor.MiddleCenter; // 중앙 기준 적용
            label.alignment = TextAlignment.Center; // 중앙 정렬 적용
            label.color = accent != null ? accent.color : Color.white; // 병력 발광색 기반 라벨 적용
        }

        private BoxCollider CreateHitZone(Transform parent, string name, Vector3 position, Vector3 size, FirearmHitRegion region, EnemyActor actor) // 기존 총기 피격 영역 생성
        {
            GameObject zoneObject = new GameObject(name); // 피격 영역 오브젝트 생성
            zoneObject.transform.SetParent(parent, false); // 병력 루트 연결
            zoneObject.transform.localPosition = position; // 피격 영역 위치 적용
            BoxCollider collider = zoneObject.AddComponent<BoxCollider>(); // 박스 피격 영역 추가
            collider.size = size; // 실제 피격 범위 적용
            collider.isTrigger = true; // 이동 충돌과 분리
            FirearmHitZone zone = zoneObject.AddComponent<FirearmHitZone>(); // 기존 총기 부위 표식 추가
            zone.Configure(region, actor, null); // 기존 적 생명 관리자 연결
            return collider; // 생성 충돌체 반환
        }

        private GameObject AddPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material) // 충돌 없는 런타임 모델 부품 생성
        {
            GameObject part = GameObject.CreatePrimitive(type); // 기본 프리미티브 생성
            part.name = name; // 부품 이름 적용
            part.transform.SetParent(parent, false); // 지정 부모 연결
            part.transform.localPosition = position; // 로컬 위치 적용
            part.transform.localScale = scale; // 로컬 크기 적용
            Renderer renderer = part.GetComponent<Renderer>(); // 부품 렌더러 조회
            if (renderer != null) // 렌더러 존재 확인
            {
                renderer.sharedMaterial = material; // 공유 재질 연결
            }

            Collider collider = part.GetComponent<Collider>(); // 기본 충돌체 조회
            if (collider != null) // 장식 충돌체 존재 확인
            {
                Destroy(collider); // 루트 이동 충돌과 피격 트리거만 남김
            }

            return part; // 생성 부품 반환
        }

        private Material MaterialFor(string key, Color color, bool emission) // 런타임 특수 병력 재질 생성·재사용
        {
            if (materials.TryGetValue(key, out Material existing) && existing != null) // 현재 재질 캐시 확인
            {
                return existing; // 기존 재질 재사용
            }

            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 렌더 파이프라인 Lit 셰이더 조회
            if (shader == null) // URP 셰이더 누락 확인
            {
                shader = Shader.Find("Standard"); // Standard 셰이더 대체
            }
            if (shader == null) // Standard 셰이더도 누락 확인
            {
                shader = Shader.Find("Unlit/Color"); // 최종 단색 셰이더 대체
            }

            Material material = new Material(shader); // 런타임 공유 재질 생성
            material.name = "Day25_" + key; // 재질 식별 이름 적용
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP 기본 색상 적용
            if (material.HasProperty("_Color")) material.SetColor("_Color", color); // Built-in 기본 색상 적용

            if (emission) // 발광 재질 확인
            {
                material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 3f); // 네온 발광 적용
            }

            materials.Add(key, material); // 재질 캐시 등록
            return material; // 생성 재질 반환
        }

        private void EnsureDay23AndDay24Components(MapWantedGuardAgent heavy) // E-03 기존 추격 보조 기능 즉시 연결
        {
            if (heavy.GetComponent<Map23GuardChaseStability>() == null) // Day23 정체 복구 존재 확인
            {
                heavy.gameObject.AddComponent<Map23GuardChaseStability>(); // Day23 정체 복구 추가
            }
            if (heavy.GetComponent<Map24GuardLayerAgent>() == null) // Day24 층 이동 존재 확인
            {
                heavy.gameObject.AddComponent<Map24GuardLayerAgent>(); // Day24 지상·지하 이동 추가
            }
        }

        private void IgnoreGuardCollisions(MapWantedGuardAgent activated) // E-03과 기존 수배 경비 물리 충돌 무시
        {
            if (activated == null || activated.Controller == null) // 새 중장갑 충돌체 확인
            {
                return; // 설정 생략
            }

            var guards = Map28RuntimeRegistry.ActiveGuards; // 현재 활성 수배 경비 조회
            foreach (MapWantedGuardAgent other in guards) // 활성 경비 순회
            {
                if (other == null || other == activated || other.Controller == null) // 자기 자신·누락 충돌체 제외
                {
                    continue; // 다음 경비 처리
                }

                Physics.IgnoreCollision(activated.Controller, other.Controller, true); // 경비끼리 밀고 막는 현상 방지
            }
        }

        private bool HasActiveResponseNear(Vector3 point, float radius) // 특수·기본 병력 배치 중복 확인
        {
            float radiusSqr = radius * radius; // 검사 반경 제곱 계산
            var guards = Map28RuntimeRegistry.ActiveGuards; // 현재 활성 지상 경비 조회
            foreach (MapWantedGuardAgent guard in guards) // 모든 활성 경비 순회
            {
                if (guard != null && !guard.IsDead && (guard.transform.position - point).sqrMagnitude < radiusSqr) // 후보 근처 생존 경비 확인
                {
                    return true; // 배치 중복 존재
                }
            }

            return false; // 배치 가능
        }

        private void RecycleDeadUnits() // 시체·파괴 드론 유지 시간이 끝난 특수 병력 회수
        {
            foreach (MapWantedGuardAgent heavy in heavyPool) // E-03 풀 순회
            {
                if (heavy != null && heavy.gameObject.activeSelf && heavy.ReadyToRecycle) // E-03 사망 유지 완료 확인
                {
                    heavy.gameObject.SetActive(false); // 중장갑 풀 대기 복귀
                }
            }

            foreach (Map25SurveillanceDrone drone in dronePool) // E-04 풀 순회
            {
                if (drone != null && drone.gameObject.activeSelf && drone.ReadyToRecycle) // 드론 파괴 유지 완료 확인
                {
                    drone.gameObject.SetActive(false); // 드론 풀 대기 복귀
                }
            }
        }

        private int CountActiveHeavy() // 활성 생존 E-03 수 집계
        {
            int count = 0; // 집계 초기화
            foreach (MapWantedGuardAgent heavy in heavyPool) // 중장갑 풀 순회
            {
                if (heavy != null && heavy.gameObject.activeSelf && !heavy.IsDead) // 활성 생존 중장갑 확인
                {
                    count++; // E-03 수 증가
                }
            }
            return count; // E-03 활성 수 반환
        }

        private int CountActiveDrones() // 활성 생존 E-04 수 집계
        {
            int count = 0; // 집계 초기화
            foreach (Map25SurveillanceDrone drone in dronePool) // 드론 풀 순회
            {
                if (drone != null && drone.gameObject.activeSelf && !drone.IsDead) // 활성 생존 드론 확인
                {
                    count++; // E-04 수 증가
                }
            }
            return count; // E-04 활성 수 반환
        }

        private MapWantedGuardAgent FindInactiveHeavy() // 대기 E-03 조회
        {
            foreach (MapWantedGuardAgent heavy in heavyPool) // 중장갑 풀 순회
            {
                if (heavy != null && !heavy.gameObject.activeSelf) // 대기 E-03 확인
                {
                    return heavy; // 첫 대기 E-03 반환
                }
            }
            return null; // 사용 가능한 E-03 없음
        }

        private Map25SurveillanceDrone FindInactiveDrone() // 대기 E-04 조회
        {
            foreach (Map25SurveillanceDrone drone in dronePool) // 드론 풀 순회
            {
                if (drone != null && !drone.gameObject.activeSelf) // 대기 드론 확인
                {
                    return drone; // 첫 대기 드론 반환
                }
            }
            return null; // 사용 가능한 E-04 없음
        }

        private bool RecycleOneHeavy() // 수배 하락 시 E-03 한 명 철수
        {
            for (int i = heavyPool.Count - 1; i >= 0; i--) // 풀 뒤쪽부터 철수 후보 확인
            {
                MapWantedGuardAgent heavy = heavyPool[i]; // 현재 중장갑 조회
                if (heavy == null || !heavy.gameObject.activeSelf || heavy.IsDead) // 비활성·사망 중장갑 제외
                {
                    continue; // 다음 후보 확인
                }

                heavy.gameObject.SetActive(false); // 수배 단계 하락에 맞춰 E-03 즉시 철수
                return true; // 철수 성공
            }

            return false; // 철수 후보 없음
        }

        private bool RecycleOneDrone() // 수배 하락 시 E-04 한 대 철수
        {
            for (int i = dronePool.Count - 1; i >= 0; i--) // 풀 뒤쪽부터 철수 후보 확인
            {
                Map25SurveillanceDrone drone = dronePool[i]; // 현재 드론 조회
                if (drone == null || !drone.gameObject.activeSelf || drone.IsDead) // 비활성·파괴 드론 제외
                {
                    continue; // 다음 후보 확인
                }

                drone.gameObject.SetActive(false); // 오성 해제 시 E-04 즉시 철수
                return true; // 철수 성공
            }

            return false; // 철수 후보 없음
        }

        private static float PlanarDistance(Vector3 first, Vector3 second) // XZ 평면 거리 계산
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)); // 수평 거리 반환
        }
    }
}
