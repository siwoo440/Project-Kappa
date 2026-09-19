#if UNITY_EDITOR // 훈련장 편집 전용
using System; // 설치 오류 처리
using System.Collections.Generic; // 적과 표적 목록 처리
using UnityEditor; // 기존 위치와 경로 편집
using UnityEngine; // 훈련장 모형
using UnityEngine.SceneManagement; // 대상 씬 제한
using G13 = ProjectKDay13Geometry; // 공통 모형 생성

public static class ProjectKDay13Campus // 스폰과 실전 적이 분리된 확장 훈련장
{
    public const string RootName = "Day13_TrainingCampus"; // 반복 설치 기준
    public static T[] Components<T>(Scene scene) where T : Component // 지정 씬의 컴포넌트만 수집
    {
        List<T> list = new List<T>(); // 반환 목록 생성
        foreach (GameObject root in scene.GetRootGameObjects()) // 실제 씬 루트 순회
        {
            list.AddRange(root.GetComponentsInChildren<T>(true)); // 비활성 배치까지 조회
        }
        return list.ToArray(); // 고정 조회 결과
    }

    public static TrainingCampusMarker Build(Scene scene, GameObject player, FirearmDefinition[] guns) // 기존 구간을 보존한 확장 배치
    {
        GameObject existing = ProjectKDay9Setup.FindNamed(scene, RootName); // 이전 확장 여부 확인
        if (existing != null) // 수동 편집한 확장 구역 보존
        {
            TrainingCampusMarker old = existing.GetComponent<TrainingCampusMarker>(); // 완성된 구성 표식 확인
            if (old == null || !old.Completed) // 실패한 부분 배치 확인
            {
                throw new InvalidOperationException("Day13: 이전 설치가 완료되지 않았습니다. 저장하지 않은 Test 씬을 다시 열고 실행하세요."); // 부분 구조 위에 중복 설치 방지
            }
            return old; // 재실행 시 위치를 다시 바꾸지 않음
        }
        Physics.SyncTransforms(); // 기존 배치의 충돌 범위 갱신
        Vector3 spawn = player.transform.position; // 스폰은 이동하지 않고 기준으로 유지
        float ground = GroundBelow(player.transform); // 스폰 바닥 높이
        float edge = spawn.x + 55f; // 기존 구역과 최소 여유 거리
        foreach (Collider collider in Components<Collider>(scene)) // 기존 실제 충돌 영역 조사
        {
            if (collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger && !collider.transform.IsChildOf(player.transform) && collider.bounds.size.x < 500f) // 배경 전역 지형과 트리거 제외
            {
                edge = Mathf.Max(edge, collider.bounds.max.x); // 기존 맵 오른쪽 끝 탐색
            }
        }
        GameObject campus = new GameObject(RootName); // 확장 훈련장 루트
        SceneManager.MoveGameObjectToScene(campus, scene); // Test 씬만 편집
        campus.transform.position = new Vector3(edge + 18f, ground, spawn.z); // 기존 사격장과 겹치지 않는 오른쪽 확장
        TrainingCampusMarker marker = campus.AddComponent<TrainingCampusMarker>(); // 중복 생성 방지 표식
        Transform root = campus.transform; // 이후 모든 새 요소의 부모
        Material floor = G13.Mat("Deck", new Color(0.12f, 0.16f, 0.20f)); // 넓은 콘크리트 바닥
        Material steel = G13.Mat("StructuralSteel", new Color(0.21f, 0.28f, 0.34f)); // 구조물 금속
        Material rubber = G13.Mat("BallisticRubber", new Color(0.035f, 0.048f, 0.055f)); // 탄도 흡수 벽
        Material white = G13.Mat("TargetWhite", new Color(0.82f, 0.86f, 0.88f)); // 표적판 밝은 외장
        Material cyan = G13.Mat("SafeCyan", new Color(0.08f, 0.8f, 0.94f), true); // 안전 구역 색상
        Material orange = G13.Mat("RangeAmber", new Color(1f, 0.49f, 0.08f), true); // 사격 구역 색상
        Material red = G13.Mat("LiveRed", new Color(0.93f, 0.12f, 0.15f), true); // 실제 적 구역 색상
        Material marks = G13.Mat("TargetMarks", Color.white); // 탄착 표시 공용 재질

        G13.Box(root, "CampusFoundation_140x212", new Vector3(62f, -0.3f, 94f), new Vector3(140f, 0.6f, 212f), floor, true); // 단일 높이로 겹침 없는 전체 바닥
        for (int x = 0; x <= 130; x += 10) // 바닥 구획선
        {
            G13.Box(root, "DeckJointX_" + x, new Vector3(x, 0.012f, 94f), new Vector3(0.03f, 0.018f, 208f), rubber); // 바닥과 떨어진 얇은 이음선
        }
        for (int z = -10; z <= 190; z += 10) // 가로 구획선
        {
            G13.Box(root, "DeckJointZ_" + z, new Vector3(62f, 0.03f, z), new Vector3(138f, 0.008f, 0.03f), rubber); // 이전 이음선과도 면 겹침 최소화
        }
        BuildPerimeter(root, steel, cyan); // 외곽 방호 구조
        BuildConnection(scene, root, player, spawn, ground, steel, floor, cyan); // 기존 시작 구역 연결
        BuildHub(root, guns, steel, rubber, cyan); // 안내와 장비 전시
        BuildLanes(root, steel, rubber, white, cyan, orange, marks); // 고정과 이동 표적
        BuildArena(root, steel, rubber, red); // 멀리 떨어진 실전 교전 구역
        int moved = RelocateActors(scene, root, player.transform, steel); // 기존 적과 순찰 경로 이동
        UpgradeLegacyTargets(scene, root); // 기존 탄착판에도 피격 넘어짐 추가
        marker.Configure(spawn, moved); // 완료된 배치 기록
        EditorUtility.SetDirty(marker); // 저장 대상 표시
        return marker; // 완성 구역 반환
    }

