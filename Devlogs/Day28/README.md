---
# 28일차 개발 일지

기록일: 2026-09-20  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 대규모 도시 코드·렌더링 최적화  
검토 브랜치: `main`  
작성 전 기준 커밋: `cc40abdbff14b1c8f8356d4447cbdda8c6ba39d9` — `a`  
이전 기준 커밋: `940f7787bc293a40a8b7f116748b54e0b3fe4848` — `27일차 : 도시 통합 QA와 Prefab 기반 건물 재구축`

현재 최신 커밋에는 28일차 도시 확장 대비 코드 최적화가 이미 포함되어 있으며 `Devlogs/Day28/README.md`만 없는 상태다.

README 추가 후 기존 최신 커밋을 `--amend`하여 28일차 커밋 메시지를 정식 형식으로 정리한다.

---
## 1. 개발 목표

27일차에서 Prefab 기반 도시 건물 수가 크게 증가한 이후에도 시민·차량·경비·드론·감시 센서가 함께 동작할 수 있도록 CPU와 렌더링 부담을 줄인다.

여러 시스템이 각자 `FindObjectsByType`를 반복하던 구조를 공용 Runtime Registry 중심으로 변경하고, 차량·시민 주변 탐색에는 공간 분할을 적용한다.

매 프레임 수행되던 차량 전방 검사와 감시 센서 Raycast를 거리별 주기 검사로 변경해 도시 규모가 커질수록 증가하는 연산량을 줄인다.

V3 건물은 플레이어 거리별로 디테일 Renderer를 단계적으로 줄여 원거리 렌더링 비용을 감소시킨다.

---
## 2. Runtime Registry 추가

`Map28RuntimeRegistry`를 추가한다.

시민·차량·수배 경비·감시 드론·DetectionSensor·시민 신고 상태를 하나의 공용 목록으로 관리한다.

관리 대상은 다음과 같다.

```text
Citizens
Vehicles
Wanted Guards
Surveillance Drones
Detection Sensors
Citizen Witnesses
```

기존에는 여러 시스템이 각각 전역 검색을 실행했다.

28일차 이후에는 Registry가 낮은 빈도로 전체 검색을 수행하고 다른 시스템이 같은 결과를 재사용한다.

기본 전체 재검색 간격은 약 3초다.

활성 개체 목록은 더 짧은 간격으로 캐시한다.

---
## 3. 차량·시민 공간 분할

도시를 약 28m 크기의 공간 셀로 나눈다.

활성 차량과 시민의 현재 위치를 공간 셀에 등록한다.

차량이 앞쪽 장애물을 검사할 때 전체 차량·시민 풀을 순회하지 않고 현재 차량 주변 셀만 확인한다.

```text
기존

차량 1대
→ 모든 차량 검사
→ 모든 시민 검사
→ 플레이어 검사

변경

차량 1대
→ 주변 Spatial Cell 검사
→ 근처 차량 검사
→ 근처 시민 검사
→ 플레이어 검사
```

도시 개체 수가 증가할수록 전체 순회보다 주변 셀 검색의 효율이 높아지도록 구성한다.

---
## 4. 차량 전방 검사 주기 최적화

차량 이동 자체는 기존처럼 매 프레임 유지한다.

비용이 큰 전방 차량·시민 검사는 거리별로 결과를 캐시한다.

초기 기준은 다음과 같다.

| 플레이어와 차량 거리 | 전방 검사 간격 |
|---|---:|
| 80m 이내 | 약 0.05초 |
| 80~160m | 약 0.12초 |
| 160m 이상 | 약 0.25초 |

차량 위치·회전·속도 보간은 계속 매 프레임 처리한다.

따라서 이동 자체를 낮은 프레임으로 만드는 방식이 아니라 주변 장애물 검색 비용만 줄인다.

---
## 5. 시민 횡단 검사 최적화

시민의 일반 보행과 자유 도주는 기존 Update 흐름을 유지한다.

횡단보도 진입 전 차량 안전 검사만 주기적으로 수행한다.

초기 기준은 다음과 같다.

```text
플레이어 80m 이내
→ 약 0.10초 간격

80m 밖
→ 약 0.22초 간격
```

횡단 차량 검색도 전체 차량 풀 순회 대신 `Map28RuntimeRegistry`의 공간 셀을 사용한다.

새 횡단보도에 진입할 때는 이전 검사 결과를 그대로 사용하지 않고 즉시 다시 검사하도록 초기화한다.

---
## 6. DetectionSensor 시야 검사 최적화

기존 `DetectionSensor`는 활성 상태에서 매 프레임 실제 시야 Raycast를 실행했다.

28일차에서는 시야 결과를 짧게 캐시하고 실제 Raycast 빈도를 거리별로 조정한다.

