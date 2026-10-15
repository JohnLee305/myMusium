# Archive 1.0 / 여러 전시품 단계

manifest 목록을 읽어 순서대로 자동 배치한다. 기본 샘플은 항아리·책·고리 조형물 3개다.

## 구조

```text
museum-archive/
  manifest.json
  schemas/
    manifest.schema.json
    artifact.schema.json
  artifacts/sample-vase/
    artifact.json
    models/pc.glb
```

manifest는 `schemaVersion: "1.0"`과 전시품 JSON의 상대경로 배열 `artifacts`를 갖는다. 1~10,000개의 중복 없는 경로를 허용한다. 선택적 rooms 배열로 방별 배정을 저장한다. rooms가 없는 기존 자료는 목록을30개씩 나누어 자동 해석한다. CLI는 전체 방의 자료를 검사하며 Viewer는 이동한 방만 모델을 읽는다.

전시품 필드:

| 필드 | 규칙 |
|---|---|
| schemaVersion | 1.0 |
| id | 소문자 영문/숫자/하이픈, 폴더명과 일치 |
| title | 1~80자, 공백만 있는 값 금지 |
| description | 1~400자, 줄바꿈 지원 |
| model | Archive 기준 artifacts/{id}/models/{파일명}.glb |
| scale | 0.01~10, 원본 미터 크기에 곱하는 값 |

모든 경로는 Archive 루트 기준 `/` 구분 상대경로다. 절대경로, `..`, `.` 및 링크 경로는 허용하지 않는다. ID와 파일 위치는 일치해야 한다.

이번 범위의 GLB는 64MiB 이하, glTF 2.0 자체 포함 파일이다. 외부 버퍼/이미지 URI는 허용하지 않는다. 검증 도구는 헤더와 JSON chunk 및 참조를 검사하며, 실제 mesh/material 해석은 glTFast에서 추가 검증한다.

## 실행

```powershell
C:\Dev\vr-museum\tools\validate-archive.ps1
C:\Dev\vr-museum\tools\test-archive.ps1
C:\Dev\vr-museum\tools\open-viewer.ps1
```

검증 CLI는 통과 시 종료코드 0, 실패 시 1. PowerShell 7의 Test-Json을 사용하며 추가 패키지 설치는 필요 없다. 현재 개발 셸은 이 기능을 지원한다.

Viewer는 실행 위치에서 상위 폴더의 museum-archive를 찾는다. 다른 경로를 시험하려면 실행 파일에 `-museum-archive "C:\다른 폴더"`를 지정한다. 배포 시 Windows 빌드만 복사하면 Archive는 포함되지 않으므로 데이터 폴더도 함께 전달하거나 위 옵션으로 지정해야 한다.

## 재빌드 없이 수정

1. 앱을 닫는다.
2. artifact.json의 title/description/scale 또는 model을 바꾼다.
3. validate-archive.ps1을 실행한다.
4. 같은 실행 파일을 다시 연다.

실행 중 자동 새로고침은 아직 없다. 모델의 밑면을 전시대 위에 놓고 X/Z 중심을 맞춘다. scale은 보존하며 큰 모델을 임의로 축소하지 않는다. 배치 공간을 지키기 위해 크기 적용 후 가로/깊이 0.95m 또는 높이 1.8m를 넘는 모델은 오류 대체 표시한다. 해당 artifact.json의 scale을 줄여 다시 실행한다.

항목별 오류 발생 시 해당 위치에 대체 큐브와 안내 문구를 표시하고 다른 전시품은 계속 로딩한다. manifest 자체가 잘못되거나 중복 경로가 있으면 목록 전체를 거부하고 안내를 표시한다. 자세한 원인은 실행 로그의 ARTIFACT_FAILED에 남긴다. 검증되지 않은 전시품을 정상 로딩으로 표시하지 않는다.

## 샘플과 지원 범위

