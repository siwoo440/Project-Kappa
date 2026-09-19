#if UNITY_EDITOR // 실제 Map 자료와 물리 검증
using System; // 검사 실패 보고
using System.Collections.Generic; // 중복 데이터와 이름 검사
using UnityEditor; // 저장 에셋과 누락 스크립트 검사
using UnityEngine; // 지형과 스폰 충돌 검사
using UnityEngine.SceneManagement; // 현재 Map에 한정한 검사

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public static class MapBuildValidation // 저장 전과 수동 편집 이후 공통 검사
    {
        private static void Require(bool condition, string message) // 실패한 조건을 정확히 보고
        {
            if (!condition) // 통과 여부 확인
            {
                throw new InvalidOperationException("Map 검사: " + message); // 실패 지점 전달
            }
        }

        [MenuItem("Project K/Day 16/Validate Map")] // 사용자 Map 설정 검사
        public static void ValidateMenu() // 실제 열린 Map 검사
        {
            if (Application.isPlaying) // 편집 상태 확인
            {
                Debug.LogWarning("Play를 중지하고 Map을 검사하세요."); // 실행 중 물리 상태 간섭 방지
                return; // 편집기 검증만 허용
            }
            MapWorldRoot[] roots = TrainingCenterMigration.Components<MapWorldRoot>(SceneManager.GetActiveScene()); // 선택된 씬만 확인
            if (roots.Length != 1) // 검사할 Map 구분
            {
                Debug.LogError("Map.unity를 열고 활성 씬으로 선택하세요."); // 정확한 검사 대상 안내
                return; // Test 전체를 검색하지 않음
            }
            try // 검사 결과를 Console로 표시
            {
                int checks = Validate(roots[0]); // 실제 타일과 도착점 검사
                Debug.Log("Map 검사 통과: " + checks + "항목 · Terrain 경계 · 단일 플레이어 · 안전 도착점 · 자료 참조"); // 실제 실행 결과
            }
            catch (Exception error) // 실패 항목 보고
            {
                Debug.LogException(error); // 자세한 실패 위치 표시
            }
        }

        public static int Validate(MapWorldRoot map) // 생성 도중에도 사용할 실제 자료 검사
        {
            Require(map != null && map.Tiles != null && map.Tiles.Length == 9, "Terrain 아홉 개가 필요합니다."); // 타일 수 확인
            Scene scene = map.gameObject.scene; // 물리 검사와 객체 조회의 범위
            HashSet<TerrainData> unique = new HashSet<TerrainData>(); // 독립된 지형 데이터 검사
            int checks = 1; // 완료한 조건 수
            map.ConnectTerrains(); // 명시적인 상호 이웃 관계 복구
            for (int z = 0; z < 3; z++) // 세로 타일 검사
            {
                for (int x = 0; x < 3; x++) // 가로 타일 검사
                {
                    Terrain tile = map.Tile(x, z); // 현재 타일 조회
                    Require(tile != null && tile.gameObject.scene == scene && tile.enabled, "Terrain 누락 또는 다른 씬 참조"); // 지형 소속과 활성 상태
                    TerrainData data = tile.terrainData; // 실제 높이 자료
                    Require(data != null && unique.Add(data) && EditorUtility.IsPersistent(data), "각 TerrainData는 서로 다른 저장 에셋이어야 합니다."); // 복사 타일 데이터 공유 방지
                    Require(Mathf.Approximately(data.size.x, map.TileSize) && Mathf.Approximately(data.size.z, map.TileSize), "타일 크기 불일치"); // 타일 간 규격 확인
                    TerrainCollider body = tile.GetComponent<TerrainCollider>(); // 실제 지면 충돌
                    Require(body != null && body.enabled && body.terrainData == data, "TerrainCollider 연결 누락"); // 렌더와 물리 지형 일치
                    Vector3 expected = new Vector3((x - 1.5f) * map.TileSize, 0f, (z - 1.5f) * map.TileSize); // 격자 원점 위치
                    Require(Vector3.Distance(tile.transform.position, expected) < 0.002f, "Terrain 격자 위치가 어긋났습니다."); // 경계 틈 검사
                    Require(tile.leftNeighbor == map.Tile(x - 1, z) && tile.topNeighbor == map.Tile(x, z + 1) && tile.rightNeighbor == map.Tile(x + 1, z) && tile.bottomNeighbor == map.Tile(x, z - 1), "이웃 Terrain 연결 불일치"); // 양방향 연결 검사
                    Require(data.terrainLayers != null && data.terrainLayers.Length == 3 && tile.materialTemplate != null, "Terrain 표면 자료 누락"); // 페인트와 재질 확인
                    checks += 7; // 타일 기본 조건 누적
                }
            }
            checks += ValidateSeams(map); // 실제 저장 높이 경계 비교
            Require(map.Player != null && map.PlayCamera != null && map.Player.scene == scene && map.PlayCamera.gameObject.scene == scene, "플레이어와 카메라 소속 확인"); // 다른 Test 객체 참조 방지
            Require(TrainingCenterMigration.Components<PlayerMovement>(scene).Length == 1, "PlayerMovement는 하나여야 합니다."); // 중복 플레이어 차단
            Require(TrainingCenterMigration.Components<ThirdPersonCamera>(scene).Length == 1, "3인칭 카메라는 하나여야 합니다."); // 중복 시점 차단
            Require(TrainingCenterMigration.Components<TrainingCenterRoot>(scene).Length == 0 && TrainingCenterMigration.Components<BalanceSessionRunner>(scene).Length == 0, "훈련센터 또는 계측 관리자가 Map에 남아 있습니다."); // 본편과 시험 기능 분리
            Require(TrainingCenterMigration.Components<EnemyActor>(scene).Length == 0, "16일차 Map에는 아직 적을 배치하지 않습니다."); // 범위 밖 적 자동 생성 방지
            checks += 5; // 게임 기능 조건 누적
            SerializedProperty target = new SerializedObject(map.PlayCamera.GetComponent<ThirdPersonCamera>()).FindProperty("target"); // 실제 저장된 추적 대상
            Require(target != null && target.objectReferenceValue == map.Player.transform, "카메라가 Map 플레이어를 바라봐야 합니다."); // 이전 Test 참조 방지
            Require(map.Places != null && map.Places.Length == 5, "주요 장소 다섯 개가 필요합니다."); // 필수 방문 지역 확인
            checks += 2; // 장소와 카메라 검사 누적
            Physics.SyncTransforms(); // 새 배치 물리 위치 동기화
            HashSet<string> ids = new HashSet<string>(); // 고정 장소 ID 중복 검사
            foreach (MapPoint place in map.Places) // 모든 실제 도착점 검사
            {
                Require(place != null && place.Arrival != null && !string.IsNullOrEmpty(place.PlaceId) && ids.Add(place.PlaceId), "장소 ID 또는 도착점 누락·중복"); // 미션 배치 전 기준 보장
                Require(SafeCapsule(map.Player, place.Arrival.position, place.Arrival.rotation, scene), place.DisplayName + " 도착점이 벽이나 천장과 겹칩니다."); // 캐릭터 전체 크기 확인
                Require(HasFloor(place.Arrival.position, scene, map.Player.transform), place.DisplayName + " 도착점 아래 보행면이 없습니다."); // 공중 스폰 방지
                checks += 3; // 방문점 조건 누적
            }
            foreach (GameObject detail in map.DetailVisuals ?? new GameObject[0]) // 표시 전용 묶음 확인
            {
                Require(detail != null && detail.GetComponentsInChildren<Collider>(true).Length == 0, "거리 표시 장식 안에 충돌체가 있습니다."); // 보이지 않는 물리 장애물 방지
                checks++; // 장식 구분 확인
            }
            foreach (GameObject root in scene.GetRootGameObjects()) // 저장할 전체 객체 조회
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) // 숨겨진 자식도 확인
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing Script: " + item.name); // 누락 스크립트 검사
                }
            }
            checks++; // 스크립트 무결성 확인
            return checks; // 실제 실행한 검사 수 반환
        }

        public static int ValidateSeams(MapWorldRoot map) // 실제 높이 배열의 공통 경계 검사
        {
            int checks = 0; // 비교한 경계 수
            for (int z = 0; z < 3; z++) // 타일 세로 순회
            {
                for (int x = 0; x < 3; x++) // 타일 가로 순회
                {
                    TerrainData data = map.Tile(x, z).terrainData; // 현재 높이 자료
                    int size = data.heightmapResolution; // 현재 해상도
                    if (x < 2) // 오른쪽 이웃이 있는 경우
                    {
                        TerrainData right = map.Tile(x + 1, z).terrainData; // 이웃 자료
                        Require(right.heightmapResolution == size, "좌우 해상도 불일치"); // 동일한 격자 확인
                        float[,] a = data.GetHeights(size - 1, 0, 1, size); // 현재 오른쪽 경계
                        float[,] b = right.GetHeights(0, 0, 1, size); // 이웃 왼쪽 경계
                        for (int i = 0; i < size; i++) // 모든 높이 표본 비교
                        {
                            Require(Mathf.Abs(a[i, 0] - b[i, 0]) * data.size.y < 0.002f, "좌우 Terrain 높이 경계 불일치"); // 실제 높이 이밀리미터 허용
                        }
                        checks++; // 좌우 경계 통과
                    }
                    if (z < 2) // 위쪽 이웃이 있는 경우
                    {
                        TerrainData top = map.Tile(x, z + 1).terrainData; // 북쪽 자료
                        Require(top.heightmapResolution == size, "앞뒤 해상도 불일치"); // 동일한 격자 확인
                        float[,] a = data.GetHeights(0, size - 1, size, 1); // 현재 위쪽 경계
                        float[,] b = top.GetHeights(0, 0, size, 1); // 이웃 아래쪽 경계
                        for (int i = 0; i < size; i++) // 모든 경계 표본 비교
                        {
                            Require(Mathf.Abs(a[0, i] - b[0, i]) * data.size.y < 0.002f, "앞뒤 Terrain 높이 경계 불일치"); // 앞뒤 틈 검사
                        }
                        checks++; // 앞뒤 경계 통과
                    }
                }
            }
            return checks; // 실제 인접 경계 수
        }

        private static bool SafeCapsule(GameObject player, Vector3 position, Quaternion rotation, Scene scene) // 캐릭터 전체의 공간 확보 검사
        {
            CharacterController body = player.GetComponent<CharacterController>(); // 원래 캡슐 규격
            if (body == null) // 충돌체 누락 확인
            {
                return false; // 안전 판정 불가
            }
            float radius = Mathf.Max(0.05f, body.radius - 0.02f); // 피부 여유를 제외한 반경
            Vector3 center = position + rotation * body.center; // 도착 지점의 캡슐 중심
            Vector3 offset = rotation * Vector3.up * Mathf.Max(0f, body.height * 0.5f - radius); // 위아래 구 중심
            foreach (Collider hit in Physics.OverlapCapsule(center + offset, center - offset, radius, ~0, QueryTriggerInteraction.Ignore)) // 전체 캡슐 겹침 조회
            {
                if (hit.gameObject.scene == scene && !hit.transform.IsChildOf(player.transform) && hit.transform != player.transform) // 다른 씬과 자기 몸 제외
                {
                    return false; // 실제 장애물과 겹침
                }
            }
            return true; // 캐릭터가 들어갈 수 있는 도착점
        }

        private static bool HasFloor(Vector3 point, Scene scene, Transform player) // 도착점 아래 실제 바닥 확인
        {
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 0.05f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)) // 지면 또는 옥상 확인
            {
                if (hit.collider.gameObject.scene == scene && !hit.transform.IsChildOf(player) && hit.transform != player && hit.normal.y > 0.7f) // 자기 몸과 수직 벽 제외
                {
                    return true; // 서 있을 보행면 존재
                }
            }
            return false; // 공중 도착점 보고
        }

        [MenuItem("Project K/Day 16/Test Terrain Rules")] // 씬 생성 없이 좌표 규칙 검증
        public static void TestRules() // 실제 C# 높이 계산의 경계와 범위 검사
        {
            try // 첫 실패 위치 보고
            {
                int[] sizes = new int[] // 지원하는 실제 타일 길이
                {
                    256, // 작은 밀집 도시
                    512, // 기본 도시
                    1000 // 큰 타일 선택
                };
                int[] resolutions = new int[] // 지원하는 높이 해상도
                {
                    257, // 기본 표본
                    513 // 정밀 표본
                };
                int checks = 0; // 검증한 높이쌍 수
                foreach (int metres in sizes) // 크기마다 공유 좌표 확인
                {
                    foreach (int resolution in resolutions) // 각 해상도 확인
                    {
                        for (int tile = 0; tile < 2; tile++) // 내부 경계 두 개
                        {
                            for (int sample = 0; sample < resolution; sample++) // 전체 경계 표본 확인
                            {
                                float left = MapTerrainMath.Sample(tile, 0, resolution - 1, sample, resolution, metres, 1616); // 왼쪽 타일 끝
                                float right = MapTerrainMath.Sample(tile + 1, 0, 0, sample, resolution, metres, 1616); // 오른쪽 타일 시작
                                float bottom = MapTerrainMath.Sample(0, tile, sample, resolution - 1, resolution, metres, 1616); // 아래쪽 타일 끝
                                float top = MapTerrainMath.Sample(0, tile + 1, sample, 0, resolution, metres, 1616); // 위쪽 타일 시작
                                Require(left == right && bottom == top && left >= 0f && left <= 1f, "공유 격자 경계 또는 높이 범위 오류"); // 정확히 같은 부동소수점 값 확인
                                checks++; // 검사 수 누적
                            }
                        }
                        Require(Math.Abs(MapTerrainMath.Height(0, 0, metres * 3, 1616) - MapTerrainMath.Ground) < 0.000001, "도시 중심 지면 오류"); // 도로 평탄화 기준
                    }
                }
                Debug.Log("Terrain 좌표 규칙 통과: " + checks + "개 표본 · 타일 크기 3종 · 해상도 2종"); // 실제 C# 실행 결과
            }
            catch (Exception error) // 실패한 규칙 안내
            {
                Debug.LogException(error); // 구체적인 입력과 위치 확인
            }
        }
    }
}
#endif
