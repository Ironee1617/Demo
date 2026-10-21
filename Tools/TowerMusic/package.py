"""Build the local listening page and provenance manifest from rendered tracks."""
import html
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSources/Music"
sample_libraries = [
    json.loads((SOURCE / "Samples" / instrument / "manifest.json").read_text(encoding="utf-8"))
    for instrument in ("Guitar", "Bass", "Drums")
]
tracks = json.loads((Path(__file__).parent / "score.json").read_text(encoding="utf-8"))["tracks"]
names = ["용암지대", "암석지대", "정글지대", "빙결지대", "수상지대", "구름지대", "폐허지대"]
colors = ["#ff855b", "#b4a7ee", "#9cdb79", "#91daff", "#60d8ce", "#efc977", "#d7a27a"]
descriptions = [
    "긴장감 있는 E단조 기타 리프와 위로 치솟는 리드 기타. 묵직한 베이스와 드럼이 용암 성채의 속도감을 받친다.",
    "하프타임 드럼과 낮은 기타 리프가 암석의 무게를 표현한다. 맑게 뜯는 기타가 짧은 여백을 채운다.",
    "도리안 선율과 엇박 베이스 위로 기타가 달린다. 리듬 기타와 드럼의 주고받음으로 정글의 움직임을 만든다.",
    "맑은 클린 기타 도입에서 넓게 펼쳐지는 리드 기타로 이어진다. 차가운 선율에 베이스와 드럼의 온기를 더한다.",
    "7화음과 엇박 기타가 물결처럼 흐르는 밴드 편곡. 움직이는 베이스 위에서 리드 기타가 선율을 이어간다.",
    "밝은 장조 선율과 빠른 드럼, 열린 기타 코드로 하늘을 가르는 속도감을 만든다.",
    "반음의 긴장과 낮은 기타 리프, 깊은 탐 드럼이 폐허의 무게를 만든다. 리드 기타가 어두운 공간을 가로지른다.",
]
reports = []
cards = []
rows = []
for index, (track, name, color) in enumerate(zip(tracks, names, colors), 1):
    ident = track["id"]
    report = json.loads((SOURCE / (ident + ".analysis.json")).read_text(encoding="utf-8"))
    if report.get("production") != "recorded-band-v2":
        raise ValueError(f"{ident} is not a recorded-band-v2 render. Run render_band.py before packaging.")
    reports.append(report)
    duration = round(report["ogg"]["seconds"])
    label = f"{duration // 60}:{duration % 60:02}"
    description = html.escape(descriptions[index - 1])
    audio_url = f"Masters/{ident}.wav?v={hashlib.sha256((SOURCE / 'Masters' / (ident + '.wav')).read_bytes()).hexdigest()[:12]}"
    cards.append(f"""
    <article style="--accent:{color}">
      <div class="top"><span class="number">{index:02}</span><span>{name}</span><span class="duration">{label}</span></div>
      <h2>{html.escape(track['title_ko'])}</h2>
      <p class="subtitle">{html.escape(track['title'])} <span>· {track['bpm']} BPM</span></p>
      <p class="description">{description}</p>
      <audio controls preload="none" aria-label="{name} BGM">
        <source src="{audio_url}" type="audio/wav">
      </audio>
      <div class="links"><a href="{audio_url}" download="{ident}.wav">WAV 음원 ↓</a></div>
    </article>""")
    rows.append(f"| {name} | {track['title_ko']} / {track['title']} | {label} | {track['bpm']} | [WAV](Masters/{ident}.wav) |")

page = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Towerbound · 테마 BGM</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#10131d;color:#eef1fa;font-family:system-ui,"Malgun Gothic",sans-serif}
main{max-width:1120px;margin:auto;padding:60px 28px 40px}header{margin-bottom:34px}
.eyebrow{color:#99a7c6;letter-spacing:.22em;font-size:12px;font-weight:700}
h1{font-size:clamp(32px,5vw,52px);letter-spacing:-.055em;margin:12px 0 14px}
.intro{color:#a7b2c9;line-height:1.8;max-width:740px;font-size:15px}
.badge{display:inline-block;border:1px solid #35415c;border-radius:20px;padding:7px 12px;color:#c5d0e9;font-size:12px;margin:8px 6px 0 0}
.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}
article{background:linear-gradient(125deg,#1c2232,#161b29);border:1px solid #2a3246;border-top:2px solid var(--accent);border-radius:14px;padding:24px}
.top{display:flex;align-items:center;gap:12px;color:var(--accent);font-size:13px;font-weight:650}
.number{font:14px ui-monospace,monospace;opacity:.6}.duration{margin-left:auto;color:#c6cfe2;font:14px ui-monospace,monospace}
h2{font-size:24px;letter-spacing:-.04em;margin:22px 0 6px}.subtitle{margin:0;color:#bdc8df;font-size:14px}.subtitle span{color:#8593b0}
.description{min-height:54px;font-size:13px;line-height:1.9;color:#a6b2cb;margin:18px 0}
audio{display:block;width:100%;height:40px;border-radius:9px;color-scheme:dark}
.links{display:flex;gap:18px;font-size:12px;margin-top:18px}a{color:var(--accent,#b7c7f1);text-decoration:none}a:hover{text-decoration:underline}
footer{margin-top:30px;color:#77849e;font-size:12px;line-height:1.9}
@media(max-width:700px){main{padding:30px 18px}.grid{grid-template-columns:1fr}.description{min-height:0}}
</style></head><body><main><header>
<div class="eyebrow">TOWERBOUND / BAND ARRANGEMENT</div>
<h1>일곱 세계, 일곱 개의 리듬.</h1>
<p class="intro">록맨 X4·5·6에서 떠올린 멜로딕 액션 록의 분위기를 바탕으로 만든 오리지널 연주곡입니다.
실제 악기를 녹음한 기타·베이스·드럼 샘플을 편곡하고 앰프·공간 효과로 렌더링한 밴드 사운드입니다.
원작의 멜로디나 음원 샘플은 사용하지 않았습니다.</p>
<span class="badge">7 TRACKS</span><span class="badge">1:38–1:53 / TRACK</span><span class="badge">STEREO · LOOP READY</span>
</header><section class="grid">""" + "\n".join(cards) + """</section>
<footer>같은 테마에서는 음악이 이어지고, 테마가 바뀌면 1.25초에 걸쳐 전환됩니다.<br>
44.1kHz 스테레오 WAV · 녹음된 악기 샘플 기반 밴드 편곡<br>
샘플 출처와 라이선스는 <a href="README.md">제작 안내</a>에서 확인할 수 있습니다.</footer>
</main><script>
document.querySelectorAll('audio').forEach(player=>player.addEventListener('play',()=>{
  document.querySelectorAll('audio').forEach(other=>{if(other!==player)other.pause()});
}));
</script></body></html>"""
(SOURCE / "listen.html").write_text(page, encoding="utf-8")
manifest = {
    "created_date_kst": "2026-10-20", "collection": "Towerbound Original Stage Music",
    "method": "Original authored notes arranged using recorded guitar, bass and drum samples, rendered offline with amp and spatial processing. Not a live band performance.",
    "external_samples": sample_libraries, "reference": "Broad melodic action-rock stage atmosphere of Mega Man X4/X5/X6; no soundtrack copying.",
    "source_score": "Tools/TowerMusic/score.json", "renderer": "Tools/TowerMusic/render_band.py",
    "audio_format": "44.1 kHz stereo; PCM16 WAV masters and Vorbis OGG runtime assets.",
    "qa_scope": "Automated decoded-audio measurements and separate Unity playback smoke. No claim of human auditory review.",
    "tracks": reports,
}
(SOURCE / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
readme = """# Towerbound 테마 BGM

7개 테마용 오리지널 밴드 편곡입니다. 록맨 X4·5·6의 멜로딕 액션 록 분위기를 참고했으며, 원작 음원·샘플·멜로디는 사용하지 않았습니다. 녹음된 기타·베이스·드럼 샘플을 편곡하고 앰프·공간 효과로 렌더링했습니다. 밴드의 실시간 합주 녹음은 아닙니다.

[전체 감상 페이지](listen.html)

| 테마 | 곡명 | 길이 | BPM | 파일 |
| --- | --- | ---: | ---: | --- |
""" + "\n".join(rows) + """

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
"""
(SOURCE / "README.md").write_text(readme, encoding="utf-8")
print("Packaged seven tracks:", SOURCE / "listen.html")
