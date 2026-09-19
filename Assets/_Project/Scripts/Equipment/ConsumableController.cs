using UnityEngine; // 유니티 기본 기능

public enum ConsumableKind // 빠른 슬롯 아이템 종류
{
    Healing, // 회복 주입기
    Lure, // 소음 유인기
    Smoke // 연막 캡슐
}

[DisallowMultipleComponent] // 중복 소모품 관리자 방지
public sealed class ConsumableController : MonoBehaviour // 소모품 선택과 수량 관리
{
    [SerializeField] private EquipmentTuning tuning; // 소모품 수치 설정
    private PlayerEquipmentManager equipment; // 장비 사용 상태
    private PlayerHealth health; // 플레이어 회복 대상
    private ConsumableKind selected; // 선택한 빠른 슬롯
    private readonly int[] counts = new int[3]; // 종류별 런타임 수량

    public ConsumableKind Selected => selected; // 선택 종류 조회
    public int SelectedCount => counts[(int)selected]; // 선택 아이템 수량 조회
    public string SelectedName => GetName(selected); // 선택 아이템 이름 조회

    private void Awake() // 소모품 초기 지급
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 상태 연결
        health = GetComponent<PlayerHealth>(); // 체력 참조 연결
        Refill(); // 테스트 소모품 지급
    }

    public void Configure(EquipmentTuning settings) // 에디터 소모품 설정
    {
        tuning = settings; // 공통 수치 저장
    }

    public void Cycle() // 다음 빠른 슬롯 선택
    {
        if (equipment == null || !equipment.CanUseEquipment()) // 행동 잠금 확인
        {
            return; // 선택 중단
        }

        selected = (ConsumableKind)EquipmentRules.WrapIndex((int)selected + 1, counts.Length); // 슬롯 순환
        equipment.Notify(SelectedName + " 선택"); // 선택 안내
    }

    public bool TryUse() // 선택 소모품 사용
    {
        if (equipment == null || tuning == null || health == null || !equipment.CanUseEquipment()) // 공통 사용 조건 확인
        {
            return false; // 사용 중단
        }

        if (SelectedCount <= 0) // 소지 수량 확인
        {
            equipment.Notify(SelectedName + " 부족 - 보급대에서 F"); // 수량 부족 안내
            return false; // 수량 유지
        }

        if (selected == ConsumableKind.Healing) // 회복 주입기 선택 확인
        {
            float healed = health.Heal(tuning.healingAmount); // 실제 회복량 적용
            if (healed <= 0f) // 최대 체력이나 사망 상태 확인
            {
                equipment.Notify("회복할 체력이 없어 소모하지 않음"); // 불필요한 소비 방지 안내
                return false; // 수량 유지
            }

            equipment.Notify("체력 +" + healed.ToString("0")); // 실제 회복량 안내
        }
        else // 투척 소모품 처리
        {
            if (!EquipmentTargeting.TryThrowOrigin(transform, out Vector3 origin)) // 벽 앞 투척 공간 확인
            {
                equipment.Notify("투척할 공간이 부족함"); // 막힌 투척 안내
                return false; // 수량 유지
            }

            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : transform.forward; // 카메라 기준 투척 방향
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized; // 수평 방향 정리
            if (forward.sqrMagnitude < 0.001f) // 수직 시점 예외 확인
            {
                forward = transform.forward; // 플레이어 전방 사용
            }

            GameObject projectile = new GameObject("Thrown_" + selected); // 투척 루트 생성
            projectile.transform.position = origin; // 투척 시작점 지정
            GameObject prefab = selected == ConsumableKind.Lure ? tuning.lureModel : tuning.smokeModel; // 선택한 모형 조회
            if (prefab != null) // 모형 에셋 확인
            {
                Instantiate(prefab, projectile.transform); // 소모품 모형 연결
            }

            projectile.AddComponent<ThrownEquipment>().Launch(gameObject, selected, forward * tuning.throwSpeed + Vector3.up * tuning.throwLift, tuning); // 충돌 검사형 투척 시작
            equipment.Notify(SelectedName + " 투척"); // 투척 안내
        }

        counts[(int)selected]--; // 성공한 사용만 수량 감소
        equipment.BeginUse(tuning.useDuration); // 사용 중 장비 행동 잠금
        return true; // 소모품 사용 성공
    }

    public void Refill() // 훈련장 소모품 보충
    {
        int capacity = tuning != null ? Mathf.Max(0, tuning.maxConsumables) : 0; // 종류별 지급 한도
        for (int i = 0; i < counts.Length; i++) // 종류별 수량 순회
        {
            counts[i] = capacity; // 최대 수량 보충
        }
    }

    public int Count(ConsumableKind kind) // 종류별 수량 조회
    {
        int index = (int)kind; // 종류 번호 계산
        return index >= 0 && index < counts.Length ? counts[index] : 0; // 안전한 수량 조회
    }

    public static string GetName(ConsumableKind kind) // 아이템 표시 이름
    {
        switch (kind) // 종류별 이름 분기
        {
            case ConsumableKind.Healing: // 회복 종류
                return "회복 주입기"; // 회복 이름 반환
            case ConsumableKind.Lure: // 유인 종류
                return "소음 유인기"; // 유인 이름 반환
            default: // 연막 종류
                return "연막 캡슐"; // 연막 이름 반환
        }
    }
}
