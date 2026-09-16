using UnityEngine; // 유니티 기본 기능

public sealed class TestInteractable : MonoBehaviour, IInteractable // 훈련장 상호작용 테스트 객체
{
    [SerializeField] private string interactionLabel = "테스트 장치"; // 표시 이름
    [SerializeField] private string interactionMessage = "상호작용 실행"; // 실행 메시지
    [SerializeField] private bool emitNoise; // 소음 발생 여부
    [SerializeField] private float noiseRadius = 4f; // 소음 발생 반경
    [SerializeField] private NoiseType noiseType = NoiseType.Interaction; // 소음 유형
    [SerializeField] private bool interactionEnabled = true; // 상호작용 활성 상태

    public string InteractionLabel => interactionLabel; // 표시 이름 읽기

    public bool CanInteract(GameObject interactor) // 상호작용 가능 여부 확인
    {
        return interactionEnabled && isActiveAndEnabled; // 활성 상태 반환
    }

    public void Interact(GameObject interactor) // 상호작용 실행
    {
        Debug.Log($"{interactionMessage} - {interactionLabel}"); // 실행 로그 출력

        if (!emitNoise) // 소음 발생 여부 확인
        {
            return; // 소음 처리 중단
        }

        NoiseSystem.Emit(transform.position, noiseRadius, noiseType, gameObject); // 상호작용 소음 발생
    }

    public void Configure(string label, string message, bool shouldEmitNoise, float radius, NoiseType type) // 테스트 설정 적용
    {
        interactionLabel = label; // 표시 이름 저장
        interactionMessage = message; // 실행 메시지 저장
        emitNoise = shouldEmitNoise; // 소음 발생 여부 저장
        noiseRadius = radius; // 소음 반경 저장
        noiseType = type; // 소음 유형 저장
        interactionEnabled = true; // 상호작용 활성화
    }
}