    private static float GroundBelow(Transform player) // 시작점 바로 아래 바닥 조사
    {
        float highest = float.NegativeInfinity; // 최고 바닥 후보
        foreach (RaycastHit hit in Physics.RaycastAll(player.position + Vector3.up * 0.05f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore)) // 지붕이 아닌 발 아래만 조사
        {
            if (hit.transform.gameObject.scene == player.gameObject.scene && !hit.transform.IsChildOf(player) && hit.transform != player) // 자기 충돌체 제외
            {
                highest = Mathf.Max(highest, hit.point.y); // 가장 가까운 아래 바닥
            }
        }
        return float.IsNegativeInfinity(highest) ? player.position.y - 0.1f : highest; // 바닥 없는 경우 시작점 바로 아래 기준
    }

    private static void BuildPerimeter(Transform root, Material steel, Material accent) // 외곽 담장과 반복 구조
    {
        G13.Box(root, "Backstop", new Vector3(62f, 3.5f, 199f), new Vector3(140f, 7f, 0.8f), steel, true); // 사격장 끝 안전벽
        G13.Box(root, "OuterEast", new Vector3(132f, 2.5f, 94f), new Vector3(0.5f, 5f, 212f), steel, true); // 동쪽 안전벽
        G13.Box(root, "OuterWest", new Vector3(-8f, 1.4f, 106f), new Vector3(0.4f, 2.8f, 185f), steel, true); // 연결 통로를 남긴 서쪽 경계
        float[] edgeX = new float[] // 경계 기둥 가로 위치
        {
            -7.8f, // 서쪽 위치
            131.5f // 동쪽 위치
        };
        for (int z = 0; z <= 195; z += 15) // 외곽 반복 기둥
        {
            foreach (float x in edgeX) // 좌우 양측
            {
                G13.Box(root, "FencePost", new Vector3(x, 3f, z), new Vector3(0.4f, 6f, 0.4f), steel); // 구조 기둥
                G13.Box(root, "FenceBeacon", new Vector3(x, 5.8f, z), new Vector3(0.44f, 0.18f, 0.44f), accent); // 야간 안내 표시
            }
        }
        for (int i = 0; i < 5; i++) // 후방 관측 설비
        {
            float x = 12f + i * 24f; // 관측 구조 간격
            G13.Box(root, "RearButtress", new Vector3(x, 2f, 197f), new Vector3(0.7f, 4f, 2.5f), steel); // 방호벽 지지 구조
        }
    }

