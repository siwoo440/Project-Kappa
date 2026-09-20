---
# 32일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: M-01 체크포인트·실패·재시도·결과 화면 구현  
검토 브랜치: `main`  
원격 기준 최신 커밋: `5259356d3d283546c313abb3932e640be5d6bbca` — `31일차 : 공통 MissionManager와 M-01 목표 진행 시스템 구현`

현재 원격 `main`에는 Day32 작업이 아직 올라오지 않은 상태다.

Day32 작업은 31일차 MissionManager 위에 체크포인트 저장, 플레이어 사망에 따른 임무 실패, 체크포인트 재시도, 임무 포기, 완료 결과 화면, 보상 중복 방지 기반을 추가하는 방향으로 진행했다.

초기 Day32 Editor 패처에서 `const string propertyNew`에 `Environment.NewLine`을 사용해 `CS0133` 컴파일 오류가 발생했고, `propertyNew`을 일반 `string`으로 변경해 수정했다.

---
## 1. 개발 목표

31일차에서 M-01을 수락하고 다음 목표로 진행해 임무를 완료할 수 있게 했다.

32일차에서는 임무 진행 중 실패했을 때 처음부터 다시 시작하지 않고 마지막 안전 체크포인트에서 복원할 수 있도록 한다.

또한 M-01 최종 목표 완료 뒤 Toast만 표시하고 끝내지 않고 별도의 결과 화면을 표시한다.

전체 흐름은 다음과 같다.

```text
M-01 수락
↓
체크포인트 저장
↓
목표 진행
↓
중간 체크포인트 갱신
↓
플레이어 사망
↓
MISSION FAILED
↓
체크포인트 재시도
↓
목표·위치·임무 물품 복원
↓
M-01 완료
↓
MISSION COMPLETE 결과 화면
```

---
## 2. 체크포인트 데이터

`Map32MissionCheckpointState`를 추가한다.

저장 항목은 다음과 같다.

```text
Mission ID
Checkpoint Label
Objective Index
Player Position
Player Rotation
Owned Mission Items
Delivered Mission Items
Captured Time
```

현재 저장은 플레이 세션 메모리 기반이다.

디스크 저장·불러오기는 이후 영구 Save 시스템에서 연결한다.

---
## 3. 체크포인트 저장 시점

M-01 진행 중 다음 시점에 체크포인트를 저장한다.

```text
임무 수락
→ 임무 시작

겹길 시장 도착
→ 겹길 시장 진입

운송 기록 조사 완료
→ 운송 기록 조사 완료

린 거점 복귀
→ 린 거점 복귀
```

각 체크포인트는 다음 목표를 시작하기 직전의 진행 상태를 저장한다.

---
## 4. MissionManager 체크포인트 연동

31일차 `Map31MissionManager`에 Day32 연동을 추가한다.

추가되는 주요 기능은 다음과 같다.

```text
ActiveObjectiveIndex
RestoreFromCheckpoint(...)
AbandonMission(...)
```

목표 완료 뒤 다음 Objective Index를 Journal에 반영한 후 체크포인트를 갱신한다.

재시도 시 저장된 Objective Index를 다시 활성 임무에 적용한다.

---
## 5. 임무 물품 스냅샷

`Map31MissionInventory`에 다음 API를 추가한다.

```text
CaptureOwnedItems()
CaptureDeliveredItems()
RestoreSnapshot(...)
```

체크포인트 저장 시 현재 보유한 임무 물품과 이미 인계한 임무 물품을 각각 배열로 복사한다.

재시도 시 현재 MissionInventory를 비우고 저장된 상태를 다시 복원한다.

---
## 6. 백야 인증 조각 복원

M-01의 `BAEKYA_TOKEN`은 일반 소모품이 아니라 임무 물품 슬롯에서 관리한다.

예를 들어 운송 기록 조사 완료 뒤 사망하면 다음 상태가 복원된다.

```text
운송 기록 조사
→ 완료 유지

백야 인증 조각
→ 보유 상태 복원

현재 Objective
→ 린 거점 복귀

Day29 Terminal
→ 린 거점 복귀 목표 표시
```

---
## 7. 플레이어 사망 감지

`Map32MissionCheckpointSystem`이 현재 플레이어의 `PlayerHealth.IsDead` 상태를 감시한다.

활성 임무가 있는 상태에서 플레이어가 사망하면 다음 흐름을 실행한다.

```text
Player Death
↓
Map31MissionManager.FailMission(...)
↓
Mission Journal 실패 상태
↓
Day29 Terminal 실패 상태
↓
Day32 실패 결과 화면
```

같은 사망 프레임에서 중복 실패 화면이 표시되지 않도록 별도 플래그를 사용한다.

---
## 8. 체크포인트 재시도 화면

실패 시 전체 화면 청록·파랑 UI를 표시한다.

