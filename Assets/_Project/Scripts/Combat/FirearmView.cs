using UnityEngine; // 총기 표시와 임시 소리 기능

[DisallowMultipleComponent] // 표시 제어 중복 방지
public sealed class FirearmView : MonoBehaviour // 권총 반동과 재장전 표시
{
    [SerializeField] private Transform muzzle; // 총구 기준 위치
    [SerializeField] private Transform movingParts; // 반동을 받는 외형
    [SerializeField] private Transform magazine; // 재장전 탄창 외형
    [SerializeField] private GameObject muzzleFlash; // 짧은 총구 발광 표시
    [SerializeField] private GameObject suppressorVisual; // 선택형 테스트 소음기 외형
    [SerializeField] private Transform suppressedMuzzle; // 소음기 끝 실제 총구
    private bool suppressed; // 현재 소음기 상태
    private Vector3 flashScale; // 총구 효과 원래 크기
    public bool HasSuppressor => suppressorVisual != null && suppressedMuzzle != null; // 소음기 구성 검사
    private AudioSource audioSource; // 검증용 발사 소리
    private AudioClip generatedClip; // 실행 중 생성한 효과음
    private Vector3 partsHome; // 외형 초기 위치
    private Quaternion partsRotation; // 외형 초기 회전
    private Vector3 magazineHome; // 탄창 초기 위치
    private bool poseCaptured; // 실행 중 초기 외형 저장 여부
    private float recoil; // 현재 반동 비율
    private float flashUntil; // 총구 표시 종료 시각

    public Transform Muzzle => suppressed && HasSuppressor ? suppressedMuzzle : muzzle; // 실제 총구 조회

    public void Configure(Transform barrelEnd, Transform parts, Transform clip, GameObject flash) // 프리팹 표시 참조 연결
    {
        muzzle = barrelEnd; // 총구 저장
        movingParts = parts; // 외형 저장
        magazine = clip; // 탄창 저장
        muzzleFlash = flash; // 총구 효과 저장
    }

    public void ConfigureSuppressor(GameObject visual, Transform endpoint) // 에디터 소음기 참조 연결
    {
        suppressorVisual = visual; // 소음기 모형 저장
        suppressedMuzzle = endpoint; // 소음기 총구 저장
    }

    public void SetSuppressed(bool value) // 총구와 소음기 외형 동기화
    {
        suppressed = value && HasSuppressor; // 존재하는 소음기만 적용
        if (suppressorVisual != null) // 모형 참조 확인
        {
            suppressorVisual.SetActive(suppressed); // 장착 상태에 맞춘 외형 표시
        }

        if (muzzleFlash != null && Muzzle != null) // 효과와 총구 유효성 확인
        {
            muzzleFlash.transform.SetParent(Muzzle, false); // 실제 끝단에서만 총구 효과 표시
            muzzleFlash.transform.localPosition = new Vector3(0f, 0f, 0.045f); // 총구 앞 효과 위치
            muzzleFlash.SetActive(false); // 전환 프레임의 이전 섬광 제거
        }

        flashUntil = 0f; // 이전 섬광 시간 종료
    }

    private void Awake() // 초기 외형과 소리 준비
    {
        partsHome = movingParts != null ? movingParts.localPosition : Vector3.zero; // 외형 원점 기억
        partsRotation = movingParts != null ? movingParts.localRotation : Quaternion.identity; // 외형 기본 회전 기억
        magazineHome = magazine != null ? magazine.localPosition : Vector3.zero; // 탄창 원점 기억
        flashScale = muzzleFlash != null ? muzzleFlash.transform.localScale : Vector3.one; // 원래 총구 섬광 크기 저장
        poseCaptured = true; // 초기 외형 저장 완료
        audioSource = gameObject.AddComponent<AudioSource>(); // 총기 전용 임시 소리 연결
        audioSource.playOnAwake = false; // 장착 시 자동 소리 방지
        audioSource.spatialBlend = 0.5f; // 근거리 입체감 적용
        audioSource.volume = 0.14f; // 검증용 소리 크기 제한
        BuildShotClip(); // 짧은 전자 발사 소리 생성
    }

