using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 부착 방지
[RequireComponent(typeof(CharacterController))] // 캐릭터 컨트롤러 필수 지정
public sealed class PlayerDirectionIndicator : MonoBehaviour // 플레이어 방향 표시 관리자
{
    [Header("Visibility")] // 표시 설정 구분
    [SerializeField] private float movementThreshold = 0.15f; // 표시 최소 이동 속도
    [SerializeField] private float fadeSpeed = 10f; // 표시 전환 속도
    [SerializeField] private bool requireGrounded = true; // 지상 상태 표시 제한

    [Header("Ground Placement")] // 바닥 배치 설정 구분
    [SerializeField] private LayerMask groundMask = ~0; // 바닥 감지 마스크
    [SerializeField] private float groundRayStartHeight = 0.6f; // 바닥 레이 시작 높이
    [SerializeField] private float groundRayDistance = 3.2f; // 바닥 레이 거리
    [SerializeField] private float surfaceOffset = 0.035f; // 바닥 겹침 방지 높이

    [Header("Ring")] // 원형 링 설정 구분
    [SerializeField] private float ringRadius = 0.72f; // 링 반경
    [SerializeField] private float ringThickness = 0.045f; // 링 두께
    [SerializeField] private int ringSegments = 64; // 링 분할 수
    [SerializeField] private Color ringColor = new Color(0.15f, 0.88f, 0.95f, 0.34f); // 링 색상

    [Header("Arrow")] // 화살표 설정 구분
    [SerializeField] private float arrowStart = 0.56f; // 화살표 시작 거리
    [SerializeField] private float arrowTip = 1.02f; // 화살표 끝 거리
    [SerializeField] private float arrowHalfWidth = 0.22f; // 화살표 반폭
    [SerializeField] private Color arrowColor = new Color(0.25f, 0.96f, 1f, 0.78f); // 화살표 색상

    private CharacterController controller; // 캐릭터 컨트롤러 참조
    private PlayerMovement movement; // 플레이어 이동 참조
    private Transform visualRoot; // 시각화 루트
    private MeshRenderer ringRenderer; // 링 렌더러
    private MeshRenderer arrowRenderer; // 화살표 렌더러
    private Material ringMaterial; // 링 재질
    private Material arrowMaterial; // 화살표 재질
    private Vector3 previousPosition; // 이전 위치
    private float visibility; // 현재 표시 비율
    private bool suppressed; // 행동 중 숨김 상태
    private bool initialized; // 초기화 완료 상태

    public bool IsSuppressed => suppressed; // 숨김 상태 읽기
    public bool IsVisible => visibility > 0.01f; // 표시 상태 읽기

    private void Awake() // 초기화 처리
    {
        controller = GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        movement = GetComponent<PlayerMovement>(); // 플레이어 이동 조회
        previousPosition = transform.position; // 초기 위치 저장
        EnsureVisuals(); // 시각 요소 생성
        ApplyVisibility(0f); // 초기 숨김 적용
        initialized = true; // 초기화 상태 저장
    }

    private void OnEnable() // 활성화 처리
    {
        if (!initialized) // 초기화 여부 확인
        {
            return; // 중복 처리 방지
        }

        previousPosition = transform.position; // 현재 위치 저장
        EnsureVisuals(); // 시각 요소 보장
    }

    private void LateUpdate() // 매 프레임 표시 처리
    {
        EnsureVisuals(); // 시각 요소 보장
        float horizontalSpeed = GetHorizontalSpeed(); // 실제 수평 이동 속도 계산
        bool moving = horizontalSpeed > movementThreshold; // 이동 상태 계산
        bool groundedAllowed = !requireGrounded || controller == null || controller.isGrounded; // 지상 표시 조건 계산
        bool movementAllowed = movement == null || movement.MovementEnabled || horizontalSpeed > movementThreshold; // 이동 시스템 조건 계산
        bool shouldShow = moving && groundedAllowed && movementAllowed && !suppressed; // 최종 표시 조건 계산
        float targetVisibility = shouldShow ? 1f : 0f; // 목표 표시 비율 계산
        visibility = Mathf.MoveTowards(visibility, targetVisibility, fadeSpeed * Time.deltaTime); // 표시 비율 보간
        UpdateGroundPose(); // 바닥 위치와 방향 갱신
        ApplyVisibility(visibility); // 표시 투명도 적용
        previousPosition = transform.position; // 현재 위치 저장
    }