    private static void BuildConnection(Scene scene, Transform root, GameObject player, Vector3 spawn, float ground, Material steel, Material floor, Material cyan) // 기존 스폰과 보행 연결
    {
        float endX = root.position.x; // 확장 입구 가로 위치
        float beginX = spawn.x + 3f; // 스폰 패드 옆 시작
        Transform bridge = G13.Node(root, "SpawnBridge", Vector3.zero); // 연결 구조 루트
        bridge.position = new Vector3((beginX + endX) * 0.5f, ground, spawn.z); // 원래 스폰 기준 연결
        float length = endX - beginX; // 실제 연결 거리
        G13.Box(bridge, "BridgeDeck", new Vector3(0f, -0.10f, 0f), new Vector3(length, 0.32f, 6f), floor, true); // 기존 바닥과 표면이 겹치지 않는 낮은 보행 다리
        for (int side = -1; side <= 1; side += 2) // 다리 양쪽 난간
        {
            G13.Box(bridge, "Rail", new Vector3(0f, 1f, side * 2.8f), new Vector3(length, 0.08f, 0.09f), steel, true); // 추락 방지 난간
            G13.Box(bridge, "GuideLight", new Vector3(0f, 0.083f, side * 2.6f), new Vector3(length, 0.035f, 0.08f), cyan); // 이동 유도선
        }
        GameObject boundary = ProjectKDay9Setup.FindNamed(scene, "RightBoundary"); // 기존 외벽 출입구 확인
        if (boundary != null && boundary.activeSelf) // 기존 벽 보존 상태
        {
            Undo.RecordObject(boundary, "Day13 Open East Boundary"); // 되돌릴 수 있는 변경
            boundary.SetActive(false); // 원본을 삭제하지 않고 동쪽 출입 개방
        }
        Transform arrival = G13.Node(root, "SafeArrival", new Vector3(2f, 0.12f, 1.8f)); // 확장 입구 안전 도착점
        Transform returnPoint = G13.Node(root, "OriginalSpawnReturn", Vector3.zero); // 원래 출발 지점 복귀
        returnPoint.position = spawn + Vector3.up * 0.1f; // 플레이어 시작점 유지
        returnPoint.rotation = player.transform.rotation; // 기존 진행 방향 보존
        Transform startConsole = Console(root, new Vector3(0f, 0f, 0f), steel, cyan, "F / TRAINING TRANSFER"); // 큰 맵 이동용 단말기
        startConsole.position = new Vector3(spawn.x + 1.8f, ground, spawn.z + 1.5f); // 기존 스폰에서 바로 접근 가능
        startConsole.gameObject.AddComponent<TrainingCampusStation>().Configure(root, arrival, "확장 훈련장으로 이동"); // 보행 외 빠른 이동 제공
        Transform backConsole = Console(root, new Vector3(-2.4f, 0f, 2f), steel, cyan, "F / RETURN TO SPAWN"); // 입구 복귀 단말기
        backConsole.gameObject.AddComponent<TrainingCampusStation>().Configure(root, returnPoint, "기존 스폰으로 복귀"); // 돌아갈 길 제공
        G13.Panel(root, "CampusEntry", new Vector3(16f, 4f, 0f), "YEONMU / TRAINING CAMPUS\nSAFE HUB - NO HOSTILES", steel, cyan); // 입구 식별 표시
    }

    private static Transform Console(Transform root, Vector3 position, Material steel, Material accent, string text) // 실제 F키 단말기 모형
    {
        Transform node = G13.Node(root, "ControlConsole", position); // 상호작용 기준
        G13.Box(node, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(1.2f, 1.3f, 0.7f), steel, true); // 상호작용 충돌
        G13.Box(node, "Screen", new Vector3(0f, 1.05f, -0.36f), new Vector3(0.9f, 0.4f, 0.02f), accent); // 화면 발광
        G13.Label(node, text, new Vector3(0f, 1.65f, -0.38f), 0.024f); // 조작 안내
        return node; // 기능 연결용 반환
    }

