"""Bakes chuchuking's hero (Assets/RogueLike/Character/Spine/Player) for TowerHeroPixelArt.

    python paint_hero.py [--preview DIR] [--out DIR]

One 18-cell strip (96 px cells) in Resources/Tower/Hero/Hero.png:
0-10 the body poses the gods use (idle, run, jump, fall, hurt), 11 the weapon hand, 12 the free hand
(open, also the orb stance's reaching hand), 13 sword, 14 crossbow, 15 orb, 16 crossbow bolt,
17 card portrait. Every weapon cell points up from the grip pixel, so the crossbow is stood on end.
Hero.json carries the body pivot, the upper-body sink of each pose, the arm layout shared with
chuchuking's Zeus, how far the blade reaches, where a bolt leaves the crossbow and the drawn
area of each hand and weapon cell.
"""
import argparse
import json
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import paint_gods as gods  # noqa: E402

OUT_DIR = os.path.join(gods.ROOT, 'Assets', 'Demos', 'Roguelike', 'Resources', 'Tower', 'Hero')
CELLS = {'weaponHand': 11, 'freeHand': 12, 'sword': 13, 'crossbow': 14, 'orb': 15, 'bolt': 16, 'portrait': 17}
HERO = dict(id='hero', spine='Player')
# Zeus's arm: the weapon hand hangs 8 px from the shoulder behind the body, the free hand rests in front.
SHOULDER = (4, 13)
ARM = 8
FRONT = (8, 9)
BACK = (-7, 12)
# The resting sword guard (TowerHeroPixelArt.Guard with the aim level): arm 18 degrees down, blade 64 up.
GUARD_ARM, GUARD_BLADE = -18, 64
# chuchuking's crossbow: the bolt tip (51, 34) sits 21 px ahead of and 4 px above the grip (30, 38).
CROSSBOW_MUZZLE = (21, 4)


def trim(cell):
    left, top, right, bottom = cell.getbbox()
    return [left, top, right - left, bottom - top]


def centred(image):
    """A cell with the middle of `image`'s drawn pixels on the grip pixel."""
    left, top, right, bottom = image.getbbox()
    return gods.held_cell(image, ((left + right) // 2, (top + bottom) // 2))


def build():
    body_cells, sink, free = gods.spine_body(HERO)
    hand = gods.held_cell(gods.gear_image('Player_arm'), gods.FRONT_ARM_GRIP)
    sword_art = gods.gear_image('Player_sword')
    sword = gods.held_cell(sword_art, gods.WEAPON_GRIP)
    reach = gods.WEAPON_GRIP[1] - sword_art.getbbox()[1]
    crossbow = gods.rotate_about_grip(gods.held_cell(gods.gear_image('Player_crossbow'), gods.WEAPON_GRIP), 90)
    orb = centred(gods.gear_image('Player_orb'))
    bolt = centred(gods.gear_image('Player_arrow'))
    layout = {
        'version': 1, 'cell': gods.CELL, 'grip': gods.GRIP, 'cells': CELLS,
        'bodyPivot': [gods.BODY_OFFSET[0] + gods.PIVOT_X, gods.BODY_OFFSET[1] + gods.GROUND],
        'top': gods.HEAD_TOP, 'bob': sink,
        'front': list(FRONT), 'back': list(BACK), 'shoulder': list(SHOULDER), 'arm': ARM,
        'swordReach': reach,
        # Stood on end, the bolt tip is CROSSBOW_MUZZLE turned a quarter counter-clockwise.
        'muzzle': [-CROSSBOW_MUZZLE[1], CROSSBOW_MUZZLE[0]],
    }
    guard_hand =(SHOULDER[0] + ARM * math.cos(math.radians(GUARD_ARM)), SHOULDER[1] + ARM * math.sin(math.radians(GUARD_ARM)))
    card_layout = {'rest': GUARD_BLADE - 90, 'front': [round(v) for v in guard_hand], 'back': list(BACK), 'weaponBehind': True,
                   'oneHand': False, 'extra': [], 'secondGrip': 0, 'bodyPivot': layout['bodyPivot']}
    # TowerHeroPixelArt turns the hanging arm to point from the shoulder: arm angle + 90.
    portrait = gods.portrait(body_cells[0], gods.rotate_about_grip(hand, GUARD_ARM + 90), free, sword, None, card_layout)
    layout['card'] = [(gods.CELL - gods.CARD[0]) // 2, 0, gods.CARD[0], gods.CARD[1]]
    # Hands and weapons are cut to their drawn pixels (x, y from the cell's top left, width, height) so a turning
    # sprite's quad stays close to what it shows.
    held = [hand, free, sword, crossbow, orb, bolt]
    layout['trim'] = [v for cell in held for v in trim(cell)]
    cells = body_cells + held + [portrait]
    sheet = Image.new('RGBA', (gods.CELL * len(cells), gods.CELL))
    for i, cell in enumerate(cells):
        sheet.alpha_composite(cell, (i * gods.CELL, 0))
    return sheet, layout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--preview', default='')
    ap.add_argument('--out', default=OUT_DIR)
    args = ap.parse_args()
    sheet, layout = build()
    os.makedirs(args.out, exist_ok=True)
    sheet.save(os.path.join(args.out, 'Hero.png'))
    with open(os.path.join(args.out, 'Hero.json'), 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(layout, fh, indent=1)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        back = Image.new('RGBA', sheet.size, (78, 96, 116, 255))
        back.alpha_composite(sheet)
        back.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(args.preview, 'hero.png'))
    print('baked hero', sheet.size)


if __name__ == '__main__':
    main()