    public void SetSuppressed(bool value) // 특정 행동 숨김 설정
    {
        suppressed = value; // 숨김 상태 저장

        if (suppressed) // 숨김 활성 확인
        {
            visibility = 0f; // 표시 비율 즉시 초기화
            ApplyVisibility(0f); // 즉시 숨김 적용
        }
    }

    public void SetVisibleDuringGroundMovementOnly(bool value) // 지상 이동 제한 설정
    {
        requireGrounded = value; // 지상 제한 상태 저장
    }

    private float GetHorizontalSpeed() // 실제 수평 이동 속도 계산
    {
        if (controller != null && controller.enabled) // 캐릭터 컨트롤러 사용 가능 확인
        {
            Vector3 controllerVelocity = controller.velocity; // 컨트롤러 실제 속도 조회
            controllerVelocity.y = 0f; // 수직 속도 제거
            return controllerVelocity.magnitude; // 실제 수평 속도 반환
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f); // 프레임 시간 보정
        Vector3 frameVelocity = (transform.position - previousPosition) / deltaTime; // 위치 변화 기반 속도 계산
        frameVelocity.y = 0f; // 수직 속도 제거
        return frameVelocity.magnitude; // 대체 수평 속도 반환
    }

    private void UpdateGroundPose() // 바닥 위치와 방향 갱신
    {
        if (visualRoot == null) // 시각화 루트 확인
        {
            return; // 처리 중단
        }

        Vector3 rayOrigin = transform.position + Vector3.up * groundRayStartHeight; // 바닥 레이 시작 위치 계산
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore); // 바닥 충돌 목록 조회
        bool foundGround = false; // 바닥 검색 상태 초기화
        RaycastHit nearestHit = default; // 가장 가까운 바닥 충돌 초기화
        float nearestDistance = float.MaxValue; // 가장 가까운 거리 초기화

        for (int i = 0; i < hits.Length; i++) // 충돌 목록 순회
        {
            Transform hitTransform = hits[i].transform; // 충돌 객체 조회

            if (hitTransform == transform || hitTransform.IsChildOf(transform)) // 자기 자신 충돌 확인
            {
                continue; // 자기 충돌 제외
            }

            if (hits[i].distance >= nearestDistance) // 기존 바닥보다 먼 충돌 확인
            {
                continue; // 먼 충돌 제외
            }

            nearestDistance = hits[i].distance; // 가장 가까운 거리 저장
            nearestHit = hits[i]; // 가장 가까운 충돌 저장
            foundGround = true; // 바닥 검색 성공 저장
        }

