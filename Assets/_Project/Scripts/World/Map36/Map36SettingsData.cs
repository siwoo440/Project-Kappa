using System; // 설정 데이터 직렬화

namespace ProjectK.Day36 // 36일차 설정 시스템 이름 공간
{
    [Serializable] // settings.json 저장 구조
    public sealed class Map36SettingsData // 그래픽·사운드·조작·게임플레이 설정 V1
    {
        public int Version = 1; // 설정 데이터 버전

        public int ResolutionWidth = 1920; // 화면 가로 해상도
        public int ResolutionHeight = 1080; // 화면 세로 해상도
        public int WindowMode = 0; // 0 전체 창모드 1 독점 전체화면 2 창모드
        public int QualityLevel = 2; // Unity QualitySettings 단계
        public bool VSync; // 수직 동기화 사용 여부
        public int FrameRateLimit = 60; // -1 무제한 또는 지정 FPS

        public float MasterVolume = 1f; // 전체 음량 0~1
        public bool MasterMute; // 전체 음소거 여부

        public float MouseSensitivity = 0.12f; // 마우스 카메라 감도
        public float GamepadLookSpeed = 140f; // 게임패드 카메라 회전 속도
        public bool InvertY; // 카메라 Y축 반전 여부

        public bool MinimapVisible = true; // 미니맵 기본 표시 여부
        public int MinimapSizeLevel = 1; // 미니맵 소형·중형·대형
        public float MissionGuideDuration = 4f; // G 목표 안내 표시 시간

        public Map36SettingsData Clone() // 적용값과 편집값 분리를 위한 복사본 생성
        {
            return new Map36SettingsData
            {
                Version = Version, // 버전 복사
                ResolutionWidth = ResolutionWidth, // 가로 해상도 복사
                ResolutionHeight = ResolutionHeight, // 세로 해상도 복사
                WindowMode = WindowMode, // 화면 모드 복사
                QualityLevel = QualityLevel, // 품질 복사
                VSync = VSync, // VSync 복사
                FrameRateLimit = FrameRateLimit, // FPS 제한 복사
                MasterVolume = MasterVolume, // 전체 음량 복사
                MasterMute = MasterMute, // 음소거 복사
                MouseSensitivity = MouseSensitivity, // 마우스 감도 복사
                GamepadLookSpeed = GamepadLookSpeed, // 게임패드 감도 복사
                InvertY = InvertY, // Y축 반전 복사
                MinimapVisible = MinimapVisible, // 미니맵 표시 복사
                MinimapSizeLevel = MinimapSizeLevel, // 미니맵 크기 복사
                MissionGuideDuration = MissionGuideDuration // 목표 안내 시간 복사
            };
        }
    }
}
