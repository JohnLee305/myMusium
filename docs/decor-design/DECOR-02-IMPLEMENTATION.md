# DECOR-02 구현 인계

2026-10-17 · 순수 도메인 구현 · 화면/디스크 저장/실제 박물관 미연결

## 인터페이스

네임스페이스 `FamilyMuseum.Decor`. DECOR-01의 DTO와 저장 schema를 변경하지 않았다.

```text
new DecorEditSession(catalog, world)
new DecorEditSession(catalog, world, additionalValidator, historyLimit)
DecorEditSession.BeginEdit(world, catalog)
session.ApplyCommand(command) -> DecorCommandResult
session.Undo() / Redo() / Cancel() -> DecorCommandResult
session.BuildSnapshot() -> DecorWorld (깊은 복사)
session.Inventory() -> DecorInventorySummary
session.UndoCount / RedoCount / IsClosed
```

`DecorCommands`의 팩토리는 CreateAndPlace, SetLocation, SetVariant, Store, PlaceStored를 제공한다. 명령에 commandId와 대상 instanceId를 넣고, 생성 명령에는 미리 발급한 새 instanceId를 넣는다. 세션이 ID를 임의로 발급하거나 바꾸지 않는다.

`DecorCommandResult`는 accepted, changed, duplicate, historyTrimmed, resultingDraft, issues를 제공한다. 결과의 snapshot과 오류 배열은 내부 상태와 분리된다. accepted=true, changed=false는 중복 요청 또는 실제 변화가 없는 명령일 수 있다. 성공해도 디스크 저장이나 geometry 검증이 완료된 것은 아니다.

## 원본·초안과 검사

원본 world와 catalog를 진입 때 깊은 복사한다. BuildSnapshot과 모든 결과는 다시 깊은 복사하여 반환한다. 외부 코드가 반환된 위치 배열·ID를 바꾸어도 세션은 바뀌지 않는다. 원본과 history 안의 배열 참조를 공유하지 않는다.

기본 참조 검사1~5는 항상 실행한다. 선택적인 additionalValidator는 기본 검사 통과 후 호출하며, 향후 DECOR-04가 geometry를 주입한다. 호출에는 복사본을 전달하므로 검사기가 인자를 수정해도 candidate나 세션이 바뀌지 않는다. 예외 또는 null 결과는 VALIDATOR_FAILED로 거절한다.

추가 검사기는 같은 catalog/초안에 대해 결정적인 결과를 반환해야 한다. 시간이나 외부 mutable 설정에 의존하지 않는다. 세션은 단일 스레드 소유를 전제로 한다. 검사 callback에서 명령/Undo/Redo/Cancel로 재진입하면 SESSION_BUSY로 거절한다.

## 명령과 취소

- 새 물건과 이동 프리뷰는 초안 밖에 둔다. 놓기 성공 시 한 번만 ApplyCommand를 호출한다. UI의 프리뷰 취소는 ApplyCommand 없이 끝낸다.
- CreateAndPlace는 새 instanceId 하나를 만든다. 한 번 성공한 ID는 Undo로 개체가 사라져도 다른 생성 명령에 재사용할 수 없다. Redo는 같은 원래 ID를 복원한다.
- SetLocation은 이미 설치된 물건의 이동·회전·재부착이다. 보관 루트를 다시 놓으려면 PlaceStored를 사용한다.
- Store는 대상 location을 storage로 바꾼다. 자식 parent 참조는 유지한다. 자식만 선택해서 Store하면 그 자식만 보관한다.
- PlaceStored는 storage 루트만 대상으로 한다. room 또는 허용된 parent location을 받는다. 이미 놓인 물건에 호출하면 ITEM_NOT_STORED다.
- SetVariant는 보관/설치 상태 모두에서 정의에 있는 변형으로 변경할 수 있다.
- 의미 없는 변경은 accepted=true/changed=false이며 Undo/Redo와 보관 최근 순서를 바꾸지 않는다.
- Cancel은 최초 원본으로 복원하고 세션을 닫는다. Undo 한도에서 밀려난 변경도 취소한다. 이후 편집 명령은 SESSION_CLOSED다. 새 편집은 새 세션을 만든다.

revision과 lastCommandBatchId는 편집 중 증가·교체하지 않는다. 저장 트랜잭션은 DECOR-03이다. 저장 후 원본 기준 갱신/관람 모드 복귀 수명은 후속 통합에서 연결한다.

## Undo/Redo와 요청 중복

