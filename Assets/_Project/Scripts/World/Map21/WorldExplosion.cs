using System.Collections.Generic; // 범위 피해 대상 중복 방지
using UnityEngine; // 폭발 물리·VFX·광원

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    public static class WorldExplosion // 차량 폭발의 범위 피해와 임시 VFX 처리
    {
        public static void Detonate(Vector3 position, float radius, float maximumDamage, GameObject instigator, GameObject sourceRoot) // 월드 전체 대상에 거리 감쇠 폭발 피해 적용
        {
            float safeRadius = Mathf.Max(0.5f, radius); // 유효 폭발 반경 보정
            float safeDamage = Mathf.Max(0f, maximumDamage); // 유효 최대 피해 보정
            Collider[] hits = Physics.OverlapSphere(position, safeRadius, ~0, QueryTriggerInteraction.Collide); // 폭발 반경 모든 충돌체 조회
            HashSet<WorldDamageReceiver> worldTargets = new HashSet<WorldDamageReceiver>(); // 시민·차량 중복 피해 방지
            HashSet<EnemyActor> enemies = new HashSet<EnemyActor>(); // 경비·적 중복 피해 방지
            HashSet<PlayerHealth> players = new HashSet<PlayerHealth>(); // 플레이어 중복 피해 방지
            foreach (Collider hit in hits) // 모든 충돌체 순회
            {
                if (hit == null) // 누락 충돌체 확인
                {
                    continue; // 다음 충돌체 처리
                }
                Transform root = hit.transform.root; // 충돌체 최상위 루트 조회
                if (sourceRoot != null && (hit.gameObject == sourceRoot || hit.transform.IsChildOf(sourceRoot.transform))) // 폭발 원본 차량 자기 피해 확인
                {
                    continue; // 자기 자신 재폭발 피해 제외
                }
                float distance = Vector3.Distance(position, hit.ClosestPoint(position)); // 폭발 중심과 실제 충돌체 거리 계산
                float ratio = Mathf.Clamp01(1f - distance / safeRadius); // 거리 감쇠 비율 계산
                float damage = safeDamage * Mathf.Lerp(0.15f, 1f, ratio); // 외곽 최소 피해를 남긴 폭발 피해 계산
                WorldDamageReceiver receiver = hit.GetComponentInParent<WorldDamageReceiver>(); // 시민·차량 공통 피해 대상 조회
                if (receiver != null && receiver.AcceptsHit && worldTargets.Add(receiver)) // 살아 있는 월드 대상 중복 확인
                {
                    float reduced = damage * (1f - receiver.ArmorReduction * 0.5f); // 폭발은 장갑 효과를 절반만 적용
                    receiver.ApplyDamage(reduced, instigator, WorldDamageType.Explosion); // 시민·차량 폭발 피해 적용
                    continue; // 같은 루트의 EnemyActor 중복 처리 방지
                }
                EnemyActor enemy = hit.GetComponentInParent<EnemyActor>(); // 기존 적 생명 관리자 조회
                if (enemy != null && !enemy.IsDead && enemies.Add(enemy)) // 살아 있는 적 중복 확인
                {
                    float before = enemy.CurrentHealth; // 경비 범죄 판정용 피격 전 체력
                    enemy.TakeDamage(damage, damage * 0.35f, instigator); // 폭발 체력·자세 피해 적용
                    enemy.GetComponent<MapGuardCrimeTag>()?.ReportDamage(instigator, before, enemy.CurrentHealth); // 법 집행 경비라면 범죄 Heat 반영
                    continue; // 플레이어 중복 처리 방지
                }
                PlayerHealth player = hit.GetComponentInParent<PlayerHealth>(); // 플레이어 생명 관리자 조회
                if (player != null && !player.IsDead && players.Add(player)) // 살아 있는 플레이어 중복 확인
                {
                    player.TakeDamage(damage, damage * 0.25f, sourceRoot); // 폭발 체력·자세 피해 적용
                }
            }
            NoiseSystem.Emit(position, Mathf.Max(55f, safeRadius * 7f), NoiseType.Gunshot, instigator != null ? instigator : sourceRoot); // 기존 경비 청각에 큰 폭발 소음 전달
            MapWantedSystem.Instance?.ReportCrime(CrimeType.Explosion, position, instigator, true, false); // 목격·청취된 폭발 Heat 추가
            CreateVisual(position, safeRadius); // 짧은 폭발 VFX와 광원 생성
        }

        private static void CreateVisual(Vector3 position, float radius) // 코드만으로 간단한 폭발 시각 효과 생성
        {
            GameObject root = new GameObject("VehicleExplosionFX"); // 폭발 임시 루트 생성
            root.transform.position = position; // 폭발 중심 위치 적용
            ParticleSystem particles = root.AddComponent<ParticleSystem>(); // 폭발 입자 추가
            ParticleSystem.MainModule main = particles.main; // 기본 입자 설정 모듈 조회
            main.duration = 0.55f; // 짧은 폭발 방출 시간
            main.startLifetime = 0.75f; // 입자 잔류 시간
            main.startSpeed = Mathf.Clamp(radius * 1.25f, 4f, 12f); // 반경에 맞춘 확산 속도
            main.startSize = Mathf.Clamp(radius * 0.22f, 0.7f, 2.2f); // 반경에 맞춘 입자 크기
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.18f, 0.03f), new Color(1f, 0.78f, 0.10f)); // 주황·적색 폭발 색상
            main.loop = false; // 일회성 폭발 설정
            ParticleSystem.EmissionModule emission = particles.emission; // 방출 모듈 조회
            emission.rateOverTime = 0f; // 지속 방출 비활성화
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 34) }); // 한 번에 폭발 입자 방출
            ParticleSystem.ShapeModule shape = particles.shape; // 폭발 형태 모듈 조회
            shape.shapeType = ParticleSystemShapeType.Sphere; // 구형 폭발 사용
            shape.radius = 0.35f; // 작은 중심에서 퍼지게 설정
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>(); // 입자 렌더러 조회
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); // URP 입자 셰이더 조회
            shader = shader != null ? shader : Shader.Find("Particles/Standard Unlit"); // Built-in 대체 셰이더 조회
            if (shader != null) // 유효한 입자 셰이더 확인
            {
                Material material = new Material(shader); // 임시 폭발 재질 생성
                material.color = new Color(1f, 0.42f, 0.05f, 0.92f); // 폭발 기본 색상 적용
                renderer.material = material; // 입자 렌더러에 임시 재질 적용
            }
            Light light = root.AddComponent<Light>(); // 순간 폭발 광원 추가
            light.type = LightType.Point; // 전방향 광원 사용
            light.color = new Color(1f, 0.28f, 0.05f); // 주황 폭발광 적용
            light.range = Mathf.Clamp(radius * 2.2f, 8f, 20f); // 폭발 반경 기반 광원 범위
            light.intensity = 6f; // 순간 강조 밝기
            light.shadows = LightShadows.None; // 짧은 VFX 그림자 비용 제거
            particles.Play(); // 폭발 입자 즉시 재생
            Object.Destroy(root, 1.6f); // 임시 VFX 자동 정리
        }
    }
}
