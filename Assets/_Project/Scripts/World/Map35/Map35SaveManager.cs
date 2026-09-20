using System; // 저장 시각과 배열 처리
using System.Collections; // Dictionary·HashSet 비제네릭 접근
using System.Collections.Generic; // 슬롯 캐시
using System.IO; // JSON·PNG 파일 저장
using System.Reflection; // 기존 런타임 상태 안전 캡처·복원
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day29; // 현재 목표 Provider 복원
using ProjectK.Day30; // Mission Journal 상태 복원
using ProjectK.Day31; // MissionManager·MissionInventory 참조
using ProjectK.Day32; // 체크포인트·보상 기록 참조
using UnityEngine; // JsonUtility·Texture2D·플레이어 위치

namespace ProjectK.Day35 // 35일차 저장·불러오기 이름 공간
{
    [DisallowMultipleComponent] // SaveManager 중복 방지
    public sealed class Map35SaveManager : MonoBehaviour // 10개 수동 저장 슬롯과 수직 슬라이스 상태 영구 저장
    {
        private const int SlotCount = 10; // 저장 슬롯 총 개수
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic; // private 인스턴스 멤버 접근 플래그
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic; // private 정적 멤버 접근 플래그
        private static Map35SaveManager instance; // 현재 SaveManager
        private readonly Map35SlotInfo[] slots = new Map35SlotInfo[SlotCount]; // 슬롯 메타·미리보기 캐시
        private string saveDirectory = string.Empty; // 실제 저장 폴더 경로

        public static Map35SaveManager Instance => instance; // 현재 SaveManager 조회
        public int Count => SlotCount; // UI용 슬롯 수
        public string SaveDirectory => saveDirectory; // 디버그용 저장 폴더
        public IReadOnlyList<Map35SlotInfo> Slots => slots; // 현재 슬롯 정보 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 새 플레이 세션 정적 참조 초기화
        {
            instance = null; // 이전 SaveManager 참조 제거
        }

        private void Awake() // 단일 SaveManager 등록과 저장 폴더 준비
        {
            if (instance != null && instance != this) // 기존 SaveManager 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 SaveManager 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            saveDirectory = Path.Combine(Application.persistentDataPath, "ProjectK", "Saves"); // 플랫폼별 영구 저장 폴더 구성
            Directory.CreateDirectory(saveDirectory); // 저장 폴더 생성
            RefreshSlots(); // 시작 시 슬롯 메타데이터 읽기
        }

        public Map35SlotInfo SlotInfo(int slot) // 슬롯 번호로 카드 정보 조회
        {
            int index = slot - 1; // 1 기반 슬롯을 배열 인덱스로 변환
            return index >= 0 && index < slots.Length ? slots[index] : null; // 유효 슬롯 정보 반환
        }

        public void RefreshSlots() // JSON·PNG 파일에서 10개 슬롯 캐시 갱신
        {
            for (int i = 0; i < slots.Length; i++) // 전체 슬롯 순회
            {
                if (slots[i] != null && slots[i].Preview != null) // 이전 로드 미리보기 확인
                {
                    Destroy(slots[i].Preview); // 이전 미리보기 텍스처 메모리 정리
                }

                int slot = i + 1; // 실제 슬롯 번호 계산
                Map35SlotInfo info = new Map35SlotInfo(); // 새 슬롯 카드 정보 생성
                info.Slot = slot; // 슬롯 번호 저장
                string dataPath = DataPath(slot); // 슬롯 JSON 경로 계산
                info.HasData = File.Exists(dataPath); // 저장 파일 존재 여부 확인

                if (info.HasData) // 저장 데이터 존재 확인
                {
                    Map35SaveData data = ReadDataWithBackup(slot); // 정상 또는 백업 JSON 읽기
                    info.HasData = data != null; // JSON 파싱 성공 여부 반영
                    info.Meta = data != null ? data.Meta : null; // 카드 메타데이터 저장
                    info.Preview = LoadPreview(slot); // PNG 미리보기 로드
                }

                slots[i] = info; // 슬롯 캐시 교체
            }
        }

        public bool SaveSlot(int slot, Texture2D preview) // 현재 플레이 상태를 지정 슬롯에 저장
        {
            if (!ValidSlot(slot)) // 슬롯 범위 확인
            {
                return false; // 저장 실패
            }

            try // 파일 저장 예외 처리
            {
                Map35SaveData data = CaptureCurrentState(slot); // 현재 플레이 상태 캡처
                if (data == null) // 캡처 실패 확인
                {
                    return false; // 저장 중단
                }

                string json = JsonUtility.ToJson(data, true); // 가독성 있는 JSON 생성
                string dataPath = DataPath(slot); // 최종 JSON 경로
                string backupPath = BackupPath(slot); // 이전 JSON 백업 경로
                string tempPath = dataPath + ".tmp"; // 원자적 저장용 임시 경로

                File.WriteAllText(tempPath, json); // 임시 파일에 먼저 전체 JSON 기록
                if (File.Exists(dataPath)) File.Copy(dataPath, backupPath, true); // 이전 정상 저장을 백업으로 보존
                File.Copy(tempPath, dataPath, true); // 완성된 임시 파일을 최종 슬롯으로 복사
                File.Delete(tempPath); // 임시 파일 정리

                if (preview != null) // 메뉴 진입 전 플레이 화면 미리보기 확인
                {
                    byte[] png = preview.EncodeToPNG(); // 미리보기 PNG 인코딩
                    if (png != null && png.Length > 0) File.WriteAllBytes(PreviewPath(slot), png); // 슬롯 PNG 저장
                }

                RefreshSlots(); // 카드 정보 즉시 갱신
                Debug.Log("[Day35] 슬롯 저장 완료 · " + slot + " / " + data.Meta.MissionTitle); // 개발 확인 로그
                return true; // 저장 성공
            }
            catch (Exception error) // 파일 접근·직렬화 오류 처리
            {
                Debug.LogException(error); // 상세 오류 출력
                return false; // 저장 실패
            }
        }

