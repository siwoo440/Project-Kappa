using System; // 이벤트 기능
using UnityEngine; // 유니티 기본 기능

public enum NoiseType // 게임플레이 소음 유형
{
    Footstep, // 일반 발소리
    Sprint, // 달리기 소리
    CrouchStep, // 앉기 이동 소리
    Landing, // 착지 소리
    Interaction, // 상호작용 소리
    Gunshot, // 총성
    Lure // 유인 소리
}

public readonly struct NoiseEvent // 소음 이벤트 정보
{
    public readonly Vector3 Position; // 발생 위치
    public readonly float Radius; // 전달 반경
    public readonly NoiseType Type; // 소음 유형
    public readonly GameObject Source; // 발생 객체
    public readonly float TimeStamp; // 발생 시간

    public NoiseEvent(Vector3 position, float radius, NoiseType type, GameObject source) // 소음 이벤트 생성
    {
        Position = position; // 발생 위치 저장
        Radius = Mathf.Max(0f, radius); // 전달 반경 저장
        Type = type; // 소음 유형 저장
        Source = source; // 발생 객체 저장
        TimeStamp = Time.time; // 발생 시간 저장
    }
}

public static class NoiseSystem // 전역 게임플레이 소음 전달 시스템
{
    public static event Action<NoiseEvent> NoiseEmitted; // 소음 발생 이벤트

    public static NoiseEvent LastNoise { get; private set; } // 최근 소음 정보
    public static bool HasNoise { get; private set; } // 최근 소음 존재 여부

    public static void Emit(Vector3 position, float radius, NoiseType type, GameObject source) // 소음 발생 처리
    {
        NoiseEvent noiseEvent = new NoiseEvent(position, radius, type, source); // 소음 이벤트 생성
        LastNoise = noiseEvent; // 최근 소음 저장
        HasNoise = true; // 최근 소음 존재 상태 저장
        NoiseEmitted?.Invoke(noiseEvent); // 소음 이벤트 전달
    }
}
