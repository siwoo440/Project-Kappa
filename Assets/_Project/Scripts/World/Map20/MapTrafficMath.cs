using System; // 경계 계산 오류 처리
using UnityEngine; // 월드 좌표와 방향 계산

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    public static class MapTrafficMath // Day16 도로 구조를 차량 차선으로 변환
    {
        public const int BlockCount = 12; // 도시 블록 가로 세로 수
        public const int RoadCount = 13; // 블록 경계 도로선 수

        public static float RoadMinimum(float worldSize) // 도시 남서 도로 시작 좌표
        {
            return -Mathf.Max(1f, worldSize) * 0.42f; // Day16 도로 생성 규칙 재사용
        }

        public static float RoadPitch(float worldSize) // 교차로 사이 간격
        {
            return Mathf.Max(1f, worldSize) * 0.84f / BlockCount; // 12개 블록 간격 계산
        }

        public static float RoadWidth(float worldSize) // 실제 도로 폭
        {
            return Mathf.Clamp(RoadPitch(worldSize) * 0.13f, 7f, 14f); // Day16 도로 폭 규칙 재사용
        }

        public static float LaneOffset(float worldSize) // 중앙선에서 차량 차선까지 거리
        {
            return RoadWidth(worldSize) * 0.24f; // 양방향 차선 분리 거리 계산
        }

        public static float SidewalkOffset(float worldSize) // 교차로 중심에서 보도 모서리까지 거리
        {
            return RoadWidth(worldSize) * 0.5f + 2.0f; // 도로 밖 보행 안전 여유 추가
        }

        public static float RoadCoordinate(int index, float worldSize) // 도로선 번호의 월드 좌표
        {
            if (index < 0 || index >= RoadCount) // 유효한 도로 번호 확인
            {
                throw new ArgumentOutOfRangeException(nameof(index)); // 잘못된 도로 번호 보고
            }
            return RoadMinimum(worldSize) + RoadPitch(worldSize) * index; // 실제 월드 좌표 반환
        }

        public static Vector3 LanePoint(int x, int z, MapTrafficDirection direction, float worldSize, float y) // 교차로의 방향별 차량 차선 좌표
        {
            float roadX = RoadCoordinate(x, worldSize); // 교차로 가로 좌표
            float roadZ = RoadCoordinate(z, worldSize); // 교차로 세로 좌표
            float lane = LaneOffset(worldSize); // 차선 오프셋 조회
            switch (direction) // 진행 방향별 우측 통행 차선 선택
            {
                case MapTrafficDirection.East: // 동쪽 진행 차선
                    return new Vector3(roadX, y, roadZ - lane); // 도로 남쪽 차선 반환
                case MapTrafficDirection.West: // 서쪽 진행 차선
                    return new Vector3(roadX, y, roadZ + lane); // 도로 북쪽 차선 반환
                case MapTrafficDirection.North: // 북쪽 진행 차선
                    return new Vector3(roadX + lane, y, roadZ); // 도로 동쪽 차선 반환
                default: // 남쪽 진행 차선
                    return new Vector3(roadX - lane, y, roadZ); // 도로 서쪽 차선 반환
            }
        }

        public static Vector2Int Delta(MapTrafficDirection direction) // 방향을 교차로 인덱스 변화로 변환
        {
            switch (direction) // 진행 방향 확인
            {
                case MapTrafficDirection.East: // 동쪽 진행
                    return Vector2Int.right; // 가로 번호 증가
                case MapTrafficDirection.West: // 서쪽 진행
                    return Vector2Int.left; // 가로 번호 감소
                case MapTrafficDirection.North: // 북쪽 진행
                    return Vector2Int.up; // 세로 번호 증가
                default: // 남쪽 진행
                    return Vector2Int.down; // 세로 번호 감소
            }
        }

        public static bool CanAdvance(int x, int z, MapTrafficDirection direction) // 현재 교차로에서 다음 교차로 존재 여부
        {
            Vector2Int delta = Delta(direction); // 진행 인덱스 변화 조회
            int nextX = x + delta.x; // 다음 가로 번호 계산
            int nextZ = z + delta.y; // 다음 세로 번호 계산
            return nextX >= 0 && nextX < RoadCount && nextZ >= 0 && nextZ < RoadCount; // 도시 도로 경계 안쪽 여부 반환
        }

        public static Vector2Int Step(int x, int z, MapTrafficDirection direction) // 다음 교차로 번호 계산
        {
            Vector2Int delta = Delta(direction); // 방향 인덱스 변화 조회
            return new Vector2Int(x + delta.x, z + delta.y); // 다음 교차로 번호 반환
        }

        public static MapTrafficDirection Opposite(MapTrafficDirection direction) // 유턴 제외용 반대 방향 조회
        {
            switch (direction) // 현재 방향 확인
            {
                case MapTrafficDirection.East: // 동쪽 반대
                    return MapTrafficDirection.West; // 서쪽 반환
                case MapTrafficDirection.West: // 서쪽 반대
                    return MapTrafficDirection.East; // 동쪽 반환
                case MapTrafficDirection.North: // 북쪽 반대
                    return MapTrafficDirection.South; // 남쪽 반환
                default: // 남쪽 반대
                    return MapTrafficDirection.North; // 북쪽 반환
            }
        }

        public static Vector3 DirectionVector(MapTrafficDirection direction) // 차량 회전용 월드 방향 벡터
        {
            switch (direction) // 진행 방향 확인
            {
                case MapTrafficDirection.East: // 동쪽 진행
                    return Vector3.right; // 동쪽 벡터 반환
                case MapTrafficDirection.West: // 서쪽 진행
                    return Vector3.left; // 서쪽 벡터 반환
                case MapTrafficDirection.North: // 북쪽 진행
                    return Vector3.forward; // 북쪽 벡터 반환
                default: // 남쪽 진행
                    return Vector3.back; // 남쪽 벡터 반환
            }
        }

        public static int IntersectionId(int x, int z) // 교차로 점유용 고유 번호 계산
        {
            return z * RoadCount + x; // 13x13 일차원 번호 반환
        }
    }
}