        public bool LoadSlot(int slot) // 지정 슬롯 데이터를 현재 플레이 세션에 복원
        {
            if (!ValidSlot(slot)) // 슬롯 범위 확인
            {
                return false; // 불러오기 실패
            }

            try // 파일 읽기·복원 예외 처리
            {
                Map35SaveData data = ReadDataWithBackup(slot); // 정상 파일 또는 백업 읽기
                if (data == null) // 유효 저장 데이터 확인
                {
                    return false; // 불러오기 실패
                }

                RestoreCurrentState(data); // 현재 런타임 상태에 저장 데이터 적용
                RefreshSlots(); // 슬롯 카드 정보 재확인
                Debug.Log("[Day35] 슬롯 불러오기 완료 · " + slot + " / " + data.Meta.MissionTitle); // 개발 확인 로그
                return true; // 불러오기 성공
            }
            catch (Exception error) // 복원 예외 처리
            {
                Debug.LogException(error); // 상세 오류 출력
                return false; // 불러오기 실패
            }
        }

        public bool DeleteSlot(int slot) // 선택 슬롯의 JSON·백업·미리보기 제거
        {
            if (!ValidSlot(slot)) // 슬롯 범위 확인
            {
                return false; // 삭제 실패
            }

            try // 파일 삭제 예외 처리
            {
                DeleteIfExists(DataPath(slot)); // 현재 JSON 삭제
                DeleteIfExists(BackupPath(slot)); // 백업 JSON 삭제
                DeleteIfExists(PreviewPath(slot)); // PNG 미리보기 삭제
                RefreshSlots(); // 카드 상태 갱신
                return true; // 삭제 성공
            }
            catch (Exception error) // 파일 삭제 실패 처리
            {
                Debug.LogException(error); // 상세 오류 출력
                return false; // 삭제 실패
            }
        }

        private Map35SaveData CaptureCurrentState(int slot) // 플레이·미션·보상 상태를 하나의 SaveData로 수집
        {
            MapWorldRoot world = ResolveWorld(); // 현재 본편 월드 조회
            if (world == null || world.Player == null) // 저장 가능한 플레이어 확인
            {
                return null; // 저장 불가
            }

            GameObject player = world.Player; // 현재 플레이어 저장
            PlayerHealth health = player.GetComponent<PlayerHealth>(); // 생존 수치 조회
            Map31MissionManager manager = Map31MissionManager.Instance; // 중앙 MissionManager 조회
            Map30MissionJournal journal = Map30MissionJournal.Instance; // 현재 Mission Journal 조회
            Map35SaveData data = new Map35SaveData(); // 새 V1 저장 데이터 생성

            data.Meta.Version = 1; // 메타 버전 저장
            data.Meta.Slot = slot; // 슬롯 번호 저장
            data.Meta.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // 로컬 저장 시각 문자열
            data.Meta.PlaySeconds = Time.unscaledTime; // 현재 플레이 세션 경과 시간
            data.Player.Position = player.transform.position; // 플레이어 위치 저장
            data.Player.Euler = player.transform.eulerAngles; // 플레이어 회전 저장
            data.Player.Health = health != null ? health.CurrentHealth : 100f; // 현재 체력 저장
            data.Player.Posture = health != null ? health.CurrentPosture : 100f; // 현재 자세 저장
            data.ActiveMissionId = manager != null ? manager.ActiveMissionId : string.Empty; // 활성 임무 ID 저장
            data.Missions = CaptureMissionStates(manager, journal); // M-01·M-02·S-01 진행 상태 저장

            if (manager != null && manager.Inventory != null) // 임무 물품 슬롯 확인
            {
                data.OwnedMissionItems = manager.Inventory.CaptureOwnedItems(); // 보유 임무 물품 저장
                data.DeliveredMissionItems = manager.Inventory.CaptureDeliveredItems(); // 인계 임무 물품 저장
            }

            data.Checkpoint = CaptureCheckpoint(); // Day32 안전 체크포인트 저장
            data.GrantedMissionRewards = CaptureStaticStringSet(typeof(Map32MissionRewardLedger), "granted"); // 결과 보상 지급 기록 저장
            data.Credits = ReadOptionalStaticIntProperty("ProjectK.Day34.Map34RewardWallet", "Credits"); // Day34가 존재할 때 실제 크레딧 저장
            data.CreditRewardedMissions = CaptureOptionalWalletRewards(); // Day34가 존재할 때 크레딧 지급 완료 임무 저장
            data.S01 = CaptureS01State(); // ‘틈’ 계약·선택 보상 상태 저장
            CaptureConsumables(player, data); // 소모품 수량과 선택 상태 저장
            FillMetaMission(data, manager, journal); // 슬롯 카드용 임무·목표 문구 저장
            return data; // 완성 저장 데이터 반환
        }