주요 내용은 다음과 같다.

```text
MISSION FAILED

Mission ID
Mission Title

실패 원인
재시도 지점

[체크포인트 재시도]
[임무 포기]
```

일반 플레이 HUD는 결과 화면 중 숨긴다.

---
## 9. 플레이어 체크포인트 복원

재시도 버튼을 누르면 다음 상태를 복원한다.

```text
Player Position
Player Rotation
HP
Posture
Dead State
Movement Lock
Combat Lock
Horizontal Velocity
Vertical Velocity
Spawn Point
```

CharacterController를 잠시 비활성화한 뒤 위치를 변경해 순간 이동 충돌 보정을 피한다.

복원 후 `Physics.SyncTransforms()`를 호출해 위치를 즉시 물리에 반영한다.

---
## 10. PlayerHealth 복구 API

기존 `PlayerHealth`에 `RestoreFromCheckpoint()`를 추가한다.

복구 내용은 다음과 같다.

```text
dead = false
postureBroken = false
HP = Max
Posture = Max
Firearm Interrupt
Combat External Lock 해제
Defense External Lock 해제
Movement 활성
```

따라서 사망 뒤 동일 플레이어 GameObject를 재사용할 수 있다.

---
## 11. 수배 상태 초기화

체크포인트 재시도 직후 이전 전투의 수배 상태가 그대로 남는 문제를 막기 위해 `MapWantedSystem.ResetForCheckpoint()`를 추가한다.

초기화 대상은 다음과 같다.

```text
Heat
Wanted Stars
Currently Seen
Last Crime Time
Last Seen Time
Decay Gate
Last Known Position
Shared Target Position
Shared Target Duration
```

플레이어가 복원된 안전 위치를 새 기준 위치로 사용한다.

---
## 12. 신고 상태 초기화

Day26 신고 시스템에 체크포인트 초기화 API를 추가한다.

`Map26CrimeReportSystem.ClearForCheckpoint()`는 다음을 처리한다.

```text
활성 범죄 사건 제거
보안망 예약 신고 제거
시민 진행 신고 제거
목격자 상태 초기화
```

`Map26CitizenWitness.ForceResetForCheckpoint()`를 통해 시민별 놀람·신고 진행 상태도 복구한다.

---
## 13. 임무 포기

실패 결과 화면에서 `임무 포기`를 선택할 수 있다.

포기 시 다음을 처리한다.

```text
플레이어 마지막 체크포인트 위치 복원
체력·자세 복구
수배·신고 초기화
M-01 상태 Available 복구
Objective Index 0 초기화
MissionInventory 초기화
Day29 목표 해제
Day30 임무 창 첫 목표 상태 복구
```

따라서 Tab 창에서 다시 M-01을 처음부터 수락할 수 있다.

---
## 14. 임무 완료 결과 화면

M-01의 마지막 인계 목표가 완료되면 별도 결과 화면을 연다.

표시 항목은 다음과 같다.

```text
MISSION COMPLETE

Mission ID
Mission Title

Reward
Next Mission
Reward State

[확인 · 도시로 복귀]
```

M-01 완료 뒤 후속 임무 ID인 `M-02`도 결과 화면에 표시할 수 있도록 한다.

---
## 15. 완료 후 체크포인트 정리

임무 정상 완료 시 해당 M-01 체크포인트를 무효화한다.

완료한 임무에서 이전 실패 화면을 통해 다시 재시도하는 상황을 방지한다.

---
## 16. 보상 중복 방지 기반

`Map32MissionRewardLedger`를 추가한다.

현재 플레이 세션에서 최초로 완료 결과를 처리한 Mission ID를 기록한다.

```text
TryMarkGranted(...)
WasGranted(...)
ResetForNewGame()
```

같은 임무 결과 화면이 다시 발생해도 최초 지급 여부를 구분할 수 있다.

실제 크레딧·장비 지급 시스템은 이후 이 Ledger를 기준으로 연결한다.

---
## 17. 결과 화면 입력 잠금

실패·완료 결과 화면이 열리면 다음을 정지한다.

```text
Player Movement
Camera Look
Map UI
Firearm
Game Time
```

마우스 커서를 해제하고 표시해 UI 버튼을 선택할 수 있게 한다.

화면 종료 시 이전 시간 배율·카메라·지도 UI·커서 상태를 복구한다.

---
## 18. 일반 HUD와 결과 화면 충돌 방지

`Map30UITheme.HideGameplayHUD`를 확장한다.

기존:

```text
Tab Mission Window
```

외에도:

```text
Day32 Mission Result Screen
```

이 열려 있을 때 일반 플레이 HUD를 숨긴다.

결과 화면 중 다음 UI가 겹치지 않는다.

```text
HP HUD
QA HUD
Mission Terminal HUD
Minimap
Wanted HUD
Equipment HUD
Interaction HUD
Mission Toast
```

