using UnityEngine; // 통합 훈련센터 상태 관리
using UnityEngine.InputSystem; // 기존 플레이어 입력 연결

[DisallowMultipleComponent] // 센터 관리자 중복 방지
public sealed class TrainingCenterRoot : MonoBehaviour // 스폰과 구역 간 이동 관리자
{
    [SerializeField] private Transform spawn; // 새 로비 복귀 지점
    [SerializeField] private TrainingCenterZone[] zones; // 훈련 구역 목록
    [SerializeField] private GameObject player; // 기존 플레이어 객체
    [SerializeField] private string backupScene; // 개편 전 씬 보관 경로
    [SerializeField] private GameObject[] standaloneSystems; // 원래 최상위 공통 시스템
    public GameObject[] StandaloneSystems => standaloneSystems; // 씬 루트 보존 목록 조회
    [SerializeField] private bool completed; // 편집기 구성 완료 여부
    public Transform Spawn => spawn; // 검증용 로비 위치
    public TrainingCenterZone[] Zones => zones; // 구역 참조 조회
    public GameObject Player => player; // 보존된 플레이어 조회
    public string BackupScene => backupScene; // 원본 씬 복구 위치 조회
    public bool Completed => completed; // 설치 상태 조회

    public void Configure(GameObject actor, Transform lobby, TrainingCenterZone[] areas, string backup) // 최초 센터 연결
    {
        player = actor; // 기존 플레이어 저장
        spawn = lobby; // 통합 스폰 저장
        zones = areas; // 구역 목록 저장
        backupScene = backup; // 원본 백업 저장
        completed = true; // 완성된 구성 기록
    }

    public void SetStandaloneSystems(GameObject[] systems) // 씬 유지용 기존 관리자 연결
    {
        standaloneSystems = systems; // 맵과 별도 최상위 객체 저장
    }

    private void Start() // 플레이어 초기화 이후 복귀점 지정
    {
        if (player != null && spawn != null) // 유효한 스폰 확인
        {
            player.GetComponent<PlayerMovement>()?.SetSpawnPoint(spawn.position, spawn.rotation); // 추락 복귀점도 새 로비로 변경
        }
    }

    public void StopTrials() // 모든 시험 적 정지
    {
        foreach (TrainingCenterZone area in zones ?? new TrainingCenterZone[0]) // 연결된 구역 순회
        {
            if (area != null) // 남은 구역 확인
            {
                area.StopTrial(); // 해당 구역의 실행 적 제거
            }
        }
    }

    public bool StartTrial(TrainingCenterZone area, GameObject user) // 선택 구역 시험 시작
    {
        if (area == null || !area.HasTemplates || area.Entry == null || !CanUse(user)) // 완성된 시험 구역 확인
        {
            return false; // 준비되지 않은 구역 제외
        }
        StopTrials(); // 다른 구역 총성 반응 차단
        Teleport(user, area.Entry); // 안전한 내부 시작점 이동
        return area.BeginTrial(user); // 해당 구역만 가동
    }

    public void ResetArea(TrainingCenterZone area, GameObject user) // 선택 구역 반복 시험 준비
    {
        if (!CanUse(user)) // 실제 사용자 행동 확인
        {
            return; // 행동 중 초기화 차단
        }
        area?.ResetTargets(); // 선택 구역 표적만 초기화
        Supply(user); // 기존 장비 자원 보충
        user.GetComponent<PlayerEquipmentManager>()?.Notify("구역 표적 초기화 · 탄약과 소모품 보충"); // 결과 안내
    }

    public static bool CanUse(GameObject user) // 기존 장비 행동 제한 재사용
    {
        PlayerHealth health = user != null ? user.GetComponent<PlayerHealth>() : null; // 생존 상태 조회
        PlayerEquipmentManager equipment = user != null ? user.GetComponent<PlayerEquipmentManager>() : null; // 장비 상태 조회
        return health != null && !health.IsDead && equipment != null && equipment.CanUseEquipment(); // 정상 행동에서만 장치 사용
    }

    public static void Supply(GameObject user) // 원래 무기 수치를 바꾸지 않는 훈련 보급
    {
        if (user == null) // 대상 누락 확인
        {
            return; // 보급 중단
        }
        user.GetComponent<PlayerFirearmController>()?.RefillAll(); // 생성된 총기의 탄약 보충
        user.GetComponent<PlayerFirearmController>()?.ResetPracticeStatistics(); // 현재 사격 계측 초기화
        user.GetComponent<SupportEquipmentController>()?.Refill(); // 마비침 보충
        user.GetComponent<ConsumableController>()?.Refill(); // 소모품 보충
        PlayerHealth health = user.GetComponent<PlayerHealth>(); // 체력 관리자 조회
        if (health != null && !health.IsDead) // 살아 있는 사용자 확인
        {
            health.Heal(health.MaxHealth); // 최대 체력까지 회복
        }
    }

    public void ReturnToLobby(GameObject user) // 안전한 로비 복귀
    {
        if (spawn == null || !CanUse(user)) // 이동 조건 확인
        {
            return; // 특수행동 중 순간 이동 차단
        }
        StopTrials(); // 시험 적과 감시 센서 정지
        Teleport(user, spawn); // 로비 도착점 사용
        user.GetComponent<PlayerEquipmentManager>()?.Notify("로비 복귀 · 모든 실전 시험 종료"); // 복귀 결과 표시
    }

    public static void Teleport(GameObject user, Transform destination) // 기존 플레이어를 이동하는 공통 처리
    {
        if (user == null || destination == null) // 참조 누락 확인
        {
            return; // 잘못된 이동 중단
        }
        user.GetComponent<PlayerFirearmController>()?.Interrupt(); // 이동 전 사격 예약과 조준 정리
        CharacterController body = user.GetComponent<CharacterController>(); // 현재 이동 충돌체
        bool wasEnabled = body != null && body.enabled; // 기존 활성 상태 보존
        if (body != null) // 충돌체 확인
        {
            body.enabled = false; // 순간 이동 중 충돌 보정 중단
        }
        user.transform.SetPositionAndRotation(destination.position, destination.rotation); // 안전한 위치와 방향 적용
        PlayerMovement movement = user.GetComponent<PlayerMovement>(); // 기존 이동 상태
        if (movement != null) // 이동 관리자 확인
        {
            movement.SetHorizontalVelocity(Vector3.zero); // 이전 이동 관성 제거
            movement.VerticalVelocity = 0f; // 이전 낙하 속도 제거
        }
        if (body != null) // 보존한 충돌체 확인
        {
            body.enabled = wasEnabled; // 원래 활성 상태 복구
        }
        ThirdPersonCamera camera = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null; // 기존 추적 카메라
        camera?.Configure(user.transform, user.GetComponent<PlayerInput>()); // 이동 후 시선과 추적 거리 재정렬
        Physics.SyncTransforms(); // 도착 위치 물리 정보 갱신
    }
}
