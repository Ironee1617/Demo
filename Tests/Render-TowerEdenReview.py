"""Package actual Unity captures as a six-second animation preview; never edits game art."""
import argparse
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
report = json.loads((args.directory / "eden-report.json").read_text(encoding="utf-8"))
if not report["success"] or len(report["screenshots"]) != 144:
    raise SystemExit("A passing 144-frame Eden review is required.")
frames = []
for filename in report["screenshots"]:
    with Image.open(filename) as source:
        frames.append(source.convert("RGB"))
# GIF stores hundredths of a second: distribute rounding to retain 24 fps.
durations = [(round((i + 1) * 100 / 24) - round(i * 100 / 24)) * 10 for i in range(len(frames))]
destination = args.directory / "eden-animation.gif"
frames[0].save(destination, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True)
print(destination.resolve())