    private void Update() // 반동과 발광 갱신
    {
        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 9f); // 반동 복구
        if (movingParts != null) // 외형 참조 확인
        {
            movingParts.localPosition = partsHome + Vector3.back * (0.065f * recoil); // 총기 후퇴 반동
            movingParts.localRotation = partsRotation * Quaternion.Euler(-6f * recoil, 0f, 0f); // 총기 들림 반동
        }

        if (muzzleFlash != null) // 총구 표시 참조 확인
        {
            muzzleFlash.SetActive(Time.time < flashUntil); // 짧은 총구 효과 표시
        }
    }

    public void ShowShot() // 이전 발사 호출 호환
    {
        ShowShot(1f); // 기본 음량 발사 표시
    }

    public void ShowShot(float volumeScale) // 소음기 음량과 총구 피드백
    {
        recoil = 1f; // 반동 시작
        flashUntil = Time.time + 0.045f; // 짧은 발광 시간 지정
        if (muzzleFlash != null) // 효과 참조 확인
        {
            muzzleFlash.transform.localScale = flashScale * (suppressed ? 0.4f : 1f); // 소음기 섬광 축소
            muzzleFlash.SetActive(true); // 발사 프레임 즉시 표시
        }

        if (audioSource != null && generatedClip != null) // 효과음 준비 확인
        {
            audioSource.PlayOneShot(generatedClip, Mathf.Clamp01(volumeScale)); // 검증용 전자 발사음 재생
        }
    }

    public void ShowReload(float progress, bool reloading) // 탄창 꺼냄과 삽입 표시
    {
        if (poseCaptured && magazine != null) // 실행 중 초기 위치를 저장한 탄창만 갱신
        {
            float offset = reloading ? Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * 0.22f : 0f; // 재장전 중간의 탄창 이동량
            magazine.localPosition = magazineHome + Vector3.down * offset; // 탄창 외형만 이동
        }
    }

    private void BuildShotClip() // 외부 음원 없는 검증용 효과음 생성
    {
        const int sampleRate = 22050; // 효과음 샘플 속도
        float[] samples = new float[2205]; // 0.1초 효과음 배열
        for (int i = 0; i < samples.Length; i++) // 소리 표본 생성
        {
            float t = i / (float)sampleRate; // 현재 소리 시각
            float envelope = Mathf.Exp(-t * 55f); // 빠르게 줄어드는 음량
            samples[i] = (Mathf.Sin(2f * Mathf.PI * (950f * t - 2200f * t * t)) + 0.25f * Mathf.Sin(2f * Mathf.PI * 3400f * t)) * envelope * 0.45f; // 짧은 전자 펄스 합성
        }

        generatedClip = AudioClip.Create("D10_TestShot", samples.Length, 1, sampleRate, false); // 임시 오디오 생성
        generatedClip.SetData(samples, 0); // 소리 표본 연결
    }

    private void OnDisable() // 장착 해제 표시 정리
    {
        if (poseCaptured && movingParts != null) // 실제 초기 외형을 기억한 경우만 복구
        {
            movingParts.localPosition = partsHome; // 반동 후 위치 잔류 방지
            movingParts.localRotation = partsRotation; // 반동 후 기울기 잔류 방지
        }

        recoil = 0f; // 반동 초기화
        flashUntil = 0f; // 발광 초기화
        ShowReload(0f, false); // 탄창 원위치
        if (muzzleFlash != null) // 총구 표시 확인
        {
            muzzleFlash.SetActive(false); // 비활성 총기 발광 제거
        }
    }

    private void OnDestroy() // 실행 중 효과음 정리
    {
        if (generatedClip != null) // 임시 소리 존재 확인
        {
            Destroy(generatedClip); // 생성된 소리 메모리 해제
        }
    }
}
