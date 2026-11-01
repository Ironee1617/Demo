"""Render the authored Tower score with the instrument palette of the referenced
Mega Man X4/X6 stage themes (Air Force, Bio Laboratory, Laser Institute, Weapon
Center), as documented by their fan MIDI transcriptions:

  polysynth melody, sawtooth sequence, distorted/overdriven and muted guitars,
  picked electric bass, power/jazz drum kit, electric piano, low/high strings,
  choir pad, crystal FX, brass section with echo, sci-fi riser.

Guitars, bass, drums and brass are recorded CC0 samples (ArtSources/Music/
Samples); the synth patches are rompler-style voices from rompler.py. The
authored melodies and harmony are unchanged; only the instrumentation and the
accompaniment roles follow the reference palette. No soundtrack audio, samples
or melodies from any reference are used.
Usage: python Tools/TowerMusic/render_rompler.py [--theme lava]
"""
from __future__ import annotations

import argparse
from functools import lru_cache
import json
import math
from pathlib import Path

import render as base
from render import np, SR, filt
from render_band import filter_stereo
from render_synth import chorus, ping_pong, reverb, limit, drum as synth_drum, pad as strings
from render_guitar import (GuitarInstruments, rhythm_amp, bass_amp, eq, room_ir, hall_ir, convolve,
                           normalize, active_level, HEAVY)
import brass as brass_section
import rompler

BELL_THEMES = ("frozen", "water", "rock")


