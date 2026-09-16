using UnityEngine; // 유니티 기본 기능
using UnityEngine.UI; // 유니티 UI 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class DetectionBillboardUI : MonoBehaviour // 탐지 머리 위 UI 관리자
{
    [Header("References")] // 참조 설정 구분
    [SerializeField] private DetectionSensor sensor; // 탐지 센서 참조
    [SerializeField] private Camera targetCamera; // 플레이어 카메라 참조

    [Header("Layout")] // 배치 설정 구분
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.8f, 0f); // 머리 위 위치 보정
    [SerializeField] private Vector2 canvasSize = new Vector2(140f, 28f); // 캔버스 크기
    [SerializeField] private Vector2 iconSize = new Vector2(22f, 22f); // 아이콘 크기
    [SerializeField] private Vector2 barSize = new Vector2(92f, 14f); // 게이지 크기
    [SerializeField] private float worldScale = 0.01f; // 월드 UI 배율

    [Header("Colors")] // 색상 설정 구분
    [SerializeField] private Color idleColor = new Color(0.24f, 0.88f, 0.36f, 1f); // 평상 색상
    [SerializeField] private Color suspiciousColor = new Color(1f, 0.78f, 0.12f, 1f); // 의심 색상
    [SerializeField] private Color detectedColor = new Color(1f, 0.25f, 0.25f, 1f); // 발견 색상
    [SerializeField] private Color searchingColor = new Color(0.55f, 0.78f, 1f, 1f); // 수색 색상
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.88f); // 배경 색상
    [SerializeField] private Color frameColor = new Color(0.92f, 0.96f, 1f, 0.96f); // 프레임 색상

    private Canvas worldCanvas; // 월드 캔버스 참조
    private RectTransform canvasRect; // 캔버스 사각형 참조
    private RectTransform fillRect; // 채움 바 참조
    private Image fillImage; // 채움 이미지 참조
    private Image eyeImage; // 눈 이미지 참조

    private void Awake() // 초기 설정
    {
        if (sensor == null) // 센서 누락 확인
        {
            sensor = GetComponent<DetectionSensor>(); // 동일 객체 센서 조회
        }

        if (targetCamera == null) // 카메라 누락 확인
        {
            targetCamera = Camera.main; // 메인 카메라 조회
        }

        EnsureUI(); // UI 생성 보장
        ApplyOffset(); // 위치 보정 적용
    }

    private void LateUpdate() // 매 프레임 UI 갱신
    {
        if (sensor == null) // 센서 참조 확인
        {
            return; // 갱신 중단
        }

        if (targetCamera == null) // 카메라 참조 확인
        {
            targetCamera = Camera.main; // 메인 카메라 재조회
        }

        ApplyOffset(); // 머리 위 위치 적용
        FaceCamera(); // 플레이어 방향 정렬
        RefreshVisuals(); // 탐지 게이지 갱신
    }

    public void Configure(DetectionSensor targetSensor, Camera camera, Vector3 offset) // UI 설정 적용
    {
        sensor = targetSensor; // 센서 참조 저장
        targetCamera = camera; // 카메라 참조 저장
        worldOffset = offset; // 위치 보정 저장
        EnsureUI(); // UI 생성 보장
        ApplyOffset(); // 위치 보정 적용
    }

    private void EnsureUI() // UI 생성 보장
    {
        Transform existing = transform.Find("WorldUI"); // 기존 UI 조회
        if (existing != null) // 기존 UI 존재 확인
        {
            worldCanvas = existing.GetComponent<Canvas>(); // 기존 캔버스 조회
            canvasRect = existing.GetComponent<RectTransform>(); // 기존 사각형 조회
            Transform fill = existing.Find("Panel/BarFrame/BarFill"); // 기존 채움 바 조회
            fillRect = fill != null ? fill.GetComponent<RectTransform>() : null; // 기존 채움 사각형 조회
            fillImage = fill != null ? fill.GetComponent<Image>() : null; // 기존 채움 이미지 조회
            Transform eye = existing.Find("Panel/EyeIcon"); // 기존 눈 아이콘 조회
            eyeImage = eye != null ? eye.GetComponent<Image>() : null; // 기존 눈 이미지 조회
        }

        if (worldCanvas == null || canvasRect == null || fillRect == null || fillImage == null || eyeImage == null) // UI 구조 확인
        {
            RebuildUI(); // UI 재생성
        }
    }

    private void RebuildUI() // UI 재생성
    {
        Transform oldUI = transform.Find("WorldUI"); // 기존 UI 조회
        if (oldUI != null) // 기존 UI 확인
        {
            DestroyImmediate(oldUI.gameObject); // 기존 UI 삭제
        }

        GameObject canvasObject = new GameObject("WorldUI", typeof(RectTransform)); // 캔버스 객체 생성
        canvasObject.transform.SetParent(transform, false); // 센서 객체에 연결
        worldCanvas = canvasObject.AddComponent<Canvas>(); // 캔버스 추가
        worldCanvas.renderMode = RenderMode.WorldSpace; // 월드 공간 설정
        worldCanvas.sortingOrder = 50; // UI 정렬 우선순위 설정
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>(); // 캔버스 스케일러 추가
        scaler.dynamicPixelsPerUnit = 12f; // 픽셀 밀도 설정
        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>(); // 레이캐스터 추가
        raycaster.enabled = false; // 입력 레이캐스터 비활성화
        canvasRect = canvasObject.GetComponent<RectTransform>(); // 캔버스 사각형 조회
        canvasRect.sizeDelta = canvasSize; // 캔버스 크기 적용
        canvasObject.transform.localScale = Vector3.one * worldScale; // 월드 배율 적용

        GameObject panelObject = CreateUIObject("Panel", canvasObject.transform); // 패널 생성
        RectTransform panelRect = panelObject.GetComponent<RectTransform>(); // 패널 사각형 조회
        panelRect.sizeDelta = canvasSize; // 패널 크기 적용
        Image panelImage = panelObject.AddComponent<Image>(); // 패널 이미지 추가
        panelImage.color = backgroundColor; // 패널 색상 적용

        GameObject eyeObject = CreateUIObject("EyeIcon", panelObject.transform); // 눈 아이콘 객체 생성
        RectTransform eyeRect = eyeObject.GetComponent<RectTransform>(); // 눈 아이콘 사각형 조회
        eyeRect.anchorMin = new Vector2(0f, 0.5f); // 눈 앵커 최소 설정
        eyeRect.anchorMax = new Vector2(0f, 0.5f); // 눈 앵커 최대 설정
        eyeRect.pivot = new Vector2(0f, 0.5f); // 눈 피벗 설정
        eyeRect.anchoredPosition = new Vector2(8f, 0f); // 눈 위치 설정
        eyeRect.sizeDelta = iconSize; // 눈 크기 설정
        eyeImage = eyeObject.AddComponent<Image>(); // 눈 이미지 추가
        eyeImage.sprite = CreateEyeSprite(); // 눈 스프라이트 적용
        eyeImage.color = idleColor; // 눈 초기 색상 적용

        GameObject frameObject = CreateUIObject("BarFrame", panelObject.transform); // 게이지 프레임 생성
        RectTransform frameRect = frameObject.GetComponent<RectTransform>(); // 프레임 사각형 조회
        frameRect.anchorMin = new Vector2(0f, 0.5f); // 프레임 앵커 최소 설정
        frameRect.anchorMax = new Vector2(0f, 0.5f); // 프레임 앵커 최대 설정
        frameRect.pivot = new Vector2(0f, 0.5f); // 프레임 피벗 설정
        frameRect.anchoredPosition = new Vector2(38f, 0f); // 프레임 위치 설정
        frameRect.sizeDelta = barSize; // 프레임 크기 설정
        Image frameImage = frameObject.AddComponent<Image>(); // 프레임 이미지 추가
        frameImage.color = frameColor; // 프레임 색상 적용

        GameObject backgroundObject = CreateUIObject("BarBackground", frameObject.transform); // 게이지 배경 생성
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>(); // 배경 사각형 조회
        backgroundRect.anchorMin = new Vector2(0f, 0.5f); // 배경 앵커 최소 설정
        backgroundRect.anchorMax = new Vector2(0f, 0.5f); // 배경 앵커 최대 설정
        backgroundRect.pivot = new Vector2(0f, 0.5f); // 배경 피벗 설정
        backgroundRect.anchoredPosition = new Vector2(2f, 0f); // 배경 위치 설정
        backgroundRect.sizeDelta = new Vector2(barSize.x - 4f, barSize.y - 4f); // 배경 크기 설정
        Image backgroundImage = backgroundObject.AddComponent<Image>(); // 배경 이미지 추가
        backgroundImage.color = backgroundColor; // 배경 색상 적용

        GameObject fillObject = CreateUIObject("BarFill", frameObject.transform); // 채움 바 생성
        fillRect = fillObject.GetComponent<RectTransform>(); // 채움 사각형 조회
        fillRect.anchorMin = new Vector2(0f, 0.5f); // 채움 앵커 최소 설정
        fillRect.anchorMax = new Vector2(0f, 0.5f); // 채움 앵커 최대 설정
        fillRect.pivot = new Vector2(0f, 0.5f); // 채움 피벗 설정
        fillRect.anchoredPosition = new Vector2(2f, 0f); // 채움 위치 설정
        fillRect.sizeDelta = new Vector2(0f, barSize.y - 4f); // 초기 채움 크기 설정
        fillImage = fillObject.AddComponent<Image>(); // 채움 이미지 추가
        fillImage.color = idleColor; // 초기 채움 색상 적용
    }

    private void ApplyOffset() // UI 위치 보정 적용
    {
        if (worldCanvas == null) // 캔버스 확인
        {
            return; // 위치 적용 중단
        }

        Transform uiTransform = worldCanvas.transform; // UI 트랜스폼 조회
        uiTransform.position = transform.position + worldOffset; // 월드 위치 적용
        uiTransform.localScale = Vector3.one * worldScale; // 월드 배율 유지
    }

    private void FaceCamera() // 플레이어 카메라 바라보기
    {
        if (worldCanvas == null || targetCamera == null) // 참조 확인
        {
            return; // 회전 중단
        }

        Vector3 direction = worldCanvas.transform.position - targetCamera.transform.position; // 카메라 반대 방향 계산
        if (direction.sqrMagnitude <= 0.0001f) // 방향 크기 확인
        {
            return; // 회전 중단
        }

        worldCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // UI만 카메라 방향으로 회전
    }

    private void RefreshVisuals() // 탐지 시각 상태 갱신
    {
        if (fillRect == null || fillImage == null || eyeImage == null) // UI 참조 확인
        {
            return; // 갱신 중단
        }

        float progress = Mathf.Clamp01(sensor.DetectionProgress); // 탐지 진행도 보정
        fillRect.sizeDelta = new Vector2((barSize.x - 4f) * progress, barSize.y - 4f); // 탐지 게이지 폭 적용
        Color color = GetStateColor(sensor.State); // 상태 색상 조회
        fillImage.color = color; // 게이지 색상 적용
        eyeImage.color = color; // 눈 아이콘 색상 적용
    }

    private Color GetStateColor(DetectionState state) // 상태별 색상 반환
    {
        switch (state) // 상태 분기
        {
            case DetectionState.Suspicious: // 의심 상태
                return suspiciousColor; // 의심 색상 반환
            case DetectionState.Detected: // 발견 상태
                return detectedColor; // 발견 색상 반환
            case DetectionState.Searching: // 수색 상태
                return searchingColor; // 수색 색상 반환
            default: // 평상 상태
                return idleColor; // 평상 색상 반환
        }
    }

    private Sprite CreateEyeSprite() // 눈 아이콘 생성
    {
        const int textureSize = 64; // 텍스처 크기
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false); // 텍스처 생성
        texture.name = "GeneratedEyeIcon"; // 텍스처 이름 설정
        texture.filterMode = FilterMode.Bilinear; // 필터 모드 설정
        texture.wrapMode = TextureWrapMode.Clamp; // 랩 모드 설정

        for (int y = 0; y < textureSize; y++) // 세로 픽셀 순회
        {
            for (int x = 0; x < textureSize; x++) // 가로 픽셀 순회
            {
                float nx = (x - 31.5f) / 31.5f; // 정규화 x 계산
                float ny = (y - 31.5f) / 31.5f; // 정규화 y 계산
                float ellipse = nx * nx / 0.82f + ny * ny / 0.20f; // 눈 윤곽 계산
                float radial = nx * nx + ny * ny; // 중심 거리 계산
                Color pixel = Color.clear; // 픽셀 기본값 설정

                if (ellipse <= 1f) // 눈 흰자 확인
                {
                    pixel = Color.white; // 흰자 색상 적용
                }

                if (ellipse <= 0.96f && radial <= 0.12f) // 홍채 확인
                {
                    pixel = new Color(0.18f, 0.30f, 0.38f, 1f); // 홍채 색상 적용
                }

                if (ellipse <= 0.92f && radial <= 0.035f) // 동공 확인
                {
                    pixel = Color.black; // 동공 색상 적용
                }

                texture.SetPixel(x, y, pixel); // 픽셀 저장
            }
        }

        texture.Apply(); // 텍스처 적용
        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize); // 스프라이트 반환
    }

    private static GameObject CreateUIObject(string objectName, Transform parent) // UI 객체 생성
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform)); // UI 객체 생성
        obj.transform.SetParent(parent, false); // 부모 연결
        return obj; // UI 객체 반환
    }
}
