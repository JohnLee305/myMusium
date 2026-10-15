# Living Museum — 설명 기반 관람객 반응 시스템 (설계 제안 v0.1)

> 문서 상태: **설계 이력 / PC 첫 버전 구현·검증 완료, Quest 미검증** (2026-10-16).
> 제품 방향: 개인 컬렉션과 물건에 담긴 이야기를 보존하고 **다른 누군가도 감상하는 살아 있는 박물관**. 관람객은 컬렉션의 감상 경험을 풍부하게 하는 존재이며, 수입·평판·해금은 **선택적 후속 확장**이다.
> 관련: [TASKS.md](../TASKS.md) `REACT-001~009`, [아키텍처 검토](ARCHITECTURE_REVIEW_2026-10-15.md), [Work 인계 방법](WORK_HANDOFF.md).
> 이 문서의 예시 스키마·파일명은 **제안안**이며 아래 예시 그대로의 형식은 현재 계약이 아니다. 실제 지원 형식은 ADR-0006을 따른다. Work는 실제 코드와 대조 후 구현/ADR을 확정한다.

## 1. 목표와 비목표

### 목표
1. 전시품 `title`, `description` 및 나중에 승인된 추가 스토리 정보를 해석해 감상 포인트를 구조화한다.
2. **전시품에 근거하는** 짧은 감탄/질문/서사형 코멘트 후보를 여러 개 사전 생성하고 관리자가 미리보기·수정·승인한다.
3. 외형과 독립적인 관람객의 관심 성향(일반·디테일·수집가·서사·팬덤 등)에 따라 후보를 선택하고 반복을 억제한다.
4. 관람객이 특정 전시품을 실제로 바라보는 순간에 해당 작품에 맞는 생각/말풍선이 나타난다. PC 환경에서 먼저 검증하고 Quest 실기기는 별도 확인한다.
5. 정보 부족/후보 없음/파일 손상/AI 비활성 상태에서도 기존 관람·이동·배치·저장 기능이 작동한다.

### 이번 범위 밖
- 실시간 생성형 AI 호출, 실시간 음성·대화, 관람객 사이 대화, 수입·입장료·해금, 온라인 멀티플레이, 외부 모델 학습.
- 설명에 없는 희귀성·작가·제작 과정·세계관 설정·감정·역사를 사실처럼 만드는 것.
- 초기 구현에서 `artifact.json`의 기존 필수 필드/스키마를 파괴적으로 변경하는 것.

## 2. 실제 코드의 연결 지점

- `apps/quest-viewer/Assets/Museum/Runtime/MuseumGuest.cs`의 `Walking → Viewing` 전환: 현재 `wait=4.5f; Viewed++; museum.RecordView(); thought.text="어떤 이야기가 담겨 있을까요?";` 동작을 가짐. **이 위치에서 `ArtifactDisplay.Data.id`와 방문객 관심 프로필을 전달해 반응을 선택**하는 작은 어댑터를 두는 것이 1차 후보.
- `apps/quest-viewer/Assets/Museum/Runtime/MuseumVisitors.cs`: NPC 생성/이동과 외형 카탈로그, 개수/속도 설정을 운영한다. 이 클래스에 LLM 호출·반응 문구 데이터 편집·장기 저장까지 넣지 않는다.
- `apps/curator-admin/server.mjs`, `app.js`: 기존 등록/수정/백업 API는 유지한다. 감상 포인트/반응 후보 작업은 별도 서비스·UI 모듈로 점진 추가한다.
- `museum-archive/schemas/artifact.schema.json`: `additionalProperties:false`, 기본 필드 `schemaVersion, id, title, description, model, scale`만 허용. **무계획한 신규 필드 추가 금지**. v1 호환 사이드카가 적합한 초기 후보.
- `VisitorAppearance` 카탈로그는 외형/애니메이션 속성이다. 관람객 **관심 성향은 appearance ID·성별·피부색 등 외형과 독립적**으로 부여한다.

## 3. 제안 데이터 계약: 선택적 사이드카

예시 위치: `museum-archive/artifacts/{id}/reactions.json`.

처음에는 `artifact.json`과 `manifest.json`을 변경하지 않고 선택적 파일로 시작한다. `ARCH-002/003` 데이터 생애주기·스키마 결정과 함께 최종 계약을 확정한다. 사이드카가 없는 예전 전시품은 기존 상태 그대로 표시된다.

**예시 형식 (미승인 계약 / 표시용 샘플 / 가상의 설명):**

