# 방별 색상 테마

2026-10-13. PC Quest Link 우선 정책은 유지하며 이번 검증은 데스크톱 실행이다.

## 저장과 호환

manifest.rooms[].theme는 선택적인 색상 객체다. 생략한 항목은 기본색으로 표시한다. 키와 기본값은 apps/curator-admin/themes.mjs, Unity 대응 필드는 RoomTheme.cs에 정의한다. 색은 불투명 #RRGGBB 형식이며 23개 항목만 지원한다. 전체 theme 생략은 기존 자료와 호환된다. 색상 변경은 전시품의 GLB 재질과 원문을 바꾸지 않는다.

프리셋은 관리화면에서 전체 색상 값으로 펼쳐 저장한다. 프리셋 이름을 런타임 규칙으로 저장하지 않으므로 개별 변경과 향후 프리셋 변경이 기존 방에 영향을 주지 않는다. 초기 방 및 자동 추가 방은 기본색이다.

## 저장 API

POST /api/room-theme. 기존 관리 요청과 동일한 로컬 origin/host 검사, 토큰, application/octet-stream, URI 인코딩 JSON X-Museum-Metadata를 사용한다. metadata는 roomId, theme, themeRevision이다. /api/state의 방마다 themeRevision을 반환한다. 현재 저장값의 SHA256 revision이 다르면 409로 거부한다. theme=null은 override를 제거한다. 저장 전 manifest 백업, 임시파일 후 rename을 사용한다. 기존 등록 및 방 이동도 theme를 보존한다.

## 화면

관리페이지의 ‘방마다 다른 분위기’에서 방/프리셋/개별 색을 선택하고 저장한다. 미리보기는 2D 색 조합이며 실제 조명 렌더링과 같지 않다. 글자 대비 안내는 선택값의 대비를 기준으로 한다. 기본색 복원도 저장 버튼을 눌러야 반영된다. 저장 후 관람 앱을 종료하고 재실행해야 한다.

## Unity

RoomPalette가 환경 및 해당 방 전시대의 재질 복사본을 소유한다. 공유 템플릿 재질을 변경하지 않는다. 방 교체 때 RoomPalette가 소유한 재질을 정리한다. GalleryDisplay는 현재 방 Theme를 적용하고 ArtifactDisplay/Wayfinding/DetailsPanel에 전달한다. 색이 없는 다음 방도 기본값을 명시적으로 적용하여 이전 방 색이 남지 않게 한다.

## 검증

- tools/test-themes.mjs: 별도 자료에서 18개 API/저장/충돌/기존자료/복원 사례. 정상 및 invalid 하위 자료를 생성한다.
- tools/test-admin.mjs: 기존 등록/방관리 23개. 실제 자료의 방 배정 및 과거 백업을 시험 초기값으로 쓰지 않는다.
- tools/test-room-themes.ps1 -ArchivePath <test-themes가 출력한 폴더>: 밝은방/어두운방/상세창/기본자료/오류자료 5개 실행. THEME_CHECK는 환경·전시대·명판·제목·조명 색과 현재방 import 수를 대조하고 4회 왕복한다.
- 브라우저에서 프리셋 및 개별 HEX 저장, 방 간 분리, 기본복원 미리보기, 콘솔 오류 부재 확인. Quest 실기기는 후속이다.
