# 박물관 개발 에이전트 시작 지침

## 1. 작업 전 필수 Gate

1. 이 저장소는 `JohnLee305/myMusium`, 소스 정본은 `origin/codex/museum-tycoon`이다. GitHub 기본브랜치도 이 최신 통합판으로 통일한다. Windows 개발·빌드·실행은 PROJECT_STATE의 sourceRoot 한 곳만 사용한다. C:/Dev/vr-museum 및 C:/Dev/vr-museum-tycoon은 기록/개인자료 보존용이며 개발·실행하지 않는다.
2. 실제 루트·HEAD·브랜치·미커밋/미추적 자료를 확인하고 fetch한다. [공유 담당 목록](https://github.com/JohnLee305/myMusium/issues?q=is%3Aissue+is%3Aopen) 또는 `node tools/task-coordination.mjs list`로 담당을 확인하고, 전용브랜치에서 `claim --task ID --agent 담당 --summary 목적`에 성공한 뒤 작업한다. [담당 선점 절차](docs/TASK_COORDINATION.md)를 따른다. Windows `./tools/git-preflight.ps1 -TaskId 실제ID`, 공통 `node tools/git-preflight.mjs --task 실제ID`를 사용한다. 시작 전 detached면 최신원격에서 작업전용 codex/ID 브랜치를 먼저 만든다. dirty/분기/원격실패/중복작업은 자료를 보존하고 보고한다. 자동stash/reset/clean/force push 금지.
3. **읽기 순서:** 이 파일 → [현재 현황](docs/LIVE_STATUS.md) → [배포 정본](docs/PROJECT_STATE.json) → TASKS.md의 해당ID → 관련 설계/인계. STATUS/WORKLOG 전체와 과거 문서를 매번 읽지 않는다. 필요할 때 해당ID의 검증 이력만 검색한다.
4. 작업별 별도 Clone/Worktree·codex/ID 브랜치를 사용한다. 같은 물리폴더·Unity Library/빌드·관리포트를 동시에 쓰지 않는다. 동일 작업의 PR/브랜치 및 docs/handoffs 기록을 확인한다.

## 2. 진행·종료·통합

- 진행/막힘/검토 상태와 다음행동은 task-coordination update로 공유하고, 완료·통합 확인 후 done→release로 해제한다. 남의 담당을 시간만으로 빼앗지 않는다. codex/claims/*는 협업메타데이터이며 소스버전/빌드/일반완료브랜치정리 대상이 아니다.
- 시작 전에 `docs/handoffs/ID.json`에 담당·브랜치·시작SHA·범위·다음행동을 기록한다. 남의 인계파일을 수정하지 않는다. [인계 가이드](docs/WORK_HANDOFF.md)의 예를 따른다.
- 실행/자료 경로·보호대상·한도는 PROJECT_STATE.json, 작업의 상태/근거는 작업별JSON, 계획은 TASKS/PROJECT_STATUS, 역사적 검증은 WORKLOG/STATUS에 둔다. 같은 최신정보를 여러곳에 다시 복사하지 않는다. LIVE_STATUS는 생성물이며 `node tools/handoff-state.mjs`로 갱신한다.
- 의미 있는 작업 전후 STATUS/WORKLOG에는 해당ID의 시작·결과·재개위치를 간결히 남긴다. 성공/미검증/예정을 구분한다. PC 검증을 Quest 실기기 완료로 기록하지 않는다.
- 완료 시 실제 회귀·실행 경로를 검증한다. 사용자에게 확인 가능한 결과를 제시하고, 사용자가 확인·승인한 뒤 커밋·push·통합한다. 사용자의 명시적 통합 요청은 해당 작업의 승인으로 기록한다. GitHub CI 자체를 검증하는 작업의 전용브랜치 push/시험PR은 그 구현·검증 범위에 포함한다.
- 통합 전 최신fetch/기준차이·CI를 다시 확인한다. 작업 ID를 제목에 넣고 PR로 codex/museum-tycoon에 통합한다. PR에 실제검증/남은위험을 적고 승인 없는 강제병합·검사 우회는 하지 않는다.
- 최종 상태: 파일 생성/기능브랜치push/PR미병합/통합완료를 구분한다. 병합SHA 확인→사용자 실행폴더FF→개인자료 보존→바탕화면 경로 확인. 완료브랜치 정리는 병합 확인 후. 현재 최신SHA를 문서마다 손으로 복사하지 않는다. GitHub의 실제 PR 병합SHA가 정본이다.
- 사용자 요청(2026-10-17)으로 [작업 회고 게시판](https://github.com/JohnLee305/myMusium/issues/35)을 운영한다. 의미 있는 작업 후 본인의 판단·시행착오·생각·소감을 자유롭게 댓글로 남긴다. 댓글 하나는 공백·줄바꿈 포함 1,000자 이하, 반말이 원칙이다. 욕도 자연스럽게 섞어 쓸 수 있으나 의무는 아니다. 보고서 양식·필수 항목 없이 자연스럽게 쓰고, 사람처럼 보이려고 경험이나 감정을 지어내지는 않는다. 기존 댓글을 읽고 중복을 피하며 개인 자료·자격정보는 공개하지 않는다. 이 게시판의 회고와 후속 의견 작성은 사용자 요청 범위다. 게시판은 계속 열어 두며 제안만으로 새 작업을 임의 시작하지 않는다. [작성 안내](docs/RETROSPECTIVES.md)를 따른다.

## 3. 보호와 제품 방향

- 2026-10-17 사용자 승인으로 기준판을 은퇴했다. 단일 정본 codex/museum-tycoon과 개인 Archive를 보호한다. baseline 브랜치/태그를 다시 만들거나 과거 폴더에서 개발·실행하지 않는다. Git 과거기록은 로컬 bundle/백업에 보존한다. 임시 작업브랜치는 검증·PR 통합 후 삭제한다.
- 개인 Archive/모델/대사/가족기록은 공개Git/외부AI에 무단 전송하지 않는다. 실행소스와 개인자료를 분리하고 기존 metadata/모델/설정/승인을 보존한다.
- PC Quest Link 우선, standalone 후속. 소장품/스토리에 반응하는 살아있는 박물관이 목적이며 경제/해금은 미래 선택 확장이다.
- 새 모델 맞춤 대사/보충 요청은 [.agents/skills/museum-model-reactions/SKILL.md](.agents/skills/museum-model-reactions/SKILL.md)를 읽는다. 에이전트가 관찰→작성→검토→요청에 따라draft/승인→API·실제표시를 확인한다. 업로드 자동실행/1,000개 저장은 미구현.
- 설치는 공식출처와 게시자서명/체크섬을 확인한다. 이 지침은 샌드박스 밖 쓰기 권한을 부여하지 않는다.

[Git 운영 상세](docs/GIT_MULTI_AGENT_WORKFLOW.md) · [과거 지침 보존본](docs/history/AGENTS_20261017.md)
과거 지침의 경로·절차가 충돌하면 이 시작 지침과 현재 배포정본이 우선한다.
