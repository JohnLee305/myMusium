# SEC-001 · 공개 자료 초기 목록 점검

일자: 2026-10-16. 기준 소스 f1e3bd7와 REACT 작업 브랜치. 상태: **초기 점검 수행, 전체 개인정보/과거 이력 감사는 미완료**. 저장소 Public 상태와 기본 브랜치 codex/museum-baseline을 GitHub에서 조회했다. 공개범위·기준판·Git 이력을 변경하지 않았다.

## 수행한 점검

- 현재 추적 Archive GLB 5개를 SHA-256으로 대조했다. 자체 샘플3종과 등록 사본2개이며 등록 사본의 바이트는 해당 자체 샘플과 일치한다. 자체 샘플 README에 외부 모델/사진/텍스처 없이 생성했다고 기록되어 있다.
- Quaternius 동봉 License.txt/Men-License.txt/Women-License.txt에 CC0 1.0 문구가 있음을 확인했다. 이번에 추가 다운로드/재배포한 에셋은 없다. 다른 팩·새 배포본에 이 판정을 확대하지 않는다. 기존 출처/수령 해시는 third_party/visitors/*/provenance.json 참조.
- 현재 추적 파일명에서 .env/개인키/credentials/관리 백업/visitors.json 패턴 0개. 이는 내용 기반 비밀정보 전수검사 결과가 아니다.
- 로컬에 가져온 모든 ref의 Archive 변경 경로 17개를 목록 점검했다. 과거 설명 본문·모든 LFS 과거 버전·이미지 내부 개인정보·도달 불가능한 커밋까지 검사했다는 뜻이 아니다.
- 원래 실험 폴더의 미추적 새 전시품·관람객 설정 및 manifest 변경을 보존했고 이 PR에 넣지 않았다. 새 반응 원문/승인문/백업은 실제 개인 데이터로 취급하며 이번 검증은 가상 fixture만 사용했다.

| 공개 추적 GLB | SHA-256 앞16자리 | 확인 근거 |
|---|---|---|
| `museum-archive/artifacts/exhibit-mv6sl5rz-49a731/models/pc.glb` | 3dc34174e5cfb4b6… | 자체 샘플과 SHA-256 일치 |
| `museum-archive/artifacts/exhibit-mv6sllm7-45d745/models/pc.glb` | f137eff8df7dc704… | 자체 샘플과 SHA-256 일치 |
| `museum-archive/artifacts/sample-books/models/pc.glb` | 3dc34174e5cfb4b6… | 자체 생성 샘플 README/생성기 있음 |
| `museum-archive/artifacts/sample-sculpture/models/pc.glb` | f137eff8df7dc704… | 자체 생성 샘플 README/생성기 있음 |
| `museum-archive/artifacts/sample-vase/models/pc.glb` | 15086cfac40a5f61… | 자체 생성 샘플 README/생성기 있음 |

## 보관 정책 및 남은 결정

코드·직접 생성 샘플·권리 확인 에셋·가상 시험자료만 공개 Git에 추가한다. 실제 소장품/사진/이야기/반응 후보·승인문/백업은 공개 대상이라고 자동 간주하지 않는다. 신규 개인 Archive는 별도 비공개 저장 위치를 사용하고 공개 승인된 자료만 선택적으로 내보낸다. 캐시/게임 세이브 삭제가 Archive 원본을 지우지 않게 한다.

SEC-001 완료에는 현재/과거 전시품 메타데이터의 공개 적합성 확인과 필요 시 사용자에 의한 처리 결정이 남아 있다. 이야기 본문을 자동으로 외부 AI에 보내거나 이력을 삭제/재작성하지 않았다. 현재 로컬 개인 Archive를 별도 경로로 자동 이전하지 않았고 새 reactions.json은 개인정보 스냅샷을 포함할 수 있어 공개 코드 저장소에서 기본 ignore한다. 기존 개인 자료를 보관하는 이전 폴더의 ignore/추적 상태나 전체 저장소 공개 정책을 자동 변경한 것은 아니다.
