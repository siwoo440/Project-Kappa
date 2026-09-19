#if UNITY_EDITOR // 기존 씬 보존과 통합 배치 이전
using System; // 잘못된 이전 조건 보고
using System.Collections.Generic; // 보존 객체와 적 목록
using UnityEditor; // 씬 참조 편집과 되돌리기
using UnityEngine; // 기존 플레이어와 적 복사
using UnityEngine.EventSystems; // 기존 입력 UI 보존
using UnityEngine.InputSystem; // 기존 플레이어 입력 참조
using UnityEngine.Rendering; // 기존 화면 효과 보존
using UnityEngine.SceneManagement; // Test 씬 범위 제한
using G = TrainingCenterGeometry; // 공통 시설 구조

public static class TrainingCenterMigration // 원본 씬 백업 뒤 게임 기능만 이전
{
    public sealed class Snapshot // 변경 전 연결 자료
    {
        public GameObject[] Roots; // 원래 씬의 최상위 객체
        public GameObject Player; // 그대로 유지할 플레이어
        public List<GameObject> Services; // 카메라와 공통 시스템
        public List<GameObject> Standalone; // 씬 전환 유지가 필요한 최상위 시스템
        public EnemyActor[] Actors; // 원본 상태의 적
        public D01SimplePan[] Cameras; // 원래 감시 카메라
        public List<FirearmDefinition> Guns; // 현재 장착 목록
    }

    public static T[] Components<T>(Scene scene) where T : Component // 지정 씬의 객체만 조회
    {
        List<T> result = new List<T>(); // 읽기용 결과
        foreach (GameObject root in scene.GetRootGameObjects()) // 현재 씬 루트 순회
        {
            result.AddRange(root.GetComponentsInChildren<T>(true)); // 비활성 원본 포함
        }
        return result.ToArray(); // 고정된 결과 반환
    }

