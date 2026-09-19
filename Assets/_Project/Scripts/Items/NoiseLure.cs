using UnityEngine; // 유니티 기본 기능

public sealed class NoiseLure : MonoBehaviour // 착탄 위치의 반복 유인 소음
{
    private GameObject owner; // 투척자 참조
    private float radius; // 소음 반경
    private float endTime; // 소음 종료 시각
    private float nextPulse; // 다음 소음 발생 시각
    private Material effectMaterial; // 소음 표시 재질

    public void Configure(GameObject source, float range, float duration, Material material) // 유인 효과 초기화
    {
        owner = source; // 투척자 저장
        radius = Mathf.Max(0.1f, range); // 소음 반경 보정
        endTime = Time.time + Mathf.Max(0.1f, duration); // 종료 시각 설정
        nextPulse = Time.time; // 즉시 첫 소음 예약
        effectMaterial = material; // 소음 표시 재질 저장
    }

    private void Update() // 반복 소음 갱신
    {
        if (Time.time >= endTime) // 소음 지속 시간 종료 확인
        {
            Destroy(gameObject); // 소진된 유인기 제거
            return; // 갱신 중단
        }

        if (Time.time >= nextPulse) // 소음 발생 주기 확인
        {
            NoiseSystem.Emit(transform.position, radius, NoiseType.Lure, owner); // 실제 착탄 위치로 경비 유도
            EquipmentTransientEffect.ShowRing(transform.position + Vector3.up * 0.08f, 0.55f, effectMaterial, new Color(1f, 0.78f, 0.15f), 0.5f); // 소음 발생 표시
            nextPulse = Time.time + 1f; // 다음 소음 예약
        }
    }
}
