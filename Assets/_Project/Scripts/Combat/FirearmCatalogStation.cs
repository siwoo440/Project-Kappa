using UnityEngine; // 대표 총기 훈련 단말기 기능

[DisallowMultipleComponent] // 단말기 중복 부착 방지
public sealed class FirearmCatalogStation : MonoBehaviour, IInteractable // F키 탄약 보급과 피해 기록 초기화
{
    [SerializeField] private FirearmDamageProbe[] probes; // 자신의 비교 표적 목록
    [SerializeField] private FirearmPracticeTarget[] targets; // 자신의 원거리 표적 목록
    public string InteractionLabel => "대표 총기 탄약 보충 · 피해 기록 초기화"; // 기존 HUD 안내 재사용

    public void Configure(FirearmDamageProbe[] damageProbes, FirearmPracticeTarget[] rangeTargets) // 훈련 대상 연결
    {
        probes = damageProbes; // 피해 표적 저장
        targets = rangeTargets; // 원거리 표적 저장
    }

    public bool CanInteract(GameObject user) // 기존 장비 사용 조건 확인
    {
        PlayerEquipmentManager equipment = user != null ? user.GetComponent<PlayerEquipmentManager>() : null; // 사용자 장비 확인
        return equipment != null && equipment.CanUseEquipment() && equipment.Firearm != null; // 특수행동 중 보급 차단
    }

    public void Interact(GameObject user) // 비교 훈련 초기화
    {
        if (!CanInteract(user)) // 실행 직전 상태 재확인
        {
            return; // 허용되지 않은 보급 중단
        }

        PlayerEquipmentManager equipment = user.GetComponent<PlayerEquipmentManager>(); // 검증된 장비 참조
        equipment.Firearm.RefillAll(); // 총기마다 별도로 보급
        equipment.Firearm.ResetPracticeStatistics(); // 플레이어 사격 기록 초기화
        foreach (FirearmDamageProbe probe in probes ?? new FirearmDamageProbe[0]) // 소유한 비교 표적 순회
        {
            probe?.ResetProbe(); // 피해 표시 초기화
        }

        foreach (FirearmPracticeTarget target in targets ?? new FirearmPracticeTarget[0]) // 소유한 거리 표적 순회
        {
            target?.ClearMarks(); // 탄착 흔적 정리
        }

        equipment.Notify("총기별 탄약 보충 · 12일차 기록 초기화 완료"); // 처리 결과 안내
    }
}
