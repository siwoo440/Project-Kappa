using UnityEngine; // 유니티 기본 기능

[DisallowMultipleComponent] // 중복 보조장비 방지
public sealed class SupportEquipmentController : MonoBehaviour // 마비침 사용과 탄수 관리
{
    [SerializeField] private EquipmentTuning tuning; // 마비침 설정
    [SerializeField] private Transform supportSocket; // 손목 발사기 장착 위치
    private PlayerEquipmentManager equipment; // 장비 상태 참조
    private int remainingDarts; // 현재 마비침 수
    private float nextShotTime; // 다음 발사 가능 시각
    private GameObject model; // 발사기 모형

    public int RemainingDarts => remainingDarts; // 남은 탄수 조회
    public int Capacity => tuning != null ? tuning.maxDarts : 0; // 최대 탄수 조회
    public float CooldownRemaining => Mathf.Max(0f, nextShotTime - Time.time); // 재사용 대기 조회

    private void Awake() // 보조장비 초기화
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 상태 연결
        remainingDarts = Capacity; // 테스트 탄수 지급
    }

    private void Start() // 보조장비 모형 구성
    {
        if (tuning != null && tuning.dartModel != null && supportSocket != null) // 손목 모형 참조 확인
        {
            model = Instantiate(tuning.dartModel, supportSocket); // 마비침 발사기 장착
            model.name = "DartLauncher"; // 장착 모형 이름 지정
        }
    }

    public void Configure(EquipmentTuning settings, Transform socket) // 에디터 보조장비 설정
    {
        tuning = settings; // 마비침 수치 저장
        supportSocket = socket; // 발사기 장착 위치 저장
    }

    public bool TryFire() // 마비침 발사 시도
    {
        if (equipment == null || tuning == null || !equipment.CanUseEquipment()) // 공통 장비 상태 검사
        {
            return false; // 사용 불가 반환
        }

        if (remainingDarts <= 0 || Time.time < nextShotTime) // 탄수와 재사용 검사
        {
            equipment.Notify(remainingDarts <= 0 ? "마비침 부족 - 보급대에서 F" : "마비침 재사용 대기"); // 실패 원인 안내
            return false; // 자원 소모 없는 실패
        }

        Vector3 muzzle = supportSocket != null ? supportSocket.position + transform.forward * 0.35f : EquipmentTargeting.BodyCenter(transform); // 발사 시작점 선택
        bool hit = EquipmentTargeting.TryAim(Camera.main, transform, tuning.dartRange, out RaycastHit targetHit, out Vector3 endpoint); // 화면 중앙 조준
        Transform allowed = hit ? targetHit.transform : null; // 조준 대상 선택
        EnemyActor actor = hit ? targetHit.collider.GetComponentInParent<EnemyActor>() : null; // 살아 있는 적 검색
        Transform targetRoot = actor != null ? actor.transform : allowed; // 대상의 최상위 충돌 기준
        bool clear = EquipmentTargeting.HasClearPath(muzzle, endpoint, transform, targetRoot); // 손과 목표 사이 벽 검사

        remainingDarts--; // 실제 발사 탄수 소모
        nextShotTime = Time.time + tuning.dartCooldown; // 다음 발사 시각 지정
        equipment.BeginUse(tuning.useDuration); // 사용 중 다른 장비 입력 잠금
        NoiseSystem.Emit(muzzle, tuning.dartNoiseRadius, NoiseType.Gunshot, gameObject); // 작은 발사 소음 전달
        EquipmentTransientEffect.ShowLine(muzzle, clear ? endpoint : muzzle + transform.forward * 0.3f, tuning.effectMaterial, new Color(0.68f, 0.3f, 1f), 0.16f); // 마비침 궤적 표시

        if (clear && hit && actor != null && !actor.IsDead) // 장애물 없는 유효 적 확인
        {
            EnemyStatusController status = actor.GetComponent<EnemyStatusController>(); // 적 상태 관리자 조회
            if (status == null) // 새 적의 상태 관리자 누락 확인
            {
                status = actor.gameObject.AddComponent<EnemyStatusController>(); // 공통 마비 기능 연결
            }

            status.ApplyStun(tuning.stunDuration, tuning.effectMaterial); // 마비 시간 적용
            equipment.Notify("마비침 명중 - " + tuning.stunDuration.ToString("0.0") + "초 마비"); // 명중 안내
        }
        else // 유효한 마비 대상 없음
        {
            equipment.Notify("마비침 발사 - 명중 대상 없음"); // 빗나감 안내
        }

        return true; // 발사 처리 완료
    }

    public void Refill() // 훈련장 탄수 보충
    {
        remainingDarts = Capacity; // 최대 탄수로 보충
        nextShotTime = 0f; // 보급 후 발사 대기 해제
    }
}