        Vector3 surfaceNormal = foundGround ? nearestHit.normal : Vector3.up; // 바닥 법선 선택
        Vector3 surfacePosition = foundGround ? nearestHit.point : GetFallbackGroundPosition(); // 바닥 위치 선택
        visualRoot.position = surfacePosition + surfaceNormal * surfaceOffset; // 바닥 위 위치 적용
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal); // 바닥 기준 전방 계산

        if (forward.sqrMagnitude <= 0.0001f) // 전방 벡터 확인
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal); // 기본 전방 계산
        }

        visualRoot.rotation = Quaternion.LookRotation(forward.normalized, surfaceNormal); // 플레이어 방향 회전 적용
    }

    private Vector3 GetFallbackGroundPosition() // 대체 발밑 위치 계산
    {
        float halfHeight = controller != null ? controller.height * 0.5f : 1f; // 캐릭터 반높이 계산
        return transform.position + Vector3.down * halfHeight; // 대체 발밑 위치 반환
    }

    private void EnsureVisuals() // 시각 요소 생성 보장
    {
        if (visualRoot == null) // 시각화 루트 확인
        {
            Transform existing = transform.Find("__DirectionIndicator"); // 기존 시각화 루트 검색

            if (existing != null) // 기존 루트 존재 확인
            {
                visualRoot = existing; // 기존 루트 저장
            }
            else // 기존 루트 미존재 처리
            {
                GameObject rootObject = new GameObject("__DirectionIndicator"); // 시각화 루트 생성
                visualRoot = rootObject.transform; // 시각화 루트 저장
                visualRoot.SetParent(transform, false); // 플레이어 하위 연결
            }
        }

        if (ringRenderer == null) // 링 렌더러 확인
        {
            ringRenderer = CreateMeshVisual("Ring", BuildRingMesh(), ringColor); // 링 시각화 생성
        }

        if (arrowRenderer == null) // 화살표 렌더러 확인
        {
            arrowRenderer = CreateMeshVisual("Arrow", BuildArrowMesh(), arrowColor); // 화살표 시각화 생성
        }
    }

    private MeshRenderer CreateMeshVisual(string objectName, Mesh targetMesh, Color color) // 메쉬 시각화 생성
    {
        Transform existing = visualRoot.Find(objectName); // 기존 자식 검색
        GameObject visualObject = existing != null ? existing.gameObject : new GameObject(objectName); // 기존 또는 새 객체 선택
        visualObject.transform.SetParent(visualRoot, false); // 시각화 루트 연결
        visualObject.transform.localPosition = Vector3.zero; // 로컬 위치 초기화
        visualObject.transform.localRotation = Quaternion.identity; // 로컬 회전 초기화
        visualObject.transform.localScale = Vector3.one; // 로컬 크기 초기화

        MeshFilter filter = visualObject.GetComponent<MeshFilter>(); // 메쉬 필터 조회

        if (filter == null) // 메쉬 필터 누락 확인
        {
            filter = visualObject.AddComponent<MeshFilter>(); // 메쉬 필터 추가
        }

        filter.sharedMesh = targetMesh; // 메쉬 연결
        MeshRenderer renderer = visualObject.GetComponent<MeshRenderer>(); // 메쉬 렌더러 조회

        if (renderer == null) // 메쉬 렌더러 누락 확인
        {
            renderer = visualObject.AddComponent<MeshRenderer>(); // 메쉬 렌더러 추가
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 그림자 생성 비활성
        renderer.receiveShadows = false; // 그림자 수신 비활성
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; // 라이트 프로브 비활성
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; // 반사 프로브 비활성
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; // 모션 벡터 비활성
        renderer.sortingOrder = 80; // 투명 시각화 정렬 순서 적용

        Material material = CreateTransparentMaterial(objectName + "_Material", color); // 투명 재질 생성
        renderer.sharedMaterial = material; // 재질 연결

        if (objectName == "Ring") // 링 여부 확인
        {
            ringMaterial = material; // 링 재질 저장
        }
        else // 화살표 처리
        {
            arrowMaterial = material; // 화살표 재질 저장
        }

        return renderer; // 생성 렌더러 반환
    }

    private Mesh BuildRingMesh() // 원형 링 메쉬 생성
    {
        int segmentCount = Mathf.Clamp(ringSegments, 16, 128); // 링 분할 수 보정
        float outerRadius = Mathf.Max(0.1f, ringRadius); // 외곽 반경 보정
        float innerRadius = Mathf.Max(0.01f, outerRadius - Mathf.Max(0.01f, ringThickness)); // 내부 반경 계산
        Vector3[] vertices = new Vector3[(segmentCount + 1) * 2]; // 링 정점 배열 생성
        int[] triangles = new int[segmentCount * 6]; // 링 삼각형 배열 생성

        for (int i = 0; i <= segmentCount; i++) // 링 정점 순회
        {
            float t = i / (float)segmentCount; // 링 보간값 계산
            float angleValue = t * Mathf.PI * 2f; // 링 각도 계산
            float x = Mathf.Sin(angleValue); // x 방향 계산
            float z = Mathf.Cos(angleValue); // z 방향 계산
            vertices[i * 2] = new Vector3(x * innerRadius, 0f, z * innerRadius); // 내부 정점 저장
            vertices[i * 2 + 1] = new Vector3(x * outerRadius, 0f, z * outerRadius); // 외부 정점 저장
        }

        for (int i = 0; i < segmentCount; i++) // 링 삼각형 순회
        {
            int vertexIndex = i * 2; // 현재 정점 시작 인덱스 계산
            int triangleIndex = i * 6; // 현재 삼각형 시작 인덱스 계산
            triangles[triangleIndex] = vertexIndex; // 첫 정점 연결
            triangles[triangleIndex + 1] = vertexIndex + 3; // 두 번째 정점 연결
            triangles[triangleIndex + 2] = vertexIndex + 1; // 세 번째 정점 연결
            triangles[triangleIndex + 3] = vertexIndex; // 네 번째 정점 연결
            triangles[triangleIndex + 4] = vertexIndex + 2; // 다섯 번째 정점 연결
            triangles[triangleIndex + 5] = vertexIndex + 3; // 여섯 번째 정점 연결
        }

        Mesh mesh = new Mesh(); // 링 메쉬 생성
        mesh.name = "PlayerDirectionRing"; // 링 메쉬 이름 지정
        mesh.vertices = vertices; // 링 정점 적용
        mesh.triangles = triangles; // 링 삼각형 적용
        mesh.RecalculateNormals(); // 링 법선 재계산
        mesh.RecalculateBounds(); // 링 경계 재계산
        return mesh; // 링 메쉬 반환
    }

    private Mesh BuildArrowMesh() // 방향 화살표 메쉬 생성
    {
        float baseDistance = Mathf.Max(0.1f, arrowStart); // 화살표 시작 거리 보정
        float tipDistance = Mathf.Max(baseDistance + 0.05f, arrowTip); // 화살표 끝 거리 보정
        float halfWidth = Mathf.Max(0.04f, arrowHalfWidth); // 화살표 반폭 보정
        Vector3[] vertices = new Vector3[5]; // 화살표 정점 배열 생성
        vertices[0] = new Vector3(-halfWidth * 0.45f, 0f, baseDistance); // 좌측 안쪽 정점 저장
        vertices[1] = new Vector3(-halfWidth, 0f, baseDistance + (tipDistance - baseDistance) * 0.34f); // 좌측 외곽 정점 저장
        vertices[2] = new Vector3(0f, 0f, tipDistance); // 전방 끝 정점 저장
        vertices[3] = new Vector3(halfWidth, 0f, baseDistance + (tipDistance - baseDistance) * 0.34f); // 우측 외곽 정점 저장
        vertices[4] = new Vector3(halfWidth * 0.45f, 0f, baseDistance); // 우측 안쪽 정점 저장
        int[] triangles = new int[] { 0, 1, 2, 0, 2, 4, 4, 2, 3 }; // 화살표 삼각형 배열 생성

        Mesh mesh = new Mesh(); // 화살표 메쉬 생성
        mesh.name = "PlayerDirectionArrow"; // 화살표 메쉬 이름 지정
        mesh.vertices = vertices; // 화살표 정점 적용
        mesh.triangles = triangles; // 화살표 삼각형 적용
        mesh.RecalculateNormals(); // 화살표 법선 재계산
        mesh.RecalculateBounds(); // 화살표 경계 재계산
        return mesh; // 화살표 메쉬 반환
    }

    private Material CreateTransparentMaterial(string materialName, Color color) // 투명 재질 생성
    {
        Shader shader = Shader.Find("Sprites/Default"); // 투명 스프라이트 셰이더 조회

        if (shader == null) // 스프라이트 셰이더 누락 확인
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP Unlit 셰이더 조회
        }

        Material material = new Material(shader); // 새 재질 생성
        material.name = materialName; // 재질 이름 지정
        material.color = color; // 기본 색상 적용
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 투명 렌더 순서 적용

        if (material.HasProperty("_BaseColor")) // URP 기본 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }

        return material; // 생성 재질 반환
    }

    private void ApplyVisibility(float alphaMultiplier) // 표시 투명도 적용
    {
        ApplyMaterialAlpha(ringMaterial, ringColor, alphaMultiplier); // 링 투명도 적용
        ApplyMaterialAlpha(arrowMaterial, arrowColor, alphaMultiplier); // 화살표 투명도 적용
        bool rendererEnabled = alphaMultiplier > 0.001f; // 렌더러 표시 여부 계산

        if (ringRenderer != null) // 링 렌더러 확인
        {
            ringRenderer.enabled = rendererEnabled; // 링 표시 상태 적용
        }

        if (arrowRenderer != null) // 화살표 렌더러 확인
        {
            arrowRenderer.enabled = rendererEnabled; // 화살표 표시 상태 적용
        }
    }

    private static void ApplyMaterialAlpha(Material material, Color baseColor, float alphaMultiplier) // 재질 투명도 적용
    {
        if (material == null) // 재질 확인
        {
            return; // 처리 중단
        }

        Color finalColor = baseColor; // 최종 색상 초기화
        finalColor.a *= Mathf.Clamp01(alphaMultiplier); // 최종 투명도 계산
        material.color = finalColor; // 기본 색상 적용

        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", finalColor); // URP 색상 적용
        }
    }

    private void OnDestroy() // 삭제 처리
    {
        if (!Application.isPlaying) // 플레이 모드 여부 확인
        {
            return; // 에디터 삭제 처리 생략
        }

        if (ringMaterial != null) // 링 재질 확인
        {
            Destroy(ringMaterial); // 링 재질 삭제
        }

        if (arrowMaterial != null) // 화살표 재질 확인
        {
            Destroy(arrowMaterial); // 화살표 재질 삭제
        }
    }
}
