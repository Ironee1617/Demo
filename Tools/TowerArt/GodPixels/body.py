"""Shared chibi anatomy measured from chuchuking's Zeus: face, torso, legs and capes.

Coordinates are absolute in the 61x75 frame. The face looks three quarters to the
right; the ear and the back of the head are on the left, the cape trails left.
"""
from pixkit import draw, mask_rows, BODY

# --- face -------------------------------------------------------------------------------------
# Zeus's face, pixel for pixel where his beard leaves it visible: the forehead under the
# bangs is in shadow, the eyes sit on rows 33..35 and the mouth on row 37. Below that the
# jaw follows the outline of his beard and closes on row 40.
FACE = [
    ".qqqqqqqq.",   # 29
    "qqqqqqqqqq",   # 30
    "qqqqqqqqqq",   # 31
    "qqqqqqqqqq",   # 32
    "kkkkkkkkkk",   # 33
    "kkkkkkkkkk",   # 34
    "kkkkkkkkkk",   # 35
    "kkkkkkkkkk",   # 36
    "kkkkkkkkkk",   # 37
    "qkkkkkkkkq",   # 38
    ".qkkkkkkq.",   # 39
    "..qqqqqq..",   # 40
]
FACE_X, FACE_Y = 27, 29

# Rows 32..36. Zeus's eye: a lash row, then the white, iris and pupil, then the lit iris;
# the bridge between the eyes keeps his alternating shadow. '.' leaves the face as it is.
# The near (left) eye spans x 27..30, the far one x 34..36. Every look keeps Zeus's
# placement and size class, only the lid line, iris and pupil change.
_ZEUS_EYES = ["EEEqkqkqEE", "WiEEkkkEiW", "kIIkkkkIIk"]
EYES = {
    # Zeus: open heroic eyes.
    'hero':    [None] + _ZEUS_EYES,
    # Upturned outer corners and a slanted lash: keen hunters and warriors.
    'sharp':   ["E.......EE", "kEEEqkqEEk", "kWiEkkkEiW", "kkIIkkkIIk"],
    # Narrower still, the long phoenix eye of Guan Yu.
    'phoenix': ["E........E", "kEEEqkqEEk", "kkiEkkkEik", "kkqqkkkqqk"],
    # Outer corners droop: soft and kind.
    'gentle':  [None, "kEEEqkqEEk", "EiEEkkkEiE", "kIIkkkkIIk"],
    # Heavy lids over the top of the iris: smug or cold.
    'sly':     [None, "kkkqkqkqkk", "EEEEkkkEEE", "kiIqkkkqiI"],
    # A flat thick lid and no white: stern and unreadable.
    'stern':   [None, "EEEEqkqEEE", "kiEEkkkEEi", "kqIqkkkqIq"],
    # Angry brows and pinpoint pupils in the white.
    'glare':   ["aaa....aaa", "EEEEqkqEEE", "WiEWkkkWEi", "kkIkkkkIkk"],
    # Wide white eyes with tiny pupils: battle frenzy.
    'wild':    [None, "EEEEqkqEEE", "WWEWkkkWEW", "kWWkkkkWWk"],
    # Big round eyes, a catch light and a fourth row: young and bright.
    'round':   [None, "EEEEqkqEEE", "EWiEkkkEWi", "EiiEkkkEii", "kIIkkkkIIk"],
    # A vertical slit in a bright iris: dragons, serpents and cats.
    'slit':    [None, "EEEqkqkqEE", "iIEIkkkIEI", "kIEIkkkIEk"],
    # No pupil, the iris glows.
    'glow':    [None, "EEEqkqkqEE", "E%*EkkkE*%", "k**kkkk**k"],
    # Thick lines above and below with a raised tail: Egyptian kohl.
    'kohl':    [None, "EEEEqkqEEE", "EiIEkkkEIi", "kEEkkkkEEE"],
    # Small eyes under bushy brows (drawn after the hair) with bags beneath.
    'wise':    [None, "qqqqqkqqqq", "kEiEkkkEik", "kqqkkkkqqk"],
    # Closed in meditation.
    'serene':  [None, "kkkqkqkkkk", "EEEkkkkEEE", "kqkkkkkkqk"],
    # Laughing, eyes shut in arcs.
    'happy':   [None, ".EE....EE.", "E..E..E..E", "kkkkkkkkkk"],
    # Small kind dots.
    'dot':     [None, "kkkqkqkkkk", "kEEkkkkEEk", "kkIkkkkkIk"],
    'hurt':    [None, "E........E", ".EE....EE.", "E........E"],
}
# Brows drawn over the bangs, as (x0, y0, rows) in hair tones.
BROWS = {
    'bushy': [(26, 31, ["dddd...ddd.", "abbba..abba"])],
    'angry': [(27, 31, ["aa......aa", "..aa..aa.."])],
}
MOUTHS = {
    'smile': ["....MM...."],
    'flat':  ["...MMM...."],
    'grin':  ["...MMMM...", "....MM...."],
    'open':  ["....MM....", "....MM...."],
    'none':  [".........."],
}