        private void RestoreCurrentState(Map35SaveData data) // 저장 데이터를 현재 런타임 시스템에 복원
        {
            MapWorldRoot world = ResolveWorld(); // 현재 본편 월드 조회
            if (world == null || world.Player == null) // 플레이어 준비 확인
            {
                throw new InvalidOperationException("플레이어 월드가 준비되지 않았습니다."); // 복원 불가 보고
            }

            GameObject player = world.Player; // 현재 플레이어 조회
            RestorePlayer(player, data.Player); // 위치·체력·자세 복원

            Map31MissionManager manager = Map31MissionManager.Instance; // MissionManager 조회
            if (manager != null && manager.Inventory != null) // 임무 물품 슬롯 확인
            {
                manager.Inventory.RestoreSnapshot(data.OwnedMissionItems, data.DeliveredMissionItems); // 임무 물품 보유·인계 상태 복원
            }

            RestoreMissionStates(manager, data); // 전체 임무 상태와 현재 목표 복원
            RestoreCheckpoint(data.Checkpoint); // 안전 체크포인트 복원
            RestoreStaticStringSet(typeof(Map32MissionRewardLedger), "granted", data.GrantedMissionRewards); // Day32 보상 중복 방지 기록 복원
            RestoreWallet(data.Credits, data.CreditRewardedMissions); // 크레딧과 지급 임무 복원
            RestoreS01State(data.S01); // S-01 계약·선택 보상 상태 복원
            RestoreConsumables(player, data); // 소모품 수량·선택 상태 복원
            Physics.SyncTransforms(); // 순간 위치 변경을 물리 시스템에 즉시 반영
        }

        private static Map35MissionStateData[] CaptureMissionStates(Map31MissionManager manager, Map30MissionJournal journal) // private MissionManager Dictionary 상태 캡처
        {
            if (manager == null) // 관리자 존재 확인
            {
                return Array.Empty<Map35MissionStateData>(); // 빈 상태 반환
            }

            FieldInfo statesField = typeof(Map31MissionManager).GetField("states", InstancePrivate); // private 임무 상태 Dictionary 조회
            IDictionary states = statesField != null ? statesField.GetValue(manager) as IDictionary : null; // 비제네릭 Dictionary 인터페이스 변환
            if (states == null) // Dictionary 접근 실패 확인
            {
                return Array.Empty<Map35MissionStateData>(); // 빈 상태 반환
            }

            List<Map35MissionStateData> result = new List<Map35MissionStateData>(); // 저장 임무 목록 생성
            foreach (DictionaryEntry pair in states) // 등록 임무 전체 순회
            {
                Map31MissionRuntimeState runtime = pair.Value as Map31MissionRuntimeState; // 현재 런타임 상태 변환
                if (runtime == null || runtime.Definition == null) // 유효 임무 확인
                {
                    continue; // 잘못된 항목 제외
                }

                Map30MissionEntry entry = journal != null ? journal.Find(runtime.Definition.MissionId) : null; // Journal 표시 상태 조회
                result.Add(new Map35MissionStateData
                {
                    MissionId = runtime.Definition.MissionId, // 임무 ID 저장
                    RuntimeStatus = (int)runtime.Status, // 실제 런타임 상태 저장
                    JournalStatus = entry != null ? (int)entry.Status : (int)Map30MissionStatus.Available, // UI 잠김·추적 상태 포함 저장
                    ObjectiveIndex = runtime.ObjectiveIndex // 현재 목표 순번 저장
                });
            }

            return result.ToArray(); // JSON 직렬화 배열 반환
        }

