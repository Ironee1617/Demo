"""Bakes the companion gods as chibi pixel sheets matching chuchuking's Zeus.

    python paint_gods.py [--only zeus,athena] [--preview DIR] [--out DIR]

Each god becomes one 16-cell strip (96 px cells) in Resources/Tower/Myths/Pixel/<id>.png:
0-3 idle, 4-7 run, 8 jump, 9 fall, 10 hurt, 11 front fist, 12 back fist, 13 weapon,
14 off-hand item, 15 card portrait. Gods.json carries pivots, grips and the rest pose
that TowerGodPixelArt animates at run time. Zeus keeps chuchuking's idle frames,
fists and lightning bolt; only his run/jump/hurt frames are derived from them.
"""
import argparse
import json
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pixkit import (Canvas, GEAR, BODY, GROUND, cells_of, draw, fixed, hair_ramp, image_cells, overlay, paint, ramp, rgb,
                    shifted, W as BW, H as BH)
import body
import extras
import hair
import heads
import weapons
from gods import GODS, SKINS, EYES, GOLD, SILVER

ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ZEUS_DIR = os.path.join(ROOT, 'Assets', 'RogueLike', 'Character', 'Zeus')
OUT_DIR = os.path.join(ROOT, 'Assets', 'Demos', 'Roguelike', 'Resources', 'Tower', 'Myths', 'Pixel')
CELL = 96
BODY_OFFSET = (17, 10)          # where the 61x75 body frame sits inside a cell
GRIP = 48                       # weapons and fists put their grip pixel at (48, 48)
POSES = [('idle', 0), ('idle', 1), ('idle', 2), ('idle', 3), ('run', 0), ('run', 1), ('run', 2), ('run', 3),
         ('jump', 0), ('fall', 0), ('hurt', 0)]
CELLS = {'front': 11, 'back': 12, 'weapon': 13, 'offhand': 14, 'portrait': 15}
# Rest layout in art pixels from the ground point between the boots (x right, y up).
FRONT_HAND = (8, 11)
BACK_HAND = (-7, 12)
REST_ANGLE = {'swing': -32, 'thrust': -6, 'bow': 0, 'cast': -10, 'throw': -14}
FRONT_BY_KIND = {'bow': (10, 13), 'thrust': (8, 10)}
EXTRA_HANDS = [(-12, 19), (13, 20)]


def palette(g):
    hair_color = g['hair'][1]
    cloth2 = g.get('cloth2', g['cloth'])
    glow = rgb(g.get('glow', '#ffe68a'))
    p = {
        'skin': SKINS[g['skin']], 'eye': EYES[g['eyes']],
        'white': fixed('#8a8a96', '#c8c8d4', g.get('white', '#f3f3f5'), '#ffffff', '#ffffff'),
        'mouth': fixed('#9b6143'), 'blush': fixed(g.get('blush', '#f7b09c')), 'ink': fixed('#282628'),
        'hair': hair_ramp(hair_color), 'cloth': ramp(g['cloth']), 'trim': ramp(g.get('trim', g['cloth'])),
        'cloth2': ramp(cloth2), 'metal': ramp(g.get('metal', GOLD)), 'metal2': ramp(g.get('metal2', SILVER)),
        'cape': ramp(g.get('cape', cloth2)), 'boot': ramp(g.get('boot', '#7a5634')),
        'fur': hair_ramp(g.get('fur', hair_color)), 'wing': ramp(g.get('wing', g.get('wings', '#f4f4f8'))),
        'gem': ramp(g.get('gem', '#4f8ad8')),
        'glow': fixed(tuple(int(c * .75) for c in glow), tuple(int(c * .88) for c in glow), glow,
                      tuple(min(255, int(c + (255 - c) * .5)) for c in glow), '#ffffff'),
        'hat': ramp(g.get('hat', cloth2)), 'crown': ramp(g.get('crown', '#f4f2ea')),
        'sun': ramp(g.get('sun', '#e8462e')), 'horn': ramp(g.get('horn', '#efe6d0')),
        'flower': ramp(g.get('flower', '#ffffff')), 'leaf': ramp('#5aa84a'),
        'snake': ramp(g.get('snake', '#4f9a6a')), 'sash': ramp(g.get('sash', '#e8402e')),
        'quiver': ramp(g.get('quiver', '#7a5634')), 'eyegem': ramp('#e0402e'),
        'blade': ramp(g.get('blade', '#d4d8e2')), 'gold': ramp(GOLD),
        'wood': ramp(g.get('bow', g.get('wood', '#8a5a34'))), 'ribbon': ramp(g.get('ribbon', g.get('trim', g['cloth']))),
        'dark': ramp(g.get('dark', '#3a3048')), 'string': fixed('#f0eee6'),
        'feather': ramp(g.get('feather', g.get('wing', g.get('wings', '#f4f4f8')))),
        'tiger': ramp('#e8902e'), 'ermine': ramp('#f6f4f0'),
    }
    glove = g.get('glove')
    p['hand'] = ramp(glove) if glove else p['skin']
    p['cuff'] = p['metal'] if glove else p['trim']
    return p


