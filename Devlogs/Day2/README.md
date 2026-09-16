# 2일차 개발 일지

## 개발 목표

플레이어의 기본 이동·점프·앉기·달리기와 3인칭 카메라를 구현해 이후 파쿠르 시스템을 연결할 수 있는 이동 기반을 완성한다.

## 작업 내용

- `PlayerMovement`를 추가해 WASD 기반 카메라 상대 이동 구현
- 기본 이동·달리기·앉기 속도와 가속·감속 처리 추가
- 이동 방향을 기준으로 플레이어가 부드럽게 회전하도록 수정
- 점프·중력·착지·공중 제어 처리 추가
- Coyote Time과 Jump Buffer를 추가해 점프 입력 보정
- 앉기 시 CharacterController 높이와 중심 위치가 변경되도록 구현
- 머리 위 장애물이 있을 때 일어서지 못하도록 충돌 검사 추가
- 일정 높이 아래로 추락하면 시작 위치로 복귀하는 기능 추가
- `ThirdPersonCamera`를 추가해 마우스와 게임패드 기반 3인칭 시점 회전 구현
- 카메라 상하 회전 각도 제한과 거리 보간 처리 추가
- SphereCast 기반 카메라 벽 충돌과 자동 거리 보정 추가
- ESC로 커서를 해제하고 화면 클릭으로 다시 잠그는 기능 추가
- `ProjectKDay2Setup`을 추가해 Test 씬의 Player·Camera 구성을 자동 적용
- Test 씬 Player에 CharacterController·PlayerInput·PlayerMovement 연결
- Main Camera에 ThirdPersonCamera를 연결하고 Player 추적 대상으로 지정
- 기존 Input System 액션을 이동·점프·앉기·달리기·카메라 입력에 연동
- 현재 Crouch 액션의 키보드 바인딩이 C 키로 설정된 상태 확인

## 확인 결과

- 원격 `main` 최신 커밋은 1일차 커밋으로 확인
- 2일차 구현 파일은 아직 원격 저장소에 올라가지 않은 상태 확인
- 2일차 구현은 기존 Input System과 Test 씬 구조를 유지하는 방향으로 구성
- 실제 Unity 에디터 컴파일과 Play 테스트 결과는 로컬 환경에서 최종 확인 필요
