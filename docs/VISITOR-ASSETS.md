# 관람객 캐릭터 에셋 조사

확인일: 2026-10-15 · 작업 브랜치: `codex/museum-tycoon`

## 첫 후보

**Quaternius Ultimate Modular Men / Women의 일상복 캐릭터**를 추천합니다. 전시실 손님으로 자연스러운 옷차림을 고를 수 있고, 부위별 조합으로 변형하기 좋습니다. 우선 남성 Casual_2 샘플과 Humanoid 버전을 공식 배포 폴더에서 확보했습니다. 여성 팩은 후보 조사만 했으며 아직 다운로드·파일 검증하지 않았습니다.

![실제 확보한 Humanoid 샘플의 외형 미리보기](assets/visitor-casual2-preview.png)

위 이미지는 확보한 FBX를 Blender에서 렌더링한 T-pose 외형 확인용입니다. 박물관 안에서 걷는 화면이나 최종 관람객 디자인은 아닙니다.

## 비교 결과

| 후보 | 공식 안내·파일 확인 | 이번 판단 |
|---|---|---|
| [Quaternius Ultimate Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html) / [Women](https://quaternius.com/packs/ultimatemodularwomen.html) | 소개 기준 남성11·여성10종, 각24애니메이션·부위교환. 남성 일상복 샘플의 실제 파일 검사 수행 | **첫 후보.** 일상복을 골라 관람객 구성. Unity 재생 검증 후 채택 확정 |
| [Kenney Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists) | 공식 무료 ZIP 다운로드, 동봉 CC0 확인. 실제 파일은 공통 모델과4개 스킨, idle/run/jump 애니메이션 | 친근한 인형 비율의 대안. 별도 Walk 파일은 없어 그대로 관람객 걷기에 쓰기에는 보완 필요 |
| [Quaternius Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html) | 공식 소개: 기본 체형6종·헤어20종, 평균13k삼각형, Humanoid, 별도 애니메이션 라이브러리 호환 | 의상·외형 조합 작업이 더 필요해 첫 관람객용으로는 후순위. 실제 다운로드·Unity 검사 미실행 |

‘가벼움’은 첫 후보의 지오메트리 수치를 보고 내린 잠정 판단입니다. 머티리얼 수·그림자·애니메이션·동시 인원도 성능에 영향을 주므로 Quest 성능을 보장하는 결과는 아닙니다.

## 실제 남성 샘플 검사

검사 도구: 설치된 Blender 4.5.13 LTS, FBX import 및 mesh/action 목록 검사.

| 파일 | 용량 | 형상 | 리그·동작 | 상태 |
|---|---|---|---|---|
| Casual_2 일반 FBX | 8,099,148 bytes | mesh4개, vertex2,964개, triangle5,776개 | bone79개, action24개 | Walk / Idle_Neutral / Interact / Wave 존재 확인 |
| Casual2 Humanoid FBX | 429,964 bytes | mesh4개, triangle5,776개 | bone62개, action없음 | T-pose 외형 렌더링 확인, 동작 별도 연결 필요 |

일반 FBX의 액션 이름은 `CharacterArmature|CharacterArmature|Walk` 같은 형식입니다. 최초 검증은 Walk와 Idle_Neutral부터 연결하며, 전투·무기 동작은 관람객에 사용하지 않습니다.

일반 FBX의 Blender 대기 렌더는 이번 임시 미리보기에서 모델이 표시되지 않아 정상 재생으로 판정하지 않았습니다. 외형 미리보기는 Humanoid 버전을 사용했습니다. 클립 이름·형상 import 확인과 실제 애니메이션 재생 성공은 별개입니다. Unity Generic import로 정상 재생되는지 우선 확인하고, 필요하면 Humanoid 버전과 별도 동작 매핑을 검토합니다.

Unity 프로젝트 Assets에는 아직 편입하지 않았습니다. 현재 보관 위치는 `third_party/visitors/quaternius-modular-men/`입니다. 행동 코드, 애니메이터, 관람객용 Windows 빌드와 실기기 검사도 아직 없습니다.

## 출처·라이선스

공식 팩 페이지의 다운로드 버튼이 가리킨 제작자 공유 폴더:

- [Ultimate Modular Men 공식 배포 폴더](https://drive.google.com/drive/folders/1USAAquX2JJWuA2m6zol0KUkFe3UkZ8zX)
- [이 폴더의 License.txt 원문](https://drive.google.com/uc?export=download&id=1TTvylHa1CsiJuHFWWiv6PFGhLM-aAH5z)
- [일반 FBX 원본](https://drive.google.com/uc?export=download&id=15q2F99fLycew7wQunZhfg0n_wChZfyCM)
- [Humanoid FBX 원본](https://drive.google.com/uc?export=download&id=1fd7_cf4Dvtt6kjxfsK7y-tWf7RK-gSfn)

이 폴더의 **License.txt는 CC0 1.0 Universal / Public Domain Dedication을 명시**하며, 원문을 파일과 함께 보관했습니다. 이 판단은 위 배포 폴더에서 이번에 확보한 두 파일에 한정합니다.

주의해서 확인한 사항: Quaternius 팩 소개에는 CC0가 적혀 있지만, [현재 공통 License 페이지](https://quaternius.com/license.html)는 2026-08-28 갱신 QAL로 원본 에셋 재배포를 제한하고 있습니다. 따라서 다른 팩이나 새 배포본에 이번 CC0 판정을 일괄 적용하지 않습니다. 추가 다운로드마다 해당 파일의 동봉 조건을 다시 확인합니다.

로컬 파일 SHA-256은 `third_party/visitors/quaternius-modular-men/provenance.json`에 저장합니다. 이는 수령 파일 식별용 값이며 제작자가 공개한 공식 체크섬과 비교했다는 의미는 아닙니다. 제작자 코드나 설치 프로그램을 실행하지 않고 FBX 형상 데이터만 검사했습니다.

Kenney ZIP은 `logs/visitor-asset-research/protagonists/`에 시험용으로만 보관합니다. zip SHA-256: `EC3787DE70FA2200256848D74201B10F6B6C3126594E9857BF989753312C2B84`. 파일에 동봉된 License.txt도 CC0를 명시합니다. 이 대안 팩은 프로젝트에 편입하거나 GitHub에 업로드하지 않았습니다.

## 다음 구현

1. 확보한 일반 FBX를 실험 Unity 프로젝트로 import하여 Walk / Idle_Neutral의 실제 재생·변형·방향·크기를 검증.
2. 선택한 리그의 애니메이터로 이동/정지 전환을 연결. 위 검증이 실패하면 외형만 채택하고 리그/동작 결합 방법을 수정.
3. TYC-001 관람객1명 입장 → 전시품 이동 → 관람 → 퇴장 구현.
4. 이후 여성 일상복·옷 색상 등으로 다양화하고, 0~6명과 동선 예외를 검증.

현재 결론은 **후보 조사·샘플 확보 완료, 캐릭터 실제 통합과 관람객 행동은 구현 전**입니다.
