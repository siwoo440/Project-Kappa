#if UNITY_EDITOR // 표적 사격동 구성
using UnityEngine; // 사격선과 표적 모형
using G = TrainingCenterGeometry; // 시설 공통 부품
using P = TrainingCenterProps; // 시설 상세 모형

public static class TrainingCenterRanges // 각 레인이 독립된 고정 이동 정밀 사격장
{
    public static void Build(TrainingCenterRoot center, TrainingCenterZone fixedRange, TrainingCenterZone moving, TrainingCenterZone precision) // 사격 구역 전체 구성
    {
        BuildFixed(center, fixedRange); // 가까운 거리별 독립 표적
        BuildMoving(center, moving); // 별도 레일의 왕복 표적
        BuildPrecision(center, precision); // 90미터와 150미터 직선 사격
    }

    private static void Service(TrainingCenterRoot center, TrainingCenterZone zone, string sign) // 사격선 뒤의 시험 준비 장치
    {
        P.DoorFrame(zone.transform, new Vector3(zone.Size.x * 0.5f, 0f, 0.5f), 8f, G.Amber, sign); // 시험동 입구
        Transform console = P.Console(zone.transform, new Vector3(zone.Size.x - 3f, 0f, 2f), G.Amber, "reset"); // 우측 뒤쪽 초기화 단말기
        console.gameObject.AddComponent<TrainingCenterConsole>().Configure(center, zone, TrainingCenterConsole.Operation.ResetTargets, "이 구역 표적 초기화 · 장비 보충"); // 구역 단위 동작
    }

    private static void Booth(Transform parent, float x, string label) // 카메라가 들어갈 여유 있는 부스
    {
        Transform booth = G.Node(parent, "Booth_" + label, new Vector3(x, 0f, 6f)); // 사격 기준선 중심
        G.Box(booth, "Roof", new Vector3(0f, 5.8f, -1.5f), new Vector3(9f, 0.24f, 7.2f), G.Steel, true); // 높은 사격 부스 지붕
        for (int side = -1; side <= 1; side += 2) // 사격축을 피한 지지대
        {
            G.Box(booth, "Post", new Vector3(side * 4.2f, 2.9f, -3.2f), new Vector3(0.25f, 5.8f, 0.25f), G.Steel, true); // 후방 구조 기둥
            G.Box(booth, "SideShield", new Vector3(side * 4.2f, 1.35f, 1.6f), new Vector3(0.20f, 2.7f, 3.5f), G.Dark, true); // 사격축 바깥 보호판
        }
        G.Sign(booth, label, new Vector3(0f, 4.15f, 0.8f), 5f, 1.1f); // 조준 위치보다 높은 거리 표기
        G.Stripe(booth, "FiringLine", Vector3.zero, new Vector3(7.8f, 0f, 0.18f), G.Amber); // 정확한 사격 기준선
        G.Box(booth, "SideShelf", new Vector3(-3.4f, 0.85f, 1.0f), new Vector3(1.2f, 0.12f, 1.0f), G.LightSteel, true); // 정면을 막지 않는 장비 받침
    }

    private static TrainingReactiveTarget AddTarget(TrainingCenterZone zone, string name, float x, float meters, float travel, float hp) // 기존 표적 동작을 재사용한 새 배치
    {
        Transform p = zone.transform; // 독립 시험 구역
        TrainingReactiveTarget target = ProjectKDay13Campus.Target(p, name, new Vector3(x, 0f, 6f + meters), travel, hp, G.Steel, G.White, G.Amber, G.Dark, G.Marks); // 기존 총기와 검의 피격 처리 유지
        Transform floor = G.Node(target.transform, "MechanicalDetails", Vector3.zero); // 레일과 함께 정비 가능한 부분
        float span = Mathf.Max(2.4f, travel + 2.4f); // 기존 레일을 포함하는 장치 폭
        G.Box(floor, "CableTray", new Vector3(0f, 0.08f, 0.72f), new Vector3(span, 0.16f, 0.18f), G.Dark); // 걸림 없는 후방 전선 덮개
        G.Box(floor, "MotorServiceBox", new Vector3(span * 0.5f + 0.35f, 0.32f, 0.3f), new Vector3(0.60f, 0.64f, 0.50f), G.LightSteel); // 이동 구동기 외장
        G.Box(floor, "MotorLabel", new Vector3(span * 0.5f + 0.35f, 0.48f, 0.035f), new Vector3(0.43f, 0.07f, 0.026f), G.Amber); // 동작 장치 가독성
        G.Beam(target.Hinge, "RearBrace", new Vector3(0f, 0.05f, 0.13f), new Vector3(0f, 1.40f, 0.16f), 0.035f, G.LightSteel); // 실제로 기울어지는 판 지지대
        for (int side = -1; side <= 1; side += 2) // 몸통 판 테두리 보호
        {
            G.Box(target.Hinge, "PlateEdge", new Vector3(side * 0.48f, 0.95f, -0.04f), new Vector3(0.028f, 1.36f, 0.12f), G.Steel); // 판 두께와 교체 경계
        }
        TextMesh status = target.GetComponentInChildren<TextMesh>(true); // 기존 상태판 하나만 유지
        if (status != null) // 연결된 표시 확인
        {
            status.characterSize = 0.016f; // 여러 표적의 과도한 글자 겹침 감소
        }
        Transform firing = G.Node(p, name + "_FiringPoint", new Vector3(x, 0.14f, 6f)); // 실제 거리 기준점
        TrainingCenterLane lane = firing.gameObject.AddComponent<TrainingCenterLane>(); // 검증용 연결
        lane.Configure(firing, target, meters); // 표시 거리와 실제 배치 연결
        return target; // 추가 기능 연결용
    }

