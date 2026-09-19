#if UNITY_EDITOR // 월드 에셋 제작 전용
using System; // 잘못된 재질 보고
using System.Collections.Generic; // 공유 재질과 장식 메시 집계
using UnityEditor; // 영구 에셋 저장
using UnityEngine; // 도시 구조물 생성
using UnityEngine.Rendering; // 큰 장식 메시 인덱스

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public sealed class MapGeometry // 충돌 구조와 장식 메시를 분리하는 생성 도구
    {
        public const string Art = "Assets/_Project/Art/Map"; // 포함된 표면과 안내 이미지
        public readonly string Folder; // 이번 생성의 에셋 폴더
        public readonly float WorldSize; // 전체 도시 범위
        private readonly int parkourLayer; // 기존 벽 이동 표면 레이어
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(); // 중복 없는 재질
        private readonly Dictionary<PrimitiveType, Mesh> primitiveMeshes = new Dictionary<PrimitiveType, Mesh>(); // 내장 모형 공유
        private readonly Dictionary<int, Dictionary<Material, List<CombineInstance>>> detail = new Dictionary<int, Dictionary<Material, List<CombineInstance>>>(); // 타일별 장식 집계
        private int meshNumber; // 생성 메시 고유 번호

        public MapGeometry(string folder, float worldSize) // 생성 도구 기본 설정
        {
            Folder = folder; // 격리된 저장 경로
            WorldSize = worldSize; // 타일 분류 기준
            parkourLayer = LayerMask.NameToLayer("ParkourSurface"); // 프로젝트의 기존 레이어 조회
            if (parkourLayer < 0) // 이동 표면 레이어 확인
            {
                throw new InvalidOperationException("ParkourSurface 레이어가 있는 기존 프로젝트에서 실행하세요."); // 임의 레이어 덮어쓰기 방지
            }
            EnsureFolder(Folder + "/Materials"); // 재질 경로 준비
            EnsureFolder(Folder + "/Meshes"); // 합친 메시 경로 준비
        }

        public static void EnsureFolder(string path) // 프로젝트 상대 폴더 생성
        {
            string[] parts = path.Split('/'); // 경로 요소 분리
            string current = parts[0]; // Assets 시작
            for (int i = 1; i < parts.Length; i++) // 하위 폴더 순회
            {
                string next = current + "/" + parts[i]; // 다음 단계 경로
                if (!AssetDatabase.IsValidFolder(next)) // 폴더 존재 확인
                {
                    AssetDatabase.CreateFolder(current, parts[i]); // 누락된 폴더만 생성
                }
                current = next; // 현재 경로 갱신
            }
        }

        public Material Mat(string key) // 도시 공통 재질 조회
        {
            if (materials.TryGetValue(key, out Material existing)) // 이미 생성한 재질 확인
            {
                return existing; // 같은 재질 참조 재사용
            }
            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 파이프라인의 기본 셰이더
            if (shader == null) // 사용 가능한 셰이더 확인
            {
                throw new InvalidOperationException("URP Lit 또는 Standard 셰이더가 필요합니다."); // 분홍 재질 생성 방지
            }
            Material material = new Material(shader); // 이번 Map만 쓰는 재질
            material.name = "Map_" + key; // 소스 재질과 구분
            Color color = new Color(0.28f, 0.32f, 0.35f); // 기본 금속 회색
            string textureName = null; // 표면 이미지 선택
            bool glow = false; // 발광 재질 구분
            switch (key) // 용도별 재질 구성
            {
                case "Concrete": color = new Color(0.63f, 0.65f, 0.64f); textureName = "Concrete"; break; // 도시 콘크리트
                case "Asphalt": color = new Color(0.29f, 0.31f, 0.34f); textureName = "Asphalt"; break; // 도로 표면
                case "Steel": color = new Color(0.19f, 0.24f, 0.29f); break; // 구조 금속
                case "Pale": color = new Color(0.75f, 0.77f, 0.76f); break; // 기업 외장
                case "Rust": color = new Color(0.38f, 0.22f, 0.16f); break; // 산업 구역 외장
                case "Glass": color = new Color(0.09f, 0.22f, 0.30f); break; // 불투명 창 유리 표현
                case "Dark": color = new Color(0.05f, 0.075f, 0.09f); break; // 그늘과 고무
                case "White": color = new Color(0.84f, 0.86f, 0.83f); break; // 도로 표시
                case "Cyan": color = new Color(0.08f, 0.63f, 0.70f); glow = true; break; // 안내 발광
                case "Amber": color = new Color(0.94f, 0.49f, 0.12f); glow = true; break; // 산업 경고 발광
                case "Magenta": color = new Color(0.65f, 0.14f, 0.40f); glow = true; break; // 시장 간판 색상
            }
            material.color = color; // 기본 색상 적용
            if (material.HasProperty("_BaseColor")) // URP 색상 지원 확인
            {
                material.SetColor("_BaseColor", color); // URP 색상 연결
            }
            if (textureName != null) // 이미지 표면이 있는 재질 확인
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + textureName + ".png"); // 제공한 표면 이미지
                material.mainTexture = texture; // 기본 텍스처 연결
                if (material.HasProperty("_BaseMap")) // URP 텍스처 속성 확인
                {
                    material.SetTexture("_BaseMap", texture); // 파이프라인 표면 연결
                }
            }
            if (material.HasProperty("_Smoothness")) // 광택 지원 확인
            {
                material.SetFloat("_Smoothness", key == "Glass" ? 0.62f : 0.18f); // 창과 거친 외장 구분
            }
            if (glow) // 발광 표면 처리
            {
                material.EnableKeyword("_EMISSION"); // 제한적인 발광 사용
                material.SetColor("_EmissionColor", color * 0.65f); // 과한 번짐을 피한 밝기
            }
            material.enableInstancing = true; // 같은 기본 모형 재사용 지원
            AssetDatabase.CreateAsset(material, Folder + "/Materials/" + material.name + ".mat"); // 영구 재질 저장
            materials.Add(key, material); // 다음 생성에서 재사용
            return material; // 저장된 재질 반환
        }

        public Transform Node(Transform parent, string name, Vector3 position) // 편집 가능한 구조 기준점
        {
            GameObject node = new GameObject(name); // 구역 또는 건물 생성
            node.transform.SetParent(parent, false); // 동일 씬의 부모 연결
            node.transform.localPosition = position; // 로컬 배치 적용
            return node.transform; // 생성 기준 반환
        }

        public GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, string material, bool solid = true, bool wall = false) // 단순 충돌체를 쓰는 구조물
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); // 편집 가능한 기본 형상
            item.name = name; // 구조물 용도 기록
            item.transform.SetParent(parent, false); // 건물 또는 구역 연결
            item.transform.localPosition = position; // 기준 위치 적용
            item.transform.localScale = size; // 실제 미터 규격 적용
            item.GetComponent<Renderer>().sharedMaterial = Mat(material); // 공유 재질 연결
            if (!solid) // 장식 충돌 제외
            {
                UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>()); // 보이지 않는 걸림 방지
            }
            if (wall) // 파쿠르 가능한 구조물 확인
            {
                item.layer = parkourLayer; // 기존 벽 이동 판정 연결
            }
            GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // 정적 구조물 최적화 기준
            return item; // 추가 설정 대상 반환
        }

        private Mesh Primitive(PrimitiveType type) // 장식용 내장 메시 조회
        {
            if (!primitiveMeshes.TryGetValue(type, out Mesh mesh)) // 내장 모형 캐시 확인
            {
                GameObject temporary = GameObject.CreatePrimitive(type); // 내장 모형 참조 확보
                mesh = temporary.GetComponent<MeshFilter>().sharedMesh; // 공유 메시 보존
                UnityEngine.Object.DestroyImmediate(temporary); // 임시 오브젝트와 충돌체 제거
                primitiveMeshes.Add(type, mesh); // 다음 장식에서 재사용
            }
            return mesh; // 읽기 전용 내장 메시
        }

        public void Detail(Transform parent, Vector3 position, Vector3 size, string material, PrimitiveType type = PrimitiveType.Cube, Quaternion? rotation = null) // 충돌 없는 장식 메시 집계
        {
            Matrix4x4 matrix = parent.localToWorldMatrix * Matrix4x4.TRS(position, rotation ?? Quaternion.identity, size); // 최종 월드 변환
            Vector3 point = matrix.MultiplyPoint3x4(Vector3.zero); // 타일 분류 위치
            int x = Mathf.Clamp(Mathf.FloorToInt((point.x + WorldSize * 0.5f) / (WorldSize / 3f)), 0, 2); // 가로 장식 타일
            int z = Mathf.Clamp(Mathf.FloorToInt((point.z + WorldSize * 0.5f) / (WorldSize / 3f)), 0, 2); // 세로 장식 타일
            int tile = z * 3 + x; // 세로 우선 인덱스
            if (!detail.TryGetValue(tile, out Dictionary<Material, List<CombineInstance>> bucket)) // 타일 집계 확인
            {
                bucket = new Dictionary<Material, List<CombineInstance>>(); // 타일별 새 집계
                detail.Add(tile, bucket); // 타일 등록
            }
            Material shared = Mat(material); // 같은 표면끼리 합칠 재질
            if (!bucket.TryGetValue(shared, out List<CombineInstance> pieces)) // 재질 집계 확인
            {
                pieces = new List<CombineInstance>(); // 재질별 새 집계
                bucket.Add(shared, pieces); // 표면 등록
            }
            CombineInstance part = new CombineInstance(); // 하나의 장식 형상
            part.mesh = Primitive(type); // 기본 메시 연결
            part.transform = matrix; // 월드 기준 모양 연결
            pieces.Add(part); // 저장 시 함께 합치기
        }

        public GameObject[] BakeDetails(Transform parent) // 타일별 장식을 영구 메시로 저장
        {
            GameObject[] groups = new GameObject[9]; // 아홉 개 장식 표시 단위
            for (int tile = 0; tile < 9; tile++) // 타일 순회
            {
                Transform root = Node(parent, "DetailVisuals_" + tile, Vector3.zero); // 충돌체 없는 표시 묶음
                groups[tile] = root.gameObject; // 런타임 거리 표시 참조
                if (!detail.TryGetValue(tile, out Dictionary<Material, List<CombineInstance>> bucket)) // 빈 타일 확인
                {
                    continue; // 빈 묶음 유지
                }
                foreach (KeyValuePair<Material, List<CombineInstance>> pair in bucket) // 같은 재질 메시 결합
                {
                    Mesh mesh = new Mesh(); // 독립적인 저장 메시
                    mesh.name = "Decor_" + tile + "_" + pair.Key.name; // 타일과 재질 식별
                    mesh.indexFormat = IndexFormat.UInt32; // 많은 장식 정점 지원
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true); // 물리 없는 창과 패널 결합
                    mesh.RecalculateBounds(); // 렌더 가시성 범위 갱신
                    AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + mesh.name + ".asset"); // 씬 재열기 이후 메시 유지
                    GameObject item = Node(root, pair.Key.name, Vector3.zero).gameObject; // 재질당 한 렌더 객체
                    item.AddComponent<MeshFilter>().sharedMesh = mesh; // 영구 메시 참조
                    MeshRenderer renderer = item.AddComponent<MeshRenderer>(); // 표시 기능 연결
                    renderer.sharedMaterial = pair.Key; // 공유 표면 사용
                    renderer.shadowCastingMode = ShadowCastingMode.Off; // 작은 창과 볼트의 그림자 비용 제외
                }
            }
            return groups; // 충돌과 분리된 표시 단위
        }

        public Transform Sign(Transform parent, string key, Vector3 position, float width, float height, float yaw = 0f) // 한글이 깨지지 않는 이미지 안내판
        {
            Transform root = Node(parent, "Sign_" + key, position); // 안내판 기준
            root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 통행 방향에 맞춘 정면
            Detail(root, Vector3.zero, new Vector3(width + 0.2f, height + 0.2f, 0.16f), "Steel"); // 얇은 외곽 프레임
            string materialKey = "Sign_" + key; // 안내판 전용 재질 이름
            if (!materials.TryGetValue(materialKey, out Material material)) // 같은 안내판 재사용
            {
                Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Unlit" : "Unlit/Texture"); // 글씨를 읽기 쉬운 표시 셰이더
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Signs/" + key + ".png"); // 제공된 한글 안내판
                if (shader == null || texture == null) // 필수 안내 자료 확인
                {
                    throw new InvalidOperationException("Map 안내판 이미지 또는 Unlit 셰이더 누락: " + key); // 조용한 누락 방지
                }
                material = new Material(shader); // 이미지 전용 재질
                material.name = materialKey; // 에셋 이름 연결
                material.mainTexture = texture; // 기본 이미지 연결
                if (material.HasProperty("_BaseMap")) // URP 이미지 확인
                {
                    material.SetTexture("_BaseMap", texture); // URP 안내판 연결
                }
                AssetDatabase.CreateAsset(material, Folder + "/Materials/" + materialKey + ".mat"); // 영구 재질 저장
                materials.Add(materialKey, material); // 재사용 목록 등록
            }
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Quad); // 앞에서 읽는 단일 판
            plate.name = "PrintedPanel"; // 이미지 표시 역할
            plate.transform.SetParent(root, false); // 프레임에 연결
            plate.transform.localPosition = Vector3.back * 0.086f; // 프레임 앞쪽 표시
            plate.transform.localScale = new Vector3(width, height, 1f); // 표시 크기 적용
            plate.GetComponent<Renderer>().sharedMaterial = material; // 한글 이미지 연결
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>()); // 얇은 판의 불필요한 충돌 제거
            return root; // 안내판 방향 조절용 반환
        }

        public GameObject Ramp(Transform parent, string name, Vector3 basePosition, float width, float length, float rise, float yaw = 0f) // 평평한 도로에서 옥상으로 이어지는 경사로
        {
            Mesh mesh = new Mesh(); // 경사로의 실제 충돌 형상
            float w = width * 0.5f; // 경사로 반폭
            float l = length * 0.5f; // 경사로 반길이
            mesh.vertices = new Vector3[] // 낮은 남쪽과 높은 북쪽 꼭짓점
            {
                new Vector3(-w, 0f, -l), // 왼쪽 진입
                new Vector3(w, 0f, -l), // 오른쪽 진입
                new Vector3(-w, 0f, l), // 왼쪽 하부
                new Vector3(w, 0f, l), // 오른쪽 하부
                new Vector3(-w, rise, l), // 왼쪽 도착
                new Vector3(w, rise, l) // 오른쪽 도착
            };
            mesh.triangles = new int[] // 바닥과 측면을 포함한 경사면
            {
                0, 4, 5, 0, 5, 1, // 위쪽 보행면
                0, 2, 4, 1, 5, 3, // 양옆 면
                2, 3, 5, 2, 5, 4, // 도착 뒤쪽 면
                0, 1, 3, 0, 3, 2 // 바닥 면
            };
            mesh.RecalculateNormals(); // 표면 방향 계산
            mesh.RecalculateBounds(); // 가시성 범위 계산
            mesh.name = "Ramp_" + meshNumber++; // 중복 없는 메시 이름
            AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + mesh.name + ".asset"); // 사용자 편집과 재열기 보존
            GameObject ramp = Node(parent, name, basePosition).gameObject; // 경사로 위치 기준
            ramp.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); // 올라가는 방향
            ramp.AddComponent<MeshFilter>().sharedMesh = mesh; // 렌더 메시 연결
            ramp.AddComponent<MeshRenderer>().sharedMaterial = Mat("Concrete"); // 보행 표면
            ramp.AddComponent<MeshCollider>().sharedMesh = mesh; // 실제 경사 충돌
            return ramp; // 별도 보행 검사 대상
        }
    }
}
#endif
