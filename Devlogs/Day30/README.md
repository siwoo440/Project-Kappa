---
# 30일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 전체 임무 창과 청록·파란색 HUD 통합 디자인  
검토 브랜치: `main`  
작성 전 기준 커밋: `1d5c58028b2be49f870ef1ce0a36055a0528b3d9` — `30`  
이전 기준 커밋: `3d17b8bb4464b8c120bc8a125ecfc1f5698bb79b` — `29일차 : 플레이어 단말기 HUD와 목표 추적 표시 구현`

현재 최신 커밋에는 Tab 전체 임무 창, 보유 임무 Journal, M-01·M-02·S-01 개발용 임무 데이터, 전체 HUD 청록·파란색 테마 통합과 Tab 화면 중 일반 HUD 숨김 처리가 포함되어 있으며 `Devlogs/Day30/README.md`만 없는 상태다.

README 추가 후 기존 최신 커밋을 `--amend`하여 30일차 커밋 메시지를 정식 형식으로 정리한다.

---
## 1. 개발 목표

29일차에서 만든 왼쪽 상단 단말기는 현재 추적 중인 목표를 빠르게 확인하는 HUD로 유지한다.

30일차에서는 플레이어가 보유한 모든 임무를 확인할 수 있는 전체 화면 임무 창을 추가하고, Tab 키로 열고 닫을 수 있게 한다.

기존 HP·QA·단말기·미니맵·장비·수배 UI의 색상과 배치를 청록·파란색 계열로 통일해 하나의 사이버펑크 인터페이스처럼 보이도록 정리한다.

Tab 전체 임무 창이 열린 동안에는 일반 플레이 HUD를 모두 숨겨 화면이 겹치는 문제를 해결한다.

---
## 2. Tab 전체 임무 창

`Map30MissionWindow`를 추가한다.

새 Input System의 Tab 입력을 사용한다.

```text
Tab
→ 전체 임무 창 열기

Tab
→ 전체 임무 창 닫기

ESC
→ 전체 임무 창 닫기
```

기존 `UnityEngine.Input`은 사용하지 않는다.

---
## 3. 전체 임무 창 입력 잠금

전체 임무 창이 열리면 다음 플레이 입력을 잠근다.

```text
플레이어 이동
카메라 회전
사격·조준
지도 입력
```

총기 사용 중 메뉴를 열면 `PlayerFirearmController.Interrupt()`를 호출해 예약된 발사·조준·재장전 상태를 정리한다.

`PlayerMovement.SetMovementEnabled(false)`를 사용해 이동을 중단한다.

카메라와 지도 UI는 메뉴가 열리기 전 활성 상태를 저장한 뒤 일시 비활성화한다.

---
## 4. 게임 시간과 커서 처리

전체 임무 창이 열릴 때 기존 `Time.timeScale`을 저장하고 0으로 변경한다.

마우스 커서는 다음 상태로 변경한다.

```text
CursorLockMode.None
Cursor.visible = true
```

임무 창을 닫으면 다음 값을 모두 열기 전 상태로 복원한다.

```text
Time.timeScale
플레이어 이동 상태
카메라 활성 상태
지도 UI 활성 상태
Cursor.lockState
Cursor.visible
```

---
## 5. 임무 창과 전체 지도 충돌 방지

기존 M 전체 지도가 열린 상태에서는 Tab 임무 창을 열지 않는다.

반대로 Tab 임무 창이 열린 동안 MapNavigationUI를 비활성화해 M·N 입력과 미니맵 표시가 동시에 동작하지 않게 한다.

전체 화면 메뉴를 한 번에 하나만 사용할 수 있도록 구성한다.

---
## 6. Mission Journal

`Map30MissionJournal`을 추가한다.

플레이어가 현재 보유한 모든 임무 데이터를 한 곳에서 관리한다.

각 임무는 다음 정보를 가진다.

