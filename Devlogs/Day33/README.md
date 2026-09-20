---
# 33일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: M-02 잠입 암살 임무와 목표 안내 UI 구현  
검토 브랜치: `main`  
작성 전 기준 커밋: `2bea7c5a72c8ef086625beb450b1795e65afefdf` — `33`  
이전 기준 커밋: `2e7e6e19b17452f0adb1c3d80efb3e49dccd5e90` — `32일차 : M-01 체크포인트·재시도와 결과 화면 구현`

현재 최신 원격 커밋에는 M-02 조사·잠입 암살 임무, G 목표 안내 화살표, 임무 단말기 HUD 최소화와 좌측 배치, G/H 입력 재배치, HUD 겹침 보정, Day23 시장 부지 음수 BoxCollider 수정이 포함되어 있으며 `Devlogs/Day33/README.md`는 없는 상태다.

README 추가 후 최신 `33` 커밋을 `--amend`하여 정식 33일차 커밋 메시지로 정리한다.

---
## 1. 개발 목표

32일차까지 M-01의 진행·실패·체크포인트 재시도·결과 화면을 완성했다.

33일차에서는 두 번째 메인 임무 M-02를 실제 진행 가능한 임무로 연결하고, 잠입 중 현재 목표를 빠르게 찾을 수 있도록 G 목표 안내 화살표를 추가한다.

또한 좌측 HUD가 서로 겹치는 문제를 정리하고 임무 관련 HUD의 정보량을 줄여 실제 플레이 화면을 덜 가리도록 수정한다.

---
## 2. M-02 잠금과 해금

M-02는 M-01을 완료하기 전까지 Tab 임무 창에서 잠김 상태로 표시한다.

```text
M-02
잠김

잠김 · M-01 완료 필요
```

M-01 완료 시 M-02 상태를 자동으로 `Available`로 전환한다.

```text
M-01 Completed
↓
M-02 Available
```

Tab 임무 창에서 M-02를 수락할 수 있게 한다.

---
## 3. M-02 실제 MissionData

31일차 공통 MissionManager에 M-02 실제 데이터를 추가한다.

기본 정보는 다음과 같다.

```text
ID
M-02

제목
남겨진 주소 · 추적

유형
조사 · 잠입 암살

의뢰인
린

지역
겹길

후속 임무
M-03
```

M-01의 운송 기록을 분석해 겹길 감시 구역의 표적을 확인하고, 표적 제거 뒤 암호화 장부를 회수해 린에게 전달하는 흐름으로 구성한다.

---
## 4. M-02 목표 단계

M-02는 일곱 단계 목표로 구성한다.

```text
1. 린 분석 단말기에서 후속 좌표 확인
2. 겹길 감시 구역 진입
3. 감시 단말기에서 표적 신원 확인
4. 확인된 표적 제거
5. 암호화 장부 회수
6. 감시 구역 탈출
7. 린에게 암호화 장부 인계
```

기존 공통 목표 유형을 재사용한다.

```text
Investigate
Reach
Investigate
Eliminate
Acquire
Escape
Deliver
```

---
## 5. M-02 런타임 월드 구성

`Map33MissionWorldController`를 추가한다.

별도 Scene 수동 배치 없이 M-02에 필요한 임무 오브젝트를 런타임에서 자동 생성한다.

```text
M02_BriefingTerminal
M02_ZoneEntry
M02_VerifyTerminal
M02_Target
M02_EncryptedLedger
M02_EscapePoint
M02_DeliveryTerminal
```

기존 `LIN_HOME`과 `GYEOPGIL_PLAZA` MapPoint를 기준 위치로 사용한다.

---
## 6. M-02 브리핑 단말기

린의 거점에 M-02 브리핑 단말기를 생성한다.

상호작용 문구는 다음과 같다.

```text
[F] M-02 분석 자료 확인
```

첫 목표 완료 뒤 겹길 감시 구역 진입 목표로 진행한다.

---
## 7. 감시 구역 진입

겹길 시장 주변에 M-02 감시 구역 진입 지점을 만든다.

도착형 `Reach` 목표로 처리한다.

해당 목표 단계에서만 청록색 구역 표식을 표시해 플레이어가 진입 위치를 확인할 수 있게 한다.

