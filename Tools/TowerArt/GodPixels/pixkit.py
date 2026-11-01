"""Tiny pixel canvas with material ramps, automatic rim shading and part outlines.

Every body part is authored as ASCII in the 61x75 frame of chuchuking's Zeus
(Assets/RogueLike/Character/Zeus). A character is a list of parts drawn back to
front; each part paints a one-pixel outline ring around itself first, so a part
in front is always separated from the one behind it, as in the reference art.
"""
import colorsys
from PIL import Image

W, H = 61, 75
CX = 31          # body centre column, between the boots
GROUND = 55      # first transparent row below the boots

# Body legend: char -> (material, tone). Tones: 0 outline, 1 shadow, 2 base, 3 light, 4 highlight.
BODY = {
    'a': ('hair', 0), 'b': ('hair', 1), 'c': ('hair', 2), 'd': ('hair', 3), 'e': ('hair', 4),
    'Q': ('skin', 0), 'q': ('skin', 1), 'k': ('skin', 2), 'K': ('skin', 3),
    'E': ('eye', 0), 'i': ('eye', 2), 'I': ('eye', 3), 'W': ('white', 2), 'M': ('mouth', 2), 'B': ('blush', 2),
    '0': ('cloth', 0), '1': ('cloth', 1), '2': ('cloth', 2), '3': ('cloth', 3), '4': ('cloth', 4),
    '5': ('trim', 0), '6': ('trim', 1), '7': ('trim', 2), '8': ('trim', 3), '9': ('trim', 4),
    'm': ('metal', 0), 'n': ('metal', 1), 'o': ('metal', 2), 'p': ('metal', 3), 'P': ('metal', 4),
    'T': ('cape', 0), 't': ('cape', 1), 'v': ('cape', 2), 'V': ('cape', 3),
    'F': ('boot', 0), 'f': ('boot', 1), 'g': ('boot', 2), 'G': ('boot', 3),
    'z': ('gem', 1), 'j': ('gem', 2), 'J': ('gem', 3),
    'X': ('wing', 0), 'x': ('wing', 1), 'y': ('wing', 2), 'Y': ('wing', 3),
    'L': ('fur', 0), 'l': ('fur', 1), 'h': ('fur', 2), 'H': ('fur', 3),
    'R': ('cloth2', 0), 'r': ('cloth2', 1), 's': ('cloth2', 2), 'S': ('cloth2', 3),
    '#': ('ink', 0), '*': ('glow', 2), '+': ('glow', 3), '%': ('glow', 4),
}

# Weapon legend: blades, gold fittings, wooden shafts, ribbons, gems and glow.
GEAR = {
    'm': ('blade', 0), 'n': ('blade', 1), 'o': ('blade', 2), 'p': ('blade', 3), 'P': ('blade', 4),
    'G': ('gold', 0), 'g': ('gold', 1), 'h': ('gold', 2), 'H': ('gold', 3), 'y': ('gold', 4),
    '0': ('wood', 0), '1': ('wood', 1), '2': ('wood', 2), '3': ('wood', 3),
    '5': ('ribbon', 0), '6': ('ribbon', 1), '7': ('ribbon', 2), '8': ('ribbon', 3),
    'z': ('gem', 1), 'j': ('gem', 2), 'J': ('gem', 3), 'Z': ('gem', 0),
    '*': ('glow', 2), '+': ('glow', 3), '%': ('glow', 4), '~': ('glow', 1),
    '#': ('ink', 0), 's': ('string', 2), 'w': ('white', 2), 'W': ('white', 3),
    'k': ('dark', 1), 'K': ('dark', 2), 'D': ('dark', 3), 'q': ('dark', 0),
    'f': ('leaf', 1), 'F': ('leaf', 2), 'e': ('leaf', 3),
}

# Materials whose base tone is lit from the upper right and shadowed lower left.
FLAT = {'eye', 'white', 'mouth', 'blush', 'ink', 'glow', 'string'}
NO_RING = {'eye', 'white', 'mouth', 'blush', 'glow', 'string'}


def rgb(value):
    if isinstance(value, tuple):
        return value
    value = value.lstrip('#')
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def _toward(h, target, amount):
    d = (target - h + 540) % 360 - 180
    step = max(-amount, min(amount, d))
    return (h + step) % 360


def shade(color, hue_to=None, hue_by=0, s=1.0, v=1.0):
    r, g, b = [c / 255 for c in rgb(color)]
    h, sat, val = colorsys.rgb_to_hsv(r, g, b)
    h *= 360
    if hue_to is not None and sat > .08:
        h = _toward(h, hue_to, hue_by)
    sat = min(1, max(0, sat * s))
    val = min(1, max(0, val * v))
    r, g, b = colorsys.hsv_to_rgb(h / 360, sat, val)
    return (round(r * 255), round(g * 255), round(b * 255))


def mix(a, b, t):
    a, b = rgb(a), rgb(b)
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def ramp(base, outline=None, dark=.72, light=1.14):
    """Outline, shadow, base, light and highlight. Shadows lean violet, lights lean yellow."""
    base = rgb(base)
    tones = [
        shade(base, 255, 22, 1.25, .40),
        shade(base, 255, 12, 1.12, dark),
        base,
        shade(base, 60, 8, .86, light),
        None,
    ]
    tones[3] = mix(tones[3], (255, 255, 255), .12) if max(base) > 230 else tones[3]
    tones[4] = mix(tones[3], (255, 255, 255), .45)
    if outline:
        tones[0] = rgb(outline)
    return tones


