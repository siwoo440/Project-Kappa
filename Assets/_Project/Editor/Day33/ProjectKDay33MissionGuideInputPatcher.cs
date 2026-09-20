#if UNITY_EDITOR // Day33 G 목표 안내 입력과 소모품 키 재배치 자동 적용
using System; // 예외·문자열 처리
using System.IO; // 입력·HUD 소스 읽기쓰기
using UnityEditor; // SessionState·AssetDatabase 사용
using UnityEngine; // Console 출력

[InitializeOnLoad] // ZIP 덮어쓰기 후 한 번 자동 실행
public static class ProjectKDay33MissionGuideInputPatcher // G를 목표 안내 전용으로 확보하고 기존 소모품 사용을 H로 이동
{
    private const string SelfPath = "Assets/_Project/Editor/Day33/ProjectKDay33MissionGuideInputPatcher.cs"; // 일회용 패처 경로
    private const string SessionKey = "ProjectK.Day33.MissionGuideInputPatcher.Applied.V1"; // 같은 Editor 세션 중복 적용 방지
    private const string InputPath = "Assets/InputSystem_Actions.inputactions"; // Player UseItem 키 바인딩 파일
    private const string HudPath = "Assets/_Project/Scripts/UI/PlayerEquipmentHUD.cs"; // 장비 HUD 키 안내 대상

    static ProjectKDay33MissionGuideInputPatcher() // Editor 로드 후 안전 시점 패치 예약
    {
        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 적용 여부 확인
        {
            return; // 중복 수정 방지
        }

        EditorApplication.delayCall += Apply; // 첫 컴파일과 AssetDatabase 준비 뒤 실행
    }

