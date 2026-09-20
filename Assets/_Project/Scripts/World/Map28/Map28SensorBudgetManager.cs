using System.Collections.Generic; // 최적화로 비활성화한 센서 ID 관리
using ProjectK.Day16; // 본편 월드 참조
using ProjectK.Day21; // 수배 경비 참조
using ProjectK.Day25; // 감시 드론 참조
using UnityEngine; // 거리 계산과 센서 활성화 제어

namespace ProjectK.Day28 // 28일차 성능 최적화 이름 공간
{
    [DisallowMultipleComponent] // 센서 예산 관리자 중복 방지
    public sealed class Map28SensorBudgetManager : MonoBehaviour // 멀리 있는 고정 감시 센서를 자동 휴면시키는 중앙 관리자
    {
        private static Map28SensorBudgetManager instance; // 현재 센서 예산 관리자
        private readonly HashSet<int> disabledByBudget = new HashSet<int>(); // 이 관리자가 직접 비활성화한 센서 ID
        [SerializeField, Min(40f)] private float disableDistance = 260f; // 고정 센서 휴면 시작 거리
        [SerializeField, Min(20f)] private float enableDistance = 220f; // 고정 센서 재활성 거리
        [SerializeField, Range(2, 128)] private int sensorsPerFrame = 24; // 프레임당 검사 센서 수
        private MapWorldRoot world; // 현재 본편 월드
        private int sensorIndex; // 다음 검사 센서 인덱스
        private float nextWorldSearchTime; // 다음 월드 검색 시각

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 참조 초기화
        {
            instance = null; // 이전 관리자 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 센서 예산 관리자 생성
        {
            if (instance != null) // 기존 관리자 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day28] Sensor Budget Manager"); // 센서 예산 오브젝트 생성
            instance = owner.AddComponent<Map28SensorBudgetManager>(); // 센서 예산 관리자 연결
            DontDestroyOnLoad(owner); // 씬 전환 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 관리자 존재 확인
            {
                Destroy(gameObject); // 중복 오브젝트 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Start() // 첫 본편 월드 연결
        {
            ResolveWorld(); // 현재 MapWorldRoot 검색
        }

        private void Update() // 센서 거리 예산 분산 갱신
        {
            if (world == null || world.Player == null) // 본편 플레이어 참조 확인
            {
                if (Time.unscaledTime >= nextWorldSearchTime) // 재검색 시각 확인
                {
                    nextWorldSearchTime = Time.unscaledTime + 1f; // 다음 검색 예약
                    ResolveWorld(); // 본편 월드 재검색
                }

                return; // 센서 예산 처리 중단
            }

            IReadOnlyList<DetectionSensor> sensors = Map28RuntimeRegistry.AllSensors; // Registry 전체 센서 목록 조회
            if (sensors == null || sensors.Count == 0) // 센서 존재 확인
            {
                return; // 처리 생략
            }

            int budget = Mathf.Min(sensorsPerFrame, sensors.Count); // 현재 프레임 센서 검사 예산 계산
            Vector3 playerPosition = world.Player.transform.position; // 플레이어 위치 한 번만 조회
            float disableSqr = disableDistance * disableDistance; // 휴면 거리 제곱 계산
            float enableSqr = enableDistance * enableDistance; // 재활성 거리 제곱 계산

            for (int i = 0; i < budget; i++) // 제한된 센서 수만 순회
            {
                if (sensorIndex >= sensors.Count) // 목록 끝 확인
                {
                    sensorIndex = 0; // 첫 센서부터 다시 순회
                }

                DetectionSensor sensor = sensors[sensorIndex++]; // 현재 센서 조회
                if (sensor == null) // 파괴 센서 확인
                {
                    continue; // 다음 센서 처리
                }

                if (!IsBudgetCandidate(sensor)) // 고정 감시 센서인지 확인
                {
                    continue; // 경비·적·드론 센서는 기존 실시간 행동 유지
                }

                int id = sensor.GetInstanceID(); // 센서 고유 ID 조회
                bool budgetDisabled = disabledByBudget.Contains(id); // 이 관리자가 끈 센서인지 확인

                if (!sensor.gameObject.activeInHierarchy) // GameObject 자체 비활성 상태 확인
                {
                    continue; // 풀링·씬 비활성 상태는 건드리지 않음
                }

                Vector3 delta = sensor.transform.position - playerPosition; // 플레이어와 센서 거리 벡터 계산
                float distanceSqr = delta.sqrMagnitude; // 제곱 거리 계산

                if (!budgetDisabled && sensor.enabled && distanceSqr > disableSqr) // 멀리 있는 활성 고정 센서 확인
                {
                    sensor.enabled = false; // DetectionSensor Update와 Noise 구독 휴면 처리
                    disabledByBudget.Add(id); // 최적화로 비활성화한 센서 기록
                    continue; // 현재 센서 처리 종료
                }

                if (budgetDisabled && distanceSqr < enableSqr) // 플레이어가 다시 가까워진 휴면 센서 확인
                {
                    sensor.enabled = true; // 감시 센서 재활성화
                    disabledByBudget.Remove(id); // 최적화 휴면 기록 제거
                }
            }
        }

        private void OnDestroy() // 관리자 제거 시 자신이 끈 센서 복구
        {
            RestoreBudgetSensors(); // 최적화 휴면 센서 재활성화
        }

        private void ResolveWorld() // 현재 본편 월드 검색
        {
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 시스템 조회
            if (wanted != null && wanted.World != null) // 기존 월드 참조 확인
            {
                world = wanted.World; // 수배 시스템 월드 재사용
                return; // 추가 전역 검색 생략
            }

            MapWorldRoot[] worlds = Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 수배 시스템 없는 초기 씬 월드 검색
            world = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 연결
        }

        private static bool IsBudgetCandidate(DetectionSensor sensor) // 거리 휴면을 적용해도 안전한 고정 센서 판정
        {
            if (sensor == null) // 센서 존재 확인
            {
                return false; // 후보 아님
            }

            if (sensor.GetComponentInParent<MapWantedGuardAgent>() != null) // 수배 경비 센서 확인
            {
                return false; // 추격 센서는 실시간 유지
            }

            if (sensor.GetComponentInParent<Map25SurveillanceDrone>() != null) // 감시 드론 센서 확인
            {
                return false; // 드론 센서는 실시간 유지
            }

            if (sensor.GetComponentInParent<EnemyActor>() != null) // 일반 전투 적 센서 확인
            {
                return false; // 미션·전투 적 센서는 기존 행동 유지
            }

            return true; // 고정 카메라·도시 보안 센서에만 거리 휴면 적용
        }

        private void RestoreBudgetSensors() // 이 관리자가 직접 끈 센서만 복구
        {
            IReadOnlyList<DetectionSensor> sensors = Map28RuntimeRegistry.AllSensors; // 전체 센서 목록 조회

            for (int i = 0; i < sensors.Count; i++) // 센서 순회
            {
                DetectionSensor sensor = sensors[i]; // 현재 센서 조회
                if (sensor == null || !disabledByBudget.Contains(sensor.GetInstanceID())) // 최적화 휴면 센서 여부 확인
                {
                    continue; // 다음 센서 처리
                }

                if (sensor.gameObject.activeInHierarchy) // 실제 활성 GameObject 확인
                {
                    sensor.enabled = true; // 원래 감시 기능 복구
                }
            }

            disabledByBudget.Clear(); // 최적화 휴면 기록 초기화
        }
    }
}
