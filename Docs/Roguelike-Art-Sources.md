# 로그라이크 픽셀 그래픽 출처와 애니메이션

## 원본 이미지 보존

캐릭터를 새로 그리거나 AI로 다시 생성하지 않았습니다. 제공된 `Assets/Demos/Roguelike/RPGAsset/spine/cha_000`부터 `cha_016`의 **원본 픽셀 PNG 파츠**를 사용합니다. 비인간형 15번을 제외한 16종이 플레이어 1명과 동료 15명에 배정됩니다. 원본 폴더는 수정하지 않으며 실행에 필요한 이미지만 `Resources/Tower/Actors`에 독립 복사합니다.

몬스터는 제공된 `RPGAsset/mobIcon/Stage_0_0.png`, `Stage_0_1.png`, `Stage_0_2.png`, 비행형 `Stage_1_Boss.png`, 보스 `Stage_0_Boss.png`를 그대로 사용합니다. 캐릭터·몬스터 PNG 복사본 200개의 SHA-256이 원본과 일치합니다. 전체 원본 경로, 실행용 복사본 경로, 바이트 수와 해시는 `Assets/Demos/Roguelike/Resources/Tower/ArtSources.json` 및 `Artifacts/TowerArt/source-manifest.json`에 기록합니다. 원본 스켈레톤 16개의 해시와 실행용 `ArtData.json` 해시도 포함합니다.

## 실제 제공된 애니메이션

`RPGAsset/unity/cha_NNN/cha_NNN.skel.bytes`는 Spine **4.2.43** 형식입니다. 16종 모두 `atk`, `idle`, `run`, `die`를 포함하여 원본 본 애니메이션 64개를 사용할 수 있습니다. `.spine` 작업 파일은 압축 바이너리이며, 별도로 제공된 Photoshop JSON에는 조립 위치만 있고 애니메이션은 비어 있습니다.

원본 바이너리에서 다음 데이터를 읽어 `Resources/Tower/ArtData.json`에 보관합니다.

- 본 계층, 기본 위치·회전·크기·전단 값
- 원본 메시의 정점, UV, 삼각형, 본 가중치와 파츠 표시 순서
- 원본 `atk`, `idle`, `run`, `die` 본 곡선을 24fps로 샘플링한 값
- 원본 조립에서 사용한 날개, 모자, 활시위, 화살, 안드로이드 귀 장식 등 부속 파츠

실행 시 원본 PNG를 메시 텍스처로 직접 사용하고 가중치에 따라 정점을 움직입니다. 일반적인 인체 위치를 임의로 정해서 조립하던 이전 방식에서 원본 메시·본 조립으로 바꾸었습니다. PNG를 프레임별로 다시 그리거나 새 캐릭터 이미지 파일로 출력하지 않습니다.

이 데모는 외부 Spine 런타임 패키지를 추가하지 않습니다. 파일 형식 확인에는 [Spine 4.2의 공식 데이터 리더](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/spine-csharp/src/SkeletonBinary.cs)를 참고했습니다. 전용 데이터 추출 도구는 `Tools/TowerArt/extract-original-art.py`, 재생 코드는 `TowerActorArt.cs`입니다. 전체 Spine 런타임을 구현한 것은 아니므로 별도의 메시 변형 곡선과 슬롯 이미지 전환 전체를 재생하지는 않습니다. 본·가중치로 이루어진 원본 움직임과 데모 전용 보완 포즈를 재생합니다.

## 16종의 재생 프로필

각 프로필은 해당 원본 캐릭터의 애니메이션을 사용하며 보행·대기 속도, 공격 접점과 보완 포즈를 구분합니다. 표의 접점은 요청한 공격 전체 시간에 대한 비율입니다.

