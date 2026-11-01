"""Brass-section voices for the Tower score, played from recorded CC0 samples.

The VSCO 2 Community Edition trumpet, F horn and tenor trombone recordings in
ArtSources/Music/Samples/Brass are chosen by register, pitched by resampling
to the nearest recorded note, and played as a small section of players with
different round robins, slight detune and time offsets so the part reads as
an ensemble. Sustains use the `sus` articulation with a release envelope;
short notes use the recorded `stac` articulation. If the sample manifest is
missing, a synthesized brass voice (kept below) is used instead.
"""
from __future__ import annotations

from functools import lru_cache
import json
import math
from pathlib import Path

import numpy as np
from scipy.signal import lfilter, resample_poly

from render import SR, TAU, filt, ROOT
from render_synth import saw, pulse, sweep, hz, BLOCK

SAMPLE_ROOT = ROOT / "ArtSources/Music/Samples/Brass"
STACCATO_BELOW_MS = 360


class Kit:
    def __init__(self, root=SAMPLE_ROOT):
        manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
        self.root = root
        self.samples = {}
        for sample in manifest["samples"]:
            self.samples.setdefault((sample["instrument"], sample["articulation"]), []).append(sample)
        # The three instruments were recorded at very different levels. One gain per instrument and
        # articulation brings their median peaks together while keeping each group's velocity layers intact.
        self.level = {group: .3 / float(np.median([s["peak"] for s in samples]))
                      for group, samples in self.samples.items()}

    @lru_cache(maxsize=512)
    def load(self, file):
        audio, rate = __import__("soundfile").read(str(self.root / file), dtype="float32", always_2d=True)
        if rate != SR:
            divisor = math.gcd(rate, SR)
            audio = resample_poly(audio, SR // divisor, rate // divisor, axis=0).astype(np.float32)
        return audio

    def pick(self, instrument, articulation, midi, velocity, rr):
        candidates = self.samples[(instrument, articulation)]
        root = min({s["root_midi"] for s in candidates}, key=lambda key: (abs(key - midi), key))
        candidates = [s for s in candidates if s["root_midi"] == root]
        layer = min({s["velocity"] for s in candidates}, key=lambda v: abs(v - velocity))
        candidates = sorted((s for s in candidates if s["velocity"] == layer), key=lambda s: s["round_robin"])
        return candidates[rr % len(candidates)]

    def voice(self, instrument, articulation, midi, velocity, rr, duration, detune_cents):
        sample = self.pick(instrument, articulation, midi, velocity, rr)
        original = self.load(sample["file"])
        ratio = 2 ** ((midi - sample["root_midi"]) / 12 + (detune_cents - sample.get("tuning_cents", 0)) / 1200)
        if articulation == "stac":
            n = min(round(len(original) / ratio), round((duration + .45) * SR))
        else:
            n = round(duration * SR)
        positions = np.arange(n) * ratio
        index = np.arange(len(original))
        wave = np.stack([np.interp(positions, index, original[:, c], left=0, right=0) for c in range(original.shape[1])], axis=1)
        t = np.arange(n) / SR
        if articulation == "stac":
            fade = min(n // 3, round(.06 * SR))
            envelope = np.ones(n)
            envelope[-fade:] = np.linspace(1, 0, fade)
        else:
            envelope = np.clip((duration - t) / .09, 0, 1)
        wave *= (envelope * (.78 + .22 * velocity / 127) * self.level[(instrument, articulation)])[:, None]
        return wave.astype(np.float32)


_kit = None


def kit():
    global _kit
    if _kit is None and (SAMPLE_ROOT / "manifest.json").exists():
        _kit = Kit()
    return _kit


def instrument_for(midi, low):
    if low and midi < 70:
        return "horn" if midi >= 48 else "trombone"
    if midi >= 60:
        return "trumpet"
    return "horn" if midi >= 50 else "trombone"


@lru_cache(maxsize=None)
def sampled_voice(instrument, articulation, midi, duration_ms, velocity, rr, detune_cents):
    return kit().voice(instrument, articulation, midi, velocity, rr, duration_ms / 1000, detune_cents)


def section(midi, duration_ms, velocity, variant, low, players=3, spread=.35):
    """Return (wave, offset_seconds, pan) for each player of a small section."""
    out = []
    centre = (players - 1) / 2
    if kit() is None:
        for k in range(players):
            seed = (variant % 4) * 4 + k
            wave = voice(int(midi), int(duration_ms), int(velocity), seed, bool(low))
            offset = (k - centre) * .011 + ((seed * 37) % 7 - 3) * .0015
            out.append((wave, offset, (k - centre) / max(1, centre) * spread))
        return out
    instrument = instrument_for(int(midi), bool(low))
    articulation = "stac" if duration_ms < STACCATO_BELOW_MS else "sus"
    for k in range(players):
        rr = (variant % 4) + k
        detune = (k - centre) * 6.0
        player_velocity = int(np.clip(velocity + (k - centre) * 12, 20, 127))
        wave = sampled_voice(instrument, articulation, int(midi), int(duration_ms), player_velocity, rr, detune)
        offset = (k - centre) * .013 + (((variant + k) * 37) % 7 - 3) * .0015
        out.append((wave, offset, (k - centre) / max(1, centre) * spread))
    return out


def clear_cache():
    sampled_voice.cache_clear()
    voice.cache_clear()


# --- Synthesized fallback (used only when the sample manifest is missing) ---

def _peaking(signal, frequency, q, gain_db):
    amp = 10 ** (gain_db / 40)
    w0 = TAU * frequency / SR
    alpha = math.sin(w0) / (2 * q)
    c = math.cos(w0)
    b = np.array((1 + alpha * amp, -2 * c, 1 - alpha * amp))
    a = np.array((1 + alpha / amp, -2 * c, 1 - alpha / amp))
    return lfilter(b / a[0], a / a[0], signal)


@lru_cache(maxsize=None)
def voice(midi, duration_ms, velocity, seed, low):
    duration = duration_ms / 1000
    n = max(64, round(SR * duration))
    t = np.arange(n, dtype=np.float64) / SR
    rng = np.random.default_rng(1200 + seed * 7919 + midi * 13)
    f0 = hz(midi) * 2 ** (rng.uniform(-8, 8) / 1200)
    scoop = -35 * np.exp(-t / .05)
    vibrato = (8 + 4 * velocity / 127) * np.sin(TAU * (5.0 + rng.uniform(-.4, .4)) * t + rng.uniform(0, TAU)) \
        * np.clip((t - .22) / .3, 0, 1)
    freq = f0 * 2 ** ((scoop + vibrato) / 1200)
    raw = saw(freq, n, rng.uniform(0, 1)) * .7 + pulse(freq, n, .32, rng.uniform(0, 1)) * .3
    attack = .05 + .04 * (1 - velocity / 127) + (.03 if low else 0)
    envelope = np.minimum(t / attack, 1) ** 1.6 * np.clip((duration - t) / .09, 0, 1) * (1 - .12 * np.clip(t / 1.5, 0, 1))
    level = envelope[::BLOCK]
    cutoff = (600 if low else 900) + (2600 if low else 3800) * level ** 1.4 * (.55 + .45 * velocity / 127)
    raw = sweep(raw, cutoff, 1.1)
    for frequency, q, gain in (((500, 1.0, 5), (1200, 1.5, 3), (3000, 1.5, -3)) if low
                               else ((900, 1.0, 6), (1800, 1.5, 4), (3500, 1.5, -3))):
        raw = _peaking(raw, frequency, q, gain)
    breath = filt(rng.normal(0, 1, n), [1200, 3500], "bandpass") * .06 * np.minimum(t / .03, 1) * (.5 + .5 * np.exp(-t * 6))
    raw = np.tanh((raw + breath) * (1.2 + .6 * velocity / 127)) * envelope
    raw = filt(raw, 90, "highpass")
    return (raw * .5).astype(np.float32)
