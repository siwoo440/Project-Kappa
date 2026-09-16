using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능

[RequireComponent(typeof(PlayerInput))] // 플레이어 입력 필수 지정
public sealed class PlayerInteraction : MonoBehaviour // 플레이어 상호작용 관리자
{
    [SerializeField] private Camera interactionCamera; // 상호작용 기준 카메라
    [SerializeField] private float interactionDistance = 2.5f; // 상호작용 거리
    [SerializeField] private float interactionRadius = 0.12f; // 상호작용 감지 반경
    [SerializeField] private LayerMask interactionMask = ~0; // 상호작용 감지 마스크

    private PlayerInput playerInput; // 플레이어 입력 참조
    private InputAction interactAction; // 상호작용 입력 액션
    private IInteractable currentInteractable; // 현재 상호작용 대상
    private string currentLabel = string.Empty; // 현재 상호작용 이름

    public string CurrentLabel => currentLabel; // 현재 상호작용 이름 읽기
    public bool HasTarget => currentInteractable != null; // 상호작용 대상 여부 읽기

    private void Awake() // 초기 참조 설정
    {
        playerInput = GetComponent<PlayerInput>(); // 플레이어 입력 조회
        interactionCamera = interactionCamera != null ? interactionCamera : Camera.main; // 상호작용 카메라 보정
        ResolveAction(); // 입력 액션 연결
    }

    private void OnEnable() // 활성화 처리
    {
        ResolveAction(); // 입력 액션 재연결
    }

    private void Update() // 매 프레임 상호작용 처리
    {
        if (interactionCamera == null) // 카메라 참조 확인
        {
            interactionCamera = Camera.main; // 메인 카메라 재조회
        }

        FindInteractionTarget(); // 상호작용 대상 탐색

        if (currentInteractable == null || interactAction == null) // 대상과 입력 확인
        {
            return; // 상호작용 처리 중단
        }

        if (!interactAction.WasPressedThisFrame()) // F 입력 확인
        {
            return; // 상호작용 처리 중단
        }

        if (!currentInteractable.CanInteract(gameObject)) // 상호작용 가능 여부 확인
        {
            return; // 상호작용 처리 중단
        }

        currentInteractable.Interact(gameObject); // 대상 상호작용 실행
    }

    public void Configure(Camera targetCamera) // 상호작용 카메라 지정
    {
        interactionCamera = targetCamera; // 카메라 참조 저장
    }

    private void ResolveAction() // 입력 액션 연결
    {
        if (playerInput == null || playerInput.actions == null) // 입력 에셋 확인
        {
            interactAction = null; // 상호작용 액션 초기화
            return; // 연결 중단
        }

        InputActionMap actionMap = playerInput.actions.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        interactAction = actionMap != null ? actionMap.FindAction("Interact", false) : null; // 상호작용 액션 조회
    }

    private void FindInteractionTarget() // 상호작용 대상 탐색
    {
        if (interactionCamera == null) // 카메라 참조 확인
        {
            SetCurrentTarget(null); // 현재 대상 초기화
            return; // 탐색 중단
        }

        RaycastHit[] hits = Physics.SphereCastAll(interactionCamera.transform.position, interactionRadius, interactionCamera.transform.forward, interactionDistance, interactionMask, QueryTriggerInteraction.Collide); // 전방 상호작용 후보 조회
        IInteractable nearestInteractable = null; // 가장 가까운 대상 초기화
        float nearestDistance = float.MaxValue; // 가장 가까운 거리 초기화

        for (int i = 0; i < hits.Length; i++) // 충돌 후보 순회
        {
            Collider hitCollider = hits[i].collider; // 충돌 콜라이더 조회
            if (hitCollider == null) // 콜라이더 확인
            {
                continue; // 누락 후보 제외
            }

            Transform hitTransform = hitCollider.transform; // 충돌 트랜스폼 조회
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) // 자기 자신 충돌 확인
            {
                continue; // 자기 충돌 제외
            }

            IInteractable interactable = FindInteractable(hitCollider); // 상호작용 컴포넌트 조회
            if (interactable == null) // 상호작용 대상 확인
            {
                continue; // 일반 충돌 제외
            }

            if (!interactable.CanInteract(gameObject)) // 상호작용 가능 여부 확인
            {
                continue; // 비활성 대상 제외
            }

            if (hits[i].distance >= nearestDistance) // 현재 최근 거리 비교
            {
                continue; // 더 먼 대상 제외
            }

            nearestDistance = hits[i].distance; // 최근 거리 갱신
            nearestInteractable = interactable; // 최근 대상 갱신
        }

        SetCurrentTarget(nearestInteractable); // 현재 대상 적용
    }

    private static IInteractable FindInteractable(Collider targetCollider) // 상호작용 컴포넌트 조회
    {
        MonoBehaviour[] behaviours = targetCollider.GetComponentsInParent<MonoBehaviour>(true); // 상위 스크립트 목록 조회
        for (int i = 0; i < behaviours.Length; i++) // 스크립트 목록 순회
        {
            if (behaviours[i] is IInteractable interactable) // 상호작용 인터페이스 확인
            {
                return interactable; // 상호작용 대상 반환
            }
        }

        return null; // 대상 없음 반환
    }

    private void SetCurrentTarget(IInteractable newTarget) // 현재 상호작용 대상 변경
    {
        if (ReferenceEquals(currentInteractable, newTarget)) // 동일 대상 확인
        {
            return; // 변경 처리 중단
        }

        currentInteractable = newTarget; // 현재 대상 저장
        currentLabel = currentInteractable != null ? currentInteractable.InteractionLabel : string.Empty; // 현재 이름 저장

        if (currentInteractable != null) // 새 대상 확인
        {
            Debug.Log($"상호작용 가능: {currentLabel} [F]"); // 상호작용 안내 로그 출력
        }
    }
}
