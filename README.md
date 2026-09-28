# Prototype Lab

2인 팀이 장르별 데모를 따로 실험하기 위한 Unity 프로젝트입니다. **HideAndSeek에는 아쿠아리움 멀티플레이 데모가 구현되어 있습니다.** 나머지 장르는 시작용 씬입니다.

## 아쿠아리움 숨바꼭질

`Assets/Demos/HideAndSeek/30_HideAndSeek.unity`를 열고 Play를 누르세요. 각 플레이어의 **3인칭 시점과 3D 수영**, 물고기로 변신·먹기, 동료 삼키기와 1초 후 뱉기, 술래 작살, 승패, AI 연습, Relay 인터넷 방 코드 접속을 지원합니다.

WASD 이동 · 마우스 시점 회전 · Space 상승 / Ctrl 하강 · 휠 거리 조절 · ESC 메뉴. 친구도 최신 3인칭 빌드로 접속해 주세요.

**[실행 방법·조작·인터넷 설정·개발 구조](Docs/Aquarium-Demo.md)**

## 열기

1. Unity Hub에서 **Add project from disk**로 이 폴더를 추가합니다.
2. **Unity 6000.3.21f1**로 엽니다. 동료도 같은 버전을 설치하세요.
3. `Assets/Scenes/00_Hub.unity`를 열고 Play를 누릅니다. 버튼으로 각 데모 씬에 이동할 수 있습니다.

처음 열 때 Unity가 `Library` 폴더와 에셋 메타데이터를 생성하므로 시간이 걸릴 수 있습니다.
첫 실행 때 Console에 컴파일 오류가 없는지 확인하고, 허브에서 세 데모 씬으로 이동해 보세요.

## 씬과 작업 폴더

| 씬 | 작업 폴더 | 용도 |
| --- | --- | --- |
| `Assets/Scenes/00_Hub.unity` | `Assets/Scenes` | 데모 선택 |
| `Assets/Demos/Survivors/10_Survivors.unity` | `Assets/Demos/Survivors` | 뱀서류 실험 |
| `Assets/Demos/Roguelike/20_Roguelike.unity` | `Assets/Demos/Roguelike` | 로그라이크 실험 |
| `Assets/Demos/HideAndSeek/30_HideAndSeek.unity` | `Assets/Demos/HideAndSeek` | 숨바꼭질 실험 |

공통 에셋과 스크립트는 `Assets/Shared`에 둡니다. 현재의 `DemoNavigation.cs`는 빈 씬을 이동하기 위한 임시 UI입니다. 각 데모의 핵심 게임 플레이가 준비되면 교체해도 됩니다.

## SourceTree로 함께 쓰기

이 폴더는 `main` 브랜치와 첫 커밋이 있는 Git 저장소입니다. 아직 원격 저장소는 연결하지 않았습니다.

1. 한 사람이 GitHub/Bitbucket 등에 **빈 비공개 저장소**를 만듭니다. 자동 README나 `.gitignore`는 추가하지 않습니다.
2. SourceTree에서 이 폴더를 **Add**로 열고, 저장소 설정에 원격 URL을 추가한 다음 `main`을 Push합니다.
3. 친구를 저장소에 초대합니다. 친구는 SourceTree에서 Clone 후 Unity Hub에 복제한 폴더를 추가합니다.
4. 한 장르를 함께 실험할 때는 `main`에서 `feature/survivors` 같은 브랜치를 만들어 작업하고, 검토 후 `main`에 병합합니다. 다른 장르는 `feature/roguelike`, `feature/hide-and-seek`처럼 분리할 수 있습니다.

씬이 폴더별로 분리되어 있으므로 서로 다른 씬을 맡아 작업하면 충돌이 줄어듭니다. 같은 씬을 동시에 수정할 때는 먼저 담당을 정하세요. `.meta` 파일은 에셋과 함께 반드시 커밋하고, `Library`, `Temp`, `Logs`, `UserSettings`는 커밋하지 않습니다.

큰 이미지·오디오·3D 파일이 늘어나면 두 사람 모두 Git LFS를 설치한 뒤, 어떤 파일 형식을 LFS로 추적할지 함께 정하세요. 이 시작 프로젝트에는 LFS를 강제로 설정하지 않았습니다.