```text
Mission ID
Title
Type
Client
Region
Summary
Reward
Category
Status
Objectives
Current Objective Index
Development Seed
```

이 구조는 이후 실제 MissionManager가 같은 ID의 데이터를 갱신할 수 있도록 작성한다.

---
## 7. 임무 분류

임무는 우선 다음 두 종류로 분리한다.

```text
Main
Side
```

전체 임무 창 상단에서 다음 필터를 사용할 수 있다.

```text
전체
메인
서브
```

임무 수가 늘어나도 플레이어가 원하는 종류만 볼 수 있도록 한다.

---
## 8. 임무 진행 상태

보유 임무 상태는 다음 네 종류를 지원한다.

```text
Available
Tracking
Completed
Failed
```

UI 표시 문구는 다음과 같다.

```text
보유
추적 중
완료
실패
```

상태별 색상은 공통 UI 테마와 연결한다.

```text
보유
→ 주황

추적 중
→ 청록

완료
→ 녹색

실패
→ 적색
```

적색은 전체 UI 기본색이 아니라 실패·위험 의미에만 사용한다.

---
## 9. 첫 수직 슬라이스 임무 목록

아직 실제 MissionManager가 없으므로 전체 임무 창을 검증하기 위해 첫 완성 구간의 임무를 개발용 보유 데이터로 등록한다.

```text
M-01
M-02
S-01
```

M-01은 배달·조사 도입 흐름을 표시한다.

M-02는 조사·잠입 암살 흐름을 표시한다.

S-01은 단말기 앱 ‘틈’을 통해 받는 초기 서브 배달 의뢰 흐름을 표시한다.

실제 MissionManager가 완성되면 동일 ID 데이터로 교체하거나 `ClearDevelopmentSeeds()`를 사용해 임시 데이터를 제거할 수 있다.

---
## 10. Mission Journal 외부 API

향후 MissionManager와 저장 시스템을 연결하기 위해 다음 API를 준비한다.

```text
AddOrUpdateMission(...)
SetMissionStatus(...)
SetCurrentObjective(...)
RemoveMission(...)
ClearDevelopmentSeeds()
```

실제 미션이 추가되면 코드에서 UI를 직접 수정하지 않고 Journal 데이터만 갱신하도록 한다.

---
## 11. 임무 목록 UI

전체 임무 창의 왼쪽 영역에는 플레이어가 보유한 임무 목록을 표시한다.

각 항목은 다음 정보를 간단히 보여 준다.

```text
Mission ID
Mission Title
Status
Region
Type
```

마우스로 임무를 선택하면 오른쪽 상세 패널 내용이 변경된다.

목록이 많아지면 세로 스크롤을 사용할 수 있다.

---
## 12. 임무 상세 UI

오른쪽 상세 패널에는 선택한 임무의 다음 정보를 표시한다.

```text
Mission ID
Title
Status
Type
Client
Region
Reward
Summary
Objectives
```

목표 목록은 현재 진행 단계에 따라 다음 기호를 사용한다.

```text
✓ 완료된 목표
◆ 현재 목표
◇ 아직 진행하지 않은 목표
```

긴 임무 설명과 목표 목록을 위해 상세 영역도 세로 스크롤을 지원한다.

---
## 13. 청록·파란색 공통 UI 테마

`Map30UITheme`을 추가한다.

전체 UI가 공유하는 색상은 다음 계열을 사용한다.

```text
Background
→ 짙은 남청색

Cyan
→ 주요 외곽선·강조

Blue
→ 보조 게이지·라인

Soft
→ 얇은 내부선

Text
→ 밝은 청백색

Muted
→ 보조 정보
```

완료는 녹색, 경고는 주황, 위험·실패만 적색을 사용한다.

---
## 14. Tab 임무 창 디자인 변경

초기 전체 임무 창은 참고 이미지의 붉은 계열을 사용했다.

프로젝트 전체 UI와 일관성을 맞추기 위해 청록·파란색 계열로 변경한다.

