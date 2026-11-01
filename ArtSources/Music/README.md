# Towerbound 신화 테마 BGM

열 개의 테마(올림포스·아스가르드·단군신화·에덴·베다·파피루스·중국신화·지옥·심연·마을)를 위해 새로 작곡한 오리지널 연주곡입니다. 신화마다 선법·박자·악기를 달리했고, 기존 작품의 멜로디나 음원 샘플은 사용하지 않았습니다. 기타·베이스·드럼 키트·브라스와 피아노·플루트·오보에·클라리넷·바이올린·첼로·현 앙상블·하프·팀파니·글로켄슈필·튜블러 벨·큰북·클라베·콩가·징은 녹음된 CC0 샘플이고, 콰이어·일렉 피아노·갈대 피리·글라스 하모니카·드론·타블라·바람 등은 합성 음색입니다. 실시간 연주 녹음은 아닙니다.

[전체 감상 페이지](listen.html)

| 테마 | 곡명 | 길이 | 템포·박자 | 파일 |
| --- | --- | ---: | --- | --- |
| 올림포스 | 대리석의 산 / Mount of Marble | 1:31 | 100 · 4/4 | [WAV](Masters/olympus.wav) |
| 아스가르드 | 서리 묶인 전당 / Frostbound Halls | 1:34 | 82 · 4/4 | [WAV](Masters/asgard.wav) |
| 단군신화 | 태백산 신단 / Mountain Shrine | 1:36 | 150 · 6/8 | [WAV](Masters/dangun.wav) |
| 에덴 | 낙원의 아침 / Garden Dawn | 1:30 | 116 · 3/4 | [WAV](Masters/eden.wav) |
| 베다 | 인드라의 강 / River of Indra | 1:33 | 108 · 4/4 | [WAV](Masters/veda.wav) |
| 파피루스 | 두아트의 모래 / Sands of Duat | 1:32 | 78 · 4/4 | [WAV](Masters/papyrus.wav) |
| 중국신화 | 주작의 봉우리 / Vermilion Peaks | 1:32 | 120 · 4/4 | [WAV](Masters/chinese.wav) |
| 지옥 | 유황의 합창 / Brimstone Choir | 1:33 | 72 · 4/4 | [WAV](Masters/hell.wav) |
| 심연 | 허무의 심연 / Nihil Depths | 1:32 | 60 · 4/4 | [WAV](Masters/abyss.wav) |
| 마을 | 화롯불 등불 / Hearth Lantern | 1:33 | 88 · 4/4 | [WAV](Masters/village.wav) |

## 파일과 재생

- `Masters/`: 44.1kHz / 16bit / stereo WAV 원본. `Midi/`: 같은 곡의 MIDI 내보내기(섹션 마커 포함).
- `Samples/`: 녹음된 악기 샘플과 각 라이브러리의 출처·라이선스.
- 게임용 OGG: `Assets/Demos/Roguelike/Resources/Tower/Music/<theme>.ogg`. `TowerMusic.Track`이 테마 id 소문자로 파일을 고릅니다.
- 모든 곡은 1분 30초 안팎의 게임 루프이며, 잔향을 곡 처음으로 이어 반복 시 공백이 없습니다.
- `TowerMusic`이 테마에 따라 자동 재생합니다. 같은 테마의 층에서는 위치를 유지하고, 테마가 바뀌면 1.25초 크로스페이드합니다.
- 음악 기본 볼륨은 `TowerMusic.DefaultVolume = 0.18`; 효과음 소스와 분리되어 있습니다.

## 재현

Python 3.12에 `Tools/TowerMusic/requirements.txt`를 설치하고 저장소 루트에서 다음을 실행합니다.

```text
python Tools/TowerMusic/render_myth.py
python Tools/TowerMusic/package.py
```

한 곡만 다시 만들려면 `--piece olympus`처럼 지정합니다. 작곡 데이터(선법·형식·화성·선율·편성)는 `Tools/TowerMusic/myth_pieces.py`, 편곡 엔진과 믹스는 `render_myth.py`, 합성 음색은 `myth_voices.py`·`rompler.py`·`render_synth.py`, 기타 앰프·베이스·공간은 `render_guitar.py`, 브라스 샘플 섹션은 `brass.py`가 정의합니다. 이전 7곡 체계의 렌더러(`render_band.py`, `render_synth.py`, `render_guitar.py`, `render_rompler.py`, `score.json`)는 기록용으로 남아 있습니다.

## 악기 샘플 출처

- [기타 샘플 출처](Samples/Guitar/manifest.json)
- [베이스 샘플 출처](Samples/Bass/manifest.json)
- [드럼 샘플 출처](Samples/Drums/manifest.json)
- [브라스 샘플 출처](Samples/Brass/manifest.json)
- [오케스트라 샘플 출처](Samples/Orchestra/manifest.json) (피아노·목관·현·하프·타악, VSCO 2 CE)

## 검증

각 `.analysis.json`과 `manifest.json`에 WAV/OGG 길이·피크·RMS·반복 경계·무음 여부·해시를 기록했습니다. 열 곡 모두 80~120초, 스테레오, 유한 샘플, 디지털 클리핑 없음 조건을 통과했습니다. 이 수치 검사는 사람의 청음 평가를 대신하지 않습니다.

Unity 실행 검증은 `Tests/Run-TowerMusicSmoke.ps1`로 수행합니다. 실제 AudioSource 진행, 같은 테마의 층 이동, 테마 전환 크로스페이드, 테마별 고유 트랙, 루프 반복, 중지·재개, 정리, 효과음 독립성을 검사합니다.
