#if UNITY_EDITOR // 에디터 전용 컴파일
using System; // 형식 기능
using System.Reflection; // 리플렉션 기능
using UnityEditor; // 유니티 에디터 기능
using UnityEngine; // 유니티 기본 기능

public static class ProjectKDay8SceneViewCleanup // Scene View 아이콘 정리 도구
{
    private const string SessionKey = "ProjectK.Day8.SceneViewCleanup.V1"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleCleanup() // 자동 정리 예약
    {
        if (SessionState.GetBool(SessionKey, false)) // 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += HidePlayerInputIcon; // 아이콘 정리 예약
    }

    [MenuItem("Project K/Day 8/Hide PlayerInput Scene Icon")] // 수동 정리 메뉴
    public static void HidePlayerInputIcon() // PlayerInput Scene 아이콘 숨김
    {
        Type annotationUtilityType = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility"); // AnnotationUtility 형식 조회

        if (annotationUtilityType == null) // 형식 확인
        {
            Debug.LogWarning("Project K Day 8: AnnotationUtility not found."); // 경고 출력
            return; // 처리 중단
        }

        MethodInfo getAnnotations = annotationUtilityType.GetMethod("GetAnnotations", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); // 주석 목록 함수 조회
        MethodInfo setIconEnabled = annotationUtilityType.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); // 아이콘 설정 함수 조회

        if (getAnnotations == null || setIconEnabled == null) // 함수 확인
        {
            Debug.LogWarning("Project K Day 8: Scene icon API not found."); // 경고 출력
            return; // 처리 중단
        }

        Array annotations = getAnnotations.Invoke(null, null) as Array; // 주석 목록 조회

        if (annotations == null) // 주석 목록 확인
        {
            return; // 처리 중단
        }

        for (int i = 0; i < annotations.Length; i++) // 주석 목록 순회
        {
            object annotation = annotations.GetValue(i); // 현재 주석 조회

            if (annotation == null) // 주석 확인
            {
                continue; // 무효 주석 제외
            }

            Type annotationType = annotation.GetType(); // 주석 형식 조회
            FieldInfo classIdField = annotationType.GetField("classID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); // 클래스 ID 필드 조회
            FieldInfo scriptClassField = annotationType.GetField("scriptClass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); // 스크립트 이름 필드 조회

            if (classIdField == null || scriptClassField == null) // 필드 확인
            {
                continue; // 지원하지 않는 주석 제외
            }

            int classId = (int)classIdField.GetValue(annotation); // 클래스 ID 조회
            string scriptClass = scriptClassField.GetValue(annotation) as string; // 스크립트 클래스 이름 조회

            if (scriptClass != "PlayerInput") // PlayerInput 주석 확인
            {
                continue; // 다른 아이콘 제외
            }

            setIconEnabled.Invoke(null, new object[] { classId, scriptClass, 0 }); // PlayerInput Scene 아이콘 비활성화
            SceneView.RepaintAll(); // Scene View 갱신
            Debug.Log("Project K Day 8 PlayerInput Scene icon hidden."); // 완료 로그 출력
            return; // 처리 완료
        }

        Debug.LogWarning("Project K Day 8: PlayerInput Scene icon annotation not found. Use Scene View Gizmos menu to disable PlayerInput icon manually."); // 대체 안내 출력
    }
}
#endif // 에디터 전용 컴파일 종료
