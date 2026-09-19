#if UNITY_EDITOR // 사용자 편집기에서만 설정
using System; // 백업과 구성 오류 처리
using System.Collections.Generic; // 기존 무기 목록 수집
using UnityEditor; // 실제 씬 참조 연결
using UnityEditor.SceneManagement; // 안전한 씬 백업
using UnityEngine; // 계측 단말기 모형
using UnityEngine.SceneManagement; // 현재 Test 씬 확인
using G15 = TrainingCenterGeometry; // 기존 시설 재질과 부품 재사용

public static class ProjectKDay15Setup // 통합 맵을 재생성하지 않는 계측 설치
{
    public const string RootName = "Day15_BalanceLab"; // 중복 생성 식별자
    private static bool applying; // 중복 실행 차단

    [MenuItem("Project K/Day 15/Setup Balance Lab")] // 명시적인 설치 메뉴
    public static void Setup() // 기존 센터에 계측만 추가
    {
        if (applying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 편집 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 임포트와 컴파일이 끝난 뒤 실행하세요."); // 설치 시점 안내
            return; // 실행 중 씬 변경 금지
        }
        Scene scene = SceneManager.GetActiveScene(); // 사용자가 열어 둔 씬
        if (scene.path != TrainingCenterSetup.ScenePath) // 정확한 Test 씬 확인
        {
            Debug.LogWarning("통합 훈련센터가 있는 Test.unity를 열고 활성화하세요."); // 대상 파일 안내
            return; // 다른 씬 자동 변경 금지
        }
        TrainingCenterRoot[] centers = TrainingCenterMigration.Components<TrainingCenterRoot>(scene); // 해당 씬만 검색
        if (centers.Length != 1 || !centers[0].Completed) // 기존 통합 시설 확인
        {
            Debug.LogError("완료된 통합 훈련센터 한 개가 필요합니다. 맵을 자동 재구성하지 않습니다."); // 선행 기능 안내
            return; // 기존 맵 보존
        }
        TrainingCenterRoot center = centers[0]; // 기존 시설 선택
        if (center.GetComponentInChildren<BalanceSessionRunner>(true) != null) // 이미 설치된 계측 확인
        {
            Debug.Log("이미 계측이 설치되어 있습니다. Validate Balance Lab으로 확인하세요."); // 수동 편집 보호
            return; // 씬과 컴포넌트 중복 생성 방지
        }
        int group = -1; // 변경 되돌리기 범위
        applying = true; // 재진입 제한
        try // 실패 시 새 계측 부분만 복구
        {
            if (center.Player == null || center.Spawn == null) // 핵심 사용자 참조 확인
            {
                throw new InvalidOperationException("센터의 플레이어와 로비 스폰이 필요합니다."); // 잘못된 설치 차단
            }
            TrainingCenterLane[] lanes = center.GetComponentsInChildren<TrainingCenterLane>(true); // 현재 수동 배치한 사격선 유지
            Array.Sort(lanes, (a, b) => string.CompareOrdinal(a.name, b.name)); // 재설치와 결과의 안정적인 레인 순서
            foreach (TrainingCenterLane lane in lanes) // 실제 레인 준비 상태 확인
            {
                if (lane.Target == null || lane.Target.Hinge == null || lane.FiringPoint == null) // 누락된 표적 참조 검사
                {
                    throw new InvalidOperationException("사격선 참조 누락: " + lane.name); // 구체적인 오류 표시
                }
            }
            PlayerFirearmController gun = center.Player.GetComponent<PlayerFirearmController>(); // 실제 총기 관리자
            if (gun == null || lanes.Length == 0) // 선행 총기와 표적 확인
            {
                throw new InvalidOperationException("기존 총기 관리자와 사격선이 필요합니다."); // 빈 설치 방지
            }
            SerializedProperty loadout = new SerializedObject(gun).FindProperty("loadout"); // 기존 장착 순서 그대로 조회
            List<FirearmDefinition> definitions = new List<FirearmDefinition>(); // 실제 선택 목록
            if (loadout == null || loadout.arraySize == 0) // 데이터 준비 확인
            {
                throw new InvalidOperationException("기존 loadout이 비어 있습니다."); // 총기 자동 생성 금지
            }
            for (int i = 0; i < loadout.arraySize; i++) // 원래 무기 슬롯 유지
            {
                FirearmDefinition definition = loadout.GetArrayElementAtIndex(i).objectReferenceValue as FirearmDefinition; // 실제 정의 조회
                if (definition == null || !definition.IsValid) // 미완성 총기 확인
                {
                    throw new InvalidOperationException("총기 슬롯 참조를 확인하세요: " + i); // 잘못된 번호 재배치 방지
                }
                definitions.Add(definition); // 에셋 값 변경 없이 목록 복사
            }
            string folder = "Assets/_Project/Backups/Day15"; // 사용자 복구용 씬 경로
            G15.Folder(folder); // 백업 폴더 준비
            string backup = folder + "/Test_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity"; // 기존 백업과 이름 분리
            if (!EditorSceneManager.SaveScene(scene, backup, true)) // 원본 복사 우선
            {
                throw new InvalidOperationException("원본 씬 백업 실패"); // 백업 없이 수정 금지
            }
            Undo.IncrementCurrentGroup(); // 이번 설치 되돌리기 시작
            group = Undo.GetCurrentGroup(); // 복구 범위 저장
            GameObject root = new GameObject(RootName); // 새 계측 루트
            root.transform.SetParent(center.transform, false); // 기존 통합 시설 안에만 추가
            Undo.RegisterCreatedObjectUndo(root, "Install Day 15 Balance Lab"); // 하위 장치까지 한 번에 복구
            BalanceSessionRunner runner = root.AddComponent<BalanceSessionRunner>(); // 통제 계측 연결
            runner.Configure(center, lanes, definitions.ToArray()); // 기존 레인과 총기 참조만 연결
            BalanceObservationRecorder observer = root.AddComponent<BalanceObservationRecorder>(); // 실제 경비 반응 기록
            observer.Configure(center); // 기존 실제 시험 구역 재사용
            BalancePanel panel = root.AddComponent<BalancePanel>(); // 단일 설정 화면
            panel.Configure(runner, observer); // 실제 기록과 화면 연결
            BuildConsole(root.transform, center, panel); // 로비의 F 단말기 추가
            EditorUtility.SetDirty(runner); // 저장할 시험 참조 표시
            EditorUtility.SetDirty(observer); // 저장할 관측 참조 표시
            EditorUtility.SetDirty(panel); // 저장할 화면 참조 표시
            AssetDatabase.SaveAssets(); // 재사용 재질의 초기 생성 저장
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 저장 대상 표시
            if (!EditorSceneManager.SaveScene(scene)) // 동일 Test 경로에 계측만 저장
            {
                throw new InvalidOperationException("Test 씬 저장 실패"); // 실제 저장 오류 보고
            }
            Undo.CollapseUndoOperations(group); // 사용자 되돌리기 한 번으로 묶기
            Selection.activeGameObject = root; // 설치한 구성 선택
            Debug.Log("Day 15 계측 설치 완료. Play에서 F8 또는 로비 단말기 F. 총기 수치·입력·기존 맵은 유지. 백업: " + backup); // 실제 설치 결과 안내
        }
        catch (Exception error) // 부분 설치 정리
        {
            if (group >= 0) // 변경한 계측 구조 확인
            {
                Undo.RevertAllDownToGroup(group); // 설치 부분만 되돌리기
            }
            Debug.LogException(error); // 실제 실패 원인 보고
        }
        finally // 다음 실행 준비
        {
            applying = false; // 설치 잠금 해제
        }
    }

