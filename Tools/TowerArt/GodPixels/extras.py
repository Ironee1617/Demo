"""Wings, tails, quivers, sashes, wheels and the floating fists."""
from pixkit import draw, BODY

WING_BACK = (3, 24, [
    "..............Yy....",
    "............YYyy....",
    "..........YYyyyy....",
    "........YYyyyyyx....",
    "......YYyyyyyyxy....",
    ".....Yyyyyyyyxyy....",
    "....Yyyyyyyxxyyy....",
    "...Yyyyyyxxyyyyy....",
    "..Yyyyyxxyyyyyyx....",
    ".Yyyyxxyyyyyyxxy....",
    "Yyyxxyyyyyyxxyyyy...",
    "yyx.yyyyyxxyyyyyy...",
    "yx..yyyyxyyyyyyyyy..",
    "x...yyyxyyyyyyxyyy..",
    "...yyyxyyyyyxxyyyyy.",
    "...yyxyyyyxxyyyyyyy.",
    "..yyx.yyyxyyyyyyyyy.",
    "..yx..yyxyyyyxyyyyyy",
    "..x..yyxyyyyxyyyyyyy",
    ".....yxyyyyxyyyyyyyy",
    ".....x.yyyxyyyyyyyyy",
    "......yyxyyyyyyyyyy.",
    "......yx.yyxyyyyyy..",
    "......x..yxyyyyyy...",
    ".........x.xyyyy....",
])
WING_FRONT = (37, 27, [
    ".....Y",
    "....yY",
    "...yyY",
    "..yyyY",
    ".yyyy.",
    "yyyx..",
    "yyx...",
    "yx....",
])


def wings(c, dy=0, mats=None):
    x, y, rows = WING_BACK
    draw(c, rows, x, y + dy, mats=mats, ring='outer')
    x, y, rows = WING_FRONT
    draw(c, rows, x, y + dy, mats=mats, ring='outer')


TAILS = {
    'monkey': (11, 31, [
        "..hhhh.....",
        ".hh..hh....",
        "hh....h....",
        "h.....hh...",
        "h......h...",
        "hh.....hh..",
        ".hh.....h..",
        "..hhh...hh.",
        "....hh...hh",
        ".....hh...h",
        "......hhhhh",
        "........hhh",
    ]),
    'tiger': (13, 38, [
        "..hh.......",
        ".hHh.......",
        "h#h........",
        "hh#........",
        "h#hh.......",
        ".hh#h......",
        "..hhh#hh...",
        "....hhh#hhh",
        "......hhhhh",
    ]),
    'rat': (12, 40, [
        "h..........",
        "h..........",
        ".h.........",
        ".h.........",
        "..h........",
        "...hh......",
        ".....hhh...",
        "........hhh",
    ]),
    'dragon': (8, 38, [
        "y..........",
        "yy.........",
        "yyy........",
        ".yyy.......",
        "..yyyy.....",
        "...yyyyy...",
        "....xyyyyyy",
        ".....xxyyyy",
        ".......xxyy",
    ]),
    'ox': (14, 40, [
        "LL.......",
        "LLl......",
        ".lh......",
        "..h......",
        "..hh.....",
        "...hh....",
        "....hhh..",
        "......hhh",
    ]),
    'rabbit': (21, 44, [
        ".hh.",
        "hHhh",
        "hhhh",
        ".hh.",
    ]),
}


def tail(c, name, dy=0, mats=None):
    x, y, rows = TAILS[name]
    draw(c, rows, x, y + dy, mats=mats, ring='outer')


QUIVER = (19, 33, [
    "w.w....",
    "ww.w...",
    ".33....",
    ".333...",
    "..333..",
    "..3333.",
    "...333.",
    "...3333",
    "....333",
    "....333",
    ".....33",
])


def quiver(c, dy=0):
    x, y, rows = QUIVER
    legend = dict(BODY)
    legend.update({'w': ('white', 2), '3': ('quiver', 2)})
    draw(c, rows, x, y + dy, legend=legend, ring='outer')


SASH = (8, 24, [
    "........sssss..............",
    "......ss.....ss............",
    ".....s.........s...........",
    "....s...........ss.........",
    "...s.............s.........",
    "...s..............s........",
    "..s...............s........",
    "..s................s.......",
    "..s................s.......",
    "...s...............s.......",
    "...s...............s.......",
    "....s..............s.......",
    ".....s.............s.......",
    "......ss..........s........",
    "........sssssssssss........",
])


def sash(c, dy=0):
    x, y, rows = SASH
    draw(c, rows, x, y + dy, mats={'cloth2': 'sash'}, ring='outer')


WHEEL = [".ooo.", "o*+*o", ".ooo."]


def wheels(c, pose_offsets):
    for (dx, dy), x in zip(pose_offsets, (27, 33)):
        draw(c, WHEEL, x - 1 + dx, 51 + dy, ring='outer')


# Nuwa's serpent body replaces the legs.
SNAKE = [
    (24, 48, [
        "..ssssssssss....",
        ".sSsSsSsSsSss...",
        ".ssssssssssssss.",
        "sSsSsSsSsSsSsSss",
        ".ssssssssssssss.",
        "..rrrrrrrrrrrr..",
    ]),
    (15, 48, [
        "......ss.",
        "....ssSs.",
        "..sssss..",
        ".sSs.....",
        "ss.......",
        "s........",
    ]),
]


def snake(c, sway=0):
    for i, (x, y, rows) in enumerate(SNAKE):
        draw(c, rows, x + (sway if i == 1 else 0), y, mats={'cloth2': 'snake'}, ring='outer')


# Floating fist: fingers on top, the weapon grip passes through rows 2..4.
FIST = [
    ".kkk.",
    "kkKKk",
    "QQQKk",
    "kkkkk",
    "QQQkk",
    ".kkk.",
    "77777",
    "68886",
    ".666.",
]
HAND_LEGEND = {'k': ('hand', 2), 'K': ('hand', 3), 'q': ('hand', 1), 'Q': ('hand', 0),
               '6': ('cuff', 1), '7': ('cuff', 2), '8': ('cuff', 3), '5': ('cuff', 0)}
