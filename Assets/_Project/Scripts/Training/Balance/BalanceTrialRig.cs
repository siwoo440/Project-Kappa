using System; // 필수 참조 오류 처리
using UnityEngine; // 원본 표적을 보존하는 임시 시험 구성

public sealed class BalanceTrialRig // 선택 레인의 원본을 잠시 대체하는 실행 표적
{
    private GameObject holder; // 이번 시험만 사용하는 부모
    private TrainingReactiveTarget source; // 복원할 원래 표적
    private bool sourceActive; // 원래 활성 상태
    public TrainingReactiveTarget Target // 시험 중 피해를 받을 표적
    {
        get; // 현재 값 조회
        private set; // 내부 상태 갱신
    }

    public void Create(TrainingCenterLane lane, Transform parent, float health, float armor) // 같은 모형과 거리의 시험 표적 준비
    {
        Dispose(); // 이전 시험 복사본 정리
        if (lane == null || lane.Target == null || lane.Target.Hinge == null || !lane.Target.gameObject.activeInHierarchy) // 정상 레인 확인
        {
            throw new InvalidOperationException("레인과 활성 원본 표적을 확인하세요."); // 손상된 시험 시작 차단
        }
        source = lane.Target; // 원본 참조 보존
        sourceActive = source.gameObject.activeSelf; // 원래 표시 상태 기록
        source.ResetTarget(); // 회전과 레일을 동일한 출발 자세로 초기화
        holder = new GameObject("BalanceTrial_RuntimeTarget"); // 저장하지 않는 실행 부모
        holder.transform.SetParent(parent, false); // 현재 센터에만 소속
        holder.SetActive(false); // 초기화 전 자동 동작 차단
        GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, true); // 실제 표적 모형과 피격 영역 복사
        copy.name = source.name + "_BalanceTrial"; // 원본과 구분하는 이름
        Target = copy.GetComponent<TrainingReactiveTarget>(); // 기존 넘어짐 동작 재사용
        Transform hinge = Target.Hinge; // 복사된 회전축 참조
        TextMesh label = copy.GetComponentInChildren<TextMesh>(true); // 기존 결과판 하나 사용
        Target.Configure(hinge, hinge.parent, hinge.GetComponentsInChildren<Collider>(true), label, source.Travel, 2f, health); // 모든 총기에 같은 체력과 이동 속도 적용
        BalanceTargetTag tag = copy.GetComponent<BalanceTargetTag>() ?? copy.AddComponent<BalanceTargetTag>(); // 임시 방어율 표식
        tag.Configure(armor); // 원본이 아닌 복사본에만 시험 방어율 적용
        Target.ResetTarget(); // 새 체력과 레일 원점 준비
        source.gameObject.SetActive(false); // 두 표적의 동시 피격 방지
        copy.SetActive(true); // 원본 활성 상태와 무관하게 복사본 준비
        holder.SetActive(true); // 모든 참조를 연결한 뒤 동작 시작
        Physics.SyncTransforms(); // 새로운 표적의 실제 피격 위치 반영
    }

    public void Dispose() // 결과 완료와 중단 공통 복원
    {
        if (holder != null) // 이번 시험 객체 확인
        {
            holder.SetActive(false); // 지연 삭제 전 피격 중단
            if (Application.isPlaying) // 실행 중 안전한 삭제
            {
                UnityEngine.Object.Destroy(holder); // 프레임 종료 시 복사본 정리
            }
            else // 에디터 검사 객체 정리
            {
                UnityEngine.Object.DestroyImmediate(holder); // 임시 검사 후 즉시 삭제
            }
        }
        if (source != null) // 원래 표적이 남아 있는지 확인
        {
            source.gameObject.SetActive(sourceActive); // 원래 활성 상태 복원
            source.ResetTarget(); // 다음 일반 사격 준비
        }
        holder = null; // 이전 실행 참조 제거
        source = null; // 이전 원본 참조 제거
        Target = null; // 이전 피격 대상 참조 제거
    }
}
