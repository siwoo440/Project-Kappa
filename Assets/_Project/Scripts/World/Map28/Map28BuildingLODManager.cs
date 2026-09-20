using System; // 이름 비교 처리
using System.Collections.Generic; // 건물 LOD 캐시 목록
using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 거리 계산과 Renderer 제어
using UnityEngine.Rendering; // 그림자 표시 모드 제어

namespace ProjectK.Day28 // 28일차 성능 최적화 이름 공간
{
    [DisallowMultipleComponent] // 건물 LOD 관리자 중복 방지
    public sealed class Map28BuildingLODManager : MonoBehaviour // V3 건물 디테일을 거리별로 단계적으로 줄이는 중앙 관리자
    {
        private enum BuildingLevel // 현재 건물 표시 단계
        {
            Unknown, // 초기 상태
            Near, // 전체 디테일 표시
            Mid, // 중간 디테일 표시
            Far, // 실루엣 중심 표시
            Culled // 전체 렌더 비활성
        }

        private sealed class BuildingNode // 한 건물의 렌더러 분류 캐시
        {
            public Transform Root; // 건물 루트
            public Renderer[] Core; // 본체·큰 실루엣 Renderer
            public Renderer[] Medium; // 출입구·네온·옥상 설비 Renderer
            public Renderer[] Detail; // 창문·안테나 등 작은 Renderer
            public BuildingLevel Level; // 현재 적용 단계
        }

        private static Map28BuildingLODManager instance; // 현재 건물 LOD 관리자
        private readonly List<BuildingNode> buildings = new List<BuildingNode>(512); // 현재 도시 건물 캐시

        [SerializeField, Min(20f)] private float fullDetailDistance = 180f; // 모든 건물 디테일 표시 거리
        [SerializeField, Min(40f)] private float mediumDetailDistance = 360f; // 중간 디테일 표시 거리
        [SerializeField, Min(80f)] private float buildingCullDistance = 720f; // 건물 전체 렌더 컬링 거리
        [SerializeField, Range(4, 128)] private int buildingsPerFrame = 32; // 프레임당 갱신할 건물 수
        [SerializeField, Min(1f)] private float discoveryInterval = 5f; // 신규 건물 재검색 간격

