using UnityEngine; // 네온 발광 제어

namespace ProjectK.Day17 // 17일차 도시 디테일 전용 이름 공간
{
    [DisallowMultipleComponent] // 발광 펄스 중복 방지
    public sealed class MapNeonPulse : MonoBehaviour // 공유 재질을 복제하지 않는 네온 맥동
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor"); // URP 발광 속성 식별자
        [SerializeField] private Renderer targetRenderer; // 발광을 바꿀 표면
        [SerializeField] private Light targetLight; // 선택적인 실제 주변 광원
        [SerializeField] private Color baseColor = Color.cyan; // 기본 네온 색상
        [SerializeField, Min(0.05f)] private float pulseSpeed = 1.2f; // 맥동 속도
        [SerializeField] private float phase; // 간판별 맥동 시차
        [SerializeField, Min(0.5f)] private float emissionMin = 2.4f; // 최소 발광 배율
        [SerializeField, Min(0.5f)] private float emissionMax = 5.5f; // 최대 발광 배율
        private MaterialPropertyBlock block; // 렌더러별 발광 속성 저장
        private float baseLightIntensity; // 실제 광원 초기 밝기
        public Renderer TargetRenderer => targetRenderer; // 검증용 렌더러 참조
        public Light TargetLight => targetLight; // 검증용 광원 참조

        public void Configure(Renderer renderer, Light light, Color color, float speed, float offset) // 편집기에서 네온 연결
        {
            targetRenderer = renderer; // 발광 렌더러 저장
            targetLight = light; // 선택 광원 저장
            baseColor = color; // 네온 색상 저장
            pulseSpeed = Mathf.Max(0.05f, speed); // 맥동 속도 보정
            phase = offset; // 간판별 시차 저장
        }

        private void Awake() // 런타임 속성 준비
        {
            block = new MaterialPropertyBlock(); // 공유 재질 보존용 블록 생성
            baseLightIntensity = targetLight != null ? targetLight.intensity : 0f; // 초기 광원 밝기 저장
        }

        private void Update() // 저비용 네온 밝기 갱신
        {
            if (targetRenderer == null) // 발광 표면 확인
            {
                return; // 누락된 간판 중단
            }
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed + phase); // 영에서 일 사이 맥동 계산
            float emission = Mathf.Lerp(emissionMin, emissionMax, wave); // 발광 배율 보간
            targetRenderer.GetPropertyBlock(block); // 기존 렌더러별 속성 읽기
            block.SetColor(EmissionColor, baseColor * emission); // 공유 재질을 건드리지 않고 발광 변경
            targetRenderer.SetPropertyBlock(block); // 이번 프레임 속성 적용
            if (targetLight != null) // 주변 광원 연결 확인
            {
                targetLight.intensity = baseLightIntensity * Mathf.Lerp(0.86f, 1.08f, wave); // 광원도 약하게 같은 주기 적용
            }
        }
    }
}
