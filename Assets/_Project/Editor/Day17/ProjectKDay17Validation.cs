#if UNITY_EDITOR // 17일차 도시 디테일 검사 전용
using System; // 검사 실패 예외 처리
using ProjectK.Day16; // 기존 Map 월드 참조
using ProjectK.Day17; // 디테일 런타임 참조
using UnityEditor; // 검사 메뉴와 선택
using UnityEngine; // 씬 객체와 광원 검사
using UnityEngine.SceneManagement; // 활성 씬 경로 검사

public static class ProjectKDay17Validation // 네온 도시 디테일의 참조와 과도한 광원 검사
{
    // 실제 Map 씬 검사 메뉴
    public static void ValidateMenu() // 활성 Map의 디테일 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay17CyberpunkSetup.MapScenePath) // Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 검사 대상 안내
            return; // 다른 씬 검사 중단
        }
        MapCyberpunkDetailMarker marker = FindSingle<MapCyberpunkDetailMarker>(scene); // 설치 결과 표식 조회
        if (marker == null) // 설치 누락 확인
        {
            Debug.LogError("Day17 디테일이 없습니다. Enhance Map Cyberpunk City 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 오류를 Console에 명확히 표시
        {
            int checks = Validate(marker, true); // 전체 구조 검사 실행
            Debug.Log("Day17 사이버펑크 도시 검사 통과 · " + checks + "항목"); // 검사 결과 안내
            Selection.activeGameObject = marker.gameObject; // 결과 루트 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    // 빠른 규칙 검사 메뉴
    public static void RuleTestMenu() // 현재 씬의 규칙 검사 재사용
    {
        ValidateMenu(); // 동일한 실제 구조 검사를 실행
    }

    public static int Validate(MapCyberpunkDetailMarker marker, bool strict) // 저장 전과 수동 메뉴에서 공통 검사
    {
        int checks = 0; // 통과 항목 수 집계
        Require(marker != null, "Day17 디테일 표식이 필요합니다."); checks++; // 표식 존재 확인
        Require(marker.Completed, "Day17 디테일 생성이 완료 상태가 아닙니다."); checks++; // 완료 플래그 확인
        Require(marker.World != null, "MapWorldRoot 참조가 없습니다."); checks++; // 본편 월드 참조 확인
        Require(marker.SourceCommit == MapCyberpunkDetailBuilder.SourceCommit, "Day17 기준 커밋 기록이 다릅니다."); checks++; // 제작 기준 일치 확인
        Require(marker.NeonSignCount >= 24, "네온 간판 수가 예상보다 적습니다: " + marker.NeonSignCount); checks++; // 충분한 도시 네온 밀도 확인
        Require(marker.PropCount >= 100, "세부 소품 수가 예상보다 적습니다: " + marker.PropCount); checks++; // 세부 모델링 밀도 확인
        Require(marker.LightCount > 0 && marker.LightCount <= 24, "실제 점광원 수를 확인하세요: " + marker.LightCount); checks++; // 성능을 고려한 실제 광원 수 제한
        Require(marker.ClusterCount >= 9, "거리 표시 묶음 수가 부족합니다: " + marker.ClusterCount); checks++; // 먼 장식 비활성 구조 확인
        MapDetailCluster[] clusters = marker.GetComponentsInChildren<MapDetailCluster>(true); // 거리 표시 관리자 목록 조회
        Require(clusters.Length == marker.ClusterCount, "거리 표시 묶음 집계와 실제 개수가 다릅니다."); checks++; // 집계와 실제 구조 일치 확인
        foreach (MapDetailCluster cluster in clusters) // 모든 거리 묶음 검사
        {
            Require(cluster.World == marker.World, cluster.name + " 월드 참조가 다릅니다."); // 올바른 플레이어 기준 확인
            Require(cluster.Content != null, cluster.name + " 표시 자식이 없습니다."); // 토글 대상 존재 확인
            Require(cluster.VisibleDistance >= 40f, cluster.name + " 표시 거리가 너무 작습니다."); // 잘못된 거리 설정 방지
            checks += 3; // 묶음별 세 항목 통과 집계
        }
        MapNeonPulse[] pulses = marker.GetComponentsInChildren<MapNeonPulse>(true); // 네온 맥동 목록 조회
        Require(pulses.Length >= marker.NeonSignCount, "네온 맥동 표면 수가 간판 수보다 적습니다."); checks++; // 간판과 발광 효과 연결 확인
        foreach (MapNeonPulse pulse in pulses) // 모든 맥동 표면 검사
        {
            Require(pulse.TargetRenderer != null, pulse.name + " 발광 렌더러가 없습니다."); // 실제 표면 참조 확인
            checks++; // 맥동별 검사 통과 집계
        }
        Light[] lights = marker.GetComponentsInChildren<Light>(true); // Day17이 생성한 실제 광원만 조회
        Require(lights.Length == marker.LightCount, "광원 집계와 실제 개수가 다릅니다."); checks++; // 광원 집계 일치 확인
        foreach (Light light in lights) // 모든 실제 네온 광원 검사
        {
            Require(light.type == LightType.Point, light.name + "는 점광원이 아닙니다."); // 계획하지 않은 광원 종류 차단
            Require(light.range <= 18.01f, light.name + " 광원 범위가 너무 큽니다."); // 과도한 추가 광원 범위 제한
            Require(light.shadows == LightShadows.None, light.name + " 네온 광원 그림자를 끄세요."); // 다수 점광원 그림자 부하 차단
            checks += 3; // 광원별 세 항목 통과 집계
        }
        if (strict) // 저장 전 추가 중복 검사
        {
            MapCyberpunkDetailMarker[] all = marker.World.GetComponentsInChildren<MapCyberpunkDetailMarker>(true); // 월드 안 설치 표식 전체 조회
            Require(all.Length == 1, "Day17 디테일 루트가 중복되었습니다."); checks++; // 반복 실행 중복 생성 방지
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
