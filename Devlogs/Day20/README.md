---
# 20일차 개발 일지

기록일: 2026-09-19  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 자동 차량·시민 NPC와 도시 생활 시스템 구현  
검토 브랜치: `main`  
작성 전 기준 커밋: `2c0b5621d29b6262997be2bec1554af489bfd1d2` — `20`  
이전 기준 커밋: `010a637b837f0743dd5851d34c942a93975453ad` — `19일차 : 전체 지도·미니맵과 지도 조작 기능 구현`

위 해시는 개발 일지를 추가하기 전의 원격 기준이다. amend 후 커밋 해시는 변경된다.

---
## 1. 개발 목표

정적인 본편 오픈월드 도시에 실제로 움직이는 차량과 시민 NPC를 추가해 도시 생활감을 만든다.

자동차는 기존 12×12 도로 구조에서 계산한 전용 차선 그래프를 사용하고, 시민은 자동차와 분리된 보도·횡단보도 그래프를 사용한다.

전체 1.5km 규모 월드를 항상 시뮬레이션하지 않고 플레이어 주변에 필요한 차량과 시민만 활성화하는 풀링 구조를 사용한다.

---
## 2. 자동 차량 시스템

- Day16의 12×12 도로 구조를 기준으로 13×13 교차로 계산 추가
- 교차로별 양방향 차선 좌표 자동 계산 추가
- 차량 직진·좌회전·우회전 경로 선택 추가
- 월드 경계 밖 진행 방지와 경계 방향 복구 추가
- 앞차·시민·플레이어 전방 거리 검사 추가
- 장애물 거리에 따른 차량 감속·정지 추가
- 교차로 예약·해제 방식의 동시 진입 충돌 방지 추가
- 장시간 막힌 차량 자동 회수·재배치 추가
- 플레이어와 멀어진 차량 자동 풀 회수 추가

---
## 3. 차량 종류와 풀링

세 가지 임시 차량 프리팹을 자동 생성한다.

| 종류 | 역할 |
|---|---|
| `Civilian` | 일반 시민 승용차 |
| `Delivery` | 시장·생활권 배달 차량 |
| `Cargo` | 산업 지역 화물 차량 |

기본 런타임 설정은 다음과 같다.

| 항목 | 값 |
|---|---:|
| 차량 풀 크기 | 24 |
| 활성 차량 목표 | 18 |
| 생성 최소 거리 | 약 45m |
| 생성 최대 거리 | 약 230m |
| 회수 거리 | 약 330m |
| 전방 검사 거리 | 18m |
| 감속 거리 | 11m |
| 정지 거리 | 4.8m |
| 막힘 복구 시간 | 약 7초 |

---
## 4. 시민 NPC 시스템

- 13×13 교차로의 네 모서리를 기준으로 보행 노드 자동 생성
- 총 676개 보행 노드 구조 추가
- 인도 구간과 교차로 횡단보도 연결 추가
- 시민 `Walk`·`Idle`·`Crosswalk`·`Flee` 상태 추가
- 목적지 도착 후 2.5~7.5초 생활 대기 추가
- 직전 노드로 즉시 되돌아가는 행동 최소화 추가
- 횡단보도 진입 전 주변 차량 확인 추가
- 가까운 이동 차량이 있으면 횡단 대기 추가
- 플레이어와 멀어진 시민 자동 풀 회수 추가

---
## 5. 시민 종류

프로젝트 세계관의 인간·안드로이드·완전 기계화 시민을 동일한 생활 AI 위에서 구분한다.

| 종류 | 임시 시각 요소 |
|---|---|
| `Human` | 인간형 실루엣·생활 가방 |
| `Android` | 금속 신체·발광 눈 |
| `Mechanical` | 기계형 신체·발광 코어 |

AI 로직은 공통으로 사용하고 이후 최종 모델 제작 시 프리팹 외형만 교체할 수 있도록 구성한다.

---
## 6. 총성 반응

기존 프로젝트의 `NoiseSystem`과 `NoiseType.Gunshot`을 시민 AI에 연결한다.

