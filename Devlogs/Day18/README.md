---
# 18일차 개발 일지

기록일: 2026-09-19  
프로젝트: 프로젝트 κ / Project-Kappa  
작업명: 도시 밀도·거리 디테일·간판 가독성 보강  
검토 브랜치: `main`  
작성 전 기준 커밋: `99dd9be9307d23f03ed193256fdd9260248a1783` — `18`  
이전 기준 커밋: `a3683e64b54ad48cc536528c1283bff615e08839` — `17일차 : 사이버펑크 네온 도시 디테일과 구역 분위기 보강`

위 해시는 개발 일지를 추가하기 전의 원격 기준이다. amend 후 커밋 해시는 변경된다.

---
## 1. 개발 목표

17일차에 보강한 사이버펑크 네온 도시를 유지하면서 건물 1층·도로 가장자리·옥상·공중 공간의 중간 및 소형 오브젝트 밀도를 높인다.

상점 전면·차량·화물·전력 설비·보행 구조·공중 연결 구조를 추가해 큰 건물과 작은 네온 사이의 빈 공간을 채운다.

추가로 네온 간판 글자가 패널과 건물 뒤에서 관통되어 보이던 문제를 수정하고, 잘못 Day19로 분리했던 거리 보정 기능을 18일차 구조로 통합한다.

---
## 2. 도시 밀도 보강

- 일반 건물 1층 상점·셔터·진열창·차양 추가
- 구역별 상점 이름과 중·대형 네온 광고판 추가
- 정차 차량·배달 바이크·화물 팔레트 추가
- 전력함·도로 파일런·포스터·젖은 바닥 패치 추가
- 옥상 기계실·실외기·비상 발판 추가
- 건물 사이 공중 정비 통로 추가
- 건물 사이 처진 전력 케이블 추가
- 구역별 밀도 차이를 적용한 배치 규칙 추가
- Terrain 타일 단위 거리 표시 클러스터 추가

---
## 3. 거리 디테일 보강

- 도시 블록 앞 보도 블럭 띠 추가
- 도로와 보행 공간을 구분하는 낮은 경계 구조 추가
- 전봇대·가로등·서비스 전력함 추가
- 일부 전봇대에 실제 점광원 추가
- 겹길·저류·유리관 중심로 전용 보행·조명 구조 추가
- 넓은 도로 가장자리의 생활 요소와 보행 기준점 보강

---
## 4. 간판 관통·가독성 수정

기존 네온 문자는 문자열 길이를 기준으로만 크기를 제한해 긴 문구가 패널 범위를 넘거나 건물 뒤에서도 보이는 문제가 있었다.

이번 수정에서는 다음 규칙을 적용했다.

- `MED//PATCH`, `DELIVERY//NODE`와 같은 구분 문구를 두 줄로 정리
- 간판 크기에 따라 문자 기본 크기를 보수적으로 축소
- `Renderer.localBounds`로 실제 생성된 문자 크기를 확인해 패널 내부에 맞춤
- 높은 `sortingOrder`를 제거해 일반 3D 깊이 관계 사용
- 전용 `ProjectK/WorldText` 셰이더 추가
- 월드 텍스트 셰이더에 `ZTest LEqual` 적용
- 문자 재질이 실제 글꼴 아틀라스를 사용하도록 연결
- 기존 Day17·Day18 네온 간판에도 같은 보정 규칙 적용
- 실제 문자 바운드가 패널 크기를 넘는지 검사하는 검증 메뉴 추가

---
## 5. Day19 잘못된 분리 정리

거리 간판·전봇대·보도 보강은 별도 19일차 작업이 아니라 18일차 도시 밀도 작업의 보완으로 관리한다.

### 현재 구조

```text
Assets/_Project/Editor/Day18/
Assets/_Project/Scripts/World/Map18/
Assets/_Project/Shaders/MapWorldText.shader
```

### 현재 씬 루트

```text
Day18_UrbanDensityLayer
Day18_StreetPolishLayer
```

구형 `Day19_UrbanPolishLayer`는 현재 보정 메뉴 실행 시 제거한다.

구형 `Assets/_Project/Editor/Day19`와 `Assets/_Project/Scripts/World/Map19` 코드도 현재 18일차 정리 기능으로 제거한다.

기존 `Assets/_Project/Backups/Day19` 씬 백업은 복구 가능성을 위해 남겨 둔다.

---
## 6. 주요 파일

### 도시 밀도

```text
Assets/_Project/Editor/Day18/MapUrbanDensityBuilder.cs
Assets/_Project/Editor/Day18/MapUrbanDensityGeometry.cs
Assets/_Project/Editor/Day18/ProjectKDay18DensitySetup.cs
Assets/_Project/Editor/Day18/ProjectKDay18Validation.cs
Assets/_Project/Scripts/World/Map18/MapUrbanDensityMarker.cs
```

### 거리·간판 보정

```text
Assets/_Project/Editor/Day18/MapUrbanStreetPolishBuilder.cs
Assets/_Project/Editor/Day18/ProjectKDay18StreetPolishFix.cs
Assets/_Project/Editor/Day18/ProjectKDay18StreetPolishValidation.cs
Assets/_Project/Scripts/World/Map18/MapUrbanStreetPolishMarker.cs
Assets/_Project/Shaders/MapWorldText.shader
```

### 공통 수정

```text
Assets/_Project/Editor/Day17/MapCyberpunkGeometry.cs
Assets/_Project/Scenes/Map.unity
```

---
## 7. 편집기 메뉴

### 도시 밀도

```text
Project K → Day 18 → Increase Urban Density
```

### 간판·거리 보정

```text
Project K → Day 18 → Repair Signs And Streetscape
Project K → Day 18 → Validate Signs And Streetscape
Project K → Day 18 → Test Sign Bounds And Depth
Project K → Day 18 → Cleanup Legacy Day19 Code
```

---
## 8. 검토 결과와 한계

- 원격 `main` 최신 커밋이 `99dd9be`이며 메시지가 `18`인 상태 확인
- 직전 17일차 커밋 `a3683e6` 이후 18일차 변경이 최신 커밋 하나에 포함된 상태 확인
- 도시 밀도·거리 보정·월드 텍스트 관련 핵심 원격 파일 9개의 Git blob SHA가 검증한 로컬 원본과 모두 일치함
- 간판 크기·깊이 수정 계약 검사 8/8 통과
- C# 정적 구분자·스타일 검사 오류 0건
- 월드 텍스트 셰이더의 `ZTest LEqual`과 깊이 검사 구성 확인
- 활성 Day19 에디터·런타임 코드는 최신 변경 목록에 포함되지 않음
- GitHub commit status와 check run은 등록되어 있지 않음
- 이번 검토 환경에서는 Unity Editor 컴파일·Play Mode·실제 화면 렌더링을 직접 실행하지 않음
- 따라서 실제 카메라 각도별 간판 가림·보도 높이·전봇대 충돌과 야간 조명 체감은 로컬 Unity에서 계속 확인해야 함

이번 README는 개발 일지만 추가하며 게임 코드·씬·셰이더를 변경하지 않는다.
