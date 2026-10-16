# ChatGPT 기획 ↔ Work 구현: GitHub 인계 규칙

> 시작: 2026-10-16. 이 문서는 **사용자가 선택한 작업 방식**이다. 여기서 작성한 TODO가 자동으로 Work에서 실행되는 것은 아니다.
> 개발 대상: `JohnLee305/myMusium`의 `codex/museum-tycoon`.
> 보존 대상: `codex/museum-baseline` 및 `museum-baseline-20261015` 태그.

## 1. 역할

| 역할 | 책임 | 결과물 |
|---|---|---|
| 아이디어 회의(ChatGPT) | 목표/경험 정의, 코드와 계획 비교, 설계 제안, 쪼갠 TODO와 완료 조건/위험/우선순위 작성 | `docs/*DESIGN*.md`, `TASKS.md`, `PROJECT_STATUS.md` |
| 구현(ChatGPT Work) | 실제 로컬·GitHub 최신 상태 확인, 코딩, 테스트, PC/실기기 검증 범위 보고, 작업 기록, 커밋/push | 소스 변경 + 검증 근거 + TODO/상태 갱신 |
| 사용자 | 제품 방향과 우선순위 확정, 장치 조작/개인 콘텐츠 공개 및 클라우드 활용 동의, 실제 관람 경험 평가 | 승인/피드백 |

모든 새 아이디어가 즉시 구현 대상으로 확정되는 것은 아니다. `TASKS.md`의 체크박스와 `docs`의 `proposal / approved / implemented` 상태를 구분한다.

## 2. 단일 진실 원본과 확인 순서

1. **코드/실제 빌드·검증 기록**이 기능 구현 사실의 기준이다. 기획서만으로 완료라고 판단하지 않는다.
2. **실제 개발 브랜치의** `PROJECT_STATUS.md`에 현황, `TASKS.md`에 작업 ID, `STATUS.md`에 작업 재개 상태, `WORKLOG.md`에 검증 이력을 유지한다.
3. 결정된 요구를 `docs/REACTION_SYSTEM_DESIGN.md` 등 feature spec에 저장하고, Work가 관련 ID를 골라 구현한다.
4. **매 작업 시작·재개 때 먼저 [GIT_MULTI_AGENT_WORKFLOW.md](GIT_MULTI_AGENT_WORKFLOW.md)의 필수 시작 Gate를 수행한다.** `git status`/브랜치·원격/HEAD → `git fetch --prune origin` → 안전할 때만 `git pull --ff-only origin codex/museum-tycoon` → 로컬·원격 차이 확인의 순서다. 문제 발생 시 덮어쓰지 않고 중단·보고하며, 최신 원격 상태를 확보하지 못한 상태에서 구현을 진행하지 않는다.
5. **Git 최신화 뒤** 최신 `AGENTS.md` → `PROJECT_STATUS.md` → `TASKS.md` → `STATUS.md` → `WORKLOG.md` → 관련 설계/ADR을 읽는다. 같은 TODO ID의 진행 브랜치/PR과 충돌 위험을 확인하고 작업 ID·시작 SHA·담당자를 남긴다.
6. 여러 에이전트가 개발하므로 **별도 Clone/Worktree와 작업 전용 브랜치**를 쓰고, 원칙적으로 PR을 통해 실험 브랜치에 통합한다. 매번 PR 병합 전에도 최신 원격과 회귀 영향도를 확인한다.
7. Work의 실제 코드 수정은 현재 작업 공간의 승인·권한 범위 내에서만 한다. 요구가 문서에 추가됐다는 사실은 백그라운드 작업이 이미 시작됐다는 뜻이 아니다.

## 3. 아이디어가 개발 완료가 되기까지

```text
아이디어 회의
 → 제안 문서 + 명확한 TODO ID
 → 사용자 우선순위/핵심 계약 확정
 → Work가 Git 최신 fetch/안전한 pull + 최신 TODO·현황·중복 PR 점검
 → 에이전트 전용 Worktree/브랜치에서 작업 단위 구현 + 회귀 검사
 → 실제 검증 수준과 남은 위험 기록
 → 기능 브랜치 push·PR 작성·최신 베이스 충돌 확인 후 통합
 → 통합 SHA·TODO·상태 문서 확인 → 다음 아이디어 회의에서 상태 재점검
```

`TASKS.md`의 `ARCH-`는 데이터 계약/모듈 경계, `ASSET-`은 3D 변환, `REACT-`는 의미 기반 NPC 반응, `TYC-`는 관람객 운영/타이쿤 확장이다. `REACT-`는 컬렉션 감상 경험을 우선하며, `TYC-006` 경제 시스템은 선행 필수가 아니다.

## 4. 새 Work 대화에 전달할 시작 지시문

다음 문장을 Work에 전달한다. **다른 에이전트도 동일한 동기화 절차를 따른다.**

