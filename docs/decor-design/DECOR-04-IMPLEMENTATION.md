# DECOR-04 배치·표면·충돌 검사 인계

2026-10-17 · 첫 직각 배치 계약 · 실제 씬/mesh/관람객 길찾기 미연결

DECOR-01/02 통합판 251202c에서 독립 작업했다. DECOR-03 저장 구현은 별도 작업폴더에 미커밋 검토 상태로 보존했고 이 작업에 복사하거나 통합하지 않았다. 저장과 충돌은 상위 계약의 공통 DTO로 연결할 수 있으며, 각 작업의 사용자 확인/통합은 따로 확인한다.

## 제공 API

네임스페이스 `FamilyMuseum.Decor`. schema와 기존 DTO·참조검사·명령 소스는 변경하지 않았다.

```text
DecorPlacementValidator.Validate(catalog, world) -> ValidationReport
DecorPlacementValidator.ValidateJson(json, out document) -> ValidationReport
DecorPlacementValidator.BeginEdit(catalog, world, historyLimit=200) -> DecorEditSession

DecorGeometry.SupportedYaw(yaw)
DecorGeometry.NormalizeYaw(yaw)
DecorGeometry.QuantizeM(value)
DecorGeometry.SnapPosition(position, origin, stepM)
DecorGeometry.TransformPoint(position, yaw, localPoint)
DecorGeometry.Bounds(localBox, position, yaw) -> DecorBounds
DecorGeometry.Contains(outer, inner)
DecorGeometry.Overlaps(a, b)
```

벡터는 [X,Y,Z] double 배열, 단위 m다. DecorBounds는 읽기 전용 minX/minY/minZ/maxX/maxY/maxZ를 제공한다. 출력 벡터와 bounds는 입력 데이터와 공유하지 않는다.

## 검사 순서

1. 기존 strict JSON/형식/참조 검사 단계1~5. 실패하면 그 오류를 반환하며 모호한 참조에 대해 geometry를 추측하지 않는다. 참조를 수정한 뒤 다시 검사한다.
2. 설치 물건의 local yaw가 0/90/180/270인지 확인. 45도는 ROTATION_UNSUPPORTED이며 임의 보정하지 않는다. 부모 회전이 지원되지 않으면 그 자손의 world geometry 계산을 건너뛴다.
3. 방 바닥 가구는 room.bounds의 yMin에 원점이 있는지 검사한다.
4. 자식은 부모 로컬 상판에 원점과 collision proxy 밑면이 접촉하는지, 실제 회전 밑면 외곽이 상판 XZ 영역에 들어가는지 검사한다.
5. room에 놓인 각 가구·소품의 실제 회전 proxy가 방 안에 있고 예약 공간을 침범하지 않는지 검사한다.
6. 같은 방 또는 같은 storage assembly 내부의 모든 proxy pair를 검사한다. 오류 정렬은 기존 ValidationReport를 사용한다.

geometry 오류는 ROTATION_UNSUPPORTED, FLOOR_HEIGHT_INVALID, SUPPORT_HEIGHT_INVALID, SUPPORT_OVERHANG, OUTSIDE_ROOM, RESERVED_ZONE_BLOCKED, ITEM_COLLISION이다. 오류에는 가능한 경우 대상 ID·관련 ID·방·world 위치를 넣는다. 쌍 충돌은 ID Ordinal 순의 첫 물건을 primary, 다른 물건을 related로 보고하여 입력 순서에 영향을 받지 않는다.

## 좌표·맞춤·회전

Unity의 +Y yaw 방향을 따른다. 90도에서 local +Z는 world +X, local +X는 world -Z다. 부모 world 위치와 회전을 적용한 자식 좌표를 사용하고, 자식의 자체 회전은 부모 회전에 합성한다. 상판 맞춤과 지지 외곽은 부모의 로컬 공간에서 검사하므로 부모 회전 때문에 기준축이 바뀌지 않는다.

SnapPosition은 XZ만 25cm 또는 5cm 눈금에 맞춘다. 원점은 바닥이면 방 원점, 상판이면 해당 surface.centerM이다. Y는 눈금에 맞추지 않고 1mm 정규화만 한다. 양/음의 반칸은 모두 원점에서 먼 방향으로 반올림한다. 0.075/0.05의 이진 부동소수 오차 때문에 반칸이 아래로 떨어지지 않도록 decimal 산술을 사용한다.

QuantizeM은 1mm, NormalizeYaw는 음수와 360도 주기를 처리한다. 이는 **편집 입력 도구**다. Validate와 로딩은 위치·각도를 snap하거나 원본을 수정하지 않는다. JSON의 raw yaw=360은 schema 단계에서 거절하며 편집기가 제출 전에 NormalizeYaw를 사용한다.

