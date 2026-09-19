using UnityEngine; // 범죄·피해 공통 자료형

namespace ProjectK.Day21 // 21일차 피해·수배 이름 공간
{
    public enum WorldDamageType // 월드 피해 종류
    {
        Firearm, // 총기 피해
        Melee, // 근접 공격 피해
        Explosion // 폭발 피해
    }

    public enum CrimeType // Heat 증가 원인
    {
        Gunfire, // 신고된 총격
        CitizenAttack, // 시민 공격
        CitizenKilled, // 시민 사망
        VehicleAttack, // 차량 공격
        VehicleDestroyed, // 차량 파괴
        Explosion, // 폭발 발생
        GuardAttack, // 경비 공격
        GuardKilled // 경비 처치
    }

    public interface IWorldDamageable // 시민·차량 공통 피해 계약
    {
        float CurrentHealth { get; } // 현재 체력 조회
        float MaxHealth { get; } // 최대 체력 조회
        float ArmorReduction { get; } // 총기·폭발 기본 방어율 조회
        bool IsDead { get; } // 사망·파괴 상태 조회
        void ApplyWorldDamage(float damage, GameObject instigator, WorldDamageType type); // 최종 피해 적용
    }

    public static class MapWantedRules // Heat·별·증원 규칙 모음
    {
        public const int MaximumStars = 5; // 최대 수배 단계

        public static int StarsForHeat(float heat) // Heat를 0~5성으로 변환
        {
            if (heat >= 150f) // 최고 수배 기준 확인
            {
                return 5; // 오성 반환
            }
            if (heat >= 100f) // 사성 기준 확인
            {
                return 4; // 사성 반환
            }
            if (heat >= 60f) // 삼성 기준 확인
            {
                return 3; // 삼성 반환
            }
            if (heat >= 30f) // 이성 기준 확인
            {
                return 2; // 이성 반환
            }
            if (heat >= 10f) // 일성 기준 확인
            {
                return 1; // 일성 반환
            }
            return 0; // 수배 없음 반환
        }

        public static float HeatForCrime(CrimeType type) // 범죄 종류별 기본 Heat 계산
        {
            switch (type) // 범죄 종류 분기
            {
                case CrimeType.CitizenAttack: // 시민 공격 확인
                    return 10f; // 시민 공격 Heat
                case CrimeType.CitizenKilled: // 시민 사망 확인
                    return 25f; // 시민 사망 Heat
                case CrimeType.VehicleAttack: // 차량 공격 확인
                    return 5f; // 차량 공격 Heat
                case CrimeType.VehicleDestroyed: // 차량 파괴 확인
                    return 20f; // 차량 파괴 Heat
                case CrimeType.Explosion: // 폭발 확인
                    return 15f; // 폭발 Heat
                case CrimeType.GuardAttack: // 경비 공격 확인
                    return 20f; // 경비 공격 Heat
                case CrimeType.GuardKilled: // 경비 처치 확인
                    return 35f; // 경비 처치 Heat
                default: // 일반 총격 처리
                    return 5f; // 총격 Heat
            }
        }

        public static float PursuitRadius(int stars) // 수배 단계별 추적 반경
        {
            switch (Mathf.Clamp(stars, 0, MaximumStars)) // 안전한 단계 분기
            {
                case 1: return 80f; // 일성 추적 반경
                case 2: return 140f; // 이성 추적 반경
                case 3: return 220f; // 삼성 추적 반경
                case 4: return 320f; // 사성 추적 반경
                case 5: return 450f; // 오성 추적 반경
                default: return 0f; // 수배 없음 반경
            }
        }

        public static int GuardTargetCount(int stars) // 수배 단계별 활성 추적 병력 목표
        {
            switch (Mathf.Clamp(stars, 0, MaximumStars)) // 안전한 단계 분기
            {
                case 1: return 3; // 일성 경비 수
                case 2: return 5; // 이성 경비 수
                case 3: return 8; // 삼성 경비 수
                case 4: return 12; // 사성 경비 수
                case 5: return 16; // 오성 경비 수
                default: return 0; // 수배 없음
            }
        }

        public static float EliteRatio(int stars) // 수배 단계별 E-02 정예 비율
        {
            switch (Mathf.Clamp(stars, 0, MaximumStars)) // 안전한 단계 분기
            {
                case 3: return 0.20f; // 삼성부터 소수 정예 투입
                case 4: return 0.42f; // 사성 정예 비율 증가
                case 5: return 0.65f; // 오성 정예 중심 대응
                default: return 0f; // 이성 이하 일반 E-01만 사용
            }
        }

        public static float DecayDelayForStars(int stars) // 별 감소를 시작하기 위한 미발각 시간
        {
            switch (Mathf.Clamp(stars, 0, MaximumStars)) // 안전한 단계 분기
            {
                case 1: return 8f; // 일성 대기
                case 2: return 12f; // 이성 대기
                case 3: return 18f; // 삼성 대기
                case 4: return 24f; // 사성 대기
                case 5: return 30f; // 오성 대기
                default: return 0f; // 수배 없음
            }
        }
    }
}
