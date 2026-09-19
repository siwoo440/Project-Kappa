using System; // 배열과 예외 처리
using System.Collections.Generic; // 이웃 목록 생성
using UnityEngine; // 보행 좌표 계산

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    public sealed class MapPedestrianGraph // 13x13 교차로 주변 보도·횡단보도 그래프
    {
        public readonly struct Node // 단일 보행 노드
        {
            public readonly Vector3 Position; // 실제 월드 보행 위치
            public readonly int[] Neighbors; // 연결된 다음 노드 목록

            public Node(Vector3 position, int[] neighbors) // 보행 노드 생성
            {
                Position = position; // 월드 위치 저장
                Neighbors = neighbors ?? Array.Empty<int>(); // 이웃 목록 저장
            }
        }

        private readonly Node[] nodes; // 전체 보행 노드
        private readonly float worldSize; // 생성 기준 월드 크기
        public int Count => nodes.Length; // 전체 노드 수 조회
        public float WorldSize => worldSize; // 생성 기준 월드 크기 조회

        private MapPedestrianGraph(float size, Node[] graphNodes) // 완성 그래프 저장
        {
            worldSize = size; // 월드 크기 저장
            nodes = graphNodes; // 노드 배열 저장
        }

        public Node Get(int index) // 안전한 노드 조회
        {
            if (index < 0 || index >= nodes.Length) // 유효 인덱스 확인
            {
                throw new ArgumentOutOfRangeException(nameof(index)); // 잘못된 노드 번호 보고
            }
            return nodes[index]; // 요청 노드 반환
        }

        public int[] Neighbors(int index) // 노드 이웃 목록 조회
        {
            return Get(index).Neighbors; // 저장된 이웃 배열 반환
        }

        public bool IsCrosswalk(int from, int to) // 같은 교차로 안에서 도로를 건너는 연결인지 확인
        {
            if (from < 0 || from >= Count || to < 0 || to >= Count) // 노드 번호 확인
            {
                return false; // 잘못된 연결 제외
            }
            int intersectionA = from / 4; // 시작 교차로 번호 계산
            int intersectionB = to / 4; // 도착 교차로 번호 계산
            return intersectionA == intersectionB && from != to; // 같은 교차로의 다른 모서리면 횡단보도
        }

        public static MapPedestrianGraph Build(float worldSize, float y) // Day16 도로 구조에서 보행 그래프 생성
        {
            int count = MapTrafficMath.RoadCount * MapTrafficMath.RoadCount * 4; // 교차로마다 네 모서리 노드 계산
            Vector3[] positions = new Vector3[count]; // 노드 위치 준비
            List<int>[] links = new List<int>[count]; // 가변 이웃 목록 준비
            float offset = MapTrafficMath.SidewalkOffset(worldSize); // 도로 밖 보도 거리 조회
            for (int z = 0; z < MapTrafficMath.RoadCount; z++) // 세로 교차로 순회
            {
                for (int x = 0; x < MapTrafficMath.RoadCount; x++) // 가로 교차로 순회
                {
                    float roadX = MapTrafficMath.RoadCoordinate(x, worldSize); // 교차로 가로 월드 좌표
                    float roadZ = MapTrafficMath.RoadCoordinate(z, worldSize); // 교차로 세로 월드 좌표
                    SetCorner(positions, links, x, z, 0, new Vector3(roadX - offset, y, roadZ + offset)); // 북서 보도 모서리
                    SetCorner(positions, links, x, z, 1, new Vector3(roadX + offset, y, roadZ + offset)); // 북동 보도 모서리
                    SetCorner(positions, links, x, z, 2, new Vector3(roadX + offset, y, roadZ - offset)); // 남동 보도 모서리
                    SetCorner(positions, links, x, z, 3, new Vector3(roadX - offset, y, roadZ - offset)); // 남서 보도 모서리
                }
            }
            for (int z = 0; z < MapTrafficMath.RoadCount; z++) // 모든 교차로 연결 순회
            {
                for (int x = 0; x < MapTrafficMath.RoadCount; x++) // 가로 교차로 연결 순회
                {
                    Connect(links, Index(x, z, 0), Index(x, z, 1)); // 북쪽 횡단보도 연결
                    Connect(links, Index(x, z, 3), Index(x, z, 2)); // 남쪽 횡단보도 연결
                    Connect(links, Index(x, z, 0), Index(x, z, 3)); // 서쪽 횡단보도 연결
                    Connect(links, Index(x, z, 1), Index(x, z, 2)); // 동쪽 횡단보도 연결
                    if (x < MapTrafficMath.RoadCount - 1) // 동쪽 다음 교차로 존재 확인
                    {
                        Connect(links, Index(x, z, 1), Index(x + 1, z, 0)); // 도로 북쪽 보도 연결
                        Connect(links, Index(x, z, 2), Index(x + 1, z, 3)); // 도로 남쪽 보도 연결
                    }
                    if (z < MapTrafficMath.RoadCount - 1) // 북쪽 다음 교차로 존재 확인
                    {
                        Connect(links, Index(x, z, 0), Index(x, z + 1, 3)); // 도로 서쪽 보도 연결
                        Connect(links, Index(x, z, 1), Index(x, z + 1, 2)); // 도로 동쪽 보도 연결
                    }
                }
            }
            Node[] nodes = new Node[count]; // 최종 고정 노드 배열 준비
            for (int i = 0; i < count; i++) // 전체 노드 고정 배열 변환
            {
                nodes[i] = new Node(positions[i], links[i].ToArray()); // 위치와 이웃 저장
            }
            return new MapPedestrianGraph(worldSize, nodes); // 완성 그래프 반환
        }

        private static void SetCorner(Vector3[] positions, List<int>[] links, int x, int z, int corner, Vector3 position) // 보도 모서리 자료 초기화
        {
            int index = Index(x, z, corner); // 노드 고유 번호 계산
            positions[index] = position; // 월드 위치 저장
            links[index] = new List<int>(4); // 최대 네 방향 이웃 목록 준비
        }

        private static int Index(int x, int z, int corner) // 교차로 모서리 고유 번호 계산
        {
            return (z * MapTrafficMath.RoadCount + x) * 4 + corner; // 교차로 번호 뒤 네 모서리 배치
        }

        private static void Connect(List<int>[] links, int a, int b) // 양방향 보행 연결 생성
        {
            if (!links[a].Contains(b)) // 첫 방향 중복 확인
            {
                links[a].Add(b); // 첫 노드에 둘째 노드 연결
            }
            if (!links[b].Contains(a)) // 반대 방향 중복 확인
            {
                links[b].Add(a); // 둘째 노드에 첫 노드 연결
            }
        }
    }
}
