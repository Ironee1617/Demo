"""Render the ten mythology pieces in myth_pieces.py.

Each piece names its mode, tempo, form, chords, melodies and the layers active
in every section; this module turns that into notes for the recorded sample
libraries (guitar, bass, drum kit, brass) and the synthesized voices in
myth_voices.py, rompler.py and render_synth.py, mixes the buses into a loop and
writes the WAV master, the game OGG, a MIDI export and an analysis report.
Usage: python Tools/TowerMusic/render_myth.py [--piece olympus]
"""
from __future__ import annotations

import argparse
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import struct

import render as base
from render import np, sf, SR, ROOT, filt
from render_band import filter_stereo
from render_synth import chorus, ping_pong, reverb, limit, drum as synth_drum, pad as strings_pad, bell as fm_bell, lead as synth_lead
from render_guitar import GuitarInstruments, rhythm_amp, clean_amp, bass_amp, eq, room_ir, hall_ir, convolve, normalize, active_level
import brass as brass_section
import rompler
import myth_voices as mv
import sampled
from myth_pieces import PIECES


def render_voice(name, midi, seconds, velocity, variant=0, glide_from=None, instruments=None):
    """A melodic voice by name. Recorded VSCO 2 CE samples are used where the library has the instrument;
    the synthesized voices in myth_voices / rompler / render_synth cover the rest."""
    lib = sampled.library()
    duration_ms = int(round((seconds + .06) * 1000))
    rr = variant
    short = seconds < .32
    if lib is not None:
        if name == "piano" and lib.has("piano"):
            return lib.voice("piano", "sus", midi, velocity, rr, seconds + .35, release=.3)
        if name == "flute" and lib.has("flute"):
            return lib.voice("flute", "stac" if short else "susvib", midi, velocity, rr, seconds + .05, glide_from=glide_from)
        if name in ("bansuri", "dizi", "daegeum") and lib.has("flute"):
            # Non-vibrato flute recordings carry the folk flutes: their own vibrato, glide, breath and (dizi) membrane buzz.
            vibrato = {"bansuri": (5.2, 16, .25), "dizi": (6.2, 14, .18), "daegeum": (4.0, 35, .35)}[name]
            wave = lib.voice("flute", "stac" if short else "susNV", midi, velocity, rr, seconds + .05, glide_from=glide_from,
                             vibrato=None if short else vibrato)
            n = len(wave)
            t = np.arange(n) / SR
            f = 440 * 2 ** ((midi - 69) / 12)
            rng = np.random.default_rng(midi * 7 + variant)
            breath = {"bansuri": .05, "dizi": .03, "daegeum": .09}[name]
            noise = filt(rng.normal(0, 1, n), [max(300.0, f * 1.5), min(9000.0, f * 6)], "bandpass") * breath
            noise *= np.minimum(t / .03, 1) * (1 + 3 * np.exp(-t * 12)) * np.clip((seconds - t) / .08, 0, 1) * .3
            wave = wave + noise[:, None]
            if name == "dizi":
                wave = (wave + .35 * (np.tanh(wave * 4) / 4)).astype(np.float32)
            # The flute recordings stop at C3; the low daegeum register is re-pitched below it and comes out quiet.
            return (wave * (3.0 if name == "daegeum" else 1.4)).astype(np.float32)
        if name == "oboe" and lib.has("oboe"):
            return lib.voice("oboe", "stac" if short else "vib", midi, velocity, rr, seconds + .05)
        if name == "clarinet" and lib.has("clarinet"):
            return lib.voice("clarinet", "stac" if short else "sus", midi, velocity, rr, seconds + .05)
        if name in ("fiddle", "erhu") and lib.has("violin"):
            glide = glide_from if name == "erhu" else None
            wave = lib.voice("violin", "spic" if short else "vib", midi, velocity, rr, seconds + .05, glide_from=glide,
                             vibrato=(5.5, 10, .3) if name == "erhu" and not short else None)
            if name == "erhu":
                wave = eq(wave, ("peaking", 1300, 2.0, 4), ("peaking", 2800, 2.0, 2), ("lowshelf", 300, .8, -3))
            return wave.astype(np.float32)
        if name == "cello" and lib.has("cellos"):
            return lib.voice("cellos", "vib", midi, velocity, rr, seconds + .05, attack=.05)
        if name == "harp" and lib.has("harp"):
            return lib.voice("harp", "pluck", midi, velocity, rr, seconds + .6, release=.15)
        if name == "strings" and lib.has("violins"):
            return lib.voice("violins" if midi >= 55 else "cellos", "vib", midi, velocity, rr, seconds + .15, release=.2, attack=.08)
        if name == "crystal" and lib.has("glock"):
            return lib.voice("glock", "hit", midi, velocity, rr, seconds + .6, release=.2, hold=False)
    if name in ("flute", "bansuri", "dizi", "daegeum"):
        breath = {"flute": 7, "bansuri": 11, "dizi": 6, "daegeum": 18}[name]
        return mv.flute(midi, duration_ms, velocity, breath, name, glide_from)
    if name in ("erhu", "fiddle", "cello"):
        return mv.bowed(midi, duration_ms, velocity, name, glide_from)
    if name == "oboe":
        return mv.oboe(midi, duration_ms, velocity)
    if name == "clarinet":
        return mv.clarinet(midi, duration_ms, velocity)
    if name == "glass":
        return mv.glass(midi, int(round((seconds + .4) * 1000)))
    if name == "harp":
        return mv.harp(midi, int(round((seconds + .5) * 1000)))
    if name == "piano":
        return mv.piano(midi, int(round((seconds + .4) * 1000)), velocity)
    if name == "strings":
        return strings_pad("lava", midi, int(round((seconds + .2) * 1000)))
    if name == "polysynth":
        return rompler.polysynth(midi, duration_ms, velocity, variant % 7)
    if name == "reed":
        return synth_lead("ruins", midi, duration_ms, velocity, None)
    if name == "crystal":
        return rompler.crystal(midi, int(round((seconds + .6) * 1000)))
    if name == "choir_lead":
        return rompler.choir(midi, int(round((seconds + .2) * 1000)))
    if name == "ep":
        return rompler.electric_piano(midi, int(round((seconds + .2) * 1000)), velocity)
    if name == "clean_guitar" and instruments is not None:
        return instruments.pitched("Guitar", min(int(midi), 88), velocity, variant, seconds + .12, False, False)
    raise ValueError(name)


