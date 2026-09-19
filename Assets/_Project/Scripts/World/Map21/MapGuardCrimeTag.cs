using UnityEngine; // 경비 공격·처치 범죄 연결

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    [DisallowMultipleComponent] // 경비 범죄 표식 중복 방지
    public sealed class MapGuardCrimeTag : MonoBehaviour // 법 집행 경비 공격을 Heat에 연결
    {
        private float lastAttackReport = float.NegativeInfinity; // 연속 타격 Heat 폭증 방지 시각
        private bool killReported; // 처치 범죄 중복 방지

        private void OnEnable() // 풀 재사용 상태 초기화
        {
            lastAttackReport = float.NegativeInfinity; // 공격 신고 시각 초기화
            killReported = false; // 처치 신고 초기화
        }

        public void ReportDamage(GameObject instigator, float before, float after) // 경비 체력 변화에서 범죄 발생
        {
            MapWantedSystem wanted = MapWantedSystem.Instance; // 현재 수배 관리자 조회
            if (wanted == null || wanted.Player == null || !MapCrimeWitness.IsPlayerInstigator(instigator, wanted.Player)) // 플레이어 공격 여부 확인
            {
                return; // 다른 피해는 수배에 반영하지 않음
            }
            if (before > 0f && after <= 0f && !killReported) // 이번 피해로 경비 처치 확인
            {
                killReported = true; // 중복 처치 신고 방지
                wanted.ReportCrime(CrimeType.GuardKilled, transform.position, instigator, true, true); // 경비 처치는 즉시 신고
                return; // 공격 범죄와 이중 합산 방지
            }
            if (after < before && Time.time - lastAttackReport >= 1f) // 실제 피해와 신고 쿨다운 확인
            {
                lastAttackReport = Time.time; // 최근 공격 신고 시각 저장
                wanted.ReportCrime(CrimeType.GuardAttack, transform.position, instigator, true, true); // 경비 공격은 즉시 신고
            }
        }
    }
}
