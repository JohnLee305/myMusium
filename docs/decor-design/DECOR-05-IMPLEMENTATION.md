# DECOR-05 시험 씬·카메라·화면 인계

## 범위와 기반

DECOR-03 PR51, DECOR-04 PR52의 필수 CI와 병합을 확인한 668fe67에서 시작했다. Issue53을 선점한 별도 작업이다. 기존 관람 씬·관리웹·개인 Archive를 변경하지 않고 가상 자료만 표시한다. 실제 배치·명령·저장 연결은 DECOR-06/07이다.

## 실행과 재현

PowerShell 7에서 작업폴더의 `tools/run-decor-prototype.ps1`을 사용한다.

```powershell
./tools/run-decor-prototype.ps1 -Task Build
./tools/run-decor-prototype.ps1 -Task Launch
./tools/run-decor-prototype.ps1 -Task Verify
```

Build는 별도 DecorPrototype 씬만 PC 실행 파일로 만든다. 기존 EditorBuildSettings와 관람 씬 목록은 바꾸지 않는다. Launch는 가상 시험 화면이며 museum-archive 경로를 받거나 파일을 저장하지 않는다. Verify는 별도 Player에서 실제 Input System 장치 이벤트를 주입하고 화면/검사 결과를 logs/decor-ui-*에 남긴다. 검사가 120초 안에 끝나지 않으면 해당 시험 프로세스만 종료한다.

## 화면과 입력

- UI Toolkit UXML/USS와 PanelSettings/Theme 에셋을 사용한다. 기존 Jua 한국어 폰트에서 동적 FontAsset을 생성한다.
- 상단은 방 전체·선택 맞춤·벽 보기·눈금·정보·도움말, 하단은 검색·종류 필터·카탈로그·보관함이다.
- 한국어 이름은 두 줄과 전체 이름 툴팁을 제공한다. 상세판에는 종류·실제 치수·설치 상태·색상 정보·받침대 선택을 표시한다.
- 소장품·공간/마감 탭은 비활성 및 후속 단계 사유를 표시한다. 카탈로그 카드는 정보 조회다. 아직 놓거나 색상을 적용하는 기능으로 보이게 하지 않는다.
- 좁은 창은 상세판을 접고 정보 버튼/선택 때 연다. 짧은 창은 카드 썸네일을 접어 카메라와 상태줄 자리를 확보한다. 상세판과 카탈로그는 스크롤된다.
- 카메라: WASD/방향키 평행 이동, 가운데 드래그 이동, 오른쪽 드래그 회전, 커서 근처 휠 확대, F 선택/방 맞춤, G 눈금이다. Q/E 물건 회전은 이 단계에 바인딩하지 않는다.
- TextField 내부 포커스가 있으면 방 단축키를 차단한다. UI 패널에서 시작한 마우스 제스처는 방으로 넘어가도 카메라 조작으로 바뀌지 않는다. UI 위 휠은 카메라 확대에 전달하지 않는다.

색·주요 UI 크기·카메라 감도와 범위는 EditorPresentationSettings에서 관리한다. 저장 DTO와 섞지 않는다. USS는 구조·상태별 스타일을 담당한다.

## 데이터와 다음 연결점

`Assets/Museum/UI/Decor/prototype-fixture.json`은 공통 계약의 가상 catalog/world다. 시작 시 strict JSON과 배치 검사를 통과해야 표시한다. 20×16m 방, 전시대/꽃병/큰 가구와 보관된 전시대+꽃병을 포함한다. 추가 카탈로그 카드는 검색·한국어 줄바꿈·수평 스크롤을 검토할 가상 정의다.

보관함 수량/소품 포함 표시는 DecorInventoryQuery의 실제 결과를 사용한다. 표시를 위한 별도 수량을 저장하지 않는다. 도형은 원본 치수에 맞춘 시제품이며 최종 미술 에셋이 아니다.

DECOR-06 연결점:

```text
DecorPanel.SelectionChanged(ItemInstance) // 방/보관함의 개체 선택
DecorPanel.CatalogSelected(ItemDefinition) // 새 배치 도구 진입 지점
DecorPanel.SelectInstance(id), SelectAt(screenPoint)
DecorPanel.IsOverUI(screenPoint), TextEditing
DecorPanel.SetStatus(message), ShowDefinition(definition, instance)
DecorCamera.Frame(bounds), Pivot, Distance, Yaw, Pitch
```

이 단계에서는 모델을 수정하지 않는다. DECOR-06은 세션 초안에서 읽는 뷰와 프리뷰를 분리하고, 성공한 클릭 때만 명령을 제출해야 한다. 실제 편집 세션은 DecorPlacementValidator.BeginEdit를 사용한다. 드래그 매 프레임에 명령/Undo/저장을 만들지 않는다. 현재 카탈로그 이벤트와 실제 보관함 개체 이벤트를 혼동해 개체를 복제하지 않는다.

## 검증 기록

정확한 통과/실패·로그 경로는 docs/handoffs/DECOR-05.json에 기록한다. 처음 컴파일에서 TextAsset/MouseButton 형식 이름 충돌을 수정했다. 첫 Player에서 UI 패널 설정 참조가 비어 있던 것을 발견해 씬 저장 전에 다시 연결·검사했다. 가장 작은 창/150%에서 상태줄이 밀리는 문제와 숨겨진 시험 창의 입력 비활성도 실제 실행으로 발견했다. 이력은 실패 로그와 함께 보존한다.

실행 검사는 가상 입력 장치를 사용하는 실제 Windows Player 검사다. UI 배율 125/150% 검사와 Windows 자체 배율·모니터 간 이동 검사를 구분한다. 실제 Quest, 사람의 장시간 사용성 검토, 배치/저장/경제/실제 소장품 연결은 이 단계의 완료로 주장하지 않는다.

최종 Windows 빌드와 실제 표시 창의 Player63검사 PASS. 원격 CI는03/04 통합 때 모두 통과했으며05 자체는 아직 로컬 검토 단계다. 숨긴 첫 창의 검은 캡처는 표시 창에서 재검증했고, 실제 마우스와의 경합을 피하도록 시험 입력 장치를 명시했다. 초기 영역 검사 외에 카드 설명이 스크롤바 안쪽에 들어오는 경계를 추가해 최종6조합을 통과시켰다. 1280선택물·초기화면 및1024/150% 캡처를 직접 확인했다.

UI 구현 참고: [Unity ScrollView 공식 설명](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/uielements/scrollview). 실제 호환 여부와 동작은 설치된6000.3.23f1 Player로 확인했다.