| 원본 번호 | 배정 | 재생 프로필 | 공격 접점 |
| --- | --- | --- | --- |
| 000 | 플레이어 | Oathblade | 43% |
| 001 | 루크 | Blackguard | 46% |
| 002 | 엘린 | Dawnstaff | 50% |
| 003 | 잭스 | FrostNinja | 38% |
| 004 | 세라 | FlameWitch | 50% |
| 005 | 베아 | BeastFist | 35% |
| 006 | 아틀라스 | DragonLancer | 48% |
| 007 | 노아 | AstralFrost | 52% |
| 008 | 미라 | Lifebloom | 55% |
| 009 | 코르 | Corsair | 42% |
| 010 | 셰이드 | ShadowKnife | 34% |
| 011 | 핀 | Crossbow | 52% |
| 012 | 레아 | NightViolin | 50% |
| 013 | 브람 | IronFist | 40% |
| 014 | 리라 | SylvanBow | 58% |
| 016 | 기즈모 | ClockworkBlades | 44% |

걷기·달리기·대기·공격·사망은 원본 클립을 사용합니다. 시전 준비·발사·회수, 피격, 점프·낙하·회피는 원본 조립과 클립 위에 추가한 데모 포즈입니다. 플레이어 검 공격은 콤보 번호에 따라 베기와 찌르기 보완 포즈를 사용합니다.

## 전투와 동기화하는 API

- `PlayAttack(duration, combo)`는 공격 준비부터 회수까지 한 번 재생합니다. `AttackHitTime(duration)`과 `AttackReleaseTime(duration)`이 해당 캐릭터의 접점 시간을 반환하며 게임은 이 시간에 실제 타격·투사체를 적용합니다.
- `PlayCast(effect, duration)`와 `CastReleaseTime(effect, duration)`은 스킬 준비와 발사를 연결합니다. 치유·보호막은 전체 시간의 55%, 다른 스킬은 해당 프로필 발사 시점에 효과를 적용합니다.
- `SetLocomotion(grounded, verticalVelocity, dashing, horizontalSpeed)`가 지상·점프·낙하·회피·걷기·달리기를 구분합니다.
- `PlayDeath(duration)`는 사망을 한 번 재생하며 다른 행동이 다시 덮어쓰지 않습니다.
- `InterruptAction()`은 공격·시전을 취소하고 0.1초 동안 현재 이동 자세로 연결합니다. 회피 입력은 즉시 적용됩니다.
- `PlayBossAttack(motion, duration)`는 원본 왕관 슬라임 이미지의 세 보스 모션을 재생합니다. `BossMotionName`, `BossReleaseTime(motion, duration)`, `BossVisualHeight`, `RenderedHeight`로 모션 이름·첫 발사/착지 시각·표시 높이를 확인합니다.
- 기존 `SetPose`는 호환됩니다. 공격 플래그를 계속 켜 두어도 모션을 반복 초기화하지 않으며 명시적으로 시작한 공격·시전을 유지합니다. 정지 화면에서 `dt=0`이면 재생 시간도 정지합니다.
- `ProfileName`, `MotionName`, `ActionTime`, `ActionDuration`으로 실제 재생 상태를 확인할 수 있습니다.

모든 요청 시간은 최소 0.08초로 제한하며, 타격 시각 계산과 모션 재생이 같은 제한을 사용합니다.

## 왕관 슬라임 보스

보스는 `RPGAsset/mobIcon/Stage_0_Boss.png`의 **원본 170×170 이미지**를 `Resources/Tower/Monsters/monster_4.png`에 그대로 복사한 것입니다. 알파 영역은 `(0, 17)`부터 `(170, 147)`까지이며 실제 높이는 130픽셀입니다. 메타데이터의 피벗을 알파 중심에 두고 실행 시 실제 높이를 `50/32` Unity 단위로 맞춥니다. 보스 루트 1.8배에서 대기 자세 높이는 2.8125, 너비는 약 3.678 단위입니다. 이미지 파일의 픽셀·해상도는 바꾸지 않습니다. 정지 자세의 알파 하단은 발 위치 0에 맞습니다.

이 몬스터 원본은 단일 이미지이므로 원본 애니메이션 클립은 없습니다. 같은 원본 픽셀을 회전·이동·크기 변형하여 준비, 발사, 회수를 구분합니다. 다른 캐릭터의 원본 64개 본 클립은 그대로 유지됩니다.

