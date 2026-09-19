using UnityEngine; // 장비 화면 표시 기능

[DisallowMultipleComponent] // 장비 HUD 중복 방지
public sealed class PlayerEquipmentHUD : MonoBehaviour // 근접 총기 보조장비 통합 HUD
{
    private PlayerEquipmentManager equipment; // 장착 장비 참조
    private PlayerFirearmController firearm; // 탄약과 조준 참조
    private SupportEquipmentController support; // 마비침 참조
    private ConsumableController consumables; // 소모품 참조
    private PlayerInteraction interaction; // 상호작용 참조
    private PlayerAssassination assassination; // 암살 안내 참조
    private PlayerHealth health; // 사망 화면 제한
    private GUIStyle titleStyle; // 장비 제목 스타일
    private GUIStyle bodyStyle; // 수치 안내 스타일
    private GUIStyle hintStyle; // 중앙 안내 스타일

    private void Awake() // 기존 장비 시스템 연결
    {
        equipment = GetComponent<PlayerEquipmentManager>(); // 장착 관리자 연결
        firearm = GetComponent<PlayerFirearmController>(); // 총기 관리자 연결
        support = GetComponent<SupportEquipmentController>(); // 마비침 연결
        consumables = GetComponent<ConsumableController>(); // 소모품 연결
        interaction = GetComponent<PlayerInteraction>(); // F 입력 대상 연결
        assassination = GetComponent<PlayerAssassination>(); // 암살 안내 연결
        health = GetComponent<PlayerHealth>(); // 생존 상태 연결
    }

