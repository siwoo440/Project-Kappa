#if UNITY_EDITOR // 복사된 씬에서 게임 기능만 보존
using System; // 선행 자료 누락 보고
using System.Collections.Generic; // 중복 없는 보존 대상
using UnityEditor; // 프리팹 인스턴스와 저장 참조
using UnityEngine; // 기존 플레이어와 카메라
using UnityEngine.InputSystem; // 기존 입력 재연결
using UnityEngine.SceneManagement; // 임시 Map 씬 범위

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public static class MapPlayerImport // 원본 Test가 아닌 작업 복사본만 정리
    {
        public sealed class Result // 보존한 실제 게임 기능
        {
            public GameObject Player; // 기존 컴포넌트가 있는 플레이어
            public Camera Camera; // 기존 3인칭 카메라
            public float FootLift; // 지면에서 플레이어 피벗까지의 보정
        }

        public static Result Prepare(Scene copiedScene) // 동일한 복사 씬 안에서 참조를 유지한 정리
        {
            TrainingCenterMigration.Snapshot source = TrainingCenterMigration.Inspect(copiedScene); // 검증된 기존 플레이어와 화면 목록
            HashSet<GameObject> candidates = new HashSet<GameObject>(source.Services); // 중복 없는 보존 목록
            HashSet<GameObject> roots = new HashSet<GameObject>(); // 부모에 포함되지 않는 보존 객체
            foreach (GameObject value in candidates) // 최상위 보존 단위 선별
            {
                if (value == null) // 삭제된 참조 확인
                {
                    continue; // 누락 대상 제외
                }
                bool ancestorKept = false; // 다른 보존 부모 존재 여부
                for (Transform parent = value.transform.parent; parent != null; parent = parent.parent) // 상위 연결 조사
                {
                    if (candidates.Contains(parent.gameObject)) // 부모가 이미 보존 대상인지 확인
                    {
                        ancestorKept = true; // 원래 자식 구조 유지
                        break; // 중복 검사 중단
                    }
                }
                if (!ancestorKept) // 별도 이동할 최상위 대상
                {
                    roots.Add(value); // 실제 보존 루트 등록
                }
            }
            foreach (GameObject value in roots) // 게임 기능만 씬 루트로 이동
            {
                GameObject prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(value); // 복사된 프리팹 상위 확인
                if (prefab != null) // 인스턴스 재부모 연결 준비
                {
                    PrefabUtility.UnpackPrefabInstance(prefab, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); // 원본 에셋 수정 없는 복사본 해제
                }
                value.transform.SetParent(null, true); // 객체 ID와 내부 참조를 보존한 분리
            }
            foreach (GameObject old in copiedScene.GetRootGameObjects()) // 임시 복사본의 옛 배치 순회
            {
                if (!roots.Contains(old)) // 플레이어와 실제 게임 기능이 아닌 대상
                {
                    UnityEngine.Object.DestroyImmediate(old); // 복사본의 훈련장과 적과 계측 도구만 제거
                }
            }
            ThirdPersonCamera[] cameras = TrainingCenterMigration.Components<ThirdPersonCamera>(copiedScene); // 복사본의 실제 카메라만 조회
            if (cameras.Length != 1) // 중복되거나 누락된 카메라 검사
            {
                throw new InvalidOperationException("Test의 ThirdPersonCamera가 정확히 하나 필요합니다."); // 모호한 카메라를 임의 선택하지 않음
            }
            Camera camera = cameras[0].GetComponent<Camera>(); // 기존 카메라 컴포넌트
            camera.tag = "MainCamera"; // Map의 단일 메인 카메라
            camera.enabled = true; // 기본 표시 상태
            cameras[0].Configure(source.Player.transform, source.Player.GetComponent<PlayerInput>()); // 복사본 플레이어를 명시적으로 연결
            source.Player.GetComponent<PlayerMovement>().Configure(camera); // 복사본 내부의 이동 기준
            CharacterController body = source.Player.GetComponent<CharacterController>(); // 정확한 피벗과 발 위치
            if (body == null || source.Player.transform.lossyScale != Vector3.one) // 비정상 사용자 스케일 확인
            {
                throw new InvalidOperationException("플레이어 CharacterController와 스케일 1,1,1을 확인하세요."); // 잘못된 도착점 높이 생성 방지
            }
            float lift = Mathf.Max(0f, body.height * 0.5f - body.center.y) + body.skinWidth + 0.08f; // 바닥 접촉보다 조금 높은 시작점
            Result result = new Result(); // 보존한 참조 묶음
            result.Player = source.Player; // 플레이어 연결
            result.Camera = camera; // 카메라 연결
            result.FootLift = lift; // 도착점 높이 보정
            return result; // 월드 생성으로 전달
        }
    }
}
#endif
