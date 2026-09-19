using UnityEngine; // 사격장 보급과 기록 초기화

[DisallowMultipleComponent] // 중복 보급 방지
public sealed class FirearmPracticeStation : MonoBehaviour, IInteractable // 기존 F 상호작용 사격대
{
    [SerializeField] private FirearmPracticeTarget[] targets; // 자신이 관리하는 사격 표적
    [SerializeField] private FirearmHearingProbe[] probes; // 사격장 청취 표시등
    public string InteractionLabel => "사격 기록 초기화 · 권총 탄약 보충"; // 기존 HUD 안내

    public void Configure(FirearmPracticeTarget[] practiceTargets, FirearmHearingProbe[] hearingProbes) // 에디터 관리 대상 연결
    {
        targets = practiceTargets; // 표적 목록 저장
        probes = hearingProbes; // 청각 검사 목록 저장
    }

    public bool CanInteract(GameObject user) // 기존 전투 상태를 유지한 보급 검사
    {
        PlayerEquipmentManager equipment = user != null ? user.GetComponent<PlayerEquipmentManager>() : null; // 사용자의 장비 관리자
        return equipment != null && equipment.CanUseEquipment() && equipment.Firearm != null; // 사망과 특수행동 중 보급 차단
    }

    public void Interact(GameObject user) // 사격 훈련 재시작
    {
        if (!CanInteract(user)) // 실행 직전 보급 가능 여부 재검사
        {
            return; // 잘못된 보급 중단
        }

        PlayerEquipmentManager equipment = user.GetComponent<PlayerEquipmentManager>(); // 검증된 장비 관리자
        equipment.Firearm.RefillAll(); // 기존 무기별 탄약 보충 함수 재사용
        equipment.Firearm.ResetPracticeStatistics(); // 플레이어 표적 명중률 초기화
        if (targets != null) // 표적 목록 확인
        {
            foreach (FirearmPracticeTarget target in targets) // 소유한 표적만 순회
            {
                target?.ClearMarks(); // 표적 탄착 흔적 정리
            }
        }

        if (probes != null) // 청취 표시 목록 확인
        {
            foreach (FirearmHearingProbe probe in probes) // 훈련용 센서만 순회
            {
                probe?.ResetCount(); // 청취 검사 기준 갱신
            }
        }

        equipment.Notify("사격 기록 초기화 · 탄약 보충 완료"); // 처리 결과 안내
    }
}