```json
{
  "schemaVersion": "1.0",
  "artifactId": "example-miniature",
  "source": {
    "artifactDescriptionSha256": "sha256-of-reviewed-input",
    "generator": "manual-or-local-template-v1"
  },
  "semantic": {
    "categories": ["miniature", "fantasy"],
    "themes": ["heroic", "craftsmanship"],
    "viewingPoints": [
      {
        "id": "detail-01",
        "claim": "설명에 직접 도색했다고 기록되어 있음",
        "evidence": "직접 도색한 미니어처"
      }
    ]
  },
  "reactions": [
    {
      "id": "reaction-01",
      "persona": "detail",
      "tone": "admiration",
      "tier": "short",
      "text": "직접 칠한 작은 디테일이 눈에 들어오네.",
      "evidencePointIds": ["detail-01"],
      "status": "approved"
    },
    {
      "id": "reaction-02",
      "persona": "casual",
      "tone": "curiosity",
      "tier": "short",
      "text": "이 작은 작품에도 이야기가 있나 봐.",
      "evidencePointIds": [],
      "status": "draft"
    }
  ]
}
```

규칙:
- `source`는 무엇을 입력으로 후보를 만들었는지 식별한다. 설명/스토리가 바뀌면 이전 승인 반응은 `stale` 또는 검토 필요로 표시한다. 기존 승인본을 묵시적으로 덮어쓰지 않는다.
- `status=approved` 후보만 런타임에서 노출한다. `draft`/거절 상태는 노출하지 않는다. `reactions.json` 자체가 없거나 잘못되면 정상적인 기본 문구로 폴백한다.
- 반응은 짧은/보통/희귀 코멘트 등으로 구분하되 실제 관람 시간(현재 예: 4.5초) 및 UI 공간에 맞춰 줄 수·노출 시간을 제한한다.
- 설명이 없는 기술적 주장은 생성하지 않는다. 단순한 미적 의견, 관심·질문은 사실 단정 없이 허용한다.
- 파일 크기·배열 상한·문장 길이·schemaVersion·ID 일치·문구 타입·금칙 문자 등의 검증 정책은 구현 시 유효/무효 fixture와 함께 확정한다.
- 소장품 스토리의 공개 범위, 로컬/클라우드 생성 정책, 외부 모델 전달 동의는 `SEC-001` 및 향후 Visibility 정책과 연결한다. **사적인 가족 기록은 무단 외부 LLM 전송 금지**.

## 4. 추천 역할 분리

```text
[Curator Admin]
   - 설명/스토리 편집
   - 반응 분석/생성 요청
   - 후보 미리보기, 수정, 승인/거절
         |
         v
[Reaction Content Pipeline]  (오프라인/등록·수정 시점)
   - semantic extraction (규칙/모델은 교체 가능)
   - provenance/grounding validation
   - persona별 후보 생성
   - save revisions/approval workflow
         |
         v
[Artifact Archive] artifact.json + (optional) reactions.json
         |
         v
[Unity Reaction Catalog Loader]
         |
         v
[Visitor Persona / Reaction Selector] --(at Viewing state)--> [Bubble/UI]
   - 외형과 독립적인 관심 성향
   - 전시품 ID + persona + tier + recent history
   - 후보 선택/중복 방지/빈 후보 폴백
```

LLM은 **문구 생성용 선택적 어댑터**다. MVP는 수동 입력 + 규칙/템플릿 생성만으로 오프라인 검증할 수 있어야 한다. Unity 런타임이 네트워크·API 키·AI 서비스를 필수로 요구하지 않는다. 비밀키는 Archive, Git, 클라이언트 파일에 기록하지 않는다.

## 5. 제품 경험 규칙 (컬렉션 우선)

- 작품을 모르는 관람객도 전시품 제목/소개를 바탕으로 자연스러운 반응을 보일 수 있다.
- 디테일에 관심 있는 관람객, 서사에 끌리는 관람객 등 서로 다른 **관심 성향**을 쓰되, 101종 기존 외형 프리팹과는 독립이다.
- 방문객의 같은 작품/같은 짧은 세션 반복 반응은 cooldown·최근 이력으로 줄인다.
- 지나치게 많은 말풍선은 감상과 VR 가독성을 해친다. 밀도/거리/길이 제한, 기본 비활성화 옵션 및 접근성 고려.
- 현재 소장품은 원본 스토리가 가장 중요한 데이터다. 게임 점수·경제보다 컬렉션의 의미와 감상을 우선한다.
- 인기 전시품 등 수치 요약(`TYC-004`)은 **감상 반응 시스템의 선택적 후속 소비자**일 뿐, 감상 기능의 선행 필수조건이 아니다.

## 6. 단계별 수용 기준 (Work 개발 인계)

