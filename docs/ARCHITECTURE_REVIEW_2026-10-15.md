# VR 박물관 현재 아키텍처 점검 · 2026-10-15

> 대상: JohnLee305/myMusium, `codex/museum-tycoon` 브랜치. 기준판 `codex/museum-baseline`은 보존한다.
> 성격: 코드·스키마·개발 기록의 **정적 검토**. 새 빌드나 Quest 실기기 검증 결과가 아니다.
> 판단 원칙: 실제 구현을 우선 확인하고 기존 기능을 깨지 않는 점진적 개선을 제안한다. 아래의 '권장'은 아직 승인된 설계 결정(ADR)이 아니다.

## 1. 현재 구조 (코드에서 확인)

```text
museum-archive/
  manifest.json                 전시품 목록 + room/cell/theme/trash
  artifacts/{id}/artifact.json  title/description/model/scale, schemaVersion 1.0
  artifacts/{id}/models/pc.glb  현재 업로드된 전시 모델
  visitors.json                 실험 관람객 실행 설정(존재 시)
  schemas/                     Archive JSON Schema
           │
           ├── apps/curator-admin/         Node 내장 HTTP + HTML/JS + Three.js
           │     server.mjs                등록/수정/배치/테마/관람객 설정 API
           │     preview-checks.mjs        모델 구조·크기·지원 확장 검사
           │     preview.js                브라우저 3D 미리보기
           │
           ├── tools/validate-archive.ps1  별도 Archive 검증
           │
           └── apps/quest-viewer/          Unity + glTFast + OpenXR
                 ArchiveData.cs            파일/manifest/GLB 검증 및 읽기
                 ArtifactDisplay.cs        GLB 읽기·생성·리소스 정리
                 GalleryDisplay.cs         30슬롯 배치·방 전환·이전 방 해제
                 MuseumVisitors.cs         관람객 시뮬레이션 및 설정 갱신
```

현재 PC 관람/관리/방 편집/테마/웹 미리보기는 구현되고 PC 검증 기록이 있다. 관람객 시뮬레이션도 실험판에서 구현되어 있다. **실제 Quest HMD 테스트는 미완료**이며, 미리보기/Windows 검사 결과를 Quest 성능의 근거로 해석하면 안 된다.

## 2. 계획과의 정합성

| 항목 | 현재 확인 | 평가 |
|---|---|---|
| Archive가 Source of Truth | 파일·JSON을 Admin과 Unity가 읽고 씀 | **유지** |
| Scene 수정 없이 전시품 추가 | manifest 기반 동적 GLB 로딩, 재실행 시 반영 | **유지** (실시간 새로고침 아님) |
| Room 단위 로딩·해제 | 방 전환 시 현재 방 교체; 전환 중 양쪽 데이터 일시 공존 가능 | **구현**, 실기기 자원 검증 필요 |
| Admin 분리 | 로컬 Node 서버 + 브라우저 UI | **유지** |
| MASTER / PC / MOBILE 분리 | 실제 업로드를 곧바로 `pc.glb`로 저장, 단일 `model` 경로 | **미구현** |
| 공개범위/소장품 서사/컬렉션 | 현행 엄격한 artifact schema는 기본 필드만 허용 | **후속 스키마 설계 필요** |
| VRChat 별도 Export | 전용 Exporter 아직 없음 | **계획 유지 / 미구현** |
| 타이쿤 관람객/경제 | 관람객·설정 구현, 만족도/수입/세이브 미구현 | **초기 범위를 넘어선 제품 확장** |
| PC Link 우선 | OpenXR·Windows 빌드 중심, Quest 실기기 미완료 | **현실적 개발 우선순위 변경** |

## 3. 중요한 아키텍처 리스크와 제안

### P0 — 공개 저장소에 포함된 Archive 자산과 개인정보·라이선스 점검
- 저장소가 Public이다. 추적된 `museum-archive` JSON/GLB 및 Git LFS 객체는 공개 노출 여부를 점검해야 한다. 외부 사이트에서 다운로드한 모델의 재배포 라이선스도 확인한다.
- 가족 기록이나 비공개 전시품 원본은 공개 저장소에 커밋하지 않는 구조가 안전하다. Public 전환 전후의 **Git 히스토리**도 함께 검토한다. 파일을 나중에 삭제하는 것만으로 과거 노출이 지워지지 않는다.
- 설계 권장: 예시용 합법적인 샘플만 Git, 실제 개인 Museum Archive는 별도 비공개 로컬 저장소/백업; 설정 파일에는 데이터 위치만 기록. 공개/비공개 변경은 사용자 결정으로 한다.

