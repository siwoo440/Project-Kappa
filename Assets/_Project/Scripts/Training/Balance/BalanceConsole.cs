using UnityEngine; // 기존 F 상호작용 연결

public sealed class BalanceConsole : MonoBehaviour, IInteractable // 로비의 총기 비교 단말기
{
    [SerializeField] private BalancePanel panel; // 단일 결과 화면
    public string InteractionLabel => "총기 비교 시험 · 결과 기록"; // 기존 HUD 안내

    public void Configure(BalancePanel screen) // 에디터 화면 연결
    {
        panel = screen; // 실제 설정 창 저장
    }

    public bool CanInteract(GameObject user) // 기존 행동 제한 유지
    {
        return panel != null && !panel.IsOpen && TrainingCenterRoot.CanUse(user); // 정상 상태에서만 화면 사용
    }

    public void Interact(GameObject user) // F 상호작용 실행
    {
        if (CanInteract(user)) // 실행 순간 상태 재확인
        {
            panel.Open(); // 같은 F8 설정 창 열기
        }
    }
}