- 활성 시민은 전역 `NoiseSystem.NoiseEmitted` 이벤트 구독
- 총성 범위 안의 시민만 반응
- 총성 위치를 위협 위치로 저장
- 위협에서 더 멀어지는 보행 노드 선택
- 약 5~8초 동안 도주 상태 유지
- 도주 종료 후 일반 보행 생활 루프로 복귀
- 발소리·앉기 이동 등 일반 소음에는 시민 도주를 발생시키지 않음

---
## 7. 시민 풀링

기본 런타임 설정은 다음과 같다.

| 항목 | 값 |
|---|---:|
| 시민 풀 크기 | 48 |
| 활성 시민 목표 | 32 |
| 생성 최소 거리 | 약 20m |
| 생성 최대 거리 | 약 180m |
| 회수 거리 | 약 250m |
| 횡단보도 차량 안전 검사 | 약 12m |

차량 18대와 시민 32명을 기본 활성 목표로 사용해 전체 도시가 아닌 플레이어 주변 생활 요소만 지속적으로 갱신한다.

---
## 8. 프로시저럴 프리팹

Unity Editor 설정 메뉴가 기능 검증용 차량·시민 프리팹을 자동으로 생성한다.

```text
Assets/_Project/Generated/Map20/
├─ Materials/
└─ Prefabs/
   ├─ Vehicle_Civilian.prefab
   ├─ Vehicle_Delivery.prefab
   ├─ Vehicle_Cargo.prefab
   ├─ Citizen_Human.prefab
   ├─ Citizen_Android.prefab
   └─ Citizen_Mechanical.prefab
```

차량은 차체·객실·유리·바퀴·조명·적재함으로 구성하고, 시민은 몸통·머리·팔·다리와 유형별 식별 요소를 사용한다.

최종 FBX 리소스가 준비되면 생성 프리팹 내부 외형을 교체하는 방식으로 확장한다.

---
## 9. 주요 파일

### 런타임

```text
Assets/_Project/Scripts/World/Map20/MapCityLifeTypes.cs
Assets/_Project/Scripts/World/Map20/MapTrafficMath.cs
Assets/_Project/Scripts/World/Map20/MapPedestrianGraph.cs
Assets/_Project/Scripts/World/Map20/MapTrafficVehicle.cs
Assets/_Project/Scripts/World/Map20/MapCitizenAgent.cs
Assets/_Project/Scripts/World/Map20/MapCityLifeManager.cs
```

### 에디터

```text
Assets/_Project/Editor/Day20/MapCityLifeAssets.cs
Assets/_Project/Editor/Day20/ProjectKDay20CityLifeSetup.cs
Assets/_Project/Editor/Day20/ProjectKDay20CityLifeValidation.cs
```

### 생성 결과

```text
Assets/_Project/Generated/Map20/
Assets/_Project/Backups/Day20/
Assets/_Project/Scenes/Map.unity
```

---
## 10. 편집기 메뉴

### 설치

```text
Project K → Day 20 → Setup Traffic And Citizens
```

### 검사

```text
Project K → Day 20 → Validate Traffic And Citizens
Project K → Day 20 → Test City Life Rules
```

설치 메뉴는 현재 Map 씬을 먼저 백업한 뒤 `Day20_CityLife` 루트를 만들고 차량·시민 프리팹과 월드 참조를 연결한다.

---
## 11. 검토 결과와 한계

- 원격 `main` 최신 커밋이 `2c0b562`이며 메시지가 `20`인 상태 확인
- 직전 19일차 커밋 `010a637` 이후 Day20 교통·시민 변경이 최신 커밋 하나에 포함된 상태 확인
- 핵심 C# 파일 9개의 원격 Git blob SHA가 검증한 로컬 원본과 9/9 일치함
- 도시 생활 기능 계약 검사 30/30 통과
- C# 정적 검사 오류 0건
- 런타임 파일의 UnityEditor API 혼입 없음
- 차량·시민 풀링·교차로 예약·횡단보도 판정·총성 도주 구조 확인
- GitHub commit status와 check run은 등록되어 있지 않음
- 이번 검토 환경에서는 Unity Editor 컴파일·Play Mode·실제 차량 주행과 시민 보행을 직접 실행하지 않음
- 실제 플레이에서는 차선 높이·차량 회전 궤적·횡단보도 대기·시민 겹침·장시간 교통 정체를 추가 확인해야 함

이번 README는 개발 일지만 추가하며 게임 코드·씬·프리팹을 변경하지 않는다.
