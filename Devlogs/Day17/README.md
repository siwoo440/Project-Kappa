---
# 17일차 개발 일지

기록일: 2026-09-19  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 사이버펑크 네온 도시 디테일·구역별 분위기 보강  
검토 브랜치: `main`  
작성 전 기준 커밋: `fe896773797c3666949a159206831e417fa33f2d` — `a`  
이전 기준 커밋: `5c792b214160ff02dd115b099131633262c6e068` — `16일차 : 본편 Map 씬·3x3 Terrain과 오픈월드 기반 구현`

위 해시는 개발 일지를 추가하기 전의 원격 기준이다. amend 후 커밋 해시는 변경된다.

---
## 1. 개발 목표

16일차에 만든 `Map` 씬의 3×3 Terrain·도로·건물·주요 구역을 유지하면서, 도시 전체를 저녁·야간 사이버펑크 분위기로 보강한다.

단순히 네온을 추가하는 데 그치지 않고 일반 건물 외벽·옥상 설비·시장·산업 구역·기업 구역·첨탑의 실루엣과 생활 소품을 추가해 구역별 분위기를 구분한다.

이번 일차는 도시의 비주얼 디테일과 네온 표현을 강화하는 단계다. NPC·미션·상시 경비·월드 저장은 완료 범위에 포함하지 않는다.

---
## 2. 작업 내용

- `Map` 씬 전체를 짙은 청색 저녁·야간 분위기로 수정
- 환경광·안개·카메라 원거리 가시거리 조정
- 방향광의 색상·강도·방향을 야간 도시 기준으로 수정
- 일반 건물 옥상 실외기·안테나·통신 비콘 추가
- 일반 건물 외벽 수직 네온·입체 광고판 추가
- 일부 건물 외벽에 실제 밟을 수 있는 정비 발판 추가
- 도로와 골목에 전력함·네온 볼라드·자동판매기·수거함 추가
- 건물 사이 공중 전력 케이블 추가
- 린의 옥상 작업실에 네온·정비 설비·통신 설비 추가
- 겹길 시장에 네온 간판·시장 차양·판매기·생활 소품 추가
- 저류 산업지구에 대형 배관·정비 발판·전력함·경고 조명 추가
- 유리관 기업지구에 수직 네온·보안 파일런·기업 광고판 추가
- 첨탑에 장거리 수직 네온·접근 비콘·식별 간판 추가
- 네온 공유 재질을 복제하지 않는 `MaterialPropertyBlock` 맥동 처리 추가
- 실제 점광원 수와 범위를 제한한 성능 기준 추가
- 거리별 장식 묶음 활성·비활성 처리 추가
- 설치 전 `Map` 씬 백업과 반복 설치 방지 처리 추가
- Day17 도시 디테일 검사 메뉴 추가

---
## 3. 주요 구역별 변화

| 구역 | 17일차 보강 내용 |
|---|---|
| 린의 옥상 작업실 | 청록 중심 네온·실외기·안테나·전력 설비 |
| 겹길 | 자홍·청록·주황·보라 간판·시장 차양·판매기·골목 케이블 |
| 저류 | 녹슨 산업 배관·정비 발판·황색 경고 조명·전력함 |
| 유리관 | 정돈된 기업 외벽·청록/보라 수직 네온·보안 파일런 |
| 첨탑 | 도시 기준점 역할의 장거리 수직 네온·진입 비콘 |

---
## 4. 디테일 생성 구조

모든 신규 도시 디테일은 기존 16일차 Map 구조를 직접 파괴하지 않고 아래 루트에 모아 관리한다.

```text
Day17_CyberpunkCityDetail
```

이 루트에는 일반 건물 장식·거리 생활 소품·각 주요 구역 전용 디테일과 거리별 표시 묶음이 포함된다.

기존 Terrain·도로·건물·플레이어·카메라·총기·Test 훈련센터는 삭제하지 않는다.

---
## 5. 네온과 조명 처리

대부분의 네온은 발광 재질을 사용하고, 실제 `Point Light`는 중요 구역에만 제한적으로 사용한다.

- 네온 표면은 `MapNeonPulse`로 밝기를 천천히 변화
- 공유 재질 자체를 매 프레임 복제하지 않고 `MaterialPropertyBlock` 사용
- 실제 네온 광원은 점광원만 사용
- 추가 광원의 범위는 18m 이하
- 추가 광원 그림자는 비활성화
- 먼 장식은 `MapDetailCluster` 단위로 일정 거리 밖에서 비활성화

---
## 6. 주요 파일

### 런타임

```text
Assets/_Project/Scripts/World/Map17/MapCyberpunkDetailMarker.cs
Assets/_Project/Scripts/World/Map17/MapDetailCluster.cs
Assets/_Project/Scripts/World/Map17/MapNeonPulse.cs
```

### 에디터

```text
Assets/_Project/Editor/Day17/MapCyberpunkGeometry.cs
Assets/_Project/Editor/Day17/MapCyberpunkDetailBuilder.cs
Assets/_Project/Editor/Day17/ProjectKDay17CyberpunkSetup.cs
Assets/_Project/Editor/Day17/ProjectKDay17Validation.cs
```

### 생성 결과

```text
Assets/_Project/Materials/Map17/
Assets/_Project/Backups/Day17/
Assets/_Project/Scenes/Map.unity
```

---
## 7. 편집기 메뉴

```text
Project K → Day 17 → Enhance Map Cyberpunk City
Project K → Day 17 → Validate Cyberpunk City
Project K → Day 17 → Test Cyberpunk Detail Rules
```

설치 메뉴는 `Map.unity`를 먼저 백업하고 기존 `Day17_CyberpunkCityDetail`이 있으면 재설치하지 않는다.

---
## 8. 검토 결과와 한계

- 원격 `main` 최신 커밋 `fe89677`이 17일차 도시 디테일 변경을 포함한 상태 확인
- 직전 16일차 커밋 `5c792b2` 이후 Day17 변경이 최신 커밋 하나에 포함된 상태 확인
- 원격 Day17 신규 C# 7개와 제작 시 사용한 로컬 원본의 Git blob SHA가 모두 일치함
- C# 7개, 998줄에 대한 정적 구분자·주석·메타 구조 검사 오류 0건
- Day17 필수 파일·메뉴·구성 계약 검사 7/7 통과
- 원격 커밋에 GitHub check run과 status가 등록되어 있지 않음
- 이번 검토 환경에서는 Unity Editor 컴파일·Play Mode·실제 야간 렌더링을 직접 실행하지 않음
- 실제 화면 확인 결과 도시의 큰 구조와 네온은 추가되었지만 도로 가장자리·건물 1층·중간 크기 구조물·전경 생활 요소의 밀도는 다음 일차에서 추가 보강 대상임

이번 README는 개발 일지만 추가하며 게임 코드·씬·재질을 변경하지 않는다.
