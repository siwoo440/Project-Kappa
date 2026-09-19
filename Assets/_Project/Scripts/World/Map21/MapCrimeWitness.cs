using ProjectK.Day20; // 활성 시민 목격 확인
using UnityEngine; // 거리와 시야 검사

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    public static class MapCrimeWitness // 범죄가 신고되는지 간단하게 판단
    {
        public static bool IsReported(Vector3 position, GameObject instigator, bool loud) // 시민·경비·카메라 목격 여부 검사
        {
            float citizenRadius = loud ? 52f : 28f; // 큰 소음의 신고 가능 범위 확대
            MapCitizenAgent[] citizens = Object.FindObjectsByType<MapCitizenAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 시민 전체 조회
            foreach (MapCitizenAgent citizen in citizens) // 시민 목격자 순회
            {
                if (citizen == null) // 누락 시민 확인
                {
                    continue; // 다음 시민 검사
                }
                MapCitizenVitals vitals = citizen.GetComponent<MapCitizenVitals>(); // 시민 생존 상태 조회
                if (vitals != null && vitals.IsDead) // 사망 시민 확인
                {
                    continue; // 목격자 제외
                }
                float citizenDistance = (citizen.transform.position - position).sqrMagnitude; // 사건과 시민 거리 제곱 계산
                if (!loud && citizenDistance < 1.6f * 1.6f) // 조용한 근접 공격의 피해자 본인을 목격자로 세지 않음
                {
                    continue; // 주변 제삼자 목격자만 검사
                }
                if (citizenDistance <= citizenRadius * citizenRadius) // 범죄 근처 시민 확인
                {
                    return true; // 시민 신고 성공
                }
            }
            DetectionSensor[] sensors = Object.FindObjectsByType<DetectionSensor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 활성 경비·카메라 센서 조회
            foreach (DetectionSensor sensor in sensors) // 센서 목격·청취 확인
            {
                if (sensor == null) // 누락 센서 확인
                {
                    continue; // 다음 센서 검사
                }
                float hearing = Mathf.Max(sensor.HearingRadius, loud ? 30f : 14f); // 소음 여부에 따른 신고 거리 계산
                if (sensor.TargetVisible || (sensor.transform.position - position).sqrMagnitude <= hearing * hearing) // 시야 또는 청취 범위 확인
                {
                    return true; // 보안망 신고 성공
                }
            }
            return false; // 주변 신고 수단 없음
        }

        public static bool IsPlayerInstigator(GameObject instigator, GameObject player) // 피해 주체가 실제 플레이어인지 확인
        {
            if (instigator == null || player == null) // 필수 객체 확인
            {
                return false; // 플레이어 범죄 아님
            }
            Transform source = instigator.transform; // 피해 주체 트랜스폼 조회
            Transform target = player.transform; // 플레이어 트랜스폼 조회
            return source == target || source.IsChildOf(target) || target.IsChildOf(source); // 플레이어 또는 장비 자식 여부 반환
        }
    }
}
