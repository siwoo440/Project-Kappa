using System; // 오류와 문자열 처리
using UnityEngine; // 단일 계측 화면
using UnityEngine.InputSystem; // 기존 입력을 보존하는 화면 조작

[DisallowMultipleComponent] // 비교 화면 중복 방지
public sealed class BalancePanel : MonoBehaviour // F8과 F 단말기로 여는 시험 설정 창
{
    [SerializeField] private BalanceSessionRunner runner; // 실제 시험 관리자
    [SerializeField] private BalanceObservationRecorder observation; // 실전 청각 관측기
    private InputActionMap suspendedMap; // 창에서만 잠시 중단한 게임 입력
    private bool mapWasEnabled; // 이전 입력 상태
    private ThirdPersonCamera cameraRig; // 기존 커서 잠금 담당
    private bool cameraWasEnabled; // 원래 카메라 제어 상태
    private CursorLockMode savedLock; // 원래 커서 잠금
    private bool savedVisible; // 원래 커서 표시
    private int weaponIndex; // 선택한 총기
    private int laneIndex; // 선택한 사격선
    private int hpIndex; // 표적 체력 선택
    private int armorIndex; // 방어율 선택
    private int policyIndex; // 허용할 머리와 몸통 적중
    private int stanceIndex; // 자세 선택
    private Vector2 scroll; // 작은 화면의 내용 스크롤
    private string notice = ""; // 패널 자체 안내
    public bool IsOpen // 실제 설정 창 표시 상태
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }
    private static readonly float[] HealthOptions = new float[] // 공통 시험 체력
    {
        150f, // 선택값 1
        300f, // 선택값 2
        600f // 선택값 3
    };
    private static readonly string[] StanceLabels = new string[] // 조건별 표시 이름
    {
        "정지 조준", // 선택값 1
        "정지 비조준", // 선택값 2
        "앉기 조준", // 선택값 3
        "좌우 이동 조준" // 선택값 4
    };


    private static readonly string[] HealthLabels = new string[] // 체력 선택 표시
    {
        "HP 150", // 선택값 1
        "HP 300", // 선택값 2
        "HP 600" // 선택값 3
    };
    private static readonly string[] ArmorLabels = new string[] // 방어율 선택 표시
    {
        "방어율 0%", // 선택값 1
        "방어율 50%" // 선택값 2
    };
    private static readonly string[] PolicyLabels = new string[] // 부위 선택 표시
    {
        "부위 자유", // 선택값 1
        "몸통만 적중", // 선택값 2
        "머리만 적중" // 선택값 3
    };

    public void Configure(BalanceSessionRunner owner, BalanceObservationRecorder logger) // 에디터 화면 연결
    {
        runner = owner; // 시험 관리자 저장
        observation = logger; // 실전 기록기 저장
    }

    private void Start() // 첫 시험은 가장 가까운 고정 표적으로 준비
    {
        if (runner == null || runner.Lanes == null) // 설치 참조 확인
        {
            return; // 미설정 상태 유지
        }
        float nearest = float.PositiveInfinity; // 가장 가까운 거리 후보
        for (int i = 0; i < runner.Lanes.Length; i++) // 실제 레인 목록 순회
        {
            TrainingCenterLane lane = runner.Lanes[i]; // 현재 사격선
            if (lane != null && lane.Target != null && lane.Target.Travel == 0f && lane.Distance < nearest) // 가까운 고정 표적 확인
            {
                laneIndex = i; // 기본 시험 레인 선택
                nearest = lane.Distance; // 현재 최소 거리 저장
            }
        }
    }

    private void Update() // 입력 에셋을 바꾸지 않는 검사 화면 키
    {
        if (Keyboard.current == null || !Application.isFocused) // 입력 장치와 창 초점 확인
        {
            return; // 다른 창의 키 입력 제외
        }
        if (Keyboard.current.f8Key.wasPressedThisFrame) // F8 화면 전환
        {
            if (IsOpen) // 기존 창 확인
            {
                Close(false); // 원래 조작 상태 복구
            }
            else // 새 창 요청
            {
                Open(); // 기존 전투와 분리된 설정 표시
            }
        }
        else if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) // 설정 창 닫기
        {
            Close(false); // 원래 입력 복원
        }
    }

    public void Open() // 실제 시험 중단과 안전한 설정 진입
    {
        if (IsOpen || runner == null || runner.Center == null || runner.Center.Player == null) // 완성된 계측 구성 확인
        {
            return; // 중복 창 방지
        }
        GameObject user = runner.Center.Player; // 원래 플레이어
        runner.Abort("사용자가 결과 화면을 열어 시험 중단"); // 진행 기록을 실패와 구분해 저장
        user.GetComponent<PlayerFirearmController>()?.Interrupt(); // 재장전과 발사 예약 정리
        if (!TrainingCenterRoot.CanUse(user)) // 암살과 자세 붕괴 등 기존 제한 확인
        {
            notice = "공격·암살·장비 동작이 끝난 뒤 F8을 누르세요."; // 현재 조건 안내
            return; // 특수행동 강제 해제 금지
        }
        PlayerInput input = user.GetComponent<PlayerInput>(); // 실제 입력 컴포넌트
        suspendedMap = input != null && input.actions != null ? input.actions.FindActionMap("Player", false) : null; // 사용할 입력 맵만 조회
        if (suspendedMap == null || !suspendedMap.enabled) // 다른 메뉴가 입력을 소유한 경우 확인
        {
            notice = "게임 입력이 활성화된 상태에서 F8을 누르세요."; // 다른 메뉴와 충돌 방지
            return; // 기존 비활성 상태 보존
        }
        runner.Center.StopTrials(); // 설정 창 동안 적 시험 종료
        savedLock = Cursor.lockState; // 사용자의 이전 커서 상태
        savedVisible = Cursor.visible; // 사용자의 이전 표시 상태
        mapWasEnabled = suspendedMap.enabled; // 복원할 실제 입력 상태
        suspendedMap.Disable(); // 설정 클릭이 공격으로 들어가지 않도록 처리
        cameraRig = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null; // 클릭 시 커서를 잠그는 기존 컴포넌트
        cameraWasEnabled = cameraRig != null && cameraRig.enabled; // 기존 활성 상태
        if (cameraRig != null) // 시점 제어 확인
        {
            cameraRig.enabled = false; // UI 클릭을 커서 잠금으로 오인하지 않도록 일시 중단
        }
        user.GetComponent<PlayerMovement>()?.SetHorizontalVelocity(Vector3.zero); // 창 뒤에서 이전 이동 관성 제거
        Cursor.lockState = CursorLockMode.None; // 설정 화면 마우스 사용
        Cursor.visible = true; // 마우스 표시
        IsOpen = true; // 화면 열기 완료
    }

    public void Close(bool startPlaying) // 입력과 카메라 상태 복원
    {
        if (!IsOpen) // 실제로 연 창인지 확인
        {
            return; // 다른 메뉴 입력 변경 방지
        }
        IsOpen = false; // 창 표시 종료
        if (suspendedMap != null && mapWasEnabled) // 자신이 중단한 맵만 복구
        {
            suspendedMap.Enable(); // 원래 게임 입력 재개
        }
        if (cameraRig != null) // 카메라 참조 확인
        {
            cameraRig.enabled = cameraWasEnabled; // 원래 제어 상태 복원
        }
        Cursor.lockState = startPlaying ? CursorLockMode.Locked : savedLock; // 시작 버튼은 실제 게임 조작으로 전환
        Cursor.visible = startPlaying ? false : savedVisible; // 이전 커서 표시 복원
        suspendedMap = null; // 이전 맵 참조 해제
    }

    private void OnGUI() // 기존 HUD와 떨어진 계측 표시
    {
        if (runner == null) // 관리자 존재 확인
        {
            return; // 미설정 화면 생략
        }
        Matrix4x4 oldMatrix = GUI.matrix; // 다른 화면 크기 설정 보존
        Color oldColor = GUI.color; // 다른 화면 색상 보존
        try // 표시 후 상태 복원 보장
        {
            float scale = Mathf.Max(0.1f, Mathf.Min(1f, Screen.width / 1020f, Screen.height / 730f)); // 작은 Game 화면에서도 읽을 수 있는 축소
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); // 계측 패널에만 크기 적용
            float width = Screen.width / scale; // 보정한 화면 너비
            float height = Screen.height / scale; // 보정한 화면 높이
            if (!IsOpen) // 게임 중 작은 안내만 표시
            {
                string info = runner.IsActive ? "계측 중 · " + runner.Current.weaponName + " / " + runner.Current.stance + "\n발사 " + runner.Current.shots + " / 지정 표적 명중 " + runner.Current.hitShots + "\nF8: 중단·결과 확인" : "F8: 총기 비교 시험 / 결과\n" + runner.Message; // 일반 HUD와 중복하지 않는 결과
                GUI.Box(new Rect(14f, 14f, 450f, runner.IsActive ? 84f : 66f), info); // 왼쪽 위 소형 상태창
                return; // 큰 설정 창 생략
            }
            Rect panel = new Rect((width - 980f) * 0.5f, (height - 700f) * 0.5f, 980f, 700f); // 중앙 설정 영역
            GUI.Box(panel, GUIContent.none); // 한 개의 설정 창 배경
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 24f)); // 내부 여백
            scroll = GUILayout.BeginScrollView(scroll); // 작은 화면 결과 스크롤
            DrawControls(); // 시험 선택과 실행
            DrawSummary(); // 동일 조건 완료 결과 비교
            GUILayout.Space(10f); // 하단 기록 구분
            GUILayout.Label("최근 결과 — 중단·조건 위반·시간초과는 완료 평균에서 제외"); // 미완료 처리 기준
            int count = 0; // 화면 결과 개수
            foreach (BalanceTrialRecord r in runner.History) // 최근 저장 결과 순회
            {
                GUILayout.Label(r.weaponName + " | " + r.status + " | " + (r.ttk >= 0 ? r.ttk.ToString("0.00") + "초" : "TTK 없음") + " | " + r.reason); // 실제 기록만 표시
                if (++count >= 6) // 화면 높이 제한
                {
                    break; // 이후 결과는 파일에서 확인
                }
            }
            GUILayout.Label(runner.Message); // 최신 저장 또는 실패 안내
            if (observation != null) // 실전 로그 상태 확인
            {
                GUILayout.Label(observation.Message); // 실제 AI 기록 상태
            }
            GUILayout.Label("결과 폴더: " + BalanceReportStore.DirectoryPath); // 로컬 실제 저장 위치
            if (GUILayout.Button("결과 폴더 경로 복사")) // 경로 조회 기능
            {
                GUIUtility.systemCopyBuffer = BalanceReportStore.DirectoryPath; // 사용자가 탐색기에 붙여넣을 경로
            }
            GUILayout.Label(notice); // 입력 제한과 오류 안내
            if (GUILayout.Button("닫기 · F8 / Esc")) // 단일 종료 버튼
            {
                Close(false); // 기존 입력과 카메라 복원
            }
            GUILayout.EndScrollView(); // 내용 스크롤 마무리
            GUILayout.EndArea(); // 설정 영역 마무리
        }
        finally // 기존 HUD 표시 상태 복원
        {
            GUI.matrix = oldMatrix; // 화면 변환 복구
            GUI.color = oldColor; // 색상 복구
        }
    }

    private void DrawControls() // 시험 조건 선택
    {
        GUILayout.Label("DAY 15  |  BALANCE LAB — 같은 조건의 실제 결과 비교"); // 화면 제목
        GUILayout.Label("맵과 총기 원본 수치는 유지 · 첫 발사부터 제압까지 측정 · 60초 제한"); // 시험 범위 안내
        if (runner.Weapons == null || runner.Weapons.Length == 0 || runner.Lanes == null || runner.Lanes.Length == 0) // 설치 완료 확인
        {
            GUILayout.Label("Day 15 Setup Balance Lab 메뉴를 먼저 실행하세요."); // 필수 설정 안내
            return; // 빈 선택 목록 처리 생략
        }
        string[] gunNames = new string[runner.Weapons.Length]; // 실제 등록 총기 표시
        for (int i = 0; i < gunNames.Length; i++) // 등록 순서 유지
        {
            gunNames[i] = runner.Weapons[i] != null ? runner.Weapons[i].DisplayName : "누락"; // 실제 에셋 이름
        }
        weaponIndex = GUILayout.SelectionGrid(weaponIndex, gunNames, 5); // 다섯 총기 선택
        string[] laneNames = new string[runner.Lanes.Length]; // 기존 사격선 표시
        for (int i = 0; i < laneNames.Length; i++) // 실제 레인 순회
        {
            TrainingCenterLane lane = runner.Lanes[i]; // 실제 사격선
            laneNames[i] = lane != null && lane.Target != null ? (i + 1) + ": " + lane.Distance.ToString("0") + "m " + (lane.Target.Travel > 0 ? "이동" : "고정") : "누락"; // 실제 거리와 이동 유형
        }
        GUILayout.Label("시험 레인 — 선택한 표적만 임시 복사본으로 교체하고 종료 시 원본 복원"); // 원본 보존 안내
        laneIndex = GUILayout.SelectionGrid(laneIndex, laneNames, 5); // 기존 아홉 레인 선택
        hpIndex = GUILayout.SelectionGrid(hpIndex, HealthLabels, 3); // 같은 표적 체력 선택
        armorIndex = GUILayout.SelectionGrid(armorIndex, ArmorLabels, 2); // 같은 피해 감소율 선택
        stanceIndex = GUILayout.SelectionGrid(stanceIndex, StanceLabels, 4); // 같은 발사 조건 선택
        policyIndex = GUILayout.SelectionGrid(policyIndex, PolicyLabels, 3); // 부위별 시험 결과 분리
        GUILayout.Label("정지 시험은 이동 없이 사격 / 조준 시험은 확대 완료 후 사격 / 이동 시험은 A·D로 좌우 이동하며 사격"); // 조건 위반 예방
        GUILayout.Label("무기 변경·보급·다른 공격·앞뒤 이동·창 초점 이탈은 무효 기록 / 재장전은 기존 T 사용"); // 비교 데이터 오염 기준
        if (GUILayout.Button("선택한 조건으로 시험 시작", GUILayout.Height(36f))) // 실제 시험 진입
        {
            if (runner.TryStart(weaponIndex, laneIndex, HealthOptions[hpIndex], armorIndex == 0 ? 0f : 0.5f, (BalanceStance)stanceIndex, (BalanceHitPolicy)policyIndex)) // 선택 조건 적용
            {
                notice = ""; // 이전 실패 안내 제거
                Close(true); // 마우스 클릭과 사격 입력 분리
            }
        }
    }

    private void DrawSummary() // 같은 조건과 같은 설정 버전만 평균 비교
    {
        if (runner.Weapons == null || runner.Lanes == null || runner.Lanes.Length == 0) // 비교 자료 참조 확인
        {
            return; // 미설정 표시 생략
        }
        GUILayout.Space(10f); // 비교 영역 구분
        GUILayout.Label("현재 조건 완료 평균 — 무기 수치가 바뀐 이전 기록은 제외"); // 비교 기준 안내
        string profile = runner.ProfileKey(laneIndex, HealthOptions[hpIndex], armorIndex == 0 ? 0f : 0.5f, (BalanceStance)stanceIndex, (BalanceHitPolicy)policyIndex); // 선택 조건 식별
        foreach (FirearmDefinition weapon in runner.Weapons) // 현재 무기별 비교
        {
            if (weapon == null || weapon.Stats == null) // 누락 에셋 확인
            {
                continue; // 미완성 무기 제외
            }
            PlayerFirearmController gun = runner.Center.Player.GetComponent<PlayerFirearmController>(); // 현재 부착 상태 확인
            bool suppressed = gun != null && gun.Definition == weapon && gun.IsSuppressed; // 선택 무기의 실제 부착 상태
            string fingerprint = BalanceReportStore.Hash(BalanceReportStore.Snapshot(weapon, suppressed)); // 현재 설정 버전
            int count = 0; // 완료 표본 수
            double ttk = 0; // 처치 시간 합계
            double shots = 0; // 사용 탄약 합계
            double accuracy = 0; // 발사 명중률 합계
            foreach (BalanceTrialRecord r in runner.History) // 저장 결과 순회
            {
                if (r.status == "Completed" && r.ttk >= 0 && r.profileKey == profile && r.weaponId == weapon.Stats.Id && r.weaponFingerprint == fingerprint) // 조건과 설정과 완료 여부 모두 동일
                {
                    count++; // 유효한 완료 표본 수
                    ttk += r.ttk; // 완료 시간 누적
                    shots += r.shots; // 처치까지 소비한 탄약 누적
                    accuracy += BalanceTrialMetrics.Percent(r.hitShots, r.shots); // 시험별 명중률 누적
                }
            }
            GUILayout.Label(weapon.DisplayName + (count == 0 ? " | 같은 조건 완료 기록 없음" : " | " + count + "회 | 평균 TTK " + (ttk / count).ToString("0.00") + "초 | 탄약 " + (shots / count).ToString("0.0") + "발 | 명중 " + (accuracy / count).ToString("0.0") + "%")); // 실제 계산한 평균만 표시
        }
    }

    private void OnDisable() // 화면 종료 중 조작 잠김 방지
    {
        Close(false); // 자신이 중단한 입력과 카메라만 복원
    }
}
