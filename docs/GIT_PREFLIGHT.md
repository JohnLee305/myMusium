# OPS-002 Git 사전 점검 도구

작업 시작 전 Git 최신화와 중복 작업 확인을 보조한다. 문서 내용을 이해하거나 작업을 배정하는 기능은 아니며, 통과 후에도 AGENTS와 현황·설계를 읽는다. Git hook/CI/브랜치 보호를 설치하지 않는다.

## 실행

Windows (Node가 PATH에 없어도 설치된 Codex 번들 Node를 탐색):

```powershell
./tools/git-preflight.ps1 -TaskId OPS-002
```

Windows/Work 공통 (Node.js 필요):

```sh
node tools/git-preflight.mjs --task OPS-002 --root /absolute/repository/root
```

기본은 파일/브랜치를 변경하지 않고 `fetch --prune origin`으로 원격 참조만 갱신한다. 저장소 루트에서 실행하거나 --root로 정확한 루트를 지정한다. JSON ok=true·종료코드0은 기계적인 시작 Gate 통과, 종료코드1은 확인/중단 필요,2는 잘못된 호출이다. stdout에는 토큰·원격 URL·명령 stderr를 출력하지 않는다. 파일명/로컬경로는 포함되므로 개인 자료가 있는 결과를 공개 Git에 올리지 않는다.

### 선택적 동기화

```sh
node tools/git-preflight.mjs --task OPS-002 --update
```

정확히 codex/museum-tycoon이며 깨끗하고 stash·중복작업·필수문서 오류 없이 원격보다 뒤처진 경우만, 확인한 원격SHA로 `merge --ff-only`한다. 원격 자동push, 브랜치 전환, reset/clean/stash/rebase/충돌 해결은 하지 않는다. 작업 브랜치의 업데이트나 기준판·detached HEAD에서는 파일을 변경하지 않는다. 개인자료가 있는 작업폴더는 보존하고 별도 worktree에서 진행한다.

## 확인 범위

- 루트·origin이 JohnLee305/myMusium인지 확인 (GitHub HTTPS/SSH 형식), Git 경로를 재지정하는 환경변수 차단.
- HEAD/작업브랜치/원격SHA/앞섬·뒤처짐, 미커밋 및 untracked/stash, 전체 worktree 목록.
- 원격 최신화 실패/통합브랜치 부재/분기/기준판/미게시 통합커밋/필수문서 누락은 중단.
- gh로 열린PR 목록을 확인하고 작업ID가 같은 타브랜치PR 또는 로컬·원격 브랜치가 있으면 중단. 내 현재브랜치의 PR은 허용. 완료됐지만 남은 브랜치도 보수적으로 중단하므로 기록 확인 후 별도로 정리한다.
- gh 미설치/인증·연결 실패나 목록상한 도달 시 확인 불가로 중단. 중복 검사 생략 옵션은 CLI에 없다. gh 인증은 사용자가 기존 계정으로 준비한다.
- PR 없이 작업 중이며 아직 브랜치도 게시하지 않은 다른 Clone은 감지 불가. 작업 선언/LIVE_STATUS·PR 게시 절차는 여전히 필요하다.

## 검증

```sh
node tools/test-git-preflight.mjs
```

logs/git-preflight-tests-*에 실제 bare원격/clone/worktree를 만들고 정상·FF·dirty·stash·분기·원격실패·문서/중복PR·작업브랜치를 검사한다. 실제 개인Archive를 사용하지 않는다. 테스트의 GitHub PR 목록은 주입된 fixture이며 실제 gh 연결은 별도 저장소 실행에서 확인한다.

이 도구는 프로세스/Unity실행/개인Archive/바탕화면 적용을 검사하지 않는다. 기능 완료 Gate의 빌드·실행·사용자 확인을 대체하지 않는다.
