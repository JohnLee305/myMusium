# LIVE STATUS — 실시간 현황판 (정본: codex/museum-tycoon)

> 이 파일은 에이전트 공동 작업의 유일한 실시간 진실 원본이다.
> 코드·빌드·커밋 전에 이 파일을 먼저 읽는다. 작업 시작/종료 시 **자신의 블록만** 갱신하고 push한다.
> 남의 블록을 수정하지 않는다. 이 파일을 안 읽고 시작한 작업은 무효로 간주한다.
> 갱신 시각은 KST 기준이다.

갱신: 2026-10-16 22:55 (KST) / 작성: opencode

## 지금 손대면 안 되는 것

- `C:\Dev\vr-museum-tycoon` 미커밋 개인자료 3건 — 읽기 전용, 커밋·되돌리기 금지:
  - `museum-archive/manifest.json` 수정 (전시품 room-1 배정 + room-1 테마 변경, 사용자 관리페이지 작업)
  - `museum-archive/artifacts/exhibit-mv9n8ahg-c57c76/` 미추적 (사용자 등록 전시품 + 반응 파일)
  - `museum-archive/visitors.json` 미추적 (관람객 설정)
- `codex/museum-baseline` + 태그 `museum-baseline-20261015` — 보존, 기능 병합 금지.

## 진행 중 작업

| 작업 | 담당 | 폴더/브랜치 | 상태 | 마지막 SHA |
|---|---|---|---|---|
| ROUTE-002 tycoon 통합 | opencode | sync-20261016 / tmp/route-002-merge-20261016 | 병합 완료·빌드 중 | 9d6e492 (merge), 기반 05456e6 |

## 브랜치·통합 대기열

- `route-002` → `tycoon`: 병합 커밋 완료, Unity 빌드·회귀 PASS 후 tycoon 반영 push 예정. PR UI 미사용(gh 인증 없음, 기록에 명시).
- `route-002` 원 브랜치는 push 완료 상태로 유지, 삭제하지 않는다.

## 다음 주자 할 일

1. tycoon 반영 push 여부 확인 (`git rev-list HEAD...origin/codex/museum-tycoon`).
2. 반영됐으면 `tmp/route-002-merge-20261016` 브랜치 삭제 여부 확인, sync 폴더를 `route-002`로 원상복구했는지 확인.
3. Codex 한도 해제(10-17 02:22) 후 복귀 시 이 파일 정본부터 읽을 것.
