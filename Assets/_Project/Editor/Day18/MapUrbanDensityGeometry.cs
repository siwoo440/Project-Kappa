#if UNITY_EDITOR // 18일차 도시 밀도 모형 편집 전용
using ProjectK.Day16; // 본편 월드 참조
using UnityEngine; // 모형과 좌표 구성

public sealed class MapUrbanDensityGeometry // 도로·상점·차량·옥상 중간 밀도 생성기
{
    private readonly MapCyberpunkGeometry g; // 17일차 공통 네온 모형 재사용
    public int StorefrontCount { get; private set; } // 상점 전면 집계
    public int VehicleCount { get; private set; } // 차량과 바이크 집계
    public int StreetPropCount { get; private set; } // 거리 소품 집계
    public int RooftopCount { get; private set; } // 옥상 구조 집계
    public int BridgeCount { get; private set; } // 공중 연결 구조 집계
    public int CableCount { get; private set; } // 케이블 집계
    public int PoleCount { get; private set; } // 전봇대와 가로등 집계
    public int SidewalkCount { get; private set; } // 보도 블럭 구간 집계
    public int NeonSignCount => g.SignCount; // 공통 간판 집계 조회
    public int ClusterCount => g.ClusterCount; // 공통 묶음 집계 조회
    public int LightCount => g.LightCount; // 실제 점광원 집계 조회

    public MapUrbanDensityGeometry(Transform root, MapWorldRoot world) // 기존 Day17 재질과 거리 묶음 연결
    {
        g = new MapCyberpunkGeometry(root, world); // 공통 생성기 준비
    }

    public Transform Cluster(string name, Vector3 center, float distance) // 타일 단위 거리 묶음 생성
    {
        return g.Cluster(name, center, distance); // 기존 거리 표시 구조 재사용
    }