## 경계·접촉·침범

1mm를 허용 오차로 사용하며 여기에 1e-9m의 산술 잡음만 추가한다. 정확한 경계 접촉과 1mm 이하의 작은 차이는 허용하고 그 이상은 거절한다. 이는 통로 폭이나 사람 안전거리의 규칙이 아니다.

직각 회전한 직육면체 proxy는 AABB로 정확히 표현할 수 있다. centerM이 원점에서 벗어난 proxy도 중심을 회전한 뒤 외곽을 계산한다. 임의 yaw나 실제 mesh의 빈 공간·오목한 형태는 이 단계의 지원 범위가 아니다.

충돌 깊이는 단순 교집합 폭이 아니라, 각 축에서 둘을 분리하는 데 필요한 최소 이동 거리다. 얇은 소품이 두꺼운 물체 안에 완전히 들어가면 얇은 교집합 폭만 보고 놓치지 않아야 하기 때문이다. 모든 축에서 이 거리가 허용 오차보다 크면 ITEM_COLLISION이다.

부모와 자식 사이도 일반 proxy 충돌을 검사한다. 정상 상판 접촉은 관통 깊이가 0이므로 통과한다. 부모의 몸체 안에 선언된 상판이나 아래로 튀어나온 소품을 ‘부모 관계’라는 이유로 통째로 면제하지 않는다.

## 공간과 보관

다른 roomId의 동일 로컬 좌표는 겹침이 아니다. storage의 서로 다른 루트도 각자 별도 가상 공간이다. 같은 저장 묶음 안의 소품 지지·형제끼리 겹침·부모 몸체 침범은 계속 검사한다. storage 루트에는 방 경계나 문 검사만 적용하지 않는다.

문 여유 공간은 room.reservedZones의 3차원 box다. 바닥 투영만으로 높은 소품을 통행 장애물이라고 판단하지 않는다. 실제 관람객의 전체 경로 연결, 대기 공간, 몸 반경과 군중 정체는 DECOR-08 동선 연결 검토 대상이다. 이번에 ‘동선 전체가 안전하다’고 판정하지 않는다.

## 명령과 프리뷰 연결

기존 DecorEditSession의 기본 생성자는 여전히 참조 검사만 수행한다. 실제 배치 편집은 **DecorPlacementValidator.BeginEdit**를 사용하거나 Validate delegate를 additionalValidator로 넣는다. baseline과 geometry를 통과한 candidate만 초안/history에 반영한다.

잘못된 위치 클릭은 rejected 결과와 오류를 표시하고 프리뷰를 계속 움직일 수 있게 한다. UI 프리뷰는 초안 밖에서 world 복사본에 후보 transform을 넣어 Validate한다. 드래그 중 매 프레임 ApplyCommand를 호출하지 않는다. 놓기 성공 때만 명령 하나를 제출한다.

문 앞 이동·상판 돌출·다른 가구 안으로 이동이 거절될 때 원본·초안·수량·Undo 기록이 보존되는지 실제 명령 연결 검사로 확인했다. valid 회전과 Undo/Redo에서는 자식의 ID·parent·상대 좌표를 유지한다.

## 검증과 다음 작업

`pwsh -NoProfile -File tools/test-decor-placement.ps1`가 실제 C# 도메인8파일을 컴파일한다. 설계 fixture15개의 기대 형식/의미 오류를 대조하며, 회전 외곽은 별도 sin/cos를 이용한 8꼭짓점 계산 40조합과 비교한다.

기타 검증은 ±반칸·원점이 있는 5cm snap·1mm/초과 경계·정상/보관된 지지·회전된 소품·얇은 완전포함 충돌·다른 방/저장묶음·문 볼륨 높이·64가구·명령 거절/복원/오류 정렬이다. 로그와 실제 개수는 작업 handoff가 정본이다.

초기 시험 도구에서 PowerShell 배열 산술 괄호를 수정했고, 64가구 시험이 문 여유 공간을 침범한 것을 확인해 그 시험에서는 예약 공간을 분리했다. 문 침범 거절 시험은 별도로 유지했다. 제품 규칙을 완화해 시험을 통과시키지 않았다.

Unity/실제 렌더·mesh·일반 다각형·길찾기·벽/천장·성능 보장은 미검증/범위 밖이다. 다음은 DECOR-05의 시험 씬·카메라·편집 UI이고, DECOR-06에서 이 검사와 프리뷰를 연결한다. DECOR-03 미통합 저장 코드를 여기서 통합했다고 간주하지 않는다.
