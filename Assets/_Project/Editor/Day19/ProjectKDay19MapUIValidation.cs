#if UNITY_EDITOR // 19일차 지도 UI 검사 메뉴
using System; // 검사 오류 처리
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day19; // 지도 UI와 계산 참조
using UnityEditor; // 검사 메뉴와 선택
using UnityEngine; // 화면 좌표 검사
using UnityEngine.SceneManagement; // 활성 Map 씬 확인

public static class ProjectKDay19MapUIValidation // M 지도와 N 미니맵 구성 검사
{
    // 실제 씬 검사 메뉴
    public static void ValidateMenu() // 활성 Map 지도 UI 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay19MapUISetup.MapScenePath) // 대상 Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 씬 안내
            return; // 다른 씬 검사 중단
        }
        MapNavigationUI ui = FindSingle<MapNavigationUI>(scene); // 지도 UI 조회
        if (ui == null) // 설치 누락 확인
        {
            Debug.LogError("Day19 지도 UI가 없습니다. Setup World Map And Minimap 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 결과를 Console에 명확히 표시
        {
            int checks = Validate(ui, true); // 전체 구성 검사 실행
            Debug.Log("Day19 지도·미니맵 검사 통과 · " + checks + "항목"); // 통과 항목 안내
            Selection.activeGameObject = ui.gameObject; // 검사 결과 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    // 좌표와 크기 빠른 검사 메뉴
    public static void RuleTestMenu() // 동일한 검사 재사용
    {
        ValidateMenu(); // 실제 지도 UI 검사 실행
    }

    public static int Validate(MapNavigationUI ui, bool strict) // 설치 직후와 수동 메뉴 공통 검사
    {
        int checks = 0; // 통과 항목 수 초기화
        Require(ui != null, "MapNavigationUI가 필요합니다."); checks++; // UI 존재 확인
        Require(ui.World != null, "지도 UI의 MapWorldRoot 참조가 없습니다."); checks++; // 월드 참조 확인
        Require(ui.World.Player != null, "지도 UI 플레이어 참조가 없습니다."); checks++; // 플레이어 존재 확인
        Require(ui.World.PlayCamera != null, "지도 UI 카메라 참조가 없습니다."); checks++; // 카메라 존재 확인
        Require(ui.World.Places != null && ui.World.Places.Length >= 5, "주요 장소 MapPoint가 5개 이상 필요합니다."); checks++; // 랜드마크 표시 자료 확인
        Require(ui.World.WorldSize > 1000f, "본편 월드 크기를 확인하세요."); checks++; // 실제 Map 크기 확인
        Require(!string.IsNullOrEmpty(ui.SourceCommit), "지도 UI 기준 커밋 기록이 없습니다."); checks++; // 기준 기록 확인
        Rect rect = new Rect(0f, 0f, 100f, 100f); // 계산 검사용 화면 사각형
        Vector2 mapped = MapNavigationMath.WorldToViewport(Vector3.zero, Vector2.zero, 100f, rect); // 월드 중심 화면 변환
        Require(Vector2.Distance(mapped, new Vector2(50f, 50f)) < 0.001f, "월드 중심 지도 좌표 계산이 잘못되었습니다."); checks++; // 좌표 중심 검사
        Vector2 clamped = MapNavigationMath.ClampCenter(new Vector2(9999f, -9999f), 500f, 1500f); // 월드 밖 지도 중심 보정
        Require(Mathf.Abs(clamped.x) <= 500.001f && Mathf.Abs(clamped.y) <= 500.001f, "전체 지도 중심 제한 계산이 잘못되었습니다."); checks++; // 이동 경계 검사
        float small = MapNavigationMath.MiniMapPixelSize(0, 1920f, 1080f); // 소형 미니맵 계산
        float medium = MapNavigationMath.MiniMapPixelSize(1, 1920f, 1080f); // 중형 미니맵 계산
        float large = MapNavigationMath.MiniMapPixelSize(2, 1920f, 1080f); // 대형 미니맵 계산
        Require(small < medium && medium < large, "미니맵 크기 단계 순서가 잘못되었습니다."); checks++; // Shift+N 크기 규칙 검사
        Require(MapNavigationMath.ClampMiniMapSpan(1f) == 120f && MapNavigationMath.ClampMiniMapSpan(9999f) == 600f, "미니맵 표시 거리 제한이 잘못되었습니다."); checks++; // 대괄호 줌 범위 검사
        if (strict) // 씬 중복 설치 검사
        {
            MapNavigationUI[] all = ui.World.GetComponentsInChildren<MapNavigationUI>(true); // 월드 내 지도 UI 전체 조회
            Require(all.Length == 1, "MapNavigationUI가 중복 설치되었습니다."); checks++; // 단일 UI 보장
        }
        return checks; // 전체 통과 항목 수 반환
    }

    private static T FindSingle<T>(Scene scene) where T : Component // 지정 씬 단일 컴포넌트 조회
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
        if (!condition) // 조건 실패 확인
        {
            throw new InvalidOperationException(message); // 저장 중단 가능한 오류 발생
        }
    }
}
#endif
