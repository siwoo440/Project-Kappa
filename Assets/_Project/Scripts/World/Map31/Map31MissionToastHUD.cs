using ProjectK.Day30; // 공통 청록·파란색 UI 테마
using UnityEngine; // 목표·임무 전환 알림 HUD

namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    [DisallowMultipleComponent] // 알림 HUD 중복 방지
    public sealed class Map31MissionToastHUD : MonoBehaviour // 목표 완료·신규 목표·임무 완료 중앙 알림
    {
        private static Map31MissionToastHUD instance; // 현재 알림 HUD 인스턴스
        private string title = string.Empty; // 현재 알림 제목
        private string detail = string.Empty; // 현재 알림 설명
        private float visibleUntil; // 알림 종료 시각
        private Color accent = Map30UITheme.Cyan; // 현재 알림 강조색
        private GUIStyle titleStyle; // 제목 스타일
        private GUIStyle detailStyle; // 설명 스타일

        public static Map31MissionToastHUD Instance => instance; // 현재 알림 HUD 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 HUD 참조 제거
        }

        private void Awake() // 단일 알림 HUD 등록
        {
            if (instance != null && instance != this) // 기존 HUD 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 HUD 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        public static void Show(string newTitle, string newDetail, float duration = 1.8f, bool success = false, bool warning = false) // 공통 미션 알림 표시
        {
            EnsureInstance(); // HUD 존재 보장
            instance.title = newTitle ?? string.Empty; // 제목 저장
            instance.detail = newDetail ?? string.Empty; // 설명 저장
            instance.visibleUntil = Time.unscaledTime + Mathf.Max(0.4f, duration); // 종료 시각 저장
            instance.accent = success ? Map30UITheme.Green : warning ? Map30UITheme.Amber : Map30UITheme.Cyan; // 알림 의미별 색상 적용
        }

        private void OnGUI() // 화면 상단 중앙에 짧은 임무 알림 표시
        {
            if (Time.unscaledTime >= visibleUntil || Map30MissionWindow.IsOpen) // 알림 종료 또는 Tab 임무 창 상태 확인
            {
                return; // 알림 숨김
            }

            EnsureStyles(); // 텍스트 스타일 준비
            float width = Mathf.Clamp(Screen.width * 0.34f, 360f, 560f); // 화면 크기 기반 알림 너비
            Rect rect = new Rect(Screen.width * 0.5f - width * 0.5f, 88f, width, 72f); // 상단 중앙 알림 영역
            Map30UITheme.DrawPanel(rect); // 공통 파란색 패널 출력
            Map30UITheme.DrawSolid(new Rect(rect.x, rect.y, 4f, rect.height), accent); // 왼쪽 상태 강조선

            Color previous = GUI.color; // 기존 GUI 색상 보존
            GUI.color = accent; // 제목 상태 색상 적용
            GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, rect.width - 32f, 24f), title, titleStyle); // 알림 제목 출력
            GUI.color = previous; // 기본 GUI 색상 복원
            GUI.Label(new Rect(rect.x + 16f, rect.y + 35f, rect.width - 32f, 25f), detail, detailStyle); // 알림 설명 출력
        }

        private void EnsureStyles() // 알림 텍스트 스타일 준비
        {
            if (titleStyle != null) // 이미 생성된 스타일 확인
            {
                return; // 재생성 생략
            }

            titleStyle = new GUIStyle(GUI.skin.label); // 제목 스타일 생성
            titleStyle.fontSize = 15; // 제목 크기
            titleStyle.fontStyle = FontStyle.Bold; // 제목 강조
            titleStyle.normal.textColor = Map30UITheme.Text; // 기본 청백색
            detailStyle = new GUIStyle(GUI.skin.label); // 설명 스타일 생성
            detailStyle.fontSize = 12; // 설명 크기
            detailStyle.normal.textColor = Map30UITheme.Muted; // 보조 청록색
        }

        private static void EnsureInstance() // 외부 호출 시 알림 HUD 자동 생성
        {
            if (instance != null) // 기존 HUD 확인
            {
                return; // 생성 생략
            }

            GameObject owner = new GameObject("[Day31] Mission Toast HUD"); // 알림 HUD 오브젝트 생성
            instance = owner.AddComponent<Map31MissionToastHUD>(); // HUD 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
