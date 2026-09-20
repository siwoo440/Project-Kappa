#if UNITY_EDITOR // 에디터 전용 컴파일
using System.Collections.Generic; // 컬렉션 기능
using UnityEditor; // 유니티 에디터 기능
using UnityEditor.SceneManagement; // 에디터 씬 기능
using UnityEngine; // 유니티 기본 기능
using UnityEngine.InputSystem; // 입력 시스템 기능
using UnityEngine.SceneManagement; // 씬 구조 기능

public static class ProjectKDay3Setup // 3일차 자동 구성 도구
{
    private const string TestScenePath = "Assets/_Project/Scenes/Test.unity"; // 테스트 씬 경로
    private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions"; // 입력 에셋 경로
    private const string SessionKey = "ProjectK.Day3Setup.ZFightFixV2"; // 수정본 세션 적용 확인 키
    private const string ParkourLayerName = "ParkourSurface"; // 파쿠르 표면 레이어 이름
    private const string RootName = "__TrainingGround"; // 훈련장 루트 이름

    [InitializeOnLoadMethod] // 에디터 로드 자동 실행
    private static void ScheduleAutoSetup() // 자동 구성 예약
    {
        if (System.IO.File.Exists("Assets/_Project/Editor/ProjectKDay9Setup.cs")) // 9일차 이후 자동 재생성 방지
        {
            return; // 기존 테스트 씬과 장비 배치 보존
        }

        if (SessionState.GetBool(SessionKey, false)) // 현재 세션 적용 여부 확인
        {
            return; // 중복 자동 적용 방지
        }

        SessionState.SetBool(SessionKey, true); // 세션 적용 상태 저장
        EditorApplication.delayCall += ApplyDay3Setup; // 컴파일 종료 후 적용 예약
    }

    // 수동 구성 메뉴
    public static void ApplyDay3Setup() // 3일차 구성 적용
    {
        int parkourLayer = EnsureLayer(ParkourLayerName); // 파쿠르 레이어 확보
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath); // 입력 에셋 조회
        if (inputAsset != null) // 입력 에셋 확인
        {
            EnsureCrouchBinding(inputAsset); // 앉기 바인딩 보정
        }

