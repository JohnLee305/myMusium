# LIVE STATUS — 실시간 현황판 (정본: codex/museum-tycoon)

> 이 파일은 에이전트 공동 작업의 유일한 실시간 진실 원본이다.
> 코드·빌드·커밋 전에 이 파일을 먼저 읽는다. 작업 시작/종료 시 **자신의 블록만** 갱신하고 push한다.
> 남의 블록을 수정하지 않는다. 이 파일을 안 읽고 시작한 작업은 무효로 간주한다.
> 갱신 시각은 KST 기준이다.

## Codex / OPS-003 / 2026-10-17
- 진행 중: CI/PR보호·인계정본정리, 시작26e3056, codex/ops-003-ci-handoff-20261017.
- 다음: 구성/격리회귀→실제PR Actions 확인→필수검사보호→통합/인계. 앱/개인자료 변경 없음.

## Codex / OPS-002 / 2026-10-17
- 상태: 사용자 승인·구현/검증 완료. 통합 이력 [PR#11](https://github.com/JohnLee305/myMusium/pull/11)의 병합 상태와 SHA가 정본. 시작270760f, 구현커밋5b7b32d, sync-20261016.
- 작업: Git점검CLI/Windows실행기/가이드, 안전한 선택적FF. [사용법](GIT_PREFLIGHT.md). 격리Git17사례 PASS(logs/git-preflight-tests-1792208465009).
- 실제Windows CLI: dirty중단·GitHubPR조회 확인, 커밋 후clean Gate ok=true/blockers0(logs/git-preflight-clean-20261017.json). 내PR은 허용, 다른동일작업은 중단.
- 기존Unity/개인Archive/4186 변경 없음. 이전OPS-008 PR#9/#10 통합 완료. 완료작업브랜치는 병합확인 후 정리한다.
- 종료 Gate: PR#11 병합SHA/두실험폴더FF·개인자료해시 확인 후 보고. 다음 후보 OPS-003 CI/PR 보호이며 별도 작업 요청 시 시작한다.

## Codex / OPS-008 / 2026-10-17 최신 체크포인트
- 상태: PR#9 병합a28b4aa·양쪽실험소스FF 완료. 반응JSON Schema96/성향ID 후속 보정·계약80 PASS. 후속 이력 [PR#10](https://github.com/JohnLee305/myMusium/pull/10) 병합 상태 참조. 통합 이력은 [PR#9](https://github.com/JohnLee305/myMusium/pull/9)와 GitHub 병합 상태 참조. 기준726ee93, codex/ops-008-integration-review-20261017, 소스/빌드 sync-20261016.
- 아래 opencode 블록은 당시 기록으로 보존. ROUTE-002 PR#8·REACT-014/015·ROUTE-003은 이미 통합 완료. 삭제된 완료 작업브랜치로 돌아가지 않는다.
- 테마34개 이관, 기준판 세 파일 복원·clean 확인. 전체 Git bundle/기준판 patch: logs/ops-008-preservation-20261017. 개인 Archive51파일 해시 동일,6전시품416승인 유지.
- 최신 관리4186 실행, 구4181 정리 완료. 브라우저34프리셋/동선 안내/error0 확인, PC 바로가기 최신 빌드 실행. 스킬 원본/설치본96개 기준 동일.
- 검증·구조평가: [OPS-008 평가](INTEGRATION_REVIEW_20261017.md). 종료 Gate: PR#9 병합SHA/두 실험 작업폴더FF/개인자료 보존을 확인하고 최종 응답에 기록한다.
- 다음 기능: OPS-002/003 자동 Gate·CI, REACT-011 모델stale, 신규persona 배정·1,000개 확장, Quest 실기기. 자동 착수하지 않는다.

갱신: 2026-10-16 23:40 (KST) / 작성: opencode

## 지금 손대면 안 되는 것

- `C:\Dev\vr-museum-tycoon` 미커밋 개인자료 3건 — 읽기 전용, 커밋·되돌리기 금지:
  - `museum-archive/manifest.json` 수정 (전시품 room-1 배정 + room-1 테마 변경, 사용자 관리페이지 작업)
  - `museum-archive/artifacts/exhibit-mv9n8ahg-c57c76/` 미추적 (사용자 등록 전시품 + 반응 파일)
  - `museum-archive/visitors.json` 미추적 (관람객 설정, .gitignore 추가됨)
- `codex/museum-baseline` + 태그 `museum-baseline-20261015` — 보존, 기능 병합 금지.

## 진행 중 작업

| 작업 | 담당 | 폴더/브랜치 | 상태 | 마지막 SHA |
|---|---|---|---|---|
| REACT-014 생각풍선 만화풍·가시성 | opencode | sync-20261016 / codex/thought-asset-20261016 | 사용자 승인·tycoon 반영 완료 | CC0 구름+Jua 확정 |
| ROUTE-002 tycoon 통합 | opencode | sync-20261016 | 완료 (tycoon 반영 a5a6d3d) | 병합 9d6e492 |

## 검증 근거 (병합 트리)

- 빌드 `logs/unity-Build-20261016-225548.log` exit 0
- 내비 API 29 PASS `logs/navigation-tests-1792159033105`
- 출구 경계 PASS `logs/exit-boundary-tests-1792159033513` (single + four-directions)
- 투어 플레이어 PASS `tour-player-20261016-225816` (신규 픽스처)
- 상세 4 PASS `logs/details-tests-20261016-225722`

## 브랜치·통합 대기열

- `route-002` → `tycoon`: 병합 커밋 완료, Unity 빌드·회귀 PASS 후 tycoon 반영 push 예정. PR UI 미사용(gh 인증 없음, 기록에 명시).
- `route-002` 원 브랜치는 push 완료 상태로 유지, 삭제하지 않는다.

## 다음 주자 할 일

1. tycoon 반영 push 여부 확인 (`git rev-list HEAD...origin/codex/museum-tycoon`).
2. 반영됐으면 `tmp/route-002-merge-20261016` 브랜치 삭제 여부 확인, sync 폴더를 `route-002`로 원상복구했는지 확인.
3. Codex 한도 해제(10-17 02:22) 후 복귀 시 이 파일 정본부터 읽을 것.
