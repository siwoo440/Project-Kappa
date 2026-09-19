using System.Collections.Generic; // 자신이 만든 탄착 목록 관리
using UnityEngine; // 훈련장 표적과 탄착 표시

[DisallowMultipleComponent] // 표적 중복 기록 방지
public sealed class FirearmPracticeTarget : MonoBehaviour // 사격장 전용 탄착 기록
{
    [SerializeField] private Material markMaterial; // 탄착 표시 공용 재질
    [SerializeField, Range(1, 150)] private int maximumMarks = 60; // 표적별 탄착 표시 한도
    private readonly Queue<GameObject> marks = new Queue<GameObject>(); // 자신이 생성한 흔적만 보관
    public int Hits // 이번 훈련 적중 횟수
    {
        get; // 현재 기록 조회
        private set; // 실제 명중 기록 갱신
    }

    public void Configure(Material material) // 에디터 표적 설정
    {
        markMaterial = material; // 공용 탄착 재질 연결
    }

    public void RegisterHit(Vector3 point, Vector3 normal, bool suppressed) // 실제 충돌 위치 기록
    {
        Hits++; // 표적 적중 수 증가
        if (markMaterial == null) // 준비되지 않은 표시 재질 확인
        {
            return; // 기록만 유지하고 잘못된 재질 생성 방지
        }

        while (marks.Count >= Mathf.Max(1, maximumMarks)) // 흔적 개수 제한
        {
            RemoveMark(marks.Dequeue()); // 가장 오래된 자기 흔적만 제거
        }

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Sphere); // 얇은 원형 탄착 생성
        mark.name = "Day11_Hit_" + Hits; // 탄착 번호 지정
        mark.layer = 2; // 후속 사격의 레이 대상에서 제외
        Collider collider = mark.GetComponent<Collider>(); // 자동 생성된 표시 충돌체
        collider.enabled = false; // 삭제 대기 중에도 탄도 간섭 차단
        Destroy(collider); // 장식 충돌체 제거
        mark.transform.SetParent(transform, false); // 해당 표적에만 연결
        mark.transform.SetPositionAndRotation(point + normal * 0.016f, Quaternion.LookRotation(normal)); // 표면 바로 앞 실제 충돌 위치
        mark.transform.localScale = new Vector3(0.07f, 0.07f, 0.014f); // 멀리서 읽을 수 있는 작은 흔적
        Renderer renderer = mark.GetComponent<Renderer>(); // 탄착 표시 렌더러
        renderer.sharedMaterial = markMaterial; // 재질 중복 생성 방지
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 작은 탄착의 그림자 깜빡임 방지
        renderer.receiveShadows = false; // 탄착 표시 가독성 유지
        MaterialPropertyBlock properties = new MaterialPropertyBlock(); // 탄착별 색상만 분리
        Color color = suppressed ? new Color(0.12f, 0.9f, 1f) : new Color(1f, 0.35f, 0.1f); // 소음기 비교 색상
        properties.SetColor("_BaseColor", color); // URP 색상 적용
        properties.SetColor("_Color", color); // 기본 색상 적용
        renderer.SetPropertyBlock(properties); // 공용 재질을 유지한 색상 변경
        marks.Enqueue(mark); // 탄착 수명 관리 등록
    }

    public void ClearMarks() // 훈련대 재시도 초기화
    {
        while (marks.Count > 0) // 자기 탄착 목록 순회
        {
            RemoveMark(marks.Dequeue()); // 오래된 흔적 정리
        }

        Hits = 0; // 적중 기록 초기화
    }

    private static void RemoveMark(GameObject mark) // 지연 삭제 전 표시 해제
    {
        if (mark != null) // 이미 삭제된 흔적 제외
        {
            mark.SetActive(false); // 즉시 화면과 사격에서 제거
            Destroy(mark); // 다음 안전한 시점에 메모리 해제
        }
    }
}
