using ProjectK.Day20; // 시민 종류와 생활 AI 참조
using UnityEngine; // 시민 생명·풀링 처리

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 시민 생명 중복 방지
    public sealed class MapCitizenVitals : MonoBehaviour, IWorldDamageable // 시민 체력·사망·풀 복귀 관리자
    {
        [SerializeField] private MapCitizenKind kind; // 시민 유형
        [SerializeField] private float maxHealth = 60f; // 최대 체력
        [SerializeField, Range(0f, 0.75f)] private float armorReduction; // 기본 방어율
        [SerializeField] private float corpseSeconds = 12f; // 시체 최소 유지 시간
        private float currentHealth; // 현재 체력
        private bool dead; // 사망 상태
        private float recycleAt; // 풀 복귀 가능 시각
        private float lastAttackCrimeAt = float.NegativeInfinity; // 시민 공격 Heat 반복 제한
        private MapCitizenAgent agent; // 생활 AI 참조
        private Collider bodyCollider; // 시민 충돌체 참조
        private Quaternion baseRotation; // 풀 재사용 기본 회전
        public float CurrentHealth => currentHealth; // 현재 체력 조회
        public float MaxHealth => maxHealth; // 최대 체력 조회
        public float ArmorReduction => armorReduction; // 총기·폭발 방어율 조회
        public bool IsDead => dead; // 사망 상태 조회

        public void Configure(MapCitizenKind citizenKind) // 프리팹 유형별 체력 설정
        {
            kind = citizenKind; // 시민 유형 저장
            if (kind == MapCitizenKind.Android) // 안드로이드 확인
            {
                maxHealth = 90f; // 안드로이드 체력 적용
                armorReduction = 0.08f; // 안드로이드 경량 방어 적용
            }
            else if (kind == MapCitizenKind.Mechanical) // 완전 기계화 확인
            {
                maxHealth = 140f; // 기계화 시민 체력 적용
                armorReduction = 0.18f; // 기계화 장갑 적용
            }
            else // 인간 시민 처리
            {
                maxHealth = 60f; // 인간 시민 체력 적용
                armorReduction = 0f; // 인간 기본 방어 없음
            }
            ResetVitals(); // 현재 체력도 새 설정으로 초기화
        }

        private void Awake() // 기본 컴포넌트와 회전 저장
        {
            agent = GetComponent<MapCitizenAgent>(); // 시민 생활 AI 조회
            bodyCollider = GetComponent<Collider>(); // 시민 충돌체 조회
            baseRotation = transform.localRotation; // 풀 재사용 기본 회전 저장
            ResetVitals(); // 최초 체력 초기화
        }

        private void OnEnable() // 시민 풀 재활성화 처리
        {
            agent = agent != null ? agent : GetComponent<MapCitizenAgent>(); // 생활 AI 참조 복구
            bodyCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider>(); // 충돌체 참조 복구
            ResetVitals(); // 사망 상태와 체력 완전 초기화
            if (agent != null) // 생활 AI 존재 확인
            {
                agent.enabled = true; // 새 시민의 보행 기능 복구
            }
            if (bodyCollider != null) // 시민 충돌체 존재 확인
            {
                bodyCollider.enabled = true; // 새 시민 충돌 복구
            }
            transform.localRotation = baseRotation; // 사망 기울기 제거
        }

        private void Update() // 사망 시민의 지연 풀 복귀
        {
            if (!dead || Time.time < recycleAt) // 사망과 유지 시간 확인
            {
                return; // 아직 시체 유지
            }
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 플레이어 참조 조회
            if (wanted != null && wanted.Player != null && (wanted.Player.transform.position - transform.position).sqrMagnitude < 30f * 30f) // 플레이어 바로 앞 시체 확인
            {
                return; // 눈앞에서 시체가 사라지지 않도록 유지
            }
            gameObject.SetActive(false); // Day20 시민 풀로 반환
        }

        public void ApplyWorldDamage(float damage, GameObject instigator, WorldDamageType type) // 시민 피해 적용
        {
            if (dead || damage <= 0f) // 사망 또는 무효 피해 확인
            {
                return; // 피해 처리 중단
            }
            currentHealth = Mathf.Max(0f, currentHealth - damage); // 실제 체력 감소
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 관리자 조회
            bool loud = type != WorldDamageType.Melee; // 총기·폭발은 큰 소리 사건으로 처리
            if (wanted != null && Time.time - lastAttackCrimeAt >= 1f) // 공격 Heat 반복 제한 확인
            {
                if (wanted.ReportCrime(CrimeType.CitizenAttack, transform.position, instigator, loud, false)) // 신고된 시민 공격 처리
                {
                    lastAttackCrimeAt = Time.time; // 신고 성공 시 쿨다운 갱신
                }
            }
            if (currentHealth <= 0f) // 시민 사망 확인
            {
                Die(instigator, loud); // 사망과 추가 Heat 처리
            }
            else if (agent != null && wanted != null && wanted.Player != null && MapCrimeWitness.IsPlayerInstigator(instigator, wanted.Player)) // 살아 있는 시민의 플레이어 공격 확인
            {
                agent.BeginFlee(wanted.Player.transform.position); // 공격자 반대 방향 도주 시작
            }
        }

        private void Die(GameObject instigator, bool loud) // 시민 사망 처리
        {
            if (dead) // 중복 사망 확인
            {
                return; // 처리 중단
            }
            dead = true; // 사망 상태 저장
            currentHealth = 0f; // 현재 체력 영점
            recycleAt = Time.time + corpseSeconds; // 시체 최소 유지 시각 예약
            if (agent != null) // 시민 생활 AI 확인
            {
                agent.enabled = false; // 사망 후 이동과 소음 반응 중지
            }
            if (bodyCollider != null) // 시민 충돌체 확인
            {
                bodyCollider.enabled = false; // 시체가 차량·플레이어를 막지 않도록 해제
            }
            transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, 78f); // 임시 쓰러짐 연출
            MapWantedSystem wanted = MapWantedSystem.Instance; // 수배 관리자 조회
            wanted?.ReportCrime(CrimeType.CitizenKilled, transform.position, instigator, loud, false); // 목격된 시민 사망 Heat 추가
        }

        private void ResetVitals() // 풀링 재사용용 생명 상태 초기화
        {
            currentHealth = Mathf.Max(1f, maxHealth); // 최대 체력으로 복구
            dead = false; // 사망 상태 해제
            recycleAt = float.PositiveInfinity; // 풀 복귀 예약 제거
            lastAttackCrimeAt = float.NegativeInfinity; // 공격 신고 쿨다운 초기화
        }
    }
}
