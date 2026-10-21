"""Render the original Tower score with a deterministic offline synth.

No samples, game soundtrack excerpts or external model outputs are used.
Usage: python Tools/TowerMusic/render.py [--theme lava] [--sample-rate 44100]
Dependencies: requirements.txt. Optional workspace dependencies: .scratch/music-python.
"""
from __future__ import annotations

import argparse
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
if (ROOT / ".scratch/music-python").exists():
    sys.path.insert(0, str(ROOT / ".scratch/music-python"))
import numpy as np
from scipy.signal import butter, sosfilt
import soundfile as sf

SR = 44100
TAU = 2 * np.pi


def filt(data, frequency, kind="lowpass", order=2):
    return sosfilt(butter(order, frequency, kind, fs=SR, output="sos"), data).astype(np.float32)


def envelope(t, duration, attack=.008, release=.055, decay=0.0):
    return (np.minimum(t / attack, 1) *
            np.minimum(np.maximum((duration - t) / release, 0), 1) *
            np.exp(-decay * t))


@lru_cache(maxsize=4096)
def synth(voice, midi, duration_ms, variant=0):
    duration = duration_ms / 1000
    n = max(8, round(SR * duration))
    t = np.arange(n, dtype=np.float64) / SR
    f = 440 * 2 ** ((midi - 69) / 12)
    rng = np.random.default_rng(5029 + variant * 101 + midi * 17 + duration_ms)
    if voice == "guitar":
        # Two independently detuned, panned performances are placed by the score.
        f *= 2 ** (((variant % 3) - 1) * 4.5 / 1200)
        phase = TAU * f * t + .017 * np.sin(TAU * 5.1 * t)
        raw = np.zeros(n)
        for h in range(1, min(27, int(13500 / f))):
            raw += np.sin(h * phase + .12 * h) / h ** 1.12 * np.exp(-t * h * .62)
        raw += rng.normal(0, .065, n) * np.exp(-t * 85)
        raw *= np.exp(-t * (4.1 if duration < .3 else 1.4))
        raw = filt(np.tanh(raw * 3.8), 3800, order=3)
        raw = filt(raw, 105, "highpass")
        raw *= envelope(t, duration, .003, .028)
        return (raw * .72).astype(np.float32)
    if voice == "bass":
        phase = TAU * f * t
        raw = np.sin(phase) * .85
        for h in range(2, min(13, int(7000 / f))):
            raw += np.sin(h * phase) * .28 / h * np.exp(-t * h * 1.6)
        raw = filt(np.tanh(raw * 1.45), 2200)
        raw *= envelope(t, duration, .004, .04, 1.1)
        return (raw * .80).astype(np.float32)
    if voice.startswith("lead"):
        flavor = voice.split("_")[-1]
        vibrato = .038 * np.sin(TAU * 5.4 * t) * np.minimum(t / .23, 1)
        phase = TAU * f * t + vibrato
        raw = np.zeros(n)
        for h in range(1, min(18, int(16000 / f))):
            # Rounded saw/pulse hybrid, closer to a sampled console lead than a beep.
            weight = (1 if h % 2 else .48) / h ** 1.35
            raw += weight * np.sin(phase * h) * (.68 + .32 * np.exp(-t * h * 2))
            raw += .26 * weight * np.sin(TAU * f * 1.0035 * t * h + .3)
        if flavor in ("ice", "water"):
            raw = raw * .65 + .35 * np.sin(phase + .9 * np.sin(2 * phase) * np.exp(-t * 2))
        if flavor == "ruins":
            raw += .15 * np.sin(phase * 2 + 1.3 * np.sin(phase) * np.exp(-t * 4))
        raw = filt(raw, 5400 if flavor in ("sky", "ice") else 4300)
        raw *= envelope(t, duration, .012, .065, .2)
        return (raw * .48).astype(np.float32)
    if voice == "pad":
        raw = np.zeros(n)
        for detune in (.998, 1.002):
            for h in (1, 2, 3, 4, 5):
                raw += np.sin(TAU * f * detune * h * t + h * .2) / h ** 2
        raw = filt(raw, 1800)
        raw *= envelope(t, duration, .11, .24)
        return (raw * .24).astype(np.float32)
    if voice in ("bell", "pluck"):
        phase = TAU * f * t
        raw = np.sin(phase + (1.8 if voice == "bell" else .7) *
                     np.sin(phase * (2 if voice == "bell" else 3)) * np.exp(-t * 4))
        raw += .17 * np.sin(phase * 2.005) * np.exp(-t * 7)
        raw *= envelope(t, duration, .003, .07, 3.7 if voice == "bell" else 6.0)
        return (raw * .48).astype(np.float32)
    raise ValueError(voice)