def compose(g, kind, f):
    long_outfit = g['outfit'] in body.LONG
    look, mouth = g.get('look', 'hero'), g.get('mouth', 'smile')
    if kind == 'idle':
        dy = ([0, 1, 1, 0] if long_outfit else [0, 1, 2, 1])[f]
        stride, cape_frame, cape_run = 'idle', f, False
    elif kind == 'run':
        dy = [1, 0, 1, 0][f]
        stride, cape_frame, cape_run = 'run%d' % f, f, True
    elif kind == 'jump':
        dy, stride, cape_frame, cape_run = -1, 'jump', 0, True
    elif kind == 'fall':
        dy, stride, cape_frame, cape_run = 0, 'fall', 1, False
    else:
        dy, stride, cape_frame, cape_run = 1, 'idle', 2, False
        look, mouth = 'hurt', ('none' if mouth == 'none' else 'open')
    flap = [-1, 0, 1, 0][f] if kind == 'idle' else (-2 if kind in ('run', 'jump') else 0)
    style = g['hair'][0]
    covered = any(name in heads.COVERS_HAIR for name in g.get('gear', []))

    upper, lower = Canvas(), Canvas()
    if g.get('wings'):
        extras.wings(upper, dy=flap)
    if g.get('sash'):
        extras.sash(upper)
    for name in g.get('gear', []):
        heads.gear(upper, name, stage='back')
    if g.get('cape'):
        # Zeus's idle capes were measured with his breathing already in them.
        body.cape(upper, cape_frame, run=cape_run, dy=0 if cape_run else -dy)
    if g.get('quiver'):
        extras.quiver(upper)
    if not covered or name_has(g, 'helm_corinthian'):
        hair.back(upper, style)

    if g.get('tail'):
        extras.tail(lower, g['tail'])
    if g.get('serpent'):
        extras.snake(lower, sway=[0, 1, 0, -1][f] if kind in ('idle', 'run') else 0)
    else:
        body.legs(lower, stride, g.get('boots', 'boot'), stance=1 if g.get('build') in ('broad', 'stout') else 0)
        if g.get('wheels'):
            extras.wheels(lower, body.STRIDES[stride])

    build = g.get('build', 'normal')
    body.torso(upper, g['outfit'], build)
    for name in g.get('wear', []):
        body.wear(upper, name, build)
    body.neck(upper)
    for name in g.get('wear', []):
        body.wear(upper, name, build, stage='neck')
    body.face(upper, look, mouth, lash=g.get('lash', False))
    if g.get('beard'):
        heads.beard(upper, g['beard'])
    # The near ear is part of each hairstyle, as in chuchuking's Zeus.
    if not covered or name_has(g, 'helm_corinthian'):
        hair.front(upper, style)
    if g.get('brow') and look != 'hurt':
        body.brows(upper, g['brow'])
    for name in g.get('gear', []):
        heads.gear(upper, name)

    out = Canvas()
    overlay(out, lower)
    overlay(out, shifted(upper, 0, dy))
    return out


def name_has(g, gear):
    return gear in g.get('gear', [])


def render_rows(rows, legend, pal, mats=None, flip=False):
    c = Canvas(64, 96)
    cells = cells_of(rows, 4, 4, legend, mats, flip)
    paint(c, cells, ring='outer')
    return c.render(pal)


