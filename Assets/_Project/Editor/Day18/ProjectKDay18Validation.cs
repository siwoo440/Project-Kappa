#if UNITY_EDITOR // 18일차 도시 밀도 검사 전용
using System; // 검사 실패 예외 처리
using ProjectK.Day17; // 거리 묶음과 네온 참조
using ProjectK.Day18; // 밀도 설치 표식 참조
using UnityEditor; // 검사 메뉴와 선택
using UnityEngine; // 씬 오브젝트 검사
using UnityEngine.SceneManagement; // 활성 씬 경로 검사

public static class ProjectKDay18Validation // 도로·상점·차량·공중 구조 밀도 검사
{
    [MenuItem("Project K/Day 18/Validate Urban Density")] // 실제 Map 밀도 검사 메뉴
    public static void ValidateMenu() // 활성 Map의 밀도 레이어 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay18DensitySetup.MapScenePath) // Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 검사 대상 안내
            return; // 다른 씬 검사 중단
        }
        MapUrbanDensityMarker marker = FindSingle<MapUrbanDensityMarker>(scene); // 설치 결과 표식 조회
        if (marker == null) // 설치 누락 확인
        {
            Debug.LogError("Day18 도시 밀도 레이어가 없습니다. Increase Urban Density 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 실패를 Console에 명확히 표시
        {
            int checks = Validate(marker, true); // 전체 구조 검사 실행
            Debug.Log("Day18 도시 밀도 검사 통과 · " + checks + "항목"); // 검사 결과 안내
            Selection.activeGameObject = marker.gameObject; // 결과 루트 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    [MenuItem("Project K/Day 18/Test Urban Density Rules")] // 빠른 밀도 규칙 검사 메뉴
    public static void RuleTestMenu() // 실제 구조 검사 재사용
    {
        ValidateMenu(); // 동일한 검사 실행
    }

    public static int Validate(MapUrbanDensityMarker marker, bool strict) // 저장 전과 수동 메뉴 공통 검사
    {
        int checks = 0; // 통과 항목 수 집계
        Require(marker != null, "Day18 밀도 표식이 필요합니다."); checks++; // 표식 존재 확인
        Require(marker.Completed, "Day18 도시 밀도 생성이 완료 상태가 아닙니다."); checks++; // 완료 플래그 확인
        Require(marker.World != null, "MapWorldRoot 참조가 없습니다."); checks++; // 본편 월드 참조 확인
        Require(!string.IsNullOrEmpty(marker.SourceCommit), "Day18 기준 커밋 기록이 없습니다."); checks++; // 제작 기준 기록 확인
        Require(marker.StorefrontCount >= 90, "1층 상점 전면 수가 부족합니다: " + marker.StorefrontCount); checks++; // 보행 시야 밀도 확인
        Require(marker.VehicleCount >= 35, "차량과 바이크 수가 부족합니다: " + marker.VehicleCount); checks++; // 도로 전경 밀도 확인
        Require(marker.StreetPropCount >= 160, "거리 생활 소품 수가 부족합니다: " + marker.StreetPropCount); checks++; // 소형 오브젝트 밀도 확인
        Require(marker.RooftopCount >= 35, "옥상 증축과 설비 수가 부족합니다: " + marker.RooftopCount); checks++; // 건물 실루엣 분할 확인
        Require(marker.BridgeCount >= 10, "공중 연결 구조 수가 부족합니다: " + marker.BridgeCount); checks++; // 수직 공간 밀도 확인
        Require(marker.CableCount >= 60, "공중 케이블 수가 부족합니다: " + marker.CableCount); checks++; // 상부 공간 정보량 확인
        Require(marker.NeonSignCount >= 80, "추가 네온 간판 수가 부족합니다: " + marker.NeonSignCount); checks++; // 대중소 간판 밀도 확인
        Require(marker.ClusterCount >= 14, "거리 표시 묶음 수가 부족합니다: " + marker.ClusterCount); checks++; // 넓은 맵 표시 분할 확인
        MapDetailCluster[] clusters = marker.GetComponentsInChildren<MapDetailCluster>(true); // Day18 거리 표시 관리자 목록
        Require(clusters.Length == marker.ClusterCount, "거리 표시 묶음 집계와 실제 수가 다릅니다."); checks++; // 집계 일치 확인
        foreach (MapDetailCluster cluster in clusters) // 모든 표시 묶음 검사
        {
            Require(cluster.World == marker.World, cluster.name + " 월드 참조가 다릅니다."); // 플레이어 기준 연결 확인
            Require(cluster.Content != null, cluster.name + " 표시 자식이 없습니다."); // 실제 토글 대상 확인
            checks += 2; // 묶음별 검사 집계
        }
        Light[] lights = marker.GetComponentsInChildren<Light>(true); // Day18이 추가한 실제 광원 조회
        Require(lights.Length == 0, "Day18은 추가 점광원 없이 발광 재질만 사용해야 합니다: " + lights.Length); checks++; // 광원 부하 증가 방지
        MapNeonPulse[] pulses = marker.GetComponentsInChildren<MapNeonPulse>(true); // 추가 네온 맥동 표면 조회
        Require(pulses.Length >= marker.NeonSignCount, "간판보다 네온 맥동 표면 수가 적습니다."); checks++; // 발광 처리 연결 확인
        if (strict) // 저장 전 중복 설치 검사
        {
            MapUrbanDensityMarker[] all = marker.World.GetComponentsInChildren<MapUrbanDensityMarker>(true); // 월드 안 Day18 표식 전체 조회
            Require(all.Length == 1, "Day18 도시 밀도 루트가 중복되었습니다."); checks++; // 반복 실행 중복 생성 방지
        }
        return checks; // 전체 통과 항목 수 반환
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
        return found; // 단일 결과 또는 없음 반환
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
