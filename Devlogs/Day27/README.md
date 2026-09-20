---
# 27일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 도시 통합 QA와 Prefab 기반 건물 재구축  
검토 브랜치: `main`  
작성 전 기준 커밋: `2256cf9f1c2936d0cd21f08d45726952d5515489` — `27`  
이전 기준 커밋: `1bd8ca49512e1ec60e1d425f0b0a27dd4c514fc9` — `26일차 : 시민 목격·지연 신고와 지역 경보 시스템 구현`

현재 최신 커밋에는 27일차 도시 QA, 건물 배치 다양화, Prefab 전환, V2/V3 건물 복구 작업과 Map 씬 변경이 포함되어 있으며 `Devlogs/Day27/README.md`만 없는 상태다.

README 추가 후 기존 최신 커밋을 `--amend`하여 27일차 커밋 메시지를 정식 형식으로 정리한다.

---
## 1. 개발 목표

21~26일차까지 구축한 시민·차량·수배·지하철·특수 병력·목격 신고 시스템을 실제 도시 환경에서 함께 동작시키기 위해 통합 QA 체계를 추가한다.

기존 일반 도시 블록이 대부분 동일한 두 건물 구조를 사용해 반복적인 실루엣을 가지던 문제를 개선하고, 건물 수·높이·배치를 다양화한다.

건물 본체와 창문·간판·옥상 설비가 서로 다른 생성 계층에 존재해 건물 이동 후 장식만 공중에 남는 문제를 해결하기 위해 일반 건물을 Prefab 중심 구조로 전환한다.

최종 단계에서는 새 건물 Prefab 전체가 정상 생성된 것을 확인한 뒤에만 기존 건물을 제거하는 V3 안전 복구 구조를 적용한다.

---
## 2. 도시 통합 QA HUD

`Map27QAMonitor`를 런타임 자동 생성 방식으로 추가한다.

화면 왼쪽에 다음 개발 정보를 표시한다.

```text
DAY27 QA
FPS
Citizen
Vehicle
Guard
E-03
E-04
Reports
Wanted
Stuck
```

F10 키로 QA HUD 표시 여부를 전환한다.

초기 구현에서 기존 `UnityEngine.Input.GetKeyDown`을 사용해 Input System 전용 프로젝트에서 예외가 발생하던 문제를 수정한다.

현재는 다음 새 Input System 입력을 사용한다.

```text
Keyboard.current.f10Key.wasPressedThisFrame
```

---
## 3. 경비·시민 정체 지점 기록

약 0.8초 간격으로 경비와 도주 시민의 실제 이동량을 검사한다.

경비가 추격·수색해야 하는 상태인데 약 2.4초 동안 거의 움직이지 않으면 정체 지점으로 기록한다.

도주 상태의 시민도 같은 방식으로 반복 정체를 검사한다.

Console에는 다음 형태로 좌표를 출력한다.

```text
[Day27] Guard 정체 지점 · Position (...)
[Day27] Citizen 정체 지점 · Position (...)
```

같은 개체에서 지나치게 많은 로그가 발생하지 않도록 일정 시간 로그 간격을 둔다.

---
## 4. E-04 건물 충돌 회피

25일차 감시 드론은 공중 좌표를 직접 이동하므로 건물 내부를 통과할 가능성이 있었다.

`Map27DroneCollisionAvoidance`를 추가해 이동 방향 앞쪽을 SphereCast로 검사한다.

고정 구조물이 감지되면 다음 우회 방향을 사용한다.

```text
기존 이동 방향
↓
고정 건물 감지
↓
측면 이동
+
약간 상승
↓
장애물 우회
```

경비·시민·차량 같은 동적 개체는 고정 벽으로 취급하지 않는다.

반복적으로 같은 장소에서 막히면 QA 정체 좌표로 기록한다.

---
## 5. 일반 도시 블록 배치 다양화

기존 일반 블록은 대부분 다음 구조를 사용했다.

```text
Block
├─ Building_West
└─ Building_East
```

27일차에서는 일반 블록마다 건물 수를 2~5동으로 변화시키는 구조를 도입한다.

```text
2동
3동
4동
5동
```

3~4동 블록이 가장 자주 생성되며, 일부 블록은 넓은 고층 부지를 위해 동수를 줄인다.

주요 랜드마크 전용 블록은 자동 재배치 대상에서 제외한다.

---
## 6. 높이와 도시 실루엣 다양화

일반 건물을 다음 높이 계열로 분리한다.

```text
Low
Mid
High
Tower
```

지역별 선호 분포를 다르게 한다.

### 외곽

- Low 중심
- Mid 혼합
- 드물게 High

### 중앙 시가지

- Low·Mid 혼합
- High 비율 증가
- 일부 Tower 배치

### 동쪽 기업 구역

- Mid·High 중심
- Tower 비율 증가
- 일부 블록은 건물 수를 줄여 높은 실루엣 강조

이를 통해 전체 12×12 도시가 동일한 높이로 반복되는 문제를 완화한다.

---
## 7. 블록 경계와 건물 겹침 검사

