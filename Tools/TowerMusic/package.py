"""Build the local listening page and provenance manifest from the rendered mythology pieces."""
import html
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent))
from myth_pieces import PIECES  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSources/Music"
PRODUCTION = "myth-v8"
sample_libraries = [
    json.loads((SOURCE / "Samples" / instrument / "manifest.json").read_text(encoding="utf-8"))
    for instrument in ("Guitar", "Bass", "Drums", "Brass", "Orchestra")
]
reports = []
cards = []
rows = []
for index, piece in enumerate(PIECES, 1):
    ident = piece["id"]
    report = json.loads((SOURCE / (ident + ".analysis.json")).read_text(encoding="utf-8"))
    if report.get("production") != PRODUCTION:
        raise ValueError(f"{ident} is not a {PRODUCTION} render. Run render_myth.py before packaging.")
    reports.append(report)
    duration = round(report["ogg"]["seconds"])
    label = f"{duration // 60}:{duration % 60:02}"
    meter = {3: "3/4", 4: "4/4", 6: "6/8"}[piece["beats"]]
    audio_url = f"Masters/{ident}.wav?v={hashlib.sha256((SOURCE / 'Masters' / (ident + '.wav')).read_bytes()).hexdigest()[:12]}"
    cards.append(f"""
    <article style="--accent:#{piece['color']}">
      <div class="top"><span class="number">{index:02}</span><span>{html.escape(piece['theme_ko'])}</span><span class="duration">{label}</span></div>
      <h2>{html.escape(piece['title_ko'])}</h2>
      <p class="subtitle">{html.escape(piece['title'])} <span>· {piece['bpm']} BPM · {meter}</span></p>
      <p class="description">{html.escape(piece['description'])}</p>
      <audio controls preload="none" aria-label="{html.escape(piece['theme_ko'])} BGM">
        <source src="{audio_url}" type="audio/wav">
      </audio>
      <div class="links"><a href="{audio_url}" download="{ident}.wav">WAV 음원 ↓</a></div>
    </article>""")
    rows.append(f"| {piece['theme_ko']} | {piece['title_ko']} / {piece['title']} | {label} | {piece['bpm']} · {meter} | [WAV](Masters/{ident}.wav) |")

page = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Towerbound · 신화 테마 BGM</title>
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
<div class="eyebrow">TOWERBOUND / MYTHOLOGY SUITE</div>
<h1>열 개의 신화, 열 개의 노래.</h1>
<p class="intro">올림포스, 아스가르드, 단군, 에덴, 베다, 파피루스, 중국 신화와 지옥·심연·마을을 위해 새로 작곡한 오리지널 연주곡입니다.
신화마다 선법과 박자, 악기를 달리해 그 세계의 분위기를 담았고, 녹음된 기타·베이스·드럼·브라스 샘플과 플루트·찰현·가야금·하프·피아노·드론·타악 등 합성 음색으로 렌더링했습니다.
기존 작품의 멜로디나 음원 샘플은 사용하지 않았습니다.</p>
<span class="badge">10 TRACKS</span><span class="badge">1:30–1:36 / TRACK</span><span class="badge">STEREO · LOOP READY</span>
</header><section class="grid">""" + "\n".join(cards) + """</section>
<footer>같은 테마에서는 음악이 이어지고, 테마가 바뀌면 1.25초에 걸쳐 전환됩니다.<br>
44.1kHz 스테레오 WAV · 녹음 샘플(기타·베이스·드럼·브라스)과 합성 음색의 혼합 편곡<br>
샘플 출처와 라이선스는 <a href="README.md">제작 안내</a>에서 확인할 수 있습니다.</footer>
</main><script>
document.querySelectorAll('audio').forEach(player=>player.addEventListener('play',()=>{
  document.querySelectorAll('audio').forEach(other=>{if(other!==player)other.pause()});
}));
</script></body></html>"""
(SOURCE / "listen.html").write_text(page, encoding="utf-8")
manifest = {
    "created_date_kst": "2026-10-27", "collection": "Towerbound Mythology Suite",
    "method": "Ten original pieces, one per mythology, authored as mode, form, chords, melodies and section layers (Tools/TowerMusic/myth_pieces.py) and rendered offline by Tools/TowerMusic/render_myth.py with recorded CC0 guitar, bass, drum and brass samples plus synthesized winds, bowed strings, zithers, harp, keyboards, drones and percussion. Not a live performance.",
    "external_samples": sample_libraries,
    "reference": "Mood references only (Undertale 'Once Upon a Time', Mega Man X6 'Moonlight'); no audio, samples or melodies from any existing work are used.",
    "source_score": "Tools/TowerMusic/myth_pieces.py", "renderer": "Tools/TowerMusic/render_myth.py",
    "audio_format": "44.1 kHz stereo; PCM16 WAV masters and Vorbis OGG runtime assets.",
    "qa_scope": "Automated decoded-audio measurements and separate Unity playback smoke. No claim of human auditory review.",
    "tracks": reports,
}
(SOURCE / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
readme = """# Towerbound 신화 테마 BGM

열 개의 테마(올림포스·아스가르드·단군신화·에덴·베다·파피루스·중국신화·지옥·심연·마을)를 위해 새로 작곡한 오리지널 연주곡입니다. 신화마다 선법·박자·악기를 달리했고, 기존 작품의 멜로디나 음원 샘플은 사용하지 않았습니다. 기타·베이스·드럼 키트·브라스와 피아노·플루트·오보에·클라리넷·바이올린·첼로·현 앙상블·하프·팀파니·글로켄슈필·튜블러 벨·큰북·클라베·콩가·징은 녹음된 CC0 샘플이고, 콰이어·일렉 피아노·갈대 피리·글라스 하모니카·드론·타블라·바람 등은 합성 음색입니다. 실시간 연주 녹음은 아닙니다.

[전체 감상 페이지](listen.html)

| 테마 | 곡명 | 길이 | 템포·박자 | 파일 |
| --- | --- | ---: | --- | --- |
""" + "\n".join(rows) + """

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
"""
(SOURCE / "README.md").write_text(readme, encoding="utf-8")
print("Packaged", len(PIECES), "tracks:", SOURCE / "listen.html")
