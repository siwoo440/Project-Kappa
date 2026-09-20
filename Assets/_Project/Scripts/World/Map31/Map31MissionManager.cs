using System.Collections.Generic; // 임무 정의·런타임 목표 목록
using ProjectK.Day16; // 본편 MapPoint와 월드 참조
using ProjectK.Day29; // 왼쪽 상단 단말기 목표 Provider
using ProjectK.Day30; // Tab 전체 임무 Journal 연동
using UnityEngine; // 런타임 미션 진행과 월드 오브젝트 생성
using UnityEngine.Rendering; // 런타임 단말기 재질 설정
using UnityEngine.SceneManagement; // 씬 전환 시 목표 참조 복구

using ProjectK.Day32; // Day32 체크포인트·실패·결과 화면 연동
namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    [DisallowMultipleComponent] // MissionManager 중복 방지
    public sealed class Map31MissionManager : MonoBehaviour // 임무 수락·목표 진행·완료와 Day29·Day30 UI를 연결하는 중앙 관리자
    {
        private static Map31MissionManager instance; // 현재 MissionManager 인스턴스
        private readonly Dictionary<string, Map31MissionDefinition> definitions = new Dictionary<string, Map31MissionDefinition>(); // 등록 임무 정의
        private readonly Dictionary<string, Map31MissionRuntimeState> states = new Dictionary<string, Map31MissionRuntimeState>(); // 임무별 런타임 상태
        private readonly Dictionary<string, Transform> runtimeTargets = new Dictionary<string, Transform>(); // 런타임 생성 목표 Transform
        private MapWorldRoot world; // 현재 본편 월드
        private Map31MissionInventory inventory; // 임무 물품 슬롯
        private Map31MissionRuntimeState activeMission; // 현재 진행 중인 단일 임무
        private GameObject runtimeRoot; // 현재 씬 임무 오브젝트 루트
        private Material terminalBodyMaterial; // 임무 단말기 본체 재질
        private Material terminalScreenMaterial; // 임무 단말기 화면 재질
        private float nextWorldResolveTime; // 다음 월드 검색 시각
        private float pendingTerminalSyncAt; // 목표 완료 후 다음 단말기 갱신 시각
        private float clearTerminalAt; // 임무 완료 후 단말기 대기 복귀 시각
        private bool pendingTerminalSync; // 다음 목표 단말기 표시 대기
        private bool pendingTerminalClear; // 임무 완료 단말기 정리 대기
        private bool journalRegistered; // Day30 Journal 실제 데이터 교체 완료 여부
        private int startFrame; // Journal Start 완료 대기용 생성 프레임
        private int worldInstanceId; // 현재 목표 오브젝트가 연결된 월드 ID

        public static Map31MissionManager Instance => instance; // 현재 MissionManager 조회
        public string ActiveMissionId => activeMission != null && activeMission.Definition != null ? activeMission.Definition.MissionId : string.Empty; // 현재 진행 임무 ID 조회
        public Map31MissionInventory Inventory => inventory; // 임무 물품 슬롯 조회
        public int ActiveObjectiveIndex => activeMission != null ? activeMission.ObjectiveIndex : 0; // Day32 체크포인트용 현재 목표 순번

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 MissionManager 참조 제거
        }

        private void Awake() // 단일 MissionManager 등록과 정의 구성
        {
            if (instance != null && instance != this) // 다른 MissionManager 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 MissionManager 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            inventory = GetComponent<Map31MissionInventory>(); // 같은 오브젝트 임무 인벤토리 조회

            if (inventory == null) // Bootstrap 순서로 인벤토리 누락 확인
            {
                inventory = gameObject.AddComponent<Map31MissionInventory>(); // 임무 물품 슬롯 자동 추가
            }

            startFrame = Time.frameCount; // Journal Start가 먼저 실행될 수 있도록 현재 프레임 저장
            RegisterPrototypeDefinitions(); // M-01 공통 MissionData 프로토타입 등록
        }

        private void OnEnable() // 씬 전환 참조 복구 연결
        {
            SceneManager.activeSceneChanged += HandleSceneChanged; // 활성 씬 변경 감지
        }

        private void OnDisable() // 씬 전환 이벤트 해제
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged; // 정적 이벤트 참조 해제
        }

        private void Start() // 첫 본편 월드 탐색
        {
            ResolveWorldAndTargets(); // Map과 임무 단말기 준비
        }

        private void Update() // 월드 복구·목표 도착·UI 전환 처리
        {
            if (!journalRegistered && Time.frameCount > startFrame) // Day30 Journal Start 이후 실제 M-01 데이터 등록 시점 확인
            {
                TryRegisterJournalData(); // 개발용 Seed를 실제 MissionManager 데이터로 교체
            }

            if (world == null || world.Player == null || worldInstanceId != world.GetInstanceID()) // 씬 전환 또는 월드 미준비 확인
            {
                if (Time.unscaledTime >= nextWorldResolveTime) // 월드 재검색 간격 확인
                {
                    nextWorldResolveTime = Time.unscaledTime + 0.5f; // 다음 검색 예약
                    ResolveWorldAndTargets(); // 현재 월드와 런타임 목표 복구
                }
            }

            if (pendingTerminalSync && Time.unscaledTime >= pendingTerminalSyncAt) // 목표 완료 알림 뒤 다음 목표 표시 시각 확인
            {
                pendingTerminalSync = false; // 대기 상태 해제
                SyncCurrentObjectiveToUI(); // 새 현재 목표를 단말기와 Journal에 반영
            }

            if (pendingTerminalClear && Time.unscaledTime >= clearTerminalAt) // 임무 완료 표시 종료 시각 확인
            {
                pendingTerminalClear = false; // 정리 대기 해제
                Map29TerminalObjectiveProvider.ClearObjective(); // 왼쪽 상단 단말기를 대기 상태로 복귀
            }

            if (activeMission == null || activeMission.Status != Map31MissionRuntimeStatus.Active || Map30MissionWindow.IsOpen) // 진행 중 임무와 메뉴 상태 확인
            {
                return; // 목표 자동 판정 생략
            }

            Map31MissionObjectiveData objective = activeMission.CurrentObjective; // 현재 목표 조회
            if (objective == null) // 잘못된 목표 상태 확인
            {
                return; // 진행 중단
            }

            if (objective.Type == Map31MissionObjectiveType.Reach || objective.Type == Map31MissionObjectiveType.Escape) // 위치 도착형 목표 확인
            {
                Transform target = ResolveObjectiveTarget(objective); // 현재 목표 Transform 조회
                if (target == null || world == null || world.Player == null) // 목표와 플레이어 참조 확인
                {
                    return; // 도착 판정 생략
                }

                Vector3 delta = world.Player.transform.position - target.position; // 목표와 플레이어 거리 벡터 계산
                if (delta.sqrMagnitude <= objective.CompletionRadius * objective.CompletionRadius) // 완료 반경 진입 확인
                {
                    CompleteCurrentObjective(); // 현재 위치 목표 완료
                }
            }
            else if (objective.Type == Map31MissionObjectiveType.Acquire && !string.IsNullOrEmpty(objective.RequiredItemId) && inventory.Has(objective.RequiredItemId)) // 물품 획득형 자동 완료 확인
            {
                CompleteCurrentObjective(); // 이미 보유한 물품 목표 완료
            }
        }

        public bool SupportsMission(string missionId) // MissionManager가 실제 진행을 지원하는 임무인지 확인
        {
            return !string.IsNullOrWhiteSpace(missionId) && definitions.ContainsKey(missionId); // 등록 정의 존재 여부 반환
        }

        public bool CanAcceptMission(string missionId) // 현재 선택 임무 수락 가능 여부
        {
            if (!states.TryGetValue(missionId, out Map31MissionRuntimeState state) || state == null) // 등록 런타임 상태 확인
            {
                return false; // 미지원 임무
            }

            if (state.Status != Map31MissionRuntimeStatus.Available) // 이미 진행·완료·실패 상태 확인
            {
                return false; // 신규 수락 불가
            }

            return activeMission == null || activeMission.Status != Map31MissionRuntimeStatus.Active; // 현재 다른 임무 진행 여부 반환
        }

        public bool AcceptMission(string missionId) // Tab 임무 창에서 선택한 임무 수락
        {
            if (!CanAcceptMission(missionId)) // 수락 가능 상태 확인
            {
                Map31MissionToastHUD.Show("MISSION LINK", "현재 다른 임무가 진행 중이거나 수락할 수 없습니다.", 1.8f, false, true); // 수락 실패 안내
                return false; // 수락 실패
            }

            if (world == null || world.Player == null || runtimeTargets.Count == 0) // 현재 Map 목표 준비 확인
            {
                ResolveWorldAndTargets(); // 즉시 월드·목표 복구 시도

                if (world == null || world.Player == null || runtimeTargets.Count == 0) // 복구 결과 재확인
                {
                    Map31MissionToastHUD.Show("MISSION LINK", "임무 목표 위치를 아직 준비하지 못했습니다.", 1.8f, false, true); // 준비 실패 안내
                    return false; // 수락 중단
                }
            }

            Map31MissionRuntimeState state = states[missionId]; // 수락 임무 상태 조회
            state.Status = Map31MissionRuntimeStatus.Active; // 진행 중 상태 적용
            state.ObjectiveIndex = 0; // 첫 목표부터 시작
            activeMission = state; // 현재 진행 임무 연결
            inventory.ClearAll(); // 새 프로토타입 임무 물품 상태 초기화
            pendingTerminalClear = false; // 이전 완료 단말기 정리 취소

            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Tracking); // Tab 목록 상태를 추적 중으로 변경
            Map30MissionJournal.SetCurrentObjective(missionId, 0); // 첫 목표 순번 반영
            Map30MissionJournal.Instance?.SelectMission(missionId); // 수락한 임무를 Tab 상세 선택으로 유지
            SyncCurrentObjectiveToUI(); // 왼쪽 상단 단말기에 첫 목표 연결
            Map32MissionCheckpointSystem.Capture("임무 시작"); // Day32 최초 안전 체크포인트 저장
            Map31MissionToastHUD.Show("MISSION ACCEPTED", state.Definition.Title, 2.0f); // 수락 알림
            return true; // 수락 성공
        }

        public bool RestartMission(string missionId) // 실패 임무 처음부터 재시도
        {
            if (!states.TryGetValue(missionId, out Map31MissionRuntimeState state) || state == null) // 실제 임무 상태 확인
            {
                return false; // 재시도 불가
            }

            if (activeMission != null && activeMission.Status == Map31MissionRuntimeStatus.Active && activeMission != state) // 다른 임무 진행 확인
            {
                return false; // 다른 임무 중 재시도 차단
            }

            state.Status = Map31MissionRuntimeStatus.Available; // 다시 수락 가능한 상태로 복구
            state.ObjectiveIndex = 0; // 첫 목표 순번 복구
            activeMission = null; // 기존 진행 참조 해제
            inventory.ClearAll(); // 임무 물품 초기화
            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Available); // Tab 상태 복구
            Map30MissionJournal.SetCurrentObjective(missionId, 0); // 첫 목표 표시 복구
            return AcceptMission(missionId); // 즉시 재수락
        }

        public bool IsCurrentObjective(string missionId, string objectiveId) // 현재 활성 목표 ID 확인
        {
            Map31MissionObjectiveData current = activeMission != null ? activeMission.CurrentObjective : null; // 현재 목표 조회
            return activeMission != null &&
                   activeMission.Status == Map31MissionRuntimeStatus.Active &&
                   activeMission.Definition != null &&
                   activeMission.Definition.MissionId == missionId &&
                   current != null &&
                   current.ObjectiveId == objectiveId; // 임무·목표 모두 일치 여부 반환
        }

        public bool CanInteractObjective(string missionId, string objectiveId, string requiredItemId) // IInteractable 표시 가능 여부
        {
            return IsCurrentObjective(missionId, objectiveId); // 필요 물품 부족도 F 안내를 보여 주고 실제 상호작용에서 설명
        }

        public bool TryInteractObjective(string missionId, string objectiveId, GameObject interactor, string grantItemId, string requiredItemId, string deliverItemId) // F 상호작용 목표 처리
        {
            if (!IsCurrentObjective(missionId, objectiveId)) // 현재 목표 여부 확인
            {
                return false; // 다른 임무 단계 상호작용 무시
            }

            if (world == null || interactor == null || interactor != world.Player) // 실제 플레이어 상호작용 확인
            {
                return false; // 잘못된 사용자 차단
            }

            if (!string.IsNullOrEmpty(requiredItemId) && !inventory.Has(requiredItemId)) // 인계·상호작용 필수 물품 확인
            {
                Map31MissionToastHUD.Show("MISSION ITEM", "필요한 임무 물품이 없습니다.", 1.6f, false, true); // 물품 부족 안내
                return false; // 목표 완료 차단
            }

            if (!string.IsNullOrEmpty(grantItemId) && inventory.Add(grantItemId)) // 이번 상호작용에서 물품 획득 확인
            {
                Map31MissionToastHUD.Show("MISSION ITEM ACQUIRED", DisplayItemName(grantItemId), 1.8f); // 임무 물품 획득 알림
            }

            if (!string.IsNullOrEmpty(deliverItemId) && !inventory.Deliver(deliverItemId)) // 인계 목표 물품 처리
            {
                Map31MissionToastHUD.Show("MISSION ITEM", "인계할 임무 물품이 없습니다.", 1.6f, false, true); // 인계 실패 안내
                return false; // 목표 완료 차단
            }

            CompleteCurrentObjective(); // 상호작용·인계 목표 완료
            return true; // 처리 성공
        }

        public bool RestoreFromCheckpoint(string missionId, int objectiveIndex) // Day32 체크포인트 목표 단계 복원
        {
            if (!states.TryGetValue(missionId, out Map31MissionRuntimeState state) || state == null || state.Definition == null || state.Definition.Objectives.Count == 0) // 저장된 임무 정의 확인
            {
                return false; // 복원 실패
            }

            state.Status = Map31MissionRuntimeStatus.Active; // 임무 진행 상태 복구
            state.ObjectiveIndex = Mathf.Clamp(objectiveIndex, 0, state.Definition.Objectives.Count - 1); // 저장 목표 순번 복구
            activeMission = state; // 현재 활성 임무 다시 연결
            pendingTerminalSync = false; // 이전 목표 갱신 예약 제거
            pendingTerminalClear = false; // 이전 완료 정리 예약 제거
            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Tracking); // Tab 목록 추적 상태 복구
            Map30MissionJournal.SetCurrentObjective(missionId, state.ObjectiveIndex); // Tab 목표 강조 복구
            Map30MissionJournal.Instance?.SelectMission(missionId); // 재시도 임무 선택 유지
            SyncCurrentObjectiveToUI(); // Day29 단말기 현재 목표 재연결
            return true; // 복원 성공
        }

        public void AbandonMission(string missionId) // Day32 실패 화면에서 임무 포기·초기화
        {
            if (!states.TryGetValue(missionId, out Map31MissionRuntimeState state) || state == null) // 임무 상태 존재 확인
            {
                return; // 초기화 생략
            }

            state.Status = Map31MissionRuntimeStatus.Available; // 다시 수락 가능한 상태로 복구
            state.ObjectiveIndex = 0; // 첫 목표부터 다시 시작하도록 초기화

            if (activeMission == state) // 현재 활성 임무와 같은지 확인
            {
                activeMission = null; // 진행 임무 연결 해제
            }

            inventory.ClearAll(); // 임무 물품 전체 초기화
            pendingTerminalSync = false; // 목표 전환 예약 제거
            pendingTerminalClear = false; // 완료 후 정리 예약 제거
            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Available); // Tab 목록 보유 상태 복구
            Map30MissionJournal.SetCurrentObjective(missionId, 0); // 첫 목표 강조 복구
            Map29TerminalObjectiveProvider.ClearObjective(); // 단말기 현재 목표 해제
        }

        public bool NotifyItemAcquired(string itemId) // 이후 아이템 시스템에서 임무 물품 획득 이벤트 전달
        {
            if (string.IsNullOrWhiteSpace(itemId)) // 유효 물품 ID 확인
            {
                return false; // 처리 실패
            }

            bool added = inventory.Add(itemId); // 임무 물품 슬롯에 획득 기록
            Map31MissionObjectiveData objective = activeMission != null ? activeMission.CurrentObjective : null; // 현재 목표 조회

            if (objective != null &&
                objective.Type == Map31MissionObjectiveType.Acquire &&
                objective.RequiredItemId == itemId) // 현재 획득 목표와 물품 ID 일치 확인
            {
                Map31MissionToastHUD.Show("MISSION ITEM ACQUIRED", DisplayItemName(itemId), 1.8f); // 획득 알림
                CompleteCurrentObjective(); // 획득 목표 완료
            }

            return added; // 신규 획득 여부 반환
        }

        public bool NotifyTargetEliminated(string targetKey) // 이후 적 사망 시스템에서 제거 목표 이벤트 전달
        {
            Map31MissionObjectiveData objective = activeMission != null ? activeMission.CurrentObjective : null; // 현재 목표 조회
            if (objective == null || objective.Type != Map31MissionObjectiveType.Eliminate) // 제거 목표 상태 확인
            {
                return false; // 현재 제거 목표 아님
            }

            bool matches = objective.RuntimeTargetKey == targetKey || objective.TargetPlaceId == targetKey; // 목표 키 일치 확인
            if (!matches) // 다른 표적 제거 확인
            {
                return false; // 진행 변화 없음
            }

            CompleteCurrentObjective(); // 제거 목표 완료
            return true; // 이벤트 처리 성공
        }

        public void FailMission(string reason) // 이후 실패 조건 시스템에서 호출할 공통 실패 API
        {
            if (activeMission == null || activeMission.Status != Map31MissionRuntimeStatus.Active || activeMission.Definition == null) // 진행 중 임무 확인
            {
                return; // 실패 처리 생략
            }

            string missionId = activeMission.Definition.MissionId; // 현재 임무 ID 저장
            activeMission.Status = Map31MissionRuntimeStatus.Failed; // 런타임 실패 상태 적용
            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Failed); // Tab 목록 실패 상태 반영
            Map29TerminalObjectiveProvider.SetStatus(Map29TerminalObjectiveStatus.Failed, string.IsNullOrWhiteSpace(reason) ? "임무 실패" : reason); // 단말기 실패 표시
            Map31MissionToastHUD.Show("MISSION FAILED", string.IsNullOrWhiteSpace(reason) ? activeMission.Definition.Title : reason, 2.2f, false, true); // 실패 알림
            Map32MissionResultScreen.ShowFailure(missionId, activeMission.Definition.Title, string.IsNullOrWhiteSpace(reason) ? "임무 실패" : reason, Map32MissionCheckpointSystem.Instance != null ? Map32MissionCheckpointSystem.Instance.CurrentLabel : "체크포인트 없음"); // Day32 실패·재시도 화면 표시
            activeMission = null; // 현재 진행 임무 해제
        }

        private void CompleteCurrentObjective() // 현재 목표 완료와 다음 단계 예약
        {
            if (activeMission == null || activeMission.Status != Map31MissionRuntimeStatus.Active || activeMission.Definition == null) // 진행 상태 확인
            {
                return; // 완료 처리 생략
            }

            Map31MissionObjectiveData objective = activeMission.CurrentObjective; // 완료 대상 목표 조회
            if (objective == null) // 목표 존재 확인
            {
                return; // 완료 처리 중단
            }

            if (!string.IsNullOrEmpty(objective.GrantItemId) && inventory.Add(objective.GrantItemId)) // 목표 자체 지급 물품 처리
            {
                Map31MissionToastHUD.Show("MISSION ITEM ACQUIRED", DisplayItemName(objective.GrantItemId), 1.8f); // 획득 알림
            }

            activeMission.ObjectiveIndex++; // 다음 목표 순번으로 이동

            if (activeMission.ObjectiveIndex >= activeMission.Definition.Objectives.Count) // 마지막 필수 목표 완료 확인
            {
                CompleteMission(); // 임무 최종 완료
                return; // 다음 목표 없음
            }

            Map30MissionJournal.SetCurrentObjective(activeMission.Definition.MissionId, activeMission.ObjectiveIndex); // Tab 목표 강조를 다음 단계로 이동
            string checkpointLabel = activeMission.ObjectiveIndex == 1 ? "겹길 시장 진입" :
                                     activeMission.ObjectiveIndex == 2 ? "운송 기록 조사 완료" :
                                     activeMission.ObjectiveIndex == 3 ? "린 거점 복귀" :
                                     "목표 진행 체크포인트"; // 현재 M-01 진행 단계용 체크포인트 이름
            Map32MissionCheckpointSystem.Capture(checkpointLabel); // 목표·위치·임무 물품 상태 저장
            Map29TerminalObjectiveProvider.SetStatus(Map29TerminalObjectiveStatus.Completed, "목표 완료 · " + objective.Description); // 왼쪽 단말기에 짧은 완료 표시
            Map31MissionToastHUD.Show("OBJECTIVE COMPLETE", objective.Description, 1.3f, true); // 목표 완료 알림
            pendingTerminalSync = true; // 다음 목표 표시 대기 시작
            pendingTerminalSyncAt = Time.unscaledTime + 0.75f; // 완료 문구 읽을 짧은 시간 확보
        }

        private void CompleteMission() // 모든 필수 목표 완료
        {
            if (activeMission == null || activeMission.Definition == null) // 현재 임무 확인
            {
                return; // 완료 처리 생략
            }

            Map31MissionRuntimeState completed = activeMission; // 완료 임무 상태 보존
            completed.Status = Map31MissionRuntimeStatus.Completed; // 런타임 완료 상태 적용
            string missionId = completed.Definition.MissionId; // 완료 임무 ID 저장
            Map30MissionJournal.SetMissionStatus(missionId, Map30MissionStatus.Completed); // Tab 임무 완료 표시
            Map29TerminalObjectiveProvider.SetStatus(Map29TerminalObjectiveStatus.Completed, "임무 완료 · " + completed.Definition.Title); // 단말기 완료 표시
            Map31MissionToastHUD.Show("MISSION COMPLETE", completed.Definition.Title + "  /  " + completed.Definition.Reward, 2.6f, true); // 완료·보상 알림
            Map32MissionResultScreen.ShowSuccess(missionId, completed.Definition.Title, completed.Definition.Reward, completed.Definition.NextMissionId); // Day32 완료·보상 결과 화면 표시
            activeMission = null; // 진행 임무 해제
            pendingTerminalSync = false; // 다음 목표 갱신 취소
            pendingTerminalClear = true; // 완료 문구 이후 단말기 정리 예약
            clearTerminalAt = Time.unscaledTime + 2.4f; // 완료 표시 유지 시간 설정
        }

        private void SyncCurrentObjectiveToUI() // 현재 목표를 Day29 단말기와 Day30 Journal에 연결
        {
            if (activeMission == null || activeMission.Definition == null || activeMission.Status != Map31MissionRuntimeStatus.Active) // 진행 상태 확인
            {
                return; // 동기화 생략
            }

            Map31MissionObjectiveData objective = activeMission.CurrentObjective; // 현재 목표 조회
            if (objective == null) // 목표 존재 확인
            {
                return; // 동기화 중단
            }

            Transform target = ResolveObjectiveTarget(objective); // 실제 월드 목표 Transform 조회
            Map30MissionJournal.SetMissionStatus(activeMission.Definition.MissionId, Map30MissionStatus.Tracking); // Tab 상태 추적 중 유지
            Map30MissionJournal.SetCurrentObjective(activeMission.Definition.MissionId, activeMission.ObjectiveIndex); // Tab 현재 목표 순번 반영

            if (target != null) // 위치를 가진 목표 확인
            {
                Map29TerminalObjectiveProvider.SetObjective(activeMission.Definition.MissionId, activeMission.Definition.Title, objective.Description, target, objective.TargetLabel); // 단말기 거리·층·방향 추적 연결
            }
            else // 위치 정보가 없는 목표 처리
            {
                Vector3 fallback = world != null && world.Player != null ? world.Player.transform.position : Vector3.zero; // 현재 플레이어 위치를 임시 좌표로 사용
                Map29TerminalObjectiveProvider.SetWorldObjective(activeMission.Definition.MissionId, activeMission.Definition.Title, objective.Description, fallback, objective.TargetLabel); // 목표 문구 유지용 좌표 연결
            }

            Map31MissionToastHUD.Show("NEW OBJECTIVE", objective.Description, 1.7f); // 새 목표 알림
        }

        private Transform ResolveObjectiveTarget(Map31MissionObjectiveData objective) // 목표 정의를 실제 월드 Transform으로 변환
        {
            if (objective == null) // 목표 존재 확인
            {
                return null; // 대상 없음
            }

            if (!string.IsNullOrEmpty(objective.RuntimeTargetKey) && runtimeTargets.TryGetValue(objective.RuntimeTargetKey, out Transform runtimeTarget) && runtimeTarget != null) // 런타임 단말기 목표 확인
            {
                return runtimeTarget; // 런타임 목표 반환
            }

            if (!string.IsNullOrEmpty(objective.TargetPlaceId)) // 기존 MapPoint 목표 ID 확인
            {
                MapPoint place = FindPlace(objective.TargetPlaceId); // MapPoint 검색
                if (place != null && place.Arrival != null) // 안전 도착점 존재 확인
                {
                    return place.Arrival; // 기존 도시 지점 반환
                }
            }

            return null; // 목표 위치 미해결
        }

        private void RegisterPrototypeDefinitions() // 첫 실제 진행 임무 M-01 구성
        {
            Map31MissionDefinition m01 = ScriptableObject.CreateInstance<Map31MissionDefinition>(); // 런타임 M-01 MissionData 생성
            m01.name = "M-01_Runtime"; // 디버그 이름 지정
            m01.ConfigureRuntime(
                "M-01",
                "남겨진 주소 · 운송 기록",
                "배달 · 조사",
                "린",
                "겹길",
                "현재 의뢰와 과거의 운송 번호가 연결되는지 확인한다. 겹길 운송 기록을 조사해 백야 인증 조각을 확보하고 린의 작업실로 가져간다.",
                "스토리 진행 · 백야 인증 조각 기록 보존",
                "게임 시작 후 수락 가능",
                "M-02",
                Map30MissionCategory.Main,
                new[]
                {
                    "임무 포기",
                    "필수 임무 물품 진행 불능"
                },
                new[]
                {
                    new Map31MissionObjectiveData("REACH_GYEOPGIL", Map31MissionObjectiveType.Reach, "겹길 시장의 운송 기록 지점으로 이동하십시오.", "GYEOPGIL_PLAZA", string.Empty, "겹길 시장", 8f),
                    new Map31MissionObjectiveData("SCAN_ARCHIVE", Map31MissionObjectiveType.Investigate, "현장 단말기에서 과거 운송 기록을 조사하십시오.", string.Empty, "M01_ARCHIVE_TERMINAL", "운송 기록 단말기", 2.5f, "BAEKYA_TOKEN"),
                    new Map31MissionObjectiveData("RETURN_LIN", Map31MissionObjectiveType.Reach, "백야 인증 조각을 가지고 린의 옥상 작업실로 복귀하십시오.", "LIN_HOME", string.Empty, "린의 옥상 작업실", 8f, requiredItem: "BAEKYA_TOKEN"),
                    new Map31MissionObjectiveData("DELIVER_TOKEN", Map31MissionObjectiveType.Deliver, "린의 분석 단말기에 백야 인증 조각을 인계하십시오.", string.Empty, "M01_LIN_TERMINAL", "린 분석 단말기", 2.5f, requiredItem: "BAEKYA_TOKEN", deliverItem: "BAEKYA_TOKEN")
                }); // M-01 프로토타입 목표 구성

            definitions[m01.MissionId] = m01; // 임무 정의 등록
            states[m01.MissionId] = new Map31MissionRuntimeState
            {
                Definition = m01, // 원본 정의 연결
                Status = Map31MissionRuntimeStatus.Available, // 최초 수락 가능
                ObjectiveIndex = 0 // 첫 목표 순번
            }; // 런타임 상태 등록
        }

        private void TryRegisterJournalData() // Day30 개발용 M-01을 실제 진행 데이터로 교체
        {
            Map30MissionJournal journal = Map30MissionJournal.Instance; // 현재 보유 임무 Journal 조회
            if (journal == null || journal.Missions.Count == 0) // Day30 Start·Seed 완료 여부 확인
            {
                return; // 다음 프레임 재시도
            }

            if (!definitions.TryGetValue("M-01", out Map31MissionDefinition definition) || definition == null) // M-01 정의 확인
            {
                return; // 등록 불가
            }

            List<string> objectiveTexts = new List<string>(); // Tab 창에 표시할 목표 문구 목록
            for (int i = 0; i < definition.Objectives.Count; i++) // 모든 목표 순회
            {
                objectiveTexts.Add(definition.Objectives[i].Description); // 실제 목표 설명 복사
            }

            Map30MissionJournal.AddOrUpdateMission(
                definition.MissionId,
                definition.Title,
                definition.TypeLabel,
                definition.Client,
                definition.Region,
                definition.Summary,
                definition.Reward,
                definition.Category,
                Map30MissionStatus.Available,
                objectiveTexts,
                0); // M-01 개발 Seed를 실제 MissionManager 데이터로 교체

            journalRegistered = true; // Journal 실제 데이터 등록 완료
        }

        private void ResolveWorldAndTargets() // 현재 Map과 M-01 상호작용 단말기 준비
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 본편 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결

            if (world == null || world.Player == null || world.Places == null || world.Places.Length == 0) // 필수 Map 참조 확인
            {
                return; // 다음 검색까지 대기
            }

            if (worldInstanceId == world.GetInstanceID() && runtimeTargets.Count >= 2) // 현재 월드 목표 이미 준비 확인
            {
                return; // 중복 생성 방지
            }

            runtimeTargets.Clear(); // 이전 씬 목표 참조 초기화
            worldInstanceId = world.GetInstanceID(); // 현재 월드 ID 저장
            SetupMissionWorldObjects(); // 겹길·린 임무 단말기 자동 생성

            if (activeMission != null && activeMission.Status == Map31MissionRuntimeStatus.Active) // 씬 복구 중 진행 임무 확인
            {
                pendingTerminalSync = true; // 현재 목표 단말기 재연결 예약
                pendingTerminalSyncAt = Time.unscaledTime + 0.1f; // 새 목표 오브젝트 생성 직후 동기화
            }
        }

        private void SetupMissionWorldObjects() // M-01 조사·인계 단말기 런타임 생성
        {
            MapPoint gyeopgil = FindPlace("GYEOPGIL_PLAZA"); // 겹길 시장 조회
            MapPoint lin = FindPlace("LIN_HOME"); // 린 거점 조회

            if (gyeopgil == null || gyeopgil.Arrival == null || lin == null || lin.Arrival == null) // 두 핵심 MapPoint 확인
            {
                return; // 현재 씬에서는 자동 단말기 생성 중단
            }

            runtimeRoot = new GameObject("[Day31] Mission Runtime Objects"); // 현재 씬 미션 오브젝트 루트 생성
            runtimeRoot.transform.SetParent(world.transform, false); // 월드 씬과 함께 제거되도록 연결
            EnsureTerminalMaterials(); // 런타임 단말기 재질 준비

            Transform archiveTerminal = CreateMissionTerminal(
                "M01_ArchiveTerminal",
                gyeopgil.Arrival,
                new Vector3(1.8f, 0.82f, 0.5f),
                "M-01",
                "SCAN_ARCHIVE",
                "운송 기록 조사",
                "BAEKYA_TOKEN",
                null,
                null); // 겹길 운송 기록 조사 단말기 생성

            Transform linTerminal = CreateMissionTerminal(
                "M01_LinTerminal",
                lin.Arrival,
                new Vector3(1.8f, 0.82f, 0.5f),
                "M-01",
                "DELIVER_TOKEN",
                "백야 인증 조각 인계",
                null,
                "BAEKYA_TOKEN",
                "BAEKYA_TOKEN"); // 린 거점 인계 단말기 생성

            runtimeTargets["M01_ARCHIVE_TERMINAL"] = archiveTerminal; // 조사 목표 Transform 등록
            runtimeTargets["M01_LIN_TERMINAL"] = linTerminal; // 인계 목표 Transform 등록
        }

        private Transform CreateMissionTerminal(string objectName, Transform anchor, Vector3 offset, string missionId, string objectiveId, string interactionLabel, string grantItemId, string requiredItemId, string deliverItemId) // 기존 MapPoint 주변에 간단한 상호작용 단말기 생성
        {
            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube); // 충돌 가능한 단말기 본체 생성
            terminal.name = objectName; // 디버그 이름 적용
            terminal.transform.SetParent(runtimeRoot.transform, false); // 현재 미션 루트 연결
            terminal.transform.position = anchor.position + anchor.right * offset.x + Vector3.up * offset.y + anchor.forward * offset.z; // 도착점 옆 접근 가능한 위치 배치
            terminal.transform.rotation = anchor.rotation; // 장소 방향과 정렬
            terminal.transform.localScale = new Vector3(1.0f, 1.45f, 0.46f); // 세로형 도시 단말기 크기
            Renderer bodyRenderer = terminal.GetComponent<Renderer>(); // 본체 Renderer 조회
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = terminalBodyMaterial; // 남청색 본체 재질 적용

            Map31MissionInteractable interactable = terminal.AddComponent<Map31MissionInteractable>(); // 기존 F 상호작용 인터페이스 연결
            interactable.Configure(missionId, objectiveId, interactionLabel, grantItemId, requiredItemId, deliverItemId); // 현재 임무 목표 설정

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube); // 단말기 청록 화면 생성
            screen.name = "MissionScreen"; // 화면 이름 적용
            screen.transform.SetParent(terminal.transform, false); // 본체 자식 연결
            screen.transform.localPosition = new Vector3(0f, 0.12f, -0.52f); // 전면 화면 배치
            screen.transform.localScale = new Vector3(0.78f, 0.56f, 0.06f); // 얇은 화면 크기
            Renderer screenRenderer = screen.GetComponent<Renderer>(); // 화면 Renderer 조회
            if (screenRenderer != null) screenRenderer.sharedMaterial = terminalScreenMaterial; // 발광 청록 화면 재질 적용
            Collider screenCollider = screen.GetComponent<Collider>(); // 장식 화면 충돌체 조회
            if (screenCollider != null) Destroy(screenCollider); // 상호작용 충돌은 본체 하나만 사용

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상단 상태 라이트 생성
            top.name = "MissionBeacon"; // 상태 라이트 이름
            top.transform.SetParent(terminal.transform, false); // 본체 자식 연결
            top.transform.localPosition = new Vector3(0f, 0.58f, -0.52f); // 화면 위 배치
            top.transform.localScale = new Vector3(0.82f, 0.05f, 0.08f); // 얇은 청록 표시선
            Renderer topRenderer = top.GetComponent<Renderer>(); // 표시선 Renderer 조회
            if (topRenderer != null) topRenderer.sharedMaterial = terminalScreenMaterial; // 같은 발광 재질 사용
            Collider topCollider = top.GetComponent<Collider>(); // 표시선 충돌체 조회
            if (topCollider != null) Destroy(topCollider); // 장식 충돌 제거

            return terminal.transform; // 목표 추적용 Transform 반환
        }

        private void EnsureTerminalMaterials() // 임무 단말기 공용 런타임 재질 준비
        {
            if (terminalBodyMaterial != null && terminalScreenMaterial != null) // 기존 재질 확인
            {
                return; // 재생성 생략
            }

            Shader shader = Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard"); // 현재 렌더 파이프라인 기본 셰이더 검색
            if (shader == null) shader = Shader.Find("Standard"); // Built-in 대체
            if (shader == null) shader = Shader.Find("Unlit/Color"); // 최종 단색 대체

            terminalBodyMaterial = new Material(shader); // 남청색 본체 재질 생성
            terminalBodyMaterial.name = "Day31_MissionTerminal_Body"; // 디버그 이름 적용
            ApplyMaterialColor(terminalBodyMaterial, new Color(0.025f, 0.07f, 0.10f, 1f), false); // 본체 색상 적용

            terminalScreenMaterial = new Material(shader); // 청록 발광 화면 재질 생성
            terminalScreenMaterial.name = "Day31_MissionTerminal_Screen"; // 디버그 이름 적용
            ApplyMaterialColor(terminalScreenMaterial, new Color(0.05f, 0.78f, 0.95f, 1f), true); // 발광 화면 색상 적용
        }

        private static void ApplyMaterialColor(Material material, Color color, bool emission) // URP·Built-in 호환 색상 적용
        {
            if (material == null) // 재질 존재 확인
            {
                return; // 처리 생략
            }

            material.color = color; // 기본 색상 설정
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP 기본 색 적용
            if (material.HasProperty("_Color")) material.SetColor("_Color", color); // Built-in 기본 색 적용

            if (emission) // 발광 화면 확인
            {
                material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.0f); // 청록 발광 적용
            }
        }

        private MapPoint FindPlace(string placeId) // 기존 MapPoint ID 검색
        {
            if (world == null || world.Places == null || string.IsNullOrWhiteSpace(placeId)) // 월드·ID 확인
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < world.Places.Length; i++) // 주요 장소 순회
            {
                MapPoint place = world.Places[i]; // 현재 장소 조회
                if (place != null && place.PlaceId == placeId) // ID 일치 확인
                {
                    return place; // 장소 반환
                }
            }

            return null; // 장소 없음
        }

        private void HandleSceneChanged(Scene previous, Scene current) // 씬 전환 후 목표 오브젝트 참조 초기화
        {
            world = null; // 이전 월드 참조 제거
            runtimeRoot = null; // 이전 씬 루트 참조 제거
            runtimeTargets.Clear(); // 이전 목표 Transform 제거
            worldInstanceId = 0; // 월드 ID 초기화
            nextWorldResolveTime = 0f; // 새 씬 즉시 검색 허용
        }

        private static string DisplayItemName(string itemId) // 임무 물품 ID를 HUD용 이름으로 변환
        {
            return itemId == "BAEKYA_TOKEN" ? "백야 인증 조각" : itemId; // 현재 M-01 물품 이름 반환
        }

        private void OnDestroy() // 런타임 MissionData와 재질 정리
        {
            foreach (KeyValuePair<string, Map31MissionDefinition> pair in definitions) // 생성한 런타임 ScriptableObject 순회
            {
                if (pair.Value != null) Destroy(pair.Value); // 플레이 종료 시 런타임 데이터 제거
            }

            if (terminalBodyMaterial != null) Destroy(terminalBodyMaterial); // 본체 재질 제거
            if (terminalScreenMaterial != null) Destroy(terminalScreenMaterial); // 화면 재질 제거

            if (instance == this) // 현재 singleton 확인
            {
                instance = null; // 정적 참조 해제
            }
        }
    }
}
