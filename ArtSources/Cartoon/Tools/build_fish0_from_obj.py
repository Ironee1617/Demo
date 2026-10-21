"""Turn D:\\수족관술래잡기\\fish0\\Untitled.obj into a game-ready Fish0.cartoon.

The new skin inherits every blend shape of the approved Fish0 rig (swim, fins,
mouth/swallow, matched-transformation steps) by transferring the per-vertex
deltas from the old surface. Old hidden internal parts (fangs, tongue, matched
midpoint core) are kept verbatim so feeding and transformation still work.
"""
import json, struct, sys
import numpy as np
from PIL import Image
from scipy.spatial import cKDTree
sys.path.insert(0, str(__import__('pathlib').Path(__file__).resolve().parent))
from load_obj import load_obj

Image.MAX_IMAGE_PIXELS = None
OBJ = r'D:/수족관술래잡기/fish0/Untitled.obj'
TEX = r'D:/수족관술래잡기/cartoon fish 3d model (1)/cartoon+fish+3d+model_basecolor.jpg'
OLD = str(__import__('pathlib').Path(__file__).resolve().parent.parent / 'Source/BeforeFish0Tripo_20261019_1741') + '/'
OUT = str(__import__('pathlib').Path(__file__).resolve().parent.parent / 'Export') + '/'
import os; os.makedirs(OUT, exist_ok=True)

SCALE = 1.25
SHIFT = np.array([0.0, -0.512, -0.1025])