def render_perc(kind, variant, velocity=100):
    """A percussion hit: recorded orchestral percussion where the library has it, else synthesized."""
    lib = sampled.library()
    if lib is not None:
        mapping = {"taiko": ("bassdrum", 3.0), "boom": ("bassdrum", 3.0), "frame_low": ("tumba", 1.5), "frame_high": ("quinto", 1.2),
                   "wood": ("claves", .6), "gong": ("gong", 6.0)}
        if kind in mapping and lib.has(mapping[kind][0]):
            instrument, seconds = mapping[kind]
            wave = lib.hit(instrument, velocity if kind != "boom" else 60, variant, seconds)
            if kind == "boom":
                wave = wave + mv.perc("boom", variant)[:len(wave), None] * .4 if len(mv.perc("boom", variant)) >= len(wave) else wave
            return wave
        if kind == "timpani" and lib.has("timpani"):
            return lib.voice("timpani", "hit", 43 + (variant % 3) * 2 - 2, velocity, variant, 4.0, release=.3, hold=False)
        if kind == "bell_low" and lib.has("tubular"):
            return lib.voice("tubular", "hit", 60 + (variant % 2) * 7, velocity, variant, 5.0, release=.5, hold=False)
    return mv.perc(kind, variant)

BUSES = ("lead", "counter", "pluck", "clean", "pad", "choir", "keys", "bass", "drums", "perc", "brass", "guitar", "drone", "bells", "wind", "fx")
TARGETS = {"lead": -16.0, "counter": -24.0, "pluck": -21.0, "clean": -22.0, "pad": -26.0, "choir": -28.0, "keys": -23.0, "bass": -18.5,
           "drums": -17.5, "perc": -20.0, "brass": -21.0, "guitar": -19.5, "drone": -27.0, "bells": -25.0, "wind": -34.0, "fx": -30.0}
SPACES = {"room": (.12, .10), "hall": (.22, .28), "cathedral": (.30, .42)}  # (reverb send, hall wet)


def degree_to_semitone(scale, degree):
    size = len(scale)
    octave, step = divmod(int(degree), size)
    return scale[step] + 12 * octave


def chord_tones(piece, spec):
    """Semitones above the tonic for a chord spec: a scale degree (thirds stacked) or explicit tuple."""
    if isinstance(spec, tuple):
        return list(spec)
    scale = piece["scale"]
    return [degree_to_semitone(scale, spec + 2 * k) for k in range(3)]


