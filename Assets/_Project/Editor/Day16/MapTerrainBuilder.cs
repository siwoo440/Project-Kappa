#if UNITY_EDITOR // Terrain 에셋 생성 전용
using System; // 지형 자료 오류 보고
using UnityEditor; // TerrainData와 TerrainLayer 저장
using UnityEngine; // 지형과 표면 구성
using UnityEngine.Rendering; // 렌더 파이프라인 확인

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public static class MapTerrainBuilder // 실제 Terrain 아홉 개의 공유 경계 제작
    {
        public static Terrain[] Build(Transform parent, MapGeometry geometry, int tileSize, int resolution, int seed) // 지형과 충돌과 표면 생성
        {
            MapGeometry.EnsureFolder(geometry.Folder + "/Terrain"); // 독립된 지형 자료 폴더
            TerrainLayer[] layers = CreateLayers(geometry.Folder); // 공통 표면 레이어 생성
            bool urp = GraphicsSettings.currentRenderPipeline != null; // 사용 중인 렌더 파이프라인 확인
            Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Terrain/Lit" : "Nature/Terrain/Standard"); // 지형 전용 셰이더 선택
            if (shader == null) // 지형 셰이더 누락 확인
            {
                throw new InvalidOperationException("현재 렌더 파이프라인의 Terrain/Lit 셰이더가 필요합니다."); // 잘못된 지형 표시 방지
            }
            Material material = new Material(shader); // 일반 건물과 분리된 지형 재질
            material.name = "Map_Terrain"; // 영구 에셋 이름
            material.enableInstancing = true; // 지형 인스턴싱 지원
            AssetDatabase.CreateAsset(material, geometry.Folder + "/Materials/Map_Terrain.mat"); // 지형 재질 저장
            Terrain[] terrains = new Terrain[9]; // 세로 우선으로 아홉 타일 준비
            for (int z = 0; z < 3; z++) // 남쪽에서 북쪽으로 타일 배치
            {
                for (int x = 0; x < 3; x++) // 서쪽에서 동쪽으로 타일 배치
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Map / Terrain", "지형 " + (z * 3 + x + 1) + " / 9", 0.12f + (z * 3 + x) * 0.035f)) // 작업 취소 확인
                    {
                        throw new OperationCanceledException("Terrain 생성 취소"); // 안전한 정리 경로로 이동
                    }
                    TerrainData data = new TerrainData(); // 타일마다 독립된 높이 자료
                    data.name = "TerrainData_" + x + "_" + z; // 편집할 타일 식별
                    data.heightmapResolution = resolution; // 끝점을 포함한 격자 해상도
                    data.size = new Vector3(tileSize, MapTerrainMath.VerticalSize, tileSize); // 실제 미터 크기
                    data.alphamapResolution = 256; // 충분한 표면 페인트 해상도
                    data.baseMapResolution = 512; // 원거리 표면 해상도
                    data.terrainLayers = layers; // 세 가지 표면 레이어 연결
                    float[,] heights = new float[resolution, resolution]; // 세로 가로 순서의 높이 배열
                    for (int iz = 0; iz < resolution; iz++) // 타일 안 세로 표본
                    {
                        for (int ix = 0; ix < resolution; ix++) // 타일 안 가로 표본
                        {
                            heights[iz, ix] = MapTerrainMath.Sample(x, z, ix, iz, resolution, tileSize, seed); // 공유 월드 좌표로 경계까지 계산
                        }
                    }
                    data.SetHeights(0, 0, heights); // 타일당 한 번만 높이 반영
                    Paint(data, x, z, tileSize, seed); // 높이에 따른 표면 혼합
                    AssetDatabase.CreateAsset(data, geometry.Folder + "/Terrain/" + data.name + ".asset"); // 씬과 분리된 편집 가능 데이터
                    GameObject item = Terrain.CreateTerrainGameObject(data); // 실제 Terrain과 TerrainCollider 생성
                    item.name = "Terrain_" + x + "_" + z; // 행과 열 식별
                    item.transform.SetParent(parent, false); // 월드 지형 루트 연결
                    item.transform.localPosition = new Vector3((x - 1.5f) * tileSize, 0f, (z - 1.5f) * tileSize); // 월드 중심 기준 정렬
                    Terrain terrain = item.GetComponent<Terrain>(); // 렌더링 지형 컴포넌트
                    terrain.materialTemplate = material; // 지형 전용 재질 연결
                    terrain.drawInstanced = true; // 지형 드로우 인스턴싱
                    terrain.heightmapPixelError = 5f; // 초기 지형 표시 정밀도
                    terrain.basemapDistance = tileSize * 1.5f; // 원거리 표면 전환 거리
                    terrain.allowAutoConnect = false; // 다른 씬의 Terrain 자동 연결 방지
                    terrain.groupingID = 1616; // 본편 월드 타일 그룹
                    terrain.detailObjectDistance = 0f; // 아직 없는 풀 오브젝트 표시 제외
                    terrains[z * 3 + x] = terrain; // 정해진 타일 순서 보존
                }
            }
            return terrains; // 월드 관리자가 상호 이웃 연결
        }

        private static TerrainLayer[] CreateLayers(string folder) // 페인트 가능한 실제 TerrainLayer 생성
        {
            string[] names = new string[] // 지형 표면 이미지 목록
            {
                "Ground", // 평탄한 도시 주변 지면
                "Rock", // 외곽 바위 경사
                "Moss" // 능선의 낮은 식생 색상
            };
            TerrainLayer[] layers = new TerrainLayer[names.Length]; // 공유 레이어 목록
            for (int i = 0; i < names.Length; i++) // 표면별 에셋 생성
            {
                TerrainLayer layer = new TerrainLayer(); // Unity 지형 레이어
                layer.name = "Map_" + names[i]; // 지형 표면 이름
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapGeometry.Art + "/" + names[i] + ".png"); // 포함된 표면 이미지
                if (layer.diffuseTexture == null) // 부분 설치 확인
                {
                    throw new InvalidOperationException("Map 표면 이미지 누락: " + names[i]); // 무텍스처 지형 생성 방지
                }
                layer.tileSize = new Vector2(8f, 8f); // 실제 미터 기준 반복 크기
                layer.smoothness = 0.1f; // 거친 외부 지면
                AssetDatabase.CreateAsset(layer, folder + "/Terrain/" + layer.name + ".terrainlayer"); // 편집 가능한 레이어 저장
                layers[i] = layer; // 순서 보존
            }
            return layers; // 실제 Terrain 페인트 자료
        }

        private static void Paint(TerrainData data, int tileX, int tileZ, int tileSize, int seed) // 경계가 이어지는 표면 가중치
        {
            int size = data.alphamapResolution; // 표면 격자 크기
            float[,,] weights = new float[size, size, 3]; // 세 개 레이어의 혼합값
            for (int z = 0; z < size; z++) // 표면 세로 순회
            {
                for (int x = 0; x < size; x++) // 표면 가로 순회
                {
                    double wx = MapTerrainMath.Coordinate(tileX, x, size, tileSize); // 전체 좌표의 가로 위치
                    double wz = MapTerrainMath.Coordinate(tileZ, z, size, tileSize); // 전체 좌표의 세로 위치
                    float hill = Mathf.Clamp01((float)(MapTerrainMath.Height(wx, wz, tileSize * 3, seed) - MapTerrainMath.Ground) / 32f); // 경사 구릉의 표면 비율
                    weights[z, x, 0] = 1f - hill; // 낮은 지면 비중
                    weights[z, x, 1] = hill * 0.78f; // 암반 비중
                    weights[z, x, 2] = hill * 0.22f; // 낮은 식생 색 비중
                }
            }
            data.SetAlphamaps(0, 0, weights); // 합계 일인 가중치 적용
        }
    }
}
#endif
