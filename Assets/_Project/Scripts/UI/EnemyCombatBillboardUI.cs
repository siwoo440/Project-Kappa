using UnityEngine; // 유니티 기본 기능
using UnityEngine.UI; // 유니티 UI 기능

[DisallowMultipleComponent] // 중복 부착 방지
public sealed class EnemyCombatBillboardUI : MonoBehaviour // 적 머리 위 전투 UI
{
    [SerializeField] private EnemyActor actor; // 적 생명 관리자 참조
    [SerializeField] private DetectionSensor sensor; // 탐지 센서 참조
    [SerializeField] private EnemyMeleeCombat meleeCombat; // 적 근접 전투 참조
    [SerializeField] private Camera targetCamera; // 플레이어 카메라 참조
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.68f, 0f); // 머리 위 표시 위치
    [SerializeField] private Vector3 worldScale = Vector3.one * 0.0085f; // 월드 UI 크기
    [SerializeField] private float recentDamageVisibleDuration = 3f; // 피해 후 UI 표시 시간

    private Canvas canvas; // 월드 캔버스 참조
    private Transform uiRoot; // UI 루트 참조
    private bool uiBuiltThisSession; // 현재 실행 UI 생성 완료 상태
    private Image detectionFill; // 탐지 게이지 채움 참조
    private GameObject detectionRow; // 탐지 행 참조
    private Image attackFill; // 공격 게이지 채움 참조
    private GameObject attackRow; // 공격 행 참조
    private Image healthFill; // 체력 게이지 채움 참조
    private GameObject healthRow; // 체력 행 참조
    private Image postureFill; // 자세 게이지 채움 참조
    private GameObject postureRow; // 자세 행 참조
    private float cachedHealth = -1f; // 이전 체력 캐시
    private float cachedPosture = -1f; // 이전 자세 캐시
    private float recentChangeTimer; // 최근 수치 변화 표시 시간
    private bool statCacheInitialized; // 수치 캐시 초기화 상태

    private readonly Color detectionIdleColor = new Color(0.62f, 0.66f, 0.74f, 1f); // 대기 탐지 색상
    private readonly Color detectionWarnColor = new Color(0.97f, 0.75f, 0.12f, 1f); // 경계 탐지 색상
    private readonly Color detectionAlertColor = new Color(0.96f, 0.20f, 0.20f, 1f); // 발각 탐지 색상
    private readonly Color attackGaugeColor = new Color(1f, 0.55f, 0.12f, 1f); // 공격 예고 색상
    private readonly Color healthGaugeColor = new Color(0.96f, 0.20f, 0.26f, 1f); // 체력 색상
    private readonly Color postureGaugeColor = new Color(0.10f, 0.84f, 1f, 1f); // 자세 색상
    private readonly Color postureBrokenColor = new Color(1f, 0.38f, 0.38f, 1f); // 자세 붕괴 색상

    private void Awake() // 초기 참조 설정
    {
        ResolveReferences(); // 참조 자동 연결
        RebuildUI(); // UI 재생성
        uiBuiltThisSession = true; // 생성 완료 상태 저장
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveReferences(); // 참조 재확인

        if (!uiBuiltThisSession) // 현재 실행 생성 여부 확인
        {
            RebuildUI(); // UI 재생성
            uiBuiltThisSession = true; // 생성 완료 상태 저장
        }
    }

    private void OnDisable() // 사망과 비활성 상태의 화면 정리
    {
        if (canvas != null) // 생성된 캔버스 확인
        {
            canvas.enabled = false; // LateUpdate 없이도 즉시 숨김
        }

        if (uiRoot != null) // UI 루트 확인
        {
            uiRoot.gameObject.SetActive(false); // 하위 그래픽 표시 중단
        }
    }

    private void LateUpdate() // 매 프레임 UI 갱신
    {
        ResolveReferences(); // 참조 자동 연결

        if (actor == null || actor.IsDead) // 적 상태 확인
        {
            if (canvas != null) // 캔버스 확인
            {
                canvas.enabled = false; // UI 숨김
            }

            return; // 갱신 중단
        }

        if (canvas == null || uiRoot == null) // UI 존재 확인
        {
            RebuildUI(); // UI 재생성
            uiBuiltThisSession = true; // 생성 완료 상태 저장
        }

        if (uiRoot != null && !uiRoot.gameObject.activeSelf) // 재활성화된 컴포넌트 확인
        {
            uiRoot.gameObject.SetActive(true); // 기존 UI 하나만 재사용
        }

        TrackStatChanges(); // 체력과 자세 변화 추적
        bool shouldShow = ShouldShowUI(); // 최종 UI 표시 조건 계산

        if (canvas != null) // 캔버스 확인
        {
            canvas.enabled = shouldShow; // 표시 조건 적용
        }

        if (!shouldShow) // UI 숨김 상태 확인
        {
            return; // 불필요한 UI 갱신 중단
        }

        if (targetCamera == null) // 카메라 참조 확인
        {
            targetCamera = Camera.main; // 메인 카메라 보정
        }

        UpdateTransform(); // 위치와 회전 갱신
        UpdateDetectionRow(); // 탐지 행 갱신
        UpdateAttackRow(); // 공격 행 갱신
        UpdateHealthRow(); // 체력 행 갱신
        UpdatePostureRow(); // 자세 행 갱신
    }

    public void Configure(EnemyActor targetActor, DetectionSensor targetSensor, EnemyMeleeCombat targetMelee, Camera worldCamera, Vector3 offset) // 외부 설정 적용
    {
        actor = targetActor; // 적 참조 저장
        sensor = targetSensor; // 탐지 센서 저장
        meleeCombat = targetMelee; // 적 근접 전투 저장
        targetCamera = worldCamera; // 카메라 저장
        worldOffset = offset; // 월드 위치 저장

        if (!uiBuiltThisSession || canvas == null || uiRoot == null) // UI 생성 상태 확인
        {
            RebuildUI(); // UI 재생성
            uiBuiltThisSession = true; // 생성 완료 상태 저장
        }
    }

    private void ResolveReferences() // 참조 자동 연결
    {
        actor = actor != null ? actor : GetComponent<EnemyActor>(); // 적 생명 관리자 보정
        sensor = sensor != null ? sensor : GetComponent<DetectionSensor>(); // 탐지 센서 보정
        meleeCombat = meleeCombat != null ? meleeCombat : GetComponent<EnemyMeleeCombat>(); // 적 전투 보정
        targetCamera = targetCamera != null ? targetCamera : Camera.main; // 카메라 보정
    }

    private void RebuildUI() // UI 재생성
    {
        RemoveExistingCanvasChildren(); // 기존 월드 UI 제거

        GameObject rootObject = new GameObject("__EnemyCombatUI", typeof(RectTransform)); // UI 루트 생성
        rootObject.transform.SetParent(transform, false); // 적 루트 연결
        uiRoot = rootObject.transform; // UI 루트 저장
        uiRoot.localScale = worldScale; // 월드 UI 크기 적용

        canvas = rootObject.AddComponent<Canvas>(); // 월드 캔버스 추가
        canvas.renderMode = RenderMode.WorldSpace; // 월드 공간 렌더링 적용
        canvas.sortingOrder = 70; // 정렬 순서 적용
        canvas.enabled = false; // 표시 조건 계산 전 한 프레임 잔상 방지
        rootObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f; // 월드 픽셀 밀도 적용
        GraphicRaycaster raycaster = rootObject.AddComponent<GraphicRaycaster>(); // 레이캐스터 추가
        raycaster.enabled = false; // 입력 차단 방지

        RectTransform rootRect = rootObject.GetComponent<RectTransform>(); // 루트 사각형 조회
        rootRect.sizeDelta = new Vector2(190f, 110f); // 루트 크기 적용

        detectionRow = CreateGaugeRow(rootObject.transform, "DetectionRow", "EYE", new Vector2(0f, 36f), new Vector2(180f, 16f), out detectionFill); // 탐지 행 생성
        attackRow = CreateGaugeRow(rootObject.transform, "AttackRow", "ATK", new Vector2(0f, 12f), new Vector2(180f, 16f), out attackFill); // 공격 행 생성
        healthRow = CreateGaugeRow(rootObject.transform, "HealthRow", "HP", new Vector2(0f, -12f), new Vector2(180f, 18f), out healthFill); // 체력 행 생성
        postureRow = CreateGaugeRow(rootObject.transform, "PostureRow", "PST", new Vector2(0f, -38f), new Vector2(180f, 18f), out postureFill); // 자세 행 생성

        attackRow.SetActive(false); // 초기 공격 행 숨김
    }

    private void RemoveExistingCanvasChildren() // 기존 UI 흔적 제거
    {
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true); // 하위 캔버스 목록 조회

        for (int i = 0; i < canvases.Length; i++) // 하위 캔버스 순회
        {
            if (canvases[i] == null) // 유효성 확인
            {
                continue; // 무효 객체 제외
            }

            if (canvases[i].transform == transform) // 현재 루트 제외 확인
            {
                continue; // 루트 캔버스 제외
            }

            if (Application.isPlaying) // 실행 상태 확인
            {
                canvases[i].gameObject.SetActive(false); // 프레임 끝 삭제 전 중복 렌더링 차단
                Destroy(canvases[i].gameObject); // 기존 하위 캔버스 제거
            }
            else // 에디터 상태 처리
            {
                DestroyImmediate(canvases[i].gameObject); // 기존 하위 캔버스 제거
            }
        }
    }

    private void TrackStatChanges() // 체력 자세 변화 추적
    {
        if (actor == null) // 적 참조 확인
        {
            return; // 추적 중단
        }

        if (!statCacheInitialized) // 초기 수치 캐시 확인
        {
            cachedHealth = actor.CurrentHealth; // 초기 체력 저장
            cachedPosture = actor.CurrentPosture; // 초기 자세 저장
            statCacheInitialized = true; // 캐시 초기화 완료 저장
            return; // 최초 프레임 변화 처리 생략
        }

        bool healthChanged = !Mathf.Approximately(cachedHealth, actor.CurrentHealth); // 체력 변화 계산
        bool postureChanged = !Mathf.Approximately(cachedPosture, actor.CurrentPosture); // 자세 변화 계산

        if (healthChanged || postureChanged) // 수치 변화 확인
        {
            recentChangeTimer = recentDamageVisibleDuration; // 최근 변화 표시 시간 초기화
            cachedHealth = actor.CurrentHealth; // 현재 체력 캐시 갱신
            cachedPosture = actor.CurrentPosture; // 현재 자세 캐시 갱신
        }

        if (recentChangeTimer > 0f) // 최근 변화 표시 시간 확인
        {
            recentChangeTimer = Mathf.Max(0f, recentChangeTimer - Time.deltaTime); // 최근 변화 표시 시간 감소
        }
    }

    private bool ShouldShowUI() // 최종 UI 표시 조건 계산
    {
        if (actor == null || actor.IsDead) // 적 상태 확인
        {
            return false; // UI 숨김 반환
        }

        if (actor.IsPostureBroken) // 자세 붕괴 확인
        {
            return true; // UI 표시 반환
        }

        if (meleeCombat != null && meleeCombat.IsBusy) // 공격 진행 상태 확인
        {
            return true; // UI 표시 반환
        }

        if (sensor != null && sensor.State != DetectionState.Idle) // 탐지 전투 상태 확인
        {
            return true; // UI 표시 반환
        }

        if (recentChangeTimer > 0f) // 최근 피해 상태 확인
        {
            return true; // UI 표시 반환
        }

        return false; // 평상시 UI 숨김 반환
    }

    private void UpdateTransform() // UI 위치와 회전 갱신
    {
        if (uiRoot == null) // UI 루트 확인
        {
            return; // 갱신 중단
        }

        uiRoot.position = transform.position + worldOffset; // 머리 위 위치 적용

        if (targetCamera == null) // 카메라 확인
        {
            return; // 회전 갱신 중단
        }

        Vector3 direction = uiRoot.position - targetCamera.transform.position; // 카메라 반대 방향 계산
        direction.y = Mathf.Clamp(direction.y, -0.15f, 0.15f); // 상하 회전 완화

        if (direction.sqrMagnitude <= 0.0001f) // 방향 길이 확인
        {
            return; // 회전 갱신 중단
        }

        uiRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 카메라 방향 정렬
    }

    private void UpdateDetectionRow() // 탐지 행 갱신
    {
        if (detectionRow == null || detectionFill == null) // 탐지 UI 확인
        {
            return; // 갱신 중단
        }

        if (sensor == null || sensor.State == DetectionState.Idle) // 탐지 센서와 대기 상태 확인
        {
            detectionRow.SetActive(false); // 탐지 행 숨김
            return; // 갱신 중단
        }

        detectionRow.SetActive(true); // 탐지 행 표시
        float progress = 0f; // 탐지 진행도 초기화
        Color color = detectionIdleColor; // 탐지 색상 초기화

        switch (sensor.State) // 탐지 상태 분기
        {
            case DetectionState.Suspicious: // 의심 상태 처리
                progress = 0.45f; // 탐지 진행도 적용
                color = detectionWarnColor; // 탐지 색상 적용
                break; // 분기 종료
            case DetectionState.Searching: // 수색 상태 처리
                progress = 0.72f; // 탐지 진행도 적용
                color = detectionWarnColor; // 탐지 색상 적용
                break; // 분기 종료
            case DetectionState.Detected: // 발각 상태 처리
                progress = 1f; // 탐지 진행도 적용
                color = detectionAlertColor; // 탐지 색상 적용
                break; // 분기 종료
            default: // 기본 대기 상태 처리
                progress = 0.12f; // 탐지 진행도 적용
                color = detectionIdleColor; // 탐지 색상 적용
                break; // 분기 종료
        }

        detectionFill.fillAmount = progress; // 탐지 게이지 채움 적용
        detectionFill.color = color; // 탐지 게이지 색상 적용
    }

    private void UpdateAttackRow() // 공격 예고 행 갱신
    {
        if (attackRow == null || attackFill == null) // 공격 UI 확인
        {
            return; // 갱신 중단
        }

        bool shouldShow = meleeCombat != null && meleeCombat.IsWindupPhase; // 공격 예고 표시 여부 계산
        attackRow.SetActive(shouldShow); // 공격 행 표시 적용

        if (!shouldShow) // 공격 예고 여부 확인
        {
            return; // 갱신 중단
        }

        attackFill.fillAmount = meleeCombat.AttackGaugeNormalized; // 공격 예고 채움 적용
        attackFill.color = attackGaugeColor; // 공격 예고 색상 적용
    }

    private void UpdateHealthRow() // 체력 행 갱신
    {
        if (healthRow == null || healthFill == null || actor == null) // 체력 UI 확인
        {
            return; // 갱신 중단
        }

        healthRow.SetActive(true); // 체력 행 표시
        healthFill.fillAmount = actor.HealthNormalized; // 체력 채움 적용
        healthFill.color = healthGaugeColor; // 체력 색상 적용
    }

    private void UpdatePostureRow() // 자세 행 갱신
    {
        if (postureRow == null || postureFill == null || actor == null) // 자세 UI 확인
        {
            return; // 갱신 중단
        }

        postureRow.SetActive(true); // 자세 행 표시
        postureFill.fillAmount = actor.PostureNormalized; // 자세 채움 적용
        postureFill.color = actor.IsPostureBroken ? postureBrokenColor : postureGaugeColor; // 자세 색상 적용
    }

    private GameObject CreateGaugeRow(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, out Image fillImage) // 공통 게이지 행 생성
    {
        GameObject row = new GameObject(name, typeof(RectTransform)); // 행 루트 생성
        row.transform.SetParent(parent, false); // 부모 연결
        RectTransform rowRect = row.GetComponent<RectTransform>(); // 행 사각형 조회
        rowRect.sizeDelta = size; // 행 크기 적용
        rowRect.anchoredPosition = anchoredPosition; // 행 위치 적용

        GameObject labelObject = new GameObject("Label", typeof(RectTransform)); // 라벨 객체 생성
        labelObject.transform.SetParent(row.transform, false); // 행 연결
        RectTransform labelRect = labelObject.GetComponent<RectTransform>(); // 라벨 사각형 조회
        labelRect.anchorMin = new Vector2(0f, 0.5f); // 라벨 최소 앵커 적용
        labelRect.anchorMax = new Vector2(0f, 0.5f); // 라벨 최대 앵커 적용
        labelRect.pivot = new Vector2(0f, 0.5f); // 라벨 피벗 적용
        labelRect.anchoredPosition = new Vector2(0f, 0f); // 라벨 위치 적용
        labelRect.sizeDelta = new Vector2(36f, size.y); // 라벨 크기 적용
        Text labelText = labelObject.AddComponent<Text>(); // 라벨 텍스트 추가
        labelText.text = label; // 라벨 문자열 적용
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 내장 글꼴 적용
        labelText.fontSize = 13; // 라벨 글자 크기 적용
        labelText.alignment = TextAnchor.MiddleLeft; // 라벨 정렬 적용
        labelText.color = Color.white; // 라벨 색상 적용

        GameObject frameObject = new GameObject("Frame", typeof(RectTransform)); // 프레임 객체 생성
        frameObject.transform.SetParent(row.transform, false); // 행 연결
        RectTransform frameRect = frameObject.GetComponent<RectTransform>(); // 프레임 사각형 조회
        frameRect.anchorMin = new Vector2(0f, 0.5f); // 프레임 최소 앵커 적용
        frameRect.anchorMax = new Vector2(0f, 0.5f); // 프레임 최대 앵커 적용
        frameRect.pivot = new Vector2(0f, 0.5f); // 프레임 피벗 적용
        frameRect.anchoredPosition = new Vector2(42f, 0f); // 프레임 위치 적용
        frameRect.sizeDelta = new Vector2(size.x - 42f, size.y); // 프레임 크기 적용
        Image frameImage = frameObject.AddComponent<Image>(); // 프레임 이미지 추가
        frameImage.color = new Color(0.95f, 0.97f, 1f, 0.90f); // 프레임 색상 적용

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform)); // 배경 객체 생성
        backgroundObject.transform.SetParent(frameObject.transform, false); // 프레임 연결
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>(); // 배경 사각형 조회
        backgroundRect.sizeDelta = new Vector2(frameRect.sizeDelta.x - 4f, frameRect.sizeDelta.y - 4f); // 배경 크기 적용
        Image backgroundImage = backgroundObject.AddComponent<Image>(); // 배경 이미지 추가
        backgroundImage.color = new Color(0.08f, 0.10f, 0.14f, 0.92f); // 배경 색상 적용

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform)); // 채움 객체 생성
        fillObject.transform.SetParent(backgroundObject.transform, false); // 배경 연결
        RectTransform fillRect = fillObject.GetComponent<RectTransform>(); // 채움 사각형 조회
        fillRect.anchorMin = new Vector2(0f, 0f); // 채움 최소 앵커 적용
        fillRect.anchorMax = new Vector2(1f, 1f); // 채움 최대 앵커 적용
        fillRect.offsetMin = Vector2.zero; // 채움 최소 오프셋 적용
        fillRect.offsetMax = Vector2.zero; // 채움 최대 오프셋 적용
        fillImage = fillObject.AddComponent<Image>(); // 채움 이미지 추가
        fillImage.type = Image.Type.Filled; // 채움 이미지 모드 적용
        fillImage.fillMethod = Image.FillMethod.Horizontal; // 수평 채움 적용
        fillImage.fillOrigin = 0; // 채움 시작점 적용
        fillImage.fillAmount = 1f; // 채움 비율 초기화
        fillImage.color = new Color(0.4f, 0.85f, 1f, 1f); // 채움 색상 초기화
        return row; // 생성된 행 반환
    }
}
