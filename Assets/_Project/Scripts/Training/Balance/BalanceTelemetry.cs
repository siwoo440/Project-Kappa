using System; // 선택적 계측 이벤트
using System.Collections.Generic; // 한 발의 대상별 기록
using UnityEngine; // 사격과 센서 참조

public sealed class BalanceImpactReport // 하나의 대상에 대한 한 발 결과
{
    public Component Target; // 실제 적 또는 반응형 표적
    public int Pellets; // 해당 대상에 맞은 펠릿
    public int Heads; // 해당 대상 머리에 맞은 펠릿
    public float Before; // 피격 전 체력
    public float After; // 피격 후 체력
    public float AppliedDamage; // 실제로 감소한 체력
    public bool Killed; // 이번 발사로 제압된 대상
}

public sealed class BalanceShotReport // 탄약 한 발 단위의 관측 자료
{
    public Transform Shooter; // 실제 발사 주체
    public FirearmDefinition Definition; // 실제 장착 무기
    public double Time; // 게임에서 발사를 처리한 시각
    public Vector3 Muzzle; // 실제 총구 위치
    public float Spread; // 실제 적용한 분산
    public bool Blocked; // 총구 가림 발생 여부
    public bool Suppressed; // 이번 발사 소음기 상태
    public List<BalanceImpactReport> Impacts = new List<BalanceImpactReport>(); // 같은 대상 합산 이후 결과
}

public static class BalanceTelemetry // 전투 동작을 변경하지 않는 선택적 관측 통로
{
    public static event Action<BalanceShotReport> ShotResolved; // 실제 발사 결과 구독
    public static event Action<DetectionSensor, NoiseEvent> NoiseHeard; // 센서가 실제 수용한 소음
    public static event Action<DetectionSensor> DetectionChanged; // 실제 탐지 상태 전환
    public static bool HasShotListeners => ShotResolved != null; // 계측 미사용 시 추가 기록 할당 생략

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // 도메인 재로드 생략 대응
    private static void ResetStatics() // 새 Play의 이전 이벤트 제거
    {
        ShotResolved = null; // 이전 발사 구독 제거
        NoiseHeard = null; // 이전 청각 구독 제거
        DetectionChanged = null; // 이전 상태 구독 제거
    }

    public static void PublishShot(BalanceShotReport value) // 계측 오류와 전투 처리 분리
    {
        if (value == null || ShotResolved == null) // 사용 중인 계측 확인
        {
            return; // 계측 미사용 상태 종료
        }
        foreach (Action<BalanceShotReport> handler in ShotResolved.GetInvocationList()) // 구독자별 독립 전달
        {
            try // 계측 오류가 탄약과 사격을 끊지 않도록 보호
            {
                handler(value); // 관측 결과 전달
            }
            catch (Exception error) // 관측자 실패 보고
            {
                Debug.LogException(error); // 실제 오류 출력
            }
        }
    }

    public static void PublishHearing(DetectionSensor sensor, NoiseEvent sound) // 실제 청취 확정 후 호출
    {
        if (NoiseHeard == null) // 계측 구독 확인
        {
            return; // 미사용 처리 생략
        }
        foreach (Action<DetectionSensor, NoiseEvent> handler in NoiseHeard.GetInvocationList()) // 청각 구독자별 처리
        {
            try // 청각 원래 동작 보호
            {
                handler(sensor, sound); // 실제 들은 소음 전달
            }
            catch (Exception error) // 기록 오류만 보고
            {
                Debug.LogException(error); // 원인 로그 출력
            }
        }
    }

    public static void PublishDetection(DetectionSensor sensor) // 실제 상태 변경 후 호출
    {
        if (DetectionChanged == null) // 계측 구독 확인
        {
            return; // 미사용 처리 생략
        }
        foreach (Action<DetectionSensor> handler in DetectionChanged.GetInvocationList()) // 상태 구독자별 처리
        {
            try // AI 상태 처리 보호
            {
                handler(sensor); // 새 상태 전달
            }
            catch (Exception error) // 기록 오류만 보고
            {
                Debug.LogException(error); // 원인 로그 출력
            }
        }
    }
}