        Scene testScene = SceneManager.GetSceneByPath(TestScenePath); // 기존 테스트 씬 조회
        bool sceneWasLoaded = testScene.IsValid() && testScene.isLoaded; // 기존 로드 상태 확인
        if (!sceneWasLoaded) // 테스트 씬 미로드 확인
        {
            testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive); // 테스트 씬 추가 로드
        }

        GameObject player = FindObjectByName(testScene, "Player"); // 플레이어 객체 조회
        GameObject cameraObject = FindObjectByName(testScene, "Main Camera"); // 메인 카메라 객체 조회
        if (player == null || cameraObject == null) // 필수 객체 확인
        {
            Debug.LogError("Project K Day 3 setup failed: Player or Main Camera was not found in Test scene."); // 오류 로그 출력
            if (!sceneWasLoaded) // 임시 로드 상태 확인
            {
                EditorSceneManager.CloseScene(testScene, true); // 임시 테스트 씬 닫기
            }
            return; // 구성 중단
        }

        EnsurePlayerComponents(player, cameraObject); // 플레이어 관련 구성 적용
        CleanupLegacyTestGeometry(testScene, player); // 기존 테스트 지오메트리 정리
        BuildTrainingGround(testScene, parkourLayer); // 훈련장 생성
        EditorSceneManager.MarkSceneDirty(testScene); // 테스트 씬 변경 표시
        EditorSceneManager.SaveScene(testScene); // 테스트 씬 저장
        AssetDatabase.SaveAssets(); // 에셋 변경 저장
        AssetDatabase.Refresh(); // 에셋 새로고침

        if (!sceneWasLoaded) // 임시 로드 상태 확인
        {
            EditorSceneManager.CloseScene(testScene, true); // 임시 테스트 씬 닫기
        }

        Debug.Log("Project K Day 3 parkour training ground setup complete."); // 완료 로그 출력
    }

    private static void EnsureCrouchBinding(InputActionAsset inputAsset) // 앉기 바인딩 보정
    {
        InputActionMap actionMap = inputAsset.FindActionMap("Player", false); // 플레이어 액션 맵 조회
        if (actionMap == null) // 액션 맵 확인
        {
            return; // 보정 중단
        }

        InputAction crouchAction = actionMap.FindAction("Crouch", false); // 앉기 액션 조회
        if (crouchAction == null) // 액션 확인
        {
            return; // 보정 중단
        }

        bool hasLeftCtrl = false; // Left Ctrl 존재 여부 초기화
        for (int i = 0; i < crouchAction.bindings.Count; i++) // 바인딩 순회
        {
            if (crouchAction.bindings[i].path == "<Keyboard>/leftCtrl") // Left Ctrl 바인딩 확인
            {
                hasLeftCtrl = true; // 존재 상태 저장
                break; // 순회 종료
            }
        }

        if (!hasLeftCtrl) // Left Ctrl 누락 확인
        {
            crouchAction.AddBinding("<Keyboard>/leftCtrl").WithGroup("Keyboard&Mouse"); // Left Ctrl 바인딩 추가
            EditorUtility.SetDirty(inputAsset); // 입력 에셋 변경 표시
        }
    }

    private static void EnsurePlayerComponents(GameObject player, GameObject cameraObject) // 플레이어 구성 보정
    {
        CharacterController controller = player.GetComponent<CharacterController>(); // 캐릭터 컨트롤러 조회
        if (controller == null) // 컨트롤러 누락 확인
        {
            controller = player.AddComponent<CharacterController>(); // 캐릭터 컨트롤러 추가
        }

        PlayerMovement movement = player.GetComponent<PlayerMovement>(); // 이동 스크립트 조회
        if (movement == null) // 이동 스크립트 누락 확인
        {
            movement = player.AddComponent<PlayerMovement>(); // 이동 스크립트 추가
        }

        MovementEnergy energy = player.GetComponent<MovementEnergy>(); // 에너지 스크립트 조회
        if (energy == null) // 에너지 스크립트 누락 확인
        {
            energy = player.AddComponent<MovementEnergy>(); // 에너지 스크립트 추가
        }

        PlayerParkour parkour = player.GetComponent<PlayerParkour>(); // 파쿠르 스크립트 조회
        if (parkour == null) // 파쿠르 스크립트 누락 확인
        {
            parkour = player.AddComponent<PlayerParkour>(); // 파쿠르 스크립트 추가
        }

        Camera cameraComponent = cameraObject.GetComponent<Camera>(); // 카메라 컴포넌트 조회
        ThirdPersonCamera thirdPersonCamera = cameraObject.GetComponent<ThirdPersonCamera>(); // 3인칭 카메라 조회
        PlayerInput playerInput = player.GetComponent<PlayerInput>(); // 플레이어 입력 조회
        if (thirdPersonCamera != null && playerInput != null) // 카메라 스크립트와 입력 확인
        {
            thirdPersonCamera.Configure(player.transform, playerInput); // 카메라 추적 정보 갱신
        }

        movement.Configure(cameraComponent); // 이동 기준 카메라 연결
        movement.SetSpawnPoint(new Vector3(0f, 1.2f, -34f), Quaternion.identity); // 시작 복귀 위치 지정
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, -34f), Quaternion.identity); // 플레이어 시작 위치 적용
        EditorUtility.SetDirty(player); // 플레이어 변경 표시
    }

    private static void CleanupLegacyTestGeometry(Scene scene, GameObject player) // 기존 테스트 지오메트리 정리
    {
        GameObject testRoot = FindObjectByName(scene, "TestRoot"); // 1일차 테스트 루트 조회
        if (testRoot == null) // 테스트 루트 확인
        {
            return; // 정리 처리 중단
        }

        List<GameObject> legacyObjects = new List<GameObject>(); // 삭제 대상 목록 생성
        for (int i = 0; i < testRoot.transform.childCount; i++) // 테스트 루트 자식 순회
        {
            GameObject child = testRoot.transform.GetChild(i).gameObject; // 현재 자식 조회
            if (child == player) // 플레이어 객체 확인
            {
                continue; // 플레이어 보존
            }

            legacyObjects.Add(child); // 기존 지오메트리 삭제 목록 추가
        }

        for (int i = 0; i < legacyObjects.Count; i++) // 삭제 대상 순회
        {
            Object.DestroyImmediate(legacyObjects[i]); // 기존 Ground·Obstacle 제거
        }
    }

    private static void BuildTrainingGround(Scene scene, int parkourLayer) // 훈련장 생성
    {
        GameObject root = FindObjectByName(scene, RootName); // 기존 루트 조회
        if (root == null) // 루트 누락 확인
        {
            root = new GameObject(RootName); // 루트 생성
            SceneManager.MoveGameObjectToScene(root, scene); // 테스트 씬으로 이동
        }

        ClearChildren(root.transform); // 기존 훈련장 초기화

        Material floorMaterial = GetOrCreateMaterial("TrainingFloor_Mat", new Color(0.14f, 0.16f, 0.18f)); // 바닥 재질 확보
        Material trimMaterial = GetOrCreateMaterial("TrainingTrim_Mat", new Color(0.22f, 0.26f, 0.30f)); // 장식 재질 확보
        Material accentMaterial = GetOrCreateMaterial("TrainingAccent_Mat", new Color(0.10f, 0.75f, 0.95f)); // 강조 재질 확보
        Material dangerMaterial = GetOrCreateMaterial("TrainingDanger_Mat", new Color(0.92f, 0.18f, 0.22f)); // 위험 재질 확보
        Material utilityMaterial = GetOrCreateMaterial("TrainingUtility_Mat", new Color(0.32f, 0.62f, 0.22f)); // 기능 재질 확보

        CreateBlock("MainFloor", PrimitiveType.Cube, root.transform, new Vector3(0f, -0.55f, 0f), new Vector3(40f, 1f, 90f), floorMaterial, -1); // 메인 바닥 생성
        CreateBlock("LeftBoundary", PrimitiveType.Cube, root.transform, new Vector3(-20.5f, 2.5f, 0f), new Vector3(1f, 5f, 90f), trimMaterial, -1); // 좌측 경계 생성
        CreateBlock("RightBoundary", PrimitiveType.Cube, root.transform, new Vector3(20.5f, 2.5f, 0f), new Vector3(1f, 5f, 90f), trimMaterial, -1); // 우측 경계 생성
        CreateBlock("BackBoundary", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.5f, -45.5f), new Vector3(40f, 5f, 1f), trimMaterial, -1); // 후방 경계 생성
        CreateBlock("FrontBoundary", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.5f, 45.5f), new Vector3(40f, 5f, 1f), trimMaterial, -1); // 전방 경계 생성

        Transform startZone = CreateSectionRoot(root.transform, "StartZone", new Vector3(0f, 0f, -34f)); // 시작 구역 루트 생성
        CreateBlock("StartPad", PrimitiveType.Cube, startZone, new Vector3(0f, 0.20f, 0f), new Vector3(7f, 0.3f, 7f), accentMaterial, -1); // 시작 패드 생성
        CreateBlock("SpawnFrameLeft", PrimitiveType.Cube, startZone, new Vector3(-3.4f, 2f, 0f), new Vector3(0.2f, 4f, 7f), trimMaterial, -1); // 좌측 프레임 생성
        CreateBlock("SpawnFrameRight", PrimitiveType.Cube, startZone, new Vector3(3.4f, 2f, 0f), new Vector3(0.2f, 4f, 7f), trimMaterial, -1); // 우측 프레임 생성
        CreateBlock("SpawnFrameTop", PrimitiveType.Cube, startZone, new Vector3(0f, 4f, 0f), new Vector3(7f, 0.2f, 7f), trimMaterial, -1); // 상단 프레임 생성

        Transform movementLane = CreateSectionRoot(root.transform, "MovementLane", new Vector3(0f, 0f, -22f)); // 이동 구간 루트 생성
        CreateBlock("LaneFloor", PrimitiveType.Cube, movementLane, new Vector3(0f, 0.15f, 0f), new Vector3(8f, 0.2f, 16f), trimMaterial, -1); // 이동 바닥 생성
        for (int i = 0; i < 6; i++) // 마킹 반복
        {
            CreateBlock($"SpeedMark_{i}", PrimitiveType.Cube, movementLane, new Vector3(0f, 0.27f, -6f + i * 2.4f), new Vector3(1.4f, 0.02f, 0.6f), accentMaterial, -1); // 속도 마킹 생성
        }
        CreateBlock("CrouchBar", PrimitiveType.Cube, movementLane, new Vector3(0f, 1.1f, 6f), new Vector3(7f, 0.2f, 0.8f), dangerMaterial, -1); // 앉기 바 생성
        CreateBlock("JumpStepA", PrimitiveType.Cube, movementLane, new Vector3(-2.2f, 0.6f, 2f), new Vector3(2f, 1.2f, 2f), utilityMaterial, -1); // 점프 계단 생성
        CreateBlock("JumpStepB", PrimitiveType.Cube, movementLane, new Vector3(2.2f, 1.1f, 4.5f), new Vector3(2f, 2.2f, 2f), utilityMaterial, -1); // 점프 계단 생성

        Transform cameraLane = CreateSectionRoot(root.transform, "CameraCorridor", new Vector3(0f, 0f, -5f)); // 카메라 구간 루트 생성
        CreateBlock("CorridorFloor", PrimitiveType.Cube, cameraLane, new Vector3(0f, 0.10f, 0f), new Vector3(6f, 0.1f, 12f), trimMaterial, -1); // 통로 바닥 생성
        CreateBlock("CorridorWallLeft", PrimitiveType.Cube, cameraLane, new Vector3(-2.8f, 1.8f, 0f), new Vector3(0.4f, 3.6f, 12f), trimMaterial, -1); // 좌측 통로 벽 생성
        CreateBlock("CorridorWallRight", PrimitiveType.Cube, cameraLane, new Vector3(2.8f, 1.8f, 0f), new Vector3(0.4f, 3.6f, 12f), trimMaterial, -1); // 우측 통로 벽 생성
        CreateBlock("CorridorCeiling", PrimitiveType.Cube, cameraLane, new Vector3(0f, 3.7f, 0f), new Vector3(6f, 0.2f, 12f), trimMaterial, -1); // 통로 천장 생성

        Transform wallRunZone = CreateSectionRoot(root.transform, "WallRunZone", new Vector3(0f, 0f, 12f)); // 벽 달리기 구간 루트 생성
        CreateBlock("RunFloor", PrimitiveType.Cube, wallRunZone, new Vector3(0f, 0.15f, 0f), new Vector3(10f, 0.2f, 16f), trimMaterial, -1); // 벽 달리기 바닥 생성
        CreateBlock("WallRunLeft", PrimitiveType.Cube, wallRunZone, new Vector3(-3.7f, 3f, 0f), new Vector3(0.6f, 6f, 16f), dangerMaterial, parkourLayer); // 좌측 벽 달리기 벽 생성
        CreateBlock("WallRunRight", PrimitiveType.Cube, wallRunZone, new Vector3(3.7f, 3f, 0f), new Vector3(0.6f, 6f, 16f), dangerMaterial, parkourLayer); // 우측 벽 달리기 벽 생성
        CreateBlock("WallRunGapA", PrimitiveType.Cube, wallRunZone, new Vector3(0f, -2.4f, -2f), new Vector3(3f, 4.8f, 5f), floorMaterial, -1); // 낙하 구간 생성
        CreateBlock("WallRunGapB", PrimitiveType.Cube, wallRunZone, new Vector3(0f, -2.4f, 6f), new Vector3(3f, 4.8f, 5f), floorMaterial, -1); // 낙하 구간 생성

        Transform wallJumpZone = CreateSectionRoot(root.transform, "WallJumpZone", new Vector3(-10f, 0f, 28f)); // 벽 점프 구간 루트 생성
        CreateBlock("JumpPitFloor", PrimitiveType.Cube, wallJumpZone, new Vector3(0f, 0.15f, 0f), new Vector3(8f, 0.2f, 14f), trimMaterial, -1); // 벽 점프 바닥 생성
        CreateBlock("JumpWallLeft", PrimitiveType.Cube, wallJumpZone, new Vector3(-2.2f, 4f, 0f), new Vector3(0.6f, 8f, 14f), accentMaterial, parkourLayer); // 좌측 점프 벽 생성
        CreateBlock("JumpWallRight", PrimitiveType.Cube, wallJumpZone, new Vector3(2.2f, 4.5f, 0f), new Vector3(0.6f, 9f, 14f), accentMaterial, parkourLayer); // 우측 점프 벽 생성
        CreateBlock("JumpGoalPlatform", PrimitiveType.Cube, wallJumpZone, new Vector3(0f, 7f, 5.8f), new Vector3(5f, 0.4f, 2.5f), utilityMaterial, -1); // 벽 점프 도착대 생성

        Transform wallClimbZone = CreateSectionRoot(root.transform, "WallClimbZone", new Vector3(10f, 0f, 28f)); // 벽 오르기 구간 루트 생성
        CreateBlock("ClimbBase", PrimitiveType.Cube, wallClimbZone, new Vector3(0f, 0.15f, -3f), new Vector3(8f, 0.2f, 8f), trimMaterial, -1); // 벽 오르기 바닥 생성
        CreateBlock("ClimbWall", PrimitiveType.Cube, wallClimbZone, new Vector3(0f, 5f, 0f), new Vector3(8f, 10f, 0.8f), dangerMaterial, parkourLayer); // 벽 오르기 벽 생성
        CreateBlock("ClimbTopDeck", PrimitiveType.Cube, wallClimbZone, new Vector3(0f, 10.4f, 1.2f), new Vector3(8f, 0.4f, 5f), utilityMaterial, -1); // 상단 덱 생성

        Transform ledgeZone = CreateSectionRoot(root.transform, "LedgeZone", new Vector3(0f, 0f, 36f)); // 난간 구간 루트 생성
        CreateBlock("LedgeWall", PrimitiveType.Cube, ledgeZone, new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 0.8f), accentMaterial, parkourLayer); // 난간 벽 생성
        CreateBlock("LedgeTop", PrimitiveType.Cube, ledgeZone, new Vector3(0f, 7.2f, 1f), new Vector3(12f, 0.4f, 4f), utilityMaterial, -1); // 난간 상단 생성
        CreateBlock("LedgeSideRailLeft", PrimitiveType.Cube, ledgeZone, new Vector3(-5.8f, 8.4f, 1f), new Vector3(0.2f, 2.4f, 4f), trimMaterial, -1); // 좌측 난간 장식 생성
        CreateBlock("LedgeSideRailRight", PrimitiveType.Cube, ledgeZone, new Vector3(5.8f, 8.4f, 1f), new Vector3(0.2f, 2.4f, 4f), trimMaterial, -1); // 우측 난간 장식 생성

        Transform stealthZone = CreateSectionRoot(root.transform, "StealthPreviewZone", new Vector3(-12f, 0f, -10f)); // 잠입 예고 구간 루트 생성
        CreateBlock("StealthFloor", PrimitiveType.Cube, stealthZone, new Vector3(0f, 0.10f, 0f), new Vector3(10f, 0.1f, 18f), trimMaterial, -1); // 잠입 바닥 생성
        CreateBlock("CoverA", PrimitiveType.Cube, stealthZone, new Vector3(-2.5f, 0.9f, -4f), new Vector3(2.5f, 1.8f, 1f), utilityMaterial, -1); // 엄폐물 생성
        CreateBlock("CoverB", PrimitiveType.Cube, stealthZone, new Vector3(2.2f, 0.9f, 2f), new Vector3(3f, 1.8f, 1f), utilityMaterial, -1); // 엄폐물 생성
        CreateCylinder("PatrolMarkerA", stealthZone, new Vector3(-3.5f, 0.3f, 6f), new Vector3(1f, 0.3f, 1f), accentMaterial, -1); // 순찰 표식 생성
        CreateCylinder("PatrolMarkerB", stealthZone, new Vector3(3.5f, 0.3f, 6f), new Vector3(1f, 0.3f, 1f), accentMaterial, -1); // 순찰 표식 생성
        CreateCylinder("CameraMarker", stealthZone, new Vector3(0f, 3.5f, 7.5f), new Vector3(0.6f, 0.6f, 0.6f), dangerMaterial, -1); // 카메라 위치 표식 생성

        Transform combatZone = CreateSectionRoot(root.transform, "CombatPreviewZone", new Vector3(12f, 0f, -10f)); // 전투 예고 구간 루트 생성
        CreateBlock("CombatFloor", PrimitiveType.Cube, combatZone, new Vector3(0f, 0.10f, 0f), new Vector3(10f, 0.1f, 18f), trimMaterial, -1); // 전투 바닥 생성
        CreateBlock("ChestCoverA", PrimitiveType.Cube, combatZone, new Vector3(-2.5f, 0.8f, -3f), new Vector3(2.5f, 1.6f, 1f), utilityMaterial, -1); // 엄폐물 생성
        CreateBlock("ChestCoverB", PrimitiveType.Cube, combatZone, new Vector3(2.5f, 0.8f, 3f), new Vector3(2.5f, 1.6f, 1f), utilityMaterial, -1); // 엄폐물 생성
        CreateCylinder("TargetDummyA", combatZone, new Vector3(-3f, 1.5f, 6f), new Vector3(1f, 1.5f, 1f), dangerMaterial, -1); // 표적 더미 생성
        CreateCylinder("TargetDummyB", combatZone, new Vector3(0f, 1.5f, 7.5f), new Vector3(1f, 1.5f, 1f), dangerMaterial, -1); // 표적 더미 생성
        CreateCylinder("TargetDummyC", combatZone, new Vector3(3f, 1.5f, 6f), new Vector3(1f, 1.5f, 1f), dangerMaterial, -1); // 표적 더미 생성
        CreateBlock("WeaponStandA", PrimitiveType.Cube, combatZone, new Vector3(-4f, 0.6f, -6f), new Vector3(1f, 1.2f, 1f), accentMaterial, -1); // 무기 거치대 생성
        CreateBlock("WeaponStandB", PrimitiveType.Cube, combatZone, new Vector3(4f, 0.6f, -6f), new Vector3(1f, 1.2f, 1f), accentMaterial, -1); // 무기 거치대 생성

        Transform utilityZone = CreateSectionRoot(root.transform, "UtilityPreviewZone", new Vector3(0f, 0f, -43f)); // 기능 예고 구간 루트 생성
        CreateBlock("SaveStation", PrimitiveType.Cube, utilityZone, new Vector3(-5f, 1.2f, 0f), new Vector3(2f, 2.4f, 1.2f), accentMaterial, -1); // 저장 장치 생성
        CreateBlock("MissionTerminal", PrimitiveType.Cube, utilityZone, new Vector3(0f, 1.4f, 0f), new Vector3(2f, 2.8f, 1.2f), utilityMaterial, -1); // 미션 단말기 생성
        CreateBlock("ShopKiosk", PrimitiveType.Cube, utilityZone, new Vector3(5f, 1.4f, 0f), new Vector3(2f, 2.8f, 1.2f), dangerMaterial, -1); // 상점 키오스크 생성

        EnsureDirectionalLight(scene, trimMaterial); // 조명 상태 보정
    }

    private static void EnsureDirectionalLight(Scene scene, Material helperMaterial) // 방향광 보정
    {
        GameObject lightObject = FindObjectByName(scene, "Directional Light"); // 방향광 조회
        if (lightObject == null) // 방향광 누락 확인
        {
            lightObject = new GameObject("Directional Light"); // 방향광 객체 생성
            SceneManager.MoveGameObjectToScene(lightObject, scene); // 테스트 씬으로 이동
            Light lightComponent = lightObject.AddComponent<Light>(); // 라이트 컴포넌트 추가
            lightComponent.type = LightType.Directional; // 방향광 타입 지정
            lightComponent.intensity = 1.2f; // 방향광 세기 지정
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // 방향광 회전 적용
        }

        GameObject lightGuide = FindObjectByName(scene, "LightGuide"); // 조명 장식 조회
        if (lightGuide == null) // 조명 장식 누락 확인
        {
            lightGuide = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 조명 장식 생성
            lightGuide.name = "LightGuide"; // 조명 장식 이름 지정
            SceneManager.MoveGameObjectToScene(lightGuide, scene); // 테스트 씬으로 이동
            lightGuide.transform.position = new Vector3(0f, 0.5f, -39f); // 조명 장식 위치 적용
            lightGuide.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f); // 조명 장식 크기 적용
            lightGuide.GetComponent<Renderer>().sharedMaterial = helperMaterial; // 조명 장식 재질 적용
        }
    }

    private static Transform CreateSectionRoot(Transform parent, string name, Vector3 position) // 구역 루트 생성
    {
        GameObject section = new GameObject(name); // 구역 객체 생성
        section.transform.SetParent(parent, false); // 부모 연결
        section.transform.localPosition = position; // 구역 위치 적용
        return section.transform; // 구역 트랜스폼 반환
    }

    private static GameObject CreateBlock(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, int layer) // 큐브형 오브젝트 생성
    {
        GameObject block = GameObject.CreatePrimitive(primitiveType); // 프리미티브 생성
        block.name = name; // 오브젝트 이름 지정
        block.transform.SetParent(parent, false); // 부모 연결
        block.transform.localPosition = localPosition; // 로컬 위치 적용
        block.transform.localRotation = Quaternion.identity; // 로컬 회전 초기화
        block.transform.localScale = localScale; // 로컬 크기 적용
        if (layer >= 0) // 레이어 적용 여부 확인
        {
            ApplyLayerRecursively(block, layer); // 레이어 적용
        }
        Renderer renderer = block.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null && material != null) // 재질 적용 조건 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }
        return block; // 생성 객체 반환
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, int layer) // 원통형 오브젝트 생성
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 원통 프리미티브 생성
        cylinder.name = name; // 오브젝트 이름 지정
        cylinder.transform.SetParent(parent, false); // 부모 연결
        cylinder.transform.localPosition = localPosition; // 로컬 위치 적용
        cylinder.transform.localRotation = Quaternion.identity; // 로컬 회전 초기화
        cylinder.transform.localScale = localScale; // 로컬 크기 적용
        if (layer >= 0) // 레이어 적용 여부 확인
        {
            ApplyLayerRecursively(cylinder, layer); // 레이어 적용
        }
        Renderer renderer = cylinder.GetComponent<Renderer>(); // 렌더러 조회
        if (renderer != null && material != null) // 재질 적용 조건 확인
        {
            renderer.sharedMaterial = material; // 재질 적용
        }
        return cylinder; // 생성 객체 반환
    }

    private static void ClearChildren(Transform target) // 자식 객체 전체 삭제
    {
        List<GameObject> children = new List<GameObject>(); // 삭제 목록 생성
        for (int i = 0; i < target.childCount; i++) // 자식 객체 순회
        {
            children.Add(target.GetChild(i).gameObject); // 삭제 목록 추가
        }

        for (int i = 0; i < children.Count; i++) // 삭제 목록 순회
        {
            Object.DestroyImmediate(children[i]); // 자식 객체 삭제
        }
    }

    private static void ApplyLayerRecursively(GameObject target, int layer) // 레이어 재귀 적용
    {
        target.layer = layer; // 현재 객체 레이어 적용
        for (int i = 0; i < target.transform.childCount; i++) // 자식 객체 순회
        {
            ApplyLayerRecursively(target.transform.GetChild(i).gameObject, layer); // 자식 객체 레이어 적용
        }
    }

    private static Material GetOrCreateMaterial(string fileName, Color color) // 재질 확보
    {
        string folderPath = "Assets/_Project/Materials/Generated"; // 재질 폴더 경로
        EnsureFolder("Assets/_Project/Materials", "Generated"); // 재질 폴더 확보
        string assetPath = $"{folderPath}/{fileName}.mat"; // 재질 경로 계산
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath); // 기존 재질 조회
        if (material != null) // 기존 재질 확인
        {
            return material; // 기존 재질 반환
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP 셰이더 조회
        if (shader == null) // URP 셰이더 누락 확인
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

        AssetDatabase.CreateAsset(material, assetPath); // 재질 에셋 저장
        return material; // 재질 반환
    }

    private static void EnsureFolder(string parentPath, string folderName) // 폴더 확보
    {
        if (!AssetDatabase.IsValidFolder(parentPath)) // 부모 폴더 확인
        {
            string rootParent = parentPath.Substring(0, parentPath.LastIndexOf('/')); // 상위 폴더 경로 계산
            string childName = parentPath.Substring(parentPath.LastIndexOf('/') + 1); // 현재 폴더 이름 계산
            EnsureFolder(rootParent, childName); // 부모 폴더 재귀 생성
        }

        string combinedPath = $"{parentPath}/{folderName}"; // 대상 폴더 경로 계산
        if (!AssetDatabase.IsValidFolder(combinedPath)) // 대상 폴더 확인
        {
            AssetDatabase.CreateFolder(parentPath, folderName); // 대상 폴더 생성
        }
    }

    private static int EnsureLayer(string layerName) // 레이어 확보
    {
        int layer = LayerMask.NameToLayer(layerName); // 기존 레이어 조회
        if (layer >= 0) // 기존 레이어 확인
        {
            return layer; // 기존 레이어 반환
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]); // 태그 매니저 조회
        SerializedProperty layersProperty = tagManager.FindProperty("layers"); // 레이어 속성 조회
        for (int i = 8; i < 32; i++) // 사용자 레이어 구간 순회
        {
            SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i); // 레이어 슬롯 조회
            if (string.IsNullOrEmpty(layerProperty.stringValue)) // 빈 슬롯 확인
            {
                layerProperty.stringValue = layerName; // 레이어 이름 저장
                tagManager.ApplyModifiedProperties(); // 변경 적용
                AssetDatabase.SaveAssets(); // 에셋 저장
                return i; // 새 레이어 인덱스 반환
            }
        }

        Debug.LogWarning($"Project K Day 3 setup: failed to reserve layer {layerName}."); // 레이어 확보 실패 로그 출력
        return -1; // 실패 반환
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

    private static Transform FindChildRecursive(Transform current, string objectName) // 하위 객체 재귀 검색
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
#endif // 에디터 전용 기능 종료