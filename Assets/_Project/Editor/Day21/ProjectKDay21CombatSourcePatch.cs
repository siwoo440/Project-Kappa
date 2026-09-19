#if UNITY_EDITOR // 21일차 기존 총기·검 피해 경로 자동 패치
using System; // 문자열 패치 오류 처리
using System.IO; // 기존 C# 소스 읽기·쓰기
using UnityEditor; // 자동 재컴파일과 메뉴

public static class ProjectKDay21CombatSourcePatch // 시민·차량이 기존 무기에 맞도록 피해 경로 확장
{
    private const string FirearmPath = "Assets/_Project/Scripts/Training/TrainingShotResolver.cs"; // 총기 피해 집계 소스 경로
    private const string MeleePath = "Assets/_Project/Scripts/Combat/PlayerCombatController.cs"; // 검 공격 피해 소스 경로
    private const string SessionKey = "ProjectK.Day21.CombatPatch.V1"; // 세션 자동 패치 키

    [InitializeOnLoadMethod] // ZIP 덮어쓰기 뒤 자동 패치 예약
    private static void Schedule() // 최초 컴파일 뒤 기존 소스 확장
    {
        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 패치 시도 여부 확인
        {
            return; // 반복 파일 쓰기 방지
        }
        SessionState.SetBool(SessionKey, true); // 패치 시도 상태 저장
        EditorApplication.delayCall += ApplyNow; // 에디터 안정 시점에 소스 패치
    }

    [MenuItem("Project K/Day 21/Patch Combat Damage Targets")] // 자동 패치 실패 시 수동 메뉴
    public static void ApplyNow() // 총기와 검이 WorldDamageReceiver를 인식하도록 수정
    {
        bool firearm = PatchFirearm(); // 총기 집계 경로 확장
        bool melee = PatchMelee(); // 근접 공격 경로 확장
        if (firearm || melee) // 실제 파일 변경 여부 확인
        {
            AssetDatabase.Refresh(); // 변경된 C#을 다시 컴파일
            UnityEngine.Debug.Log("Day21 시민·차량 총기/검 피해 경로 패치 적용 · Unity 재컴파일 후 Setup 메뉴를 실행하세요."); // 재컴파일 안내
        }
    }

    public static bool IsPatched() // 설치 메뉴에서 현재 피해 경로 준비 여부 확인
    {
        if (!File.Exists(FirearmPath) || !File.Exists(MeleePath)) // 필수 기존 소스 존재 확인
        {
            return false; // 피해 경로 준비 안 됨
        }
        string firearm = Normalize(File.ReadAllText(FirearmPath)); // 총기 소스 읽기
        string melee = Normalize(File.ReadAllText(MeleePath)); // 검 소스 읽기
        return firearm.Contains("WorldDamageReceiver worldTarget") && melee.Contains("HashSet<WorldDamageReceiver> damagedWorld"); // 두 경로 모두 패치 여부 반환
    }

