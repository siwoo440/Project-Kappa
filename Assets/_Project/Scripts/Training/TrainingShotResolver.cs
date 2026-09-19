using System.Collections.Generic; // 한 발의 대상별 피해 집계
using UnityEngine; // 기존 총구와 명중 검사

public struct TrainingShotResult // 한 발 단위 계측 결과
{
    public bool Blocked; // 총구 앞 장애물
    public bool HitAnything; // 적 또는 표적 명중
    public bool HitPractice; // 훈련 표적 명중
    public bool Head; // 머리 펠릿 포함 여부
    public int PelletsHit; // 적중한 펠릿 수
    public float HealthDamage; // 실제 적 체력 또는 표적 피해
    public float TrialSeconds; // 제압된 표적의 처치 시간
}

public static class TrainingShotResolver // 기존 탄도를 재사용하는 산탄과 표적 처리
{
    private sealed class Impact // 한 대상에 대한 한 발 피해
    {
        public EnemyActor Enemy; // 실제 적
        public FirearmDamageProbe Probe; // 기존 비교 장치
        public TrainingReactiveTarget Reactive; // 넘어지는 훈련 표적
        public float Health; // 합산 체력 피해
        public float Posture; // 합산 자세 피해
        public bool Head; // 머리 명중 포함
        public Vector3 Direction; // 표적 넘어짐 방향
    }

