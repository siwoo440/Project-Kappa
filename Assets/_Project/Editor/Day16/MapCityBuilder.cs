#if UNITY_EDITOR // 연무 도시 외부 공간 제작
using System; // 재현 가능한 건물 배치
using System.Collections.Generic; // 주요 장소 목록
using UnityEditor; // 진행 상태와 취소 처리
using UnityEngine; // 도로와 건물과 옥상 연결

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public sealed class MapCityBuilder // 미션 없이 탐험하는 다섯 장소의 도시
    {
        private readonly MapGeometry g; // 구조물 생성 도구
        private readonly Transform root; // 도시 배치 원점
        private readonly MapWorldRoot world; // 본편 월드 관리자
        private readonly float pitch; // 도로 사이 블록 길이
        private readonly float minimum; // 도시 남서쪽 도로 위치
        private readonly float roadWidth; // 교차로 폭
        private readonly float footLift; // 실제 플레이어 피벗 높이 보정
        private readonly System.Random random; // 시드가 고정된 배치 변화
        private readonly List<MapPoint> places = new List<MapPoint>(); // 방문 지점 순서
        private const float Deck = MapTerrainMath.Ground + 0.25f; // 인도 보행면
        private int buildingCount; // 생성한 일반 건물 수
        public int BuildingCount => buildingCount; // 일반 건물 집계 조회

        public MapCityBuilder(MapGeometry geometry, MapWorldRoot owner, float pivotLift, int seed) // 도시 구획 준비
        {
            g = geometry; // 에셋 도구 연결
            world = owner; // 월드 관리 연결
            root = g.Node(owner.transform, "City_Exterior", Vector3.zero); // 실제 도시 부모
            pitch = geometry.WorldSize * 0.84f / 12f; // 전체 도로 격자 간격
            minimum = -geometry.WorldSize * 0.42f; // 외곽 구릉 안쪽 평지
            roadWidth = Mathf.Clamp(pitch * 0.13f, 7f, 14f); // 타일 크기에 맞춘 통행 폭
            footLift = pivotLift; // 플레이어 발 위치 기준
            random = new System.Random(seed); // 재현 가능한 도시 외형
        }

        private Vector3 Cell(int x, int z) // 블록 중심 위치
        {
            return new Vector3(minimum + (x + 0.5f) * pitch, Deck, minimum + (z + 0.5f) * pitch); // 실제 보행면 위 좌표
        }

        private bool Reserved(int x, int z) // 랜드마크 부지 확인
        {
            return (x == 6 && z == 2) || (x == 6 && z == 5) || (x == 1 && z == 2) || (x == 9 && z == 6) || (x == 6 && z == 10); // 다섯 장소 전용 공간
        }

        public MapPoint[] Build() // 전체 도로와 도시 외형 생성
        {
            BuildRoads(); // 타일 경계를 넘는 보행과 도로 연결
            Transform blocks = g.Node(root, "Blocks_12x12", Vector3.zero); // 편집하기 쉬운 블록 목록
            for (int z = 0; z < 12; z++) // 도시 남쪽에서 북쪽으로 배치
            {
                if (EditorUtility.DisplayCancelableProgressBar("Map / City", "도로와 도시 블록 " + (z + 1) + " / 12", 0.45f + z * 0.025f)) // 긴 배치 취소 확인
                {
                    throw new OperationCanceledException("도시 생성 취소"); // 기존 씬을 보존한 정리 경로
                }
                for (int x = 0; x < 12; x++) // 서쪽에서 동쪽으로 블록 배치
                {
                    Vector3 point = Cell(x, z); // 현재 블록 중심
                    Transform block = g.Node(blocks, "Block_" + x.ToString("D2") + "_" + z.ToString("D2"), point); // 이후 미션 배치 기준이 되는 블록
                    g.Box(block, "PavedLot", new Vector3(0f, -0.125f, 0f), new Vector3(pitch - roadWidth, 0.25f, pitch - roadWidth), "Concrete"); // 도로보다 조금 높은 인도와 부지
                    if (Reserved(x, z)) // 주요 장소의 부지 보존
                    {
                        continue; // 일반 건물 배치 제외
                    }
                    int style = x >= 8 ? 2 : x <= 3 || z <= 1 ? 0 : 1; // 산업과 시장과 기업 외장 구분
                    float width = Mathf.Min(32f, pitch * 0.27f); // 블록 사이 통행 간격 확보
                    float depth = Mathf.Min(33f, pitch * 0.40f); // 뒤편 골목 공간 확보
                    for (int side = -1; side <= 1; side += 2) // 중앙 보행 골목 양옆 건물
                    {
                        float height = style == 2 ? 26f + random.Next(0, 10) * 4f : style == 0 ? 7f + random.Next(0, 4) * 3f : 8f + random.Next(0, 5) * 3f; // 구역별 실루엣 차이
                        Building(block, "Building_" + (side < 0 ? "West" : "East"), new Vector3(side * pitch * 0.23f, 0f, 0f), width, depth, height, style); // 충돌과 지붕이 있는 건물
                        buildingCount++; // 배치 결과 집계
                    }
                    if ((x + z) % 3 == 0) // 일부 블록의 정비 소품
                    {
                        Utility(block, new Vector3(0f, 0f, pitch * 0.27f), style); // 중앙 길을 막지 않는 부속 설비
                    }
                }
            }
            places.Add(BuildLin()); // 최초 스폰은 항상 린의 옥상
            places.Add(BuildMarket()); // 시장과 보행로 중심
            places.Add(BuildIndustry()); // 산업 지역의 큰 설비
            places.Add(BuildCorporate()); // 기업 외벽과 공개 광장
            places.Add(BuildSpire()); // 멀리서 읽히는 도시 랜드마크
            ConnectSkywalk(); // 옥상과 시장 사이 대체 이동 경로
            BuildTravel(); // 직접 보행 외 선택 가능한 이동 단말기
            BuildBoundary(); // 외곽 밖 무한 낙하 방지
            return places.ToArray(); // 실제 도착점 목록
        }

        private void BuildRoads() // 겹친 면이 없는 교차로 격자
        {
            Transform roads = g.Node(root, "RoadNetwork", Vector3.zero); // 지면 통행망
            float span = pitch * 12f; // 평지 도시의 전체 길이
            for (int i = 0; i <= 12; i++) // 동서 방향 도로
            {
                float z = minimum + i * pitch; // 도로 중심
                g.Box(roads, "EastWest_" + i, new Vector3(0f, MapTerrainMath.Ground + 0.08f, z), new Vector3(span + roadWidth, 0.16f, roadWidth), "Asphalt"); // 교차로를 포함한 가로 도로
                for (int j = 0; j <= 12; j++) // 남북 방향 도로 열
                {
                    float x = minimum + j * pitch; // 도로 세로축
                    if (i < 12) // 가로 도로 사이 구간만 생성
                    {
                        g.Box(roads, "NorthSouth_" + j + "_" + i, new Vector3(x, MapTerrainMath.Ground + 0.08f, z + pitch * 0.5f), new Vector3(roadWidth, 0.16f, pitch - roadWidth), "Asphalt"); // 교차점 중복 면 제거
                        for (float d = roadWidth; d < pitch - roadWidth; d += 12f) // 도로 중심 차선
                        {
                            g.Detail(roads, new Vector3(x, MapTerrainMath.Ground + 0.173f, z + d), new Vector3(0.14f, 0.012f, 4f), "White"); // 실제 도로면 위 끊어진 선
                        }
                    }
                    if (i < 12 && j < 12 && (i + j) % 2 == 0) // 교차로 일부의 보행 표시
                    {
                        for (int stripe = -2; stripe <= 2; stripe++) // 횡단보도 무늬
                        {
                            g.Detail(roads, new Vector3(x + stripe * 0.85f, MapTerrainMath.Ground + 0.174f, z + roadWidth * 0.42f), new Vector3(0.48f, 0.014f, roadWidth * 0.35f), "White"); // 얇고 충돌 없는 표시
                        }
                        Lamp(roads, new Vector3(x + roadWidth * 0.5f + 1f, Deck, z + roadWidth * 0.5f + 1f)); // 모서리의 거리 조명
                    }
                }
                for (float x = minimum + roadWidth; x < -minimum; x += 12f) // 가로 도로 중앙 선
                {
                    g.Detail(roads, new Vector3(x, MapTerrainMath.Ground + 0.174f, z), new Vector3(4f, 0.014f, 0.14f), "Amber"); // 진행축 안내
                }
            }
        }

        private void Lamp(Transform parent, Vector3 point) // 실제 광원 수를 늘리지 않는 가로등 모형
        {
            Transform lamp = g.Node(parent, "StreetLamp", point); // 인도 위 기준
            g.Box(lamp, "Pole", new Vector3(0f, 2.7f, 0f), new Vector3(0.18f, 5.4f, 0.18f), "Steel"); // 충돌이 있는 얇은 지주
            g.Detail(lamp, new Vector3(0.7f, 5.3f, 0f), new Vector3(1.6f, 0.14f, 0.22f), "Steel"); // 꺾인 조명 팔
            g.Detail(lamp, new Vector3(1.25f, 5.22f, 0f), new Vector3(0.65f, 0.06f, 0.35f), "Cyan"); // 발광 표면만 사용
        }

        private Transform Building(Transform parent, string name, Vector3 point, float width, float depth, float height, int style) // 구역별 건물 외부 모형
        {
            Transform building = g.Node(parent, name, point); // 안정적인 건물 기준점
            string wall = style == 2 ? "Pale" : style == 0 ? "Rust" : "Concrete"; // 지역 외장 선택
            g.Box(building, "MainCollision", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), wall, true, true); // 외벽과 옥상 충돌
            g.Detail(building, new Vector3(0f, height + 0.05f, 0f), new Vector3(width + 0.16f, 0.10f, depth + 0.16f), "Steel"); // 지붕 가장자리 테두리
            int levels = Mathf.Clamp(Mathf.FloorToInt(height / 3.5f), 1, 14); // 적절한 층 표현 수
            for (int level = 0; level < levels; level++) // 창문과 층 이음매
            {
                float y = 2.3f + level * (height - 2.8f) / Mathf.Max(1, levels - 1); // 문 위부터 고른 창 배치
                for (int side = -1; side <= 1; side += 2) // 정면과 후면 표현
                {
                    if (style == 2) // 기업 건물의 연속 창면
                    {
                        g.Detail(building, new Vector3(0f, y, side * (depth * 0.5f + 0.025f)), new Vector3(width * 0.89f, 1.6f, 0.04f), "Glass"); // 긴 유리 띠
                    }
                    else // 시장과 산업 외벽의 분리 창문
                    {
                        for (int column = -1; column <= 1; column++) // 세 칸 창문
                        {
                            g.Detail(building, new Vector3(column * width * 0.27f, y, side * (depth * 0.5f + 0.025f)), new Vector3(width * 0.18f, 1.25f, 0.04f), column == 0 && level % 3 == 0 ? "Amber" : "Glass"); // 창과 실내빛 구분
                        }
                    }
                }
                g.Detail(building, new Vector3(-width * 0.5f - 0.025f, y, 0f), new Vector3(0.04f, 1.4f, depth * 0.72f), "Glass"); // 서쪽 외벽 창면
                g.Detail(building, new Vector3(width * 0.5f + 0.025f, y, 0f), new Vector3(0.04f, 1.4f, depth * 0.72f), "Glass"); // 동쪽 외벽 창면
            }
            g.Detail(building, new Vector3(0f, 1.25f, -depth * 0.5f - 0.035f), new Vector3(1.8f, 2.5f, 0.05f), "Dark"); // 이번 단계에서 닫힌 외부 출입문
            g.Detail(building, new Vector3(0f, 2.7f, -depth * 0.5f - 0.6f), new Vector3(3.3f, 0.14f, 1.4f), style == 1 ? "Magenta" : "Steel"); // 출입구 차양
            g.Detail(building, new Vector3(width * 0.31f, height * 0.5f, -depth * 0.5f - 0.2f), new Vector3(0.20f, height * 0.5f, 0.20f), "Steel", PrimitiveType.Cylinder); // 외벽 배수관
            g.Box(building, "RoofPlant", new Vector3(-width * 0.22f, height + 0.6f, depth * 0.22f), new Vector3(3.3f, 1.2f, 2.4f), "Steel", true, true); // 보이는 모양과 같은 실외기 충돌
            for (int vent = 0; vent < 4; vent++) // 지붕 설비 통풍 홈
            {
                g.Detail(building, new Vector3(-width * 0.22f - 1f + vent * 0.65f, height + 1.22f, depth * 0.22f), new Vector3(0.24f, 0.03f, 1.9f), "Dark"); // 반복 통풍 슬롯
            }
            return building; // 랜드마크 외형 보강용 반환
        }

        private void Utility(Transform parent, Vector3 point, int style) // 블록별 세부 시설
        {
            Transform utility = g.Node(parent, "ServiceFurniture", point); // 골목 바깥 정비 영역
            g.Box(utility, "PowerCabinet", new Vector3(-3f, 0.8f, 0f), new Vector3(1.6f, 1.6f, 0.8f), "Steel"); // 전기함 모형
            g.Detail(utility, new Vector3(-3f, 1.25f, -0.42f), new Vector3(1.2f, 0.10f, 0.03f), "Amber"); // 전기함 상태 띠
            g.Box(utility, "BenchSeat", new Vector3(3f, 0.45f, 0f), new Vector3(3f, 0.18f, 0.75f), "Steel"); // 실제 앉기 높이의 장식 벤치
            for (int side = -1; side <= 1; side += 2) // 벤치 지지 구조
            {
                g.Detail(utility, new Vector3(3f + side, 0.20f, 0f), new Vector3(0.16f, 0.4f, 0.55f), "Dark"); // 작은 지지대
            }
            if (style == 0) // 산업 구역에만 보급 적재물 배치
            {
                g.Box(utility, "CargoCase", new Vector3(6f, 0.75f, 1f), new Vector3(2.2f, 1.5f, 1.4f), "Rust", true, true); // 우회 가능한 적재 상자
                g.Detail(utility, new Vector3(6f, 1.53f, 1f), new Vector3(2.3f, 0.06f, 1.5f), "Steel"); // 상자 분리 뚜껑
            }
        }

        private MapPoint Place(string id, string label, Transform site, Vector3 localArrival) // 방문 기준점과 안전한 피벗 위치
        {
            Transform arrival = g.Node(site, "Arrival", localArrival + Vector3.up * footLift); // 발 기준 높이에 플레이어 보정 추가
            MapPoint place = site.gameObject.AddComponent<MapPoint>(); // 미션과 분리된 장소 ID
            place.Configure(id, label, arrival); // 일관된 참조 연결
            return place; // 방문 목록에 추가
        }

        private MapPoint BuildLin() // 시작 거점과 옥상 접근
        {
            Transform site = g.Node(root, "LIN_RooftopWorkshop", Cell(6, 2)); // 안전한 남쪽 거점 부지
            RoofDeck(site, "WorkshopFoundation"); // 계단 대신 끊김 없는 옥상 경사로
            g.Box(site, "WorkshopRoom", new Vector3(-6f, 9.65f, 4.5f), new Vector3(8f, 3.3f, 7f), "Concrete", true, true); // 로비가 아닌 본편 옥상 작업실 외형
            g.Detail(site, new Vector3(-6f, 10.0f, 0.97f), new Vector3(5.8f, 1.3f, 0.05f), "Glass"); // 작업실 전면 창문
            g.Box(site, "RepairBench", new Vector3(-6f, 8.8f, -0.8f), new Vector3(4.2f, 1.6f, 1.2f), "Steel"); // 옥상 정비 작업대
            g.Sign(site, "lin", new Vector3(-6f, 12.1f, 0.5f), 7f, 2.4f); // 거점 이름 안내
            g.Sign(site, "overview", new Vector3(6.5f, 10.4f, 8f), 7f, 4f); // 도시 방문 방향 안내
            return Place("LIN_HOME", "린의 옥상 작업실", site, new Vector3(0f, 8f, -3f)); // 건물과 난간에서 떨어진 스폰
        }

        private void RoofDeck(Transform site, string name) // 두 랜드마크의 연결 가능한 8미터 옥상
        {
            g.Box(site, name, new Vector3(0f, 4f, 0f), new Vector3(26f, 8f, 26f), "Concrete", true, true); // 실제 올라설 수 있는 전체 건물
            g.Ramp(site, "PublicRoofRamp", new Vector3(16f, 0f, -2f), 5f, 24f, 8f); // 보행만으로 접근 가능한 경사면
            g.Box(site, "RampLanding", new Vector3(15.75f, 7.8f, 11.5f), new Vector3(5.5f, 0.4f, 3f), "Concrete"); // 지붕 옆 도착 발판
            g.Box(site, "WestRailing", new Vector3(-12.8f, 8.6f, 0f), new Vector3(0.16f, 1.2f, 25f), "Steel"); // 서쪽 추락 방지
            g.Box(site, "SouthRailing", new Vector3(0f, 8.6f, -12.8f), new Vector3(25f, 1.2f, 0.16f), "Steel"); // 남쪽 추락 방지
            g.Box(site, "EastRailing", new Vector3(12.8f, 8.6f, -3f), new Vector3(0.16f, 1.2f, 18f), "Steel"); // 경사로 진입 공간은 개방
            for (int side = -1; side <= 1; side += 2) // 북쪽 연결 통로 양쪽 난간
            {
                g.Box(site, "NorthRailing", new Vector3(side * 8f, 8.6f, 12.8f), new Vector3(9.5f, 1.2f, 0.16f), "Steel"); // 중앙 보행교 출구 확보
            }
            g.Detail(site, new Vector3(0f, 8.025f, 0f), new Vector3(0.12f, 0.02f, 23f), "Cyan"); // 옥상 진행 방향 표시
        }

        private MapPoint BuildMarket() // 중앙 시장과 공중 보행로
        {
            Transform site = g.Node(root, "GYEOPGIL_Market", Cell(6, 5)); // 도시 중심의 밀집 시장
            RoofDeck(site, "MarketGallery"); // 지상과 옥상을 잇는 시장 건물
            g.Sign(site, "gyeopgil", new Vector3(0f, 5.5f, -13.2f), 13f, 3.4f); // 멀리서 읽히는 지역 이름
            for (int i = 0; i < 3; i++) // 옥상 시장 가판대
            {
                float x = -8f + i * 8f; // 중앙 통로를 남긴 좌우 배치
                g.Box(site, "StallCounter", new Vector3(x, 8.55f, 5f), new Vector3(4.5f, 1.1f, 1.3f), "Steel"); // 닫힌 상점 가판대
                g.Detail(site, new Vector3(x, 10.7f, 5.3f), new Vector3(5.2f, 0.16f, 2.8f), i % 2 == 0 ? "Magenta" : "Amber"); // 시장의 색상 차양
                for (int side = -1; side <= 1; side += 2) // 가판대 지지 기둥
                {
                    g.Detail(site, new Vector3(x + side * 2.1f, 9.4f, 5.8f), new Vector3(0.10f, 2.8f, 0.10f), "Steel"); // 날씬한 차양 지주
                }
            }
            return Place("GYEOPGIL_PLAZA", "겹길 시장", site, new Vector3(0f, 0f, -pitch * 0.35f)); // 지상 광장의 도착점
        }

        private MapPoint BuildIndustry() // 저류의 공장과 수직 배관
        {
            Transform site = g.Node(root, "JEORYU_Industrial", Cell(1, 2)); // 서남쪽 산업 구역
            float width = Mathf.Min(30f, pitch * 0.40f); // 작은 타일에서도 부지 안쪽 배치
            Building(site, "PumpHouse", new Vector3(-width * 0.45f, 0f, 4f), width, width * 0.8f, 10f, 0); // 낮은 펌프 공장
            for (int i = 0; i < 3; i++) // 저장 탱크의 반복 실루엣
            {
                Transform tank = g.Node(site, "StorageTower_" + i, new Vector3(width * 0.65f, 0f, -8f + i * 8f)); // 공장 옆 탱크 위치
                g.Box(tank, "TankCollision", new Vector3(0f, 7f, 0f), new Vector3(5f, 14f, 5f), "Steel", true, true); // 단순하고 일관된 충돌 외장
                g.Detail(tank, new Vector3(0f, 7f, 0f), new Vector3(5.1f, 7f, 5.1f), "Rust", PrimitiveType.Cylinder); // 원통형 보강 외형
                for (int band = 0; band < 4; band++) // 탱크 고정 띠
                {
                    g.Detail(tank, new Vector3(0f, 2f + band * 3f, 0f), new Vector3(5.3f, 0.13f, 5.3f), "Steel", PrimitiveType.Cylinder); // 층별 연결 띠
                }
            }
            g.Box(site, "ServiceBridge", new Vector3(0f, 10.2f, 16f), new Vector3(width * 1.6f, 0.4f, 3f), "Steel", true, true); // 산업 구역 상부 연결 프레임
            g.Sign(site, "jeoryu", new Vector3(0f, 4.2f, -pitch * 0.28f), 11f, 3.2f); // 지역 안내
            return Place("JEORYU_GATE", "저류 산업지구", site, new Vector3(0f, 0f, -pitch * 0.35f)); // 탱크와 떨어진 도착점
        }

        private MapPoint BuildCorporate() // 유리관 공개 광장과 수직 랜드마크
        {
            Transform site = g.Node(root, "YURIGWAN_Corporate", Cell(9, 6)); // 동쪽 기업 구역
            float width = Mathf.Min(28f, pitch * 0.32f); // 건물 주변 보행 공간 유지
            Building(site, "ArchiveTower", new Vector3(-width * 0.55f, 0f, 5f), width, width, 76f, 2); // 기록실 타워 외부만 표현
            Building(site, "OfficeTower", new Vector3(width * 0.68f, 0f, 7f), width * 0.75f, width * 0.75f, 52f, 2); // 높이가 다른 부속 타워
            g.Box(site, "SkyConnector", new Vector3(0f, 30f, 6f), new Vector3(width * 1.5f, 3f, 4f), "Glass", true, true); // 기업 구역 공중 연결로 외형
            g.Sign(site, "yurigwan", new Vector3(0f, 5.2f, -pitch * 0.28f), 13f, 3.7f); // 차가운 색상 지역 표지
            return Place("YURIGWAN_PLAZA", "유리관 기업지구", site, new Vector3(0f, 0f, -pitch * 0.35f)); // 두 건물 앞 공개 광장
        }

        private MapPoint BuildSpire() // 지도 없이도 보이는 최종 구역 외부 랜드마크
        {
            Transform site = g.Node(root, "CHEOMTAP_Spire", Cell(6, 10)); // 북쪽 중심의 첨탑 부지
            float width = Mathf.Min(35f, pitch * 0.44f); // 타일 크기별 광장 여유
            g.Box(site, "TowerLower", new Vector3(0f, 21f, 3f), new Vector3(width, 42f, width), "Pale", true, true); // 하층 타워
            g.Box(site, "TowerMiddle", new Vector3(0f, 67f, 3f), new Vector3(width * 0.65f, 50f, width * 0.65f), "Steel", true, true); // 좁아지는 중층 타워
            g.Box(site, "TowerUpper", new Vector3(0f, 106f, 3f), new Vector3(width * 0.34f, 28f, width * 0.34f), "Glass", true, true); // 상층 신호탑
            g.Box(site, "Antenna", new Vector3(0f, 131f, 3f), new Vector3(1.3f, 22f, 1.3f), "Steel"); // 도시 전체 기준 실루엣
            for (int side = -1; side <= 1; side += 2) // 타워의 수직 안내선
            {
                g.Detail(site, new Vector3(side * width * 0.26f, 67f, 3f - width * 0.326f), new Vector3(0.22f, 48f, 0.06f), "Cyan"); // 제한적인 기업 발광
            }
            g.Sign(site, "cheomtap", new Vector3(0f, 5.5f, -pitch * 0.28f), 13f, 3.8f); // 접근 구역 안내
            return Place("CHEOMTAP_APPROACH", "첨탑 외곽 광장", site, new Vector3(0f, 0f, -pitch * 0.36f)); // 현재는 외부 탐험만 허용
        }

        private void ConnectSkywalk() // 거점과 시장을 연결하는 옥상 대체 경로
        {
            Vector3 home = Cell(6, 2); // 린의 옥상 위치
            Vector3 market = Cell(6, 5); // 겹길 위치
            float start = home.z + 13f; // 거점 지붕 북쪽 끝
            float end = market.z - 13f; // 시장 지붕 남쪽 끝
            float length = end - start; // 건물과 겹치지 않는 연결 길이
            Transform bridge = g.Node(root, "RooftopPublicWalkway", new Vector3(home.x, Deck, (start + end) * 0.5f)); // 월드 기준 공중 보행로
            g.Box(bridge, "WalkDeck", new Vector3(0f, 7.8f, 0f), new Vector3(4.5f, 0.4f, length), "Concrete", true, true); // 걸어서 통과 가능한 상부 경로
            for (int side = -1; side <= 1; side += 2) // 다리 양쪽 난간
            {
                g.Box(bridge, "Rail", new Vector3(side * 2.18f, 8.55f, 0f), new Vector3(0.12f, 1.1f, length), "Steel"); // 기본 이동 중 추락 방지
                g.Detail(bridge, new Vector3(side * 1.8f, 8.025f, 0f), new Vector3(0.08f, 0.02f, length), "Cyan"); // 다음 장소로 이어지는 바닥 안내
            }
            for (float z = -length * 0.5f + 15f; z < length * 0.5f; z += 35f) // 반복 지지 기둥
            {
                float worldZ = bridge.position.z + z; // 실제 도로와의 위치 비교
                float nearestRoad = minimum + Mathf.Round((worldZ - minimum) / pitch) * pitch; // 가장 가까운 교차로 중심
                if (Mathf.Abs(nearestRoad - worldZ) > roadWidth) // 도로 한가운데 지주 배치 제외
                {
                    g.Box(bridge, "SupportPier", new Vector3(2.7f, 3.8f, z), new Vector3(0.55f, 7.6f, 0.55f), "Steel"); // 보행선 옆 받침
                }
            }
            Transform marketSite = places[1].transform; // 시장 지붕의 출입 경계
            Transform southRail = marketSite.Find("SouthRailing"); // 다리와 만나는 남쪽 난간
            if (southRail != null) // 기존 난간 존재 확인
            {
                UnityEngine.Object.DestroyImmediate(southRail.gameObject); // 다리 연결 위치만 개방
                for (int side = -1; side <= 1; side += 2) // 중앙 통행로 양옆 재배치
                {
                    g.Box(marketSite, "SouthEntryRail", new Vector3(side * 8f, 8.6f, -12.8f), new Vector3(9.5f, 1.2f, 0.16f), "Steel"); // 다리 폭만 열린 난간
                }
            }
        }

        private void BuildTravel() // 장소별 안내 단말기
        {
            for (int i = 1; i < places.Count; i++) // 옥상에서 주요 지역 선택
            {
                Console(places[0].transform, new Vector3(-9f + (i - 1) * 6f, 8f, -9.5f), places[i], "visit_" + i); // 거점의 실제 F 이동 장치
                Console(places[i].transform, new Vector3(5f, 0f, -pitch * 0.35f), places[0], "return"); // 각 구역의 거점 복귀 장치
            }
        }

        private void Console(Transform parent, Vector3 position, MapPoint destination, string sign) // 읽을 수 있는 실제 상호작용 장치
        {
            Transform station = g.Node(parent, "Travel_" + destination.PlaceId, position); // 목적지를 포함한 오브젝트 이름
            g.Box(station, "Pedestal", new Vector3(0f, 0.7f, 0f), new Vector3(1.5f, 1.4f, 0.75f), "Steel"); // F키 레이 판정용 실제 본체
            g.Box(station, "ScreenBack", new Vector3(0f, 1.6f, -0.375f), new Vector3(1.7f, 0.95f, 0.10f), "Dark"); // 화면을 바라본 F키도 판정
            g.Sign(station, sign, new Vector3(0f, 1.6f, -0.40f), 1.55f, 0.85f); // 거점과 목적지를 구분하는 한글 화면
            station.gameObject.AddComponent<MapTravelConsole>().Configure(world, destination); // 기존 상호작용 인터페이스 연결
        }

        private void BuildBoundary() // 지형 끝을 넘어가는 이동 제한
        {
            Transform boundary = g.Node(root, "WorldBounds", Vector3.zero); // 외곽 경계 전용
            float half = g.WorldSize * 0.5f; // 아홉 Terrain의 외곽
            for (int side = -1; side <= 1; side += 2) // 양쪽 월드 경계
            {
                GameObject east = g.Node(boundary, "LimitX_" + side, new Vector3(side * (half - 1f), 100f, 0f)).gameObject; // 지형 끝의 얇은 경계
                BoxCollider x = east.AddComponent<BoxCollider>(); // 외곽 낙하 방지 충돌
                x.size = new Vector3(2f, 240f, g.WorldSize); // 높은 구릉까지 포함
                GameObject north = g.Node(boundary, "LimitZ_" + side, new Vector3(0f, 100f, side * (half - 1f))).gameObject; // 다른 축 경계
                BoxCollider z = north.AddComponent<BoxCollider>(); // 끝단 충돌 추가
                z.size = new Vector3(g.WorldSize, 240f, 2f); // 전체 가로 범위
            }
        }
    }
}
#endif