    private static void BuildConsole(Transform parent, TrainingCenterRoot center, BalancePanel panel) // 기존 스폰 가까운 작은 계측 장치
    {
        Vector3 point = parent.InverseTransformPoint(center.Spawn.position) + new Vector3(3.2f, -0.14f, 2.5f); // 기존 로비 스폰 기준 위치
        Physics.SyncTransforms(); // 기존 시설 충돌 위치 반영
        bool found = false; // 비어 있는 후보 위치 확인
        for (int i = 0; i < 5; i++) // 짧은 로비 범위에서만 후보 탐색
        {
            Vector3 candidate = point + Vector3.right * i * 1.7f; // 옆으로 이동한 후보
            Vector3 world = parent.TransformPoint(candidate + Vector3.up * 0.8f); // 검사할 본체 중심
            if (!Physics.CheckBox(world, new Vector3(0.8f, 0.65f, 0.7f), parent.rotation, ~0, QueryTriggerInteraction.Ignore)) // 바닥 위 기존 구조물과의 겹침 검사
            {
                point = candidate; // 실제 빈 자리 선택
                found = true; // 배치 가능 기록
                break; // 첫 안전 위치 사용
            }
        }
        if (!found) // 로비 수동 편집으로 공간이 없는 경우
        {
            Debug.LogWarning("로비 단말기 자리가 없어 생성을 생략했습니다. 동일 기능은 F8로 사용합니다."); // 실제 미생성 항목 안내
            return; // 기존 시설을 밀어내거나 삭제하지 않음
        }
        Transform console = G15.Node(parent, "BalanceConsole_F", point); // 신규 장치 부모
        G15.Box(console, "Pedestal", new Vector3(0f, 0.65f, 0f), new Vector3(1.25f, 1.3f, 0.75f), G15.Steel, true); // 실제 F 상호작용 충돌
        G15.Box(console, "Screen", new Vector3(0f, 1.14f, -0.39f), new Vector3(1.05f, 0.42f, 0.035f), G15.Dark); // 화면 외장
        G15.Box(console, "StatusStrip", new Vector3(0f, 0.78f, -0.40f), new Vector3(0.90f, 0.06f, 0.035f), G15.Cyan); // 제한적인 식별 발광
        Transform label = G15.Node(console, "BalanceLabel", new Vector3(0f, 1.72f, -0.42f)); // 장치 안내 위치
        TextMesh text = label.gameObject.AddComponent<TextMesh>(); // 폰트 파일 없는 기본 표시
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 현재 Unity 내장 글꼴
        text.text = "BALANCE LAB\n[F] / [F8]"; // 원거리 식별용 간단한 이름
        text.characterSize = 0.024f; // 장비 크기에 맞는 글자
        text.fontSize = 48; // 글자 윤곽 해상도
        text.anchor = TextAnchor.MiddleCenter; // 장치 중앙 정렬
        text.alignment = TextAlignment.Center; // 여러 줄 정렬
        label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material; // 내장 글꼴 재질 연결
        console.gameObject.AddComponent<BalanceConsole>().Configure(panel); // 기존 F 입력만 사용
    }
}
#endif
