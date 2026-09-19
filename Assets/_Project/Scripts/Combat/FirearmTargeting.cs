using UnityEngine; // 물리 충돌과 카메라 조준 기능

public static class FirearmTargeting // 총구 기준 실제 명중 판정
{
    public static bool CastShot(Camera camera, Transform owner, Vector3 muzzle, float range, int mask, out RaycastHit hit, out Vector3 endpoint, out bool muzzleBlocked) // 기존 호출 호환
    {
        return CastShot(camera, owner, muzzle, range, mask, Vector2.zero, out hit, out endpoint, out muzzleBlocked); // 이전 정중앙 판정 유지
    }

    public static bool CastShot(Camera camera, Transform owner, Vector3 muzzle, float range, int mask, Vector2 viewportOffset, out RaycastHit hit, out Vector3 endpoint, out bool muzzleBlocked) // 분산과 총구 경로 통합 검사
    {
        hit = default; // 명중 정보 초기화
        muzzleBlocked = false; // 총구 막힘 초기화
        range = Mathf.Max(0.1f, range); // 유효한 검사 거리 확보
        Vector3 body = EquipmentTargeting.BodyCenter(owner); // 플레이어 충돌체 중심
        Vector3 bodyToMuzzle = muzzle - body; // 몸과 총구 사이 검사 구간
        if (bodyToMuzzle.sqrMagnitude > 0.0001f && ClosestHit(body, bodyToMuzzle.normalized, bodyToMuzzle.magnitude, owner, mask, out hit, false)) // 몸 밖으로 벽을 뚫고 나온 총구 검사
        {
            muzzleBlocked = true; // 총구 가림 기록
            endpoint = hit.point; // 앞쪽 장애물 위치 기록
            return true; // 장애물 명중 반환
        }

        Collider[] overlaps = Physics.OverlapSphere(muzzle, 0.025f, mask, QueryTriggerInteraction.Ignore); // 레이 원점이 벽 내부에 있는 경우 검사
        for (int i = 0; i < overlaps.Length; i++) // 총구 주변 충돌체 순회
        {
            if (!EquipmentTargeting.IsOwnCollider(overlaps[i], owner)) // 자기 몸을 제외한 장애물 확인
            {
                muzzleBlocked = true; // 총구 막힘 기록
                endpoint = muzzle; // 발사 원점에서 중단
                return false; // 내부 콜라이더 너머 피해 방지
            }
        }

        Ray aimRay = camera != null ? camera.ViewportPointToRay(new Vector3(0.5f + viewportOffset.x, 0.5f + viewportOffset.y, 0f)) : new Ray(body, owner.forward); // 화면 중앙 조준선
        Vector3 aimPoint = aimRay.origin + aimRay.direction * (range + Vector3.Distance(aimRay.origin, muzzle)); // 조준 실패 시 끝점
        if (ClosestHit(aimRay.origin, aimRay.direction, range + Vector3.Distance(aimRay.origin, muzzle), owner, mask, out RaycastHit cameraHit)) // 카메라 앞 가장 가까운 조준 대상
        {
            aimPoint = cameraHit.point; // 실제 조준 위치 적용
        }

        Vector3 shot = aimPoint - muzzle; // 총구에서 목표로 향하는 방향
        if (shot.sqrMagnitude < 0.0001f || Vector3.Dot(shot, aimRay.direction) <= 0f) // 총구 뒤쪽을 향한 역방향 발사 검사
        {
            muzzleBlocked = true; // 가까운 카메라 장애물 표시
            endpoint = muzzle; // 역방향 탄도 중단
            return false; // 잘못된 명중 방지
        }

        float length = Mathf.Min(range, shot.magnitude + 0.01f); // 총구 기준 사거리 제한
        Vector3 direction = shot.normalized; // 발사 방향 정규화
        endpoint = muzzle + direction * length; // 빗나간 발사의 실제 끝점
        if (!ClosestHit(muzzle, direction, length, owner, mask, out hit)) // 총구에서 첫 충돌 검사
        {
            return false; // 유효 명중 없음
        }

        endpoint = hit.point; // 첫 충돌 위치 적용
        return true; // 가장 가까운 대상만 명중
    }

    private static bool ClosestHit(Vector3 origin, Vector3 direction, float distance, Transform owner, int mask, out RaycastHit closest, bool includeRegions = true) // 총기 부위와 일반 장애물 구분
    {
        closest = default; // 충돌 결과 초기화
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, mask, includeRegions ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore); // 검사 구간 충돌 수집
        float nearest = float.PositiveInfinity; // 최근접 거리 초기화
        bool found = false; // 결과 존재 상태
        for (int i = 0; i < hits.Length; i++) // 정렬되지 않은 결과 순회
        {
            Collider collider = hits[i].collider; // 현재 충돌체 확인
            if (collider == null || EquipmentTargeting.IsOwnCollider(collider, owner)) // 누락과 자기 몸 제외
            {
                continue; // 다음 충돌 검사
            }

            FirearmHitZone zone = collider.GetComponent<FirearmHitZone>(); // 총기용 부위 표식 확인
            if (collider.isTrigger && (zone == null || !zone.AcceptsHit)) // 일반 상호작용 트리거와 사망 부위 제외
            {
                continue; // 연막과 아이템 영역을 탄도에서 제외
            }

            if (includeRegions && zone == null) // 별도 부위가 있는 적의 이동용 몸통 확인
            {
                EnemyActor actor = collider.GetComponentInParent<EnemyActor>(); // 충돌체의 적 소유자 확인
                EnemyFirearmHitboxes boxes = actor != null ? actor.GetComponent<EnemyFirearmHitboxes>() : null; // 실제 대체 부위 준비 확인
                if (actor != null && !actor.IsDead && boxes != null && boxes.Ready) // 완성된 살아 있는 적만 대체
                {
                    continue; // 큰 이동 캡슐이 머리 판정을 가리는 문제 방지
                }
            }

            if (hits[i].distance >= nearest) // 더 먼 충돌 결과 제외
            {
                continue; // 다음 충돌 검사
            }

            nearest = hits[i].distance; // 가장 가까운 거리 갱신
            closest = hits[i]; // 실제 충돌 정보 갱신
            found = true; // 명중 결과 기록
        }

        return found; // 최근접 명중 반환
    }
}
