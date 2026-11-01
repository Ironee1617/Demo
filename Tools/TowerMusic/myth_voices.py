"""Synthesized voices for the mythology pieces: winds, bowed strings, plucked
zithers and harps, keyboard tones, drones, hand and frame percussion, gongs
and wind. Every voice is deterministic and band-limited; the recorded sample
libraries (guitar, bass, drum kit, brass) are used by the renderer alongside
these.
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


def _rng(*seed):
    return np.random.default_rng(abs(hash(seed)) % (2 ** 32))


def _glide(freq, f, glide_from, t, seconds=.08):
    """Start a note on the previous pitch and slide to its own over `seconds` (meend / huayin)."""
    if glide_from is None:
        return freq
    return freq * (hz(glide_from) / f) ** np.clip(1 - t / seconds, 0, 1)


@lru_cache(maxsize=None)
def flute(midi, duration_ms, velocity, breath=7, style="flute", glide_from=None):
    """Transverse flute family. Styles: 'flute' (concert), 'bansuri' (breathy, glides), 'dizi' (buzzing
    membrane, quick vibrato) and 'daegeum' (low, very breathy, slow wide nonghyeon vibrato)."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    rate, depth, start = {"daegeum": (4.0, 35, .35), "bansuri": (5.2, 16, .25), "dizi": (6.2, 14, .18)}.get(style, (5.8, 12, .2))
    onset = 25 * np.exp(-t * 30)
    vibrato = depth * np.sin(TAU * rate * t + .5) * np.clip((t - start) / .25, 0, 1)
    freq = _glide(f * 2 ** ((onset + vibrato) / 1200), f, glide_from, t)
    tone = sine(freq, n) + .22 * sine(freq * 2, n) + .07 * sine(freq * 3, n)
    if style == "dizi":
        tone = np.tanh(tone * 2.2) / math.tanh(2.2) + .25 * sine(freq * 3, n) * velocity / 127
    noise = filt(_rng("flute", midi, duration_ms, style).normal(0, 1, n), [max(300.0, f * 1.5), min(9000.0, f * 6)], "bandpass")
    chiff = 2 if style != "daegeum" else 5
    envelope = (1 - np.exp(-t / (.045 if style != "daegeum" else .07))) * np.clip((duration - t) / .08, 0, 1) * (.75 + .25 * velocity / 127)
    raw = (tone + noise * (breath / 100) * (1 + chiff * np.exp(-t * 12))) * envelope
    return (raw * .4).astype(np.float32)


@lru_cache(maxsize=None)
def oboe(midi, duration_ms, velocity):
    """Double reed: narrow pulse with nasal formants around 1 and 2.6 kHz."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    vibrato = 14 * np.sin(TAU * 5.6 * t + .3) * np.clip((t - .25) / .25, 0, 1)
    freq = f * 2 ** (vibrato / 1200)
    raw = pulse(freq, n, .18) * .7 + saw(freq, n, .4) * .3
    raw = eq(filt(raw, 6500), ("peaking", 1000, 2.2, 7), ("peaking", 2600, 2.5, 5), ("lowshelf", 400, .8, -4))
    breath = filt(_rng("oboe", midi).normal(0, 1, n), [2000, 6000], "bandpass") * .02
    envelope = (1 - np.exp(-t / .06)) * np.clip((duration - t) / .09, 0, 1) * (.7 + .3 * velocity / 127)
    return ((raw * .3 + breath) * envelope).astype(np.float32)


@lru_cache(maxsize=None)
def clarinet(midi, duration_ms, velocity):
    """Hollow odd-harmonic single reed."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    vibrato = 8 * np.sin(TAU * 5.0 * t) * np.clip((t - .3) / .3, 0, 1)
    freq = f * 2 ** (vibrato / 1200)
    raw = pulse(freq, n, .5) * .8 + sine(freq, n) * .3
    raw = eq(filt(raw, 3800), ("peaking", 1500, 2.0, 3), ("lowshelf", 300, .8, 2))
    envelope = (1 - np.exp(-t / .05)) * np.clip((duration - t) / .1, 0, 1) * (.75 + .25 * velocity / 127)
    return (raw * .35 * envelope).astype(np.float32)


@lru_cache(maxsize=None)
def glass(midi, duration_ms):
    """Glass harmonica: near-pure tone with a slow swell and shimmer."""
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    raw = np.sin(phase) + .15 * np.sin(2 * phase) * np.exp(-t * 2) + .08 * np.sin(3 * phase)
    envelope = (1 - np.exp(-t / .25)) * np.clip((duration - t) / .3, 0, 1) * (1 + .15 * np.sin(TAU * 3.5 * t))
    return (raw * .4 * envelope).astype(np.float32)


