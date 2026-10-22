# BackdropPainter

서약의 탑 7개 테마의 고해상도 패럴랙스 원경과 96ppu 지형 아틀라스를 코드로 그려 PNG로 굽는 .NET 8 콘솔 도구입니다. 외부 패키지가 없고, 모든 노이즈를 고정 시드로 계산하므로 같은 코드에서 항상 같은 결과가 나옵니다. Unity 프로젝트 밖(`Tools/`)에 있어 에디터 컴파일 대상이 아닙니다.

## 실행

프로젝트 루트에서 실행합니다.

```powershell
dotnet run -c Release --project Tools/TowerArt/BackdropPainter -- --terrain
```

| 인자 | 동작 |
| --- | --- |
| (없음) | 7개 테마 원경과 `Backdrops.json`을 `Assets/Demos/Roguelike/Resources/Tower/Themes/Backdrops/`에 굽습니다. |
| `--terrain` | 원경과 함께 `Resources/Tower/Themes/Terrain.png`도 굽습니다. |
| `--terrain-only` | 지형 아틀라스만 굽습니다. |
| `--theme <id>` | 한 테마만 다시 굽고, 매니페스트의 다른 테마 항목은 유지합니다. |
| `--out <폴더>` | 원경 출력 폴더를 바꿉니다. 실험할 때는 임시 폴더를 지정합니다. |
| `--preview <폴더>` | 1080p 지상·높은 카메라·검증 배치를 런타임과 같은 방식으로 합성한 미리보기와 지형 확대본을 씁니다. |

굽고 나면 Unity에서 에셋을 새로고침합니다. `Editor/TowerThemeArtImporter.cs`가 `Backdrops/` 아래 PNG를 BC7·Bilinear·CPU 사본 없음으로, `Terrain.png`를 Point·무압축으로 가져옵니다.

## 구조

- `Core/` — PNG 인코더, 가로로 반복되는 캔버스, 주기 노이즈, 그리기 도구(`Kit`: 그라디언트, 볼륨 셰이딩, 수정·종유석·잎·덩굴·구름·소나무·야자수·반사 등), 고딕 아케이드 모듈(`ArchKit`)
- `Themes/` — 테마별 sky·far·mid·near 레이어
- `TerrainAtlas.cs` — 지면·지하층·발판·끝 마감·소품
- `Preview.cs` — 런타임과 같은 패럴랙스 합성 미리보기

레이어 좌표는 카메라가 기준점 `(7, 1)`에 있을 때 월드 좌표와 일치하며, 다른 위치에서는 `(카메라 - 기준점) × 이동 비율`만큼 따라 움직입니다. 런타임 구현은 `Assets/Demos/Roguelike/Scripts/TowerThemeBackdrop.cs`와 `TowerParallaxLayer.cs`입니다.
