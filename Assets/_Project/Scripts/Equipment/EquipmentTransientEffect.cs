using UnityEngine; // 유니티 기본 기능

public sealed class EquipmentTransientEffect : MonoBehaviour // 짧은 장비 사용 시각 효과
{
    private float endTime; // 효과 종료 시각

    private void Update() // 효과 수명 확인
    {
        if (Time.time >= endTime) // 수명 종료 확인
        {
            Destroy(gameObject); // 사용한 효과 정리
        }
    }

    public static void ShowLine(Vector3 start, Vector3 end, Material material, Color color, float duration) // 마비침 궤적 표시
    {
        LineRenderer line = CreateLine("DartTrail", material, color, duration); // 궤적 렌더러 생성
        line.positionCount = 2; // 시작점과 끝점 사용
        line.SetPosition(0, start); // 발사 위치 적용
        line.SetPosition(1, end); // 명중 위치 적용
    }

    public static void ShowRing(Vector3 center, float radius, Material material, Color color, float duration) // 유인음과 마비 범위 표시
    {
        LineRenderer line = CreateLine("EquipmentPulse", material, color, duration); // 원형 효과 생성
        line.positionCount = 33; // 원형 분할 수 지정
        for (int i = 0; i < line.positionCount; i++) // 원형 정점 계산
        {
            float angle = i * Mathf.PI * 2f / 32f; // 원형 각도 계산
            line.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius); // 원형 위치 적용
        }
    }

    private static LineRenderer CreateLine(string name, Material material, Color color, float duration) // 공통 선 효과 생성
    {
        GameObject effect = new GameObject(name); // 효과 루트 생성
        effect.AddComponent<EquipmentTransientEffect>().endTime = Time.time + Mathf.Max(0.02f, duration); // 자동 정리 시각 지정
        LineRenderer line = effect.AddComponent<LineRenderer>(); // 선 렌더러 연결
        line.sharedMaterial = material; // 공유 효과 재질 연결
        line.useWorldSpace = true; // 월드 기준 좌표 사용
        line.startWidth = 0.035f; // 선 시작 폭 지정
        line.endWidth = 0.035f; // 선 끝 폭 지정
        line.startColor = color; // 시작 색상 지정
        line.endColor = color; // 끝 색상 지정
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 효과 그림자 제한
        return line; // 효과 렌더러 반환
    }
}
