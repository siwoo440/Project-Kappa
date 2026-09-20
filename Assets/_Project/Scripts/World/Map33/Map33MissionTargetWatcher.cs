using ProjectK.Day31; // 공통 MissionManager 연동
using UnityEngine; // 표적 사망 감시

namespace ProjectK.Day33 // 33일차 M-02 잠입 암살 이름 공간
{
    [DisallowMultipleComponent] // 표적 감시 중복 방지
    [RequireComponent(typeof(EnemyActor))] // 적 생명 상태 필수
    public sealed class Map33MissionTargetWatcher : MonoBehaviour // M-02 표적 사망을 Eliminate 목표에 전달
    {
        private EnemyActor actor; // 감시할 적
        private GameObject evidence; // 사망 후 활성화할 암호화 장부
        private string targetKey = "M02_TARGET"; // MissionManager 표적 키
        private bool reported; // 제거 이벤트 전달 여부

        private void Awake() // 기본 생명 관리자 연결
        {
            actor = GetComponent<EnemyActor>(); // 적 생명 관리자 조회
        }

        public void Configure(EnemyActor targetActor, GameObject evidenceObject, string key) // 런타임 표적·증거 연결
        {
            actor = targetActor != null ? targetActor : GetComponent<EnemyActor>(); // 표적 참조 저장
            evidence = evidenceObject; // 증거 오브젝트 저장
            targetKey = string.IsNullOrWhiteSpace(key) ? "M02_TARGET" : key; // 표적 키 저장
            reported = false; // 신규 표적 제거 보고 허용
        }

        private void Update() // 적 사망 상태 감시
        {
            if (reported || actor == null || !actor.IsDead) // 신규 사망 여부 확인
            {
                return; // 처리 생략
            }

            if (evidence != null) // 증거 오브젝트 존재 확인
            {
                evidence.SetActive(true); // 암호화 장부 회수 가능 상태 활성화
            }

            Map31MissionManager manager = Map31MissionManager.Instance; // 중앙 MissionManager 조회
            if (manager != null && manager.NotifyTargetEliminated(targetKey)) // 현재 Eliminate 목표 처리 성공 확인
            {
                reported = true; // 중복 제거 이벤트 차단
            }
        }
    }
}
