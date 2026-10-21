"""Label Unity's actual-pose captures and make a browsable, per-character audit."""
import argparse
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
root = args.directory.resolve()
font_path = Path("C:/Windows/Fonts/arial.ttf")
font = ImageFont.truetype(str(font_path), 22) if font_path.exists() else ImageFont.load_default()
small = ImageFont.truetype(str(font_path), 16) if font_path.exists() else ImageFont.load_default()
records = []
for report_path in sorted(root.glob("[0-9]*.json"), key=lambda p: int(p.stem)):
    report = json.loads(report_path.read_text(encoding="utf-8"))
    source = Image.open(report_path.with_suffix(".png")).convert("RGB")
    width, height = source.width // 5, source.height // 2
    labeled = Image.new("RGB", (source.width, source.height + 100), "#131922")
    labeled.paste(source, (0, 100))
    draw = ImageDraw.Draw(labeled)
    draw.text((18, 10), f'{report["artIndex"]} | {report["name"]}', font=font, fill="white")
    draw.text((18, 43), "Production meshes | source / ownership / locomotion / attack / cast / facing", font=small, fill="#b3c1d0")
    for index, label in enumerate(report["poses"]):
        draw.text((index % 5 * width + 12, index // 5 * height + 78), label, font=font, fill="#b3d9ff")
    output = root / f"{report_path.stem}-labeled.png"
    labeled.save(output)
    records.append((report, source))

# Each row compares one character at full body size, so reviewers can inspect
# all fifty rather than relying on a few representative animation samples.
for first in range(0, len(records), 5):
    batch = records[first:first+5]
    sheet = Image.new("RGB", (1500, 330 * len(batch)), "#131922")
    draw = ImageDraw.Draw(sheet)
    for row, (report, source) in enumerate(batch):
        w, h = source.width // 5, source.height // 2
        for column, pose in enumerate((0, 2, 4, 5, 8)):
            crop = source.crop((pose % 5*w, pose // 5*h, (pose % 5+1)*w, (pose // 5+1)*h))
            # Crop screenshot background only; source assets remain untouched.
            bg = Image.new("RGB", crop.size, crop.getpixel((0, 0)))
            from PIL import ImageChops
            box = ImageChops.difference(crop, bg).getbbox()
            if box:
                crop = crop.crop(box)
            crop.thumbnail((280, 282), Image.Resampling.NEAREST)
            x, y = column*300+(300-crop.width)//2, row*330+40
            sheet.paste(crop, (x, y))
            label = f'{report["artIndex"]} {report["poses"][pose]}' if column == 0 else report["poses"][pose]
            draw.text((column*300+8, row*330+8), label, font=small, fill="white")
    sheet.save(root / f"review-{batch[0][0]['artIndex']}-{batch[-1][0]['artIndex']}.png")

items = "\n".join(
    f'<section><h2>{r["artIndex"]} — {html.escape(r["name"])}</h2>'
    f'<a href="{r["artIndex"]}-labeled.png"><img loading="lazy" src="{r["artIndex"]}-labeled.png"></a></section>'
    for r, _ in records
)
(root / "index.html").write_text(
    '<!doctype html><meta charset="utf-8"><title>Character limb and attack review</title>'
    '<style>body{background:#131922;color:#eef;font:16px sans-serif;max-width:1600px;margin:32px auto}'
    'img{width:100%}section{margin-bottom:36px}</style>'
    '<h1>Character limb and attack review</h1>'
    '<p>Actual Unity meshes. Click any image for full resolution. Each character includes both facing directions.</p>'
    + items, encoding="utf-8"
)
print(f"Labeled {len(records)} character reports: {root / 'index.html'}")
