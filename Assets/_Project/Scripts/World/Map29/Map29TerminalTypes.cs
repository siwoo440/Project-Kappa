using UnityEngine; // 단말기 목표 데이터 구조

namespace ProjectK.Day29 // 29일차 플레이어 단말기 이름 공간
{
    public enum Map29TerminalObjectiveStatus // 단말기 목표 상태
    {
        Idle, // 추적 목표 없음
        Tracking, // 현재 목표 추적 중
        Completed, // 현재 목표 완료
        Failed, // 현재 목표 실패
        Paused // 현재 목표 일시 정지
    }

    public readonly struct Map29TerminalObjectiveSnapshot // HUD가 읽는 현재 목표 복사본
    {
        public readonly string MissionId; // 미션 또는 추적 ID
        public readonly string MissionTitle; // 현재 미션 이름
        public readonly string ObjectiveText; // 현재 목표 한 줄 설명
        public readonly string TargetLabel; // 목표 장소 또는 대상 이름
        public readonly Vector3 TargetPosition; // 현재 목표 월드 위치
        public readonly bool HasTarget; // 목표 위치 존재 여부
        public readonly Map29TerminalObjectiveStatus Status; // 현재 목표 상태
        public readonly bool IsDemo; // 미션 시스템 전 임시 추적 여부

        public Map29TerminalObjectiveSnapshot(string missionId, string missionTitle, string objectiveText, string targetLabel, Vector3 targetPosition, bool hasTarget, Map29TerminalObjectiveStatus status, bool isDemo) // 목표 정보 생성
        {
            MissionId = missionId; // ID 저장
            MissionTitle = missionTitle; // 미션 이름 저장
            ObjectiveText = objectiveText; // 목표 문구 저장
            TargetLabel = targetLabel; // 목표 라벨 저장
            TargetPosition = targetPosition; // 목표 위치 저장
            HasTarget = hasTarget; // 목표 위치 여부 저장
            Status = status; // 상태 저장
            IsDemo = isDemo; // 임시 추적 여부 저장
        }
    }
}
