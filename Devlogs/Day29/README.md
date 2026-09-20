---
# 29일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 사이버펑크 단말기 HUD와 목표 추적 표시  
검토 브랜치: `main`  
작성 전 기준 커밋: `215cbd0ba7dcd66369bd8cd9aabed3be07eceda2` — `29`  
이전 기준 커밋: `15e874965d0351feb4a29063ec8ac32a9db35808` — `28일차 : 대규모 도시 코드·렌더링 최적화`

현재 최신 커밋에는 29일차 단말기 HUD와 목표 추적 Provider, 시험 목표 연결, Day27 생성 Prefab Missing Script 정리 결과가 포함되어 있으며 `Devlogs/Day29/README.md`만 없는 상태다.

README 추가 후 기존 최신 커밋을 `--amend`하여 29일차 커밋 메시지를 정식 형식으로 정리한다.

---
## 1. 개발 목표

공통 미션 시스템을 구현하기 전에 플레이어가 현재 목표와 이동 방향을 실제 게임 화면에서 확인할 수 있는 단말기 HUD를 먼저 구축한다.

단말기 HUD는 이후 M-01·M-02·S-01을 포함한 모든 임무가 공통으로 사용할 목표 표시 창구로 사용한다.

현재는 실제 MissionManager 없이도 거리·층·방향 표시를 검증할 수 있도록 도시 주요 장소를 이용한 시험 추적 목표를 자동 연결한다.

UI는 사이버펑크 단말기 스타일을 기준으로 얇은 청록 프레임, 이중 테두리, 절단형 모서리, 데이터 박스, 상태 LED와 스캔 효과를 사용한다.

---
## 2. 단말기 HUD 자동 생성

별도 씬 배치 없이 런타임에서 단말기 HUD가 자동 생성되도록 `Map29TerminalBootstrap`을 추가한다.

자동 생성 대상은 다음과 같다.

```text
[Day29] Terminal Objective Provider
[Day29] Terminal HUD
```

씬에 같은 HUD가 이미 존재하면 중복 생성하지 않는다.

단말기 HUD와 Provider는 씬 전환 뒤에도 유지한다.

---
## 3. 단말기 기본 표시 정보

왼쪽 상단 단말기에 다음 정보를 표시한다.

```text
TERMINAL // YEONMU LINK

Mission ID
Mission Title
Objective Text

DIST
LAYER
VECTOR

TARGET
Status
```

현재 미션 시스템이 아직 없기 때문에 임시 추적 목표도 같은 형식으로 표시한다.

---
## 4. 단말기 UI 스타일

단말기 HUD는 기존 프로젝트 HUD와 충돌을 줄이기 위해 IMGUI 기반으로 구현한다.

시각 요소는 다음과 같다.

- 짙은 청색 반투명 배경
- 청록색 이중 외곽선
- 절단형 모서리
- 데이터 구분선
- 미션 ID 배지
- 거리·층·방향 데이터 박스
- 상태 LED
- 얇은 진행 화살표 장식
- 이동하는 스캔 라인

기존 HP·QA 패널과 겹치지 않도록 왼쪽 상단에서 오른쪽으로 이동한 위치에 배치한다.

작은 해상도에서는 화면 너비를 확인해 패널 위치를 왼쪽으로 자동 보정한다.

---
## 5. 거리 표시

플레이어와 현재 목표 월드 위치 사이의 실제 3차원 거리를 계산한다.

표시 형식은 다음과 같다.

```text
824 m
```

1km 이상이면 다음 형식으로 전환한다.

```text
1.3 km
```

목표 위치가 없으면 `---`로 표시한다.

---
## 6. 지상·지하와 높이 차이 표시

24일차 `Map24LayerNavigation`을 재사용해 플레이어와 목표의 층을 비교한다.

표시 종류는 다음과 같다.

```text
동일 층
상층 ↑
하층 ↓
지상 ↑
지하 ↓
```

지상·지하가 다르면 Day24 층 판정을 우선 사용한다.

같은 층에서는 플레이어와 목표의 높이 차이가 약 6m 이상일 때 상층·하층으로 표시한다.