@lru_cache(maxsize=128)
def drum(kind, variant=0):
    lengths = {"kick": .30, "snare": .27, "hat": .095, "open": .34,
               "ride": .47, "crash": 1.35, "tom": .30, "clave": .12, "shaker": .10}
    t = np.arange(round(SR * lengths[kind]), dtype=np.float64) / SR
    rng = np.random.default_rng(701 + variant * 29 + list(lengths).index(kind))
    noise = rng.normal(0, 1, len(t))
    if kind == "kick":
        phase = TAU * (46 * t + 105 * .022 * (1 - np.exp(-t / .022)))
        raw = np.sin(phase) * np.exp(-t * 16)
        raw += .16 * filt(noise, [2500, 7500], "bandpass") * np.exp(-t * 190)
        raw = np.tanh(raw * 1.6) * .85
    elif kind == "snare":
        raw = (.54 * np.sin(TAU * 185 * t) + .20 * np.sin(TAU * 330 * t)) * np.exp(-t * 23)
        raw += filt(noise, [1300, 10500], "bandpass") * .58 * np.exp(-t * 19)
        raw += .13 * noise * np.exp(-t * 200)
        raw = np.tanh(raw * 1.25) * .66
    elif kind == "tom":
        f = (153, 122, 101, 86)[variant % 4]
        phase = TAU * (f * t + 35 * .018 * (1 - np.exp(-t / .018)))
        raw = (np.sin(phase) + .24 * np.sin(phase * 1.48)) * np.exp(-t * 15)
        raw += .1 * filt(noise, 3500) * np.exp(-t * 65)
        raw *= .7
    elif kind == "clave":
        raw = (np.sin(TAU * 1420 * t) + .4 * np.sin(TAU * 2100 * t)) * np.exp(-t * 75) * .3
    else:
        decay = {"hat": 66, "open": 15, "ride": 10, "crash": 4.6, "shaker": 55}[kind]
        raw = filt(noise, [4200 if kind != "crash" else 2100, 14500], "bandpass") * .45
        for ratio in (1, 1.342, 1.786, 2.121, 2.596):
            raw += .035 * np.sin(TAU * (3000 + variant * 29) * ratio * t)
        raw *= np.exp(-t * decay) * (.45 if kind in ("hat", "shaker") else .58)
    raw *= np.minimum(t / .0015, 1) * np.minimum((t[-1] - t) / .016, 1)
    return raw.astype(np.float32)