def place(cell_img, sprite, at):
    """Pastes sprite with its top-left at cell coordinates."""
    cell_img.alpha_composite(sprite, (int(at[0]), int(at[1])))


def fist_images(g, pal):
    imgs = []
    for flip in (False, True):
        rows = extras.FIST
        c = Canvas(16, 16)
        paint(c, cells_of(rows, 5, 3, extras.HAND_LEGEND, flip=flip), ring='outer')
        img = c.render(pal)
        cell = Image.new('RGBA', (CELL, CELL))
        # Grip pixel: column 2, row 3 of the fist rows.
        cell.alpha_composite(img, (GRIP - 7, GRIP - 6))
        imgs.append(cell)
    return imgs


def weapon_cell(name, pal, flip=False):
    rows, (gx, gy) = weapons.W[name]
    width = max(len(r) for r in rows)
    c = Canvas(width + 12, len(rows) + 12)
    paint(c, cells_of(rows, 6, 6, GEAR, flip=flip), ring='outer')
    img = c.render(pal)
    if flip:
        gx = width - 1 - gx
    cell = Image.new('RGBA', (CELL, CELL))
    cell.alpha_composite(img, (GRIP - 6 - gx, GRIP - 6 - gy))
    tip = gy  # rows above the grip
    return cell, tip


def rotate_about_grip(cell, degrees):
    # PIL rotates counter-clockwise for positive angles, like Unity's z rotation.
    return cell.rotate(degrees, resample=Image.NEAREST, center=(GRIP + .5, GRIP + .5))


def cell_of_body(frame):
    cell = Image.new('RGBA', (CELL, CELL))
    cell.alpha_composite(frame, BODY_OFFSET)
    return cell


