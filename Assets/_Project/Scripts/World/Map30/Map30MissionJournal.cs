using System; // 직렬화 가능한 임무 자료
using System.Collections.Generic; // 임무 목록 관리
using UnityEngine; // 런타임 Journal 자동 생성

namespace ProjectK.Day30 // 30일차 전체 임무 창 이름 공간
{
    public enum Map30MissionCategory // 임무 분류
    {
        Main, // 메인 임무
        Side // 서브 의뢰
    }

    public enum Map30MissionStatus // 임무 진행 상태
    {
        Available, // 보유·수락 가능
        Tracking, // 현재 추적 중
        Completed, // 완료
        Failed // 실패
    }

    [Serializable] // 향후 저장 시스템 연결용 직렬화 자료
    public sealed class Map30MissionEntry // 전체 임무 창에 표시할 단일 임무 자료
    {
        [SerializeField] private string missionId; // 임무 고정 ID
        [SerializeField] private string title; // 임무 제목
        [SerializeField] private string typeLabel; // 배달·조사·암살 등 유형
        [SerializeField] private string client; // 의뢰인
        [SerializeField] private string region; // 주요 지역
        [SerializeField] private string summary; // 임무 설명
        [SerializeField] private string reward; // 보상 설명
        [SerializeField] private Map30MissionCategory category; // 메인·서브 분류
        [SerializeField] private Map30MissionStatus status; // 현재 진행 상태
        [SerializeField] private List<string> objectives = new List<string>(); // 목표 단계 목록
        [SerializeField] private int currentObjectiveIndex; // 현재 목표 순번
        [SerializeField] private bool developmentSeed; // MissionManager 전 임시 보유 임무 여부

        public string MissionId => missionId; // 임무 ID 조회
        public string Title => title; // 제목 조회
        public string TypeLabel => typeLabel; // 유형 조회
        public string Client => client; // 의뢰인 조회
        public string Region => region; // 지역 조회
        public string Summary => summary; // 설명 조회
        public string Reward => reward; // 보상 조회
        public Map30MissionCategory Category => category; // 분류 조회
        public Map30MissionStatus Status => status; // 상태 조회
        public IReadOnlyList<string> Objectives => objectives; // 목표 목록 조회
        public int CurrentObjectiveIndex => currentObjectiveIndex; // 현재 목표 순번 조회
        public bool DevelopmentSeed => developmentSeed; // 임시 데이터 여부 조회

