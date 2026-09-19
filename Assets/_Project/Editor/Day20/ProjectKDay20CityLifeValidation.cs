#if UNITY_EDITOR // 20일차 교통·시민 검사 메뉴
using System; // 검사 오류 처리
using ProjectK.Day20; // 교통 계산과 생활 관리자 참조
using UnityEditor; // 검사 메뉴와 선택
using UnityEngine; // 벡터와 씬 오브젝트 검사
using UnityEngine.SceneManagement; // 활성 Map 씬 확인

public static class ProjectKDay20CityLifeValidation // 차량·보행 그래프와 풀 구성 검사
{
    [MenuItem("Project K/Day 20/Validate Traffic And Citizens")] // 실제 씬 검사 메뉴
    public static void ValidateMenu() // 활성 Map의 도시 생활 구성 검사
    {
        Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회
        if (!scene.IsValid() || scene.path != ProjectKDay20CityLifeSetup.MapScenePath) // 대상 Map 씬 확인
        {
            Debug.LogWarning("Map.unity를 열고 검사하세요."); // 올바른 검사 대상 안내
            return; // 다른 씬 검사 중단
        }
        MapCityLifeManager manager = FindSingle<MapCityLifeManager>(scene); // 도시 생활 관리자 조회
        if (manager == null) // 설치 누락 확인
        {
            Debug.LogError("Day20_CityLife가 없습니다. Setup Traffic And Citizens 메뉴를 먼저 실행하세요."); // 선행 설치 안내
            return; // 검사 중단
        }
        try // 검사 결과를 Console에 표시
        {
            int checks = Validate(manager, true); // 전체 구성 검사 실행
            Debug.Log("Day20 교통·시민 검사 통과 · " + checks + "항목"); // 통과 수 안내
            Selection.activeGameObject = manager.gameObject; // 검사 결과 선택
        }
        catch (Exception error) // 한 항목이라도 실패한 경우
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    [MenuItem("Project K/Day 20/Test City Life Rules")] // 그래프 수학과 경계 규칙 검사 메뉴
    public static void RuleTestMenu() // 씬 관리자 없이도 핵심 규칙 검사
    {
        try // 순수 계산 검사 예외 처리
        {
            int checks = ValidateRules(1536f); // 기본 512m 3x3 월드 기준 검사
            Debug.Log("Day20 도시 생활 규칙 검사 통과 · " + checks + "항목"); // 규칙 검사 결과 안내
        }
        catch (Exception error) // 규칙 실패 처리
        {
            Debug.LogException(error); // 구체적인 실패 원인 출력
        }
    }

    public static int Validate(MapCityLifeManager manager, bool strict) // 설치 직후와 수동 메뉴 공통 검사
    {
        int checks = 0; // 통과 항목 수 초기화
        Require(manager != null, "MapCityLifeManager가 필요합니다."); checks++; // 관리자 존재 확인
        Require(manager.World != null, "도시 생활 MapWorldRoot 참조가 없습니다."); checks++; // 월드 참조 확인
        Require(manager.World.Player != null, "도시 생활 플레이어 참조가 없습니다."); checks++; // 플레이어 존재 확인
        Require(!string.IsNullOrEmpty(manager.SourceCommit), "Day20 기준 커밋 기록이 없습니다."); checks++; // 기준 기록 확인
        Require(manager.HasAllPrefabs(), "차량 3종 또는 시민 3종 프리팹 참조가 누락되었습니다."); checks++; // 생성 프리팹 확인
        Require(manager.TargetVehicleCount >= 12 && manager.TargetVehicleCount <= manager.VehiclePoolSize, "차량 목표 수와 풀 크기를 확인하세요."); checks++; // 차량 풀 여유 확인
        Require(manager.TargetCitizenCount >= 24 && manager.TargetCitizenCount <= manager.CitizenPoolSize, "시민 목표 수와 풀 크기를 확인하세요."); checks++; // 시민 풀 여유 확인
        checks += ValidateRules(manager.World.WorldSize); // 현재 Map 크기로 핵심 수학 검사
        if (strict) // 중복 설치 검사
        {
            MapCityLifeManager[] all = manager.World.GetComponentsInChildren<MapCityLifeManager>(true); // 월드 내 관리자 전체 조회
            Require(all.Length == 1, "Day20_CityLife가 중복 설치되었습니다."); checks++; // 단일 생활 관리자 보장
        }
        return checks; // 전체 통과 항목 수 반환
    }

    private static int ValidateRules(float worldSize) // 도로·보도 그래프 핵심 규칙 검사
    {
        int checks = 0; // 규칙 통과 수 초기화
        Require(MapTrafficMath.BlockCount == 12 && MapTrafficMath.RoadCount == 13, "Day16 12x12 도로 구조와 맞지 않습니다."); checks++; // 도로 격자 수 확인
        Require(MapTrafficMath.CanAdvance(6, 6, MapTrafficDirection.East), "중앙 교차로 동쪽 진행이 막혀 있습니다."); checks++; // 중앙 도로 진행 확인
        Require(!MapTrafficMath.CanAdvance(12, 6, MapTrafficDirection.East), "동쪽 월드 경계 밖 진행이 허용됩니다."); checks++; // 동쪽 경계 확인
        Require(MapTrafficMath.Opposite(MapTrafficDirection.North) == MapTrafficDirection.South, "교통 유턴 반대 방향 계산이 잘못되었습니다."); checks++; // 반대 방향 검사
        Vector3 east = MapTrafficMath.LanePoint(6, 6, MapTrafficDirection.East, worldSize, 16.2f); // 동쪽 차선 좌표 계산
        Vector3 west = MapTrafficMath.LanePoint(6, 6, MapTrafficDirection.West, worldSize, 16.2f); // 서쪽 차선 좌표 계산
        Require(Mathf.Abs(east.z - west.z) > 1f, "양방향 차량 차선이 분리되지 않았습니다."); checks++; // 양방향 차선 분리 검사
        MapPedestrianGraph graph = MapPedestrianGraph.Build(worldSize, 16.24f); // 보행 그래프 생성
        Require(graph.Count == MapTrafficMath.RoadCount * MapTrafficMath.RoadCount * 4, "보행 그래프 노드 수가 잘못되었습니다."); checks++; // 676 노드 확인
        int center = ((6 * MapTrafficMath.RoadCount + 6) * 4); // 중앙 교차로 북서 모서리 번호 계산
        Require(graph.Neighbors(center).Length >= 4, "중앙 보도 노드의 연결 수가 부족합니다."); checks++; // 일반 보도와 횡단 연결 확인
        Require(graph.IsCrosswalk(center, center + 1), "같은 교차로 횡단보도 연결을 인식하지 못합니다."); checks++; // 횡단보도 판정 확인
        return checks; // 규칙 통과 수 반환
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
        if (!condition) // 검사 조건 실패 확인
        {
            throw new InvalidOperationException(message); // 저장 중단 가능한 오류 발생
        }
    }
}
#endif