        private static void RestoreMissionStates(Map31MissionManager manager, Map35SaveData data) // MissionManager private Dictionary와 Journal 상태 복원
        {
            if (manager == null || data == null) // 복원 대상 확인
            {
                return; // 처리 생략
            }

            FieldInfo statesField = typeof(Map31MissionManager).GetField("states", InstancePrivate); // private 상태 Dictionary 조회
            FieldInfo activeField = typeof(Map31MissionManager).GetField("activeMission", InstancePrivate); // 현재 활성 임무 필드 조회
            IDictionary states = statesField != null ? statesField.GetValue(manager) as IDictionary : null; // Dictionary 접근
            if (states == null) // 상태 Dictionary 접근 실패 확인
            {
                return; // 복원 중단
            }

            if (activeField != null) activeField.SetValue(manager, null); // 현재 세션 활성 임무 먼저 해제
            Map29TerminalObjectiveProvider.ClearObjective(); // 이전 목표 HUD 정리

            if (data.Missions != null) // 저장 임무 상태 존재 확인
            {
                for (int i = 0; i < data.Missions.Length; i++) // 저장된 임무 순회
                {
                    Map35MissionStateData saved = data.Missions[i]; // 현재 저장 임무 조회
                    if (saved == null || string.IsNullOrWhiteSpace(saved.MissionId) || !states.Contains(saved.MissionId)) // 현재 빌드에 존재하는 임무인지 확인
                    {
                        continue; // 없는 임무 제외
                    }

                    Map31MissionRuntimeState runtime = states[saved.MissionId] as Map31MissionRuntimeState; // 실제 런타임 상태 조회
                    if (runtime == null || runtime.Definition == null) // 유효 상태 확인
                    {
                        continue; // 복원 생략
                    }

                    runtime.Status = (Map31MissionRuntimeStatus)Mathf.Clamp(saved.RuntimeStatus, 0, 3); // 런타임 상태 복원
                    runtime.ObjectiveIndex = Mathf.Clamp(saved.ObjectiveIndex, 0, Mathf.Max(0, runtime.Definition.Objectives.Count - 1)); // 목표 순번 복원
                    Map30MissionStatus journalStatus = (Map30MissionStatus)Mathf.Clamp(saved.JournalStatus, 0, 4); // Journal 상태 복원값 계산
                    Map30MissionJournal.SetMissionStatus(saved.MissionId, journalStatus); // Tab 임무 상태 복원
                    Map30MissionJournal.SetCurrentObjective(saved.MissionId, runtime.ObjectiveIndex); // Tab 현재 목표 복원
                }
            }

            if (!string.IsNullOrWhiteSpace(data.ActiveMissionId) && states.Contains(data.ActiveMissionId)) // 저장 시 진행 중 임무 확인
            {
                Map35MissionStateData activeSaved = FindMissionData(data.Missions, data.ActiveMissionId); // 저장된 활성 임무 자료 검색
                int objectiveIndex = activeSaved != null ? activeSaved.ObjectiveIndex : 0; // 활성 목표 순번 선택
                manager.RestoreFromCheckpoint(data.ActiveMissionId, objectiveIndex); // 기존 공통 API로 활성 임무·목표 HUD 복원
            }
        }

        private static Map35MissionStateData FindMissionData(Map35MissionStateData[] missions, string missionId) // 임무 ID로 저장 상태 검색
        {
            if (missions == null) // 저장 배열 확인
            {
                return null; // 검색 실패
            }

            for (int i = 0; i < missions.Length; i++) // 전체 저장 임무 순회
            {
                if (missions[i] != null && missions[i].MissionId == missionId) // ID 일치 확인
                {
                    return missions[i]; // 대상 반환
                }
            }

            return null; // 대상 없음
        }

        private static Map35CheckpointData CaptureCheckpoint() // Day32 현재 체크포인트를 영구 저장 구조로 복사
        {
            Map32MissionCheckpointState current = Map32MissionCheckpointSystem.Instance != null ? Map32MissionCheckpointSystem.Instance.Current : null; // 현재 체크포인트 조회
            if (current == null) // 체크포인트 없음 확인
            {
                return new Map35CheckpointData(); // 빈 체크포인트 반환
            }

            return new Map35CheckpointData
            {
                Valid = current.Valid, // 유효 여부 복사
                MissionId = current.MissionId ?? string.Empty, // 임무 ID 복사
                Label = current.Label ?? string.Empty, // 이름 복사
                ObjectiveIndex = current.ObjectiveIndex, // 목표 순번 복사
                PlayerPosition = current.PlayerPosition, // 위치 복사
                PlayerEuler = current.PlayerEuler, // 회전 복사
                OwnedItems = current.OwnedItems ?? Array.Empty<string>(), // 보유 물품 복사
                DeliveredItems = current.DeliveredItems ?? Array.Empty<string>() // 인계 물품 복사
            };
        }

        private static void RestoreCheckpoint(Map35CheckpointData saved) // Day32 private current 체크포인트 복원
        {
            Map32MissionCheckpointSystem checkpoint = Map32MissionCheckpointSystem.Instance; // 현재 체크포인트 관리자 조회
            if (checkpoint == null || saved == null) // 복원 가능 여부 확인
            {
                return; // 처리 생략
            }

            FieldInfo currentField = typeof(Map32MissionCheckpointSystem).GetField("current", InstancePrivate); // private current 필드 조회
            if (currentField == null) // 필드 접근 실패 확인
            {
                return; // 복원 생략
            }

            Map32MissionCheckpointState state = new Map32MissionCheckpointState
            {
                Valid = saved.Valid, // 유효 여부 복원
                MissionId = saved.MissionId ?? string.Empty, // 임무 ID 복원
                Label = saved.Label ?? string.Empty, // 이름 복원
                ObjectiveIndex = saved.ObjectiveIndex, // 목표 순번 복원
                PlayerPosition = saved.PlayerPosition, // 위치 복원
                PlayerEuler = saved.PlayerEuler, // 회전 복원
                OwnedItems = saved.OwnedItems ?? Array.Empty<string>(), // 보유 물품 복원
                DeliveredItems = saved.DeliveredItems ?? Array.Empty<string>(), // 인계 물품 복원
                CapturedAt = Time.unscaledTime // 현재 세션 시각으로 복원
            };

            currentField.SetValue(checkpoint, state); // 체크포인트 관리자에 영구 저장 상태 적용
        }

