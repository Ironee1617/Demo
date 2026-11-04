"""Reads chuchuking's Spine 4.2 characters (Assets/RogueLike/Character/Spine) and poses them as pixel cells.

The skeletons are flat: every bone hangs from the root, which stands on the ground between the boots.
A 61x75 body frame swaps on one slot while legs, the free arm, Odin's cape and ravens turn on bones.
Only region attachments and bone rotate/translate/scale timelines are read; anything else in an export
(meshes, constraints, skins, deforms) raises, so a richer re-export fails loudly instead of baking wrong.
"""
import math
import os
import struct

from PIL import Image

INHERIT = ['normal', 'onlyTranslation', 'noRotationOrReflection', 'noScale', 'noScaleOrReflection']
BONE_TIMELINES = {0: ('rotate', 1), 1: ('translate', 2), 2: ('translatex', 1), 3: ('translatey', 1),
                  4: ('scale', 2), 5: ('scalex', 1), 6: ('scaley', 1), 7: ('shear', 2), 8: ('shearx', 1), 9: ('sheary', 1)}


class _Input:
    def __init__(self, data):
        self.b, self.p, self.strings = data, 0, []

    def byte(self):
        v = self.b[self.p]
        self.p += 1
        return v

    def int32(self):
        v = struct.unpack_from('>i', self.b, self.p)[0]
        self.p += 4
        return v

    def varint(self, positive=True):
        result, shift = 0, 0
        for _ in range(5):
            c = self.byte()
            result |= (c & 0x7F) << shift
            shift += 7
            if not c & 0x80:
                break
        return result if positive else (result >> 1) ^ -(result & 1)

    def signed(self):
        """Draw order offsets are written as plain varints of a signed int."""
        v = self.varint()
        return v - (1 << 32) if v >= 1 << 31 else v

    def float(self):
        v = struct.unpack_from('>f', self.b, self.p)[0]
        self.p += 4
        return v

    def string(self):
        n = self.varint()
        if n == 0:
            return None
        s = self.b[self.p:self.p + n - 1].decode('utf-8')
        self.p += n - 1
        return s

    def ref(self):
        i = self.varint()
        return None if i == 0 else self.strings[i - 1]


def _curve_frames(d, count, values, read):
    frames = []
    t, v = d.float(), [read() for _ in range(values)]
    for f in range(count):
        frame = {'time': t, 'value': v}
        frames.append(frame)
        if f == count - 1:
            break
        t2, v2 = d.float(), [read() for _ in range(values)]
        curve = d.byte()
        if curve == 1:
            frame['curve'] = 'stepped'
        elif curve == 2:
            frame['curve'] = [[d.float() for _ in range(4)] for _ in range(values)]
        t, v = t2, v2
    return frames


def read_skeleton(path):
    d = _Input(open(path, 'rb').read())
    d.p = 8                                   # hash
    version = d.string()
    if not version or not version.startswith('4.2.'):
        raise ValueError('%s: Spine %s, expected 4.2' % (path, version))
    for _ in range(5):                        # x, y, width, height, reference scale
        d.float()
    nonessential = d.byte() != 0
    if nonessential:
        d.float(), d.string(), d.string()
    d.strings = [d.string() for _ in range(d.varint())]
    bones = []
    for i in range(d.varint()):
        bone = {'name': d.string()}
        bone['parent'] = None if i == 0 else bones[d.varint()]['name']
        for key in ('rotation', 'x', 'y', 'scaleX', 'scaleY', 'shearX', 'shearY', 'length'):
            bone[key] = d.float()
        bone['inherit'] = INHERIT[d.varint()]
        d.byte()                              # skin required
        if nonessential:
            d.int32(), d.string(), d.byte()
        if bone['parent'] not in (None, 'root') or bone['inherit'] != 'normal':
            raise ValueError('%s: bone %s is not a plain child of the root' % (path, bone['name']))
        bones.append(bone)
    slots = []
    for _ in range(d.varint()):
        slot = {'name': d.string(), 'bone': bones[d.varint()]['name']}
        d.int32(), d.int32()                  # colour, dark colour
        slot['attachment'] = d.ref()
        d.varint()                            # blend mode
        if nonessential:
            d.byte()
        slots.append(slot)
    for kind in ('ik', 'transform', 'path', 'physics'):
        if d.varint():
            raise ValueError('%s: %s constraints are not supported' % (path, kind))
    skin = {}
    for _ in range(d.varint()):               # default skin slots
        slot = slots[d.varint()]['name']
        for _ in range(d.varint()):
            key, flags = d.ref(), d.byte()
            name = d.ref() if flags & 8 else key
            if flags & 7:
                raise ValueError('%s: attachment %s is not a region' % (path, name))
            region = {'path': d.ref() if flags & 16 else name}
            if flags & 32:
                d.int32()
            if flags & 64:
                raise ValueError('%s: sequence attachments are not supported' % path)
            region['rotation'] = d.float() if flags & 128 else 0.0
            for k in ('x', 'y', 'scaleX', 'scaleY', 'width', 'height'):
                region[k] = d.float()
            skin.setdefault(slot, {})[key] = region
    if d.varint():
        raise ValueError('%s: named skins are not supported' % path)
    events = []
    for _ in range(d.varint()):
        name, _, _, _, audio = d.string(), d.varint(False), d.float(), d.string(), d.string()
        if audio is not None:
            d.float(), d.float()
        events.append((name, audio))
    animations = {}
    for _ in range(d.varint()):
        name = d.string()
        animations[name] = _read_animation(d, path, bones, slots, events)
    if d.p != len(d.b):
        raise ValueError('%s: %d bytes left unread' % (path, len(d.b) - d.p))
    return {'bones': bones, 'slots': slots, 'skin': skin, 'animations': animations}


