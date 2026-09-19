using UnityEngine; // 총기 전용 피격 영역 기능

public enum FirearmHitRegion // 총기 피격 부위 구분
{
    Body, // 몸통 피해 사용
    Head // 머리 피해 사용
}

[DisallowMultipleComponent] // 같은 부위 표식 중복 방지
[RequireComponent(typeof(BoxCollider))] // 비물리 피격 상자 확보
public sealed class FirearmHitZone : MonoBehaviour // 이동 충돌과 분리한 총기 피격 표식
{
    [SerializeField] private FirearmHitRegion region; // 피격 부위 설정
    [SerializeField] private EnemyActor actor; // 연결된 적 생명 관리자
    [SerializeField] private FirearmDamageProbe probe; // 사격장 전용 피해 기록 장치
    public FirearmHitRegion Region => region; // 부위 조회
    public EnemyActor Actor => actor; // 피해 전달 대상 조회
    public FirearmDamageProbe Probe => probe; // 훈련 기록 대상 조회
    public bool AcceptsHit => isActiveAndEnabled && (actor == null || !actor.IsDead); // 사망한 적 피격 제외

    public void Configure(FirearmHitRegion hitRegion, EnemyActor target, FirearmDamageProbe trainingProbe) // 에디터 부위 연결
    {
        region = hitRegion; // 부위 저장
        actor = target; // 적 참조 저장
        probe = trainingProbe; // 훈련 장치 저장
        GetComponent<BoxCollider>().isTrigger = true; // 캐릭터 이동을 밀지 않는 피격 영역
    }
}
