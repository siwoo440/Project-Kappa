using System.Collections.Generic; // 임무 물품 보유·인계 상태
using UnityEngine; // 런타임 컴포넌트

namespace ProjectK.Day31 // 31일차 공통 미션 시스템 이름 공간
{
    [DisallowMultipleComponent] // 임무 인벤토리 중복 방지
    public sealed class Map31MissionInventory : MonoBehaviour // 일반 소모품과 분리된 임무 물품 슬롯 기반
    {
        private readonly HashSet<string> owned = new HashSet<string>(); // 현재 보유 임무 물품
        private readonly HashSet<string> delivered = new HashSet<string>(); // 이미 인계한 임무 물품

        public bool Has(string itemId) // 임무 물품 보유 여부 확인
        {
            return !string.IsNullOrWhiteSpace(itemId) && owned.Contains(itemId); // 유효 ID의 보유 여부 반환
        }

        public bool WasDelivered(string itemId) // 임무 물품 인계 완료 여부 확인
        {
            return !string.IsNullOrWhiteSpace(itemId) && delivered.Contains(itemId); // 인계 기록 반환
        }

        public bool Add(string itemId) // 임무 물품 획득
        {
            if (string.IsNullOrWhiteSpace(itemId)) // 잘못된 물품 ID 확인
            {
                return false; // 획득 실패
            }

            delivered.Remove(itemId); // 재획득 시 이전 인계 기록 해제
            return owned.Add(itemId); // 신규 보유 여부 반환
        }

        public bool Deliver(string itemId) // 임무 물품 인계
        {
            if (!Has(itemId)) // 실제 보유 여부 확인
            {
                return false; // 인계 실패
            }

            owned.Remove(itemId); // 보유 슬롯에서 제거
            delivered.Add(itemId); // 인계 기록 저장
            return true; // 인계 성공
        }

        public string[] CaptureOwnedItems() // Day32 체크포인트용 보유 임무 물품 복사
        {
            string[] result = new string[owned.Count]; // 현재 보유 수만큼 배열 생성
            owned.CopyTo(result); // HashSet 내용을 배열에 복사
            return result; // 체크포인트 저장용 배열 반환
        }

        public string[] CaptureDeliveredItems() // Day32 체크포인트용 인계 기록 복사
        {
            string[] result = new string[delivered.Count]; // 현재 인계 수만큼 배열 생성
            delivered.CopyTo(result); // HashSet 내용을 배열에 복사
            return result; // 체크포인트 저장용 배열 반환
        }

        public void RestoreSnapshot(string[] ownedItems, string[] deliveredItems) // Day32 체크포인트 임무 물품 상태 복원
        {
            owned.Clear(); // 현재 보유 상태 제거
            delivered.Clear(); // 현재 인계 기록 제거

            if (ownedItems != null) // 저장된 보유 물품 존재 확인
            {
                for (int i = 0; i < ownedItems.Length; i++) // 저장 배열 순회
                {
                    if (!string.IsNullOrWhiteSpace(ownedItems[i])) // 유효 ID 확인
                    {
                        owned.Add(ownedItems[i]); // 보유 상태 복원
                    }
                }
            }

            if (deliveredItems != null) // 저장된 인계 물품 존재 확인
            {
                for (int i = 0; i < deliveredItems.Length; i++) // 저장 배열 순회
                {
                    if (!string.IsNullOrWhiteSpace(deliveredItems[i])) // 유효 ID 확인
                    {
                        delivered.Add(deliveredItems[i]); // 인계 기록 복원
                    }
                }
            }
        }

        public void ClearAll() // 임무 재시작·디버그용 전체 초기화
        {
            owned.Clear(); // 보유 물품 초기화
            delivered.Clear(); // 인계 기록 초기화
        }
    }
}
