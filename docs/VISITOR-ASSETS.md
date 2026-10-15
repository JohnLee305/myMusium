# 관람객 캐릭터 에셋 조사

확인일: 2026-10-15 · 작업 브랜치: `codex/museum-tycoon`

후속: 일상복 캐릭터를 채택하여 Unity Generic 리그·Walk/Idle_Neutral·실제 skin 변형/화면을 확인하고 관람객에 연결했습니다. [새 외형 추가 방법](VISITOR-EXTENSION.md). 아래 Blender 내용은 최초 조사 기록입니다.

## 첫 후보

**Quaternius Ultimate Modular Men / Women의 일상복 캐릭터**를 추천합니다. 전시실 손님으로 자연스러운 옷차림을 고를 수 있고, 부위별 조합으로 변형하기 좋습니다. 우선 남성 Casual_2 샘플과 Humanoid 버전을 공식 배포 폴더에서 확보했습니다. 여성 팩은 후보 조사만 했으며 아직 다운로드·파일 검증하지 않았습니다.

![실제 확보한 Humanoid 샘플의 외형 미리보기](assets/visitor-casual2-preview.png)

위 이미지는 확보한 FBX를 Blender에서 렌더링한 T-pose 외형 확인용입니다. 박물관 안에서 걷는 화면이나 최종 관람객 디자인은 아닙니다.

## 비교 결과

| 후보 | 공식 안내·파일 확인 | 이번 판단 |
|---|---|---|
| [Quaternius Ultimate Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html) / [Women](https://quaternius.com/packs/ultimatemodularwomen.html) | 소개 기준 남성11·여성10종, 각24애니메이션·부위교환. 남성 일상복 샘플의 실제 파일 검사 수행 | **채택.** 일상복 Generic 리그·재생·PC 표시 확인. 여성 팩은 별도 추가 시 검증 |
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

일반 FBX의 초기 Blender 대기 미리보기에서는 모델이 표시되지 않아 재생 성공으로 판정하지 않았습니다. 이후 Unity Generic import의 실제 skin 변형·크기·Windows 렌더링 검사를 통과했습니다. 위 T-pose 외형 이미지는 Humanoid 버전이며, Unity 실험 앱은 일반 FBX를 사용합니다.

일반 FBX는 현재 Unity `Assets/Museum/Visitors/Source/`에 편입했고 원본은 `third_party/visitors/quaternius-modular-men/`에 보관합니다. Humanoid 버전은 대안으로만 보관하며 현재 관람객은 일반 FBX의 Generic 리그와 자체 클립을 사용합니다. Windows 관람객 빌드·PC 검증 완료, 실기기 검사는 미완료입니다.

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

## 연결 진행과 후속

1. 완료: 일반 FBX의 Unity Generic import, Walk / Idle_Neutral 실제 재생·변형·크기/표시 검사.
2. 완료: 프로필별 리그·클립을 사용하는 이동/정지 혼합 재생 연결.
3. 완료: TYC-001 입장 → 전시품 이동 → 관람 → 퇴장, 0~6명 기본 조절·동선 예외 PC검사.
4. 후속: 여성 일상복·다른 제작자 캐릭터 등으로 다양화하고, 새 에셋마다 자체 동작/표시/성능 검증.

최초 조사 후 **Quaternius 일상복 채택·Unity 통합·관람객 첫 행동 구현과 PC 검증을 완료**했습니다. skin 변형 검사의 최대 vertex 변화0.137273, 표시 크기 약0.69×1.73×0.57m를 확인했습니다. 실제 다른 리그 추가와 Quest 검사는 후속입니다.