    private static void Apply() // 입력 바인딩과 HUD 문구를 검증한 뒤 적용
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            EditorApplication.delayCall += Apply; // 다음 Editor 프레임 재시도
            return; // 현재 프레임 중단
        }

        try // Day33 목표 안내 키 적용
        {
            string input = ReadRequired(InputPath); // 최신 Input Actions JSON 읽기
            string hud = ReadRequired(HudPath); // 최신 장비 HUD 소스 읽기

            input = PatchInput(input); // UseItem G → H 변경
            hud = PatchHud(hud); // 화면 키 안내 G → H 및 목표 안내 추가

            File.WriteAllText(InputPath, input); // 입력 Actions 덮어쓰기
            File.WriteAllText(HudPath, hud); // 장비 HUD 덮어쓰기
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate); // InputAction 에셋 즉시 재가져오기
            AssetDatabase.ImportAsset(HudPath, ImportAssetOptions.ForceUpdate); // HUD 소스 재컴파일 예약
            SessionState.SetBool(SessionKey, true); // 현재 세션 적용 완료 기록
            Debug.Log("[Day33] G 임무 목표 안내 키 적용 완료 · 기존 소모품 사용 H로 이동"); // 성공 로그

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(SelfPath) != null) // 일회용 패처 존재 확인
            {
                AssetDatabase.DeleteAsset(SelfPath); // 적용 완료 후 자기 자신과 meta 삭제
            }

            AssetDatabase.Refresh(); // 변경 파일 재컴파일·재가져오기
        }
        catch (Exception error) // 현재 프로젝트 구조와 맞지 않을 때 부분 적용 방지
        {
            Debug.LogException(error); // 실제 오류 내용 출력
            Debug.LogError("[Day33] G 목표 안내 입력 자동 패치 중단 · 기존 파일은 저장하지 않았습니다."); // 실패 안내
        }
    }

    private static string PatchInput(string source) // UseItem의 G 바인딩만 H로 변경
    {
        const string gBinding = "\"path\": \"<Keyboard>/g\""; // 현재 G 키 바인딩 문자열
        const string hBinding = "\"path\": \"<Keyboard>/h\""; // 변경할 H 키 바인딩 문자열

        if (source.Contains(hBinding) && source.Contains("\"action\": \"UseItem\"")) // 이미 H 키로 수정된 상태 확인
        {
            return source; // 재실행 시 중복 변경 방지
        }

        int gCount = CountOccurrences(source, gBinding); // 전체 G 키 바인딩 개수 검사
        if (gCount != 1) // 최신 기준 유일한 UseItem G 바인딩인지 확인
        {
            throw new InvalidOperationException("InputSystem_Actions에서 예상한 G 바인딩 1개를 찾지 못했습니다. 발견 수: " + gCount); // 다른 입력과 충돌 가능성 보고
        }

        int bindingIndex = source.IndexOf(gBinding, StringComparison.Ordinal); // G 바인딩 위치 조회
        int actionIndex = source.IndexOf("\"action\": \"UseItem\"", bindingIndex, StringComparison.Ordinal); // 뒤쪽 UseItem 액션 위치 조회

        if (actionIndex < 0 || actionIndex - bindingIndex > 420) // 같은 바인딩 블록인지 거리로 확인
        {
            throw new InvalidOperationException("G 바인딩이 UseItem 액션인지 확인하지 못했습니다."); // 잘못된 키 변경 방지
        }

        return source.Remove(bindingIndex, gBinding.Length).Insert(bindingIndex, hBinding); // G를 H로 한 곳만 변경
    }

    private static string PatchHud(string source) // 장비 HUD 키 안내를 실제 입력과 맞춤
    {
        const string oldItemLine = "\"[R] 마비침 \" + (support != null ? support.RemainingDarts + \"/\" + support.Capacity : \"0\") + \"    [G] \" + (consumables != null ? consumables.SelectedName + \" x\" + consumables.SelectedCount : \"---\")";
        const string newItemLine = "\"[R] 마비침 \" + (support != null ? support.RemainingDarts + \"/\" + support.Capacity : \"0\") + \"    [H] \" + (consumables != null ? consumables.SelectedName + \" x\" + consumables.SelectedCount : \"---\")";

        if (!source.Contains(newItemLine)) // 소모품 키 문구 미수정 확인
        {
            if (!source.Contains(oldItemLine)) // 최신 HUD 구조 확인
            {
                throw new InvalidOperationException("PlayerEquipmentHUD 소모품 키 안내 패턴을 찾지 못했습니다."); // 잘못된 파일 수정 방지
            }

            source = source.Replace(oldItemLine, newItemLine); // G 소모품 안내를 H로 변경
        }

        const string oldHelp = "\"1~4 검 / 5~9 총기 / Q·E 교체\\nV 아이템 / F 상호작용\"";
        const string newHelp = "\"1~4 검 / 5~9 총기 / Q·E 교체\\nV 아이템 선택 / H 사용 / G 목표 안내 / F 상호작용\"";

        if (!source.Contains(newHelp)) // 목표 안내 도움말 미적용 확인
        {
            if (!source.Contains(oldHelp)) // 기존 도움말 구조 확인
            {
                throw new InvalidOperationException("PlayerEquipmentHUD 조작 안내 패턴을 찾지 못했습니다."); // 구조 변경 보고
            }

            source = source.Replace(oldHelp, newHelp); // G 목표 안내와 H 사용 키 안내 추가
        }

        return source; // 수정된 HUD 반환
    }

    private static int CountOccurrences(string source, string value) // 문자열 정확한 출현 횟수 계산
    {
        int count = 0; // 결과 초기화
        int index = 0; // 검색 시작 위치

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0) // 다음 일치 문자열 검색
        {
            count++; // 발견 수 증가
            index += value.Length; // 다음 검색 위치 이동
        }

        return count; // 전체 발견 수 반환
    }

    private static string ReadRequired(string path) // 필수 파일 안전 읽기
    {
        if (!File.Exists(path)) // 파일 존재 확인
        {
            throw new FileNotFoundException("Day33 목표 안내 패치 대상 파일을 찾지 못했습니다.", path); // 누락 파일 보고
        }

        return File.ReadAllText(path); // 최신 파일 내용 반환
    }
}
#endif