class Mix:
    def __init__(self, bpm, bars=64):
        self.beat = 60 / bpm
        self.frames = round(bars * 4 * self.beat * SR)
        self.dry = np.zeros((self.frames, 2), dtype=np.float32)
        self.send = np.zeros_like(self.dry)
        self.events = []

    def add(self, data, beat, volume, pan=0, wet=0):
        offset = round(beat * self.beat * SR) % self.frames
        gains = np.array([math.cos((pan + 1) * np.pi / 4),
                          math.sin((pan + 1) * np.pi / 4)], np.float32) * volume
        stereo = data[:, None] * gains
        first = min(len(data), self.frames - offset)
        self.dry[offset:offset + first] += stereo[:first]
        if wet:
            self.send[offset:offset + first] += stereo[:first] * wet
        if first < len(data):
            self.dry[:len(data) - first] += stereo[first:]
            if wet:
                self.send[:len(data) - first] += stereo[first:] * wet

    def note(self, voice, midi, beat, duration, volume, pan=0, wet=.1, variant=0, channel=0):
        ms = round((duration * self.beat + (.16 if voice == "bell" else .025)) * 1000)
        self.add(synth(voice, int(midi), ms, variant), beat, volume, pan, wet)
        self.events.append((beat, duration, int(midi), max(1, min(127, round(volume * 110))), channel))

    def hit(self, kind, beat, volume, pan=0, variant=0):
        self.add(drum(kind, variant), beat, volume, pan, .12 if kind in ("snare", "tom") else .035)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51,
              "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 75, "shaker": 70}
        self.events.append((beat, .1, gm[kind], max(1, min(127, round(volume * 100))), 9))

    def finish(self):
        # Circular delay/reverb means every tail continues across the loop boundary.
        for seconds, gain in ((self.beat * .75, .24), (self.beat * 1.5, .12),
                              (.037, .14), (.061, .11), (.113, .09),
                              (.181, .065), (.293, .04)):
            shift = round(seconds * SR)
            self.dry += np.roll(self.send[:, ::-1], shift, axis=0) * gain
        del self.send
        for channel in range(2):
            extended = np.concatenate((self.dry[-SR:, channel], self.dry[:, channel]))
            self.dry[:, channel] = filt(extended, 32, "highpass")[SR:]
        # Gentle soft bus saturation, then conservative peak ceiling for Vorbis.
        self.dry = (1.12 * np.tanh(self.dry / 1.12)).astype(np.float32)
        peak = float(np.max(np.abs(self.dry)))
        self.dry *= .82 / max(peak, 1e-9)
        return self.dry


def voice_for(theme):
    return "lead_" + {"lava": "fire", "rock": "stone", "jungle": "jungle",
                      "frozen": "ice", "water": "water", "cloud": "sky", "ruins": "ruins"}[theme]


def sections(bar):
    if bar < 4: return "intro", bar
    if bar < 20: return "a", bar - 4
    if bar < 36: return "b", bar - 20
    if bar < 44: return "break", bar - 36
    if bar < 52: return "solo", bar - 44
    if bar < 60: return "reprise", bar - 52
    return "turn", bar - 60


