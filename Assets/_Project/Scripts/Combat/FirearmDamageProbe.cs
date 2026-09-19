using UnityEngine; // 훈련용 부위 피해 기록

[DisallowMultipleComponent] // 훈련 기록 중복 방지
public sealed class FirearmDamageProbe : MonoBehaviour // 실제 적을 생성하지 않는 피해 비교 표적
{
    [SerializeField, Range(0f, 1f)] private float armorReduction; // 훈련 전용 방어율
    [SerializeField] private TextMesh display; // 단일 결과 표시
    private string lastResult = "HEAD / BODY"; // 마지막 피격 결과
    private float remainingHealth = 100f; // 비교용 가상 체력
    private int hits; // 누적 명중 수
    public float ArmorReduction => Mathf.Clamp01(armorReduction); // 비교용 방어율 조회

    public void Configure(float armor, TextMesh label) // 비교 표적 설정
    {
        armorReduction = Mathf.Clamp01(armor); // 테스트 방어율 저장
        display = label; // 표시 참조 저장
        RefreshLabel(); // 최초 표적 정보 표시
    }

    public void Record(FirearmHitRegion region, float healthDamage, float postureDamage) // 실제 명중 계산 결과 기록
    {
        if (remainingHealth <= 0f) // 이전 가상 표적 처치 여부
        {
            remainingHealth = 100f; // 다음 사격 비교용 체력 복구
        }

        hits++; // 실제 적중 횟수 증가
        remainingHealth = Mathf.Max(0f, remainingHealth - healthDamage); // 가상 체력 감소
        lastResult = (region == FirearmHitRegion.Head ? "HEAD " : "BODY ") + healthDamage.ToString("0.00") + " / PST " + postureDamage.ToString("0.0"); // 부위와 최종 피해 기록
        RefreshLabel(); // 단일 표시 갱신
    }

    public void ResetProbe() // F 단말기 기록 초기화
    {
        hits = 0; // 적중 횟수 초기화
        remainingHealth = 100f; // 비교용 체력 초기화
        lastResult = "HEAD / BODY"; // 결과 안내 복구
        RefreshLabel(); // 표시 갱신
    }

    private void RefreshLabel() // 중복 UI 없는 결과 표시
    {
        if (display != null) // 표시 객체 확인
        {
            display.text = "TEST ARMOR " + (ArmorReduction * 100f).ToString("0") + "%\n" + lastResult + "\nHP " + remainingHealth.ToString("0.0") + " / HITS " + hits; // 임시 방어율과 사격 결과 표시
        }
    }

    private void LateUpdate() // 결과판의 카메라 방향 정렬
    {
        Camera camera = Camera.main; // 현재 게임 카메라
        if (display != null && camera != null) // 표시 참조 확인
        {
            display.transform.rotation = camera.transform.rotation; // 글자가 플레이어를 향하도록 정렬
        }
    }
}
