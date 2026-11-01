# UIPainter

서약의 탑 UI 키트(금박 액자·카드 틀·문장·버튼·슬롯·초상화 틀·메달·게이지·구분선·키캡·장식·스킬트리 노드·아이콘)와 타이틀 화면의 밤하늘 배경을 PNG로 굽는 .NET 8 콘솔 도구입니다. 모든 모양은 거리 함수(SDF)로 그리고, 안쪽으로 솟는 높이장에 왼쪽 위 조명을 비춰 금속 베벨을 만듭니다. 외부 패키지가 없고 시드가 고정이라 같은 코드에서는 항상 같은 결과가 나옵니다.

## 실행

프로젝트 루트에서 실행합니다. 키트 28장은 1초 안팎, 메뉴 배경은 약 3초 걸립니다.

```powershell
dotnet run -c Release --project Tools/TowerArt/UIPainter
```

| 인자 | 동작 |
| --- | --- |
| (없음) | `Assets/Demos/Roguelike/Resources/Tower/UI/`에 키트와 `menu-backdrop.png`를 씁니다. |
| `--out <폴더>` | 출력 폴더를 바꿉니다. 실험할 때는 임시 폴더를 지정합니다. |
| `--preview <폴더>` | 모든 조각을 한 장에 모은 `ui-kit-sheet.png`를 씁니다. |
| `--no-background` | 메뉴 배경을 건너뜁니다. |

굽고 나면 Unity에서 에셋을 새로고침합니다. `Editor/TowerUIArtImporter.cs`가 키트를 무압축·Trilinear·밉맵으로, 메뉴 배경을 CompressedHQ로 가져옵니다.

## 규약

- 텍스처는 게임에서 **절반 크기**로 그립니다(UI 1단위 = 텍스처 2픽셀). 1600×900 기준 레이아웃이 1080p에서 1.2배, 4K에서 2.4배로 커져도 선명합니다.
- 9-slice 테두리는 `TowerUIStyle.cs`의 `Slice(rect, texture, border, tint)`에 텍스처 픽셀로 넘깁니다: `panel-fill` 28, `panel-shadow` 44, `panel-frame-*` 64, `card-frame-*` 88, `button-*` 24, `inset` 20, `portrait-frame-*` 36, `meter-*` 16, `keycap` 16.
- `-gold` 조각은 칠한 그대로 쓰고, `-silver` 조각과 회색 조각(`button-body`, `meter-fill`, `node-disc`, `key-fill`)은 런타임에 등급·가지 색으로 물들입니다. 따뜻한 금색 강조는 금 조각, 그 밖의 색은 은 조각을 씁니다(`TowerGame.Golden`).
- `icons.png`는 64픽셀 칸 13개이며 순서는 `TowerGame.UIIcon`(검·모래시계·조준·속도·별·하트·방패·해골·수정·탑·불꽃·십자·금화)과 같아야 합니다.
- `menu-backdrop.png`(2560×1440)는 1600×900 레이아웃에 1.6배로 대응합니다. 탑은 UI x ≈ 975에 서 있어 타이틀의 왼쪽 버튼 열과 오른쪽 장 목록 사이에 보입니다.

## 구조

- `Img.cs` — 직선 알파 float 이미지, 합성, 투명 텍셀 색 번짐(필터링 테두리 방지), PNG 기록기
- `Sdf.cs` — 상자·모따기 상자·원·마름모·테이퍼 선분·호·다각형·별·하트, 모서리 접기, 노이즈
- `Metal.cs` — 금·은·보석 색 램프, 베벨 금속 셰이딩, 그림자, 단색 채우기
- `Kit.cs` — 액자·카드·문장·버튼·슬롯·초상화 틀·메달·게이지·구분선·매듭·키캡·장식·노드
- `Icons.cs` — 아이콘 아틀라스
- `MenuBackground.cs` — 성운·별·행성·천궁도·탑·안개