@lru_cache(maxsize=1)
def plate_ir():
    """A short bright plate (0.5 s) for the power-kit snare and toms."""
    n = round(.5 * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(909)
    ir = np.zeros((n, 2))
    for channel in range(2):
        tail = rng.normal(0, 1, n) * np.exp(-t * 9)
        ir[:, channel] = filt(filt(tail, 7000), 400, "highpass") * np.minimum(t / .003, 1)
    return ir / math.sqrt(float((ir ** 2).sum(axis=0).max()))


class RomplerMix:
    production = "x-palette-v7"
    write_midi = True
    programs = {0: 90, 1: 33, 2: 30, 3: 48, 4: 98, 5: 90, 6: 30, 7: 61, 8: 4, 9: 0, 10: 52, 11: 28}

    def __init__(self, score, instruments):
        self.score = score
        self.beat = 60 / score["bpm"]
        self.frames = round(256 * self.beat * SR)
        self.theme = score["id"]
        self.tonic = score["tonic_midi"]
        self.instruments = instruments
        self.bus = {name: np.zeros((self.frames, 2), np.float32) for name in
                    ("drums", "bass", "guitar", "mute", "lead", "brass", "ep", "strings", "choir", "seq", "fx")}
        self.events = []
        self.counter = 0
        self.stabbed = set()
        self.random = np.random.default_rng(4101 + sum(map(ord, self.theme)))

    def chord_at(self, bar):
        section, local = base.sections(bar)
        if section == "turn":
            return self.score["chords_a"][(local + 4) % 8]
        chords = self.score["chords_b"] if section in ("b", "solo") else self.score["chords_a"]
        return chords[local % len(chords)]

    def place(self, bus, audio, beat, volume, pan=0):
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

    def brass(self, midi, beat, duration, gain, velocity, bar, players=3, low=None):
        """Recorded section plus a rompler brass layer, like a sampled 'Brass Section' patch."""
        if low is None:
            low = midi < 62
        duration_ms = self.ms(duration, .05)
        for wave, offset, pan in brass_section.section(midi, duration_ms, velocity, bar, low, players):
            self.place("brass", wave, beat + offset / self.beat, gain / players * 1.4, pan)
        self.place("brass", rompler.synth_brass(int(midi), duration_ms, int(velocity)), beat + .004, gain * .55, 0)
        self.events.append((beat, duration, int(midi), int(velocity), 7))

    def note(self, voice, midi, beat, duration, volume, pan=0, wet=.1, variant=0, channel=0):
        self.counter += 1
        midi = int(midi)
        bar = int(beat // 4) % 64
        section, local = base.sections(bar)
        big = section in ("b", "reprise", "solo")
        velocity = int(np.clip(round(96 + (volume / .46 - .8) * 17), 60, 124))
        human = 0 if beat == 0 else float(self.random.uniform(-.004, .004))

        if voice == "pad":
            # One call per chord tone; the first tone (pan -.45) also schedules the chord-based parts.
            first = abs(pan + .45) < 1e-6
            if first:
                self.chord_parts(bar, section, local, beat)
            if section in ("a", "intro"):
                if first:
                    self.place("strings", strings(self.theme, midi - 12, self.ms(duration, .25)), beat, volume * 1.2, -.3)
                    self.place("strings", strings(self.theme, midi - 12, self.ms(duration, .25) + 5), beat + .02, volume * 1.2, .3)
                return
            for side, offset in ((-.6, 0.0), (.6, .018)):
                self.place("strings", strings(self.theme, midi, self.ms(duration, .25) + (0 if side < 0 else 7)), beat + offset, volume * .9, side)
            if section in ("b", "reprise"):
                self.place("choir", rompler.choir(midi, self.ms(duration, .3)), beat + .03, volume * 1.6, -.5 + (midi % 5) * .25)
                self.brass(midi, beat + .02, duration * .96, .22 * volume / .11, 92 + local % 2 * 6, bar)
            elif section == "solo" and midi < 64:
                self.brass(midi, beat + .02, duration * .96, .14, 84, bar, low=True)
            self.events.append((beat, duration, midi, 72, 3))
            return

        if voice in ("bell", "pluck"):
            # Crystal FX arpeggios on the glassy themes, a sawtooth sequence elsewhere.
            if self.theme in BELL_THEMES:
                wave = rompler.crystal(midi, self.ms(duration, .25))
            else:
                wave = rompler.sequence(midi, self.ms(duration, .05), velocity)
            self.place("seq", wave, beat + human / self.beat, volume * 1.1, pan)
            self.events.append((beat, duration, midi, velocity, 4))
            return

        if voice.startswith("lead"):
            self.place("lead", rompler.polysynth(midi, self.ms(duration, .07), velocity, variant % 7), beat + human / self.beat, volume, pan * .5)
            self.events.append((beat, duration, midi, velocity, channel))
            if channel == 0 and section in ("b", "reprise") and duration >= .4:
                # The brass section rides the hook melody, X6-style.
                self.brass(midi - 12 if midi > 79 else midi, beat + .015, duration * .95, .42 * volume / .46, 104, bar)
            return

        if voice == "bass":
            seconds = duration * self.beat + .035
            wave = self.instruments.pitched("Bass", midi, velocity, self.counter + variant, seconds, False, False)
            self.place("bass", wave, beat + human / self.beat, volume, 0)
            self.events.append((beat, duration, midi, velocity, 1))
            return

        # Guitars: distorted power chords, plus a muted doubling under the chugs and horn stabs on accents.
        while midi > 88:
            midi -= 12
        pan = -.8 if pan < 0 else .8
        at = round((beat % 4) * 4) / 4
        downbeat = bar in (4, 20, 28, 44, 52, 60) and at == 0
        accent = downbeat or (section == "a" and local >= 8 and bar % 2 == 1 and at in (0, 1.5, 2.5))
        if accent and (bar, at) not in self.stabbed:
            self.stabbed.add((bar, at))
            for interval, weight in ((12, 1.0), (19, .8), (24, .6)):
                self.brass(midi + interval, beat, 1.0 if downbeat else .3, .5 * weight, 112, bar, players=2)
        seconds = duration * self.beat + .035
        chug = duration < .5
        wave = self.instruments.pitched("Guitar", midi, velocity, self.counter + variant, seconds + (.1 if chug else 0), False, False)
        if chug:
            n = round(seconds * SR)
            t = np.arange(n) / SR
            wave = (wave[:n] * np.clip((seconds - t) / .03, 0, 1) * np.exp(-t * 3)).astype(np.float32)
            if pan < 0:
                muted = self.instruments.pitched("Guitar", midi, velocity, self.counter + 9, seconds, False, True)
                self.place("mute", muted, beat + (human + .003) / self.beat, volume, 0)
        self.place("guitar", wave, beat + human / self.beat, volume, pan)
        self.events.append((beat, duration, midi, velocity, channel))
        if big and not chug and pan < 0 and volume > .1:
            third = self.instruments.pitched("Guitar", min(midi + 12, 88), max(67, velocity - 8), self.counter + 5, seconds, False, False)
            self.place("guitar", third, beat + (human + .009) / self.beat, volume * .4, 0)

    def chord_parts(self, bar, section, local, beat):
        """Electric-piano comping, the pre-section riser and a crystal flourish, driven by the bar's chord."""
        chord = self.chord_at(bar)
        tones = [self.tonic + interval for interval in chord[:4]]
        if section == "a":
            for at in (1.5, 3.5):
                for i, midi in enumerate(tones):
                    self.place("ep", rompler.electric_piano(midi, self.ms(.45, .1), 78 + i * 3), beat + at + i * .004, .5, -.3 + i * .2)
                    self.events.append((beat + at, .45, midi, 80, 8))
        elif section in ("break", "intro"):
            for i, midi in enumerate(tones):
                self.place("ep", rompler.electric_piano(midi, self.ms(3.9, .2), 66), beat + i * .012, .55, -.3 + i * .2)
                self.events.append((beat, 3.9, midi, 66, 8))
        if bar in (19, 51):
            self.place("fx", rompler.riser(self.ms(4.0), bar), beat, .6, 0)
        if bar in (4, 20, 28, 52) and self.theme in BELL_THEMES + ("cloud",):
            for step, interval in enumerate(chord[:3] + [chord[0] + 12]):
                self.place("seq", rompler.crystal(self.tonic + 12 + interval, self.ms(.5)), beat + step * .25, .35, -.4 + step * .27)

    def hit(self, kind, beat, volume, pan=0, variant=0):
        self.counter += 1
        mapping = {"open": "open_hat", "tom": ("tom_hi", "tom_mid", "tom_low", "tom_low")[variant % 4],
                   "clave": "snare_rimshot", "shaker": "hat"}
        recorded = mapping.get(kind, kind)
        duration = {"kick": 1.3, "snare": 1.3, "hat": .17, "open": self.beat * .49,
                    "ride": 3.5, "crash": 6, "tom": 1.8, "clave": .25, "shaker": .10}[kind]
        reference = {"kick": .76, "snare": .65, "hat": .34, "open": .34, "ride": .34,
                     "crash": .45, "tom": .44, "clave": .19, "shaker": .12}[kind]
        velocity = int(np.clip(65 + 52 * min(1.1, volume / reference) + self.random.normal(0, 4), 48, 127))
        if kind in ("clave", "shaker"):
            velocity = min(70, velocity)
        gain = {"kick": 1.35, "snare": 1.40, "hat": .70, "open": .72, "ride": .58,
                "crash": .85, "tom": 1.05, "clave": .38, "shaker": .35}[kind]
        audio = self.instruments.percussion(recorded, velocity, self.counter + variant, duration)
        human = 0 if kind == "kick" or beat == 0 else float(self.random.uniform(-.003, .004))
        self.place("drums", audio, beat + human / self.beat, volume * gain, pan * .45)
        if kind == "kick":
            self.place("drums", synth_drum("kick", variant), beat, volume * .3, 0)
        gm = {"kick": 36, "snare": 38, "hat": 42, "open": 46, "ride": 51,
              "crash": 49, "tom": (48, 45, 43, 41)[variant % 4], "clave": 37, "shaker": 42}
        self.events.append((beat, .1, gm[kind], velocity, 9))

    def finish(self):
        beat = self.beat
        heavy = self.theme in HEAVY
        pop = self.bus.pop
        guitar = normalize(filter_stereo(rhythm_amp(pop("guitar"), heavy), 6500), -19.5 if heavy else -20.5)
        mute = pop("mute")
        mute = normalize(filter_stereo(rhythm_amp(mute, False), 2500), -24.0) if np.any(mute) else mute
        bass = normalize(bass_amp(pop("bass")), -18.5)
        lead = normalize(chorus(pop("lead"), .6, 1.4, 9, .22), -16.0)
        lead += filter_stereo(ping_pong(lead, beat, ((.75, .22), (1.5, .1))), 4000)
        brass = normalize(eq(np.tanh(pop("brass") * 1.3) / 1.3, ("peaking", 300, 1.0, -2), ("peaking", 2500, 1.2, 2)), -21.0)
        brass += filter_stereo(ping_pong(brass, beat, ((1.0, .2), (2.0, .08))), 3500)
        ep = normalize(chorus(pop("ep"), .9, 2.0, 11, .4), -24.0)
        ep += filter_stereo(ping_pong(ep, beat, ((.375, .16),)), 3500)
        strings_bus = normalize(chorus(pop("strings"), .4, 3.2, 16, .55), -27.0)
        choir_bus = pop("choir")
        choir_bus = normalize(chorus(choir_bus, .3, 4.0, 18, .6), -29.0) if np.any(choir_bus) else choir_bus
        seq = normalize(pop("seq"), -24.0)
        seq += filter_stereo(ping_pong(seq, beat, ((.375, .2), (.75, .08))), 5000)
        fx = pop("fx")
        fx = normalize(fx, -28.0) if np.any(fx) else fx
        drums = pop("drums")
        drums = normalize(drums + .3 * np.tanh(drums * 4) / 4, -16.5)
        drums += convolve(drums, plate_ir()) * .16
        print("bus dBFS (active)", {name: round(active_level(bus), 1) for name, bus in
                                   (("drums", drums), ("bass", bass), ("guitar", guitar), ("mute", mute), ("lead", lead),
                                    ("brass", brass), ("ep", ep), ("strings", strings_bus), ("choir", choir_bus), ("seq", seq))},
              flush=True)
        send = lead * .3 + brass * .35 + ep * .4 + strings_bus * .5 + choir_bus * .5 + seq * .3 + drums * .08 + guitar * .04
        glue = convolve(guitar * .7 + mute * .5 + drums * .8 + brass + bass * .3, room_ir()) * .16
        hall = convolve(lead * .6 + strings_bus + choir_bus, hall_ir()) * .25
        dry = guitar + mute + bass + lead + brass + ep + strings_bus + choir_bus + seq + fx + drums + glue + hall
        del guitar, mute, bass, lead, brass, ep, strings_bus, choir_bus, seq, fx, drums, glue, hall
        dry += reverb(send)
        del send
        extended = np.concatenate((dry[-SR:], dry), axis=0)
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
        base.render(score, RomplerMix(score, instruments))
        strings.cache_clear()
        rompler.clear_caches()
        brass_section.clear_cache()
