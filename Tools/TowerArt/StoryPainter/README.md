# StoryPainter

스토리 캠페인용 그림을 코드로 그려 PNG로 굽는 .NET 8 콘솔 도구입니다. 외부 패키지가 없고 노이즈는 고정 시드라 같은 코드에서 항상 같은 결과가 나옵니다. Unity 프로젝트 밖(`Tools/`)에 있어 에디터 컴파일 대상이 아닙니다.

| 파일 | 내용 |
| --- | --- |
| `nihilos.png` | 최종 보스 악신 니힐로스(400×480, 왼쪽을 보는 자세, 발이 맨 아래 행). 부서진 지구 조각의 고리, 뿔 왕관과 눈 여러 개가 달린 뼈 가면, 세로로 갈라진 아가리, 갈비뼈 사이의 불타는 입, 촉수로 풀려 나가는 로브 |
| `signpost.png` `stele.png` `shrine.png` `dummy.png` | 등불 마을 소품(96ppu, 바닥 가운데 기준). 튜토리얼 표지판, 이야기 비석, 신화의 제단(되찾은 신화 불꽃 9개 자리), 수련용 허수아비 |

## 실행

프로젝트 루트에서 실행합니다.

```powershell
dotnet run -c Release --project Tools/TowerArt/StoryPainter
```

| 인자 | 동작 |
| --- | --- |
| (없음) | 모든 그림을 `Assets/Demos/Roguelike/Resources/Tower/Story/`에 굽습니다. |
| `--out <폴더>` | 출력 폴더를 바꿉니다. 실험할 때는 임시 폴더를 지정합니다. |
| `--preview <폴더>` | 각 그림의 2배 확대 미리보기를 씁니다. |
| `--zoom x0 y0 크기` | `--preview`와 함께 쓰며, 보스 그림의 한 정사각 영역을 4배로 확대합니다. |

`Editor/TowerStoryArtImporter.cs`가 `Story/` 아래 PNG를 Bilinear·무압축·CPU 사본 없음으로 가져옵니다. 런타임은 `TowerThemeMonsterArt.FinalBoss()`(보스)와 `TowerGame.StorySprite()`(마을 소품, 96ppu)에서 스프라이트를 만듭니다.

## 구조

- `Core.cs` — 색, 디더 램프, 주기 없는 값 노이즈, PNG 인코더, 부위 단위 볼륨 셰이딩(`Img.Part`: 모양을 래스터화하고 가장자리 거리로 부풀린 뒤 법선으로 조명), 튜브·타원 모양
- `Nihilos.cs` — 최종 보스 구성(뒤에서 앞으로: 후광, 뒤 촉수, 로브, 앞 촉수, 먼 팔, 몸통, 가슴 아가리, 목, 머리, 가까운 팔)
- `VillageProps.cs` — 마을 소품