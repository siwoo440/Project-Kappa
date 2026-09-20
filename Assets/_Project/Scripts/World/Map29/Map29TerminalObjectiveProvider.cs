using ProjectK.Day16; // 본편 월드와 주요 장소 참조
using UnityEngine; // 런타임 목표 위치 처리
using UnityEngine.SceneManagement; // 씬 전환 목표 참조 복구

namespace ProjectK.Day29 // 29일차 플레이어 단말기 이름 공간
{
    [DisallowMultipleComponent] // 목표 Provider 중복 방지
    public sealed class Map29TerminalObjectiveProvider : MonoBehaviour // 미션 시스템 전 단말기 목표 정보를 제공하고 이후 공통 미션 시스템의 연결 지점이 되는 Provider
    {
        private static Map29TerminalObjectiveProvider instance; // 현재 Provider 인스턴스
        private MapWorldRoot world; // 현재 본편 월드 참조
        private Transform targetTransform; // Transform 기반 목표 위치
        private Vector3 targetWorldPosition; // 좌표 기반 목표 위치
        private bool useWorldPosition; // 직접 좌표 목표 사용 여부
        private bool externalObjective; // 외부 시스템에서 설정한 실제 목표 여부
        private bool demoObjective; // 미션 시스템 전 임시 추적 목표 여부
        private bool demoFallbackEnabled = true; // 실제 미션 시스템 연결 전까지만 임시 목표 자동 생성 허용
        private string missionId = string.Empty; // 현재 미션 또는 추적 ID
        private string missionTitle = "현재 의뢰 없음"; // 현재 미션 이름
        private string objectiveText = "새로운 의뢰를 기다리는 중입니다."; // 현재 목표 문구
        private string targetLabel = "---"; // 현재 목표 장소 이름
        private Map29TerminalObjectiveStatus status = Map29TerminalObjectiveStatus.Idle; // 현재 목표 상태
        private float nextResolveTime; // 다음 월드 참조 복구 시각

        public static Map29TerminalObjectiveProvider Instance => instance; // 현재 Provider 조회
        public MapWorldRoot World => world; // HUD용 현재 월드 조회
        public bool HasExternalObjective => externalObjective; // 실제 미션 목표 연결 여부 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 정적 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 Provider 참조 제거
        }

        private void Awake() // 단일 Provider 등록
        {
            if (instance != null && instance != this) // 다른 Provider 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 Provider 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void OnEnable() // 씬 전환 참조 복구 연결
        {
            SceneManager.activeSceneChanged += HandleSceneChanged; // 활성 씬 변경 감지
        }

        private void OnDisable() // 씬 전환 이벤트 해제
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged; // 정적 이벤트 참조 해제
        }

        private void Start() // 첫 본편 월드와 임시 추적 목표 준비
        {
            ResolveWorld(); // 현재 MapWorldRoot 연결
            EnsureDemoObjective(); // 실제 미션이 없으면 겹길 시장 시험 추적 연결
        }

        private void Update() // 월드·목표 참조 복구와 임시 추적 완료 처리
        {
            if (world == null || world.Player == null) // 본편 월드 또는 플레이어 누락 확인
            {
                if (Time.unscaledTime >= nextResolveTime) // 재검색 시각 확인
                {
                    nextResolveTime = Time.unscaledTime + 0.75f; // 다음 검색 예약
                    ResolveWorld(); // 현재 월드 재검색
                    EnsureDemoObjective(); // 월드 준비 후 임시 추적 연결
                }

                return; // 목표 거리 처리 중단
            }

            if (!externalObjective && !HasTargetPosition()) // 실제 목표가 없고 임시 목표도 없는지 확인
            {
                EnsureDemoObjective(); // 다시 임시 추적 목표 연결
            }

            if (demoObjective && status == Map29TerminalObjectiveStatus.Tracking && HasTargetPosition()) // 임시 추적 목표 진행 상태 확인
            {
                float distance = Vector3.Distance(world.Player.transform.position, CurrentTargetPosition()); // 플레이어와 임시 목표 거리 계산

                if (distance <= 9f) // 임시 목표 도착 거리 확인
                {
                    status = Map29TerminalObjectiveStatus.Completed; // 임시 추적 완료 표시
                    objectiveText = "목표 지점에 도착했습니다."; // 완료 문구 표시
                }
            }
        }