1. **REACT-001 계약:** `ARCH-002/003` 고려해 버전드 반응 스키마, 승인 상태, 설명 변경 시 무효화, v1 호환성/유효·무효 fixture를 정한다. 기존 `artifact.json`은 그대로 읽는다.
2. **REACT-002 해석:** 설명에서 주제/감상 포인트 추출. 근거(evidence) 없는 희귀성·제작자·서사를 사실로 생성하지 않음. 설명 부족 시 안전한 일반형 후보.
3. **REACT-003 생성:** 수동/오프라인 템플릿을 우선, 선택적 LLM 어댑터는 동의·보안·비용·검토 후. 동일 입력/설정 재실행 가능, 기존 승인문 비파괴.
4. **REACT-004 Admin 검토:** 생성 후보/근거 보기, 직접 수정, 승인/거절/재생성, 백업·원자적 저장. 이전 업로드·전시·방배치 기능 회귀 통과.
5. **REACT-005 Persona:** 방문객 성향을 외형과 독립된 세션/프로필로 구성. 카테고리/주제 선호는 설정 가능하되 고정된 성별/외형과 연결하지 않음.
6. **REACT-006 Unity 선택:** `MuseumGuest`가 실제 대상 `ArtifactDisplay.Data.id`를 `Viewing` 진입 때 전달, 승인 후보 중 persona/tier 선택. 후보 없거나 손상 시 기존 기본 문구. 반복/희귀도 제어, 로딩/방변경 시 상태 정리.
7. **REACT-007 UI/경험:** 말풍선 가독성·길이·빈도·단계별 반응 품질과 개별 전시물의 고유성 확인. PC 우선, Quest는 실기기 검사로 상태 구분.
8. **REACT-008 테스트:** 서로 설명이 다른 3개 전시품, 서로 다른 3+ 성향, 중복 관람, 설명 변경 후 stale, 미승인/손상/없는 파일, 삭제·복구·방 전환, 기존 관람객 회귀 테스트. 완료 기록에 실제 PC/Quest 검사 구분.
9. **REACT-009 확장 검토 (후속):** 깊은 질문/대화, 음성, 멀티플레이·VRChat 공유, 사진 기반 시각 특징 감상, 분위기/재방문 등. 별도 승인 및 개인정보·성능 검토 후 진행.

### 초기 수동 검수 예시
- 설명: "직접 도색한 판타지 미니어처. 금속 갑옷과 붉은 망토 대비에 신경 썼다."
- 일반 성향: "붉은 망토가 확 눈에 들어온다."
- 디테일 성향: "갑옷과 망토 색 대비가 잘 보이네."
- 서사 성향: "직접 도색했다니 이 전시품에 특별한 애정이 있겠어."
- **금지된 사실 단정:** "이건 1998년에 한정 발매된 전설의 모델이래." (설명에 없음)

## 7. Work가 작업을 시작할 때

1. `codex/museum-tycoon`의 `AGENTS.md` → `PROJECT_STATUS.md` → `TASKS.md` → 본 문서 → 관련 코드 파일을 읽는다. 로컬 워크트리가 원격과 동일한지 확인한 뒤 미반영 변경사항을 보호한다.
2. 시작 작업의 단일 `REACT-xxx`를 `진행 중`으로 표시하고, 필요 시 관련 `ARCH`를 먼저 완료한다. **기획 문서를 구현 완료로 체크하지 않는다.**
3. 선행/미결정사항을 기술한 작은 PR 또는 커밋 단위로 작업한다. 테스트와 회귀검사 후 실제 검증 범위를 구분해 기록한다.
4. `TASKS.md`, `PROJECT_STATUS.md`, `STATUS.md`, `WORKLOG.md`를 갱신한다. 기준 브랜치 `codex/museum-baseline`은 사용자가 요청하지 않는 한 수정하지 않는다.

프로젝트의 아이디어 회의/사양 합의는 ChatGPT 대화에서 진행할 수 있고, 구현은 ChatGPT Work에 위 문서를 인계해 수행한다. Work는 이 대화 내용을 자동으로 완전히 아는 것으로 가정하지 않는다.


## 2026-10-16 구현 진행
위 예시는 당시 제안이다. 이번 PC 첫 버전의 실제 계약은 [ADR-0006](decisions/0006-approved-reactions.md), 사용법은 [REACTIONS.md](REACTIONS.md)를 따른다. 선택적 사이드카·원문지문·인용근거·4성향·승인 흐름 구현, 최종 Windows 빌드·반응 실행·화면·기존 회귀 검증 완료. categories/themes 자동 분류·심층 해석·LLM·성향 가중치 저장은 이번 MVP 범위가 아니다.