    private static bool PatchFirearm() // TrainingShotResolver에 시민·차량 공통 피해 추가
    {
        if (!File.Exists(FirearmPath)) // 기존 총기 소스 확인
        {
            throw new FileNotFoundException("TrainingShotResolver.cs를 찾지 못했습니다.", FirearmPath); // 프로젝트 구조 변경 보고
        }
        string text = Normalize(File.ReadAllText(FirearmPath)); // 줄바꿈을 통일해 안정적인 패치 준비
        if (text.Contains("WorldDamageReceiver worldTarget")) // 이미 패치됐는지 확인
        {
            return false; // 중복 패치 생략
        }
        text = ReplaceRequired(text, "using System.Collections.Generic; // 한 발의 대상별 피해 집계\n", "using System.Collections.Generic; // 한 발의 대상별 피해 집계\nusing ProjectK.Day21; // 시민·차량 공통 피해와 경비 범죄 연결\n"); // Day21 이름 공간 연결
        text = ReplaceRequired(text, "        public TrainingReactiveTarget Reactive; // 넘어지는 훈련 표적\n", "        public TrainingReactiveTarget Reactive; // 넘어지는 훈련 표적\n        public WorldDamageReceiver World; // 시민·차량 공통 피해 대상\n"); // 대상 집계 필드 추가
        text = ReplaceRequired(text, "            FirearmPracticeTarget practice = hit.collider.GetComponentInParent<FirearmPracticeTarget>(); // 기존 탄착 기록 확인\n", "            FirearmPracticeTarget practice = hit.collider.GetComponentInParent<FirearmPracticeTarget>(); // 기존 탄착 기록 확인\n            WorldDamageReceiver worldTarget = hit.collider.GetComponentInParent<WorldDamageReceiver>(); // 시민·차량 공통 피격 대상 확인\n"); // 월드 피격 대상 조회
        text = ReplaceRequired(text, "            if ((enemy == null || enemy.IsDead) && probe == null && reactive == null && practice == null) // 벽과 바닥 명중 구분\n", "            if ((enemy == null || enemy.IsDead) && probe == null && reactive == null && practice == null && (worldTarget == null || !worldTarget.AcceptsHit)) // 벽과 바닥 명중 구분\n"); // 시민·차량을 유효 명중에 포함
        text = ReplaceRequired(text, "            Object key = enemy != null ? (Object)enemy : probe != null ? (Object)probe : reactive; // 실제 피해를 받을 공통 대상\n", "            Object key = enemy != null ? (Object)enemy : worldTarget != null ? (Object)worldTarget : probe != null ? (Object)probe : reactive; // 실제 피해를 받을 공통 대상\n"); // 대상별 집계 키 확장
        text = ReplaceRequired(text, "                impact.Reactive = reactive; // 넘어지는 표적 참조\n", "                impact.Reactive = reactive; // 넘어지는 표적 참조\n                impact.World = worldTarget; // 시민·차량 공통 피해 참조\n"); // 월드 대상 집계 저장
        text = ReplaceRequired(text, "            float armor = probe != null ? probe.ArmorReduction : armorData != null ? armorData.ArmorReduction : 0f; // 미설정 방어율 영점\n", "            float armor = worldTarget != null ? worldTarget.ArmorReduction : probe != null ? probe.ArmorReduction : armorData != null ? armorData.ArmorReduction : 0f; // 시민·차량 포함 방어율 조회\n"); // 공통 방어율 사용
        text = ReplaceRequired(text, "            float measuredBefore = impact.Enemy != null ? impact.Enemy.CurrentHealth : impact.Reactive != null ? impact.Reactive.RemainingHealth : 0f; // 계측용 실제 체력 보존\n", "            float measuredBefore = impact.Enemy != null ? impact.Enemy.CurrentHealth : impact.World != null ? impact.World.CurrentHealth : impact.Reactive != null ? impact.Reactive.RemainingHealth : 0f; // 시민·차량 포함 계측용 실제 체력 보존\n"); // 피해 전 체력 확장
        string oldApply = @"            if (impact.Enemy != null && !impact.Enemy.IsDead) // 살아 있는 적 확인
            {
                float before = impact.Enemy.CurrentHealth; // 실제 체력 감소량 기준
                impact.Enemy.TakeDamage(impact.Health, impact.Posture, owner.gameObject); // 한 대상의 한 발 피해 적용
                result.HealthDamage += Mathf.Max(0f, before - impact.Enemy.CurrentHealth); // 과도한 사망 피해 제외
            }
            else // 비교 표적의 계산 결과 표시
            {
                result.HealthDamage += impact.Health; // 표적은 계산 피해 기록
            }
";
        string newApply = @"            if (impact.Enemy != null && !impact.Enemy.IsDead) // 살아 있는 적 확인
            {
                float before = impact.Enemy.CurrentHealth; // 실제 체력 감소량 기준
                impact.Enemy.TakeDamage(impact.Health, impact.Posture, owner.gameObject); // 한 대상의 한 발 피해 적용
                result.HealthDamage += Mathf.Max(0f, before - impact.Enemy.CurrentHealth); // 과도한 사망 피해 제외
                impact.Enemy.GetComponent<MapGuardCrimeTag>()?.ReportDamage(owner.gameObject, before, impact.Enemy.CurrentHealth); // 법 집행 경비 공격·처치 Heat 반영
            }
            else if (impact.World != null && impact.World.AcceptsHit) // 살아 있는 시민·차량 확인
            {
                result.HealthDamage += impact.World.ApplyDamage(impact.Health, owner.gameObject, WorldDamageType.Firearm); // 총기 피해를 공통 월드 대상에 적용
            }
            else // 비교 표적의 계산 결과 표시
            {
                result.HealthDamage += impact.Health; // 표적은 계산 피해 기록
            }
";
        text = ReplaceRequired(text, oldApply, newApply); // 실제 피해 적용 분기 확장
        text = ReplaceRequired(text, "                measured.Target = impact.Enemy != null ? (Component)impact.Enemy : impact.Reactive != null ? (Component)impact.Reactive : impact.Probe; // 같은 대상 식별\n", "                measured.Target = impact.Enemy != null ? (Component)impact.Enemy : impact.World != null ? (Component)impact.World : impact.Reactive != null ? (Component)impact.Reactive : impact.Probe; // 시민·차량 포함 같은 대상 식별\n"); // 계측 대상 확장
        text = ReplaceRequired(text, "                measured.After = impact.Enemy != null ? impact.Enemy.CurrentHealth : impact.Reactive != null ? impact.Reactive.RemainingHealth : 0f; // 피격 후 체력\n", "                measured.After = impact.Enemy != null ? impact.Enemy.CurrentHealth : impact.World != null ? impact.World.CurrentHealth : impact.Reactive != null ? impact.Reactive.RemainingHealth : 0f; // 시민·차량 포함 피격 후 체력\n"); // 계측 후 체력 확장
        text = ReplaceRequired(text, "                measured.AppliedDamage = impact.Enemy != null || impact.Reactive != null ? Mathf.Max(0f, measured.Before - measured.After) : impact.Health; // 실제 체력 감소와 단순 계산판 구분\n", "                measured.AppliedDamage = impact.Enemy != null || impact.World != null || impact.Reactive != null ? Mathf.Max(0f, measured.Before - measured.After) : impact.Health; // 시민·차량 포함 실제 체력 감소와 단순 계산판 구분\n"); // 계측 적용 피해 확장
        File.WriteAllText(FirearmPath, text); // 패치된 총기 소스 저장
        return true; // 실제 변경 반환
    }