        private void HandleSceneChanged(Scene previous, Scene current) // 씬 전환 뒤 월드와 임시 목표 참조 초기화
        {
            world = null; // 이전 씬 월드 참조 제거
            nextResolveTime = 0f; // 즉시 새 월드 검색 허용

            if (demoObjective && !externalObjective) // 임시 목표만 사용 중인지 확인
            {
                targetTransform = null; // 이전 씬 목표 Transform 제거
                useWorldPosition = false; // 이전 좌표 목표 해제
                demoObjective = false; // 새 씬에서 임시 목표 재설정
                status = Map29TerminalObjectiveStatus.Idle; // 임시 상태 초기화
            }
        }

        private void ResolveWorld() // 현재 본편 월드 검색
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 본편 월드 연결
        }

        private void EnsureDemoObjective() // 공통 미션 시스템 전 HUD 거리 기능을 확인할 임시 추적 목표 생성
        {
            if (!demoFallbackEnabled || externalObjective || demoObjective || world == null || world.Places == null || world.Places.Length == 0) // 임시 추적 허용·실제 목표·월드 자료 확인
            {
                return; // 임시 목표 설정 생략
            }

            MapPoint selected = null; // 임시 추적할 장소 초기화

            for (int i = 0; i < world.Places.Length; i++) // 주요 도시 장소 순회
            {
                MapPoint place = world.Places[i]; // 현재 장소 조회
                if (place == null || place.Arrival == null) // 유효 도착점 확인
                {
                    continue; // 다음 장소 처리
                }

                if (place.PlaceId == "GYEOPGIL_PLAZA") // 겹길 시장을 HUD 시험 추적 지점으로 우선 선택
                {
                    selected = place; // 겹길 시장 저장
                    break; // 검색 종료
                }

                if (selected == null && place.PlaceId != "LIN_HOME") // 린 거점이 아닌 첫 장소 대체 후보 확인
                {
                    selected = place; // 대체 시험 장소 저장
                }
            }

            if (selected == null || selected.Arrival == null) // 임시 목표 후보 존재 확인
            {
                return; // HUD는 대기 상태 유지
            }

            missionId = "NAV-LINK"; // 미션 이전 추적 기능 ID
            missionTitle = "도시 경로 추적"; // 임시 단말기 제목
            objectiveText = selected.DisplayName + "으로 이동하십시오."; // 임시 목표 문구
            targetLabel = selected.DisplayName; // 목표 장소 이름 저장
            targetTransform = selected.Arrival; // 실제 도착점 Transform 연결
            useWorldPosition = false; // Transform 목표 방식 사용
            status = Map29TerminalObjectiveStatus.Tracking; // 추적 상태 적용
            demoObjective = true; // 임시 목표 표시
        }

        public static void SetObjective(string newMissionId, string newMissionTitle, string newObjectiveText, Transform target, string newTargetLabel = null) // 이후 MissionManager가 호출할 Transform 기반 목표 연결 API
        {
            EnsureProvider(); // Provider 존재 보장
            instance.externalObjective = true; // 실제 목표 사용 표시
            instance.demoObjective = false; // 임시 추적 해제
            instance.demoFallbackEnabled = false; // 실제 미션 시스템 연결 이후 시험 목표 자동 복귀 중지
            instance.missionId = SafeText(newMissionId, "MISSION"); // 미션 ID 저장
            instance.missionTitle = SafeText(newMissionTitle, "현재 임무"); // 미션 제목 저장
            instance.objectiveText = SafeText(newObjectiveText, "목표를 확인하십시오."); // 목표 문구 저장
            instance.targetLabel = SafeText(newTargetLabel, target != null ? target.name : "---"); // 목표 라벨 저장
            instance.targetTransform = target; // 목표 Transform 저장
            instance.useWorldPosition = false; // Transform 목표 사용
            instance.status = Map29TerminalObjectiveStatus.Tracking; // 추적 상태 적용
        }

        public static void SetWorldObjective(string newMissionId, string newMissionTitle, string newObjectiveText, Vector3 targetPosition, string newTargetLabel = null) // Transform 없는 좌표 기반 목표 연결 API
        {
            EnsureProvider(); // Provider 존재 보장
            instance.externalObjective = true; // 실제 목표 사용 표시
            instance.demoObjective = false; // 임시 추적 해제
            instance.demoFallbackEnabled = false; // 실제 미션 시스템 연결 이후 시험 목표 자동 복귀 중지
            instance.missionId = SafeText(newMissionId, "MISSION"); // 미션 ID 저장
            instance.missionTitle = SafeText(newMissionTitle, "현재 임무"); // 미션 제목 저장
            instance.objectiveText = SafeText(newObjectiveText, "목표를 확인하십시오."); // 목표 문구 저장
            instance.targetLabel = SafeText(newTargetLabel, "목표 지점"); // 목표 라벨 저장
            instance.targetTransform = null; // Transform 목표 해제
            instance.targetWorldPosition = targetPosition; // 직접 목표 좌표 저장
            instance.useWorldPosition = true; // 좌표 목표 사용
            instance.status = Map29TerminalObjectiveStatus.Tracking; // 추적 상태 적용
        }

        public static void SetStatus(Map29TerminalObjectiveStatus newStatus, string message = null) // 이후 미션 시스템에서 완료·실패 상태 갱신 API
        {
            if (instance == null) // Provider 존재 확인
            {
                return; // 상태 갱신 생략
            }

            instance.status = newStatus; // 새 상태 저장

            if (!string.IsNullOrWhiteSpace(message)) // 교체 목표 문구 존재 확인
            {
                instance.objectiveText = message; // HUD 목표 문구 갱신
            }
        }

        public static void ClearObjective() // 실제 미션 종료 후 단말기 대기 상태 복귀 API
        {
            EnsureProvider(); // Provider 존재 보장
            instance.externalObjective = false; // 실제 목표 연결 해제
            instance.demoObjective = false; // 기존 임시 목표 해제
            instance.targetTransform = null; // Transform 목표 제거
            instance.useWorldPosition = false; // 좌표 목표 해제
            instance.missionId = string.Empty; // 미션 ID 초기화
            instance.missionTitle = "현재 의뢰 없음"; // 대기 제목 복구
            instance.objectiveText = "새로운 의뢰를 기다리는 중입니다."; // 대기 문구 복구
            instance.targetLabel = "---"; // 목표 장소 초기화
            instance.status = Map29TerminalObjectiveStatus.Idle; // 대기 상태 적용
            if (instance.demoFallbackEnabled) // 아직 실제 미션 시스템이 연결되지 않은 개발 단계 확인
            {
                instance.EnsureDemoObjective(); // 개발 단계에서만 시험 추적 목표 재연결
            }
        }

        public Map29TerminalObjectiveSnapshot Snapshot() // HUD가 사용할 현재 목표 복사본 반환
        {
            bool hasTarget = HasTargetPosition(); // 현재 목표 위치 존재 여부 조회
            Vector3 position = hasTarget ? CurrentTargetPosition() : Vector3.zero; // 현재 목표 위치 계산
            return new Map29TerminalObjectiveSnapshot(missionId, missionTitle, objectiveText, targetLabel, position, hasTarget, status, demoObjective); // 불변 복사본 반환
        }

        private bool HasTargetPosition() // 현재 추적할 목표 위치 존재 여부 확인
        {
            return useWorldPosition || targetTransform != null; // 직접 좌표 또는 Transform 목표 여부 반환
        }

        private Vector3 CurrentTargetPosition() // 현재 목표 위치 반환
        {
            return useWorldPosition ? targetWorldPosition : targetTransform != null ? targetTransform.position : Vector3.zero; // 목표 방식에 맞는 좌표 반환
        }

        private static string SafeText(string value, string fallback) // HUD 빈 문자열 방지
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value; // 유효 문자열 또는 대체 문구 반환
        }

        private static void EnsureProvider() // 외부 API 호출 시 Provider 자동 생성
        {
            if (instance != null) // 기존 Provider 확인
            {
                return; // 생성 생략
            }

            GameObject owner = new GameObject("[Day29] Terminal Objective Provider"); // Provider 전용 오브젝트 생성
            instance = owner.AddComponent<Map29TerminalObjectiveProvider>(); // Provider 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
