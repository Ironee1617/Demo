"""Draws the hairstyles in hairstyles.py. Their outlines are drawn by hand, so no ring is added."""
from pixkit import draw
import hairstyles as H

BACK_OF = {'long': 'long', 'ponytail': 'ponytail', 'braid': 'braid'}
FRONT_OF = {'braid': 'long', 'buns': 'bob', 'topknot': 'short', 'mane': 'spiky'}


def check():
    """Every row is 28 wide and the eye rows of the face stay clear."""
    for name, (y0, rows) in H.FRONT.items():
        for j, row in enumerate(rows):
            if len(row) != 28:
                raise ValueError('%s row %d is %d wide' % (name, y0 + j, len(row)))
            if 33 <= y0 + j <= 38 and any(ch != '.' for ch in row[13:23]):
                raise ValueError('%s covers the eyes on row %d' % (name, y0 + j))


def back(c, name, mats=None, dy=0):
    key = BACK_OF.get(name)
    if key:
        y0, rows = H.BACK[key]
        draw(c, rows, H.BACK_X[key], y0 + dy, mats=mats, ring=None)


def front(c, name, mats=None, dy=0):
    y0, rows = H.FRONT[FRONT_OF.get(name, name)]
    draw(c, rows, H.X0, y0 + dy, mats=mats, ring=None)
    if name == 'topknot':
        draw(c, KNOT, 27, 16 + dy, mats=mats, ring=None)


# A tied bun on the crown (Shiva's jata, the Korean sangtu), standing three rows
# above the crown like an ornament.
KNOT = [
    "..aaaa..",
    ".acdcca.",
    ".abccba.",
    "..a77a..",
]


check()