    private static void BuildHub(Transform root, FirearmDefinition[] guns, Material steel, Material rubber, Material cyan) // 보급과 대기 구역
    {
        Transform station = Console(root, new Vector3(8f, 0f, 8f), steel, cyan, "F / AMMO + RESET TARGETS"); // 기본 보급대
        station.gameObject.AddComponent<TrainingCampusStation>().Configure(root, null, "표적 초기화 · 총기 탄약 · 체력 보충"); // 통합 초기화
        for (int i = 0; i < guns.Length; i++) // 새 두 무기 진열
        {
            Transform display = G13.Node(root, "WeaponDisplay_" + i, new Vector3(15f + i * 5f, 0f, 7f)); // 입구 옆 진열대
            G13.Box(display, "Base", new Vector3(0f, 0.6f, 0f), new Vector3(2.4f, 1.2f, 1f), steel, true); // 전시 받침
            G13.Box(display, "LightStrip", new Vector3(0f, 0.4f, -0.52f), new Vector3(2.1f, 0.06f, 0.02f), cyan); // 발광 하단
            GameObject model = UnityEngine.Object.Instantiate(guns[i].ModelPrefab, display); // 장착 모형과 같은 진열
            model.transform.localPosition = new Vector3(0f, 1.6f, 0f); // 받침 위 배치
            model.transform.localRotation = Quaternion.Euler(0f, 75f, 0f); // 측면 비교 방향
            UnityEngine.Object.DestroyImmediate(model.GetComponent<FirearmMechanismView>()); // 전시품의 상태 갱신 제거
            UnityEngine.Object.DestroyImmediate(model.GetComponent<FirearmView>()); // 전시품 자동 소리 제거
            G13.Label(display, (8 + i) + " / " + ProjectKDay13Weapons.Ids[i], new Vector3(0f, 1.0f, -0.52f), 0.034f); // 장비 번호
        }
        for (int i = 0; i < 6; i++) // 휴식 벤치와 보급 상자
        {
            float x = 34f + i * 12f; // 충분한 통행 공간
            Transform bench = G13.Node(root, "Bench_" + i, new Vector3(x, 0f, 3f)); // 벤치 위치
            G13.Box(bench, "Seat", new Vector3(0f, 0.55f, 0f), new Vector3(3f, 0.15f, 0.8f), rubber, true); // 실제 앉는 면
            G13.Box(bench, "Back", new Vector3(0f, 0.92f, 0.42f), new Vector3(3f, 0.65f, 0.12f), steel); // 등받이
            G13.Box(bench, "LegL", new Vector3(-1.1f, 0.25f, 0f), new Vector3(0.2f, 0.5f, 0.6f), steel); // 왼다리
            G13.Box(bench, "LegR", new Vector3(1.1f, 0.25f, 0f), new Vector3(0.2f, 0.5f, 0.6f), steel); // 오른다리
            Crate(root, new Vector3(x + 3.5f, 0f, 4f), steel, rubber); // 보급 상자
        }
        G13.Panel(root, "RangeDirectory", new Vector3(42f, 3.8f, 7f), "01 STATIC / 02 MOVING\n03 PRECISION / 04 LIVE AI", steel, cyan); // 구역 안내
    }

