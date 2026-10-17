# DECOR-01 구현 인계

## 제공한 인터페이스

네임스페이스 `FamilyMuseum.Decor`. `Runtime/Decor/Domain/DecorData.cs`의 공개 필드 이름은 첫 schema와 같다. 벡터는 `double[]`, 회전은 degree, 버전은 int다. UnityEngine과 외부 JSON 패키지를 사용하지 않는다.

- `DecorJson.TryRead(json, out DecorDocument, out ValidationReport)`: fixture envelope의 엄격 형식 검사. 의미 검사는 하지 않는다.
- `DecorJson.TryReadCatalog`, `TryReadWorld`: 실제로 분리된 문서의 형식 검사.
- `DecorJson.Write`, `WriteCatalog`, `WriteWorld`: 형식 검사 후 문화권 불변 JSON. parent/storage의 사용하지 않는 필드는 출력하지 않는다.
- `DecorReferenceValidator.Validate(catalog, world)`: typed 입력의 형식 재검사 후 참조 단계 1~5.
- `DecorReferenceValidator.ValidateJson(json, out document)`: 형식 실패 시 SCHEMA_INVALID 한 개와 null document, 형식 성공 시 DTO와 참조 결과.
- `ValidationReport.ok`, `.issues[]`: 참조 단계만의 결과다. geometry 통과 또는 저장 허가를 의미하지 않는다.

DTO의 ItemLocation은 nullable 필드로 union을 표현한다. parent에는 parentId/surfaceId/localPositionM/yawDeg만, room에는 roomId/positionM/yawDeg만, storage에는 kind만 둔다. 새 location을 만들 때 이전 branch 필드를 남기면 SCHEMA_INVALID로 거절한다. Unity JsonUtility로 이 union을 저장하지 말고 제공된 codec을 쓴다.

public field DTO는 mutable이다. DECOR-02는 초안과 원본의 배열/객체를 깊은 복사해야 한다. 참조를 공유한 채 Undo/취소하면 원본이 바뀐다. roundtrip deep copy를 임시로 사용할 수 있으나 의미 검사 결과까지 통과한 것으로 가정하지 않는다.

## 검사 위치와 범위

`contracts/decor/edit-slice.schema.json`과 `tests/fixtures/decor`가 구현용 계약/fixture다. docs의 초기 fixture는 설계 이력이다. 계약 변경은 명시적 검토와 두 위치의 차이 설명을 남긴다.

`pwsh -NoProfile -File tools/test-decor-contract.ps1`는 실제 production C# 세 파일을 Add-Type으로 컴파일한다. PowerShell 7 이상이 필요하다. Unity·NuGet·서비스·사용자 Archive 없이 실행된다. Actions의 Ubuntu/Windows regression job에 동일 검사를 추가했다.

2026-10-17 Windows PowerShell 7 호스트에서 204검사 통과: schema와 C#의 fixture15개 판정 일치, 참조 오류, 정밀 JSON 왕복, 중복 JSON 키/모호한 union/비정상 숫자, 정확한 버전 참조, Unicode 길이 경계, 문화권 차이, 입력 순서와 무관한 오류 정렬, 512노드 순환, 개별 world/catalog 경계.

방 밖·상판 돌출·가구 겹침·문 여유 영역 침범·소품 높이 오류 fixture는 형식/참조 검사를 통과한다. 해당 판정은 DECOR-04다. 테스트 로그에서 `reference-only pass`는 그 범위만 의미한다.

Unity Editor/Player import·IL2CPP·화면·Quest는 이번에 검사하지 않았다. .meta를 포함했고 기존 ArchiveData.cs, 씬, 실행판, 개인자료는 변경하지 않았다.

## 구현 선택과 주의

- 첫 schema의 작은 고정 형식을 검증하는 순수 C# codec을 썼다. 일반 JSON Schema 엔진은 아니며 schema와 별개로 바뀌면 differential fixture 검사를 추가해야 한다.
- JSON 최대 입력 4Mi문자·중첩24·배열4096의 방어 한도를 두었다. 현재 계약의 최대 항목 수는 그 안에 있다.
- 중복 정의/개체 참조는 first/last-wins로 선택하지 않는다. 중복 자체 오류를 보고하고 모호한 키의 파생 검사를 건너뛴다.
- 부모 순환 탐색은 반복문으로 수행한다. cycle 구성원 각각을 보고하고 순환으로 생긴 표면 오류를 중복 보고하지 않는다.
- 숫자·이름·벡터의 형식 검사는 public typed API에서도 수행한다. 단, raw JSON의 unknown key와 missing key는 직렬화 경계에서만 알 수 있다.
- ID나 좌표를 읽기만 할 때 정렬·snap·재발급하지 않는다. 새 조작의 snap은 DECOR-04가 맡는다.

## 다음 작업

사용자 결과 확인과 저장소 통합 후 DECOR-02: 위 DTO/codec/ValidationReport를 사용해 명령·보관·Undo를 구현한다. DECOR-03과 04도 이 데이터 구조를 사용하며, geometry 완료 전 참조 결과만으로 실제 배치를 확정하지 않는다.
