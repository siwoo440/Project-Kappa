using System.Reflection; // Day31 런타임 목표 등록 API 안전 호출
using ProjectK.Day16; // MapWorldRoot·MapPoint 참조
using ProjectK.Day31; // MissionManager·MissionInteractable 참조
using UnityEngine; // 런타임 M-02 월드 오브젝트 생성
using UnityEngine.SceneManagement; // 씬 전환 월드 복구

namespace ProjectK.Day33 // 33일차 M-02 잠입 암살 이름 공간
{
    [DisallowMultipleComponent] // M-02 월드 관리자 중복 방지
    public sealed class Map33MissionWorldController : MonoBehaviour // 조사·표적·증거·탈출 오브젝트 자동 구성
    {
        private static Map33MissionWorldController instance; // 현재 월드 컨트롤러
        private MapWorldRoot world; // 현재 본편 월드
        private GameObject runtimeRoot; // 현재 씬 M-02 루트
        private Transform briefingTerminal; // 린 브리핑 단말기
        private Transform zoneEntry; // 감시 구역 진입 지점
        private Transform verifyTerminal; // 표적 신원 확인 단말기
        private Transform targetTransform; // 암살 표적 위치
        private EnemyActor targetActor; // 실제 표적 생명 상태
        private GameObject ledgerObject; // 암호화 장부 증거
        private Transform escapePoint; // 감시 구역 탈출 지점
        private Transform deliveryTerminal; // 린 장부 인계 단말기
        private GameObject zoneMarkerVisual; // 감시 구역 시각 표식
        private GameObject escapeMarkerVisual; // 탈출 지점 시각 표식
        private Material bodyMaterial; // 단말기 본체 재질
        private Material cyanMaterial; // 청록 발광 재질
        private Material targetMaterial; // 표적 외형 재질
        private Material evidenceMaterial; // 증거 외형 재질
        private float nextResolveTime; // 다음 월드 검색 시각
        private float nextRegisterTime; // 다음 런타임 목표 등록 시각
        private int worldInstanceId; // 현재 월드 인스턴스 ID
        private string lastMissionId = string.Empty; // 마지막 활성 임무 ID
        private int lastObjectiveIndex = -1; // 마지막 M-02 목표 순번

        public static Map33MissionWorldController Instance => instance; // 현재 컨트롤러 조회

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 플레이 시작 정적 상태 초기화
        {
            instance = null; // 이전 컨트롤러 참조 제거
        }

        private void Awake() // 단일 컨트롤러 등록
        {
            if (instance != null && instance != this) // 다른 컨트롤러 존재 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 컨트롤러 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
        }

        private void OnEnable() // 씬 전환 참조 복구 연결
        {
            SceneManager.activeSceneChanged += HandleSceneChanged; // 활성 씬 변경 감지
        }

        private void OnDisable() // 씬 전환 이벤트 해제
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged; // 정적 이벤트 참조 해제
        }

        private void Start() // 첫 Map 월드 구성
        {
            ResolveWorld(); // 현재 본편 월드 검색
        }

