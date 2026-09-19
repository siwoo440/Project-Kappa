#if UNITY_EDITOR // 고정 배치의 통합 시설 구성
using System.Collections.Generic; // 구역 목록
using UnityEngine; // 시설 위치와 모형
using G = TrainingCenterGeometry; // 공통 모형 생성
using P = TrainingCenterProps; // 상세 시설 부품

public static class TrainingCenterSite // 기존 맵 옆이 아닌 원점 기준 전체 배치
{
    public static TrainingCenterZone Zone(Transform world, string name, Vector3 origin, Vector2 size, Vector3 entry) // 기능별 독립 구역 생성
    {
        Transform root = G.Node(world, name, origin); // 고정된 시설 구역
        Transform arrival = G.Node(root, "SafeEntry", entry); // 안전 도착점
        Transform runtime = G.Node(root, "RuntimeActors", Vector3.zero); // 시험 실행 적 소속
        TrainingCenterZone zone = root.gameObject.AddComponent<TrainingCenterZone>(); // 구역별 초기화와 시험 연결
        zone.Configure(name, size, arrival, runtime); // 경계와 시작점 저장
        return zone; // 나머지 시설 연결
    }

    public static TrainingCenterZone[] Build(TrainingCenterRoot center, List<FirearmDefinition> guns, out Transform spawn) // 완전한 통합 배치 생성
    {
        Transform world = center.transform; // 기준 원점
        G.Box(world, "Foundation_180x220", new Vector3(90f, -0.35f, 110f), new Vector3(180f, 0.7f, 220f), G.Concrete, true); // 겹치지 않는 단일 바닥
        G.WallZ(world, "PerimeterWest", 0.3f, 0f, 220f, 3.2f); // 외곽 서쪽 경계
        G.WallZ(world, "PerimeterEast", 179.7f, 0f, 220f, 3.2f); // 외곽 동쪽 경계
        G.WallX(world, "PerimeterNorth", 0f, 180f, 219.7f, 6f); // 사격장 북쪽 방호
        G.WallX(world, "PerimeterSouthLeft", 0f, 48f, 0.3f, 3.2f); // 시설 입구 왼쪽
        G.WallX(world, "PerimeterSouthRight", 78f, 180f, 0.3f, 3.2f); // 시설 입구 오른쪽
        P.DoorFrame(world, new Vector3(63f, 0f, 1f), 14f, G.Cyan, "entry"); // 실제 통합 시설 입구
        G.Box(world, "ClosedExteriorGate", new Vector3(63f, 2f, 0.25f), new Vector3(30f, 4f, 0.35f), G.Dark, true); // 로비 뒤쪽의 맵 밖 추락 방지
        for (int slat = 0; slat < 8; slat++) // 외부 출입 셔터의 패널 구획
        {
            G.Box(world, "ExteriorGateSlat", new Vector3(63f, 0.25f + slat * 0.48f, 0.435f), new Vector3(29f, 0.045f, 0.018f), G.Steel); // 닫힌 외곽문 표시
        }
        TrainingCenterZone lobby = Zone(world, "01_Lobby", new Vector3(30f, 0f, 3f), new Vector2(71f, 50f), new Vector3(30f, 0.14f, 9f)); // 안전 로비와 정비실
        spawn = lobby.Entry; // 새 플레이어 스폰
        spawn.name = "PlayerSpawn_IntegratedLobby"; // 이전 스폰과 명확히 구분
        TrainingCenterZone fixedRange = Zone(world, "02_StaticRange", new Vector3(50f, 0f, 64f), new Vector2(48f, 42f), new Vector3(8f, 0.14f, 4f)); // 짧은 사격 거리 구역
        TrainingCenterZone movingRange = Zone(world, "03_MovingRange", new Vector3(50f, 0f, 112f), new Vector2(48f, 44f), new Vector3(8f, 0.14f, 4f)); // 이동 표적 전용 구역
        TrainingCenterZone parkour = Zone(world, "04_Parkour", new Vector3(5f, 0f, 55f), new Vector2(40f, 71f), new Vector3(32f, 0.14f, 8f)); // 벽과 옥상 이동 구역
        TrainingCenterZone stealth = Zone(world, "05_StealthBlock", new Vector3(5f, 0f, 134f), new Vector2(40f, 78f), new Vector3(34f, 0.14f, 8f)); // 모의 도시 잠입 구역
        TrainingCenterZone live = Zone(world, "06_LiveArena", new Vector3(51f, 0f, 168f), new Vector2(47f, 45f), new Vector3(40f, 0.14f, 8f)); // 먼 실전 구역
        TrainingCenterZone precision = Zone(world, "07_PrecisionRange", new Vector3(112f, 0f, 34f), new Vector2(65f, 177f), new Vector3(16f, 0.14f, 4f)); // 150미터 직선 시야
        TrainingCenterZone[] zones = new TrainingCenterZone[] // 고정 구역 순서
        {
            lobby, // 로비와 정비
            fixedRange, // 고정 사격
            movingRange, // 이동 사격
            parkour, // 파쿠르
            stealth, // 잠입
            live, // 실전
            precision // 정밀 사격
        };
        BuildLobby(center, lobby, guns); // 출발 화면과 장비 준비
        BuildCirculation(world); // 어느 시험장도 가로지르지 않는 이동 통로
        TrainingCenterRanges.Build(center, fixedRange, movingRange, precision); // 모든 고정 이동 표적
        TrainingCenterMovementCourse.Build(center, parkour, stealth, live); // 이동과 잠입과 실전 공간
        for (int i = 1; i < zones.Length; i++) // 각 구역의 복귀 수단
        {
            TrainingCenterZone area = zones[i]; // 현재 구역
            Transform terminal = P.Console(area.transform, new Vector3(2.2f, 0f, 2f), G.Cyan, "return"); // 사격선 밖 복귀 단말기
            terminal.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, area, TrainingCenterConsole.Operation.Lobby, "안전 로비로 복귀"); // 실제 F 기능 연결
            Transform selector = P.Console(world, new Vector3(72f + (i - 1) * 3.9f, 0f, 44f), i == 5 || i == 4 ? G.Red : G.Cyan, "travel_" + i); // 중앙 안내 단말기 열
            selector.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, area, TrainingCenterConsole.Operation.Travel, "구역 이동: " + area.ZoneName, area.Entry); // 보행 외 빠른 이동 제공
        }
        return zones; // 마이그레이션과 관리자에 전달
    }

    private static void BuildLobby(TrainingCenterRoot center, TrainingCenterZone lobby, List<FirearmDefinition> guns) // 입구와 정비 공간
    {
        Transform p = lobby.transform; // 로비 기준
        G.WallX(p, "LobbyBackLeft", 3f, 23f, 1f, 6.5f); // 통합 입구 왼쪽 벽
        G.WallX(p, "LobbyBackRight", 37f, 67f, 1f, 6.5f); // 통합 입구 오른쪽 벽
        G.WallZ(p, "RepairWall", 2f, 1f, 28f, 6.5f); // 정비실 외벽
        G.Box(p, "LobbyCanopy", new Vector3(37f, 7.1f, 12f), new Vector3(61f, 0.34f, 22f), G.Steel, true); // 높은 부분 지붕
        for (int i = 0; i < 5; i++) // 지붕 구조와 작업등
        {
            float x = 8f + i * 13f; // 기둥 반복 위치
            G.Box(p, "CanopyColumn", new Vector3(x, 3.5f, 23f), new Vector3(0.40f, 7f, 0.4f), G.Steel, true); // 로비 경로를 피한 지지대
            G.Box(p, "CeilingStrip", new Vector3(x, 6.84f, 13f), new Vector3(0.13f, 0.10f, 13f), G.Cyan); // 빛나는 천장 표시만 사용
        }
        G.Sign(p, "lobby", new Vector3(33f, 5f, 1.31f), 16f, 3f, 180f); // 입구에서 보이는 내부 안내
        G.Sign(p, "overview", new Vector3(37f, 3.2f, 24f), 9f, 4.4f); // 중앙 조감 안내판
        for (int i = 0; i < 6; i++) // 대기용 보관함
        {
            P.Locker(p, new Vector3(45f + i * 1.2f, 0f, 3f)); // 남쪽 벽의 장식 보관함
        }
        P.Bench(p, new Vector3(46f, 0f, 11f)); // 통로 밖 휴게 좌석
        P.Bench(p, new Vector3(54f, 0f, 11f)); // 두 번째 휴게 좌석
        Transform supply = P.Console(p, new Vector3(28f, 0f, 18f), G.Cyan, "supply"); // 중앙 보급 장치
        supply.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, lobby, TrainingCenterConsole.Operation.Supply, "체력 · 총기 탄약 · 마비침 · 소모품 보충"); // 모든 기존 보급 기능
        for (int i = 0; i < Mathf.Min(5, guns.Count); i++) // 사용하는 총 다섯 개만 진열
        {
            P.WeaponRack(p, new Vector3(7f + i * 4f, 0f, 4f), guns[i], "gun_" + i); // 원본 무기 데이터 유지
        }
        Transform table = G.Node(p, "MaintenanceWorkbench", new Vector3(13f, 0f, 15f)); // 정비실 대표 작업대
        G.Box(table, "Worktop", new Vector3(0f, 0.95f, 0f), new Vector3(6.4f, 0.18f, 1.6f), G.LightSteel, true); // 실제 두께 작업판
        for (int side = -1; side <= 1; side += 2) // 받침과 공구 수납
        {
            G.Box(table, "ToolCabinet", new Vector3(side * 2.3f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 1.3f), G.Steel, true); // 작업대 받침
            for (int drawer = 0; drawer < 3; drawer++) // 전면 서랍 구획
            {
                G.Box(table, "DrawerHandle", new Vector3(side * 2.3f, 0.2f + drawer * 0.25f, -0.68f), new Vector3(0.9f, 0.05f, 0.035f), G.Cyan); // 얇은 손잡이
            }
        }
        G.Box(table, "ToolBoard", new Vector3(0f, 1.75f, 0.77f), new Vector3(6.4f, 1.35f, 0.1f), G.Dark); // 뒷면 공구판
        for (int tool = 0; tool < 8; tool++) // 정비 장비 실루엣
        {
            G.Box(table, "ToolGrip", new Vector3(-2.7f + tool * 0.76f, 1.68f, 0.70f), new Vector3(0.10f, 0.46f, 0.04f), G.Amber); // 공구 손잡이
            G.Box(table, "ToolHead", new Vector3(-2.7f + tool * 0.76f, 1.96f, 0.69f), new Vector3(0.30f, 0.13f, 0.05f), G.LightSteel); // 서로 읽히는 공구 머리
        }
        P.HVAC(p, new Vector3(6f, 0f, 25f)); // 정비 구역 끝 냉각 설비
        G.Sign(p, "repair", new Vector3(15f, 4.6f, 6f), 9f, 1.8f); // 총기 준비 구역 표시
    }

    private static void BuildCirculation(Transform world) // 안전 통로와 관제 기준점
    {
        G.Stripe(world, "SpineWest", new Vector3(100.3f, 0f, 129f), new Vector3(0.12f, 0f, 171f), G.Cyan); // 사격선 밖 북쪽 주 통로
        G.Stripe(world, "SpineEast", new Vector3(107.7f, 0f, 129f), new Vector3(0.12f, 0f, 171f), G.Cyan); // 충분한 이동 폭 확보
        G.Stripe(world, "CrossAvenue", new Vector3(65f, 0f, 58f), new Vector3(95f, 0f, 0.14f), G.Cyan); // 서쪽 이동 구역 연결
        G.Stripe(world, "WestAccess", new Vector3(47f, 0f, 138f), new Vector3(0.12f, 0f, 152f), G.Violet); // 잠입 구역의 외부 복귀 통로
        G.Stripe(world, "RangeRearWalk", new Vector3(143f, 0f, 30f), new Vector3(69f, 0f, 0.12f), G.Cyan); // 저격 사격선 뒤 출입 통로
        Transform tower = G.Node(world, "ControlTower_Landmark", new Vector3(53f, 0f, 39f)); // 중앙 방향 기준 시설
        G.Box(tower, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(6f, 1f, 6f), G.Concrete, true); // 관제 구조 받침
        G.Box(tower, "Shaft", new Vector3(0f, 5.3f, 0f), new Vector3(2.8f, 9.6f, 2.8f), G.Steel, true); // 높은 중심 기둥
        G.Box(tower, "ControlCab", new Vector3(0f, 10.1f, 0f), new Vector3(7f, 2.4f, 5f), G.LightSteel, true); // 관제실 덩어리
        G.Box(tower, "Window", new Vector3(0f, 10.25f, -2.515f), new Vector3(5.9f, 1.05f, 0.035f), G.Dark); // 반투명 재질 없는 관측창 표현
        G.Box(tower, "Roof", new Vector3(0f, 11.55f, 0f), new Vector3(7.8f, 0.3f, 5.8f), G.Steel); // 관제실 지붕
        G.Sign(tower, "tower", new Vector3(0f, 7f, -1.46f), 4.4f, 2.2f); // 먼 곳에서 읽는 시설 기준번호
        G.Beam(tower, "Antenna", new Vector3(1f, 11.7f, 0f), new Vector3(1f, 15f, 0f), 0.065f, G.Steel); // 먼 거리 실루엣
        for (int i = 0; i < 6; i++) // 외부 시설의 최소 설비 반복
        {
            float z = 53f + i * 29f; // 주 통로 간격
            G.Box(world, "WayfindingPost", new Vector3(110f, 2.1f, z), new Vector3(0.2f, 4.2f, 0.2f), G.Steel); // 통로 밖 안내 지지대
            G.Box(world, "WayfindingLight", new Vector3(110f, 4.15f, z), new Vector3(0.4f, 0.12f, 0.4f), G.Cyan); // 제한적 안내 발광
        }
    }
}
#endif
