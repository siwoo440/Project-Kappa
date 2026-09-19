#if UNITY_EDITOR // 신규 총기 데이터와 모형 생성
using System; // 자료 누락 보고
using UnityEditor; // 에셋 저장과 속성 편집
using UnityEditor.SceneManagement; // 사용자 씬과 분리된 모형 생성
using UnityEngine; // 메시와 무기 데이터
using UnityEngine.SceneManagement; // 임시 씬 이동
using G13 = ProjectKDay13Geometry; // 공통 부품 생성

public static class ProjectKDay13Weapons // 문지기와 천리안 정의
{
    public const string Folder = "Assets/_Project/Data/Day13"; // 독립 데이터 위치
    public static readonly string[] Ids = new string[] // 대표 무기 식별자
    {
        "GUN-H01", // 문지기 식별자
        "GUN-X01" // 천리안 식별자
    };
    public static string PathFor(int index) // 총기 정의 경로
    {
        return Folder + "/" + Ids[index] + "_Definition.asset"; // 고정 경로 반환
    }

    public static FirearmDefinition[] Build(Material tracer) // 두 총기 최초 생성과 기존 편집 보존
    {
        ProjectKDay10ModelFactory.EnsureFolder(Folder); // 폴더 준비
        FirearmDefinition[] result = new FirearmDefinition[2]; // 고정 슬롯 순서
        for (int i = 0; i < 2; i++) // 두 총기 생성
        {
            bool sniper = i == 1; // 저격총 여부
            string prefix = Folder + "/" + Ids[i]; // 자료 접두 경로
            WeaponData stats = AssetDatabase.LoadAssetAtPath<WeaponData>(prefix + "_Stats.asset"); // 기존 수치 확인
            if (stats == null) // 최초 수치 생성
            {
                stats = ScriptableObject.CreateInstance<WeaponData>(); // 기존 형식 재사용
                SerializedObject edit = new SerializedObject(stats); // 새 수치 편집
                edit.FindProperty("id").stringValue = Ids[i]; // 고유 ID 저장
                edit.FindProperty("displayName").stringValue = sniper ? "천리안" : "문지기"; // 실제 표시 이름
                edit.FindProperty("category").enumValueIndex = sniper ? (int)WeaponCategory.PrecisionSpecial : (int)WeaponCategory.Shotgun; // 기존 분류 유지
                Number(edit, "healthDamage", sniper ? 88f : 72f); // 몸통 전체 피해
                Number(edit, "postureDamage", sniper ? 46f : 55f); // 자세 전체 피해
                Number(edit, "fireInterval", sniper ? 60f / 45f : 0.8f); // 기획 발사 간격
                Number(edit, "effectiveRange", sniper ? 90f : 10f); // 유효 거리
                Number(edit, "falloffEndRange", sniper ? 150f : 22f); // 거리 감쇠 종료
                Number(edit, "noiseRadius", sniper ? 65f : 48f); // 실제 총성 반경
                Number(edit, "armorPenetration", sniper ? 0.35f : 0.12f); // 장갑 피해 감소 무시 비율
                edit.FindProperty("magazineSize").intValue = sniper ? 5 : 6; // 탄창 용량
                edit.FindProperty("reserveAmmo").intValue = sniper ? 20 : 30; // 최초 예비탄
                edit.ApplyModifiedPropertiesWithoutUndo(); // 최초 수치 적용
                AssetDatabase.CreateAsset(stats, prefix + "_Stats.asset"); // 수치 저장
            }
            FirearmHandlingProfile handling = AssetDatabase.LoadAssetAtPath<FirearmHandlingProfile>(prefix + "_Handling.asset"); // 기존 반동 편집 유지
            if (handling == null) // 최초 사격 감각 설정
            {
                handling = ScriptableObject.CreateInstance<FirearmHandlingProfile>(); // 공통 조정 자료
                SerializedObject edit = new SerializedObject(handling); // 자료 속성 편집
                Number(edit, "hipSpread", sniper ? 6f : 7f); // 비조준 분산
                Number(edit, "aimSpread", sniper ? 0.08f : 5f); // 조준 분산
                Number(edit, "pitchKick", sniper ? 9f : 7.5f); // 수직 반동
                Number(edit, "yawKick", sniper ? 2.5f : 3f); // 수평 반동
                Number(edit, "maximumPitchKick", 15f); // 단발 반동 잘림 방지
                Number(edit, "maximumYawKick", 6f); // 좌우 반동 상한
                Number(edit, "recoilRecovery", 8f); // 단발 사격 시험용 회복 속도
                Number(edit, "bloomRecovery", 3f); // 펌프 사이 분산 회복
                Number(edit, "aimSensitivityRatio", sniper ? 0.3f : 0.7f); // 확대 시 조작 감도
                edit.FindProperty("supportsSuppressor").boolValue = false; // 미정 소음기 효과 중복 적용 방지
                edit.ApplyModifiedPropertiesWithoutUndo(); // 조정 자료 적용
                AssetDatabase.CreateAsset(handling, prefix + "_Handling.asset"); // 반동 자료 저장
            }
            FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(PathFor(i)); // 사용자 수정 정의 유지
            if (definition == null) // 최초 정의 생성
            {
                GameObject model = BuildModel(sniper); // 다른 실루엣의 총기 모형
                definition = ScriptableObject.CreateInstance<FirearmDefinition>(); // 공통 총기 자료
                definition.Configure(stats, model, tracer); // 기존 데이터 연결
                definition.ConfigureHandling(handling, model); // 사격 감각 연결
                SerializedObject edit = new SerializedObject(definition); // 동작 설정 편집
                edit.FindProperty("fireMode").enumValueIndex = (int)FirearmFireMode.SemiAutomatic; // 클릭당 한 발 입력
                edit.FindProperty("pelletCount").intValue = sniper ? 1 : 8; // 한 발당 탄환 수
                edit.FindProperty("singleRoundReload").boolValue = true; // 두 무기 모두 한 발씩 삽입
                edit.FindProperty("boltAction").boolValue = sniper; // 기구 표시 구분
                edit.FindProperty("scopeEnabled").boolValue = sniper; // 저격 조준 표시
                Number(edit, "cycleDuration", sniper ? 60f / 45f : 0.8f); // 기구 준비와 발사 간격 연결
                Number(edit, "headDamage", sniper ? 176f : 90f); // 머리 전체 피해
                Number(edit, "reloadDuration", sniper ? 0.9f : 0.72f); // 한 발 삽입 시간
                Number(edit, "maximumRange", sniper ? 150f : 22f); // 실제 최대 검사 거리
                Number(edit, "minimumDamageRatio", 0.55f); // 기존 감쇠 하한 재사용
                Number(edit, "equipDuration", sniper ? 0.8f : 0.65f); // 장착 준비 시간
                Number(edit, "aimDuration", sniper ? 0.42f : 0.25f); // 조준 전환 시간
                Number(edit, "movementMultiplier", sniper ? 0.78f : 0.88f); // 장착 이동 배율
                Number(edit, "scopeFovRatio", 0.26f); // 임시 저격 확대 값
                edit.ApplyModifiedPropertiesWithoutUndo(); // 새 동작 설정 적용
                AssetDatabase.CreateAsset(definition, PathFor(i)); // 독립 무기 정의 저장
            }
            if (!definition.IsValid || definition.ModelPrefab.GetComponent<FirearmView>()?.Muzzle == null) // 실제 실행 참조 확인
            {
                throw new InvalidOperationException("Day13: 총기 자료 참조 누락 " + Ids[i]); // 미완성 모형 연결 중단
            }
            result[i] = definition; // 슬롯 등록
        }
        return result; // 두 총기 반환
    }