# ---------------------------------------------------------------- old rig
d = json.load(open(OLD + 'Fish0.json'))
with open(OLD + 'Fish0.cartoon', 'rb') as f:
    nv, ni, ns, nm = struct.unpack('<4i', f.read(16))
    ov = np.frombuffer(f.read(nv * 72), '<f4').reshape(nv, 18).copy()
    otri = np.frombuffer(f.read(ni * 4), '<i4').reshape(-1, 3).copy()
    ogroups = np.frombuffer(f.read(ni // 3 * 4), '<i4').copy()
    os_ = np.frombuffer(f.read(), '<f4').reshape(ns, nv, 6).copy()
names = d['shapeNames']
omat = (ov[:, 6] * 4).astype(int) + 4 * (ov[:, 7] * 4).astype(int)
SKIN = np.isin(omat, [0, 1, 2, 4, 5])        # outer surface materials
KEEP = np.isin(omat, [3, 6, 7])              # hidden internal parts kept as-is

# ---------------------------------------------------------------- new mesh
V, VT, F = load_obj(OBJ)
key = {}
verts, uvs, tris = [], [], []
for face in F:
    idx = []
    for vi, ti in face:
        k = (vi, ti)
        if k not in key:
            key[k] = len(verts)
            verts.append(vi)
            uvs.append(VT[ti] if ti >= 0 else (0.0, 0.0))
        idx.append(key[k])
    for j in range(1, len(idx) - 1):
        tris.append((idx[0], idx[j], idx[j + 1]))
src_obj = np.array(verts)
# Tripo meshes split vertices; weld by position so normals and deltas stay continuous
_, pos_id = np.unique(np.round(V, 5), axis=0, return_inverse=True)
pos_id = pos_id.ravel()
src = pos_id[src_obj]
P = V[src_obj] * SCALE + SHIFT
UV = np.array(uvs, float)
T = np.array(tris, np.int64)
n_new = len(P)

# weld by original position index for smooth normals across UV seams
def normals(points):
    acc = np.zeros((src.max() + 1, 3))
    fn = np.cross(points[T[:, 1]] - points[T[:, 0]], points[T[:, 2]] - points[T[:, 0]])
    for j in range(3):
        np.add.at(acc, src[T[:, j]], fn)
    acc /= np.maximum(np.linalg.norm(acc, axis=1, keepdims=True), 1e-12)
    return acc[src]

N = normals(P)
c = P.mean(0)
print('new verts', n_new, 'tris', len(T), 'outward normals', np.mean(((P - c) * N).sum(1) > 0).round(3))

# ---------------------------------------------------------------- transfer
op = ov[:, :3]
skin_idx = np.where(SKIN)[0]
tree = cKDTree(op[skin_idx])
# small local warp so the snout lines up with the old mouth for the mouth shapes
old_snout = op[omat == 4].mean(0)
front = P[:, 2] > P[:, 2].max() - 0.11
new_snout = P[front].mean(0)
w_snout = np.exp(-np.sum((P - new_snout) ** 2, 1) / (2 * 0.18 ** 2))[:, None]
Q = P + w_snout * (old_snout - new_snout)
K = 10
dist, nn = tree.query(Q, k=K)
gi = skin_idx[nn]
on = ov[gi, 3:6]
align = np.clip(np.einsum('nkj,nj->nk', on, N), 0, 1) ** 2 + 1e-3
sigma = dist[:, :1] * 1.5 + 0.004
w = np.exp(-(dist / sigma) ** 2) * align
w /= w.sum(1, keepdims=True)

new_shapes = np.zeros((ns, n_new, 6), np.float32)
for i in range(ns):
    new_shapes[i, :, :3] = np.einsum('nk,nkj->nj', w, os_[i, gi, :3])
uv2 = np.einsum('nk,nkj->nj', w, ov[gi, 8:10])

# smooth the transferred deltas a little over the new surface (removes k-NN noise)
nbr = [[] for _ in range(n_new)]
for a, b, cc in T:
    nbr[a] += [b, cc]; nbr[b] += [a, cc]; nbr[cc] += [a, b]
# weld neighbours across seams via original position index
by_pos = {}
for i, s in enumerate(src):
    by_pos.setdefault(s, []).append(i)
rows, cols = [], []
for i in range(n_new):
    ns_ = set(nbr[i])
    for j in by_pos[src[i]]:
        ns_.update(nbr[j])
    for j in ns_:
        rows.append(i); cols.append(j)
from scipy.sparse import csr_matrix
A = csr_matrix((np.ones(len(rows)), (rows, cols)), shape=(n_new, n_new))
deg = np.asarray(A.sum(1)).ravel()
def smooth(x, it=2):
    for _ in range(it):
        x = 0.5 * x + 0.5 * (A @ x) / np.maximum(deg, 1)[:, None]
    return x
# Mouth opening: procedural, fold-free radial opening of the new snout.
# r' = g(r) opens the lip hole far more than the outer lip, and the dimple
# interior is pushed back into a cavity. Magnitudes follow the old maw.
mo = names.index('Alien_Mouth_Open')
nc = np.array([new_snout[0], new_snout[1]])
rel = P[:, :2] - nc
nr = np.linalg.norm(rel, axis=1)
gr = np.interp(nr, [0, .03, .10, .20, .30, .46], [0, .275, .34, .38, .405, .47])   # greedy maw (R18; R17 was .225/.29/.335/.37/.46)
zf = P[:, 2].max()
zz = np.clip((P[:, 2] - .35) / (.50 - .35), 0, 1)
wz = zz * zz * (3 - 2 * zz)
r_new = nr + wz * (gr - nr)
# deeper parts of the lip bowl stay narrower, so the opened maw is a funnel that
# is seen from the front (never a vertical or overhanging wall)
bowl = np.clip((.07 - nr) / (.07 - .035), 0, 1)
depth_frac = np.clip((zf - P[:, 2]) / .047, 0, 1)
r_new *= 1 - .55 * depth_frac * bowl * bowl * (3 - 2 * bowl)
scale = np.where(nr > 1e-6, r_new / np.maximum(nr, 1e-6), 1.0)
hh = np.clip((.06 - nr) / (.06 - .035), 0, 1)
dd = np.clip(((zf - P[:, 2]) - .006) / (.035 - .006), 0, 1)
inner = hh * hh * (3 - 2 * hh) * dd * dd * (3 - 2 * dd)
mouth_delta = np.zeros_like(P)
mouth_delta[:, :2] = rel * (scale - 1)[:, None]
mouth_delta[:, 2] = -.12 * inner * wz   # R18: shallower funnel keeps the huge maw fold-free (R17 -.16)
eye_c = np.array([[0.218, -0.04, 0.27], [-0.218, -0.04, 0.27]])
ed = np.min(np.linalg.norm(P[:, None, :] - eye_c[None], axis=2), axis=1)
ee = np.clip((ed - .13) / (.24 - .13), 0, 1)
mouth_delta *= (ee * ee * (3 - 2 * ee))[:, None]
new_shapes[mo, :, :3] = mouth_delta
# stronger visible gulp while the meal goes down
sw = names.index('Swallow')
wa = np.zeros((n_new, 1))

for i in range(ns):
    if i == mo:
        new_shapes[i, :, :3] = smooth(new_shapes[i, :, :3], 2)
    else:
        new_shapes[i, :, :3] = smooth(new_shapes[i, :, :3], 4)
new_shapes[sw, :, :3] *= 2.0

for i in range(ns):
    if np.abs(new_shapes[i, :, :3]).max() > 0:
        new_shapes[i, :, 3:6] = normals(P + new_shapes[i, :, :3]) - N

# ---------------------------------------------------------------- assemble
nv_rows = np.zeros((n_new, 18), np.float32)
nv_rows[:, 0:3] = P
nv_rows[:, 3:6] = N
nv_rows[:, 6] = UV[:, 0]
nv_rows[:, 7] = 0.5 + 0.5 * UV[:, 1]          # new texture lives in the top half of the atlas
nv_rows[:, 8:10] = uv2
nv_rows[:, 10] = 1.0                          # weight0
nv_rows[:, 14] = 1.0                          # bone index 0 -> Body

keep_idx = np.where(KEEP)[0]
remap = -np.ones(nv, np.int64)
remap[keep_idx] = np.arange(len(keep_idx)) + n_new
keep_tris = otri[np.all(KEEP[otri], axis=1)]
kept = ov[keep_idx].copy()
head_part = np.isin(omat[keep_idx], [3, 6])          # fangs and tongue follow the new mouth
kept[head_part, 0:3] += (new_snout - old_snout) * np.array([1, 1, 0])
all_v = np.concatenate([nv_rows, kept])
all_t = np.concatenate([T, remap[keep_tris]])
all_s = np.concatenate([new_shapes, os_[:, keep_idx]], axis=1)
groups = np.zeros(len(all_t), np.int32)
print('final verts', len(all_v), 'tris', len(all_t), 'kept internal verts', len(keep_idx))

with open(OUT + 'Fish0.cartoon', 'wb') as f:
    f.write(struct.pack('<4i', len(all_v), all_t.size, ns, 1))
    f.write(all_v.astype('<f4').tobytes())
    f.write(all_t.astype('<i4').tobytes())
    f.write(groups.astype('<i4').tobytes())
    f.write(all_s.astype('<f4').tobytes())

# ---------------------------------------------------------------- json + anchors
mouth_y = float(new_snout[1])
front_z = float(P[:, 2].max())
d['vertices'] = int(len(all_v))
d['triangles'] = int(len(all_t))
d['parts'] = ['Fish0_Tripo_Skin'] + [p for p in d['parts'] if p.startswith(('Fish_Fang', 'Fish_Tongue', 'Fish_Matched'))]
d['mouthAnchor'] = [0.0, round(mouth_y, 4), round(front_z - 0.012, 4)]   # lip plane: prey fills the opening here
d['throatAnchor'] = [0.0, round(mouth_y, 4), round(front_z - 0.012 - 0.1526, 4)]
json.dump(d, open(OUT + 'Fish0.json', 'w'), separators=(',', ':'))

# ---------------------------------------------------------------- atlas
old_atlas = Image.open(OLD + 'Fish0_Atlas.png').convert('RGB')
W, H = old_atlas.size
atlas = old_atlas.copy()
tex = Image.open(TEX).convert('RGB').resize((W, H // 2), Image.LANCZOS)
atlas.paste(tex, (0, 0))
atlas.save(OUT + 'Fish0_Atlas.png', optimize=True)
pass
print('anchors', d['mouthAnchor'], d['throatAnchor'])
for i, nme in enumerate(names):
    print(f'{nme:18s} old max {np.linalg.norm(os_[i,:,:3],axis=1).max():.3f}  new max {np.linalg.norm(new_shapes[i,:,:3],axis=1).max():.3f}')
