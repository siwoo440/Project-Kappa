using System.Collections.Generic; // 실행 적 목록 관리
using UnityEngine; // 훈련 구역 경계와 표적 관리

[DefaultExecutionOrder(-400)] // 적 행동 이전에 구역 이탈 검사
[DisallowMultipleComponent] // 동일 구역 중복 실행 방지
public sealed class TrainingCenterZone : MonoBehaviour // 비활성 원본에서 독립 시험 적 생성
{
    [SerializeField] private string zoneName; // 구역 표시 이름
    [SerializeField] private Vector2 size; // 지면 기준 가로와 세로 크기
    [SerializeField] private Transform entry; // 시험 내부의 안전한 도착점
    [SerializeField] private GameObject[] templates; // 전투에 사용하지 않는 비활성 원본
    [SerializeField] private Transform runtimeRoot; // 이번 시험 적의 부모
    [SerializeField] private Renderer statusLamp; // 입구 상태등
    [SerializeField] private Material idleMaterial; // 대기 상태 재질
    [SerializeField] private Material activeMaterial; // 가동 상태 재질
    private readonly List<GameObject> running = new List<GameObject>(); // 현재 시험에서 생성한 객체
    private GameObject participant; // 현재 시험 사용자
    public string ZoneName => zoneName; // 상호작용용 이름
    public Transform Entry => entry; // 내부 이동 지점
    public Vector2 Size => size; // 검증용 구역 크기
    public GameObject[] Templates => templates; // 원본 보관 상태 검증
    public Transform RuntimeRoot => runtimeRoot; // 실행 객체 상태 조회
    public bool HasTemplates => templates != null && templates.Length > 0; // 실제 적 시험 가능 여부
    public bool IsRunning => participant != null; // 시험 가동 상태
    public int RunningCount => running.Count; // 검사 중 실행 객체 수

    public void Configure(string label, Vector2 dimensions, Transform arrival, Transform instances) // 구역 기본 연결
    {
        zoneName = label; // 표시 이름 저장
        size = dimensions; // 경계 크기 저장
        entry = arrival; // 진입 위치 저장
        runtimeRoot = instances; // 실행 객체 부모 저장
    }

    public void SetTemplates(GameObject[] source) // 편집기 원본 재배치 결과 연결
    {
        templates = source; // 원본 참조 보존
        foreach (GameObject template in templates ?? new GameObject[0]) // 원본 실행 방지
        {
            if (template != null) // 원본 객체 확인
            {
                template.SetActive(false); // 편집 씬에서도 안전한 기본 상태
            }
        }
    }

    public void SetLamp(Renderer lamp, Material idle, Material active) // 입구 표시 연결
    {
        statusLamp = lamp; // 발광 표면 참조
        idleMaterial = idle; // 준비 상태 저장
        activeMaterial = active; // 시험 상태 저장
        UpdateLamp(); // 최초 준비 색상 표시
    }

    public bool Contains(Vector3 worldPosition) // 직사각형 시험 경계 검사
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition); // 구역 기준 좌표
        return local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.y && local.y > -8f && local.y < 30f; // 지면과 옥상을 포함한 경계
    }

    private void Awake() // 시작 상태 보장
    {
        SetTemplates(templates); // 보관 원본의 탐지와 공격 비활성화
        UpdateLamp(); // 초기 상태등 적용
    }

    public bool BeginTrial(GameObject user) // 구역 안에서 시험 적 활성화
    {
        if (user == null || runtimeRoot == null || !HasTemplates || !Contains(user.transform.position)) // 안전한 시험 시작 검사
        {
            return false; // 경계 밖 시작 차단
        }
        StopTrial(); // 이전 사망체와 기록용 실행 객체 정리
        participant = user; // 경계 검사 대상 저장
        foreach (GameObject template in templates) // 보존된 초기 적 순회
        {
            if (template == null) // 삭제된 원본 확인
            {
                continue; // 없는 적 제외
            }
            GameObject actor = Instantiate(template, runtimeRoot); // 매번 초기 상태의 적 생성
            actor.name = template.name + "_Trial"; // 원본과 실행 객체 구분
            actor.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation); // 검증한 시작 위치 유지
            running.Add(actor); // 정리 대상에 먼저 등록
            actor.SetActive(true); // 모든 원본 참조 복제 뒤 탐지와 전투 시작
        }
        if (running.Count == 0) // 삭제된 원본만 연결된 경우 확인
        {
            participant = null; // 빈 시험을 실행 중으로 표시하지 않음
        }
        UpdateLamp(); // 시험 중 상태등 표시
        return running.Count > 0; // 실제 생성 여부 반환
    }

    private void Update() // 플레이어 이탈과 사망 감지
    {
        if (!IsRunning) // 대기 구역 확인
        {
            return; // 대기 중 AI 없음
        }
        PlayerHealth health = participant.GetComponent<PlayerHealth>(); // 생존 상태 확인
        if (!Contains(participant.transform.position) || (health != null && health.IsDead)) // 시험 범위 이탈 또는 사망
        {
            participant.GetComponent<PlayerEquipmentManager>()?.Notify("구역 이탈 또는 사망 · 실전 시험 종료"); // 종료 이유 안내
            StopTrial(); // 적이 안전 구역을 추격하지 않도록 즉시 정지
        }
    }

    private void LateUpdate() // 실제 적의 구역 탈출 방지
    {
        if (!IsRunning) // 실행 여부 확인
        {
            return; // 검사 생략
        }
        foreach (GameObject actor in running) // 이번 시험 적만 조회
        {
            if (actor != null && actor.activeInHierarchy && !Contains(actor.transform.position)) // 경계 밖으로 이동한 적 확인
            {
                participant.GetComponent<PlayerEquipmentManager>()?.Notify("적의 구역 이탈 감지 · 시험을 다시 시작하세요"); // 안전 중단 안내
                StopTrial(); // 안전 구역 침입과 잘못된 순찰을 함께 중단
                break; // 변경된 목록 순회 중단
            }
        }
    }

    public void StopTrial() // 원본을 건드리지 않는 실행 객체 제거
    {
        participant = null; // 시험 상태 해제
        foreach (GameObject actor in running) // 사망체를 포함한 실행 객체 조회
        {
            if (actor != null) // 아직 남은 객체 확인
            {
                actor.SetActive(false); // 지연 삭제 전 공격과 UI와 청각 즉시 정지
                if (Application.isPlaying) // 게임 실행 상태
                {
                    Destroy(actor); // 이번 프레임 종료 후 안전한 삭제
                }
                else // 편집기 검사 상태
                {
                    DestroyImmediate(actor); // 임시 검사 객체 즉시 정리
                }
            }
        }
        running.Clear(); // 다음 시험 목록 준비
        UpdateLamp(); // 대기 상태등 복구
    }

    public void ResetTargets() // 해당 구역 표적만 반복 시험 준비
    {
        foreach (TrainingReactiveTarget target in GetComponentsInChildren<TrainingReactiveTarget>(true)) // 구역별 표적 조회
        {
            target.ResetTarget(); // 위치와 체력과 탄착과 넘어짐 복구
        }
    }

    private void UpdateLamp() // 재질 복제 없는 상태 표시
    {
        if (statusLamp != null && idleMaterial != null && activeMaterial != null) // 표시 재질 확인
        {
            statusLamp.sharedMaterial = IsRunning ? activeMaterial : idleMaterial; // 준비와 실행을 동일 표시등에 적용
        }
    }

    private void OnDisable() // 씬 종료와 비활성화 처리
    {
        StopTrial(); // 실행 적과 사망체 정리
    }
}