def arrange(score, mix=None):
    mix = Mix(score["bpm"]) if mix is None else mix
    theme = score["id"]
    tonic = score["tonic_midi"]
    lead = voice_for(theme)
    groove = {
        "lava": ([0, .75, 1.5, 2, 2.75, 3.5], [0, .5, .75, 1.5, 2, 2.5, 2.75, 3.5]),
        "rock": ([0, 1.75, 2.5, 3.25], [0, .75, 1.5, 2, 2.75, 3.5]),
        "jungle": ([0, .75, 2, 2.75], [0, .75, 1.25, 1.75, 2.5, 3.25, 3.75]),
        "frozen": ([0, 1.5, 2, 3.5], [0, .5, 1.5, 2, 2.5, 3.5]),
        "water": ([0, 1.5, 2.75], [0, .75, 1.5, 2.25, 2.75, 3.5]),
        "cloud": ([0, .5, 1.5, 2, 2.5, 3.5], [0, .5, 1, 1.5, 2, 2.5, 3, 3.5]),
        "ruins": ([0, .75, 2, 3.25], [0, .75, 1.5, 2, 2.75, 3.25, 3.75]),
    }[theme]

    for bar in range(64):
        section, local = sections(bar)
        b = bar * 4
        big = section in ("b", "reprise", "solo")
        quiet = section == "break" and local < 4
        chords = score["chords_b"] if section in ("b", "solo") else score["chords_a"]
        chord = chords[local % len(chords)]
        if section == "turn":
            chord = score["chords_a"][(local + 4) % 8]
        root = chord[0]
        if section == "intro":
            level = (.62, .68, .8, .9)[local]
        else:
            level = .52 if quiet else 1.0

        # Bass follows independently authored syncopations, with octave/fifth pickups.
        bass_hits = groove[1] if not quiet else [0, 1.5, 2.75]
        if section == "intro" and theme == "frozen" and local < 2:
            bass_hits = []
        for i, at in enumerate(bass_hits):
            interval = root
            if i == len(bass_hits) - 1 and bar % 2 == 1:
                interval = root + 7
            elif theme in ("jungle", "water") and i % 3 == 1:
                interval = root + 12
            next_hit = bass_hits[i + 1] if i + 1 < len(bass_hits) else 4
            mix.note("bass", tonic - 24 + interval, b + at,
                     min(.7, (next_hit - at) * .88), .63 * level, wet=.01, channel=1)

        # Two guitar performances. Verses are staccato; the hook opens into sustained chords.
        sparse_intro = section == "intro" and theme in ("frozen", "water", "jungle") and local < 2
        if not quiet and not sparse_intro:
            guitar_hits = [0, 1.5, 2.5] if big else groove[1]
            if theme == "water":
                guitar_hits = [.5, 1.5, 2.5, 3.5]
            for i, at in enumerate(guitar_hits):
                duration = (.78 if big else .23)
                if theme == "rock": duration *= 1.2
                guitar_gain = .115 if theme in ("water", "frozen") else .16
                for pan, detune, delay in ((-.69, 0, 0), (.69, 2, .016)):
                    for interval, weight in ((root, 1), (root + 7, .76), (root + 12, .44)):
                        mix.note("guitar", tonic - 12 + interval, b + at + delay,
                                 duration, guitar_gain * weight * level, pan, .06, detune, 2 if pan < 0 else 6)

        # Quiet harmonic bed and characteristic arpeggios.
        for i, interval in enumerate(chord):
            mix.note("pad", tonic + interval, b, 3.98, (.11 if big else .08) * level,
                     -.45 + i * .27, .45, channel=3)
        arp_enabled = theme in ("frozen", "water", "cloud", "ruins") or quiet or section == "intro"
        if arp_enabled:
            arp_voice = "bell" if theme in ("frozen", "water", "rock") else "pluck"
            steps = 8 if theme in ("water", "cloud") or quiet else 4
            order = [0, 2, 1, 2, 0, 1, 2, 1]
            for step in range(steps):
                interval = chord[order[step] % len(chord)] + (12 if step % 4 == 3 else 0)
                pitch = tonic + 12 + interval
                while pitch > 91:
                    pitch -= 12
                mix.note(arp_voice, pitch, b + step * 4 / steps, .33,
                         .16 if quiet else .11, -.43 if step % 2 == 0 else .43, .48, channel=4)

        # Backbeat, kick syncopation, fills, and alternating hat dynamics.
        icy_intro = section == "intro" and theme == "frozen" and local < 2
        if (not quiet or local % 2 == 0) and not icy_intro:
            for at in groove[0] if not quiet else [0, 2.5]:
                mix.hit("kick", b + at, .76 * level)
        if (section != "intro" or local >= 2) and not (quiet and theme == "frozen"):
            halftime = quiet or (theme == "rock" and not big)
            for at in ([2] if halftime else [1, 3]):
                mix.hit("snare", b + at, .65 * level, .04, bar % 4)
            if theme in ("jungle", "water") and not quiet:
                mix.hit("snare", b + 2.75, .12, .04, 3)
        for step in range(0 if icy_intro else 8):
            at = step * .5
            hat = "ride" if big and theme in ("cloud", "rock") else "hat"
            if step == 7 and bar % 2 == 1 and not quiet: hat = "open"
            mix.hit(hat, b + at, (.34 if step % 2 == 0 else .22) * level,
                    .28, (bar + step) % 4)
        if theme in ("jungle", "ruins"):
            for step, at in enumerate([.5, 1.75, 2.5, 3.25]):
                mix.hit("clave" if theme == "jungle" else "tom", b + at,
                        .19 * level, -.40, step)
        if theme == "water":
            for step in range(16):
                mix.hit("shaker", b + step / 4, .12 * level, -.45, step % 4)
        if bar in (0, 4, 20, 28, 44, 52, 60) and not sparse_intro:
            mix.hit("crash", b, .45, -.25, bar % 4)
        if local % 8 == 7 or bar in (3, 19, 35, 43, 51, 59, 63):
            for i, at in enumerate((2.75, 3.0, 3.25, 3.5, 3.75)):
                mix.hit("snare" if i < 2 else "tom", b + at,
                        .29 + i * .045, -.35 + i * .18, max(0, i - 1))

        # Score hooks are authored notes, not random scale walks.
        if section in ("a", "b", "reprise", "solo") or (section == "break" and local >= 4):
            hook = score["hook_b"] if section in ("b", "solo") else score["hook_a"]
            phrase = hook[local % 4]
            for index, event in enumerate(phrase):
                at, duration, interval, velocity = event
                # A few long answer notes become a descending arpeggiated solo gesture.
                if section == "solo" and duration >= .9:
                    for sub in range(3):
                        pitch = tonic + chord[(2 - sub) % len(chord)] + 12
                        mix.note(lead, pitch, b + at + sub * duration / 3,
                                 duration / 3 * .89, .39 * velocity, -.05, .36, channel=0)
                else:
                    mix.note(lead, tonic + interval, b + at, duration * .94,
                             (.46 if big else .43) * velocity, -.05, .38, channel=0)
                # Chord-tone answer under the final hook, without crowding every note.
                if section == "reprise" and duration >= .65:
                    target = interval - 3
                    candidates = [n + octv for n in chord for octv in (-12, 0, 12, 24)]
                    harmony = min(candidates, key=lambda n: abs(n - target))
                    mix.note(lead, tonic + harmony, b + at, duration * .92,
                             .15 * velocity, .3, .38, channel=5)
        elif section == "intro":
            # An early signature fragment establishes identity before the full theme.
            if local in (0, 2) and not (theme in ("frozen", "jungle") and local == 0):
                for at, duration, interval, velocity in score["hook_a"][0][:3]:
                    mix.note(lead, tonic + interval, b + at, duration * .9,
                             .31 * velocity, -.05, .38, channel=0)
        elif section == "turn":
            if local < 2:
                for at, duration, interval, velocity in score["hook_a"][local]:
                    mix.note(lead, tonic + interval, b + at, duration * .92,
                             .37 * velocity, -.05, .38, channel=0)
            else:
                for i, interval in enumerate(reversed(chord[:3])):
                    mix.note(lead, tonic + interval + 12, b + i, .7,
                             .34, -.05, .4, channel=0)
    return mix