| 번호 | 모션 | 재생 시간 | 실제 공격과 맞춘 순간 | 표현 |
| --- | --- | --- | --- | --- |
| 0 | CrownVolley | 2.25초 | 1.2 / 1.6 / 2.0초 | 몸을 모은 뒤 세 번 펼치고 반동을 회수 |
| 1 | TwinSpiral | 3.2초 | 첫 발사 1.3초, 이후 0.22초 간격 | 준비 후 낮게 부양하며 720도 회전, 착지 |
| 2 | LeapingSlam | 2.75초 | 이륙 1.35초, 착지 2.2초 | 웅크림, 공중 신장, 착지 눌림, 복귀 |

도약의 월드 좌표와 충돌은 보스 AI가 관리합니다. 아트는 추가 점프 궤적을 더하지 않습니다. 나선 모션의 작은 부양 표현도 지상 보스의 피격 자격을 바꾸지 않습니다. 모든 모션은 같은 원본 이미지로 복귀하며 `MotionName`은 각각 `BossCrownVolley`, `BossTwinSpiral`, `BossLeapingSlam`입니다.

## 픽셀 표시와 초상화

캐릭터 기준 밀도는 원본 1픽셀당 1/32 Unity 단위입니다. 원본 PNG는 Point 필터, 밉맵 없음, 비압축으로 가져오고 이미지 크기를 임의로 바꾸지 않습니다. 화면상의 캐릭터 위치는 원본 픽셀 단위로 정렬합니다.

UI 초상화도 HD 일러스트 대신 **실제 원본 픽셀 메시를 카메라로 찍은 결과**입니다. 각 캐릭터의 원본 머리 위치를 기준으로 64×64 원본 픽셀 범위를 128×128 Point 텍스처에 담습니다. 초상화는 얼굴·상체에 초점을 맞추므로 긴 무기와 날개 끝은 프레임 밖으로 나갈 수 있습니다. 같은 프레임에 여러 초상화를 생성할 때 임시 캐릭터가 겹치지 않도록 촬영 후 즉시 비활성화합니다. 원본 픽셀 초상화 파일을 새로 저장하거나 HD 얼굴을 픽셀풍으로 변환하지 않습니다.

기존 `RPGAsset/cha/누끼`와 `cha/아이콘`의 고해상도 일러스트는 보존하지만 현재 캐릭터와 UI 초상화 재생 경로에서 사용하지 않습니다.

## 재생 데이터 재생성

Git에는 실행과 원본 대조·재추출에 필요한 PNG, 스켈레톤, 성 배경 ZIP 및 각각의 `.meta`를 선별하여 보관합니다. 대형 PSD, 미사용 제작 파일과 과거 HD 초상화 복사본은 로컬에 보존하며 커밋 대상에서 제외합니다. 원본 경로와 해시는 `Resources/Tower/ArtSources.json`이 기준입니다. `Artifacts/TowerArt/source-manifest.json`과 검증 로그·JSON은 도구를 실행할 때 로컬에 다시 생성됩니다.

저장소의 `Tools/TowerArt/extract-original-art.py`를 프로젝트 루트에서 실행합니다. 준비 도구는 Python의 NumPy·Pillow를 사용하지만 Unity 실행 파일에는 이 의존성이 포함되지 않습니다. 현재 Codex 번들 Python으로 실행하는 PowerShell 예시는 다음과 같습니다.

```powershell
& "$env:USERPROFILE\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" Tools/TowerArt/extract-original-art.py
```

도구는 원본 `.skel.bytes`와 PNG를 읽어 `ArtData.json`, `ArtSources.json`, `Artifacts/TowerArt/source-manifest.json`을 갱신합니다. 누락된 원본 파츠 복사본과 메타데이터를 추가하고, 실행용 PNG가 원본과 다르면 오류로 중단합니다. 이미지 저장·보정·재생성은 수행하지 않습니다. 새 런타임 패키지를 설치하거나 Unity 검증 프로젝트와 원본 폴더를 연결하지 않습니다. 생성된 Resources 데이터와 `.meta` 파일을 함께 보존하면 원본 폴더를 제외한 독립 검증·빌드에서도 동일하게 재생됩니다.
