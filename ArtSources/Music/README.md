# Towerbound 테마 BGM

7개 테마용 오리지널 밴드 편곡입니다. 록맨 X4·5·6의 멜로딕 액션 록 분위기를 참고했으며, 원작 음원·샘플·멜로디는 사용하지 않았습니다. 녹음된 기타·베이스·드럼 샘플을 편곡하고 앰프·공간 효과로 렌더링했습니다. 밴드의 실시간 합주 녹음은 아닙니다.

[전체 감상 페이지](listen.html)

| 테마 | 곡명 | 길이 | BPM | 파일 |
| --- | --- | ---: | ---: | --- |
| 용암지대 | 용광 회로 / Molten Circuit | 1:41 | 152 | [WAV](Masters/lava.wav) |
| 암석지대 | 수정 단층 / Crystal Fault | 1:47 | 144 | [WAV](Masters/rock.wav) |
| 정글지대 | 수관의 전류 / Canopy Voltage | 1:44 | 148 | [WAV](Masters/jungle.wav) |
| 빙결지대 | 프리즘 눈보라 / Prism Blizzard | 1:53 | 136 | [WAV](Masters/frozen.wav) |
| 수상지대 | 조류의 신호 / Tidal Relay | 1:50 | 140 | [WAV](Masters/water.wav) |
| 구름지대 | 천공의 벡터 / Skyward Vector | 1:38 | 156 | [WAV](Masters/cloud.wav) |
| 폐허지대 | 매몰된 태양 기관 / Buried Sun Engine | 1:45 | 146 | [WAV](Masters/ruins.wav) |

## 파일과 재생

- `Masters/`: 44.1kHz / 16bit / stereo WAV 원본.
- `Samples/`: 녹음된 악기 샘플과 각 라이브러리의 출처·라이선스.
- 게임용 OGG: `Assets/Demos/Roguelike/Resources/Tower/Music/`.
- 64마디: 도입 4 → A 16 → B 16 → 브레이크 8 → 솔로 8 → 재현 8 → 턴어라운드 4.
- 딜레이·잔향을 곡 처음으로 이어서 루프 경계의 공백을 없앴습니다. 일반 감상용 엔딩 페이드가 없는 게임 루프입니다.
- `TowerMusic`이 테마에 따라 자동 재생합니다. 같은 테마의 층에서는 위치를 유지하고, 테마가 바뀌면 1.25초 크로스페이드합니다.
- 음악 기본 볼륨은 `TowerMusic.DefaultVolume = 0.18`; 효과음 소스와 분리되어 있습니다.
- OGG는 스테레오 스트리밍 / Vorbis 0.85 / 백그라운드 로드 / 샘플레이트 유지로 가져옵니다.

## 재현

Python에 `Tools/TowerMusic/requirements.txt`를 설치하고 저장소 루트에서 다음을 실행합니다.

```text
python Tools/TowerMusic/render_band.py
python Tools/TowerMusic/package.py
```

한 곡만 다시 만들려면 `--theme lava`처럼 지정합니다. 악보는 `Tools/TowerMusic/score.json`에 있으며, 밴드 음색과 믹스는 `render_band.py`, 공통 편곡 구조와 출력 도구는 `render.py`가 정의합니다. 악보의 arrangement_hint는 제작 방향을 기록한 것이므로 모든 문장이 최종 렌더링의 상세 사양을 뜻하지는 않습니다.

## 악기 샘플 출처

각 라이브러리의 이름·제작자·원본 주소·라이선스·사용 샘플을 아래에 기록했습니다. 종합 `manifest.json`에도 같은 정보를 포함합니다.

- [기타 샘플 출처](Samples/Guitar/manifest.json)
- [베이스 샘플 출처](Samples/Bass/manifest.json)
- [드럼 샘플 출처](Samples/Drums/manifest.json)

## 검증

각 `.analysis.json`과 `manifest.json`에 WAV/OGG 길이·피크·RMS·반복 경계·무음 여부·해시를 기록했습니다. 7곡 모두 요청한 90~120초, 스테레오, 유한 샘플, 디지털 클리핑 없음 조건을 통과했습니다. 이 수치 검사는 사람의 청음 평가를 대신하지 않습니다.

Unity 실행 검증은 `Tests/Run-TowerMusicSmoke.ps1`로 수행합니다. 실제 AudioSource 진행, 같은 테마의 층 이동, 테마 전환 크로스페이드, 루프 반복, 중지·재개, 정리, 효과음 독립성을 검사합니다. 검증용 층 전환은 전투 전체를 플레이한 결과가 아닙니다.
