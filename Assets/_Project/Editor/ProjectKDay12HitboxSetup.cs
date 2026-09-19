#if UNITY_EDITOR // 적 부위별 피격 설정 전용
using UnityEditor; // 새 컴포넌트 저장 표시
using UnityEngine; // 피격 상자와 머리 경계 계산
using UnityEngine.SceneManagement; // 대상 씬 한정 검색

public static class ProjectKDay12HitboxSetup // 기존 적 외형을 유지하는 총기 피격 구성
{
    public static int ConfigureScene(Scene scene) // 현재 훈련장 적의 부위 설정
    {
        int configured = 0; // 처리한 적 개수
        foreach (GameObject root in scene.GetRootGameObjects()) // 대상 씬 루트만 검색
        {
            EnemyActor[] actors = root.GetComponentsInChildren<EnemyActor>(true); // 실제 체력 관리자 목록
            foreach (EnemyActor actor in actors) // 중복 루트와 무관하게 적마다 처리
            {
                if (ConfigureEnemy(actor)) // 유효한 머리 외형과 몸통 확인
                {
                    configured++; // 구성 완료 적 집계
                }
            }
        }

        return configured; // 결과 개수 반환
    }

    public static bool ConfigureEnemy(EnemyActor actor) // 기존 이동 충돌체와 별도 부위 구성
    {
        EnemyFirearmHitboxes rig = actor.GetComponent<EnemyFirearmHitboxes>(); // 기존 수동 조정 부위 구성
        if (rig != null && rig.Body != null && rig.Head != null) // 이미 저장된 부위가 있는지 확인
        {
            return true; // 사용자 조정 위치와 크기 보존
        }

        Renderer head = FindHead(actor); // 이름과 실제 렌더러를 가진 머리 조회
        CharacterController controller = actor.GetComponent<CharacterController>(); // 이동형 적의 몸통 기준
        CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>(); // 고정 더미의 몸통 기준
        if (head == null || (controller == null && capsule == null)) // 자동 위치 추정 근거 부족 확인
        {
            Debug.LogWarning("Day12: " + actor.name + "은 머리 또는 몸통 기준이 없어 기존 몸통 피해 판정을 유지합니다.", actor); // 안전한 기존 동작 안내
            return false; // 임의 머리 위치 생성 금지
        }

        Bounds localHead = LocalBounds(actor.transform, head.bounds); // 적 루트 기준 실제 머리 경계
        float radius = controller != null ? controller.radius : capsule.radius; // 기존 몸통 폭
        float height = controller != null ? controller.height : capsule.height; // 기존 몸통 높이
        Vector3 center = controller != null ? controller.center : capsule.center; // 기존 몸통 중심
        float bottom = center.y - height * 0.5f; // 발바닥 기준
        float bodyTop = localHead.min.y; // 머리 아래까지만 몸통 처리
        if (bodyTop <= bottom + 0.2f || localHead.size.y < 0.05f) // 비정상 경계값 확인
        {
            Debug.LogWarning("Day12: " + actor.name + "의 머리 경계를 수동 확인하세요.", actor); // 모델 기준 수정 안내
            return false; // 잘못된 피격 구성 중단
        }

        Transform parent = actor.transform.Find("__Day12Hitboxes"); // 저장된 부위 루트 조회
        if (parent == null) // 최초 부위 루트 생성
        {
            parent = ProjectKDay12ModelFactory.Node(actor.transform, "__Day12Hitboxes", Vector3.zero); // 적의 움직임을 따르는 별도 루트
            parent.gameObject.layer = actor.gameObject.layer; // 기존 적의 피격 레이어 보존
        }

        Vector3 bodyCenter = new Vector3(center.x, (bodyTop + bottom) * 0.5f, center.z); // 머리와 겹치지 않는 몸통 중심
        BoxCollider bodyBox = Zone(parent, "Body", bodyCenter, new Vector3(radius * 2f, bodyTop - bottom, radius * 2f), FirearmHitRegion.Body, actor, null); // 몸통 부위 생성
        BoxCollider headBox = Zone(parent, "Head", localHead.center, localHead.size, FirearmHitRegion.Head, actor, null); // 실제 머리 부위 생성
        rig = rig != null ? rig : actor.gameObject.AddComponent<EnemyFirearmHitboxes>(); // 이동 캡슐 대체 표식 추가
        rig.Configure(bodyBox, headBox); // 정상 부위 두 개를 동시에 연결
        EditorUtility.SetDirty(rig); // 부위 연결 저장 표시
        return true; // 설정 완료
    }

