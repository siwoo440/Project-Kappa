using System; // 좌표와 높이 계산

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public static class MapTerrainMath // 타일 경계에서 같은 값을 만드는 지형 계산
    {
        public const int Grid = 3; // 가로 세로 타일 수
        public const float Ground = 16f; // 도시 기본 지면 높이
        public const float VerticalSize = 160f; // Terrain 높이 범위

        public static double Coordinate(int tile, int sample, int resolution, double tileSize) // 공유 격자 좌표 계산
        {
            if (tile < 0 || tile >= Grid || sample < 0 || sample >= resolution || resolution < 2 || tileSize <= 0) // 입력 범위 확인
            {
                throw new ArgumentOutOfRangeException(nameof(sample)); // 잘못된 격자 입력 보고
            }
            int globalSample = tile * (resolution - 1) + sample; // 경계에서 동일한 정수 좌표
            return globalSample * (tileSize / (resolution - 1)) - tileSize * Grid * 0.5; // 월드 중심 기준 위치
        }

        public static double Height(double x, double z, double worldSize, int seed) // 연속된 전체 월드 높이
        {
            double edge = Math.Max(Math.Abs(x), Math.Abs(z)) / (worldSize * 0.5); // 외곽까지의 비율
            double t = Math.Max(0, Math.Min(1, (edge - 0.86) / 0.14)); // 도시 밖 경사 범위
            double blend = t * t * (3 - 2 * t); // 평지와 외곽 경사의 부드러운 연결
            double phase = seed * 0.017; // 시드별 지형 변화
            double wave = Math.Sin(x / worldSize * 19 + phase) * Math.Cos(z / worldSize * 23 - phase); // 큰 능선 변화
            double detail = Math.Sin(x / worldSize * 67 + phase) * Math.Sin(z / worldSize * 53 + phase); // 작은 능선 변화
            return Ground + blend * (52 + wave * 15 + detail * 5); // 도시 평면을 유지한 외곽 구릉
        }

        public static float Sample(int tileX, int tileZ, int x, int z, int resolution, double tileSize, int seed) // Terrain 정규화 높이
        {
            double worldX = Coordinate(tileX, x, resolution, tileSize); // 공유 가로 좌표
            double worldZ = Coordinate(tileZ, z, resolution, tileSize); // 공유 세로 좌표
            return (float)(Height(worldX, worldZ, tileSize * Grid, seed) / VerticalSize); // 지형 높이를 영에서 일로 변환
        }
    }
}