def _read_animation(d, path, bones, slots, events):
    anim = {'slots': {}, 'bones': {}, 'drawOrder': []}
    d.varint()                                # timeline count
    for _ in range(d.varint()):
        slot = slots[d.varint()]['name']
        for _ in range(d.varint()):
            kind, count = d.byte(), d.varint()
            if kind != 0:
                raise ValueError('%s: slot colour timelines are not supported' % path)
            anim['slots'][slot] = [(d.float(), d.ref()) for _ in range(count)]
    for _ in range(d.varint()):
        bone = bones[d.varint()]['name']
        for _ in range(d.varint()):
            kind, count = d.byte(), d.varint()
            if kind not in BONE_TIMELINES:
                raise ValueError('%s: bone timeline %d is not supported' % (path, kind))
            d.varint()                        # bezier count
            name, values = BONE_TIMELINES[kind]
            anim['bones'].setdefault(bone, {})[name] = _curve_frames(d, count, values, d.float)
    for kind in ('ik', 'transform', 'path', 'physics', 'attachment'):
        if d.varint():
            raise ValueError('%s: %s timelines are not supported' % (path, kind))
    for _ in range(d.varint()):
        t = d.float()
        anim['drawOrder'].append((t, [(slots[d.varint()]['name'], d.signed()) for _ in range(d.varint())]))
    for _ in range(d.varint()):
        d.float()
        name, audio = events[d.varint()]
        d.varint(False), d.float()
        if d.byte():
            d.string()
        if audio is not None:
            d.float(), d.float()
    return anim


def read_atlas(path):
    """Region images of a one-page atlas, unrotated."""
    lines = [l.strip() for l in open(path, encoding='utf-8').read().splitlines() if l.strip()]
    page = Image.open(os.path.join(os.path.dirname(path), lines[0])).convert('RGBA')
    regions, current = {}, None
    for line in lines[1:]:
        if ':' not in line:
            current = regions[line] = {'rotate': 0}
        elif current is not None:
            key, value = (s.strip() for s in line.split(':', 1))
            if key == 'bounds':
                current['bounds'] = [int(v) for v in value.split(',')]
            elif key == 'rotate':
                current['rotate'] = 90 if value in ('90', 'true') else int(value)
    images = {}
    for name, r in regions.items():
        x, y, w, h = r['bounds']
        if r['rotate'] == 90:
            # Packed turned a quarter counter-clockwise; Odin's arm0 matches Weapons/Odin_arm turned back clockwise.
            images[name] = page.crop((x, y, x + h, y + w)).transpose(Image.ROTATE_270)
        elif r['rotate'] == 0:
            images[name] = page.crop((x, y, x + w, y + h))
        else:
            raise ValueError('%s: region %s rotated %d' % (path, name, r['rotate']))
    return images


def _bezier(c, t0, t1, v0, v1, time):
    cx1, cy1, cx2, cy2 = c
    lo, hi = 0.0, 1.0
    for _ in range(40):
        s = (lo + hi) / 2
        x = (1 - s) ** 3 * t0 + 3 * (1 - s) ** 2 * s * cx1 + 3 * (1 - s) * s * s * cx2 + s ** 3 * t1
        lo, hi = (s, hi) if x < time else (lo, s)
    s = (lo + hi) / 2
    return (1 - s) ** 3 * v0 + 3 * (1 - s) ** 2 * s * cy1 + 3 * (1 - s) * s * s * cy2 + s ** 3 * v1


