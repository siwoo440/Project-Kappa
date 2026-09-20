using System; // 설정 저장 시각·예외 처리
using System.Collections.Generic; // 해상도 목록 구성
using System.IO; // settings.json 저장
using System.Reflection; // 기존 시스템 private 설정 안전 적용
using ProjectK.Day19; // 미니맵 설정 적용
using ProjectK.Day33; // 목표 안내 표시 시간 적용
using UnityEngine; // Screen·QualitySettings·AudioListener
using UnityEngine.SceneManagement; // 씬 전환 뒤 설정 재적용

namespace ProjectK.Day36 // 36일차 설정 시스템 이름 공간
{
    [DisallowMultipleComponent] // 설정 관리자 중복 방지
    public sealed class Map36SettingsManager : MonoBehaviour // 설정 로드·편집·적용·저장 중앙 관리자
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic; // private 인스턴스 필드 접근
        private static Map36SettingsManager instance; // 현재 설정 관리자
        private Map36SettingsData current; // 실제 적용된 설정
        private Map36SettingsData draft; // UI에서 편집 중인 설정
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>(); // 중복 제거 해상도 목록
        private string settingsPath = string.Empty; // 설정 JSON 경로
        private string backupPath = string.Empty; // 설정 백업 JSON 경로
        private float reapplyAt; // 씬 전환 뒤 재적용 시각
        private bool pendingReapply; // 씬 전환 설정 재적용 대기 여부