    public void Storefront(Transform parent, Vector3 localPosition, float width, string label, string neon, bool shutter) // 1층 상점 전면 생성
    {
        Transform front = g.Node(parent, "Storefront", localPosition); // 상점 전면 기준 생성
        float safeWidth = Mathf.Clamp(width, 4.8f, 9.5f); // 전면 폭 보정
        g.Box(front, "BackPanel", new Vector3(0f, 1.65f, 0f), new Vector3(safeWidth, 3.3f, 0.26f), "DarkMetal", false); // 기존 벽 앞 얇은 전면 추가
        g.Box(front, "DoorFrame", new Vector3(-safeWidth * 0.31f, 1.25f, -0.16f), new Vector3(1.55f, 2.5f, 0.18f), "Steel", false); // 출입구 프레임 추가
        g.Box(front, "Door", new Vector3(-safeWidth * 0.31f, 1.18f, -0.27f), new Vector3(1.15f, 2.25f, 0.08f), "DarkMetal", false); // 닫힌 출입문 표현
        if (shutter) // 셔터형 상점 확인
        {
            g.Box(front, "Shutter", new Vector3(safeWidth * 0.13f, 1.42f, -0.18f), new Vector3(safeWidth * 0.50f, 2.45f, 0.12f), "Steel", false); // 폐쇄 셔터 추가
            for (int i = 0; i < 7; i++) // 셔터 가로 홈 반복
            {
                g.Box(front, "ShutterGroove", new Vector3(safeWidth * 0.13f, 0.45f + i * 0.32f, -0.25f), new Vector3(safeWidth * 0.46f, 0.035f, 0.03f), "DarkMetal", false); // 셔터 층선 표현
            }
        }
        else // 영업형 상점 확인
        {
            g.Box(front, "Window", new Vector3(safeWidth * 0.14f, 1.50f, -0.20f), new Vector3(safeWidth * 0.52f, 1.85f, 0.10f), "WetConcrete", false); // 어두운 진열창 바탕 추가
            g.NeonStrip(front, "WindowGlow", new Vector3(safeWidth * 0.14f, 1.50f, -0.27f), new Vector3(safeWidth * 0.45f, 0.08f, 0.04f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.82f, localPosition.x * 0.03f); // 진열창 네온 띠 추가
        }
        g.Box(front, "Awning", new Vector3(0f, 3.18f, -0.72f), new Vector3(safeWidth * 0.82f, 0.15f, 1.25f), "DarkMetal", false); // 상점 차양 추가
        g.NeonStrip(front, "AwningLight", new Vector3(0f, 3.08f, -1.36f), new Vector3(safeWidth * 0.74f, 0.07f, 0.06f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.75f, localPosition.z * 0.02f); // 차양 아래 조명 추가
        g.NeonSign(front, "ShopSign", new Vector3(0f, 4.05f, -0.34f), new Vector3(safeWidth * 0.54f, 1.05f, 0f), label, neon, MapCyberpunkGeometry.ColorFor(neon), Quaternion.identity); // 겹침을 줄인 상점 이름 간판 추가
        StorefrontCount++; // 상점 전면 집계 증가
    }

    public void Billboard(Transform parent, Vector3 localPosition, Vector3 size, string label, string neon) // 중대형 외벽 광고판 생성
    {
        g.NeonSign(parent, "FacadeBillboard", localPosition, size, label, neon, MapCyberpunkGeometry.ColorFor(neon), Quaternion.identity); // 기존 네온 간판 모형으로 광고판 추가
        StreetPropCount++; // 광고 구조 집계 증가
    }

    public void RooftopAnnex(Transform parent, Vector3 localPosition, Vector3 size, string neon) // 건물 실루엣을 나누는 옥상 증축 생성
    {
        Transform annex = g.Node(parent, "RooftopAnnex", localPosition); // 옥상 증축 기준 생성
        Vector3 bodySize = new Vector3(Mathf.Clamp(size.x, 4.5f, 9f), Mathf.Clamp(size.y, 2.5f, 4.8f), Mathf.Clamp(size.z, 4f, 8f)); // 증축 크기 보정
        g.Box(annex, "PlantRoom", new Vector3(0f, bodySize.y * 0.5f, 0f), bodySize, "DarkMetal", true); // 실제 올라설 수 있는 기계실 추가
        g.Box(annex, "RoofCap", new Vector3(0f, bodySize.y + 0.08f, 0f), new Vector3(bodySize.x + 0.25f, 0.16f, bodySize.z + 0.25f), "Steel", false); // 지붕 덮개 추가
        g.AirUnit(annex, new Vector3(-bodySize.x * 0.23f, bodySize.y + 0.10f, 0f), 0.65f); // 옥상 실외기 추가
        g.NeonStrip(annex, "ServiceBeacon", new Vector3(bodySize.x * 0.34f, bodySize.y * 0.60f, -bodySize.z * 0.51f), new Vector3(0.10f, bodySize.y * 0.70f, 0.05f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.65f, localPosition.x * 0.02f); // 정비실 수직 신호등 추가
        RooftopCount++; // 옥상 구조 집계 증가
    }

    public void FireEscape(Transform parent, Vector3 localPosition, float width, float availableHeight) // 외벽 비상 발판과 사다리 생성
    {
        Transform escape = g.Node(parent, "FireEscape", localPosition); // 외벽 탈출 구조 기준 생성
        int levels = Mathf.Clamp(Mathf.FloorToInt((availableHeight - 3f) / 3f), 2, 4); // 설치 가능한 층수 계산
        for (int level = 0; level < levels; level++) // 층별 발판 생성
        {
            float y = 3.1f + level * 3f; // 발판 높이 계산
            g.Box(escape, "Platform", new Vector3(0f, y, 0f), new Vector3(width, 0.24f, 1.25f), "Steel", true); // 실제 밟을 수 있는 발판 추가
            for (int side = -1; side <= 1; side += 2) // 양쪽 난간 생성
            {
                g.Box(escape, "RailPost", new Vector3(side * width * 0.47f, y + 0.65f, -0.52f), new Vector3(0.08f, 1.25f, 0.08f), "Steel", false); // 외곽 난간 기둥 추가
            }
            g.Box(escape, "FrontRail", new Vector3(0f, y + 1.18f, -0.52f), new Vector3(width, 0.08f, 0.08f), "Steel", false); // 전면 난간 추가
        }
        for (int rung = 0; rung < levels * 6; rung++) // 연속 사다리 가로대 생성
        {
            g.Box(escape, "LadderRung", new Vector3(width * 0.34f, 1.1f + rung * 0.48f, -0.44f), new Vector3(0.75f, 0.07f, 0.07f), "Steel", false); // 사다리 가로대 표현
        }
        StreetPropCount++; // 외벽 구조 집계 증가
    }

    public void ServiceBridge(Transform parent, Vector3 localStart, Vector3 localEnd, string neon) // 건물 사이 중간 높이 연결 통로 생성
    {
        Vector3 delta = localEnd - localStart; // 두 건물 사이 방향 계산
        float length = Mathf.Max(2f, delta.magnitude); // 연결 길이 보정
        Vector3 midpoint = (localStart + localEnd) * 0.5f; // 연결 통로 중심 계산
        Transform bridge = g.Node(parent, "ServiceBridge", midpoint); // 공중 연결 기준 생성
        bridge.localRotation = Quaternion.FromToRotation(Vector3.right, delta.normalized); // 가로 축을 두 점 방향으로 회전
        g.Box(bridge, "Deck", Vector3.zero, new Vector3(length, 0.28f, 1.8f), "Steel", true); // 실제 보행 가능한 바닥 추가
        for (int side = -1; side <= 1; side += 2) // 양쪽 난간 생성
        {
            g.Box(bridge, "Rail", new Vector3(0f, 0.78f, side * 0.84f), new Vector3(length, 1.25f, 0.08f), "DarkMetal", false); // 공중 통로 난간 추가
            g.NeonStrip(bridge, "GuideLight", new Vector3(0f, 0.18f, side * 0.91f), new Vector3(length * 0.94f, 0.055f, 0.045f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.55f, midpoint.y * 0.03f + side); // 통로 가장자리 조명 추가
        }
        BridgeCount++; // 공중 통로 집계 증가
    }

    public void ParkedCar(Transform parent, Vector3 localPosition, float yaw, string neon) // 정차된 저상 차량 생성
    {
        Transform car = g.Node(parent, "ParkedCar", localPosition); // 차량 기준 생성
        car.localRotation = Quaternion.Euler(0f, yaw, 0f); // 도로 방향 적용
        g.Box(car, "LowerBody", new Vector3(0f, 0.58f, 0f), new Vector3(4.3f, 0.65f, 1.82f), "DarkMetal", true); // 차량 하부 충돌체 추가
        g.Box(car, "Cabin", new Vector3(-0.2f, 1.18f, 0f), new Vector3(2.35f, 0.78f, 1.58f), "Steel", false); // 상부 객실 외형 추가
        g.Box(car, "Windshield", new Vector3(0.76f, 1.28f, 0f), new Vector3(0.08f, 0.48f, 1.22f), "WetConcrete", false); // 전면 유리 표현
        for (int x = -1; x <= 1; x += 2) // 앞뒤 차축 순회
        {
            for (int z = -1; z <= 1; z += 2) // 좌우 바퀴 순회
            {
                g.Cylinder(car, "Wheel", new Vector3(x * 1.45f, 0.38f, z * 0.92f), new Vector3(0.34f, 0.12f, 0.34f), "DarkMetal", Quaternion.Euler(0f, 0f, 90f), true); // 바퀴 모형과 충돌 추가
            }
        }
        g.NeonStrip(car, "TailLight", new Vector3(-2.18f, 0.68f, 0f), new Vector3(0.05f, 0.18f, 1.05f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.72f, localPosition.x * 0.01f); // 차량 후방 네온 표시 추가
        VehicleCount++; // 차량 집계 증가
    }

    public void DeliveryBike(Transform parent, Vector3 localPosition, float yaw, string neon) // 소형 배달 바이크 생성
    {
        Transform bike = g.Node(parent, "DeliveryBike", localPosition); // 바이크 기준 생성
        bike.localRotation = Quaternion.Euler(0f, yaw, 0f); // 도로 방향 적용
        for (int z = -1; z <= 1; z += 2) // 앞뒤 바퀴 생성
        {
            g.Cylinder(bike, "Wheel", new Vector3(0f, 0.52f, z * 0.85f), new Vector3(0.34f, 0.10f, 0.34f), "DarkMetal", Quaternion.Euler(0f, 0f, 90f), true); // 얇은 바퀴 추가
        }
        g.Box(bike, "Frame", new Vector3(0f, 0.72f, 0f), new Vector3(0.45f, 0.36f, 1.35f), "Steel", true); // 차체 충돌 외형 추가
        g.Box(bike, "CargoPod", new Vector3(0f, 1.10f, -0.52f), new Vector3(0.75f, 0.72f, 0.72f), "DarkMetal", false); // 후방 배달 상자 추가
        g.Box(bike, "Handle", new Vector3(0f, 1.18f, 0.62f), new Vector3(1.05f, 0.07f, 0.07f), "Steel", false); // 핸들바 표현
        g.NeonStrip(bike, "StatusLight", new Vector3(0f, 1.12f, -0.90f), new Vector3(0.45f, 0.12f, 0.04f), neon, MapCyberpunkGeometry.ColorFor(neon), 1.0f, localPosition.z * 0.02f); // 배달 상태 네온 추가
        VehicleCount++; // 바이크 집계 증가
    }

    public void CargoStack(Transform parent, Vector3 localPosition, string accent) // 배송 상자와 팔레트 묶음 생성
    {
        Transform cargo = g.Node(parent, "CargoStack", localPosition); // 화물 기준 생성
        g.Box(cargo, "Pallet", new Vector3(0f, 0.10f, 0f), new Vector3(2.4f, 0.20f, 1.55f), "DarkMetal", true); // 낮은 팔레트 추가
        g.Box(cargo, "CaseA", new Vector3(-0.55f, 0.65f, 0f), new Vector3(1.0f, 1.0f, 1.2f), "Steel", true); // 첫 화물 상자 추가
        g.Box(cargo, "CaseB", new Vector3(0.58f, 0.58f, 0.18f), new Vector3(1.05f, 0.86f, 1.0f), "Steel", true); // 둘째 화물 상자 추가
        g.Box(cargo, "CaseC", new Vector3(0.18f, 1.42f, -0.18f), new Vector3(1.25f, 0.62f, 0.85f), "DarkMetal", true); // 상단 화물 상자 추가
        g.NeonStrip(cargo, "CargoId", new Vector3(-0.55f, 0.75f, -0.62f), new Vector3(0.48f, 0.10f, 0.035f), accent, MapCyberpunkGeometry.ColorFor(accent), 0.62f, localPosition.x * 0.03f); // 화물 식별 표시 추가
        StreetPropCount++; // 화물 묶음 집계 증가
    }

    public void Puddle(Transform parent, Vector3 localPosition, Vector2 size) // 젖은 도로 패치 생성
    {
        g.Box(parent, "RoadPuddle", new Vector3(localPosition.x, localPosition.y + 0.015f, localPosition.z), new Vector3(Mathf.Max(1.5f, size.x), 0.025f, Mathf.Max(0.9f, size.y)), "WetConcrete", false); // 도로 위 얇은 반사 패치 추가
        StreetPropCount++; // 바닥 디테일 집계 증가
    }

    public void Curb(Transform parent, Vector3 localPosition, Vector3 size) // 도로와 보행 부지를 나누는 낮은 턱 생성
    {
        g.Box(parent, "Curb", localPosition + Vector3.up * 0.09f, new Vector3(size.x, 0.18f, size.z), "Steel", true); // 낮은 보행 경계 추가
        StreetPropCount++; // 경계 소품 집계 증가
    }

    public void TrafficPylon(Transform parent, Vector3 localPosition, string neon) // 도로 모서리 신호 기둥 생성
    {
        Transform pylon = g.Node(parent, "TrafficPylon", localPosition); // 신호 기둥 기준 생성
        g.Box(pylon, "Pole", new Vector3(0f, 1.5f, 0f), new Vector3(0.16f, 3f, 0.16f), "Steel", true); // 충돌 있는 기둥 추가
        g.Box(pylon, "SignalBox", new Vector3(0f, 2.55f, 0f), new Vector3(0.58f, 0.82f, 0.38f), "DarkMetal", false); // 신호 장치 본체 추가
        g.NeonStrip(pylon, "Signal", new Vector3(0f, 2.62f, -0.21f), new Vector3(0.26f, 0.44f, 0.04f), neon, MapCyberpunkGeometry.ColorFor(neon), 1.2f, localPosition.x * 0.02f); // 신호 네온 추가
        StreetPropCount++; // 신호 기둥 집계 증가
    }

    public void UtilityPole(Transform parent, Vector3 localPosition, float height, string neon, bool createLight) // 전봇대와 가로등 겸용 구조 생성
    {
        Transform pole = g.Node(parent, "UtilityPole", localPosition); // 전봇대 기준 생성
        float safeHeight = Mathf.Clamp(height, 4.8f, 8.5f); // 구조 높이 보정
        g.Box(pole, "Base", new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.36f, 0.7f), "DarkMetal", true); // 낮은 기초 구조 추가
        g.Box(pole, "Pole", new Vector3(0f, safeHeight * 0.5f, 0f), new Vector3(0.24f, safeHeight, 0.24f), "Steel", true); // 세로 기둥 추가
        g.Box(pole, "CrossArm", new Vector3(0f, safeHeight - 0.65f, 0f), new Vector3(1.6f, 0.10f, 0.10f), "DarkMetal", false); // 상부 전선 지지대 추가
        g.Box(pole, "ServiceBox", new Vector3(0.34f, 1.65f, 0f), new Vector3(0.55f, 0.78f, 0.34f), "DarkMetal", false); // 보조 전력함 추가
        g.NeonStrip(pole, "PoleId", new Vector3(0f, 2.55f, -0.15f), new Vector3(0.18f, 0.72f, 0.04f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.95f, localPosition.x * 0.02f); // 식별 네온 띠 추가
        if (createLight) // 실제 조명 설치 여부 확인
        {
            g.Box(pole, "LampArm", new Vector3(0.38f, safeHeight - 1.05f, 0f), new Vector3(0.84f, 0.08f, 0.08f), "Steel", false); // 가로등 지지대 추가
            g.Box(pole, "LampHead", new Vector3(0.78f, safeHeight - 1.05f, 0f), new Vector3(0.34f, 0.18f, 0.34f), "Steel", false); // 램프 헤드 추가
            Color lampColor = Color.Lerp(MapCyberpunkGeometry.ColorFor(neon), Color.white, 0.65f); // 흰색이 섞인 도시 조명색 계산
            g.PointLight(pole, "LampLight", new Vector3(0.72f, safeHeight - 1.22f, 0f), lampColor, 12f, 2.2f); // 길거리 주변광 추가
        }
        PoleCount++; // 전봇대 집계 증가
    }

    public void SidewalkStrip(Transform parent, Vector3 localPosition, float width, float depth, int columns, int rows) // 보도 블럭 패턴이 있는 보행 띠 생성
    {
        Transform strip = g.Node(parent, "SidewalkStrip", localPosition); // 보도 블럭 기준 생성
        float safeWidth = Mathf.Max(2.4f, width); // 최소 보도 폭 보정
        float safeDepth = Mathf.Max(1.4f, depth); // 최소 보도 깊이 보정
        g.Box(strip, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(safeWidth, 0.10f, safeDepth), "Steel", true); // 보행 가능한 기초 판 추가
        int columnCount = Mathf.Clamp(columns, 3, 12); // 가로 분할 수 보정
        int rowCount = Mathf.Clamp(rows, 2, 6); // 세로 분할 수 보정
        float stepX = safeWidth / columnCount; // 가로 타일 간격 계산
        float stepZ = safeDepth / rowCount; // 세로 타일 간격 계산
        for (int c = 1; c < columnCount; c++) // 가로 타일 경계 반복
        {
            float x = -safeWidth * 0.5f + c * stepX; // 세로 홈 위치 계산
            g.Box(strip, "GrooveX", new Vector3(x, 0.106f, 0f), new Vector3(0.035f, 0.012f, safeDepth * 0.96f), "DarkMetal", false); // 가로 타일 경계 홈 추가
        }
        for (int r = 1; r < rowCount; r++) // 세로 타일 경계 반복
        {
            float z = -safeDepth * 0.5f + r * stepZ; // 가로 홈 위치 계산
            g.Box(strip, "GrooveZ", new Vector3(0f, 0.106f, z), new Vector3(safeWidth * 0.96f, 0.012f, 0.035f), "DarkMetal", false); // 세로 타일 경계 홈 추가
        }
        SidewalkCount++; // 보도 구간 집계 증가
    }

    public void HangingCable(Transform parent, Vector3 localStart, Vector3 localEnd) // 건물 사이 공중 케이블 생성
    {
        Vector3 sag = Vector3.down * Mathf.Clamp(Vector3.Distance(localStart, localEnd) * 0.07f, 0.8f, 2.8f); // 중앙 처짐 거리 계산
        Vector3 middle = (localStart + localEnd) * 0.5f + sag; // 처진 케이블 중앙점 계산
        g.Cable(parent, "OverheadCableA", localStart, middle, 0.040f); // 첫 구간 케이블 생성
        g.Cable(parent, "OverheadCableB", middle, localEnd, 0.040f); // 둘째 구간 케이블 생성
        CableCount++; // 케이블 묶음 집계 증가
    }

    public void PosterPanel(Transform parent, Vector3 localPosition, string text, string neon, float yaw = 0f) // 벽면 포스터와 생활 문구 생성
    {
        Transform panel = g.Node(parent, "StreetPoster", localPosition); // 포스터 기준 생성
        panel.localRotation = Quaternion.Euler(0f, yaw, 0f); // 벽 방향 적용
        g.Box(panel, "Paper", Vector3.zero, new Vector3(1.9f, 2.6f, 0.035f), "WetConcrete", false); // 포스터 바탕 추가
        g.NeonStrip(panel, "Header", new Vector3(0f, 0.93f, -0.035f), new Vector3(1.5f, 0.10f, 0.03f), neon, MapCyberpunkGeometry.ColorFor(neon), 0.8f, localPosition.z * 0.04f); // 상단 발광 띠 추가
        GameObject label = new GameObject("PosterText"); // 포스터 문자 객체 생성
        label.transform.SetParent(panel, false); // 포스터에 문자 연결
        label.transform.localPosition = new Vector3(0f, 0f, -0.06f); // 바탕보다 앞쪽 배치
        TextMesh mesh = label.AddComponent<TextMesh>(); // 기본 텍스트 메시 추가
        mesh.text = text; // 생활 문구 적용
        mesh.characterSize = 0.18f; // 작은 포스터 문자 크기
        mesh.fontSize = 58; // 선명한 문자 메시 생성
        mesh.anchor = TextAnchor.MiddleCenter; // 중앙 정렬 기준
        mesh.alignment = TextAlignment.Center; // 중앙 문장 정렬
        mesh.color = MapCyberpunkGeometry.ColorFor(neon); // 구역 네온 색상 적용
        StreetPropCount++; // 포스터 집계 증가
    }
}
#endif
