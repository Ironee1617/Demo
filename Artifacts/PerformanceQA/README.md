# 2026-10-17 추가 최적화 측정

기준 커밋은 `3e12b65`입니다. 동일 PC·Unity `6000.3.21f1`에서 수정 전후를 비교했습니다. 아래 표는 개별 처리 비용과 CPU 게임 루프 측정입니다.

**Player 프로파일은 배치 모드에서 자동 카메라 렌더링이 생략된 CPU 게임 루프 벤치마크입니다.** 두 보고서 모두 draw calls·triangles·render time이 0이고 GPU timing은 미지원입니다. 표시 해상도 1440×900과 RTX 4060 장치 정보가 있어도 이 결과를 화면 FPS·GPU 성능으로 해석하지 않습니다. 환경 렌더링은 별도의 명시적 `Camera.Render` 검사로 확인했습니다.

## 변경 범위

- 수조·통로의 보수적 거리 캐시로 확실한 자유 공간을 빠르게 판별합니다. 경계 근처는 기존 정밀 판정을 사용하고, 석회암 지형에 닿을 가능성이 없는 광선은 세부 검사를 생략합니다.
- 촉수 포식의 도달 가중치가 0인 구간에서 최종 자세에 영향을 주지 않는 곡선 풀이를 생략합니다. 표시용 플레이어·물고기 키와 기본 세 종의 문자열도 재사용합니다.
- 호스트 표시와 전송에 같은 스냅샷을 사용하고, 원격 4명에게 JSON·버퍼를 한 번 준비해 배치 전송합니다. 입장 welcome은 새 상태를 즉시 캡처하며 프로토콜은 v6입니다.
- 환경 메시 12개의 과도한 경계를 줄이고 라벨 머티리얼을 79개에서 1개로 공유합니다. 메시 정점·텍스처·수영·포식 표현은 유지합니다.

## 시뮬레이션

고정 시드 73, 물고기 32마리, 30회 준비 뒤 1/30초 틱 180회를 측정했습니다. [수정 전](before/model.txt) · [수정 후](after/model.txt)

| 한 틱 평균 | 수정 전 | 수정 후 |
|---|---:|---:|
| 순항 | 1.127ms | 0.295ms |
| 먹이 유인 | 1.173ms | 0.318ms |
| 밀집 먹이 유인 | 16.578ms | 2.215ms |
| 플레이어가 있는 순항 | 1.340ms | 0.411ms |

밀집 먹이 유인의 평균은 **86.6% 감소**했고 p95는 18.471→2.663ms입니다.

## Player CPU 게임 루프

장면마다 5초 중 초기 0.8초를 제외했습니다. 평균 GC 할당은 Player의 `GC Allocated In Frame` 카운터입니다. [수정 전](before/player-frames.json) · [수정 후](after/player-frames.json)

| 장면 | CPU 프레임 p95, 전→후 | 평균 GC 할당 B/frame, 전→후 |
|---|---:|---:|
| 수영 | 3.596→1.313ms | 5,701→1,668 |
| 물고기 포식 | 3.554→1.323ms | 5,779→1,699 |
| 촉수 포식 | 3.647→1.362ms | 5,793→1,744 |
| 동료 삼킴 | 3.536→1.339ms | 5,789→1,700 |
| 밀집 물고기 | 35.636→1.430ms | 6,485→1,679 |

수영의 평균 GC 할당은 **70.7% 감소**했습니다. 밀집 장면의 CPU 프레임 최댓값은 70.588→7.775ms입니다. 이 표는 실제 화면 렌더링을 포함한 FPS 검증을 대신하지 않습니다.

## 포식·네트워크·캐시

같은 Editor 프로세스에서 기준 구현과 현재 구현을 비교했습니다. [촉수 보고서](after/hotpath/hotpath-checks.txt) · [네트워크·캐시 보고서](after/network/network-performance-checks.txt)

| 처리 | 기준 구현 | 현재 구현 |
|---|---:|---:|
| 촉수 포식 전체 시간 구간의 평균 호출 | 84.832μs | 60.306μs |
| 촉수 포식 시작 구간 | 86.437μs | 5.921μs |
| 원격 4명용 상태 패킷 준비 | 617.000μs | 151.534μs |
| 준비된 플레이어 키 조회 | 0.343μs | 0.067μs |

