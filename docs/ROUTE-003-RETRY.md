# ROUTE-003 재시도 인계 (2026-10-17 작성, 원복 후)

## 하려던 것

동선 편집 UI 직관화 2건 (사용자 승인済み, "둘 다").

1. **벽 슬롯 직접 클릭 배치**: 격자 위·아래·양옆에 벽 슬롯 버튼을 그려 `입구 지정/출구 지정` 모드 따라 문 배치. `아래 벽 3번째` 같은 글로 고르는 드롭다운은 보조로 유지.
2. **저장 전 도달 가능 미리보기**: 서버 저장 검사와 같은 `checkReachable`(navigation.mjs BFS)을 브라우저에서 그대로 import해 격자 아래에 표시. `navigation.mjs`를 서버 에셋으로 추가 제공.

## 한 것 (전부 원복됨, 브랜치 삭제)

- `codex/route-003-nav-editor-20261017` (로컬·원격 삭제). 작업 트리는 REACT-015(`3c205b5`)로 복귀.
- 구현 파일: `navigation-editor.js` 전면 개편, `index.html` 모드 버튼+미리보기 요소, `style.css` wall-slot/nav-map, `server.mjs` 에셋 추가, `test-navigation.mjs` 에셋 검사 추가.
- 검증됐던 것: Node 29개 통과, 미리보기 도달/차단 양방향 확인, 에셋 제공 확인.

## 실패 증상

- 관리페이지 동선 섹션(전체 페이지 가능) 표시 붕괴. 1차 "넓이 깨짐" → 8열 격자로 수정 후에도 "다 개판".
- DOM 스텁 모의실행(Node, 브라우저 없음)에서 `syncNavigationEditor → render`가 DOM 생성 20만 회 후 OOM. 단일 render 안에서 격자 행 루프 부근(구 코드 47행, 오른쪽 벽 슬롯 append)에서 append 폭주. 정확한 무한 반복 경로는 미확정.

## 다음 세션 주의점

1. **원인부터 확정**: 커밋된 `tools/test-navigation.mjs`는 통과하지만 DOM 렌더를 검사하지 않는다. 먼저 DOM 스텁 회귀 테스트(예: `tools/test-navigation-editor-dom.mjs` 신설, jsdom 없이 getElementById/createElement 스텁 + 연산 횟수 상한)로 폭주를 재현·수정하고, **사용자가 실제 4186 페이지에서 눈으로 확인한 뒤** push한다. 작성자는 브라우저를 직접 못 보므로 이 확인을 생략하지 않는다.
2. **작게 나눠서**: 벽 슬롯 배치와 미리보기를 별도 커밋·별도 확인으로 진행한다. 한 번에 둘 다 올리지 않는다.
3. **서버·상한 정합**: 개인 Archive에는 96개 반응 파일이 있다. 96-cap 이전 코드로 4186을 실행하면 반응 섹션이 오류가 난다. 동선 작업 브랜치는 REACT-015 포함 여부(merge 또는 rebase)를 먼저 결정하고, 4186 재시작 후 권총 96개 조회를 매번 확인한다.
4. **4186 재시작 절차**: 해당 포트 PID 확인 → 종료 → 같은 Archive 경로로 새 코드 실행 → `/api/reactions`(권총 96) + `/navigation.mjs` 200 확인. 시험용 임시 포트는 사용 후 반드시 종료한다.
5. **베이스**: REACT-015 PR 미병합 상태에서는 동선 브랜치 베이스를 `origin/codex/museum-tycoon` 최신으로 하고, 필요시 REACT-015를 merge한다(2026-10-17에 merge로 해결한 전례). PR은 REACT-015 병합 후 ROUTE-003 순서를 권장한다.

## 재시도 결과 (2026-10-17, 브랜치 `codex/route-003-wall-slots-20261017`, 커밋 `a9660ce`, push 후 원격 SHA 일치)

- DOM 가드 `tools/test-navigation-editor-dom.mjs` 신설(8개): 렌더 상한 + 벽 슬롯 개수/순서 + 미리보기 도달/차단. 기존 `test-navigation.mjs` 29개 통과(`/navigation.mjs` 제공 확인 포함).
- 벽 슬롯: 3열 격자 정렬, 좌우 열은 위→아래 5→1 표시로 방 좌표와 일치. 사용자 눈 확인 통과.
- 미리보기: 격자 아래 "저장 전 확인" 1줄, 서버와 같은 `checkReachable`. 사용자 눈 확인 통과.
- 4186 재시작 2회(벽 슬롯은 디스크 직독으로 무재시작, `server.mjs` 에셋 추가는 재시작 필요). 실제 Archive 수정 없음.