    public static void Number(SerializedObject edit, string field, float value) // 숫자 속성 저장
    {
        SerializedProperty property = edit.FindProperty(field); // 실제 코드 필드 검색
        if (property == null) // 코드 버전 불일치 확인
        {
            throw new InvalidOperationException("Day13: 필드 누락 " + field); // 잘못된 덮어쓰기 안내
        }
        property.floatValue = value; // 숫자 설정
    }

    private static GameObject BuildModel(bool sniper) // 펌프와 볼트 외형 생성
    {
        string folder = "Assets/_Project/Prefabs/Day13"; // 모형 저장 폴더
        ProjectKDay10ModelFactory.EnsureFolder(folder); // 폴더 확보
        string path = folder + "/" + (sniper ? "GUN-X01" : "GUN-H01") + ".prefab"; // 프리팹 경로
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 수동 모형 편집 확인
        if (existing != null) // 재실행 시 프리팹 보존
        {
            return existing; // 기존 모형 반환
        }
        Scene preview = EditorSceneManager.NewPreviewScene(); // 씬 오염 없는 모형 공간
        GameObject root = new GameObject(sniper ? "Cheonrian" : "Munjigi"); // 총기 모형 루트
        SceneManager.MoveGameObjectToScene(root, preview); // 생성 공간 격리
        try // 실패 시 임시 공간 정리
        {
            Material dark = G13.Mat("WeaponBody", new Color(0.07f, 0.10f, 0.14f)); // 무광 외장
            Material metal = G13.Mat("WeaponSteel", new Color(0.35f, 0.42f, 0.5f)); // 기구 금속
            Material rubber = G13.Mat("WeaponRubber", new Color(0.02f, 0.028f, 0.035f)); // 고무 그립
            Material accent = G13.Mat(sniper ? "SniperBlue" : "ShotgunOrange", sniper ? new Color(0.12f, 0.7f, 1f) : new Color(1f, 0.4f, 0.08f), true); // 두 무기 구분색
            Transform parts = G13.Node(root.transform, "RecoilParts", Vector3.zero); // 기존 총기 반동 구조
            G13.Box(parts, "Receiver", new Vector3(0f, 0.08f, 0.05f), new Vector3(0.15f, 0.19f, 0.55f), dark); // 본체
            G13.Box(parts, "Stock", new Vector3(0f, 0.02f, -0.37f), new Vector3(0.12f, 0.21f, 0.38f), rubber); // 개머리판
            G13.Box(parts, "CheekRest", new Vector3(0f, 0.17f, -0.36f), new Vector3(0.13f, 0.08f, 0.27f), metal); // 뺨 받침
            G13.Box(parts, "Grip", new Vector3(0f, -0.12f, -0.11f), new Vector3(0.09f, 0.24f, 0.13f), rubber).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f); // 후방 손잡이
            G13.Box(parts, "TriggerGuardBottom", new Vector3(0f, -0.065f, 0.04f), new Vector3(0.05f, 0.026f, 0.17f), metal); // 방아쇠 보호대 하부
            G13.Box(parts, "TriggerGuardFront", new Vector3(0f, -0.005f, 0.12f), new Vector3(0.05f, 0.13f, 0.025f), metal); // 보호대 전방
            G13.Box(parts, "Trigger", new Vector3(0f, -0.015f, 0.015f), new Vector3(0.024f, 0.07f, 0.025f), metal); // 방아쇠 외형
            float end = sniper ? 1.23f : 0.95f; // 총열 길이 차이
            G13.Tube(parts, "Barrel", new Vector3(0f, 0.12f, 0.64f), sniper ? 0.026f : 0.037f, sniper ? 1.02f : 0.63f, metal); // 긴 총열
            G13.Tube(parts, "MuzzleBrake", new Vector3(0f, 0.12f, end - 0.06f), 0.047f, 0.1f, dark); // 총구 보호부
            G13.Tube(parts, "Bore", new Vector3(0f, 0.12f, end - 0.004f), 0.027f, 0.004f, rubber); // 검은 총구 입구
            Transform handle; // 실제 움직이는 부품
            if (sniper) // 천리안의 조준경과 볼트
            {
                G13.Box(parts, "ScopeMount", new Vector3(0f, 0.24f, 0.04f), new Vector3(0.12f, 0.1f, 0.28f), metal); // 조준경 받침
                G13.Tube(parts, "ScopeTube", new Vector3(0f, 0.34f, 0.04f), 0.075f, 0.42f, dark); // 망원 조준경
                G13.Tube(parts, "ScopeLens", new Vector3(0f, 0.34f, 0.255f), 0.059f, 0.007f, accent); // 발광 렌즈
                G13.Box(parts, "AdjustmentDial", new Vector3(0.09f, 0.34f, 0.04f), new Vector3(0.05f, 0.06f, 0.06f), metal); // 조절 다이얼
                handle = G13.Node(parts, "BoltHandle", new Vector3(0.105f, 0.1f, -0.06f)); // 볼트 회전 위치
                G13.Box(handle, "Stem", new Vector3(0.045f, 0f, 0f), new Vector3(0.11f, 0.025f, 0.025f), metal); // 손잡이 축
                G13.Part(handle, "Knob", PrimitiveType.Sphere, new Vector3(0.1f, -0.018f, 0f), Vector3.one * 0.055f, rubber); // 볼트 손잡이 구
                for (int i = -1; i <= 1; i += 2) // 접힌 양각대
                {
                    G13.Box(parts, "Bipod", new Vector3(i * 0.055f, -0.04f, 0.53f), new Vector3(0.025f, 0.06f, 0.3f), metal); // 접힌 받침 다리
                }
            }
            else // 문지기의 펌프와 관형 탄창
            {
                G13.Tube(parts, "MagazineTube", new Vector3(0f, 0.015f, 0.59f), 0.033f, 0.6f, dark); // 총열 아래 관형 탄창
                handle = G13.Node(parts, "PumpGrip", new Vector3(0f, -0.02f, 0.47f)); // 앞뒤 이동 펌프 기준
                G13.Box(handle, "ForeEnd", Vector3.zero, new Vector3(0.16f, 0.14f, 0.26f), rubber); // 큰 펌프 그립
                for (int i = 0; i < 6; i++) // 미끄럼 방지 요철
                {
                    G13.Box(handle, "Rib_" + i, new Vector3(0f, -0.005f, -0.11f + i * 0.04f), new Vector3(0.17f, 0.15f, 0.014f), metal); // 펌프 표면 홈
                }
            }
            for (int i = 0; i < 6; i++) // 공통 냉각 홈과 나사
            {
                G13.Box(parts, "Vent_" + i, new Vector3(-0.078f, 0.1f, -0.1f + i * 0.062f), new Vector3(0.009f, 0.03f, 0.025f), rubber); // 측면 냉각 홈
                G13.Part(parts, "Bolt_" + i, PrimitiveType.Sphere, new Vector3(0.079f, 0.03f, -0.1f + i * 0.062f), Vector3.one * 0.019f, metal); // 고정 나사
            }
            G13.Box(parts, "EnergyLine", new Vector3(-0.082f, 0.04f, 0.04f), new Vector3(0.01f, 0.018f, 0.36f), accent); // 총기 식별 발광선
            Transform shell = G13.Node(parts, "LoadingRound", new Vector3(0f, -0.07f, 0.18f)); // 재장전 삽입 탄약
            G13.Tube(shell, "Casing", Vector3.zero, 0.022f, sniper ? 0.10f : 0.07f, accent); // 한 발 장전용 모형
            shell.gameObject.SetActive(false); // 대기 중 삽입 탄약 숨김
            Transform muzzle = G13.Node(parts, "Muzzle", new Vector3(0f, 0.12f, end + 0.02f)); // 실제 사격 원점
            GameObject flash = G13.Box(muzzle, "Flash", new Vector3(0f, 0f, 0.05f), new Vector3(0.07f, 0.07f, 0.13f), accent); // 총구 섬광
            flash.SetActive(false); // 사격 전 숨김
            root.AddComponent<FirearmView>().Configure(muzzle, parts, shell, flash); // 기존 반동과 발사 효과 연결
            root.AddComponent<FirearmMechanismView>().Configure(handle, shell, sniper); // 기구 동작 연결
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); // 프리팹 저장
            if (prefab == null) // 저장 실패 확인
            {
                throw new InvalidOperationException("Day13 모형 저장 실패"); // 실패 보고
            }
            return prefab; // 저장된 총기 모형
        }
        finally // 임시 모형 정리
        {
            UnityEngine.Object.DestroyImmediate(root); // 작업 씬에 잔상 방지
            EditorSceneManager.ClosePreviewScene(preview); // 임시 씬 종료
        }
    }
}
#endif // 게임 빌드에서 모형 생성 제외
