# 에덴 천사 도트 개편 · 자연스러운 비율

실행 파일: `Build/TowerEdenBalanced/TowerEdenBalanced.exe`. 공유할 때는 `TowerEdenBalanced` 폴더 전체를 전달합니다.

미카엘·가브리엘·라파엘·우리엘·메타트론을 3~3.5등신을 목표로 한 청년 체형으로 다듬었습니다. 이전 2등신 버전의 큰 눈·동그란 볼을 줄이고, 팔다리와 갑옷·깃털을 자연스럽게 그렸습니다. 부드러운 표정과 도트 윤곽은 유지합니다. 각자의 금발/은발/갈색/적색/연보라 머리, 검/나팔/치유 지팡이/화염검/기록서가 구분됩니다. 초상화, 신화 선택, 서약 선택, 보상, 파티, 전투가 같은 새 리소스를 사용합니다.

`Assets/Demos/Roguelike/Resources/Tower/Myths/EdenCute/`의 다섯 PNG는 내장 ImageGen으로 제작했습니다. 현재 버전의 전체 프롬프트는 `ArtSources/EdenBalanced/generation-record.json`에 저장했습니다. 앞선 2등신 원본은 `ArtSources/EdenCute/v1/`에 보존했습니다. 활성 PNG의 기존 `.meta`와 GUID는 유지하며, 기존 `eden.png`와 다른 신화의 이미지·측정 데이터도 그대로 둡니다.

## 프레임과 재생

각 시트는 4열 × 4행, 총 16개 포즈입니다.

| 프레임 | 동작 |
| --- | --- |
| 0–1 | 대기, 날개짓 |
| 2–5 | 달리기 4박자 |
| 6–8 | 점프 상승, 낙하, 착지 |
| 9–11 | 공격 준비, 발동, 회수 |
| 12–13 | 시전 준비, 발동 |
| 14–15 | 피격, 주저앉는 패배 |

`TowerEdenArt`는 에덴 인덱스 100–104만 담당합니다. 정지된 그림을 관절로 늘리지 않고 작성된 포즈를 교체합니다. Point 필터, 고정 픽셀 배율, 수평 반전으로 표시하며 45명은 기존 `TowerMythRig`를 사용합니다.

생성 이미지의 무기가 셀 경계를 넘는 경우가 있어 명목상 등분선 주변의 투명한 간격을 먼저 찾습니다. 각 포즈는 그 경계 안에서 불투명 픽셀을 측정해 발밑을 루트 높이에 맞춥니다. 전체 시트의 픽셀 배율은 첫 대기 프레임에서 한 번만 정하므로 웅크림이나 무기를 드는 동작이 몸 전체의 확대·축소를 일으키지 않습니다. 원본 PNG를 가공하지 않습니다.

달리기 주기는 실제 이동 속도와 동료 배율을 반영합니다. 공중 위치는 기존 물리·추적 AI가 결정하고, 그림은 상승·낙하·착지 상태를 따릅니다. 공격은 실제 타격 비율 0.46, 치유·보호막은 실제 시전 비율 0.55에 발동 포즈를 표시합니다. 종료·취소 시 현재 이동 포즈로 돌아갑니다. 일시정지 중 프레임은 진행하지 않습니다.

## 검증

2026-10-21 비율 조정판: `Artifacts/TowerEdenBalanced/review-final/eden-report.json`에서 5명·80포즈·144프레임 검사 통과, `Artifacts/TowerEdenBalanced/ui-final-report.json`에서 16화면 검사 통과. 실제 대기·점프·공격·시전과 서약 선택 화면을 검토했습니다. 미리보기는 `Artifacts/TowerEdenBalanced/review-final/eden-animation.gif`입니다.

`Tests/Run-TowerEdenReview.ps1`은 독립 실행 파일에서 다섯 캐릭터의 80개 포즈 도달, 발밑 높이, 점프·착지, 공격·시전 시점, 회수·취소, 정지 중 좌우 반전, 픽셀 배율과 초상화 경로를 확인합니다. 24fps, 6초의 실제 재생 화면 144장을 저장합니다. 명령줄로는 `-towerEdenReview -towerEdenCapture "<절대 출력 폴더>"`입니다.

기존 Myth 검증은 새 스프라이트와 기존 메쉬를 모두 검사합니다. Companion의 바닥 접촉 검사도 두 렌더러를 구분합니다. Art 검증은 에덴의 프레임 번호와 긴 대기 포즈 주기를 읽습니다. 관절 메쉬만을 검사하는 Animation 검증은 `non-eden-rendered-foot-surfaces-v4` 계약으로 105–149번 45명을 대상으로 하며, 에덴은 위 전용 검증을 실행해야 합니다.

이전 2등신 버전의 결과: `Artifacts/TowerEdenCute/review-final/eden-report.json`의 5명·80포즈·144프레임 통과. `myth-20261020-233127-report.json`의 50명·300상태 표시와 `oath-20261020-233126-report.json`의 서약 전투 검사 통과. UI 16화면도 오류 없이 확인했습니다. `Tests/Render-TowerEdenReview.py <검증 폴더>`로 실제 캡처를 GIF 미리보기로 묶을 수 있습니다.

전체 Art 검증은 에덴 구간을 통과한 뒤 기존 포세이돈(106)의 공격 복귀 검사에서 실패했습니다. 이전 UIRefresh 실행 파일에서도 동일 실패가 재현되었습니다. 비교 보고서는 `Artifacts/TowerEdenCute/art-baseline-ui-report.json`과 `art-20261020-233124-report.json`입니다. 이번 변경에서는 다른 신화의 관절 동작을 수정하지 않았습니다.