        private MapWorldRoot world; // 현재 본편 월드 참조
        private int updateIndex; // 다음 갱신 건물 인덱스
        private float nextDiscoveryTime; // 다음 도시 건물 재검색 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 참조 초기화
        {
            instance = null; // 이전 관리자 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 건물 LOD 관리자 생성
        {
            if (instance != null) // 기존 관리자 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day28] Building LOD Manager"); // LOD 관리자 오브젝트 생성
            instance = owner.AddComponent<Map28BuildingLODManager>(); // LOD 관리자 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 관리자 존재 확인
            {
                Destroy(gameObject); // 중복 관리자 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 건물 캐시 생성
        {
            DiscoverBuildings(); // 현재 Map 건물 검색
            nextDiscoveryTime = Time.unscaledTime + discoveryInterval; // 다음 검색 예약
        }

        private void Update() // 건물 표시 단계 분산 갱신
        {
            if (world == null || world.Player == null) // 본편 월드와 플레이어 확인
            {
                if (Time.unscaledTime >= nextDiscoveryTime) // 재검색 시각 확인
                {
                    nextDiscoveryTime = Time.unscaledTime + 1f; // 월드 미준비 시 짧은 재검색 예약
                    DiscoverBuildings(); // 월드와 건물 재검색
                }

                return; // LOD 처리 중단
            }

            if (Time.unscaledTime >= nextDiscoveryTime) // 신규 도시 요소 재검색 시각 확인
            {
                nextDiscoveryTime = Time.unscaledTime + discoveryInterval; // 다음 재검색 예약
                DiscoverBuildings(); // 건물 캐시 갱신
            }

            if (buildings.Count == 0) // 관리할 건물 존재 확인
            {
                return; // 처리 생략
            }

            int budget = Mathf.Min(buildingsPerFrame, buildings.Count); // 현재 프레임 갱신 예산 계산
            Vector3 playerPosition = world.Player.transform.position; // 플레이어 위치 한 번만 조회
            float nearSqr = fullDetailDistance * fullDetailDistance; // 근거리 제곱 거리 계산
            float midSqr = mediumDetailDistance * mediumDetailDistance; // 중거리 제곱 거리 계산
            float cullSqr = buildingCullDistance * buildingCullDistance; // 컬링 제곱 거리 계산

            for (int i = 0; i < budget; i++) // 프레임당 제한된 건물 순회
            {
                if (updateIndex >= buildings.Count) // 목록 끝 확인
                {
                    updateIndex = 0; // 첫 건물부터 다시 순회
                }

                BuildingNode node = buildings[updateIndex++]; // 현재 갱신 건물 조회
                if (node == null || node.Root == null) // 파괴된 건물 확인
                {
                    continue; // 다음 건물 처리
                }

                Vector3 delta = node.Root.position - playerPosition; // 플레이어와 건물 거리 벡터 계산
                delta.y = 0f; // 높이 차이 제외
                float distanceSqr = delta.sqrMagnitude; // 수평 제곱 거리 계산
                BuildingLevel desired = distanceSqr <= nearSqr ? BuildingLevel.Near :
                                        distanceSqr <= midSqr ? BuildingLevel.Mid :
                                        distanceSqr <= cullSqr ? BuildingLevel.Far :
                                        BuildingLevel.Culled; // 거리별 표시 단계 결정

                if (node.Level != desired) // 실제 단계 변화 확인
                {
                    ApplyLevel(node, desired); // Renderer 표시 단계 변경
                    node.Level = desired; // 현재 단계 저장
                }
            }
        }

        private void DiscoverBuildings() // Map 일반 건물 루트를 검색하고 Renderer 분류 캐시 생성
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 본편 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 본편 월드 연결
            buildings.Clear(); // 이전 건물 캐시 제거
            updateIndex = 0; // 순회 인덱스 초기화

            if (world == null) // 본편 월드 존재 확인
            {
                return; // 검색 중단
            }

            Transform blocks = FindByName(world.transform, "Blocks_12x12"); // 일반 도시 블록 루트 검색
            if (blocks == null) // 도시 블록 존재 확인
            {
                return; // 건물 캐시 생성 중단
            }

            for (int blockIndex = 0; blockIndex < blocks.childCount; blockIndex++) // 전체 도시 블록 순회
            {
                Transform block = blocks.GetChild(blockIndex); // 현재 블록 조회

                for (int childIndex = 0; childIndex < block.childCount; childIndex++) // 블록 직접 자식 순회
                {
                    Transform candidate = block.GetChild(childIndex); // 현재 자식 조회
                    if (candidate == null || !IsManagedBuilding(candidate.name)) // Day27 일반 건물 이름 확인
                    {
                        continue; // 다음 자식 처리
                    }

                    BuildingNode node = BuildNode(candidate); // Renderer 분류 캐시 생성
                    if (node != null) // 정상 건물 확인
                    {
                        buildings.Add(node); // LOD 관리 목록 등록
                    }
                }
            }
        }

        private static bool IsManagedBuilding(string name) // Day27 일반 건물 이름 판정
        {
            return !string.IsNullOrEmpty(name) &&
                   (name.StartsWith("BuildingV3_", StringComparison.Ordinal) ||
                    name.StartsWith("BuildingV2_", StringComparison.Ordinal) ||
                    name.StartsWith("BuildingPrefab_", StringComparison.Ordinal)); // 현재와 이전 Prefab 건물 호환
        }

        private static BuildingNode BuildNode(Transform root) // 건물 자식 Renderer를 세 단계로 분류
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true); // 전체 건물 Renderer 조회
            if (renderers == null || renderers.Length == 0) // 시각 요소 존재 확인
            {
                return null; // 빈 건물 제외
            }

            List<Renderer> core = new List<Renderer>(8); // 실루엣 Renderer 목록
            List<Renderer> medium = new List<Renderer>(12); // 중간 디테일 Renderer 목록
            List<Renderer> detail = new List<Renderer>(64); // 작은 디테일 Renderer 목록

            for (int i = 0; i < renderers.Length; i++) // 전체 Renderer 순회
            {
                Renderer renderer = renderers[i]; // 현재 Renderer 조회
                if (renderer == null) // 누락 Renderer 확인
                {
                    continue; // 다음 Renderer 처리
                }

                string itemName = renderer.gameObject.name; // 시각 요소 이름 조회

                if (IsCoreName(itemName)) // 큰 건물 실루엣 확인
                {
                    core.Add(renderer); // 원거리 유지 Renderer 등록
                    renderer.shadowCastingMode = ShadowCastingMode.On; // 큰 본체 그림자 유지
                    renderer.receiveShadows = true; // 본체 그림자 수신 유지
                }
                else if (IsMediumName(itemName)) // 중간 크기 외관 확인
                {
                    medium.Add(renderer); // 중거리 유지 Renderer 등록
                    renderer.shadowCastingMode = ShadowCastingMode.Off; // 작은 외관 그림자 비용 제거
                    renderer.receiveShadows = false; // 작은 외관 그림자 수신 비용 제거
                }
                else // 창문·안테나 등 작은 외관 처리
                {
                    detail.Add(renderer); // 근거리 전용 Renderer 등록
                    renderer.shadowCastingMode = ShadowCastingMode.Off; // 작은 디테일 그림자 제거
                    renderer.receiveShadows = false; // 작은 디테일 그림자 수신 제거
                }
            }

            BuildingNode node = new BuildingNode(); // 건물 캐시 생성
            node.Root = root; // 건물 루트 저장
            node.Core = core.ToArray(); // 실루엣 Renderer 배열 저장
            node.Medium = medium.ToArray(); // 중간 Renderer 배열 저장
            node.Detail = detail.ToArray(); // 근거리 Renderer 배열 저장
            node.Level = BuildingLevel.Unknown; // 첫 갱신 강제 적용
            return node; // 완성 캐시 반환
        }