### P1 — 업로드 원본과 런타임 모델의 생애주기 분리
- `apps/curator-admin/server.mjs`: 64MiB 이하 GLB를 메모리로 받아 `models/pc.glb`에 그대로 저장한다. 원본 MASTER와 생성된 PC/MOBILE 자산을 구분하지 않는다.
- `apps/quest-viewer/Assets/Museum/Runtime/ArchiveData.cs`: 런타임 파일도 64MiB 상한이며 파일 전체를 메모리에 읽는다.
- `apps/curator-admin/preview-checks.mjs`: Draco/Meshopt/KTX2 계열 확장을 현재 명시적으로 거부한다.
- 결과: 압축 변환 명령만 추가하면 웹 미리보기 및 로더 호환성에서 실패할 수 있다. **입력 업로드 제한과 전시용 런타임 예산은 별도로 관리**해야 한다. 무작정 64MiB를 모두 높이지 않는다.

### P1 — Archive 스키마 진화와 호환성
- `artifact.schema.json`은 `additionalProperties:false`이며 `model` 단일 필드와 1.0 스키마를 사용한다.
- 권장: 기존 v1 자료를 읽을 수 있도록 유지한 상태에서 `models.master / pc / mobile`, 생성 상태·압축 타입·원본/파생 해시 등 필요한 최소 메타데이터를 스키마 v2/마이그레이션 또는 호환 확장으로 설계한다.
- 우선 서면 계약을 확정하고 Node Admin·검증 CLI·Unity Loader가 같은 fixture를 통과하게 한다. 모델 프로파일 구조를 곧바로 기존 JSON에 무검증으로 추가하지 않는다.

### P1 — 소장품 원본, 전시 배치, 타이쿤 저장 정보의 경계
- 지금 `manifest.json`에는 방/셀/테마/휴지통이 들어 있고, `visitors.json` 설정도 같은 Archive 루트에 있다.
- 이는 로컬 MVP로 작동하지만, 미래의 '원본 소장품 보존'과 '게임 진행 저장·불러오기/리셋'을 혼동하기 쉽다.
- 권장 논리적 분리: **Artifact Archive(소장품/원본/파생 모델)**, **Exhibition Layout(방/배치/테마)**, **Tycoon Save(관람객 취향/수입/시설 해금 등 진행 상황)**. 첫 단계에 폴더를 전면 이동하지 말고 현재 JSON의 호환성을 유지하는 읽기/쓰기 경계를 정의한다.

### P2 — 변경 범위가 커지는 모듈의 경계
- `server.mjs`가 HTTP, 파일 검증, 모델 업로드, 백업, 방/테마, 관람객 설정을 함께 처리한다.
- `GalleryDisplay.cs`가 방 전환·모델 슬롯·장면 환경·네비게이션 및 정리를 묶고 있다. `MuseumVisitors.cs`는 그 결과물에 결합돼 있다.
- 지금 당장 프레임워크/마이크로서비스로 갈 이유는 없다. 기능을 추가하기 전 **Asset Import Service**, **Archive Repository/Validator**, **Exhibition Layout Service**, **Tycoon Runtime/Save** 정도의 책임/인터페이스를 정하고 변경하는 코드부터 추출한다.

### P2 — 확장 전 검증 및 문서 일치
- Windows PC·자동시험의 통과와 Quest 실기기 검증을 계속 구분한다.
- 실제 다운로드 모델(작은/큰 메시, 큰 텍스처, 압축 확장 포함) fixture로 Node→Admin Preview→Unity glTFast 호환성 매트릭스를 만든다.
- VR 리소스 예산은 파일 크기뿐 아니라 삼각형 수, 텍스처 해상도, 드로콜, 피크 메모리, 방 전환 잔류량을 측정한 뒤 결정한다.
- `README.md`, `docs/ARCHIVE.md`, `docs/CURATOR.md`, `docs/VERSIONING.md`의 시점에 따른 설명 차이를 정리한다. 기존 태그/브랜치는 건드리지 않는다.