        private static void RestorePlayer(GameObject player, Map35PlayerStateData saved) // 플레이어 위치·생존 수치 복원
        {
            if (player == null || saved == null) // 복원 자료 확인
            {
                return; // 처리 생략
            }

            PlayerMovement movement = player.GetComponent<PlayerMovement>(); // 이동 관리자 조회
            CharacterController controller = player.GetComponent<CharacterController>(); // 충돌 컨트롤러 조회
            PlayerHealth health = player.GetComponent<PlayerHealth>(); // 생존 관리자 조회
            bool controllerEnabled = controller != null && controller.enabled; // 기존 충돌체 상태 저장

            player.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 사격·재장전 중단
            if (controller != null) controller.enabled = false; // 순간 이동 동안 CharacterController 비활성화
            player.transform.SetPositionAndRotation(saved.Position, Quaternion.Euler(saved.Euler)); // 저장 위치·방향 복원
            movement?.SetHorizontalVelocity(Vector3.zero); // 수평 관성 초기화

            if (movement != null) // 이동 관리자 확인
            {
                movement.VerticalVelocity = 0f; // 수직 속도 초기화
                movement.SetSpawnPoint(saved.Position, Quaternion.Euler(saved.Euler)); // 추락 복귀 지점도 저장 위치로 갱신
            }

            if (controller != null) controller.enabled = controllerEnabled; // 기존 CharacterController 상태 복구

            if (health != null) // 생존 수치 복원 가능 여부 확인
            {
                health.RestoreFromCheckpoint(); // 사망·자세 붕괴·전투 잠금 안전 해제
                SetPrivateFloat(health, "currentHealth", Mathf.Clamp(saved.Health, 1f, health.MaxHealth)); // 저장 체력 적용
                SetPrivateFloat(health, "currentPosture", Mathf.Clamp(saved.Posture, 1f, health.MaxPosture)); // 저장 자세 적용
            }
        }

        private static void CaptureConsumables(GameObject player, Map35SaveData data) // 소모품 배열·현재 선택 종류 캡처
        {
            ConsumableController controller = player != null ? player.GetComponent<ConsumableController>() : null; // 플레이어 소모품 관리자 조회
            if (controller == null) // 관리자 누락 확인
            {
                return; // 저장 생략
            }

            FieldInfo countsField = typeof(ConsumableController).GetField("counts", InstancePrivate); // private 수량 배열 조회
            FieldInfo selectedField = typeof(ConsumableController).GetField("selected", InstancePrivate); // private 선택 종류 조회
            int[] counts = countsField != null ? countsField.GetValue(controller) as int[] : null; // 실제 배열 접근
            data.ConsumableCounts = counts != null ? (int[])counts.Clone() : Array.Empty<int>(); // 수량 배열 복사
            data.SelectedConsumable = selectedField != null ? (int)(ConsumableKind)selectedField.GetValue(controller) : 0; // 선택 종류 정수값 저장
        }

        private static void RestoreConsumables(GameObject player, Map35SaveData data) // 소모품 수량·선택 상태 복원
        {
            ConsumableController controller = player != null ? player.GetComponent<ConsumableController>() : null; // 소모품 관리자 조회
            if (controller == null || data == null) // 복원 가능 여부 확인
            {
                return; // 처리 생략
            }

            FieldInfo countsField = typeof(ConsumableController).GetField("counts", InstancePrivate); // private 수량 배열 조회
            FieldInfo selectedField = typeof(ConsumableController).GetField("selected", InstancePrivate); // private 선택 상태 조회
            int[] counts = countsField != null ? countsField.GetValue(controller) as int[] : null; // 현재 배열 접근

            if (counts != null && data.ConsumableCounts != null) // 저장 수량 존재 확인
            {
                int length = Mathf.Min(counts.Length, data.ConsumableCounts.Length); // 공통 배열 길이 계산
                for (int i = 0; i < length; i++) counts[i] = Mathf.Max(0, data.ConsumableCounts[i]); // 각 소모품 수량 복원
            }

            if (selectedField != null) selectedField.SetValue(controller, (ConsumableKind)Mathf.Clamp(data.SelectedConsumable, 0, 2)); // 현재 선택 소모품 복원
        }