- 청록색 항아리는 tools/create-sample-vase.py로 직접 생성한 약 44.5cm 높이의 샘플이다. 실제 소장품을 의미하지 않는다.
- 한글 표시는 Windows의 맑은 고딕을 사용한다. 폰트를 복사하거나 재배포하지 않는다. 다른 OS/Quest 단독 빌드의 폰트는 후속 결정이다.
- 공개 여부/소유자/미디어 필드와 원본·모바일 자산은 후속 단계에 추가한다. 현재는 로컬 개인용 PC 모델 하나만 소비하며 공개/업로드 기능은 없다.
- 기존 환경 점검용 10cm GLB는 samples/environment-check에 별도로 남아 있다.

## 전시실 배정과 이동

manifest.rooms는 선택 필드다. 각 방은 id(소문자영문/숫자/하이픈 1~64자), title(1~80자), artifacts(0~30개 상대경로)를 갖는다. 방 ID는 고유해야 하고 전체 manifest.artifacts의 모든 항목이 정확히 한 방에 속해야 한다. 미배정/중복배정/없는 경로/30개 초과는 거부한다. 빈 방도 허용한다. 방 순서는 rooms 배열 순서다.

기존 자료는 읽기만 할 때 변경하지 않는다. 관리 화면에서 처음 등록·방 추가·이동을 저장할 때 기존 전체 목록을30개씩 나눠 rooms를 만들고 함께 저장한다. 자동 배정은 첫 여유 방부터 채우며 모든 방이 가득 차면 새 방을 추가한다. 특정 방을 골랐는데 꽉 찼다면 자동으로 다른 방에 넣지 않고 오류를 안내한다.

현재 상한: 전체10,000개, 방1,000개, manifest4MiB. 일반 artifact JSON은1MiB, GLB64MiB. 이는 유한한 로컬 처리 상한이며 해당 규모의 성능 보증이 아니다.

한 방은 고정6열×5행. 열2.4m/행2.8m 간격, 공간16.4m×19m. 전시품 수가 줄어도 방 크기와 빈 칸은 유지한다. 항목 실패 시 그 자리를 유지한다.

Page Up/Down, 상단 이전·다음 버튼, 입구 좌우 표지 근처에서 방을 전환한다. 로딩 동안 데스크톱 이동을 잠그고 목적지 GLB를 순차 로딩한다. 새 방이 준비되면 이전 루트를 파괴하고 glTF importer/font를 정리한 다음 UnloadUnusedAssets를 호출한다. 안정 상태에는 현재 방 최대30개만 남는다. 전환 중에는 이전 방과 목적지 자원이 일시 공존할 수 있어 최대60개 메모리가 필요할 수 있다. HMD 로딩 연출과 실기기 성능은 별도 검증 대상이다.

전시품이나 방 구성을 관리 화면에서 바꾼 뒤에는 Viewer를 재실행한다. 실행 중 자동 갱신은 아직 없다.

방별 선택속성 rooms[].theme는 23개 색상키의 #RRGGBB 객체다. 생략한 값은 기본색이다. 스키마와 docs/decisions/0005-room-themes.md 참고.


## 고정 격자와 휴지통 (2026-10-14)
rooms[].cells는 artifacts와 길이가 같은 정수 배열이다. 각 값은 0~29이며 방 안에서 중복될 수 없다. 0~5는 입구 쪽 첫 행, 24~29는 안쪽 다섯째 행이다. X=(cell%6-2.5)*2.4, Z=floor(cell/6)*2.8. 생략한 기존 방은 artifacts 순서대로 0부터 배정하며 읽기만으로 원본 파일을 변경하지 않는다.
휴지통은 manifest.trash 배열에 {entry,roomId,cell,deletedAt}를 보관한다. active artifacts 및 방 배정에서는 제거하되 실제 파일은 남는다. 복원은 빈 칸에 다시 배정한다. 전체 전시품0개도 유효하며 방은 최소1개를 유지한다. 삭제된 ID는 파일이 남아 있어 신규 등록에 재사용할 수 없다.
배치 변경과 휴지통은 manifest 원자적 교체·백업, 설명 수정은 해당 artifact JSON 원자적 교체·백업을 사용한다. API revision과 itemRevision으로 오래된 화면의 덮어쓰기를 방지한다. 영구삭제 UI는 제공하지 않는다.
