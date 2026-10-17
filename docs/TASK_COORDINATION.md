# OPS-004 에이전트 담당 현황과 중복 작업 방지

**실시간 목록:** [진행 중 GitHub 작업](https://github.com/JohnLee305/myMusium/issues?q=is%3Aissue+is%3Aopen). 담당·상태·브랜치·시작SHA·갱신시간·다음행동을 Issue에서 본다. 제목의 작업ID와 담당으로 찾는다. [OPS-004 실제 작업](https://github.com/JohnLee305/myMusium/issues/13).

GitHub Issue만 먼저 검색하고 작성하면 동시요청 둘 다 성공할 수 있다. 그래서 담당 선점의 정본은 작업별 `codex/claims/소문자ID` ref다. GitHub ref의 원자적 생성과 non-fast-forward 거절을 이용해 동일ID에는 한 세션만 성공한다. 이 ref는 소스버전이 아니며 코드에 합치거나 체크아웃/빌드하지 않는다. 담당정보는 커밋메시지의 작은 JSON이고 tree는 기존소스 그대로다. 완료후에는 released 이력을 남겨, 삭제/재생성 경합 없이 안전하게 다음 선점을 허용한다.

## 모든 에이전트의 시작 순서

1. 저장소·원격/미커밋/최신base를 먼저 확인하고 전용 codex/ID 브랜치를 만든다. [실시간 담당](https://github.com/JohnLee305/myMusium/issues?q=is%3Aissue+is%3Aopen)을 보거나 `list`로 공유현황을 조회한다.
2. 코드를 수정하기 **전에** `claim`한다. 성공한 세션만 작업한다. 중복/권한/네트워크실패는 진행승인이 아니다.
3. `git-preflight`를 실행한다. 미선점/다른세션/브랜치불일치/담당조회실패면 Gate가 중단한다. 내 PR과 내작업의 claims ref는 중복소스로 세지 않는다.
4. 인계JSON과 TASKS/설계를 확인하고 작업한다. 담당현황은 GitHub 실시간값, handoff JSON은 검증·다음행동 이력이다.

Windows (PATH에 Node가 없어도 Codex 번들 탐색):

```powershell
./tools/task-coordination.ps1 -Action list
./tools/task-coordination.ps1 -Action claim -TaskId OPS-123 -Agent Codex -Summary '작업 목적' -Next '다음 행동'
./tools/git-preflight.ps1 -TaskId OPS-123
./tools/task-coordination.ps1 -Action update -TaskId OPS-123 -Status review -Next 'PR 검증·사용자 확인'
```

Node/Work 공통:

```sh
node tools/task-coordination.mjs list
node tools/task-coordination.mjs claim --task OPS-123 --agent Work --summary "작업 목적" --next "다음 행동"
node tools/git-preflight.mjs --task OPS-123
node tools/task-coordination.mjs update --task OPS-123 --status blocked --next "사용자 결정 대기"
```

상태: in_progress / blocked / review / done / released. done은 작업결과를 확인하고 마무리할 때 사용하며, 실제 PR 병합은 별도로 확인한다. 미완료 작업은 done으로 해제하지 않는다.

```sh
node tools/task-coordination.mjs update --task OPS-123 --status done --next "PR URL·병합SHA·다음 업무"
node tools/task-coordination.mjs release --task OPS-123
```

release는 기존담당의 영수증과done을 요구하고 Issue를 닫은 뒤 ref를 released로 바꾼다. 이력은 보존되고 새담당의재선점은한명만성공한다. 소스작업브랜치 삭제는 별도의병합확인절차를따른다. 작업선점ref는완료브랜치일괄정리대상이아니다.

## 세션 소유권·중단 복구

- 랜덤 세션토큰 영수증은 `logs/task-claims/ID.json`에만 저장한다. Git ignore를 확인하고 토큰은 stdout/Issue/Git에 올리지 않는다. GitHub에는 해시만 저장한다. GitHub쓰기권한을가진동일계정의악의적변경에대한보안경계가아닌협조적에이전트의경합방지다.
- Issue 생성/상태전파가실패해도선점정본은보존한다. 같은영수증으로 `claim` 또는 `update`를다시실행하면 Issue연결을복구한다. 같은세션의재시도는새선점/중복Issue를만들지않는다. 응답이유실돼도다른담당이라고가정하여지우지않는다. `list`로확인한다.
- 선점거절후남은로컬영수증은작업권한이아니다. 원격담당과해시/브랜치가일치하지않으면시작Gate가거절한다. 다른세션의영수증을자동복사하거나원격ref를강제변경하지않는다.
- 시간이오래됐다고자동만료/빼앗기를하지않는다. 다른담당은중단·대기한다. 인계승인시 원담당은blocked/nextAction에인계를기록하고, 같은작업브랜치와영수증을안전한로컬수단으로전달한다. 원담당은작업을중단하고더이상그영수증을사용하지않는다. 수신자는 `update --task ID --agent 새담당 --status in_progress --next 인계내용`으로담당표시를바꾼다. 새세션으로무단추측복구하지않는다.
- 원영수증을분실한경우소유권확인이불가능하므로자동takeover명령은제공하지않는다. 사용자/원담당에게확인하고원격정본SHA와작업자료를보존한별도복구를진행한다. 이한계는인계파일에기록한다.

## 검증과 한계

- 실제두Node프로세스·동일bare원격에동시선점→정확히한명성공. 타세션update/release거절·다른ID동시작업·세션/브랜치Gate·차단작업보존·경쟁update거절·완료후재선점/구토큰거절·소스불변·유실응답재시도13사례.
- Git시작Gate에도미선점/타담당/조회불가중단회귀. CI회귀는격리원격을쓰며실제GitHub담당을선점하지않는다. 실제PR정책검사는공개GitHub담당정본을조회해task/branch/agent가인계JSON과맞는지확인한다. 담당조회가실패하면통합을중단한다.
- 도구를쓰지않는사람/에이전트의파일편집을OS수준에서막지는않는다. AGENTS필수절차·PR·CI와함께사용한다.
- 공개Issue에는개인모델/대사/경로/자격정보를쓰지않는다. 작업목적·개발브랜치등공개개발정보만허용한다.
