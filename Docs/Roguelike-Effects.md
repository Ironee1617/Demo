# 로그라이크 픽셀 전투 효과

캐릭터, 초상화, 무기 파츠의 원본 이미지는 다시 그리지 않았습니다. 기존 RPG 에셋을 먼저 조사하고 검격·회전 검기·화살 이미지를 재사용했습니다. 원본에 없는 마법 효과만 `TowerVfx.cs`에서 작은 픽셀 프레임과 사각형 기하로 생성합니다. 외부 이미지 생성 서비스와 고해상도 캐릭터 이미지를 사용하지 않았습니다.

## 원본 파일과 독립 복사

다음 PNG는 `Assets/Demos/Roguelike/RPGAsset`의 파일을 `Resources/Tower/Effects`로 바이트 그대로 복사했습니다. 소스 폴더와 링크를 공유하지 않습니다. 최초 복사 직후 SHA-256 일치를 확인했습니다.

| 리소스 파일 | RPGAsset 내 원본 | SHA-256 |
| --- | --- | --- |
| `slash_0.png` | `cha/하뚝/eff/skill0.png` | `49BCD432A99B8E4B76774EC17D807EC3EFBDE4799F47D341D77B108919F5036E` |
| `slash_1.png` | `cha/하뚝/eff/skill1.png` | `3681B23E3C808A2EC455E658A46AFFF5419D923F18E5AB98DD67C4CF1C480F30` |
| `slash_2.png` | `cha/하뚝/eff/skill2.png` | `46B80733B90963437B229718EDBAA85B63288D78ADFE37FB4C24BC4161C1FEAC` |
| `slash_3.png` | `cha/하뚝/eff/skill3.png` | `8D39FE04FBD55A8D1B9DBAAE65DC4483524BAD462AF40AB31AC77403CC0DDBDF` |
| `slash_4.png` | `cha/하뚝/eff/skill4.png` | `601C0CD7D977272D87D7573BDE4C4C63EDF9B9D83BC48C2F8A06E94F2C92D24E` |
| `slash_5.png` | `cha/하뚝/eff/skill5.png` | `9939FB5AEB45CEF240D5DC28E2A976737181D81AA9466A434702E1F9AE9D0B97` |
| `slash_6.png` | `cha/하뚝/eff/skill6.png` | `5A7F994DDEB48D5BB5563FC08DBAE6EB0F1D25571E92C169E4E509F2E4F7E6B6` |
| `swirl.png` | `cha/하뚝/images/eff0.png` | `EDDED781838D5EAEA21AF37DAA06302A3D275A06470ACF450B72208A3E9378DF` |
| `arrow.png` | `spine/cha_014/arrow.png` | `16BF1FBA9CC16A29F6635575B836696FBC4AD75390910B030E2B5B5C1B65E5C6` |

검격의 7개 원본에는 시안색 매트가 포함되어 있습니다. `EnsureOriginals()`는 실행 중 복사 텍스처에서 원본 알파 × 빨강 채널을 흰 검기 알파로 사용합니다. PNG 파일은 변경하지 않으며, 검기 모양과 7프레임 순서는 원본을 따릅니다. 화살과 회전 검기에는 이 마스크를 적용하지 않습니다. 잔상은 현재 SpriteRenderer와 MeshRenderer를 함께 지원합니다. 가중치 Spine 메시의 현재 정점은 독립 Mesh로 고정 복제하고 원본 텍스처·머티리얼을 공유합니다. 새 캐릭터 이미지를 생성하지 않으며, 복제 Mesh는 잔상 만료와 `Clear()`에서 해제합니다.

## 빠진 효과의 코드 생성

각 효과는 48×48 픽셀 프레임 8장을 사용합니다. Point 필터, 밉맵 없음, 32 PPU를 적용합니다. 프레임은 런타임 메모리에만 생성되며 새로운 PNG를 원본 에셋 폴더에 쓰지 않습니다. 에너지 선, 룬 표식과 파편은 한 픽셀 사각형을 명시적인 길이와 두께로 그립니다. 흐린 원형 Glow 텍스처를 공통 효과로 사용하지 않습니다.

| 스킬 | 식별 가능한 시각 요소 |
| --- | --- |
| Fireball | 흰 열핵, 주황 불꽃 꼬리, 폭발 파편 |
| IceNova | 여섯 방향 얼음 결정과 팽창하는 냉기 고리 |
| ChainLightning | 꺾이는 번개, 흰 심선과 가지 번개 |
| Meteor | 바닥 경고 룬, 낙하하는 바위와 불꽃, 충돌 화염 기둥 |
| ArcaneVolley | 보라색 마름모 탄두와 비전 십자 문양 |
| Shockwave | 전진하는 금색 초승달 충격파 |
| BladeStorm | 원본 회전 검기의 회전과 검격 프레임 |
| DashStrike | 수평 섬광과 실제 캐릭터 스프라이트 잔상 |
| MultiShot | 원본 화살을 부채꼴로 분산 |
| PierceShot | 원본 화살을 길고 선명하게 표현 |
| RainArrows | 원본 화살의 하강 열과 지면 파편 |
| PoisonCloud | 세 덩어리의 초록 독무와 움직이는 기포 |
| HealPulse | 밝은 치유 십자와 초록 룬 고리 |
| ShieldPulse | 이중 육각 방패와 중앙 문장 |
| LifeDrain | 소용돌이치는 녹색 핵과 생명력 연결선 |
| Sentry | 픽셀 금속 포탑, 발광 조준경과 발사 반동 |

## 런타임 연결

`TowerVfx.Create(parent)`가 서비스를 만듭니다. `Tick(dt)`만 애니메이션 시간을 진행하므로 게임의 일시정지와 동기화됩니다. `Projectile()`는 같은 투사체 루트에서 여러 번 호출해도 기존 자식 시각 효과를 교체합니다. `AttachZone()`는 게임이 소유한 영역 루트에 시각 효과만 붙입니다. `Clear()`는 서비스가 만든 시각 효과만 지우고 게임의 투사체·영역 루트는 삭제하지 않습니다. 루트가 게임에서 삭제되면 다음 `Tick()`에 등록이 정리됩니다.

`Cast`, `Impact`, `Trail`, `TelegraphMeteor`, `BasicSlash`, `Hit`, `Afterimage`는 실제 전투의 시전·충돌·연결·경고·기본 공격·피격·회피 이벤트에 연결합니다. `ActiveCount`와 `KnownEffectsCount`는 선택 실행 QA 갤러리에서 16개 효과의 표시, 프레임 변화와 수명 종료를 검사할 때 사용합니다.
