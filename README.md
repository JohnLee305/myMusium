# 가족 VR 박물관

가족의 소중한 물건과 이야기를 3D로 보존하고 관람하는 확장형 개인 박물관.

실제 개발 위치: `C:\Dev\vr-museum`.

개발을 재개할 때 실제 개발 폴더의 `STATUS.md`를 먼저 읽는다.


## 전체 계획과 진행상황

- [최신 전체 진행상황·개발 계획 (GitHub 실험 브랜치)](https://github.com/JohnLee305/myMusium/blob/codex/museum-tycoon/PROJECT_STATUS.md)
- [이 브랜치의 진행상황 문서](PROJECT_STATUS.md)
- [기준/실험 버전 구분과 복원](docs/VERSIONING.md)

앞으로 기능을 추가·수정할 때마다 실제 작업 브랜치의 진행상황 MD와 검증 결과를 갱신하고, 관련 코드와 함께 GitHub에 올립니다.

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
기준 버전의 Open-Curator.cmd 또는 관리 바로가기를 열면 서버 제어창이 열린다. 서버 켜기 → 관리페이지 열기 순으로 사용한다. 실험 관리 서버는 별도 포트를 사용한다. 샘플 또는 GLB 파일로 전시품을 등록하고 관람 앱을 재실행한다. 사용법: docs/CURATOR.md.

## 타이쿤 실험 관람객
이 브랜치의 개발 폴더는 C:\Dev\vr-museum-tycoon입니다. 바탕화면 VR 박물관 (타이쿤 실험)으로 실행하고 오른쪽 패널에서 관람객을 0~6명 조절합니다. 기존 관람 버전은 별도 폴더·바로가기로 유지합니다.

새 사람 에셋 추가·교체는 [관람객 외형 확장](docs/VISITOR-EXTENSION.md), 현재 현황은 [PROJECT_STATUS.md](PROJECT_STATUS.md)를 참고합니다.