def _sample(frames, time):
    if time < frames[0]['time']:
        return None
    i = 0
    while i + 1 < len(frames) and frames[i + 1]['time'] <= time:
        i += 1
    f = frames[i]
    if i == len(frames) - 1 or f.get('curve') == 'stepped':
        return list(f['value'])
    n = frames[i + 1]
    if isinstance(f.get('curve'), list):
        return [_bezier(f['curve'][k], f['time'], n['time'], f['value'][k], n['value'][k], time) for k in range(len(f['value']))]
    a = (time - f['time']) / (n['time'] - f['time'])
    return [v + (w - v) * a for v, w in zip(f['value'], n['value'])]


def _draw_order(slots, keys, time):
    names = [s['name'] for s in slots]
    offsets = None
    for t, offs in keys:
        if t <= time:
            offsets = offs
    if not offsets:
        return names
    order, unchanged, original = [-1] * len(names), [], 0
    for slot, offset in offsets:
        index = names.index(slot)
        while original != index:
            unchanged.append(original)
            original += 1
        order[original + offset] = original
        original += 1
    unchanged += range(original, len(names))
    for i in range(len(names) - 1, -1, -1):
        if order[i] == -1:
            order[i] = unchanged.pop()
    return [names[i] for i in order]


class Character:
    """One of chuchuking's Spine characters, e.g. Character(SPINE_DIR, 'Odin')."""

    def __init__(self, spine_dir, name):
        folder = os.path.join(spine_dir, name)
        self.name = name
        self.skeleton = read_skeleton(os.path.join(folder, name + '.skel.bytes'))
        self.images = read_atlas(os.path.join(folder, name + '.atlas.txt'))
        self.duration = {k: max([fr[-1]['time'] for tl in a['bones'].values() for fr in tl.values()] +
                                [tl[-1][0] for tl in a['slots'].values()]) for k, a in self.skeleton['animations'].items()}

    def pose(self, animation, time):
        """Bone transforms, slot attachments and draw order at `time` seconds into `animation`."""
        anim = self.skeleton['animations'][animation]
        bones = {}
        for b in self.skeleton['bones']:
            rot, x, y, sx, sy = b['rotation'], b['x'], b['y'], b['scaleX'], b['scaleY']
            tl = anim['bones'].get(b['name'], {})
            v = _sample(tl['rotate'], time) if 'rotate' in tl else None
            if v:
                rot += v[0]
            v = _sample(tl['translate'], time) if 'translate' in tl else None
            if v:
                x, y = x + v[0], y + v[1]
            v = _sample(tl['scale'], time) if 'scale' in tl else None
            if v:
                sx, sy = sx * v[0], sy * v[1]
            bones[b['name']] = (x, y, rot, sx, sy)
        attachments = {}
        for s in self.skeleton['slots']:
            name = s['attachment']
            for t, key in anim['slots'].get(s['name'], []):
                if t <= time:
                    name = key
            attachments[s['name']] = name
        return bones, attachments, _draw_order(self.skeleton['slots'], anim['drawOrder'], time)

    def bone_point(self, animation, time, bone, x=0.0, y=0.0):
        """A point in a bone's space, in Spine units (y up, root on the ground)."""
        bx, by, rot, sx, sy = self.pose(animation, time)[0][bone]
        r = math.radians(rot)
        return bx + x * sx * math.cos(r) - y * sy * math.sin(r), by + x * sx * math.sin(r) + y * sy * math.cos(r)

    def render(self, animation, time, size, root, skip=(), only=None):
        """Draws the pose into a `size` image with the Spine root on boundary coordinates `root` (x right, y down)."""
        out = Image.new('RGBA', size)
        bones, attachments, order = self.pose(animation, time)
        slot_bone = {s['name']: s['bone'] for s in self.skeleton['slots']}
        for slot in order:
            name = attachments[slot]
            if name is None or slot in skip or (only is not None and slot not in only):
                continue
            region = self.skeleton['skin'][slot][name]
            bx, by, rot, sx, sy = bones[slot_bone[slot]]
            r = math.radians(rot)
            ax, ay = region['x'] * sx, region['y'] * sy
            cx, cy = bx + ax * math.cos(r) - ay * math.sin(r), by + ax * math.sin(r) + ay * math.cos(r)
            angle = rot + region['rotation']
            image = self.images[region['path']]
            turned = round(angle, 3) % 360
            if turned:
                # Spine and PIL both turn counter-clockwise on screen for a positive angle.
                image = image.rotate(angle, resample=Image.NEAREST, expand=True)
            out.alpha_composite(image, (int(math.floor(root[0] + cx - image.width / 2 + .5)),
                                        int(math.floor(root[1] - cy - image.height / 2 + .5))))
        return out
