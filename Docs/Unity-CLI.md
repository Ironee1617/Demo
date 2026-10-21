# Unity CLI 에디터 연결

프로젝트는 Unity 6000.3.21f1과 공식 `com.unity.pipeline` **0.8.0-exp.1**을 사용합니다. Pipeline은 실험 버전이며 버전을 `Packages/manifest.json`에 고정합니다. Unity CLI는 각 개발 PC에 설치하는 별도 도구입니다. 이 연결에는 Python, UV 또는 Coplay Unity MCP 패키지가 필요하지 않습니다.

## 연결

Coplay Unity MCP를 사용하던 프로젝트에서 이 패키지 구성으로 전환할 때는 Unity 에디터를 종료한 상태에서 변경 사항을 받습니다. 열린 에디터에서 패키지가 제거되면 `Packages/com.coplaydev.unity-mcp/package-icon.png.meta` 경로를 찾지 못할 수 있습니다. 이 경우 프로젝트를 다시 실행해 변경된 패키지 목록을 읽도록 합니다.

1. Unity CLI를 설치하고 `unity --version`으로 확인합니다.
2. 프로젝트를 지정된 Unity 버전으로 열고 패키지 가져오기와 컴파일이 끝날 때까지 기다립니다. 에디터가 백그라운드에 있었다면 창을 한 번 활성화합니다. 자동 새로고침을 꺼 두었다면 `Assets > Refresh`(Ctrl+R)로 패키지 변경을 반영합니다.
3. 프로젝트 루트의 PowerShell에서 다음 명령을 실행합니다.

```powershell
$projectRoot = (Get-Location).Path
unity pipeline list --json
unity status --project-path "$projectRoot" --until-ready --timeout 60 --json
unity command --project-path "$projectRoot" --json editor_status
unity command --project-path "$projectRoot" --json list_open_scenes
```

Pipeline 패키지 설치 여부와 실행 중인 서버 연결 여부는 별개입니다. `pipeline list`에서 해당 프로젝트의 `pipelineServer.isReachable`이 `true`이고 `status`가 연결된 에디터를 반환해야 제어할 수 있습니다. 서버가 꺼져 있으면 Unity의 `Window > Pipeline > Start Server`를 확인합니다.

## 에디터 제어

```powershell
unity command --project-path "$projectRoot" --query editor_play --json
unity command --project-path "$projectRoot" --json editor_play
unity status --project-path "$projectRoot" --until-ready --timeout 60 --json
unity command --project-path "$projectRoot" --json editor_pause
unity command --project-path "$projectRoot" --json editor_stop
unity status --project-path "$projectRoot" --until-ready --timeout 60 --json
unity recompile --project-path "$projectRoot" --timeout 120 --json
```

`editor_pause`는 토글이므로 현재 정지 상태를 먼저 확인합니다. Play/Stop이나 재컴파일 중에는 도메인 재로드로 연결이 잠시 끊길 수 있으며, 다음 명령 전에 `status --until-ready`로 준비 상태를 확인합니다. 자동 제어 후에는 원래의 실행·일시정지 상태를 복구합니다. 열린 씬의 저장되지 않은 변경을 자동 저장하거나 폐기하지 않습니다.

기존 `Tests/Build-TowerDemo.ps1`의 `Unity.exe -batchmode -executeMethod` 빌드는 독립 검증 프로젝트를 사용하는 별도 경로입니다. 현재 프로젝트가 열려 있을 때 같은 소스 디렉터리를 다른 Unity 프로세스로 동시에 열지 않습니다.

## 공유 파일

팀에 공유하는 파일은 `Packages/manifest.json`, `Packages/packages-lock.json` 및 이 안내입니다. 연결 정보는 각 PC에서 자동 생성됩니다. Editor 연결 파일은 `Library/Pipeline/` 아래에 있으며 Git에서 제외합니다. Runtime 연결 파일과 빌드 중 생성되는 Pipeline 리소스도 `.gitignore`에서 제외합니다. 이번 연결은 에디터용이며, 빌드에 Runtime 서버를 활성화하지 않습니다.

