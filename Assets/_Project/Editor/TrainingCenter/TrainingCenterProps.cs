#if UNITY_EDITOR // 시설 세부 모형 편집 전용
using UnityEditor; // 기존 무기 프리팹 표시
using UnityEngine; // 조립 부품 기능
using G = TrainingCenterGeometry; // 공용 부품 규격

public static class TrainingCenterProps // 용도를 구분할 수 있는 시설 모형
{
    public static Transform Console(Transform parent, Vector3 point, Material accent, string sign, float yaw = 0f) // 경사진 조작 단말기
    {
        Transform root = G.Node(parent, "Console_" + sign, point); // 상호작용 루트
        root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 사용자 접근 방향
        G.Box(root, "Foot", new Vector3(0f, 0.10f, 0f), new Vector3(1.30f, 0.20f, 0.90f), G.Steel, true); // 바닥 고정 베이스
        G.Box(root, "Cabinet", new Vector3(0f, 0.72f, 0f), new Vector3(1.05f, 1.24f, 0.58f), G.LightSteel, true); // F 판정과 몸체 충돌
        G.Box(root, "ServiceDoor", new Vector3(0f, 0.64f, -0.30f), new Vector3(0.78f, 0.72f, 0.035f), G.Steel); // 정비 덮개
        G.Box(root, "StatusBar", new Vector3(0f, 0.97f, -0.329f), new Vector3(0.60f, 0.055f, 0.025f), accent); // 동작 가능한 장비 표시
        Transform panel = G.Node(root, "Panel", new Vector3(0f, 1.30f, -0.13f)); // 화면 기준점
        panel.localRotation = Quaternion.Euler(18f, 0f, 0f); // 읽기 쉬운 화면 기울기
        G.Box(panel, "Bezel", Vector3.zero, new Vector3(1.2f, 0.62f, 0.18f), G.Dark); // 화면 보호 프레임
        G.Sign(panel, sign, new Vector3(0f, 0f, -0.102f), 1.09f, 0.42f); // 짧은 한글 기능 안내
        for (int i = 0; i < 3; i++) // 물리 버튼 모형
        {
            G.Box(root, "Key", new Vector3(-0.27f + i * 0.27f, 1.02f, -0.38f), new Vector3(0.16f, 0.055f, 0.10f), i == 2 ? accent : G.Dark); // 기능과 상태 버튼
        }
        G.Beam(root, "Power", new Vector3(0.36f, 0.7f, 0.25f), new Vector3(0.45f, 0.08f, 0.35f), 0.028f, G.Dark); // 짧은 전원 연결
        return root; // 실제 기능 컴포넌트 연결용
    }

    public static void DoorFrame(Transform parent, Vector3 point, float width, Material accent, string sign, float yaw = 0f) // 이동 가능한 개방 출입구
    {
        Transform root = G.Node(parent, "Doorway_" + sign, point); // 개방 문틀 기준
        root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 통로 방향 정렬
        for (int side = -1; side <= 1; side += 2) // 문 좌우 구조
        {
            G.Box(root, "Pillar", new Vector3(side * width * 0.5f, 2.7f, 0f), new Vector3(0.55f, 5.4f, 0.8f), G.Steel, true); // 카메라 여유가 있는 기둥
            G.Box(root, "Guide", new Vector3(side * (width * 0.5f - 0.30f), 2f, -0.41f), new Vector3(0.08f, 3.5f, 0.02f), accent); // 통로 가장자리 강조
        }
        G.Box(root, "MotorHousing", new Vector3(0f, 5.2f, 0f), new Vector3(width + 1f, 0.9f, 1.0f), G.LightSteel, true); // 상부 구동함 모형
        G.Sign(root, sign, new Vector3(0f, 4.95f, -0.56f), width - 0.25f, 0.85f); // 머리와 사격선을 피한 안내
        G.Stripe(root, "Threshold", Vector3.zero, new Vector3(width - 0.6f, 0f, 0.22f), accent); // 걸리지 않는 바닥 경계
    }

    public static void Bench(Transform parent, Vector3 point, float yaw = 0f) // 산업시설 휴게 벤치
    {
        Transform root = G.Node(parent, "Bench", point); // 벤치 기준
        root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 착석 방향 표시
        G.Box(root, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(2.4f, 0.12f, 0.65f), G.LightSteel, true); // 공용 좌석
        G.Box(root, "Back", new Vector3(0f, 0.87f, 0.32f), new Vector3(2.4f, 0.58f, 0.10f), G.Steel); // 등받이
        for (int side = -1; side <= 1; side += 2) // 양쪽 다리
        {
            G.Box(root, "Leg", new Vector3(side * 0.85f, 0.24f, 0f), new Vector3(0.16f, 0.48f, 0.5f), G.Steel); // 금속 받침
        }
    }

