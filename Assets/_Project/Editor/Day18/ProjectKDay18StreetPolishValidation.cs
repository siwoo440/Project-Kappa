#if UNITY_EDITOR // 18일차 간판·거리 보정 검사 전용
using System; // 검사 실패 예외 처리
using ProjectK.Day18; // 현재 일차 밀도와 거리 보정 표식 참조
using UnityEditor; // 검사 메뉴와 선택
using UnityEngine; // 실제 문자 바운드와 씬 오브젝트 검사
using UnityEngine.SceneManagement; // 활성 씬 경로 검사

public static class ProjectKDay18StreetPolishValidation // 간판 관통과 거리 디테일 검사
{
    [MenuItem("Project K/Day 18/Validate Signs And Streetscape")] // 실제 Map 보정 검사 메뉴
    public static void ValidateMenu() // 활성 Map의 거리 보정 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay18StreetPolishFix.MapScenePath) // Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 검사 대상 안내
            return; // 다른 씬 검사 중단
        }
        MapUrbanStreetPolishMarker marker = FindSingle<MapUrbanStreetPolishMarker>(scene); // 설치 결과 표식 조회
        if (marker == null) // 설치 누락 확인
        {
            Debug.LogError("Day18 거리 보정 레이어가 없습니다. Repair Signs And Streetscape 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 실패를 Console에 명확히 표시
        {
            int checks = Validate(marker, true); // 전체 구조 검사 실행
            Debug.Log("Day18 간판·거리 보정 검사 통과 · " + checks + "항목"); // 검사 결과 안내
            Selection.activeGameObject = marker.gameObject; // 결과 루트 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    [MenuItem("Project K/Day 18/Test Sign Bounds And Depth")] // 문자 바운드 중심 빠른 검사 메뉴
    public static void RuleTestMenu() // 동일한 검사 재사용
    {
        ValidateMenu(); // 실제 검사 실행
    }

    public static int Validate(MapUrbanStreetPolishMarker marker, bool strict) // 저장 전과 수동 메뉴 공통 검사
    {
        int checks = 0; // 통과 항목 수 집계
        Require(marker != null, "Day18 거리 보정 표식이 필요합니다."); checks++; // 표식 존재 확인
        Require(marker.Completed, "Day18 거리 보정 생성이 완료 상태가 아닙니다."); checks++; // 완료 플래그 확인
        Require(marker.World != null, "MapWorldRoot 참조가 없습니다."); checks++; // 본편 월드 참조 확인
        Require(marker.FixedSignCount >= 20, "수정된 간판 수가 부족합니다: " + marker.FixedSignCount); checks++; // 간판 보정 적용 확인
        Require(marker.PoleCount >= 30, "전봇대 수가 부족합니다: " + marker.PoleCount); checks++; // 거리 구조 밀도 확인
        Require(marker.SidewalkCount >= 40, "보도 블럭 구간 수가 부족합니다: " + marker.SidewalkCount); checks++; // 보행 공간 디테일 확인
        Require(marker.LightCount >= 18, "추가 조명 수가 부족합니다: " + marker.LightCount); checks++; // 야간 거리 조명 확인
        Require(marker.ClusterCount >= 12, "거리 표시 묶음 수가 부족합니다: " + marker.ClusterCount); checks++; // 넓은 맵 표시 분할 확인
        int signCount = 0; // 실제 검사한 네온 문자 수
        int overflow = 0; // 패널 크기를 넘는 문자 수
        int wrongShader = 0; // 깊이 검사를 사용하지 않는 문자 수
        foreach (TextMesh text in marker.World.GetComponentsInChildren<TextMesh>(true)) // 전체 월드 문자 순회
        {
            if (text == null || text.transform.parent == null) // 문자 부모 확인
            {
                continue; // 잘못된 문자 제외
            }
            Transform sign = text.transform.parent; // 간판 루트 조회
            Transform frame = sign.Find("Frame"); // 프레임 조회
            Transform face = sign.Find("NeonFace"); // 전면 패널 조회
            if (frame == null || face == null) // 네온 간판 구조 확인
            {
                continue; // 포스터와 일반 문자 제외
            }
            signCount++; // 실제 간판 문자 집계
            Renderer renderer = text.GetComponent<Renderer>(); // 문자 렌더러 조회
            if (renderer == null) // 렌더러 누락 확인
            {
                overflow++; // 정상 간판으로 간주하지 않음
                continue; // 다음 문자 검사
            }
            Bounds bounds = renderer.localBounds; // 문자 자체 로컬 바운드 조회
            float width = bounds.size.x * Mathf.Abs(text.transform.localScale.x); // 최종 문자 가로 크기 계산
            float height = bounds.size.y * Mathf.Abs(text.transform.localScale.y); // 최종 문자 세로 크기 계산
            float panelWidth = Mathf.Max(0.8f, frame.localScale.x - 0.35f); // 간판 내부 폭 계산
            float panelHeight = Mathf.Max(0.55f, frame.localScale.y - 0.35f); // 간판 내부 높이 계산
            if (width > panelWidth * 0.90f + 0.03f || height > panelHeight * 0.78f + 0.03f) // 여유를 포함한 실제 넘침 확인
            {
                overflow++; // 패널 밖 문자 집계
            }
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || renderer.sharedMaterial.shader.name != "ProjectK/WorldText") // 깊이 검사 셰이더 확인
            {
                wrongShader++; // 벽 관통 가능 문자 집계
            }
        }
        Require(signCount >= 20, "검사할 네온 간판 수가 부족합니다: " + signCount); checks++; // 실제 간판 존재 확인
        Require(overflow == 0, "간판 패널을 넘어가는 문자가 남아 있습니다: " + overflow); checks++; // 문자 크기 수정 확인
        Require(wrongShader == 0, "3D 깊이 검사 셰이더가 적용되지 않은 간판이 있습니다: " + wrongShader); checks++; // 건물 관통 방지 확인
        Require(MapUrbanStreetPolishBuilder.Find(marker.World.transform, "Day19_UrbanPolishLayer") == null, "구형 Day19 씬 루트가 남아 있습니다."); checks++; // 잘못된 일차 루트 제거 확인
        if (strict) // 저장 전 중복 설치 검사
        {
            MapUrbanStreetPolishMarker[] all = marker.World.GetComponentsInChildren<MapUrbanStreetPolishMarker>(true); // 현재 표식 전체 조회
            Require(all.Length == 1, "Day18 거리 보정 루트가 중복되었습니다."); checks++; // 반복 실행 중복 생성 방지
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
