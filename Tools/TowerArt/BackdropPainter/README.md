# BackdropPainter

서약의 탑 10개 테마(올림포스·아스가르드·단군신화·에덴·베다·파피루스·중국신화·지옥·심연·등불 마을)의 고해상도 패럴랙스 원경과 조명 지형 키트를 PNG로 굽는 .NET 8 콘솔 도구입니다. 장면은 거리 함수(SDF)로 모델링하고 오프라인 레이 마칭으로 렌더링합니다. 외부 패키지가 없고 모든 노이즈를 고정 시드로 계산하므로, 같은 코드에서는 항상 같은 결과가 나옵니다. Unity 프로젝트 밖(`Tools/`)에 있어 에디터 컴파일 대상이 아닙니다.

## 실행

프로젝트 루트에서 실행합니다. 전체 베이크는 테마당 약 2.5분 걸립니다.

```powershell
dotnet run -c Release --project Tools/TowerArt/BackdropPainter -- --kits
```

| 인자 | 동작 |
| --- | --- |
| (없음) | 10개 테마 원경과 `Backdrops.json`을 `Assets/Demos/Roguelike/Resources/Tower/Themes/Backdrops/`에 굽습니다. |
| `--kits` | 원경과 함께 조명 지형 키트를 `Resources/Tower/Themes/Terrain/<id>-top/-body/-slab/-cap/-props.png`로 굽습니다. |
| `--terrain-only` | 원경은 건너뜁니다. `--kits`와 함께 쓰면 키트만 굽습니다. |
| `--terrain` | `--kits` 없이 쓰면 예전 2D 지형 아틀라스 `Terrain.png`도 굽습니다(조명 키트가 없는 테마의 대체용). |
| `--theme <id>` | 한 테마만 다시 굽고, 매니페스트의 다른 테마 항목은 유지합니다. |
| `--fast` | 절반 해상도, 픽셀당 2샘플로 렌더링합니다. 미리보기 전용이며 에셋은 쓰지 않습니다. |
| `--out <폴더>` | 원경 출력 폴더를 바꿉니다. 키트는 `<폴더>/../Terrain`에 씁니다. 실험할 때는 임시 폴더를 지정합니다. |
| `--preview <폴더>` | 1080p 지상·높은 카메라·검증 배치를 런타임과 같은 방식으로 합성한 미리보기, 레이어별 PNG, 키트 확대본을 씁니다. |
| `--probe <id> <x> <v>` | 해당 테마 지형 상면의 한 점에서 그림자·AO·조명 항목을 출력하고 끝냅니다. |

빠른 확인 예:

```powershell
dotnet run -c Release --project Tools/TowerArt/BackdropPainter -- --theme village --fast --preview <임시 폴더>
```

굽고 나면 Unity에서 에셋을 새로고침합니다. `Editor/TowerThemeArtImporter.cs`는 `Backdrops/` 아래 PNG를 BC7·Bilinear·CPU 사본 없음으로 가져오고, `Terrain/` 아래 키트는 CompressedHQ·Bilinear·밉맵·Repeat로 가져옵니다.

## 구조

- `Render/` — 실사풍 렌더러
  - `Renderer.cs`: 직교 카메라(약간 내려다봄) 구 추적, 부드러운 그림자, AO, GGX 반사, 잎 투과, 수면 반사, 볼륨(구름·연기·안개·폭포·빛줄기), 대기 원근, 높이 안개, 경계 적응 3×3 슈퍼샘플링, 블룸, ACES 톤 매핑
  - `Scene.cs`: 경계 상자를 가진 물체와 볼륨, 열 단위 후보 목록. x는 레이어 반복 폭으로 감깁니다.
  - `Sdf.cs`, `RMath.cs`, `Noise.cs`: 거리 함수, 수학, x 주기 노이즈
  - `Mats.cs`: 대리석·암석·사암·목재·도장·금속·잎·지면·구름·눈·물·발광·용암·기와·문양 재질
  - `Kit3D.cs`: 하늘(그라디언트·태양·별·오로라·권운), 기둥·계단·룬·상형문자·육망성·사슬·수관·나무·산·구름바다
  - `Bake.cs`: 레이어 렌더링과 위아래 단색 채움. `TerrainBake.cs`: 지형 키트 베이크와 몸체 타일(석축·잡석·흙)
- `Themes3D/` — 테마별 `*Scene.cs`(sky·far·mid·near)와 `*Terrain.cs`(상면 띠·몸체 타일·발판·끝 마감·소품 3종)
- `Program.cs` — 테마·키트 배열. 순서는 런타임 `TowerThemeId`(Olympus … Chinese, Hell, Abyss, Village)와 같아야 합니다.
- `Preview.cs` — 런타임과 같은 패럴랙스 합성 미리보기
- `Core/`, `Themes/`, `TerrainAtlas.cs` — 예전 2D 화풍의 그리기 도구와 지형 아틀라스. 조명 키트가 없는 테마의 대체용으로 남겨 두었습니다.

레이어 좌표는 카메라가 기준점 `(7, 1)`에 있을 때 월드 좌표와 일치하고, 다른 위치에서는 `(카메라 - 기준점) × 이동 비율`만큼 따라 움직입니다. sky 레이어(이동 비율 0.95)는 게임 카메라 이동 범위 전체에서 대략 x = -4 … 18 구간만 보이므로, 달·행성·거대한 눈 같은 주요 요소는 이 구간에 둡니다. 런타임 구현은 `Assets/Demos/Roguelike/Scripts/TowerThemeBackdrop.cs`, `TowerParallaxLayer.cs`, `TowerLitTerrain.cs`입니다.

## 지형 키트 규약

키트는 걷는 선을 y = 0으로 두고 그립니다. 상면은 z = -0.6(앞 가장자리)부터 0.6까지이고, 카메라는 10도 내려다봅니다. 런타임 `TowerLitTerrain.cs`의 상수와 같아야 합니다.

| 텍스처 | 반복 | 세로 범위 |
| --- | --- | --- |
| `-top` | 가로 12유닛 | 선 위 0.25 ~ 아래 1.25(아래 0.3은 투명으로 사라짐) |
| `-body` | 가로·세로 6유닛 | 상면 띠 아래에서 바닥까지 월드 정렬 |
| `-slab` | 가로 6유닛 | 선 위 0.25 ~ 아래 1 |
| `-cap` | 끝 마감 0.75유닛 폭 | 발판과 같음(왼쪽 끝은 좌우 반전) |
| `-props` | 1.5유닛 칸 3개 | 발이 칸 아래 0.1 지점 |