---
## 8. 표적 신원 확인

감시 구역 내부에 별도 확인 단말기를 생성한다.

```text
[F] 표적 신원 확인
```

확인 전에는 암살 표적을 실제 진행 대상으로 노출하지 않는다.

신원 확인이 완료되면 다음 목표가 `Eliminate`로 전환되고 M-02 표적을 활성화한다.

---
## 9. M-02 암살 표적

M-02 전용 표적을 런타임에서 생성한다.

기존 전투 시스템과 연결하기 위해 다음 컴포넌트를 사용한다.

```text
EnemyActor
EnemyStatusController
DetectionSensor
EnemyMeleeCombat
Map33MissionTargetAI
Map33MissionTargetWatcher
```

표적은 후방 암살이 가능한 일반 EnemyActor로 구성한다.

완전히 발각되기 전 후방에서 접근하면 기존 PlayerAssassination 흐름을 사용할 수 있다.

발각된 경우 플레이어 방향으로 회전하고 근접 거리에서는 기존 EnemyMeleeCombat으로 반격한다.

---
## 10. 표적 사망 목표 연동

`Map33MissionTargetWatcher`를 추가한다.

표적의 실제 `EnemyActor.IsDead` 상태를 확인한 뒤 다음 공통 API로 연결한다.

```text
NotifyTargetEliminated("M02_TARGET")
```

따라서 단순 위치 진입이 아니라 실제 표적 사망이 `Eliminate` 목표의 완료 조건이 된다.

---
## 11. 암호화 장부

M-02 임무 물품으로 다음 ID를 사용한다.

```text
CRYPTO_LEDGER
```

HUD 표시 이름은 다음과 같다.

```text
암호화 장부
```

표적이 살아 있는 동안 장부는 비활성 상태로 둔다.

표적이 제거되면 장부를 활성화하고 기존 F 상호작용으로 회수할 수 있게 한다.

```text
표적 제거
↓
M02_EncryptedLedger 활성화
↓
[F] 암호화 장부 회수
↓
MissionInventory 보유
```

---
## 12. 탈출 목표

암호화 장부를 회수한 뒤 `Escape` 목표를 활성화한다.

청록색 탈출 지점을 표시하고 플레이어가 지정 반경 안에 들어오면 목표를 완료한다.

이 단계에서는 기존 도시 경비·수배·추격 시스템을 그대로 사용할 수 있다.

---
## 13. 최종 인계

탈출 완료 뒤 린의 거점으로 복귀한다.

최종 단말기에서 다음 상호작용을 사용한다.

```text
[F] 암호화 장부 인계
```

`CRYPTO_LEDGER`를 실제로 보유한 상태에서만 인계할 수 있다.

인계 완료 후 M-02를 Completed 상태로 전환하고 Day32 결과 화면을 사용한다.

---
## 14. M-02 체크포인트

Day32 체크포인트 시스템을 M-02에도 그대로 사용한다.

목표 진행에 따라 다음 이름을 기록한다.

```text
임무 시작
린 분석 확인
감시 구역 진입
표적 신원 확인
표적 제거
암호화 장부 확보
감시 구역 탈출
```

사망 시 마지막 저장 목표·위치·임무 물품 상태를 복원한다.

---
## 15. Tab 임무 창 Locked 상태

`Map30MissionStatus`에 `Locked` 상태를 추가한다.

기존 enum 번호가 밀리지 않도록 기존 상태 뒤에 Locked를 추가한다.

임무 창에서는 잠김 상태를 흐린 청색으로 표시한다.

---
## 16. G 목표 안내 기능

현재 추적 중인 임무 목표를 빠르게 찾기 위해 G 키 목표 안내 HUD를 추가한다.

`Map33MissionObjectiveArrowHUD`가 Day29 `Map29TerminalObjectiveProvider`의 현재 목표 위치를 그대로 사용한다.

G를 누르면 일정 시간 동안 목표 방향을 표시한다.

---
## 17. 화면 안 목표 화살표

현재 목표가 카메라 화면 안에 있을 경우 목표 위치 위에 아래쪽 화살표를 표시한다.

최종 최소 UI는 다음 정보만 보여 준다.

```text
▼
230 m
```

목표 이름이나 별도의 패널 설명은 표시하지 않는다.

