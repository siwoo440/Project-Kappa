using UnityEngine; // 시민 이동과 상태 처리

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    [DisallowMultipleComponent] // 시민 AI 중복 방지
    public sealed class MapCitizenAgent : MonoBehaviour // 보도와 횡단보도를 따라 움직이는 비전투 시민
    {
        [SerializeField] private MapCitizenKind kind; // 시민 외형 유형
        [SerializeField] private MapCitizenState state; // 현재 생활 상태
        [SerializeField] private float walkSpeed = 2.1f; // 일반 보행 속도
        [SerializeField] private float fleeSpeed = 4.8f; // 총성 도주 속도
        [SerializeField] private float turnSpeed = 9f; // 방향 회전 속도
        private MapCityLifeManager manager; // 도시 생활 관리자 참조
        private int currentNode = -1; // 현재 보행 노드 번호
        private int targetNode = -1; // 현재 목표 노드 번호
        private int previousNode = -1; // 직전 노드 번호
        private float waitTimer; // 현재 대기 남은 시간
        private float fleeUntil; // 도주 종료 시각
        private Vector3 threatPosition; // 도주 원인이 된 총성 위치
        private bool initialized; // 초기화 여부
        public MapCitizenKind Kind => kind; // 시민 유형 조회
        public MapCitizenState State => state; // 현재 상태 조회
        public int CurrentNode => currentNode; // 현재 노드 조회
        public int TargetNode => targetNode; // 목표 노드 조회

        public void Initialize(MapCityLifeManager owner, MapCitizenKind citizenKind) // 풀 생성 뒤 시민 초기화
        {
            manager = owner; // 도시 생활 관리자 저장
            kind = citizenKind; // 시민 유형 저장
            walkSpeed = kind == MapCitizenKind.Mechanical ? 1.85f : kind == MapCitizenKind.Android ? 2.25f : 2.05f; // 유형별 보행 속도 차이 적용
            initialized = true; // 초기화 완료 저장
        }

        public void ActivateAt(int nodeIndex) // 풀 시민을 보행 노드에서 활성화
        {
            if (!initialized || manager == null || manager.PedestrianGraph == null) // 필수 그래프 확인
            {
                return; // 잘못된 활성화 중단
            }
            currentNode = nodeIndex; // 시작 노드 저장
            previousNode = -1; // 직전 노드 초기화
            targetNode = -1; // 목표 노드 초기화
            state = MapCitizenState.Idle; // 시작은 짧은 대기 상태
            waitTimer = manager.RandomRange(0.2f, 1.4f); // 시민마다 시작 시간 분산
            fleeUntil = 0f; // 도주 상태 초기화
            transform.position = manager.PedestrianGraph.Get(currentNode).Position; // 실제 보도 위치 적용
            gameObject.SetActive(true); // 시민 표시
        }

        private void OnEnable() // 활성 시민 소음 이벤트 연결
        {
            NoiseSystem.NoiseEmitted += OnNoise; // 플레이어 총성 이벤트 구독
        }

        private void OnDisable() // 풀 회수 시 이벤트 연결 해제
        {
            NoiseSystem.NoiseEmitted -= OnNoise; // 정적 이벤트 참조 누수 방지
            targetNode = -1; // 목표 노드 초기화
            currentNode = -1; // 현재 노드 초기화
            previousNode = -1; // 이전 노드 초기화
        }

        private void Update() // 시민 생활 행동 갱신
        {
            if (manager == null || manager.World == null || manager.World.Player == null || manager.PedestrianGraph == null) // 필수 참조 확인
            {
                return; // 생활 처리 중단
            }
            if ((transform.position - manager.World.Player.transform.position).sqrMagnitude > manager.CitizenDespawnDistance * manager.CitizenDespawnDistance) // 플레이어와 멀어진 시민 확인
            {
                manager.RecycleCitizen(this); // 먼 시민 풀 회수
                return; // 현재 프레임 종료
            }
            if (state == MapCitizenState.Flee && Time.time >= fleeUntil) // 도주 시간 종료 확인
            {
                state = MapCitizenState.Walk; // 일반 보행 상태 복귀
                ChooseRegularTarget(); // 새 생활 목적지 선택
            }
            if (state == MapCitizenState.Idle) // 목적지 대기 상태 확인
            {
                UpdateIdle(); // 대기 시간 갱신
                return; // 보행 처리 생략
            }
            if (targetNode < 0) // 목표 노드 누락 확인
            {
                ChooseRegularTarget(); // 정상 이동 목표 선택
                return; // 다음 프레임 이동
            }
            bool crossing = manager.PedestrianGraph.IsCrosswalk(currentNode, targetNode); // 현재 연결이 횡단보도인지 확인
            if (crossing && state != MapCitizenState.Flee && !manager.IsCrosswalkSafe(transform.position)) // 차량 접근 중 일반 횡단 확인
            {
                state = MapCitizenState.Crosswalk; // 횡단 대기 상태 적용
                return; // 차량이 지나갈 때까지 정지
            }
            if (state == MapCitizenState.Crosswalk) // 횡단 대기 해제 확인
            {
                state = MapCitizenState.Walk; // 안전해진 횡단 이동 시작
            }
            MoveToTarget(); // 현재 목표 노드로 이동
        }

        private void UpdateIdle() // 목적지에서 생활 대기 처리
        {
            waitTimer -= Time.deltaTime; // 대기 시간 감소
            if (waitTimer > 0f) // 남은 대기 시간 확인
            {
                return; // 계속 대기
            }
            state = MapCitizenState.Walk; // 보행 상태 전환
            ChooseRegularTarget(); // 다음 목적지 선택
        }

        private void MoveToTarget() // 목표 보행 노드까지 실제 이동
        {
            Vector3 target = manager.PedestrianGraph.Get(targetNode).Position; // 목표 월드 좌표 조회
            Vector3 delta = target - transform.position; // 목표 방향 계산
            delta.y = 0f; // 평면 보행 방향만 사용
            if (delta.sqrMagnitude > 0.002f) // 회전 필요 여부 확인
            {
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up); // 목표 방향 회전 계산
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turnSpeed * Time.deltaTime); // 부드러운 방향 전환
            }
            float speed = state == MapCitizenState.Flee ? fleeSpeed : walkSpeed; // 현재 상태 이동 속도 선택
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime); // 보도 노드 방향 이동
            if ((transform.position - target).sqrMagnitude <= 0.04f) // 목표 노드 도착 확인
            {
                previousNode = currentNode; // 직전 노드 저장
                currentNode = targetNode; // 현재 노드 갱신
                if (state == MapCitizenState.Flee) // 도주 중 목표 도착 확인
                {
                    ChooseFleeTarget(); // 위협에서 더 먼 이웃 선택
                }
                else // 일반 생활 상태
                {
                    state = MapCitizenState.Idle; // 잠시 머무는 상태 전환
                    waitTimer = manager.RandomRange(2.5f, 7.5f); // 생활 대기 시간 무작위 적용
                    targetNode = -1; // 다음 이동까지 목표 해제
                }
            }
        }

        private void ChooseRegularTarget() // 이전 노드를 되도록 피한 일반 보행 목표 선택
        {
            if (currentNode < 0) // 현재 노드 확인
            {
                return; // 선택 중단
            }
            int[] neighbors = manager.PedestrianGraph.Neighbors(currentNode); // 연결된 이웃 목록 조회
            if (neighbors.Length == 0) // 막힌 보행 노드 확인
            {
                state = MapCitizenState.Idle; // 현재 위치 대기
                waitTimer = 1f; // 짧은 재시도 시간
                return; // 목표 선택 종료
            }
            int start = manager.RandomIndex(neighbors.Length); // 무작위 탐색 시작 번호 선택
            int selected = neighbors[start]; // 기본 후보 선택
            for (int i = 0; i < neighbors.Length; i++) // 이웃 후보 순회
            {
                int candidate = neighbors[(start + i) % neighbors.Length]; // 현재 후보 조회
                if (candidate != previousNode || neighbors.Length == 1) // 즉시 되돌아가기 최소화
                {
                    selected = candidate; // 새 목적지 선택
                    break; // 탐색 종료
                }
            }
            targetNode = selected; // 실제 목표 노드 저장
        }

        private void OnNoise(NoiseEvent noise) // 총성에 대한 시민 도주 반응
        {
            if (!isActiveAndEnabled || noise.Type != NoiseType.Gunshot) // 활성 시민의 총성만 처리
            {
                return; // 다른 소음 유형 제외
            }
            float range = noise.Radius * 1.25f; // 시민 공포 반응에 약간 넓은 여유 적용
            if ((transform.position - noise.Position).sqrMagnitude > range * range) // 총성 영향 범위 확인
            {
                return; // 먼 총성 무시
            }
            BeginFlee(noise.Position); // 총성 반대 방향 도주 시작
        }

        public void BeginFlee(Vector3 threat) // 외부 교전 시스템에서도 사용할 수 있는 도주 시작
        {
            threatPosition = threat; // 위협 위치 저장
            fleeUntil = Time.time + manager.RandomRange(5f, 8f); // 도주 지속 시간 설정
            state = MapCitizenState.Flee; // 도주 상태 적용
            waitTimer = 0f; // 생활 대기 즉시 해제
            ChooseFleeTarget(); // 위협에서 먼 보행 노드 선택
        }

        private void ChooseFleeTarget() // 위협에서 가장 멀어지는 연결 노드 선택
        {
            if (currentNode < 0) // 현재 노드 확인
            {
                return; // 선택 중단
            }
            int[] neighbors = manager.PedestrianGraph.Neighbors(currentNode); // 이웃 노드 조회
            if (neighbors.Length == 0) // 이동할 노드 없음 확인
            {
                return; // 현재 위치 유지
            }
            float best = float.MinValue; // 가장 먼 후보 거리 초기값
            int selected = neighbors[0]; // 첫 이웃 기본 선택
            foreach (int candidate in neighbors) // 모든 연결 후보 순회
            {
                Vector3 point = manager.PedestrianGraph.Get(candidate).Position; // 후보 위치 조회
                float distance = (point - threatPosition).sqrMagnitude; // 위협과 거리 제곱 계산
                if (distance > best) // 더 안전한 후보 확인
                {
                    best = distance; // 최대 거리 갱신
                    selected = candidate; // 안전한 노드 저장
                }
            }
            targetNode = selected; // 실제 도주 목표 적용
        }
    }
}