    private static bool PatchMelee() // PlayerCombatController에 시민·차량 검 피해 추가
    {
        if (!File.Exists(MeleePath)) // 기존 검 전투 소스 확인
        {
            throw new FileNotFoundException("PlayerCombatController.cs를 찾지 못했습니다.", MeleePath); // 프로젝트 구조 변경 보고
        }
        string text = Normalize(File.ReadAllText(MeleePath)); // 줄바꿈 통일
        if (text.Contains("HashSet<WorldDamageReceiver> damagedWorld")) // 이미 패치됐는지 확인
        {
            return false; // 중복 패치 생략
        }
        text = ReplaceRequired(text, "using System.Collections.Generic; // 중복 피격 방지 집합\n", "using System.Collections.Generic; // 중복 피격 방지 집합\nusing ProjectK.Day21; // 시민·차량 공통 피해 연결\n"); // Day21 이름 공간 연결
        text = ReplaceRequired(text, "        HashSet<EnemyActor> damagedEnemies = new HashSet<EnemyActor>(); // 중복 피해 방지 집합 생성\n", "        HashSet<EnemyActor> damagedEnemies = new HashSet<EnemyActor>(); // 중복 피해 방지 집합 생성\n        HashSet<WorldDamageReceiver> damagedWorld = new HashSet<WorldDamageReceiver>(); // 시민·차량 중복 피해 방지 집합 생성\n"); // 공통 피해 집합 추가
        string marker = "            EnemyActor enemy = hits[i].GetComponentInParent<EnemyActor>(); // 적 생명 관리자 조회\n";
        string block = @"            WorldDamageReceiver worldTarget = hits[i].GetComponentInParent<WorldDamageReceiver>(); // 시민·차량 공통 피해 대상 조회
            if (worldTarget != null && worldTarget.AcceptsHit && !damagedWorld.Contains(worldTarget)) // 살아 있는 월드 대상과 중복 여부 확인
            {
                Vector3 worldDirection = worldTarget.transform.position - transform.position; // 대상 방향 계산
                worldDirection.y = 0f; // 수직 성분 제거
                if (worldDirection.sqrMagnitude > 0.0001f && Vector3.Dot(transform.forward, worldDirection.normalized) >= -0.10f && EquipmentTargeting.HasClearPath(EquipmentTargeting.BodyCenter(transform), hits[i].bounds.center, transform, worldTarget.transform)) // 전방과 엄폐 조건 확인
                {
                    damagedWorld.Add(worldTarget); // 이번 공격 처리 대상 기록
                    worldTarget.ApplyDamage(healthDamage, gameObject, WorldDamageType.Melee); // 검 체력 피해 적용
                }
                continue; // 시민·차량을 적 체력 로직으로 중복 처리하지 않음
            }
";
        text = ReplaceRequired(text, marker, block + marker); // 기존 적 처리 앞에 시민·차량 처리 삽입
        File.WriteAllText(MeleePath, text); // 패치된 검 소스 저장
        return true; // 실제 변경 반환
    }

    private static string ReplaceRequired(string text, string oldValue, string newValue) // 현재 소스 모양이 다르면 조용히 망가지지 않도록 검사
    {
        if (!text.Contains(oldValue)) // 예상한 기존 코드 확인
        {
            throw new InvalidOperationException("Day21 전투 소스 패치 기준과 현재 프로젝트 코드가 다릅니다. 패치 대상 코드를 다시 확인하세요."); // 잘못된 자동 수정 차단
        }
        return text.Replace(oldValue, newValue); // 정확한 기준 코드만 교체
    }

    private static string Normalize(string text) // 플랫폼 줄바꿈 차이를 제거
    {
        return (text ?? string.Empty).Replace("\r\n", "\n"); // LF 기준으로 통일
    }
}
#endif
