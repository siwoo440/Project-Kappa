using UnityEngine; // 시민 이동과 상태 처리

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    [DisallowMultipleComponent] // 시민 AI 중복 방지
    public sealed class MapCitizenAgent : MonoBehaviour // 평상시는 보도, 도주는 자유 이동을 사용하는 비전투 시민
    {
        [SerializeField] private MapCitizenKind kind; // 시민 외형 유형
        [SerializeField] private MapCitizenState state; // 현재 생활 상태
        [SerializeField] private float walkSpeed = 2.1f; // 일반 보행 속도
        [SerializeField] private float fleeSpeed = 5.2f; // 자유 도주 속도
        [SerializeField] private float turnSpeed = 9f; // 방향 회전 속도
        private const float fleeProbeDistance = 2.6f; // 도주 중 고정 장애물 검사 거리
        private const float fleeProbeRadius = 0.26f; // 도주 중 장애물 검사 반경

        private MapCityLifeManager manager; // 도시 생활 관리자 참조
        private Rigidbody body; // 시민 기동 강체 참조
        private int currentNode = -1; // 현재 보행 노드 번호
        private int targetNode = -1; // 현재 목표 노드 번호
        private int previousNode = -1; // 직전 노드 번호
        private float waitTimer; // 현재 대기 남은 시간
        private float fleeUntil; // 도주 종료 시각
        private float nextFleeSteer; // 다음 자유 도주 방향 갱신 시각
        private float fleeSideSign = 1f; // 개체별 좌우 도주 편향
        private Vector3 threatPosition; // 도주 원인이 된 총성 위치
        private Vector3 fleeDirection; // 현재 자유 도주 방향
        private bool initialized; // 초기화 여부
        public MapCitizenKind Kind => kind; // 시민 유형 조회
        public MapCitizenState State => state; // 현재 상태 조회
        public int CurrentNode => currentNode; // 현재 노드 조회
        public int TargetNode => targetNode; // 목표 노드 조회

        private void Awake() // 런타임 이동 참조 준비
        {
            body = GetComponent<Rigidbody>(); // 프리팹의 기동 강체 조회
        }

        public void Initialize(MapCityLifeManager owner, MapCitizenKind citizenKind) // 풀 생성 뒤 시민 초기화
        {
            manager = owner; // 도시 생활 관리자 저장
            kind = citizenKind; // 시민 유형 저장
            walkSpeed = kind == MapCitizenKind.Mechanical ? 1.85f : kind == MapCitizenKind.Android ? 2.25f : 2.05f; // 유형별 보행 속도 차이 적용
            fleeSpeed = kind == MapCitizenKind.Mechanical ? 4.7f : kind == MapCitizenKind.Android ? 5.5f : 5.2f; // 유형별 도주 속도 적용
            body = body != null ? body : GetComponent<Rigidbody>(); // 늦은 초기화 강체 참조 보정
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
            nextFleeSteer = 0f; // 자유 도주 방향 갱신 시각 초기화
            fleeDirection = Vector3.zero; // 이전 도주 방향 제거
            fleeSideSign = GetInstanceID() % 2 == 0 ? 1f : -1f; // 개체별 좌우 도주 방향 분산
            Vector3 spawn = manager.PedestrianGraph.Get(currentNode).Position; // 실제 보도 시작 위치 조회
            if (body != null) // 기동 강체 존재 확인
            {
                body.position = spawn; // 풀 시민 위치 적용
                body.linearVelocity = Vector3.zero; // 이전 물리 속도 제거
                body.angularVelocity = Vector3.zero; // 이전 회전 속도 제거
            }
            transform.position = spawn; // 강체 없는 경우와 트랜스폼 동기화
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
            fleeDirection = Vector3.zero; // 도주 방향 초기화
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

            if (state == MapCitizenState.Flee) // 총성·공격 도주 상태 확인
            {
                if (Time.time >= fleeUntil) // 자유 도주 종료 시간 확인
                {
                    EndFreeFlee(); // 가장 가까운 보행 노드로 복귀해 평상 생활 재개
                }
                else // 아직 도주 중
                {
                    UpdateFreeFlee(); // 횡단보도·차량 대기 없이 위협 반대 방향 자유 이동
                }
                return; // 도주 중 일반 보행 그래프 로직 완전 생략
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
            bool crossing = manager.PedestrianGraph.IsCrosswalk(currentNode, targetNode); // 평상시 연결이 횡단보도인지 확인
            if (crossing && !manager.IsCrosswalkSafe(transform.position)) // 평상시 횡단 전 차량 확인
            {
                state = MapCitizenState.Crosswalk; // 횡단 대기 상태 적용
                return; // 차량이 지나갈 때까지 정지
            }
            if (state == MapCitizenState.Crosswalk) // 횡단 대기 해제 확인
            {
                state = MapCitizenState.Walk; // 안전해진 횡단 이동 시작
            }
            MoveToTarget(); // 현재 목표 보행 노드로 이동
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

        private void MoveToTarget() // 평상시 목표 보행 노드까지 이동
        {
            Vector3 target = manager.PedestrianGraph.Get(targetNode).Position; // 목표 월드 좌표 조회
            Vector3 delta = target - transform.position; // 목표 방향 계산
            delta.y = 0f; // 평면 보행 방향만 사용
            if (delta.sqrMagnitude > 0.002f) // 회전 필요 여부 확인
            {
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up); // 목표 방향 회전 계산
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turnSpeed * Time.deltaTime); // 부드러운 방향 전환
            }
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime); // 평상시 보행 노드 방향 이동
            if ((transform.position - target).sqrMagnitude <= 0.04f) // 목표 노드 도착 확인
            {
                previousNode = currentNode; // 직전 노드 저장
                currentNode = targetNode; // 현재 노드 갱신
                state = MapCitizenState.Idle; // 잠시 머무는 상태 전환
                waitTimer = manager.RandomRange(2.5f, 7.5f); // 생활 대기 시간 무작위 적용
                targetNode = -1; // 다음 이동까지 목표 해제
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
            BeginFlee(noise.Position); // 총성 반대 방향 자유 도주 시작
        }

        public void BeginFlee(Vector3 threat) // 외부 교전 시스템에서도 사용할 수 있는 자유 도주 시작
        {
            threatPosition = threat; // 위협 위치 저장
            fleeUntil = Time.time + manager.RandomRange(5f, 8f); // 도주 지속 시간 설정
            state = MapCitizenState.Flee; // 도주 상태 적용
            waitTimer = 0f; // 생활 대기 즉시 해제
            targetNode = -1; // 횡단보도·보행 노드 목적지 완전 해제
            Vector3 away = transform.position - threatPosition; // 위협 반대 방향 계산
            away.y = 0f; // 평면 도주 방향 사용
            if (away.sqrMagnitude <= 0.001f) // 위협과 거의 같은 위치 확인
            {
                away = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward; // 현재 전방 또는 기본 방향 사용
            }
            float spread = manager.RandomRange(18f, 34f) * fleeSideSign; // 시민끼리 한 줄로 겹치지 않도록 좌우 분산
            fleeDirection = Quaternion.Euler(0f, spread, 0f) * away.normalized; // 첫 자유 도주 방향 생성
            nextFleeSteer = 0f; // 첫 프레임 즉시 장애물 회피 방향 확인
        }

        private void UpdateFreeFlee() // 횡단보도와 차량 대기 규칙을 무시한 자유 도주
        {
            Vector3 away = transform.position - threatPosition; // 현재 위치에서 위협 반대 방향 재계산
            away.y = 0f; // 평면 방향만 사용
            if (away.sqrMagnitude <= 0.001f) // 유효한 위협 반대 방향 확인
            {
                away = fleeDirection.sqrMagnitude > 0.001f ? fleeDirection : transform.forward; // 기존 도주 방향 유지
            }
            if (Time.time >= nextFleeSteer || fleeDirection.sqrMagnitude <= 0.001f) // 장애물 회피 갱신 시점 확인
            {
                Vector3 desired = away.normalized; // 위협에서 직접 멀어지는 기본 방향
                fleeDirection = FindFreeFleeDirection(desired); // 건물·벽만 간단히 우회하는 자유 방향 선택
                nextFleeSteer = Time.time + 0.22f; // 너무 잦은 좌우 떨림 방지
            }
            Vector3 movement = fleeDirection.normalized * fleeSpeed * Time.deltaTime; // 이번 프레임 자유 도주 이동량 계산
            if (body != null && body.isKinematic) // 기동 강체가 준비된 시민 확인
            {
                body.MovePosition(transform.position + movement); // 도로·횡단보도 규칙 없이 직접 이동
            }
            else // 강체가 없는 경우 처리
            {
                transform.position += movement; // 직접 트랜스폼 이동
            }
            if (fleeDirection.sqrMagnitude > 0.001f) // 회전 가능한 도주 방향 확인
            {
                Quaternion rotation = Quaternion.LookRotation(fleeDirection.normalized, Vector3.up); // 도주 방향 회전 계산
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turnSpeed * 1.35f * Time.deltaTime); // 일반 보행보다 빠른 도주 회전
            }
        }

        private Vector3 FindFreeFleeDirection(Vector3 desired) // 앞 건물에 막히면 좌우 방향으로 자유 우회
        {
            if (IsFleeDirectionOpen(desired)) // 정면 도주 가능 여부 확인
            {
                return desired; // 가장 빠른 위협 반대 방향 사용
            }
            float[] angles = { 35f, -35f, 70f, -70f, 110f, -110f }; // 단계별 우회 각도 후보
            foreach (float raw in angles) // 우회 후보 순회
            {
                float angle = raw * fleeSideSign; // 개체별 좌우 우선 방향 적용
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desired; // 우회 도주 방향 계산
                if (IsFleeDirectionOpen(candidate)) // 해당 방향 고정 장애물 확인
                {
                    return candidate.normalized; // 첫 통과 가능한 자유 방향 사용
                }
            }
            return desired; // 완전히 막힌 경우에도 위협 반대 방향 유지
        }

        private bool IsFleeDirectionOpen(Vector3 direction) // 시민 도주 앞쪽의 고정 장애물만 검사
        {
            Vector3 origin = transform.position + Vector3.up * 0.85f; // 허리 높이 검사 원점
            RaycastHit[] hits = Physics.SphereCastAll(origin, fleeProbeRadius, direction.normalized, fleeProbeDistance, ~0, QueryTriggerInteraction.Ignore); // 짧은 전방 장애물 조회
            foreach (RaycastHit hit in hits) // 충돌 후보 순회
            {
                Collider collider = hit.collider; // 현재 충돌체 조회
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) // 자기 몸 제외
                {
                    continue; // 다음 충돌체 검사
                }
                if (collider.GetComponentInParent<MapCitizenAgent>() != null) // 다른 시민 확인
                {
                    continue; // 도주 군중은 고정 벽처럼 취급하지 않음
                }
                if (collider.GetComponentInParent<MapTrafficVehicle>() != null) // 이동 차량 확인
                {
                    continue; // 도주 방향 선택에서는 차량 때문에 횡단보도를 기다리지 않음
                }
                if (collider.GetComponentInParent<ProjectK.Day21.MapWantedGuardAgent>() != null) // 수배 경비 확인
                {
                    continue; // 도망 중 경비를 고정 벽처럼 보지 않음
                }
                return false; // 건물·벽·고정 구조물에 막힌 방향
            }
            return true; // 자유 도주 가능 방향
        }

        private void EndFreeFlee() // 자유 도주가 끝난 뒤 평상 보행 그래프로 복귀
        {
            currentNode = FindNearestPedestrianNode(transform.position); // 현재 위치와 가장 가까운 보행 노드 재연결
            previousNode = -1; // 이전 노드 이력 초기화
            targetNode = -1; // 목표 노드 초기화
            fleeDirection = Vector3.zero; // 자유 도주 방향 제거
            state = MapCitizenState.Walk; // 평상 보행 상태 복귀
            ChooseRegularTarget(); // 새로운 일반 생활 경로 선택
        }

        private int FindNearestPedestrianNode(Vector3 position) // 자유 도주 종료 위치와 가장 가까운 보행 노드 검색
        {
            if (manager == null || manager.PedestrianGraph == null || manager.PedestrianGraph.Count <= 0) // 보행 그래프 확인
            {
                return -1; // 복귀할 노드 없음
            }
            int bestNode = 0; // 최근접 노드 초기값
            float bestDistance = float.PositiveInfinity; // 최근접 거리 초기값
            for (int i = 0; i < manager.PedestrianGraph.Count; i++) // 전체 보행 노드 순회
            {
                Vector3 point = manager.PedestrianGraph.Get(i).Position; // 현재 노드 위치 조회
                Vector2 delta = new Vector2(point.x - position.x, point.z - position.z); // 수평 거리 계산
                float distance = delta.sqrMagnitude; // 거리 점수 계산
                if (distance < bestDistance) // 더 가까운 노드 확인
                {
                    bestDistance = distance; // 최소 거리 갱신
                    bestNode = i; // 최근접 노드 저장
                }
            }
            return bestNode; // 평상 보행 복귀 노드 반환
        }
    }
}