def vlq(value):
    data = [value & 0x7f]
    while value >> 7:
        value >>= 7
        data.insert(0, (value & 0x7f) | 0x80)
    return bytes(data)


def write_midi(path, score, events):
    resolution = 480
    tracks = []
    tempo = round(60000000 / score["bpm"])
    title = score["title"].encode("ascii", "replace")
    meta = b"\0\xff\x03" + vlq(len(title)) + title
    meta += b"\0\xff\x51\x03" + tempo.to_bytes(3, "big")
    meta += b"\0\xff\x58\x04\x04\x02\x18\x08"
    previous = 0
    for beat, name in ((0, "INTRO"), (16, "A"), (80, "B"), (144, "BREAK"),
                       (176, "SOLO"), (208, "REPRISE"), (240, "TURNAROUND")):
        encoded = name.encode("ascii")
        tick = beat * resolution
        meta += vlq(tick - previous) + b"\xff\x06" + vlq(len(encoded)) + encoded
        previous = tick
    meta += vlq(256 * resolution - previous) + b"\xff\x2f\0"
    tracks.append(meta)
    programs = {0: 81, 1: 33, 2: 30, 3: 89, 4: 10, 5: 81, 6: 30, 9: 0}
    for channel, program in programs.items():
        sequence = []
        for beat, duration, note, velocity, ch in events:
            if ch != channel: continue
            start = round(beat * resolution)
            stop = min(64 * 4 * resolution, round((beat + duration) * resolution))
            if stop <= start: continue
            sequence.extend([(start, 1, bytes((0x90 + ch, note, velocity))),
                             (stop, 0, bytes((0x80 + ch, note, 0)))])
        sequence.sort(key=lambda event: (event[0], event[1]))
        data = bytes((0, 0xc0 + channel, program))
        previous = 0
        for tick, _, payload in sequence:
            data += vlq(tick - previous) + payload
            previous = tick
        data += vlq(64 * 4 * resolution - previous) + b"\xff\x2f\0"
        tracks.append(data)
    content = b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), resolution)
    for track in tracks:
        content += b"MTrk" + struct.pack(">I", len(track)) + track
    path.write_bytes(content)


