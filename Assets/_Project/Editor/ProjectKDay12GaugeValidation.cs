#if UNITY_EDITOR // 에디터 검사 전용
using System; // 검사 실패 예외
using System.Collections.Generic; // UI 꼭짓점 목록
using System.Reflection; // 실제 UI 갱신과 메시 검사
using UnityEditor; // 검사 메뉴
using UnityEditor.SceneManagement; // 저장하지 않는 임시 씬
using UnityEngine; // 체력과 UI 크기 검사
using UnityEngine.SceneManagement; // 편집 씬 복구
using UnityEngine.UI; // 실제 게이지 메시

public static class ProjectKDay12GaugeValidation // 체력 변화와 표시 폭 회귀 검사
{
    // 체력바 메시 검사 메뉴
    public static void TestRendering() // 기존 씬을 변경하지 않는 검사
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) // 안전한 검사 시점 확인
        {
            Debug.LogWarning("Play를 중지하고 컴파일이 끝난 뒤 체력바 검사를 실행하세요."); // 실행 조건 안내
            return; // 실행 중 검사 중단
        }

        Scene previous = SceneManager.GetActiveScene(); // 원래 편집 씬 보존
        Scene temporary = default; // 검사 전용 씬 참조
        int checks = 0; // 통과한 검사 수
        try // 실패 시에도 임시 씬 정리
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); // 빈 검사 씬 생성
            SceneManager.SetActiveScene(temporary); // 검사 객체 생성 위치 지정
            GameObject cameraObject = new GameObject("GaugeTest_Camera"); // 사용자 카메라와 분리한 참조
            Camera camera = cameraObject.AddComponent<Camera>(); // 월드 UI 검사 카메라
            camera.enabled = false; // 검사 화면 렌더링 차단
            camera.transform.position = new Vector3(0f, 2f, -5f); // UI 정면 기준 위치
            GameObject target = new GameObject("GaugeTest_Enemy"); // 임시 적 생성
            EnemyActor actor = target.AddComponent<EnemyActor>(); // 실제 체력 관리자 사용
            actor.Configure(100f, 100f, true); // 검사 시작 체력과 자세
            EnemyCombatBillboardUI ui = target.AddComponent<EnemyCombatBillboardUI>(); // 실제 게임 UI 사용
            ui.Configure(actor, null, null, camera, new Vector3(0f, 2.68f, 0f)); // 검사 적과 카메라 연결
            InvokeUI(ui, "LateUpdate"); // 첫 수치 캐시 준비
            Canvas canvas = target.GetComponentInChildren<Canvas>(true); // 생성된 실제 캔버스 조회
            Check(canvas != null && !canvas.enabled, "평상시 UI 숨김", ref checks); // 기존 표시 정책 확인
            Check(target.GetComponentsInChildren<Canvas>(true).Length == 1, "UI 한 세트만 생성", ref checks); // 중복 생성 검사
            Image hp = FindFill(target, "HealthRow"); // 실제 체력 채움 이미지
            Image posture = FindFill(target, "PostureRow"); // 실제 자세 채움 이미지

            actor.TakeDamage(24f, 16f, target); // 유령손 몸통 피해 전달
            InvokeUI(ui, "LateUpdate"); // 피해 이후 실제 UI 갱신
            Check(Near(actor.CurrentHealth, 76f), "체력 100에서 76으로 감소", ref checks); // 체력 계산과 UI 문제 분리
            Check(canvas.enabled, "피해 이후 UI 표시", ref checks); // 피해 표시 조건 검사
            CheckGauge(hp, 0.76f, "유령손 피격 HP", ref checks); // 채움 숫자가 아닌 실제 표시 폭 검사
            CheckGauge(posture, 0.84f, "유령손 피격 자세", ref checks); // 자세 표시 폭 검사

            actor.TakeDamage(16f, 9f, target); // 골목비 몸통 피해 전달
            InvokeUI(ui, "LateUpdate"); // 다음 피해 표시 갱신
            CheckGauge(hp, 0.60f, "누적 피격 HP", ref checks); // 체력 누적 감소 검사
            RectTransform background = hp.transform.parent as RectTransform; // 바 배경 사각형 조회
            background.sizeDelta = new Vector2(206f, 14f); // 다른 배경 너비 모사
            InvokeUI(ui, "LateUpdate"); // 크기 변경 뒤 UI 갱신
            CheckGauge(hp, 0.60f, "크기 변경 뒤 HP", ref checks); // 고정 픽셀 폭 의존 방지

            actor.TakeDamage(27f, 17f, target); // 청룡선 몸통 피해 전달
            InvokeUI(ui, "LateUpdate"); // 소총 피해 표시 갱신
            CheckGauge(hp, 0.33f, "소총 피격 HP", ref checks); // 추가 감소 표시 검사
            actor.TakeDamage(32f, 18f, target); // 체력 1 상태 준비
            InvokeUI(ui, "LateUpdate"); // 체력 1 표시 갱신
            CheckGauge(hp, 0.01f, "체력 1의 얇은 HP", ref checks); // 작은 잔여 체력 검사
            actor.ApplyPostureDamage(40f, target); // 남은 자세 소진
            InvokeUI(ui, "LateUpdate"); // 자세 붕괴 표시 갱신
            Check(actor.IsPostureBroken, "자세 붕괴 진입", ref checks); // 실제 붕괴 상태 확인
            CheckGauge(posture, 0f, "자세 0의 빈 게이지", ref checks); // 빈 게이지 잔상 검사

            actor.Configure(100f, 100f, true); // 회복 상태 준비
            InvokeUI(ui, "LateUpdate"); // 회복 표시 갱신
            CheckGauge(hp, 1f, "회복 뒤 HP", ref checks); // 감소 후 다시 증가하는 폭 검사
            CheckGauge(posture, 1f, "회복 뒤 자세", ref checks); // 영점 이후 채움 재활성 검사

            string[] rows = new string[] // 같은 생성 코드를 쓰는 게이지 목록
            {
                "HealthRow", // 체력 게이지
                "PostureRow", // 자세 게이지
                "AttackRow", // 공격 예고 게이지
                "DetectionRow" // 탐지 게이지
            };
            float[] samples = new float[] // 채움 경계와 잘못된 값
            {
                -1f, // 음수 입력
                0f, // 완전 소진
                0.01f, // 작은 잔여 비율
                0.25f, // 사분의 일
                0.5f, // 절반
                0.76f, // 피격 후 비율
                1f, // 가득 찬 비율
                2f, // 최대치 초과
                float.NaN, // 잘못된 계산값
                float.PositiveInfinity // 무한대 입력
            };
            MethodInfo setter = typeof(EnemyCombatBillboardUI).GetMethod("SetGaugeFill", BindingFlags.Static | BindingFlags.NonPublic); // 실제 공통 채움 함수 조회
            Check(setter != null, "공통 채움 함수 존재", ref checks); // 검사 대상 누락 확인
            foreach (string row in rows) // 네 게이지 동일 규칙 검사
            {
                Image fill = FindFill(target, row); // 해당 채움 이미지 조회
                target.transform.Find("__EnemyCombatUI/" + row).gameObject.SetActive(true); // 검사 행만 임시 표시
                foreach (float value in samples) // 감소 증가 경계 순회
                {
                    object[] arguments = new object[2]; // 실제 함수 전달값 배열
                    arguments[0] = fill; // 갱신할 게이지
                    arguments[1] = value; // 검사할 채움 비율
                    setter.Invoke(null, arguments); // 게임의 실제 채움 함수 호출
                    float expected = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value); // 안전한 기대 비율
                    CheckGauge(fill, expected, row + " " + value, ref checks); // 실제 사각형과 메시 폭 비교
                }
            }

            ui.enabled = false; // 임시 컴포넌트 비활성화
            InvokeUI(ui, "OnDisable"); // 에디터에서도 정리 처리 재현
            Check(!canvas.enabled && !canvas.gameObject.activeSelf, "비활성화 뒤 UI 숨김", ref checks); // 기존 정리 동작 확인
            ui.enabled = true; // 컴포넌트 재활성화
            InvokeUI(ui, "OnEnable"); // 기존 UI 재사용 처리
            actor.TakeDamage(1f, 0f, target); // 재활성화 뒤 표시 조건 생성
            InvokeUI(ui, "LateUpdate"); // 복구된 UI 갱신
            CheckGauge(hp, 0.99f, "재활성화 뒤 HP", ref checks); // 복구 뒤 정상 표시 검사
            Check(target.GetComponentsInChildren<Canvas>(true).Length == 1, "재활성화 뒤 중복 없음", ref checks); // UI 중복 방지 유지
            actor.TakeDamage(100f, 0f, target); // 실제 사망 처리
            InvokeUI(ui, "LateUpdate"); // 사망 상태 최종 확인
            Check(actor.IsDead && !canvas.enabled, "사망 뒤 체력 UI 숨김", ref checks); // 사망 잔류 표시 방지
        }
        finally // 사용자 편집 씬 복구
        {
            if (previous.IsValid() && previous.isLoaded) // 원래 씬 유효성 확인
            {
                SceneManager.SetActiveScene(previous); // 기존 작업 씬으로 복귀
            }

            if (temporary.IsValid() && temporary.isLoaded) // 생성한 임시 씬 확인
            {
                EditorSceneManager.CloseScene(temporary, true); // 저장하지 않고 검사 씬 제거
            }
        }

        Debug.Log("Day12 체력바 실제 UI 메시 검사 통과: " + checks + "항목. Test 씬은 변경하지 않았습니다."); // 모든 검사 성공 시에만 결과 출력
    }

    private static Image FindFill(GameObject target, string row) // 생성된 게이지 채움 조회
    {
        Transform child = target.transform.Find("__EnemyCombatUI/" + row + "/Frame/Background/Fill"); // 정확한 게이지 경로
        Image image = child != null ? child.GetComponent<Image>() : null; // 채움 이미지 참조
        if (image == null) // 구조 누락 확인
        {
            throw new InvalidOperationException("체력바 검사 실패: " + row + " 채움 이미지 누락"); // 잘못된 검사를 성공으로 처리하지 않음
        }

        return image; // 실제 UI 이미지 반환
    }

    private static void InvokeUI(EnemyCombatBillboardUI ui, string name) // 실제 UI 콜백 검사
    {
        MethodInfo method = typeof(EnemyCombatBillboardUI).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic); // 비공개 갱신 함수 조회
        if (method == null) // 콜백 누락 확인
        {
            throw new InvalidOperationException("체력바 검사 함수 누락: " + name); // 검사 대상 오류 전달
        }

        method.Invoke(ui, null); // 에디터에서 동일한 갱신 실행
    }

    private static void CheckGauge(Image image, float expected, string label, ref int checks) // 값 대신 실제 표시 폭 검사
    {
        RectTransform rect = image.rectTransform; // 실제 채움 사각형
        RectTransform parent = rect.parent as RectTransform; // 전체 폭 배경
        rect.ForceUpdateRectTransforms(); // 변경된 앵커 계산 갱신
        Check(parent != null && parent.rect.width > 0f, label + " 배경 폭", ref checks); // 영점 나눗셈 방지
        Check(Near(rect.rect.width / parent.rect.width, expected), label + " 실제 사각형 폭", ref checks); // fillAmount 숫자만 맞는 버그 검출
        Check(Near(rect.offsetMin.x, 0f) && Near(rect.anchorMin.x, 0f), label + " 왼쪽 고정", ref checks); // 가운데로 줄어드는 오류 방지
        Check(image.enabled == (expected > 0f), label + " 영점 표시", ref checks); // 영점 잔상과 회복 후 표시 확인
        float meshWidth = image.enabled ? MeshWidth(image) : 0f; // 실제 Unity 메시의 가로 폭
        Check(Near(meshWidth / parent.rect.width, expected), label + " 메시 폭", ref checks); // 원본 이미지 없는 렌더링 경로 검사
    }

    private static float MeshWidth(Image image) // Unity Image가 생성한 실제 꼭짓점 폭
    {
        Type[] parameterTypes = new Type[1]; // 메시 함수 매개변수 형식
        parameterTypes[0] = typeof(VertexHelper); // 실제 UI 메시 도우미 형식
        MethodInfo populate = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null); // 메시 생성 함수의 정확한 오버로드
        if (populate == null) // Unity API 지원 확인
        {
            throw new InvalidOperationException("현재 Unity에서 UI 메시 검사 함수를 찾지 못했습니다."); // 검사 미실행을 성공과 구분
        }

        using (VertexHelper helper = new VertexHelper()) // 메시 메모리 수명 제한
        {
            object[] arguments = new object[1]; // 메시 함수 전달값 배열
            arguments[0] = helper; // 검사할 메시 도우미
            populate.Invoke(image, arguments); // 실제 Image 렌더링 경로 실행
            List<UIVertex> vertices = new List<UIVertex>(); // 메시 꼭짓점 보관
            helper.GetUIVertexStream(vertices); // 렌더링용 삼각형 꼭짓점 추출
            float minimum = float.PositiveInfinity; // 왼쪽 끝 초기값
            float maximum = float.NegativeInfinity; // 오른쪽 끝 초기값
            foreach (UIVertex vertex in vertices) // 실제 메시 범위 순회
            {
                minimum = Mathf.Min(minimum, vertex.position.x); // 가장 왼쪽 좌표
                maximum = Mathf.Max(maximum, vertex.position.x); // 가장 오른쪽 좌표
            }

            return vertices.Count > 0 ? maximum - minimum : 0f; // 메시 가로 폭 반환
        }
    }

    private static bool Near(float value, float expected) // 부동소수점 오차 허용
    {
        return Mathf.Abs(value - expected) <= 0.001f; // UI 비율 비교 오차
    }

    private static void Check(bool condition, string label, ref int checks) // 검사 실패 즉시 중단
    {
        if (!condition) // 기대 동작 불일치 확인
        {
            throw new InvalidOperationException("체력바 검사 실패: " + label); // 실패 항목 보고
        }

        checks++; // 통과한 항목만 집계
    }
}
#endif // 에디터 검사 코드 제외
