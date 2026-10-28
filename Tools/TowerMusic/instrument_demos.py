"""Render a short demo of every instrument used by the mythology pieces, plus a listing page.

Output: ArtSources/Music/Instruments/<name>.ogg and ArtSources/Music/instruments.html.
Usage: python Tools/TowerMusic/instrument_demos.py
"""
from __future__ import annotations

import html
import json
from pathlib import Path

import render as base
from render import np, sf, SR, ROOT
from render_band import filter_stereo
from render_synth import pad as strings_pad, lead as synth_lead
from render_guitar import GuitarInstruments, rhythm_amp, clean_amp, bass_amp, hall_ir, convolve
import brass as brass_section
import rompler
import myth_voices as mv
import sampled
from myth_pieces import PIECES
from render_myth import render_voice, render_perc

OUT = ROOT / "ArtSources/Music/Instruments"
BEAT = .42
MAJOR = (0, 2, 4, 5, 7, 9, 11, 12)

# name, Korean label, family, base midi, phrase degrees (None = percussion/other)
MELODIC = [
    ("oboe", "오보에 (녹음 샘플)", "선율", 74), ("clarinet", "클라리넷 (녹음 샘플)", "선율", 70), ("flute", "플루트 (녹음 샘플)", "선율", 77),
    ("bansuri", "반수리 (플루트 녹음 + 글라이드·숨소리)", "선율", 69), ("dizi", "디쯔 (플루트 녹음 + 막 버징)", "선율", 81),
    ("daegeum", "대금 (저음 플루트 녹음 + 넓은 농현·숨소리)", "선율", 57), ("erhu", "얼후 대역 (바이올린 녹음 + 글라이드·공명)", "선율", 74),
    ("fiddle", "피들 (독주 바이올린 녹음)", "선율", 76), ("cello", "첼로 (첼로 섹션 녹음)", "선율·저음", 50),
    ("reed", "갈대 피리 (네이풍 신스 리드)", "선율", 73), ("harp", "하프 (녹음 샘플)", "반주", 72), ("piano", "업라이트 피아노 (녹음 샘플)", "선율·반주", 60),
    ("ep", "일렉트릭 피아노 (FM)", "선율·반주", 67), ("glass", "글라스 하모니카", "선율", 78), ("polysynth", "폴리신스", "선율", 76),
    ("strings", "현 앙상블 (바이올린·첼로 섹션 녹음)", "패드·선율", 62), ("choir", "콰이어 (아-)", "패드", 55), ("crystal", "글로켄슈필 (녹음 샘플)", "장식", 84),
    ("brass", "브라스 섹션 (VSCO 2 CE 녹음 + 신스 레이어)", "선율·화음", 65),
]
OTHER = [
    ("tanpura", "탄푸라 드론", "드론"), ("dark_drone", "어두운 드론", "드론"), ("sub_drone", "서브 드론", "드론"),
    ("guitar", "디스토션 기타 파워코드 (녹음 샘플)", "반주"), ("clean_guitar", "클린 기타 아르페지오 (녹음 샘플)", "반주"), ("bass", "일렉 베이스 (녹음 샘플)", "저음"),
    ("drum_kit", "드럼 키트 (녹음 샘플)", "타악"), ("frame_drums", "프레임 드럼 둠·텍 (콩가 녹음)", "타악"), ("taiko", "큰북 (오케스트라 큰북 녹음)", "타악"),
    ("tabla", "타블라 (합성)", "타악"), ("timpani", "팀파니 (녹음 샘플)", "타악"), ("gong_wood", "징·클라베 (녹음 샘플)", "타악"), ("bell_low", "튜블러 벨 (녹음 샘플)", "타악"), ("wind", "바람", "효과"),
]


def phrase(voice_fn, base_midi, durations=(1, 1, 1, 1, 2.5), degrees=(0, 2, 4, 7, 4)):
    """Place a five-note phrase and return stereo audio."""
    total = round((sum(durations) * BEAT + 1.5) * SR)
    out = np.zeros((total, 2), np.float32)
    at = 0.0
    for duration, degree in zip(durations, degrees):
        wave = voice_fn(base_midi + MAJOR[degree], duration * BEAT)
        if wave.ndim == 1:
            wave = np.stack([wave, wave], axis=1)
        start = round(at * SR)
        n = min(len(wave), total - start)
        out[start:start + n] += wave[:n]
        at += duration * BEAT
    return out


