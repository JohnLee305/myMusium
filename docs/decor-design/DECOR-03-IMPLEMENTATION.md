# DECOR-03 저장·복구 구현 인계

2026-10-17 · 가상 월드 저장 기반 · 실제 박물관/개인자료/화면 미연결

## API와 작성자

네임스페이스 `FamilyMuseum.Decor`. 입력 catalog는 복사해 고정하고 디스크에는 world만 쓴다. 저장 schema와 DTO는 변경하지 않았다. DecorJson에는 strict JSON의 최상위 schemaVersion만 검사하는 internal helper를 추가하여 미지원 버전을 손상과 구분한다.

```text
DecorWorldStore.TryOpen(root, worldId, catalog, out store, out diagnostic)
DecorWorldStore.TryOpen(root, worldId, catalog, faultHook, out store, out diagnostic)
store.Load() -> DecorLoadResult
DecorWorldStore.ReadOnly(root, worldId, catalog) -> DecorLoadResult
store.Commit(expectedRevision, commandBatchId, snapshot) -> DecorCommitResult
store.RestoreBackup(backupId, minimumRevision, commandBatchId) -> DecorCommitResult
store.Dispose()
```

root는 호출자가 정하는 저장 루트다. 설치폴더나 개인 Archive를 하드코딩하지 않는다. 실제 기본 저장 위치 연결은 DECOR-07/08에서 결정한다. 각 store는 단일 스레드 소유이며 Commit/Restore 중 같은 인스턴스의 재진입은 STORE_BUSY다. 병렬 Dispose나 외부 파일 변조에 대한 동기화 경계로 사용하지 않는다.

## 저장 구조와 잠금

```text
<root>/<worldId>/
  writer.lock
  world.json
  backups/r<10자리 revision>-<guid>.json
  preserved/corrupt-<guid>.json
  .world-<guid>.tmp       강제 종료 때 남을 수 있음; 로딩 대상 아님
```

worldId와 backupId는 제한된 문자열만 허용한다. 경로 이탈과 링크 경로를 거절한다. writer.lock을 FileShare.None OS 핸들로 연 채 유지한다. 파일 이름이나 수정 시각을 소유권으로 판단하지 않고 잠금 파일을 삭제하지 않는다. Dispose 또는 프로세스 종료 뒤 같은 파일을 다시 열 수 있다.

다른 writer는 WORLD_IN_USE로 거절한다. 일반 디스크 열기 실패는 STORE_IO_FAILED, 권한 실패는 STORE_ACCESS_DENIED로 구분한다. Open 실패 시 얻은 핸들을 정리한다. ReadOnly는 writer가 열려 있어도 읽을 수 있고, 없는 저장 디렉터리를 만들지 않는다.

## 로드 결과

| status | 의미 | 일반 저장 |
|---|---|---|
| loaded | 지원 schema와 고정 catalog의 참조 검사 통과 | revision 조건하에 가능 |
| notFound | 정상 snapshot과 유효한 이전 backup이 없음 | expectedRevision=0으로 최초 생성 |
| recoveryAvailable | 최신 파일 손상/누락, 유효한 backup 있음 | 명시적 복구 전 거절 |
| corrupt | 최신 파일 손상, 유효한 backup 없음 | 자동 덮어쓰기 거절 |
| unsupportedVersion | 알려지지 않은 schemaVersion | 덮어쓰기/복구 거절 |
| contentMismatch | catalog pin 또는 정의 참조 불일치 | 올바른 콘텐츠를 준비하기 전 거절 |
| failed | 접근/잠금 수명/파일 읽기 실패 | 오류 해결·재읽기 필요 |

recoveryAvailable/corrupt에서 world=null이다. 조용히 backup으로 관람하거나 빈 월드를 만들어 손상본을 덮어쓰지 않는다. loaded에서도 오류 없는 geometry를 의미하지 않는다. backups에는 실제로 읽고 형식·참조 검사를 통과한 후보의 id/revision만 반환한다. 손상된 backup은 복원 후보로 광고하지 않고 원본 파일은 보존한다.

## Commit과 재시도

1. 저장 인스턴스 수명·재진입·인자 검사.
2. snapshot 형식·참조·worldId 검사. caller의 데이터는 수정하지 않는다.
3. 현재 파일의 실제 상태 재읽기. 손상/미지원/콘텐츠 불일치에서 종료.
4. 같은 마지막 batchId면 내용 지문을 먼저 비교. 동일 내용은 alreadySaved, 다른 내용은 conflict.
5. 새 요청은 현재 revision, expectedRevision, snapshot.revision이 일치해야 한다. 신규 파일은 0을 기준으로 한다.
6. revision을 1 증가하고 lastCommandBatchId를 새 batch로 설정.
7. 같은 디렉터리에 임시 파일 작성 → Flush(true) → strict JSON·참조 검사·정규화 결과 재대조.
8. 기존 정상본을 backup 임시 파일로 복사·flush하고 backup 이름으로 게시.
9. 최초 생성은 File.Move, 기존 교체는 File.Replace로 게시. delete-then-move 대안은 사용하지 않는다.
10. 최근 정상 backup 5개를 유지하고 saved를 반환.

