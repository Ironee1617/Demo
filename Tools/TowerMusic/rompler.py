"""Rompler-style synthesized voices for the Tower score.

These model the sample-playback synth patches heard in late-1990s PlayStation
action soundtracks: a wide polysynth lead, a sawtooth sequence, an FM electric
piano, a choir pad, crystal bells, a synth brass section and a noise riser.
They are glossy, band-limited and un-resonant on purpose; nothing here is a
chiptune pulse wave. Every voice is deterministic.
"""
from __future__ import annotations

from functools import lru_cache
import math

import numpy as np

from render import SR, TAU, filt
from render_synth import saw, pulse, sine, sweep, hz, BLOCK
from render_guitar import eq


def frame(duration_ms):
    duration = duration_ms / 1000
    n = max(64, round(SR * duration))
    return duration, n, np.arange(n, dtype=np.float64) / SR


@lru_cache(maxsize=None)
def polysynth(midi, duration_ms, velocity, variant):
    """Wide unison-saw polysynth lead: three detuned saws per side, soft top, light late vibrato."""
    duration, n, t = frame(duration_ms)
    vibrato = 9 * np.sin(TAU * 5.3 * t + variant) * np.clip((t - .25) / .3, 0, 1)
    freq = hz(midi) * 2 ** (vibrato / 1200)
    left = sum(saw(freq * 2 ** (d / 1200), n, k * .19) for k, d in enumerate((-11, -4, 3))) / 3
    right = sum(saw(freq * 2 ** (d / 1200), n, k * .23 + .5) for k, d in enumerate((-3, 4, 11))) / 3
    octave = pulse(freq * 2, n, .5, .3) * .12
    tb = t[::BLOCK]
    cutoff = 5500 + 2500 * np.exp(-tb * 6)
    stereo = np.stack([sweep(left + octave, cutoff, .75), sweep(right + octave, cutoff, .75)], axis=1)
    envelope = np.minimum(t / .006, 1) * np.clip((duration - t) / .08, 0, 1)
    stereo = np.tanh(stereo * 1.3) / math.tanh(1.3) * (envelope * (.7 + .3 * velocity / 127))[:, None]
    return (stereo * .5).astype(np.float32)


@lru_cache(maxsize=None)
def sequence(midi, duration_ms, velocity):
    """Sawtooth continuo: a plucky filtered saw for ostinato and arpeggio figures."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = saw(f, n) * .8 + saw(f * 2 ** (7 / 1200), n, .5) * .4 + sine(f / 2, n) * .15
    tb = t[::BLOCK]
    raw = sweep(raw, 900 + 3200 * np.exp(-tb * 14) * (.6 + .4 * velocity / 127), 1.2)
    raw *= np.minimum(t / .002, 1) * np.clip((duration - t) / .04, 0, 1) * np.exp(-t * 4)
    return (raw * .5).astype(np.float32)


@lru_cache(maxsize=None)
def electric_piano(midi, duration_ms, velocity):
    """Two-operator FM electric piano with a tine partial, velocity opening the brightness."""
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    index = (.6 + 1.4 * velocity / 127) * np.exp(-t * 3.5)
    tone = np.sin(phase + index * np.sin(phase + .3))
    tine = np.sin(phase * 14.0) * np.exp(-t * 22) * .08 * velocity / 127
    body = np.sin(phase * 2.0) * np.exp(-t * 5) * .15
    raw = (tone + tine + body) * np.exp(-t * 1.1) * np.minimum(t / .002, 1) * np.clip((duration - t) / .12, 0, 1)
    return (filt(raw, 6000) * .55).astype(np.float32)


@lru_cache(maxsize=None)
def choir(midi, duration_ms):
    """Formant-filtered 'ah' choir pad with a slow swell."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = pulse(f, n, .5) * .5 + saw(f * 2 ** (5 / 1200), n, .4) * .35 + saw(f * 2 ** (-5 / 1200), n, .7) * .35
    raw = filt(raw, 4500)
    raw = eq(raw, ("peaking", 700, 2.5, 8), ("peaking", 1150, 3.0, 6), ("peaking", 2600, 3.0, 4), ("lowshelf", 300, .8, -6))
    raw *= np.minimum(t / .22, 1) * np.clip((duration - t) / .3, 0, 1)
    return (raw * .3).astype(np.float32)


@lru_cache(maxsize=None)
def crystal(midi, duration_ms):
    """Bright inharmonic FM bell (crystal FX)."""
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    raw = np.sin(phase + 2.2 * np.exp(-t * 4) * np.sin(3.01 * phase))
    raw += .35 * np.sin(phase * 5.19 + .5) * np.exp(-t * 9) + .2 * np.sin(phase * 2) * np.exp(-t * 6)
    raw *= np.minimum(t / .002, 1) * np.exp(-t * 3.2) * np.clip((duration - t) / .1, 0, 1)
    return (raw * .45).astype(np.float32)


@lru_cache(maxsize=None)
def synth_brass(midi, duration_ms, velocity):
    """Rompler brass section: four detuned saws with a fast filter attack and mild drive."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = sum(saw(f * 2 ** (d / 1200), n, k * .21) for k, d in enumerate((-14, -5, 4, 13))) / 4
    tb = t[::BLOCK]
    cutoff = 700 + (2200 + 1500 * velocity / 127) * (1 - np.exp(-tb / .03)) * (.6 + .4 * np.exp(-tb * 4))
    raw = np.tanh(sweep(raw, cutoff, 1.0) * 1.6) / math.tanh(1.6)
    raw *= np.minimum(t / .02, 1) * np.clip((duration - t) / .07, 0, 1) * (1 - .1 * np.clip(t / 1.5, 0, 1))
    return (raw * .5).astype(np.float32)


@lru_cache(maxsize=None)
def riser(duration_ms, variant):
    """Filtered-noise sweep rising into a section change (sci-fi FX)."""
    duration, n, t = frame(duration_ms)
    rng = np.random.default_rng(77 + variant)
    noise = rng.normal(0, 1, n)
    tb = t[::BLOCK]
    raw = sweep(noise, 300 * 20 ** (tb / duration), .9)
    raw = filt(raw, 200, "highpass") * (t / duration) ** 2 * np.clip((duration - t) / .02, 0, 1)
    return (raw * .3).astype(np.float32)


def clear_caches():
    for function in (polysynth, sequence, electric_piano, choir, crystal, synth_brass, riser):
        function.cache_clear()
