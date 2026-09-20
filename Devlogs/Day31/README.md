---
# 31일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 공통 MissionManager와 M-01 목표 진행 시스템  
검토 브랜치: `main`  
작성 전 기준 커밋: `589d854fe38c546b96e37b325e3e95491019646f` — `31`  
이전 기준 커밋: `85d4cec0fddd2fb5114298ad67ae272fb3b874c1` — `30일차 : 전체 임무 창과 청록·파란색 HUD 통합`

현재 최신 커밋에는 31일차 공통 MissionManager, 목표 데이터, 임무 물품 슬롯, 임무 상호작용, 목표 알림 HUD, M-01 실제 진행 프로토타입과 Day30 전체 임무 창 수락·재시도 연동이 포함되어 있으며 `Devlogs/Day31/README.md`만 없는 상태다.

README 추가 후 기존 최신 커밋을 `--amend`하여 31일차 커밋 메시지를 정식 형식으로 정리한다.

---
## 1. 개발 목표

29일차에서 현재 목표를 표시하는 단말기 HUD를 만들고, 30일차에서 보유한 모든 임무를 확인하는 Tab 전체 임무 창을 추가했다.

31일차에서는 두 UI가 실제 게임 진행 데이터를 받도록 공통 MissionManager를 추가한다.

임무를 수락하면 첫 목표가 활성화되고, 위치 도착이나 F 상호작용으로 목표가 완료되며 다음 목표로 자동 진행되도록 한다.

첫 실제 프로토타입 임무로 M-01을 연결해 다음 전체 흐름을 검증한다.

```text
Tab 임무 창
↓
M-01 수락
↓
겹길 시장 이동
↓
운송 기록 단말기 조사
↓
백야 인증 조각 획득
↓
린의 옥상 작업실 복귀
↓
분석 단말기에 임무 물품 인계
↓
MISSION COMPLETE
```

---
## 2. 공통 MissionData 구조

`Map31MissionDefinition`을 ScriptableObject 기반 공통 임무 정의로 추가한다.

다음 데이터를 가진다.

```text
Mission ID
Title
Type
Client
Region
Summary
Reward
Unlock Condition
Next Mission ID
Category
Failure Conditions
Objectives
```

Unity의 `CreateAssetMenu`도 제공해 이후 실제 M-01~M-08, S-01~S-06을 에셋으로 분리할 수 있게 한다.

현재 M-01은 코드에서 런타임 MissionData로 생성하지만 이후 동일 구조의 에셋 데이터로 교체할 수 있다.

---
## 3. 공통 목표 데이터

`Map31MissionObjectiveData`를 추가한다.

지원 목표 유형은 다음과 같다.

```text
Reach
Interact
Acquire
Deliver
Eliminate
Investigate
Escape
```

각 목표는 다음 값을 가진다.

```text
Objective ID
Objective Type
Description
Target Place ID
Runtime Target Key
Target Label
Completion Radius
Grant Item ID
Required Item ID
Deliver Item ID
```

기존 `MapPoint` 목표와 런타임 생성 오브젝트 목표를 모두 지원한다.

---
## 4. MissionManager

`Map31MissionManager`를 중앙 임무 관리자 역할로 추가한다.

주요 책임은 다음과 같다.

```text
임무 정의 등록
임무 수락 가능 여부 확인
임무 수락
현재 목표 관리
위치 목표 자동 완료
상호작용 목표 처리
임무 물품 획득·인계
목표 완료 후 다음 목표 진행
임무 완료
임무 실패
실패 임무 재시도
Day29 단말기 동기화
Day30 Journal 동기화
```

한 번에 하나의 활성 임무를 진행하도록 구성한다.

---
## 5. 임무 런타임 상태

공통 진행 상태는 다음과 같다.

```text
Available
Active
Completed
Failed
```

임무마다 `Map31MissionRuntimeState`를 별도로 유지한다.

현재 목표 순번은 `ObjectiveIndex`로 관리한다.

모든 목표가 완료되면 해당 임무 상태를 `Completed`로 전환한다.

---
## 6. Day30 전체 임무 창 연동

Day30 `Map30MissionWindow`의 상세 패널 하단에 실제 임무 액션 영역을 추가한다.

M-01은 상태별로 다음 내용을 표시한다.

```text
보유
→ 임무 수락 및 추적

추적 중
→ 현재 목표 추적 중 · Tab을 닫고 진행

완료
→ 임무 완료

실패
→ 임무 처음부터 재시도
```

아직 MissionManager에 연결되지 않은 M-02와 S-01은 진행 시스템 연결 대기 상태로 유지한다.

---
## 7. Day30 Mission Journal 실제 데이터 교체

30일차의 M-01은 임무 창 검증을 위한 Development Seed였다.

31일차 MissionManager가 준비되면 같은 `M-01` ID를 사용해 실제 진행 가능한 데이터로 교체한다.

Day30 Journal에는 다음 실제 목표 단계가 표시된다.

