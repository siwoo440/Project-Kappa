using System; // 체크포인트 직렬화 자료
using System.Reflection; // 기존 시스템에 Day32 확장 API를 안전하게 호출
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day21; // 수배 초기화 참조
using ProjectK.Day26; // 신고 초기화 참조
using ProjectK.Day30; // 임무 Journal 참조
using ProjectK.Day31; // MissionManager·Inventory 참조
using UnityEngine; // 위치·회전·플레이어 복원

namespace ProjectK.Day32 // 32일차 체크포인트·재시도 이름 공간
{
    [Serializable] // 체크포인트 상태 직렬화 가능 구조
    public sealed class Map32MissionCheckpointState // 현재 임무 재시도에 필요한 최소 상태
    {
        public bool Valid; // 유효 체크포인트 여부
        public string MissionId; // 임무 ID
        public string Label; // 화면 표시용 체크포인트 이름
        public int ObjectiveIndex; // 다음 진행 목표 순번
        public Vector3 PlayerPosition; // 플레이어 안전 위치
        public Vector3 PlayerEuler; // 플레이어 방향
        public string[] OwnedItems; // 보유 임무 물품
        public string[] DeliveredItems; // 인계 완료 임무 물품
        public float CapturedAt; // 플레이 세션 저장 시각
    }

    [DisallowMultipleComponent] // 체크포인트 관리자 중복 방지
    public sealed class Map32MissionCheckpointSystem : MonoBehaviour // 임무 상태·플레이어 위치·임무 물품을 저장하고 재시도 시 복원
    {
        private static Map32MissionCheckpointSystem instance; // 현재 체크포인트 관리자
        private Map32MissionCheckpointState current = new Map32MissionCheckpointState(); // 현재 안전 체크포인트
        private MapWorldRoot world; // 현재 본편 월드
        private PlayerHealth playerHealth; // 플레이어 생존 상태
        private bool deathHandled; // 현재 사망 실패 처리 여부
        private float nextResolveTime; // 다음 월드 참조 검색 시각

        public static Map32MissionCheckpointSystem Instance => instance; // 현재 체크포인트 관리자 조회
        public Map32MissionCheckpointState Current => current; // 현재 체크포인트 조회
        public string CurrentLabel => current != null && current.Valid ? current.Label : "체크포인트 없음"; // 실패 화면용 체크포인트 이름

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 참조 초기화
        {
            instance = null; // 이전 관리자 제거
        }