---
## 18. 화면 밖 목표 화살표

현재 목표가 화면 밖에 있으면 화면 가장자리에 방향 화살표와 거리만 표시한다.

지원 방향은 다음과 같다.

```text
▲
▼
◀
▶
↖
↗
↙
↘
```

예시는 다음과 같다.

```text
↗
230 m
```

---
## 19. 목표 화살표 HUD 충돌 방지

목표 화살표가 주요 고정 HUD 위에 표시되지 않도록 안전 영역을 추가한다.

회피 대상은 다음과 같다.

```text
좌측
HP / QA / Mission HUD

우측 상단
Minimap

우측 하단
Equipment HUD
```

화살표 패널이 해당 영역과 겹치면 화면 안쪽으로 보정한다.

---
## 20. G 키 입력 충돌 수정

기존 프로젝트에서 G 키는 `UseItem`에 연결되어 있었다.

목표 안내와 소모품 사용이 동시에 실행되는 문제를 방지하기 위해 입력을 다음처럼 변경한다.

```text
G
→ 현재 임무 목표 안내

H
→ 선택 소모품 사용

V
→ 소모품 선택
```

최신 Input Actions에서 G의 기존 UseItem 바인딩이 제거되고 H가 UseItem으로 연결된 상태를 확인했다.

---
## 21. 장비 HUD 입력 안내 수정

실제 입력 변경에 맞춰 `PlayerEquipmentHUD`의 도움말도 수정한다.

```text
[R] 마비침
[H] 선택 소모품 사용

V 아이템 선택
H 사용
G 목표 안내
F 상호작용
```

입력과 HUD 문구가 서로 다르게 표시되는 문제를 방지한다.

---
## 22. 임무 단말기 HUD 최소화

기존 Day29 단말기 HUD의 정보량을 줄인다.

현재 표시 항목은 다음 정도로 제한한다.

```text
MISSION // LINK
임무명
현재 목표
남은 거리
```

기존 거리·층·방향·Target 세부 데이터 박스는 제거한다.

---
## 23. 임무 단말기 좌측 배치와 HUD 겹침 수정

HP·QA·임무 단말기가 모두 좌측에 배치되면서 낮은 해상도에서 UI가 겹치는 문제가 발생했다.

기존 단말기의 별도 GUI 스케일 계산을 제거하고 실제 화면 픽셀 기준으로 배치하도록 수정한다.

좌측 HUD는 다음 순서를 기준으로 배치한다.

```text
HP / POSTURE
↓
SYSTEM QA
↓
MISSION HUD
```

화면 높이가 부족한 경우에도 기존 HUD 영역을 피하도록 단말기 위치를 보정한다.

---
## 24. Day33 입력 패처

`ProjectKDay33MissionGuideInputPatcher`를 이용해 Input Actions와 장비 HUD의 키 안내를 자동 수정한다.

최신 원격 커밋에는 해당 일회용 패처 파일이 아직 포함되어 있다.

Input Actions와 장비 HUD에는 이미 G→H 변경이 적용된 상태다.

패처는 재실행해도 기존 H 바인딩을 확인한 뒤 중복 변경하지 않으며, Editor에서 실행이 완료되면 자기 자신을 삭제하도록 구성되어 있다.

---
## 25. 음수 BoxCollider 경고 수정

Day23 지하철 개구부 주변 시장 바닥에서 다음 경고가 발생했다.

```text
BoxCollider does not support negative scale or size.
Scene hierarchy path:
Map_World/Day23_UrbanChaseExpansion/
MarketLot_RebuiltAroundSubway/LotWest
```

문제 원인은 음수 Scale을 포함한 부모 계층 아래에서 BoxCollider가 사용된 것이었다.

외형은 유지하고 기존 패널 Collider 대신 양수 Scale 전용 Collider Proxy를 사용하는 방식으로 수정했다.

수정 대상은 다음 시장 패널이다.

```text
LotWest
LotEast
LotSouth
LotNorth
```

수정 전 Map 씬 백업도 Day33 백업 폴더에 생성했다.

```text
Assets/_Project/Backups/Day33/
Map_before_negative_box_fix_20260920_185603_626.unity
```

