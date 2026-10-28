"""Render the authored Tower score with a late-1990s console synth palette.

The timbres follow the production style of PlayStation-era action soundtracks
such as the Mega Man X4 stage themes: detuned sawtooth leads with portamento
and delayed vibrato, synth-brass stabs, a resonant sequenced synth bass,
string pads, bleep arpeggios, orchestra hits and a punchy drum kit with a
gated snare reverb. Every pitched sound is an oscillator generated here. The
drums layer synthesized 909-style elements over the CC0 recorded kit in
ArtSources/Music/Samples/Drums for body. No audio, samples or melodies from
any soundtrack are used.
Usage: python Tools/TowerMusic/render_synth.py [--theme lava]
"""
from __future__ import annotations

import argparse
from functools import lru_cache
import json
import math
from pathlib import Path

import render as base
from render import np, SR, TAU, filt
from render_band import Instruments, filter_stereo
from scipy.ndimage import maximum_filter1d
from scipy.signal import lfilter

FLAVOR = {"lava": "fire", "rock": "stone", "jungle": "jungle", "frozen": "ice",
          "water": "water", "cloud": "sky", "ruins": "ruins"}
HEAVY = ("lava", "rock", "ruins")
BLOCK = 48


def hz(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def _blep_saw(phase, dt):
    """Sawtooth with PolyBLEP correction at the reset discontinuity."""
    out = 2 * phase - 1
    head = phase < dt
    tt = phase[head] / dt[head]
    out[head] -= tt + tt - tt * tt - 1
    tail = phase > 1 - dt
    tt = (phase[tail] - 1) / dt[tail]
    out[tail] -= tt * tt + tt + tt + 1
    return out


def _phase(freq, n, start):
    dt = np.broadcast_to(np.asarray(freq, dtype=np.float64) / SR, (n,))
    return (start + np.cumsum(dt)) % 1.0, dt


def saw(freq, n, start=0.0):
    phase, dt = _phase(freq, n, start)
    return _blep_saw(phase, dt)


def pulse(freq, n, width=.5, start=0.0):
    phase, dt = _phase(freq, n, start)
    return _blep_saw(phase, dt) - _blep_saw((phase + width) % 1.0, dt)


def sine(freq, n):
    return np.sin(TAU * np.cumsum(np.broadcast_to(np.asarray(freq, dtype=np.float64), (n,))) / SR)


def lowpass(cutoff, q):
    w0 = TAU * min(max(cutoff, 30.0), SR * .45) / SR
    alpha = math.sin(w0) / (2 * q)
    c = math.cos(w0)
    a0 = 1 + alpha
    return (np.array(((1 - c) / 2, 1 - c, (1 - c) / 2)) / a0,
            np.array((1.0, -2 * c / a0, (1 - alpha) / a0)))


def sweep(signal, cutoffs, q):
    """Resonant two-pole lowpass whose cutoff follows `cutoffs`, one value per BLOCK samples."""
    out = np.empty_like(signal)
    state = np.zeros(2)
    last = len(cutoffs) - 1
    for index, start in enumerate(range(0, len(signal), BLOCK)):
        b, a = lowpass(float(cutoffs[min(index, last)]), q)
        out[start:start + BLOCK], state = lfilter(b, a, signal[start:start + BLOCK], zi=state)
    return out


def frame(duration_ms):
    duration = duration_ms / 1000
    n = max(32, round(SR * duration))
    t = np.arange(n, dtype=np.float64) / SR
    return duration, n, t


def drive(signal, amount):
    return np.tanh(signal * amount) / math.tanh(amount) if amount > 1 else signal


@lru_cache(maxsize=None)
def lead(flavor, midi, duration_ms, velocity, glide_from):
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    if glide_from is None:
        freq = np.full(n, f)
    else:
        start = hz(glide_from)
        freq = start * (f / start) ** np.clip(t / .05, 0, 1)
    cents = 20 * np.sin(TAU * 5.6 * t) * np.clip((t - .13) / .22, 0, 1)
    if flavor in ("fire", "stone", "ruins"):
        cents -= 14 * np.exp(-t * 55)
    freq = freq * 2 ** (cents / 1200)

    def detuned(offset, octave=0):
        return freq * 2 ** (offset / 1200 + octave)

    if flavor == "fire":
        raw = sum(saw(detuned(d), n, k * .31) for k, d in enumerate((-9, 0, 9))) * .34
        raw += pulse(detuned(0, -1), n, .5, .5) * .18
    elif flavor == "stone":
        raw = (saw(detuned(-7), n) + saw(detuned(7), n, .4)) * .3 + pulse(detuned(0), n, .5, .2) * .4
        raw += sine(freq / 2, n) * .15
    elif flavor == "jungle":
        raw = pulse(detuned(0), n, .33) * .55 + saw(detuned(5), n, .5) * .35
    elif flavor == "ice":
        width = .5 - .18 * np.sin(TAU * .7 * t)
        raw = pulse(detuned(0), n, width) * .6 + saw(detuned(6), n, .5) * .25 + sine(detuned(0, 1), n) * .08
    elif flavor == "water":
        phase = TAU * np.cumsum(freq) / SR
        raw = pulse(detuned(0), n, .42) * .45 + np.sin(phase + 1.6 * np.exp(-t * 3.5) * np.sin(2 * phase)) * .45
    elif flavor == "sky":
        raw = (saw(detuned(-6), n) + saw(detuned(6), n, .5)) * .35 + pulse(detuned(0, 1), n, .5) * .2
    else:  # ruins: narrow pulse with a nasal resonance
        raw = pulse(detuned(0), n, .17) * .7 + saw(detuned(4), n, .5) * .2
    cutoff = {"fire": 3700, "stone": 3000, "jungle": 4100, "ice": 3450, "water": 3200, "sky": 4800, "ruins": 2750}[flavor]
    q = {"fire": 1.6, "stone": 1.4, "jungle": 2.4, "ice": 1.3, "water": 1.5, "sky": 1.5, "ruins": 3.2}[flavor]
    tb = t[::BLOCK]
    raw = sweep(raw, cutoff * (.6 + .4 * np.exp(-tb * 8)) * (.8 + .2 * velocity / 127), q)
    raw = drive(raw, {"fire": 2.6, "stone": 3.0, "ruins": 2.0, "jungle": 1.4, "sky": 1.3, "ice": 1.0, "water": 1.0}[flavor])
    raw = filt(raw, 120, "highpass")
    raw *= base.envelope(t, duration, .006, .06)
    makeup = {"water": 2.4, "sky": 1.3}.get(flavor, 1.0)
    return (raw * .5 * makeup).astype(np.float32)


@lru_cache(maxsize=None)
def bass(theme, midi, duration_ms, velocity):
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = saw(f, n) * .55 + pulse(f, n, .5, .5) * .45 + sine(f / 2, n) * .12
    bright = theme in ("water", "jungle", "cloud")
    tb = t[::BLOCK]
    cutoffs = ((420 if bright else 300) + (1900 if bright else 1500) * np.exp(-tb * 16)) * (.7 + .3 * velocity / 127)
    raw = drive(sweep(raw, cutoffs, 2.4), 1.7)
    if theme == "water":
        rng = np.random.default_rng(midi)
        raw += filt(rng.normal(0, 1, n), 2000, "highpass") * np.exp(-t * 300) * .25
    raw *= base.envelope(t, duration, .003, .04, .9)
    return (raw * .6).astype(np.float32)


@lru_cache(maxsize=None)
def brass(theme, midi, duration_ms, velocity, variant):
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = sum(saw(f * 2 ** ((d + variant * 1.5) / 1200), n, k * .23) for k, d in enumerate((-13, -4, 5, 12))) / 4
    tb = t[::BLOCK]
    cutoffs = 500 + 3400 * (1 - np.exp(-tb / .03)) * (.5 + .5 * np.exp(-tb * 6)) * (.75 + .25 * velocity / 127)
    raw = drive(sweep(raw, cutoffs, 1.3), 2.8 if theme in HEAVY else 1.5)
    raw = filt(raw, 115, "highpass")
    stab = duration < .3
    env = base.envelope(t, duration, .007, .04 if stab else .09)
    if stab:
        env = env * np.exp(-t * 3.5)
    raw *= env
    return (raw * (.45 if theme in HEAVY else .63)).astype(np.float32)


@lru_cache(maxsize=None)
def pad(theme, midi, duration_ms):
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = saw(f * 2 ** (-7 / 1200), n) * .35 + saw(f * 2 ** (7 / 1200), n, .5) * .35
    raw += pulse(f * 2 ** (3 / 1200), n, .5, .25) * .3
    if theme in ("frozen", "water", "cloud"):
        raw += sine(2 * f, n) * .12
    tb = t[::BLOCK]
    raw = sweep(raw, 900 + 1500 * (1 - np.exp(-tb / .6)), .8)
    raw *= base.envelope(t, duration, .14, .28)
    return (raw * .3).astype(np.float32)


@lru_cache(maxsize=None)
def pluck(midi, duration_ms):
    duration, n, t = frame(duration_ms)
    f = hz(midi)
    raw = pulse(f, n, .5) * .6 + saw(f, n, .5) * .4
    tb = t[::BLOCK]
    raw = sweep(raw, 600 + 6500 * np.exp(-tb * 16), 2.2)
    raw *= base.envelope(t, duration, .001, .04, 6.5)
    return (raw * .55).astype(np.float32)


@lru_cache(maxsize=None)
def bell(midi, duration_ms, glass):
    duration, n, t = frame(duration_ms)
    phase = TAU * hz(midi) * t
    ratio, index = (3.5, 1.9) if glass else (2.0, 1.4)
    raw = np.sin(phase + index * np.exp(-t * 5) * np.sin(ratio * phase)) + .2 * np.sin(2 * phase) * np.exp(-t * 8)
    raw *= base.envelope(t, duration, .002, .06, 3.6)
    return (raw * .5).astype(np.float32)


@lru_cache(maxsize=None)
def orchestra_hit(tonic, chord, variant):
    duration, n, t = frame(950)
    raw = np.zeros(n)
    count = 0
    for interval in chord[:3]:
        for octave in (-12, 0):
            f = hz(tonic + interval + octave)
            for d in (-8, 8):
                raw += saw(f * 2 ** (d / 1200), n, (count * .137) % 1)
                count += 1
    raw /= count
    tb = t[::BLOCK]
    raw = sweep(raw, 700 + 3800 * np.exp(-tb * 6), 1.1)
    rng = np.random.default_rng(900 + variant)
    raw += filt(rng.normal(0, 1, n), [1500, 7000], "bandpass") * np.exp(-t * 35) * .35
    raw = drive(raw, 2.0)
    raw *= np.minimum(t / .004, 1) * np.exp(-t * 4.5) * np.clip((duration - t) / .1, 0, 1)
    return (raw * .6).astype(np.float32)


@lru_cache(maxsize=256)
def drum(kind, variant=0):
    lengths = {"kick": .36, "snare": .19, "clap": .26, "hat": .09, "open": .33,
               "ride": .5, "crash": 1.4, "tom": .42, "clave": .07, "shaker": .07}
    t = np.arange(round(SR * lengths[kind]), dtype=np.float64) / SR
    n = len(t)
    rng = np.random.default_rng(3301 + variant * 31 + list(lengths).index(kind))
    noise = rng.normal(0, 1, n)
    if kind == "kick":
        raw = sine(55 + 120 * np.exp(-t / .028), n) * np.exp(-t * 10)
        raw += filt(noise, 2200, "highpass") * np.exp(-t * 400) * .35
        raw = drive(raw, 1.6) * .9
    elif kind == "snare":
        raw = (.5 * np.sin(TAU * 182 * t) + .25 * np.sin(TAU * 332 * t)) * np.exp(-t * 28)
        raw += filt(noise, [1500, 9000], "bandpass") * .7 * np.exp(-t * 22)
        # Classic gated reverb: a held noise bloom that is cut off abruptly.
        gate = np.where(t < .11, 1.0, np.clip((.14 - t) / .03, 0, 1))
        raw += filt(rng.normal(0, 1, n), [800, 6000], "bandpass") * gate * .3 * (1 - np.exp(-t * 60))
        raw = drive(raw, 1.3) * .7
    elif kind == "clap":
        raw = np.zeros(n)
        for k in range(4):
            start = round(k * .009 * SR)
            raw[start:] += rng.normal(0, 1, n - start) * np.exp(-t[:n - start] * 320)
        raw += rng.normal(0, 1, n) * np.exp(-t * 18) * .6
        raw = filt(raw, [1000, 5500], "bandpass") * .6
    elif kind == "tom":
        f = (190, 150, 118, 96)[variant % 4]
        phase = TAU * np.cumsum(f * (1 + .6 * np.exp(-t / .02))) / SR
        raw = (np.sin(phase) + .3 * np.sin(phase * 1.5)) * np.exp(-t * 9)
        raw += filt(noise, 3000) * np.exp(-t * 90) * .12
        raw = drive(raw, 1.3) * .75
    elif kind == "clave":
        raw = (np.sin(TAU * 1700 * t) + .5 * np.sin(TAU * 2600 * t)) * np.exp(-t * 120) * .5
        raw += filt(noise, [2000, 8000], "bandpass") * np.exp(-t * 200) * .3
    elif kind == "shaker":
        raw = filt(noise, [5000, 12000], "bandpass") * np.exp(-t * 60) * .45
    else:  # hat / open / ride / crash: metallic pulse stack plus noise
        raw = np.zeros(n)
        for k, f in enumerate((3150, 4320, 5640, 6840, 7560, 9000)):
            raw += pulse(f * (1 + variant * .004), n, .5, k * .17)
        raw = raw / 6 + filt(noise, [5000, 14000], "bandpass") * .5
        if kind == "crash":
            raw = filt(noise, [2000, 12000], "bandpass") * .6 + raw * .35
        decay = {"hat": 55, "open": 9, "ride": 7, "crash": 3.4}[kind]
        raw = filt(raw, 3000 if kind == "crash" else 6500, "highpass") * np.exp(-t * decay)
        raw *= {"hat": .5, "open": .45, "ride": .4, "crash": .55}[kind]
    raw *= np.minimum(t / .0012, 1) * np.minimum((t[-1] - t) / .012, 1)
    return raw.astype(np.float32)


def chorus(stereo, rate, depth_ms, base_ms, mix):
    n = len(stereo)
    index = np.arange(n, dtype=np.float64)
    out = np.empty_like(stereo)
    for channel, phase in ((0, 0.0), (1, 2.1)):
        delay = (base_ms + depth_ms * np.sin(TAU * rate * index / SR + phase)) * SR / 1000
        wet = np.interp((index - delay) % n, index, stereo[:, channel])
        out[:, channel] = stereo[:, channel] * (1 - mix * .4) + wet * mix
    return out


def ping_pong(stereo, beat, taps):
    out = np.zeros_like(stereo)
    for i, (beats, gain) in enumerate(taps):
        rolled = np.roll(stereo, round(beats * beat * SR), axis=0)
        out += (rolled[:, ::-1] if i % 2 == 0 else rolled) * gain
    return out


@lru_cache(maxsize=1)
def reverb_ir(seconds=1.8):
    n = round(seconds * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(4411)
    ir = np.zeros((n, 2))
    for channel in range(2):
        tail = rng.normal(0, 1, n) * np.exp(-t * 3.6)
        ir[:, channel] = filt(filt(tail, 5200), 180, "highpass") * np.minimum(t / .02, 1)
    for ms, gain, channel in ((11, .5, 0), (17, .42, 1), (23, .36, 0), (29, .3, 1), (37, .26, 0), (43, .22, 1)):
        ir[round(ms * SR / 1000), channel] += gain
    return ir / math.sqrt(float((ir ** 2).sum(axis=0).max()))


def reverb(send):
    """Circular FFT convolution: tails wrap to the loop start instead of being cut."""
    ir = reverb_ir()
    n = len(send)
    out = np.empty_like(send)
    for channel in range(2):
        spectrum = np.fft.rfft(send[:, channel].astype(np.float64), n) * np.fft.rfft(ir[:, channel], n)
        out[:, channel] = np.fft.irfft(spectrum, n)
    return out


def dbfs(audio):
    return 20 * math.log10(max(float(np.sqrt(np.mean(audio.astype(np.float64) ** 2))), 1e-9))


def limit(dry):
    """Look-ahead peak limiter matching the band master so game loudness stays consistent."""
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


class SynthMix:
    production = "console-synth-v3"
    write_midi = True
    programs = {0: 81, 1: 38, 2: 62, 3: 48, 4: 80, 5: 81, 6: 62, 9: 0}

    def __init__(self, score, instruments):
        self.score = score
        self.beat = 60 / score["bpm"]
        self.frames = round(256 * self.beat * SR)
        self.theme = score["id"]
        self.tonic = score["tonic_midi"]
        self.instruments = instruments
        self.bus = {name: np.zeros((self.frames, 2), np.float32)
                    for name in ("lead", "brass", "bass", "pad", "arp", "drums", "hits")}
        self.events = []
        self.counter = 0
        self.last_lead = None

    def chord_at(self, bar):
        section, local = base.sections(bar)
        if section == "turn":
            return self.score["chords_a"][(local + 4) % 8]
        chords = self.score["chords_b"] if section in ("b", "solo") else self.score["chords_a"]
        return chords[local % len(chords)]

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
        midi = int(midi)
        section, _ = base.sections(int(beat // 4) % 64)
        velocity = int(np.clip(round((96 + (volume / .46 - .8) * 17) / 4) * 4, 60, 124))
        seconds = duration * self.beat

        def ms(value):
            return int(round(value * 1000))

        if voice == "pad":
            bus, gain = "pad", volume * (1.0 if section == "a" else 1.5)
            wave = pad(self.theme, midi, ms(seconds + .25))
        elif voice in ("bell", "pluck"):
            bus, gain = "arp", volume
            if voice == "bell":
                wave = bell(midi, ms(seconds + .2), self.theme in ("frozen", "water", "rock"))
            else:
                wave = pluck(midi, ms(seconds + .08))
        elif voice == "guitar":
            bus, gain = "brass", volume * 1.3
            pan = -.55 if pan < 0 else .55
            wave = brass(self.theme, midi, ms(seconds + .05), velocity, variant)
        elif voice == "bass":
            bus, gain = "bass", volume
            wave = bass(self.theme, midi, ms(seconds + .03), velocity)
        else:
            bus, gain = "lead", volume
            glide = None
            if channel == 0:
                last = self.last_lead
                if last and 0 <= beat - last[1] <= .09 and 0 < abs(midi - last[0]) <= 9:
                    glide = last[0]
                self.last_lead = (midi, beat + duration)
            wave = lead(FLAVOR[self.theme], midi, ms(seconds + .06), velocity, glide)
        self.place(bus, wave, beat, gain, pan)
        self.events.append((beat, duration, midi, velocity, channel))

    def hit(self, kind, beat, volume, pan=0, variant=0):
        self.counter += 1
        bar = int(beat // 4) % 64
        section, _ = base.sections(bar)
        big = section in ("b", "reprise", "solo")
        reference = {"kick": .76, "snare": .65, "hat": .34, "open": .34, "ride": .34,
                     "crash": .45, "tom": .44, "clave": .19, "shaker": .12}[kind]
        velocity = int(np.clip(68 + 50 * min(1.1, volume / reference), 48, 127))
        synth_gain = {"kick": .7, "snare": .8, "hat": .65, "open": .7, "ride": .4,
                      "crash": .5, "tom": .9, "clave": .7, "shaker": .6}[kind]
        self.place("drums", drum(kind, variant), beat, volume * synth_gain, pan * .45)
        recorded = {"kick": ("kick", 1.0, 1.0), "snare": ("snare", 1.0, .9), "hat": ("hat", .6, .17),
                    "open": ("open_hat", .55, self.beat * .49), "ride": ("ride", .7, 3.5),
                    "crash": ("crash", .8, 6.0), "clave": ("snare_rimshot", .5, .25),
                    "tom": (("tom_hi", "tom_mid", "tom_low", "tom_low")[variant % 4], .7, 1.8)}.get(kind)
        if recorded:
            name, gain, seconds = recorded
            audio = self.instruments.percussion(name, velocity, self.counter + variant, seconds)
            self.place("drums", audio, beat, volume * gain, pan * .45)
        if kind == "snare" and big and volume > .5:
            self.place("drums", drum("clap", bar % 3), beat, volume * .5, pan * .45)
        if kind == "crash" and volume >= .4:
            self.place("hits", orchestra_hit(self.tonic, tuple(self.chord_at(bar)), bar % 4), beat, .5, 0)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51,
              "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 37, "shaker": 42}
        self.events.append((beat, .1, gm[kind], velocity, 9))

    def finish(self):
        beat = self.beat
        levels = {}

        def take(name, gain):
            audio = self.bus.pop(name) * gain
            # Whole-track RMS, then RMS over the 50 ms windows where the bus is active,
            # so sparse parts (arpeggio, orchestra hits) are measured at their own level.
            window = round(.05 * SR)
            usable = len(audio) - len(audio) % window
            blocks = np.sqrt(np.mean(audio[:usable].astype(np.float64).reshape(-1, window, 2) ** 2, axis=(1, 2)))
            active = blocks[blocks > 1e-3]
            levels[name] = (round(dbfs(audio), 1),
                            round(20 * math.log10(float(np.sqrt(np.mean(active ** 2)))), 1) if len(active) else None)
            return audio

        lead_bus = chorus(take("lead", 2.0), .9, 1.6, 9, .3)
        lead_bus += ping_pong(lead_bus, beat, ((.75, .26), (1.5, .12)))
        brass_bus = chorus(take("brass", 1.4), .6, 2.0, 12, .25)
        pad_bus = chorus(take("pad", 1.5), .4, 3.2, 16, .55)
        arp_bus = take("arp", 1.3)
        arp_bus += ping_pong(arp_bus, beat, ((.375, .22), (.75, .1)))
        bass_bus = filter_stereo(np.tanh(take("bass", 1.2) * 1.4) / 1.4, 5000)
        drums_bus = take("drums", .85)
        drums_bus += np.tanh(drums_bus * 2.5) / 2.5 * .25
        hits_bus = take("hits", 1.0)
        print("bus dBFS (whole, active)", levels, flush=True)
        send = lead_bus * .30 + brass_bus * .26 + pad_bus * .5 + arp_bus * .3 + drums_bus * .14 + hits_bus * .5
        dry = lead_bus + brass_bus + bass_bus + pad_bus + arp_bus + drums_bus + hits_bus
        del lead_bus, brass_bus, bass_bus, pad_bus, arp_bus, drums_bus, hits_bus
        dry += reverb(send)
        del send
        extended = np.concatenate((dry[-SR:], dry), axis=0)
        # Loop-aware 31 Hz high-pass, then a +2 dB sheen above 5 kHz for the console-synth top end.
        dry = filter_stereo(extended, 31, "highpass")[SR:]
        dry += .26 * filter_stereo(dry, 5000, "highpass")
        del extended
        dry = (1.7 * np.tanh(dry / 1.7)).astype(np.float32)
        rms = float(np.sqrt(np.mean(dry.astype(np.float64) ** 2)))
        dry *= min(3.0, .178 / max(rms, 1e-9))
        return limit(dry)


def clear_caches():
    for function in (lead, bass, brass, pad, pluck, bell, orchestra_hit):
        function.cache_clear()


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
        base.render(score, SynthMix(score, instruments))
        clear_caches()