    private static void BuildFixed(TrainingCenterRoot center, TrainingCenterZone zone) // 5미터부터 30미터까지 독립 시험선
    {
        Transform p = zone.transform; // 고정 사격동 기준
        Service(center, zone, "static"); // 초기화와 출입 표시
        G.WallZ(p, "LeftShield", 0f, 10f, 42f, 4.6f); // 좌측 구획
        G.WallZ(p, "RightShield", 48f, 10f, 42f, 4.6f); // 우측 구획
        G.WallX(p, "RearWall", 0f, 48f, 41.7f, 6f); // 공통 후방 방호벽
        float[] xs = new float[] // 카메라 공간을 갖춘 네 레인
        {
            6f, // 첫 레인
            18f, // 둘째 레인
            30f, // 셋째 레인
            42f // 비교 계측 레인
        };
        float[] distances = new float[] // 독립 목표 거리
        {
            5f, // 초근거리
            10f, // 산탄 유효 거리
            20f, // 산탄 감쇠 시험
            30f // 반복 피해 계측
        };
        for (int i = 0; i < xs.Length; i++) // 각 표적 앞뒤 간섭을 막는 배치
        {
            Booth(p, xs[i], "distance_" + (int)distances[i]); // 기준점과 거리 안내
            AddTarget(zone, i == 3 ? "TTK_STATIC_150HP" : "STATIC_" + i, xs[i], distances[i], 0f, i == 3 ? 150f : 1f); // 계측용 하나와 단발 접이 표적
            G.Box(p, "BallisticBack_" + i, new Vector3(xs[i], 2.1f, distances[i] + 8.8f), new Vector3(10.5f, 4.2f, 0.35f), G.Dark, true); // 표적 뒤 깔끔한 배경
            if (i < xs.Length - 1) // 인접 레인 구분
            {
                G.WallZ(p, "LaneDivider_" + i, xs[i] + 6f, 9f, 40f, 3.2f); // 다른 시험 사선 침범 방지
            }
        }
    }

    private static void BuildMoving(TrainingCenterRoot center, TrainingCenterZone zone) // 독립된 왕복 표적 사격선
    {
        Transform p = zone.transform; // 이동 시험동 기준
        Service(center, zone, "moving"); // 구역 조작 연결
        for (int i = 0; i < 4; i++) // 표적의 전체 이동 폭을 감싼 벽
        {
            G.WallZ(p, "LaneShield_" + i, i * 16f, 10f, 43f, 4.6f); // 앞뒤 표적이 겹치지 않는 구획
        }
        G.WallX(p, "Backstop", 0f, 48f, 43.6f, 6f); // 후방 방호벽
        for (int i = 0; i < 3; i++) // 왕복 레일 세 곳
        {
            float x = 8f + i * 16f; // 충분한 레일 간격
            Booth(p, x, i == 1 ? "ttk" : "moving_lane"); // 일반 표적과 계측 표적 구분
            AddTarget(zone, i == 1 ? "TTK_MOVING_150HP" : "MOVING_" + i, x, 22f, 8f, i == 1 ? 150f : 1f); // 초당 기존 속도의 좌우 왕복
            G.Box(p, "RailBed", new Vector3(x, 0.025f, 28f), new Vector3(12f, 0.04f, 2.3f), G.Dark); // 노출 레일을 정돈하는 바닥 홈 표현
        }
    }

    private static void BuildPrecision(TrainingCenterRoot center, TrainingCenterZone zone) // 완전히 분리된 장거리 두 사격선
    {
        Transform p = zone.transform; // 장거리 동 기준
        Service(center, zone, "precision"); // 입구와 보급
        G.WallZ(p, "WestWall", 0f, 10f, 175f, 5f); // 주 통로와 사격선 분리
        G.WallZ(p, "MiddleWall", 31f, 10f, 175f, 4.5f); // 90미터 표적이 150미터 표적을 가리지 않게 분리
        G.WallZ(p, "EastWall", 62f, 10f, 175f, 5f); // 외측 정비 통로 분리
        G.WallX(p, "TerminalWall", 0f, 62f, 175f, 7f); // 원거리 뒷벽
        Booth(p, 16f, "distance_90"); // 첫 장거리 부스
        Booth(p, 46f, "distance_150"); // 두 번째 장거리 부스
        AddTarget(zone, "PRECISION_90M", 16f, 90f, 0f, 1f); // 같은 기준선의 90미터 표적
        AddTarget(zone, "PRECISION_150M", 46f, 150f, 0f, 1f); // 독립 사격선의 150미터 표적
        G.Box(p, "TargetBackdrop90", new Vector3(16f, 3.5f, 100f), new Vector3(16f, 7f, 0.45f), G.Dark, true); // 표적 뒤 대비 배경
        G.Box(p, "TargetBackdrop150", new Vector3(46f, 3.5f, 160f), new Vector3(16f, 7f, 0.45f), G.Dark, true); // 정밀 조준 배경
        for (int z = 36; z < 172; z += 30) // 실제 사격선보다 높은 보호 구조
        {
            G.Box(p, "OverheadBaffle", new Vector3(31f, 8f, z), new Vector3(61.5f, 0.5f, 1.0f), G.Steel, true); // 탄도와 시야를 막지 않는 상부 방호판
            G.Stripe(p, "DistanceSideTick", new Vector3(60.8f, 0f, z), new Vector3(0.6f, 0f, 0.12f), G.Amber); // 측면 거리 기준
        }
    }
}
#endif