        private void Update() // 월드 복구·목표 등록·M-02 단계 시각 상태 동기화
        {
            if (world == null || world.Player == null || worldInstanceId != world.GetInstanceID()) // 유효 월드 참조 확인
            {
                if (Time.unscaledTime >= nextResolveTime) // 재검색 시각 확인
                {
                    nextResolveTime = Time.unscaledTime + 0.5f; // 다음 검색 예약
                    ResolveWorld(); // 월드와 M-02 오브젝트 복구
                }

                return; // 현재 프레임 상태 동기화 생략
            }

            Map31MissionManager manager = Map31MissionManager.Instance; // 중앙 MissionManager 조회

            if (manager == null) // MissionManager 생성 순서 확인
            {
                return; // 다음 프레임 대기
            }

            if (Time.unscaledTime >= nextRegisterTime) // Day33 패처 적용 뒤 런타임 목표 재등록 주기 확인
            {
                nextRegisterTime = Time.unscaledTime + 0.6f; // 다음 등록 확인 예약
                RegisterAllRuntimeTargets(manager); // Day31 private 목표 사전에 M-02 Transform 등록
            }

            string activeMissionId = manager.ActiveMissionId; // 현재 활성 임무 ID 조회
            int objectiveIndex = activeMissionId == "M-02" ? manager.ActiveObjectiveIndex : -1; // M-02 현재 목표 순번 조회

            if (activeMissionId == lastMissionId && objectiveIndex == lastObjectiveIndex) // 임무 단계 변화 여부 확인
            {
                return; // 불필요한 오브젝트 상태 변경 생략
            }

            lastMissionId = activeMissionId; // 현재 임무 ID 저장
            lastObjectiveIndex = objectiveIndex; // 현재 목표 순번 저장

            if (activeMissionId != "M-02") // M-02 비활성 상태 확인
            {
                SetDynamicMissionObjects(false, false, false); // 표적·증거·탈출 표식 숨김
                return; // M-02 단계 처리 종료
            }

            SyncObjectiveState(objectiveIndex); // 현재 M-02 목표에 맞는 월드 상태 적용
        }

        private void ResolveWorld() // 현재 MapWorldRoot 검색과 M-02 오브젝트 생성
        {
            MapWorldRoot[] worlds = UnityEngine.Object.FindObjectsByType<MapWorldRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 본편 월드 검색
            MapWorldRoot found = worlds.Length > 0 ? worlds[0] : null; // 첫 월드 선택

            if (found == null || found.Player == null || found.Places == null || found.Places.Length == 0) // 필수 월드 자료 확인
            {
                world = found; // 생성 순서 중인 월드 참조 보관
                return; // 다음 검색까지 대기
            }

            if (world == found && runtimeRoot != null && worldInstanceId == found.GetInstanceID()) // 현재 월드가 이미 구성됐는지 확인
            {
                return; // 중복 월드 오브젝트 생성 방지
            }

            DestroyRuntimeRoot(); // 이전 월드 런타임 오브젝트 정리
            world = found; // 새 월드 저장
            worldInstanceId = found.GetInstanceID(); // 새 월드 ID 저장
            BuildMissionWorld(); // M-02 조사·암살 공간 자동 구성
            nextRegisterTime = 0f; // 새 목표 즉시 등록 허용
            lastMissionId = string.Empty; // 상태 재동기화 강제
            lastObjectiveIndex = -1; // 목표 순번 재동기화 강제
        }