def portrait(body_cell, front, back, weapon, offhand, layout):
    """Rest pose, assembled exactly like the runtime component does."""
    out = Image.new('RGBA', (CELL * 2, CELL * 2))
    origin = (CELL, int(CELL * 1.6))

    def at(offset):
        return (origin[0] + offset[0] - GRIP, origin[1] - offset[1] - GRIP)

    for i in range(0, len(layout['extra']), 2):
        out.alpha_composite(back, at(layout['extra'][i:i + 2]))
    if offhand is not None and layout['oneHand']:
        out.alpha_composite(offhand, at(layout['back']))
    elif not layout['oneHand']:
        if offhand is not None:
            out.alpha_composite(offhand, at(layout['back']))
        out.alpha_composite(back, at(layout['back']))
    out.alpha_composite(body_cell, (int(round(origin[0] - layout['bodyPivot'][0])), int(origin[1] - layout['bodyPivot'][1])))
    out.alpha_composite(rotate_about_grip(weapon, layout['rest']), at(layout['front']))
    out.alpha_composite(front, at(layout['front']))
    # Every card shows the same window around the body, so the UI draws all gods at
    # Zeus's height; a long bolt or tall crown is cropped instead of shrinking its owner.
    left, top = origin[0] - CARD[0] // 2, origin[1] - CARD[1] + CARD_BELOW
    out = out.crop((left, top, left + CARD[0], top + CARD[1]))
    cell = Image.new('RGBA', (CELL, CELL))
    cell.alpha_composite(out, ((CELL - CARD[0]) // 2, CELL - CARD[1]))
    return cell


CARD = (54, 54)       # card window in art pixels
CARD_BELOW = 2        # rows under the boots
PIVOT_X = 31.0
HEAD_TOP = 36   # Zeus's hair starts 36 px above the ground; tall crowns and ears do not move the health bar


def bob(g):
    """How far the upper body (and so the fists) sinks in each body frame, in pixels."""
    idle = [0, 1, 1, 0] if g.get('outfit') in body.LONG else [0, 1, 2, 1]
    return idle + [1, 0, 1, 0] + [-1, 0, 2 if g['id'] == 'zeus' else 1]


def ground_pivot_x(frame):
    """Centre of the boots in the idle frame (columns with pixels in rows 50..54)."""
    xs = [x for y in range(50, 55) for x in range(frame.width) if frame.getpixel((x, y))[3]]
    return (min(xs) + max(xs) + 1) / 2.0


def head_top(frames):
    return min(f.getbbox()[1] for f in frames[:4])


def zeus_frames():
    """chuchuking's idle frames plus run/jump/fall/hurt derived from them."""
    idle = [Image.open(os.path.join(ZEUS_DIR, 'Idle_%d.png' % i)).convert('RGBA') for i in range(4)]
    navy = (16, 41, 99)
    base = idle[0]
    boots_l = base.crop((24, 50, 31, 55))
    boots_r = base.crop((31, 50, 38, 55))

    def without(img, fn):
        out = img.copy()
        px = out.load()
        for y in range(out.height):
            for x in range(out.width):
                if px[x, y][3] and fn(x, y, px[x, y]):
                    px[x, y] = (0, 0, 0, 0)
        return out

    def legs_at(img, stride):
        (lx, ly), (rx, ry) = body.STRIDES[stride]
        out = without(img, lambda x, y, p: y >= 50 and 24 <= x <= 37)
        out.alpha_composite(boots_r, (31 + rx, 50 + ry))
        out.alpha_composite(boots_l, (24 + lx, 50 + ly))
        return out

    def upper_shift(img, dy):
        top = without(img, lambda x, y, p: y >= 50)
        bottom = without(img, lambda x, y, p: y < 50)
        out = Image.new('RGBA', img.size)
        out.alpha_composite(bottom)
        out.alpha_composite(top, (0, dy))
        return out

    def recape(img, frame, run):
        out = without(img, lambda x, y, p: p[:3] == navy)
        c = Canvas()
        body.cape(c, frame, run=run, ring=None)
        cape_img = c.render({'cape': fixed(navy)})
        under = Image.new('RGBA', img.size)
        under.alpha_composite(cape_img)
        under.alpha_composite(out)
        return under

    frames = list(idle)
    for f in range(4):
        frames.append(recape(upper_shift(legs_at(base, 'run%d' % f), [1, 0, 1, 0][f]), f % 2, True))
    frames.append(recape(upper_shift(legs_at(base, 'jump'), -1), 0, True))
    frames.append(legs_at(idle[1], 'fall'))
    hurt = upper_shift(idle[2].copy(), 0)
    px = hurt.load()
    skin, lash, mouth = (253, 216, 183, 255), (11, 14, 28, 255), (155, 97, 67, 255)
    for y in range(33, 36):
        for x in range(27, 37):
            if px[x, y + 2][:3] in ((11, 14, 28), (46, 71, 104), (82, 129, 181), (243, 243, 245), (244, 167, 125)):
                px[x, y + 2] = skin
    for x, y in ((27, 33), (28, 34), (29, 34), (27, 35), (36, 33), (35, 34), (34, 34), (36, 35)):
        px[x, y + 2] = lash
    for x, y in ((31, 37), (32, 37), (31, 38), (32, 38)):
        px[x, y + 2] = mouth
    frames.append(hurt)
    return frames


def build(g):
    if g['id'] == 'zeus':
        frames = zeus_frames()
        arm0 = Image.open(os.path.join(ZEUS_DIR, 'Arm0.png')).convert('RGBA')
        arm1 = Image.open(os.path.join(ZEUS_DIR, 'Arm1.png')).convert('RGBA')
        front, back = Image.new('RGBA', (CELL, CELL)), Image.new('RGBA', (CELL, CELL))
        front.alpha_composite(arm0, (GRIP - 9, GRIP - 10))
        back.alpha_composite(arm1, (GRIP - 10, GRIP - 10))
        bolt = Image.open(os.path.join(ZEUS_DIR, 'Weapon.png')).convert('RGBA')
        weapon = Image.new('RGBA', (CELL, CELL))
        grip = (29, 50)
        weapon.alpha_composite(bolt, (GRIP - grip[0], GRIP - grip[1]))
        tip = grip[1] - bolt.getbbox()[1]
        offhand = None
    else:
        pal = palette(g)
        frames = [compose(g, k, f).render(pal) for k, f in POSES]
        front, back = fist_images(g, pal)
        weapon, tip = weapon_cell(g['weapon'], pal)
        offhand = weapon_cell(g['offhand'], pal)[0] if g.get('offhand') else None

    kind = g['kind']
    # Broad and stout bodies hold their fists a pixel further out.
    wide = 1 if g.get('build') in ('broad', 'stout') else 0
    # Every body is centred on column 31 like chuchuking's Zeus (boots 26..35, torso 25..36).
    body_pivot = (BODY_OFFSET[0] + PIVOT_X, BODY_OFFSET[1] + GROUND)
    layout = {
        'id': g['id'], 'art': g['art'], 'kind': kind,
        'bodyPivot': [round(body_pivot[0], 2), body_pivot[1]],
        'pivot': [0, 0],
        'top': HEAD_TOP,
        'bob': bob(g),
        'front': [x + reach for x, reach in zip(FRONT_BY_KIND.get(kind, FRONT_HAND), (wide, 0))],
        'back': [x + reach for x, reach in zip(BACK_HAND, (-wide, 0))],
        'rest': REST_ANGLE[kind],
        'reach': tip,
        'offhand': offhand is not None,
        'oneHand': bool(g.get('one_hand')),
        'extra': [v for e in EXTRA_HANDS[:g.get('extra_hands', 0)] for v in e],
    }
    cells = [cell_of_body(f) for f in frames]
    cells += [front, back, weapon, offhand or Image.new('RGBA', (CELL, CELL))]
    cells.append(portrait(cells[0], front, back, weapon, offhand, layout))
    # Card portraits are a fixed window: x, y from the cell bottom, width, height.
    layout['card'] = [(CELL - CARD[0]) // 2, 0, CARD[0], CARD[1]]
    sheet = Image.new('RGBA', (CELL * len(cells), CELL))
    for i, cell in enumerate(cells):
        sheet.alpha_composite(cell, (i * CELL, 0))
    del layout['pivot']
    return sheet, layout, frames


ZEUS = dict(id='zeus', art=105, kind='throw')


def preview(results, path, scale=4):
    """Idle 0 of every god with the rest pose, plus run and hurt frames."""
    cols = 10
    rows = (len(results) + cols - 1) // cols
    w, h = 80, 80
    sheet = Image.new('RGBA', (cols * w * scale, rows * h * scale), (78, 96, 116, 255))
    for i, (gid, img) in enumerate(results):
        port = img.crop((CELLS['portrait'] * CELL, 0, (CELLS['portrait'] + 1) * CELL, CELL))
        port = port.crop((8, 16, 88, 96))
        x, y = (i % cols) * w * scale, (i // cols) * h * scale
        sheet.alpha_composite(port.resize((w * scale, h * scale), Image.NEAREST), (x, y))
    sheet.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='')
    ap.add_argument('--preview', default='')
    ap.add_argument('--out', default=OUT_DIR)
    ap.add_argument('--no-write', action='store_true')
    args = ap.parse_args()
    roster = [ZEUS] + GODS
    roster.sort(key=lambda g: g['art'])
    only = set(filter(None, args.only.split(',')))
    results, layouts = [], []
    for g in roster:
        if only and g['id'] not in only:
            continue
        sheet, layout, frames = build(g)
        results.append((g['id'], sheet))
        layouts.append(layout)
        if not args.no_write:
            os.makedirs(args.out, exist_ok=True)
            sheet.save(os.path.join(args.out, g['id'] + '.png'))
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            strip = Image.new('RGBA', (11 * 61, 75), (78, 96, 116, 255))
            for i, fr in enumerate(frames):
                strip.alpha_composite(fr, (i * 61, 0))
            strip.resize((strip.width * 4, strip.height * 4), Image.NEAREST).save(
                os.path.join(args.preview, 'frames-' + g['id'] + '.png'))
    if not args.no_write and not only:
        doc = {'version': 1, 'cell': CELL, 'grip': GRIP, 'cells': CELLS, 'gods': layouts}
        with open(os.path.join(args.out, 'Gods.json'), 'w', encoding='utf-8', newline='\n') as fh:
            json.dump(doc, fh, indent=1)
    if args.preview:
        preview(results, os.path.join(args.preview, 'roster.png'))
    print('baked', len(results), 'gods')


if __name__ == '__main__':
    main()
