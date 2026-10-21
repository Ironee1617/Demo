# 탑 배경의 원본 도트 리소스

`Assets/Demos/Roguelike/RPGAsset/RPGAsset/RPG_Buildings_CASTLE_v1.0.zip`에서 다음 파일을 압축 해제하여 독립 복사했습니다. PNG의 크기와 픽셀은 변경하지 않았습니다. 원본 ZIP과 그 안의 파일은 그대로 보존합니다.

| 실행 리소스 | ZIP 내부 원본 | 크기 |
| --- | --- | --- |
| `Resources/Tower/Environment/CastleInterior.png` | `castle_interriors.png` | 1056×1280 |
| `Resources/Tower/Environment/CastleDecor.png` | `castle_decoratives.png` | 512×512 |
| `Resources/Tower/Environment/Torch.png` | `Anim/torch.png` | 128×64 |
| `Resources/Tower/Environment/Castle-License.txt` | `public-license.txt` | 원문 라이선스 |

작가는 Szadi art입니다. 배포된 라이선스 원문을 복사본과 함께 보존합니다.

`TowerWorld.cs`에서 원본 타일의 영역을 Sprite로 잘라 조합합니다. 좌표는 이미지 왼쪽 위를 기준으로 하며 단위는 픽셀입니다.

| 용도 | 아틀라스 | x, y, 너비, 높이 |
| --- | --- | --- |
| 돌벽 | CastleInterior | 0, 160, 64, 64 |
| 바닥 | CastleInterior | 32, 1152, 32, 32 |
| 발판 모서리 | CastleInterior | 32, 160, 32, 16 |
| 아치형 창 | CastleInterior | 224, 288, 96, 128 |
| 기둥 | CastleInterior | 416, 112, 32, 160 |
| 탑의 문 | CastleInterior | 576, 448, 64, 96 |
| 깃발 | CastleDecor | 0, 224, 96, 96 |
| 횃불 4프레임 | Torch | 32×64 영역을 가로 순서로 재생 |

32 PPU·Point 필터·밉맵 없음·무압축으로 가져옵니다. 배경의 깊이를 위해 일부 레이어에 색조를 곱합니다. 작은 그림자, 먼지와 출구의 빛은 단순 도형으로 표현합니다. 새로운 캐릭터 또는 배경 이미지를 생성하지 않습니다.