    private static void BuildLanes(Transform root, Material steel, Material rubber, Material white, Material cyan, Material orange, Material marks) // 사격 표적과 엄폐
    {
        float[] laneX = new float[] // 서로 다른 표적 레인
        {
            10f, // 첫 고정 레인
            24f, // 둘째 고정 레인
            42f, // 첫 이동 레인
            58f, // 둘째 이동 레인
            76f, // 90미터 레인
            86f // 150미터 레인
        };
        for (int i = 0; i < laneX.Length; i++) // 사격 부스 구성
        {
            float x = laneX[i]; // 레인 중심
            G13.Box(root, "FirePad_" + i, new Vector3(x, 0.024f, 20f), new Vector3(4f, 0.025f, 4f), i < 4 ? orange : cyan); // 사격 기준 패드
            G13.Box(root, "BoothRoof_" + i, new Vector3(x, 3.8f, 18f), new Vector3(6f, 0.22f, 5f), steel, true); // 부스 지붕
            for (int side = -1; side <= 1; side += 2) // 부스 양옆 기둥
            {
                G13.Box(root, "BoothPost", new Vector3(x + side * 2.8f, 1.9f, 16f), new Vector3(0.2f, 3.8f, 0.2f), steel, true); // 후방 지지대
            }
            G13.Panel(root, "LaneLabel_" + i, new Vector3(x, 3.15f, 20f), i < 2 ? "STATIC / HIT TO DROP" : i < 4 ? "MOVING / RAIL TARGET" : "PRECISION / LONG RANGE", steel, i < 4 ? orange : cyan); // 부스 식별
            for (int z = 25; z <= 185; z += 10) // 거리 진행 표시
            {
                G13.Box(root, "LaneDash", new Vector3(x - 3f, 0.025f, z), new Vector3(0.06f, 0.02f, 2f), i < 4 ? orange : cyan); // 보행과 사격 레인 구분
            }
        }
        float[] shortDistances = new float[] // 문지기 거리별 비교
        {
            5f, // 근거리 표적
            10f, // 유효 거리 표적
            20f // 감쇠 거리 표적
        };
        for (int i = 0; i < 3; i++) // 고정 표적 배치
        {
            Target(root, "Static_" + i, new Vector3(8f + i * 8f, 0f, 20f + shortDistances[i]), 0f, 1f, steel, white, orange, rubber, marks); // 피격 시 접히는 표적
        }
        for (int i = 0; i < 3; i++) // 좌우 이동 표적 배치
        {
            Target(root, "Moving_" + i, new Vector3(i % 2 == 0 ? 42f : 58f, 0f, 38f + i * 20f), 10f, 1f, steel, white, orange, rubber, marks); // 교차하지 않는 레일 표적
        }
        Target(root, "Precision_90m", new Vector3(76f, 0f, 110f), 0f, 1f, steel, white, cyan, rubber, marks); // 발사 패드부터 90미터
        Target(root, "Precision_150m", new Vector3(86f, 0f, 170f), 0f, 1f, steel, white, cyan, rubber, marks); // 발사 패드부터 150미터
        Target(root, "TTK_Static_150HP", new Vector3(62f, 0f, 45f), 0f, 150f, steel, white, cyan, rubber, marks); // 여러 발 피해 누적 계측
        Target(root, "TTK_Moving_150HP", new Vector3(50f, 0f, 86f), 8f, 150f, steel, white, cyan, rubber, marks); // 이동 중 제압 시간 비교
        for (int z = 55; z <= 175; z += 40) // 저격 레인 상부 안전 구조
        {
            G13.Box(root, "OverheadBaffle", new Vector3(81f, 5.5f, z), new Vector3(20f, 1f, 0.45f), rubber, true); // 머리 위 탄도 안전판
            G13.Box(root, "BafflePost", new Vector3(90f, 2.7f, z), new Vector3(0.3f, 5.4f, 0.4f), steel, true); // 레인 밖 지지대
        }
        G13.Box(root, "PartialCover", new Vector3(29f, 0.75f, 31f), new Vector3(2f, 1.5f, 0.4f), steel, true); // 일부 펠릿 차단 시험용 엄폐
        G13.Panel(root, "PrecisionDistances", new Vector3(81f, 4f, 92f), "90 m / 150 m\nBOLT ACTION RANGE", steel, cyan); // 원거리 구역 표시
    }