        public static Map36SettingsManager Instance => instance; // 현재 관리자 조회
        public Map36SettingsData Current => current; // 실제 적용값 조회
        public Map36SettingsData Draft => draft; // UI 편집값 조회
        public IReadOnlyList<Vector2Int> Resolutions => resolutions; // 해상도 선택 목록
        public bool HasUnsavedChanges => !Same(current, draft); // 적용되지 않은 변경 존재 여부

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] // Domain Reload 비활성 환경 초기화
        private static void ResetStatics() // 새 플레이 세션 정적 상태 초기화
        {
            instance = null; // 이전 관리자 참조 제거
        }

        private void Awake() // 단일 관리자 등록과 설정 로드
        {
            if (instance != null && instance != this) // 기존 관리자 확인
            {
                Destroy(gameObject); // 중복 제거
                return; // 초기화 중단
            }

            instance = this; // 현재 관리자 등록
            DontDestroyOnLoad(gameObject); // 씬 전환 유지
            string directory = Path.Combine(Application.persistentDataPath, "ProjectK"); // 공통 프로젝트 설정 폴더
            Directory.CreateDirectory(directory); // 폴더 생성
            settingsPath = Path.Combine(directory, "settings.json"); // 현재 설정 파일 경로
            backupPath = Path.Combine(directory, "settings.backup.json"); // 백업 설정 파일 경로
            BuildResolutionList(); // 사용 가능한 해상도 목록 구성
            current = LoadOrDefault(); // 저장 설정 또는 기본값 불러오기
            draft = current.Clone(); // UI 편집값 초기화
            Apply(current, true); // 게임 시작 즉시 설정 적용
        }

        private void OnEnable() // 씬 전환 이벤트 연결
        {
            SceneManager.activeSceneChanged += HandleSceneChanged; // 새 카메라·지도에 설정 재적용
        }

        private void OnDisable() // 씬 전환 이벤트 해제
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged; // 정적 이벤트 참조 해제
        }

        private void Update() // 씬 전환 뒤 늦게 생성되는 카메라·지도 설정 복구
        {
            if (!pendingReapply || Time.unscaledTime < reapplyAt) // 재적용 대기 여부 확인
            {
                return; // 처리 생략
            }

            pendingReapply = false; // 대기 상태 해제
            ApplyRuntimeOnly(current); // 해상도 재전환 없이 런타임 객체 설정만 재적용
        }

        public void BeginEdit() // 설정 페이지 진입 시 현재 적용값을 편집값으로 복사
        {
            if (current == null) current = CreateDefaults(); // 비정상 상태 기본값 보장
            draft = current.Clone(); // 적용값 기준 새 편집 세션 시작
        }

        public void CancelEdit() // 아직 적용하지 않은 변경 취소
        {
            draft = current != null ? current.Clone() : CreateDefaults(); // 현재 적용값으로 편집 상태 복원
        }

        public void ResetDraftToDefaults() // 기본값 복원 버튼용 편집값 초기화
        {
            draft = CreateDefaults(); // 시스템 기본 해상도와 프로젝트 기본 옵션 적용
        }

        public bool ApplyAndSave() // 편집값 검증·실제 적용·settings.json 저장
        {
            if (draft == null) // 편집 데이터 확인
            {
                draft = CreateDefaults(); // 기본값 복구
            }

            Sanitize(draft); // 범위 밖 값 보정
            current = draft.Clone(); // 실제 적용값 갱신
            Apply(current, true); // 화면·오디오·입력·게임플레이 설정 적용
            return SaveCurrent(); // JSON 영구 저장 결과 반환
        }

        public void SetResolutionIndex(int index) // UI 좌우 버튼으로 해상도 변경
        {
            if (draft == null || resolutions.Count == 0) // 편집값과 목록 확인
            {
                return; // 변경 생략
            }

            index = WrapIndex(index, resolutions.Count); // 목록 범위 순환
            Vector2Int resolution = resolutions[index]; // 선택 해상도 조회
            draft.ResolutionWidth = resolution.x; // 가로값 저장
            draft.ResolutionHeight = resolution.y; // 세로값 저장
        }

        public int ResolutionIndex() // 현재 편집 해상도의 목록 위치 조회
        {
            if (draft == null || resolutions.Count == 0) // 편집값·목록 확인
            {
                return 0; // 기본 인덱스 반환
            }

            int nearest = 0; // 가장 가까운 해상도 기본 인덱스
            int bestScore = int.MaxValue; // 거리 점수 초기화

            for (int i = 0; i < resolutions.Count; i++) // 전체 해상도 순회
            {
                Vector2Int candidate = resolutions[i]; // 현재 후보
                int score = Mathf.Abs(candidate.x - draft.ResolutionWidth) + Mathf.Abs(candidate.y - draft.ResolutionHeight); // 해상도 차이 점수

                if (score < bestScore) // 더 가까운 해상도 확인
                {
                    bestScore = score; // 최적 점수 갱신
                    nearest = i; // 최적 인덱스 저장
                }
            }

            return nearest; // 현재 해상도와 가장 가까운 목록 위치 반환
        }

        private void Apply(Map36SettingsData data, bool applyDisplay) // 전체 설정 실제 게임 시스템에 적용
        {
            if (data == null) // 설정 데이터 확인
            {
                return; // 처리 생략
            }

            if (applyDisplay) // 해상도·화면 모드 재적용 여부 확인
            {
                FullScreenMode mode = WindowModeToUnity(data.WindowMode); // 저장 정수값을 Unity 화면 모드로 변환
                Screen.SetResolution(data.ResolutionWidth, data.ResolutionHeight, mode); // 해상도와 화면 모드 적용
            }

            if (QualitySettings.names.Length > 0) // 품질 단계 존재 확인
            {
                int quality = Mathf.Clamp(data.QualityLevel, 0, QualitySettings.names.Length - 1); // 품질 인덱스 보정
                QualitySettings.SetQualityLevel(quality, true); // 실제 그래픽 품질 적용
            }

            QualitySettings.vSyncCount = data.VSync ? 1 : 0; // 수직 동기화 적용
            Application.targetFrameRate = data.FrameRateLimit <= 0 ? -1 : data.FrameRateLimit; // FPS 제한 적용
            AudioListener.volume = data.MasterMute ? 0f : Mathf.Clamp01(data.MasterVolume); // 전체 음량·음소거 적용
            ApplyRuntimeOnly(data); // 카메라·미니맵·목표 안내 설정 적용
        }

        private static void ApplyRuntimeOnly(Map36SettingsData data) // 현재 씬 런타임 객체에만 사용자 설정 적용
        {
            if (data == null) // 설정 자료 확인
            {
                return; // 처리 생략
            }

            ThirdPersonCamera[] cameras = UnityEngine.Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 3인칭 카메라 검색
            for (int i = 0; i < cameras.Length; i++) // 모든 카메라 순회
            {
                ThirdPersonCamera camera = cameras[i]; // 현재 카메라 조회
                if (camera == null) // 유효 카메라 확인
                {
                    continue; // 다음 카메라 처리
                }

                SetPrivateFloat(camera, "mouseSensitivity", Mathf.Clamp(data.MouseSensitivity, 0.03f, 0.40f)); // 기존 마우스 감도 필드 적용
                SetPrivateFloat(camera, "gamepadLookSpeed", Mathf.Clamp(data.GamepadLookSpeed, 60f, 300f)); // 기존 게임패드 감도 필드 적용
                SetPrivateBool(camera, "invertY", data.InvertY); // Day36 패처가 추가하는 Y축 반전 필드 적용
            }

            MapNavigationUI[] maps = UnityEngine.Object.FindObjectsByType<MapNavigationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 지도 UI 검색
            for (int i = 0; i < maps.Length; i++) // 모든 지도 UI 순회
            {
                ApplyMapSettings(maps[i], data); // 미니맵 표시·크기 적용
            }

            Map33MissionObjectiveArrowHUD guide = Map33MissionObjectiveArrowHUD.Instance; // G 목표 안내 HUD 조회
            if (guide != null) // 목표 안내 시스템 존재 확인
            {
                FieldInfo durationField = typeof(Map33MissionObjectiveArrowHUD).GetField("visibleDuration", InstancePrivate); // private 표시 시간 필드 조회
                durationField?.SetValue(guide, Mathf.Clamp(data.MissionGuideDuration, 2f, 8f)); // 목표 안내 표시 시간 적용
            }
        }

        private static void ApplyMapSettings(MapNavigationUI map, Map36SettingsData data) // 기존 Day19 미니맵 API와 private 거리 설정 연동
        {
            if (map == null || data == null) // 대상 확인
            {
                return; // 처리 생략
            }

            if (map.MinimapVisible != data.MinimapVisible) // 현재 표시 상태와 설정값 비교
            {
                map.ToggleMiniMap(); // 기존 공개 API로 표시 상태 반전
            }

            int desiredSize = Mathf.Clamp(data.MinimapSizeLevel, 0, 2); // 목표 크기 단계 보정
            int guard = 0; // 무한 순환 방지

            while (map.MinimapSizeLevel != desiredSize && guard < 3) // 최대 세 번 순환해 목표 단계 도달
            {
                map.CycleMiniMapSize(); // 기존 공개 API로 다음 크기 단계 선택
                guard++; // 순환 횟수 증가
            }
        }

        private Map36SettingsData LoadOrDefault() // 현재 설정 파일 또는 백업·기본값 읽기
        {
            Map36SettingsData loaded = ReadFile(settingsPath); // 현재 설정 파일 우선 읽기
            if (loaded == null) loaded = ReadFile(backupPath); // 실패 시 백업 파일 읽기
            if (loaded == null) loaded = CreateDefaults(); // 저장 데이터가 없으면 기본값 생성
            Sanitize(loaded); // 범위 안전 보정
            return loaded; // 실제 적용값 반환
        }

        private bool SaveCurrent() // 현재 적용 설정을 안전하게 JSON 저장
        {
            try // 파일 저장 예외 처리
            {
                string json = JsonUtility.ToJson(current, true); // 가독성 있는 JSON 생성
                string tempPath = settingsPath + ".tmp"; // 원자적 저장용 임시 파일
                File.WriteAllText(tempPath, json); // 임시 파일에 전체 설정 기록
                if (File.Exists(settingsPath)) File.Copy(settingsPath, backupPath, true); // 이전 정상 설정 백업
                File.Copy(tempPath, settingsPath, true); // 완성 임시 파일을 최종 설정으로 교체
                File.Delete(tempPath); // 임시 파일 정리
                return true; // 저장 성공
            }
            catch (Exception error) // 파일 접근 오류 처리
            {
                Debug.LogException(error); // 상세 오류 출력
                return false; // 저장 실패
            }
        }

        private static Map36SettingsData ReadFile(string path) // 단일 settings.json 안전 읽기
        {
            if (!File.Exists(path)) // 파일 존재 확인
            {
                return null; // 설정 없음
            }

            try // JSON 파싱 오류 처리
            {
                Map36SettingsData data = JsonUtility.FromJson<Map36SettingsData>(File.ReadAllText(path)); // JSON 역직렬화
                return data != null && data.Version == 1 ? data : null; // 지원 버전만 반환
            }
            catch // 손상 설정 파일 처리
            {
                return null; // 백업 또는 기본값 사용
            }
        }

        private Map36SettingsData CreateDefaults() // 현재 PC 환경 기준 기본 설정 생성
        {
            Vector2Int resolution = CurrentDesktopResolution(); // 현재 화면 해상도 조회
            int quality = QualitySettings.names.Length > 0 ? Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1) : 0; // 현재 품질 단계 조회

            return new Map36SettingsData
            {
                Version = 1, // 설정 버전
                ResolutionWidth = resolution.x, // 현재 화면 가로 해상도
                ResolutionHeight = resolution.y, // 현재 화면 세로 해상도
                WindowMode = 0, // 기본 전체 창모드
                QualityLevel = quality, // 현재 Unity 품질 사용
                VSync = false, // 기본 VSync 끄기
                FrameRateLimit = 60, // 기본 60 FPS 제한
                MasterVolume = 1f, // 기본 전체 음량 100%
                MasterMute = false, // 기본 음소거 해제
                MouseSensitivity = 0.12f, // 기존 카메라 기본 마우스 감도
                GamepadLookSpeed = 140f, // 기존 카메라 기본 게임패드 감도
                InvertY = false, // 기본 Y축 반전 해제
                MinimapVisible = true, // 기본 미니맵 표시
                MinimapSizeLevel = 1, // 기본 중형 미니맵
                MissionGuideDuration = 4f // 기본 목표 안내 4초
            };
        }

        private void BuildResolutionList() // Screen.resolutions에서 가로·세로 중복 제거 목록 구성
        {
            resolutions.Clear(); // 이전 목록 초기화
            Resolution[] available = Screen.resolutions; // OS 제공 해상도 목록 조회
            HashSet<string> unique = new HashSet<string>(); // 가로×세로 중복 제거

            for (int i = 0; i < available.Length; i++) // 모든 OS 해상도 순회
            {
                Resolution resolution = available[i]; // 현재 해상도
                if (resolution.width < 1024 || resolution.height < 576) // 너무 작은 해상도 제외
                {
                    continue; // 다음 항목
                }

                string key = resolution.width + "x" + resolution.height; // 중복 키 생성
                if (!unique.Add(key)) // 같은 가로·세로 조합 존재 확인
                {
                    continue; // 중복 제외
                }

                resolutions.Add(new Vector2Int(resolution.width, resolution.height)); // 고유 해상도 추가
            }

            if (resolutions.Count == 0) // Editor나 플랫폼에서 목록을 주지 않는 경우
            {
                Vector2Int fallback = CurrentDesktopResolution(); // 현재 화면 크기 대체
                resolutions.Add(fallback); // 최소 한 개 옵션 보장
            }

            resolutions.Sort((a, b) => // 낮은 해상도부터 정렬
            {
                int pixelCompare = (a.x * a.y).CompareTo(b.x * b.y); // 총 픽셀 수 비교
                return pixelCompare != 0 ? pixelCompare : a.x.CompareTo(b.x); // 동일 픽셀 수면 가로 길이 비교
            });
        }

        private static Vector2Int CurrentDesktopResolution() // 현재 화면 크기 안전 조회
        {
            int width = Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Mathf.Max(1280, Screen.width); // 가로값 선택
            int height = Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Mathf.Max(720, Screen.height); // 세로값 선택
            return new Vector2Int(width, height); // 현재 화면 해상도 반환
        }

        private static void Sanitize(Map36SettingsData data) // 설정 파일의 잘못된 값을 안전 범위로 보정
        {
            if (data == null) // 데이터 확인
            {
                return; // 처리 생략
            }

            data.Version = 1; // 지원 버전 고정
            data.ResolutionWidth = Mathf.Max(1024, data.ResolutionWidth); // 최소 가로 해상도
            data.ResolutionHeight = Mathf.Max(576, data.ResolutionHeight); // 최소 세로 해상도
            data.WindowMode = Mathf.Clamp(data.WindowMode, 0, 2); // 화면 모드 범위
            data.QualityLevel = QualitySettings.names.Length > 0 ? Mathf.Clamp(data.QualityLevel, 0, QualitySettings.names.Length - 1) : 0; // 품질 범위
            data.FrameRateLimit = NormalizeFps(data.FrameRateLimit); // 지원 FPS 값으로 보정
            data.MasterVolume = Mathf.Clamp01(data.MasterVolume); // 음량 범위
            data.MouseSensitivity = Mathf.Clamp(data.MouseSensitivity, 0.03f, 0.40f); // 마우스 감도 범위
            data.GamepadLookSpeed = Mathf.Clamp(data.GamepadLookSpeed, 60f, 300f); // 게임패드 감도 범위
            data.MinimapSizeLevel = Mathf.Clamp(data.MinimapSizeLevel, 0, 2); // 미니맵 크기 범위
            data.MissionGuideDuration = Mathf.Clamp(data.MissionGuideDuration, 2f, 8f); // 목표 안내 시간 범위
        }

        private static int NormalizeFps(int value) // 지원 FPS 제한 값으로 변환
        {
            int[] options = { -1, 30, 60, 120, 144 }; // UI 지원 FPS 목록
            int nearest = options[0]; // 기본 무제한
            int best = int.MaxValue; // 차이 초기화

            for (int i = 0; i < options.Length; i++) // 모든 FPS 옵션 순회
            {
                if (value <= 0 && options[i] == -1) return -1; // 무제한 값 즉시 반환
                int compareValue = options[i] < 0 ? 1000 : options[i]; // 무제한은 일반 숫자 비교에서 제외
                int score = Mathf.Abs(compareValue - value); // 차이 계산

                if (score < best) // 더 가까운 옵션 확인
                {
                    best = score; // 최적 차이 저장
                    nearest = options[i]; // 최적 FPS 저장
                }
            }

            return nearest; // 가장 가까운 지원값 반환
        }

        private static FullScreenMode WindowModeToUnity(int mode) // 저장 정수값을 Unity FullScreenMode로 변환
        {
            if (mode == 1) return FullScreenMode.ExclusiveFullScreen; // 독점 전체화면
            if (mode == 2) return FullScreenMode.Windowed; // 창모드
            return FullScreenMode.FullScreenWindow; // 기본 전체 창모드
        }

        private static bool Same(Map36SettingsData a, Map36SettingsData b) // 적용값과 편집값 변경 여부 비교
        {
            if (a == null || b == null) // 데이터 누락 확인
            {
                return false; // 동일하지 않음
            }

            return a.ResolutionWidth == b.ResolutionWidth &&
                   a.ResolutionHeight == b.ResolutionHeight &&
                   a.WindowMode == b.WindowMode &&
                   a.QualityLevel == b.QualityLevel &&
                   a.VSync == b.VSync &&
                   a.FrameRateLimit == b.FrameRateLimit &&
                   Mathf.Approximately(a.MasterVolume, b.MasterVolume) &&
                   a.MasterMute == b.MasterMute &&
                   Mathf.Approximately(a.MouseSensitivity, b.MouseSensitivity) &&
                   Mathf.Approximately(a.GamepadLookSpeed, b.GamepadLookSpeed) &&
                   a.InvertY == b.InvertY &&
                   a.MinimapVisible == b.MinimapVisible &&
                   a.MinimapSizeLevel == b.MinimapSizeLevel &&
                   Mathf.Approximately(a.MissionGuideDuration, b.MissionGuideDuration); // 모든 옵션 동일 여부 반환
        }

        private static void SetPrivateFloat(object target, string fieldName, float value) // 기존 컴포넌트 private float 설정
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, InstancePrivate) : null; // 대상 필드 조회
            field?.SetValue(target, value); // 필드 존재 시 값 적용
        }

        private static void SetPrivateBool(object target, string fieldName, bool value) // 기존 컴포넌트 private bool 설정
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, InstancePrivate) : null; // 대상 필드 조회
            field?.SetValue(target, value); // 필드 존재 시 값 적용
        }

        private static int WrapIndex(int value, int count) // 좌우 버튼 선택값 순환
        {
            if (count <= 0) return 0; // 빈 목록 보호
            int wrapped = value % count; // 나머지 계산
            return wrapped < 0 ? wrapped + count : wrapped; // 음수 보정
        }

        private void HandleSceneChanged(Scene previous, Scene currentScene) // 새 씬 카메라·지도 생성 뒤 설정 재적용 예약
        {
            pendingReapply = true; // 재적용 필요 상태 저장
            reapplyAt = Time.unscaledTime + 0.4f; // 씬 생성 완료 대기
        }
    }
}
