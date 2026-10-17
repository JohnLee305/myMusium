# DECOR-02 · 편집 명령과 보관·Undo

상태: 미착수. 이 카드는 작업 선점이나 실행 승인을 대신하지 않는다. 실제 저장소 AGENTS 및 담당 Gate를 먼저 따른다.

## 선행과 읽기

- 선행: DECOR-01 검토 완료
- 읽기: 03-공통계약 4절, 01-제품결정의 회수/카탈로그 규칙
- 공통 규칙: ../05-인계와검토.md. 관련 카드·계약·선행 인계만 우선 읽고 과거 전체 로그를 다시 읽지 않는다.

## 수정 예정 영역

- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain/DecorCommands.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain/DecorEditSession.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain/DecorInventoryQuery.cs`
- `tools/test-decor-commands.ps1`

새 파일 이름은 기준안이다. 충돌하는 기존 파일이 있으면 내용을 보존하고 인계에 알린다. 공유 DTO를 임의로 변경하지 않는다.

## 구현 또는 설계할 내용

1. CreateAndPlace/SetLocation/SetVariant/Store/PlaceStored와 명령 결과를 구현한다.
2. 검사기를 주입받는다. 04 이전에는 01 참조 검사기를 사용하되 검증 전체 완료로 표시하지 않는다.
3. 원본 snapshot을 직접 수정하지 않는 초안을 만들고, 되돌리기/다시 실행/전체 취소를 구현한다.
4. 보관함 수량을 부모 루트 상태에서 도출한다. storage 부모의 자식은 부모 참조를 유지한다.
5. 명령 ID 재전달·새 ID 생성·Redo 폐기를 계약대로 처리한다.

## 합격 기준

- 전시대 생성+꽃병 부착+부모 회수 후 두 물건은 보관 상태이며 ID·부착 유지.
- 회수한 기존 물건 재배치가 소유 총수를 늘리지 않음.
- 20회 이동 Undo/Redo 후 정확한 좌표와 색상 복원.
- 거절/프리뷰 취소/중복 요청이 초안과 수량을 바꾸지 않음.
- Undo 뒤 새 명령은 Redo 비움, 동일 commandId 다른 payload 거절.

검사 도구 이름은 예정이며 현재 존재/통과를 주장하지 않는다. 구현 담당자가 실행 가능한 검사와 재현 절차를 함께 제공한다. 순수 로직 검사는 Unity 전체 빌드 없이 실행 가능하게 하고, UI/실행 검사는 실제 Player에서 확인한다.

## 제외 범위

JSON 저장, 가격·구매·판매, 사용자 원본 수정.

## 인계와 다음 행동

저장소 규약의 docs/handoffs/DECOR-02.json에 변경 파일·실제 검사·실패·미검증·계약 변경 여부를 기록한다. 명령 API와 선택물 처리 예시를 DECOR-06에 인계.
