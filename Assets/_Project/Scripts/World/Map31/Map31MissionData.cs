using System; // 미션 데이터 직렬화
using System.Collections.Generic; // 목표·실패 조건 목록
using UnityEngine; // ScriptableObject 기반 임무 정의

namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    public enum Map31MissionObjectiveType // 공통 목표 유형
    {
        Reach, // 지정 위치 도착
        Interact, // 지정 오브젝트 상호작용
        Acquire, // 임무 물품 획득
        Deliver, // 임무 물품 인계
        Eliminate, // 지정 표적 제거
        Investigate, // 조사 대상 확인
        Escape // 지정 탈출 지점 도착
    }

    public enum Map31MissionRuntimeStatus // 실제 MissionManager 진행 상태
    {
        Available, // 수락 가능
        Active, // 진행 중
        Completed, // 완료
        Failed // 실패
    }

    [Serializable] // ScriptableObject 내부 목표 직렬화
    public sealed class Map31MissionObjectiveData // 한 단계 목표 정의
    {
        [SerializeField] private string objectiveId; // 목표 고정 ID
        [SerializeField] private Map31MissionObjectiveType type; // 목표 유형
        [SerializeField] private string description; // 플레이어 표시 문구
        [SerializeField] private string targetPlaceId; // 기존 MapPoint 목표 ID
        [SerializeField] private string runtimeTargetKey; // 런타임 생성 목표 Transform 키
        [SerializeField] private string targetLabel; // 단말기 목표 이름
        [SerializeField] private float completionRadius = 8f; // 도착 목표 완료 반경
        [SerializeField] private string grantItemId; // 완료 시 지급할 임무 물품
        [SerializeField] private string requiredItemId; // 완료에 필요한 임무 물품
        [SerializeField] private string deliverItemId; // 완료 시 인계 처리할 임무 물품

        public string ObjectiveId => objectiveId; // 목표 ID 조회
        public Map31MissionObjectiveType Type => type; // 유형 조회
        public string Description => description; // 목표 문구 조회
        public string TargetPlaceId => targetPlaceId; // MapPoint 목표 ID 조회
        public string RuntimeTargetKey => runtimeTargetKey; // 런타임 목표 키 조회
        public string TargetLabel => targetLabel; // 목표 이름 조회
        public float CompletionRadius => completionRadius; // 완료 반경 조회
        public string GrantItemId => grantItemId; // 지급 물품 조회
        public string RequiredItemId => requiredItemId; // 필요 물품 조회
        public string DeliverItemId => deliverItemId; // 인계 물품 조회

        public Map31MissionObjectiveData(string id, Map31MissionObjectiveType objectiveType, string text, string placeId, string runtimeKey, string label, float radius = 8f, string grantItem = null, string requiredItem = null, string deliverItem = null) // 런타임 미션 정의용 생성자
        {
            objectiveId = Safe(id, "OBJECTIVE"); // 목표 ID 저장
            type = objectiveType; // 목표 유형 저장
            description = Safe(text, "목표를 수행하십시오."); // 표시 문구 저장
            targetPlaceId = placeId ?? string.Empty; // 장소 ID 저장
            runtimeTargetKey = runtimeKey ?? string.Empty; // 런타임 목표 키 저장
            targetLabel = Safe(label, "목표 지점"); // 목표 이름 저장
            completionRadius = Mathf.Max(0.5f, radius); // 완료 반경 안전 보정
            grantItemId = grantItem ?? string.Empty; // 지급 물품 저장
            requiredItemId = requiredItem ?? string.Empty; // 필요 물품 저장
            deliverItemId = deliverItem ?? string.Empty; // 인계 물품 저장
        }

        private static string Safe(string value, string fallback) // 빈 문자열 보정
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value; // 유효 문자열 또는 대체값 반환
        }
    }

    [CreateAssetMenu(menuName = "Project K/Mission Definition", fileName = "MissionData")] // 이후 실제 MissionData 에셋 제작용 메뉴
    public sealed class Map31MissionDefinition : ScriptableObject // 공통 임무 정의 데이터
    {
        [SerializeField] private string missionId; // 임무 고정 ID
        [SerializeField] private string title; // 임무 제목
        [SerializeField] private string typeLabel; // 임무 유형
        [SerializeField] private string client; // 의뢰인
        [SerializeField] private string region; // 주요 구역
        [SerializeField] private string summary; // 임무 설명
        [SerializeField] private string reward; // 보상 설명
        [SerializeField] private string unlockCondition; // 해금 조건 설명
        [SerializeField] private string nextMissionId; // 후속 임무 ID
        [SerializeField] private ProjectK.Day30.Map30MissionCategory category; // 메인·서브 분류
        [SerializeField] private List<string> failureConditions = new List<string>(); // 실패 조건 설명
        [SerializeField] private List<Map31MissionObjectiveData> objectives = new List<Map31MissionObjectiveData>(); // 필수 목표 단계

        public string MissionId => missionId; // 임무 ID 조회
        public string Title => title; // 제목 조회
        public string TypeLabel => typeLabel; // 유형 조회
        public string Client => client; // 의뢰인 조회
        public string Region => region; // 지역 조회
        public string Summary => summary; // 설명 조회
        public string Reward => reward; // 보상 조회
        public string UnlockCondition => unlockCondition; // 해금 조건 조회
        public string NextMissionId => nextMissionId; // 후속 임무 조회
        public ProjectK.Day30.Map30MissionCategory Category => category; // 분류 조회
        public IReadOnlyList<string> FailureConditions => failureConditions; // 실패 조건 조회
        public IReadOnlyList<Map31MissionObjectiveData> Objectives => objectives; // 목표 목록 조회

        public void ConfigureRuntime(string id, string missionTitle, string missionType, string missionClient, string missionRegion, string description, string missionReward, string unlock, string nextMission, ProjectK.Day30.Map30MissionCategory missionCategory, IEnumerable<string> failures, IEnumerable<Map31MissionObjectiveData> missionObjectives) // 자동 테스트 임무 런타임 구성
        {
            missionId = id; // 임무 ID 저장
            title = missionTitle; // 제목 저장
            typeLabel = missionType; // 유형 저장
            client = missionClient; // 의뢰인 저장
            region = missionRegion; // 지역 저장
            summary = description; // 설명 저장
            reward = missionReward; // 보상 저장
            unlockCondition = unlock; // 해금 조건 저장
            nextMissionId = nextMission; // 후속 임무 저장
            category = missionCategory; // 분류 저장
            failureConditions = failures != null ? new List<string>(failures) : new List<string>(); // 실패 조건 복사
            objectives = missionObjectives != null ? new List<Map31MissionObjectiveData>(missionObjectives) : new List<Map31MissionObjectiveData>(); // 목표 목록 복사
        }
    }

    public sealed class Map31MissionRuntimeState // 현재 플레이 세션의 단일 임무 진행 상태
    {
        public Map31MissionDefinition Definition; // 원본 임무 정의
        public Map31MissionRuntimeStatus Status; // 현재 상태
        public int ObjectiveIndex; // 현재 목표 순번

        public Map31MissionObjectiveData CurrentObjective // 현재 목표 조회
        {
            get
            {
                if (Definition == null || Definition.Objectives == null || ObjectiveIndex < 0 || ObjectiveIndex >= Definition.Objectives.Count) // 유효 목표 범위 확인
                {
                    return null; // 현재 목표 없음
                }

                return Definition.Objectives[ObjectiveIndex]; // 현재 목표 반환
            }
        }
    }
}