def unity_meta(path, folder=False):
    companion = Path(str(path) + ".meta")
    if companion.exists(): return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "PrototypeLab/TowerMusic/" + path.name).hex
    if folder:
        text = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        text = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.85
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    companion.write_text(text, encoding="utf-8")


def measure(audio, sample_rate):
    peak = float(np.max(np.abs(audio)))
    rms = float(np.sqrt(np.mean(audio.astype(np.float64) ** 2)))
    seam = float(np.max(np.abs(audio[0] - audio[-1])))
    half_second = sample_rate // 2
    energy = [float(np.sqrt(np.mean(audio[i:i + half_second] ** 2)))
              for i in range(0, len(audio) - half_second, half_second)]
    return {"seconds": round(len(audio) / sample_rate, 6), "sample_rate": sample_rate,
            "channels": audio.shape[1], "peak_dbfs": round(20 * math.log10(max(peak, 1e-12)), 3),
            "rms_dbfs": round(20 * math.log10(max(rms, 1e-12)), 3),
            "loop_seam_delta": round(seam, 6), "minimum_half_second_rms": round(min(energy), 6),
            "finite": bool(np.isfinite(audio).all()), "clipped_samples": int(np.count_nonzero(abs(audio) >= 1))}


def render(score, mixer=None):
    print(f"Rendering {score['id']} / {score['bpm']} BPM...", flush=True)
    mix = arrange(score, mixer)
    audio = mix.finish()
    asset_dir = ROOT / "Assets/Demos/Roguelike/Resources/Tower/Music"
    source_dir = ROOT / "ArtSources/Music"
    masters = source_dir / "Masters"
    midi_dir = source_dir / "Midi"
    for directory in (asset_dir, masters, midi_dir):
        directory.mkdir(parents=True, exist_ok=True)
    unity_meta(asset_dir, True)
    wav = masters / (score["id"] + ".wav")
    ogg = asset_dir / (score["id"] + ".ogg")
    # Small blocks avoid libsndfile's Windows Vorbis encoder overflowing the
    # native thread stack when handed an entire multi-minute float array.
    for path, options in ((wav, {"subtype": "PCM_16"}),
                          (ogg, {"format": "OGG", "subtype": "VORBIS", "compression_level": .35})):
        with sf.SoundFile(str(path), "w", samplerate=SR, channels=2, **options) as output:
            for offset in range(0, len(audio), 16384):
                output.write(audio[offset:offset + 16384])
    unity_meta(ogg)
    if mixer is None:
        write_midi(midi_dir / (score["id"] + ".mid"), score, mix.events)
    decoded, rate = sf.read(str(ogg), dtype="float32", always_2d=True)
    report = {"id": score["id"], "title": score["title"], "title_ko": score["title_ko"],
              "bpm": score["bpm"], "bars": 64, "note_events": len(mix.events),
              "production": getattr(mix, "production", "procedural-synth-v1"),
              "master": measure(audio, SR), "ogg": measure(decoded, rate),
              "ogg_bytes": ogg.stat().st_size, "sha256": hashlib.sha256(ogg.read_bytes()).hexdigest()}
    assert 90 <= report["ogg"]["seconds"] <= 120
    assert report["ogg"]["finite"] and report["ogg"]["clipped_samples"] == 0
    assert report["ogg"]["minimum_half_second_rms"] > .005
    report_path = source_dir / (score["id"] + ".analysis.json")
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False), flush=True)
    synth.cache_clear()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--theme", default="all")
    parser.add_argument("--sample-rate", type=int, default=44100)
    args = parser.parse_args()
    SR = args.sample_rate
    scores = json.loads((Path(__file__).parent / "score.json").read_text(encoding="utf-8"))
    if isinstance(scores, dict):
        scores = scores["tracks"]
    selected = [score for score in scores if args.theme == "all" or score["id"] == args.theme]
    if not selected:
        parser.error("Unknown theme: " + args.theme)
    for score in selected:
        render(score)