## 4. 제안하는 목표 구조 (논리적, 즉시 폴더 이동 아님)

```text
[ Curator Admin ]
      │  등록/분석/미리보기/재처리/승인
      ▼
[ Archive API / Repository ] ───── [ Exhibition Layout ]
      │                                   │ 방/셀/테마
      ├─ Artifact Metadata (versioned)    │
      ├─ MASTER 원본 보존                  │
      └─ [ Asset Optimizer Worker/CLI ]    │
                ├─ PC GLB                 │
                └─ MOBILE GLB             │
                  │                       │
                  └────────┬──────────────┘
                           ▼
                  [ Unity Museum Viewer ]
                  데이터 로딩 / 룸 / 상호작용
                           │
                  [ Tycoon Runtime + Save ]
                  방문객 / 만족도 / 운영/경제
   (추후) [ VRChat Exporter ] ← 공개 허용된 Artifact만
```

**고정 원칙:** MASTER는 절대 덮어쓰지 않는다. 업로드와 변환은 스테이징/검증 후 원자적으로 게시하고 실패 시 정상 버전 유지. Unity는 자산 가공 작업을 하지 않는다. PC와 Quest는 호환성이 확인된 프로파일만 선택한다. Tycoon Save 삭제/초기화가 원본 Archive에 영향을 주지 않아야 한다.

## 5. 우선순위와 단계별 Gate

1. **ARCH-001/002/003 (설계 Gate):** 현재 책임 지도, 데이터/세이브 경계, v1 호환 스키마/마이그레이션 명세를 확정한다.
2. **ASSET-001/002 (호환성 Gate):** 대표 GLB 분석 + Web/Unity 지원 확장 테스트를 만든다. 지원되지 않는 압축은 자동 사용하지 않는다.
3. **ASSET-003/004/005 (변환 Gate):** 독립 CLI로 원본 보존 → PC/MOBILE 최적화 → 검증 → 이전 정상본 보존을 증명한다.
4. **ASSET-006/007 (통합 Gate):** 기존 관리자 업로드·상태/미리보기와 Archive Reader 연동. 실패·중단·재시도·원본 손상 0을 확인한다.
5. **ARCH-004/005 및 ASSET-008 (안정화 Gate):** 기존 관리자·관람·관람객 회귀 검사와 실제 PC/Quest 성능 프로파일링. 중복 검증 로직과 큰 모듈을 점진적으로 분리한다.
6. **P2 후속:** 추가 입력 FBX/OBJ, Tycoon Save 구현, 공개 정책 및 VRChat Export.

GitHub 공개 자산 점검 **SEC-001**은 다른 작업과 무관하게 즉시 진행한다.

## 6. 소스 근거

- 관리/저장: [server.mjs](../apps/curator-admin/server.mjs), [preview-checks.mjs](../apps/curator-admin/preview-checks.mjs), [preview.js](../apps/curator-admin/preview.js)
- 아카이브 계약: [artifact schema](../museum-archive/schemas/artifact.schema.json), [manifest schema](../museum-archive/schemas/manifest.schema.json), [ARCHIVE.md](ARCHIVE.md)
- Unity: [ArchiveData.cs](../apps/quest-viewer/Assets/Museum/Runtime/ArchiveData.cs), [ArtifactDisplay.cs](../apps/quest-viewer/Assets/Museum/Runtime/ArtifactDisplay.cs), [GalleryDisplay.cs](../apps/quest-viewer/Assets/Museum/Runtime/GalleryDisplay.cs), [MuseumVisitors.cs](../apps/quest-viewer/Assets/Museum/Runtime/MuseumVisitors.cs)
- 현황/분기: [PROJECT_STATUS.md](../PROJECT_STATUS.md), [TASKS.md](../TASKS.md), [VERSIONING.md](VERSIONING.md)

관련 작업 ID는 TASKS.md의 '아키텍처/자산 파이프라인' 묶음에 추가한다. 이 문서는 개선 제안이지 구현 완료 선언이나 새 ADR의 승인 기록이 아니다.
