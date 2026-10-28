"""Reproduce the curated CC0 orchestral samples (VSCO 2 Community Edition) used by the mythology pieces.

Usage:
  python prepare_samples.py --download [--staging <dir>]   # fetch the pinned files, then process
  python prepare_samples.py --staging <dir>                # process files already downloaded

Selected: upright piano, flute, oboe, clarinet, solo violin, cello section, violin section, harp,
timpani, glockenspiel, tubular bells, bass drum, claves, congas, gong and log drums. Leading silence
is trimmed, long sustains are shortened with a fade, one fixed gain per instrument/articulation is
applied, stereo and sample rate are kept, and everything is written as FLAC with provenance in
manifest.json (VSCO names middle C as C3, so C3 = MIDI 60).
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import re
import subprocess
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

# instrument, articulation, repository folder, filename filter, pitched, max seconds, gain
SELECTION = [
    ("piano", "sus", "Keys/Upright Nr1", r"^UR1_.*\.wav$", True, 6.0, 1.0),
    ("flute", "susNV", "Woodwinds/Flute/susNV", r"\.wav$", True, 4.5, 1.0),
    ("flute", "susvib", "Woodwinds/Flute/susvib", r"\.wav$", True, 4.5, 1.0),
    ("flute", "stac", "Woodwinds/Flute/stac", r"\.wav$", True, 2.0, 1.0),
    ("oboe", "vib", "Woodwinds/Oboe/Vib", r"\.wav$", True, 4.5, 1.0),
    ("oboe", "stac", "Woodwinds/Oboe/Stacc", r"\.wav$", True, 2.0, 1.0),
    ("clarinet", "sus", "Woodwinds/Clarinet/susLong", r"\.wav$", True, 4.5, 1.0),
    ("clarinet", "stac", "Woodwinds/Clarinet/stac", r"\.wav$", True, 2.0, 1.0),
    ("violin", "vib", "Strings/Solo Violin/Arco Vib", r"\.wav$", True, 4.5, 1.0),
    ("violin", "spic", "Strings/Solo Violin/spic", r"\.wav$", True, 2.0, 1.0),
    ("cellos", "vib", "Strings/Cello Section/susvib", r"\.wav$", True, 4.5, 1.0),
    ("violins", "vib", "Strings/Violin Section/susVib", r"\.wav$", True, 4.5, 1.0),
    ("harp", "pluck", "Strings/Harp", r"\.wav$", True, 5.0, 1.0),
    ("timpani", "hit", "Percussion/Timpani", r"\.wav$", True, 4.0, 1.0),
    ("glock", "hit", "Percussion/Glock", r"\.wav$", True, 4.0, 1.0),
    ("tubular", "hit", "Percussion", r"^TB_hit_.*\.wav$", True, 5.0, 1.0),
    ("bassdrum", "hit", "Percussion", r"^BDrumNewhit_.*\.wav$", False, 3.0, 1.0),
    ("claves", "hit", "Percussion", r"^Claves1_Hit_.*\.wav$", False, 1.0, 1.0),
    ("conga", "hit", "Percussion", r"^Conga-HitN_.*\.wav$", False, 2.0, 1.0),
    ("quinto", "hit", "Percussion", r"^Quinto-HitN_.*\.wav$", False, 2.0, 1.0),
    ("tumba", "hit", "Percussion", r"^Tumba-HitN_.*\.wav$", False, 2.0, 1.0),
    ("gong", "hit", "Percussion", r"^gongHit_.*\.wav$", False, 6.0, 1.0),
    ("logdrum", "hit", "Percussion", r"^LogDrum(Hi|Lo)_.*\.wav$", False, 2.0, 1.0),
]
SEMITONE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
OCTAVE_DOWN = {("piano", "sus"), ("harp", "pluck"), ("violin", "vib"), ("tubular", "hit")}
DYNAMICS = {"ppp": 20, "pp": 30, "p": 45, "mp": 60, "mf": 75, "f": 95, "ff": 110, "fff": 127}
NOTE = re.compile(r"(?:^|[_\-])([A-G]#?)(\d)(?=[_\-.])")
LAYER = re.compile(r"[_\-]v(\d+)(?=[_\-.])")
DYNAMIC = re.compile(r"[_\-](ppp|pp|p|mp|mf|f|ff|fff)(?=[_\-.])")
ROBIN = re.compile(r"(?:[_\-](?:rr|RR)(\d+)|_v\d+_(\d)\.wav$)")


def fetch(url):
    with urlopen(Request(url, headers={"User-Agent": "PrototypeLab-music"}), timeout=180) as response:
        return response.read()


TREE = None


def load_tree(path):
    """A recursive git tree of the pinned commit (api.github.com/repos/.../git/trees/<sha>?recursive=1),
    used instead of one contents call per folder to stay under the unauthenticated API rate limit."""
    global TREE
    TREE = json.loads(Path(path).read_text(encoding="utf-8"))["tree"]


def listing(folder):
    if TREE is not None:
        prefix = folder + "/"
        return [{"name": e["path"][len(prefix):], "size": e["size"]} for e in TREE
                if e["type"] == "blob" and e["path"].startswith(prefix) and "/" not in e["path"][len(prefix):]]
    url = API + quote(folder) + f"?ref={COMMIT}"
    data = subprocess.run(["curl", "-sS", "-H", "Accept: application/vnd.github+json", url], capture_output=True, text=True).stdout
    entries = json.loads(data)
    if isinstance(entries, dict):
        raise RuntimeError(f"{folder}: {entries.get('message')}")
    return [e for e in entries if e["type"] == "file"]


def jobs():
    out = []
    for instrument, articulation, folder, pattern, pitched, seconds, gain in SELECTION:
        for entry in listing(folder):
            if re.search(pattern, entry["name"]):
                out.append((instrument, articulation, folder + "/" + entry["name"], entry["size"], pitched, seconds, gain))
    return out


def download(staging, selected):
    def one(job):
        path, size = job[2], job[3]
        target = staging / path
        target.parent.mkdir(parents=True, exist_ok=True)
        if not (target.exists() and (target.stat().st_size == size or (not size and target.stat().st_size > 44))):
            for attempt in range(4):
                try:
                    data = fetch(RAW + quote(path))
                    if (size and len(data) != size) or len(data) <= 44:
                        raise IOError(f"size mismatch {len(data)} != {size}")
                    target.write_bytes(data)
                    break
                except Exception as error:
                    if attempt == 3:
                        return path, str(error)
        return path, None

    failures = []
    with ThreadPoolExecutor(max_workers=8) as pool:
        for index, (path, error) in enumerate(pool.map(one, selected), 1):
            if error:
                failures.append((path, error))
            if index % 25 == 0 or index == len(selected):
                print(f"fetched {index}/{len(selected)}", flush=True)
    for path in ("LICENSE", "Readme.txt"):
        target = staging / path
        if not target.exists():
            target.write_bytes(fetch(RAW + path))
    if failures:
        raise RuntimeError("downloads failed: " + "; ".join(f"{p}: {e}" for p, e in failures[:10]))


def parse(name):
    note = NOTE.search(name)
    midi = (int(note.group(2)) + 2) * 12 + SEMITONE[note.group(1)] if note else None
    timpani = re.match(r"Timpani(\d)_", name)
    if timpani:
        # The four timpani are recorded by drum, not by note; roots follow autocorrelation measurements of the hits.
        midi = (42, 34, 37, 40)[min(3, int(timpani.group(1)) - 1)]
    layer = LAYER.search(name)
    dynamic = DYNAMIC.search(name)
    robin = ROBIN.search(name)
    rr = int(next(g for g in robin.groups() if g)) if robin else 1
    return midi, (int(layer.group(1)) if layer else None), (dynamic.group(1) if dynamic else None), rr


def measure_pitch(audio, rate, expected_hz, short):
    mono = audio.mean(axis=1) if audio.ndim == 2 else audio
    length = round(.3 * rate) if short else rate
    start = min(max(0, len(mono) - length), round((.04 if short else .3) * rate))
    slice_ = mono[start:start + length].astype(np.float64)
    slice_ -= slice_.mean()
    spectrum = np.fft.rfft(slice_, 2 * len(slice_))
    correlation = np.fft.irfft(spectrum * np.conj(spectrum))[:len(slice_)]
    low, high = max(2, int(rate / (expected_hz * 1.6))), int(rate / (expected_hz / 1.6))
    if high <= low + 2 or high >= len(correlation):
        return expected_hz
    lag = low + int(np.argmax(correlation[low:high]))
    if 1 <= lag < len(correlation) - 1:
        a, b, c = correlation[lag - 1], correlation[lag], correlation[lag + 1]
        denominator = a - 2 * b + c
        if denominator != 0:
            lag += .5 * (a - c) / denominator
    return rate / lag


def process(staging, selected):
    samples = []
    (ROOT / "LICENSE-CC0.txt").write_bytes((staging / "LICENSE").read_bytes())
    groups = {}
    for job in selected:
        groups.setdefault((job[0], job[1]), []).append(job)
    for (instrument, articulation), items in groups.items():
        out_dir = ROOT / instrument
        out_dir.mkdir(exist_ok=True)
        layers = sorted({parse(Path(j[2]).name)[1] for j in items if parse(Path(j[2]).name)[1] is not None})
        for job in items:
            _, _, path, size, pitched, max_seconds, gain = job
            name = Path(path).name
            midi, layer, dynamic, rr = parse(name)
            if pitched and midi is None:
                print("skip (no note):", name)
                continue
            if (instrument, articulation) in OCTAVE_DOWN:
                # These folders name notes with C4 = 60 instead of VSCO's usual C3 = 60 (verified by pitch measurement).
                midi -= 12
            if dynamic:
                velocity = DYNAMICS[dynamic]
            elif layer is not None and layers:
                velocity = round(127 * layer / max(layers))
            else:
                velocity = 100
            source = (staging / path).read_bytes()
            audio, rate = sf.read(str(staging / path), dtype="float32", always_2d=True)
            envelope = np.abs(audio).max(axis=1)
            active = np.flatnonzero(envelope > max(1e-4, float(envelope.max()) * .004))
            lead = max(0, int(active[0]) - round(.002 * rate)) if len(active) else 0
            audio = audio[lead:].copy()
            if len(audio) > max_seconds * rate:
                audio = audio[:round(max_seconds * rate)].copy()
                fade = round(.08 * rate)
                audio[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)[:, None]
            else:
                fade = min(len(audio) // 4, round(.015 * rate))
                if fade > 0:
                    audio[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)[:, None]
            audio *= gain
            cents, measured, reliable = 0.0, None, False
            if pitched:
                expected = 440 * 2 ** ((midi - 69) / 12)
                measured = float(measure_pitch(audio, rate, expected, articulation in ("stac", "spic", "hit", "pluck")))
                cents = float(1200 * np.log2(measured / expected))
                reliable = abs(cents) <= 45
            tag = f"{midi}" if pitched else "x"
            out_name = f"{instrument}_{articulation}_{tag}_v{velocity}_rr{rr}.flac"
            out_path = out_dir / out_name
            if out_path.exists():
                out_name = f"{instrument}_{articulation}_{tag}_v{velocity}_rr{rr}_{len(samples)}.flac"
                out_path = out_dir / out_name
            sf.write(str(out_path), audio, rate, format="FLAC", subtype="PCM_16")
            samples.append({
                "instrument": instrument, "articulation": articulation, "file": f"{instrument}/{out_name}",
                "root_midi": midi, "velocity": velocity, "source_dynamic": dynamic, "source_velocity_layer": layer,
                "round_robin": rr, "source_path": path, "source_url": RAW + quote(path),
                "source_sha256": hashlib.sha256(source).hexdigest(), "source_bytes": len(source),
                "sha256": hashlib.sha256(out_path.read_bytes()).hexdigest(),
                "sample_rate": rate, "channels": int(audio.shape[1]), "frames": int(len(audio)),
                "duration_seconds": round(len(audio) / rate, 6), "peak": round(float(np.abs(audio).max()), 6),
                "trimmed_leading_frames": lead, "gain": gain,
                "measured_hz": round(measured, 3) if measured else None, "measured_cents": round(cents, 2),
                "tuning_cents": round(cents, 2) if reliable else 0.0, "tuning_reliable": reliable,
            })
        print(instrument, articulation, len(items), "files", flush=True)
    manifest = {
        "schema_version": 1, "library": "VSCO 2 Community Edition (VS Chamber Orchestra: CE)",
        "author": "Versilian Studios / Sam Gossner & Simon Dalzell; sample cutting by Elan Hickler (Soundemote)",
        "license": "CC0-1.0", "license_file": "LICENSE-CC0.txt", "repository_url": REPOSITORY, "commit": COMMIT,
        "license_evidence": [REPOSITORY + "/blob/" + COMMIT + "/LICENSE", "http://vis.versilstudios.net/vsco-community.html"],
        "selection": [{"instrument": s[0], "articulation": s[1], "folder": s[2], "filter": s[3]} for s in SELECTION],
        "processing": "Leading silence trimmed (2 ms kept), long sustains shortened with an 80 ms fade, short samples given a "
                      "15 ms tail fade, stereo and sample rate kept, FLAC output. No instrument waveform is synthesized.",
        "pitch_convention": "VSCO usually names middle C as C3 (C3 = 60); the upright piano, harp, solo violin Arco Vib and "
                            "tubular bells folders name it C4 instead, so their root_midi values are lowered by 12 (verified by "
                            "autocorrelation pitch measurement). Timpani roots are measured per drum. tuning_cents is the measured "
                            "offset from the root where the measurement was reliable.",
        "samples": samples,
    }
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print("manifest:", len(samples), "samples")
    for instrument in sorted({s["instrument"] for s in samples if s["root_midi"] is not None}):
        offsets = [s["measured_cents"] for s in samples if s["instrument"] == instrument]
        unreliable = sum(1 for s in samples if s["instrument"] == instrument and not s["tuning_reliable"])
        print(f"  {instrument}: median offset {float(np.median(offsets)):+.1f} cents, {unreliable} unreliable")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--staging", type=Path)
    parser.add_argument("--download", action="store_true")
    parser.add_argument("--tree", type=Path, help="recursive git tree JSON of the pinned commit (avoids per-folder API calls)")
    args = parser.parse_args()
    staging = args.staging or (ROOT / "originals")
    if args.tree:
        load_tree(args.tree)
    selected = jobs()
    print(f"{len(selected)} files, {sum(j[3] for j in selected) / 1e6:.1f} MB selected", flush=True)
    if args.download:
        download(staging, selected)
    process(staging, selected)
