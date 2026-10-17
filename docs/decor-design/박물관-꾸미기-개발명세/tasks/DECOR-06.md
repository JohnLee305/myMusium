# DECOR-06 · 놓기·상판 소품·회수의 UI 연결

상태: 미착수. 이 카드는 작업 선점이나 실행 승인을 대신하지 않는다. 실제 저장소 AGENTS 및 담당 Gate를 먼저 따른다.

## 선행과 읽기

- 선행: DECOR-02,04,05 검토 완료
- 읽기: 02 전체, 03의 명령/표면 규칙
- 공통 규칙: ../05-인계와검토.md. 관련 카드·계약·선행 인계만 우선 읽고 과거 전체 로그를 다시 읽지 않는다.

## 수정 예정 영역

- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorSelection.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorPlacementPreview.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorEditorController.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorPanel.cs`
- `tools/test-decor-interaction.ps1`

새 파일 이름은 기준안이다. 충돌하는 기존 파일이 있으면 내용을 보존하고 인계에 알린다. 공유 DTO를 임의로 변경하지 않는다.

## 구현 또는 설계할 내용

1. 카탈로그 선택→투명 미리보기→성공 클릭 시 명령 제출의 흐름을 연결한다.
2. 개별 소품 선택과 부모 선택 버튼, Move, Q/E 직각 회전, Alt snap 해제, Shift 연속 배치를 구현한다.
3. 프리뷰 취소는 초안에 명령을 남기지 않는다. 회수·보관 재설치·색상·Undo UI를 연결한다.
4. 오류 코드별 짧은 한글 이유와 문제 위치를 표시한다.
5. 부모를 움직이는 프리뷰에서 자손도 같이 움직이고 전체 외곽을 검사한다.

## 합격 기준

- 02의 체감 장면 1~4를 실제 마우스로 수행하고 결과 화면 기록.
- 전시대 위 꽃병만 선택/이동 가능, 부모 선택 후 함께 이동.
- 잘못된 위치 클릭은 물건·수량·Undo를 바꾸지 않음.
- 회수 후 보관함에서 같은 개체와 소품을 복원.
- 50회 프리뷰 생성/취소 뒤 임시 객체와 재질 수가 계속 증가하지 않음.

검사 도구 이름은 예정이며 현재 존재/통과를 주장하지 않는다. 구현 담당자가 실행 가능한 검사와 재현 절차를 함께 제공한다. 순수 로직 검사는 Unity 전체 빌드 없이 실행 가능하게 하고, UI/실행 검사는 실제 Player에서 확인한다.

## 제외 범위

실제 관람객, 영구 저장, 자유 yaw, 벽·천장.

## 인계와 다음 행동

저장소 규약의 docs/handoffs/DECOR-06.json에 변경 파일·실제 검사·실패·미검증·계약 변경 여부를 기록한다. DECOR-07에 다섯 조작 장면과 known issue 인계.
