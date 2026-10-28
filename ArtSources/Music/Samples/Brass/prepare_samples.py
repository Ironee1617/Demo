"""Reproduce the curated CC0 brass samples (VSCO 2 Community Edition) used by the Tower score.

Usage:
  python prepare_samples.py --staging <dir>   # process files already downloaded into <dir>
  python prepare_samples.py --download        # fetch the pinned files from GitHub first, then process

Trumpet, F horn and tenor trombone sustains and staccatos are trimmed of common leading
silence, sustains are shortened to 4.5 s with an 80 ms fade, staccatos get a 15 ms tail fade,
and one fixed gain per instrument/articulation is applied. Stereo and sample rate are kept.
Source and output SHA256 values are recorded in manifest.json.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import quote
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parents[3] / ".scratch" / "music-python"))
import numpy as np
import soundfile as sf

REPOSITORY = "https://github.com/sgossner/VSCO-2-CE"
COMMIT = "440300901dfe9275fd84e0b7763af1f8443ae62e"
RAW = f"https://raw.githubusercontent.com/sgossner/VSCO-2-CE/{COMMIT}/"
API = f"https://api.github.com/repos/sgossner/VSCO-2-CE/contents/"
FOLDERS = {"trumpet": "Brass/Trumpet", "horn": "Brass/F Horn", "trombone": "Brass/Tenor Trombone"}
ARTICULATIONS = ("sus", "stac")
NAME = re.compile(r"_(sus|stac)_([A-G]#?)(\d)_v(\d+)_(?:rr|r)?(\d+)\.wav$")
SEMITONE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
SUSTAIN_SECONDS = 4.5
GAIN = {("trumpet", "sus"): .9, ("trumpet", "stac"): .9, ("horn", "sus"): 1.0, ("horn", "stac"): 1.0,
        ("trombone", "sus"): 1.0, ("trombone", "stac"): 1.0}


def note_midi(name, octave):
    # VSCO names middle C "C3", so C3 = MIDI 60.
    return (int(octave) + 2) * 12 + SEMITONE[name]


def fetch(url):
    with urlopen(Request(url, headers={"User-Agent": "PrototypeLab-music"}), timeout=120) as response:
        return response.read()


def listing(instrument, articulation):
    url = API + quote(FOLDERS[instrument] + "/" + articulation) + f"?ref={COMMIT}"
    return [entry["name"] for entry in json.loads(fetch(url)) if entry["type"] == "file" and entry["name"].endswith(".wav")]


def download(staging):
    jobs = []
    for instrument, folder in FOLDERS.items():
        for articulation in ARTICULATIONS:
            for name in listing(instrument, articulation):
                jobs.append(f"{folder}/{articulation}/{name}")
    jobs += ["LICENSE", "Readme.txt"]

    def one(path):
        target = staging / path
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.write_bytes(fetch(RAW + quote(path)))
        return path

    with ThreadPoolExecutor(max_workers=8) as pool:
        for path in pool.map(one, jobs):
            print("fetched", path, flush=True)


def measure_pitch(audio, rate, expected_hz, staccato):
    """Autocorrelation pitch of a slice after the onset, searched around the expected note.

    Sustains use one second from 0.3 s in; staccatos use 0.3 s from 40 ms in, before the decay.
    """
    mono = audio.mean(axis=1) if audio.ndim == 2 else audio
    length = round(.3 * rate) if staccato else rate
    start = min(max(0, len(mono) - length), round((.04 if staccato else .3) * rate))
    slice_ = mono[start:start + length].astype(np.float64)
    slice_ -= slice_.mean()
    spectrum = np.fft.rfft(slice_, 2 * len(slice_))
    correlation = np.fft.irfft(spectrum * np.conj(spectrum))[:len(slice_)]
    low, high = int(rate / (expected_hz * 1.6)), int(rate / (expected_hz / 1.6))
    lag = low + int(np.argmax(correlation[low:high]))
    # Parabolic refinement.
    if 1 <= lag < len(correlation) - 1:
        a, b, c = correlation[lag - 1], correlation[lag], correlation[lag + 1]
        denominator = a - 2 * b + c
        if denominator != 0:
            lag += .5 * (a - c) / denominator
    return rate / lag


def process(staging):
    samples = []
    license_text = (staging / "LICENSE").read_bytes()
    (ROOT / "LICENSE-CC0.txt").write_bytes(license_text)
    for instrument, folder in FOLDERS.items():
        for articulation in ARTICULATIONS:
            source_dir = staging / folder / articulation
            files = sorted(p for p in source_dir.iterdir() if NAME.search(p.name))
            layers = sorted({int(NAME.search(p.name).group(4)) for p in files})
            out_dir = ROOT / instrument
            out_dir.mkdir(exist_ok=True)
            for path in files:
                match = NAME.search(path.name)
                note, octave, layer, rr = match.group(2), match.group(3), int(match.group(4)), int(match.group(5))
                midi = note_midi(note, octave)
                source = path.read_bytes()
                audio, rate = sf.read(str(path), dtype="float32", always_2d=True)
                envelope = np.abs(audio).max(axis=1)
                active = np.flatnonzero(envelope > max(1e-4, float(envelope.max()) * .004))
                lead = max(0, int(active[0]) - round(.002 * rate)) if len(active) else 0
                audio = audio[lead:]
                if articulation == "sus" and len(audio) > SUSTAIN_SECONDS * rate:
                    audio = audio[:round(SUSTAIN_SECONDS * rate)].copy()
                    fade = round(.08 * rate)
                    audio[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)[:, None]
                else:
                    fade = min(len(audio) // 4, round(.015 * rate))
                    audio = audio.copy()
                    audio[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)[:, None]
                audio *= GAIN[(instrument, articulation)]
                expected = 440 * 2 ** ((midi - 69) / 12)
                measured = measure_pitch(audio, rate, expected, articulation == "stac")
                cents = float(1200 * np.log2(measured / expected))
                # Only trust small offsets as intonation; a large value is a detector miss on a noisy attack.
                reliable = abs(cents) <= 45
                out_name = f"{instrument}_{articulation}_{midi}_v{layer}_rr{rr}.flac"
                out_path = out_dir / out_name
                sf.write(str(out_path), audio, rate, format="FLAC", subtype="PCM_16")
                samples.append({
                    "instrument": instrument, "articulation": articulation, "file": f"{instrument}/{out_name}",
                    "note_name": f"{note}{octave}", "root_midi": midi,
                    "velocity": round(127 * layer / max(layers)), "source_velocity_layer": layer,
                    "source_velocity_layer_count": max(layers), "round_robin": rr,
                    "source_path": f"{folder}/{articulation}/{path.name}",
                    "source_url": RAW + quote(f"{folder}/{articulation}/{path.name}"),
                    "source_sha256": hashlib.sha256(source).hexdigest(), "source_bytes": len(source),
                    "sha256": hashlib.sha256(out_path.read_bytes()).hexdigest(),
                    "sample_rate": rate, "channels": int(audio.shape[1]), "frames": int(len(audio)),
                    "duration_seconds": round(len(audio) / rate, 6), "peak": round(float(np.abs(audio).max()), 6),
                    "trimmed_leading_frames": lead, "gain": GAIN[(instrument, articulation)],
                    "measured_hz": round(float(measured), 3), "measured_cents": round(cents, 2),
                    "tuning_cents": round(cents, 2) if reliable else 0.0, "tuning_reliable": reliable,
                })
                print(out_name, f"{len(audio) / rate:.2f}s", f"{cents:+.1f}c", "" if reliable else "(unreliable, not applied)", flush=True)
    manifest = {
        "schema_version": 1, "library": "VSCO 2 Community Edition (VS Chamber Orchestra: CE)",
        "author": "Versilian Studios / Sam Gossner & Simon Dalzell; sample cutting by Elan Hickler (Soundemote)",
        "license": "CC0-1.0", "license_file": "LICENSE-CC0.txt", "repository_url": REPOSITORY, "commit": COMMIT,
        "license_evidence": [REPOSITORY + "/blob/" + COMMIT + "/LICENSE", "http://vis.versilstudios.net/vsco-community.html"],
        "selection": "Trumpet, F horn and tenor trombone: sus and stac articulations, every velocity layer and round robin.",
        "processing": "Common leading silence trimmed (2 ms kept), sustains shortened to 4.5 s with an 80 ms fade, "
                      "staccatos given a 15 ms tail fade, one fixed gain per instrument/articulation, stereo and sample rate kept, FLAC output.",
        "pitch_convention": "VSCO names middle C as C3; root_midi follows that (C3 = 60). tuning_cents is the measured "
                            "autocorrelation pitch offset from the named note.",
        "samples": samples,
    }
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print("manifest:", len(samples), "samples")
    for instrument in FOLDERS:
        offsets = [s["measured_cents"] for s in samples if s["instrument"] == instrument]
        unreliable = sum(1 for s in samples if s["instrument"] == instrument and not s["tuning_reliable"])
        print(instrument, "median measured offset", round(float(np.median(offsets)), 1), "cents;",
              unreliable, "unreliable measurements left uncorrected")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--staging", type=Path)
    parser.add_argument("--download", action="store_true")
    args = parser.parse_args()
    staging = args.staging or (ROOT / "originals")
    if args.download:
        download(staging)
    process(staging)
