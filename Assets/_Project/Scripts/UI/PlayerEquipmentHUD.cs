using UnityEngine; // 유니티 화면 표시 기능

[DisallowMultipleComponent] // 중복 HUD 방지
public sealed class PlayerEquipmentHUD : MonoBehaviour // 장착 무기와 아이템 테스트 HUD
{
    private PlayerEquipmentManager equipment; // 현재 장비 참조
    private SupportEquipmentController support; // 보조장비 참조
    private ConsumableController consumables; // 소모품 참조
    private PlayerInteraction interaction; // F 상호작용 대상
    private PlayerAssassination assassination; // 암살 안내 상태
    private GUIStyle titleStyle; // 제목 글자 스타일
    private GUIStyle bodyStyle; // 본문 글자 스타일
    private GUIStyle hintStyle; // 중앙 안내 스타일

    private void Awake() // HUD 참조 연결
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장비 관리자 연결
        support = GetComponent<SupportEquipmentController>(); // 보조장비 연결
        consumables = GetComponent<ConsumableController>(); // 소모품 연결
        interaction = GetComponent<PlayerInteraction>(); // 상호작용 연결
        assassination = GetComponent<PlayerAssassination>(); // 암살 연결
    }

    private void OnGUI() // 테스트 장비 화면 표시
    {
        if (equipment == null || equipment.CurrentWeapon == null) // 장착 상태 확인
        {
            return; // 미구성 장비 표시 생략
        }

        EnsureStyles(); // GUI 호출 안에서 스타일 구성
        float width = Mathf.Min(350f, Screen.width - 24f); // 작은 화면 너비 보정
        Rect panel = new Rect(Screen.width - width - 12f, Screen.height - 226f, width, 214f); // 우측 하단 장비 영역
        GUI.Box(panel, GUIContent.none); // 장비 패널 배경
        MeleeWeaponDefinition weapon = equipment.CurrentWeapon; // 선택 무기 조회
        WeaponData stats = weapon.Stats; // 공통 무기 수치 조회
        float x = panel.x + 14f; // 글자 왼쪽 여백
        float y = panel.y + 10f; // 글자 위쪽 여백
        GUI.Label(new Rect(x, y, width - 28f, 27f), "[" + (equipment.SelectedIndex + 1) + "] " + weapon.DisplayName, titleStyle); // 장착 무기 이름
        GUI.Label(new Rect(x, y + 30f, width - 28f, 23f), stats != null ? "피해 " + stats.HealthDamage.ToString("0") + " / 자세 " + stats.PostureDamage.ToString("0") + " / 간격 " + stats.FireInterval.ToString("0.00") + "초" : string.Empty, bodyStyle); // 실제 무기 수치
        GUI.Label(new Rect(x, y + 56f, width - 28f, 25f), "[R] 마비침  " + (support != null ? support.RemainingDarts + "/" + support.Capacity : "0"), bodyStyle); // 보조장비 탄수
        GUI.Label(new Rect(x, y + 83f, width - 28f, 25f), consumables != null ? "[G] " + consumables.SelectedName + "  x" + consumables.SelectedCount : string.Empty, bodyStyle); // 선택 소모품과 수량
        GUI.Label(new Rect(x, y + 112f, width - 28f, 42f), "1~4 무기 선택   Q/E 이전·다음\nV 아이템 선택   F 상호작용·보급", bodyStyle); // 장비 조작 안내
        GUI.Label(new Rect(x, y + 162f, width - 28f, 32f), equipment.Message, bodyStyle); // 실패 원인과 사용 결과

        if (interaction != null && interaction.HasTarget && (assassination == null || !assassination.HasTarget)) // 암살 안내와 중복 방지
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 175f, Screen.height * 0.74f, 350f, 36f), "[F] " + interaction.CurrentLabel, hintStyle); // 일반 상호작용 안내
        }

        Color previous = GUI.color; // 기존 GUI 색상 저장
        GUI.color = Color.white; // 조준점 색상 설정
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture); // 마비침 조준 중심 표시
        GUI.color = previous; // 다른 UI 색상 복구
    }

    private void EnsureStyles() // 프레임 간 GUI 스타일 재사용
    {
        if (titleStyle != null) // 스타일 생성 여부 확인
        {
            return; // 반복 생성 방지
        }

        titleStyle = new GUIStyle(GUI.skin.label); // 제목 스타일 생성
        titleStyle.fontSize = 20; // 제목 크기 설정
        titleStyle.normal.textColor = new Color(0.3f, 0.95f, 1f); // 장비 제목 청록색
        bodyStyle = new GUIStyle(GUI.skin.label); // 본문 스타일 생성
        bodyStyle.fontSize = 14; // 본문 크기 설정
        bodyStyle.wordWrap = true; // 긴 안내 줄바꿈
        bodyStyle.normal.textColor = Color.white; // 본문 색상 설정
        hintStyle = new GUIStyle(GUI.skin.box); // 중앙 안내 스타일 생성
        hintStyle.fontSize = 18; // 안내 크기 설정
        hintStyle.alignment = TextAnchor.MiddleCenter; // 안내 중앙 정렬
    }
}
