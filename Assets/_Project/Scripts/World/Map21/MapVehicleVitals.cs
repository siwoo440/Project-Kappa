using ProjectK.Day20; // 차량 종류와 주행 AI 참조
using UnityEngine; // 차량 체력·폭발·풀링 처리

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 차량 생명 중복 방지
    public sealed class MapVehicleVitals : MonoBehaviour, IWorldDamageable // 차량 체력·손상·폭발·잔해 풀링 관리자
    {
        [SerializeField] private MapVehicleKind kind; // 차량 유형
        [SerializeField] private float maxHealth = 320f; // 최대 내구도
        [SerializeField, Range(0f, 0.75f)] private float armorReduction = 0.05f; // 기본 방어율
        [SerializeField] private Vector2 explosionDelay = new Vector2(0.5f, 1.5f); // 파괴 후 폭발 지연
        [SerializeField] private Vector2 wreckSeconds = new Vector2(15f, 25f); // 폭발 잔해 유지 시간
        private float currentHealth; // 현재 내구도
        private bool dead; // 파괴 임계 상태
        private bool exploded; // 폭발 완료 상태
        private float explodeAt; // 폭발 예정 시각
        private float recycleAt; // 잔해 풀 복귀 시각
        private GameObject deathInstigator; // 폭발 연쇄 범죄 귀속 대상
        private float lastAttackCrimeAt = float.NegativeInfinity; // 차량 공격 Heat 반복 제한
        private MapTrafficVehicle agent; // 차량 주행 AI 참조
        private Collider bodyCollider; // 차량 충돌체 참조
        private Renderer[] renderers; // 손상 시각화 대상
        private Color[] baseColors; // 기본 재질 색상 복원 자료
        public float CurrentHealth => currentHealth; // 현재 내구도 조회
        public float MaxHealth => maxHealth; // 최대 내구도 조회
        public float ArmorReduction => armorReduction; // 기본 방어율 조회
        public bool IsDead => dead; // 파괴 상태 조회

        public void Configure(MapVehicleKind vehicleKind) // 차량 종류별 체력 설정
        {
            kind = vehicleKind; // 차량 유형 저장
            if (kind == MapVehicleKind.Delivery) // 배달 차량 확인
            {
                maxHealth = 420f; // 배달 차량 내구도
                armorReduction = 0.08f; // 중간 방어율
            }
            else if (kind == MapVehicleKind.Cargo) // 화물 차량 확인
            {
                maxHealth = 650f; // 화물 차량 내구도
                armorReduction = 0.14f; // 높은 방어율
            }
            else // 일반 승용차 처리
            {
                maxHealth = 320f; // 일반 승용차 내구도
                armorReduction = 0.05f; // 일반 차량 방어율
            }
            ResetVitals(); // 현재 내구도도 새 설정으로 초기화
        }

        private void Awake() // 차량 참조와 시각화 자료 준비
        {
            agent = GetComponent<MapTrafficVehicle>(); // 차량 주행 AI 조회
            bodyCollider = GetComponent<Collider>(); // 차량 충돌체 조회
            renderers = GetComponentsInChildren<Renderer>(true); // 전체 차량 렌더러 조회
            baseColors = new Color[renderers.Length]; // 기본 색상 배열 준비
            for (int i = 0; i < renderers.Length; i++) // 각 렌더러 기본 색 저장
            {
                Material material = renderers[i] != null ? renderers[i].sharedMaterial : null; // 공유 재질 조회
                baseColors[i] = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material != null ? material.color : Color.white; // 기본 색상 저장
            }
            ResetVitals(); // 최초 내구도 초기화
        }

        private void OnEnable() // 차량 풀 재활성화 처리
        {
            agent = agent != null ? agent : GetComponent<MapTrafficVehicle>(); // 주행 AI 참조 복구
            bodyCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider>(); // 충돌체 참조 복구
            ResetVitals(); // 내구도와 폭발 상태 초기화
            if (agent != null) // 차량 주행 AI 확인
            {
                agent.enabled = true; // 재활성 차량 주행 복구
            }
            if (bodyCollider != null) // 차량 충돌체 확인
            {
                bodyCollider.enabled = true; // 재활성 차량 충돌 복구
            }
            RestoreVisuals(); // 손상·폭발 시각 상태 복원
        }

        private void Update() // 폭발 지연과 잔해 풀링 처리
        {
            if (dead && !exploded && Time.time >= explodeAt) // 폭발 예정 차량 확인
            {
                Explode(); // 범위 피해와 폭발 연출 발생
            }
            if (!exploded || Time.time < recycleAt) // 폭발 완료와 잔해 시간 확인
            {
                return; // 아직 잔해 유지
            }
            MapWantedSystem wanted = MapWantedSystem.Instance; // 플레이어 참조 조회
            if (wanted != null && wanted.Player != null && (wanted.Player.transform.position - transform.position).sqrMagnitude < 35f * 35f) // 플레이어 근거리 잔해 확인
            {
                return; // 눈앞에서 차량 잔해가 사라지지 않도록 유지
            }
            gameObject.SetActive(false); // Day20 차량 풀로 반환
        }

        public void ApplyWorldDamage(float damage, GameObject instigator, WorldDamageType type) // 차량 피해 적용
        {
            if (dead || damage <= 0f) // 이미 파괴되었거나 무효 피해 확인
            {
                return; // 피해 처리 중단
            }
            currentHealth = Mathf.Max(0f, currentHealth - damage); // 실제 내구도 감소
            deathInstigator = instigator != null ? instigator : deathInstigator; // 연쇄 폭발 귀속 주체 보존
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 관리자 조회
            bool loud = type != WorldDamageType.Melee; // 총기·폭발은 큰 소리 사건
            if (wanted != null && Time.time - lastAttackCrimeAt >= 1f) // 차량 공격 신고 쿨다운 확인
            {
                if (wanted.ReportCrime(CrimeType.VehicleAttack, transform.position, instigator, loud, false)) // 목격된 차량 공격 Heat 추가
                {
                    lastAttackCrimeAt = Time.time; // 최근 신고 시각 갱신
                }
            }
            UpdateDamageVisuals(); // 현재 내구도에 따라 손상 색상 갱신
            if (currentHealth <= 0f) // 차량 파괴 임계 확인
            {
                BeginExplosion(instigator); // 폭발 지연 상태 시작
            }
        }

        private void BeginExplosion(GameObject instigator) // HP 영점 후 위험 상태 전환
        {
            if (dead) // 중복 파괴 확인
            {
                return; // 처리 중단
            }
            dead = true; // 파괴 상태 저장
            deathInstigator = instigator; // 파괴 원인 저장
            explodeAt = Time.time + Random.Range(explosionDelay.x, explosionDelay.y); // 불규칙한 폭발 지연 적용
            if (agent != null) // 차량 주행 AI 확인
            {
                agent.enabled = false; // 폭발 전 차량 정지
            }
        }

        private void Explode() // 차량 폭발과 범위 피해 처리
        {
            if (exploded) // 중복 폭발 확인
            {
                return; // 처리 중단
            }
            exploded = true; // 폭발 완료 상태 저장
            recycleAt = Time.time + Random.Range(wreckSeconds.x, wreckSeconds.y); // 잔해 유지 시간 예약
            WorldExplosion.Detonate(transform.position + Vector3.up * 0.8f, 8f, 105f, deathInstigator, gameObject); // 플레이어·시민·경비·차량 범위 피해 발생
            MapWantedSystem.Instance?.ReportCrime(CrimeType.VehicleDestroyed, transform.position, deathInstigator, true, false); // 목격된 차량 파괴 Heat 추가
            if (bodyCollider != null) // 차량 충돌체 확인
            {
                bodyCollider.enabled = true; // 폭발 잔해는 낮은 장애물로 유지
            }
            transform.rotation = transform.rotation * Quaternion.Euler(8f, 0f, 10f); // 임시 파손 기울기 연출
            DarkenWreck(); // 폭발 후 검게 탄 외형 표현
        }

        private void UpdateDamageVisuals() // 내구도 비율에 따른 간단한 손상 표현
        {
            float ratio = maxHealth > 0f ? currentHealth / maxHealth : 0f; // 현재 내구도 비율 계산
            if (ratio > 0.5f) // 경미 손상 구간 확인
            {
                return; // 기본 외형 유지
            }
            float darken = ratio <= 0.2f ? 0.42f : 0.68f; // 위험 상태에서 더 어둡게 표시
            TintRenderers(darken); // 차량 외형 어둡게 표시
        }

        private void DarkenWreck() // 폭발 잔해 외형 처리
        {
            TintRenderers(0.18f); // 검게 탄 차량 표시
        }

        private void TintRenderers(float multiplier) // 공유 재질을 손상하지 않는 MaterialPropertyBlock 색상 보정
        {
            if (renderers == null) // 렌더러 배열 확인
            {
                return; // 시각 처리 생략
            }
            for (int i = 0; i < renderers.Length; i++) // 전체 차량 렌더러 순회
            {
                Renderer renderer = renderers[i]; // 현재 렌더러 조회
                if (renderer == null) // 누락 렌더러 확인
                {
                    continue; // 다음 렌더러 처리
                }
                MaterialPropertyBlock block = new MaterialPropertyBlock(); // 인스턴스별 색상 블록 생성
                renderer.GetPropertyBlock(block); // 기존 속성 읽기
                block.SetColor("_BaseColor", baseColors != null && i < baseColors.Length ? baseColors[i] * multiplier : Color.gray); // 기본 색상 기준 손상 명도 적용
                block.SetColor("_Color", baseColors != null && i < baseColors.Length ? baseColors[i] * multiplier : Color.gray); // Built-in 셰이더 호환 색상 적용
                renderer.SetPropertyBlock(block); // 개별 렌더러에 손상 표현 적용
            }
        }

        private void RestoreVisuals() // 풀 재활성화 때 파손 색상 제거
        {
            if (renderers == null) // 렌더러 배열 확인
            {
                return; // 복원 생략
            }
            foreach (Renderer renderer in renderers) // 전체 렌더러 순회
            {
                if (renderer != null) // 유효 렌더러 확인
                {
                    renderer.SetPropertyBlock(null); // 인스턴스 손상 색상 제거
                }
            }
        }

        private void ResetVitals() // 풀링 재사용용 차량 상태 초기화
        {
            currentHealth = Mathf.Max(1f, maxHealth); // 최대 내구도로 복구
            dead = false; // 파괴 상태 해제
            exploded = false; // 폭발 상태 해제
            explodeAt = float.PositiveInfinity; // 폭발 예약 제거
            recycleAt = float.PositiveInfinity; // 잔해 회수 예약 제거
            deathInstigator = null; // 이전 파괴자 참조 제거
            lastAttackCrimeAt = float.NegativeInfinity; // 공격 신고 쿨다운 초기화
        }
    }
}