---
## 7. 목표 방향 표시

플레이어 전방 방향과 목표의 수평 방향을 비교한다.

표시는 다음과 같다.

```text
전방
후방
좌 42°
우 67°
도착
```

목표와 거의 같은 위치에 있으면 `도착`으로 표시한다.

---
## 8. 단말기 상태 표시

현재 목표 상태를 다음 enum으로 관리한다.

```text
Idle
Tracking
Completed
Failed
Paused
```

HUD에서는 상태별로 다음 문구를 사용한다.

```text
STANDBY / 연결 대기
TRACKING / 추적 중
COMPLETE / 완료
FAILED / 실패
PAUSED / 대기
```

시험 추적 목표는 별도로 다음 상태를 표시한다.

```text
LOCAL TRACK / 시험 추적
```

상태별 네온 색상도 구분한다.

- 추적: 청록
- 시험 추적: 주황
- 완료: 녹색
- 실패: 적색
- 대기: 주황

---
## 9. 시험 목표 자동 연결

아직 공통 미션 시스템이 없으므로 단말기 거리·층·방향 기능을 바로 확인할 수 있도록 기존 `MapPoint`를 임시 목표로 사용한다.

기본 시험 목표는 다음 장소다.

```text
GYEOPGIL_PLAZA
겹길 시장
```

겹길 시장이 없으면 린의 거점을 제외한 첫 번째 유효 MapPoint를 대체 목표로 사용한다.

시험 목표까지 약 9m 안으로 접근하면 다음 상태로 변경한다.

```text
COMPLETE / 완료
목표 지점에 도착했습니다.
```

---
## 10. 실제 미션 시스템 연결 API

다음 단계에서 MissionManager가 바로 단말기 HUD에 연결될 수 있도록 외부 API를 준비한다.

Transform 기반 목표 연결:

```text
Map29TerminalObjectiveProvider.SetObjective(...)
```

월드 좌표 기반 목표 연결:

```text
Map29TerminalObjectiveProvider.SetWorldObjective(...)
```

목표 상태 변경:

```text
Map29TerminalObjectiveProvider.SetStatus(...)
```

목표 제거:

```text
Map29TerminalObjectiveProvider.ClearObjective()
```

실제 미션 목표가 한 번 연결되면 시험용 겹길 목표가 자동으로 다시 나타나지 않도록 `demoFallbackEnabled`를 해제한다.

---
## 11. 전체 지도와 HUD 표시 상태 연동

기존 Day19 `MapNavigationUI`의 전체 지도 상태를 확인한다.

M 전체 지도가 열려 있을 때는 단말기 HUD를 숨긴다.

```text
일반 플레이
→ 단말기 표시

전체 지도 열기
→ 단말기 숨김

전체 지도 닫기
→ 단말기 다시 표시
```

플레이어 사망 상태에서도 단말기 HUD를 숨긴다.

---
## 12. 기존 UI와 충돌 방지

기존 프로젝트는 HP HUD, QA HUD, 미니맵, 장비 HUD 등 여러 IMGUI 화면을 사용한다.

단말기 HUD는 자신의 `GUI.matrix`와 `GUI.color`를 저장하고 종료 시 복원한다.

따라서 단말기 스케일과 색상이 기존 다른 HUD에 영향을 주지 않도록 처리한다.

---
## 13. StreetLight Missing Script 문제

29일차 적용 뒤 다음 Unity 경고가 확인됐다.

```text
The referenced script on this Behaviour (Game Object 'StreetLight') is missing!
```

원인을 확인한 결과 가로등의 실제 기능 스크립트가 누락된 것이 아니었다.

Day27 Prefab 생성 과정에서 메타데이터용 `Map27PrefabElement`가 일부 생성 Prefab에 다음 상태로 저장되어 있었다.

```text
m_Script: {fileID: 0}
```

Unity에서는 이 상태를 Missing MonoBehaviour로 인식해 경고를 출력했다.

---
## 14. Missing Script 자동 정리

일회용 Day29 Editor 정리 스크립트를 사용해 다음 범위의 Missing MonoBehaviour를 제거했다.