def hair_ramp(base):
    """Hair needs wider steps than cloth so its clump lines read like Zeus's (76/179/240/255).

    Very dark hair is lifted and given a cool sheen; otherwise black hair loses every strand.
    """
    base = rgb(base)
    value = max(base) / 255
    if value < .38:
        lifted = shade(base, s=1.0, v=.25 / max(value, .05))
        return [shade(lifted, 255, 20, 1.2, .30), shade(lifted, 255, 12, 1.1, .58), lifted,
                shade(lifted, 225, 25, .8, 1.75), shade(lifted, 225, 25, .7, 2.4)]
    tones = ramp(base, dark=.68, light=1.16)
    tones[0] = shade(base, 255, 22, 1.25, .34)
    return tones


def fixed(*colors):
    tones = [rgb(c) for c in colors]
    while len(tones) < 5:
        tones.append(tones[-1])
    return tones


class Canvas:
    def __init__(self, w=W, h=H):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[y][x]
        return None

    def put(self, x, y, cell):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = cell

    def copy(self):
        c = Canvas(self.w, self.h)
        c.px = [row[:] for row in self.px]
        return c

    def bbox(self):
        xs = [x for y in range(self.h) for x in range(self.w) if self.px[y][x]]
        ys = [y for y in range(self.h) for x in range(self.w) if self.px[y][x]]
        return (min(xs), min(ys), max(xs), max(ys)) if xs else None

    def render(self, palette):
        img = Image.new('RGBA', (self.w, self.h), (0, 0, 0, 0))
        pix = img.load()
        for y in range(self.h):
            for x in range(self.w):
                cell = self.px[y][x]
                if cell:
                    mat, tone = cell
                    if mat not in palette:
                        raise KeyError('palette has no material ' + mat)
                    pix[x, y] = palette[mat][tone] + (255,)
        return img


def cells_of(rows, x0, y0, legend=BODY, mats=None, flip=False):
    cells = {}
    width = max(len(r) for r in rows) if rows else 0
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch in ' .':
                continue
            if ch not in legend:
                raise KeyError('unknown pixel %r' % ch)
            mat, tone = legend[ch]
            if mats:
                mat = mats.get(mat, mat)
            x = x0 + (width - 1 - i if flip else i)
            cells[(x, y0 + j)] = (mat, tone)
    return cells


def autoshade(cells, flat=FLAT):
    out = {}
    for (x, y), (mat, tone) in cells.items():
        if tone == 2 and mat not in flat:
            def outside(px, py):
                return (px, py) not in cells
            if outside(x, y - 1):
                tone = 3
            elif outside(x, y + 1):
                tone = 1
            elif outside(x + 1, y):
                tone = 3
            elif outside(x - 1, y):
                tone = 1
        out[(x, y)] = (mat, tone)
    return out


def paint(canvas, cells, ring='all', shade_rim=True, ring_mat=None):
    """Draws cells over the canvas. ring='all' outlines over earlier parts, 'outer' only over empty pixels."""
    if shade_rim:
        cells = autoshade(cells)
    if ring:
        for (x, y), (mat, tone) in cells.items():
            if mat in NO_RING:
                continue
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if (nx, ny) in cells:
                    continue
                if ring == 'outer' and canvas.get(nx, ny) is not None:
                    continue
                canvas.put(nx, ny, (ring_mat or mat, 0))
    for (x, y), cell in cells.items():
        canvas.put(x, y, cell)
    return canvas


def draw(canvas, rows, x0, y0, legend=BODY, mats=None, ring='all', shade_rim=True, flip=False, ring_mat=None):
    return paint(canvas, cells_of(rows, x0, y0, legend, mats, flip), ring, shade_rim, ring_mat)


def mask_rows(spans, ch='c'):
    """Builds ASCII rows from {row: [(x0, x1), ...]} spans. Returns (rows, x0, y0)."""
    ys = sorted(spans)
    xs = [x for y in ys for a, b in spans[y] for x in (a, b)]
    left, top = min(xs), ys[0]
    rows = []
    for y in range(top, ys[-1] + 1):
        line = [' '] * (max(xs) - left + 1)
        for a, b in spans.get(y, []):
            for x in range(a, b + 1):
                line[x - left] = ch
        rows.append(''.join(line))
    return rows, left, top


def overlay(base, top):
    """Copies non-empty pixels of top over base."""
    for y in range(base.h):
        for x in range(base.w):
            if top.px[y][x]:
                base.px[y][x] = top.px[y][x]
    return base


def shifted(canvas, dx, dy):
    out = Canvas(canvas.w, canvas.h)
    for y in range(canvas.h):
        for x in range(canvas.w):
            if canvas.px[y][x]:
                out.put(x + dx, y + dy, canvas.px[y][x])
    return out


def image_cells(img, colors):
    """Turns an existing RGBA image into cells using an exact color->(material,tone) map."""
    canvas = Canvas(img.width, img.height)
    pix = img.convert('RGBA').load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = pix[x, y]
            if a:
                canvas.px[y][x] = colors[(r, g, b)]
    return canvas