| 대상 거리 | 실제 시야 검사 간격 |
|---|---:|
| 45m 이내 | 약 0.05초 |
| 45m 이상 | 약 0.20초 |

탐지 진행도, 의심 상태, 수색 타이머 등 기존 상태 로직은 계속 갱신한다.

센서 설정이 변경되거나 다시 활성화되면 다음 시야 검사를 즉시 허용한다.

---
## 7. 수배 시스템 Registry 연동

`MapWantedSystem`에서 주기적으로 수행하던 전체 `DetectionSensor` 검색을 Registry 목록 재사용 방식으로 변경한다.

수배 시스템의 기존 직접 발각·5초 위치 공유·Heat 감소 구조는 유지한다.

변경 대상은 센서 목록 조회 방식이다.

```text
기존
FindObjectsByType<DetectionSensor>

변경
Map28RuntimeRegistry.ActiveSensors
```

---
## 8. Day23·Day24 추격 Bootstrap 최적화

기존 Day23과 Day24 Bootstrap은 일정 간격으로 수배 경비 전체를 다시 검색했다.

다음 기능의 경비 목록을 Registry로 변경한다.

```text
Day23
- 추격 정체 복구 컴포넌트 자동 연결
- 수배 해제 후 남은 경비 철수

Day24
- 지상·지하 계단 추격 컴포넌트 자동 연결
```

활성·비활성 전체 풀 경비는 `AllGuards`, 실제 활성 경비는 `ActiveGuards`를 사용한다.

---
## 9. Day25 특수 병력 최적화

E-03 중장갑 경비 배치와 충돌 무시 처리에서 사용하던 활성 경비 전역 검색을 Registry로 변경한다.

다음 작업이 대상이다.

- E-03 주변 기존 경비 확인
- 중복 배치 방지
- 활성 경비 간 CharacterController 충돌 무시 연결

E-03·E-04 풀 자체는 기존 Day25 전용 풀을 유지한다.

---
## 10. Day26 목격·신고 최적화

범죄 발생 시 시민과 보안 센서를 찾기 위해 실행하던 전역 검색을 Registry로 교체한다.

Registry에서 다음 목록을 재사용한다.

```text
ActiveSensors
ActiveCitizens
ActiveCitizenWitnesses
```

적용 영역은 다음과 같다.

- 보안 센서 신고 후보 검색
- 시민 목격 후보 검색
- 지역 경보 시민 검색
- 비활성 신고 상태 정리

범죄 판정 방식과 신고 지연·중복 방지 규칙은 변경하지 않는다.

---
## 11. Day27 QA 최적화

Day27 QA HUD와 정체 검사의 시민·차량·경비·드론 집계를 Registry 기반으로 변경한다.

기존 QA 기능은 유지한다.

```text
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

F10 토글은 27일차에서 수정한 새 Input System 방식을 그대로 유지한다.

---
## 12. 건물 거리별 Renderer 최적화

`Map28BuildingLODManager`를 추가한다.

Day27 V3 일반 건물의 Renderer를 역할별로 세 그룹으로 캐시한다.

### Core

원거리에서도 건물 실루엣을 유지한다.

```text
MainCollision
Foundation
RoofCap
Crown
SideAnnex
```

### Medium

중거리까지 유지한다.

```text
Entrance
Awning
FacadeAccent
RoofPlant
CrownLight
```

### Detail

근거리에서만 표시한다.

```text
Windows
Antenna
기타 작은 장식
```

---
## 13. 건물 거리 단계

초기 거리 기준은 다음과 같다.

| 거리 | 표시 내용 |
|---|---|
| 0~180m | 전체 건물 디테일 |
| 180~360m | 본체 + 중간 외관 |
| 360~720m | 본체 실루엣 중심 |
| 720m 이상 | 건물 Renderer 비활성 |

모든 건물을 같은 프레임에 검사하지 않는다.

기본적으로 한 프레임에 최대 32동씩 나누어 거리 단계를 갱신한다.

작은 창문·네온·안테나 Renderer는 그림자 생성과 그림자 수신을 비활성화해 추가 렌더링 비용을 줄인다.

---
## 14. 고정 보안 센서 거리 휴면

`Map28SensorBudgetManager`를 추가한다.

멀리 있는 고정 감시 카메라와 일반 도시 보안 센서는 DetectionSensor 자체를 휴면시킨다.

초기 기준은 다음과 같다.

```text
260m 밖
→ DetectionSensor 비활성

