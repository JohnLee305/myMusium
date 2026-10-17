# 다른 에이전트에게 작업 인계하기

**실시간 담당은 [GitHub 작업목록](https://github.com/JohnLee305/myMusium/issues?q=is%3Aissue+is%3Aopen) / `node tools/task-coordination.mjs list`로 확인한다.** [OPS-004 선점/갱신/해제](TASK_COORDINATION.md)는 코드 수정 전 필수이며 인계JSON의 과거 상태만으로 현재 담당을 추측하지 않는다.

## 먼저 찾을 세 가지

1. **최신 코드:** origin/codex/museum-tycoon와 실제 PR 병합SHA. GitHub 기본브랜치도 동일한 단일 최신판이다.
2. **어디서 실행하는가:** PROJECT_STATE.json. Windows 경로는 그 PC용이며 클라우드에서 존재한다고 가정하지 않는다.
3. **누가 무엇을 하는가:** handoffs/ID.json → 자동생성 LIVE_STATUS.md. 기록된 프로세스PID가 현재 살아 있다고 가정하지 않는다.

## 작성할 것

작업당 인계JSON 하나를 만든다. 기존 인계파일을 복사해 taskId/owner/branch/baseSha/date/status/summary/validation/nextAction/pullRequest/limitations를 채운다. 파일명은 ID.json, 브랜치는 codex/와 ID를 포함한다. 상태는 in_progress / blocked / validated. validated는 검증 완료이며 병합 완료를 뜻하지 않는다. 실제 통합은 PR 링크의 GitHub 상태로 확인한다.

- 시작: TASKS의 ID와 내 인계JSON에 범위·담당·시작SHA·다음행동 기록. 초기에 PR을 게시해 미게시 Clone 중복을 줄인다.
- 검증: validation에 실제 명령·결과·필요한 로그/CI 링크만 남긴다. 미검증/실패는 limitations에 쓰고 완료로 표시하지 않는다.
- 다음 에이전트: nextAction의 한 가지 구체적인 동작부터 시작. 중단된 담당의 commit/untracked/stash/PR부터 확인하고 작업기록에 이어받음을 선언한다. 남의기록 삭제/덮어쓰기 금지. 이어받는 새담당의 변경은 소유권 인계를 명시한 뒤 해당ID파일에 기록한다.
- 종료: 사용자 확인/통합 승인 → 최신fetch/CI → PR → 병합SHA 확인 → 실제실행폴더FF/개인자료보존 → 완료브랜치정리. 새업무는 별도ID/브랜치.
- 현황판 갱신: `node tools/handoff-state.mjs`. 생성물을 직접 편집하지 않는다. 겹치는 생성물 충돌은 입력JSON을 보존·병합한 뒤 다시 생성한다.

## 문서 역할을 겹치지 않게 유지

| 파일 | 역할 |
|---|---|
| PROJECT_STATE.json | 통합ref·보호ref·PC배포/개인자료경로·계약한도·마지막 실제실행 검증 근거 |
| handoffs/ID.json | 작업담당·시작점·검증·다음행동·PR 연결 |
| LIVE_STATUS.md | 위 정본으로 생성한 첫 화면 |
| TASKS.md / PROJECT_STATUS.md | 할일·제품계획·완료수준, 실행경로 복사 금지 |
| WORKLOG.md / STATUS.md | 날짜별 역사와 의미있는 작업 시작/종료; 최신판 판단 근거로 단독사용 금지 |
| history/ | 이전인계/지침 원문 보존, 현재명령으로 사용하지 않음 |

소스커밋·빌드·개인자료는 서로 다른 생애주기다. 소스가 최신이어도 빌드는 과거일 수 있으므로 앱 변경시 buildLog/실행경로를 재검증하고 PROJECT_STATE를 갱신한다. 빌드파일/개인자료를 여러clone에 복제해버전을 맞추지 않는다.

## 새 에이전트에게 전달할 문장

> JohnLee305/myMusium의 origin/codex/museum-tycoon을 기준으로 진행해 줘. AGENTS.md와 docs/LIVE_STATUS.md부터 읽고 Git 사전Gate를 수행해. docs/PROJECT_STATE.json에서 실제실행/개인자료경로를 확인하고, TASKS의 해당ID와 작업별 인계JSON에서 담당·다음행동을 확인해. 별도 codex/ID 브랜치로 작업하고 실제검증 뒤 인계JSON과 생성현황판을 갱신해. 개인자료와 과거 복구기록은 보존하고 PR/CI를 거쳐 통합해.

[상세 Git 절차](GIT_MULTI_AGENT_WORKFLOW.md) · [과거 인계 원문](history/WORK_HANDOFF_20261017.md)
