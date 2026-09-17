using UnityEngine; // 유니티 기본 기능
using UnityEngine.UI; // 유니티 UI 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class EnemyPostureBillboardUI : MonoBehaviour // 적 자세 머리 위 UI
{
    [SerializeField] private EnemyActor actor; // 적 생명 관리자 참조
    [SerializeField] private Camera targetCamera; // 플레이어 카메라 참조
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.42f, 0f); // 머리 위 위치
    [SerializeField] private Vector2 barSize = new Vector2(110f, 10f); // 자세 바 크기
    [SerializeField] private Color normalColor = new Color(1f, 0.72f, 0.16f, 1f); // 일반 자세 색상
    [SerializeField] private Color brokenColor = new Color(1f, 0.20f, 0.20f, 1f); // 붕괴 자세 색상

    private Transform uiRoot; // UI 루트 참조
    private RectTransform fillRect; // 자세 채움 바 참조
    private Image fillImage; // 자세 채움 이미지 참조
    private Canvas canvas; // 월드 캔버스 참조

    private void Awake() // 초기 참조 설정
    {
        actor = actor != null ? actor : GetComponent<EnemyActor>(); // 적 생명 관리자 보정
        targetCamera = targetCamera != null ? targetCamera : Camera.main; // 메인 카메라 보정
        EnsureUI(); // UI 생성 보장
    }

    private void LateUpdate() // 매 프레임 UI 갱신
    {
        if (actor == null || actor.IsDead) // 적 상태 확인
        {
            if (canvas != null) // 캔버스 확인
            {
                canvas.enabled = false; // UI 숨김
            }

            return; // 갱신 중단
        }

        targetCamera = targetCamera != null ? targetCamera : Camera.main; // 카메라 재확인
        EnsureUI(); // UI 생성 보장
        transformUI(); // 위치와 회전 적용
        float progress = Mathf.Clamp01(actor.PostureNormalized); // 자세 비율 계산
        fillRect.sizeDelta = new Vector2((barSize.x - 4f) * progress, barSize.y - 4f); // 자세 채움 크기 적용
        fillImage.color = actor.IsPostureBroken ? brokenColor : normalColor; // 자세 상태 색상 적용
        canvas.enabled = actor.CurrentPosture < actor.MaxPosture || actor.IsPostureBroken; // 피해 시 UI 표시
    }

    public void Configure(EnemyActor targetActor, Camera worldCamera, Vector3 offset) // 외부 설정 적용
    {
        actor = targetActor; // 적 생명 관리자 저장
        targetCamera = worldCamera; // 카메라 저장
        worldOffset = offset; // 위치 저장
        EnsureUI(); // UI 생성 보장
    }

    private void EnsureUI() // UI 생성 보장
    {
        if (canvas != null && fillRect != null && fillImage != null) // 기존 UI 확인
        {
            return; // 생성 중단
        }

        Transform existing = transform.Find("__PostureUI"); // 기존 UI 조회

        if (existing != null) // 기존 UI 존재 확인
        {
            Object.DestroyImmediate(existing.gameObject); // 기존 UI 재생성
        }

        GameObject canvasObject = new GameObject("__PostureUI", typeof(RectTransform)); // 월드 캔버스 생성
        canvasObject.transform.SetParent(transform, false); // 적 루트 연결
        uiRoot = canvasObject.transform; // UI 루트 저장
        canvas = canvasObject.AddComponent<Canvas>(); // 캔버스 추가
        canvas.renderMode = RenderMode.WorldSpace; // 월드 캔버스 적용
        canvas.sortingOrder = 45; // 정렬 순서 적용
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>(); // 스케일러 추가
        scaler.dynamicPixelsPerUnit = 12f; // 월드 픽셀 밀도 적용
        canvasObject.AddComponent<GraphicRaycaster>().enabled = false; // 레이캐스터 비활성화
        RectTransform rootRect = canvasObject.GetComponent<RectTransform>(); // 루트 사각형 조회
        rootRect.sizeDelta = new Vector2(120f, 18f); // 루트 크기 적용
        canvasObject.transform.localScale = Vector3.one * 0.01f; // 월드 크기 적용

        GameObject frameObject = new GameObject("Frame", typeof(RectTransform)); // 프레임 생성
        frameObject.transform.SetParent(canvasObject.transform, false); // 캔버스 연결
        RectTransform frameRect = frameObject.GetComponent<RectTransform>(); // 프레임 사각형 조회
        frameRect.sizeDelta = barSize; // 프레임 크기 적용
        Image frameImage = frameObject.AddComponent<Image>(); // 프레임 이미지 추가
        frameImage.color = new Color(0.88f, 0.92f, 0.95f, 0.92f); // 프레임 색상 적용

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform)); // 배경 생성
        backgroundObject.transform.SetParent(frameObject.transform, false); // 프레임 연결
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>(); // 배경 사각형 조회
        backgroundRect.sizeDelta = new Vector2(barSize.x - 4f, barSize.y - 4f); // 배경 크기 적용
        Image backgroundImage = backgroundObject.AddComponent<Image>(); // 배경 이미지 추가
        backgroundImage.color = new Color(0.08f, 0.10f, 0.13f, 0.90f); // 배경 색상 적용

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform)); // 채움 바 생성
        fillObject.transform.SetParent(frameObject.transform, false); // 프레임 연결
        fillRect = fillObject.GetComponent<RectTransform>(); // 채움 사각형 조회
        fillRect.anchorMin = new Vector2(0f, 0.5f); // 최소 앵커 적용
        fillRect.anchorMax = new Vector2(0f, 0.5f); // 최대 앵커 적용
        fillRect.pivot = new Vector2(0f, 0.5f); // 피벗 적용
        fillRect.anchoredPosition = new Vector2(-barSize.x * 0.5f + 2f, 0f); // 시작 위치 적용
        fillRect.sizeDelta = new Vector2(barSize.x - 4f, barSize.y - 4f); // 초기 크기 적용
        fillImage = fillObject.AddComponent<Image>(); // 채움 이미지 추가
        fillImage.color = normalColor; // 초기 색상 적용
    }

    private void transformUI() // UI 위치 회전 처리
    {
        if (uiRoot == null) // UI 루트 확인
        {
            return; // 처리 중단
        }

        uiRoot.position = transform.position + worldOffset; // 머리 위 위치 적용

        if (targetCamera == null) // 카메라 확인
        {
            return; // 회전 처리 중단
        }

        Vector3 direction = uiRoot.position - targetCamera.transform.position; // 카메라 반대 방향 계산

        if (direction.sqrMagnitude <= 0.0001f) // 방향 길이 확인
        {
            return; // 회전 처리 중단
        }

        uiRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 카메라 방향 정렬
    }
}