def melodic(instruments, name, base_midi):
    ms = lambda seconds, tail=.06: int(round((seconds + tail) * 1000))
    if name == "choir":
        return phrase(lambda m, s: rompler.choir(m, ms(s, .4)), base_midi)
    if name != "brass":
        last = [None]

        def fn(midi, seconds):
            glide = last[0] if name in ("bansuri", "daegeum", "erhu") else None
            last[0] = midi
            return render_voice(name, midi, seconds, 100, 0, glide, instruments)
        return phrase(fn, base_midi)
    if name == "brass":
        def fn(midi, seconds):
            total = round((seconds + .3) * SR)
            out = np.zeros((total, 2), np.float32)
            for wave, offset, pan in brass_section.section(midi, ms(seconds), 100, 0, midi < 62, 3):
                start = max(0, round((offset + .05) * SR))
                n = min(len(wave), total - start)
                gains = np.sqrt(np.array([1 - pan, 1 + pan], np.float32))
                out[start:start + n] += wave[:n] * gains * .5
            synth = rompler.synth_brass(midi, ms(seconds), 100)
            n = min(len(synth), total)
            out[:n] += synth[:n, None] * .3
            return out
        return phrase(fn, base_midi)
    raise ValueError(name)


def other(instruments, name):
    seconds = 4.0
    total = round(seconds * SR)
    out = np.zeros((total, 2), np.float32)

    def put(wave, at, gain=1.0, pan=0.0):
        if wave.ndim == 1:
            gains = np.array([np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)], np.float32)
            wave = wave[:, None] * gains
        start = round(at * SR)
        n = min(len(wave), total - start)
        if n > 0:
            out[start:start + n] += wave[:n] * gain

    if name in ("tanpura", "dark_drone", "sub_drone"):
        kind = {"tanpura": "tanpura", "dark_drone": "dark", "sub_drone": "sub"}[name]
        put(mv.drone(45 if kind != "sub" else 42, 3800, kind), 0)
        if kind == "tanpura":
            put(mv.drone(52, 3800, kind), .3, .6, .2)
    elif name == "guitar":
        bus = np.zeros((total, 2), np.float32)
        for i, root in enumerate((40, 40, 43, 45)):
            for interval, weight in ((0, 1.0), (7, .8), (12, .5)):
                for pan, rr in ((-.8, 0), (.8, 3)):
                    wave = instruments.pitched("Guitar", root + interval, 100, rr + i, .9, False, False)
                    gains = np.sqrt(np.array([1 - pan, 1 + pan], np.float32))
                    start = round(i * .9 * SR)
                    n = min(len(wave), total - start)
                    bus[start:start + n] += wave[:n, None] * gains * weight
        out += filter_stereo(rhythm_amp(bus, True), 6000) * .04
    elif name == "clean_guitar":
        bus = np.zeros((total, 2), np.float32)
        for i, midi in enumerate((55, 62, 67, 71, 74, 71, 67, 62, 55, 60, 64, 67, 72, 67, 64, 60)):
            wave = instruments.pitched("Guitar", midi, 90, i, .5, False, False)
            start = round(i * .22 * SR)
            n = min(len(wave), total - start)
            gains = np.array([np.cos((-.3 + (i % 4) * .2 + 1) * np.pi / 4), np.sin((-.3 + (i % 4) * .2 + 1) * np.pi / 4)], np.float32)
            bus[start:start + n] += wave[:n, None] * gains
        out += clean_amp(bus) * .5
    elif name == "bass":
        bus = np.zeros((total, 2), np.float32)
        for i, midi in enumerate((40, 40, 47, 45, 43, 40)):
            wave = instruments.pitched("Bass", midi, 96, i, .55, False, False)
            start = round(i * .6 * SR)
            n = min(len(wave), total - start)
            bus[start:start + n] += wave[:n, None]
        out += bass_amp(bus) * .5
    elif name == "drum_kit":
        for i in range(8):
            at = i * .45
            if i % 2 == 0:
                put(instruments.percussion("kick", 110, i, 1.0), at, 1.3)
            else:
                put(instruments.percussion("snare", 110, i, 1.0), at, 1.3)
            put(instruments.percussion("hat", 90, i, .17), at, .7, .3)
            put(instruments.percussion("hat", 70, i + 3, .17), at + .225, .5, .3)
        put(instruments.percussion("crash", 110, 1, 3.0), 3.6, .8, -.2)
    elif name == "frame_drums":
        pattern = [("frame_low", 0), ("frame_high", .45), ("frame_low", .68), ("frame_high", .9), ("frame_high", 1.13), ("frame_low", 1.35), ("frame_high", 1.58)]
        for cycle in range(2):
            for kind, at in pattern:
                put(render_perc(kind, cycle, 100), at + cycle * 1.8, .8, .3 if kind == "frame_high" else 0)
    elif name == "taiko":
        for at, gain in ((0, 1), (.9, .7), (1.8, 1), (2.4, .7), (2.7, .8), (3.0, .9), (3.3, 1)):
            put(render_perc("taiko", int(at * 10), int(60 + 60 * gain)), at, gain * .8)
    elif name == "tabla":
        cycle = [("tabla_dha", 0), ("tabla_tin", .25), ("tabla_tin", .5), ("tabla_dha", .75), ("tabla_dha", 1), ("tabla_tin", 1.25), ("tabla_na", 1.5), ("tabla_tin", 1.75)]
        for rep in range(2):
            for kind, at in cycle:
                put(mv.perc(kind, rep), at + rep * 2, .8 if kind == "tabla_dha" else .55, .2 if kind != "tabla_dha" else 0)
    elif name == "timpani":
        for at, gain in ((0, 1), (1.2, .8), (2.0, .6), (2.3, .65), (2.6, .7), (2.9, .8), (3.2, 1)):
            put(render_perc("timpani", int(at * 10), int(60 + 60 * gain)), at, gain * .7)
    elif name == "gong_wood":
        put(render_perc("gong", 0, 110), 0, .8)
        for at in (1.2, 1.5, 1.9, 2.1, 2.4):
            put(render_perc("wood", int(at * 10), 100), at, .5, .3)
        put(render_perc("gong", 1, 90), 2.8, .7)
    elif name == "bell_low":
        put(render_perc("bell_low", 0, 100), 0, .8)
        put(render_perc("bell_low", 1, 90), 2.0, .7, .2)
    elif name == "wind":
        put(mv.wind(3900, 1), 0, 1.5)
    return out


