using ProjectK.Day20; // 시민 생활 AI 참조
using ProjectK.Day21; // 시민 생존 상태와 범죄 종류 참조
using UnityEngine; // 신고 타이머와 월드 UI 처리

namespace ProjectK.Day26 // 26일차 목격·신고 이름 공간
{
    [DisallowMultipleComponent] // 시민 신고 상태 중복 방지
    public sealed class Map26CitizenWitness : MonoBehaviour // 시민의 목격·공포·신고 진행 관리자
    {
        private MapCitizenAgent agent; // 시민 생활 AI 참조
        private MapCitizenVitals vitals; // 시민 생존 상태 참조
        private Map26CrimeReportSystem owner; // 중앙 신고 관리자
        private Map26WitnessState state; // 현재 목격·신고 상태
        private CrimeType crimeType; // 현재 신고할 범죄 종류
        private int incidentId = -1; // 중앙 사건 식별 번호
        private bool directIdentity; // 플레이어를 직접 봤는지 여부
        private Vector3 knownPosition; // 시민이 신고할 마지막 위치
        private float panicUntil; // 놀람 상태 종료 시각
        private float reportAt; // 신고 완료 예정 시각
        private float reportedUntil; // 신고 완료 표시 종료 시각
        private GUIStyle labelStyle; // 신고 상태 월드 UI 스타일

        public Map26WitnessState State => state; // 외부 검사용 현재 신고 상태
        public int IncidentId => incidentId; // 현재 사건 번호 조회
        public bool IsBusy => state == Map26WitnessState.Witnessed || state == Map26WitnessState.Reporting; // 현재 신고 처리 중 여부

        private void Awake() // 시민 참조 연결
        {
            agent = GetComponent<MapCitizenAgent>(); // 생활 AI 조회
            vitals = GetComponent<MapCitizenVitals>(); // 생존 상태 조회
        }

        private void OnDisable() // 풀 회수·사망 비활성 상태 정리
        {
            if (owner != null && incidentId >= 0 && IsBusy) // 진행 중 신고 존재 확인
            {
                owner.CancelCitizenWitness(incidentId, GetInstanceID()); // 중앙 사건의 목격자 등록 해제
            }

            ResetState(); // 신고 상태 초기화
        }

        private void Update() // 놀람·신고·완료 상태 갱신
        {
            if (state == Map26WitnessState.None) // 평상 상태 확인
            {
                return; // 갱신 생략
            }

            if (vitals != null && vitals.IsDead) // 신고 중 시민 사망 확인
            {
                if (owner != null && incidentId >= 0 && IsBusy) // 중앙 사건 연결 확인
                {
                    owner.CancelCitizenWitness(incidentId, GetInstanceID()); // 죽은 시민의 신고 취소
                }

                ResetState(); // 사망 시민 상태 초기화
                return; // 갱신 종료
            }

            if (state == Map26WitnessState.Witnessed && Time.time >= panicUntil) // 놀람 시간이 끝났는지 확인
            {
                state = Map26WitnessState.Reporting; // 실제 신고 진행 상태 전환
            }

            if ((state == Map26WitnessState.Witnessed || state == Map26WitnessState.Reporting) && Time.time >= reportAt) // 신고 완료 시각 확인
            {
                if (owner != null && incidentId >= 0) // 중앙 신고 관리자 연결 확인
                {
                    owner.CompleteCitizenReport(incidentId, GetInstanceID(), knownPosition, directIdentity); // 첫 신고 완료 처리 요청
                }

                state = Map26WitnessState.Reported; // 시민 신고 완료 상태 적용
                reportedUntil = Time.time + 1.25f; // 짧은 완료 표시 유지
            }

            if (state == Map26WitnessState.Reported && Time.time >= reportedUntil) // 신고 완료 표시 종료 확인
            {
                ResetState(); // 평상 상태 복귀
            }
        }

        public void ForceResetForCheckpoint() // Day32 재시도 시 시민 목격·신고 상태 즉시 초기화
        {
            ResetState(); // 기존 내부 상태 초기화 로직 재사용
        }

