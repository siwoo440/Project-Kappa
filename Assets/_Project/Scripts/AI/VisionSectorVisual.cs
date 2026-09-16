using UnityEngine; // 유니티 기본 기능

[ExecuteAlways] // 에디터 상시 실행
[DisallowMultipleComponent] // 중복 부착 방지
public sealed class VisionSectorVisual : MonoBehaviour // 감시 영역 시각화
{
    [SerializeField] private Transform yawSource; // 회전 기준 참조
    [SerializeField] private float radius = 18f; // 시야 거리
    [SerializeField] private float angle = 90f; // 시야 각도
    [SerializeField] private float groundOffset = 0.03f; // 바닥 띄움 높이
    [SerializeField] private int segments = 36; // 부채꼴 분할 수
    [SerializeField] private Color sectorColor = new Color(1f, 0.25f, 0.25f, 0.10f); // 부채꼴 색상

    private Transform visualRoot; // 시각화 루트
    private MeshFilter meshFilter; // 메쉬 필터
    private MeshRenderer meshRenderer; // 메쉬 렌더러
    private Mesh mesh; // 생성 메쉬
    private Material runtimeMaterial; // 런타임 재질
    private float cachedRadius = -1f; // 거리 캐시
    private float cachedAngle = -1f; // 각도 캐시
    private int cachedSegments = -1; // 분할 수 캐시
    private Color cachedColor = Color.clear; // 색상 캐시

    public void Configure(Transform source, float sectorRadius, float sectorAngle, float sectorGroundOffset, Color color) // 외부 설정 적용
    {
        yawSource = source; // 회전 기준 저장
        radius = sectorRadius; // 시야 거리 저장
        angle = sectorAngle; // 시야 각도 저장
        groundOffset = sectorGroundOffset; // 바닥 높이 저장
        sectorColor = color; // 색상 저장
        EnsureVisual(); // 시각화 구조 보장
        RebuildMeshIfNeeded(true); // 메쉬 강제 갱신
        SyncVisualTransform(); // 위치와 회전 적용
    }

    private void OnEnable() // 활성화 처리
    {
        EnsureVisual(); // 시각화 구조 보장
        RebuildMeshIfNeeded(true); // 메쉬 강제 갱신
        SyncVisualTransform(); // 위치와 회전 적용
    }

    private void OnValidate() // 값 변경 처리
    {
        radius = Mathf.Max(0.1f, radius); // 최소 거리 보정
        angle = Mathf.Clamp(angle, 1f, 179f); // 각도 범위 보정
        segments = Mathf.Clamp(segments, 8, 96); // 분할 수 보정
        groundOffset = Mathf.Max(0f, groundOffset); // 바닥 높이 보정
        EnsureVisual(); // 시각화 구조 보장
        RebuildMeshIfNeeded(true); // 메쉬 강제 갱신
        SyncVisualTransform(); // 위치와 회전 적용
    }

    private void LateUpdate() // 매 프레임 갱신
    {
        EnsureVisual(); // 시각화 구조 보장
        RebuildMeshIfNeeded(false); // 필요시 메쉬 갱신
        SyncVisualTransform(); // 위치와 회전 적용
        ApplyColor(); // 색상 적용
    }