내용 지문은 World의 revision/lastCommandBatchId를 제외하고 SHA-256으로 계산한다. 키는 codec이 정렬하고 숫자는 문화권 불변 형식이다. 배열 순서는 유지한다. 재시도에서 기존 payload를 보관하고 그대로 전달한다. batchId나 payload를 새로 만들면 같은 요청이 아니다.

result.status는 saved / alreadySaved / conflict / failed다. revision은 saved/alreadySaved에서 확정값이다. 오래된 새 batch는 WORLD_REVISION_CONFLICT, 같은 batch의 다른 내용은 BATCH_CONTENT_CONFLICT다. int.MaxValue revision은 REVISION_EXHAUSTED로 거절한다.

## 저장 성공과 실패 상태의 구분

교체 이후 후처리에서 실패하면 디스크를 재확인한다. 새 batch/내용이 확인되면 saved + POST_SAVE_WARNING으로 반환한다. 이미 저장했는데 실패했다고 표시해 중복 조작을 유도하지 않는다. 새 snapshot인지 확인할 수조차 없으면 failed + COMMIT_STATE_UNKNOWN이다. 이 경우 이전 상태가 유지됐다고 가정하지 말고 저장소를 재읽은 뒤 같은 batch를 재시도한다.

교체 전 I/O 실패는 failed이고 이전 snapshot을 보존한다. 테스트 중 강제 종료로 남은 .tmp와 backup .tmp는 로딩하지 않는다. 자동 청소는 이번 범위에 포함하지 않아 증거를 보존한다. 사용자에게 보여주는 파일 이름이나 I/O 세부 구현은 UI 판단에 필요한 경우만 사용한다.

## 명시적 복구

Load가 반환한 backupId를 사용자가 선택·확인한 후 RestoreBackup을 호출한다. source backup을 다시 검사하고 최신 schema가 미지원이거나 catalog가 다른 경우 복원하지 않는다.

복구는 선택한 내용으로 **새 revision**을 저장한다. 최소 revision은 caller의 마지막 확인값, 현재 읽을 수 있는 revision, 정상 backup 최대 revision+1 중 큰 값을 기준으로 하며, 그보다 1 크게 저장한다. 정상 backup을 매 Commit에서 유지한 저장소에서는 교체 직전 실패로 생성된 backup도 고려하여 revision 역행을 피한다. 이전 파일이 손상되어 정확한 revision을 읽을 수 없으므로 건너뛴 revision이 생길 수 있다.

minimumRevision에는 앱이 마지막으로 확인한 revision을 전달한다. 사용자가 외부에서 backup을 삭제하거나 저장 파일 집합을 임의로 섞었다면 원래 최신 revision을 이 파일들만으로 증명할 수 없다. 실제 자료의 수동 구조 복구는 별도 확인이 필요하다. 이 한계를 숨기기 위해 손상 파일에서 임의 숫자를 추출하지 않는다.

복원 전 최신 파일이 정상이라면 일반 backup을, 손상이라면 preserved/corrupt 파일을 만들어 원본을 보존한다. 복원에 사용한 backup은 즉시 재시도할 수 있게 최근5개 안에 남긴다. 정상 복원 후 같은 batch·내용 요청은 alreadySaved다. 후속의 다른 Commit이 생긴 뒤 과거 batch 재전달은 일반 충돌 규칙을 따른다.

## 검증 도구와 범위

`pwsh -NoProfile -File tools/test-decor-store.ps1`가 실제 production C#를 컴파일한다. tests/decor/DecorStoreTestHooks.cs는 시험 전용이며 Unity 프로젝트에 포함하지 않는다. faultHook은 기본 null이며 명시적으로 주입한 시험에서만 작동한다.

주입 지점은 BeforeWrite, DuringWrite, AfterTempValidated, AfterBackup, BeforeReplace, AfterReplace다. 각 지점의 IOException 시험과 별도 pwsh 프로세스를 그 위치에서 멈춘 후 강제 종료하는 시험을 수행한다. 최초 생성과 기존 교체를 각각 검사하고 실제 종료로 응답이 사라진 요청을 재시도한다. 시험은 logs/decor-store-*의 새 가상 저장만 사용한다.

실제 프로세스 종료, 파일 잠금, Flush/원자 교체 경로를 확인한 것이다. 하드웨어 전원 차단·디스크 컨트롤러 캐시·네트워크 파일시스템의 내구성을 검증한 것은 아니다. Ubuntu 실행은 다음 PR CI에서 확인 대상이며 로컬 Windows 결과와 구분한다.

## 다음 연결

DECOR-04는 참조 검사 뒤의 실제 geometry다. 현재 Store는 형식/참조만 검사하므로 이 결과만으로 배치·동선 안전을 보장하지 않는다. DECOR-07에서는 준비된 방 검증 → Commit → 실제 저장 상태 확인 → 활성화 순으로 연결한다. saved와 화면 적용 실패는 다른 상태로 표현한다. 자동 초안 저장·개인 자료 이전·실행판 교체는 이번 범위 밖이다.
