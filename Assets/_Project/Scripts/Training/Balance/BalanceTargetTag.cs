using UnityEngine; // 임시 계측 표적의 시험 설정

[DisallowMultipleComponent] // 방어율 중복 적용 방지
public sealed class BalanceTargetTag : MonoBehaviour // 원본 적과 분리된 계측용 방어율
{
    [SerializeField, Range(0f, 1f)] private float armor; // 시험에서 지정한 체력 피해 감소율
    public float Armor => Mathf.Clamp01(armor); // 유효한 방어율 조회
    public void Configure(float reduction) // 시험 시작 전 조건 적용
    {
        armor = Mathf.Clamp01(reduction); // 에셋이 아닌 임시 객체에만 저장
    }
}
