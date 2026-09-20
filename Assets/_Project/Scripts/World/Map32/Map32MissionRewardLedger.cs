using System.Collections.Generic; // 보상 지급 기록
namespace ProjectK.Day32 // 32일차 체크포인트·재시도 이름 공간
{
    public static class Map32MissionRewardLedger // 현재 세션에서 임무 보상이 중복 지급되지 않도록 기록
    {
        private static readonly HashSet<string> granted = new HashSet<string>(); // 이미 지급 처리한 임무 ID 목록

        public static bool TryMarkGranted(string missionId) // 임무 완료 보상 최초 지급 여부 확인
        {
            if (string.IsNullOrWhiteSpace(missionId)) // 잘못된 임무 ID 확인
            {
                return false; // 지급 기록 실패
            }

            return granted.Add(missionId); // 최초 기록일 때만 true 반환
        }

        public static bool WasGranted(string missionId) // 이미 보상 처리한 임무 여부 확인
        {
            return !string.IsNullOrWhiteSpace(missionId) && granted.Contains(missionId); // 지급 기록 반환
        }

        public static void ResetForNewGame() // 이후 새 게임·세이브 로드 시스템용 초기화 API
        {
            granted.Clear(); // 세션 보상 기록 전체 제거
        }
    }
}
