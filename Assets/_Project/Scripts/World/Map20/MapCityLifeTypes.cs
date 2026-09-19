using UnityEngine; // 도시 생활 공통 자료형

namespace ProjectK.Day20 // 20일차 도시 생활 이름 공간
{
    public enum MapTrafficDirection // 차량 진행 방향
    {
        East, // 동쪽 진행
        West, // 서쪽 진행
        North, // 북쪽 진행
        South // 남쪽 진행
    }

    public enum MapVehicleKind // 도로 차량 종류
    {
        Civilian, // 일반 시민 승용차
        Delivery, // 시장 중심 배달 차량
        Cargo // 산업 중심 화물 차량
    }

    public enum MapCitizenKind // 시민 외형 유형
    {
        Human, // 인간 시민
        Android, // 안드로이드 시민
        Mechanical // 완전 기계화 시민
    }

    public enum MapCitizenState // 시민 생활 상태
    {
        Idle, // 목적지에서 대기
        Walk, // 보도 이동
        Crosswalk, // 횡단보도 안전 대기 또는 이동
        Flee // 총성 반대 방향 도주
    }

    public readonly struct MapTrafficSpawn // 차량 시작 위치와 진행 정보
    {
        public readonly int X; // 시작 교차로 가로 번호
        public readonly int Z; // 시작 교차로 세로 번호
        public readonly MapTrafficDirection Direction; // 시작 진행 방향

        public MapTrafficSpawn(int x, int z, MapTrafficDirection direction) // 차량 시작 자료 생성
        {
            X = x; // 가로 교차로 저장
            Z = z; // 세로 교차로 저장
            Direction = direction; // 진행 방향 저장
        }
    }
}