최신 원격 커밋에서 해당 백업 파일과 수정된 `Map.unity`가 함께 변경된 상태를 확인했다.

---
## 26. 신규 Day33 런타임 파일

```text
Assets/_Project/Scripts/World/Map33/
├─ Map33MissionBootstrap.cs
├─ Map33MissionWorldController.cs
├─ Map33MissionTargetAI.cs
├─ Map33MissionTargetWatcher.cs
├─ Map33MissionObjectiveArrowBootstrap.cs
└─ Map33MissionObjectiveArrowHUD.cs
```

---
## 27. 주요 수정 파일

```text
Assets/InputSystem_Actions.inputactions

Assets/_Project/Scenes/
└─ Map.unity

Assets/_Project/Scripts/UI/
└─ PlayerEquipmentHUD.cs

Assets/_Project/Scripts/World/Map29/
└─ Map29TerminalHUD.cs

Assets/_Project/Scripts/World/Map30/
├─ Map30MissionJournal.cs
└─ Map30MissionWindow.cs

Assets/_Project/Scripts/World/Map31/
└─ Map31MissionManager.cs
```

---
## 28. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `2bea7c5a` 확인
- 최신 커밋 메시지가 임시 제목 `33`인 상태 확인
- `Devlogs/Day33/README.md`가 원격에 없는 상태 확인
- M-02 실제 MissionData와 7단계 목표 구성 확인
- M-01 완료 전 M-02 수락 차단 확인
- M-01 완료 후 M-02 해금 처리 확인
- M-02 표적 사망과 `NotifyTargetEliminated` 연동 확인
- `CRYPTO_LEDGER` 임무 물품 처리 확인
- M-02 단계별 체크포인트 이름 처리 확인
- G 목표 화살표 입력 처리 확인
- 목표 화살표가 화살표와 거리만 표시하도록 최소화된 상태 확인
- 목표 화살표의 좌·우 HUD 안전 영역 회피 처리 확인
- Input Actions에서 G UseItem 바인딩 제거 상태 확인
- Input Actions에서 H UseItem 바인딩 적용 상태 확인
- 장비 HUD의 H 사용·G 목표 안내 문구 반영 확인
- Day29 임무 단말기 HUD 최소화 상태 확인
- 검사한 Day33 관련 C#의 중괄호 균형 이상 없음 확인
- 검사한 Day33 관련 C#에서 기존 `Input.GetKeyDown` 사용 없음 확인
- 검사한 Day33 관련 C#에서 `const string + Environment.NewLine` 충돌 없음 확인
- 수정된 Map 씬과 음수 Collider 수정 전 백업 파일이 최신 커밋에 포함된 상태 확인
- 커넥터에서는 Map.unity 전체 직렬화 본문을 직접 읽지 못해 Collider Proxy 내부 값까지 재검증하지 못함
- 이번 검토 환경에서는 Unity Editor 전체 컴파일과 M-02 처음부터 끝까지의 Play Mode 실행은 직접 수행하지 않음

---
## 29. 이후 확인 항목

- Unity 재시작 뒤 Day33 입력 패처가 중복 변경 없이 정리되는지 확인
- M-01 완료 전 M-02 잠김 표시 확인
- M-01 완료 후 M-02 수락 버튼 활성화 확인
- M-02 브리핑 단말기 상호작용 확인
- 감시 구역 진입 목표 확인
- 표적 신원 확인 뒤 M02_Target 활성화 확인
- 후방 암살과 일반 전투 처치 모두 Eliminate 목표를 완료하는지 확인
- 표적 제거 뒤 암호화 장부 활성화 확인
- 장부 회수 뒤 탈출 목표 전환 확인
- M-02 진행 중 사망 시 Day32 체크포인트 복원 확인
- G 입력 시 화면 안·밖 목표 화살표와 거리 표시 확인
- G 화살표가 좌측 HUD·미니맵·장비 HUD와 겹치지 않는지 확인
- H 입력으로 기존 소모품 사용이 정상 동작하는지 확인
- 낮은 Game View 해상도에서 HP·QA·Mission HUD 겹침 여부 확인
- Map 실행 시 LotWest 계열 음수 BoxCollider 경고가 다시 발생하지 않는지 확인

README 추가 후 최신 `33` 커밋을 amend하여 Day33 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