        private void BuildMissionWorld() // LIN_HOME·GYEOPGIL_PLAZA 기준 M-02 런타임 오브젝트 배치
        {
            MapPoint lin = FindPlace("LIN_HOME"); // 린 옥상 작업실 조회
            MapPoint gyeopgil = FindPlace("GYEOPGIL_PLAZA"); // 겹길 시장 조회

            if (lin == null || lin.Arrival == null || gyeopgil == null || gyeopgil.Arrival == null) // 필수 장소 확인
            {
                return; // M-02 공간 생성 중단
            }

            EnsureMaterials(); // 공용 런타임 재질 준비
            runtimeRoot = new GameObject("[Day33] M-02 Runtime Objects"); // M-02 현재 씬 루트 생성
            runtimeRoot.transform.SetParent(world.transform, false); // 월드 씬과 함께 제거되도록 연결

            briefingTerminal = CreateTerminal(
                "M02_BriefingTerminal",
                lin.Arrival.position + lin.Arrival.right * -2.1f + Vector3.up * 0.82f + lin.Arrival.forward * 0.7f,
                lin.Arrival.rotation,
                "M-02",
                "BRIEF_LIN",
                "M-02 분석 자료 확인",
                null,
                null,
                null); // 린 브리핑 단말기 생성

            zoneEntry = CreatePoint(
                "M02_ZoneEntry",
                gyeopgil.Arrival.position + gyeopgil.Arrival.forward * 7f + gyeopgil.Arrival.right * -2f,
                gyeopgil.Arrival.rotation); // 감시 구역 진입 위치 생성
            zoneMarkerVisual = CreateMarker("M02_ZoneMarker", zoneEntry, cyanMaterial, 2.3f); // 감시 구역 청록 표식 생성

            verifyTerminal = CreateTerminal(
                "M02_VerifyTerminal",
                gyeopgil.Arrival.position + gyeopgil.Arrival.forward * 10f + gyeopgil.Arrival.right * -4.5f + Vector3.up * 0.82f,
                gyeopgil.Arrival.rotation,
                "M-02",
                "VERIFY_TARGET",
                "표적 신원 확인",
                null,
                null,
                null); // 감시 구역 신원 확인 단말기 생성

            Vector3 targetPosition = gyeopgil.Arrival.position + gyeopgil.Arrival.forward * 13f + gyeopgil.Arrival.right * 4.5f + Vector3.up; // 표적 기본 위치 계산
            CreateLedger(targetPosition + gyeopgil.Arrival.right * 1.5f); // 암호화 장부 오브젝트 선생성
            RebuildTarget(targetPosition, gyeopgil.Arrival.rotation); // 암살 표적 생성

            escapePoint = CreatePoint(
                "M02_EscapePoint",
                gyeopgil.Arrival.position + gyeopgil.Arrival.forward * -8f + gyeopgil.Arrival.right * -7f,
                Quaternion.LookRotation(-gyeopgil.Arrival.forward, Vector3.up)); // 겹길 반대편 탈출 위치 생성
            escapeMarkerVisual = CreateMarker("M02_EscapeMarker", escapePoint, cyanMaterial, 2.8f); // 탈출 청록 표식 생성

            deliveryTerminal = CreateTerminal(
                "M02_DeliveryTerminal",
                lin.Arrival.position + lin.Arrival.right * 2.1f + Vector3.up * 0.82f + lin.Arrival.forward * 0.7f,
                lin.Arrival.rotation,
                "M-02",
                "DELIVER_LEDGER",
                "암호화 장부 인계",
                null,
                "CRYPTO_LEDGER",
                "CRYPTO_LEDGER"); // 린 최종 인계 단말기 생성

            SetDynamicMissionObjects(false, false, false); // 임무 수락 전 표적·증거·탈출 표식 숨김
        }

        private Transform CreateTerminal(string objectName, Vector3 position, Quaternion rotation, string missionId, string objectiveId, string label, string grantItem, string requiredItem, string deliverItem) // M-02 상호작용 단말기 생성
        {
            GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube); // 충돌 가능한 단말기 본체 생성
            terminal.name = objectName; // 오브젝트 이름 저장
            terminal.transform.SetParent(runtimeRoot.transform, false); // M-02 루트 연결
            terminal.transform.SetPositionAndRotation(position, rotation); // 월드 위치·방향 적용
            terminal.transform.localScale = new Vector3(1.0f, 1.45f, 0.46f); // 세로형 단말기 크기 적용
            Renderer bodyRenderer = terminal.GetComponent<Renderer>(); // 본체 Renderer 조회
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = bodyMaterial; // 남청색 본체 재질 적용

