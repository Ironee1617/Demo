"""Render the authored Tower melodies with recorded guitar, bass and drum samples.

No MIDI playback, GM soundfont, Runway, or synthetic instrument oscillator is
used in this rendition. The CC0 recordings and licenses are under
ArtSources/Music/Samples. Amplifier, speaker EQ and mix effects are modeled DSP.
"""
from __future__ import annotations

import argparse
from functools import lru_cache
import json
import math
from pathlib import Path

import render as base
from render import np, sf, SR, ROOT, filt
from scipy.signal import resample_poly
from scipy.ndimage import maximum_filter1d

SAMPLE_ROOT = ROOT / "ArtSources/Music/Samples"


class Instruments:
    def __init__(self):
        self.entries = {}
        for name in ("Guitar", "Bass", "Drums"):
            manifest = json.loads((SAMPLE_ROOT / name / "manifest.json").read_text(encoding="utf-8"))
            self.entries[name] = manifest["samples"]

    @lru_cache(maxsize=256)
    def load(self, library, filename):
        audio, rate = sf.read(str(SAMPLE_ROOT / library / filename), dtype="float32", always_2d=True)
        if rate != SR:
            divisor = math.gcd(rate, SR)
            audio = resample_poly(audio, SR // divisor, rate // divisor, axis=0).astype(np.float32)
        if library != "Drums":
            audio = audio.mean(axis=1)
            # Remove silence preceding the recorded pick, retaining a 1ms lead-in.
            envelope = np.abs(audio)
            active = np.flatnonzero(envelope > max(.0001, float(envelope.max()) * .003))
            if len(active):
                audio = audio[max(0, int(active[0]) - round(.001 * SR)):]
            # Shared recordings have very different pickup recording gains.
            # Spectral velocity layers remain distinct; dynamics are applied below.
            peak = float(np.max(np.abs(audio)))
            body_rms = float(np.sqrt(np.mean(audio[:round(.35 * SR)] ** 2)))
            audio = audio * min(2.0 / max(peak, .001), .18 / max(body_rms, .001))
            audio = filt(audio, 11000, order=3)
        return audio.astype(np.float32)

    def pitched(self, library, note, velocity, variant, duration, lead=False, muted=False):
        candidates = self.entries[library]
        root = min({s["root_midi"] for s in candidates}, key=lambda key: abs(key - note))
        candidates = [s for s in candidates if s["root_midi"] == root]
        layer = min({s["velocity"] for s in candidates}, key=lambda v: abs(v - velocity))
        candidates = sorted((s for s in candidates if s["velocity"] == layer), key=lambda s: s["round_robin"])
        sample = candidates[variant % len(candidates)]
        original = self.load(library, sample["file"])
        n = round(duration * SR)
        time = np.arange(n, dtype=np.float64) / SR
        tuning = sample.get("tuning_cents", 0)
        detune = ((variant % 7) - 3) * 1.2
        ratio = 2 ** ((note - root) / 12 + (detune - tuning) / 1200)
        if lead:
            # Recorded strings carry the timbre. A small bend and late vibrato
            # vary playback speed instead of adding an artificial lead oscillator.
            bend = -20 * np.exp(-time * 40)
            vibrato = 12 * np.sin(2 * np.pi * 5.2 * time) * np.clip((time - .16) / .2, 0, 1)
            positions = np.cumsum(ratio * 2 ** ((bend + vibrato) / 1200)) - ratio
        else:
            positions = np.arange(n) * ratio
        wave = np.interp(positions, np.arange(len(original)), original, left=0, right=0)
        release = .065 if lead else .035 if muted else .10
        wave *= np.minimum(time / .002, 1) * np.clip((duration - time) / release, 0, 1)
        if muted:
            wave *= np.exp(-time * 6.5)
            dark = filt(wave, 1500)
            wave = dark * .34 + wave * .66
        wave *= .72 + .28 * velocity / 127
        return wave.astype(np.float32)

    def percussion(self, kind, velocity, variant, duration):
        candidates = [s for s in self.entries["Drums"] if s["kind"] == kind]
        layer = min({s["velocity"] for s in candidates}, key=lambda v: abs(v - velocity))
        candidates = sorted((s for s in candidates if s["velocity"] == layer), key=lambda s: s["round_robin"])
        sample = candidates[variant % len(candidates)]
        audio = self.load("Drums", sample["file"])
        audio = audio[:min(len(audio), round(duration * SR))].copy()
        release = min(len(audio) // 3, round(.04 * SR))
        audio[-release:] *= np.linspace(1, 0, release, dtype=np.float32)[:, None]
        # Preserve recorded dynamic layers; only interpolate gently within a layer.
        audio *= min(1.12, max(.72, velocity / max(layer, 1)))
        return audio


def filter_stereo(audio, frequency, kind="lowpass", order=2):
    return np.stack([filt(audio[:, c], frequency, kind, order) for c in range(2)], axis=1)


def amp(audio, lead=False):
    """Oversampled soft clipping and a dark guitar-speaker EQ."""
    extended = np.concatenate((audio[-SR:], audio, audio[:SR]), axis=0)
    extended = filter_stereo(extended, 95 if not lead else 130, "highpass")
    bright = extended - filter_stereo(extended, 720)
    preamp = extended * .68 + bright * .60
    oversampled = resample_poly(preamp, 2, 1, axis=0)
    driven = np.tanh(oversampled * (18 if lead else 14) + .025) - np.tanh(.025)
    driven = resample_poly(driven, 1, 2, axis=0).astype(np.float32)
    driven = filter_stereo(driven, 4800 if lead else 4300, order=3)
    body = filter_stereo(driven, [600, 1600], "bandpass", 1)
    driven = filter_stereo(driven + .18 * body, 95, "highpass")
    return driven[SR:-SR] * (.31 if lead else .29)


class BandMix:
    production = "recorded-band-v2"

    def __init__(self, score, instruments):
        self.beat = 60 / score["bpm"]
        self.frames = round(256 * self.beat * SR)
        self.theme = score["id"]
        self.instruments = instruments
        self.bus = {name: np.zeros((self.frames, 2), np.float32)
                    for name in ("rhythm", "lead", "bass", "clean", "drums")}
        self.events = []
        self.counter = 0
        self.random = np.random.default_rng(7182 + sum(map(ord, self.theme)))

    def place(self, bus, audio, beat, volume, pan=0):
        offset = round(beat * self.beat * SR) % self.frames
        if audio.ndim == 1:
            gains = np.array([math.cos((pan + 1) * np.pi / 4),
                              math.sin((pan + 1) * np.pi / 4)], np.float32)
            stereo = audio[:, None] * gains * volume
        else:
            gains = np.sqrt(np.array([1 - pan, 1 + pan], np.float32))
            stereo = audio * gains * volume
        first = min(len(stereo), self.frames - offset)
        self.bus[bus][offset:offset + first] += stereo[:first]
        if first < len(stereo):
            self.bus[bus][:len(stereo) - first] += stereo[first:]

    def note(self, voice, midi, beat, duration, volume, pan=0, wet=.1, variant=0, channel=0):
        self.counter += 1
        section, _ = base.sections(int(beat // 4))
        if voice == "pad":
            # Leave space for the band; the former pad becomes quiet clean strings.
            if section not in ("break", "intro"):
                return
            voice = "clean"
            volume *= .65
        elif voice in ("bell", "pluck"):
            voice = "clean"
            volume *= 1.15
        lead = voice.startswith("lead")
        bus = "lead" if lead else "rhythm" if voice == "guitar" else "bass" if voice == "bass" else "clean"
        library = "Bass" if bus == "bass" else "Guitar"
        while library == "Guitar" and midi > 88:
            midi -= 12
        if bus == "bass":
            volume *= .78
        if bus == "rhythm":
            pan = -.78 if pan < 0 else .78
        human = float(self.random.uniform(-.004, .004))
        if beat == 0:
            human = 0
        duration_seconds = duration * self.beat + (.09 if bus == "clean" else .035)
        velocity = int(np.clip(96 + self.random.normal(0, 6) + (volume / .46 - .8) * (17 if lead else 3), 67, 124))
        variation = self.counter + variant
        wave = self.instruments.pitched(library, int(midi), velocity, variation,
                                       duration_seconds, lead, bus == "rhythm" and duration < .5)
        if bus == "clean":
            wave = filt(wave, 6500)
        self.place(bus, wave, beat + human / self.beat, volume, pan)
        self.events.append((beat, duration, int(midi), velocity, channel))

    def hit(self, kind, beat, volume, pan=0, variant=0):
        self.counter += 1
        mapping = {"open": "open_hat", "tom": ("tom_hi", "tom_mid", "tom_low", "tom_low")[variant % 4],
                   "clave": "snare_rimshot", "shaker": "hat"}
        recorded = mapping.get(kind, kind)
        duration = {"kick": 1.3, "snare": 1.3, "hat": .17, "open": self.beat * .49,
                    "ride": 3.5, "crash": 6, "tom": 1.8, "clave": .25, "shaker": .10}[kind]
        reference = {"kick": .76, "snare": .65, "hat": .34, "open": .34, "ride": .34,
                     "crash": .45, "tom": .44, "clave": .19, "shaker": .12}[kind]
        intensity = min(1.1, volume / reference)
        velocity = int(np.clip(65 + 52 * intensity + self.random.normal(0, 4), 48, 127))
        if kind in ("clave", "shaker"):
            velocity = min(70, velocity)
        gain = {"kick": 1.35, "snare": 1.40, "hat": .70, "open": .72, "ride": .58,
                "crash": .85, "tom": 1.05, "clave": .38, "shaker": .35}[kind]
        audio = self.instruments.percussion(recorded, velocity, self.counter + variant, duration)
        human = 0 if kind == "kick" or beat == 0 else float(self.random.uniform(-.004, .006))
        self.place("drums", audio, beat + human / self.beat, volume * gain, pan * .45)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51,
              "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 37, "shaker": 42}
        self.events.append((beat, .1, gm[kind], velocity, 9))

    def finish(self):
        # Drive summed guitar strings through the amp, so chords interact naturally.
        rhythm = amp(self.bus.pop("rhythm"))
        lead = amp(self.bus.pop("lead"), True)
        bass = self.bus.pop("bass")
        bass = filter_stereo(np.tanh(bass * 1.6) / 1.6, 3800)
        clean = self.bus.pop("clean")
        drums = self.bus.pop("drums")
        # A short room under the close microphones preserves transient punch.
        dry = rhythm + lead + bass + clean + drums * .88
        del bass
        for seconds, gain in ((.031, .07), (.071, .052), (.137, .034)):
            dry += np.roll(drums[:, ::-1], round(seconds * SR), axis=0) * gain
        del drums
        # A restrained guitar delay; tails remain circular at the game loop.
        send = lead * .38 + clean * .55 + rhythm * .045
        del rhythm, lead, clean
        for seconds, gain in ((self.beat * .75, .29), (self.beat * 1.5, .13),
                              (.043, .11), (.097, .07), (.179, .04)):
            dry += np.roll(send[:, ::-1], round(seconds * SR), axis=0) * gain
        del send
        # Warm-start the filter from the previous loop and use a gentle bus ceiling.
        extended = np.concatenate((dry[-SR:], dry), axis=0)
        dry = filter_stereo(extended, 31, "highpass")[SR:]
        del extended
        dry = (1.7 * np.tanh(dry / 1.7)).astype(np.float32)
        # Recorded drums have much larger transient/average ratios than synth
        # drums. A short look-ahead limiter gives the band consistent game loudness
        # while retaining the pick and snare attacks.
        rms = float(np.sqrt(np.mean(dry.astype(np.float64) ** 2)))
        dry *= min(3.0, .178 / max(rms, 1e-9))
        block = 64
        extra = (-len(dry)) % block
        padded = np.concatenate((dry, dry[:extra]), axis=0) if extra else dry
        peaks = np.max(np.abs(padded).reshape(-1, block, 2), axis=(1, 2))
        future = maximum_filter1d(peaks, size=9, mode="wrap")
        requested = np.minimum(1, .86 / np.maximum(future, 1e-6))
        gains = np.empty(len(requested), np.float32)
        gain = 1.0
        release = 1 - math.exp(-block / (SR * .08))
        for _ in range(3):
            for i, target in enumerate(requested):
                gain = float(target) if target < gain else gain + (float(target) - gain) * release
                gains[i] = gain
        points = np.arange(len(gains) + 1) * block
        smooth = np.interp(np.arange(len(dry)), points, np.r_[gains, gains[0]])
        dry *= smooth[:, None]
        dry *= min(1.0, .84 / max(float(np.max(np.abs(dry))), 1e-9))
        return dry


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--theme", default="all")
    args = parser.parse_args()
    scores = json.loads((Path(__file__).parent / "score.json").read_text(encoding="utf-8"))["tracks"]
    selected = [s for s in scores if args.theme == "all" or s["id"] == args.theme]
    if not selected:
        parser.error("Unknown theme " + args.theme)
    instruments = Instruments()
    for score in selected:
        base.render(score, BandMix(score, instruments))