def face(c, look='hero', mouth='smile', lash=False):
    draw(c, FACE, FACE_X, FACE_Y, shade_rim=False)
    for j, row in enumerate(EYES[look]):
        if row:
            draw(c, [row], FACE_X, 32 + j, ring=None, shade_rim=False)
    if lash and look != 'hurt':
        # Lash flicks past the outer corners.
        draw(c, ["E..........E"], FACE_X - 1, 33, ring=None, shade_rim=False)
    draw(c, MOUTHS[mouth], FACE_X, 37, ring=None, shade_rim=False)


def brows(c, name):
    for x0, y0, rows in BROWS[name]:
        draw(c, rows, x0, y0, ring=None, shade_rim=False)


def neck(c, ch='q'):
    draw(c, [ch * 4, ch * 4], 30, 40, ring='outer', shade_rim=False)


# --- torso outfits: rows 41..52, columns 25..36; the chest is 8 wide (27..34) like Zeus ----------
TORSO_X, TORSO_Y = 25, 41
TORSOS = {
    # Greek chiton like Zeus: gold collar, white cloth, coloured hem and dark shorts.
    'tunic': [
        "....oooo....",
        "..o7oooo7o..",
        ".o72222227o.",
        "..72222222..",
        "..22223222..",
        "..77777777..",
        "..66666666..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
    'armor': [
        "....oooo....",
        "..ooppppoo..",
        ".oP333333po.",
        "..o333332o..",
        "..o233322o..",
        "..oo2222oo..",
        "..67777776..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
    'robe': [
        "....7777....",
        "..77222277..",
        ".7722222277.",
        "..27222222..",
        "..22722222..",
        "..22272222..",
        "..mmoppomm..",
        "..22222722..",
        ".2222222272.",
        ".2222222227.",
        "222222222222",
        "777777777777",
    ],
    'dress': [
        "....7777....",
        "..72222227..",
        ".7222332227.",
        "..22222222..",
        "..22222222..",
        "..mmoppomm..",
        "..22222222..",
        ".2222222222.",
        ".2222222222.",
        "222222222222",
        "777777777777",
    ],
    'short_dress': [
        "....7777....",
        "..72222227..",
        ".7222332227.",
        "..22222222..",
        "..22222222..",
        "..mmoppomm..",
        ".2222222222.",
        ".2222222222.",
        ".7777777777.",
    ],
    'bare': [
        "....kkkk....",
        "..ojojojoo..",
        ".kooooooook.",
        "..kkkkKkkk..",
        "..qkkkKkkq..",
        "..qkkkkkkq..",
        "..77777777..",
        "..mmoppomm..",
        "..22222222..",
        "..22222222..",
    ],
    'wrap': [
        "....k77k....",
        "..22722722..",
        ".2222772222.",
        "..22227222..",
        "..22222722..",
        "..22222272..",
        "..66666666..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
    'hanbok': [
        "....k77k....",
        "..22722722..",
        ".2222772222.",
        "..22227222..",
        "..77777777..",
        "..ssssssss..",
        ".ssssssssss.",
        ".ssssssssss.",
        "ssssssssssss",
        "ssssssssssss",
        "rrrrrrrrrrrr",
    ],
    'fur': [
        "...hhhhhh...",
        ".hhHHHHHHhh.",
        "..h222222h..",
        "..22223222..",
        "..22222222..",
        "..22222222..",
        "..22222222..",
        "..ffgGGgff..",
        "..rrrrrrrr..",
    ],
    'coat': [
        "....7777....",
        "..72222227..",
        ".7222772227.",
        "..22277222..",
        "..22277222..",
        "..22277222..",
        "..mmoppomm..",
        "..22277222..",
        ".2222772222.",
        ".7777777777.",
    ],
    # Greek muscle cuirass: chest and belly modelled in the metal.
    'cuirass': [
        "....7777....",
        "..oooooooo..",
        ".ooPPooPPoo.",
        "..ooonoooo..",
        "..ooonoooo..",
        "..ooonoooo..",
        "..oooooooo..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
    # Norse ring mail over a coloured tunic.
    'mail': [
        "....pppp....",
        "..onpnpnpo..",
        ".opnpnpnpno.",
        "..npnpnpnp..",
        "..pnpnpnpn..",
        "..npnpnpnp..",
        "..77777777..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
    # East Asian lamellar: rows of laced plates, a belt and plated tassets.
    'lamellar': [
        "....7777....",
        "..opopopop..",
        ".onnnnnnnno.",
        "..popopopo..",
        "..nnnnnnnn..",
        "..opopopop..",
        "..mmoppomm..",
        "..o2o22o2o..",
        "..n2n22n2n..",
    ],
    # An open vest over the bare chest.
    'vest': [
        "....kkkk....",
        "..22kkkk22..",
        ".222kkkk222.",
        "..22kKkk22..",
        "..22kkkk22..",
        "..22kqkk22..",
        "..77777777..",
        "..mmoppomm..",
        "..rrrrrrrr..",
    ],
}
# Rows below 49 cover the legs; the boots then only show beneath the hem.
LONG = {'robe', 'dress', 'hanbok'}


# --- builds: the same outfit widened or narrowed around the chest --------------------------------
# Columns 29 and 32 sit symmetrically inside the chest (25..36 around 30.5). A broad body repeats
# them, a slim one drops them, so belts, collars and seams keep their shape; parts left of the
# chest move out by one pixel and parts right of it the other way. A stout body is broad with a
# belly that bulges one more pixel on rows 45..48.
STRETCH = (29, 32)
BUILDS = ('slim', 'normal', 'broad', 'stout')


def fit(rows, x0, y0, build):
    """Returns the rows and their left column reshaped for a build."""
    if build == 'normal':
        return rows, x0
    cells = {}
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            ax = x0 + i
            if build == 'slim':
                if ax in STRETCH:
                    continue
                cells[(ax + 1 - sum(1 for s in STRETCH if s < ax), j)] = ch
            else:
                nx = ax - 1 + sum(1 for s in STRETCH if s < ax)
                cells[(nx, j)] = ch
                if ax in STRETCH:
                    cells[(nx + 1, j)] = ch
    if build == 'stout':
        for j in range(len(rows)):
            if 45 <= y0 + j <= 48:
                xs = [x for (x, jj), ch in cells.items() if jj == j and ch not in ' .']
                if xs:
                    left, right = min(xs), max(xs)
                    cells[(left - 1, j)] = cells[(left, j)]
                    cells[(right + 1, j)] = cells[(right, j)]
    xs = [x for (x, j) in cells]
    left = min(xs)
    out = []
    for j in range(len(rows)):
        line = ['.'] * (max(xs) - left + 1)
        for (x, jj), ch in cells.items():
            if jj == j:
                line[x - left] = ch
        out.append(''.join(line))
    return out, left


def torso(c, style, build='normal', mats=None, dy=0):
    rows, x0 = fit(TORSOS[style], TORSO_X, TORSO_Y, build)
    draw(c, rows, x0, TORSO_Y + dy, mats=mats)


# --- things worn over the outfit, laid out for a normal build ---------------------------------
# (x0, y0, rows[, options]). Options: 'stage' = 'neck' draws after the neck (a torc), 'mats'
# remaps materials, 'ring' sets the outline mode.
WEAR = {
    # Round shoulder plates.
    'pauldrons': [(24, 42, [".oo.", "oPoo", "oooo"]), (34, 42, [".oo.", "ooPo", "oooo"])],
    # Layered plates hanging from the shoulders (Chinese and Korean armour).
    'spaulders': [(23, 42, ["ooo.", "nnno", "ooop", "nnn."]), (35, 42, [".ooo", "onnn", "pooo", ".nnn"])],
    # Spiked plates for demons.
    'spikes': [(23, 39, ["o...", ".o..", ".oo.", "oPoo", "oooo"]), (35, 39, ["...o", "..o.", ".oo.", "ooPo", "oooo"])],
    # A fur ruff wider than the shoulders.
    'fur_mantle': [(25, 41, [
        "..hhhhhhhh..",
        ".hHhhHhhHhh.",
        "hhhhhhhhhhhh",
        "h.hh.hh.hh.h",
    ])],
    # White ermine with black tails.
    'ermine': [(24, 41, [
        ".HHHHHHHHHHHH.",
        "HH#HHHHHH#HHHH",
        "HHHHH#HHHHH#HH",
        ".H.HH.HH.HH.H.",
    ], {'mats': {'fur': 'ermine'}})],
    # A feather cape over the shoulders (crow or falcon).
    'feathers': [(24, 41, [
        ".yyyyyyyyyyyy.",
        "yYyyYyyyyYyyYy",
        "yyxyyyxyyyxyyy",
        "y.y.yy..yy.y.y",
    ], {'mats': {'wing': 'feather'}})],
    # Egyptian broad collar: gold, a row of beads and a coloured band.
    'usekh': [(26, 41, [
        "..oooooo..",
        ".jjjjjjjj.",
        "oPooooooPo",
        ".77777777.",
    ])],
    # A bead necklace with a pendant.
    'beads': [(27, 41, [
        "P......P",
        ".o....o.",
        ".o....o.",
        "..oPPo..",
        "...jj...",
    ], {'ring': None})],
    # Vishnu's long flower garland.
    'garland': [(27, 42, [
        "2......2",
        "7......7",
        "2......2",
        ".7....7.",
        ".2....2.",
        ".7....7.",
        "..2..2..",
        "..7227..",
    ], {'ring': None, 'mats': {'cloth': 'flower', 'trim': 'leaf'}})],
    # A Celtic torc closing at the throat.
    'torc': [(28, 41, ["PooooooP", ".P....P."], {'stage': 'neck'})],
    # A priest's stole hanging down both sides of the chest.
    'stole': [(27, 41, [
        "77....77",
        ".7....7.",
        ".7....7.",
        ".7....7.",
        ".8....8.",
        ".o....o.",
    ], {'ring': None})],
    # A strap across the chest from the right shoulder, for a quiver or a bag.
    'strap': [(26, 42, [
        ".......77",
        "......77.",
        ".....77..",
        "....77...",
        "...77....",
        "..77.....",
    ], {'ring': None, 'mats': {'trim': 'quiver'}})],
    'satchel': [(26, 42, [
        ".......77",
        "......77.",
        ".....77..",
        "....77...",
        "...77....",
    ], {'ring': None, 'mats': {'trim': 'quiver'}}), (24, 46, ["777", "787", "777"], {'mats': {'trim': 'quiver'}})],
    # Freyja's Brisingamen or a seer's amulet.
    'pendant': [(28, 41, ["o....o", ".o..o.", "..JJ..", "..jj.."], {'ring': None})],
    # Thor's belt of strength.
    'big_belt': [(26, 47, ["oooPPooooo", "nnnPjjPnnn"])],
    # Scalloped cloud collar.
    'cloud_collar': [(25, 41, [
        "..77777777..",
        ".7778778777.",
        "77.77..77.77",
    ])],
    # The sacred thread from the left shoulder to the right hip.
    'thread': [(27, 42, ["W.......", ".W......", "..W.....", "...W....", "....W...", ".....W.."], {'ring': None})],
    # Skirts over the shorts.
    'pteruges': [(27, 49, ["78787878", "7.7.7.7.", "6.6.6.6."], {'ring': 'outer'})],
    'kilt': [(26, 48, ["oPoooooPoo", "2222222222", ".22223222."])],
    'tiger_skirt': [(26, 48, ["hhhhhhhhhh", "h#hhh#hhh#", "hh#h.hh#h."], {'mats': {'fur': 'tiger'}})],
    'lotus_skirt': [(26, 48, ["hhhhhhhhhh", "hHhhHhhHhh", "h.hh.hh.hh"], {'mats': {'fur': 'leaf'}})],
    'dhoti': [(26, 48, ["7777777777", "2222322222", "222.22.222"])],
}


def wear(c, name, build='normal', stage='front', dy=0):
    for layer in WEAR[name]:
        x0, y0, rows = layer[:3]
        opts = layer[3] if len(layer) > 3 else {}
        if opts.get('stage', 'front') != stage:
            continue
        # The stout belly only bulges below the chest.
        rows, x0 = fit(rows, x0, y0, 'broad' if build == 'stout' and y0 + len(rows) <= 45 else build)
        draw(c, rows, x0, y0 + dy, mats=opts.get('mats'), ring=opts.get('ring', 'all'))


# --- legs: two boots 3 wide, standing at columns 27..29 and 33..35 ------------------------------
# Zeus's boots, rows 50..53 with the outline closing on row 54 (the ground is row 55):
# the near boot is three pixels wide at the cuff and two at the foot, the far one two.
BOOTS = {
    'boot': (["ggg", "sSs", "sS", "gG"], ["gg", "sS", "sS", "gG"]),
    'sandal': (["kkk", "kKk", "kK", "gG"], ["kk", "kK", "kK", "gG"]),
    'bare': (["kkk", "kKk", "kK", "kk"], ["kk", "kK", "kK", "kk"]),
    # Metal shin guards over the boot.
    'greaves': (["ooo", "oPo", "oP", "gG"], ["oo", "oP", "oP", "gG"]),
    # Fur wrapped to the calf.
    'fur': (["hhh", "hHh", "gg", "gG"], ["hh", "hH", "gg", "gG"]),
    # Tall riding boots.
    'tall': (["ggg", "gGg", "gG", "gGg"], ["gg", "gG", "gG", "gGg"]),
    # Loose trousers tied over slippers with an upturned toe.
    'shoes': (["sss", "sSs", "gg", "gGg"], ["ss", "sS", "gg", "gGg"]),
    # Cross-gartered leg wraps.
    'wraps': (["ggg", "SsS", "sS", "gG"], ["gg", "Ss", "sS", "gG"]),
}
BOOT_L, BOOT_R, BOOT_Y = 27, 33, 50
# Offsets (dx, dy) of (left, right) boots per pose.
STRIDES = {
    'idle': ((0, 0), (0, 0)),
    'run0': ((-2, 0), (2, -1)),
    'run1': ((-1, -1), (1, 0)),
    'run2': ((2, -1), (-2, 0)),
    'run3': ((1, 0), (-1, -1)),
    'jump': ((-1, -2), (2, -3)),
    'fall': ((-1, 0), (1, -1)),
}


def legs(c, pose='idle', boot='boot', mats=None, stance=0):
    """stance widens the gap between the feet on both sides (broad bodies)."""
    (lx, ly), (rx, ry) = STRIDES[pose]
    near, far = BOOTS[boot or 'boot']
    draw(c, far, BOOT_R + rx + stance, BOOT_Y + ry, mats=mats)
    draw(c, near, BOOT_L + lx - stance, BOOT_Y + ly, mats=mats)


# --- capes measured from chuchuking's four idle frames -----------------------------------------
CAPE_FRAMES = [
    {42: [(36, 36)], 43: [(17, 20), (24, 24), (36, 36)], 44: [(15, 25), (35, 35)], 45: [(14, 25)],
     46: [(13, 25), (35, 35)], 47: [(12, 14), (16, 25), (35, 35)], 48: [(12, 12), (16, 25)],
     49: [(16, 18), (22, 24)], 50: [(16, 16)]},
    {43: [(14, 16), (36, 36)], 44: [(15, 17), (23, 24), (36, 36)], 45: [(16, 18), (22, 25), (35, 35)],
     46: [(13, 25)], 47: [(11, 25), (35, 35)], 48: [(12, 24), (35, 35)], 49: [(14, 24)], 50: [(15, 23)],
     51: [(16, 21)]},
    {43: [(36, 36)], 44: [(23, 24), (36, 36)], 45: [(21, 25), (35, 35)], 46: [(20, 25)],
     47: [(19, 24), (35, 35)], 48: [(18, 24), (35, 35)], 49: [(12, 23)], 50: [(14, 23)], 51: [(13, 22)],
     52: [(16, 22)], 53: [(15, 20)]},
    {42: [(36, 36)], 43: [(36, 36)], 44: [(19, 25), (35, 35)], 45: [(17, 25)], 46: [(16, 25), (35, 35)],
     47: [(15, 25), (35, 35)], 48: [(14, 23)], 49: [(11, 22)], 50: [(13, 16), (18, 22)], 51: [(17, 21)],
     52: [(15, 20)]},
]
# While running the cape streams out level behind the shoulders.
CAPE_RUN = [
    {42: [(20, 25), (36, 36)], 43: [(15, 25), (36, 36)], 44: [(11, 25), (35, 35)], 45: [(9, 24)],
     46: [(10, 22)], 47: [(12, 18)]},
    {42: [(22, 25), (36, 36)], 43: [(16, 25), (36, 36)], 44: [(12, 25), (35, 35)], 45: [(10, 24)],
     46: [(9, 13), (16, 22)], 47: [(13, 18)]},
]


def cape(c, frame=0, run=False, dy=0, ch='v', mats=None, ring='outer'):
    spans = (CAPE_RUN[frame % 2] if run else CAPE_FRAMES[frame % 4])
    spans = {y + dy: v for y, v in spans.items()}
    rows, x0, y0 = mask_rows(spans, ch)
    draw(c, rows, x0, y0, mats=mats, ring=ring)
