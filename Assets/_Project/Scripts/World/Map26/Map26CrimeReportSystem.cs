using System.Collections.Generic; // 사건·목격자 목록 관리
using ProjectK.Day20; // 시민 생활 AI 참조
using ProjectK.Day21; // 범죄·수배 시스템 참조
using ProjectK.Day24; // 지상·지하 층 판정 참조
using ProjectK.Day25; // E-04 드론 구분 참조
using UnityEngine; // 런타임 탐색·거리·시야 처리

namespace ProjectK.Day26 // 26일차 목격·신고 이름 공간
{
    [DisallowMultipleComponent] // 중앙 신고 관리자 중복 방지
    public sealed class Map26CrimeReportSystem : MonoBehaviour // 실제 목격·청취 후 지연 신고를 처리하는 중앙 관리자
    {
        private sealed class Incident // 한 번의 범죄 신고 단위
        {
            public int Id; // 사건 식별 번호
            public CrimeType Type; // 범죄 종류
            public Vector3 CrimePosition; // 실제 범죄 발생 위치
            public GameObject Player; // 범죄 주체 플레이어
            public float CreatedAt; // 사건 생성 시각
            public float ExpiresAt; // 신고 가능 만료 시각
            public bool Reported; // 이미 Heat 반영된 사건 여부
            public float CleanupAt; // 신고 완료 후 사건 삭제 시각
            public bool SecurityScheduled; // 보안 센서 자동 신고 예약 여부
            public float SecurityReportAt; // 보안 센서 신고 완료 시각
            public Vector3 SecurityKnownPosition; // 보안 센서가 알고 있는 위치
            public bool SecurityExactIdentity; // 보안 센서가 플레이어를 직접 확인했는지 여부
            public readonly HashSet<int> Witnesses = new HashSet<int>(); // 현재 신고 중인 시민 인스턴스 목록
        }

        private struct CitizenCandidate // 시민 목격 후보 자료
        {
            public MapCitizenAgent Citizen; // 시민 생활 AI
            public bool Direct; // 플레이어 직접 목격 여부
            public float Distance; // 사건과 시민 거리
        }

        private static Map26CrimeReportSystem instance; // 현재 중앙 신고 관리자
        private readonly List<Incident> incidents = new List<Incident>(); // 활성 범죄 사건 목록
        private int nextIncidentId = 1; // 다음 사건 번호
        private float nextCleanupScan; // 다음 목격자 구성 보정 시각