```text
1. 겹길 시장의 운송 기록 지점으로 이동
2. 현장 단말기에서 과거 운송 기록 조사
3. 백야 인증 조각을 가지고 린의 옥상 작업실로 복귀
4. 린의 분석 단말기에 백야 인증 조각 인계
```

목표 진행 시 현재 Objective Index도 함께 갱신한다.

---
## 8. Day29 플레이어 단말기 연동

임무 수락 시 `Map29TerminalObjectiveProvider`를 이용해 첫 목표를 연결한다.

목표가 바뀔 때마다 다음 정보가 자동 갱신된다.

```text
Mission ID
Mission Title
Objective Text
Target Transform
Target Label
Distance
Layer
Vector
```

따라서 임무 진행과 Day29 거리·층·방향 표시가 같은 데이터를 사용한다.

---
## 9. 목표 완료 전환

위치형 목표는 플레이어가 완료 반경 안에 들어오면 자동 완료한다.

현재 지원되는 자동 도착 목표는 다음과 같다.

```text
Reach
Escape
```

현재 목표 완료 후 다음 흐름을 사용한다.

```text
OBJECTIVE COMPLETE
↓
짧은 완료 표시
↓
Objective Index 증가
↓
NEW OBJECTIVE
↓
Day29 단말기 목표 갱신
↓
Day30 임무 창 현재 목표 갱신
```

---
## 10. MissionInventory

`Map31MissionInventory`를 추가한다.

일반 장비·소모품 인벤토리와 분리된 임무 전용 슬롯이다.

상태는 다음 두 종류로 관리한다.

```text
Owned
Delivered
```

주요 API는 다음과 같다.

```text
Has()
Add()
Deliver()
ClearAll()
WasDelivered()
```

필수 증거·인증 조각·화물 같은 임무 물품을 일반 소모품과 분리해 관리할 수 있다.

---
## 11. M-01 임무 물품

31일차 M-01에서 첫 임무 물품으로 다음 ID를 사용한다.

```text
BAEKYA_TOKEN
```

HUD 표시 이름은 다음과 같다.

```text
백야 인증 조각
```

현장 단말기를 조사하면 MissionInventory에 추가한다.

린의 분석 단말기에서는 이 물품을 실제로 보유한 경우에만 인계할 수 있다.

인계가 완료되면 보유 상태에서 제거하고 Delivered 기록을 남긴다.

---
## 12. MissionInteractable

`Map31MissionInteractable`을 추가한다.

기존 `IInteractable`과 `PlayerInteraction` 시스템을 그대로 사용한다.

따라서 기존 F 입력 흐름을 수정하지 않고 임무 목표를 연결한다.

다음 정보를 가진다.

```text
Mission ID
Objective ID
Interaction Label
Grant Item ID
Required Item ID
Deliver Item ID
```

현재 활성 목표와 ID가 맞을 때만 상호작용 가능 상태가 된다.

---
## 13. M-01 현장 조사 단말기

겹길 시장의 `GYEOPGIL_PLAZA` 도착점을 기준으로 런타임 조사 단말기를 생성한다.

오브젝트 이름은 다음과 같다.

```text
M01_ArchiveTerminal
```

상호작용 문구:

```text
[F] 운송 기록 조사
```

조사 완료 시 백야 인증 조각을 지급하고 다음 목표로 진행한다.

---
## 14. M-01 린 분석 단말기

린 거점의 `LIN_HOME` 도착점을 기준으로 런타임 분석 단말기를 생성한다.

오브젝트 이름은 다음과 같다.

```text
M01_LinTerminal
```

상호작용 문구:

```text
[F] 백야 인증 조각 인계
```

`BAEKYA_TOKEN`을 실제로 보유해야 완료할 수 있다.

인계 완료 후 M-01의 모든 필수 목표가 끝나므로 Mission Complete 상태로 전환한다.

---
## 15. 임무 단말기 시각 구성

임무 단말기는 현재 프로토타입 단계이므로 Unity 기본 Primitive를 이용해 자동 생성한다.

구성은 다음과 같다.

```text
Terminal Body
├─ MissionScreen
└─ MissionBeacon
```

본체는 짙은 남청색으로 표시한다.

화면과 상태선은 청록 발광 재질을 사용한다.

현재 UI 테마와 시각적으로 연결되도록 구성한다.

---
## 16. 목표 알림 HUD

`Map31MissionToastHUD`를 추가한다.

화면 상단 중앙에 짧은 상태 알림을 표시한다.

지원 알림은 다음과 같다.

```text
MISSION ACCEPTED
OBJECTIVE COMPLETE
NEW OBJECTIVE
MISSION ITEM ACQUIRED
MISSION COMPLETE
MISSION FAILED
MISSION ITEM
```

Day30 전체 임무 창이 열려 있을 때는 Toast HUD를 숨긴다.

---
## 17. 임무 완료