        private void Awake() // 단일 체크포인트 관리자 등록
        {
            if (instance != null && instance != this) // 다른 관리자 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 월드와 플레이어 연결
        {
            ResolveWorld(); // 현재 Map 참조 연결
        }

        private void Update() // 플레이어 사망 감지와 참조 복구
        {
            if (world == null || world.Player == null || playerHealth == null) // 월드·플레이어 참조 확인
            {
                if (Time.unscaledTime >= nextResolveTime) // 재검색 시각 확인
                {
                    nextResolveTime = Time.unscaledTime + 0.5f; // 다음 검색 예약
                    ResolveWorld(); // 월드와 플레이어 재연결
                }

                return; // 사망 감지 생략
            }

            if (!playerHealth.IsDead) // 살아 있는 상태 확인
            {
                deathHandled = false; // 다음 사망 감지 허용
                return; // 실패 처리 없음
            }

            if (deathHandled || Map32MissionResultScreen.IsOpen) // 이미 현재 사망을 처리했는지 확인
            {
                return; // 중복 실패 화면 방지
            }

            Map31MissionManager manager = Map31MissionManager.Instance; // 현재 MissionManager 조회
            string missionId = manager != null ? manager.ActiveMissionId : string.Empty; // 현재 진행 임무 ID 조회

            if (string.IsNullOrEmpty(missionId)) // 진행 중 임무가 없는 사망 확인
            {
                return; // 기존 사망 처리 유지
            }

            deathHandled = true; // 현재 사망 처리 완료 기록
            Map30MissionEntry entry = Map30MissionJournal.Instance != null ? Map30MissionJournal.Instance.Find(missionId) : null; // 실패 화면용 임무 정보 조회
            string missionTitle = entry != null ? entry.Title : missionId; // 임무 제목 보정
            manager.FailMission("플레이어 사망"); // 공통 MissionManager 실패 상태 적용

            if (!Map32MissionResultScreen.IsOpen) // Day32 패치 전 예외 상황에서도 실패 화면 보장
            {
                Map32MissionResultScreen.ShowFailure(missionId, missionTitle, "플레이어 사망", CurrentLabel); // 체크포인트 재시도 화면 표시
            }
        }

        public static void Capture(string label) // MissionManager에서 현재 상태를 안전 체크포인트로 기록
        {
            EnsureInstance(); // 체크포인트 관리자 존재 보장
            instance.CaptureInternal(label); // 실제 상태 저장
        }

        private void CaptureInternal(string label) // 플레이어·목표·임무 물품 스냅샷 저장
        {
            Map31MissionManager manager = Map31MissionManager.Instance; // 현재 MissionManager 조회
            if (manager == null || string.IsNullOrEmpty(manager.ActiveMissionId)) // 진행 중 임무 확인
            {
                return; // 저장 대상 없음
            }

            ResolveWorld(); // 최신 플레이어 참조 확보
            if (world == null || world.Player == null) // 실제 플레이어 확인
            {
                return; // 저장 중단
            }

            Map31MissionInventory inventory = manager.Inventory; // 현재 임무 물품 슬롯 조회
            current = new Map32MissionCheckpointState // 새 스냅샷 생성
            {
                Valid = true, // 유효 체크포인트 표시
                MissionId = manager.ActiveMissionId, // 현재 임무 ID 저장
                Label = string.IsNullOrWhiteSpace(label) ? "임무 체크포인트" : label, // 표시 이름 저장
                ObjectiveIndex = ReadIntProperty(manager, "ActiveObjectiveIndex", 0), // Day32 확장 속성에서 현재 목표 순번 저장
                PlayerPosition = world.Player.transform.position, // 안전 위치 저장
                PlayerEuler = world.Player.transform.eulerAngles, // 플레이어 방향 저장
                OwnedItems = inventory != null ? InvokeStringArray(inventory, "CaptureOwnedItems") : Array.Empty<string>(), // Day32 확장 API로 보유 임무 물품 저장
                DeliveredItems = inventory != null ? InvokeStringArray(inventory, "CaptureDeliveredItems") : Array.Empty<string>(), // Day32 확장 API로 인계 기록 저장
                CapturedAt = Time.unscaledTime // 세션 시각 저장
            };

            Debug.Log("[Day32] 체크포인트 저장 · " + current.MissionId + " / " + current.Label + " / Objective " + current.ObjectiveIndex); // 개발 확인 로그
        }

        public bool RetryCurrentCheckpoint() // 실패 화면의 재시도 버튼 처리
        {
            if (current == null || !current.Valid || string.IsNullOrEmpty(current.MissionId)) // 체크포인트 유효성 확인
            {
                return false; // 복원 실패
            }

            Map31MissionManager manager = Map31MissionManager.Instance; // 현재 MissionManager 조회
            if (manager == null) // MissionManager 존재 확인
            {
                return false; // 복원 불가
            }

            ResolveWorld(); // 최신 월드·플레이어 참조 확보
            if (world == null || world.Player == null) // 실제 플레이어 확인
            {
                return false; // 복원 실패
            }

            ResetWorldAlertState(); // 수배·진행 중 신고 초기화
            RestorePlayerPoseAndVitals(); // 플레이어 위치·체력·자세 복구

            if (manager.Inventory != null) // 임무 물품 슬롯 확인
            {
                InvokeVoid(manager.Inventory, "RestoreSnapshot", current.OwnedItems, current.DeliveredItems); // Day32 확장 API로 체크포인트 물품 상태 복원
            }

            bool restored = InvokeBool(manager, "RestoreFromCheckpoint", current.MissionId, current.ObjectiveIndex); // Day32 확장 API로 MissionManager 진행 단계 복원

            if (restored) // 임무 상태 정상 복원 확인
            {
                Map31MissionToastHUD.Show("CHECKPOINT RESTORED", current.Label, 1.8f, true); // 재시도 성공 알림
            }

            deathHandled = false; // 다음 사망 감지 허용
            return restored; // 최종 복원 결과 반환
        }

        public bool AbandonCurrentMission() // 실패 화면에서 임무 포기 후 안전 위치로 복귀
        {
            if (current == null || !current.Valid || string.IsNullOrEmpty(current.MissionId)) // 체크포인트 존재 확인
            {
                return false; // 포기 처리 불가
            }

            Map31MissionManager manager = Map31MissionManager.Instance; // MissionManager 조회
            if (manager == null) // 관리자 존재 확인
            {
                return false; // 처리 실패
            }

            ResolveWorld(); // 현재 플레이어 참조 연결
            ResetWorldAlertState(); // 수배·신고 상태 정리
            RestorePlayerPoseAndVitals(); // 플레이어는 마지막 안전 체크포인트 위치로 복구
            InvokeVoid(manager, "AbandonMission", current.MissionId); // Day32 확장 API로 임무를 수락 가능 상태로 되돌림
            current.Valid = false; // 이전 진행 체크포인트 폐기
            deathHandled = false; // 다음 사망 감지 허용
            Map31MissionToastHUD.Show("MISSION ABANDONED", "임무 진행을 초기화했습니다.", 1.7f, false, true); // 포기 알림
            return true; // 포기 완료
        }

        public void ClearCheckpoint(string missionId) // 임무 정상 완료 후 진행 체크포인트 제거
        {
            if (current == null || !current.Valid || current.MissionId != missionId) // 현재 저장과 완료 임무 일치 확인
            {
                return; // 다른 체크포인트 유지
            }

            current.Valid = false; // 완료 임무 재시도 상태 폐기
        }

        private void RestorePlayerPoseAndVitals() // 체크포인트 플레이어 위치·속도·체력 복원
        {
            if (world == null || world.Player == null || current == null || !current.Valid) // 복원 필수 자료 확인
            {
                return; // 복원 생략
            }

            GameObject player = world.Player; // 실제 플레이어 조회
            PlayerMovement movement = player.GetComponent<PlayerMovement>(); // 이동 상태 조회
            CharacterController body = player.GetComponent<CharacterController>(); // 충돌체 조회
            bool bodyEnabled = body != null && body.enabled; // 기존 충돌체 상태 저장

            player.GetComponent<PlayerFirearmController>()?.Interrupt(); // 진행 중 사격·재장전 정리

            if (body != null) // CharacterController 존재 확인
            {
                body.enabled = false; // 순간 이동 중 물리 보정 잠시 중지
            }

            player.transform.SetPositionAndRotation(current.PlayerPosition, Quaternion.Euler(current.PlayerEuler)); // 안전 체크포인트 위치·회전 적용
            movement?.SetHorizontalVelocity(Vector3.zero); // 수평 관성 초기화

            if (movement != null) // 이동 시스템 확인
            {
                movement.VerticalVelocity = 0f; // 낙하 속도 초기화
                movement.SetSpawnPoint(current.PlayerPosition, Quaternion.Euler(current.PlayerEuler)); // 이후 추락 복귀도 현재 체크포인트 사용
            }

            if (body != null) // 충돌체 원상 복구
            {
                body.enabled = bodyEnabled; // 이전 활성 상태 복구
            }

            playerHealth = player.GetComponent<PlayerHealth>(); // 최신 체력 참조 조회
            if (playerHealth != null) InvokeVoid(playerHealth, "RestoreFromCheckpoint"); // Day32 확장 API로 체력·자세·사망 잠금 해제
            Physics.SyncTransforms(); // 순간 이동을 물리 시스템에 즉시 반영
        }

        private void ResetWorldAlertState() // 재시도 직후 이전 수배·신고 상태 제거
        {
            if (MapWantedSystem.Instance != null) InvokeVoid(MapWantedSystem.Instance, "ResetForCheckpoint"); // Day32 확장 API로 Heat·별·공유 위치 초기화
            if (Map26CrimeReportSystem.Instance != null) InvokeVoid(Map26CrimeReportSystem.Instance, "ClearForCheckpoint"); // Day32 확장 API로 진행 중 사건·신고 초기화
        }

        private void ResolveWorld() // 현재 MapWorldRoot와 PlayerHealth 검색
        {
            MapWantedSystem wanted = MapWantedSystem.Instance; // 기존 수배 시스템 월드 참조 우선 사용
            world = wanted != null ? wanted.World : null; // 현재 월드 연결

            if (world == null) // 수배 시스템이 아직 준비되지 않은지 확인
            {
                MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 본편 월드 대체 검색
                world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
            }

            playerHealth = world != null && world.Player != null ? world.Player.GetComponent<PlayerHealth>() : null; // 현재 플레이어 체력 연결
        }

        private static int ReadIntProperty(object target, string propertyName, int fallback) // Day32 패처가 추가한 정수 속성 안전 조회
        {
            if (target == null) // 대상 존재 확인
            {
                return fallback; // 기본값 반환
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public); // 공개 속성 조회
            if (property == null || property.PropertyType != typeof(int)) // 속성 존재·형식 확인
            {
                return fallback; // 패치 전 기본값 반환
            }

            object value = property.GetValue(target); // 실제 속성 값 조회
            return value is int number ? number : fallback; // 정수 값 또는 기본값 반환
        }

        private static string[] InvokeStringArray(object target, string methodName) // 문자열 배열 반환 확장 API 안전 호출
        {
            if (target == null) // 대상 확인
            {
                return Array.Empty<string>(); // 빈 배열 반환
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public); // 공개 메서드 조회
            if (method == null) // Day32 패치 전 상태 확인
            {
                return Array.Empty<string>(); // 빈 배열 반환
            }

            object result = method.Invoke(target, null); // 확장 API 호출
            return result as string[] ?? Array.Empty<string>(); // 안전한 문자열 배열 반환
        }

        private static bool InvokeBool(object target, string methodName, params object[] args) // bool 반환 확장 API 안전 호출
        {
            if (target == null) // 대상 확인
            {
                return false; // 호출 실패
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public); // 공개 메서드 조회
            if (method == null) // 패치 적용 여부 확인
            {
                return false; // 호출 실패
            }

            object result = method.Invoke(target, args); // 확장 API 호출
            return result is bool value && value; // bool 결과 반환
        }

        private static void InvokeVoid(object target, string methodName, params object[] args) // void 확장 API 안전 호출
        {
            if (target == null) // 대상 확인
            {
                return; // 호출 생략
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public); // 공개 메서드 조회
            method?.Invoke(target, args); // 패치된 경우에만 호출
        }

        private static void EnsureInstance() // 외부 Capture 호출 시 관리자 자동 생성
        {
            if (instance != null) // 기존 관리자 확인
            {
                return; // 생성 생략
            }

            GameObject owner = new GameObject("[Day32] Mission Checkpoint"); // 체크포인트 관리자 오브젝트 생성
            instance = owner.AddComponent<Map32MissionCheckpointSystem>(); // 관리자 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