성공한 실제 변화마다 이전 snapshot과 보관함 최근 순서를 한 history 항목으로 기록한다. 기본 200개이며 범위 1~10000의 설정을 받는다. 초과하면 가장 오래된 Undo만 버리고 historyTrimmed=true를 반환한다. UI가 이 결과로 안내한다. 새 실제 변경은 Redo를 비우지만 거절·중복·no-op는 Redo를 보존한다.

commandId는 세션 수명 동안 유일하다. 같은 ID+같은 payload 재전달은 초안에 다시 적용하지 않는다. 이미 Undo한 명령의 재전달도 다시 실행하지 않으며 사용자가 원하는 복원은 Redo로 수행한다. 같은 ID+다른 payload는 COMMAND_ID_REUSED다.

유효한 commandId를 가진 거절 요청도 그 오류를 기억한다. 같은 ID 재시도는 이전 거절을 반환하고, 사용자가 수정했으면 새 commandId를 써야 한다. fingerprint는 길이 표시 문자열과 문화권 불변 숫자를 사용하며 JSON 필드 순서에 의존하지 않는다. 중복 응답의 resultingDraft는 과거 snapshot이 아니라 현재 초안의 복사본이다.

## 보관함 조회

`DecorInventoryQuery.Read(catalog, world, lastStored?)`는 모든 참조가 유효한 데이터에서 다음을 계산한다.

- ownedCount: 인스턴스 전체 개수.
- storedCount: storage 루트와 그 자손 수.
- placedCount: room 루트와 그 자손 수.
- storedGroups: 보관 루트의 묶음. definitionId/version/variant, 자식들의 정의·색상·표면·상대 위치·회전이 같아야 합친다.

각 group에는 rootIds, rootCount, memberCount, hasChildren이 있다. rootIds는 선택 가능한 실제 개체다. 묶음 수량을 world에 중복 저장하지 않는다. 장식이 붙은 전시대는 빈 전시대와 합치지 않고, 동일한 장식 구성을 가진 전시대끼리는 자식 ID가 달라도 합친다.

세션의 마지막 회수 순서를 우선하고 동률은 ID Ordinal 순이다. 최근 순서는 편집용 metadata이며 Undo/Redo와 함께 복원된다. **새 세션/재실행에서 최근 순서는 ID 순으로 되돌아간다.** 첫 schema에 영구 회수 시각이 없기 때문이다. 재실행 후까지 최근 순서를 유지하려면 후속 계약 검토가 필요하며 이번에 저장 필드를 추가하지 않았다.

같은 카드의 첫 rootId를 선택해 PlaceStored하고, 특정 개체 선택은 rootIds에서 고른다. 결과 배열과 카드 수정은 세션에 영향을 주지 않는다.

## 오류 코드

기본 SCHEMA_INVALID·참조 오류에 더해 COMMAND_INVALID, COMMAND_ID_REUSED, INSTANCE_ALREADY_EXISTS, INSTANCE_NOT_FOUND, ITEM_NOT_PLACED, ITEM_NOT_STORED, NOTHING_TO_UNDO, NOTHING_TO_REDO, SESSION_CLOSED, SESSION_BUSY, VALIDATOR_FAILED를 반환한다. 한글 메시지는 UI 계층에서 매핑한다.

## 검증과 남은 작업

`pwsh -NoProfile -File tools/test-decor-commands.ps1`는 기존3개와 신규3개의 실제 C# 소스를 함께 컴파일한다. 가상 자료만 사용하며 파일 저장·씬·서비스를 만들지 않는다. 계약 검사와 명령 검사는 기존 tools/run-ci.mjs를 통해 Ubuntu/Windows CI에 연결했다. 이번 작업의 원격 CI 결과는 아직 게시 전이므로 주장하지 않는다.

검증 시나리오는 20회 이동+색상 전체 Undo/Redo, 부모/자식 회수·재배치, 불변 원본·결과 복사, 성공/거절 요청 재전달, Undo 뒤 명령 ID/개체 ID, history 한도·전체 취소, 묶음 수량·최근 순서, 주입 검사 거절·변조·예외다. 실제 실행 결과는 작업 handoff와 로그가 정본이다.

다음 DECOR-03은 WorldStore다. BuildSnapshot을 가져와 expectedRevision/batchId로 저장하되 원본 DTO를 직접 변경하지 않는다. DECOR-04는 추가 배치 검사기를 제공하고, DECOR-06은 프리뷰와 명령 제출을 연결한다. 실제 geometry·동선·UI·Undo 조작감은 아직 검증하지 않았다.