220m 안
→ DetectionSensor 재활성
```

다음 센서는 거리 휴면 대상에서 제외한다.

```text
수배 경비 E-01~E-03
E-04 감시 드론
일반 EnemyActor
```

전투·추격 AI까지 원거리라는 이유만으로 끄지 않도록 구분한다.

---
## 15. 원거리 건물 그림자 최적화

건물 본체와 큰 실루엣은 기존 그림자를 유지한다.

다음 작은 요소는 그림자 생성·수신을 제거한다.

- 창문
- 네온
- 출입구 장식
- 작은 옥상 설비
- 안테나
- 기타 Detail Renderer

건물 수가 늘어났을 때 Shadow Caster 수가 과도하게 증가하는 것을 완화한다.

---
## 16. 컴파일 오류 수정

Day28 `Map28BuildingLODManager`에서 `using System`과 `using UnityEngine`이 함께 사용되면서 다음 참조가 모호해지는 컴파일 오류가 발생했다.

```text
CS0104
Object is an ambiguous reference between UnityEngine.Object and object
```

해당 검색을 다음처럼 명확하게 수정한다.

```text
UnityEngine.Object.FindObjectsByType<MapWorldRoot>
```

최신 원격 커밋에는 이 수정이 반영되어 있다.

---
## 17. 신규 파일

```text
Assets/_Project/Scripts/World/Map28/
├─ Map28RuntimeRegistry.cs
├─ Map28BuildingLODManager.cs
└─ Map28SensorBudgetManager.cs
```

코드 최적화를 적용하기 위해 사용한 `ProjectKDay28OptimizationPatcher.cs`는 일회용 Editor 패처로 실행 완료 후 자기 자신을 삭제한다.

따라서 최종 최신 커밋에는 Day28 Editor 폴더 메타 정보만 남고 일회용 패처 본체는 포함되지 않는다.

---
## 18. 수정 파일

```text
Assets/_Project/Scripts/AI/DetectionSensor.cs

Assets/_Project/Scripts/World/Map20/
├─ MapCitizenAgent.cs
├─ MapCityLifeManager.cs
└─ MapTrafficVehicle.cs

Assets/_Project/Scripts/World/Map21/
└─ MapWantedSystem.cs

Assets/_Project/Scripts/World/Map23/
└─ Map23ChaseBootstrap.cs

Assets/_Project/Scripts/World/Map24/
└─ Map24Bootstrap.cs

Assets/_Project/Scripts/World/Map25/
└─ Map25ResponseDirector.cs

Assets/_Project/Scripts/World/Map26/
└─ Map26CrimeReportSystem.cs

Assets/_Project/Scripts/World/Map27/
└─ Map27QAMonitor.cs
```

---
## 19. 최신 커밋 검토 결과

- 원격 `main` 최신 커밋 `cc40abdb` 확인
- 최신 커밋 메시지가 임시 제목 `a`인 상태 확인
- `Devlogs/Day28/README.md`가 원격에 없는 상태 확인
- `Map28RuntimeRegistry` 신규 추가 확인
- 차량·시민 공간 분할 구조 추가 확인
- `Map28BuildingLODManager` 신규 추가 확인
- `Map28SensorBudgetManager` 신규 추가 확인
- 기존 Day20~Day27 시스템의 Registry 연결 수정 확인
- `DetectionSensor` 거리별 시야 검사 캐시 추가 확인
- 차량 전방 검사 거리별 주기 캐시 추가 확인
- 시민 횡단 검사 거리별 주기 캐시 추가 확인
- Day28 핵심 변경 파일에서 기존 `Input.GetKeyDown` 사용 없음 확인
- `System.Object`와 `UnityEngine.Object`의 모호한 `Object.FindObjectsByType` 참조 없음 확인
- `Map28BuildingLODManager`가 `UnityEngine.Object.FindObjectsByType`를 사용하는 상태 확인
- 검사한 Day28 관련 C# 파일의 중괄호 균형 이상 없음 확인
- 이번 검토 환경에서는 Unity Editor 전체 컴파일과 Profiler 성능 측정을 직접 실행하지 않음

---
## 20. 이후 확인 항목

- Unity Console에 추가 컴파일 오류가 없는지 확인
- 차량이 가까운 상황에서 앞차·시민 충돌 방지가 정상인지 확인
- 멀리 있는 차량의 전방 검사 간격 감소가 눈에 띄는 충돌을 만들지 않는지 확인
- 시민 횡단보도 안전 검사가 기존처럼 동작하는지 확인
- 경비·카메라 탐지가 지나치게 늦어지지 않는지 확인
- 고정 카메라가 220m 안으로 접근했을 때 다시 활성화되는지 확인
- V3 건물의 거리별 창문·네온 표시 전환 확인
- 720m 이상 건물이 정상적으로 컬링되고 접근하면 복구되는지 확인
- 5성 수배 + 시민·차량 최대 상태의 FPS 비교
- Unity Profiler에서 Scripts·Physics·Rendering·GC Alloc 변화 측정

README 추가 후 최신 커밋을 amend하여 28일차 작업명과 변경 내용을 정식 커밋 메시지로 정리한다.
