#if UNITY_EDITOR // 21일차 피해·수배·증원 검사 메뉴
using System; // 검사 오류 처리
using ProjectK.Day20; // 차량·시민 종류 참조
using ProjectK.Day21; // 피해·수배 런타임 참조
using UnityEditor; // 검사 메뉴와 프리팹 조회
using UnityEngine; // 씬 오브젝트 검사
using UnityEngine.SceneManagement; // 활성 Map 씬 확인

public static class ProjectKDay21WantedValidation // 시민·차량 피해와 0~5성 수배 규칙 검사
{
    // 실제 씬 검사 메뉴
    public static void ValidateMenu() // 활성 Map의 Day21 구성 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay21WantedSetup.MapScenePath) // 대상 Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 검사 대상 안내
            return; // 다른 씬 검사 중단
        }
        MapWantedSystem wanted = FindSingle<MapWantedSystem>(scene); // 수배 관리자 조회
        MapWantedResponseManager response = FindSingle<MapWantedResponseManager>(scene); // 증원 관리자 조회
        if (wanted == null || response == null) // 설치 누락 확인
        {
            Debug.LogError("Day21_CrimeWanted가 없습니다. Setup Damage Wanted And Response 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 실패를 Console에 명확히 표시
        {
            int checks = Validate(wanted, response, true); // 전체 구조 검사 실행
            Debug.Log("Day21 피해·수배·증원 검사 통과 · " + checks + "항목"); // 검사 결과 안내
            Selection.activeGameObject = wanted.gameObject; // 검사 결과 루트 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    // Heat·별 수학 검사 메뉴
    public static void RuleTestMenu() // 씬 프리팹과 별 규칙 공통 검사
    {
        try // 규칙 검사 예외 처리
        {
            int checks = ValidateRules(); // 순수 Heat·수배 규칙 검사
            Debug.Log("Day21 수배 규칙 검사 통과 · " + checks + "항목"); // 규칙 검사 결과 안내
        }
        catch (Exception error) // 규칙 실패 처리
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    public static int Validate(MapWantedSystem wanted, MapWantedResponseManager response, bool strict) // 저장 전과 수동 메뉴 공통 검사
    {
        int checks = 0; // 통과 항목 수 초기화
        Require(wanted != null, "MapWantedSystem이 필요합니다."); checks++; // 수배 관리자 존재 확인
        Require(wanted.World != null && wanted.Player != null, "수배 시스템 월드·플레이어 참조가 없습니다."); checks++; // 본편 참조 확인
        Require(!string.IsNullOrEmpty(wanted.SourceCommit), "Day21 기준 커밋 기록이 없습니다."); checks++; // 기준 기록 확인
        Require(response != null && response.World == wanted.World && response.Wanted == wanted, "수배 증원 관리자 참조가 잘못되었습니다."); checks++; // 증원 연결 확인
        Require(response.HasPrefabs(), "E-01/E-02 수배 경비 프리팹이 없습니다."); checks++; // 경비 프리팹 확인
        Require(response.GuardPoolSize >= 18, "오성 대응에 필요한 경비 풀 크기가 부족합니다."); checks++; // 최대 병력 여유 확인
        Require(Mathf.Approximately(wanted.SharedAwarenessDuration, 5f), "Shared awareness 5초 위치 공유 시간이 잘못되었습니다."); checks++; // 전체 경비 정확 위치 공유 시간 확인
        Require(response.SearchRadius >= 30f, "Free pursuit / free movement 수색 반경이 너무 작습니다."); checks++; // 횡단보도 그래프 없이 넓게 분산 수색할 범위 확인
        Require(ProjectKDay21CombatSourcePatch.IsPatched(), "총기·검 시민/차량 피해 소스 패치가 적용되지 않았습니다."); checks++; // 기존 무기 연결 확인
        checks += ValidateRules(); // Heat·별 규칙 검사 추가
        checks += ValidateCityLifePrefabs(); // Day20 시민·차량 프리팹 피해 연결 검사
        if (strict) // 중복 설치 검사
        {
            MapWantedSystem[] systems = wanted.World.GetComponentsInChildren<MapWantedSystem>(true); // 월드 내 수배 관리자 전체 조회
            Require(systems.Length == 1, "MapWantedSystem이 중복 설치되었습니다."); checks++; // 단일 수배 시스템 보장
            MapWantedResponseManager[] responses = wanted.World.GetComponentsInChildren<MapWantedResponseManager>(true); // 월드 내 증원 관리자 전체 조회
            Require(responses.Length == 1, "MapWantedResponseManager가 중복 설치되었습니다."); checks++; // 단일 증원 관리자 보장
        }
        return checks; // 전체 통과 항목 수 반환
    }

    private static int ValidateRules() // Heat·별·추적 반경·병력 규칙 검사
    {
        int checks = 0; // 규칙 통과 수 초기화
        Require(MapWantedRules.MaximumStars == 5, "최대 수배 단계가 5성이 아닙니다."); checks++; // 최대 별 확인
        Require(MapWantedRules.StarsForHeat(0f) == 0 && MapWantedRules.StarsForHeat(10f) == 1, "0~1성 Heat 기준이 잘못되었습니다."); checks++; // 일성 기준 확인
        Require(MapWantedRules.StarsForHeat(30f) == 2 && MapWantedRules.StarsForHeat(60f) == 3, "2~3성 Heat 기준이 잘못되었습니다."); checks++; // 이삼성 기준 확인
        Require(MapWantedRules.StarsForHeat(100f) == 4 && MapWantedRules.StarsForHeat(150f) == 5, "4~5성 Heat 기준이 잘못되었습니다."); checks++; // 사오성 기준 확인
        Require(MapWantedRules.PursuitRadius(1) == 80f && MapWantedRules.PursuitRadius(5) == 450f, "수배 단계 추적 반경 규칙이 잘못되었습니다."); checks++; // 추적 범위 확인
        Require(MapWantedRules.GuardTargetCount(1) == 3 && MapWantedRules.GuardTargetCount(5) == 16, "수배 단계 병력 수 규칙이 잘못되었습니다."); checks++; // 증원 수 확인
        Require(MapWantedRules.EliteRatio(2) == 0f && MapWantedRules.EliteRatio(5) >= 0.60f, "고수배 정예 경비 비율 규칙이 잘못되었습니다."); checks++; // 정예 등장 단계 확인
        Require(MapWantedRules.DecayDelayForStars(1) == 8f && MapWantedRules.DecayDelayForStars(5) == 30f, "수배 감소 대기 규칙이 잘못되었습니다."); checks++; // 별 감소 대기 확인
        Require(MapWantedRules.HeatForCrime(CrimeType.CitizenKilled) == 25f && MapWantedRules.HeatForCrime(CrimeType.GuardKilled) == 35f, "처치 범죄 Heat 규칙이 잘못되었습니다."); checks++; // 범죄 Heat 확인
        return checks; // 규칙 통과 수 반환
    }

    private static int ValidateCityLifePrefabs() // Day20 여섯 프리팹의 체력·피격 컴포넌트 검사
    {
        int checks = 0; // 프리팹 통과 수 초기화
        checks += RequireCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Human.prefab", MapCitizenKind.Human); // 인간 시민 검사
        checks += RequireCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Android.prefab", MapCitizenKind.Android); // 안드로이드 시민 검사
        checks += RequireCitizen("Assets/_Project/Generated/Map20/Prefabs/Citizen_Mechanical.prefab", MapCitizenKind.Mechanical); // 기계화 시민 검사
        checks += RequireVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Civilian.prefab", MapVehicleKind.Civilian); // 일반 차량 검사
        checks += RequireVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Delivery.prefab", MapVehicleKind.Delivery); // 배달 차량 검사
        checks += RequireVehicle("Assets/_Project/Generated/Map20/Prefabs/Vehicle_Cargo.prefab", MapVehicleKind.Cargo); // 화물 차량 검사
        return checks; // 여섯 프리팹 검사 수 반환
    }

    private static int RequireCitizen(string path, MapCitizenKind kind) // 시민 프리팹 생명 구성 검사
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 시민 프리팹 조회
        Require(prefab != null, "시민 프리팹이 없습니다: " + path); // 프리팹 존재 확인
        Require(prefab.GetComponent<MapCitizenVitals>() != null && prefab.GetComponent<WorldDamageReceiver>() != null, "시민 체력·피격 컴포넌트가 없습니다: " + path); // 피해 연결 확인
        return 1; // 한 프리팹 검사 통과
    }

    private static int RequireVehicle(string path, MapVehicleKind kind) // 차량 프리팹 생명 구성 검사
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 차량 프리팹 조회
        Require(prefab != null, "차량 프리팹이 없습니다: " + path); // 프리팹 존재 확인
        Require(prefab.GetComponent<MapVehicleVitals>() != null && prefab.GetComponent<WorldDamageReceiver>() != null, "차량 내구도·피격 컴포넌트가 없습니다: " + path); // 피해 연결 확인
        return 1; // 한 프리팹 검사 통과
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 지정 씬의 단일 컴포넌트 조회
    {
        T found = null; // 첫 결과 저장 변수
        foreach (GameObject root in scene.GetRootGameObjects()) // 씬 루트 순회
        {
            foreach (T item in root.GetComponentsInChildren<T>(true)) // 비활성 자식까지 조회
            {
                if (found != null) // 두 번째 결과 확인
                {
                    throw new InvalidOperationException(typeof(T).Name + "가 두 개 이상 있습니다."); // 중복 구성 보고
                }
                found = item; // 첫 결과 저장
            }
        }
        return found; // 단일 결과 반환
    }

    private static void Require(bool condition, string message) // 읽기 쉬운 검사 실패 도우미
    {
        if (!condition) // 검사 조건 실패 확인
        {
            throw new InvalidOperationException(message); // 저장 중단 가능한 오류 발생
        }
    }
}
#endif