class MythMix:
    production = "myth-v8"
    programs = {0: 73, 1: 33, 2: 46, 3: 48, 4: 52, 5: 0, 6: 61, 7: 30, 8: 10, 9: 0, 10: 89, 11: 48}

    def __init__(self, piece, instruments):
        self.piece = piece
        self.beat = 60 / piece["bpm"]
        self.beats = piece["beats"]
        self.total_bars = sum(bars for _, bars in piece["form"])
        self.frames = round(self.total_bars * self.beats * self.beat * SR)
        self.instruments = instruments
        self.bus = {name: np.zeros((self.frames, 2), np.float32) for name in BUSES}
        self.events = []
        self.markers = []
        self.counter = 0
        self.last_note = {}
        self.random = np.random.default_rng(1900 + sum(map(ord, piece["id"])))

    # -- placement -------------------------------------------------------------------------
    def place(self, bus, audio, beat, volume, pan=0.0):
        offset = round(beat * self.beat * SR) % self.frames
        if audio.ndim == 1:
            gains = np.array([math.cos((pan + 1) * np.pi / 4), math.sin((pan + 1) * np.pi / 4)], np.float32)
            stereo = audio[:, None] * gains * volume
        else:
            gains = np.sqrt(np.array([1 - pan, 1 + pan], np.float32))
            stereo = audio * gains * volume
        first = min(len(stereo), self.frames - offset)
        self.bus[bus][offset:offset + first] += stereo[:first]
        if first < len(stereo):
            self.bus[bus][:len(stereo) - first] += stereo[first:]

    def ms(self, beats, tail=0.0):
        return int(round((beats * self.beat + tail) * 1000))

    def human(self, spread=.006):
        return float(self.random.uniform(-spread, spread)) / self.beat

    # -- voices ----------------------------------------------------------------------------
    def voice(self, name, midi, beats, velocity, variant=0, glide_from=None):
        """A melodic voice by name; returns mono or stereo audio for `beats` of sound."""
        self.counter += 1
        return render_voice(name, int(midi), beats * self.beat, int(velocity), self.counter + variant if name == "clean_guitar" else variant,
                            glide_from, self.instruments)

    def strings(self, midi, beats, velocity=72):
        return render_voice("strings", int(midi), beats * self.beat, velocity, 0, None, self.instruments)

    def brass(self, midi, beat, beats, gain, velocity, variant, players=3):
        duration_ms = self.ms(beats, .05)
        low = midi < 62
        for wave, offset, pan in brass_section.section(midi, duration_ms, velocity, variant, low, players):
            self.place("brass", wave, beat + offset / self.beat, gain / players * 1.4, pan)
        self.place("brass", rompler.synth_brass(int(midi), duration_ms, int(velocity)), beat + .004, gain * .4, 0)

    # -- arrangement -----------------------------------------------------------------------
    def arrange(self):
        piece = self.piece
        bar_start = 0
        for section, bars in piece["form"]:
            self.markers.append((bar_start * self.beats, section.upper()))
            layers = piece["layers"].get(section, {})
            level = layers.get("level", .8)
            chords = piece["chords"].get(section) or piece["chords"]["a"]
            melody_name = piece.get("melody_map", {}).get(section)
            melody = self.melody_bars(melody_name, bars) if melody_name else None
            leads = [part for part in piece.get("lead", {}).get(section, "").split("+") if part]
            for local in range(bars):
                bar = bar_start + local
                beat0 = bar * self.beats
                tones = chord_tones(piece, chords[local % len(chords)])
                self.accompany(section, layers, level, bar, local, bars, beat0, tones)
                if melody is not None and local < len(melody):
                    self.play_melody(melody[local], leads, beat0, level, tones, layers.get("counter"))
            bar_start += bars

    def melody_bars(self, name, bars):
        melodies = self.piece["melody"]
        if name.endswith(":frag"):
            source = melodies[name[:-5]]
            fragment = source[:2]
            out = []
            for repeat in range((bars + 1) // 2):
                shift = (0, 2, -1, 1)[repeat % 4]
                out += [[(at, dur, degree + shift, vel * .85) for at, dur, degree, vel in bar] for bar in fragment]
            return out[:bars]
        source = melodies[name]
        return [source[i % len(source)] for i in range(bars)]

    def play_melody(self, bar_events, leads, beat0, level, tones, counter):
        piece = self.piece
        scale = piece["scale"]
        for at, duration, degree, velocity_scale in bar_events:
            midi = piece["tonic"] + degree_to_semitone(scale, degree)
            velocity = int(np.clip(60 + 60 * velocity_scale * level, 50, 124))
            primary_taken = False
            for index, part in enumerate(leads):
                name, _, shift = part.partition("@")
                shift = int(shift or 0)
                note = midi + shift
                beat = beat0 + at + (0 if index == 0 else .008 / self.beat) + self.human(.004)
                if name == "brass":
                    # Brass has its own bus; it never takes the lead bus's primary slot.
                    self.brass(note, beat, duration * .95, level * .9, min(120, velocity + 8), int(beat0 // self.beats))
                    continue
                gain = level * (1.0 if not primary_taken else .55) * (1.6 if name == "strings" else 1.0)
                primary_taken = True
                previous = self.last_note.get(name)
                glide = None
                if name in ("bansuri", "erhu", "daegeum") and previous and 0 <= beat0 + at - previous[1] <= .12 and 0 < abs(note - previous[0]) <= 7:
                    glide = previous[0]
                elif name in ("flute", "dizi", "fiddle", "oboe") and at == 0 and duration >= 1:
                    # Grace note from the scale step above on phrase downbeats.
                    grace = piece["tonic"] + degree_to_semitone(scale, degree + 1) + shift
                    self.place("lead", self.voice(name, grace, .12, velocity - 20), beat - .09 / self.beat, gain * .5, -.1 + index * .2)
                wave = self.voice(name, note, duration * .97, velocity, int(at * 4), glide)
                self.place("lead", wave, beat, gain, -.1 + index * .2)
                self.last_note[name] = (note, beat0 + at + duration)
            self.events.append((beat0 + at, duration, midi, velocity, 0))
            if counter and duration >= 1:
                # A quiet harmony voice on the nearest chord tone at least a third below the melody.
                candidates = [piece["tonic"] + t + o for t in tones for o in (-24, -12, 0, 12)]
                below = [c for c in candidates if midi - c >= 3]
                if below:
                    harmony = max(below)
                    self.place("counter", self.voice("strings", harmony, duration, velocity - 10), beat0 + at + .02, level * .9, .3)
                    self.events.append((beat0 + at, duration, harmony, velocity - 10, 11))

    def accompany(self, section, layers, level, bar, local, bars, beat0, tones):
        piece = self.piece
        tonic = piece["tonic"]
        beats = self.beats
        inst = piece.get("instruments", {})
        root = tonic + tones[0]
        last_bar = local == bars - 1

        if layers.get("pad"):
            pad_root = tonic - 12
            for i, tone in enumerate(tones + [tones[0] + 12]):
                midi = pad_root + tone
                self.place("pad", self.strings(midi, beats * 1.02, 72), beat0 + i * .02 / self.beat, level * .9, -.5 + i * .33)
                self.events.append((beat0, beats, midi, 70, 3))
        if layers.get("choir"):
            choir_root = tonic - (24 if inst.get("choir") == "low" else 12)
            for i, tone in enumerate(tones):
                midi = choir_root + tone
                self.place("choir", rompler.choir(midi, self.ms(beats, .4)), beat0 + .03 / self.beat + i * .015, level, -.6 + i * .4)
                self.events.append((beat0, beats, midi, 64, 4))
        if layers.get("drone") and local % 4 == 0:
            kind = inst.get("drone", "sub")
            span = min(4, bars - local) * beats
            midi = tonic - 24 + tones[0] % 12 if kind != "tanpura" else tonic - 24
            self.place("drone", mv.drone(midi, self.ms(span, .5), kind), beat0, level, 0)
            if kind == "tanpura":
                self.place("drone", mv.drone(midi + 7, self.ms(span, .5), kind), beat0 + .3, level * .6, .2)
        if layers.get("wind") and local % 4 == 0:
            span = min(4, bars - local) * beats
            self.place("wind", mv.wind(self.ms(span, .8), bar), beat0, level, 0)

        pluck_name = inst.get("pluck")
        pattern = layers.get("pluck")
        if pattern and pluck_name:
            self.pluck(pattern, pluck_name, tones, beat0, level, local)

        keys = layers.get("keys")
        keys_name = inst.get("keys", "piano")
        if keys:
            voice = "piano" if keys_name == "piano" else "ep"
            chord = [tonic - 12 + t for t in tones] + [tonic + tones[0]]
            if keys == "sustain":
                for i, midi in enumerate(chord):
                    self.place("keys", self.voice(voice, midi, beats * .95, 72), beat0 + i * .018 / self.beat, level * .9, -.3 + i * .2)
                    self.events.append((beat0, beats, midi, 72, 5))
            else:
                hits = (0, 2) if beats == 4 else (0,) if beats == 3 else (0, 3)
                for at in hits:
                    for i, midi in enumerate(chord):
                        self.place("keys", self.voice(voice, midi, (beats / len(hits)) * .9, 80 - i * 3), beat0 + at + i * .012 / self.beat, level * .9, -.3 + i * .2)
                        self.events.append((beat0 + at, beats / len(hits), midi, 78, 5))

        bass = layers.get("bass")
        if bass:
            self.bass_line(bass, inst.get("bass", "sampled"), tones, beat0, level)

        drums = layers.get("drums")
        if drums:
            self.kit(drums, beat0, level, local, last_bar, section)
        perc = layers.get("perc")
        if perc:
            self.percussion(perc, beat0, level, local, last_bar)

        brass = layers.get("brass")
        if brass == "chords":
            for i, tone in enumerate(tones):
                midi = tonic - 12 + tone
                self.brass(midi, beat0 + .05 + i * .01, beats * .92, level * .8, 96, bar, players=2)
                self.events.append((beat0, beats, midi, 96, 6))
        elif brass == "hits":
            for at in (0, 2.5):
                for i, tone in enumerate(tones[:3]):
                    self.brass(tonic - 12 + tone + 12, beat0 + at, .35, level * .9, 112, bar, players=2)
                self.events.append((beat0 + at, .35, root, 112, 6))

        guitar = layers.get("guitar")
        if guitar in ("power", "power8"):
            # Double-tracked power chords: halves of the bar, or driving eighths with a palm-muted feel.
            if guitar == "power":
                hits = [(0, beats / 2), (beats / 2, beats / 2)] if beats != 3 else [(0, 3)]
            else:
                hits = [(i * .5, .5) for i in range(beats * 2)] if beats != 6 else [(i, 1) for i in range(6)]
            root_midi = tonic - 24 + tones[0]
            while root_midi < 40:
                root_midi += 12
            for at, length in hits:
                chug = length < 1
                for interval, weight in ((0, 1.0), (7, .8), (12, .5)):
                    midi = root_midi + interval
                    for pan, rr in ((-.8, 0), (.8, 3)):
                        seconds = length * self.beat + (.1 if chug else .04)
                        wave = self.instruments.pitched("Guitar", midi, 104 if at == 0 else 96, self.counter + rr + bar + int(at * 2), seconds, False, False)
                        if chug:
                            n = round((length * self.beat + .03) * SR)
                            t = np.arange(n) / SR
                            wave = (wave[:n] * np.clip((length * self.beat + .03 - t) / .03, 0, 1) * np.exp(-t * 3)).astype(np.float32)
                        self.place("guitar", wave, beat0 + at + self.human(.004), level * weight, pan)
                self.events.append((beat0 + at, length, root_midi, 100, 7))
            self.counter += 1
        elif guitar == "clean_strum":
            # Clean chord strums on the main beats, through the clean channel.
            strum_at = (0, 2) if beats == 4 else (0,) if beats == 3 else (0, 3)
            for at in strum_at:
                for i, tone in enumerate(tones):
                    midi = tonic - 12 + tone
                    wave = self.instruments.pitched("Guitar", midi, 88, self.counter + i + bar, (beats / len(strum_at)) * self.beat * .9, False, False)
                    self.place("clean", wave, beat0 + at + (i * .014 + self.human(.003) * self.beat) / self.beat, level * .9, -.4 + i * .3)
                self.events.append((beat0 + at, beats / len(strum_at), tonic - 12 + tones[0], 88, 7))
            self.counter += 1

        if layers.get("bells") and local % 4 == 0:
            name = inst.get("bells", "crystal")
            for step, tone in enumerate(tones + [tones[0] + 12]):
                midi = tonic + 12 + tone
                if name == "bell_low":
                    wave = render_perc("bell_low", step, 90)
                elif name == "glass":
                    wave = mv.glass(midi, self.ms(1.5))
                else:
                    wave = render_voice("crystal", midi, 1.5, 90, step)
                self.place("bells", wave, beat0 + step * .5, level * (.35 if name == "bell_low" else .6), -.4 + step * .27)
                self.events.append((beat0 + step * .5, .5, midi, 70, 8))

        if layers.get("fx") and last_bar:
            self.place("fx", rompler.riser(self.ms(beats), bar), beat0, level * .7, 0)

    def pluck(self, pattern, name, tones, beat0, level, local):
        beats = self.beats
        tonic = self.piece["tonic"]
        pool = [tonic + t for t in tones] + [tonic + 12 + t for t in tones]
        if pattern == "up":
            steps = [0, 1, 2, 3, 2, 1, 0, 1] if beats == 4 else [0, 1, 2, 3, 4, 5]
            step = beats / len(steps)
        elif pattern == "updown":
            steps = [0, 1, 2, 3, 4, 3, 2, 1] if beats == 4 else [0, 2, 1, 3, 2, 4]
            step = beats / len(steps)
        elif pattern == "waltz":
            steps = [0, 2, 4, 1, 3, 5]
            step = beats / len(steps)
        elif pattern == "raga":
            steps = [0, 1, 2, 1, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1, 0]
            step = beats / len(steps)
        elif pattern == "oud":
            steps = [0, None, 1, 0, None, 2, 1, 0]
            step = beats / len(steps)
        elif pattern == "gliss":
            # A quick zither sweep every bar, rising on even bars and falling on odd bars.
            steps = list(range(6)) if local % 2 == 0 else list(range(5, -1, -1))
            step = .5 / 6
        else:  # sparse
            steps = [0, None, None, 2, None, None, 1, None] if beats == 4 else [0, None, None, 2, None, None]
            step = beats / len(steps)
        bus = "clean" if name == "clean_guitar" else "pluck"
        for i, index in enumerate(steps):
            if index is None:
                continue
            midi = pool[index % len(pool)]
            velocity = 96 if i == 0 else 78 + (i % 3) * 6
            self.place(bus, self.voice(name, midi, step * 1.6, velocity, i), beat0 + i * step + self.human(.005), level * (1.0 if i == 0 else .8), -.35 + (i % 4) * .23)
            self.events.append((beat0 + i * step, step, midi, velocity, 2))

    def bass_line(self, pattern, kind, tones, beat0, level):
        beats = self.beats
        root = self.piece["tonic"] - 24 + tones[0]
        while root < 36:
            root += 12
        while root > 55:
            root -= 12
        fifth = root + 7 if root + 7 <= 60 else root - 5
        octave = root + 12 if root + 12 <= 60 else root
        if pattern == "long":
            hits = [(0, beats, root)]
        elif beats == 6:
            hits = {"roots": [(0, 3, root), (3, 3, root)], "pulse8": [(i, 1, root if i % 3 != 2 else fifth) for i in range(6)],
                    "walk": [(0, 2, root), (2, 1, fifth), (3, 2, root), (5, 1, octave)]}.get(pattern, [(0, 3, root), (3, 3, root)])
        elif beats == 3:
            hits = {"roots": [(0, 1.5, root), (1.5, 1.5, root)], "pulse8": [(i * .5, .5, root if i % 3 != 2 else fifth) for i in range(6)],
                    "walk": [(0, 1, root), (1, 1, fifth), (2, 1, octave)], "waltz": [(0, 1.5, root), (1.5, 1.5, fifth)]}.get(pattern, [(0, 3, root)])
        elif pattern == "roots":
            hits = [(0, beats / 2, root), (beats / 2, beats / 2, root)]
        elif pattern == "pulse8":
            hits = [(i * .5, .5, root if i % 4 != 3 else fifth) for i in range(beats * 2)]
        elif pattern == "walk":
            hits = [(0, 1, root), (1, 1, fifth), (2, 1, octave), (3, 1, fifth)][:beats]
        else:
            hits = [(0, beats, root)]
        for at, duration, midi in hits:
            if kind == "lowstrings":
                self.place("bass", render_voice("cello", midi, duration * self.beat + .2, 80, 0, None, self.instruments), beat0 + at, level * .9, 0)
            else:
                wave = self.instruments.pitched("Bass", midi, 96, self.counter + int(at * 2), duration * self.beat + .04, False, False)
                self.place("bass", wave, beat0 + at + self.human(.003), level, 0)
            self.events.append((beat0 + at, duration, midi, 90, 1))
        self.counter += 1

    def hit(self, kind, beat, volume, pan=0.0, variant=0):
        mapping = {"open": "open_hat", "tom": ("tom_hi", "tom_mid", "tom_low", "tom_low")[variant % 4], "clave": "snare_rimshot"}
        recorded = mapping.get(kind, kind)
        duration = {"kick": 1.3, "snare": 1.3, "hat": .17, "open": self.beat * .49, "ride": 3.5, "crash": 6, "tom": 1.8, "clave": .25}[kind]
        velocity = int(np.clip(60 + 60 * volume + self.random.normal(0, 4), 48, 127))
        gain = {"kick": 1.35, "snare": 1.40, "hat": .70, "open": .72, "ride": .58, "crash": .85, "tom": 1.05, "clave": .38}[kind]
        audio = self.instruments.percussion(recorded, velocity, self.counter + variant, duration)
        human = 0 if kind == "kick" else float(self.random.uniform(-.003, .004)) / self.beat
        self.place("drums", audio, beat + human, volume * gain, pan * .45)
        if kind == "kick":
            self.place("drums", synth_drum("kick", variant), beat, volume * .3, 0)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51, "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 37}
        self.events.append((beat, .1, gm[kind], velocity, 9))
        self.counter += 1

    def kit(self, pattern, beat0, level, local, last_bar, section):
        beats = self.beats
        if beats == 3:
            pattern = {"light": "brush_waltz", "half": "brush_waltz", "full": "waltz_full", "heavy": "waltz_full"}.get(pattern, pattern)
        elif beats == 6:
            pattern = {"light": "six_light", "half": "six_light", "brush": "six_light", "full": "six_full", "heavy": "six_full"}.get(pattern, pattern)
        if pattern == "waltz_full":
            self.hit("kick", beat0, .75 * level)
            self.hit("snare", beat0 + 2, .6 * level, .05, local)
            for i in range(6):
                self.hit("hat", beat0 + i * .5, (.3 if i % 2 == 0 else .2) * level, .3, i)
        elif pattern == "six_light":
            self.hit("kick", beat0, .7 * level)
            self.hit("snare", beat0 + 3, .5 * level, .05, local)
            for i in range(6):
                self.hit("hat", beat0 + i, (.3 if i % 3 == 0 else .2) * level, .3, i)
        elif pattern == "six_full":
            for at in (0, 2.5):
                self.hit("kick", beat0 + at, .8 * level)
            self.hit("snare", beat0 + 3, .7 * level, .05, local)
            for i in range(6):
                self.hit("ride" if local % 2 else "hat", beat0 + i, (.34 if i % 3 == 0 else .22) * level, .3, i)
            if local % 4 == 3:
                for i, at in enumerate((4.5, 5, 5.5)):
                    self.hit("tom", beat0 + at, .5 * level, -.3 + i * .3, i)
        elif pattern == "light":
            for at in (0, 2.5):
                self.hit("kick", beat0 + at, .7 * level)
            self.hit("snare", beat0 + 2, .55 * level, .05)
            for i in range(beats * 2):
                self.hit("hat", beat0 + i * .5, (.32 if i % 2 == 0 else .22) * level, .3, i)
        elif pattern == "full":
            for at in (0, 1.5, 2.5):
                self.hit("kick", beat0 + at, .8 * level)
            for at in (1, 3):
                self.hit("snare", beat0 + at, .7 * level, .05, local)
            for i in range(beats * 2):
                self.hit("ride" if local % 2 else "hat", beat0 + i * .5, (.36 if i % 2 == 0 else .24) * level, .3, i)
        elif pattern == "half":
            self.hit("kick", beat0, .8 * level)
            self.hit("kick", beat0 + 2.5, .6 * level)
            self.hit("snare", beat0 + 2, .7 * level, .05, local)
            for i in range(beats):
                self.hit("hat", beat0 + i, .28 * level, .3, i)
        elif pattern == "heavy":
            for at in (0, .75, 2.5):
                self.hit("kick", beat0 + at, .85 * level)
            self.hit("snare", beat0 + 2, .8 * level, .05, local)
            for i in range(beats):
                self.hit("ride", beat0 + i, .3 * level, .3, i)
            if local % 4 == 3:
                for i, at in enumerate((3, 3.25, 3.5, 3.75)):
                    self.hit("tom", beat0 + at, .5 * level, -.3 + i * .2, i)
        elif pattern == "brush":
            for at in (0, 2.5):
                self.hit("kick", beat0 + at, .45 * level)
            for at in (1, 3):
                self.hit("clave", beat0 + at, .5 * level, .1)
            for i in range(beats * 2):
                self.hit("hat", beat0 + i * .5, (.22 if i % 2 == 0 else .15) * level, .3, i)
        elif pattern == "brush_waltz":
            self.hit("kick", beat0, .45 * level)
            for at in (1, 2):
                self.hit("clave", beat0 + at, .4 * level, .1)
            for i in range(beats * 2):
                self.hit("hat", beat0 + i * .5, (.2 if i % 2 == 0 else .14) * level, .3, i)
        if local == 0 and section not in ("intro",):
            self.hit("crash", beat0, .5 * level, -.2)

    def percussion(self, pattern, beat0, level, local, last_bar):
        beats = self.beats
        P = lambda kind, at, vol, pan=0.0, variant=0: self.place("perc", render_perc(kind, variant, int(np.clip(60 + 60 * vol, 40, 127))),
                                                              beat0 + at + self.human(.004), vol * level, pan)
        if pattern.startswith("taiko"):
            slow = pattern == "taiko_slow"
            P("taiko", 0, .9)
            if not slow:
                P("taiko", 2.5, .7)
                P("frame_high", 1, .4, .3)
                P("frame_high", 3, .45, .3)
            if last_bar or (local % 4 == 3 and not slow):
                for i in range(4):
                    P("taiko", 3 + i * .25, .5 + i * .12, 0, i)
        elif pattern == "heartbeat":
            if local % 2 == 0:
                P("boom", 0, .8)
                P("boom", .6, .55)
        elif pattern.startswith("timpani"):
            if pattern == "timpani_roll" and last_bar:
                for i in range(8):
                    P("timpani", 2 + i * .25, .35 + i * .08, 0, i)
            elif pattern == "timpani":
                P("timpani", 0, .8)
                P("timpani", 2.5, .6)
        elif pattern.startswith("frame"):
            soft = pattern == "frame_soft"
            full = pattern == "frame_full"
            P("frame_low", 0, .9)
            P("frame_high", 1, .5, .3)
            P("frame_low", 1.5, .7)
            P("frame_high", 2, .5, -.3)
            if not soft:
                P("frame_high", 2.5, .4, .3)
                P("frame_low", 3, .7)
                P("frame_high", 3.5, .5, -.3)
            if full:
                for at in (.75, 2.75):
                    P("frame_high", at, .35, .2, 1)
                P("boom", 0, .5)
        elif pattern.startswith("tabla"):
            soft = pattern == "tabla_soft"
            full = pattern == "tabla_full"
            cycle = [("tabla_dha", 0), ("tabla_tin", .5), ("tabla_tin", 1), ("tabla_dha", 1.5), ("tabla_dha", 2), ("tabla_tin", 2.5), ("tabla_na", 3), ("tabla_tin", 3.5)]
            for kind, at in cycle:
                if soft and at % 1:
                    continue
                P(kind, at, .8 if kind == "tabla_dha" else .55, .2 if kind != "tabla_dha" else 0)
            if full:
                for at in (.25, 1.25, 2.25, 3.25):
                    P("tabla_na", at, .3, -.2)
            if local % 4 == 3 and not soft:
                for i in range(4):
                    P("tabla_dha" if i % 2 == 0 else "tabla_tin", 3 + i * .25, .6, 0, i)
        elif pattern.startswith("janggu"):
            soft = pattern == "janggu_soft"
            full = pattern == "janggu_full"
            for at in (0, 3):
                P("frame_low", at, .85 if at == 0 else .6)
            for at in (1, 2, 4, 5):
                P("frame_high", at, .4 if at % 3 == 1 else .3, .3 if at < 3 else -.3)
            if not soft:
                P("taiko", 0, .5)
                P("frame_high", 2.5, .35, .2)
            if full:
                P("taiko", 3, .45)
                P("frame_high", 4.5, .35, -.2)
            if (local % 4 == 3 and not soft) or last_bar:
                for i in range(3):
                    P("frame_high", 5 + i / 3, .4 + i * .1, 0, i)
        elif pattern.startswith("wood") or pattern == "gong_wood":
            full = pattern == "wood_drum_full"
            if pattern == "gong_wood":
                if local % 2 == 0:
                    P("gong", 0, .8)
                for at in (1.5, 2.5, 3.5):
                    P("wood", at, .4, .3)
            else:
                P("taiko", 0, .7)
                P("taiko", 2.5, .5)
                for at in (1, 1.75, 3, 3.5):
                    P("wood", at, .45, .3 if at < 2 else -.3)
                if full:
                    P("frame_high", 2, .4, .2)
                    P("frame_high", 3.75, .35, -.2)
                if local % 8 == 0:
                    P("gong", 0, .7)
        if last_bar and pattern not in ("heartbeat",):
            P("gong" if pattern.startswith(("wood", "gong")) else "boom", beats - .02, .5)

    # -- mix -------------------------------------------------------------------------------
    def finish(self):
        piece = self.piece
        beat = self.beat
        send, hall_wet = SPACES[piece.get("space", "hall")]
        targets = dict(TARGETS)
        targets.update(piece.get("mix", {}))
        out = {}
        for name in BUSES:
            bus = self.bus.pop(name)
            if not np.any(bus):
                out[name] = bus
                continue
            if name == "guitar":
                bus = filter_stereo(rhythm_amp(bus, True), 6000)
            elif name == "clean":
                bus = chorus(clean_amp(bus), .9, 2.0, 10, .35)
            elif name == "bass":
                bus = bass_amp(bus)
            elif name == "lead":
                bus = chorus(bus, .6, 1.2, 8, .15)
            elif name in ("pad", "choir", "counter"):
                bus = chorus(bus, .4, 3.0, 16, .5)
            elif name == "drone":
                bus = filter_stereo(bus, 1200)
            elif name == "brass":
                bus = eq(np.tanh(bus * 1.3) / 1.3, ("peaking", 300, 1.0, -2), ("peaking", 2500, 1.2, 2))
            elif name == "perc":
                bus = np.tanh(bus * 1.2) / 1.2
            elif name == "drums":
                bus = bus + .3 * np.tanh(bus * 4) / 4
            out[name] = normalize(bus, targets[name])
        out["lead"] += filter_stereo(ping_pong(out["lead"], beat, ((.75, .18), (1.5, .08))), 4000)
        out["pluck"] += filter_stereo(ping_pong(out["pluck"], beat, ((.375 if self.beats != 6 else 1.0, .16),)), 5000)
        out["brass"] += filter_stereo(ping_pong(out["brass"], beat, ((1.0, .16),)), 3500)
        levels = {name: round(active_level(bus), 1) for name, bus in out.items() if np.any(bus)}
        print("bus dBFS (active)", levels, flush=True)
        glue = convolve(out["guitar"] * .7 + out["clean"] * .5 + out["drums"] * .8 + out["perc"] * .6 + out["brass"] + out["bass"] * .3 + out["keys"] * .5, room_ir()) * .16
        hall = convolve(out["lead"] * .6 + out["pad"] + out["choir"] + out["counter"] + out["bells"] + out["pluck"] * .5 + out["fx"], hall_ir()) * hall_wet
        wet = reverb(out["lead"] * .3 + out["pluck"] * .35 + out["clean"] * .3 + out["keys"] * .3 + out["perc"] * .15 + out["brass"] * .3 + out["drums"] * .08) * (send / .22)
        dry = sum(out.values()) + glue + hall + wet
        del out, glue, hall, wet
        extended = np.concatenate((dry[-SR:], dry), axis=0)
        dry = eq(filter_stereo(extended, 31, "highpass"), ("highshelf", 7000, .7, 2.0))[SR:]
        del extended
        dry = (1.7 * np.tanh(dry / 1.7)).astype(np.float32)
        rms = float(np.sqrt(np.mean(dry.astype(np.float64) ** 2)))
        dry *= min(3.0, .178 / max(rms, 1e-9))
        return limit(dry)


# -- output --------------------------------------------------------------------------------
def vlq(value):
    data = [value & 0x7f]
    while value >> 7:
        value >>= 7
        data.insert(0, (value & 0x7f) | 0x80)
    return bytes(data)


def write_midi(path, piece, mix):
    resolution = 480
    total_beats = mix.total_bars * mix.beats
    tempo = round(60000000 / piece["bpm"])
    title = piece["title"].encode("ascii", "replace")
    meta = b"\0\xff\x03" + vlq(len(title)) + title + b"\0\xff\x51\x03" + tempo.to_bytes(3, "big")
    meta += b"\0\xff\x58\x04" + bytes((mix.beats, 2 if mix.beats != 6 else 3, 24, 8))
    previous = 0
    for beat, name in mix.markers:
        encoded = name.encode("ascii")
        tick = round(beat * resolution)
        meta += vlq(tick - previous) + b"\xff\x06" + vlq(len(encoded)) + encoded
        previous = tick
    meta += vlq(total_beats * resolution - previous) + b"\xff\x2f\0"
    tracks = [meta]
    for channel, program in mix.programs.items():
        sequence = []
        for beat, duration, note, velocity, ch in mix.events:
            if ch != channel:
                continue
            start = round(beat * resolution)
            stop = min(total_beats * resolution, round((beat + duration) * resolution))
            if stop <= start:
                continue
            note = int(np.clip(note, 0, 127))
            sequence.extend([(start, 1, bytes((0x90 + ch, note, int(np.clip(velocity, 1, 127))))), (stop, 0, bytes((0x80 + ch, note, 0)))])
        if not sequence:
            continue
        sequence.sort(key=lambda event: (event[0], event[1]))
        data = bytes((0, 0xc0 + channel, program))
        previous = 0
        for tick, _, payload in sequence:
            data += vlq(tick - previous) + payload
            previous = tick
        data += vlq(total_beats * resolution - previous) + b"\xff\x2f\0"
        tracks.append(data)
    content = b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), resolution)
    for track in tracks:
        content += b"MTrk" + struct.pack(">I", len(track)) + track
    path.write_bytes(content)


def render(piece, instruments):
    print(f"Rendering {piece['id']} / {piece['bpm']} BPM x {piece['beats']} beats...", flush=True)
    mix = MythMix(piece, instruments)
    mix.arrange()
    audio = mix.finish()
    asset_dir = ROOT / "Assets/Demos/Roguelike/Resources/Tower/Music"
    source_dir = ROOT / "ArtSources/Music"
    masters, midi_dir = source_dir / "Masters", source_dir / "Midi"
    for directory in (asset_dir, masters, midi_dir):
        directory.mkdir(parents=True, exist_ok=True)
    base.unity_meta(asset_dir, True)
    wav, ogg = masters / (piece["id"] + ".wav"), asset_dir / (piece["id"] + ".ogg")
    for path, options in ((wav, {"subtype": "PCM_16"}), (ogg, {"format": "OGG", "subtype": "VORBIS", "compression_level": .35})):
        with sf.SoundFile(str(path), "w", samplerate=SR, channels=2, **options) as output:
            for offset in range(0, len(audio), 16384):
                output.write(audio[offset:offset + 16384])
    base.unity_meta(ogg)
    write_midi(midi_dir / (piece["id"] + ".mid"), piece, mix)
    decoded, rate = sf.read(str(ogg), dtype="float32", always_2d=True)
    report = {"id": piece["id"], "title": piece["title"], "title_ko": piece["title_ko"], "bpm": piece["bpm"], "beats": piece["beats"],
              "bars": mix.total_bars, "note_events": len(mix.events), "production": MythMix.production,
              "master": base.measure(audio, SR), "ogg": base.measure(decoded, rate),
              "ogg_bytes": ogg.stat().st_size, "sha256": hashlib.sha256(ogg.read_bytes()).hexdigest()}
    assert 80 <= report["ogg"]["seconds"] <= 120, report["ogg"]["seconds"]
    assert report["ogg"]["finite"] and report["ogg"]["clipped_samples"] == 0
    assert report["ogg"]["minimum_half_second_rms"] > .003
    (source_dir / (piece["id"] + ".analysis.json")).write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "title_ko"}, ensure_ascii=False), flush=True)
    for module in (mv, rompler):
        module.clear_caches()
    strings_pad.cache_clear()
    synth_lead.cache_clear()
    brass_section.clear_cache()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--piece", default="all")
    args = parser.parse_args()
    selected = [p for p in PIECES if args.piece == "all" or p["id"] == args.piece]
    if not selected:
        parser.error("Unknown piece " + args.piece)
    instruments = GuitarInstruments()
    for piece in selected:
        render(piece, instruments)
