using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // Heat·수배 UI와 시야 검사

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 수배 관리자 중복 방지
    public sealed class MapWantedSystem : MonoBehaviour // Heat·별·마지막 목격 위치·감소 타이머 관리자
    {
        [SerializeField] private MapWorldRoot world; // 본편 월드 참조
        [SerializeField] private string sourceCommit; // 제작 기준 커밋
        [SerializeField] private float heat; // 현재 범죄 Heat
        [SerializeField] private float decayPerSecond = 4f; // 수배 감소 속도
        private const float SharedAwarenessSeconds = 5f; // 한 경비가 발견한 플레이어 위치의 전체 공유 시간
        private int stars; // 현재 별 단계
        private bool currentlySeen; // 경비·카메라에 현재 보이는지 여부
        private Vector3 lastKnownPosition; // 마지막으로 확인된 플레이어 위치
        private float lastCrimeTime = float.NegativeInfinity; // 마지막 범죄 시각
        private float lastSeenTime = float.NegativeInfinity; // 마지막 직접 목격 시각
        private float decayGateUntil; // 다음 Heat 감소 가능 시각
        private float nextPerceptionCheck; // 다음 센서 검사 시각
        private Vector3 sharedTargetPosition; // 경비 전체가 잠시 공유하는 정확한 발견 위치
        private float sharedTargetUntil = float.NegativeInfinity; // 공유 위치 만료 시각
        public static MapWantedSystem Instance { get; private set; } // 현재 Map의 단일 수배 관리자
        public MapWorldRoot World => world; // 본편 월드 조회
        public GameObject Player => world != null ? world.Player : null; // 실제 플레이어 조회
        public string SourceCommit => sourceCommit; // 기준 커밋 조회
        public float Heat => heat; // 현재 Heat 조회
        public int Stars => stars; // 현재 별 단계 조회
        public bool CurrentlySeen => currentlySeen; // 현재 직접 발각 여부 조회
        public Vector3 LastKnownPosition => lastKnownPosition; // 마지막 확인 위치 조회
        public bool HasSharedTarget => stars > 0 && Time.time <= sharedTargetUntil; // 현재 전체 경비가 정확한 위치를 공유하는지 조회
        public Vector3 SharedTargetPosition => sharedTargetPosition; // 현재 공유 중인 정확한 플레이어 위치 조회
        public float SharedAwarenessDuration => SharedAwarenessSeconds; // 검사 메뉴용 위치 공유 시간 조회
        public float PursuitRadius => MapWantedRules.PursuitRadius(stars); // 현재 추적 반경 조회
        public int GuardTargetCount => MapWantedRules.GuardTargetCount(stars); // 현재 추적 병력 목표 조회

        public void Configure(MapWorldRoot owner, string commit) // 에디터에서 본편 월드 연결
        {
            world = owner; // 본편 월드 저장
            sourceCommit = commit; // 기준 커밋 저장
            lastKnownPosition = world != null && world.Player != null ? world.Player.transform.position : Vector3.zero; // 초기 마지막 위치 저장
            sharedTargetPosition = lastKnownPosition; // 최초 공유 위치도 현재 플레이어 위치로 정렬
            sharedTargetUntil = float.NegativeInfinity; // 수배 전에는 위치 공유 비활성화
        }

        private void Awake() // 단일 수배 관리자 등록
        {
            Instance = this; // 현재 인스턴스 등록
            if (world == null) // 직렬화 참조 누락 확인
            {
                world = GetComponentInParent<MapWorldRoot>(); // 부모 월드에서 자동 복구
            }
            stars = MapWantedRules.StarsForHeat(heat); // 저장 Heat에서 별 단계 복원
        }

        private void OnEnable() // 총성 범죄 이벤트 구독
        {
            Instance = this; // 재활성화 인스턴스 갱신
            NoiseSystem.NoiseEmitted += HandleNoise; // 기존 총성 시스템 연결
        }

        private void OnDisable() // 정적 이벤트 연결 해제
        {
            NoiseSystem.NoiseEmitted -= HandleNoise; // 총성 이벤트 구독 해제
            if (Instance == this) // 현재 인스턴스 여부 확인
            {
                Instance = null; // 정적 참조 해제
            }
        }

        private void Update() // 발각 검사와 Heat 감소 처리
        {
            if (world == null || world.Player == null) // 필수 월드 확인
            {
                return; // 수배 처리 중단
            }
            if (Time.unscaledTime >= nextPerceptionCheck) // 센서 검사 시점 확인
            {
                nextPerceptionCheck = Time.unscaledTime + 0.35f; // 빈번한 전체 센서 검색 제한
                UpdatePerception(); // 경비·카메라 시야 확인
            }
            if (stars <= 0 || currentlySeen || HasSharedTarget) // 수배 없음·직접 추격·공유 추적 중 확인
            {
                return; // Heat 감소 중단
            }
            float delay = MapWantedRules.DecayDelayForStars(stars); // 현재 단계 미발각 대기 조회
            float latestEvent = Mathf.Max(lastCrimeTime, lastSeenTime); // 마지막 범죄·목격 중 최근 시각 계산
            if (Time.time < decayGateUntil || Time.time - latestEvent < delay) // 감소 조건 대기 확인
            {
                return; // 아직 Heat 유지
            }
            int previousStars = stars; // 별 감소 감지용 이전 단계 저장
            heat = Mathf.MoveTowards(heat, 0f, decayPerSecond * Time.deltaTime); // 새 범죄가 없으면 Heat 점진 감소
            stars = MapWantedRules.StarsForHeat(heat); // 감소된 Heat에서 별 단계 재계산
            if (stars < previousStars) // 실제 별 하나가 내려갔는지 확인
            {
                decayGateUntil = Time.time + MapWantedRules.DecayDelayForStars(stars); // 다음 단계 감소 전 다시 숨는 시간 부여
            }
        }

        public bool ReportCrime(CrimeType type, Vector3 position, GameObject instigator, bool loud, bool forceReported = false) // 플레이어 범죄 Heat 반영
        {
            if (world == null || world.Player == null || !MapCrimeWitness.IsPlayerInstigator(instigator, world.Player)) // 플레이어가 일으킨 사건인지 확인
            {
                return false; // NPC 간 사건은 플레이어 수배에 반영하지 않음
            }
            bool reported = forceReported || MapCrimeWitness.IsReported(position, instigator, loud); // 목격·청취·강제 신고 여부 계산
            if (!reported) // 스텔스 범죄 신고 여부 확인
            {
                return false; // Heat를 올리지 않고 은폐 성공
            }
            heat = Mathf.Clamp(heat + MapWantedRules.HeatForCrime(type), 0f, 220f); // 범죄 종류별 Heat 누적
            int previous = stars; // 이전 별 단계 저장
            stars = MapWantedRules.StarsForHeat(heat); // 새 별 단계 계산
            lastCrimeTime = Time.time; // 범죄 시각 갱신
            lastKnownPosition = position; // 신고된 범죄 장소를 마지막 위치로 사용
            decayGateUntil = Time.time + MapWantedRules.DecayDelayForStars(stars); // 즉시 Heat 감소 방지
            if (stars > previous) // 수배 단계 상승 확인
            {
                Debug.Log("수배 단계 상승: " + stars + " / Heat " + heat.ToString("0")); // 단계 상승 로그 출력
            }
            return true; // 신고된 범죄 처리 완료
        }

        public void ForceSeen(Vector3 playerPosition) // 기존 호출 호환용 직접 발견 처리
        {
            BroadcastDetection(playerPosition); // 한 경비의 발견을 전체 수배 경비에 공유
        }

        public void BroadcastDetection(Vector3 playerPosition) // 한 경비가 발견하면 전체가 5초 동안 정확한 위치를 공유
        {
            if (stars <= 0) // 수배 상태 여부 확인
            {
                return; // 평상시 위치 공유 생략
            }
            currentlySeen = true; // 이번 프레임 직접 목격 상태 적용
            lastSeenTime = Time.time; // 직접 발견 시각 갱신
            lastKnownPosition = playerPosition; // 수색 중심으로 사용할 마지막 확인 위치 갱신
            sharedTargetPosition = playerPosition; // 모든 수배 경비가 추적할 정확한 위치 저장
            sharedTargetUntil = Time.time + SharedAwarenessSeconds; // 정확한 위치 공유를 5초 동안 유지
            decayGateUntil = Time.time + MapWantedRules.DecayDelayForStars(stars); // 공유 추적 중 Heat 감소 방지
        }

        private void UpdatePerception() // 기존 DetectionSensor를 이용한 직접 발각 검사
        {
            currentlySeen = false; // 이번 검사 기본 미발각 상태
            DetectionSensor[] sensors = Object.FindObjectsByType<DetectionSensor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 경비·카메라 센서 조회
            foreach (DetectionSensor sensor in sensors) // 모든 보안 센서 순회
            {
                if (sensor == null || sensor.Target != world.Player.transform) // 플레이어를 감시하지 않는 센서 제외
                {
                    continue; // 다음 센서 검사
                }
                if (sensor.TargetVisible) // 실제 플레이어 시야 확인
                {
                    BroadcastDetection(world.Player.transform.position); // 한 센서의 발견을 전체 경비에게 5초간 공유
                    return; // 하나의 센서만 봐도 현재 발각으로 처리
                }
            }
        }

        private void HandleNoise(NoiseEvent noise) // 플레이어 총성 자체의 범죄 처리
        {
            if (noise.Type != NoiseType.Gunshot || world == null || world.Player == null) // 총성·월드 유효성 확인
            {
                return; // 다른 소음 제외
            }
            if (!MapCrimeWitness.IsPlayerInstigator(noise.Source, world.Player)) // 플레이어 총성 여부 확인
            {
                return; // NPC 소음 제외
            }
            ReportCrime(CrimeType.Gunfire, noise.Position, noise.Source, true, false); // 목격·청취된 총격 Heat 반영
        }

        private void OnGUI() // GTA식 별 수배도 HUD 표시
        {
            if (stars <= 0) // 평상시 수배 HUD 숨김
            {
                return; // 별 표시 생략
            }
            string display = string.Empty; // 별 문자열 초기화
            for (int i = 0; i < MapWantedRules.MaximumStars; i++) // 다섯 칸 별 표시 생성
            {
                display += i < stars ? "★" : "☆"; // 현재 단계까지만 채운 별 사용
            }
            GUIStyle style = new GUIStyle(GUI.skin.box); // 수배 HUD 스타일 생성
            style.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 38f), 20, 34); // 해상도 기반 별 크기 설정
            style.fontStyle = FontStyle.Bold; // 별 강조
            style.alignment = TextAnchor.MiddleCenter; // 화면 상단 중앙 정렬
            style.normal.textColor = currentlySeen || HasSharedTarget ? new Color(1f, 0.18f, 0.12f) : new Color(1f, 0.62f, 0.10f); // 직접·공유 추적 중 빨강, 분산 수색 중 주황 표시
            Rect rect = new Rect(Screen.width * 0.5f - 150f, 18f, 300f, 48f); // 화면 상단 중앙 위치
            GUI.Box(rect, display, style); // 별 수배도 출력
        }
    }
}