    private void OnGUI() // 기존 HUD 하나에서 모든 장비 표시
    {
        if (equipment == null || (!equipment.IsFirearmEquipped && equipment.CurrentWeapon == null)) // 표시 가능한 장착 상태 확인
        {
            return; // 미설정 상태 표시 생략
        }

        EnsureStyles(); // GUI 컨텍스트에서 글자 설정
        Matrix4x4 savedMatrix = GUI.matrix; // 다른 UI의 화면 변환 보존
        Color savedColor = GUI.color; // 다른 UI의 색상 보존
        float scale = Mathf.Max(0.1f, Mathf.Min(1f, Screen.width / 760f, Screen.height / 560f)); // 작은 Game 창에서도 잘리지 않는 축소율
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale); // 장비 패널에만 해상도 보정 적용
        float width = Screen.width / scale; // 보정된 화면 너비
        float height = Screen.height / scale; // 보정된 화면 높이
        try // UI 상태 복구 보장
        {
            // 조준경을 먼저 그린 뒤 장비 패널 표시
            if (health == null || !health.IsDead) // 살아 있는 플레이어 안내 확인
            {
                DrawReticle(width, height); // 단일 조준점과 명중 표시
                if (interaction != null && interaction.HasTarget && (assassination == null || !assassination.HasTarget)) // 암살 안내와 중복 방지
                {
                    GUI.Box(new Rect(width * 0.5f - 175f, height * 0.74f, 350f, 36f), "[F] " + interaction.CurrentLabel, hintStyle); // 기존 보급대와 상호작용 안내
                }
            }
            DrawPanel(width, height); // 조준경 위에 기존 장비 패널 한 번 표시
        }
        finally // 다른 HUD로 색상과 크기 영향 방지
        {
            GUI.matrix = savedMatrix; // 화면 변환 복원
            GUI.color = savedColor; // 원래 색상 복원
        }
    }

    private void DrawPanel(float screenWidth, float screenHeight) // 우측 하단 장비 정보
    {
        const float width = 362f; // 읽기 쉬운 패널 너비
        Rect panel = new Rect(screenWidth - width - 12f, screenHeight - 352f, width, 340f); // 화면 내부 패널 위치
        GUI.Box(panel, GUIContent.none); // 기존 패널 배경
        float x = panel.x + 14f; // 왼쪽 글자 여백
        float y = panel.y + 10f; // 위쪽 글자 여백
        bool gun = equipment.IsFirearmEquipped && firearm != null && firearm.Definition != null; // 총기 표시 모드 확인
        WeaponData stats = gun ? firearm.Definition.Stats : equipment.CurrentWeapon.Stats; // 현재 무기의 공통 수치
        string name = gun ? firearm.Definition.DisplayName : equipment.CurrentWeapon.DisplayName; // 현재 장착 이름
        GUI.Label(new Rect(x, y, width - 28f, 27f), "[" + (equipment.CurrentSlot + 1) + "] " + name, titleStyle); // 장착 슬롯과 무기 이름
        GUI.Label(new Rect(x, y + 29f, width - 28f, 22f), stats != null ? "피해 " + stats.HealthDamage.ToString("0") + " / 자세 " + stats.PostureDamage.ToString("0") + " / 간격 " + stats.FireInterval.ToString("0.00") + "초" : string.Empty, bodyStyle); // 무기별 실제 데이터 표시

        if (gun && firearm.State != null) // 총기 탄약 표시 조건
        {
            string state = firearm.IsEquipping ? "장착 중" : firearm.IsCycling ? (firearm.Definition.BoltAction ? "볼트 준비" : "펌프 준비") : firearm.IsReloading ? "재장전 " + (firearm.ReloadProgress * 100f).ToString("0") + "%" : firearm.IsAiming ? "조준 " + (firearm.AimProgress * 100f).ToString("0") + "%" : firearm.State.Rounds == 0 ? "T 재장전" : firearm.Definition.FireModeLabel; // 현재 사격 상태
            GUI.Label(new Rect(x, y + 53f, width - 28f, 23f), "탄약 " + firearm.State.Rounds + "/" + firearm.State.Capacity + "   예비 " + firearm.State.Reserve + "   " + state, bodyStyle); // 장탄수와 예비탄 구분
            DrawReloadBar(new Rect(x, y + 81f, width - 28f, 7f), firearm.IsReloading ? firearm.ReloadProgress : firearm.IsCycling ? firearm.CycleProgress : 0f); // HP와 섞이지 않는 총기 재장전 표시
        }
        else // 기존 검술 조작 표시
        {
            GUI.Label(new Rect(x, y + 53f, width - 28f, 29f), "LMB 검 공격 / RMB 방어·받아치기", bodyStyle); // 기존 조작 안내 유지
        }

        if (gun) // 총기별 실제 정확도와 소음기 안내
        {
            GUI.Label(new Rect(x, y + 90f, width - 28f, 22f), "분산 ±" + firearm.SpreadDegrees.ToString("0.00") + "° / " + (firearm.SupportsSuppressor ? "소음기 " + (firearm.IsSuppressed ? "ON [B]" : "OFF [B]") : "기본 총성 사용"), bodyStyle); // 실제 사격 수치 표시
            GUI.Label(new Rect(x, y + 113f, width - 28f, 22f), "표적 " + firearm.PracticeHits + "/" + firearm.PracticeShots + " (" + firearm.PracticeAccuracy.ToString("0") + "%) / 총성 " + firearm.EffectiveNoiseRadius.ToString("0.0") + "m", bodyStyle); // 사격장 적중률과 발사 반경 표시
        }

        GUI.Label(new Rect(x, y + 139f, width - 28f, 23f), "[R] 마비침  " + (support != null ? support.RemainingDarts + "/" + support.Capacity : "0"), bodyStyle); // 마비침 탄약 별도 유지
        GUI.Label(new Rect(x, y + 164f, width - 28f, 23f), consumables != null ? "[G] " + consumables.SelectedName + "  x" + consumables.SelectedCount : string.Empty, bodyStyle); // 소모품 수량 유지
        GUI.Label(new Rect(x, y + 193f, width - 28f, 57f), "1~4 검 / 5~9 총기 / Q·E 이전·다음\n총: LMB 발사 / RMB 조준 / T 재장전\nV 아이템 선택 / F 상호작용·보급", bodyStyle); // 충돌 없는 장비 조작 안내
        GUI.Label(new Rect(x, y + 249f, width - 28f, 22f), gun ? "탄환 적중 " + firearm.LastPelletHits + "/" + firearm.Definition.PelletCount + (firearm.LastPracticeTime >= 0f ? "  TTK " + firearm.LastPracticeTime.ToString("0.00") + "초" : "") : string.Empty, bodyStyle); // 산탄과 처치 기록
        GUI.Label(new Rect(x, y + 276f, width - 28f, 39f), equipment.Message, bodyStyle); // 실패 원인과 장비 사용 결과
    }

    private static void DrawReloadBar(Rect rect, float progress) // 재장전 게이지 표시
    {
        Color previous = GUI.color; // 기존 글자 색상 보존
        GUI.color = new Color(0.13f, 0.16f, 0.20f, 1f); // 게이지 바탕 색상
        GUI.DrawTexture(rect, Texture2D.whiteTexture); // 재장전 바탕
        GUI.color = new Color(1f, 0.66f, 0.17f, 1f); // 총기 재장전 주황색
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height), Texture2D.whiteTexture); // 재장전 진행도 표시
        GUI.color = previous; // 다른 표시 색상 복원
    }

    private void DrawReticle(float width, float height) // 총기와 마비침 공용 조준점
    {
        float x = width * 0.5f; // 화면 중앙 가로 위치
        float y = height * 0.5f; // 화면 중앙 세로 위치
        GUI.color = Color.white; // 기본 조준점 색상
        if (equipment.IsFirearmEquipped && firearm != null) // 총기 조준 표시
        {
            if (firearm.Definition.ScopeEnabled && firearm.AimProgress > 0.85f && !firearm.IsCycling) // 천리안 완전 조준 표시
            {
                float size = Mathf.Min(width, height) * 0.72f; // 중앙 확대 영역 크기
                GUI.color = new Color(0f, 0f, 0f, 0.84f); // 주변부 시야 제한
                GUI.DrawTexture(new Rect(0f, 0f, (width - size) * 0.5f, height), Texture2D.whiteTexture); // 왼쪽 가림
                GUI.DrawTexture(new Rect((width + size) * 0.5f, 0f, (width - size) * 0.5f, height), Texture2D.whiteTexture); // 오른쪽 가림
                GUI.DrawTexture(new Rect((width - size) * 0.5f, 0f, size, (height - size) * 0.5f), Texture2D.whiteTexture); // 위쪽 가림
                GUI.DrawTexture(new Rect((width - size) * 0.5f, (height + size) * 0.5f, size, (height - size) * 0.5f), Texture2D.whiteTexture); // 아래쪽 가림
                GUI.color = new Color(0.2f, 0.95f, 1f, 0.85f); // 조준경 눈금 색상
                GUI.DrawTexture(new Rect(x - size * 0.34f, y, size * 0.68f, 1f), Texture2D.whiteTexture); // 수평 십자선
                GUI.DrawTexture(new Rect(x, y - size * 0.34f, 1f, size * 0.68f), Texture2D.whiteTexture); // 수직 십자선
                for (int tick = -3; tick <= 3; tick++) // 거리 보정용 모형 눈금
                {
                    GUI.DrawTexture(new Rect(x + tick * 24f, y - 4f, 1f, 8f), Texture2D.whiteTexture); // 가로 눈금
                }
            }
            float gap = firearm.ReticleRadiusPixels * height / Mathf.Max(1f, Screen.height); // 실제 분산 픽셀과 HUD 축소율 일치
            GUI.color = firearm.HasHitMarker ? (firearm.LastHitWasHead ? new Color(1f, 0.2f, 0.35f) : new Color(1f, 0.55f, 0.16f)) : Color.white; // 명중 시 주황색 표시
            GUI.DrawTexture(new Rect(x - gap - 7f, y - 1f, 7f, 2f), Texture2D.whiteTexture); // 왼쪽 조준선
            GUI.DrawTexture(new Rect(x + gap, y - 1f, 7f, 2f), Texture2D.whiteTexture); // 오른쪽 조준선
            GUI.DrawTexture(new Rect(x - 1f, y - gap - 7f, 2f, 7f), Texture2D.whiteTexture); // 위쪽 조준선
            GUI.DrawTexture(new Rect(x - 1f, y + gap, 2f, 7f), Texture2D.whiteTexture); // 아래쪽 조준선
        }

        GUI.DrawTexture(new Rect(x - 1f, y - 1f, 2f, 2f), Texture2D.whiteTexture); // 중복 없는 중앙 조준점
        GUI.color = Color.white; // 이후 글자 색상 복원
    }

    private void EnsureStyles() // 기존 GUI 글꼴 재사용
    {
        if (titleStyle != null) // 스타일 생성 여부 확인
        {
            return; // 매 프레임 스타일 생성 방지
        }

        titleStyle = new GUIStyle(GUI.skin.label); // 제목 스타일 생성
        titleStyle.fontSize = 20; // 제목 글자 크기
        titleStyle.normal.textColor = new Color(0.3f, 0.95f, 1f); // 장비 제목 청록색
        bodyStyle = new GUIStyle(GUI.skin.label); // 수치 스타일 생성
        bodyStyle.fontSize = 14; // 수치 글자 크기
        bodyStyle.wordWrap = true; // 긴 안내 자동 줄바꿈
        bodyStyle.normal.textColor = Color.white; // 기본 글자 색상
        hintStyle = new GUIStyle(GUI.skin.box); // F 안내 스타일
        hintStyle.fontSize = 18; // F 안내 글자 크기
        hintStyle.alignment = TextAnchor.MiddleCenter; // 중앙 정렬
    }
}
