using System.Collections.Generic; // 연막 목록 관리
using UnityEngine; // 유니티 기본 기능

public sealed class SmokeZone : MonoBehaviour // 이동을 막지 않는 시야 차단 연막
{
    private static readonly List<SmokeZone> activeZones = new List<SmokeZone>(); // 현재 연막 목록
    private float radius; // 차단 구체 반경
    private float endTime; // 효과 종료 시각
    private bool configured; // 연막 구성 상태

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 재실행 정적 상태 초기화
    private static void ResetRegistry() // 이전 실행 연막 목록 정리
    {
        activeZones.Clear(); // 종료된 연막 참조 제거
    }

    private void OnEnable() // 활성 연막 등록
    {
        if (!activeZones.Contains(this)) // 중복 등록 확인
        {
            activeZones.Add(this); // 활성 목록 등록
        }
    }

    private void OnDisable() // 비활성 연막 해제
    {
        activeZones.Remove(this); // 감지 목록에서 제거
    }

    public void Configure(float range, float duration, Material material) // 시야와 연기 설정
    {
        radius = Mathf.Max(0.1f, range); // 차단 범위 저장
        endTime = Time.time + Mathf.Max(0.1f, duration); // 종료 시각 저장
        configured = true; // 차단 검사 활성화
        ParticleSystem smoke = gameObject.AddComponent<ParticleSystem>(); // 연막 입자 생성
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 설정 중 자동 재생 중단
        ParticleSystem.MainModule main = smoke.main; // 입자 기본 설정 조회
        main.loop = true; // 유지 시간 동안 연기 반복
        main.startLifetime = 1.5f; // 연기 조각 유지 시간
        main.startSpeed = 0.12f; // 완만한 연기 확산
        main.startSize = radius * 1.1f; // 연기 조각 크기
        main.startColor = new Color(0.52f, 0.6f, 0.66f, 0.3f); // 청회색 연막 색상
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 월드 위치에서 입자 유지
        main.maxParticles = 100; // 입자 수 제한
        ParticleSystem.EmissionModule emission = smoke.emission; // 입자 생성 설정 조회
        emission.rateOverTime = 24f; // 초당 연기 생성량
        ParticleSystem.ShapeModule shape = smoke.shape; // 연기 발생 범위 설정
        shape.shapeType = ParticleSystemShapeType.Sphere; // 구체 형태 연막
        shape.radius = radius * 0.65f; // 입자 발생 반경
        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>(); // 연막 렌더러 조회
        renderer.sharedMaterial = material; // 연막 전용 투명 재질
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 그림자 생성 제한
        renderer.receiveShadows = false; // 그림자 수신 제한
        smoke.Play(); // 연기 표시 시작
        smoke.Emit(30); // 시작 프레임부터 연막 표시
    }

    private void Update() // 연막 수명 갱신
    {
        if (configured && Time.time >= endTime) // 지속 시간 종료 확인
        {
            configured = false; // 남은 프레임의 차단 즉시 해제
            Destroy(gameObject); // 연막과 입자 함께 제거
        }
    }

    public static bool BlocksSight(Vector3 start, Vector3 end) // 두 위치 사이의 연막 확인
    {
        for (int i = activeZones.Count - 1; i >= 0; i--) // 활성 연막 순회
        {
            SmokeZone zone = activeZones[i]; // 현재 연막 조회
            if (zone == null || !zone.isActiveAndEnabled || !zone.configured || Time.time >= zone.endTime) // 비활성 연막 제외
            {
                continue; // 다음 연막 확인
            }

            if (EquipmentRules.SegmentIntersectsSphere(start, end, zone.transform.position, zone.radius)) // 시선 구간 관통 확인
            {
                return true; // 시야 차단
            }
        }

        return false; // 연막 없는 시야
    }

    private void OnDrawGizmosSelected() // 선택 시 차단 범위 표시
    {
        Gizmos.color = new Color(0.6f, 0.7f, 0.8f, 0.25f); // 디버그 연막 색상
        Gizmos.DrawWireSphere(transform.position, radius); // 실제 차단 반경 표시
    }
}
