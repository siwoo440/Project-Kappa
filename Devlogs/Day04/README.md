# 4일차 개발 일지

## 개발 목표

플레이어 상호작용, 행동 소음, 시야·청각 기반 탐지 상태를 구현하고 기존 Test 훈련장을 기능 구분이 명확한 테스트 환경으로 확장한다.

## 작업 내용

- `IInteractable` 공통 인터페이스를 추가해 상호작용 대상의 공통 구조 추가
- `PlayerInteraction`을 추가해 카메라 중앙 기준 근거리 상호작용 판정 추가
- 상호작용 키를 `F`로 변경하고 기존 Interact 입력 바인딩 수정
- `TestInteractable`을 추가해 미션 단말기·저장 장치·상점 키오스크 테스트 상호작용 추가
- `NoiseSystem`과 `NoiseEmitter`를 추가해 걷기·달리기·앉기·착지·상호작용 소음 이벤트 구조 추가
- 소음 발생 위치·반경·종류를 주변 탐지 센서에 전달하는 구조 추가
- `DetectionState`를 추가해 Idle·Suspicious·Detected·Searching 탐지 상태 추가
- `DetectionSensor`를 추가해 거리·시야각·엄폐 판정 기반 시야 탐지 추가
- 청각 반경 안의 소음 이벤트를 받아 의심 상태와 조사 위치를 저장하도록 추가
- 플레이어를 놓쳤을 때 마지막 위치를 기억하고 Searching 상태로 전환하도록 추가
- Test 훈련장의 Stealth 구역에 탐지 센서와 소음 테스트 요소 추가
- MissionTerminal·SaveStation·ShopKiosk를 실제 상호작용 테스트 대상으로 연결
- NoiseTestEmitter를 추가해 F키로 유인 소음을 발생시키는 테스트 구조 추가
- 각 훈련 구역에 START·MOVE·CAM·WALL RUN·WALL JUMP·WALL CLIMB·LEDGE·STEALTH·COMBAT·UTILITY 표식 추가
- MissionTerminal·SaveStation·ShopKiosk·NoiseTestEmitter의 외형을 세부 모델링으로 강화
- DetectionGuard·TargetDummy·Cover·WeaponStand 모델링과 구분용 라벨 추가
- 훈련장 요소의 역할을 시각적으로 구분할 수 있도록 구역별 색상·게이트·장식 구조 추가

## 확인 결과

- 원격 `main` 최신 커밋은 3일차 커밋으로 확인
- 원격 저장소에 4일차 상호작용·소음·탐지 스크립트가 아직 없는 상태 확인
- 4일차 작업은 새 커밋으로 추가해야 하는 상태
- 실제 Unity Play Mode에서 탐지 거리·게이지 속도·소음 반경은 이후 플레이 테스트에 따라 추가 조정 필요
