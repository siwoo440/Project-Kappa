#if UNITY_EDITOR // 에디터 전용 컴파일
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.SceneManagement; // 씬 기능

public static class ProjectKDay4VisualUpgrade // 훈련장 시각 강화 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string SessionKey = "ProjectK.Day4.VisualUpgrade.SessionApplied"; // 세션 적용 키

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoUpgrade() // 자동 업그레이드 예약
    {
        if (System.IO.File.Exists("Assets/_Project/Editor/ProjectKDay9Setup.cs")) // 9일차 이후 자동 재생성 방지
        {
            return; // 기존 테스트 씬과 장비 배치 보존
        }

        if (SessionState.GetBool(SessionKey, false)) // 세션 적용 여부 확인
        {
            return; // 중복 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += UpgradeVisuals; // 지연 실행 등록
    }

    [MenuItem("Project K/Day 4/Upgrade Training Ground Visuals")] // 메뉴 항목 등록
    public static void UpgradeVisuals() // 시각 업그레이드 적용
    {
        Scene testScene = SceneManager.GetSceneByPath(TestScenePath); // 테스트 씬 조회
        bool wasLoaded = testScene.IsValid() && testScene.isLoaded; // 기존 로드 상태 확인

        if (!wasLoaded) // 테스트 씬 미로드 확인
        {
            testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        Material darkMaterial = GetOrCreateMaterial("Detail_Dark", new Color(0.15f, 0.18f, 0.22f)); // 짙은 재질 확보
        Material lightMaterial = GetOrCreateMaterial("Detail_Light", new Color(0.72f, 0.78f, 0.84f)); // 밝은 재질 확보
        Material cyanMaterial = GetOrCreateMaterial("Detail_Cyan", new Color(0.12f, 0.84f, 0.95f)); // 청록 재질 확보
        Material greenMaterial = GetOrCreateMaterial("Detail_Green", new Color(0.24f, 0.82f, 0.38f)); // 녹색 재질 확보
        Material redMaterial = GetOrCreateMaterial("Detail_Red", new Color(0.92f, 0.22f, 0.28f)); // 적색 재질 확보
        Material yellowMaterial = GetOrCreateMaterial("Detail_Yellow", new Color(1.00f, 0.84f, 0.18f)); // 황색 재질 확보
        Material purpleMaterial = GetOrCreateMaterial("Detail_Purple", new Color(0.55f, 0.38f, 0.95f)); // 보라 재질 확보

        UpgradeSectionMarker(testScene, "StartZone", "START", cyanMaterial, darkMaterial); // 시작 구역 표식 강화
        UpgradeSectionMarker(testScene, "MovementLane", "MOVE", greenMaterial, darkMaterial); // 이동 구역 표식 강화
        UpgradeSectionMarker(testScene, "CameraCorridor", "CAM", cyanMaterial, darkMaterial); // 카메라 구역 표식 강화
        UpgradeSectionMarker(testScene, "WallRunZone", "WALL RUN", redMaterial, darkMaterial); // 벽 달리기 구역 표식 강화
        UpgradeSectionMarker(testScene, "WallJumpZone", "WALL JUMP", yellowMaterial, darkMaterial); // 벽 점프 구역 표식 강화
        UpgradeSectionMarker(testScene, "WallClimbZone", "WALL CLIMB", purpleMaterial, darkMaterial); // 벽 오르기 구역 표식 강화
        UpgradeSectionMarker(testScene, "LedgeZone", "LEDGE", cyanMaterial, darkMaterial); // 난간 구역 표식 강화
        UpgradeSectionMarker(testScene, "StealthPreviewZone", "STEALTH", greenMaterial, darkMaterial); // 잠입 구역 표식 강화
        UpgradeSectionMarker(testScene, "CombatPreviewZone", "COMBAT", redMaterial, darkMaterial); // 전투 구역 표식 강화
        UpgradeSectionMarker(testScene, "UtilityPreviewZone", "UTILITY", yellowMaterial, darkMaterial); // 기능 구역 표식 강화

        UpgradeMissionTerminal(testScene, darkMaterial, cyanMaterial, lightMaterial); // 미션 단말기 강화
        UpgradeSaveStation(testScene, darkMaterial, greenMaterial, lightMaterial); // 저장 장치 강화
        UpgradeShopKiosk(testScene, darkMaterial, yellowMaterial, lightMaterial, cyanMaterial); // 상점 키오스크 강화
        UpgradeNoiseEmitter(testScene, darkMaterial, yellowMaterial, redMaterial); // 소음 발생기 강화

        UpgradeDetectionGuard(testScene, "DetectionGuard_A", darkMaterial, greenMaterial, redMaterial); // 센서 경비 A 강화
        UpgradeDetectionGuard(testScene, "DetectionGuard_B", darkMaterial, greenMaterial, redMaterial); // 센서 경비 B 강화

        UpgradeTargetDummy(testScene, "TargetDummyA", darkMaterial, redMaterial, lightMaterial); // 표적 더미 A 강화
        UpgradeTargetDummy(testScene, "TargetDummyB", darkMaterial, redMaterial, lightMaterial); // 표적 더미 B 강화
        UpgradeTargetDummy(testScene, "TargetDummyC", darkMaterial, redMaterial, lightMaterial); // 표적 더미 C 강화

        UpgradeCoverObject(testScene, "CoverA", darkMaterial, lightMaterial); // 엄폐물 A 강화
        UpgradeCoverObject(testScene, "CoverB", darkMaterial, lightMaterial); // 엄폐물 B 강화
        UpgradeCoverObject(testScene, "ChestCoverA", darkMaterial, lightMaterial); // 엄폐물 C 강화
        UpgradeCoverObject(testScene, "ChestCoverB", darkMaterial, lightMaterial); // 엄폐물 D 강화
        UpgradeWeaponStand(testScene, "WeaponStandA", darkMaterial, cyanMaterial); // 무기 거치대 A 강화
        UpgradeWeaponStand(testScene, "WeaponStandB", darkMaterial, cyanMaterial); // 무기 거치대 B 강화

        EditorSceneManager.MarkSceneDirty(testScene); // 씬 변경 표시
        EditorSceneManager.SaveScene(testScene); // 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 저장
        AssetDatabase.Refresh(); // 에셋 새로고침

        if (!wasLoaded) // 임시 로드 상태 확인
        {
            EditorSceneManager.CloseScene(testScene, true); // 테스트 씬 닫기
        }

        Debug.Log("Project K Day 4 visual upgrade complete."); // 완료 로그 출력
    }

    private static void UpgradeSectionMarker(Scene scene, string rootName, string title, Material accentMaterial, Material darkMaterial) // 구역 게이트 생성
    {
        GameObject root = FindObjectByName(scene, rootName); // 구역 루트 조회
        if (root == null) // 구역 루트 확인
        {
            return; // 구역 생성 중단
        }

        Transform visualRoot = EnsureVisualRoot(root.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("GateLeft", PrimitiveType.Cube, visualRoot, new Vector3(-3.4f, 2.2f, -5.2f), new Vector3(0.35f, 4.4f, 0.35f), darkMaterial); // 좌측 기둥 생성
        CreatePrimitive("GateRight", PrimitiveType.Cube, visualRoot, new Vector3(3.4f, 2.2f, -5.2f), new Vector3(0.35f, 4.4f, 0.35f), darkMaterial); // 우측 기둥 생성
        CreatePrimitive("GateTop", PrimitiveType.Cube, visualRoot, new Vector3(0f, 4.25f, -5.2f), new Vector3(7.4f, 0.35f, 0.35f), accentMaterial); // 상단 게이트 생성
        CreatePrimitive("GatePanel", PrimitiveType.Cube, visualRoot, new Vector3(0f, 3.25f, -5.0f), new Vector3(4.6f, 1.3f, 0.12f), accentMaterial); // 전면 패널 생성
        CreatePrimitive("GateTrim", PrimitiveType.Cube, visualRoot, new Vector3(0f, 3.25f, -4.92f), new Vector3(4.2f, 0.12f, 0.06f), darkMaterial); // 전면 장식 생성
        CreateText("Title", visualRoot, title, new Vector3(0f, 3.25f, -4.84f), Color.black, 48, 0.13f); // 제목 텍스트 생성
        CreatePrimitive("BaseStrip", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.04f, -5.1f), new Vector3(7.8f, 0.08f, 0.9f), accentMaterial); // 바닥 스트립 생성
    }

    private static void UpgradeMissionTerminal(Scene scene, Material bodyMaterial, Material screenMaterial, Material trimMaterial) // 미션 단말기 외형 강화
    {
        GameObject target = FindObjectByName(scene, "MissionTerminal"); // 미션 단말기 조회
        if (target == null) // 단말기 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("Pedestal", PrimitiveType.Cube, visualRoot, new Vector3(0f, -0.1f, 0f), new Vector3(1.4f, 2.0f, 1.0f), bodyMaterial); // 하단 본체 생성
        CreatePrimitive("ScreenArm", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.35f, -0.1f), new Vector3(0.25f, 0.9f, 0.25f), bodyMaterial); // 화면 지지대 생성
        CreatePrimitive("ScreenBody", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.15f, -0.08f), new Vector3(1.3f, 0.85f, 0.18f), bodyMaterial, new Vector3(18f, 0f, 0f)); // 화면 본체 생성
        CreatePrimitive("ScreenGlow", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.12f, -0.16f), new Vector3(1.12f, 0.62f, 0.03f), screenMaterial, new Vector3(18f, 0f, 0f)); // 화면 패널 생성
        CreatePrimitive("KeyboardShelf", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.25f, 0.28f), new Vector3(1.18f, 0.12f, 0.52f), trimMaterial, new Vector3(18f, 0f, 0f)); // 키보드 선반 생성
        CreatePrimitive("Antenna", PrimitiveType.Cylinder, visualRoot, new Vector3(0.45f, 2.55f, 0.18f), new Vector3(0.04f, 0.34f, 0.04f), trimMaterial); // 안테나 생성
        CreatePrimitive("SignalOrb", PrimitiveType.Sphere, visualRoot, new Vector3(0.45f, 2.94f, 0.18f), new Vector3(0.16f, 0.16f, 0.16f), screenMaterial); // 신호 구체 생성
        CreateText("Label", visualRoot, "MISSION\nTERMINAL", new Vector3(0f, 3.15f, 0f), screenMaterial.color, 36, 0.08f); // 라벨 텍스트 생성
    }

    private static void UpgradeSaveStation(Scene scene, Material bodyMaterial, Material glowMaterial, Material trimMaterial) // 저장 장치 외형 강화
    {
        GameObject target = FindObjectByName(scene, "SaveStation"); // 저장 장치 조회
        if (target == null) // 저장 장치 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("BaseCore", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.15f, 0.8f), bodyMaterial); // 하단 받침 생성
        CreatePrimitive("MainTower", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.4f, 0f), new Vector3(0.34f, 1.1f, 0.34f), bodyMaterial); // 메인 타워 생성
        CreatePrimitive("Crystal", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.85f, 0f), new Vector3(0.78f, 0.78f, 0.78f), glowMaterial); // 저장 구체 생성
        CreatePrimitive("RingA", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 2.85f, 0f), new Vector3(0.92f, 0.02f, 0.92f), trimMaterial, new Vector3(90f, 0f, 0f)); // 가로 고리 생성
        CreatePrimitive("RingB", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 2.85f, 0f), new Vector3(0.92f, 0.02f, 0.92f), trimMaterial, new Vector3(0f, 0f, 90f)); // 세로 고리 생성
        CreatePrimitive("SidePanelA", PrimitiveType.Cube, visualRoot, new Vector3(-0.5f, 1.2f, 0f), new Vector3(0.18f, 0.95f, 0.5f), trimMaterial); // 좌측 패널 생성
        CreatePrimitive("SidePanelB", PrimitiveType.Cube, visualRoot, new Vector3(0.5f, 1.2f, 0f), new Vector3(0.18f, 0.95f, 0.5f), trimMaterial); // 우측 패널 생성
        CreateText("Label", visualRoot, "SAVE", new Vector3(0f, 3.7f, 0f), glowMaterial.color, 40, 0.09f); // 라벨 텍스트 생성
    }

    private static void UpgradeShopKiosk(Scene scene, Material bodyMaterial, Material accentMaterial, Material trimMaterial, Material screenMaterial) // 상점 키오스크 외형 강화
    {
        GameObject target = FindObjectByName(scene, "ShopKiosk"); // 상점 키오스크 조회
        if (target == null) // 상점 키오스크 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("Counter", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.55f, 0f), new Vector3(1.7f, 1.1f, 1.05f), bodyMaterial); // 카운터 생성
        CreatePrimitive("ShelfBack", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.8f, -0.3f), new Vector3(1.65f, 1.5f, 0.22f), bodyMaterial); // 후면 선반 생성
        CreatePrimitive("Roof", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.85f, 0f), new Vector3(2.1f, 0.18f, 1.6f), accentMaterial); // 지붕 생성
        CreatePrimitive("ScreenLeft", PrimitiveType.Cube, visualRoot, new Vector3(-0.42f, 1.85f, 0.18f), new Vector3(0.5f, 0.4f, 0.05f), screenMaterial); // 좌측 화면 생성
        CreatePrimitive("ScreenRight", PrimitiveType.Cube, visualRoot, new Vector3(0.42f, 1.85f, 0.18f), new Vector3(0.5f, 0.4f, 0.05f), screenMaterial); // 우측 화면 생성
        CreatePrimitive("CrateA", PrimitiveType.Cube, visualRoot, new Vector3(-0.48f, 0.35f, -0.42f), new Vector3(0.36f, 0.36f, 0.36f), trimMaterial); // 좌측 상자 생성
        CreatePrimitive("CrateB", PrimitiveType.Cube, visualRoot, new Vector3(0.48f, 0.35f, -0.42f), new Vector3(0.36f, 0.36f, 0.36f), trimMaterial); // 우측 상자 생성
        CreateText("Label", visualRoot, "SHOP", new Vector3(0f, 3.42f, 0f), accentMaterial.color, 40, 0.09f); // 라벨 텍스트 생성
    }

    private static void UpgradeNoiseEmitter(Scene scene, Material bodyMaterial, Material accentMaterial, Material dangerMaterial) // 소음 발생기 외형 강화
    {
        GameObject target = FindObjectByName(scene, "NoiseTestEmitter"); // 소음 발생기 조회
        if (target == null) // 소음 발생기 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("Base", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.12f, 0.45f), bodyMaterial); // 하단 받침 생성
        CreatePrimitive("Pole", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 0.95f, 0.08f), bodyMaterial); // 지지 기둥 생성
        CreatePrimitive("Speaker", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.08f, 0f), new Vector3(0.85f, 0.72f, 0.65f), accentMaterial); // 스피커 본체 생성
        CreatePrimitive("HornA", PrimitiveType.Cylinder, visualRoot, new Vector3(-0.18f, 2.05f, 0.35f), new Vector3(0.12f, 0.2f, 0.12f), dangerMaterial, new Vector3(90f, 0f, 0f)); // 좌측 혼 생성
        CreatePrimitive("HornB", PrimitiveType.Cylinder, visualRoot, new Vector3(0.18f, 2.05f, 0.35f), new Vector3(0.12f, 0.2f, 0.12f), dangerMaterial, new Vector3(90f, 0f, 0f)); // 우측 혼 생성
        CreateText("Label", visualRoot, "NOISE\nTEST", new Vector3(0f, 3.05f, 0f), accentMaterial.color, 34, 0.08f); // 라벨 텍스트 생성
    }

    private static void UpgradeDetectionGuard(Scene scene, string objectName, Material bodyMaterial, Material friendlyMaterial, Material dangerMaterial) // 센서 경비 외형 강화
    {
        GameObject target = FindObjectByName(scene, objectName); // 센서 경비 조회
        if (target == null) // 센서 경비 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("Base", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.12f, 0.45f), bodyMaterial); // 하단 받침 생성
        CreatePrimitive("LegLeft", PrimitiveType.Cylinder, visualRoot, new Vector3(-0.12f, 0.6f, 0f), new Vector3(0.06f, 0.45f, 0.06f), bodyMaterial); // 좌측 다리 생성
        CreatePrimitive("LegRight", PrimitiveType.Cylinder, visualRoot, new Vector3(0.12f, 0.6f, 0f), new Vector3(0.06f, 0.45f, 0.06f), bodyMaterial); // 우측 다리 생성
        CreatePrimitive("Torso", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.9f, 0.35f), friendlyMaterial); // 몸통 생성
        CreatePrimitive("Head", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.02f, 0f), new Vector3(0.38f, 0.38f, 0.38f), bodyMaterial); // 머리 생성
        CreatePrimitive("EyeBar", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.02f, 0.19f), new Vector3(0.24f, 0.05f, 0.04f), dangerMaterial); // 눈 패널 생성
        CreatePrimitive("ArmLeft", PrimitiveType.Cylinder, visualRoot, new Vector3(-0.42f, 1.42f, 0f), new Vector3(0.05f, 0.3f, 0.05f), bodyMaterial, new Vector3(0f, 0f, 90f)); // 좌측 팔 생성
        CreatePrimitive("ArmRight", PrimitiveType.Cylinder, visualRoot, new Vector3(0.42f, 1.42f, 0f), new Vector3(0.05f, 0.3f, 0.05f), bodyMaterial, new Vector3(0f, 0f, 90f)); // 우측 팔 생성
        CreatePrimitive("Antenna", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 2.46f, 0f), new Vector3(0.03f, 0.18f, 0.03f), bodyMaterial); // 안테나 생성
        CreatePrimitive("Beacon", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.7f, 0f), new Vector3(0.12f, 0.12f, 0.12f), dangerMaterial); // 경고등 생성
        CreateText("Label", visualRoot, objectName.Replace("_", "\n"), new Vector3(0f, 3.2f, 0f), friendlyMaterial.color, 28, 0.07f); // 라벨 텍스트 생성
    }

    private static void UpgradeTargetDummy(Scene scene, string objectName, Material baseMaterial, Material bodyMaterial, Material headMaterial) // 표적 더미 외형 강화
    {
        GameObject target = FindObjectByName(scene, objectName); // 표적 더미 조회
        if (target == null) // 표적 더미 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("Base", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.12f, 0f), new Vector3(0.46f, 0.12f, 0.46f), baseMaterial); // 하단 받침 생성
        CreatePrimitive("Pole", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.92f, 0f), new Vector3(0.07f, 0.8f, 0.07f), baseMaterial); // 지지 기둥 생성
        CreatePrimitive("Torso", PrimitiveType.Capsule, visualRoot, new Vector3(0f, 1.95f, 0f), new Vector3(0.55f, 0.65f, 0.38f), bodyMaterial); // 몸통 생성
        CreatePrimitive("Head", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 2.82f, 0f), new Vector3(0.38f, 0.38f, 0.38f), headMaterial); // 머리 생성
        CreatePrimitive("ShoulderBar", PrimitiveType.Cube, visualRoot, new Vector3(0f, 2.15f, 0f), new Vector3(0.95f, 0.12f, 0.12f), headMaterial); // 어깨 바 생성
        CreatePrimitive("ChestTarget", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.98f, 0.22f), new Vector3(0.20f, 0.04f, 0.20f), headMaterial, new Vector3(90f, 0f, 0f)); // 흉부 표식 생성
        CreateText("Label", visualRoot, "TARGET", new Vector3(0f, 3.35f, 0f), headMaterial.color, 28, 0.07f); // 라벨 텍스트 생성
    }

    private static void UpgradeCoverObject(Scene scene, string objectName, Material bodyMaterial, Material trimMaterial) // 엄폐물 외형 강화
    {
        GameObject target = FindObjectByName(scene, objectName); // 엄폐물 조회
        if (target == null) // 엄폐물 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("TopTrim", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.94f, 0f), new Vector3(1.04f, 0.08f, 1.04f), trimMaterial); // 상단 장식 생성
        CreatePrimitive("LeftBrace", PrimitiveType.Cube, visualRoot, new Vector3(-0.42f, 0.42f, 0f), new Vector3(0.08f, 0.72f, 0.16f), bodyMaterial); // 좌측 보강대 생성
        CreatePrimitive("RightBrace", PrimitiveType.Cube, visualRoot, new Vector3(0.42f, 0.42f, 0f), new Vector3(0.08f, 0.72f, 0.16f), bodyMaterial); // 우측 보강대 생성
        CreateText("Label", visualRoot, "COVER", new Vector3(0f, 1.35f, 0f), trimMaterial.color, 26, 0.06f); // 라벨 텍스트 생성
    }

    private static void UpgradeWeaponStand(Scene scene, string objectName, Material bodyMaterial, Material glowMaterial) // 무기 거치대 외형 강화
    {
        GameObject target = FindObjectByName(scene, objectName); // 무기 거치대 조회
        if (target == null) // 무기 거치대 확인
        {
            return; // 강화 중단
        }

        Transform visualRoot = EnsureVisualRoot(target.transform); // 시각 루트 확보
        ClearChildren(visualRoot); // 기존 시각 요소 정리

        CreatePrimitive("StandBase", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.12f, 0.5f), bodyMaterial); // 받침대 생성
        CreatePrimitive("StandPole", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 0.85f, 0f), new Vector3(0.07f, 0.65f, 0.07f), bodyMaterial); // 기둥 생성
        CreatePrimitive("Rack", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.55f, 0f), new Vector3(0.75f, 0.12f, 0.22f), glowMaterial); // 거치대 생성
        CreatePrimitive("GunProxy", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.74f, 0f), new Vector3(0.9f, 0.10f, 0.12f), bodyMaterial); // 총기 모형 생성
        CreatePrimitive("GripProxy", PrimitiveType.Cube, visualRoot, new Vector3(-0.12f, 1.56f, -0.04f), new Vector3(0.10f, 0.26f, 0.08f), bodyMaterial); // 손잡이 모형 생성
        CreateText("Label", visualRoot, "WEAPON", new Vector3(0f, 2.2f, 0f), glowMaterial.color, 28, 0.07f); // 라벨 텍스트 생성
    }

    private static Transform EnsureVisualRoot(Transform target) // 시각 루트 확보
    {
        Transform visualRoot = target.Find("__DetailedVisuals"); // 기존 시각 루트 조회
        if (visualRoot == null) // 기존 시각 루트 확인
        {
            GameObject visualObject = new GameObject("__DetailedVisuals"); // 시각 루트 생성
            visualRoot = visualObject.transform; // 시각 루트 참조 저장
            visualRoot.SetParent(target, false); // 대상 오브젝트에 연결
            visualRoot.localPosition = Vector3.zero; // 로컬 위치 초기화
            visualRoot.localRotation = Quaternion.identity; // 로컬 회전 초기화
            visualRoot.localScale = Vector3.one; // 로컬 크기 초기화
        }

        return visualRoot; // 시각 루트 반환
    }

    private static void ClearChildren(Transform target) // 자식 시각 요소 삭제
    {
        for (int i = target.childCount - 1; i >= 0; i--) // 자식 객체 역순 순회
        {
            Object.DestroyImmediate(target.GetChild(i).gameObject); // 자식 객체 삭제
        }
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material) // 프리미티브 생성
    {
        return CreatePrimitive(name, type, parent, localPosition, localScale, material, Vector3.zero); // 기본 회전 생성 호출
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler) // 프리미티브 생성
    {
        GameObject primitive = GameObject.CreatePrimitive(type); // 프리미티브 오브젝트 생성
        primitive.name = name; // 오브젝트 이름 지정
        primitive.transform.SetParent(parent, false); // 부모 연결
        primitive.transform.localPosition = localPosition; // 로컬 위치 지정
        primitive.transform.localRotation = Quaternion.Euler(localEuler); // 로컬 회전 지정
        primitive.transform.localScale = localScale; // 로컬 크기 지정

        Renderer renderer = primitive.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null && material != null) // 재질 적용 가능 여부 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }

        Collider collider = primitive.GetComponent<Collider>(); // 콜라이더 조회
        if (collider != null) // 콜라이더 존재 여부 확인
        {
            Object.DestroyImmediate(collider); // 세부 장식 충돌 삭제
        }

        return primitive; // 생성 객체 반환
    }

    private static void CreateText(string name, Transform parent, string text, Vector3 localPosition, Color color, int fontSize, float characterSize) // 텍스트 생성
    {
        GameObject textObject = new GameObject(name); // 텍스트 객체 생성
        textObject.transform.SetParent(parent, false); // 부모 연결
        textObject.transform.localPosition = localPosition; // 로컬 위치 지정
        textObject.transform.localRotation = Quaternion.identity; // 로컬 회전 초기화
        textObject.transform.localScale = Vector3.one; // 로컬 크기 초기화

        TextMesh textMesh = textObject.AddComponent<TextMesh>(); // 텍스트 메쉬 추가
        textMesh.text = text; // 표시 문자열 지정
        textMesh.fontSize = fontSize; // 글꼴 크기 지정
        textMesh.characterSize = characterSize; // 문자 크기 지정
        textMesh.anchor = TextAnchor.MiddleCenter; // 기준점 지정
        textMesh.alignment = TextAlignment.Center; // 정렬 지정
        textMesh.color = color; // 글자 색상 지정
        textMesh.richText = false; // 리치 텍스트 비활성
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        string parentFolder = "Assets/_Project/Materials"; // 상위 폴더 경로
        string folderPath = "Assets/_Project/Materials/Generated"; // 생성 폴더 경로
        EnsureFolder("Assets", "_Project"); // 프로젝트 폴더 확보
        EnsureFolder("Assets/_Project", "Materials"); // 재질 폴더 확보
        EnsureFolder(parentFolder, "Generated"); // 생성 폴더 확보
        string assetPath = folderPath + "/" + fileName + ".mat"; // 재질 경로 계산

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 재질 조회
        if (material != null) // 기존 재질 존재 여부 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 셰이더 조회
        if (shader == null) // URP 셰이더 존재 여부 확인
        {
            shader = Shader.Find("Standard"); // 기본 셰이더 대체 조회
        }

        material = new Material(shader); // 새 재질 생성
        material.name = fileName; // 재질 이름 지정

        if (material.HasProperty("_BaseColor")) // URP 색상 속성 확인
        {
            material.SetColor("_BaseColor", color); // URP 색상 적용
        }

        if (material.HasProperty("_Color")) // 기본 색상 속성 확인
        {
            material.SetColor("_Color", color); // 기본 색상 적용
        }

        AssetDatabase.CreateAsset(material, assetPath); // 재질 에셋 생성
        return material; // 재질 반환
    }

    private static void EnsureFolder(string parentPath, string folderName) // 폴더 확보
    {
        string combinedPath = parentPath + "/" + folderName; // 대상 폴더 경로 계산
        if (!AssetDatabase.IsValidFolder(combinedPath)) // 대상 폴더 존재 여부 확인
        {
            AssetDatabase.CreateFolder(parentPath, folderName); // 대상 폴더 생성
        }
    }

    private static GameObject FindObjectByName(Scene scene, string objectName) // 씬 내부 이름 검색
    {
        GameObject[] roots = scene.GetRootGameObjects(); // 루트 객체 목록 조회
        for (int i = 0; i < roots.Length; i++) // 루트 객체 순회
        {
            Transform found = FindChildRecursive(roots[i].transform, objectName); // 하위 객체 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found.gameObject; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }

    private static Transform FindChildRecursive(Transform current, string objectName) // 자식 객체 재귀 검색
    {
        if (current.name == objectName) // 현재 객체 이름 확인
        {
            return current; // 현재 객체 반환
        }

        for (int i = 0; i < current.childCount; i++) // 자식 객체 순회
        {
            Transform found = FindChildRecursive(current.GetChild(i), objectName); // 자식 객체 재귀 검색
            if (found != null) // 검색 결과 확인
            {
                return found; // 검색 객체 반환
            }
        }

        return null; // 검색 실패 반환
    }
}
#endif // 에디터 전용 컴파일 종료
