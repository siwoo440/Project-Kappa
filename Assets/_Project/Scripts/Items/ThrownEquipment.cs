using UnityEngine; // 유니티 기본 기능

public sealed class ThrownEquipment : MonoBehaviour // 연속 충돌 검사형 투척체
{
    private GameObject owner; // 투척자 참조
    private ConsumableKind kind; // 투척 소모품 종류
    private EquipmentTuning tuning; // 효과 설정
    private Vector3 velocity; // 투척 속도
    private float expiry; // 최대 비행 종료 시각
    private bool launched; // 비행 상태

    public void Launch(GameObject source, ConsumableKind itemKind, Vector3 initialVelocity, EquipmentTuning settings) // 투척 초기화
    {
        owner = source; // 투척자 저장
        kind = itemKind; // 종류 저장
        velocity = initialVelocity; // 초기 속도 저장
        tuning = settings; // 효과 수치 연결
        expiry = Time.time + 3f; // 최대 비행 시간 설정
        launched = true; // 비행 시작
    }

    private void FixedUpdate() // 물리 프레임 투척 갱신
    {
        if (!launched || tuning == null) // 비행 가능 상태 확인
        {
            return; // 갱신 중단
        }

        velocity += Physics.gravity * Time.fixedDeltaTime; // 중력 누적
        Vector3 delta = velocity * Time.fixedDeltaTime; // 이번 이동 거리
        RaycastHit[] hits = delta.sqrMagnitude > 0.000001f ? Physics.SphereCastAll(transform.position, 0.12f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore) : new RaycastHit[0]; // 빠른 투척의 벽 통과 방지
        float nearest = float.MaxValue; // 최근접 충돌 거리
        RaycastHit impact = default; // 충돌 정보 초기화
        bool found = false; // 충돌 여부 초기화

        for (int i = 0; i < hits.Length; i++) // 충돌 후보 순회
        {
            if (EquipmentTargeting.IsOwnCollider(hits[i].collider, owner != null ? owner.transform : null) || hits[i].distance >= nearest) // 자기 몸과 먼 충돌 제외
            {
                continue; // 다음 후보 검사
            }

            nearest = hits[i].distance; // 최근접 거리 저장
            impact = hits[i]; // 충돌 위치 저장
            found = true; // 유효 충돌 기록
        }

        if (found) // 착탄 확인
        {
            Deploy(impact.point + impact.normal * 0.15f); // 실제 착탄 위치에서 효과 시작
            return; // 비행 종료
        }

        transform.position += delta; // 투척체 이동
        transform.Rotate(170f * Time.fixedDeltaTime, 80f * Time.fixedDeltaTime, 0f, Space.Self); // 캡슐 회전 연출
        if (Time.time >= expiry) // 비행 시간 제한 확인
        {
            Deploy(transform.position); // 비행 제한 위치에서 효과 시작
        }
    }

    private void Deploy(Vector3 position) // 착탄 효과 배치
    {
        launched = false; // 중복 효과 방지
        if (kind == ConsumableKind.Lure) // 소음 유인기 확인
        {
            transform.position = position; // 유인기 착탄 위치 고정
            transform.rotation = Quaternion.identity; // 유인기 세우기
            gameObject.AddComponent<NoiseLure>().Configure(owner, tuning.lureRadius, tuning.lureDuration, tuning.effectMaterial); // 착탄 후 반복 유인음 시작
            Destroy(this); // 비행 제어만 제거
        }
        else // 연막 캡슐 확인
        {
            GameObject cloud = new GameObject("Day9_SmokeZone"); // 연막 루트 생성
            cloud.transform.position = position + Vector3.up * (tuning.smokeRadius * 0.4f); // 눈높이를 포함하는 연막 중심
            cloud.AddComponent<SmokeZone>().Configure(tuning.smokeRadius, tuning.smokeDuration, tuning.smokeMaterial); // 시야 차단과 입자 효과 시작
            Destroy(gameObject); // 사용한 캡슐 제거
        }
    }
}