        public bool BeginReport(Map26CrimeReportSystem system, int newIncidentId, CrimeType type, Vector3 crimePosition, Vector3 reportPosition, bool sawPlayer, float delay) // 새 범죄 목격·신고 시작
        {
            if (!isActiveAndEnabled || system == null || newIncidentId < 0) // 유효한 시민과 사건 확인
            {
                return false; // 신고 시작 실패
            }

            vitals = vitals != null ? vitals : GetComponent<MapCitizenVitals>(); // 생존 상태 참조 복구
            if (vitals != null && vitals.IsDead) // 사망 시민 확인
            {
                return false; // 신고 시작 실패
            }

            if (IsBusy) // 이미 다른 사건을 신고 중인지 확인
            {
                float currentPriority = MapWantedRules.HeatForCrime(crimeType); // 현재 신고 범죄 중요도 계산
                float newPriority = MapWantedRules.HeatForCrime(type); // 신규 범죄 중요도 계산
                if (newPriority <= currentPriority && reportAt <= Time.time + Mathf.Max(0.1f, delay)) // 기존 신고가 더 중요하거나 빠른지 확인
                {
                    return false; // 기존 신고 유지
                }

                if (owner != null && incidentId >= 0) // 기존 사건 연결 확인
                {
                    owner.CancelCitizenWitness(incidentId, GetInstanceID()); // 기존 사건의 시민 등록 해제
                }
            }

            owner = system; // 중앙 신고 관리자 저장
            incidentId = newIncidentId; // 새 사건 번호 저장
            crimeType = type; // 범죄 종류 저장
            directIdentity = sawPlayer; // 플레이어 직접 식별 여부 저장
            knownPosition = reportPosition; // 신고할 위치 저장
            float variance = Mathf.Abs(GetInstanceID() % 7) * 0.055f; // 시민별 놀람 시간 분산
            panicUntil = Time.time + 0.45f + variance; // 약 0.45~0.78초 놀람 상태 설정
            reportAt = Time.time + Mathf.Max(0.65f, delay); // 실제 신고 완료 시각 설정
            state = Map26WitnessState.Witnessed; // 목격 직후 상태 적용

            agent = agent != null ? agent : GetComponent<MapCitizenAgent>(); // 시민 생활 AI 참조 복구
            if (agent != null) // 생활 AI 존재 확인
            {
                agent.BeginFlee(crimePosition); // 범죄 위치 반대 방향으로 즉시 도주 시작
            }

            return true; // 신고 시작 성공
        }

        private void OnGUI() // 신고 중 시민 머리 위 상태 표시
        {
            if (state == Map26WitnessState.None) // 평상 시민 확인
            {
                return; // UI 표시 없음
            }

            Camera camera = Camera.main; // 현재 게임 카메라 조회
            if (camera == null) // 카메라 존재 확인
            {
                return; // 월드 UI 표시 생략
            }

            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * 2.45f); // 시민 머리 위 화면 좌표 계산
            if (screen.z <= 0f) // 카메라 뒤쪽 시민 확인
            {
                return; // 표시 생략
            }

            EnsureStyle(); // 월드 UI 스타일 준비
            string text = state == Map26WitnessState.Witnessed ? "!" : state == Map26WitnessState.Reporting ? "신고 중" : "신고 완료"; // 현재 상태 문구 선택
            Vector2 size = labelStyle.CalcSize(new GUIContent(text)); // 실제 문구 크기 계산
            Rect rect = new Rect(screen.x - size.x * 0.5f - 8f, Screen.height - screen.y - size.y * 0.5f - 4f, size.x + 16f, size.y + 8f); // 머리 위 라벨 위치 계산
            GUI.Box(rect, text, labelStyle); // 신고 상태 표시
        }

        private void EnsureStyle() // 신고 상태 UI 스타일 생성
        {
            if (labelStyle != null) // 기존 스타일 존재 확인
            {
                return; // 중복 생성 방지
            }

            labelStyle = new GUIStyle(GUI.skin.box); // 기본 박스 스타일 복사
            labelStyle.fontSize = 13; // 월드 UI 글자 크기 적용
            labelStyle.fontStyle = FontStyle.Bold; // 신고 상태 강조
            labelStyle.alignment = TextAnchor.MiddleCenter; // 중앙 정렬 적용
            labelStyle.normal.textColor = new Color(1f, 0.82f, 0.20f, 1f); // 노란 경고 글자색 적용
        }

        private void ResetState() // 풀링과 신고 완료 후 상태 초기화
        {
            owner = null; // 중앙 관리자 참조 제거
            state = Map26WitnessState.None; // 평상 상태 복귀
            crimeType = default; // 범죄 종류 초기화
            incidentId = -1; // 사건 번호 초기화
            directIdentity = false; // 직접 식별 상태 초기화
            knownPosition = Vector3.zero; // 신고 위치 초기화
            panicUntil = float.PositiveInfinity; // 놀람 시각 초기화
            reportAt = float.PositiveInfinity; // 신고 시각 초기화
            reportedUntil = float.PositiveInfinity; // 완료 표시 시각 초기화
        }
    }
}
