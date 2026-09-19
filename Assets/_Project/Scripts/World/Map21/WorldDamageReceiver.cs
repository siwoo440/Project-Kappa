using UnityEngine; // 월드 피해 연결 기능

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 피해 연결 중복 방지
    public sealed class WorldDamageReceiver : MonoBehaviour // 총기·검·폭발을 시민과 차량 생명 컴포넌트에 전달
    {
        private IWorldDamageable target; // 실제 생명 대상 인터페이스
        public bool AcceptsHit => target != null && !target.IsDead; // 현재 피격 가능 여부
        public float CurrentHealth => target != null ? target.CurrentHealth : 0f; // 현재 체력 조회
        public float MaxHealth => target != null ? target.MaxHealth : 0f; // 최대 체력 조회
        public float ArmorReduction => target != null ? Mathf.Clamp01(target.ArmorReduction) : 0f; // 기본 방어율 조회
        public bool IsDead => target == null || target.IsDead; // 파괴 상태 조회

        private void Awake() // 최초 피해 대상 탐색
        {
            ResolveTarget(); // 같은 루트의 생명 컴포넌트 연결
        }

        private void OnEnable() // 풀 재활성화 시 대상 연결 복구
        {
            ResolveTarget(); // 같은 루트의 생명 컴포넌트 다시 확인
        }

        public float ApplyDamage(float damage, GameObject instigator, WorldDamageType type) // 최종 계산된 피해 적용
        {
            ResolveTarget(); // 늦게 추가된 생명 컴포넌트 확인
            if (target == null || target.IsDead) // 유효한 피해 대상 확인
            {
                return 0f; // 적용 피해 없음
            }
            float before = target.CurrentHealth; // 피해 전 체력 저장
            target.ApplyWorldDamage(Mathf.Max(0f, damage), instigator, type); // 실제 생명 컴포넌트에 피해 전달
            return Mathf.Max(0f, before - target.CurrentHealth); // 실제 감소 체력 반환
        }

        private void ResolveTarget() // 같은 게임 오브젝트의 공통 피해 대상 검색
        {
            if (target != null) // 이미 연결된 대상 확인
            {
                return; // 반복 검색 생략
            }
            MonoBehaviour[] components = GetComponents<MonoBehaviour>(); // 같은 루트의 런타임 컴포넌트 조회
            foreach (MonoBehaviour component in components) // 모든 컴포넌트 순회
            {
                if (component is IWorldDamageable damageable) // 공통 피해 인터페이스 확인
                {
                    target = damageable; // 실제 생명 대상 저장
                    return; // 첫 대상 연결 후 종료
                }
            }
        }
    }
}
