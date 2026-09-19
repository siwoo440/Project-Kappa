using UnityEngine; // 본편 월드와 Terrain 연결
using UnityEngine.InputSystem; // 기존 플레이어 입력 연결

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    [DisallowMultipleComponent] // 월드 관리자 중복 방지
    public sealed class MapWorldRoot : MonoBehaviour // 미션 없는 본편 도시의 기반
    {
        [SerializeField] private Terrain[] tiles; // 세로 우선으로 저장한 Terrain 아홉 개
        [SerializeField] private GameObject player; // Test에서 별도로 복사한 플레이어
        [SerializeField] private Camera playCamera; // 복사한 단일 플레이 카메라
        [SerializeField] private MapPoint[] places; // 주요 도시 방문 지점
        [SerializeField] private GameObject[] detailVisuals; // 물리 판정이 없는 장식 전용 묶음
        [SerializeField] private float tileSize = 512f; // 한 타일의 실제 길이
        [SerializeField] private string generationFolder; // 이 월드만 사용하는 생성 에셋 폴더
        [SerializeField] private string sourceCommit; // 기준 소스 커밋
        [SerializeField] private int seed; // 지형과 건물 배치 시드
        private float nextDetailCheck; // 장식 거리 검사 시각
        public Terrain[] Tiles => tiles; // 에디터 검사 참조
        public GameObject Player => player; // 실제 플레이어 조회
        public Camera PlayCamera => playCamera; // 실제 카메라 조회
        public MapPoint[] Places => places; // 방문 지점 조회
        public GameObject[] DetailVisuals => detailVisuals; // 장식 전용 참조
        public float TileSize => tileSize; // 타일 길이 조회
        public float WorldSize => tileSize * MapTerrainMath.Grid; // 전체 길이 조회
        public string GenerationFolder => generationFolder; // 저장된 자료 경로

        public void Configure(Terrain[] terrain, GameObject actor, Camera camera, MapPoint[] locations, GameObject[] decoration, float size, int randomSeed, string folder, string commit) // 새 Map 씬 참조 저장
        {
            tiles = terrain; // 지형 목록 연결
            player = actor; // 플레이어 연결
            playCamera = camera; // 카메라 연결
            places = locations; // 방문 지점 연결
            detailVisuals = decoration; // 충돌 없는 장식 목록 연결
            tileSize = size; // 생성 당시 타일 길이 저장
            seed = randomSeed; // 생성 시드 보존
            generationFolder = folder; // 수동 편집 가능한 자료 위치
            sourceCommit = commit; // 제작 기준 기록
            ConnectTerrains(); // 저장 전 지형 연결
        }

        public Terrain Tile(int x, int z) // 경계 밖 타일은 없는 것으로 처리
        {
            return tiles != null && tiles.Length == 9 && x >= 0 && x < 3 && z >= 0 && z < 3 ? tiles[z * 3 + x] : null; // 안전한 이웃 조회
        }

        public void ConnectTerrains() // 재생과 씬 재열기 후 이웃 관계 복구
        {
            for (int z = 0; z < 3; z++) // 세로 타일 순회
            {
                for (int x = 0; x < 3; x++) // 가로 타일 순회
                {
                    Terrain terrain = Tile(x, z); // 현재 타일 조회
                    if (terrain != null) // 실제 타일 존재 확인
                    {
                        terrain.SetNeighbors(Tile(x - 1, z), Tile(x, z + 1), Tile(x + 1, z), Tile(x, z - 1)); // 왼쪽 위 오른쪽 아래 연결
                    }
                }
            }
        }

        private void OnEnable() // 실행 시 지형 관계 복구
        {
            ConnectTerrains(); // 전체 타일에 상호 연결 적용
        }

        private void Start() // 기존 이동 초기화 이후 거점 연결
        {
            if (player == null || places == null || places.Length == 0 || places[0] == null || places[0].Arrival == null) // 필수 참조 검사
            {
                Debug.LogError("Map의 플레이어와 거점 도착점을 확인하세요."); // 누락 설정 보고
                return; // 잘못된 위치 적용 방지
            }
            PlayerMovement movement = player.GetComponent<PlayerMovement>(); // 기존 이동 기능 조회
            movement?.SetSpawnPoint(places[0].Arrival.position, places[0].Arrival.rotation); // 추락 복귀도 새 옥상 거점 사용
            movement?.Configure(playCamera); // 단일 Map 카메라로 이동 기준 연결
            playCamera?.GetComponent<ThirdPersonCamera>()?.Configure(player.transform, player.GetComponent<PlayerInput>()); // 기존 시점 입력 유지
            player.GetComponent<PlayerEquipmentManager>()?.Notify("연무 자유 탐험 · F 안내 단말기 · 미션과 적 배치는 이후 단계"); // 현재 구현 범위 안내
        }

        private void Update() // 물리 지형은 유지하고 먼 장식만 숨김
        {
            if (player == null || detailVisuals == null || Time.unscaledTime < nextDetailCheck) // 유효한 갱신 시점 확인
            {
                return; // 반복 검사 절약
            }
            nextDetailCheck = Time.unscaledTime + 0.5f; // 반초 간격으로 장식 검사
            for (int i = 0; i < detailVisuals.Length; i++) // 타일별 장식 순회
            {
                GameObject detail = detailVisuals[i]; // 장식 묶음 참조
                Terrain terrain = tiles != null && i < tiles.Length ? tiles[i] : null; // 같은 타일의 지형 조회
                if (detail == null || terrain == null) // 누락 자료 검사
                {
                    continue; // 다른 타일은 계속 처리
                }
                Vector3 center = terrain.transform.position + new Vector3(tileSize * 0.5f, 60f, tileSize * 0.5f); // 장식 범위 중심
                Bounds area = new Bounds(center, new Vector3(tileSize, 200f, tileSize)); // 높이를 포함한 표시 구역
                bool visible = area.SqrDistance(player.transform.position) < tileSize * tileSize * 1.44f; // 가까운 타일의 장식 표시
                if (detail.activeSelf != visible) // 표시 상태 변화 확인
                {
                    detail.SetActive(visible); // 충돌체 없는 장식만 전환
                }
            }
        }

        public bool Travel(MapPoint place, GameObject user) // 기존 F 상호작용에서만 안전 이동
        {
            PlayerEquipmentManager equipment = user != null ? user.GetComponent<PlayerEquipmentManager>() : null; // 행동 제한 확인
            if (place == null || place.Arrival == null || user != player || equipment == null || !equipment.CanUseEquipment()) // 잘못된 사용자와 특수행동 제외
            {
                return false; // 이동 중단
            }
            PlayerMovement movement = user.GetComponent<PlayerMovement>(); // 이동 속도 초기화 대상
            CharacterController body = user.GetComponent<CharacterController>(); // 실제 플레이어 충돌체
            bool enabled = body != null && body.enabled; // 이전 상태 보존
            user.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 조준과 발사 정리
            if (body != null) // 충돌체 확인
            {
                body.enabled = false; // 좌표 이동 중 충돌 보정 정지
            }
            user.transform.SetPositionAndRotation(place.Arrival.position, place.Arrival.rotation); // 검증한 도착점 적용
            movement?.SetHorizontalVelocity(Vector3.zero); // 이동 관성 초기화
            if (movement != null) // 실제 이동 기능 확인
            {
                movement.VerticalVelocity = 0f; // 낙하 속도 초기화
            }
            if (body != null) // 원래 충돌체 복구
            {
                body.enabled = enabled; // 이전 활성 상태로 복귀
            }
            playCamera?.GetComponent<ThirdPersonCamera>()?.Configure(user.transform, user.GetComponent<PlayerInput>()); // 도착 방향으로 카메라 연결
            Physics.SyncTransforms(); // 순간 이동 결과를 물리에 반영
            equipment.Notify(place.DisplayName + " 도착"); // 기존 장비 HUD로만 안내
            return true; // 이동 성공 반환
        }

        private void OnDisable() // 저장 편집용 장식 표시 복구
        {
            foreach (GameObject detail in detailVisuals ?? new GameObject[0]) // 존재하는 장식 순회
            {
                if (detail != null) // 남은 장식 확인
                {
                    detail.SetActive(true); // 재시작할 때 숨김 상태 잔류 방지
                }
            }
        }
    }
}