    public static BoxCollider Zone(Transform parent, string name, Vector3 center, Vector3 size, FirearmHitRegion region, EnemyActor actor, FirearmDamageProbe probe) // 총기 전용 피격 상자 생성
    {
        Transform node = parent.Find(name); // 이전 부분 생성 객체 조회
        if (node == null) // 새 부위 확인
        {
            node = ProjectKDay12ModelFactory.Node(parent, name, center); // 부위 중심 배치
        }

        node.gameObject.layer = parent.gameObject.layer; // 기존 피격 레이어 유지
        BoxCollider collider = node.GetComponent<BoxCollider>(); // 부위 충돌체 조회
        collider = collider != null ? collider : node.gameObject.AddComponent<BoxCollider>(); // 누락 부위 충돌체 추가
        collider.center = Vector3.zero; // 부위 기준점 사용
        collider.size = new Vector3(Mathf.Max(0.02f, size.x), Mathf.Max(0.02f, size.y), Mathf.Max(0.02f, size.z)); // 유효한 피격 크기
        collider.isTrigger = true; // 이동과 카메라 충돌을 바꾸지 않는 판정
        FirearmHitZone zone = node.GetComponent<FirearmHitZone>(); // 부위 표식 조회
        zone = zone != null ? zone : node.gameObject.AddComponent<FirearmHitZone>(); // 표식 하나만 추가
        zone.Configure(region, actor, probe); // 실제 피해 수신자 연결
        return collider; // 부위 참조 반환
    }

    private static Renderer FindHead(EnemyActor actor) // 명시된 머리 외형만 탐색
    {
        Renderer selected = null; // 선택된 머리 외형
        float largest = 0f; // 작은 장식보다 큰 머리 우선
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true)) // 적 모델 렌더러 순회
        {
            if (renderer.GetComponentInParent<EnemyActor>() != actor || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>() != null) // 다른 적과 효과와 글자 제외
            {
                continue; // 일반 모델만 검사
            }

            string name = renderer.name.ToLowerInvariant(); // 모델 이름 비교 준비
            if (!name.Contains("head") && !name.Contains("helmet")) // 머리 이름 근거 확인
            {
                continue; // 임의 몸체를 머리로 지정하지 않음
            }

            Vector3 size = renderer.bounds.size; // 실제 외형 크기
            float volume = size.x * size.y * size.z; // 외형 체적 비교
            if (volume > largest) // 더 큰 머리 본체 확인
            {
                selected = renderer; // 머리 렌더러 선택
                largest = volume; // 선택 기준 갱신
            }
        }

        return selected; // 명확한 머리가 없으면 누락 반환
    }

    private static Bounds LocalBounds(Transform root, Bounds world) // 회전한 적의 로컬 경계 계산
    {
        Bounds result = new Bounds(root.InverseTransformPoint(world.center), Vector3.zero); // 루트 기준 중심
        for (int i = 0; i < 8; i++) // 월드 경계 상자의 모서리 검사
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? world.min.x : world.max.x, (i & 2) == 0 ? world.min.y : world.max.y, (i & 4) == 0 ? world.min.z : world.max.z); // 현재 경계 모서리
            result.Encapsulate(root.InverseTransformPoint(corner)); // 모든 모서리를 포함한 부위 범위
        }

        return result; // 로컬 부위 경계 반환
    }
}
#endif // 게임 빌드에서 부위 배치 도구 제외
