# 가족 VR 박물관

**현재 개발: DECOR-07 시험 배치 저장.** [설계·위임 명세](docs/decor-design/README.md) → [현재 구현 인계](docs/decor-design/DECOR-07-IMPLEMENTATION.md). DECOR-01~06은 PR46/48/51/52/54/56으로 통합 완료했습니다. DECOR-07 적용·전체 취소·재실행 복원은 로컬 구현·Player105/순수23 및 전체회귀 PASS, 사용자 검토 대기입니다. 실제 박물관과 개인 자료 연결은 후속 단계입니다.

**누가 작업 중인가:** [공유 작업목록](https://github.com/JohnLee305/myMusium/issues?q=is%3Aissue+is%3Aopen) · [담당 선점·중복 방지](docs/TASK_COORDINATION.md).

**에이전트 시작점:** [AGENTS](AGENTS.md) → [현재 작업·실행 경로](docs/LIVE_STATUS.md). 배포 정본은 [PROJECT_STATE.json](docs/PROJECT_STATE.json), 인계는 [작업별 기록](docs/handoffs). 아래 이전 경로·기능 설명과 충돌하면 이 정본을 따른다.

가족의 소중한 물건과 이야기를 3D로 보존하고 관람하는 확장형 개인 박물관.

기본브랜치와 유일한 최신 소스는 `codex/museum-tycoon`이다. Windows 개발·빌드·실행 폴더는 `C:/Dev/vr-museum-sync-20261016` 하나다. 예전 폴더는 기록/개인자료 보관용이다. 실제 배포/개인자료 경로는 [배포 정본](docs/PROJECT_STATE.json)을 확인한다. Work 등 별도 환경은 독립 Clone/Worktree를 사용한다.

**모든 개발 에이전트는 작업 시작·재개 전 GitHub 최신 코드를 안전하게 동기화하고 최신 `TASKS.md`/`PROJECT_STATUS.md`/`STATUS.md`/`WORKLOG.md`를 확인한다.** [Git 다중 에이전트 작업 규칙](docs/GIT_MULTI_AGENT_WORKFLOW.md) 및 [Work 인계 가이드](docs/WORK_HANDOFF.md) 참조.


## 전체 계획과 진행상황

- [최신 전체 진행상황·개발 계획 (GitHub 최신 통합판)](https://github.com/JohnLee305/myMusium/blob/codex/museum-tycoon/PROJECT_STATUS.md)
- [이 브랜치의 진행상황 문서](PROJECT_STATUS.md)
- [단일 버전·실행 경로 운영](docs/VERSIONING.md)
- [매 작업 시작 전 Git 최신화 / 다중 에이전트 충돌 방지](docs/GIT_MULTI_AGENT_WORKFLOW.md)
- [ChatGPT 기획 → Work 구현 인계](docs/WORK_HANDOFF.md)

앞으로 기능을 추가·수정할 때마다 실제 작업 브랜치의 진행상황 MD와 검증 결과를 갱신하고, 관련 코드와 함께 GitHub에 올립니다.

## 개발 원칙

- Museum Archive가 원본이며 Unity는 이를 읽는 Viewer다.
- 전시품 데이터, 전시 공간, 실행 앱을 분리한다.
- 초기에는 JSON과 로컬 파일을 사용한다.
- 원본 자산과 PC/모바일 최적화 자산은 구분한다.
- 개인 자료의 공개 여부와 VRChat 내보내기는 이후 별도로 구현한다.
- 처음에는 PC 연결 Quest 3를 목표로 한다. 단독 실행은 후속 단계다.

## 순서

1. 개발환경과 Quest Link 확인
2. Archive 규칙, 샘플, 검증 도구
3. 전시품 하나와 설명을 표시하는 Viewer
4. 데이터를 추가해 여러 전시품 표시
5. Curator Admin
6. 컬렉션과 방 확장
7. 미디어 강화와 공개 자료 VRChat Export

## 실행 가능한 미리보기
Windows: Builds/Windows/VRMuseum.exe
Unity 프로젝트: apps/quest-viewer
실행 방법과 재빌드: docs/QUICKSTART.md
현재는 Archive의 항아리·책·고리 샘플 GLB와 JSON 설명을 읽어 자동 배치한다. 한 방에 최대30개 표시, 방 자동추가/이동시 로딩/이전방 해제 및 항목별 오류 격리를 지원한다. 데이터 규칙: docs/ARCHIVE.md.


## HTML 전시품 추가
바탕화면 VR 박물관 관리 바로가기로 최신4186 서버를 사용한다. 서버 켜기 → 관리페이지 열기 순으로 사용한다. 샘플 또는 GLB 파일로 전시품을 등록하고 관람 앱을 재실행한다. 사용법: docs/CURATOR.md.

## 타이쿤 실험 관람객
이 브랜치의 실제 개발·실행 폴더는 [배포 정본](docs/PROJECT_STATE.json)을 확인합니다. 바탕화면 VR 박물관 (타이쿤 실험)으로 실행하고 오른쪽 패널에서 관람객을 0~6명 조절합니다. 기존 관람 버전은 별도 폴더·바로가기로 유지합니다.

새 사람 에셋 추가·교체는 [관람객 외형 확장](docs/VISITOR-EXTENSION.md), 현재 현황은 [PROJECT_STATUS.md](PROJECT_STATUS.md)를 참고합니다.


## 전시품에 반응하는 관람객
전시품 원문에서 반응 후보를 만들고 직접 승인한 문구를 NPC의 실제 감상 순간에 표시하는 PC 첫 버전을 구현하고 검증했습니다. [사용법과 제약](docs/REACTIONS.md), [확정 계약](docs/decisions/0006-approved-reactions.md). 외부 AI 없이 오프라인으로 동작하며 실제 Quest 검증은 별도입니다.


새 전시품 등록 시 설명 기반 감상 초안40개를 준비합니다. 관리자에서12개씩 검토·승인할 수 있습니다. 실제 외형을 관찰한 맞춤 반응은 별도 작성하며 [모델당1,000개 확장 방향](docs/REACTION_CONTENT_SCALE.md)은 후속 계획입니다.
