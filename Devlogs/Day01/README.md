# 1일차 개발 일지

## 개발 목표

프로젝트 κ의 본격적인 기능 구현 전에 Unity 프로젝트의 기본 구조와 공통 데이터 기반을 구축한다.

## 개발 환경

- Unity 6000.3.21f1
- Universal Render Pipeline 17.3.0
- Input System 1.20.0
- 메인 브랜치: `main`

## 작업 내용

- `Assets/_Project` 중심의 프로젝트 전용 폴더 구조 구성
- 외부 에셋 관리를 위한 `Assets/ThirdParty` 영역 분리
- `Bootstrap`, `MainMenu`, `Hub`, `Test` 기본 씬 구성
- 전역 초기화를 담당하는 `GameBootstrap` 추가
- 프로젝트 공통 씬 이름과 경로를 관리하는 `ProjectKConstants` 추가
- 캐릭터·적·아이템·미션·무기의 공통 ScriptableObject 데이터 구조 추가
- 무기 분류와 체력 피해·자세 피해·사거리·소음·관통·탄약 데이터를 위한 기반 추가
- 적 체력·자세·순찰·추격·시야·청각·암살 가능 여부 데이터 기반 추가
- 미션 유형·선행 미션·시작 씬·크레딧 보상 데이터 기반 추가
- 프로젝트 자동 초기화를 위한 `ProjectKDay1Setup` 에디터 도구 추가
- 테스트용 Player, Ground, 장애물이 포함된 `Test` 씬 구성
- Input System 기본 액션 구성
- Unity 임시 생성 파일과 빌드 산출물을 제외하기 위한 `.gitignore` 구성

## 확인 결과

- 최신 커밋에서 1일차 프로젝트 기반 파일이 정상적으로 GitHub에 포함된 것을 확인
- 핵심 C# 스크립트의 참조 관계와 기본 문법을 정적 검토
- Input System과 URP 패키지 포함 상태 확인
- GitHub Actions 또는 별도 CI는 현재 등록되어 있지 않아 Unity 에디터 실제 컴파일 결과는 로컬에서 확인 필요