        private static bool IsCoreName(string name) // 원거리에도 남길 건물 실루엣 이름
        {
            return name == "MainCollision" ||
                   name == "Foundation" ||
                   name == "RoofCap" ||
                   name == "Crown" ||
                   name == "SideAnnex"; // 큰 형태 유지 요소 판정
        }

        private static bool IsMediumName(string name) // 중거리까지 유지할 외관 이름
        {
            return name == "Entrance" ||
                   name == "Awning" ||
                   name == "EntranceAwning" ||
                   name == "FacadeAccent" ||
                   name == "RoofPlant" ||
                   name == "CrownLight"; // 중간 크기 외관 판정
        }

        private static void ApplyLevel(BuildingNode node, BuildingLevel level) // 건물 거리 단계별 Renderer 활성화
        {
            bool coreVisible = level != BuildingLevel.Culled; // 컬링 전까지 본체 표시
            bool mediumVisible = level == BuildingLevel.Near || level == BuildingLevel.Mid; // 중거리까지 외관 표시
            bool detailVisible = level == BuildingLevel.Near; // 근거리에서만 작은 디테일 표시

            SetRenderers(node.Core, coreVisible); // 본체·실루엣 표시 적용
            SetRenderers(node.Medium, mediumVisible); // 중간 외관 표시 적용
            SetRenderers(node.Detail, detailVisible); // 창문·안테나 표시 적용
        }

        private static void SetRenderers(Renderer[] renderers, bool visible) // Renderer 배열 표시 상태 일괄 변경
        {
            if (renderers == null) // 배열 존재 확인
            {
                return; // 처리 생략
            }

            for (int i = 0; i < renderers.Length; i++) // Renderer 배열 순회
            {
                Renderer renderer = renderers[i]; // 현재 Renderer 조회
                if (renderer != null && renderer.enabled != visible) // 실제 상태 변화 확인
                {
                    renderer.enabled = visible; // Renderer 표시 상태 변경
                }
            }
        }

        private static Transform FindByName(Transform root, string name) // 하위 계층 이름 검색
        {
            if (root == null) // 검색 루트 확인
            {
                return null; // 검색 실패
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) // 전체 하위 Transform 순회
            {
                if (child.name == name) // 이름 일치 확인
                {
                    return child; // 첫 결과 반환
                }
            }

            return null; // 검색 실패
        }
    }
}