        private static Map35SideContractStateData CaptureS01State() // Day34가 존재할 때 계약·선택 보상 상태 캡처
        {
            Map35SideContractStateData data = new Map35SideContractStateData(); // 새 계약 저장 구조 생성
            Type controllerType = FindOptionalType("ProjectK.Day34.Map34SideMissionWorldController"); // 선택 Day34 월드 관리자 타입 검색
            object controller = ReadOptionalStaticProperty(controllerType, "Instance"); // 현재 S-01 월드 관리자 조회

            if (controller != null) // Day34 월드 관리자가 존재하는 빌드 확인
            {
                data.ContractReleased = ReadPrivateBool(controller, "contractReleased"); // 신규 의뢰 공개 여부 저장
                data.CreditGranted = ReadPrivateBool(controller, "creditGranted"); // 크레딧 처리 여부 저장
                data.RewardChoiceShown = ReadPrivateBool(controller, "rewardChoiceShown"); // 선택 화면 표시 여부 저장
                data.ContractClosedShown = ReadPrivateBool(controller, "contractClosedShown"); // 종료 메시지 표시 여부 저장
            }

            Type rewardType = FindOptionalType("ProjectK.Day34.Map34RewardChoiceHUD"); // 선택 Day34 보상 HUD 타입 검색
            data.RewardChosen = ReadOptionalStaticBoolProperty(rewardType, "RewardChosen"); // 선택 보상 완료 여부 저장
            data.SelectedRewardName = ReadOptionalStaticStringProperty(rewardType, "SelectedRewardName"); // 선택 보상 이름 저장
            return data; // 계약 저장 상태 반환
        }

        private static void RestoreS01State(Map35SideContractStateData saved) // Day34가 존재할 때 계약·선택 보상 상태 복원
        {
            if (saved == null) // 저장 자료 확인
            {
                return; // 처리 생략
            }

            Type controllerType = FindOptionalType("ProjectK.Day34.Map34SideMissionWorldController"); // 선택 Day34 월드 관리자 타입 검색
            object controller = ReadOptionalStaticProperty(controllerType, "Instance"); // 현재 S-01 월드 관리자 조회
            if (controller != null) // Day34 관리자 존재 확인
            {
                SetPrivateBool(controller, "contractReleased", saved.ContractReleased); // 신규 의뢰 공개 상태 복원
                SetPrivateBool(controller, "releaseScheduled", saved.ContractReleased); // 공개된 계약은 재예약 방지
                SetPrivateBool(controller, "creditGranted", saved.CreditGranted); // 크레딧 처리 플래그 복원
                SetPrivateBool(controller, "rewardChoiceShown", saved.RewardChoiceShown || saved.RewardChosen); // 선택 화면 중복 표시 방지
                SetPrivateBool(controller, "contractClosedShown", saved.ContractClosedShown); // 종료 메시지 상태 복원
            }

            Type rewardType = FindOptionalType("ProjectK.Day34.Map34RewardChoiceHUD"); // 선택 Day34 보상 HUD 타입 검색
            if (rewardType != null) // 보상 HUD가 존재하는 프로젝트 확인
            {
                FieldInfo chosenField = rewardType.GetField("rewardChosen", StaticPrivate); // private static 보상 선택 여부 조회
                FieldInfo nameField = rewardType.GetField("selectedRewardName", StaticPrivate); // private static 선택 이름 조회
                if (chosenField != null) chosenField.SetValue(null, saved.RewardChosen); // 선택 완료 여부 복원
                if (nameField != null) nameField.SetValue(null, saved.SelectedRewardName ?? string.Empty); // 선택 보상 이름 복원
            }
        }

        private static string[] CaptureOptionalWalletRewards() // Day34가 있을 때 private 크레딧 지급 기록 캡처
        {
            Type walletType = FindOptionalType("ProjectK.Day34.Map34RewardWallet"); // 선택 Day34 지갑 타입 검색
            object wallet = ReadOptionalStaticProperty(walletType, "Instance"); // 현재 지갑 인스턴스 조회
            return CaptureInstanceStringSet(wallet, "rewardedMissions"); // 지갑이 없으면 빈 배열 반환
        }

        private static string[] CaptureStaticStringSet(Type ownerType, string fieldName) // private static HashSet<string> 배열 변환
        {
            FieldInfo field = ownerType.GetField(fieldName, StaticPrivate); // 정적 private 필드 조회
            object collection = field != null ? field.GetValue(null) : null; // 컬렉션 접근
            return CollectionToStringArray(collection); // 문자열 배열 반환
        }

        private static void RestoreStaticStringSet(Type ownerType, string fieldName, string[] values) // private static HashSet<string> 내용 복원
        {
            FieldInfo field = ownerType.GetField(fieldName, StaticPrivate); // 정적 private 필드 조회
            object collection = field != null ? field.GetValue(null) : null; // 실제 HashSet 접근
            ReplaceStringCollection(collection, values); // 기존 내용 제거 후 저장값 복원
        }

        private static string[] CaptureInstanceStringSet(object target, string fieldName) // private 인스턴스 HashSet<string> 배열 변환
        {
            if (target == null) // 대상 존재 확인
            {
                return Array.Empty<string>(); // 빈 배열 반환
            }

            FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate); // private 필드 조회
            object collection = field != null ? field.GetValue(target) : null; // 컬렉션 접근
            return CollectionToStringArray(collection); // 문자열 배열 반환
        }