    public static void Crate(Transform parent, Vector3 point, Material accent, bool tall = false) // 표준 엄폐와 보급 상자
    {
        float h = tall ? 1.8f : 0.95f; // 서로 구분되는 엄폐 높이
        Transform root = G.Node(parent, tall ? "HighCover" : "SupplyCrate", point); // 기능별 모형 이름
        G.Box(root, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(2.2f, h, 1.2f), G.Steel, true); // 단순한 실제 충돌 형태
        G.Box(root, "Lid", new Vector3(0f, h + 0.035f, 0f), new Vector3(2.26f, 0.07f, 1.26f), G.LightSteel); // 분리된 뚜껑
        for (int side = -1; side <= 1; side += 2) // 모서리 보강
        {
            G.Box(root, "Strap", new Vector3(side * 0.73f, h * 0.5f, -0.614f), new Vector3(0.12f, h, 0.026f), G.Dark); // 겹침 없는 잠금띠
            G.Box(root, "Latch", new Vector3(side * 0.73f, h - 0.13f, -0.636f), new Vector3(0.19f, 0.22f, 0.04f), accent); // 잠금 장치 표시
        }
        G.Box(root, "Handle", new Vector3(0f, h * 0.64f, -0.647f), new Vector3(0.52f, 0.10f, 0.07f), G.LightSteel); // 정면 손잡이
    }

    public static void Locker(Transform parent, Vector3 point) // 장식용 장비 보관함
    {
        Transform root = G.Node(parent, "Locker_Decoration", point); // 실제 상호작용과 이름 구분
        G.Box(root, "Frame", new Vector3(0f, 1.2f, 0f), new Vector3(0.95f, 2.4f, 0.65f), G.Steel, true); // 큰 몸체 충돌
        G.Box(root, "Door", new Vector3(0f, 1.2f, -0.336f), new Vector3(0.80f, 2.20f, 0.035f), G.LightSteel); // 외장 패널
        for (int i = 0; i < 4; i++) // 위쪽 환기 홈
        {
            G.Box(root, "Vent", new Vector3(0f, 1.85f + i * 0.09f, -0.359f), new Vector3(0.52f, 0.032f, 0.012f), G.Dark); // 낮은 비용의 표면 디테일
        }
        G.Box(root, "Handle", new Vector3(0.24f, 1.18f, -0.40f), new Vector3(0.06f, 0.31f, 0.075f), G.Dark); // 문 손잡이
    }

    public static void HVAC(Transform parent, Vector3 point, float yaw = 0f) // 공장 환기와 냉각 모형
    {
        Transform root = G.Node(parent, "HVAC_Decoration", point); // 고정 설비 구분
        root.localRotation = Quaternion.Euler(0f, yaw, 0f); // 설비 방향
        G.Box(root, "Unit", new Vector3(0f, 0.65f, 0f), new Vector3(2.2f, 1.3f, 1.1f), G.LightSteel, true); // 실외기 몸체
        GameObject fan = G.Part(root, "FanRecess", PrimitiveType.Cylinder, new Vector3(-0.36f, 0.70f, -0.572f), new Vector3(0.92f, 0.025f, 0.92f), G.Dark); // 둥근 팬 홈
        fan.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 정면 팬 방향
        for (int i = 0; i < 5; i++) // 정면 팬 보호격자
        {
            G.Box(root, "FanGuard", new Vector3(-0.36f, 0.40f + i * 0.14f, -0.608f), new Vector3(0.78f, 0.025f, 0.026f), G.Steel); // 얇은 보호대
            G.Box(root, "VentSlat", new Vector3(0.64f, 0.40f + i * 0.14f, -0.566f), new Vector3(0.57f, 0.035f, 0.016f), G.Dark); // 열 배출 홈
        }
        G.Beam(root, "Pipe", new Vector3(1f, 0.4f, 0.3f), new Vector3(1.25f, 0.05f, 0.45f), 0.07f, G.Dark); // 하부 배관 연결
    }

    public static void WeaponRack(Transform parent, Vector3 point, FirearmDefinition weapon, string sign) // 실제 사용 가능한 총기 진열
    {
        Transform rack = G.Node(parent, "Rack_" + weapon.name, point); // 무기별 진열 구획
        G.Box(rack, "Backboard", new Vector3(0f, 1.5f, 0f), new Vector3(2.8f, 2.5f, 0.3f), G.Dark, true); // 진열 패널
        G.Box(rack, "Pedestal", new Vector3(0f, 0.25f, -0.3f), new Vector3(3f, 0.5f, 1f), G.Steel, true); // 하단 고정 받침
        for (int i = 0; i < 3; i++) // 수평 거치 레일
        {
            G.Box(rack, "Rail", new Vector3(0f, 0.9f + i * 0.50f, -0.18f), new Vector3(2.5f, 0.05f, 0.045f), G.LightSteel); // 모형을 받치는 레일
        }
        G.Sign(rack, sign, new Vector3(0f, 2.40f, -0.195f), 2.55f, 0.55f); // 무기 이름과 슬롯 번호
        GameObject model = UnityEngine.Object.Instantiate(weapon.ModelPrefab, rack); // 기존 무기 에셋 복제
        model.name = "DisplayOnly_" + weapon.name; // 장착 객체와 구분
        model.SetActive(false); // 기능 제거 중 실행 차단
        model.transform.localPosition = new Vector3(0f, 1.40f, -0.35f); // 거치대 중앙 배치
        model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 옆모습으로 종류 구분
        foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) // 진열물의 게임 기능 제외
        {
            if (script != null) // 유효한 스크립트 확인
            {
                script.enabled = false; // 발사와 효과음과 반동 실행 방지
            }
        }
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) // 진열 모형의 충돌 정리
        {
            collider.enabled = false; // 받침대만 충돌에 사용
        }
        model.SetActive(true); // 렌더링만 활성화
    }
}
#endif