최종 구조는 다음과 같다.

```text
TERMINAL // MISSION ARCHIVE

[전체] [메인] [서브]

┌ 임무 목록 ─────────────┐
│ M-01                   │
│ M-02                   │
│ S-01                   │
└────────────────────────┘

┌ 선택 임무 상세 ───────────────────┐
│ ID / 상태                         │
│ 제목                              │
│ 유형 / 의뢰인 / 지역 / 보상       │
│ 의뢰 정보                         │
│ 진행 목표                         │
└───────────────────────────────────┘
```

---
## 15. Tab 화면에서 일반 HUD 숨김

`Map30UITheme.HideGameplayHUD`를 공통 표시 조건으로 사용한다.

전체 임무 창이 열린 동안 다음 일반 HUD를 모두 숨긴다.

```text
PlayerHealth
Map27QAMonitor
PlayerEquipmentHUD
MapNavigationUI
MapWantedSystem
WantedMiniMapVisionOverlay
Map29TerminalHUD
```

따라서 전체 임무 창 위에 HP·QA·단말기·미니맵·장비 정보가 겹치지 않는다.

---
## 16. HP·자세 HUD 재배치

기존 좌측 상단 검은 박스형 HP HUD를 작은 청록 패널로 변경한다.

표시 정보는 다음과 같다.

```text
VITAL STATUS
HP
POSTURE
READY / POSTURE BREAK / DEAD
```

HP와 자세는 숫자뿐 아니라 청록·파랑 게이지로 함께 표시한다.

---
## 17. QA HUD 재배치

Day27 QA HUD는 HP 아래에 배치한다.

기존 세로로 긴 검은 박스를 줄이고 핵심 정보만 세 줄로 정리한다.

```text
FPS / Citizen / Vehicle
Guard / E-03 / E-04
Report / Wanted / Stuck
```

F10 토글은 그대로 유지한다.

---
## 18. 단말기 HUD 위치 조정

29일차 단말기는 기존 HP·QA 패널과 겹치지 않도록 왼쪽 상단에서 더 오른쪽으로 이동한다.

단말기 내용과 기능은 그대로 유지한다.

```text
Mission ID
Mission Title
Objective
Distance
Layer
Vector
Target
```

Tab 전체 임무 창이 열리면 단말기 HUD는 숨긴다.

---
## 19. 장비 HUD 간소화

오른쪽 하단 장비 HUD의 높이와 정보량을 줄인다.

기존 장비 패널의 반복 정보를 정리하고 다음 핵심 정보 중심으로 표시한다.

```text
장착 무기
피해
자세 피해
공격 간격
탄약
예비탄
재장전
분산
총성 반경
마비침
선택 소모품
핵심 조작
장비 메시지
```

장비 HUD도 공통 청록·파랑 패널을 사용한다.

---
## 20. 상호작용 HUD 디자인 통일

기존 중앙 `[F]` 상호작용 안내의 검은 기본 `GUI.Box`를 제거한다.

공통 청록 패널을 사용해 다른 HUD와 같은 스타일로 변경한다.

---
## 21. 미니맵·수배 HUD 연동

기존 미니맵과 수배 기능 자체는 유지한다.

Tab 전체 임무 창이 열려 있을 때만 렌더링을 중단한다.

```text
미니맵
수배 병력 오버레이
신고 진행 HUD
수배 별 HUD
```

임무 창을 닫으면 기존 상태 그대로 다시 표시한다.

---
## 22. 자동 패치 구조

기존 HUD 파일들을 수정하기 위해 Day30 일회용 Editor 패처를 사용한다.

패처는 다음 파일의 UI 표시 부분을 수정한다.

```text
PlayerHealth.cs
PlayerEquipmentHUD.cs
MapNavigationUI.cs
MapWantedSystem.cs
WantedMiniMapVisionOverlay.cs
Map27QAMonitor.cs
Map29TerminalHUD.cs
```

