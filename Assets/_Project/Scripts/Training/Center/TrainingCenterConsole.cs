using UnityEngine; // 기존 F 상호작용을 사용하는 시설 단말기

[DisallowMultipleComponent] // 중복 단말기 실행 방지
public sealed class TrainingCenterConsole : MonoBehaviour, IInteractable // 기능이 명확한 구역별 단말기
{
    public enum Operation // 단말기 기능 구분
    {
        Supply, // 장비 보급
        ResetTargets, // 해당 사격장 표적 초기화
        Trial, // 실전 구역 시작과 종료
        Lobby, // 로비 복귀
        Travel // 선택 구역 안내 지점 이동
    }
    [SerializeField] private TrainingCenterRoot center; // 통합 시설 관리자
    [SerializeField] private TrainingCenterZone zone; // 단말기 소속 구역
    [SerializeField] private Transform destination; // 빠른 이동 도착점
    [SerializeField] private Operation operation; // 실제 기능
    [SerializeField] private string label; // 한글 안내 문구
    public TrainingCenterRoot Center => center; // 검사 메뉴 참조
    public TrainingCenterZone Zone => zone; // 검사 메뉴 소속 조회
    public Operation Action => operation; // 단말기 동작 조회
    public string InteractionLabel => operation == Operation.Trial && zone != null && zone.IsRunning ? zone.ZoneName + " 시험 종료" : label; // 기존 HUD에만 조작 안내

    public void Configure(TrainingCenterRoot owner, TrainingCenterZone area, Operation action, string text, Transform arrival = null) // 편집기의 단말기 설정
    {
        center = owner; // 시설 연결
        zone = area; // 구역 연결
        operation = action; // 기능 저장
        label = text; // 사용자 안내 저장
        destination = arrival; // 이동 참조 저장
    }

    public bool CanInteract(GameObject user) // 실제 장비 사용 조건 재확인
    {
        return center != null && TrainingCenterRoot.CanUse(user); // 기존 암살과 전투 행동 충돌 방지
    }

    public void Interact(GameObject user) // 단말기 기능 실행
    {
        if (!CanInteract(user)) // 실행 순간 사용자 상태 확인
        {
            return; // 제한 상태 실행 차단
        }
        switch (operation) // 단일 동작 선택
        {
            case Operation.Supply: // 공용 보급 처리
                TrainingCenterRoot.Supply(user); // 총기와 보조장비와 소모품 보충
                user.GetComponent<PlayerEquipmentManager>()?.Notify("체력 · 탄약 · 마비침 · 소모품 보충 완료"); // 보급 결과 안내
                break; // 다른 기능 실행 차단
            case Operation.ResetTargets: // 사격 구역 초기화
                center.ResetArea(zone, user); // 지정 표적과 장비만 준비
                break; // 단일 기능 완료
            case Operation.Trial: // 적 시험 전환
                if (zone != null && zone.IsRunning) // 현재 시험 상태 확인
                {
                    zone.StopTrial(); // 해당 적과 카메라 정지
                    user.GetComponent<PlayerEquipmentManager>()?.Notify("실전 시험 종료 · 적 원본 보존"); // 정리 결과 안내
                }
                else // 새 시험 요청
                {
                    bool started = center.StartTrial(zone, user); // 나머지 구역을 끄고 선택 구역 가동
                    user.GetComponent<PlayerEquipmentManager>()?.Notify(started ? "시험 시작 · 구역을 나가면 자동 종료" : "시험용 적 원본이 없거나 진입점이 잘못되었습니다"); // 시작 결과 안내
                }
                break; // 중복 실행 차단
            case Operation.Lobby: // 안전 구역 복귀
                center.ReturnToLobby(user); // 실전 시험 종료와 로비 이동
                break; // 기능 완료
            case Operation.Travel: // 시설 내부 빠른 이동
                center.StopTrials(); // 이동 전 다른 구역 시험 종료
                TrainingCenterRoot.Teleport(user, destination); // 일반 통로의 보조 이동 기능
                user.GetComponent<PlayerEquipmentManager>()?.Notify(label); // 도착 구역 안내
                break; // 단일 이동 완료
        }
    }
}