        private static void RestoreWallet(int credits, string[] rewardedMissions) // Day34가 존재할 때 크레딧·지급 기록 복원
        {
            Type walletType = FindOptionalType("ProjectK.Day34.Map34RewardWallet"); // 선택 Day34 지갑 타입 검색
            object wallet = ReadOptionalStaticProperty(walletType, "Instance"); // 현재 지갑 인스턴스 조회
            if (wallet == null || walletType == null) // Day34가 없는 현재 빌드 확인
            {
                return; // 선택 기능 복원 생략
            }

            FieldInfo creditsField = walletType.GetField("credits", InstancePrivate); // private 크레딧 필드 조회
            FieldInfo rewardedField = walletType.GetField("rewardedMissions", InstancePrivate); // private 지급 HashSet 조회
            if (creditsField != null) creditsField.SetValue(wallet, Mathf.Max(0, credits)); // 크레딧 복원
            object collection = rewardedField != null ? rewardedField.GetValue(wallet) : null; // 지급 기록 컬렉션 접근
            ReplaceStringCollection(collection, rewardedMissions); // 지급 완료 임무 복원
        }

        private static Type FindOptionalType(string fullTypeName) // 현재 로드된 어셈블리에서 선택 시스템 타입 검색
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies(); // 현재 로드된 모든 어셈블리 조회
            for (int i = 0; i < assemblies.Length; i++) // 어셈블리 순회
            {
                Type type = assemblies[i].GetType(fullTypeName, false); // 정확한 전체 타입 이름 검색
                if (type != null) // 타입 발견 확인
                {
                    return type; // 타입 반환
                }
            }

