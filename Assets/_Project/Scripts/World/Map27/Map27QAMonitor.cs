using ProjectK.Day30; // Day30 통합 HUD 테마와 Tab 임무 창 상태 참조
using System.Collections.Generic; // 개체별 이동 기록
using ProjectK.Day20; // 시민·차량 생활 개체 참조
using ProjectK.Day21; // 수배 경비 참조
using ProjectK.Day25; // E-04 감시 드론 참조
using ProjectK.Day26; // 신고 진행 상태 참조
using UnityEngine; // 런타임 QA와 HUD 처리
using UnityEngine.InputSystem; // 현재 프로젝트의 새 Input System 사용

using ProjectK.Day28; // Day28 런타임 Registry 참조
namespace ProjectK.Day27 // 27일차 통합 QA 이름 공간
{
    [DisallowMultipleComponent] // QA 관리자 중복 방지
    public sealed class Map27QAMonitor : MonoBehaviour // 경비·시민 정체와 활성 개체 수를 실시간 점검
    {
        private sealed class MovementTrack // 개체별 이전 위치와 정체 횟수
        {
            public Vector3 Position; // 직전 검사 위치
            public int StuckSamples; // 연속 정체 횟수
            public float NextLogAt; // 반복 로그 제한 시각
        }

        private static Map27QAMonitor instance; // 현재 QA 관리자
        private readonly Dictionary<int, MovementTrack> guardTracks = new Dictionary<int, MovementTrack>(); // 경비 이동 기록
        private readonly Dictionary<int, MovementTrack> citizenTracks = new Dictionary<int, MovementTrack>(); // 시민 이동 기록
        private float nextSampleTime; // 다음 이동 검사
        private float nextCountTime; // 다음 개체 집계
        private float smoothedFps = 60f; // 평균 FPS
        private int activeCitizens; // 활성 시민
        private int activeVehicles; // 활성 차량
        private int activeGuards; // 활성 경비
        private int activeHeavy; // E-03
        private int activeDrones; // E-04
        private int stuckHotspots; // 정체 지점 수
        private bool showHud = true; // QA HUD 표시 여부

