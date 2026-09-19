#if UNITY_EDITOR // 별도의 Map 씬 생성
using System; // 실패 복구와 고유 생성 이름
using System.Collections.Generic; // 기존 빌드 목록 보존
using System.IO; // 씬과 원본 해시 확인
using System.Security.Cryptography; // Test 원본 변경 여부 검사
using UnityEditor; // 에셋 생성과 빌드 목록
using UnityEditor.SceneManagement; // 복사 씬만 편집하고 결과 저장
using UnityEngine; // 본편 월드 연결
using UnityEngine.Rendering; // 환경 조명 방식
using UnityEngine.SceneManagement; // 임시 작업 씬 관리

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public static class MapSceneBuilder // 원본 Test를 수정하지 않는 작업 복사 방식
    {
        public const string MapPath = "Assets/_Project/Scenes/Map.unity"; // 사용자가 요청한 실제 씬 이름
        public const string SourcePath = "Assets/_Project/Scenes/Test.unity"; // 기존 플레이어 설정을 가져올 씬
        public const string BaseCommit = "38a0032d673b031f7608fc06b90941ee4e13f2d5"; // 검토한 원격 기준
        private static bool building; // 생성 중복 실행 방지

        public static string FileHash(string path) // 원본 씬의 읽기 전용 비교
        {
            using (SHA256 hash = SHA256.Create()) // 안정적인 해시 도구
            using (FileStream file = File.OpenRead(path)) // 원본은 읽기 전용으로 열기
            {
                return BitConverter.ToString(hash.ComputeHash(file)); // 생성 전후 비교값
            }
        }

        public static void Build(int tileSize, int resolution, int seed, bool addToBuild) // 필요한 모든 작업을 새 씬에 적용
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
            {
                Debug.LogWarning("Play를 중지하고 임포트와 컴파일이 끝난 뒤 실행하세요."); // 실행 시점 안내
                return; // 실행 중 지형 변경 차단
            }
            if (File.Exists(MapPath)) // 사용자 Map 보호
            {
                Debug.LogWarning("Map.unity가 이미 존재합니다. 기존 맵은 덮어쓰지 않았습니다."); // 중복 생성 안내
                return; // 수동 편집 보존
            }
            if (!File.Exists(SourcePath)) // 플레이어 소스 확인
            {
                Debug.LogError("현재 프로젝트의 Test.unity가 필요합니다."); // 설정된 플레이어 누락 안내
                return; // 빈 플레이어를 임의 생성하지 않음
            }
            if ((tileSize != 256 && tileSize != 512 && tileSize != 1000) || (resolution != 257 && resolution != 513)) // 검증한 생성 범위 확인
            {
                Debug.LogError("타일 256/512/1000과 해상도 257/513 중에서 선택하세요."); // 과도한 지형 메모리 요청 차단
                return; // 지원 범위 밖 생성 중단
            }
            for (int i = 0; i < SceneManager.sceneCount; i++) // 열려 있는 모든 편집 씬 확인
            {
                if (SceneManager.GetSceneAt(i).isDirty) // 저장하지 않은 장면 검사
                {
                    Debug.LogWarning("열려 있는 씬을 저장한 뒤 Map을 생성하세요. 저장하지 않은 편집은 건드리지 않았습니다."); // 원본 편집 보호
                    return; // 임의 저장이나 폐기 금지
                }
            }
            Scene originalActive = SceneManager.GetActiveScene(); // 실패 시 돌려줄 작업 씬
            string sourceHash = FileHash(SourcePath); // 작업 전 Test 원본 해시
            string folder = "Assets/_Project/Generated/Map/Build_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6); // 기존 자료와 겹치지 않는 에셋 묶음
            string stagingPath = folder + "/Map_WorkingCopy.unity"; // 원본이 아닌 작업용 씬
            Scene staging = default(Scene); // 작업 씬 참조
            bool saved = false; // 완성 씬 저장 여부
            building = true; // 재진입 방지
            try // 실패한 생성만 제거하는 작업 범위
            {
                MapGeometry.EnsureFolder(folder); // 이번 생성 폴더 확보
                if (!AssetDatabase.CopyAsset(SourcePath, stagingPath)) // 원본 Test의 에셋 복사
                {
                    throw new IOException("Test 작업 복사본 생성 실패"); // 원본을 대신 수정하지 않음
                }
                staging = EditorSceneManager.OpenScene(stagingPath, OpenSceneMode.Additive); // 다른 경로의 복사본만 열기
                SceneManager.SetActiveScene(staging); // 새 객체를 복사본 씬에만 생성
                MapPlayerImport.Result source = MapPlayerImport.Prepare(staging); // 게임 기능 보존과 훈련 배치 제외
                GameObject root = new GameObject("Map_World"); // 월드 원점은 항상 영점
                MapWorldRoot map = root.AddComponent<MapWorldRoot>(); // 실행 시 이웃 관계와 방문 지점 관리
                MapGeometry geometry = new MapGeometry(folder, tileSize * 3f); // 이번 Map의 독립 재질 도구
                Transform ground = geometry.Node(root.transform, "TerrainGrid_3x3", Vector3.zero); // Terrain 아홉 개의 부모
                Terrain[] tiles = MapTerrainBuilder.Build(ground, geometry, tileSize, resolution, seed); // 실제 지형 생성
                MapCityBuilder city = new MapCityBuilder(geometry, map, source.FootLift, seed); // 기존 플레이어 높이에 맞춘 도시
                MapPoint[] points = city.Build(); // 도로와 지역과 거점 연결
                EditorUtility.DisplayProgressBar("Map", "장식 메시 저장", 0.82f); // 결합 단계 표시
                GameObject[] details = geometry.BakeDetails(root.transform); // 창과 배관을 타일별로 결합
                map.Configure(tiles, source.Player, source.Camera, points, details, tileSize, seed, folder, BaseCommit); // 모든 참조를 하나의 Map 안에 연결
                source.Player.transform.SetPositionAndRotation(points[0].Arrival.position, points[0].Arrival.rotation); // 시작점을 린의 옥상으로 이동
                source.Camera.farClipPlane = Mathf.Max(1600f, tileSize * 3.2f); // 먼 도시 랜드마크 표시
                source.Camera.transform.position = points[0].Arrival.position + new Vector3(0f, 3f, -6f); // 편집 직후에도 스폰을 볼 수 있는 위치
                source.Camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f); // 도시 방향의 초기 시선
                ConfigureLighting(staging); // 현재 복사 씬의 환경만 조정
                Physics.SyncTransforms(); // 새 도로와 지형의 실제 충돌 갱신
                EditorUtility.DisplayProgressBar("Map", "Terrain 경계와 스폰 검사", 0.91f); // 최종 검사 진행
                int checks = MapBuildValidation.Validate(map); // 실제 저장 자료와 안전 지점 검사
                if (FileHash(SourcePath) != sourceHash) // 원본 Test 변경 여부 검사
                {
                    throw new InvalidOperationException("Test 파일이 생성 중 외부에서 변경되었습니다. 새 Map 저장을 중단합니다."); // 동시 편집 충돌 보고
                }
                SaveGeneratedAssets(folder); // 생성 폴더의 자료만 저장
                if (File.Exists(MapPath)) // 긴 생성 중 외부에서 만든 파일 확인
                {
                    throw new IOException("Map 파일이 생성 중 새로 생겼습니다. 덮어쓰기를 중단합니다."); // 동시에 생성된 사용자 파일 보호
                }
                if (!EditorSceneManager.SaveScene(staging, MapPath, saveAsCopy: true)) // 원본과 다른 이름의 실제 Map 씬 저장
                {
                    throw new IOException("Map.unity 저장 실패"); // 저장 결과 확인
                }
                saved = true; // 결과 에셋을 보존할 시점
                EditorSceneManager.CloseScene(staging, true); // 임시 씬과 중복 카메라 닫기
                AssetDatabase.DeleteAsset(stagingPath); // 생성에만 쓴 복사본 파일 제거
                EditorSceneManager.OpenScene(MapPath, OpenSceneMode.Single); // 실제 재생 대상은 Map 하나만 열기
                if (addToBuild) // 사용자가 선택한 빌드 목록 연결
                {
                    AddBuildScene(); // 기존 시작 씬 순서 보존
                }
                Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<MapWorldRoot>()?.Player; // 실제 Map의 스폰 선택
                SceneView.lastActiveSceneView?.FrameSelected(); // 새 도시의 거점으로 편집 시점 이동
                Debug.Log("Map 저장 완료 · Terrain 3×3 · " + (tileSize * 3) + "m × " + (tileSize * 3) + "m · 일반 건물 " + city.BuildingCount + "개 · 검사 " + checks + "항목 · Test 원본 유지"); // 실제 생성 결과 안내
            }
            catch (Exception error) // 부분 생성 복구
            {
                if (staging.IsValid() && staging.isLoaded) // 임시 씬이 열렸는지 확인
                {
                    EditorSceneManager.CloseScene(staging, true); // 실패한 장면을 저장하지 않고 닫기
                }
                if (!saved && AssetDatabase.IsValidFolder(folder)) // 완성되지 않은 이번 생성 자료만 확인
                {
                    AssetDatabase.DeleteAsset(folder); // 기존 에셋과 Test를 제외한 부분 자료 제거
                }
                if (originalActive.IsValid() && originalActive.isLoaded) // 기존 편집 장면 유지 여부
                {
                    SceneManager.SetActiveScene(originalActive); // 원래 작업 대상으로 복귀
                }
                Debug.LogException(error); // 구체적인 생성 실패 보고
            }
            finally // 에디터 상태 복구
            {
                EditorUtility.ClearProgressBar(); // 진행 창 정리
                building = false; // 재실행 허용
            }
        }

        private static void SaveGeneratedAssets(string folder) // 사용자 외부 에셋은 저장하지 않기
        {
            string[] folders = new string[] // 이번 생성 폴더만 저장
            {
                folder // 외부 에셋을 제외한 경로
            };
            foreach (string guid in AssetDatabase.FindAssets("", folders)) // 이번 생성 폴더만 조회
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)); // 실제 생성 자료 읽기
                if (asset != null) // 폴더 이외 자료 확인
                {
                    AssetDatabase.SaveAssetIfDirty(asset); // 변경된 생성 자료만 저장
                }
            }
        }

        private static void ConfigureLighting(Scene scene) // 맵 전용 외부 환경
        {
            RenderSettings.ambientMode = AmbientMode.Trilight; // 지붕과 벽을 읽기 쉬운 환경광
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.68f); // 하늘색 주변광
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.43f, 0.47f); // 거리와 벽의 중간 밝기
            RenderSettings.ambientGroundColor = new Color(0.23f, 0.25f, 0.28f); // 바닥 반사광
            RenderSettings.fog = true; // 먼 외곽의 거리감
            RenderSettings.fogMode = FogMode.Linear; // 일정한 거리 안에서만 안개
            RenderSettings.fogColor = new Color(0.47f, 0.56f, 0.64f); // 외곽 하늘과 연결
            RenderSettings.fogStartDistance = 800f; // 가까운 이동 경로는 선명하게 유지
            RenderSettings.fogEndDistance = 2400f; // 먼 도시 실루엣 유지
            foreach (Light light in TrainingCenterMigration.Components<Light>(scene)) // 복사본의 광원만 확인
            {
                if (light.type == LightType.Directional) // 원래 태양 광원 확인
                {
                    light.transform.rotation = Quaternion.Euler(46f, -32f, 0f); // 도시 면을 구분하는 비스듬한 조명
                    light.color = new Color(1f, 0.94f, 0.86f); // 과하지 않은 따뜻한 직사광
                    light.intensity = 1.15f; // 낮은 네온 대비를 읽기 쉬운 밝기
                    RenderSettings.sun = light; // 현재 Map의 주 광원 연결
                }
            }
        }

        private static void AddBuildScene() // 기존 시작 씬을 바꾸지 않는 목록 추가
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); // 기존 설정 보존
            if (!scenes.Exists(value => value.path == MapPath)) // 중복 경로 확인
            {
                scenes.Add(new EditorBuildSettingsScene(MapPath, true)); // 목록 끝에 Map 추가
                EditorBuildSettings.scenes = scenes.ToArray(); // 바뀐 목록 적용
            }
        }
    }
}
#endif
