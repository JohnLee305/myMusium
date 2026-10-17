# OPS-003 PR 검증과 인수인계 비용 개선

## 적용 구성

- Museum CI: codex/museum-tycoon 대상 PR과 통합 후 push에 실행. Ubuntu/Windows Node24,13개 회귀묶음과 인계정책 검사. 경로필터 없이 필수검사가 누락되지 않게 한다.
- museum-required: 모든 검사 결과가 success일 때만 성공. 취소/실패/건너뛴 검사는 통과시키지 않는다.
- Actions는 contents:read만 사용,checkout 토큰 저장 금지,공식 checkout/setup-node를 검증한 commit SHA로 고정. pull_request_target이나 자체호스트/개인Archive를 쓰지 않는다.
- PR은 작업ID를 제목에 넣고 자신의 docs/handoffs/ID.json을 갱신한다. 코드/설정 변경은 TASKS 갱신도 필요. 실제검증을 마친 인계 상태validated·정확한브랜치·필수문서·생성현황판 일치를 검사한다.
- 현재 작업별JSON의validation은 에이전트가 기록한 근거다. CI회귀/실제GitHub PR 검사는 따로 확인하며 그 기록을 검증된 실행으로 자동 간주하지 않는다.

## 브랜치 보호

codex/museum-tycoon만 대상으로 PR 경유·museum-required 필수·최신base필수·대화해결·관리자도적용·force push/삭제금지를 적용했고 API 재조회로 확인했다 (2026-10-17). 기존 기준브랜치/태그/기본브랜치는 바꾸지 않는다. 요구승인리뷰 수는0으로 하여 단독사용자를 다른GitHub계정 승인 때문에 막지 않는다. 사용자의 채팅상 통합승인 규칙은 AGENTS에 유지한다. 실제 적용 여부/검사URL은 OPS-003 인계JSON에 기록한다.

## 버전이 분산된 이유와 정리 원칙

이전에는 로컬폴더·Git브랜치·실행빌드·개인자료가 각각 버전처럼 취급됐고, STATUS/LIVE_STATUS/PROJECT_STATUS가 서로 다른 시점의 최신상태를 복사했다. 기록량보다 정본과 소유권이 불명확했던 것이 인계비용의 주원인이다.

1. 소스는 origin/codex/museum-tycoon의 실제SHA 하나를 정본으로 한다. 작업브랜치는 통합 후 정리하며 최신SHA를 모든MD에 복사하지 않는다.
2. 실행/자료경로와보호대상은 PROJECT_STATE.json 한곳. 빌드검증은 날짜/로그로 기록하고 지금실행중이라고 해석하지 않는다. 앱변경시 실행경로 검증은 여전히 필요하다.
3. 작업별인계JSON은 작업담당이 소유한다. 다른작업JSON을 덮어쓰지 않는다. 여러작업이 겹칠때 LIVE_STATUS 충돌은 입력JSON을 보존한 뒤 재생성한다.
4. TASKS는계획,WORKLOG/STATUS는역사. 현재판판단을 위해긴로그를전체다시읽지 않는다. 과거지침은history/에원문보존하고현행명령으로사용하지 않는다.
5. OPS-004 후속에서 담당선점/중복감지의GitHub Issue 또는Draft PR 절차를 더자동화할수있다. 아직미게시Clone의동시작업은기계적으로찾지못한다.

## 실행과 검증

```sh
node tools/handoff-state.mjs
node tools/handoff-state.mjs --check
node tools/ci-policy.mjs
node tools/run-ci.mjs
```

Unity 빌드·Quest·실제데스크톱연결은 이CI에 포함되지 않는다. 공개샘플LFS를checkout하므로개인자료는올리지않는다. CI실패를무시하거나관리자우회로통합하지않는다.

참고: [GitHub 최소토큰권한](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token), [브랜치 보호·필수검사](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).

## 실제 검증 결과

- [성공한 Actions39000998335](https://github.com/JohnLee305/myMusium/actions/runs/39000998335): 인계정책·Ubuntu/Windows13회귀·museum-required 모두success.
- [최초 실패39000864161](https://github.com/JohnLee305/myMusium/actions/runs/39000864161): 고정 C:/Dev 기준판 경로 때문에 제어시험 실패,집계검사도failure. 저장소상대경로+격리Archive로 수정한뒤통과.
- 실제두Git브랜치 비충돌합류/충돌중단·인계누락/미검증거절8사례, 기존 Git17안전성사례 포함.
- 브랜치보호는 tycoon만 설정·재조회. 기준판/태그/기본브랜치 변경 없음. 기존CLI의workflow scope 부족은 기존GitHub연결로 파일업로드하여해결; 새계정권한 추가 없음.
- [PR#12](https://github.com/JohnLee305/myMusium/pull/12)의최종검사·병합상태·SHA가현재통합정본이다.