@lru_cache(maxsize=None)
def bowed(midi, duration_ms, velocity, body, glide_from=None):
    """Bowed solo string: 'erhu', 'fiddle' or 'cello' body resonances, bow noise and late vibrato."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    depth = {"erhu": 25, "fiddle": 20, "cello": 16}[body]
    vibrato = depth * np.sin(TAU * 5.5 * t + 1.1) * np.clip((t - .3) / .3, 0, 1)
    freq = _glide(f * 2 ** (vibrato / 1200), f, glide_from, t, .1)
    raw = saw(freq * 2 ** (-3 / 1200), n) + saw(freq * 2 ** (3 / 1200), n, .5) + .3 * pulse(freq, n, .3, .2)
    raw = filt(raw, 3500 if body == "cello" else 5200)
    formants = {"erhu": (("peaking", 600, 2.0, 5), ("peaking", 1300, 2.5, 6), ("peaking", 2800, 2.0, 3)),
                "fiddle": (("peaking", 300, 1.5, 4), ("peaking", 900, 2.0, 5), ("peaking", 2500, 2.0, 3), ("peaking", 4000, 2.0, -2)),
                "cello": (("peaking", 220, 1.5, 5), ("peaking", 500, 2.0, 4), ("peaking", 1100, 2.0, 2))}[body]
    raw = eq(raw, *formants)
    bow = filt(_rng("bow", midi, body).normal(0, 1, n), [2000, 6000], "bandpass") * .03 * (1 + 3 * np.exp(-t * 20))
    attack = .14 if body == "cello" else .09
    envelope = (1 - np.exp(-t / attack)) * np.clip((duration - t) / .12, 0, 1) * (.7 + .3 * velocity / 127)
    return ((raw / 3 + bow) * envelope * .55).astype(np.float32)


@lru_cache(maxsize=None)
def koto(midi, duration_ms, velocity):
    """Plucked zither (gayageum, guzheng): bright pick, quick filter close, slight attack bend."""
    duration, n, t = frame(duration_ms)
    f = hz(midi) * 2 ** (30 * np.exp(-t * 25) / 1200)
    raw = pulse(f, n, .38) * .6 + saw(f, n, .3) * .4
    tb = t[::BLOCK]
    raw = sweep(raw, 1200 + 6000 * np.exp(-tb * 9) * (.6 + .4 * velocity / 127), 1.0)
    raw += filt(_rng("koto", midi).normal(0, 1, n), 3000, "highpass") * np.exp(-t * 300) * .3
    envelope = np.minimum(t / .0015, 1) * np.exp(-t * (3.2 + (midi - 60) * .03)) * np.clip((duration - t) / .05, 0, 1)
    return (raw * envelope * .5).astype(np.float32)


@lru_cache(maxsize=None)
def sitar(midi, duration_ms, velocity):
    """Buzzing plucked string with a long sympathetic tail."""
    duration, n, t = frame(duration_ms)
    f = hz(midi) * 2 ** (20 * np.exp(-t * 20) / 1200)
    raw = pulse(f, n, .12) * .45 + saw(f, n, .3) * .35 + pulse(f, n, .5, .6) * .2
    raw = np.tanh(raw * 2.5) / math.tanh(2.5)
    tb = t[::BLOCK]
    raw = sweep(raw, 1500 + 5500 * np.exp(-tb * 5), 1.1)
    envelope = np.minimum(t / .002, 1) * np.exp(-t * 1.9) * np.clip((duration - t) / .08, 0, 1) * (.7 + .3 * velocity / 127)
    return (raw * envelope * .45).astype(np.float32)


@lru_cache(maxsize=None)
def harp(midi, duration_ms):
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    raw = np.sin(phase) + .4 * np.sin(2 * phase) * np.exp(-t * 3) + .2 * np.sin(3 * phase) * np.exp(-t * 5) + .1 * np.sin(4 * phase) * np.exp(-t * 7)
    envelope = np.minimum(t / .001, 1) * np.exp(-t * 2.0) * np.clip((duration - t) / .06, 0, 1)
    return (raw * envelope * .45).astype(np.float32)


@lru_cache(maxsize=None)
def music_box(midi, duration_ms):
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    raw = np.sin(phase + .9 * np.exp(-t * 6) * np.sin(phase * 4.0)) + .3 * np.sin(phase * 2.99) * np.exp(-t * 10)
    envelope = np.minimum(t / .001, 1) * np.exp(-t * 3.5) * np.clip((duration - t) / .05, 0, 1)
    return (raw * envelope * .4).astype(np.float32)


@lru_cache(maxsize=None)
def piano(midi, duration_ms, velocity):
    """Acoustic-style piano model, returned in stereo.

    Partial amplitudes follow a hammer striking at 1/8 of the string (the comb that gives a piano
    its hollow 7th/8th partials), inharmonicity stiffens the upper partials, each partial decays in
    two stages (prompt sound, then the slow aftersound of the coupled strings), three slightly
    detuned strings beat against each other, velocity opens the spectrum, a short felt thump marks
    the attack, a soundboard resonance colours the body, and low notes sit left, high notes right.
    """
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    strike = .12
    stiffness = 2.5e-4 * (f / 262) ** 1.3 + 1e-5
    bright = (velocity / 127) ** 1.2
    partials = max(4, min(28, int(9000 / f)))
    raw = np.zeros(n)
    strings = (-1.6, 0.0, 1.6) if midi > 44 else (0.0,)
    for detune in strings:
        base = f * 2 ** (detune / 1200)
        for k in range(1, partials + 1):
            comb = abs(math.sin(math.pi * k * strike)) / k ** 1.05
            tilt = bright ** ((k - 1) / 6)
            fast = 2.0 + .9 * k * (f / 262) ** .6
            slow = .45 + .18 * k * (f / 262) ** .6
            envelope_k = .55 * np.exp(-t * fast) + .45 * np.exp(-t * slow)
            raw += comb * tilt * envelope_k * np.sin(TAU * base * k * math.sqrt(1 + stiffness * k * k) * t + k * 1.9)
    raw /= len(strings)
    rng = _rng("piano", midi, velocity)
    thump = filt(rng.normal(0, 1, n), [120, 900], "bandpass") * np.exp(-t * 110) * .35 * velocity / 127
    raw = raw * (2.2 + .8 * bright) + thump
    raw = eq(raw, ("peaking", 180, 1.2, 2.5), ("peaking", 520, 1.5, 1.5), ("peaking", 1400, 1.8, 1.0), ("highshelf", 5000, .8, -3))
    raw = filt(raw, 7500 if midi < 72 else 9500)
    release = .22 if duration > .5 else .12
    raw *= np.minimum(t / .0015, 1) * np.clip((duration - t) / release, 0, 1)
    raw = np.tanh(raw * .8) / math.tanh(.8)
    position = np.clip((midi - 60) / 48, -.6, .6)
    left, right = math.cos((position + 1) * math.pi / 4), math.sin((position + 1) * math.pi / 4)
    shift = max(1, round(.0004 * SR))
    stereo = np.stack([raw * left, np.concatenate((np.zeros(shift), raw[:-shift])) * right], axis=1)
    return (stereo * .42).astype(np.float32)


@lru_cache(maxsize=None)
def drone(midi, duration_ms, kind):
    """'tanpura' shimmering harmonic drone, 'dark' slowly breathing saw drone or 'sub' sine bed."""
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    if kind == "tanpura":
        raw = np.zeros(n)
        for k in range(1, 13):
            raw += k ** -.9 * (1 + .5 * np.sin(TAU * (.1 + .07 * k) * t + k)) * np.sin(TAU * f * k * t)
        raw += .6 * sum(k ** -1.0 * np.sin(TAU * f * 1.5 * k * t + k) for k in range(1, 7))
        raw = filt(raw, 5000) / 4
        envelope = (1 - np.exp(-t / .5)) * np.clip((duration - t) / .6, 0, 1)
        gain = .25
    elif kind == "dark":
        raw = saw(f, n) + saw(f * 2 ** (5 / 1200), n, .5) + sine(f / 2, n)
        tb = t[::BLOCK]
        raw = sweep(raw, 400 + 300 * np.sin(TAU * .08 * tb), 1.2)
        envelope = (1 - np.exp(-t / .8)) * np.clip((duration - t) / .8, 0, 1)
        gain = .3
    else:
        raw = sine(f, n) + .3 * sine(f * 2, n)
        envelope = (1 - np.exp(-t / .6)) * np.clip((duration - t) / .8, 0, 1)
        gain = .4
    return (raw * envelope * gain).astype(np.float32)


@lru_cache(maxsize=256)
def perc(kind, variant=0):
    """Hand and frame percussion, taiko, timpani, tabla strokes, gong and wood."""
    lengths = {"frame_low": .45, "frame_high": .22, "taiko": .8, "timpani": 1.4, "tabla_dha": .5,
               "tabla_tin": .25, "tabla_na": .12, "gong": 3.5, "wood": .08, "boom": 1.6, "bell_low": 3.0}
    t = np.arange(round(SR * lengths[kind]), dtype=np.float64) / SR
    n = len(t)
    rng = _rng("perc", kind, variant)
    noise = rng.normal(0, 1, n)
    if kind == "frame_low":
        raw = sine(85 + 40 * np.exp(-t / .03), n) * np.exp(-t * 9) + filt(noise, 1800) * np.exp(-t * 60) * .25
    elif kind == "frame_high":
        raw = sine(240 + 120 * np.exp(-t / .01), n) * np.exp(-t * 28) * .6 + filt(noise, [2500, 9000], "bandpass") * np.exp(-t * 70) * .5
    elif kind == "taiko":
        raw = sine(62 + 55 * np.exp(-t / .05), n) * np.exp(-t * 5) + filt(noise, 2500) * np.exp(-t * 35) * .35
        raw = np.tanh(raw * 1.8) / math.tanh(1.8)
    elif kind == "timpani":
        raw = sine(66 + 12 * np.exp(-t / .2), n) * np.exp(-t * 2.8) + .3 * sine(66 * 1.5, n) * np.exp(-t * 6)
        raw += filt(noise, 1500) * np.exp(-t * 90) * .2
    elif kind == "tabla_dha":
        raw = sine(118 + 70 * np.exp(-t / .08), n) * np.exp(-t * 7) + .25 * sine(118 * 2.7, n) * np.exp(-t * 30)
        raw += filt(noise, [3000, 9000], "bandpass") * np.exp(-t * 120) * .3
    elif kind == "tabla_tin":
        raw = (sine(620.0, n) + .5 * sine(620 * 1.9, n)) * np.exp(-t * 22) * .6 + filt(noise, [4000, 10000], "bandpass") * np.exp(-t * 160) * .3
    elif kind == "tabla_na":
        raw = filt(noise, [2000, 8000], "bandpass") * np.exp(-t * 110) * .7 + sine(330.0, n) * np.exp(-t * 60) * .3
    elif kind == "gong":
        raw = np.zeros(n)
        for ratio, amp in ((1, 1), (1.5, .6), (2.3, .5), (3.7, .35), (5.1, .25)):
            raw += amp * np.sin(TAU * 110 * ratio * t + ratio) * np.exp(-t * (1.1 + ratio * .3))
        raw = raw / 2.7 * (1 - .4 * np.exp(-t * 3)) + filt(noise, [1500, 9000], "bandpass") * np.exp(-t * 6) * .25
    elif kind == "wood":
        raw = (np.sin(TAU * 1500 * t) + .5 * np.sin(TAU * 2300 * t)) * np.exp(-t * 110) * .5 + filt(noise, [2500, 9000], "bandpass") * np.exp(-t * 220) * .3
    elif kind == "boom":
        raw = sine(44 + 30 * np.exp(-t / .12), n) * np.exp(-t * 2.5) + filt(noise, 800) * np.exp(-t * 14) * .2
        raw = np.tanh(raw * 1.5) / math.tanh(1.5)
    else:  # bell_low: temple bell
        phase = TAU * 196 * t
        raw = np.sin(phase + 1.4 * np.exp(-t * 2) * np.sin(2.0 * phase)) * np.exp(-t * 1.2) + .3 * np.sin(phase * 2.76) * np.exp(-t * 2.5)
        raw *= .6
    raw *= np.minimum(t / .0015, 1) * np.minimum((t[-1] - t) / .02, 1)
    return raw.astype(np.float32)


@lru_cache(maxsize=16)
def wind(duration_ms, variant):
    """Slow, stereo filtered-noise wind bed."""
    duration, n, t = frame(duration_ms)
    rng = _rng("wind", variant)
    out = np.zeros((n, 2), np.float32)
    tb = t[::BLOCK]
    for channel in range(2):
        gust = .5 + .5 * np.sin(TAU * .05 * tb + channel * 2 + variant) * np.sin(TAU * .013 * tb + channel)
        raw = sweep(rng.normal(0, 1, n), 250 + 900 * gust, 1.6)
        out[:, channel] = filt(raw, 120, "highpass") * (.6 + .4 * np.interp(t, tb, gust))
    out *= (np.minimum(t / 1.5, 1) * np.clip((duration - t) / 1.5, 0, 1))[:, None]
    return (out * .2).astype(np.float32)


def clear_caches():
    for function in (flute, oboe, clarinet, glass, bowed, koto, sitar, harp, music_box, piano, drone, perc, wind):
        function.cache_clear()
