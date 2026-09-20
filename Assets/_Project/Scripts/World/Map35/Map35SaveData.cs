using System; // JSON 직렬화 자료
using UnityEngine; // Vector3 저장

namespace ProjectK.Day35 // 35일차 저장·불러오기 이름 공간
{
    [Serializable] // 슬롯 메타데이터 직렬화
    public sealed class Map35SaveMeta // 저장 슬롯 카드에 필요한 최소 정보
    {
        public int Version = 1; // 저장 데이터 버전
        public int Slot; // 저장 슬롯 번호
        public string SavedAt = string.Empty; // 저장 시각
        public string MissionId = string.Empty; // 현재 임무 ID
        public string MissionTitle = string.Empty; // 현재 임무 제목
        public string ObjectiveText = string.Empty; // 현재 목표 문구
        public int Credits; // 저장 시점 크레딧
        public float PlaySeconds; // 세션 플레이 시간
    }

    [Serializable] // 단일 임무 진행 상태 직렬화
    public sealed class Map35MissionStateData // MissionManager 런타임 상태 저장
    {
        public string MissionId = string.Empty; // 임무 고정 ID
        public int RuntimeStatus; // Map31MissionRuntimeStatus 정수값
        public int JournalStatus; // Map30MissionStatus 정수값
        public int ObjectiveIndex; // 현재 목표 순번
    }

    [Serializable] // Day32 체크포인트 직렬화
    public sealed class Map35CheckpointData // 영구 저장 가능한 체크포인트 복사본
    {
        public bool Valid; // 체크포인트 유효 여부
        public string MissionId = string.Empty; // 체크포인트 임무 ID
        public string Label = string.Empty; // 체크포인트 이름
        public int ObjectiveIndex; // 다음 진행 목표 순번
        public Vector3 PlayerPosition; // 체크포인트 위치
        public Vector3 PlayerEuler; // 체크포인트 방향
        public string[] OwnedItems = Array.Empty<string>(); // 체크포인트 보유 임무 물품
        public string[] DeliveredItems = Array.Empty<string>(); // 체크포인트 인계 물품
    }

    [Serializable] // 플레이어 생존 수치 직렬화
    public sealed class Map35PlayerStateData // 플레이어 위치·체력·자세 저장
    {
        public Vector3 Position; // 플레이어 월드 위치
        public Vector3 Euler; // 플레이어 월드 회전
        public float Health; // 현재 체력
        public float Posture; // 현재 자세
    }

    [Serializable] // S-01 틈 계약 상태 직렬화
    public sealed class Map35SideContractStateData // Day34 계약 알림·보상 상태 저장
    {
        public bool ContractReleased; // 신규 의뢰 공개 여부
        public bool CreditGranted; // S-01 크레딧 지급 여부
        public bool RewardChoiceShown; // 선택 보상 화면 표시 여부
        public bool ContractClosedShown; // 계약 종료 메시지 표시 여부
        public bool RewardChosen; // 선택 보상 완료 여부
        public string SelectedRewardName = string.Empty; // 선택 보상 이름
    }

    [Serializable] // 한 슬롯의 실제 저장 데이터
    public sealed class Map35SaveData // 수직 슬라이스 영구 저장 데이터 V1
    {
        public int Version = 1; // 저장 데이터 버전
        public Map35SaveMeta Meta = new Map35SaveMeta(); // 슬롯 카드 메타데이터
        public Map35PlayerStateData Player = new Map35PlayerStateData(); // 플레이어 상태
        public string ActiveMissionId = string.Empty; // 현재 진행 임무 ID
        public Map35MissionStateData[] Missions = Array.Empty<Map35MissionStateData>(); // 전체 임무 상태
        public string[] OwnedMissionItems = Array.Empty<string>(); // 현재 보유 임무 물품
        public string[] DeliveredMissionItems = Array.Empty<string>(); // 현재 인계 완료 임무 물품
        public Map35CheckpointData Checkpoint = new Map35CheckpointData(); // 현재 안전 체크포인트
        public string[] GrantedMissionRewards = Array.Empty<string>(); // Day32 결과 보상 기록
        public int Credits; // Day34 실제 크레딧
        public string[] CreditRewardedMissions = Array.Empty<string>(); // 크레딧 지급 완료 임무 ID
        public Map35SideContractStateData S01 = new Map35SideContractStateData(); // S-01 계약 상태
        public int[] ConsumableCounts = Array.Empty<int>(); // 회복·유인·연막 수량
        public int SelectedConsumable; // 현재 선택 소모품 종류
    }

    public sealed class Map35SlotInfo // 저장·불러오기 화면에서 사용하는 런타임 슬롯 정보
    {
        public int Slot; // 슬롯 번호
        public bool HasData; // 저장 데이터 존재 여부
        public Map35SaveMeta Meta; // 저장 메타데이터
        public Texture2D Preview; // 저장 화면 미리보기
    }
}