각 건물을 배치할 때 실제 PavedLot 크기를 기준으로 안전 영역을 계산한다.

건물은 블록 경계에서 일정 여유를 둔 상태로 배치한다.

건물끼리는 기본적으로 약 4.5~5m의 빈 공간을 확보하도록 점유 영역을 계산한다.

최종 검수에서는 다음을 확인한다.

- 블록당 건물 수 2~5동
- 건물 본체가 PavedLot 밖으로 나가는지 여부
- 건물 본체끼리 실제로 겹치는지 여부
- Prefab 연결 상태
- MainCollision 존재 여부
- MeshFilter와 Mesh 존재 여부
- MeshRenderer와 Material 존재 여부

문제가 있으면 `[Day27]` 또는 `[Day27 V3]` 형식으로 Console에 경고를 출력한다.

---
## 8. 도시 요소 Prefab 전환

반복되는 도시 요소를 개별 Prefab 에셋으로 관리하는 구조를 도입한다.

생성 대상에는 다음 요소가 포함된다.

```text
Buildings
Street Lamps
Road Street Lights
Service Furniture
Vending Machines
Utility Poles
Landmarks
```

주요 랜드마크는 기존 `MapPoint`, `Arrival` 등 씬 참조를 유지하기 위해 오브젝트를 삭제 후 재생성하지 않고 기존 오브젝트를 Prefab과 연결하는 방식을 사용한다.

---
## 9. 초기 Prefab 추출 방식 문제

초기 Prefab 전환은 기존 씬 건물 전체를 복제해 Prefab으로 저장하는 방식이었다.

그러나 Day16에서 창문·패널 등 일부 장식은 개별 건물 자식이 아니라 타일 단위 `DetailVisuals_*` 합쳐진 Mesh로 별도 저장되어 있었다.

따라서 일반 건물을 이동하면 다음 문제가 발생할 수 있었다.

```text
건물 본체 이동
↓
DetailVisuals는 기존 월드 좌표 유지
↓
창문·패널·간판 조각이 공중에 남음
```

또한 기존 건물을 제거한 뒤 새 Prefab 생성을 시도하는 순서에서는 생성 과정이 실패할 경우 일반 도시 건물이 사라지는 문제가 발생할 수 있었다.

---
## 10. V2 완성형 건물 Prefab

기존 건물을 그대로 추출하는 방식을 보완하기 위해 건물 본체와 모든 주요 외관 요소를 같은 Prefab 계층 안에 넣는 V2 구조를 제작했다.

V2 건물에는 다음 요소가 포함된다.

```text
MainCollision
RoofCap
Entrance
EntranceAwning
Windows
FacadeAccent
RoofPlant
Crown
Antenna
SideAnnex
```

저층·중층·고층·타워 총 12종의 건물 Prefab을 생성한다.

전용 외벽·유리·금속·네온 재질도 함께 생성한다.

---
## 11. V3 안전 복구 구조

최종 건물 복구는 V3 구조로 변경한다.

기존 방식은 다음 위험이 있었다.

```text
기존 건물 삭제
↓
새 도시 생성
↓
중간 실패
↓
일반 건물이 없는 Map
```

V3에서는 순서를 다음과 같이 변경한다.

```text
V3 건물 Prefab 생성
↓
모든 일반 블록에 신규 건물을 Staging 상태로 생성
↓
모든 일반 블록에 최소 2동 이상 정상 본체가 있는지 검사
↓
검증 성공
↓
기존 건물 제거
↓
신규 V3 건물을 최종 건물로 전환
```

새 도시 전체 생성이 검증되지 않으면 기존 건물을 제거하지 않는다.

오류 발생 시 Undo 그룹을 이용해 부분 변경을 되돌린다.

---
## 12. V3 건물 Prefab

최종 V3 건물 Prefab은 총 12종으로 구성한다.

```text
BuildingV3_Low_A
BuildingV3_Low_B
BuildingV3_Low_C

BuildingV3_Mid_A
BuildingV3_Mid_B
BuildingV3_Mid_C
BuildingV3_Mid_D

BuildingV3_High_A
BuildingV3_High_B
BuildingV3_High_C

BuildingV3_Tower_A
BuildingV3_Tower_B
```

각 건물은 루트 스케일을 과도하게 늘리지 않고 미리 정의된 실제 크기로 생성한다.

건물 내부 구성은 다음과 같다.

```text
BuildingV3
├─ MainCollision
├─ Foundation
├─ RoofCap
├─ Entrance
├─ Awning
├─ WindowFront_*
├─ WindowBack_*
├─ WindowLeft_*
├─ WindowRight_*
├─ FacadeAccent
├─ RoofPlant
├─ Crown
├─ CrownLight
├─ Antenna
└─ SideAnnex
```

따라서 본체와 외관 요소가 항상 하나의 Prefab 인스턴스로 이동한다.

---
## 13. 구형 분리 장식 정리

V3 건물 전체 생성이 검증된 이후에만 기존 건물 위치에 종속된 구형 장식을 제거한다.

주요 제거 대상은 다음과 같다.