---
## 19. Tab 임무 창 충돌 방지

Day32 결과 화면이 열려 있을 때 `Map30MissionWindow.Open()`을 차단한다.

실패·완료 화면 위에 Tab 전체 임무 창이 겹쳐 열리는 상황을 방지한다.

---
## 20. Mission Toast 충돌 방지

`Map31MissionToastHUD`도 결과 화면이 열린 동안 렌더링하지 않는다.

Mission Failed·Complete 결과 화면과 이전 Objective Toast가 동시에 보이지 않게 한다.

---
## 21. Day32 런타임 자동 설치

`Map32MissionFlowBootstrap`을 추가한다.

씬 수정 없이 다음 오브젝트를 런타임에서 자동 생성한다.

```text
[Day32] Mission Checkpoint
[Day32] Mission Result Screen
```

---
## 22. 일회용 Day32 패처

기존 Day21·Day26·Day30·Day31 파일과 연결하기 위해 Editor 패처를 사용한다.

수정 대상은 다음과 같다.

```text
PlayerHealth.cs
Map31MissionInventory.cs
Map31MissionManager.cs
MapWantedSystem.cs
Map26CrimeReportSystem.cs
Map26CitizenWitness.cs
Map30UITheme.cs
Map30MissionWindow.cs
Map31MissionToastHUD.cs
```

패처는 모든 파일 패턴을 먼저 검증한 뒤 한 번에 저장한다.

적용 성공 후 패처 자체를 삭제한다.

---
## 23. CS0133 컴파일 오류 수정

초기 Day32 패처에서 다음 오류가 발생했다.

```text
Assets/_Project/Editor/Day32/ProjectKDay32CheckpointPatcher.cs(223,13):
error CS0133:
The expression being assigned to 'propertyNew' must be constant
```

원인은 다음 선언이었다.

```text
const string propertyNew =
    "..." + Environment.NewLine + "...";
```

`Environment.NewLine`은 컴파일 타임 상수가 아니므로 `const string` 초기화에 사용할 수 없다.

수정은 다음과 같다.

```text
string propertyNew =
    "..." + Environment.NewLine + "...";
```

동일 패처의 다른 `const string + Environment.NewLine` 충돌도 검사했으며 추가 동일 패턴은 확인되지 않았다.

---
## 24. 신규 파일

```text
Assets/_Project/Scripts/World/Map32/
├─ Map32MissionCheckpointSystem.cs
├─ Map32MissionFlowBootstrap.cs
├─ Map32MissionResultScreen.cs
└─ Map32MissionRewardLedger.cs
```

---
## 25. 자동 수정 대상

```text
Assets/_Project/Scripts/Combat/
└─ PlayerHealth.cs

Assets/_Project/Scripts/World/Map21/
└─ MapWantedSystem.cs

Assets/_Project/Scripts/World/Map26/
├─ Map26CrimeReportSystem.cs
└─ Map26CitizenWitness.cs

Assets/_Project/Scripts/World/Map30/
├─ Map30MissionWindow.cs
└─ Map30UITheme.cs

Assets/_Project/Scripts/World/Map31/
├─ Map31MissionInventory.cs
├─ Map31MissionManager.cs
└─ Map31MissionToastHUD.cs
```

---
## 26. Git 상태 확인

현재 원격 `main`의 최신 커밋은 다음이다.

```text
5259356d3d283546c313abb3932e640be5d6bbca
31일차 : 공통 MissionManager와 M-01 목표 진행 시스템 구현
```

원격에는 Day32 커밋과 `Devlogs/Day32/README.md`가 아직 없다.

따라서 Day32는 `--amend`가 아니라 새 커밋으로 추가한다.

---
## 27. 이후 확인 항목

- Day32 패처가 CS0133 없이 컴파일되는지 확인
- 패처 완료 후 Editor 스크립트가 자동 삭제되는지 확인
- M-01 수락 직후 첫 체크포인트 생성 확인
- 각 목표 완료 후 체크포인트 이름 변경 확인
- 운송 기록 조사 뒤 백야 인증 조각 상태 저장 확인
- M-01 진행 중 플레이어 사망 시 실패 화면 확인
- 체크포인트 재시도 시 플레이어 위치 복구 확인
- HP·Posture·이동·공격 잠금 정상 복구 확인
- Mission Inventory 체크포인트 상태 복원 확인
- 수배 별과 Heat 초기화 확인
- 진행 중 시민·보안 신고 제거 확인
- 임무 포기 후 M-01 Available 상태 복구 확인
- 최종 인계 후 Mission Complete 결과 화면 확인
- 완료 결과 화면과 일반 HUD가 겹치지 않는지 확인
- 동일 M-01 완료 보상 중복 기록 방지 확인

---
