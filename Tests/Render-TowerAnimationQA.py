"""Package unmodified Unity renderer captures for normal/slow motion visual QA."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

FONT_PATH = Path('C:/Windows/Fonts/consola.ttf')
FONT = ImageFont.truetype(str(FONT_PATH), 18) if FONT_PATH.exists() else ImageFont.load_default()
SMALL = ImageFont.truetype(str(FONT_PATH), 15) if FONT_PATH.exists() else FONT
BACKGROUND = '#07101a'


def read_report(directory):
    return json.loads((directory / 'animation-report.json').read_text(encoding='utf-8-sig'))


def frame_label(source, report, title, index, running=False, slow=False):
    result = ImageOps.expand(source.convert('RGB'), border=(0, 52, 0, 0), fill=BACKGROUND)
    draw = ImageDraw.Draw(result)
    if running:
        speed = 1.5 if index < 60 else 3 if index < 120 else 5
        phase = f'ROOT SPEED {speed} world units/s'
    elif report.get('frameMotions'):
        phase = report['frameMotions'][index]
    else:
        phase = 'IDLE' if index < 12 or index >= 54 else 'RUN' if index < 30 else 'ATTACK + RECOVERY'
    rate = ' | 0.33x slow motion' if slow else ' | normal speed'
    draw.text((12, 5), f'{title} | {index / 60:.2f}s | {phase}{rate}', font=FONT, fill='#e3edf2')
    if report.get('probeContract'):
        draw.text((12, 29), 'Actor scale 0.82 | camera follows actors | floor meter ticks remain fixed in the world', font=SMALL, fill='#abbec9')
        for actor in report['characters']:
            x = int(188 + actor['column'] * 301)
            y = 70 + actor['row'] * 335
            name = f"{actor['artIndex']} {actor['name']} / {actor['role']}"
            box = draw.textbbox((0, 0), name, font=SMALL)
            draw.text((x - (box[2] - box[0]) // 2, y), name, font=SMALL, fill='#abbec9')
    else:
        draw.text((120, 29), 'Michael / winged           Zeus / mage           Rabbit / melee', font=SMALL, fill='#abbec9')
    return result


def palette_for(directory, report, stem, count):
    sample = Image.new('RGB', (800, 90 * 4), BACKGROUND)
    for slot, index in enumerate(range(0, count, max(1, count // 40))):
        if slot >= 40:
            break
        with Image.open(directory / f'{stem}{index:03}.png') as image:
            image = image.convert('RGB')
            image.thumbnail((80, 90))
            sample.paste(image, ((slot % 10) * 80, (slot // 10) * 90))
    return sample.quantize(colors=224)


def encode(directory, report, title, stem, count, destination, slow=False, crop=None):
    # Shared palette prevents quantizer flicker; only one raw image is held at once.
    palette = palette_for(directory, report, stem, count)
    encoded = []
    for index in range(0, count, 2):
        with Image.open(directory / f'{stem}{index:03}.png') as source:
            if crop:
                source = source.crop(crop).resize((640, 640), Image.Resampling.NEAREST)
            labeled = frame_label(source, report, title, index, running=stem == 'run-', slow=slow)
            encoded.append(labeled.quantize(palette=palette, dither=Image.Dither.NONE))
    duration = 100 if slow else [40 if i % 3 == 0 else 30 for i in range(len(encoded))]
    encoded[0].save(destination, save_all=True, append_images=encoded[1:], loop=0,
                    duration=duration, disposal=2, optimize=False)


def contact_sheet(directory, report, title):
    count = report['sampledFrames']
    moments = (0, 30, 60, 86, 98, 108, 125, 156, 169, 188, 208, 239) if count == 240 else (0, 12, 20, 29, 35, 43, 49, 54, 59)
    with Image.open(directory / 'frame-000.png') as source:
        width, height = source.width, source.height + 52
    sheet = Image.new('RGB', (width * 3, height * ((len(moments) + 2) // 3)), BACKGROUND)
    for slot, index in enumerate(moments):
        with Image.open(directory / f'frame-{index:03}.png') as source:
            image = frame_label(source, report, title, index)
            sheet.paste(image, ((slot % 3) * width, (slot // 3) * height))
    sheet.save(directory / 'contact-sheet.png')


def metrics(directory, report):
    if not report.get('catalog'):
        return
    actors = report['catalog']
    ground = [a for a in actors if not a.get('hovering')]
    lines = [f"Actual renderer samples: {report['catalogMotionSamples']} / {len(actors)} companions",
             f"Production actor scale: {report.get('actorScale', 1)}; grounded actors: {len(ground)}; hovering: {len(actors) - len(ground)}",
             f"Face / garment / weapon maximum edge strain: {max(a['maximumFaceStretch'] for a in actors):.6%} / {max(a['maximumGarmentStretch'] for a in actors):.6%} / {max(a['maximumWeaponStretch'] for a in actors):.6%}"]
    if ground and ground[0].get('gaits'):
        lines.append(f"Worst rendered contact slip, steady gait / actions: {max(g['maximumContactSlipRatio'] for a in ground for g in a['gaits']):.3%} / {max(a['maximumActionStanceSlipRatio'] for a in ground):.3%}")
        lines.append(f"Least observed action hand excursion, attack / cast: {min(a['attackHandExcursion'] for a in actors):.3f}H / {min(a['castHandExcursion'] for a in actors):.3f}H")
    lines += [''] + report.get('failures', [])
    (directory / 'metrics.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--title', default='Animation QA')
    parser.add_argument('--before', type=Path)
    args = parser.parse_args()
    report = read_report(args.directory)
    count = report['sampledFrames']
    encode(args.directory, report, args.title, 'frame-', count, args.directory / 'animation.gif')
    encode(args.directory, report, args.title, 'frame-', count, args.directory / 'animation-slow.gif', slow=True)
    if report.get('previewFrames'):
        encode(args.directory, report, args.title, 'run-', report['previewFrames'], args.directory / 'run.gif')
        encode(args.directory, report, args.title, 'run-', report['previewFrames'], args.directory / 'run-slow.gif', slow=True)
    contact_sheet(args.directory, report, args.title)
    metrics(args.directory, report)
    if args.before:
        before = read_report(args.before)
        # Older evidence has a different fixture layout and duration. Show its complete
        # native clip in the comparison, rather than equating unrelated timestamps.
        reference = args.directory / 'before-reference.gif'
        encode(args.before, before, 'BEFORE / previous fixture layout', 'frame-', before['sampledFrames'], reference)
    print(args.directory / 'animation.gif')
    print(args.directory / 'animation-slow.gif')
    print(args.directory / 'contact-sheet.png')


if __name__ == '__main__':
    main()
