using ProjectK.Day21; // 수배 시스템과 병력 규칙 참조
using ProjectK.Day24; // 지상·지하 층 판정 참조
using UnityEngine; // 공중 이동과 감시 처리

namespace ProjectK.Day25 // 25일차 수배 병력 차별화 이름 공간
{
    [DisallowMultipleComponent] // 감시 드론 AI 중복 방지
    [RequireComponent(typeof(EnemyActor))] // 드론 내구도 필수
    [RequireComponent(typeof(DetectionSensor))] // 드론 시야 센서 필수
    [RequireComponent(typeof(EnemyStatusController))] // EMP·마비 상태 필수
    public sealed class Map25SurveillanceDrone : MonoBehaviour // E-04 공중 감시·위치 공유 드론
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f; // 기획 비행 속도
        [SerializeField, Min(1f)] private float hoverHeight = 14f; // 지상 기준 기본 비행 높이
        [SerializeField, Min(1f)] private float orbitRadius = 20f; // 플레이어 주변 감시 반경
        [SerializeField, Min(1f)] private float informationShareDistance = 25f; // 기획 정보 공유 거리
        [SerializeField, Min(0.1f)] private float recycleDelay = 10f; // 파괴 후 풀 복귀 대기
        private EnemyActor actor; // 드론 내구도 관리자
        private DetectionSensor sensor; // 드론 시야 센서
        private EnemyStatusController status; // EMP·마비 상태
        private MapGuardCrimeTag crimeTag; // 법 집행 드론 범죄 태그
        private int droneIndex; // 드론별 궤도 분산 번호
        private float deadAt = float.PositiveInfinity; // 파괴 시각
        private float orbitPhase; // 드론별 초기 궤도 각도

        public bool IsDead => actor != null && actor.IsDead; // 파괴 여부 조회
        public bool ReadyToRecycle => IsDead && Time.time - deadAt >= recycleDelay; // 풀 복귀 가능 여부
        public DetectionSensor Sensor => sensor; // 미니맵·검사용 센서 조회

        private void Awake() // 드론 필수 컴포넌트 연결
        {
            actor = GetComponent<EnemyActor>(); // 드론 내구도 조회
            sensor = GetComponent<DetectionSensor>(); // 감시 센서 조회
            status = GetComponent<EnemyStatusController>(); // 마비 상태 조회
            crimeTag = GetComponent<MapGuardCrimeTag>(); // 경비 범죄 태그 조회
        }

        public void Initialize(int index) // 풀 생성 시 드론 번호 설정
        {
            droneIndex = Mathf.Max(0, index); // 안전한 드론 번호 저장
            orbitPhase = droneIndex * 180f; // 두 드론이 반대편에서 순찰하도록 초기 각도 분산
        }

        public void ActivateAt(Vector3 position) // E-04 실제 투입
        {
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null || wanted.Player == null) // 플레이어 참조 확인
            {
                return; // 활성화 중단
            }

            transform.position = position; // 초기 비행 위치 적용
            transform.rotation = Quaternion.identity; // 파괴 기울기 제거
            actor = actor != null ? actor : GetComponent<EnemyActor>(); // 내구도 참조 보정
            sensor = sensor != null ? sensor : GetComponent<DetectionSensor>(); // 센서 참조 보정
            status = status != null ? status : GetComponent<EnemyStatusController>(); // 상태 참조 보정
            crimeTag = crimeTag != null ? crimeTag : GetComponent<MapGuardCrimeTag>(); // 범죄 태그 참조 보정
            actor.enabled = true; // 풀 재사용 EnemyActor 활성화
            actor.Configure(60f, 1f, false); // 기획 내구도 60·암살 불가 적용
            sensor.enabled = true; // 사망 후 꺼진 센서 복구
            sensor.Configure(wanted.Player.transform, 22f, 110f, 0f, ~0); // 기획 시야 22m·110도·청각 없음 적용
            if (status != null) // 상태 관리자 확인
            {
                status.enabled = true; // EMP 상태 관리자 활성화
            }
            if (crimeTag != null) // 범죄 태그 확인
            {
                crimeTag.enabled = true; // 공격·파괴 Heat 연결 복구
            }
            deadAt = float.PositiveInfinity; // 파괴 시각 초기화
            gameObject.SetActive(true); // 드론 실제 활성화
        }

        private void Update() // 공중 감시·위치 공유 갱신
        {
            if (actor == null || sensor == null) // 필수 참조 확인
            {
                return; // 행동 중단
            }

            if (actor.IsDead) // 드론 파괴 확인
            {
                if (float.IsPositiveInfinity(deadAt)) // 최초 파괴 프레임 확인
                {
                    deadAt = Time.time; // 시체 유지 시작 시각 저장
                }
                return; // 파괴 후 비행·탐지 중단
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted == null || wanted.Player == null || wanted.Stars < 5) // 오성 수배 유지 여부 확인
            {
                return; // 오성 이외 감시 행동 중단
            }

            if (status != null && status.IsStunned) // EMP·마비 상태 확인
            {
                return; // 마비 중 비행과 정보 공유 중단
            }

            Vector3 focus = ResolveSurfaceFocus(wanted.Player.transform.position); // 지상 감시 중심 계산
            float angle = (Time.time * 24f + orbitPhase) * Mathf.Deg2Rad; // 현재 궤도 각도 계산
            Vector3 orbit = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius; // 수평 원형 순찰 오프셋 계산
            Vector3 desired = focus + orbit; // 수평 감시 목표 계산
            desired.y = Mathf.Max(focus.y + hoverHeight, 12f); // 지상 또는 옥상 위 비행 높이 설정
            transform.position = Vector3.MoveTowards(transform.position, desired, moveSpeed * Time.deltaTime); // 기획 비행 속도로 목표 이동
            RotateToward(wanted.Player.transform.position); // 플레이어 방향으로 렌즈 회전

            if (sensor.TargetVisible) // 플레이어 직접 발견 확인
            {
                float distance = Vector3.Distance(transform.position, wanted.Player.transform.position); // 실제 정보 공유 거리 계산
                if (distance <= informationShareDistance) // 기획 공유 반경 확인
                {
                    wanted.BroadcastDetection(wanted.Player.transform.position); // 기존 5초 수배 경비 위치 공유 재사용
                }
            }
        }

        private Vector3 ResolveSurfaceFocus(Vector3 playerPosition) // 지하 플레이어 추적 시 지하철 입구 감시 중심 선택
        {
            Map24LayerNavigation navigation = Map24LayerNavigation.Instance; // 지상·지하 관리자 조회
            if (navigation != null && navigation.Ready && navigation.GetLayer(playerPosition) == Map24WorldLayer.Underground) // 플레이어 지하 여부 확인
            {
                return navigation.SurfaceAnchor; // 드론은 지하 진입 대신 지하철 지상 입구 감시
            }

            return playerPosition; // 지상에서는 플레이어 주변 감시
        }

        private void RotateToward(Vector3 targetPosition) // 드론 시야 방향 회전
        {
            Vector3 direction = targetPosition - transform.position; // 목표 방향 계산
            if (direction.sqrMagnitude <= 0.001f) // 유효 방향 확인
            {
                return; // 회전 생략
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); // 목표 렌즈 방향 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 4.5f * Time.deltaTime); // 공중 감시 회전 적용
        }
    }
}