    private void EnsureVisual() // 시각화 구조 보장
    {
        if (visualRoot == null) // 시각화 루트 확인
        {
            Transform found = transform.Find("__VisionSector"); // 기존 루트 조회
            if (found != null) // 기존 루트 존재 확인
            {
                visualRoot = found; // 기존 루트 저장
            }
            else // 루트 미존재 처리
            {
                GameObject rootObject = new GameObject("__VisionSector"); // 루트 오브젝트 생성
                visualRoot = rootObject.transform; // 루트 참조 저장
                visualRoot.SetParent(transform, false); // 부모 연결
            }
        }

        if (meshFilter == null) // 메쉬 필터 확인
        {
            meshFilter = visualRoot.GetComponent<MeshFilter>(); // 기존 메쉬 필터 조회
            if (meshFilter == null) // 메쉬 필터 미존재 확인
            {
                meshFilter = visualRoot.gameObject.AddComponent<MeshFilter>(); // 메쉬 필터 추가
            }
        }

        if (meshRenderer == null) // 메쉬 렌더러 확인
        {
            meshRenderer = visualRoot.GetComponent<MeshRenderer>(); // 기존 렌더러 조회
            if (meshRenderer == null) // 렌더러 미존재 확인
            {
                meshRenderer = visualRoot.gameObject.AddComponent<MeshRenderer>(); // 메쉬 렌더러 추가
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 그림자 비활성
            meshRenderer.receiveShadows = false; // 그림자 수신 비활성
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; // 모션 벡터 비활성
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; // 라이트 프로브 비활성
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; // 반사 프로브 비활성
        }

        if (mesh == null) // 메쉬 확인
        {
            mesh = new Mesh(); // 새 메쉬 생성
            mesh.name = "VisionSectorMesh"; // 메쉬 이름 지정
            meshFilter.sharedMesh = mesh; // 메쉬 연결
        }

        if (runtimeMaterial == null) // 재질 확인
        {
            runtimeMaterial = CreateTransparentMaterial(); // 반투명 재질 생성
        }

        if (meshRenderer.sharedMaterial != runtimeMaterial) // 재질 연결 상태 확인
        {
            meshRenderer.sharedMaterial = runtimeMaterial; // 재질 연결
        }
    }

    private void RebuildMeshIfNeeded(bool force) // 메쉬 갱신 처리
    {
        if (!force && Mathf.Approximately(cachedRadius, radius) && Mathf.Approximately(cachedAngle, angle) && cachedSegments == segments) // 메쉬 값 변화 여부 확인
        {
            return; // 갱신 중단
        }

        cachedRadius = radius; // 거리 캐시 저장
        cachedAngle = angle; // 각도 캐시 저장
        cachedSegments = segments; // 분할 수 캐시 저장

        int vertexCount = segments + 2; // 정점 수 계산
        Vector3[] vertices = new Vector3[vertexCount]; // 정점 배열 생성
        int[] triangles = new int[segments * 3]; // 삼각형 배열 생성
        Vector2[] uv = new Vector2[vertexCount]; // UV 배열 생성

        vertices[0] = Vector3.zero; // 중심 정점 저장
        uv[0] = new Vector2(0.5f, 0f); // 중심 UV 저장

        float halfAngle = angle * 0.5f; // 반각 계산
        for (int i = 0; i <= segments; i++) // 외곽 정점 순회
        {
            float t = i / (float)segments; // 보간 값 계산
            float currentAngle = -halfAngle + angle * t; // 현재 각도 계산
            float rad = currentAngle * Mathf.Deg2Rad; // 라디안 변환
            float x = Mathf.Sin(rad) * radius; // x 좌표 계산
            float z = Mathf.Cos(rad) * radius; // z 좌표 계산
            vertices[i + 1] = new Vector3(x, 0f, z); // 외곽 정점 저장
            uv[i + 1] = new Vector2(t, 1f); // 외곽 UV 저장
        }

        for (int i = 0; i < segments; i++) // 삼각형 생성
        {
            int index = i * 3; // 삼각형 시작 인덱스 계산
            triangles[index + 0] = 0; // 중심 정점 연결
            triangles[index + 1] = i + 1; // 현재 외곽 정점 연결
            triangles[index + 2] = i + 2; // 다음 외곽 정점 연결
        }

        mesh.Clear(); // 기존 메쉬 초기화
        mesh.vertices = vertices; // 정점 적용
        mesh.triangles = triangles; // 삼각형 적용
        mesh.uv = uv; // UV 적용
        mesh.RecalculateNormals(); // 법선 재계산
        mesh.RecalculateBounds(); // 경계 재계산
    }

    private void SyncVisualTransform() // 위치와 회전 동기화
    {
        if (visualRoot == null) // 루트 확인
        {
            return; // 처리 중단
        }

        visualRoot.position = new Vector3(transform.position.x, transform.position.y + groundOffset, transform.position.z); // 바닥 위치 적용

        Transform source = yawSource != null ? yawSource : transform; // 회전 기준 선택
        Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up); // 수평 전방 계산
        if (forward.sqrMagnitude <= 0.0001f) // 전방 벡터 확인
        {
            forward = Vector3.forward; // 기본 전방 적용
        }

        visualRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up); // 회전 적용
    }

    private void ApplyColor() // 색상 적용
    {
        if (runtimeMaterial == null) // 재질 확인
        {
            return; // 처리 중단
        }

        if (cachedColor == sectorColor) // 색상 변화 여부 확인
        {
            return; // 처리 중단
        }

        cachedColor = sectorColor; // 색상 캐시 저장

        if (runtimeMaterial.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            runtimeMaterial.SetColor("_BaseColor", sectorColor); // URP 색상 적용
        }

        if (runtimeMaterial.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            runtimeMaterial.SetColor("_Color", sectorColor); // 기본 색상 적용
        }
    }

    private Material CreateTransparentMaterial() // 반투명 재질 생성
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP Unlit 셰이더 조회
        if (shader == null) // URP 셰이더 확인
        {
            shader = Shader.Find("Standard"); // 기본 셰이더 조회
        }

        if (shader == null) // 기본 셰이더 확인
        {
            shader = Shader.Find("Sprites/Default"); // 스프라이트 셰이더 조회
        }

        Material material = new Material(shader); // 새 재질 생성
        material.name = "RuntimeVisionSectorMaterial"; // 재질 이름 지정

        if (shader.name.Contains("Universal Render Pipeline")) // URP 셰이더 확인
        {
            if (material.HasProperty("_Surface")) // 표면 속성 확인
            {
                material.SetFloat("_Surface", 1f); // 투명 표면 적용
            }

            if (material.HasProperty("_Blend")) // 블렌드 속성 확인
            {
                material.SetFloat("_Blend", 0f); // 알파 블렌드 적용
            }

            if (material.HasProperty("_SrcBlend")) // 소스 블렌드 확인
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); // 소스 블렌드 적용
            }

            if (material.HasProperty("_DstBlend")) // 대상 블렌드 확인
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); // 대상 블렌드 적용
            }

            if (material.HasProperty("_ZWrite")) // ZWrite 속성 확인
            {
                material.SetFloat("_ZWrite", 0f); // 깊이 쓰기 비활성
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 투명 렌더 큐 적용
        }
        else if (shader.name == "Standard") // 기본 셰이더 확인
        {
            material.SetFloat("_Mode", 3f); // 투명 모드 적용
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); // 소스 블렌드 적용
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); // 대상 블렌드 적용
            material.SetInt("_ZWrite", 0); // 깊이 쓰기 비활성
            material.DisableKeyword("_ALPHATEST_ON"); // 알파 테스트 비활성
            material.EnableKeyword("_ALPHABLEND_ON"); // 알파 블렌드 활성
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // 프리멀티플라이 비활성
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 투명 렌더 큐 적용
        }

        return material; // 재질 반환
    }
}
