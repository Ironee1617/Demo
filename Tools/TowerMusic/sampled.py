"""Playback of the curated VSCO 2 CE orchestral samples (ArtSources/Music/Samples/Orchestra).

`Library` picks the nearest recorded note, velocity layer and round robin for an instrument and
articulation, re-pitches it with cubic interpolation (optionally along a pitch curve for glides and
added vibrato), shapes the release and evens out the recorded level differences between groups.
"""
from __future__ import annotations

from functools import lru_cache
import json
import math

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

from render import SR, ROOT
from render_synth import hz
from render_guitar import interpolate

SAMPLE_ROOT = ROOT / "ArtSources/Music/Samples/Orchestra"


class Library:
    def __init__(self, root=SAMPLE_ROOT):
        manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
        self.root = root
        self.groups = {}
        for sample in manifest["samples"]:
            self.groups.setdefault((sample["instrument"], sample["articulation"]), []).append(sample)
        self.level = {group: .3 / float(np.median([s["peak"] for s in samples])) for group, samples in self.groups.items()}

    def has(self, instrument, articulation=None):
        return any(key[0] == instrument and (articulation is None or key[1] == articulation) for key in self.groups)

    @lru_cache(maxsize=1024)
    def load(self, file):
        audio, rate = sf.read(str(self.root / file), dtype="float32", always_2d=True)
        if rate != SR:
            divisor = math.gcd(rate, SR)
            audio = resample_poly(audio, SR // divisor, rate // divisor, axis=0).astype(np.float32)
        return audio

    def pick(self, instrument, articulation, midi, velocity, rr):
        candidates = self.groups[(instrument, articulation)]
        if midi is not None:
            root = min({s["root_midi"] for s in candidates}, key=lambda key: (abs(key - midi), key))
            candidates = [s for s in candidates if s["root_midi"] == root]
        layer = min({s["velocity"] for s in candidates}, key=lambda v: abs(v - velocity))
        candidates = sorted((s for s in candidates if s["velocity"] == layer), key=lambda s: s["round_robin"])
        return candidates[rr % len(candidates)]

    def voice(self, instrument, articulation, midi, velocity, rr, seconds, cents=0.0, glide_from=None, vibrato=None,
              release=.09, attack=None, hold=True):
        """Re-pitched sample of `seconds`; pitched percussion keeps its natural length when `hold` is False."""
        sample = self.pick(instrument, articulation, midi, velocity, rr)
        original = self.load(sample["file"])
        ratio = 2 ** ((midi - sample["root_midi"]) / 12 + (cents - sample.get("tuning_cents", 0)) / 1200)
        natural = len(original) / ratio / SR
        if not hold:
            seconds = min(seconds, natural) if seconds else natural
        n = max(32, round(seconds * SR))
        t = np.arange(n) / SR
        if glide_from is not None or vibrato is not None:
            modulation = np.zeros(n)
            if vibrato is not None:
                rate, depth, start = vibrato
                modulation += depth * np.sin(2 * math.pi * rate * t + .4) * np.clip((t - start) / .25, 0, 1)
            curve = ratio * 2 ** (modulation / 1200)
            if glide_from is not None:
                curve = curve * (hz(glide_from) / hz(midi)) ** np.clip(1 - t / .08, 0, 1)
            positions = np.cumsum(curve) - curve[0]
        else:
            positions = np.arange(n) * ratio
        wave = np.stack([interpolate(original[:, c], positions) for c in range(original.shape[1])], axis=1)
        envelope = np.clip((seconds - t) / release, 0, 1)
        if attack:
            envelope = envelope * np.minimum(t / attack, 1)
        wave *= (envelope * (.75 + .25 * velocity / 127) * self.level[(instrument, articulation)])[:, None]
        return wave.astype(np.float32)

    def hit(self, instrument, velocity, rr, seconds=None, articulation="hit"):
        """An unpitched percussion sample at its natural length (or shortened with a fade)."""
        sample = self.pick(instrument, articulation, None, velocity, rr)
        audio = self.load(sample["file"]) * (self.level[(instrument, articulation)] * (.75 + .25 * velocity / 127))
        if seconds and len(audio) > seconds * SR:
            audio = audio[:round(seconds * SR)].copy()
            fade = min(len(audio) // 3, round(.04 * SR))
            audio[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)[:, None]
        return audio.astype(np.float32)


_library = None


def library():
    global _library
    if _library is None and (SAMPLE_ROOT / "manifest.json").exists():
        _library = Library()
    return _library
