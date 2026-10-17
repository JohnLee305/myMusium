# DECOR-01 · 공통 DTO와 참조 규칙

상태: 미착수. 이 카드는 작업 선점이나 실행 승인을 대신하지 않는다. 실제 저장소 AGENTS 및 담당 Gate를 먼저 따른다.

## 선행과 읽기

- 선행: 없음
- 읽기: 03-공통계약 1~3절, contracts/edit-slice.schema.json, fixtures 전체의 기대값
- 공통 규칙: ../05-인계와검토.md. 관련 카드·계약·선행 인계만 우선 읽고 과거 전체 로그를 다시 읽지 않는다.

## 수정 예정 영역

- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain/DecorData.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain/DecorReferenceValidator.cs`
- `contracts/decor/edit-slice.schema.json`
- `tests/fixtures/decor/*`
- `tools/test-decor-contract.ps1`

새 파일 이름은 기준안이다. 충돌하는 기존 파일이 있으면 내용을 보존하고 인계에 알린다. 공유 DTO를 임의로 변경하지 않는다.

## 구현 또는 설계할 내용

1. 스키마의 필드 이름과 enum을 보존한 순수 C# DTO를 만든다. Vector3 대신 Unity에 의존하지 않는 숫자 벡터를 사용한다.
2. 형식·참조 검사 순서 1~5를 구현한다. 순환 부모를 먼저 탐지하여 재귀 무한루프를 막는다.
3. ValidationIssue(code, primaryId, relatedIds, roomId?, position?) 계약을 구현하고 결정적 순서로 반환한다.
4. 정상 및 관계 오류 fixture를 테스트 자산으로 옮긴다. 기존 ArchiveData.cs는 변경하지 않는다.

## 합격 기준

- 정상 2종은 참조 검사 통과.
- 미존재 정의/변형/부모, 중복 ID, 부모 순환, 허용되지 않은 바닥 소품은 해당 코드를 반환.
- 스키마 형식 오류 2종은 의미 검사 전에 거절.
- 정밀 좌표·부모 location을 읽고 써도 값과 ID가 유지.

검사 도구 이름은 예정이며 현재 존재/통과를 주장하지 않는다. 구현 담당자가 실행 가능한 검사와 재현 절차를 함께 제공한다. 순수 로직 검사는 Unity 전체 빌드 없이 실행 가능하게 하고, UI/실행 검사는 실제 Player에서 확인한다.

## 제외 범위

충돌·동선·저장·UI 구현. geometry 오류 fixture는 04의 기대값으로 보존하고 아직 통과했다고 쓰지 않는다.

## 인계와 다음 행동

저장소 규약의 docs/handoffs/DECOR-01.json에 변경 파일·실제 검사·실패·미검증·계약 변경 여부를 기록한다. DECOR-02/03/04/05가 같은 DTO를 사용하게 인터페이스를 인계.
