Shader "ProjectK/Day9/SoftEffect" // 장비 효과 전용 셰이더
{
    Properties // 재질 공개 속성
    {
        _Color ("Tint", Color) = (1,1,1,1) // 효과 기본 색상
        _SoftParticle ("Soft Particle", Float) = 0 // 연막 가장자리 옵션
    }
    SubShader // 렌더링 처리 묶음
    {
        Tags // 투명 효과 렌더링 설정
        {
            "Queue"="Transparent" // 투명 렌더링 순서
            "RenderType"="Transparent" // 투명 재질 분류
            "IgnoreProjector"="True" // 프로젝터 제외
        }
        Pass // 효과 렌더링 단계
        {
            Tags // 효과 패스 설정
            {
                "LightMode"="SRPDefaultUnlit" // 조명 없는 효과 패스
            }
            Blend SrcAlpha OneMinusSrcAlpha // 투명 색상 합성
            ZWrite Off // 효과 깊이 기록 제한
            Cull Off // 입자 양면 표시
            CGPROGRAM // 셰이더 코드 시작
            #pragma vertex vert // 정점 계산 함수
            #pragma fragment frag // 픽셀 계산 함수
            #include "UnityCG.cginc" // 기본 좌표 변환 기능
            struct appdata // 입력 정점 자료
            {
                float4 vertex : POSITION; // 정점 위치
                float4 color : COLOR; // 입자와 선 색상
                float2 uv : TEXCOORD0; // 입자 좌표
            };
            struct v2f // 픽셀 전달 자료
            {
                float4 vertex : SV_POSITION; // 화면 위치
                float4 color : COLOR; // 보간 색상
                float2 uv : TEXCOORD0; // 입자 좌표
            };
            fixed4 _Color; // 효과 색상 설정
            float _SoftParticle; // 부드러운 입자 옵션
            v2f vert(appdata v) // 정점 좌표 계산
            {
                v2f o; // 출력 자료 준비
                o.vertex = UnityObjectToClipPos(v.vertex); // 화면 좌표 변환
                o.color = v.color * _Color; // 정점 색상 결합
                o.uv = v.uv; // 입자 좌표 전달
                return o; // 정점 결과 반환
            }
            fixed4 frag(v2f i) : SV_Target // 투명 효과 색상 계산
            {
                float edge = 1.0 - smoothstep(0.1, 1.0, length(i.uv * 2.0 - 1.0)); // 연기 가장자리 감쇠
                fixed4 color = i.color; // 기본 효과 색상
                color.a *= lerp(1.0, edge, saturate(_SoftParticle)); // 연막 투명도 적용
                return color; // 최종 효과 색상
            }
            ENDCG // 셰이더 코드 종료
        }
    }
}
