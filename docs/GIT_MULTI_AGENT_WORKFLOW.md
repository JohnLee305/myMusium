# GitHub 기반 다중 에이전트 개발 운영 규칙

> 적용일: 2026-10-16 · 범위: **ChatGPT Work, Codex 및 기타 구현 에이전트 전부** · 우선순위: 이 저장소의 구현 세션 시작/재개 절차.
>
> **필수 원칙:** 모든 에이전트는 코드·문서 수정 전 GitHub에서 **최신 원격 상태를 확인·안전하게 동기화**하고, 최신 `TASKS.md`와 `PROJECT_STATUS.md`를 읽는다. **동기화 실패, 충돌, 미커밋 변경이 있으면 구버전 코드에서 새 작업을 강행하지 않는다.** 원본/로컬 작업을 강제 덮어쓰지 않는다.
>
> 협업 기준 브랜치: `codex/museum-tycoon` (GitHub 기본브랜치도 동일한 단일 최신판).
> 보호 대상: `codex/museum-tycoon`, 기존 실물 소장품 Archive, 로컬 빌드/사용자 데이터. 기준판은 OPS-009에서 사용자 승인으로 은퇴하며 과거 기록은 로컬백업에 보존한다.
>
> 이 문서는 **에이전트가 따라야 할 절차**이다. Git hook, CI, 브랜치 보호 설정이 자동으로 설치되었다는 의미는 아니다. 이를 기계적으로 강제할지 여부는 `OPS-002/003` 후속 작업으로 관리한다.

## 사전 점검 도구 (OPS-002)

[Git 사전 점검 CLI](GIT_PREFLIGHT.md)를 제공한다. Windows 실행기는 Node가 PATH에 없으면 Codex 번들을 탐색한다. 기본은 원격참조만 fetch하고, `--update`는 깨끗한 통합브랜치에서만 FF한다. dirty/stash/분기/원격실패/중복PR·브랜치는 중단하며 아래 수동 문서·담당 확인을 대체하지 않는다. 자동 Git hook/CI/브랜치 보호(OPS-003)는 미구현이다.