> `JohnLee305/myMusium`의 `codex/museum-tycoon`에서 진행해 줘. **코드를 수정하기 전에** `docs/GIT_MULTI_AGENT_WORKFLOW.md`의 Git 사전 Gate를 반드시 수행해: `git status`/작업 폴더/브랜치/HEAD 확인 → `git fetch --prune origin` → 안전한 경우 `git pull --ff-only origin codex/museum-tycoon`으로 최신화. 미커밋 변경/분기/충돌이 있으면 자료를 보존하고 보고해 줘. **그다음 최신** `AGENTS.md`, `PROJECT_STATUS.md`, `TASKS.md`, `STATUS.md`, `WORKLOG.md`, `docs/WORK_HANDOFF.md`, `docs/REACTION_SYSTEM_DESIGN.md`를 읽고 작업 상태 및 다른 에이전트의 같은 TODO ID/PR을 확인해 줘. `REACT-001`부터 시작하되, 각 에이전트는 독립 Clone/Worktree·작업 브랜치로 충돌을 방지하고 테스트+상태문서 갱신 후 PR로 통합해 줘. `codex/museum-baseline`/복원 태그는 보존해 줘. 승인이 필요한 설계·개인 데이터 공개·파괴적 작업은 임의로 진행하지 마.

다른 기능 작업은 `REACT-001`만 해당 TODO ID로 바꾼다. Git 최신화와 TODO·현황 읽기는 **사용자가 별도로 반복 지시하지 않아도 필수**다.

## 5. 상태 갱신 템플릿

- 작업 ID:
- 기준 브랜치 및 시작 커밋:
- 상태: 제안 / 승인·대기 / 진행 중 / 코드 구현·미검증 / PC 검증 / Quest 실기기 검증 / 보류
- 소스 변경 및 아키텍처 변경:
- 검증 커맨드/결과:
- 사용자 조작/동의가 필요한 항목:
- 미구현/기술 부채:
- 커밋 SHA 및 push 상태:
- 다음 작업:

## 6. 다중 에이전트 충돌 방지 요약

- 통합 브랜치는 `codex/museum-tycoon`, 개별 작업은 `work/<TASK-ID>-<agent>-<unique>` 등 별도 브랜치. GitHub 기본 브랜치는 현재 기준판이므로 무심코 기본 브랜치에서 작업하지 않는다.
- **같은 작업 폴더·Unity Library·빌드 출력·테스트 서버 포트를 복수 에이전트가 동시에 공유하지 않는다.**
- 작업 전/병합 전 최신 `fetch`; 다른 에이전트의 같은 TODO/PR 조회; 파일/스키마 충돌은 직렬화 또는 작업 분리.
- `TASKS.md`/`PROJECT_STATUS.md`의 최종 진행상태는 병합된 소스와 실제 검증 수준을 기준으로 정한다. PR을 열었다고 구현 완료가 아니다.
- 인증 실패·로컬 수정·분기·충돌 시 `reset --hard`/`clean -fd`/무단 `force push`로 덮어쓰지 않고 상태를 보고한다.
- 필요시 Git 동기화 사전 점검 스크립트와 CI/브랜치 보호를 별도 작업(`OPS-002/003`)으로 구현·검증한다. 아직 자동 강제하는 시스템은 없다.

## 7. 이번 기획 결정 (컬렉션 중심)

- 최상위 목표: 소장품/이야기의 보존과 감상을 중심으로 한 **살아 있는 개인 컬렉션 박물관**.
- NPC는 단순 장식이 아니라 전시품 설명에 맞춰 **각자 다른 관심사/반응을 보이는 감상자**.
- 반응 후보는 우선 관리자에서 사전 생성·검토·저장하고, 런타임은 오프라인 선택. 실시간 LLM 의존성 없음.
- 타이쿤 경제·성장 시스템은 향후 선택적 확장이며, 위 경험의 선행 조건이 아니다.
- 기존 소스/데이터 호환·원본 보존·Quest 검증 수준 표기를 유지한다.

**중요:** 사용자 승인 없이 공개 저장소에 개인 전시 기록이나 미승인 생성 텍스트를 추가하지 않는다.

## 모델 맞춤 대사 처리 인계
새 모델 대사 요청은 [museum-model-reactions](../.agents/skills/museum-model-reactions/SKILL.md)를 사용한다. [운영 순서](REACTION_AUTHORING_WORKFLOW.md)에 따라 관찰→작성→검토→승인/저장→실제 표시를 구분한다. 업로드 자동 실행은 미구현이며 명시적인 작업 요청 시 에이전트가 처리한다. 개인 콘텐츠는 로컬 Archive에 보존한다.
## 관람객 이동 동선
ROUTE-001 구현은 [ORDERED_TOUR.md](ORDERED_TOUR.md)를 읽는다. roomOrder/navigation은 선택적 v1 계약이며 Node·Unity·스키마·CLI를 함께 유지한다. 사용자 카메라 방 이동 시 손님 관람 진행을 초기화하지 않는다. 새 동선 변경은 미로·막힌 출구·실제 순서/퇴장·기존 배치/반응 회귀와 바탕화면 실행 경로를 확인한다.