            return null; // 선택 타입 없음
        }

        private static object ReadOptionalStaticProperty(Type type, string propertyName) // 선택 타입의 공개 정적 속성 안전 조회
        {
            if (type == null) // 타입 존재 확인
            {
                return null; // 선택 기능 없음
            }

            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static); // 공개 정적 속성 조회
            return property != null ? property.GetValue(null) : null; // 값 또는 null 반환
        }

        private static int ReadOptionalStaticIntProperty(string fullTypeName, string propertyName) // 선택 타입의 정적 int 속성 조회
        {
            Type type = FindOptionalType(fullTypeName); // 타입 검색
            object value = ReadOptionalStaticProperty(type, propertyName); // 속성 읽기
            return value is int number ? number : 0; // 크레딧 또는 기본 0 반환
        }

        private static bool ReadOptionalStaticBoolProperty(Type type, string propertyName) // 선택 타입의 정적 bool 속성 조회
        {
            object value = ReadOptionalStaticProperty(type, propertyName); // 속성 읽기
            return value is bool flag && flag; // bool 또는 false 반환
        }

        private static string ReadOptionalStaticStringProperty(Type type, string propertyName) // 선택 타입의 정적 string 속성 조회
        {
            object value = ReadOptionalStaticProperty(type, propertyName); // 속성 읽기
            return value as string ?? string.Empty; // 문자열 또는 빈 값 반환
        }

        private static string[] CollectionToStringArray(object collection) // 문자열 컬렉션을 JSON 배열로 변환
        {
            IEnumerable enumerable = collection as IEnumerable; // 비제네릭 열거 인터페이스 변환
            if (enumerable == null) // 컬렉션 접근 실패 확인
            {
                return Array.Empty<string>(); // 빈 배열 반환
            }

            List<string> result = new List<string>(); // 결과 목록 생성
            foreach (object item in enumerable) // 모든 컬렉션 항목 순회
            {
                string value = item as string; // 문자열 변환
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value); // 유효 ID만 추가
            }

            return result.ToArray(); // 문자열 배열 반환
        }

        private static void ReplaceStringCollection(object collection, string[] values) // HashSet<string> private 컬렉션을 리플렉션으로 교체
        {
            if (collection == null) // 대상 컬렉션 확인
            {
                return; // 처리 생략
            }

            MethodInfo clear = collection.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public); // Clear 메서드 조회
            MethodInfo add = collection.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public); // Add 메서드 조회
            clear?.Invoke(collection, null); // 기존 컬렉션 초기화

            if (values == null || add == null) // 복원 값과 Add API 확인
            {
                return; // 추가 처리 생략
            }

            for (int i = 0; i < values.Length; i++) // 저장 문자열 순회
            {
                if (!string.IsNullOrWhiteSpace(values[i])) add.Invoke(collection, new object[] { values[i] }); // 유효 값 복원
            }
        }

        private static void FillMetaMission(Map35SaveData data, Map31MissionManager manager, Map30MissionJournal journal) // 슬롯 카드용 현재 임무·목표 정보 구성
        {
            string missionId = manager != null ? manager.ActiveMissionId : string.Empty; // 현재 활성 임무 ID 조회
            Map30MissionEntry entry = !string.IsNullOrEmpty(missionId) && journal != null ? journal.Find(missionId) : null; // 현재 임무 Journal 조회

            if (entry == null && journal != null) // 진행 중 임무가 없는 경우 최근 표시할 완료·보유 임무 검색
            {
                for (int i = journal.Missions.Count - 1; i >= 0; i--) // 뒤쪽 임무부터 검색
                {
                    if (journal.Missions[i] != null && journal.Missions[i].Status == Map30MissionStatus.Completed) // 완료 임무 우선 확인
                    {
                        entry = journal.Missions[i]; // 카드 표시 임무 선택
                        break; // 검색 종료
                    }
                }
            }

            data.Meta.MissionId = entry != null ? entry.MissionId : string.Empty; // 카드 임무 ID 저장
            data.Meta.MissionTitle = entry != null ? entry.Title : "도시 자유 이동"; // 카드 임무 제목 저장

            if (entry != null && entry.Objectives != null && entry.Objectives.Count > 0) // 현재 목표 문구 존재 확인
            {
                int index = Mathf.Clamp(entry.CurrentObjectiveIndex, 0, entry.Objectives.Count - 1); // 안전한 목표 순번 계산
                data.Meta.ObjectiveText = entry.Status == Map30MissionStatus.Completed ? "임무 완료" : entry.Objectives[index]; // 현재 목표 또는 완료 문구 저장
            }
            else // 임무 목표 없음 처리
            {
                data.Meta.ObjectiveText = "연무 도시 탐색"; // 기본 자유 이동 문구
            }

            data.Meta.Credits = data.Credits; // 카드 크레딧 값 저장
        }

        private static MapWorldRoot ResolveWorld() // 현재 활성 MapWorldRoot 검색
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 본편 월드 검색
            return worlds.Length > 0 ? worlds[0] : null; // 첫 월드 반환
        }

        private Map35SaveData ReadDataWithBackup(int slot) // 현재 JSON 실패 시 백업 JSON까지 시도
        {
            Map35SaveData data = ReadDataFile(DataPath(slot)); // 기본 저장 파일 읽기
            if (data != null) // 정상 파싱 확인
            {
                return data; // 기본 데이터 반환
            }

            return ReadDataFile(BackupPath(slot)); // 백업 파일 대체 읽기
        }

        private static Map35SaveData ReadDataFile(string path) // 단일 JSON 파일 안전 읽기
        {
            if (!File.Exists(path)) // 파일 존재 확인
            {
                return null; // 데이터 없음
            }

            try // JSON 파싱 오류 처리
            {
                string json = File.ReadAllText(path); // JSON 전체 읽기
                Map35SaveData data = JsonUtility.FromJson<Map35SaveData>(json); // SaveData 역직렬화
                return data != null && data.Version == 1 ? data : null; // V1 데이터만 반환
            }
            catch // 손상 JSON 처리
            {
                return null; // 백업 시도를 위해 null 반환
            }
        }

        private Texture2D LoadPreview(int slot) // 슬롯 PNG 미리보기 로드
        {
            string path = PreviewPath(slot); // PNG 경로 계산
            if (!File.Exists(path)) // 미리보기 파일 확인
            {
                return null; // 이미지 없음
            }

            try // 이미지 파일 읽기 오류 처리
            {
                byte[] bytes = File.ReadAllBytes(path); // PNG 바이트 읽기
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false); // 임시 텍스처 생성
                texture.name = "SavePreview_" + slot; // 디버그 이름 적용
                texture.filterMode = FilterMode.Bilinear; // 카드 축소·확대 부드럽게 표시

                if (!texture.LoadImage(bytes, false)) // PNG 디코딩 성공 확인
                {
                    Destroy(texture); // 실패 텍스처 정리
                    return null; // 미리보기 없음
                }

                return texture; // 완성 미리보기 반환
            }
            catch // 이미지 손상 처리
            {
                return null; // 미리보기 생략
            }
        }

        private string DataPath(int slot) // 슬롯 JSON 경로
        {
            return Path.Combine(saveDirectory, "slot_" + slot.ToString("00") + ".json"); // 01~10 JSON 파일명 반환
        }

        private string BackupPath(int slot) // 슬롯 백업 JSON 경로
        {
            return Path.Combine(saveDirectory, "slot_" + slot.ToString("00") + ".backup.json"); // 백업 파일명 반환
        }

        private string PreviewPath(int slot) // 슬롯 PNG 경로
        {
            return Path.Combine(saveDirectory, "slot_" + slot.ToString("00") + ".png"); // 미리보기 파일명 반환
        }

        private static bool ValidSlot(int slot) // 1~10 슬롯 범위 확인
        {
            return slot >= 1 && slot <= SlotCount; // 유효 범위 반환
        }

        private static void DeleteIfExists(string path) // 존재하는 파일만 삭제
        {
            if (File.Exists(path)) File.Delete(path); // 파일 존재 시 제거
        }

        private static void SetPrivateFloat(object target, string fieldName, float value) // private float 필드 안전 설정
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, InstancePrivate) : null; // 대상 필드 조회
            field?.SetValue(target, value); // 필드 존재 시 값 적용
        }

        private static bool ReadPrivateBool(object target, string fieldName) // private bool 필드 안전 조회
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, InstancePrivate) : null; // 대상 필드 조회
            object value = field != null ? field.GetValue(target) : null; // 필드 값 읽기
            return value is bool flag && flag; // bool 값 반환
        }

        private static void SetPrivateBool(object target, string fieldName, bool value) // private bool 필드 안전 설정
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, InstancePrivate) : null; // 대상 필드 조회
            field?.SetValue(target, value); // 필드 존재 시 값 적용
        }
    }
}
