# 단일 최신판 운영

2026-10-17 사용자 승인으로 기준판/실험판 운영을 종료한다. 기능은 codex/museum-tycoon에 모으며 GitHub 기본브랜치도 동일하다. 이름의 tycoon은 기존 브랜치 식별자이며 다른 앱 버전이 있다는 뜻이 아니다.

| 대상 | 단일 기준 |
|---|---|
| 소스·기본브랜치 | codex/museum-tycoon |
| Windows 개발·빌드·실행 | C:/Dev/vr-museum-sync-20261016 |
| 개인 전시 자료 | C:/Dev/vr-museum-tycoon/museum-archive |
| 관리 | http://127.0.0.1:4186 |
| PC·Quest Link | 동일한 최신 Builds/Windows/VRMuseum.exe와 개인 Archive |

다른 환경의 에이전트는 최신 통합판에서 독립 작업폴더/임시브랜치를 만든다. 완료 후 PR·필수검사를 거쳐 통합하고 작업브랜치를 삭제한다. ADMIN/OPS/ROUTE 등은 작업 종류/ID다. codex/claims/*는 담당 충돌 방지 기록이며 병합하거나 소스로 실행하지 않는다.

C:/Dev/vr-museum와 C:/Dev/vr-museum-tycoon의 소스/빌드는 과거 보존 자료다. 개발·빌드·실행하지 않는다. 특히 개인 Archive 폴더는 소스가 아니며 임의삭제/공개게시하지 않는다. C:/Dev/vr-museum-snapshots는 오프라인 과거 복구 기록이며 별도 배포판이 아니다. 기준판 전용 Git 기록은 활성 소스 logs/ops-009/pre-retirement.bundle에 검증하여 보존했다.

README·AGENTS·LIVE_STATUS에서 PROJECT_STATE.json으로 진입한다. 과거 STATUS/WORKLOG/인계의 경로는 당시 기록이다. 과거 버전 운영 문서는 history/VERSIONING_20261017.md에 남긴다. 앞으로 baseline 브랜치/태그나 기준판 바로가기를 다시 만들지 않는다. 단일 최신판을 되돌릴 필요가 있으면 사용자 자료를 보존하고 해당 변경을 PR로 되돌린다.

기존 기준판의 Quest 검사 이력은 최신판 실기기 검증으로 승계하지 않는다. PC Quest Link 우선 방향은 유지하지만 최신판의 실제 기기 확인은 별도다.
