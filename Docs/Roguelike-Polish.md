# 서약의 탑 · 2026-10-20 개선 기록

실행: `Build/TowerPolish/TowerPolish.exe`. 다른 PC에 전달할 때는 `TowerPolish` 폴더 전체를 복사합니다. Unity에서는 `Assets/Demos/Roguelike/20_Roguelike.unity`를 실행합니다.

기존 25층 횡스크롤 로그라이크, 선택한 신화의 동료, 매 층 3장 중 1장 증강, 등급 확률 40/40/20, 5층 테마와 보스 구조를 유지했습니다. 기존 작업 중인 캐릭터·테마·발판 변경 위에 적용했습니다.

## 추가

- **회피 반격:** Shift 회피 첫 0.12초 안에 실제 적 탄환을 통과하면 3초간 반격 준비. 다음 적중 기본 검격 피해 ×1.5. 회피당 한 번, 중첩 없음, 헛스윙 유지, 피격·층 전환 시 해제. 자동 스킬과 동료 피해는 그대로입니다.
- **전투 기록:** 보상 화면의 층 전투 시간·무피격 연속 기록, 종료 화면의 무피격 층 수·최고 연속·순간 회피·반격 적중·체력 피해 합계. 보호막 접촉도 무피격을 끊습니다. 이번 등반 기록이며 영구 저장/추가 재화는 없습니다.
- **회피 HUD:** 준비/재사용 대기와 반격 남은 시간을 화면 왼쪽 아래에서 확인합니다.
- **특성 비교:** 보상 특성에 현재 능력치와 선택 후 실제 능력치를 표시합니다. 상한 직전의 일부 증가분도 정확하게 표시합니다.

## 수정·제거

- 관통탄이 한 프레임에 여러 적을 지나가도 한 명에게만 피해를 주던 문제를 수정했습니다. 실제 충돌 순서대로 처리하고 이미 맞은 적은 중복 피해에서 제외합니다.
- 적 생성 순서 때문에 앞의 적을 건너뛰고 뒤의 적을 맞히던 문제를 수정했습니다. 폭발 피해로 적 목록이 바뀌어도 안전하게 처리합니다.
- 탄환 수명/사거리가 끝난 뒤 프레임의 남은 이동만큼 더 피해를 주던 문제를 수정했습니다.
- 층 클리어 후 보상창에서 멈췄다가 재개하던 잔류 탄환을 제거했습니다.
- 정상 25층 등반에서 효과가 더 늘지 않는 상한 특성을 후보에서 제외했습니다. 치명타 80%, 흡혈 60%, 스킬/회피 재사용 감소 75%는 유지합니다. 테스트용 초장기 등반에서 같은 등급의 유효한 고유 후보가 전부 소진된 경우에만 기존 후보를 허용하여 세 장/새로고침 규칙을 유지합니다.
- 신화 초상화는 아틀라스를 직접 그립니다. 일반 HUD에서 50개의 임시 카메라·RenderTexture·GPU ReadPixels 및 초상화 텍스처 생성 경로를 제거했습니다. 기존 캡처 검증용 Texture2D API는 유지했습니다.
- 탄환 및 기본 검격의 반복 `enemies.ToArray()`와 적 탄환 개수 확인용 `FindAll()` 배열/목록 할당을 제거했습니다. 충돌 후보 버퍼는 재사용합니다.
- 출구의 SpriteRenderer를 캐시하여 매 프레임 자식 검색과 배열 할당을 제거했습니다. 출구 상태가 바뀔 때만 색을 갱신합니다.
- 사용되지 않던 `Flash` 목록·갱신 루프를 제거하고 게임 인스턴스가 만든 효과음 AudioClip 3개를 종료 시 해제합니다.

## 검증

검증용 시나리오는 저장/구매 내역을 기록하지 않습니다. 에디터 자동 검증 후 원래 씬의 실행 중지 상태를 복원합니다. 독립 빌드는 `.validation/TowerDemoProject`에 실제 파일을 복사하며 원본 Assets/Packages와 링크하지 않습니다.

- 모델: 성장·보상·신화·저장 30개, 발판 5개, 테마 8개, 난이도 6개 통과.
- Unity Combat: 충돌 순서, 동일 프레임 관통/처치, 중복 타격, 폭발 처치, 사거리 종료, 기존 이동/진형/공중 대상/탄막 검증 통과.
- Unity Polish: 반격 피해 42 대 일반 검격 28, 회피당 한 번, 헛스윙 보존, 스킬·동료 제외, 일시정지/만료, 새 등반 초기화, 무피격 연속·보호막/실제 피해, 클리어 후 탄환 제거 검증 통과. 4층 기록 시나리오는 빠른 상태 설정을 쓰므로 25층 자연 플레이 밸런스 검증은 아닙니다.
- Unity Boss: 25층의 구성/성장 수치, 5개 보스 층, 실제 보스·부하 전투, 패턴/일시정지와 보상 1회 처리 검증 통과.
- Windows 실행본 빌드 성공. 실행본에서 첫 층 실제 전투, 16개 스킬 효과, 영입/해고/보상/25층 완료 흐름과 별도 Polish 검증 모두 통과했습니다. 기본 시나리오는 1회 체력 보정과 후속 24층 상태 설정을 사용하므로 자연 플레이 완주 시험은 아닙니다.
- 최초 실행본 Polish 검사의 동일 프레임 `Update()` 반복이 프레임 시간에 의존해 실패했습니다. 검사 코드를 실제 프레임 진행으로 수정했고, 게임 로직 변경 없이 에디터와 실행본에서 3초 만료 검사가 모두 통과했습니다.
- 반격 준비·보상·종료 및 신화 선택 화면 캡처를 확인했습니다. 에디터 프로브에서 HUD용 신화 초상화 ReadPixels 텍스처 캐시 0개, 환경 `Tick` 1,000회 관리 메모리 할당 0바이트를 확인했습니다. 전체 게임의 FPS 향상률은 계측하지 않았습니다.

로컬 결과: [최종 검증 요약](../Artifacts/TowerPolish/verification.json), [전투 보고서](../Artifacts/TowerEditor/combat-20261020-124630-report.json), [보스 보고서](../Artifacts/TowerEditor/boss-20261020-124959-report.json), [반격 에디터 보고서](../Artifacts/TowerEditor/polish-20261020-125334-report.json), [실행본 반격 보고서](../Artifacts/TowerDemo/polish-report.json), [실행본 게임 흐름 보고서](../Artifacts/TowerDemo/playtest-report.json).

재현 명령:

```powershell
./Tests/Run-TowerRunTests.ps1
./Tests/Run-TowerPlatformTests.ps1
./Tests/Run-TowerThemeTests.ps1
./Tests/Run-TowerDifficultyTests.ps1
./Tests/Run-TowerEditorSmoke.ps1 -Suite Combat
./Tests/Run-TowerEditorSmoke.ps1 -Suite Polish
./Tests/Run-TowerEditorSmoke.ps1 -Suite Boss
./Tests/Run-TowerSmoke.ps1 -Executable ./Build/TowerPolish/TowerPolish.exe -PolishOnly
```