```text
DetailVisuals_*

Day17
Generic_*
StreetLife

Day18
Density_Tile_*
Polish_Tile_*

Day23
HighRise_*
FillCluster_*
Alley_*
```

`MapWorldRoot.detailVisuals` 직렬화 배열도 비워 제거된 장식이 런타임에서 다시 활성화되지 않도록 한다.

Terrain, 도로 본체, 지하 Terrain, 지하철역, 계단과 주요 랜드마크는 유지한다.

---
## 14. 자동 실행 안정화

초기 Day27 Editor 자동 실행은 컴파일이나 Asset Import 중이면 실행을 종료하고 다시 시도하지 못할 가능성이 있었다.

최종 V3 복구에서는 `EditorApplication.update`를 이용한다.

```text
Unity 컴파일 중
→ 대기

Asset Import 중
→ 대기

Play Mode 전환 중
→ 대기

편집 가능한 안전 상태
→ 도시 상태 검사
→ 필요할 때만 복구 실행
```

이전 Day27 자동 변경 스크립트는 호환용 빈 클래스로 교체해 여러 자동 배치기가 동시에 Map을 수정하지 않도록 한다.

---
## 15. Map 자동 백업

27일차의 각 주요 도시 변경 단계 전에 Map 씬을 자동 백업한다.

사용된 백업 경로는 다음과 같다.

```text
Assets/_Project/Backups/Day27/
Assets/_Project/Backups/Day27Prefab/
Assets/_Project/Backups/Day27PrefabRepair/
Assets/_Project/Backups/Day27CityRecovery/
```

최종 V3 복구는 `Day27CityRecovery` 백업을 사용한다.

---
## 16. 생성 자료

27일차에서 다음 종류의 생성 에셋이 추가됐다.

```text
Assets/_Project/Generated/Map27/
├─ Prefabs/
├─ PrefabsV2/
├─ MaterialsV2/
└─ V3/
   ├─ Buildings/
   └─ Materials/
```

기존 Prefab 전환 과정에서 만들어진 이전 버전 자료는 현재 V3와 구분된 폴더에 남겨 복구와 비교가 가능하도록 한다.

최종 도시 건물 기준은 `V3/Buildings`이다.

---
## 17. 주요 코드

```text
Assets/_Project/Scripts/World/Map27/
├─ Map27Bootstrap.cs
├─ Map27DroneCollisionAvoidance.cs
├─ Map27QAMonitor.cs
├─ Map27UrbanVariationMarker.cs
├─ Map27PrefabCityMarker.cs
├─ Map27PrefabRepairMarker.cs
└─ Map27CityRecoveryMarker.cs
```

```text
Assets/_Project/Editor/Day27/
├─ ProjectKDay27AutoUrbanDiversifier.cs
├─ ProjectKDay27PrefabCityRebuild.cs
├─ ProjectKDay27PrefabBuildingRepair.cs
└─ ProjectKDay27CityRecovery.cs
```

최종 자동 도시 복구는 `ProjectKDay27CityRecovery`가 담당한다.

---
## 18. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `2256cf9f` 확인
- 최신 커밋 메시지가 임시 제목 `27`인 상태 확인
- V3 건물 Prefab 12종 생성 확인
- V3 전용 재질 에셋 생성 확인
- Map 씬 변경 파일 포함 확인
- Day27 CityRecovery 백업 씬 생성 확인
- `Map27QAMonitor`의 F10 입력이 새 Input System으로 변경된 상태 확인
- `ProjectKDay27CityRecovery`의 staged 생성 후 교체 구조 확인
- 이전 Day27 자동 배치 클래스가 최신 V3 복구와 충돌하지 않도록 정리된 구조 확인
- `Devlogs/Day27/README.md`가 원격 최신 커밋에 없는 상태 확인
- 최신 소스에서 즉시 확인되는 명확한 타입 누락이나 이전 Input API 사용은 확인되지 않음
- GitHub 커넥터의 대용량 파일 제한으로 최신 `Map.unity` 내부 YAML 직렬화 내용은 직접 검증하지 못함
- 이번 검토 환경에서는 Unity Editor 컴파일과 Play Mode를 직접 실행하지 않음

---
## 19. 이후 확인 항목

- 일반 12×12 블록마다 실제 건물이 최소 2동 이상 보이는지 확인
- V3 건물 본체와 창문·문·네온이 함께 이동하는지 확인
- 공중에 남은 구형 창문·패널·간판 조각이 없는지 확인
- 건물이 도로 또는 인접 블록을 침범하지 않는지 확인
- 건물 본체끼리 겹치지 않는지 확인
- 시민·차량 경로가 새 건물 배치와 충돌하지 않는지 확인
- E-01~E-03 추격 중 새 건물 모서리 정체 지점 확인
- E-04가 새 고층 건물을 정상적으로 우회하는지 확인
- F10 QA HUD가 Input System 예외 없이 작동하는지 확인
- 최대 시민·차량·수배 병력이 동시에 활성화된 상태의 FPS 측정

README 추가 후 최신 커밋을 amend하여 27일차 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