    public static TrainingReactiveTarget Target(Transform root, string name, Vector3 position, float travel, float hp, Material steel, Material white, Material accent, Material rubber, Material marks) // 기계식 접이 표적 제작
    {
        Transform target = G13.Node(root, name, position); // 레일과 기록의 고정 기준
        float railLength = Mathf.Max(2.4f, travel + 2.4f); // 이동거리보다 넓은 레일
        for (int side = -1; side <= 1; side += 2) // 평행 레일 두 줄
        {
            G13.Box(target, "Track", new Vector3(0f, 0.07f, side * 0.30f), new Vector3(railLength, 0.14f, 0.12f), steel); // 낮은 레일 모형
            G13.Box(target, "EndStop", new Vector3(side * railLength * 0.5f, 0.18f, 0f), new Vector3(0.18f, 0.36f, 0.85f), rubber); // 이동 끝 완충 장치
        }
        Transform carriage = G13.Node(target, "Carriage", Vector3.zero); // 왕복 이동 받침
        G13.Box(carriage, "MotorBase", new Vector3(0f, 0.19f, 0f), new Vector3(1.4f, 0.28f, 0.85f), steel); // 전동 구동 받침
        G13.Box(carriage, "MotorStatus", new Vector3(0f, 0.21f, -0.44f), new Vector3(0.9f, 0.04f, 0.02f), accent); // 구동 상태 표시
        Transform hinge = G13.Node(carriage, "Hinge", new Vector3(0f, 0.37f, 0f)); // 표적 발밑 회전축
        GameObject axle = G13.Part(carriage, "HingeAxle", PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.16f, 0.78f, 0.16f), steel); // 두꺼운 실제 힌지 모형
        axle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 좌우 축 정렬
        G13.Box(hinge, "Spine", new Vector3(0f, 0.95f, 0.055f), new Vector3(0.16f, 1.9f, 0.12f), steel); // 표적 뒤 지지대
        G13.Box(hinge, "BodyPlate", new Vector3(0f, 0.95f, -0.035f), new Vector3(0.9f, 1.40f, 0.10f), white); // 실루엣 몸통 판
        G13.Box(hinge, "ShoulderPlate", new Vector3(0f, 1.38f, -0.035f), new Vector3(1.1f, 0.34f, 0.10f), white); // 어깨 실루엣
        G13.Part(hinge, "HeadPlate", PrimitiveType.Cylinder, new Vector3(0f, 1.90f, -0.035f), new Vector3(0.48f, 0.05f, 0.48f), white).transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 원형 머리판
        for (int ring = 0; ring < 3; ring++) // 몸통 동심원 조준 표시
        {
            G13.Tube(hinge, "Bullseye_" + ring, new Vector3(0f, 1.05f, -0.092f - ring * 0.009f), 0.31f - ring * 0.09f, 0.007f, ring % 2 == 0 ? accent : rubber); // 겹친 면 사이 여유 확보
        }
        for (int side = -1; side <= 1; side += 2) // 표적 고정 나사
        {
            G13.Part(hinge, "Fastener", PrimitiveType.Sphere, new Vector3(side * 0.35f, 0.44f, -0.092f), Vector3.one * 0.06f, steel); // 볼트 머리 모형
        }
        BoxCollider body = G13.Node(hinge, "BodyHit", new Vector3(0f, 0.96f, -0.04f)).gameObject.AddComponent<BoxCollider>(); // 실제 몸통 충돌
        body.size = new Vector3(1.1f, 1.43f, 0.17f); // 장식보다 약간 앞쪽 피격 면
        FirearmHitZone headZone = G13.Node(hinge, "HeadHit", new Vector3(0f, 1.9f, -0.035f)).gameObject.AddComponent<FirearmHitZone>(); // 머리 피해 구분
        headZone.Configure(FirearmHitRegion.Head, null, null); // 실제 적이 아닌 표적용 부위
        BoxCollider head = headZone.GetComponent<BoxCollider>(); // 자동으로 추가된 머리 상자
        head.size = new Vector3(0.48f, 0.48f, 0.15f); // 원형 머리판 영역
        target.gameObject.AddComponent<FirearmPracticeTarget>().Configure(marks); // 기존 탄착 기록 재사용
        TextMesh label = G13.Label(target, name, new Vector3(0f, 3.05f, 0f), 0.021f); // 표적 외부 단일 기록판
        TrainingReactiveTarget reactive = target.gameObject.AddComponent<TrainingReactiveTarget>(); // 움직임과 넘어짐 제어
        Collider[] hitAreas = new Collider[] // 회전축에 종속된 피격 영역
        {
            body, // 몸통 영역
            head // 머리 영역
        };
        reactive.Configure(hinge, carriage, hitAreas, label, travel, 2f, hp); // 동일한 피격 구조로 고정과 이동 설정
        return reactive; // 기록 초기화 대상 반환
    }

    private static void BuildArena(Transform root, Material steel, Material rubber, Material red) // 스폰 반대편 실전 훈련장
    {
        G13.Box(root, "LiveWestWall", new Vector3(92f, 2.5f, 140f), new Vector3(0.6f, 5f, 72f), steel, true); // 사격장과 적 구역 분리
        G13.Box(root, "LiveNorthWall", new Vector3(112f, 2.5f, 176f), new Vector3(40f, 5f, 0.6f), steel, true); // 적 구역 후방 방호벽
        G13.Box(root, "LiveEntryLeft", new Vector3(100f, 2.5f, 104f), new Vector3(16f, 5f, 0.6f), steel, true); // 출입구 왼쪽 벽
        G13.Box(root, "LiveEntryRight", new Vector3(124f, 2.5f, 104f), new Vector3(16f, 5f, 0.6f), steel, true); // 중앙 출입구 확보
        G13.Panel(root, "LiveWarning", new Vector3(112f, 4.3f, 103.4f), "LIVE AI / RED ZONE\nENEMIES BEYOND THIS GATE", steel, red); // 위험 구역 시각 분리
        G13.Box(root, "RedThreshold", new Vector3(112f, 0.028f, 103f), new Vector3(8f, 0.025f, 1.3f), red); // 발밑 위험 경계
        for (int i = 0; i < 4; i++) // 엄폐와 반복 결투 구획
        {
            G13.Box(root, "Cover", new Vector3(i % 2 == 0 ? 96f : 128f, 0.8f, 122f + i * 11f), new Vector3(4f, 1.6f, 2f), rubber, true); // 가장자리 엄폐물
            Crate(root, new Vector3(95f + i * 10f, 0f, 171f), steel, rubber); // 후방 보급 상자
        }
        for (int i = 0; i < 3; i++) // 순찰 구역 표시만 생성
        {
            G13.Box(root, "PatrolLane", new Vector3(103f + i * 10f, 0.025f, 139f), new Vector3(0.08f, 0.02f, 60f), red); // 순찰 축 표시
        }
    }

    private static void Crate(Transform root, Vector3 point, Material steel, Material rubber) // 세부 보급 상자
    {
        Transform crate = G13.Node(root, "SupplyCase", point); // 상자 기준
        G13.Box(crate, "Case", new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1f, 1.1f), steel, true); // 충돌 있는 주 외형
        G13.Box(crate, "Lid", new Vector3(0f, 1.04f, 0f), new Vector3(1.85f, 0.10f, 1.15f), rubber); // 분리 뚜껑
        for (int side = -1; side <= 1; side += 2) // 상자 잠금띠
        {
            G13.Box(crate, "Latch", new Vector3(side * 0.6f, 0.53f, -0.568f), new Vector3(0.12f, 0.86f, 0.025f), rubber); // 전면 띠
        }
    }

    private static int RelocateActors(Scene scene, Transform campus, Transform player, Material steel) // 원래 적과 순찰 지점 함께 이동
    {
        EnemyActor[] actors = Components<EnemyActor>(scene); // 기존 실제 적 목록
        Transform group = G13.Node(campus, "RelocatedLiveActors", Vector3.zero); // 이동한 적 소속
        int index = 0; // 배치 번호
        foreach (EnemyActor actor in actors) // 적 하나씩 처리
        {
            if (actor.transform.IsChildOf(player)) // 플레이어 장식은 제외
            {
                continue; // 적이 아닌 사용자 구성 보존
            }
            Undo.SetTransformParent(actor.transform, group, "Day13 Move Enemy Away From Spawn"); // 기존 컴포넌트와 이름 유지
            Undo.RecordObject(actor.transform, "Day13 Enemy Position"); // 위치 변경 기록
            Vector3 position = new Vector3(101f + (index % 3) * 11f, 0.06f, 124f + (index / 3) * 12f); // 먼 구역의 서로 떨어진 자리
            actor.transform.localPosition = position; // 스폰과 충분한 거리 확보
            actor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 출입구 방향으로 배치
            PatrolGuardAI patrol = actor.GetComponent<PatrolGuardAI>(); // 실제 순찰 경비 확인
            if (patrol != null) // 멀리 이동해도 옛 지점으로 돌아가지 않도록 처리
            {
                Transform route = G13.Node(campus, "RelocatedRoute_" + index, Vector3.zero); // 해당 경비 전용 경로
                Transform[] points = new Transform[2]; // 서로 겹치지 않는 전후 순찰
                points[0] = G13.Node(route, "A", position + Vector3.back * 3f); // 남쪽 지점
                points[1] = G13.Node(route, "B", position + Vector3.forward * 3f); // 북쪽 지점
                points[0].localRotation = Quaternion.Euler(0f, 180f, 0f); // 남쪽에서 출입구 감시
                Undo.RecordObject(patrol, "Day13 Rebind Patrol Route"); // 기존 경로 변경 기록
                patrol.BindTargetAndRoute(player, points); // 참조까지 새 경로로 연결
                EditorUtility.SetDirty(patrol); // 변경된 참조 저장
            }
            index++; // 다음 자리 선택
        }
        int cameraIndex = 0; // 감시 장치 배치 번호
        foreach (D01SimplePan pan in Components<D01SimplePan>(scene)) // 실제 감시 카메라만 이동
        {
            if (pan.GetComponentInParent<EnemyActor>() != null) // 적 안의 회전 장식 제외
            {
                continue; // 적과 카메라 이중 이동 방지
            }
            Transform cameraRoot = pan.transform; // 회전 관리자가 붙은 카메라 루트
            Undo.SetTransformParent(cameraRoot, group, "Day13 Move Surveillance"); // 실제 렌즈와 감시 참조 유지
            cameraRoot.localPosition = new Vector3(95f + cameraIndex * 12f, 4f, 165f); // 적 구역 끝의 감시 위치
            cameraRoot.localRotation = Quaternion.Euler(0f, 180f, 0f); // 구역 안쪽 감시
            G13.Box(campus, "CameraMast", new Vector3(95f + cameraIndex * 12f, 2f, 165.5f), new Vector3(0.25f, 4f, 0.25f), steel); // 카메라 지지대
            cameraIndex++; // 다음 카메라 자리
        }
        return index; // 검증용 실제 적 수
    }

    private static void UpgradeLegacyTargets(Scene scene, Transform campus) // 기존 평면 표적의 시각적 피격 연동
    {
        HashSet<GameObject> owners = new HashSet<GameObject>(); // 두 기록 컴포넌트 중복 방지
        foreach (FirearmPracticeTarget target in Components<FirearmPracticeTarget>(scene)) // 기존 탄착판 목록
        {
            owners.Add(target.gameObject); // 표적 소유자 기록
        }
        foreach (FirearmDamageProbe probe in Components<FirearmDamageProbe>(scene)) // 기존 피해 비교판 목록
        {
            owners.Add(probe.gameObject); // 표적 소유자 기록
        }
        foreach (GameObject owner in owners) // 이전 표적만 개별 변환
        {
            if (owner.GetComponent<TrainingReactiveTarget>() != null || owner.transform.IsChildOf(campus)) // 이미 처리한 표적 확인
            {
                continue; // 중복 힌지 방지
            }
            Transform hinge = G13.Node(owner.transform, "Day13_LegacyHinge", new Vector3(0f, 0.18f, 0f)); // 기존 좌표를 유지한 회전축
            Transform[] children = new Transform[owner.transform.childCount]; // 재부모화 전 목록 고정
            for (int i = 0; i < children.Length; i++) // 기존 자식 목록 저장
            {
                children[i] = owner.transform.GetChild(i); // 원래 자식 보존
            }
            foreach (Transform child in children) // 판과 부위만 힌지에 연결
            {
                if (child != hinge && child.GetComponent<TextMesh>() == null && !child.name.Contains("Stand") && !child.name.Contains("Label")) // 받침과 글자는 고정
                {
                    Undo.SetTransformParent(child, hinge, "Day13 Fold Existing Target"); // 월드 위치를 유지한 회전 부모 연결
                }
            }
            foreach (BoxCollider oldCollider in owner.GetComponents<BoxCollider>()) // 루트에 남은 기존 표적 충돌
            {
                if (!oldCollider.enabled) // 이미 대체한 충돌체 확인
                {
                    continue; // 중복 복제 방지
                }
                Transform copyNode = G13.Node(hinge, "MigratedHit", Vector3.zero); // 힌지에 붙는 실제 충돌
                copyNode.position = owner.transform.position; // 원래 충돌 원점 보존
                copyNode.rotation = owner.transform.rotation; // 원래 충돌 방향 보존
                BoxCollider replacement = copyNode.gameObject.AddComponent<BoxCollider>(); // 새 부모의 충돌체
                replacement.center = oldCollider.center; // 기존 중심 유지
                replacement.size = oldCollider.size; // 기존 면적 유지
                replacement.isTrigger = oldCollider.isTrigger; // 이전 충돌 역할 유지
                Undo.RecordObject(oldCollider, "Day13 Disable Static Target Collider"); // 복구 가능한 변경
                oldCollider.enabled = false; // 원래 정지 위치의 유령 충돌 방지
            }
            owner.AddComponent<TrainingReactiveTarget>().Configure(hinge, null, hinge.GetComponentsInChildren<Collider>(true), null, 0f, 0f); // 기존 UI와 기록은 그대로 유지
        }
    }
}
#endif // 게임 빌드에 생성 코드 제외