마지막 인계 목표가 완료되면 다음 흐름을 실행한다.

```text
M-01 Runtime Status
→ Completed

Day30 Journal
→ 완료

Day29 Terminal
→ 임무 완료 표시

Mission Toast
→ MISSION COMPLETE

Active Mission
→ 해제
```

단말기 완료 문구는 잠시 유지한 뒤 대기 상태로 돌아간다.

---
## 18. 임무 실패·재시도 기반

`FailMission()` 공통 API를 추가한다.

현재 진행 중인 임무를 실패 상태로 변경하고 다음 시스템을 함께 갱신한다.

```text
Map30 Mission Journal
Map29 Terminal HUD
Mission Toast HUD
```

실패한 M-01은 Tab 임무 창에서 처음부터 재시도할 수 있다.

현재 31일차에서는 실제 사망·화물 손실과 FailMission 연결은 다음 단계로 남긴다.

---
## 19. 이후 시스템 연결 API

향후 실제 아이템 시스템에서 다음 API를 호출할 수 있다.

```text
NotifyItemAcquired(...)
```

Acquire 목표와 물품 ID가 일치하면 자동으로 목표를 완료한다.

향후 적 사망 시스템에서는 다음 API를 사용할 수 있다.

```text
NotifyTargetEliminated(...)
```

Eliminate 목표의 Target Key가 일치하면 목표를 완료한다.

이 구조는 이후 M-02의 잠입 암살 목표에 재사용한다.

---
## 20. 런타임 자동 설치

`Map31MissionBootstrap`을 추가한다.

별도 씬 설정 없이 다음 오브젝트를 자동 생성한다.

```text
[Day31] Mission Manager
[Day31] Mission Toast HUD
```

MissionManager 오브젝트에는 `Map31MissionInventory`도 자동으로 연결한다.

따라서 프로젝트에 ZIP을 덮어쓴 뒤 Map 씬을 직접 수정할 필요가 없다.

---
## 21. 수정 파일

```text
Assets/_Project/Scripts/World/Map30/
└─ Map30MissionWindow.cs
```

Day31 실제 MissionManager 수락·재시도 액션을 추가한다.

---
## 22. 신규 파일

```text
Assets/_Project/Scripts/World/Map31/
├─ Map31MissionBootstrap.cs
├─ Map31MissionData.cs
├─ Map31MissionInteractable.cs
├─ Map31MissionInventory.cs
├─ Map31MissionManager.cs
└─ Map31MissionToastHUD.cs
```

---
## 23. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `589d854f` 확인
- 최신 커밋 메시지가 임시 제목 `31`인 상태 확인
- `Devlogs/Day31/README.md`가 원격에 없는 상태 확인
- `Map31MissionBootstrap` 추가 확인
- `Map31MissionData` 추가 확인
- `Map31MissionInteractable` 추가 확인
- `Map31MissionInventory` 추가 확인
- `Map31MissionManager` 추가 확인
- `Map31MissionToastHUD` 추가 확인
- Day30 임무 창 Day31 수락·재시도 연결 확인
- Day29 목표 Provider 연동 확인
- Day30 Mission Journal 상태·목표 연동 확인
- M-01 실제 목표 4단계 구성 확인
- 겹길과 린 MapPoint 기반 런타임 단말기 생성 구조 확인
- 백야 인증 조각 임무 물품 슬롯 처리 확인
- 위치·조사·인계 목표 진행 구조 확인
- 이후 Acquire·Eliminate 이벤트 연결 API 확인
- 검사한 Day31 관련 C#에서 기존 `Input.GetKeyDown` 사용 없음 확인
- 검사한 Day31 관련 C#에서 `System.Object`와 `UnityEngine.Object` 모호성 없음 확인
- 검사한 C# 파일의 중괄호 균형 이상 없음 확인
- 이번 검토 환경에서는 Unity Editor 전체 컴파일과 실제 M-01 완주 Play Mode를 직접 실행하지 않음

---
## 24. 이후 확인 항목

- Tab에서 M-01 수락 버튼 동작 확인
- 수락 뒤 Day29 단말기가 M-01 첫 목표로 변경되는지 확인
- 겹길 도착 반경 판정 확인
- 겹길 운송 기록 단말기의 F 안내 확인
- 조사 후 백야 인증 조각 획득 알림 확인
- 목표가 린 거점 복귀로 변경되는지 확인
- 린 도착 후 분석 단말기 F 안내 확인
- 인증 조각 미보유 상태에서 인계가 차단되는지 확인
- 인계 후 M-01 완료 상태 확인
- Tab 목록의 M-01 목표 표시가 ✓ 상태로 변경되는지 확인
- 완료 후 단말기 대기 상태 복귀 확인
- 목표 진행 중 Tab을 열어도 진행 상태가 유지되는지 확인
- 이후 사망·화물 손실과 FailMission 연결 준비 확인

README 추가 후 최신 커밋을 amend하여 31일차 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