모든 대상 소스를 메모리에서 먼저 검증한 뒤 일괄 저장한다.

적용 성공 후 패처 자체는 삭제한다.

따라서 최신 커밋에는 일회용 패처 본체가 남지 않는다.

---
## 23. 신규 파일

```text
Assets/_Project/Scripts/World/Map30/
├─ Map30MissionArchiveBootstrap.cs
├─ Map30MissionJournal.cs
├─ Map30MissionWindow.cs
└─ Map30UITheme.cs
```

---
## 24. 수정 파일

```text
Assets/_Project/Scripts/Combat/
└─ PlayerHealth.cs

Assets/_Project/Scripts/UI/
└─ PlayerEquipmentHUD.cs

Assets/_Project/Scripts/World/Map19/
└─ MapNavigationUI.cs

Assets/_Project/Scripts/World/Map21/
└─ MapWantedSystem.cs

Assets/_Project/Scripts/World/Map22/
└─ WantedMiniMapVisionOverlay.cs

Assets/_Project/Scripts/World/Map27/
└─ Map27QAMonitor.cs

Assets/_Project/Scripts/World/Map29/
└─ Map29TerminalHUD.cs
```

---
## 25. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `1d5c5802` 확인
- 최신 커밋 메시지가 임시 제목 `30`인 상태 확인
- `Devlogs/Day30/README.md`가 원격에 없는 상태 확인
- `Map30MissionArchiveBootstrap` 추가 확인
- `Map30MissionJournal` 추가 확인
- `Map30MissionWindow` 추가 확인
- `Map30UITheme` 추가 확인
- Tab 입력이 새 Input System을 사용하는 상태 확인
- M-01·M-02·S-01 개발용 임무 목록 추가 확인
- 임무 목록·상세 패널 스크롤 처리 확인
- HP HUD Tab 숨김 처리 확인
- QA HUD Tab 숨김 처리 확인
- 장비 HUD Tab 숨김 처리 확인
- 미니맵 Tab 숨김 처리 확인
- 수배·신고 HUD Tab 숨김 처리 확인
- 수배 미니맵 오버레이 Tab 숨김 처리 확인
- Day29 단말기 HUD Tab 숨김 처리 확인
- 청록·파란색 공통 UI 테마 연결 확인
- 검사한 Day30 관련 C#에서 기존 `Input.GetKeyDown` 사용 없음 확인
- 검사한 Day30 관련 C#에서 `System.Object`와 `UnityEngine.Object` 모호성 없음 확인
- 검사한 C# 파일의 중괄호 균형 이상 없음 확인
- 일회용 Day30 UI 패처가 최신 커밋에 남아 있지 않은 상태 확인
- 이번 검토 환경에서는 Unity Editor Play Mode와 실제 해상도별 UI 화면을 직접 실행하지 않음

---
## 26. 이후 확인 항목

- 1280×720 이하 Game View에서 HUD가 서로 겹치지 않는지 확인
- 1920×1080에서 HP·QA·단말기·미니맵 위치 확인
- Tab을 열었을 때 일반 HUD가 모두 사라지는지 확인
- Tab을 닫았을 때 각 HUD가 기존 상태로 복구되는지 확인
- Tab을 연 상태에서 플레이어 이동·카메라·사격이 작동하지 않는지 확인
- Tab을 닫은 뒤 이동·카메라·커서가 정상 복구되는지 확인
- M 전체 지도와 Tab 임무 창이 동시에 열리지 않는지 확인
- 임무 필터 전체·메인·서브 동작 확인
- 왼쪽 목록에서 M-01·M-02·S-01 선택 시 오른쪽 정보 변경 확인
- 스크롤이 필요한 긴 임무 목록과 목표 목록 표시 확인
- 이후 실제 MissionManager 연결 시 Development Seed를 정상 교체하는지 확인

README 추가 후 최신 커밋을 amend하여 30일차 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