## Codex MCP 연결

같은 공식 Unity CLI를 MCP 서버로도 사용할 수 있습니다. 프로젝트 루트의 PowerShell에서 다음 명령으로 이 프로젝트에 연결할 서버를 등록합니다.

```powershell
$projectRoot = (Get-Location).Path
$unityCli = (Get-Command unity).Source
codex mcp add unityMCP -- "$unityCli" mcp --project-path "$projectRoot"
codex mcp get unityMCP
```

Codex의 사용자 설정에 있는 기존 `unityMCP` 항목을 공식 CLI의 stdio 서버로 교체합니다. 사용자 설정 파일과 로컬 인증 토큰은 저장소에 넣지 않습니다. 새 MCP 설정은 Codex의 새 연결에서 도구 목록에 반영됩니다. 기존 Coplay Unity MCP, Python 또는 UV를 다시 설치하지 않습니다.

2026-10-19에 실제 stdio MCP 초기화, 도구 160개 조회, `editor_status` 호출을 확인했습니다. 응답은 Unity `6000.3.21f1`, `ready`, 실행 중지 상태였습니다.

## CLI로 로그라이크 전투 검증

`20_Roguelike` 씬을 열고 Play를 중지한 상태에서 실행합니다. 각 시나리오는 CLI로 Play에 진입해 실제 게임 Update를 실행하고 종료 후 Stop으로 돌아갑니다.

```powershell
./Tests/Run-TowerEditorSmoke.ps1 -Suite Companions
./Tests/Run-TowerEditorSmoke.ps1 -Suite Movement
./Tests/Run-TowerEditorSmoke.ps1 -Suite Platforms -TimeoutSeconds 900
./Tests/Run-TowerEditorSmoke.ps1 -Suite Combat
./Tests/Run-TowerEditorSmoke.ps1 -Suite Boss
```

`-Suite All`은 Platforms·Companions·Combat·Boss·Polish 다섯 시나리오를 차례로 실행합니다. `-Suite Polish`는 회피 반격과 무피격 기록, 층 클리어 후 잔류 탄환 제거를 검사합니다. 결과 JSON과 화면은 로컬 `Artifacts/TowerEditor/`에 저장됩니다. 고정 배치·체력·동료 구성 등 검증용 보정은 보고서와 검증 컴포넌트에 명시하며 일반 플레이에는 적용하지 않습니다.

2026-10-19 최종 실행은 동료 25개 사례, 일반 전투 24개 판정, 보스 26개 판정을 통과했습니다. 실제 피해·쿨타임·탄막과 수정한 두 회귀 사례의 범위는 [전투 검증 기록](../Artifacts/TowerEditor/README.md)에 정리했습니다.

## 연결 검증 기록

2026-10-19 KST, Unity CLI `1.0.0-beta.13`과 Unity `6000.3.21f1`에서 확인했습니다.

- Pipeline `0.8.0-exp.1`의 로컬 서버 연결과 에디터 `ready` 상태를 확인했습니다.
- CLI `recompile`은 `up_to_date`, 오류 0개를 반환했습니다.
- `20_Roguelike` 씬을 CLI로 Play하여 게임 인스턴스 1개와 프레임 `1 → 38` 진행을 확인했습니다.
- Pause에서 프레임 `46`이 유지되고, Resume 후 `52 → 90`으로 다시 진행했습니다.
- Stop 후 원래의 실행 중지·일시정지 해제 상태와 백그라운드 실행 값을 복구했습니다.
- 열린 씬 경로·활성 상태·저장되지 않은 변경 여부는 검증 전후 같았고, 새 콘솔 오류는 0개였습니다.

## 공식 문서

- [Unity CLI 설치·사용](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [Unity Pipeline 설치](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [연결과 로컬 파일](https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/connectivity.html)
- [에디터 제어 명령](https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/commands/editor-lifecycle-and-observability.html)
- [Codex MCP 설정](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)
