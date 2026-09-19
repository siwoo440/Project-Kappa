using UnityEngine; // 총성 청취 검사 표시

[DisallowMultipleComponent] // 청취 표시 중복 방지
public sealed class FirearmHearingProbe : MonoBehaviour // 실제 DetectionSensor를 사용하는 훈련 표시등
{
    [SerializeField] private DetectionSensor sensor; // 실제 청각 판정 센서
    [SerializeField] private Renderer indicator; // 청취 표시등
    [SerializeField] private TextMesh label; // 총성 청취 횟수 안내
    private int baseline; // 기록 초기화 기준
    private int shown = -1; // 마지막 출력 횟수
    private MaterialPropertyBlock properties; // 색상 변경 자료 재사용

    public void Configure(DetectionSensor targetSensor, Renderer lightRenderer, TextMesh text) // 훈련 센서 연결
    {
        sensor = targetSensor; // 실제 게임 센서 저장
        indicator = lightRenderer; // 표시등 저장
        label = text; // 기록 글자 저장
    }

    private void Update() // 청취 기록 표시 갱신
    {
        if (sensor == null) // 센서 누락 검사
        {
            return; // 연결 전 표시 생략
        }

        int count = Mathf.Max(0, sensor.GunshotsHeard - baseline); // 초기화 이후 들은 총성 수
        if (shown != count && label != null) // 글자가 변한 프레임 확인
        {
            shown = count; // 출력 횟수 기록
            label.text = "HEARD " + count; // 새 총성 청취 기록 표시
        }

        if (indicator != null) // 표시등 확인
        {
            properties = properties ?? new MaterialPropertyBlock(); // 표시 자료 한 번만 생성
            bool active = count > 0 && Time.time - sensor.LastGunshotAt < 1.2f; // 총성 직후에만 점등
            Color color = active ? new Color(1f, 0.25f, 0.08f) : new Color(0.12f, 0.2f, 0.25f); // 청취 상태 색상
            properties.SetColor("_BaseColor", color); // URP 색상 지정
            properties.SetColor("_Color", color); // 기본 색상 지정
            indicator.SetPropertyBlock(properties); // 실제 표시등 갱신
        }
    }

    public void ResetCount() // 이전 총성과 새 훈련 구분
    {
        baseline = sensor != null ? sensor.GunshotsHeard : 0; // 현재 실제 청취 수를 기준으로 저장
        shown = -1; // 다음 프레임 글자 갱신
    }
}
