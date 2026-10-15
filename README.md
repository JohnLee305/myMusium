# 가족 VR 박물관

가족의 소중한 물건과 이야기를 3D로 보존하고 관람하는 확장형 개인 박물관.

실제 개발 위치: `C:\Dev\vr-museum`.

개발을 재개할 때 실제 개발 폴더의 `STATUS.md`를 먼저 읽는다.

## 개발 원칙

- Museum Archive가 원본이며 Unity는 이를 읽는 Viewer다.
- 전시품 데이터, 전시 공간, 실행 앱을 분리한다.
- 초기에는 JSON과 로컬 파일을 사용한다.
- 원본 자산과 PC/모바일 최적화 자산은 구분한다.
- 개인 자료의 공개 여부와 VRChat 내보내기는 이후 별도로 구현한다.
- 처음에는 PC 연결 Quest 3를 목표로 한다. 단독 실행은 후속 단계다.

## 순서

1. 개발환경과 Quest Link 확인
2. Archive 규칙, 샘플, 검증 도구
3. 전시품 하나와 설명을 표시하는 Viewer
4. 데이터를 추가해 여러 전시품 표시
5. Curator Admin
6. 컬렉션과 방 확장
7. 미디어 강화와 공개 자료 VRChat Export

## 실행 가능한 미리보기
Windows: Builds/Windows/VRMuseum.exe
Unity 프로젝트: apps/quest-viewer
실행 방법과 재빌드: docs/QUICKSTART.md
현재는 Archive의 항아리·책·고리 샘플 GLB와 JSON 설명을 읽어 자동 배치한다. 한 방에 최대30개 표시, 방 자동추가/이동시 로딩/이전방 해제 및 항목별 오류 격리를 지원한다. 데이터 규칙: docs/ARCHIVE.md.


## HTML 전시품 추가
Open-Curator.cmd를 더블클릭하면 관리 화면(http://127.0.0.1:4177)이 열린다. 샘플 또는 GLB 파일로 전시품을 등록하고 관람 앱을 재실행한다. 사용법: docs/CURATOR.md.