        public static Map26CrimeReportSystem Instance => instance; // 현재 신고 관리자 조회
        public bool HasPendingReport // HUD용 신고 진행 여부
        {
            get
            {
                foreach (Incident incident in incidents) // 활성 사건 순회
                {
                    if (incident != null && !incident.Reported && (incident.SecurityScheduled || incident.Witnesses.Count > 0)) // 실제 신고 수단이 남아 있는 사건 확인
                    {
                        return true; // 신고 진행 중 반환
                    }
                }

                return false; // 진행 중 신고 없음
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 정적 초기화
        private static void ResetStatics() // 플레이 시작 시 정적 참조 제거
        {
            instance = null; // 이전 플레이 관리자 참조 제거
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 씬 준비 뒤 자동 생성
        private static void EnsureInstance() // 별도 씬 설정 없이 신고 관리자 생성
        {
            if (instance != null) // 기존 인스턴스 존재 확인
            {
                return; // 중복 생성 방지
            }

            GameObject owner = new GameObject("[Day26] Crime Reporting"); // 자동 신고 관리자 오브젝트 생성
            instance = owner.AddComponent<Map26CrimeReportSystem>(); // 신고 관리자 컴포넌트 연결
            DontDestroyOnLoad(owner); // 씬 전환 뒤에도 유지
        }

        private void Awake() // 단일 인스턴스 등록
        {
            if (instance != null && instance != this) // 다른 인스턴스 존재 확인
            {
                Destroy(gameObject); // 중복 관리자 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 인스턴스 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void Update() // 보안 센서 신고 완료와 사건 정리
        {
            for (int i = incidents.Count - 1; i >= 0; i--) // 사건 목록 역순 순회
            {
                Incident incident = incidents[i]; // 현재 사건 조회
                if (incident == null) // 누락 사건 확인
                {
                    incidents.RemoveAt(i); // 잘못된 항목 제거
                    continue; // 다음 사건 처리
                }

                if (!incident.Reported && incident.SecurityScheduled && Time.time >= incident.SecurityReportAt) // 보안 센서 신고 완료 시각 확인
                {
                    CompleteIncident(incident, incident.SecurityKnownPosition, incident.SecurityExactIdentity); // 보안망 신고로 사건 확정
                }

                bool noReporter = !incident.SecurityScheduled && incident.Witnesses.Count == 0; // 남아 있는 신고 수단 여부 확인
                if (!incident.Reported && (Time.time >= incident.ExpiresAt || noReporter)) // 신고 실패·만료 사건 확인
                {
                    incidents.RemoveAt(i); // 신고되지 않은 사건 제거
                    continue; // 다음 사건 처리
                }

                if (incident.Reported && Time.time >= incident.CleanupAt) // 신고 완료 사건 정리 시각 확인
                {
                    incidents.RemoveAt(i); // 완료 사건 제거
                }
            }

            if (Time.unscaledTime >= nextCleanupScan) // 비활성 시민 참조 보정 시점 확인
            {
                nextCleanupScan = Time.unscaledTime + 1.5f; // 다음 보정 시각 예약
                CleanupInvalidWitnesses(); // 풀 회수된 시민 목격자 정리
            }
        }

        public bool TryQueueCrime(CrimeType type, Vector3 crimePosition, GameObject instigator, bool loud) // 기존 ReportCrime에서 실제 신고 과정 시작
        {
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 관리자 조회
            if (wanted == null || wanted.Player == null || !MapCrimeWitness.IsPlayerInstigator(instigator, wanted.Player)) // 실제 플레이어 범죄 여부 확인
            {
                return false; // 신고 대상 사건 아님
            }

            Incident incident = FindMergeIncident(type, crimePosition); // 같은 시점·위치의 중복 사건 조회
            bool created = false; // 새 사건 생성 여부
            if (incident == null) // 합칠 기존 사건 없음
            {
                incident = new Incident // 새 범죄 사건 생성
                {
                    Id = nextIncidentId++, // 고유 사건 번호 발급
                    Type = type, // 범죄 종류 저장
                    CrimePosition = crimePosition, // 범죄 위치 저장
                    Player = wanted.Player, // 플레이어 루트 저장
                    CreatedAt = Time.time, // 사건 생성 시각 저장
                    ExpiresAt = Time.time + 7.0f, // 최대 신고 가능 시간 저장
                    CleanupAt = float.PositiveInfinity // 신고 전 삭제 시각 비활성화
                };
                incidents.Add(incident); // 활성 사건 목록 등록
                created = true; // 신규 생성 표시
            }

            bool queued = QueueSecurityWitnesses(incident, crimePosition, loud, wanted.Player.transform); // 경비·카메라 신고 예약
            queued |= QueueCitizenWitnesses(incident, crimePosition, loud, wanted.Player.transform); // 시민 목격·청취 신고 예약

            if (!queued && created) // 신고 수단이 하나도 없는 새 사건 확인
            {
                incidents.Remove(incident); // 완전 은폐 범죄 사건 제거
                return false; // 신고되지 않은 범죄 반환
            }

            return queued || incident.SecurityScheduled || incident.Witnesses.Count > 0; // 기존 사건에 신고자가 있으면 처리된 범죄로 반환
        }

        public void CompleteCitizenReport(int incidentId, int witnessId, Vector3 knownPosition, bool exactIdentity) // 시민 신고 완료 요청
        {
            Incident incident = FindIncident(incidentId); // 중앙 사건 조회
            if (incident == null) // 사건이 이미 만료·정리됐는지 확인
            {
                return; // 신고 처리 생략
            }

            incident.Witnesses.Remove(witnessId); // 완료 시민을 진행 목록에서 제거
            if (incident.Reported) // 다른 시민·보안망이 먼저 신고했는지 확인
            {
                return; // Heat 중복 반영 방지
            }

            CompleteIncident(incident, knownPosition, exactIdentity); // 첫 신고자로 사건 확정
        }

        public void CancelCitizenWitness(int incidentId, int witnessId) // 시민 사망·풀 회수 시 신고 취소
        {
            Incident incident = FindIncident(incidentId); // 중앙 사건 조회
            if (incident == null) // 이미 정리된 사건 확인
            {
                return; // 취소 처리 생략
            }

            incident.Witnesses.Remove(witnessId); // 진행 중 시민 목록에서 제거
        }

        private bool QueueSecurityWitnesses(Incident incident, Vector3 crimePosition, bool loud, Transform player) // 경비·카메라·드론 신고 판정
        {
            DetectionSensor[] sensors = Object.FindObjectsByType<DetectionSensor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 보안 센서 전체 조회
            bool queued = false; // 보안 신고 예약 여부

            foreach (DetectionSensor sensor in sensors) // 보안 센서 순회
            {
                if (sensor == null || sensor.Target != player) // 플레이어를 감시하지 않는 센서 제외
                {
                    continue; // 다음 센서 처리
                }

                if (!SameLayer(sensor.transform.position, crimePosition)) // 사건과 다른 층 센서 확인
                {
                    continue; // 직접 목격·청취 모두 제외
                }

                bool direct = sensor.TargetVisible; // 플레이어 직접 시야 확인
                float hearing = Mathf.Max(sensor.HearingRadius, loud ? 30f : 14f); // 큰 소음 최소 보안 청취 거리 계산
                bool heard = loud && (sensor.transform.position - crimePosition).sqrMagnitude <= hearing * hearing; // 큰 소리 사건 청취 여부 계산
                if (!direct && !heard) // 신고 근거 없음
                {
                    continue; // 다음 센서 처리
                }

                float delay = SecurityDelay(sensor, direct); // 센서 종류별 신고 지연 계산
                Vector3 knownPosition = direct ? player.position : crimePosition; // 직접 시야면 플레이어 마지막 위치, 청취면 사건 위치 사용
                bool exact = direct; // 직접 시야만 플레이어 신원·위치 확인 처리

                if (!incident.SecurityScheduled || Time.time + delay < incident.SecurityReportAt) // 기존 예약보다 빠른 보안 신고인지 확인
                {
                    incident.SecurityScheduled = true; // 보안 신고 예약 활성화
                    incident.SecurityReportAt = Time.time + delay; // 가장 빠른 신고 완료 시각 저장
                    incident.SecurityKnownPosition = knownPosition; // 보안망이 알고 있는 위치 저장
                    incident.SecurityExactIdentity = exact; // 직접 식별 여부 저장
                }

                queued = true; // 최소 한 개 보안 신고 수단 확인
            }

            return queued; // 보안 신고 예약 결과 반환
        }

        private bool QueueCitizenWitnesses(Incident incident, Vector3 crimePosition, bool loud, Transform player) // 시민 직접 목격·청취 판정
        {
            MapCitizenAgent[] citizens = Object.FindObjectsByType<MapCitizenAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 시민 전체 조회
            List<CitizenCandidate> candidates = new List<CitizenCandidate>(); // 신고 후보 시민 목록 준비

            foreach (MapCitizenAgent citizen in citizens) // 시민 전체 순회
            {
                if (citizen == null) // 누락 시민 확인
                {
                    continue; // 다음 시민 처리
                }

                MapCitizenVitals vitals = citizen.GetComponent<MapCitizenVitals>(); // 시민 생존 상태 조회
                if (vitals != null && vitals.IsDead) // 사망 시민 확인
                {
                    continue; // 신고 후보 제외
                }

                if (!SameLayer(citizen.transform.position, crimePosition)) // 다른 층 시민 확인
                {
                    continue; // 지상·지하 관통 신고 방지
                }

                float distance = Vector3.Distance(citizen.transform.position, crimePosition); // 사건과 시민 거리 계산
                if (!loud && distance < 1.6f) // 조용한 근접 범죄의 피해자 본인 제외
                {
                    continue; // 주변 제삼자 목격자만 사용
                }

                bool direct = CanCitizenSeePlayer(citizen.transform, player, loud ? 32f : 28f); // 시민의 실제 플레이어 시야 확인
                bool heard = loud && distance <= 45f; // 큰 소리 사건 청취 범위 확인
                if (!direct && !heard) // 범죄 인지 근거 확인
                {
                    continue; // 다음 시민 검사
                }

                candidates.Add(new CitizenCandidate // 신고 후보 등록
                {
                    Citizen = citizen, // 시민 참조 저장
                    Direct = direct, // 직접 목격 여부 저장
                    Distance = distance // 우선순위 거리 저장
                });
            }

            candidates.Sort((first, second) => // 가까운 직접 목격자를 먼저 선택
            {
                int directOrder = second.Direct.CompareTo(first.Direct); // 직접 목격 우선 정렬
                return directOrder != 0 ? directOrder : first.Distance.CompareTo(second.Distance); // 같은 유형은 가까운 시민 우선
            });

            int attached = 0; // 실제 신고 시작 시민 수
            foreach (CitizenCandidate candidate in candidates) // 정렬된 후보 순회
            {
                if (attached >= 3) // 한 사건에서 최대 세 명만 신고 UI 사용
                {
                    break; // 과도한 시민 신고 처리 방지
                }

                Map26CitizenWitness witness = candidate.Citizen.GetComponent<Map26CitizenWitness>(); // 기존 Day26 시민 상태 조회
                if (witness == null) // 아직 신고 컴포넌트가 없는 시민 확인
                {
                    witness = candidate.Citizen.gameObject.AddComponent<Map26CitizenWitness>(); // 런타임 신고 상태 자동 추가
                }

                float delay = CitizenDelay(candidate.Citizen, candidate.Direct); // 직접 목격·청취별 신고 지연 계산
                Vector3 knownPosition = candidate.Direct ? player.position : crimePosition; // 직접 목격은 플레이어 마지막 위치, 청취는 사건 위치 사용
                if (!witness.BeginReport(this, incident.Id, incident.Type, crimePosition, knownPosition, candidate.Direct, delay)) // 실제 시민 신고 시작 시도
                {
                    continue; // 이미 더 중요한 신고 중인 시민 제외
                }

                incident.Witnesses.Add(witness.GetInstanceID()); // 중앙 사건에 시민 등록
                attached++; // 실제 신고자 수 증가
            }

            return attached > 0; // 시민 신고자 존재 여부 반환
        }

        private void CompleteIncident(Incident incident, Vector3 knownPosition, bool exactIdentity) // 첫 신고 완료 후 Heat·지역 경보 반영
        {
            if (incident == null || incident.Reported) // 유효 사건과 중복 신고 확인
            {
                return; // 처리 생략
            }

            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 관리자 조회
            if (wanted == null || wanted.Player == null) // 수배 시스템 준비 확인
            {
                return; // 신고 확정 대기
            }

            incident.Reported = wanted.CompleteDeferredReport(incident.Type, knownPosition, wanted.Player, exactIdentity); // 한 번만 범죄 Heat 확정
            if (!incident.Reported) // 범죄 확정 실패 확인
            {
                return; // 후속 경보 생략
            }

            incident.SecurityScheduled = false; // 보안 신고 예약 종료
            incident.CleanupAt = Time.time + 1.5f; // 신고 완료 시민 UI 정리 여유 설정
            RaiseLocalAlert(incident.CrimePosition); // 신고 위치 주변 시민 공포 반응 발생
        }

        private void RaiseLocalAlert(Vector3 position) // 신고 완료 지역 주변 시민 도주 반응
        {
            MapCitizenAgent[] citizens = Object.FindObjectsByType<MapCitizenAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 시민 조회
            const float radius = 70f; // 지역 경보 기본 반경
            float radiusSqr = radius * radius; // 반경 제곱 계산

            foreach (MapCitizenAgent citizen in citizens) // 시민 순회
            {
                if (citizen == null || (citizen.transform.position - position).sqrMagnitude > radiusSqr) // 경보 범위 확인
                {
                    continue; // 범위 밖 시민 제외
                }

                if (!SameLayer(citizen.transform.position, position)) // 다른 층 시민 확인
                {
                    continue; // 지상·지하 경보 분리
                }

                MapCitizenVitals vitals = citizen.GetComponent<MapCitizenVitals>(); // 생존 상태 조회
                if (vitals != null && vitals.IsDead) // 사망 시민 확인
                {
                    continue; // 반응 제외
                }

                citizen.BeginFlee(position); // 신고 지역 반대 방향으로 도주
            }
        }

        private Incident FindMergeIncident(CrimeType type, Vector3 position) // 짧은 시간 같은 위치 중복 범죄 병합
        {
            for (int i = incidents.Count - 1; i >= 0; i--) // 최근 사건부터 검색
            {
                Incident incident = incidents[i]; // 현재 사건 조회
                if (incident == null || incident.Reported || incident.Type != type) // 병합 가능한 상태와 범죄 종류 확인
                {
                    continue; // 다음 사건 검사
                }

                if (Time.time - incident.CreatedAt > 1.25f) // 병합 시간 범위 확인
                {
                    continue; // 오래된 사건 제외
                }

                if ((incident.CrimePosition - position).sqrMagnitude <= 4f * 4f) // 같은 장소 사건 여부 확인
                {
                    return incident; // 기존 사건 재사용
                }
            }

            return null; // 병합 가능한 사건 없음
        }

        private Incident FindIncident(int incidentId) // 사건 번호로 활성 사건 조회
        {
            foreach (Incident incident in incidents) // 활성 사건 순회
            {
                if (incident != null && incident.Id == incidentId) // 사건 번호 일치 확인
                {
                    return incident; // 사건 반환
                }
            }

            return null; // 사건 없음
        }

        private void CleanupInvalidWitnesses() // 풀 회수된 시민 신고자 참조 정리
        {
            Map26CitizenWitness[] activeWitnesses = Object.FindObjectsByType<Map26CitizenWitness>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 현재 활성 신고 시민 조회
            HashSet<int> activeIds = new HashSet<int>(); // 활성 신고 시민 ID 목록
            foreach (Map26CitizenWitness witness in activeWitnesses) // 활성 신고 컴포넌트 순회
            {
                if (witness != null && witness.IsBusy) // 실제 신고 중 시민 확인
                {
                    activeIds.Add(witness.GetInstanceID()); // 활성 ID 등록
                }
            }

            foreach (Incident incident in incidents) // 모든 사건 순회
            {
                if (incident == null || incident.Witnesses.Count == 0) // 시민 신고자가 없는 사건 확인
                {
                    continue; // 정리 생략
                }

                incident.Witnesses.RemoveWhere(id => !activeIds.Contains(id)); // 비활성·풀 회수 신고자 제거
            }
        }

        private static bool CanCitizenSeePlayer(Transform citizen, Transform player, float maximumDistance) // 시민 실제 시야 판정
        {
            if (citizen == null || player == null) // 필수 Transform 확인
            {
                return false; // 시야 실패
            }

            Vector3 origin = citizen.position + Vector3.up * 1.45f; // 시민 눈높이 근사 위치
            Vector3 target = player.position + Vector3.up * 1.0f; // 플레이어 몸 중심 근사 위치
            Vector3 direction = target - origin; // 플레이어 방향 계산
            float distance = direction.magnitude; // 실제 시야 거리 계산
            if (distance <= 0.01f || distance > maximumDistance) // 시민 시야 거리 확인
            {
                return false; // 거리 밖 시야 실패
            }

            Vector3 planar = direction; // 수평 시야각 계산용 방향 복사
            planar.y = 0f; // 높이 차이 제거
            if (planar.sqrMagnitude > 0.001f && Vector3.Angle(citizen.forward, planar.normalized) > 65f) // 약 130도 시민 시야각 확인
            {
                return false; // 시민 뒤쪽 사건 제외
            }

            return EquipmentTargeting.HasClearPath(origin, target, citizen, player); // 건물·벽에 가려지지 않은 실제 시야 확인
        }

        private static bool SameLayer(Vector3 first, Vector3 second) // 지상·지하 같은 층 여부 확인
        {
            Map24LayerNavigation navigation = Map24LayerNavigation.Instance; // Day24 층 관리자 조회
            return navigation == null || !navigation.Ready || navigation.SameLayer(first, second); // 층 시스템 준비 전에는 기존 동작 유지
        }

        private static float CitizenDelay(MapCitizenAgent citizen, bool direct) // 시민별 신고 지연 계산
        {
            float variance = citizen != null ? Mathf.Abs(citizen.GetInstanceID() % 17) / 17f : 0.5f; // 시민별 안정적인 시간 분산
            return direct ? Mathf.Lerp(2.0f, 3.6f, variance) : Mathf.Lerp(3.6f, 5.2f, variance); // 직접 목격이 청취보다 빠르게 신고
        }

        private static float SecurityDelay(DetectionSensor sensor, bool direct) // 경비·카메라·드론 신고 지연 계산
        {
            if (sensor != null && sensor.GetComponentInParent<Map25SurveillanceDrone>() != null) // E-04 감시 드론 확인
            {
                return direct ? 0.05f : 0.20f; // 드론은 거의 즉시 보안망 공유
            }

            if (sensor != null && sensor.GetComponentInParent<MapWantedGuardAgent>() != null) // 수배 경비 확인
            {
                return direct ? 0.45f : 0.80f; // 경비는 시민보다 빠른 신고
            }

            return direct ? 0.25f : 0.65f; // 고정 감시 카메라·기타 센서 신고 속도
        }
    }
}
