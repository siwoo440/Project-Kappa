#if UNITY_EDITOR // 대표 총기 자료 생성 전용
using System; // 잘못된 자료 오류 처리
using UnityEditor; // 새 에셋 수치 저장
using UnityEngine; // 무기 정의 자료 생성

public static class ProjectKDay12Catalog // 기획표의 대표 총기 세 종류
{
    public const string Folder = "Assets/_Project/Data/Day12"; // 이번 일차의 독립 데이터 폴더
    public static readonly string[] Ids = new string[] // 기획표 무기 식별자
    {
        "GUN-P01", // 유령손
        "GUN-S01", // 골목비
        "GUN-A01" // 청룡선
    };
    public static readonly string[] Names = new string[] // 사용자 표시 이름
    {
        "유령손", // 정밀 반자동 권총
        "골목비", // 근거리 자동 기관단총
        "청룡선" // 중거리 자동 돌격소총
    };

    public static string DefinitionPath(int index) // 무기별 정의 파일 경로
    {
        return Folder + "/" + Ids[index] + "_Definition.asset"; // 변경하지 않는 식별자 경로
    }

    public static FirearmDefinition[] Build(Material tracer) // 기존 값을 보존한 대표 무기 구성
    {
        ProjectKDay10ModelFactory.EnsureFolder(Folder); // 독립 자료 폴더 확보
        FirearmDefinition[] definitions = new FirearmDefinition[3]; // 대표 무기 순서 유지
        float[] body = new float[] // 기획표 몸통 피해
        {
            24f, // 유령손 수치
            16f, // 골목비 수치
            27f // 청룡선 수치
        };
        float[] head = new float[] // 기획표 머리 피해
        {
            43f, // 유령손 수치
            27f, // 골목비 수치
            45f // 청룡선 수치
        };
        float[] posture = new float[] // 기획표 자세 피해
        {
            16f, // 유령손 수치
            9f, // 골목비 수치
            17f // 청룡선 수치
        };
        float[] rpm = new float[] // 분당 최대 발사 수
        {
            360f, // 유령손 수치
            780f, // 골목비 수치
            600f // 청룡선 수치
        };
        float[] range = new float[] // 피해 감쇠 시작 거리
        {
            24f, // 유령손 수치
            20f, // 골목비 수치
            42f // 청룡선 수치
        };
        float[] end = new float[] // 피해 감쇠 종료 거리
        {
            42f, // 유령손 수치
            38f, // 골목비 수치
            72f // 청룡선 수치
        };
        float[] noise = new float[] // 추가 소음기 배율 없는 기획 기본 소음
        {
            8f, // 유령손 수치
            24f, // 골목비 수치
            42f // 청룡선 수치
        };
        float[] armor = new float[] // 적 방어율을 무시하는 비율
        {
            0.05f, // 유령손 수치
            0.02f, // 골목비 수치
            0.12f // 청룡선 수치
        };
        int[] capacity = new int[] // 탄창 최대 장탄수
        {
            10, // 유령손 수치
            30, // 골목비 수치
            30 // 청룡선 수치
        };
        int[] reserve = new int[] // 임무 시작 기준 예비탄
        {
            60, // 유령손 수치
            120, // 골목비 수치
            120 // 청룡선 수치
        };
        float[] reload = new float[] // 탄창 교체 시간
        {
            1.35f, // 유령손 수치
            1.75f, // 골목비 수치
            2.30f // 청룡선 수치
        };
        float[] hip = new float[] // 비조준 분산 반각
        {
            2.2f, // 유령손 수치
            3.2f, // 골목비 수치
            3.0f // 청룡선 수치
        };
        float[] ads = new float[] // 조준 분산 반각
        {
            0.45f, // 유령손 수치
            0.95f, // 골목비 수치
            0.55f // 청룡선 수치
        };
        float[] pitch = new float[] // 한 발 수직 반동
        {
            1.8f, // 유령손 수치
            2.0f, // 골목비 수치
            3.0f // 청룡선 수치
        };
        float[] yaw = new float[] // 한 발 좌우 반동 범위
        {
            0.7f, // 유령손 수치
            1.5f, // 골목비 수치
            1.4f // 청룡선 수치
        };
        float[] equip = new float[] // 장착 완료 시간
        {
            0.28f, // 유령손 수치
            0.35f, // 골목비 수치
            0.52f // 청룡선 수치
        };
        float[] aim = new float[] // 완전 조준 전환 시간
        {
            0.18f, // 유령손 수치
            0.20f, // 골목비 수치
            0.28f // 청룡선 수치
        };
        float[] move = new float[] // 기본 이동 대비 장착 배율
        {
            1.00f, // 유령손 수치
            1.01f, // 골목비 수치
            0.94f // 청룡선 수치
        };
        for (int i = 0; i < definitions.Length; i++) // 기획표 세 종류 생성
        {
            string statsPath = Folder + "/" + Ids[i] + "_Stats.asset"; // 무기 기본 수치 경로
            WeaponData stats = AssetDatabase.LoadAssetAtPath<WeaponData>(statsPath); // 기존 수동 조정값 조회
            if (stats == null) // 최초 생성 때만 기획 기본값 적용
            {
                stats = ScriptableObject.CreateInstance<WeaponData>(); // 공통 무기 자료 재사용
                SerializedObject edit = new SerializedObject(stats); // 비공개 자료 에디터 편집
                edit.FindProperty("id").stringValue = Ids[i]; // 기획 식별자 저장
                edit.FindProperty("displayName").stringValue = Names[i]; // 한글 표시 이름 저장
                edit.FindProperty("category").enumValueIndex = i == 0 ? (int)WeaponCategory.Sidearm : i == 1 ? (int)WeaponCategory.SubmachineGun : (int)WeaponCategory.AssaultRifle; // 기존 분류 enum 재사용
                Set(edit, "healthDamage", body[i]); // 몸통 피해 적용
                Set(edit, "postureDamage", posture[i]); // 자세 피해 적용
                Set(edit, "effectiveRange", range[i]); // 유효 거리 적용
                Set(edit, "falloffEndRange", end[i]); // 감쇠 종료 적용
                Set(edit, "noiseRadius", noise[i]); // 기본 총성 반경 적용
                Set(edit, "armorPenetration", armor[i]); // 방어 관통 적용
                Set(edit, "fireInterval", 60f / rpm[i]); // 분당 발사 수를 초 간격으로 변환
                edit.FindProperty("magazineSize").intValue = capacity[i]; // 탄창 크기 적용
                edit.FindProperty("reserveAmmo").intValue = reserve[i]; // 예비탄 적용
                edit.ApplyModifiedPropertiesWithoutUndo(); // 새 수치 저장
                AssetDatabase.CreateAsset(stats, statsPath); // 무기 에셋 생성
            }

            string profilePath = Folder + "/" + Ids[i] + "_Handling.asset"; // 반동과 분산 조정 경로
            FirearmHandlingProfile profile = AssetDatabase.LoadAssetAtPath<FirearmHandlingProfile>(profilePath); // 기존 조정 자료 조회
            if (profile == null) // 최초 사격 설정 구성
            {
                profile = ScriptableObject.CreateInstance<FirearmHandlingProfile>(); // 11일차 공통 조정 형식 사용
                SerializedObject edit = new SerializedObject(profile); // 새 사격 수치 편집
                Set(edit, "hipSpread", hip[i]); // 무기별 비조준 분산
                Set(edit, "aimSpread", ads[i]); // 무기별 조준 분산
                Set(edit, "pitchKick", pitch[i]); // 무기별 수직 반동
                Set(edit, "yawKick", yaw[i]); // 무기별 수평 반동
                edit.FindProperty("supportsSuppressor").boolValue = false; // 미확정 추가 소음기 배율의 중복 적용 방지
                edit.ApplyModifiedPropertiesWithoutUndo(); // 문서 수치와 기존 테스트 회복값 저장
                AssetDatabase.CreateAsset(profile, profilePath); // 독립 조정 에셋 생성
            }

            FirearmDefinition definition = AssetDatabase.LoadAssetAtPath<FirearmDefinition>(DefinitionPath(i)); // 기존 대표 총기 정의 보존
            if (definition == null) // 최초 총기 정의 생성
            {
                GameObject model = ProjectKDay12ModelFactory.Build(i); // 총기마다 다른 모형 생성
                definition = ScriptableObject.CreateInstance<FirearmDefinition>(); // 공통 총기 동작 정의
                definition.Configure(stats, model, tracer); // 탄약과 모형과 공용 궤적 연결
                definition.ConfigureHandling(profile, model); // 무기별 반동 분산 연결
                SerializedObject edit = new SerializedObject(definition); // 무기별 동작 설정
                edit.FindProperty("fireMode").enumValueIndex = i == 0 ? (int)FirearmFireMode.SemiAutomatic : (int)FirearmFireMode.Automatic; // 반자동과 자동 분기
                Set(edit, "headDamage", head[i]); // 기획 머리 피해 저장
                Set(edit, "reloadDuration", reload[i]); // 총기별 재장전 시간
                Set(edit, "maximumRange", end[i]); // 이번 대표 무기의 검사 종료 거리
                Set(edit, "minimumDamageRatio", 0.55f); // 문서 감쇠 종료 피해 비율
                Set(edit, "equipDuration", equip[i]); // 실제 장착 대기
                Set(edit, "aimDuration", aim[i]); // 실제 조준 전환
                Set(edit, "movementMultiplier", move[i]); // 장착 이동 배율
                edit.ApplyModifiedPropertiesWithoutUndo(); // 새 정의 수치 적용
                AssetDatabase.CreateAsset(definition, DefinitionPath(i)); // 완성 정의 저장
            }

            FirearmView view = definition.ModelPrefab != null ? definition.ModelPrefab.GetComponent<FirearmView>() : null; // 기존 에셋도 필수 참조 확인
            if (!definition.IsValid || view == null || view.Muzzle == null || definition.Handling == null) // 잘못된 프리팹과 정의 검사
            {
                throw new InvalidOperationException("Day12: " + Ids[i] + "의 정의·총구·반동 자료를 확인하세요."); // 사용자 수정값을 덮지 않고 누락 보고
            }

            definitions[i] = definition; // 대표 총기 슬롯 등록
        }

        return definitions; // 세 종류의 자료 반환
    }

    private static void Set(SerializedObject edit, string property, float value) // 새 자료 수치 적용
    {
        SerializedProperty field = edit.FindProperty(property); // 실제 직렬화 필드 확인
        if (field == null) // 코드와 저장 필드 불일치 검사
        {
            throw new InvalidOperationException("Day12 필드 누락: " + property); // 조용한 부분 적용 방지
        }

        field.floatValue = value; // 명시한 수치 저장
    }
}
#endif // 게임 빌드에서 에셋 생성 도구 제외
