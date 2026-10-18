# DECOR-05 · 시험 씬·편집 카메라·화면 골격

상태: 미착수. 이 카드는 작업 선점이나 실행 승인을 대신하지 않는다. 실제 저장소 AGENTS 및 담당 Gate를 먼저 따른다.

## 선행과 읽기

- 선행: DECOR-01 검토 완료
- 읽기: 02-화면과조작 전체, 01-제품결정
- 공통 규칙: ../05-인계와검토.md. 관련 카드·계약·선행 인계만 우선 읽고 과거 전체 로그를 다시 읽지 않는다.

## 수정 예정 영역

- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorCamera.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorInputRouter.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/UI/DecorPanel.cs`
- `apps/quest-viewer/Assets/Museum/UI/Decor/DecorPanel.uxml`
- `apps/quest-viewer/Assets/Museum/UI/Decor/DecorPanel.uss`
- `apps/quest-viewer/Assets/Museum/Scenes/DecorPrototype.unity`

새 파일 이름은 기준안이다. 충돌하는 기존 파일이 있으면 내용을 보존하고 인계에 알린다. 공유 DTO를 임의로 변경하지 않는다.

## 구현 또는 설계할 내용

1. 기존 관람 씬과 분리된 시험 씬을 만들고 시험용 방/가구를 표시한다.
2. UI Toolkit 패널·카탈로그 카드·선택 정보판을 만들며 데이터는 fixture에서 읽는다.
3. 카메라 이동·회전·확대·F 맞춤·벽 표시와 입력 포커스 분리를 구현한다.
4. 지원 전 탭은 비활성 이유를 표시하고 실제 구현 없는 버튼으로 완료처럼 보이게 하지 않는다.
5. 크기·색·카메라 수치를 EditorPresentationSettings 한 곳에 모은다.

## 합격 기준

- 1280×800,1920×1080,1024×720 및 배율 환경에서 주요 버튼이 겹치지 않음.
- 검색 입력 중 WASD/QE가 방을 조작하지 않고 UI 위 휠은 목록을 스크롤.
- 물건 위 포인터와 UI 위 포인터가 중복 동작하지 않음.
- 한국어 카드 이름 두 줄·상세 정보 표시 확인.
- 관람 기존 씬을 열 때 편집 입력이 활성화되지 않음.

검사 도구 이름은 예정이며 현재 존재/통과를 주장하지 않는다. 구현 담당자가 실행 가능한 검사와 재현 절차를 함께 제공한다. 순수 로직 검사는 Unity 전체 빌드 없이 실행 가능하게 하고, UI/실행 검사는 실제 Player에서 확인한다.

## 제외 범위

저장·명령·실제 소장품 로딩·경제. 기존 OnGUI 전면 교체.

## 인계와 다음 행동

저장소 규약의 docs/handoffs/DECOR-05.json에 변경 파일·실제 검사·실패·미검증·계약 변경 여부를 기록한다. DECOR-06에 UI 이벤트와 선택/카메라 인터페이스 인계.

## PC·VR 개정에 따른 범위 구분

이 카드는 기존 PC 시험판 구현/검증 범위를 보존한다. 사용자 PC·VR 동등지원 결정에 따른 공통입력·VR UI와 양쪽 검증은13~17에서 추가한다. 이 카드의 PC결과를 VR완료로 바꾸지 않는다. [공통 조작 계약](../06-PC-VR-공통조작.md) 참조.