    public static Snapshot Inspect(Scene scene) // 어떤 객체도 변경하지 않는 사전 검사
    {
        PlayerMovement[] players = Components<PlayerMovement>(scene); // 실제 이동 가능한 플레이어
        if (players.Length != 1) // 대상이 모호한 씬 차단
        {
            throw new InvalidOperationException("Test 씬에 PlayerMovement가 붙은 플레이어가 정확히 하나 있어야 합니다."); // 잘못된 플레이어 이전 방지
        }
        GameObject player = players[0].gameObject; // 검증한 기존 플레이어
        PlayerFirearmController firearm = player.GetComponent<PlayerFirearmController>(); // 13일차 총기 구성
        if (firearm == null || player.GetComponent<PlayerInput>() == null) // 필수 기능 확인
        {
            throw new InvalidOperationException("기존 플레이어 입력과 총기 관리자 연결을 확인하세요."); // 게임 기능 없는 새 맵 생성 방지
        }
        List<FirearmDefinition> guns = ProjectKDay11Setup.GetLoadout(firearm); // 저장된 실제 총기 목록
        if (guns.Count < 5) // 대표 총기 완성 상태 확인
        {
            throw new InvalidOperationException("13일차 총기 다섯 종이 연결된 Test 씬을 먼저 열어주세요."); // 데이터 누락을 새 자료로 덮지 않음
        }
        foreach (FirearmDefinition gun in guns) // 현재 사용자 무기 자료 확인
        {
            if (gun == null || !gun.IsValid) // 자료와 모형 연결 검사
            {
                throw new InvalidOperationException("장착 목록의 총기 데이터 또는 모형 참조가 누락되었습니다."); // 잘못된 자료 보존 경고
            }
        }
        if (LayerMask.NameToLayer("ParkourSurface") < 0) // 원래 이동 레이어 검사
        {
            throw new InvalidOperationException("기존 ParkourSurface 레이어가 필요합니다."); // 프로젝트 레이어를 임의로 덮지 않음
        }
        HashSet<GameObject> services = new HashSet<GameObject>(); // 중복 없는 보존 목록
        HashSet<GameObject> standalone = new HashSet<GameObject>(); // 기존 최상위 시스템 유지 목록
        services.Add(player); // 플레이어의 모든 자식 연결 유지
        foreach (Camera camera in Components<Camera>(scene)) // 기존 실제 렌더 카메라
        {
            if (camera.CompareTag("MainCamera") || camera.GetComponent<ThirdPersonCamera>() != null) // 플레이 카메라 확인
            {
                services.Add(camera.gameObject); // 카메라와 오디오 리스너 유지
            }
        }
        foreach (Light light in Components<Light>(scene)) // 기존 환경 광원
        {
            if (light.type == LightType.Directional) // 시설 장식등과 구분
            {
                services.Add(light.gameObject); // 주 방향광 유지
            }
        }
        foreach (Volume volume in Components<Volume>(scene)) // 기존 URP 후처리
        {
            if (volume.isGlobal) // 구역 종속 효과 제외
            {
                services.Add(volume.gameObject); // 공통 후처리 참조 유지
            }
        }
        foreach (EventSystem events in Components<EventSystem>(scene)) // UI 입력 연결
        {
            services.Add(events.gameObject); // 이벤트 시스템 유지
        }
        foreach (Canvas canvas in Components<Canvas>(scene)) // 실제 화면 HUD만 보존
        {
            if (canvas.renderMode != RenderMode.WorldSpace && canvas.GetComponentInParent<EnemyActor>() == null) // 적 머리 위 UI 제외
            {
                services.Add(canvas.gameObject); // 화면 공간 UI 이전
            }
        }
        foreach (MonoBehaviour behaviour in Components<MonoBehaviour>(scene)) // 사용자 게임 시스템 확인
        {
            if (behaviour == null) // 누락된 스크립트 제외
            {
                continue; // 없는 형식 조회 방지
            }
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour); // 에디터 원본 스크립트
            string path = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty; // 경로로 공통 시스템 식별
            if (path.Contains("/Scripts/Core/") || path.Contains("/Scripts/Save/") || path.Contains("/Scripts/Mission/")) // 장비와 무관한 기존 기능
            {
                services.Add(behaviour.gameObject); // 프로젝트 진행과 저장 연결 보존
                standalone.Add(behaviour.gameObject); // DontDestroyOnLoad 대상의 최상위 위치 보존
            }
        }
        Snapshot snapshot = new Snapshot(); // 변경 전 기준 자료 생성
        snapshot.Roots = scene.GetRootGameObjects(); // 나중에 제거할 옛 배치 기준
        snapshot.Player = player; // 동일 객체 참조
        snapshot.Services = new List<GameObject>(services); // 보존 목록 저장
        snapshot.Standalone = new List<GameObject>(standalone); // 최상위 시스템 별도 기록
        snapshot.Actors = Components<EnemyActor>(scene); // 적 원본 자료
        snapshot.Cameras = Components<D01SimplePan>(scene); // 감시 장치 원본
        snapshot.Guns = guns; // 실제 무기 에셋 유지
        return snapshot; // 검증된 기준 반환
    }

    private static int Depth(Transform value) // 부모 우선 이전 순서 계산
    {
        int depth = 0; // 부모 수 초기화
        while (value.parent != null) // 최상위까지 순회
        {
            depth++; // 단계 누적
            value = value.parent; // 상위 객체 이동
        }
        return depth; // 정렬 기준 반환
    }

    public static void Move(Snapshot source, TrainingCenterRoot center, TrainingCenterZone[] zones, Transform spawn) // 백업을 끝낸 뒤의 실제 재배치
    {
        Transform services = G.Node(center.transform, "PreservedGameSystems", Vector3.zero); // 기존 기능을 모으는 부모
        source.Services.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform))); // 부모가 자식을 함께 이동하도록 정렬
        foreach (GameObject service in source.Services) // 원래 객체의 ID와 참조 유지
        {
            if (service == null || (service.transform.IsChildOf(center.transform) && !source.Standalone.Contains(service))) // 이미 상위와 이동한 객체 확인
            {
                continue; // 중복 이전 제외
            }
            Unpack(service); // 프리팹 인스턴스의 재부모 연결 허용
            Undo.SetTransformParent(service.transform, source.Standalone.Contains(service) ? null : services, "Move Existing Game System"); // 되돌릴 수 있는 원본 이전
        }
        float standingOffset = GroundOffset(source.Player); // 기존 캐릭터 피벗의 발 높이 보정
        foreach (TrainingCenterZone area in zones) // 도착점의 실제 캐릭터 높이 반영
        {
            area.Entry.position += Vector3.up * standingOffset; // 중앙 피벗 캐릭터의 바닥 관통 방지
        }
        TrainingCenterZone stealth = zones[4]; // 모의 골목 구역
        TrainingCenterZone live = zones[5]; // 교전 구역
        Transform stealthTemplates = G.Node(stealth.transform, "Templates_DO_NOT_ACTIVATE", Vector3.zero); // 잠입 원본 보관
        Transform liveTemplates = G.Node(live.transform, "Templates_DO_NOT_ACTIVATE", Vector3.zero); // 실전 원본 보관
        stealthTemplates.gameObject.SetActive(false); // 저장 상태부터 감지 완전 차단
        liveTemplates.gameObject.SetActive(false); // 로비 시작 시 적 실행 금지
        List<GameObject> stealthList = new List<GameObject>(); // 잠입 시험 원본
        List<GameObject> liveList = new List<GameObject>(); // 실전 시험 원본
        int patrolIndex = 0; // 경비 배치 번호
        int liveIndex = 0; // 결투 적 배치 번호
        foreach (EnemyActor actor in source.Actors) // 실제 존재하는 적의 설정만 재사용
        {
            if (actor == null || actor.transform.IsChildOf(source.Player.transform)) // 플레이어 장식 제외
            {
                continue; // 무효 원본 제외
            }
            bool forStealth = actor.GetComponent<PatrolGuardAI>() != null || actor.name.IndexOf("Assassination", StringComparison.OrdinalIgnoreCase) >= 0; // 기존 적의 역할 유지
            TrainingCenterZone area = forStealth ? stealth : live; // 적을 둘 시험동
            Transform storage = forStealth ? stealthTemplates : liveTemplates; // 비활성 원본 부모
            int index = forStealth ? patrolIndex++ : liveIndex++; // 역할별 배치 번호
            Vector3 point = forStealth ? new Vector3(22.8f + index % 2 * 1.8f, 0.08f, 25f + index / 2 * 19f) : new Vector3(15f + index % 3 * 9f, 0.08f, 25f + index / 3 * 9f); // 벽과 떨어진 초기 자리
            if (!area.Contains(area.transform.TransformPoint(point))) // 과도하게 많은 원본 적 검사
            {
                throw new InvalidOperationException("시험동에 배치할 적이 너무 많습니다. 기존 적 수를 확인하세요."); // 경계 밖 적 생성 방지
            }
            GameObject copy = UnityEngine.Object.Instantiate(actor.gameObject, storage); // 원본 게임 설정과 내부 참조 복제
            copy.name = actor.name; // 익숙한 적 이름 유지
            copy.SetActive(false); // 템플릿의 탐지와 전투 차단
            copy.transform.localScale = actor.transform.lossyScale; // 원래 적의 실제 크기 유지
            point.y += GroundOffset(actor.gameObject); // 발 피벗이 아닌 적의 바닥 높이 보정
            copy.transform.localPosition = point; // 구역 기준 자리
            copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 지면에 선 초기 방향
            BindPlayer(copy, source.Player.transform); // 외부 플레이어 참조를 보존 객체로 연결
            PatrolGuardAI patrol = copy.GetComponent<PatrolGuardAI>(); // 순찰 동작 유무
            if (patrol != null) // 원래 순찰 적만 경로 연결
            {
                Transform route = G.Node(area.transform, "Route_" + index, Vector3.zero); // 원본 밖의 공유 순찰 경로
                Transform[] points = new Transform[2]; // 단순하고 막히지 않는 왕복
                points[0] = G.Node(route, "A", point + Vector3.back * 3f); // 첫 지점
                points[1] = G.Node(route, "B", point + Vector3.forward * 3f); // 둘째 지점
                patrol.BindTargetAndRoute(source.Player.transform, points); // 저장 경로와 대상 재연결
                EditorUtility.SetDirty(patrol); // 실제 직렬화된 참조 저장
            }
            if (forStealth) // 원본 목록 소속 기록
            {
                stealthList.Add(copy); // 잠입용 목록 추가
            }
            else // 결투와 고정 전투 적
            {
                liveList.Add(copy); // 실전용 목록 추가
            }
        }
        int cameraIndex = 0; // 카메라 배치 순서
        foreach (D01SimplePan pan in source.Cameras) // 기존 감시 카메라 보존
        {
            if (pan == null || pan.GetComponentInParent<EnemyActor>() != null) // 적 내부 장식 제외
            {
                continue; // 중복 카메라 생성 방지
            }
            Vector3 position = new Vector3(20f + cameraIndex % 3 * 4f, 4f, 49f + cameraIndex / 3 * 9f); // 골목 중앙을 감시하는 지점
            if (!stealth.Contains(stealth.transform.TransformPoint(position))) // 감시 장치 수에 따른 경계 검사
            {
                throw new InvalidOperationException("모의 골목에 배치할 감시 장치가 너무 많습니다."); // 경계 밖 원본 저장 방지
            }
            GameObject copy = UnityEngine.Object.Instantiate(pan.gameObject, stealthTemplates); // 실제 카메라 기능 복사
            copy.name = pan.name; // 장치 이름 유지
            copy.SetActive(false); // 시험 전 비활성화
            copy.transform.localPosition = position; // 새 감시 위치
            copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 골목 내부를 바라보는 방향
            BindPlayer(copy, source.Player.transform); // 보존된 플레이어 연결
            G.Beam(stealth.transform, "CameraMast", position + Vector3.forward * 0.45f - Vector3.up * 4f, position + Vector3.forward * 0.45f, 0.10f, G.Steel, true); // 카메라 지지 구조
            stealthList.Add(copy); // 시험 때만 나타나는 감시 장치
            cameraIndex++; // 다음 장치 번호
        }
        stealth.SetTemplates(stealthList.ToArray()); // 준비된 잠입 시험 설정
        live.SetTemplates(liveList.ToArray()); // 준비된 실전 시험 설정
        Undo.RecordObject(source.Player.transform, "Move Player Into Integrated Lobby"); // 스폰 변경 복구 기록
        source.Player.transform.SetPositionAndRotation(spawn.position, spawn.rotation); // 시작점을 통합 로비로 이동
        foreach (ThirdPersonCamera camera in source.Player.scene.IsValid() ? Components<ThirdPersonCamera>(source.Player.scene) : new ThirdPersonCamera[0]) // 보존한 추적 카메라 갱신
        {
            Undo.RecordObject(camera, "Rebind Preserved Camera"); // 직렬화 참조 변경 기록
            camera.Configure(source.Player.transform, source.Player.GetComponent<PlayerInput>()); // 같은 플레이어 추적
            Undo.RecordObject(camera.transform, "Move Camera Into Lobby"); // 편집기 화면도 시작점으로 배치
            camera.transform.SetPositionAndRotation(spawn.position + new Vector3(0f, 3f, -5f), Quaternion.Euler(15f, 0f, 0f)); // 로비 첫 화면 구도
            EditorUtility.SetDirty(camera); // 카메라 참조 저장
        }
        foreach (GameObject root in source.Roots) // 변경 전 배치만 정리
        {
            if (root != null && root != center.gameObject && !root.transform.IsChildOf(center.transform) && !source.Standalone.Contains(root)) // 이전한 시스템과 새 시설 제외
            {
                Undo.DestroyObjectImmediate(root); // 원본 씬 백업에만 옛 지형 유지
            }
        }
    }

    private static float GroundOffset(GameObject owner) // 원본 이동 충돌체의 바닥부터 피벗까지 높이
    {
        CharacterController body = owner.GetComponent<CharacterController>(); // 실제 이동 캡슐 조회
        if (body != null) // 이동 충돌체 사용 여부
        {
            return Mathf.Max(0f, body.height * 0.5f - body.center.y) * Mathf.Abs(owner.transform.lossyScale.y); // 중심 피벗과 발 피벗 모두 지원
        }
        CapsuleCollider capsule = owner.GetComponent<CapsuleCollider>(); // 고정 더미의 수직 캡슐 조회
        return capsule != null && capsule.direction == 1 ? Mathf.Max(0f, capsule.height * 0.5f - capsule.center.y) * Mathf.Abs(owner.transform.lossyScale.y) : 0f; // 별도 캡슐이 없는 원본은 발 피벗 유지
    }

    private static void BindPlayer(GameObject owner, Transform player) // 기존 수치를 바꾸지 않는 대상 참조 수정
    {
        foreach (MonoBehaviour script in owner.GetComponentsInChildren<MonoBehaviour>(true)) // 감시와 전투 스크립트 순회
        {
            if (script == null) // 누락 형식 확인
            {
                continue; // 잘못된 참조 조회 방지
            }
            SerializedObject data = new SerializedObject(script); // 실제 저장 필드 조회
            SerializedProperty target = data.FindProperty("target"); // 기존 AI 대상 필드
            if (target != null && target.propertyType == SerializedPropertyType.ObjectReference && (script is DetectionSensor || script is PatrolGuardAI || script is E02SwordGuardAI)) // 알려진 플레이어 대상만 변경
            {
                target.objectReferenceValue = player; // 동일 플레이어로 연결
                data.ApplyModifiedPropertiesWithoutUndo(); // 새 템플릿에만 적용
            }
        }
    }

    private static void Unpack(GameObject value) // 원본 연결을 유지한 씬 편집 준비
    {
        GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(value); // 편집할 인스턴스 최상위
        if (outer != null) // 프리팹 소속 확인
        {
            PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.UserAction); // 원본 프리팹 파일은 변경하지 않음
        }
    }
}
#endif
