#if UNITY_EDITOR // Unity Editor 전용
using System; // 오류 처리
using System.IO; // Editor 스크립트 파일 처리
using System.Text.RegularExpressions; // MenuItem 속성 검색
using UnityEditor; // AssetDatabase와 InitializeOnLoad 사용
using UnityEngine; // Console 출력

[InitializeOnLoad] // Unity 컴파일 완료 후 자동 실행
public static class ProjectKDayMenuCleanup // Project K 탭 Day 메뉴 자동 제거
{
    private const string EditorRoot = "Assets/_Project/Editor"; // 검사할 Editor 루트
    private const string SelfPath = "Assets/_Project/Editor/ProjectKDayMenuCleanup.cs"; // 자동 제거 스크립트 경로
    private const string SessionKey = "ProjectK.DayMenuCleanup.Ran"; // 같은 컴파일 세션 중복 실행 방지
    private static readonly Regex DayMenuPattern = new Regex( // Project K/Day MenuItem 속성 검색식
        @"\[\s*MenuItem\s*\(\s*""Project\s*K\s*/\s*Day[^""]*""[^)]*\)\s*\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled); // 대소문자 무시와 반복 검색 최적화

    static ProjectKDayMenuCleanup() // Unity Editor 자동 실행 진입점
    {
        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 실행 여부 확인
        {
            return; // 중복 수정 방지
        }

        SessionState.SetBool(SessionKey, true); // 현재 세션 실행 완료 예약
        EditorApplication.delayCall += RemoveDayMenus; // AssetDatabase 준비 뒤 실제 수정 실행
    }

    private static void RemoveDayMenus() // Project K 탭의 Day 메뉴 등록 제거
    {
        if (!Directory.Exists(EditorRoot)) // Editor 폴더 존재 확인
        {
            DeleteSelf(); // 정리 스크립트 자체 제거
            return; // 처리 종료
        }

        int changedFiles = 0; // 수정된 파일 수 집계
        int removedMenus = 0; // 제거된 MenuItem 수 집계
        string[] files = Directory.GetFiles(EditorRoot, "*.cs", SearchOption.AllDirectories); // 모든 Editor C# 파일 검색

        foreach (string file in files) // Editor 스크립트 순회
        {
            string normalized = file.Replace('\\', '/'); // Unity 경로 형식 통일
            if (string.Equals(normalized, SelfPath, StringComparison.OrdinalIgnoreCase)) // 자기 자신 파일 확인
            {
                continue; // 정리 스크립트는 검색 제외
            }

            string original = File.ReadAllText(file); // 현재 소스 읽기
            MatchCollection matches = DayMenuPattern.Matches(original); // Project K/Day MenuItem 검색
            if (matches.Count <= 0) // Day 메뉴가 없는 파일 확인
            {
                continue; // 다음 파일 처리
            }

            string updated = DayMenuPattern.Replace(original, string.Empty); // Day MenuItem 속성만 제거
            if (string.Equals(original, updated, StringComparison.Ordinal)) // 실제 변경 여부 확인
            {
                continue; // 변경 없는 파일 제외
            }

            File.WriteAllText(file, updated); // 기존 스크립트 기능은 유지하고 메뉴 속성만 제거
            changedFiles++; // 수정 파일 수 증가
            removedMenus += matches.Count; // 제거 메뉴 수 누적
        }

        Debug.Log("Project K Day 메뉴 정리 완료 · 수정 파일 " + changedFiles + "개 · 제거 메뉴 " + removedMenus + "개"); // 처리 결과 출력
        AssetDatabase.Refresh(); // 수정된 Editor 스크립트 재임포트
        EditorApplication.delayCall += DeleteSelf; // 변경 적용 뒤 정리 스크립트 자체 삭제
    }

    private static void DeleteSelf() // 일회용 정리 스크립트 제거
    {
        if (AssetDatabase.LoadAssetAtPath<MonoScript>(SelfPath) != null) // 자기 자신 에셋 존재 확인
        {
            AssetDatabase.DeleteAsset(SelfPath); // 정리 스크립트와 meta 삭제
        }

        AssetDatabase.Refresh(); // Project 창과 메뉴 갱신
    }
}
#endif
