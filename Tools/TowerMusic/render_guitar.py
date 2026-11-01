"""Render the authored Tower score as an electric-guitar rock band with brass.

Recorded CC0 guitar, bass and drum samples (ArtSources/Music/Samples) are
played through a multi-stage amplifier model with a synthetic 4x12 cabinet
response: double-tracked high-gain rhythm guitars plus a centred third track
in the wide sections, a compressed lead channel with delay, a chorused clean
channel for arpeggios, a driven bass and a punchy drum bus. A synthesized
brass section (brass.py) sustains the hook chords, doubles the hook melody and
stabs the accents, and a wide string pad fills the hook, solo and break. A
short room response glues the band. No soundtrack audio, samples or melodies
from any reference are used.
Usage: python Tools/TowerMusic/render_guitar.py [--theme lava]
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
from render_synth import chorus, ping_pong, reverb, limit, dbfs, drum as synth_drum, pad as synth_pad
import brass as brass_section
from scipy.signal import lfilter, resample_poly

HEAVY = ("lava", "rock", "ruins")
OVERSAMPLE = 4


def interpolate(signal, positions):
    """Catmull-Rom cubic resampling: far less aliasing than linear interpolation when re-pitching samples."""
    n = len(signal)
    index = np.floor(positions).astype(np.int64)
    frac = positions - index
    valid = (index >= 1) & (index < n - 2)
    index = np.clip(index, 1, max(1, n - 3))
    p0, p1, p2, p3 = signal[index - 1], signal[index], signal[index + 1], signal[index + 2]
    a = -.5 * p0 + 1.5 * p1 - 1.5 * p2 + .5 * p3
    b = p0 - 2.5 * p1 + 2 * p2 - .5 * p3
    c = -.5 * p0 + .5 * p2
    return np.where(valid, ((a * frac + b) * frac + c) * frac + p1, 0.0)


class GuitarInstruments(Instruments):
    """Sample playback with cubic interpolation, explicit detune and a modern late lead vibrato."""

    def pitched(self, library, note, velocity, variant, duration, lead=False, muted=False, cents=0.0):
        candidates = self.entries[library]
        root = min({s["root_midi"] for s in candidates}, key=lambda key: abs(key - note))
        candidates = [s for s in candidates if s["root_midi"] == root]
        layer = min({s["velocity"] for s in candidates}, key=lambda v: abs(v - velocity))
        candidates = sorted((s for s in candidates if s["velocity"] == layer), key=lambda s: s["round_robin"])
        sample = candidates[variant % len(candidates)]
        original = self.load(library, sample["file"])
        n = round(duration * SR)
        time = np.arange(n, dtype=np.float64) / SR
        ratio = 2 ** ((note - root) / 12 + (cents - sample.get("tuning_cents", 0)) / 1200)
        if lead:
            bend = -18 * np.exp(-time * 45)
            vibrato = 22 * np.sin(TAU * 5.4 * time + (variant % 5) * 1.3) * np.clip((time - .28) / .35, 0, 1)
            positions = np.cumsum(ratio * 2 ** ((bend + vibrato) / 1200)) - ratio
        else:
            positions = np.arange(n) * ratio
        wave = interpolate(original, positions)
        release = .065 if lead else .035 if muted else .10
        wave *= np.minimum(time / .002, 1) * np.clip((duration - time) / release, 0, 1)
        if muted:
            wave *= np.exp(-time * 6.5)
            dark = filt(wave, 1500)
            wave = dark * .34 + wave * .66
        wave *= .72 + .28 * velocity / 127
        return wave.astype(np.float32)


def biquad(kind, frequency, q, gain_db=0.0):
    """RBJ cookbook peaking / low-shelf / high-shelf coefficients."""
    amp = 10 ** (gain_db / 40)
    w0 = TAU * frequency / SR
    alpha = math.sin(w0) / (2 * q)
    c = math.cos(w0)
    if kind == "peaking":
        b = (1 + alpha * amp, -2 * c, 1 - alpha * amp)
        a = (1 + alpha / amp, -2 * c, 1 - alpha / amp)
    else:
        sq = 2 * math.sqrt(amp) * alpha
        sign = 1 if kind == "lowshelf" else -1
        b = (amp * ((amp + 1) - sign * (amp - 1) * c + sq),
             sign * 2 * amp * ((amp - 1) - sign * (amp + 1) * c),
             amp * ((amp + 1) - sign * (amp - 1) * c - sq))
        a = ((amp + 1) + sign * (amp - 1) * c + sq,
             -sign * 2 * ((amp - 1) + sign * (amp + 1) * c),
             (amp + 1) + sign * (amp - 1) * c - sq)
    return np.array(b) / a[0], np.array(a) / a[0]


def eq(signal, *stages):
    for kind, frequency, q, gain in stages:
        b, a = biquad(kind, frequency, q, gain)
        signal = lfilter(b, a, signal, axis=0)
    return signal.astype(np.float32)


def dc_block(signal, fs):
    a = 1 - math.exp(-TAU * 15 / fs)
    return (signal - lfilter([a], [1, -(1 - a)], signal)).astype(np.float32)


def saturate(mono, stages, bias, edge=False):
    """Cascaded asymmetric soft clipping at 4x oversampling, like stacked preamp triodes.

    `edge` adds a light hard clip after the tube stages for a sharper, more aggressive top end.
    """
    up = resample_poly(mono, OVERSAMPLE, 1).astype(np.float32)
    for gain in stages:
        up = np.tanh(up * gain + bias) - math.tanh(bias)
        up = dc_block(up, OVERSAMPLE * SR)
    if edge:
        up = np.clip(up * 1.15, -1, 1).astype(np.float32)
    return resample_poly(up, 1, OVERSAMPLE).astype(np.float32)


def sustain(mono, floor, strength, speed=20):
    """Envelope levelling before the preamp: tames the pick spike and holds the decaying tail up."""
    envelope = filt(np.abs(mono), speed, order=1)
    return (mono * (floor / np.maximum(envelope, floor * .25)) ** strength).astype(np.float32)


@lru_cache(maxsize=4)
def cabinet_ir(bright=False):
    """A synthetic 4x12 response: open top end, tight low end, two presence peaks, light close-mic reflections."""
    n = round(.05 * SR)
    ir = np.zeros(n)
    ir[0] = 1
    ir = filt(ir, 6800 if bright else 5600, order=3)
    ir = filt(ir, 90, "highpass")
    ir = eq(ir, ("peaking", 110, 1.2, 1), ("peaking", 350, 1.0, -2), ("peaking", 2800, 1.4, 2.5), ("peaking", 4500, 1.6, 2))
    reflections = ir.copy()
    for ms, gain in ((.6, .35), (1.9, .15), (4.1, .1)):
        shift = round(ms * SR / 1000)
        reflections[shift:] += ir[:n - shift] * gain
    return reflections / math.sqrt(float((reflections ** 2).sum()))


def cabinet(mono, bright=False):
    """Circular convolution over the loop, so the cabinet tail wraps to bar one."""
    n = len(mono)
    return np.fft.irfft(np.fft.rfft(mono.astype(np.float64), n) * np.fft.rfft(cabinet_ir(bright), n), n).astype(np.float32)


@lru_cache(maxsize=1)
def room_ir():
    """A small live room (early reflections, 0.3 s decay) that glues the band without washing it out."""
    n = round(.3 * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(515)
    ir = np.zeros((n, 2))
    for channel in range(2):
        tail = rng.normal(0, 1, n) * np.exp(-t * 14)
        ir[:, channel] = filt(filt(tail, 6000), 150, "highpass") * np.minimum(t / .004, 1)
        for ms, gain in ((7, .5), (13, .4), (19, .3), (29, .25), (41, .18)):
            ir[round((ms + channel * 2) * SR / 1000), channel] += gain
    return ir / math.sqrt(float((ir ** 2).sum(axis=0).max()))


def convolve(stereo, ir):
    n = len(stereo)
    out = np.empty_like(stereo)
    for channel in range(2):
        out[:, channel] = np.fft.irfft(np.fft.rfft(stereo[:, channel].astype(np.float64), n) * np.fft.rfft(ir[:, channel], n), n)
    return out


def looped(mono, chain):
    """Warm-start filters from the previous loop pass, then trim."""
    extended = np.concatenate((mono[-SR:], mono))
    return chain(extended)[SR:]


def compress(mono, threshold, ratio, makeup=1.0):
    envelope = filt(np.abs(mono), 12, order=1)
    gain = (threshold / np.maximum(envelope, threshold)) ** (1 - 1 / ratio)
    return (mono * filt(gain, 30, order=1) * makeup).astype(np.float32)


def rhythm_amp(stereo, heavy):
    def chain(x):
        x = filt(x, 150, "highpass")
        x = sustain(x, .12, .45)
        x = x + 1.6 * filt(x, [700, 2500], "bandpass", 1)
        x = eq(x, ("highshelf", 2500, .7, 1))
        x = saturate(x * (1.1 if heavy else .85), (5.5, 3.5, 2.2) if heavy else (4.0, 2.6, 1.8), .1, edge=True)
        return eq(x, ("peaking", 350, 1.0, -3 if heavy else -2), ("lowshelf", 150, .8, -2),
                  ("peaking", 3800, 1.2, .5))
    out = np.empty_like(stereo)
    for channel in range(2):
        out[:, channel] = cabinet(looped(stereo[:, channel], chain))
    return out


def lead_amp(stereo):
    """A modern lead channel: tight low cut, boosted mids into smooth stacked gain, fizz notch, compression."""
    def chain(x):
        x = filt(x, 200, "highpass")
        x = sustain(x, .12, .7)
        x = x + 1.6 * filt(x, [700, 2200], "bandpass", 1)
        x = eq(x, ("highshelf", 3000, .7, 2))
        x = saturate(x, (6.5, 3.5, 2.0), .12)
        return eq(x, ("peaking", 350, 1.0, -3), ("peaking", 1800, 1.2, 2), ("peaking", 6500, 1.5, -3))
    out = np.empty_like(stereo)
    for channel in range(2):
        out[:, channel] = compress(cabinet(looped(stereo[:, channel], chain), True), .12, 3.0, 1.3)
    return out


@lru_cache(maxsize=1)
def hall_ir():
    """A long, dark hall (2.6 s) with 20 ms pre-delay for the lead guitar."""
    n = round(2.6 * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(2024)
    ir = np.zeros((n, 2))
    for channel in range(2):
        tail = rng.normal(0, 1, n) * np.exp(-t * 2.2)
        ir[:, channel] = filt(filt(tail, 4000), 200, "highpass") * np.clip((t - .02) / .03, 0, 1)
    return ir / math.sqrt(float((ir ** 2).sum(axis=0).max()))


def clean_amp(stereo):
    def chain(x):
        x = filt(x, 90, "highpass")
        x = saturate(x, (1.4,), .05)
        return eq(x, ("peaking", 2500, 1.0, 2), ("lowshelf", 200, .8, -1))
    out = np.empty_like(stereo)
    for channel in range(2):
        out[:, channel] = cabinet(looped(stereo[:, channel], chain), True)
    return out


def bass_amp(stereo):
    def chain(x):
        x = filt(x, 35, "highpass")
        body = np.tanh(x * 1.8) / 1.8
        grit = np.tanh(filt(x, [700, 2500], "bandpass", 1) * 5) / 5
        x = filt(body + .18 * grit, 5000)
        return compress(x, .15, 2.5, 1.15)
    out = np.empty_like(stereo)
    for channel in range(2):
        out[:, channel] = looped(stereo[:, channel], chain)
    return out


def active_level(stereo):
    window = round(.05 * SR)
    usable = len(stereo) - len(stereo) % window
    blocks = np.sqrt(np.mean(stereo[:usable].astype(np.float64).reshape(-1, window, 2) ** 2, axis=(1, 2)))
    active = blocks[blocks > 1e-3]
    return 20 * math.log10(float(np.sqrt(np.mean(active ** 2)))) if len(active) else -120.0


def normalize(stereo, target_db):
    """Scale a bus so its active-window RMS sits at the target; dynamics inside the bus are untouched."""
    return (stereo * 10 ** ((target_db - active_level(stereo)) / 20)).astype(np.float32)


class GuitarMix:
    production = "guitar-brass-v6"
    write_midi = True
    programs = {0: 30, 1: 33, 2: 30, 3: 48, 4: 27, 5: 30, 6: 30, 7: 61, 9: 0}

    def __init__(self, score, instruments):
        self.beat = 60 / score["bpm"]
        self.frames = round(256 * self.beat * SR)
        self.theme = score["id"]
        self.instruments = instruments
        self.bus = {name: np.zeros((self.frames, 2), np.float32)
                    for name in ("rhythm", "lead", "lead_l", "lead_r", "lead_low", "bass", "clean", "pad", "drums", "brass")}
        self.events = []
        self.counter = 0
        self.stabbed = set()
        self.random = np.random.default_rng(7182 + sum(map(ord, self.theme)))

    def brass(self, midi, beat, duration, gain, velocity, bar, players=3, low=None):
        """A small brass section: trumpets above middle C, horns and trombones below."""
        if low is None:
            low = midi < 62
        duration_ms = int(round((duration * self.beat + .05) * 1000))
        for wave, offset, pan in brass_section.section(midi, duration_ms, velocity, bar, low, players):
            self.place("brass", wave, beat + offset / self.beat, gain / players * 1.4, pan)
        self.events.append((beat, duration, int(midi), int(velocity), 7))

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
        bar = int(beat // 4) % 64
        section, local = base.sections(bar)
        big = section in ("b", "reprise", "solo")
        if voice == "pad":
            # Verses stay a dry guitar band. Strings widen the hook, solo, reprise and the quiet break, played
            # twice with opposite pans; the brass section sustains the hook and reprise chords on top.
            if section == "a":
                return
            duration_ms = int(round((duration * self.beat + .25) * 1000))
            for side, offset in ((-.6, 0.0), (.6, .018)):
                self.place("pad", synth_pad(self.theme, midi, duration_ms + (0 if side < 0 else 7)), beat + offset, volume * .9, side)
            self.events.append((beat, duration, midi, 72, channel))
            if section in ("b", "reprise"):
                self.brass(midi, beat + .02, duration * .96, .22 * volume / .11, 92 + local % 2 * 6, bar)
            elif section == "solo" and midi < 64:
                self.brass(midi, beat + .02, duration * .96, .14, 84, bar, low=True)
            return
        if voice in ("bell", "pluck"):
            voice = "clean"
            volume *= 1.15
        lead = voice.startswith("lead")
        bus = "lead" if lead else "rhythm" if voice == "guitar" else "bass" if voice == "bass" else "clean"
        library = "Bass" if bus == "bass" else "Guitar"
        while library == "Guitar" and midi > 88:
            midi -= 12
        if bus == "rhythm":
            pan = -.8 if pan < 0 else .8
            at = round((beat % 4) * 4) / 4
            downbeat = bar in (4, 20, 28, 44, 52, 60) and at == 0
            accent = downbeat or (section == "a" and local >= 8 and bar % 2 == 1 and at in (0, 1.5, 2.5))
            if accent and (bar, at) not in self.stabbed:
                # Horn-section stabs double the guitar accents with the power-chord tones an octave up.
                self.stabbed.add((bar, at))
                for interval, weight in ((12, 1.0), (19, .8), (24, .6)):
                    self.brass(midi + interval, beat, 1.0 if downbeat else .3, .5 * weight, 112, bar, players=2)
        human = 0 if beat == 0 else float(self.random.uniform(-.005, .005))
        seconds = duration * self.beat + (.09 if bus == "clean" else .035)
        velocity = int(np.clip(96 + self.random.normal(0, 8) + (volume / .46 - .8) * (17 if lead else 3), 67, 124))
        chug = bus == "rhythm" and duration < .5
        # Chugs keep their full brightness into the amp; only the envelope is tightened (hold, then a 30 ms release).
        wave = self.instruments.pitched(library, midi, velocity, self.counter + variant,
                                       seconds + (.1 if chug else 0), lead, False)
        if chug:
            n = round(seconds * SR)
            t = np.arange(n) / SR
            wave = (wave[:n] * np.clip((seconds - t) / .03, 0, 1) * np.exp(-t * 3)).astype(np.float32)
        if bus == "clean":
            wave = filt(wave, 6500)
        self.place(bus, wave, beat + human / self.beat, volume, pan)
        self.events.append((beat, duration, midi, velocity, channel))
        if lead and channel == 0:
            # Modern lead stack: two detuned, time-shifted doubles at the sides and, in the wide sections,
            # an octave-down layer. Each goes through its own amp pass so the layers do not intermodulate.
            for side_bus, rr, cents, pan_side, shift in (("lead_l", 7, 4.0, -.45, .008), ("lead_r", 11, -4.0, .45, -.006)):
                double = self.instruments.pitched(library, midi, max(67, velocity - 4), self.counter + variant + rr, seconds, True, False, cents)
                self.place(side_bus, double, beat + (human + shift) / self.beat, volume, pan_side)
            if big and midi - 12 >= 40:
                low = self.instruments.pitched(library, midi - 12, max(67, velocity - 10), self.counter + 3, seconds, True, False, 0.0)
                self.place("lead_low", low, beat + (human + .004) / self.beat, volume, 0)
        if bus == "rhythm" and big and not chug and pan < 0 and volume > .1:
            # A third, centred rhythm track an octave up thickens the wide sections.
            third = self.instruments.pitched("Guitar", min(midi + 12, 88), max(67, velocity - 8), self.counter + 5, seconds, False, False)
            self.place("rhythm", third, beat + human / self.beat + .009, volume * .4, 0)
        if lead and channel == 0 and section in ("b", "reprise") and duration >= .4:
            # Trumpets double the hook melody under the lead guitar.
            self.brass(midi - 12 if midi > 79 else midi, beat + .015, duration * .95, .42 * volume / .46, 104, bar)

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
        human = 0 if kind == "kick" or beat == 0 else float(self.random.uniform(-.003, .004))
        self.place("drums", audio, beat + human / self.beat, volume * gain, pan * .45)
        if kind == "kick":
            # A low synthesized thump under the recorded kick gives the band a modern, tight bottom end.
            self.place("drums", synth_drum("kick", variant), beat, volume * .3, 0)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51,
              "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 37, "shaker": 42}
        self.events.append((beat, .1, gm[kind], velocity, 9))

    def finish(self):
        beat = self.beat
        heavy = self.theme in HEAVY
        rhythm = normalize(rhythm_amp(self.bus.pop("rhythm"), heavy), -19.0 if heavy else -20.0)
        lead = normalize(lead_amp(self.bus.pop("lead")), -18.0)
        doubles = lead_amp(self.bus.pop("lead_l")) + lead_amp(self.bus.pop("lead_r"))
        lead += normalize(doubles, -22.0)
        del doubles
        low = self.bus.pop("lead_low")
        if np.any(low):
            lead += normalize(lead_amp(low), -25.5)
        del low
        lead = chorus(lead, .7, 1.0, 7, .08)
        lead += filter_stereo(ping_pong(lead, beat, ((.75, .24), (1.5, .12), (2.25, .05))), 3500)
        lead += convolve(lead, hall_ir()) * .25
        clean = normalize(clean_amp(self.bus.pop("clean")), -25.0)
        clean = chorus(clean, .9, 2.0, 10, .4)
        clean += ping_pong(clean, beat, ((.375, .18),))
        bass = normalize(bass_amp(self.bus.pop("bass")), -18.5)
        pad = normalize(chorus(self.bus.pop("pad"), .4, 3.2, 16, .55), -29.5)
        brass = self.bus.pop("brass")
        brass = normalize(eq(np.tanh(brass * 1.3) / 1.3, ("peaking", 300, 1.0, -2), ("peaking", 2500, 1.2, 2)), -20.0)
        drums = self.bus.pop("drums")
        drums = normalize(drums + .3 * np.tanh(drums * 4) / 4, -16.5)
        print("bus dBFS (active)", {name: round(active_level(bus), 1) for name, bus in
                                   (("rhythm", rhythm), ("lead", lead), ("clean", clean), ("bass", bass),
                                    ("pad", pad), ("brass", brass), ("drums", drums))}, flush=True)
        send = lead * .28 + clean * .45 + pad * .5 + brass * .4 + drums * .10 + rhythm * .03
        # A small shared room under guitars, brass and drums makes the layers sit in one space.
        glue = convolve(rhythm * .7 + drums * .8 + brass + bass * .3, room_ir()) * .18
        dry = rhythm + lead + clean + bass + pad + brass + drums + glue
        del rhythm, lead, clean, bass, pad, brass, drums, glue
        dry += reverb(send)
        del send
        extended = np.concatenate((dry[-SR:], dry), axis=0)
        # Loop-aware low cut and a touch of air on the master.
        dry = eq(filter_stereo(extended, 31, "highpass"), ("highshelf", 9000, .7, 1.5))[SR:]
        del extended
        dry = (1.7 * np.tanh(dry / 1.7)).astype(np.float32)
        rms = float(np.sqrt(np.mean(dry.astype(np.float64) ** 2)))
        dry *= min(3.0, .178 / max(rms, 1e-9))
        return limit(dry)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--theme", default="all")
    args = parser.parse_args()
    scores = json.loads((Path(__file__).parent / "score.json").read_text(encoding="utf-8"))["tracks"]
    selected = [s for s in scores if args.theme == "all" or s["id"] == args.theme]
    if not selected:
        parser.error("Unknown theme " + args.theme)
    instruments = GuitarInstruments()
    for score in selected:
        base.render(score, GuitarMix(score, instruments))
        synth_pad.cache_clear()
        brass_section.clear_cache()
