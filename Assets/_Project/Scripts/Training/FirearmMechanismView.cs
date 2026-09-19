using UnityEngine; // 볼트와 펌프 표시

[DisallowMultipleComponent] // 기구 표시 중복 방지
public sealed class FirearmMechanismView : MonoBehaviour // 실제 사격 준비 시간과 연결한 외형
{
    [SerializeField] private Transform handle; // 펌프 또는 볼트 손잡이
    [SerializeField] private Transform shell; // 삽입 중 보여줄 탄약
    [SerializeField] private bool bolt; // 볼트 회전 사용 여부
    private Vector3 home; // 기구 원래 위치
    private Quaternion rotation; // 기구 원래 회전
    private PlayerFirearmController owner; // 실제 장착 총기
    private bool captured; // 실행 중 기본 자세 저장 여부

    public void Configure(Transform movingHandle, Transform insertionShell, bool isBolt) // 프리팹 참조 저장
    {
        handle = movingHandle; // 기구 연결
        shell = insertionShell; // 탄약 모형 연결
        bolt = isBolt; // 무기 동작 방식 연결
    }

    private void Awake() // 원래 자세 저장
    {
        home = handle != null ? handle.localPosition : Vector3.zero; // 위치 캡처
        rotation = handle != null ? handle.localRotation : Quaternion.identity; // 회전 캡처
        owner = GetComponentInParent<PlayerFirearmController>(); // 전시 모형은 연결되지 않음
        captured = true; // 원래 자세 준비 완료
    }

    private void OnDisable() // 무기 해제 순간 외형 복구
    {
        if (!captured) // 에디터 전시품 삭제 전 기본 자세 확인
        {
            return; // 초기화 전 모형 변형 방지
        }
        if (handle != null) // 기구 참조 확인
        {
            handle.localPosition = home; // 원래 기구 위치 복구
            handle.localRotation = rotation; // 원래 기구 방향 복구
        }
        if (shell != null) // 장전 탄약 모형 확인
        {
            shell.gameObject.SetActive(false); // 다음 장착에 이전 탄약 노출 방지
        }
    }

    private void LateUpdate() // 실제 동작 상태 표시
    {
        if (owner == null || !owner.IsEquipped || handle == null) // 장착 상태 확인
        {
            return; // 전시 모형 갱신 생략
        }
        float t = owner.IsCycling ? owner.CycleProgress : 0f; // 상태에서 얻은 동작 진행도
        float displacement = Mathf.Sin(t * Mathf.PI); // 뒤로 당겼다가 제자리 복귀
        handle.localPosition = home + Vector3.back * (0.18f * displacement); // 볼트 또는 펌프 후퇴
        handle.localRotation = rotation * Quaternion.Euler(0f, 0f, bolt ? 55f * displacement : 0f); // 볼트 손잡이 회전
        if (shell != null) // 삽입 모형 확인
        {
            shell.gameObject.SetActive(owner.IsReloading); // 한 발 재장전 중 탄약 표시
        }
    }
}