def finish(audio):
    audio = audio.astype(np.float32)
    audio += convolve(audio, hall_ir()) * .18
    rms = float(np.sqrt(np.mean(audio.astype(np.float64) ** 2)))
    audio *= min(20.0, .11 / max(rms, 1e-9))
    audio *= min(1.0, .9 / max(float(np.abs(audio).max()), 1e-9))
    return audio


def usage_table():
    """Which pieces use each melodic voice as a lead."""
    uses = {}
    for piece in PIECES:
        for section, parts in piece.get("lead", {}).items():
            for part in parts.split("+"):
                name = part.split("@")[0]
                uses.setdefault(name, set()).add(piece["theme_ko"])
    return uses


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    instruments = GuitarInstruments()
    uses = usage_table()
    rows = []
    for name, label, family, base_midi in MELODIC:
        audio = finish(melodic(instruments, name, base_midi))
        path = OUT / f"{name}.ogg"
        sf.write(str(path), audio, SR, format="OGG", subtype="VORBIS")
        rows.append((name, label, family, "·".join(sorted(uses.get(name, []))) or "-"))
        print("demo", name, round(len(audio) / SR, 1), "s", flush=True)
    for name, label, family in OTHER:
        audio = finish(other(instruments, name))
        path = OUT / f"{name}.ogg"
        sf.write(str(path), audio, SR, format="OGG", subtype="VORBIS")
        rows.append((name, label, family, "-"))
        print("demo", name, round(len(audio) / SR, 1), "s", flush=True)
    cards = "\n".join(
        f'<article><h2>{html.escape(label)}</h2><p>{html.escape(family)} · 선율로 쓰는 곡: {html.escape(used)}</p>'
        f'<audio controls preload="none"><source src="Instruments/{name}.ogg" type="audio/ogg"></audio></article>'
        for name, label, family, used in rows)
    page = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Towerbound · 악기 샘플</title>
<style>body{{margin:0;background:#10131d;color:#eef1fa;font-family:system-ui,"Malgun Gothic",sans-serif}}main{{max-width:1000px;margin:auto;padding:40px 24px}}
h1{{letter-spacing:-.04em}}.grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:14px}}
article{{background:#1a2030;border:1px solid #2a3246;border-radius:12px;padding:16px}}h2{{font-size:17px;margin:0 0 6px}}p{{color:#a6b2cb;font-size:12px;margin:0 0 10px;line-height:1.6}}
audio{{width:100%;height:36px;color-scheme:dark}}</style></head><body><main>
<h1>신화 테마 BGM 악기 샘플</h1><p>각 악기가 같은 다섯 음 프레이즈(또는 리듬 패턴)를 연주합니다. 기타·베이스·드럼·브라스는 녹음 샘플이고 나머지는 합성 음색입니다.</p>
<section class="grid">{cards}</section></main></body></html>"""
    (ROOT / "ArtSources/Music/instruments.html").write_text(page, encoding="utf-8")
    (OUT / "index.json").write_text(json.dumps([{"name": n, "label": l, "family": f, "lead_in": u} for n, l, f, u in rows], ensure_ascii=False, indent=1), encoding="utf-8")
    print("wrote", len(rows), "demos")