            Map31MissionInteractable interactable = terminal.AddComponent<Map31MissionInteractable>(); // 기존 F 상호작용 시스템 연결
            interactable.Configure(missionId, objectiveId, label, grantItem, requiredItem, deliverItem); // MissionManager 목표 ID 연결

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube); // 청록 화면 생성
            screen.name = "MissionScreen"; // 화면 오브젝트 이름
            screen.transform.SetParent(terminal.transform, false); // 단말기 자식 연결
            screen.transform.localPosition = new Vector3(0f, 0.10f, -0.53f); // 전면 화면 위치
            screen.transform.localScale = new Vector3(0.78f, 0.54f, 0.06f); // 얇은 화면 크기
            Renderer screenRenderer = screen.GetComponent<Renderer>(); // 화면 Renderer 조회
            if (screenRenderer != null) screenRenderer.sharedMaterial = cyanMaterial; // 청록 발광 재질 적용
            Collider screenCollider = screen.GetComponent<Collider>(); // 장식 충돌체 조회
            if (screenCollider != null) Destroy(screenCollider); // 상호작용 충돌 중복 제거

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상단 상태 라이트 생성
            beacon.name = "MissionBeacon"; // 라이트 이름
            beacon.transform.SetParent(terminal.transform, false); // 단말기 자식 연결
            beacon.transform.localPosition = new Vector3(0f, 0.58f, -0.52f); // 화면 위 상태선 위치
            beacon.transform.localScale = new Vector3(0.82f, 0.05f, 0.08f); // 얇은 상태선 크기
            Renderer beaconRenderer = beacon.GetComponent<Renderer>(); // 상태선 Renderer 조회
            if (beaconRenderer != null) beaconRenderer.sharedMaterial = cyanMaterial; // 발광 재질 적용
            Collider beaconCollider = beacon.GetComponent<Collider>(); // 상태선 충돌체 조회
            if (beaconCollider != null) Destroy(beaconCollider); // 장식 충돌 제거

            return terminal.transform; // HUD 추적용 Transform 반환
        }

        private void CreateLedger(Vector3 position) // 표적 사망 뒤 회수할 암호화 장부 생성
        {
            ledgerObject = GameObject.CreatePrimitive(PrimitiveType.Cube); // 장부 충돌 오브젝트 생성
            ledgerObject.name = "M02_EncryptedLedger"; // 증거 이름 적용
            ledgerObject.transform.SetParent(runtimeRoot.transform, false); // M-02 루트 연결
            ledgerObject.transform.position = position + Vector3.up * 0.55f; // 바닥보다 약간 위 배치
            ledgerObject.transform.localScale = new Vector3(0.48f, 0.12f, 0.68f); // 장부 형태 크기
            Renderer renderer = ledgerObject.GetComponent<Renderer>(); // 장부 Renderer 조회
            if (renderer != null) renderer.sharedMaterial = evidenceMaterial; // 보라·청록 증거 재질 적용

            Map31MissionInteractable interactable = ledgerObject.AddComponent<Map31MissionInteractable>(); // F 회수 상호작용 연결
            interactable.Configure("M-02", "ACQUIRE_LEDGER", "암호화 장부 회수", "CRYPTO_LEDGER", null, null); // 회수 시 임무 물품 지급
            ledgerObject.SetActive(false); // 표적 제거 전 증거 숨김
        }

        private void RebuildTarget(Vector3 position, Quaternion rotation) // 체크포인트·재수락에 맞는 암살 표적 재생성
        {
            if (targetTransform != null) // 이전 표적 존재 확인
            {
                Destroy(targetTransform.gameObject); // 사망 상태를 포함한 이전 표적 제거
            }

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 암살 가능한 표적 본체 생성
            target.name = "M02_Target"; // 임무 표적 이름 적용
            target.transform.SetParent(runtimeRoot.transform, false); // M-02 루트 연결
            target.transform.SetPositionAndRotation(position, rotation); // 기본 위치·방향 적용
            target.transform.localScale = new Vector3(0.72f, 1.0f, 0.72f); // 인간형 캡슐 비율 적용
            Renderer renderer = target.GetComponent<Renderer>(); // 표적 Renderer 조회
            if (renderer != null) renderer.sharedMaterial = targetMaterial; // 어두운 적색·남청 표적 재질 적용

            targetActor = target.AddComponent<EnemyActor>(); // 기존 적 생명·암살 시스템 연결
            targetActor.Configure(120f, 100f, true); // 체력·자세·암살 가능 상태 설정
            target.AddComponent<EnemyStatusController>(); // 기존 마비 상태 시스템 연결
            DetectionSensor sensor = target.AddComponent<DetectionSensor>(); // 기존 탐지 시스템 연결
            EnemyMeleeCombat melee = target.AddComponent<EnemyMeleeCombat>(); // 기존 근접 전투 시스템 연결
            melee.Configure(null, 22f, 34f); // 근접 반격 피해 설정

            Map33MissionTargetAI ai = target.AddComponent<Map33MissionTargetAI>(); // 고정형 경계·반격 AI 연결
            ai.Configure(world != null && world.Player != null ? world.Player.transform : null); // 플레이어 탐지 대상 연결
            sensor.Configure(world != null && world.Player != null ? world.Player.transform : null, 16f, 90f, 7f, ~0); // 시야·청각 설정

            Map33MissionTargetWatcher watcher = target.AddComponent<Map33MissionTargetWatcher>(); // 사망 이벤트 MissionManager 연결
            watcher.Configure(targetActor, ledgerObject, "M02_TARGET"); // 표적 키와 장부 연결

            targetTransform = target.transform; // HUD·MissionManager 추적 Transform 저장
            target.SetActive(false); // 신원 확인 전 표적 비활성화
        }

        private Transform CreatePoint(string objectName, Vector3 position, Quaternion rotation) // 위치형 목표용 빈 Transform 생성
        {
            GameObject point = new GameObject(objectName); // 목표 지점 오브젝트 생성
            point.transform.SetParent(runtimeRoot.transform, false); // M-02 루트 연결
            point.transform.SetPositionAndRotation(position, rotation); // 위치·방향 적용
            return point.transform; // HUD·도착 판정용 Transform 반환
        }

        private GameObject CreateMarker(string objectName, Transform anchor, Material material, float scale) // 위치 목표용 청록 시각 표식 생성
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 낮은 원형 표식 생성
            marker.name = objectName; // 표식 이름 적용
            marker.transform.SetParent(anchor, false); // 목표 지점 자식 연결
            marker.transform.localPosition = new Vector3(0f, 0.06f, 0f); // 지면보다 약간 위 배치
            marker.transform.localScale = new Vector3(scale, 0.03f, scale); // 얇은 원형 링 형태
            Renderer renderer = marker.GetComponent<Renderer>(); // 표식 Renderer 조회
            if (renderer != null) renderer.sharedMaterial = material; // 청록 발광 재질 적용
            Collider collider = marker.GetComponent<Collider>(); // 자동 생성 충돌체 조회
            if (collider != null) Destroy(collider); // 이동 방해 충돌 제거
            return marker; // 표시 상태 제어용 반환
        }

        private void SyncObjectiveState(int objectiveIndex) // 현재 M-02 단계에 맞는 표적·증거·탈출 상태 구성
        {
            if (runtimeRoot == null) // 런타임 오브젝트 준비 확인
            {
                return; // 상태 적용 생략
            }

            if (objectiveIndex <= 2) // 표적 신원 확인 이전 단계 확인
            {
                EnsureTargetAlive(false); // 살아 있는 표적을 숨김 상태로 준비
                SetDynamicMissionObjects(false, false, false); // 표적·장부·탈출 표식 숨김
            }
            else if (objectiveIndex == 3) // Eliminate 목표 단계 확인
            {
                EnsureTargetAlive(true); // 암살 가능한 살아 있는 표적 활성화
                SetDynamicMissionObjects(true, false, false); // 표적만 표시
            }
            else if (objectiveIndex == 4) // 암호화 장부 회수 단계 확인
            {
                if (targetTransform != null) targetTransform.gameObject.SetActive(false); // 처리된 표적 숨김
                if (ledgerObject != null) ledgerObject.SetActive(true); // 암호화 장부 활성화
                if (escapeMarkerVisual != null) escapeMarkerVisual.SetActive(false); // 탈출 표식 대기
            }
            else if (objectiveIndex == 5) // Escape 목표 단계 확인
            {
                SetDynamicMissionObjects(false, false, true); // 탈출 지점만 표시
            }
            else // 최종 린 인계 단계 처리
            {
                SetDynamicMissionObjects(false, false, false); // 전투 구역 동적 오브젝트 숨김
            }

            if (zoneMarkerVisual != null) zoneMarkerVisual.SetActive(objectiveIndex == 1); // 감시 구역 진입 단계에서만 표식 표시
            RegisterAllRuntimeTargets(Map31MissionManager.Instance); // 재생성된 표적 Transform까지 MissionManager에 갱신
        }

        private void EnsureTargetAlive(bool active) // 현재 목표 단계에 맞는 살아 있는 표적 보장
        {
            if (targetActor == null || targetTransform == null || targetActor.IsDead) // 표적 누락·사망 상태 확인
            {
                MapPoint gyeopgil = FindPlace("GYEOPGIL_PLAZA"); // 표적 기준 장소 재조회
                if (gyeopgil == null || gyeopgil.Arrival == null) // 재생성 위치 확인
                {
                    return; // 표적 복구 생략
                }

                Vector3 position = gyeopgil.Arrival.position + gyeopgil.Arrival.forward * 13f + gyeopgil.Arrival.right * 4.5f + Vector3.up; // 기본 표적 위치 재계산
                RebuildTarget(position, gyeopgil.Arrival.rotation); // 새 EnemyActor 표적 생성
            }

            if (targetTransform != null) targetTransform.gameObject.SetActive(active); // 요청 활성 상태 적용
        }

        private void SetDynamicMissionObjects(bool targetVisible, bool ledgerVisible, bool escapeVisible) // M-02 동적 오브젝트 표시 상태 일괄 적용
        {
            if (targetTransform != null) targetTransform.gameObject.SetActive(targetVisible); // 표적 표시 전환
            if (ledgerObject != null) ledgerObject.SetActive(ledgerVisible); // 암호화 장부 표시 전환
            if (escapeMarkerVisual != null) escapeMarkerVisual.SetActive(escapeVisible); // 탈출 표식 표시 전환
            if (zoneMarkerVisual != null && !targetVisible && !ledgerVisible && !escapeVisible) zoneMarkerVisual.SetActive(false); // 비활성 상태 진입 표식 정리
        }

        private void RegisterAllRuntimeTargets(Map31MissionManager manager) // Day31 MissionManager의 런타임 목표 사전에 M-02 위치 등록
        {
            if (manager == null || runtimeRoot == null) // 등록 필수 참조 확인
            {
                return; // 등록 생략
            }

            RegisterTarget(manager, "M02_BRIEF_TERMINAL", briefingTerminal); // 린 브리핑 목표 등록
            RegisterTarget(manager, "M02_ZONE_ENTRY", zoneEntry); // 감시 구역 진입 목표 등록
            RegisterTarget(manager, "M02_VERIFY_TERMINAL", verifyTerminal); // 신원 확인 단말기 등록
            RegisterTarget(manager, "M02_TARGET", targetTransform); // 암살 표적 등록
            RegisterTarget(manager, "M02_LEDGER", ledgerObject != null ? ledgerObject.transform : null); // 암호화 장부 등록
            RegisterTarget(manager, "M02_ESCAPE", escapePoint); // 탈출 지점 등록
            RegisterTarget(manager, "M02_DELIVERY_TERMINAL", deliveryTerminal); // 린 인계 단말기 등록
        }

        private static void RegisterTarget(Map31MissionManager manager, string key, Transform target) // 패처가 추가하는 공개 등록 API 안전 호출
        {
            if (manager == null || target == null) // 유효 등록 자료 확인
            {
                return; // 등록 생략
            }

            MethodInfo method = manager.GetType().GetMethod("RegisterRuntimeTarget", BindingFlags.Instance | BindingFlags.Public); // Day33 확장 API 조회
            method?.Invoke(manager, new object[] { key, target }); // 패치 적용 뒤에만 런타임 목표 등록
        }

        private MapPoint FindPlace(string placeId) // 현재 월드 MapPoint ID 검색
        {
            if (world == null || world.Places == null || string.IsNullOrWhiteSpace(placeId)) // 검색 자료 확인
            {
                return null; // 장소 없음
            }

            for (int i = 0; i < world.Places.Length; i++) // 주요 장소 순회
            {
                MapPoint place = world.Places[i]; // 현재 장소 조회
                if (place != null && place.PlaceId == placeId) // 고정 ID 일치 확인
                {
                    return place; // 장소 반환
                }
            }

            return null; // 해당 장소 없음
        }

        private void EnsureMaterials() // M-02 런타임 오브젝트 공용 재질 준비
        {
            if (bodyMaterial != null && cyanMaterial != null && targetMaterial != null && evidenceMaterial != null) // 기존 재질 존재 확인
            {
                return; // 재생성 생략
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 기본 셰이더 우선 검색
            if (shader == null) shader = Shader.Find("Standard"); // Built-in 대체 셰이더 검색
            if (shader == null) shader = Shader.Find("Unlit/Color"); // 최종 단색 셰이더 검색

            bodyMaterial = CreateMaterial(shader, "Day33_M02_Body", new Color(0.025f, 0.065f, 0.10f, 1f), false); // 남청 단말기 재질
            cyanMaterial = CreateMaterial(shader, "Day33_M02_Cyan", new Color(0.04f, 0.78f, 0.96f, 1f), true); // 청록 발광 재질
            targetMaterial = CreateMaterial(shader, "Day33_M02_Target", new Color(0.18f, 0.035f, 0.055f, 1f), false); // 표적 어두운 적색 재질
            evidenceMaterial = CreateMaterial(shader, "Day33_M02_Evidence", new Color(0.22f, 0.10f, 0.44f, 1f), true); // 암호화 장부 보라 발광 재질
        }

        private static Material CreateMaterial(Shader shader, string materialName, Color color, bool emission) // URP·Built-in 호환 런타임 재질 생성
        {
            Material material = new Material(shader); // 런타임 재질 생성
            material.name = materialName; // 디버그 이름 적용
            material.color = color; // 기본 색상 적용
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP 기본 색상 적용
            if (material.HasProperty("_Color")) material.SetColor("_Color", color); // Built-in 색상 적용

            if (emission) // 발광 재질 여부 확인
            {
                material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f); // 발광 색상 적용
            }

            return material; // 완성 재질 반환
        }

        private void HandleSceneChanged(Scene previous, Scene current) // 씬 전환 시 이전 월드 참조 초기화
        {
            DestroyRuntimeRoot(); // 이전 씬 M-02 오브젝트 정리
            world = null; // 이전 월드 참조 제거
            worldInstanceId = 0; // 이전 월드 ID 초기화
            nextResolveTime = 0f; // 새 씬 즉시 검색 허용
            lastMissionId = string.Empty; // 상태 동기화 초기화
            lastObjectiveIndex = -1; // 목표 단계 초기화
        }

        private void DestroyRuntimeRoot() // 현재 월드에 생성한 M-02 오브젝트·참조 정리
        {
            if (runtimeRoot != null) Destroy(runtimeRoot); // 이전 런타임 루트 제거
            runtimeRoot = null; // 루트 참조 초기화
            briefingTerminal = null; // 브리핑 단말기 참조 초기화
            zoneEntry = null; // 감시 구역 참조 초기화
            verifyTerminal = null; // 신원 확인 단말기 참조 초기화
            targetTransform = null; // 표적 참조 초기화
            targetActor = null; // 표적 생명 참조 초기화
            ledgerObject = null; // 암호화 장부 참조 초기화
            escapePoint = null; // 탈출 지점 참조 초기화
            deliveryTerminal = null; // 인계 단말기 참조 초기화
            zoneMarkerVisual = null; // 구역 표식 참조 초기화
            escapeMarkerVisual = null; // 탈출 표식 참조 초기화
        }

        private void OnDestroy() // 컨트롤러 종료 시 런타임 재질 정리
        {
            DestroyRuntimeRoot(); // 현재 M-02 오브젝트 제거
            if (bodyMaterial != null) Destroy(bodyMaterial); // 본체 재질 제거
            if (cyanMaterial != null) Destroy(cyanMaterial); // 청록 재질 제거
            if (targetMaterial != null) Destroy(targetMaterial); // 표적 재질 제거
            if (evidenceMaterial != null) Destroy(evidenceMaterial); // 증거 재질 제거

            if (instance == this) // 현재 singleton 확인
            {
                instance = null; // 정적 참조 해제
            }
        }
    }
}
