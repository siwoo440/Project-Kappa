using UnityEngine; // 기존 컴포넌트 호환용 유니티 기능

namespace ProjectK.Day22 // 22일차 수배 시야 표시 이름 공간
{
    [DisallowMultipleComponent] // 기존 시야 컴포넌트 중복 방지
    public sealed class WantedVisionPulse : MonoBehaviour // 이전 월드 시야 표시 호환 컴포넌트
    {
        private void OnEnable() // 기존 프리팹에 남은 컴포넌트 활성화 처리
        {
            enabled = false; // 월드 공간 시야 표시 완전 비활성화
        }
    }
}
