using UnityEngine; // 적 총기 피격 영역 기능

[DisallowMultipleComponent] // 적별 피격 구성 중복 방지
[RequireComponent(typeof(EnemyActor))] // 기존 체력 관리자 연결
public sealed class EnemyFirearmHitboxes : MonoBehaviour // 이동 캡슐을 대신하는 총기 전용 부위 판정
{
    [SerializeField] private BoxCollider body; // 몸통 피격 영역
    [SerializeField] private BoxCollider head; // 머리 피격 영역
    [SerializeField, Range(0f, 1f)] private float armorReduction = 0f; // 미확정 적 방어율 기본 영점
    public bool Ready => isActiveAndEnabled && body != null && head != null && body.enabled && head.enabled && body.gameObject.activeInHierarchy && head.gameObject.activeInHierarchy; // 대체 판정 완성 상태
    public float ArmorReduction => Mathf.Clamp01(armorReduction); // 총기 피해 방어율 조회
    public BoxCollider Body => body; // 검증용 몸통 참조
    public BoxCollider Head => head; // 검증용 머리 참조

    public void Configure(BoxCollider bodyCollider, BoxCollider headCollider) // 에디터 판정 참조 연결
    {
        body = bodyCollider; // 몸통 저장
        head = headCollider; // 머리 저장
    }
}