        public Map30MissionEntry(string id, string missionTitle, string missionType, string missionClient, string missionRegion, string description, string missionReward, Map30MissionCategory missionCategory, Map30MissionStatus missionStatus, IEnumerable<string> missionObjectives, int currentIndex, bool isDevelopmentSeed) // 임무 자료 생성
        {
            missionId = Safe(id, "MISSION"); // ID 저장
            title = Safe(missionTitle, "임무"); // 제목 저장
            typeLabel = Safe(missionType, "기타"); // 유형 저장
            client = Safe(missionClient, "---"); // 의뢰인 저장
            region = Safe(missionRegion, "---"); // 지역 저장
            summary = Safe(description, "상세 정보가 없습니다."); // 설명 저장
            reward = Safe(missionReward, "---"); // 보상 저장
            category = missionCategory; // 분류 저장
            status = missionStatus; // 상태 저장
            objectives = missionObjectives != null ? new List<string>(missionObjectives) : new List<string>(); // 목표 목록 복사
            currentObjectiveIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, objectives.Count - 1)); // 현재 목표 순번 보정
            developmentSeed = isDevelopmentSeed; // 임시 데이터 여부 저장
        }

        public void UpdateStatus(Map30MissionStatus newStatus) // 진행 상태 갱신
        {
            status = newStatus; // 새 상태 저장
        }

        public void UpdateCurrentObjective(int index) // 현재 목표 순번 갱신
        {
            currentObjectiveIndex = objectives.Count == 0 ? 0 : Mathf.Clamp(index, 0, objectives.Count - 1); // 목표 범위 안으로 보정
        }

        public void ReplaceObjectives(IEnumerable<string> newObjectives, int currentIndex) // 목표 단계 전체 갱신
        {
            objectives = newObjectives != null ? new List<string>(newObjectives) : new List<string>(); // 새 목표 목록 저장
            currentObjectiveIndex = objectives.Count == 0 ? 0 : Mathf.Clamp(currentIndex, 0, objectives.Count - 1); // 현재 목표 순번 보정
        }

        public void ReplaceContent(string missionTitle, string missionType, string missionClient, string missionRegion, string description, string missionReward, Map30MissionCategory missionCategory, bool isDevelopmentSeed) // MissionManager에서 동일 ID의 실제 데이터로 교체
        {
            title = Safe(missionTitle, title); // 제목 갱신
            typeLabel = Safe(missionType, typeLabel); // 유형 갱신
            client = Safe(missionClient, client); // 의뢰인 갱신
            region = Safe(missionRegion, region); // 지역 갱신
            summary = Safe(description, summary); // 설명 갱신
            reward = Safe(missionReward, reward); // 보상 갱신
            category = missionCategory; // 분류 갱신
            developmentSeed = isDevelopmentSeed; // 임시 여부 갱신
        }

        private static string Safe(string value, string fallback) // 빈 문자열 방지
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value; // 유효 문자열 또는 대체값 반환
        }
    }

    [DisallowMultipleComponent] // 임무 Journal 중복 방지
    public sealed class Map30MissionJournal : MonoBehaviour // 플레이어가 보유한 모든 임무를 한 곳에서 관리
    {
        private static Map30MissionJournal instance; // 현재 Journal 인스턴스
        private readonly List<Map30MissionEntry> missions = new List<Map30MissionEntry>(16); // 현재 보유 임무 목록
        private string selectedMissionId; // 임무 창 마지막 선택 ID

        public static Map30MissionJournal Instance => instance; // 현재 Journal 조회
        public IReadOnlyList<Map30MissionEntry> Missions => missions; // 보유 임무 목록 조회
        public string SelectedMissionId => selectedMissionId; // 현재 선택 임무 ID 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 Journal 참조 제거
        }

        private void Awake() // 단일 Journal 등록
        {
            if (instance != null && instance != this) // 다른 Journal 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 Journal 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // MissionManager 전 초기 보유 임무 자료 준비
        {
            SeedVerticalSliceMissions(); // 첫 완성 구간 M-01·M-02·S-01 UI 시험 데이터 추가
        }

        public Map30MissionEntry Find(string missionId) // ID로 보유 임무 검색
        {
            if (string.IsNullOrWhiteSpace(missionId)) // 잘못된 ID 확인
            {
                return null; // 검색 실패
            }

            for (int i = 0; i < missions.Count; i++) // 보유 임무 순회
            {
                Map30MissionEntry entry = missions[i]; // 현재 임무 조회
                if (entry != null && entry.MissionId == missionId) // ID 일치 확인
                {
                    return entry; // 임무 반환
                }
            }

            return null; // 해당 임무 없음
        }

        public void SelectMission(string missionId) // 임무 창 선택 상태 저장
        {
            Map30MissionEntry entry = Find(missionId); // 실제 보유 임무 확인
            selectedMissionId = entry != null ? entry.MissionId : string.Empty; // 유효 ID만 저장
        }

        public Map30MissionEntry SelectedMission() // 현재 선택 임무 조회
        {
            Map30MissionEntry selected = Find(selectedMissionId); // 저장 ID 검색
            if (selected != null) // 기존 선택 유지 가능 확인
            {
                return selected; // 현재 선택 반환
            }

            if (missions.Count > 0) // 보유 임무 존재 확인
            {
                selectedMissionId = missions[0].MissionId; // 첫 임무 자동 선택
                return missions[0]; // 첫 임무 반환
            }

            return null; // 보유 임무 없음
        }

        public static void AddOrUpdateMission(string id, string title, string type, string client, string region, string summary, string reward, Map30MissionCategory category, Map30MissionStatus status, IEnumerable<string> objectives, int currentObjectiveIndex = 0) // 향후 MissionManager가 실제 임무를 등록하는 API
        {
            EnsureInstance(); // Journal 존재 보장
            Map30MissionEntry existing = instance.Find(id); // 동일 ID 보유 임무 검색

            if (existing == null) // 신규 임무 확인
            {
                Map30MissionEntry created = new Map30MissionEntry(id, title, type, client, region, summary, reward, category, status, objectives, currentObjectiveIndex, false); // 실제 임무 자료 생성
                instance.missions.Add(created); // 보유 목록 추가

                if (string.IsNullOrEmpty(instance.selectedMissionId)) // 첫 임무인지 확인
                {
                    instance.selectedMissionId = created.MissionId; // 신규 임무 자동 선택
                }

                return; // 신규 등록 완료
            }

            existing.ReplaceContent(title, type, client, region, summary, reward, category, false); // 임시·기존 설명을 실제 데이터로 갱신
            existing.ReplaceObjectives(objectives, currentObjectiveIndex); // 목표 목록 갱신
            existing.UpdateStatus(status); // 진행 상태 갱신
        }

        public static void SetMissionStatus(string missionId, Map30MissionStatus status) // 외부 시스템에서 진행 상태 갱신
        {
            if (instance == null) // Journal 존재 확인
            {
                return; // 갱신 생략
            }

            Map30MissionEntry entry = instance.Find(missionId); // 대상 임무 검색
            entry?.UpdateStatus(status); // 유효 임무 상태 변경
        }

        public static void SetCurrentObjective(string missionId, int objectiveIndex) // 외부 시스템에서 현재 목표 순번 갱신
        {
            if (instance == null) // Journal 존재 확인
            {
                return; // 갱신 생략
            }

            Map30MissionEntry entry = instance.Find(missionId); // 대상 임무 검색
            entry?.UpdateCurrentObjective(objectiveIndex); // 현재 목표 순번 변경
        }

        public static void RemoveMission(string missionId) // 포기·삭제된 보유 임무 제거
        {
            if (instance == null || string.IsNullOrWhiteSpace(missionId)) // Journal과 ID 확인
            {
                return; // 제거 생략
            }

            for (int i = instance.missions.Count - 1; i >= 0; i--) // 역순 목록 순회
            {
                Map30MissionEntry entry = instance.missions[i]; // 현재 임무 조회
                if (entry == null || entry.MissionId != missionId) // 대상 ID 확인
                {
                    continue; // 다음 항목 처리
                }

                instance.missions.RemoveAt(i); // 보유 임무 제거
                break; // 하나의 고정 ID만 제거
            }

            if (instance.selectedMissionId == missionId) // 제거된 임무가 선택 상태인지 확인
            {
                instance.selectedMissionId = instance.missions.Count > 0 ? instance.missions[0].MissionId : string.Empty; // 다음 선택 보정
            }
        }

        public static void ClearDevelopmentSeeds() // 실제 MissionManager 구축 후 임시 M-01·M-02·S-01 자료 제거
        {
            if (instance == null) // Journal 존재 확인
            {
                return; // 처리 생략
            }

            for (int i = instance.missions.Count - 1; i >= 0; i--) // 역순 임무 순회
            {
                Map30MissionEntry entry = instance.missions[i]; // 현재 임무 조회
                if (entry != null && entry.DevelopmentSeed) // 개발용 임시 데이터 확인
                {
                    instance.missions.RemoveAt(i); // 임시 데이터 제거
                }
            }

            instance.selectedMissionId = instance.missions.Count > 0 ? instance.missions[0].MissionId : string.Empty; // 선택 상태 보정
        }

        private void SeedVerticalSliceMissions() // 공통 MissionManager 전 UI를 확인할 첫 완성 구간 임무 추가
        {
            if (missions.Count > 0) // 이미 실제 임무가 등록됐는지 확인
            {
                return; // 임시 데이터 추가 생략
            }

            missions.Add(new Map30MissionEntry(
                "M-01",
                "남겨진 주소 · 운송 기록",
                "배달 · 조사",
                "린",
                "겹길",
                "현재 의뢰와 과거의 운송 번호가 연결되는지 확인하기 위해 겹길의 운송 기록과 화물 흔적을 추적한다.",
                "스토리 진행 · 백야 인증 조각",
                Map30MissionCategory.Main,
                Map30MissionStatus.Available,
                new[]
                {
                    "린의 의뢰 내용을 확인한다.",
                    "겹길의 운송 기록을 추적한다.",
                    "백야와 연결된 단서를 확보한다.",
                    "안전하게 거점으로 복귀한다."
                },
                0,
                true)); // M-01 임시 자료

            missions.Add(new Map30MissionEntry(
                "M-02",
                "남겨진 주소 · 추적",
                "조사 · 잠입 암살",
                "린",
                "겹길",
                "M-01에서 확보한 운송 기록을 바탕으로 서하의 행방과 암호화 장부를 추적하고 겹길의 다른 접근 경로를 사용한다.",
                "스토리 진행 · 암호화 장부",
                Map30MissionCategory.Main,
                Map30MissionStatus.Available,
                new[]
                {
                    "운송 기록의 후속 좌표를 확인한다.",
                    "겹길의 감시 구역에 진입한다.",
                    "관련 기록과 표적 정보를 확보한다.",
                    "분석 가능한 자료를 린에게 전달한다."
                },
                0,
                true)); // M-02 임시 자료

            missions.Add(new Map30MissionEntry(
                "S-01",
                "틈 · 초기 의뢰",
                "서브 · 배달",
                "틈",
                "겹길",
                "단말기 앱 ‘틈’을 통해 전달된 초기 서브 의뢰다. 시민 생활과 도시의 계약 구조를 보여 주는 짧은 배달 흐름을 시험한다.",
                "크레딧 · 선택 보상",
                Map30MissionCategory.Side,
                Map30MissionStatus.Available,
                new[]
                {
                    "의뢰 조건을 확인한다.",
                    "지정 화물을 수령한다.",
                    "목표 지점까지 화물을 운반한다.",
                    "의뢰인에게 화물을 인계한다."
                },
                0,
                true)); // S-01 임시 자료

            selectedMissionId = missions[0].MissionId; // 첫 메인 임무 기본 선택
        }

        private static void EnsureInstance() // 외부 API 호출 시 Journal 자동 생성
        {
            if (instance != null) // 기존 Journal 확인
            {
                return; // 생성 생략
            }

            GameObject owner = new GameObject("[Day30] Mission Journal"); // Journal 오브젝트 생성
            instance = owner.AddComponent<Map30MissionJournal>(); // Journal 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }
    }
}
