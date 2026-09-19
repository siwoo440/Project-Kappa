using UnityEngine; // 유니티 충돌 기능

public static class EquipmentTargeting // 장비 조준과 장애물 검사
{
    public static bool IsOwnCollider(Collider collider, Transform owner) // 자기 몸 충돌 검사
    {
        return collider != null && owner != null && (collider.transform == owner || collider.transform.IsChildOf(owner)); // 자기 하위 오브젝트 포함
    }

    public static Vector3 BodyCenter(Transform owner) // 실제 충돌체 중심 조회
    {
        CharacterController controller = owner.GetComponent<CharacterController>(); // 캐릭터 충돌체 조회
        return controller != null && controller.enabled ? controller.bounds.center : owner.position + Vector3.up; // 중심 위치 선택
    }

    public static bool TryAim(Camera camera, Transform owner, float range, out RaycastHit closest, out Vector3 end) // 카메라 조준 위치 검색
    {
        closest = default; // 충돌 정보 초기화
        Vector3 origin = camera != null ? camera.transform.position : BodyCenter(owner); // 조준 시작점 선택
        Vector3 direction = camera != null ? camera.transform.forward : owner.forward; // 조준 방향 선택
        float length = Mathf.Max(0.1f, range) + Vector3.Distance(origin, BodyCenter(owner)); // 뒤쪽 카메라 거리 보정
        end = origin + direction * length; // 빗나갔을 때 조준 끝점
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, length, ~0, QueryTriggerInteraction.Ignore); // 조준 경로 충돌 조회
        float nearest = float.MaxValue; // 최근접 거리 초기화
        bool found = false; // 충돌 여부 초기화

        for (int i = 0; i < hits.Length; i++) // 충돌 후보 순회
        {
            if (IsOwnCollider(hits[i].collider, owner) || hits[i].distance >= nearest) // 자기 몸과 먼 충돌 제외
            {
                continue; // 다음 충돌 확인
            }

            closest = hits[i]; // 최근접 충돌 저장
            nearest = hits[i].distance; // 최근접 거리 저장
            found = true; // 충돌 성공 기록
        }

        if (found) // 실제 충돌 확인
        {
            end = closest.point; // 실제 조준 끝점 저장
        }

        Vector3 fromBody = end - BodyCenter(owner); // 플레이어 기준 발사 방향
        if (fromBody.magnitude > range) // 보조장비 사거리 초과 확인
        {
            end = BodyCenter(owner) + fromBody.normalized * range; // 사거리 끝점 제한
            return false; // 사거리 밖 명중 제외
        }

        return found; // 명중 여부 반환
    }

    public static bool HasClearPath(Vector3 start, Vector3 end, Transform owner, Transform allowedTarget, int mask = ~0) // 벽을 통과하는 타격 방지
    {
        Vector3 delta = end - start; // 검사 구간 계산
        if (delta.sqrMagnitude < 0.000001f) // 같은 위치 검사
        {
            return true; // 막힘 없는 짧은 경로
        }

        RaycastHit[] hits = Physics.RaycastAll(start, delta.normalized, Mathf.Max(0f, delta.magnitude - 0.025f), mask, QueryTriggerInteraction.Ignore); // 경로 장애물 조회
        for (int i = 0; i < hits.Length; i++) // 장애물 후보 순회
        {
            Transform hit = hits[i].transform; // 충돌 대상 조회
            if (IsOwnCollider(hits[i].collider, owner) || (allowedTarget != null && (hit == allowedTarget || hit.IsChildOf(allowedTarget)))) // 사용자와 목표 충돌 제외
            {
                continue; // 다른 장애물 검사
            }

            return false; // 경로 장애물 발견
        }

        return true; // 경로 통과 가능
    }

    public static bool TryThrowOrigin(Transform owner, out Vector3 origin) // 벽 안 투척 시작 방지
    {
        Vector3 center = BodyCenter(owner) + Vector3.up * 0.15f; // 가슴 부근 시작 위치
        origin = center + owner.forward * 0.65f; // 손 앞쪽 투척 위치
        return HasClearPath(center, origin, owner, null); // 투척 공간 확인
    }
}
