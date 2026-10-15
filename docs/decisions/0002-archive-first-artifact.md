# ADR-0002 단일 Archive 전시품

결정일: 2026-09-10

## 결정

- JSON + 외부 파일 시스템을 원본으로 유지한다.
- 첫 범위는 한 전시품. ID/title/description/model/scale와 schemaVersion만 확정한다.
- 검증 CLI는 설치된 PowerShell 7의 Test-Json을 사용한다. 스키마는 draft-07.
- 런타임 로더는 Unity 공식 패키지 glTFast 6.20.0을 사용한다. Unity registry에서 Unity 6000.0 이상 호환을 확인했다.
- 내장 렌더 파이프라인의 glTF shaders를 Windows 빌드에 포함한다.
- Archive 검증과 파일 처리는 LocalArchive, 3D 표시와 로더 수명 관리는 ArtifactDisplay에 둔다.
- 앱 재실행 시 Archive를 읽는다. 파일 변경에 앱 재빌드를 요구하지 않는다.

## 검증 기준

정상 항아리와 한글 설명 표시, 잘못된 자료 대체 표시, 동일 실행 파일에서 변경 JSON 반영을 확인한다. 실제 결과는 WORKLOG.md와 logs에 기록한다.

## 한계

- 아직 단일 전시품. 목록 배치와 부분 실패 격리는 다음 단계.
- 동기식 로컬 파일 읽기와 최대 64MiB 제한은 첫 샘플 범위용. 대용량 비동기 I/O와 자산 메모리 예산은 후속.
- 런타임은 핵심 필드·경로·크기·GLB 참조를 방어적으로 검사한다. JSON Schema 전체 검증은 CLI가 담당한다.
- Meta Quest 실기기 성능 및 standalone 호환성은 아직 검증하지 않았다.

## 공식 참고

- https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportRuntime.md
- https://packages.unity.com/com.unity.cloud.gltfast
