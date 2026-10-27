# StoryPainter

스토리 캠페인용 그림을 코드로 그려 PNG로 굽는 .NET 8 콘솔 도구입니다. 외부 패키지가 없고 노이즈는 고정 시드라 같은 코드에서 항상 같은 결과가 나옵니다. Unity 프로젝트 밖(`Tools/`)에 있어 에디터 컴파일 대상이 아닙니다.

| 파일 | 내용 |
| --- | --- |
| `nihilos.png` | 최종 보스 악신 니힐로스(400×480, 왼쪽을 보는 자세, 발이 맨 아래 행). 부서진 지구 조각의 고리, 뿔 왕관과 눈 여러 개가 달린 뼈 가면, 세로로 갈라진 아가리, 갈비뼈 사이의 불타는 입, 촉수로 풀려 나가는 로브 |
| `signpost.png` `stele.png` `runestone.png` `shrine.png` `dummy.png` | 등불 마을 소품(96ppu, 바닥 가운데 기준). 튜토리얼 표지판, 이야기 비석, 스킬트리 석판(앞면에 축소한 스킬트리, 일부 룬만 켜짐), 신화의 제단(되찾은 신화 불꽃 9개 자리), 수련용 허수아비 |
| `Breakables/<테마>-*.png` | 탑 9개 테마의 파괴 오브젝트(96ppu, 바닥 가운데 기준). 테마마다 한 방에 깨지는 그릇 하나와 두 방(마무리 일격은 한 방)에 부서지는 상자 하나: 올림포스 암포라·청동 궤짝, 아스가르드 얼음덩이·벌꿀술 통, 단군 옹기·볏짚 가마니, 에덴 선악과 바구니·덩굴 궤짝, 베다 흙 물동이·놋쇠 칼라샤, 파피루스 카노푸스 단지·채색 궤, 중국 청화 자기·붉은 칠기 함, 지옥 해골 유골함·불탄 형틀 상자, 심연 알주머니·공허 수정 |
| `Breakables/heart.png` `coin.png` `gold-coin.png` | 오브젝트가 흘리는 하트(최대 생명력 12% 회복), 동전(1 G), 청록 보석 금화(5 G). 동전 그림은 HUD 골드 표시 아이콘으로도 씁니다. |

## 실행

프로젝트 루트에서 실행합니다.

```powershell
dotnet run -c Release --project Tools/TowerArt/StoryPainter
```

| 인자 | 동작 |
| --- | --- |
| (없음) | 스토리·마을 그림은 `Assets/Demos/Roguelike/Resources/Tower/Story/`, 파괴 오브젝트와 전리품은 `.../Tower/Breakables/`에 굽습니다. |
| `--out <폴더>` | 출력 폴더를 바꿉니다(두 종류 모두 이 폴더로). 실험할 때는 임시 폴더를 지정합니다. |
| `--preview <폴더>` | 각 그림의 2배 확대 미리보기와, 파괴 오브젝트·전리품을 한 장에 모은 `breakables-sheet.png`를 씁니다. |
| `--zoom x0 y0 크기` | `--preview`와 함께 쓰며, 보스 그림의 한 정사각 영역을 4배로 확대합니다. |

`Editor/TowerStoryArtImporter.cs`가 `Story/`와 `Breakables/` 아래 PNG를 Bilinear·무압축·CPU 사본 없음으로 가져옵니다. 임포터보다 PNG가 먼저 들어가면 기본 설정(압축·밉맵)으로 임포트되니, 그때는 해당 PNG를 강제 재임포트합니다. 런타임은 `TowerThemeMonsterArt.FinalBoss()`(보스), `TowerGame.StorySprite()`(마을 소품, 96ppu), `TowerGame.LootSprite()`(파괴 오브젝트와 전리품, 96ppu, 부서질 때 그림을 3×3 조각으로 잘라 파편으로 씀)에서 스프라이트를 만듭니다.

## 구조

- `Core.cs` — 색, 디더 램프, 주기 없는 값 노이즈, PNG 인코더, 부위 단위 볼륨 셰이딩(`Img.Part`: 모양을 래스터화하고 가장자리 거리로 부풀린 뒤 법선으로 조명), 튜브·타원 모양
- `Nihilos.cs` — 최종 보스 구성(뒤에서 앞으로: 후광, 뒤 촉수, 로브, 앞 촉수, 먼 팔, 몸통, 가슴 아가리, 목, 머리, 가까운 팔)
- `VillageProps.cs` — 마을 소품
- `Breakables.cs` — 테마별 파괴 오브젝트(돌림판 그릇 `Lathe`, 상자 `Chest`, 판자 결 `Planks` 같은 공용 도우미)와 하트·동전