        public static Map27QAMonitor Instance => instance; // 현재 QA 관리자 조회
        public int StuckHotspots => stuckHotspots; // 정체 지점 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 정적 상태 초기화
        {
            instance = null; // 이전 인스턴스 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 후 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 QA 관리자 생성
        {
            if (instance != null) // 기존 인스턴스 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day27] QA Monitor"); // QA 오브젝트 생성
            instance = owner.AddComponent<Map27QAMonitor>(); // QA 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 중복 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 인스턴스 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Update() // QA 갱신
        {
            float currentFps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : smoothedFps; // 현재 FPS 계산
            smoothedFps = Mathf.Lerp(smoothedFps, currentFps, 0.08f); // FPS 값 완화

            Keyboard keyboard = Keyboard.current; // 새 Input System 키보드 조회
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame) // F10 토글 입력 확인
            {
                showHud = !showHud; // QA HUD 표시 상태 전환
            }

            if (Time.unscaledTime >= nextSampleTime) // 이동 검사 시각 확인
            {
                nextSampleTime = Time.unscaledTime + 0.8f; // 다음 검사 예약
                SampleGuardMovement(); // 경비 정체 검사
                SampleCitizenMovement(); // 시민 정체 검사
            }

            if (Time.unscaledTime >= nextCountTime) // 개체 집계 시각 확인
            {
                nextCountTime = Time.unscaledTime + 1f; // 다음 집계 예약
                RefreshCounts(); // 활성 개체 수 갱신
            }
        }

        public void ReportHotspot(string kind, Vector3 position, Object context) // 정체 지점 기록
        {
            stuckHotspots++; // 정체 수 증가
            Debug.LogWarning("[Day27] " + kind + " 정체 지점 · Position " + Format(position), context); // 좌표 출력
        }

        private void SampleGuardMovement() // 경비 이동량 검사
        {
            var guards = Map28RuntimeRegistry.ActiveGuards; // 활성 경비 조회
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 상태 조회

            foreach (MapWantedGuardAgent guard in guards) // 경비 순회
            {
                if (guard == null || guard.IsDead) // 사망·누락 제외
                {
                    continue; // 다음 경비
                }

                bool shouldMove = wanted != null && wanted.Stars > 0 && wanted.Player != null && PlanarDistance(guard.transform.position, wanted.Player.transform.position) > 5f; // 이동 필요 상태 추정
                TrackMovement(guardTracks, guard.GetInstanceID(), guard.transform.position, shouldMove, 0.35f, "Guard", guard); // 정체 추적
            }
        }

        private void SampleCitizenMovement() // 시민 도주 이동량 검사
        {
            var citizens = Map28RuntimeRegistry.ActiveCitizens; // 활성 시민 조회

            foreach (MapCitizenAgent citizen in citizens) // 시민 순회
            {
                if (citizen == null) // 누락 제외
                {
                    continue; // 다음 시민
                }

                bool shouldMove = citizen.State == MapCitizenState.Flee; // 도주 중인지 확인
                TrackMovement(citizenTracks, citizen.GetInstanceID(), citizen.transform.position, shouldMove, 0.25f, "Citizen", citizen); // 정체 추적
            }
        }

        private void TrackMovement(Dictionary<int, MovementTrack> tracks, int id, Vector3 position, bool shouldMove, float minimumDistance, string kind, Object context) // 공통 정체 판정
        {
            if (!tracks.TryGetValue(id, out MovementTrack track)) // 첫 검사 확인
            {
                track = new MovementTrack { Position = position }; // 초기 기록 생성
                tracks.Add(id, track); // 기록 등록
                return; // 다음 검사부터 판정
            }

            float moved = PlanarDistance(track.Position, position); // 이동량 계산
            track.Position = position; // 현재 위치 저장

            if (!shouldMove || moved >= minimumDistance) // 정상 이동 확인
            {
                track.StuckSamples = 0; // 정체 횟수 초기화
                return; // 처리 종료
            }

            track.StuckSamples++; // 정체 횟수 증가
            if (track.StuckSamples < 3 || Time.unscaledTime < track.NextLogAt) // 약 2.4초 반복 정체 확인
            {
                return; // 아직 경고하지 않음
            }

            track.NextLogAt = Time.unscaledTime + 8f; // 반복 로그 제한
            track.StuckSamples = 0; // 검사 초기화
            ReportHotspot(kind, position, context); // 정체 위치 기록
        }

        private void RefreshCounts() // 활성 개체 수 집계
        {
            activeCitizens = Map28RuntimeRegistry.ActiveCitizens.Count; // 시민 수
            activeVehicles = Map28RuntimeRegistry.ActiveVehicles.Count; // 차량 수
            var guards = Map28RuntimeRegistry.ActiveGuards; // 경비 조회
            var drones = Map28RuntimeRegistry.ActiveDrones; // 드론 조회
            activeGuards = 0; // 경비 집계 초기화
            activeHeavy = 0; // E-03 집계 초기화
            activeDrones = 0; // E-04 집계 초기화

            foreach (MapWantedGuardAgent guard in guards) // 경비 순회
            {
                if (guard == null || guard.IsDead) // 사망 제외
                {
                    continue; // 다음 경비
                }

                activeGuards++; // 경비 증가
                if (guard.UnitKind == MapWantedUnitKind.Heavy) activeHeavy++; // E-03 증가
            }

            foreach (Map25SurveillanceDrone drone in drones) // 드론 순회
            {
                if (drone != null && !drone.IsDead) activeDrones++; // 생존 드론 증가
            }
        }

        private void OnGUI() // QA HUD 출력
        {
            if (!showHud || Map30UITheme.HideGameplayHUD) // F10 숨김 또는 Tab 전체 임무 창 확인
            {
                return; // QA HUD 출력 생략
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 수배 상태 조회
            bool pending = Map26CrimeReportSystem.Instance != null && Map26CrimeReportSystem.Instance.HasPendingReport; // 신고 진행 여부
            int stars = wanted != null ? wanted.Stars : 0; // 별 단계 조회
            Rect rect = new Rect(14f, 92f, 180f, 150f); // HP 아래 개발 상태 패널
            Map30UITheme.DrawPanel(rect); // 공통 파란색 패널 출력

            GUIStyle header = new GUIStyle(GUI.skin.label); // QA 제목 스타일
            header.fontSize = 10; // 작은 제목
            header.fontStyle = FontStyle.Bold; // 제목 강조
            header.normal.textColor = Map30UITheme.Cyan; // 청록 제목

            GUIStyle body = new GUIStyle(GUI.skin.label); // QA 수치 스타일
            body.fontSize = 10; // 밀도 높은 개발 정보
            body.normal.textColor = Map30UITheme.Text; // 밝은 청백색

            GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, 154f, 18f), "SYSTEM QA  [F10]", header); // QA 헤더
            string line1 = "FPS " + smoothedFps.ToString("0") + "   CIT " + activeCitizens + "   VEH " + activeVehicles; // 성능 요약
            string line2 = "GUARD " + activeGuards + "   E03 " + activeHeavy + "   E04 " + activeDrones; // 병력 요약
            string line3 = "REPORT " + (pending ? "ON" : "OFF") + "   WANTED " + stars + "   STUCK " + stuckHotspots; // 사건·수배 요약
            GUI.Label(new Rect(rect.x + 10f, rect.y + 31f, 158f, 21f), line1, body); // 성능 행
            GUI.Label(new Rect(rect.x + 10f, rect.y + 57f, 158f, 21f), line2, body); // 병력 행
            GUI.Label(new Rect(rect.x + 10f, rect.y + 83f, 158f, 21f), line3, body); // 수배 행
            Map30UITheme.DrawDivider(new Rect(rect.x + 10f, rect.y + 113f, 158f, 1f)); // 하단 구분선
            GUI.Label(new Rect(rect.x + 10f, rect.y + 121f, 158f, 18f), "CITY RUNTIME MONITOR", header); // 하단 시스템 문구
        }

        
        private static float PlanarDistance(Vector3 first, Vector3 second) // XZ 거리 계산
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)); // 수평 거리 반환
        }

        private static string Format(Vector3 position) // 좌표 문자열 생성
        {
            return "(" + position.x.ToString("0.0") + ", " + position.y.ToString("0.0") + ", " + position.z.ToString("0.0") + ")"; // 좌표 반환
        }
    }
}