```text
Assets/_Project/Generated/Map27/
```

정리 대상은 Missing Script만이며 다음 정상 컴포넌트는 유지한다.

- Transform
- MeshFilter
- MeshRenderer
- Collider
- Material
- 정상 MonoBehaviour

Map 씬도 검사하도록 구성했으며, 정리 완료 뒤 일회용 정리 스크립트 자체는 삭제된다.

---
## 15. 최신 Prefab 검토

최신 원격 커밋의 다음 Prefab을 재확인했다.

```text
RoadStreetLight_A.prefab
StreetLamp_A.prefab
```

두 Prefab 모두 이전에 존재하던 다음 항목이 제거된 상태다.

```text
m_Script: {fileID: 0}
```

따라서 이전 StreetLight Missing Script 원인은 최신 원격 Prefab에서 정리된 상태다.

---
## 16. 생성 파일

```text
Assets/_Project/Scripts/World/Map29/
├─ Map29TerminalBootstrap.cs
├─ Map29TerminalHUD.cs
├─ Map29TerminalObjectiveProvider.cs
└─ Map29TerminalTypes.cs
```

Day29 Missing Script 정리용 Editor 스크립트는 일회용으로 실행 후 삭제되어 최신 커밋에는 남아 있지 않는다.

---
## 17. 수정된 생성 에셋

Missing Script 정리로 기존 Day27 생성 Prefab들이 다시 저장됐다.

주요 대상은 다음 폴더다.

```text
Assets/_Project/Generated/Map27/Prefabs/
Assets/_Project/Generated/Map27/PrefabsV2/
```

StreetLight뿐 아니라 동일한 Missing MonoBehaviour를 가진 생성 Prefab도 함께 정리한다.

---
## 18. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `215cbd0b` 확인
- 최신 커밋 메시지가 임시 제목 `29`인 상태 확인
- `Devlogs/Day29/README.md`가 원격에 없는 상태 확인
- `Map29TerminalBootstrap` 추가 확인
- `Map29TerminalHUD` 추가 확인
- `Map29TerminalObjectiveProvider` 추가 확인
- `Map29TerminalTypes` 추가 확인
- 단말기 거리 표시 로직 확인
- Day24 지상·지하 층 판정 연동 확인
- 플레이어 기준 목표 방향 표시 로직 확인
- 시험용 겹길 시장 추적 목표 연결 확인
- 실제 MissionManager 연결용 API 구조 확인
- 전체 지도 열림 상태에서 단말기 숨김 처리 확인
- 최신 Day29 C#에서 기존 `Input.GetKeyDown` 사용 없음 확인
- 최신 Day29 C#에서 `System.Object`와 `UnityEngine.Object` 모호성 없음 확인
- 검사한 Day29 C# 파일의 중괄호 균형 이상 없음 확인
- `RoadStreetLight_A.prefab` Missing Script 제거 확인
- `StreetLamp_A.prefab` Missing Script 제거 확인
- 최신 원격 생성 Street Prefab에서 `m_Script: {fileID: 0}` 없음 확인
- 이번 검토 환경에서는 Unity Editor Play Mode 화면과 전체 Console을 직접 실행하지 않음

---
## 19. 이후 확인 항목

- 단말기 HUD가 기존 HP·QA HUD와 겹치지 않는지 확인
- 작은 Game View 해상도에서 HUD가 화면 밖으로 나가지 않는지 확인
- 겹길 시장 이동 중 거리 값이 정상 감소하는지 확인
- 지상에서 지하 목표를 지정했을 때 `지하 ↓` 표시 확인
- 다른 높이의 옥상 목표에서 상층·하층 표시 확인
- 목표가 좌우·후방에 있을 때 방향 값 확인
- 전체 지도 M을 열었을 때 단말기 HUD 숨김 확인
- 사망 상태에서 단말기 HUD 숨김 확인
- StreetLight Missing Script 경고가 다시 발생하지 않는지 확인
- 다음 공통 MissionManager 연결 시 시험 목표가 자동 재생성되지 않는지 확인

README 추가 후 최신 커밋을 amend하여 29일차 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