촉수가 실제로 뻗는 구간은 85.342→86.058μs로 비슷합니다. 최적화는 가중치 0인 시작·종료 구간에 적용되며, 1,953개 표본에서 관절 위치·회전 차이는 0입니다. 네트워크 검사는 틱당 스냅샷 1회, 같은 v6 패킷의 네 수신자 일치, welcome 시퀀스, 라운드 변경 시 캐시 초기화를 확인했습니다. 패킷 준비 비교는 실제 네트워크 왕복 지연 측정이 아닙니다.

단독 Snapshot 호출 시간은 1.389→2.060μs로 늘었습니다. 중복 호출을 없앤 전송 경로의 전체 준비 비용은 위 표처럼 줄었습니다.

이 Unity Mono Editor의 할당 카운터는 신뢰할 수 없었습니다. 65,536바이트 배열 probe가 100으로 기록되어 네트워크 검사에서는 `allocation_comparison=unsupported`, 개별 bytes는 `unsupported`로 남겼습니다. 촉수 검사도 할당 계측 미지원을 명시합니다. Editor 값으로 할당 감소율을 계산하지 않았습니다.

## 환경 렌더링과 품질

카메라 3개 × 고정 시점 2개에서 각각 준비 렌더 3회 뒤 12회를 측정했습니다. `Camera.Render`와 1픽셀 `ReadPixels` 동기화를 합친 경과 시간으로, CPU·GPU·읽기 비용이 섞여 있습니다. 원본 순서·중앙값·p95는 [JSON](after/environment/environment-render-benchmark.json)에 보존했습니다.

| 카메라 / 고정 시간 | 수정 전 중앙값 | 수정 후 중앙값 |
|---|---:|---:|
| 0 / 0.7초 | 8.550ms | 7.624ms |
| 0 / 3.1초 | 7.939ms | 7.956ms |
| 1 / 0.7초 | 8.719ms | 8.523ms |
| 1 / 3.1초 | 8.888ms | 8.506ms |
| 2 / 0.7초 | 9.334ms | 8.752ms |
| 2 / 3.1초 | 9.210ms | 8.750ms |

변화 폭이 작고 수정 전을 먼저 측정하여 캐시·측정 순서의 영향을 받습니다. 한 조건은 미세하게 느려졌으며 GPU 개선율이나 draw call 감소로 단정하지 않습니다. Editor draw/triangle 카운터의 0은 미지원 값입니다. frustum 후보 삼각형 수 역시 실제 제출량의 추정치입니다.

초기 환경 준비에 **148.02ms**가 추가됩니다. 여섯 비교 화면 모두 **변경 픽셀 0개**로 품질을 유지했습니다. [환경 보고서와 전후 화면](after/environment/environment-performance-checks.txt)

## 재현·보고서 상태

- 모델: `Tests/Run-ModelPerformance.ps1`, 규칙: `Tests/Run-ModelTests.ps1`.
- Unity 통합 검사: `PrototypeLab.Aquarium.Editor.AquariumPerformanceChecks.Run`. 빌드까지는 `RunAndBuild`.
- Player 측정: Development Build에서 `-aqPerfReport <절대 JSON 경로>` 사용. 배치 모드 결과의 렌더 카운터 지원 여부를 먼저 확인합니다.
- [after/rules.txt](after/rules.txt)는 게임 규칙 84개 통과를 기록합니다.

최종 Windows 실행 파일 smoke도 확인했습니다. [호스트](after/smoke/host.json)·[클라이언트](after/smoke/client.json)는 모두 `connected=true`, 오류 0이며 클라이언트는 스냅샷 455회를 수신했습니다. [먹이 유인 음향](after/smoke/feed.json)은 근거리 재생·원거리 무음·범위 이탈 후 중단을, [변신 대상](after/smoke/target.json)은 선택과 단일 윤곽선을, [종료 화면](after/smoke/terminal.json)은 세계 정지와 자유 카메라를 확인했습니다. 세 시나리오 모두 오류 0입니다.

보존한 [before/frames.json](before/frames.json)은 탐색용 Editor 실행으로, SearchDatabase 시작 과정의 예외가 포함되어 전후 성능 비교에서 제외했습니다. [before/hotpath-checks.txt](before/hotpath-checks.txt)의 `0 bytes`도 미지원 계측이므로 할당 근거로 사용하지 않습니다. 신뢰하는 전후 기준은 **`before/after/player-frames.json`과 `before/after/model.txt`**이며 기존 원본 보고서는 수정하지 않았습니다.