    public static TrainingShotResult Resolve(Camera camera, Transform owner, Vector3 muzzle, FirearmDefinition definition, int mask, float spread, bool suppressed) // 피해만 처리하고 탄수와 총성은 호출자가 관리
    {
        TrainingShotResult result = new TrainingShotResult(); // 이번 발사 결과 초기화
        result.TrialSeconds = -1f; // 제압하지 않은 발사 기록
        Dictionary<Object, Impact> impacts = new Dictionary<Object, Impact>(); // 같은 대상을 한 번만 갱신하는 집계
        if (FirearmTargeting.CastShot(camera, owner, muzzle, definition.MaximumRange, mask, out RaycastHit centerHit, out Vector3 centerEnd, out bool centerBlocked) && !centerBlocked && centerHit.collider != null) // 조준한 측정 표적 확인
        {
            centerHit.collider.GetComponentInParent<TrainingReactiveTarget>()?.BeginTrial(Time.timeAsDouble); // 퍼진 탄환이 빗나가도 첫 발사부터 측정
        }

        for (int i = 0; i < definition.PelletCount; i++) // 한 발 안의 탄환별 명중 계산
        {
            Vector2 offset = camera != null ? FirearmHandlingMath.ViewportSample(Random.insideUnitCircle, spread, camera.fieldOfView, camera.pixelWidth, camera.pixelHeight) : Vector2.zero; // 조준점과 같은 분산 영역
            bool found = FirearmTargeting.CastShot(camera, owner, muzzle, definition.MaximumRange, mask, offset, out RaycastHit hit, out Vector3 end, out bool blocked); // 펠릿마다 가장 앞 장애물에서 중단
            result.Blocked |= blocked; // 총구 가림 기록
            if (definition.TracerMaterial != null) // 표시 재질 유효성 확인
            {
                EquipmentTransientEffect.ShowLine(blocked ? EquipmentTargeting.BodyCenter(owner) : muzzle, end, definition.TracerMaterial, new Color(1f, 0.65f, 0.15f), 0.075f); // 한 탄환의 짧은 실제 경로
            }
            if (!found || blocked || hit.collider == null) // 유효하지 않은 명중 제외
            {
                continue; // 다음 펠릿 검사
            }
            FirearmHitZone zone = hit.collider.GetComponent<FirearmHitZone>(); // 명중 부위 확인
            EnemyActor enemy = zone != null ? zone.Actor : hit.collider.GetComponentInParent<EnemyActor>(); // 기존 적 판정 호환
            FirearmDamageProbe probe = zone != null ? zone.Probe : null; // 기존 방어율 비교 장치
            TrainingReactiveTarget reactive = hit.collider.GetComponentInParent<TrainingReactiveTarget>(); // 표적 회전축 위 피격 확인
            FirearmPracticeTarget practice = hit.collider.GetComponentInParent<FirearmPracticeTarget>(); // 기존 탄착 기록 확인
            if (reactive != null && !reactive.AcceptsHit) // 넘어진 표적 확인
            {
                continue; // 내려간 표적은 적중 수에서 제외
            }
            if ((enemy == null || enemy.IsDead) && probe == null && reactive == null && practice == null) // 벽과 바닥 명중 구분
            {
                continue; // 환경에는 체력 피해를 생성하지 않음
            }
            result.HitAnything = true; // 유효한 대상 명중
            result.PelletsHit++; // 한 펠릿 적중 한 번 기록
            result.HitPractice |= probe != null || reactive != null || practice != null; // 발사 명중률 중복 증가 방지
            bool head = zone != null && zone.Region == FirearmHitRegion.Head; // 실제 머리 부위 확인
            result.Head |= head; // 이번 발사의 머리 명중 기록
            practice?.RegisterHit(hit.point, hit.normal, suppressed); // 각각의 탄착 위치는 보존
            Object key = enemy != null ? (Object)enemy : probe != null ? (Object)probe : reactive; // 실제 피해를 받을 공통 대상
            if (key == null) // 단순 탄착판 확인
            {
                continue; // 체력 없는 표적은 표시만 처리
            }
            if (!impacts.TryGetValue(key, out Impact impact)) // 같은 대상 집계 존재 확인
            {
                impact = new Impact(); // 새 대상 피해 준비
                impact.Enemy = enemy; // 실제 적 참조
                impact.Probe = probe; // 기존 비교 표적 참조
                impact.Reactive = reactive; // 넘어지는 표적 참조
                impact.Direction = hit.point - muzzle; // 충격 이동 방향
                impacts.Add(key, impact); // 대상별 합산 등록
            }
            EnemyFirearmHitboxes armorData = enemy != null ? enemy.GetComponent<EnemyFirearmHitboxes>() : null; // 실제 적 방어율 조회
            float armor = probe != null ? probe.ArmorReduction : armorData != null ? armorData.ArmorReduction : 0f; // 미설정 방어율 영점
            float distance = definition.DamageMultiplier(Vector3.Distance(muzzle, hit.point)); // 각 펠릿의 실제 거리 감쇠
            float baseDamage = (head ? definition.HeadDamage : definition.Stats.HealthDamage) / definition.PelletCount; // 전체 피해를 펠릿 수로 나눔
            impact.Health += FirearmDamageMath.Resolve(baseDamage, distance, armor, definition.Stats.ArmorPenetration); // 부위와 방어 반영 후 합산
            impact.Posture += definition.Stats.PostureDamage * distance / definition.PelletCount; // 자세 피해 중복 여덟 배 방지
            impact.Head |= head; // 표시할 부위 합산
        }

        foreach (Impact impact in impacts.Values) // 모든 경로 검사 이후에 피해 적용
        {
            if (impact.Enemy != null && !impact.Enemy.IsDead) // 살아 있는 적 확인
            {
                float before = impact.Enemy.CurrentHealth; // 실제 체력 감소량 기준
                impact.Enemy.TakeDamage(impact.Health, impact.Posture, owner.gameObject); // 한 대상의 한 발 피해 적용
                result.HealthDamage += Mathf.Max(0f, before - impact.Enemy.CurrentHealth); // 과도한 사망 피해 제외
            }
            else // 비교 표적의 계산 결과 표시
            {
                result.HealthDamage += impact.Health; // 표적은 계산 피해 기록
            }
            impact.Probe?.Record(impact.Head ? FirearmHitRegion.Head : FirearmHitRegion.Body, impact.Health, impact.Posture); // 기존 장치 한 번만 갱신
            if (impact.Reactive != null && impact.Reactive.ReceiveImpact(impact.Health, impact.Direction) && impact.Reactive.RemainingHealth <= 0f) // 합산이 끝난 뒤에만 표적 넘어짐
            {
                result.TrialSeconds = impact.Reactive.LastTrialSeconds; // 최신 제압 시간 기록
            }
        }
        return result; // 발사 단위 결과 반환
    }
}
