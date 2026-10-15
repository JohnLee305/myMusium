# ADR-0001 개발환경 기준

결정일: 2026-09-10

## 사용자 결정

- PC에 Quest 3를 연결하는 Link 관람부터 완성한다.
- Quest 단독 실행은 이후 단계에 추가한다.
- 소프트웨어는 C: 기본 위치에 설치한다.
- 실제 저장소는 `C:\Dev\vr-museum`에 둔다.
- Quest 실기기 테스트는 지금 할 수 없어 잔여 과제로 남긴다.
- 기존 Unity 계정이 있으며 로그인은 사용자가 직접 진행한다.
- 작업 상태 및 이력을 Markdown 파일로 지속 관리한다.

## 구현 선택

- Unity Hub 3.21.1과 Hub 배포 목록에 있는 Unity Editor 6000.3.23f1을 사용한다.
- Blender는 지원 중인 4.5.13 LTS를 사용한다.
- Unity의 Android 및 타 플랫폼 모듈은 필요한 단계에서 추가한다.
- Archive / Viewer / Admin / Exporter는 분리한다. 초기 클라우드 백엔드는 만들지 않는다.
- Unity 및 XR 패키지 버전은 첫 컴파일 확인 후 manifest와 lock 파일로 고정한다.

## 근거

- Unity 릴리스: https://unity.com/releases/editor/whats-new/6000.3.23f1
- Meta 개발환경: https://developers.meta.com/horizon/documentation/unity/unity-development-requirements/
- Meta Link 개발: https://developers.meta.com/horizon/documentation/unity/unity-link/
- Blender 4.5 LTS: https://www.blender.org/releases/4-5/

## 미결정

- GLB 로더, Archive Validator 언어, Admin 프레임워크
- Quest standalone 데이터 전달 및 갱신 방식
- 공개 여부 및 대상 필드의 확정 스키마
