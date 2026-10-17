# DECOR-03 · 단일 작성자 저장과 복구

상태: 미착수. 이 카드는 작업 선점이나 실행 승인을 대신하지 않는다. 실제 저장소 AGENTS 및 담당 Gate를 먼저 따른다.

## 선행과 읽기

- 선행: DECOR-01 검토 완료
- 읽기: 03-공통계약 5~6절, 상위 아키텍처 11장
- 공통 규칙: ../05-인계와검토.md. 관련 카드·계약·선행 인계만 우선 읽고 과거 전체 로그를 다시 읽지 않는다.

## 수정 예정 영역

- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Storage/DecorWorldStore.cs`
- `apps/quest-viewer/Assets/Museum/Runtime/Decor/Storage/DecorFileReplace.cs`
- `tools/test-decor-store.ps1`

새 파일 이름은 기준안이다. 충돌하는 기존 파일이 있으면 내용을 보존하고 인계에 알린다. 공유 DTO를 임의로 변경하지 않는다.

## 구현 또는 설계할 내용

1. world 단독 읽기/쓰기와 schemaVersion 거절을 구현한다. catalog는 별도 불변 입력으로 받는다.
2. OS 배타 핸들, expectedRevision, commandBatchId, 임시파일 재검사, 정상 backup, 원자적 교체를 구현한다.
3. 파일 I/O 실패 지점을 주입 가능하게 하되 실사용 경로에는 테스트 중단을 기본 비활성화한다.
4. 최초 생성·기존 교체·응답 유실 재시도·손상 발견 결과를 명확히 구분한다.
5. 개인자료 밖 임시 fixture 디렉터리에서만 저장 시험한다.

## 합격 기준

- 두 프로세스 중 writer 하나만 성공, 종료 후 다음 프로세스가 잠금 획득.
- 저장 전/임시 쓰기/교체 전/교체 후 강제 종료 결과가 이전 또는 새 정상 snapshot.
- 같은 batch 재시도는 alreadySaved, revision 한 번 증가.
- 오래된 expectedRevision은 기존 파일 유지하며 conflict.
- 파일 손상/디스크 쓰기 실패를 빈 월드로 덮어쓰지 않음.
- parent 좌표와 variantId 재실행 보존.

검사 도구 이름은 예정이며 현재 존재/통과를 주장하지 않는다. 구현 담당자가 실행 가능한 검사와 재현 절차를 함께 제공한다. 순수 로직 검사는 Unity 전체 빌드 없이 실행 가능하게 하고, UI/실행 검사는 실제 Player에서 확인한다.

## 제외 범위

초안 자동저장, 실제 Archive 이전, 실행판 배포. 전체 geometry gate는 04 연결 이후 재검증.

## 인계와 다음 행동

저장소 규약의 docs/handoffs/DECOR-03.json에 변경 파일·실제 검사·실패·미검증·계약 변경 여부를 기록한다. DECOR-07이 CommitResult를 정확히 표시하도록 실패 상태와 재현 로그 인계.
