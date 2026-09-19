#if UNITY_EDITOR // 이동 잠입 실전 구역 편집
using System; // 필수 이동 레이어 확인
using UnityEngine; // 실제 이동 구조물
using G = TrainingCenterGeometry; // 공통 시설 모형
using P = TrainingCenterProps; // 상세 부품

public static class TrainingCenterMovementCourse // 이동 경로와 실전 공간을 분리한 시험동
{
    private static void Climbable(Transform root) // 기존 파쿠르 마스크에 맞춘 실제 표면
    {
        int layer = LayerMask.NameToLayer("ParkourSurface"); // 기존 이동용 레이어 조회
        if (layer < 0) // 선행 레이어 존재 확인
        {
            throw new InvalidOperationException("ParkourSurface 레이어가 없습니다. 기존 이동 설정을 확인하세요."); // 가짜 이동 표면 생성 방지
        }
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) // 표시가 있는 이동 구조만 조회
        {
            collider.gameObject.layer = layer; // 벽 달리기와 난간 감지 허용
        }
    }

    public static void Build(TrainingCenterRoot center, TrainingCenterZone parkour, TrainingCenterZone stealth, TrainingCenterZone live) // 세 동의 기능별 배치
    {
        Parkour(center, parkour); // 안전한 수직 이동 연습
        MockStreet(center, stealth); // 실제 경비를 사용하는 잠입 시험
        Arena(center, live); // 별도 근접 전투 공간
    }

    private static void Ramp(Transform parent, Vector3 start, float width, float run, float rise, float yaw = 0f) // 일반 이동으로도 오르는 우회 경사로
    {
        Transform root = G.Node(parent, "BypassRamp", start); // 경사로 시작점
        root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 연결 방향
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg; // 실제 경사각
        float length = Mathf.Sqrt(run * run + rise * rise); // 기울어진 표면 길이
        GameObject deck = G.Box(root, "WalkableDeck", new Vector3(0f, rise * 0.5f + 0.14f, run * 0.5f), new Vector3(width, 0.28f, length), G.Steel, true); // 낮은 시작턱의 충돌판
        deck.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f); // 앞쪽으로 올라가는 기울기
        for (int side = -1; side <= 1; side += 2) // 경사로 측면 손잡이
        {
            G.Beam(root, "Handrail", new Vector3(side * width * 0.5f, 1.0f, 0f), new Vector3(side * width * 0.5f, rise + 1f, run), 0.045f, G.LightSteel, true); // 우회 이동 난간
            G.Beam(root, "Guide", new Vector3(side * width * 0.5f, 0.18f, 0f), new Vector3(side * width * 0.5f, rise + 0.18f, run), 0.025f, G.Violet); // 경사 진행 표시
        }
    }

    private static void Parkour(TrainingCenterRoot center, TrainingCenterZone zone) // 단계별 이동 훈련장
    {
        Transform p = zone.transform; // 이동동 기준
        P.DoorFrame(p, new Vector3(30f, 0f, 0.5f), 8f, G.Violet, "parkour"); // 벽 이동 훈련 안내
        G.WallZ(p, "OuterRail", 0f, 0f, 71f, 2.0f); // 서쪽 안전 경계
        G.WallX(p, "NorthBoundary", 0f, 40f, 70.5f, 3.5f); // 모의 골목과 공간 분리
        Transform course = G.Node(p, "ClimbableCourse", Vector3.zero); // 실제 파쿠르 가능한 표면 모음
        for (int step = 0; step < 3; step++) // 쉬운 순서의 점프 발판
        {
            float height = 1.0f + step * 1.0f; // 점프 가능한 높이 증가
            G.Box(course, "Step_" + step, new Vector3(15f + step * 5f, height * 0.5f, 12f), new Vector3(3.5f, height, 4f), G.Concrete, true); // 벽과 상단을 함께 감지
            G.Box(course, "GrabbableEdge_" + step, new Vector3(15f + step * 5f, height - 0.07f, 9.985f), new Vector3(3.2f, 0.12f, 0.018f), G.Violet); // 잡을 수 있는 가장자리 표시
        }
        G.Box(course, "WallRun_Left", new Vector3(17f, 2.2f, 32f), new Vector3(0.4f, 4.4f, 17f), G.Concrete, true); // 좌측 벽 달리기 표면
        G.Box(course, "WallRun_Right", new Vector3(26f, 2.2f, 32f), new Vector3(0.4f, 4.4f, 17f), G.Concrete, true); // 반대편 벽 시험
        G.Box(course, "RunGuide_Left", new Vector3(17.21f, 1.65f, 32f), new Vector3(0.018f, 0.15f, 16f), G.Violet); // 주행 높이 안내
        G.Box(course, "RunGuide_Right", new Vector3(25.79f, 1.65f, 32f), new Vector3(0.018f, 0.15f, 16f), G.Violet); // 반대편 안내선
        G.Box(course, "LowerRoof", new Vector3(8f, 1.4f, 42f), new Vector3(10f, 2.8f, 8f), G.Concrete, true); // 낮은 옥상
        G.Box(course, "UpperRoof", new Vector3(21f, 2.9f, 53f), new Vector3(14f, 5.8f, 10f), G.Concrete, true); // 높은 옥상
        G.Box(course, "RoofBridge", new Vector3(29f, 5.85f, 59f), new Vector3(11f, 0.18f, 3.5f), G.Steel, true); // 마지막 착지 연결
        Ramp(p, new Vector3(8f, 0f, 20f), 3.4f, 18f, 2.65f); // 파쿠르 없이 낮은 옥상 접근
        Ramp(p, new Vector3(8f, 2.8f, 45f), 3.4f, 14f, 3f, 90f); // 낮은 옥상에서 높은 옥상 우회
        G.Box(course, "UpperLanding", new Vector3(22f, 5.83f, 46.5f), new Vector3(3.4f, 0.18f, 4f), G.Steel, true); // 우회 경사로와 높은 옥상 연결
        G.Box(course, "CrouchTunnelRoof", new Vector3(33f, 1.42f, 28f), new Vector3(3f, 0.18f, 7f), G.Steel, true); // 앉기 통과 높이의 터널
        G.Box(course, "TunnelLeft", new Vector3(31.5f, 0.7f, 28f), new Vector3(0.16f, 1.4f, 7f), G.Concrete, true); // 터널 왼쪽
        G.Box(course, "TunnelRight", new Vector3(34.5f, 0.7f, 28f), new Vector3(0.16f, 1.4f, 7f), G.Concrete, true); // 터널 오른쪽
        Climbable(course); // 기존 파쿠르 레이어에 명시 연결
        P.HVAC(p, new Vector3(18f, 5.8f, 55f)); // 상단의 시설 형태
        P.HVAC(p, new Vector3(6f, 2.8f, 43f)); // 낮은 옥상 설비
        G.Sign(p, "movement_rules", new Vector3(31f, 3.5f, 17f), 7f, 1.7f); // 기존 조작 안내
        G.Stripe(p, "LandingArea", new Vector3(33f, 0f, 62f), new Vector3(5f, 0f, 5f), G.Violet); // 명확한 최종 착지 구역
        Transform terminal = P.Console(p, new Vector3(36f, 0f, 5f), G.Cyan, "supply"); // 이동 시험 전 보급
        terminal.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, zone, TrainingCenterConsole.Operation.Supply, "이동 시험 준비 · 체력과 장비 보충"); // 기존 회복만 재사용
    }

    private static void TrialTerminal(TrainingCenterRoot center, TrainingCenterZone zone, Vector3 point, string sign, float yaw) // 출입구의 명시적 시험 시작
    {
        Transform terminal = P.Console(zone.transform, point, G.Red, "trial", yaw); // 조작 장치 형태
        terminal.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, zone, TrainingCenterConsole.Operation.Trial, sign + " 시작 / 종료"); // 같은 F키로 전환
        Renderer lamp = G.Box(terminal, "TrialLamp", new Vector3(0.7f, 1.7f, 0f), new Vector3(0.18f, 0.45f, 0.18f), G.Cyan).GetComponent<Renderer>(); // 한 개의 실행 상태등
        zone.SetLamp(lamp, G.Cyan, G.Red); // 구역 상태에 맞춰 색상만 전환
    }

    private static void Building(Transform parent, Vector3 point, float width, float depth, float height, string sign) // 올라갈 수 있는 공장 골목 외벽
    {
        Transform root = G.Node(parent, "ClimbableBuilding_" + sign, point); // 경로와 장식 구분
        G.Box(root, "Structure", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), G.Concrete, true); // 모의 건물의 기본 충돌
        G.Box(root, "RoofEdge", new Vector3(0f, height + 0.04f, 0f), new Vector3(width + 0.25f, 0.08f, depth + 0.25f), G.Steel); // 잡는 모서리 시각 표현
        G.Box(root, "Shutter", new Vector3(0f, 1.35f, -depth * 0.5f - 0.025f), new Vector3(width * 0.62f, 2.7f, 0.04f), G.Steel); // 열리지 않는 정비 셔터 장식
        for (int slat = 0; slat < 7; slat++) // 셔터 표면 분할
        {
            G.Box(root, "ShutterSlat", new Vector3(0f, 0.22f + slat * 0.38f, -depth * 0.5f - 0.052f), new Vector3(width * 0.6f, 0.024f, 0.012f), G.Dark); // 패널 사이 틈
        }
        G.Sign(root, sign, new Vector3(0f, height - 0.65f, -depth * 0.5f - 0.06f), Mathf.Min(5f, width - 1f), 0.75f); // 모의 도시 구획 식별
        G.Beam(root, "ServicePipe", new Vector3(width * 0.5f - 0.45f, 0.2f, -depth * 0.5f - 0.18f), new Vector3(width * 0.5f - 0.45f, height - 0.2f, -depth * 0.5f - 0.18f), 0.085f, G.LightSteel); // 장식 배관은 이동 판정에서 제외
        Climbable(root); // 건물 외벽과 옥상 파쿠르 허용
        P.HVAC(root, new Vector3(-1f, height + 0.08f, 1f)); // 옥상 냉각 설비
    }

    private static void MockStreet(TrainingCenterRoot center, TrainingCenterZone zone) // 세 가지 접근 경로가 있는 모의 골목
    {
        Transform p = zone.transform; // 잠입 구역 기준
        G.WallZ(p, "WestBarrier", 0f, 0f, 78f, 5f); // 외곽 경계
        G.WallX(p, "SouthBarrier", 0f, 40f, 0f, 5f); // 안전 통로와 분리
        G.WallX(p, "NorthBarrier", 0f, 40f, 78f, 5f); // 북쪽 경계
        G.WallZ(p, "EastBarrierSouth", 40f, 0f, 3f, 5f); // 동쪽 입구 아래 벽
        G.WallZ(p, "EastBarrierNorth", 40f, 13f, 78f, 5f); // 동쪽 입구 위 벽
        P.DoorFrame(p, new Vector3(40f, 0f, 8f), 10f, G.Red, "stealth", -90f); // 주 통로를 향한 잠입 출입구
        TrialTerminal(center, zone, new Vector3(38f, 0f, 4f), "잠입 시험", -90f); // 출입 시 사용 가능한 시험 제어
        Building(p, new Vector3(8f, 0f, 27f), 10f, 12f, 4f, "mock_factory"); // 낮은 모의 공장
        Building(p, new Vector3(31f, 0f, 36f), 10f, 15f, 6f, "mock_storage"); // 높은 저장 시설
        Building(p, new Vector3(8f, 0f, 62f), 10f, 13f, 5f, "mock_service"); // 돌아오는 정비 구획
        Ramp(p, new Vector3(4f, 0f, 5f), 3.2f, 16f, 3.85f); // 지상에서 첫 지붕의 우회 경로
        G.Box(p, "ObservationLanding", new Vector3(19f, 3.95f, 33f), new Vector3(12f, 0.16f, 3.2f), G.Steel, true); // 경비를 내려다보는 상부 연결
        G.Box(p, "HiddenPassageLeft", new Vector3(17f, 1.7f, 51f), new Vector3(0.35f, 3.4f, 15f), G.Concrete, true); // 뒤쪽 접근을 위한 시야 차단벽
        P.Crate(p, new Vector3(27f, 0f, 15f), G.Amber, true); // 진입점 시야를 끊는 엄폐
        P.Crate(p, new Vector3(20f, 0f, 44f), G.Amber); // 중앙 낮은 엄폐
        P.Crate(p, new Vector3(28f, 0f, 63f), G.Amber, true); // 북쪽 높은 엄폐
        G.Sign(p, "trial_rules", new Vector3(34f, 3.2f, 14f), 6.3f, 1.8f); // 시험 시작과 이탈 규칙
    }

    private static void Arena(TrainingCenterRoot center, TrainingCenterZone zone) // 방해물 적은 검술과 실전 사격 구역
    {
        Transform p = zone.transform; // 교전동 기준
        G.WallZ(p, "WestBarrier", 0f, 0f, 45f, 5f); // 다른 시험동과 분리
        G.WallX(p, "SouthBarrier", 0f, 47f, 0f, 5f); // 후방 접근 차단
        G.WallX(p, "NorthBarrier", 0f, 47f, 45f, 6f); // 북쪽 경계
        G.WallZ(p, "EastBarrierSouth", 47f, 0f, 3f, 5f); // 출입구 아래 경계
        G.WallZ(p, "EastBarrierNorth", 47f, 13f, 45f, 5f); // 출입구 위 경계
        P.DoorFrame(p, new Vector3(47f, 0f, 8f), 10f, G.Red, "live", -90f); // 동쪽 안전 통로 출입
        TrialTerminal(center, zone, new Vector3(44f, 0f, 4f), "실전 교전 시험", -90f); // 시험 동작 제어
        G.Box(p, "EntranceBaffle", new Vector3(32f, 2f, 8f), new Vector3(0.6f, 4f, 9f), G.Concrete, true); // 입구에서 적까지 직선 시야 차단
        G.Stripe(p, "DuelSquareSouth", new Vector3(20f, 0f, 16f), new Vector3(20f, 0f, 0.15f), G.Red); // 넓은 결투 구역 표시
        G.Stripe(p, "DuelSquareNorth", new Vector3(20f, 0f, 36f), new Vector3(20f, 0f, 0.15f), G.Red); // 북쪽 결투 경계
        G.Stripe(p, "DuelSquareWest", new Vector3(10f, 0f, 26f), new Vector3(0.15f, 0f, 19.6f), G.Red); // 서쪽 결투 경계
        G.Stripe(p, "DuelSquareEast", new Vector3(30f, 0f, 26f), new Vector3(0.15f, 0f, 19.6f), G.Red); // 동쪽 결투 경계
        P.Crate(p, new Vector3(6f, 0f, 10f), G.Red, true); // 가장자리 높은 엄폐
        P.Crate(p, new Vector3(40f, 0f, 26f), G.Red); // 가장자리 낮은 엄폐
        P.Crate(p, new Vector3(7f, 0f, 39f), G.Red); // 후방 엄폐
        G.Sign(p, "trial_rules", new Vector3(31.64f, 3.0f, 8f), 6f, 1.6f, -90f); // 입장 뒤 시험 규칙
    }
}
#endif