담당선점은 [OPS-004 공유작업](TASK_COORDINATION.md)의 list→claim→Git Gate를 따른다. codex/claims/*는 담당메타데이터로서 소스작업브랜치/병합/빌드 대상과 구분한다.

## 1. 매 작업 시작·재개 필수 Gate (코드 수정 전)

**새 작업, 기존 작업 재개, 다른 에이전트가 이어받는 작업 모두 동일하게 적용한다.**

1. **위치/저장소 확인.** 현재 작업 디렉터리가 `JohnLee305/myMusium`의 정상 Clone/Worktree인지, `origin`이 맞는지 확인한다. 기본 브랜치 이름만 믿지 않는다. 개인 Windows의 `C:\Dev\vr-museum-tycoon`이 없는 Work 클라우드 환경이면 자신에게 허용된 별도 Clone/Worktree를 사용하고 없는 로컬 경로를 추측하지 않는다.
2. **미커밋 파일 보호.** `git status --short`, 현재 브랜치, 로컬 HEAD, `git remote -v`를 기록한다. 다른 세션의 변경, 미추적 개인 데이터, 빌드 실행 중 리소스를 확인한다.
3. **원격 최신 커밋 가져오기.** `git fetch --prune origin`을 실행한다. 원격 `origin/codex/museum-tycoon`과 로컬 HEAD의 선후·분기를 확인한다. GitHub 접근 불가/인증 실패/원격 부재 시 진행 불가로 보고하고 수정하지 않는다.
4. **안전하게 최신화.** 깨끗한 공유 실험 브랜치에서만 `git pull --ff-only origin codex/museum-tycoon`으로 최신화한다. 기존 작업 브랜치에서는 현재 변경사항을 보존한 채 원격 기준 커밋과의 차이를 확인하고, **안전하게 통합 가능한 경우에만** 반영한다. `--ff-only` 실패·히스토리 분기·수정 충돌 시 멈추고 이유와 선택지를 보고한다. `reset --hard`, `clean -fd`, 자동 `stash`/discard, 무분별한 `force push` 금지.
5. **최신 기획·작업 읽기.** 최신 AGENTS → LIVE_STATUS → PROJECT_STATE → 작업별 handoff → TASKS 해당ID·관련설계 순서. 과거 STATUS/WORKLOG 전체를 매번 읽지 않으며 해당ID 근거가 필요할 때 검색한다. 현재상태 중복복사를 하지 않는다. [인계정본 운영](WORK_HANDOFF.md).
6. **진행 중 작업 중복 확인.** 대상 TODO ID/선행 항목, 현재 진행 중 상태, 공개된 같은 ID의 작업 브랜치/PR, 서로 수정할 파일의 겹침을 확인한다. 이미 다른 에이전트가 동일 항목을 담당하면 임의로 중복 구현하지 말고 역할을 나누거나 보고한다.
7. **작업 범위 선언.** 작업 ID, 목표·비범위, 작업자(Work/Codex 등), 시작 기준 SHA, 브랜치, 예상 변경 파일, 검증 계획을 작업 기록 또는 PR에 명시한 뒤 착수한다.

### 권장 Git 확인 명령 (PowerShell / Bash 공통)

```sh
git remote -v
git status --short
git branch --show-current
git rev-parse HEAD
git fetch --prune origin
git rev-parse origin/codex/museum-tycoon
git log --oneline --left-right HEAD...origin/codex/museum-tycoon
```

깨끗한 상태이고 **현재 브랜치가 정확히** `codex/museum-tycoon`인 경우:

```sh
git pull --ff-only origin codex/museum-tycoon
```

이미 기능 브랜치에 변경사항이 있거나 로컬 상태가 깨끗하지 않으면 위 `pull`을 무작정 실행하지 않는다. GitHub의 최신 브랜치 HEAD를 확인하고 안전하게 통합할 방법을 결정한다. LFS 에셋을 실제 빌드·검증할 때는 해당 환경에서 `git lfs pull`과 에셋 복원 상태를 별도 확인한다.

## 2. 동시 개발 기본 규칙

- **하나의 물리적 작업 폴더에 여러 에이전트가 동시에 쓰지 않는다.** 특히 Unity `Library`/빌드/실행 서버 포트/로컬 `museum-archive` 데이터를 공유하지 않는다. 에이전트별 별도 Clone 또는 Git Worktree를 사용한다.
- 에이전트마다 **`origin/codex/museum-tycoon`의 최신 SHA에서** 작업 전용 브랜치를 만든다. 예: `work/react-001-work-<unique>`, `work/asset-002-codex-<unique>`. 작업 목적과 ID가 브랜치/PR에 드러나야 한다.
- 원칙적으로 **기능 브랜치 push → PR(초기에는 Draft 가능) → 회귀검증/변경 확인 → `codex/museum-tycoon`에 통합**한다. 이렇게 해야 다른 에이전트의 작업과 충돌을 조기에 식별한다. 실험 통합 브랜치에 병렬 직접 push하는 것은 지양한다. 기준 브랜치와 태그로의 자동 병합은 하지 않는다.
- 진행 사실은 동일 TODO ID가 포함된 브랜치/PR로 식별한다. `TASKS.md`/상태 문서는 작업 브랜치에서도 수정하되, **통합 브랜치의 최종 상태는 PR 병합 이후 다시 검증해 반영**한다. 동일 문서 동시 수정 시 충돌을 무시하거나 마지막 작성자로 덮어쓰지 않는다.
- 첫 작업에서 PR/이슈에 작업 ID, 담당 에이전트, 시작 SHA, 범위·의존 작업을 명시해 중복을 피한다. GitHub Issues/PR 조회를 지원하지 않는 에이전트는 현재 확인 불가 항목을 보고한다.
- 변경 작업 중에도 다른 PR 통합이 발생할 수 있다. **테스트 및 PR 병합 전 다시 `git fetch`와 최신 베이스 비교**를 실시한다. 베이스가 바뀌면 변경 영향·회귀검사를 재확인하고 안전하게 반영한다.
- 서로 의존적인 작업(예: 동일 Archive 스키마, `server.mjs`, `MuseumGuest.cs`)은 담당 파일/인터페이스를 협의하거나 **직렬화**한다. 별개 화면·문서·fixture는 독립 브랜치로 병렬화 가능하다.
- `origin/codex/museum-tycoon`으로 직접 push해야 하는 특수 상황이라면 **단독 작업이 보장되고 최신 HEAD 검사·충돌 없는 FF가 확인된 경우에만** 수행한다. 원격이 바뀌면 작업을 중단하고 다시 동기화한다.
- Public 저장소라는 점을 기억한다. 인증정보, 개인/가족 전시물·사진·이야기, 재배포 권리가 불확실한 모델은 임의로 push하지 않는다.

## 3. 작업 종료 및 다음 에이전트 인계 Gate

1. 해당 TODO의 완료 기준과 기존 기능 회귀 검사를 수행하고, 실제 검증 수준(코드 작성/PC 테스트/실제 Quest HMD)을 구분한다.
2. 코드+스키마+관련 문서가 같은 작업 ID를 가리키게 갱신한다. `TASKS.md` 체크박스는 **검증 근거가 있을 때만** 완료로 변경한다.
3. `PROJECT_STATUS.md`: 현재 현황·다음 작업·위험. `STATUS.md`: 현재 작업 폴더/브랜치/프로세스/중단점. `WORKLOG.md`: 실행 명령·테스트 결과·실패 사항·이력.
4. 커밋 전/PR 통합 전 최신 원격을 다시 `fetch`하고 원격 변경과 충돌 가능성을 확인한다. 충돌은 이해하고 해결하되 다른 작업을 덮어쓰지 않는다. 임의 `force push` 금지.
5. 담당 기능 브랜치에 커밋/push, PR 생성·갱신, 테스트 상태 기록. 통합되면 실험 브랜치의 변경사항/원격 SHA를 검증하고 **통합된 커밋 SHA/PR/남은 미검증**을 보고한다.
6. 통합 전이면 **'기능 브랜치 업로드 완료 / 아직 통합 안 됨'**이라고 구분하고, Work가 자동 구현/통합했다고 주장하지 않는다.
7. 다음 에이전트는 인계 시에도 처음의 **시작 Gate 전체를 다시 수행**한다. 앞선 세션의 메모리나 로컬 STATUS만 믿지 않는다.

## 4. 충돌·예외 처리 표

| 상황 | 필수 대응 |
|---|---|
| origin 접근/인증 실패 | 진행 중단, 최신화 실패 보고. 로컬 보존 |
| 로컬 미커밋 파일 발견 | 보존·차이 확인 후 사용자 또는 기존 작업자와 조율; 강제 폐기 금지 |
| 원격 베이스 업데이트 | `fetch` 후 diff 분석, 안전한 통합·영향 테스트 |
| `pull --ff-only` 실패 | 분기·충돌 원인 진단, 수동 통합 경로 결정; 강제 reset 금지 |
| 두 에이전트가 동일 TODO 시작 | PR/브랜치 확인, 담당 분담·순서 재조정 |
| 문서/TASKS 체크 충돌 | 최신 상태 확인하고 유효한 양쪽 변경을 병합. 근거 없는 완료 체크 금지 |
| GitHub 접속은 되지만 로컬 PC 검증 불가 | 원격 소스/설계만 확인하고 PC 빌드 미검증으로 표기 |
| 데이터 공개·파괴적 작업 필요 | 별도 사용자 승인 전 변경 금지 |

## 5. Work 시작 지시문 (다른 에이전트에도 재사용)

> `JohnLee305/myMusium`의 **`codex/museum-tycoon`** 최신 원격 기준으로 진행해 줘. 작업 시작 전에 `docs/GIT_MULTI_AGENT_WORKFLOW.md`의 시작 Gate를 반드시 수행해: 로컬 브랜치/HEAD/미커밋 변경을 보호하고, `git fetch --prune origin` 후 안전하게 최신 베이스를 동기화해. 그다음 최신 `AGENTS.md`, `TASKS.md`, `PROJECT_STATUS.md`, `STATUS.md`, `WORKLOG.md`, 관련 설계 문서를 읽고 기존 진행 ID/PR을 확인해. 다른 에이전트와 충돌하지 않게 전용 작업 폴더/기능 브랜치를 사용하고, 작업 완료 후 테스트와 문서를 갱신해 PR로 통합해. 최신화 실패나 충돌이 있으면 기존 변경을 건드리지 말고 보고해 줘. **과거 baseline이나 다른 로컬 폴더를 최신 소스로 사용하지 마.**
