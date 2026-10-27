"""Build game-ready cartoon fish (same rig contract as Fish0) from the painted models in
ArtSources/Cartoon/NewFish/Painted/<Name>/ (OBJ + 4K BaseColor).

Per fish it writes <Out>.cartoon / <Out>.json / <Out>_Atlas.png in Fish0's exact format:
  bones Root/Body (single-bone body, everything weighted to Body)
  shapes Metamorphosis, Alien_Mouth_Open, Swallow, Swim_Left, Swim_Right,
         To_Matched_0125..1000 (shared pearl-egg midpoint with the alien), Fin_Up, Fin_Down
  hidden parts kept from the approved Fish0 rig: fangs + tongue (moved to this mouth) and the
  shared midpoint egg (verbatim), so feeding and alien<->fish morphs behave exactly like Fish0.
Body plans: round (tangs, clownfish), shark, ray (manta: wing flaps), flat (flatfish: fringe ripple).
"""
import json, struct, sys, os
import numpy as np
from PIL import Image, ImageDraw
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix, csr_matrix
from scipy.sparse.csgraph import connected_components
from scipy import ndimage
Image.MAX_IMAGE_PIXELS = None

SRC = os.environ.get('NEWFISH_SRC', '/mnt/user-data/uploads/D:--Demo--ArtSources--Cartoon/NewFish/Painted')
OLD = os.environ.get('FISH0_OLD', '/mnt/user-data/uploads/Cartoon/Source/BeforeFish0Tripo_20261019_1741/')
OUT = os.environ.get('NEWFISH_OUT', '/home/claude/newfish/export')
os.makedirs(OUT, exist_ok=True)
FRACS = [0.043, 0.156, 0.316, 0.5, 0.685, 0.873, 0.996, 1.0]       # Fish0's skin schedule into the egg

GULLET_Q = .90                                   # inside this fraction of the lip crest the throat is lined
STRIP_U, STRIP_V0, STRIP_V1 = .58, .27, .48      # gradient strip in the Fish0 atlas tongue cell (tongue samples .625,.375)
MOUTH_DEEP, MOUTH_LIP = (84, 22, 44), (192, 88, 112)
def sm(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)

# All coordinates in the painted model's own space (front +z, up +y, length 1, resting on y=0).
CONF = {
  'BlueTang':   dict(out='FishBlueTang',   obj='blue_tang_colored.obj',     tex='blue_tang_BaseColor.png',   plan='round', scale=1.25,
                     mouth=(0, .195, .497), axis=(0, 0, 1), k=.92, pect=dict(x=.26, y=(.05, .35), z=(-.07, .25)), hinge=-.08, tail=.075, belly=(.05, .30)),
  'Clownfish':  dict(out='FishClownfish',  obj='clownfish_colored.obj',     tex='clownfish_BaseColor.png',   plan='round', scale=1.25,
                     mouth=(0, .235, .497), axis=(0, 0, 1), k=.92, pect=dict(x=.27, y=(.07, .34), z=(-.06, .25)), hinge=-.08, tail=.075, belly=(.05, .30)),
  'YellowTang': dict(out='FishYellowTang', obj='yellow_tang_colored.obj',   tex='yellow_tang_BaseColor.png', plan='round', scale=1.25,
                     mouth=(0, .195, .497), axis=(0, 0, 1), k=.92, pect=dict(x=.26, y=(.05, .35), z=(-.07, .25)), hinge=-.08, tail=.075, belly=(.05, .30)),
  'Shark':      dict(out='FishShark',      obj='cartoon_shark_colored.obj', tex='Shark_BaseColor.png',       plan='shark', scale=1.40,
                     mouth=(0, .104, .432), axis=(0, -.45, 1), k=.62, pect=dict(x=.115, y=(-.01, .145), z=(.07, .28)), hinge=.18, tail=.12, belly=(.15, .25)),
  'MantaRay':   dict(out='FishMantaRay',   obj='ray_colored.obj',           tex='ray_BaseColor.png',         plan='ray',   scale=1.45,
                     mouth=(0, .122, .428), axis=(0, -.35, 1), k=.62, core=.10, wing=.075, hinge=-.04, tail=.05, belly=(.20, .22)),
  'Flatfish':   dict(out='FishFlatfish',   obj='flatfish_colored.obj',      tex='flatfish_BaseColor.png',    plan='flat',  scale=1.25,
                     mouth=(0, .193, .497), axis=(0, 0, 1), k=.70, core=(.30, .33, .05), frill=.045, hinge=-.30, tail=.06, belly=(.10, .28)),
}

def load_obj(path):
    V = []; VT = []; F = []
    for l in open(path):
        if l.startswith('v '): V.append(list(map(float, l.split()[1:4])))
        elif l.startswith('vt '): VT.append(list(map(float, l.split()[1:3])))
        elif l.startswith('f '):
            F.append([(int(w.split('/')[0]) - 1, int(w.split('/')[1]) - 1) for w in l.split()[1:]])
    return np.array(V), np.array(VT), F

def old_rig():
    d = json.load(open(OLD + 'Fish0.json'))
    with open(OLD + 'Fish0.cartoon', 'rb') as f:
        nv, ni, ns, nm = struct.unpack('<4i', f.read(16))
        ov = np.frombuffer(f.read(nv * 72), '<f4').reshape(nv, 18).copy()
        otri = np.frombuffer(f.read(ni * 4), '<i4').reshape(-1, 3).copy()
        f.read(ni // 3 * 4)
        os_ = np.frombuffer(f.read(), '<f4').reshape(ns, nv, 6).copy()
    omat = (ov[:, 6] * 4).astype(int) + 4 * (ov[:, 7] * 4).astype(int)
    return d, ov, otri, os_, omat

def rotation_from_z(a):
    a = np.asarray(a, float); a /= np.linalg.norm(a); z = np.array([0, 0, 1.0])
    v = np.cross(z, a); c = z @ a
    if np.linalg.norm(v) < 1e-9: return np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1 / (1 + c))

def dilate(img, VT, TT):
    """Fill every texel outside the UV islands with the nearest island colour (no dark seams/mips)."""
    W, H = img.size
    mask = Image.new('L', (W, H), 0); dr = ImageDraw.Draw(mask)
    for t in TT:
        pts = [(VT[i, 0] * W, (1 - VT[i, 1]) * H) for i in t]
        dr.polygon(pts, fill=255, outline=255)
    m = np.asarray(mask) > 0
    m = ndimage.binary_dilation(m, iterations=2)          # cover the rasterised rim exactly
    a = np.asarray(img).copy()
    _, (iy, ix) = ndimage.distance_transform_edt(~m, return_indices=True)
    return Image.fromarray(a[iy, ix]), m

def build(name, write=True, report=True):
    c = CONF[name]; plan = c['plan']; S = c['scale']
    d0, ov, otri, os_, omat = old_rig(); names = d0['shapeNames']; ns = len(names)
    V, VT, F = load_obj(os.path.join(SRC, name, c['obj']))
    key = {}; verts = []; uvs = []; tris = []
    for face in F:
        idx = []
        for vi, ti in face:
            k = (vi, ti)
            if k not in key: key[k] = len(verts); verts.append(vi); uvs.append(VT[ti])
            idx.append(key[k])
        for j in range(1, len(idx) - 1): tris.append((idx[0], idx[j], idx[j + 1]))
    src = np.array(verts); UV = np.array(uvs, float); T = np.array(tris, np.int64); n = len(src)
    _, pos_id = np.unique(np.round(V, 5), axis=0, return_inverse=True); pos_id = pos_id.ravel()
    wid = pos_id[src]                                       # welded id per split vertex
    X = V[src]                                              # source-space positions
    ymid = (V[:, 1].max() + V[:, 1].min()) / 2
    def to_game(p): return (np.asarray(p, float) - np.array([0, ymid, 0])) * S
    P = to_game(X)

    def normals(points):
        acc = np.zeros((wid.max() + 1, 3))
        fn = np.cross(points[T[:, 1]] - points[T[:, 0]], points[T[:, 2]] - points[T[:, 0]])
        for j in range(3): np.add.at(acc, wid[T[:, j]], fn)
        acc /= np.maximum(np.linalg.norm(acc, axis=1, keepdims=True), 1e-12)
        return acc[wid]
    N = normals(P)

    # ---------------------------------------------------------------- parts / colours
    tex = Image.open(os.path.join(SRC, name, c['tex'])).convert('RGB')
    ta = np.asarray(tex); th, tw = ta.shape[:2]
    px = np.clip((UV[:, 0] % 1) * (tw - 1), 0, tw - 1).astype(int); py = np.clip((1 - UV[:, 1] % 1) * (th - 1), 0, th - 1).astype(int)
    vcol = ta[py, px].astype(float); lum = vcol.mean(1); sat = vcol.max(1) - vcol.min(1)
    A = coo_matrix((np.ones(len(T) * 3), (np.r_[T[:, 0], T[:, 1], T[:, 2]], np.r_[T[:, 1], T[:, 2], T[:, 0]])), shape=(n, n))
    # shells by the OBJ's own vertex indices (UV splits never cut a shell; touching shells stay apart)
    so = src[T]; m_ = len(V)
    Ao = coo_matrix((np.ones(len(T) * 3), (np.r_[so[:, 0], so[:, 1], so[:, 2]], np.r_[so[:, 1], so[:, 2], so[:, 0]])), shape=(m_, m_))
    _, lab_o = connected_components(Ao, directed=False); lab = lab_o[src]
    size = np.bincount(lab); main = np.argmax(size); body = lab == main
    eyes = []
    for comp in np.unique(lab):
        s = lab == comp
        if s.sum() < 150 or comp == main: continue
        if (lum[s] > 175).mean() > .6 and (sat[s] < 40).mean() > .6:
            Q = X[s]; ctr = (Q.max(0) + Q.min(0)) / 2; rad = (Q.max(0) - Q.min(0)).max() / 2
            dist = np.linalg.norm(X - ctr, axis=1)
            dark = (lum < 70) & (dist / rad > .5) & (dist / rad < .97) & (size[lab] < 250)
            if dark.sum() < 3: continue
            pupil = X[dark].mean(0); ax = pupil - ctr; ax /= np.linalg.norm(ax)
            eyes.append(dict(center=ctr, radius=rad, pupil=pupil, axis=ax, comp=comp))
    eyes.sort(key=lambda e: e['center'][0])
    eye_shell = np.isin(lab, [e['comp'] for e in eyes])
    if report: print(f'{name}: verts {n} tris {len(T)} shells {len(size)} body {body.sum()} eyes {len(eyes)}')
    shapes = np.zeros((ns, n, 6), np.float32)
    def put(shape, delta_src): shapes[names.index(shape), :, :3] = delta_src * S
    x, y, z = X[:, 0], X[:, 1], X[:, 2]; zmin, zmax = z.min(), z.max()
    # ---------------------------------------------------------------- swim
    swim = np.zeros_like(X); fin = np.zeros_like(X)
    hinge = c['hinge']; s_tail = sm((hinge - z) / (hinge - zmin))
    if plan in ('round', 'shark'):
        swim[:, 0] = c['tail'] * s_tail ** 2
        if plan == 'shark': swim[:, 0] -= .012 * sm((z - .25) / .25)          # the head counters the tail
    elif plan == 'ray':
        w = np.clip((np.abs(x) - c['core']) / (np.abs(x).max() - c['core']), 0, 1) ** 1.5
        swim[:, 1] = c['wing'] * w
        swim[:, 2] = -.25 * c['wing'] * w ** 2                                  # tips trail slightly on the stroke
        swim[:, 0] += c['tail'] * s_tail ** 2
    elif plan == 'flat':
        rx, rz, cz = c['core']; rho = np.sqrt((x / rx) ** 2 + ((z - cz) / rz) ** 2)
        e = sm((rho - .95) / .5)
        swim[:, 1] = c['frill'] * e * np.sin(2 * np.pi * (z + .45) / .62)
        swim[:, 1] += c['tail'] * s_tail ** 2
    put('Swim_Left', swim); put('Swim_Right', -swim)
    # ---------------------------------------------------------------- fins
    if plan in ('round', 'shark'):
        p = c['pect']; side = np.sign(x)
        region = (np.abs(x) > p['x']) & (y > p['y'][0]) & (y < p['y'][1]) & (z > p['z'][0]) & (z < p['z'][1]) & ~eye_shell
        reach = np.clip((np.abs(x) - p['x']) / max(1e-6, np.abs(x[region]).max() - p['x']), 0, 1) if region.any() else 0
        ang = np.radians(24) * sm(reach * 1.4) * region
        pivot_y = (p['y'][0] + p['y'][1]) / 2
        rx_ = np.abs(x) - p['x']; ry_ = y - pivot_y
        nx = rx_ * np.cos(ang) - ry_ * np.sin(ang); ny = rx_ * np.sin(ang) + ry_ * np.cos(ang)
        fin[:, 0] = side * (nx - rx_); fin[:, 1] = ny - ry_
    elif plan == 'ray':
        w = sm((np.abs(x) - .22) / .15)
        fin[:, 1] = .045 * w ** 2
    elif plan == 'flat':
        rx, rz, cz = c['core']; rho = np.sqrt((x / rx) ** 2 + ((z - cz) / rz) ** 2)
        fin[:, 1] = .03 * sm((rho - 1.05) / .45) * np.cos(2 * np.pi * (z + .45) / .62) + .04 * s_tail ** 2
    put('Fin_Up', fin); put('Fin_Down', -fin)
    # ---------------------------------------------------------------- gulp
    zc, half = c['belly']; band = sm(1 - np.abs(z - zc) / half)
    ymid_body = (X[body, 1].max() + X[body, 1].min()) / 2
    radial = np.c_[x, (y - ymid_body) * (1.6 if plan in ('ray', 'flat') else 1), np.zeros(n)]
    rn = np.linalg.norm(radial, axis=1, keepdims=True); radial = radial / np.maximum(rn, 1e-6)
    # A smooth radial swell of the belly band (Fish0 strength, ~.03 in game units). Everything that is not
    # the skin itself (eyes, loose triangles, other shells) and the pectoral fins ride along with the skin
    # under them instead of inflating.
    gulp = radial * (.03 / S * band * body)[:, None]
    fin_v = (np.linalg.norm(fin, axis=1) > 1e-4) if plan in ('round', 'shark') else np.zeros(n, bool)
    root = np.where(body & ~fin_v)[0]; ride = ~body | fin_v
    if ride.any():
        dd_, ii_ = cKDTree(X[root]).query(X[ride], k=4); ww_ = 1 / np.maximum(dd_, 1e-6); ww_ /= ww_.sum(1, keepdims=True)
        gulp[ride] = (gulp[root[ii_]] * ww_[:, :, None]).sum(1)
    put('Swallow', gulp)
    # ---------------------------------------------------------------- maw (R18 mapping, general axis)
    k = c['k']; Ax = np.asarray(c['axis'], float); Ax /= np.linalg.norm(Ax)
    C = np.asarray(c['mouth'], float)
    dv = X - C; s_ax = dv @ Ax; rv = dv - np.outer(s_ax, Ax); r = np.linalg.norm(rv, axis=1)
    rt = np.array([0, .03, .10, .20, .30, .46, 10]) * k / S; gt = np.array([0, .275, .34, .38, .405, .47, 10.01]) * k / S
    u = k / S
    def fish0_map(r_used, wz_used, push=True):
        """The approved Fish0 radius map (R17/R18), evaluated on an optionally re-unrolled radius."""
        r_new = r + wz_used * (np.interp(r_used, rt, gt) - r)
        bowl = np.clip((.07 * u - r) / (.035 * u), 0, 1); depth = np.clip(-s_ax / (.047 * u), 0, 1)
        if push: r_new *= 1 - .55 * depth * sm(bowl)
        scale_r = np.where(r > 1e-7, r_new / np.maximum(r, 1e-7), 1.0)
        hh = np.clip((.06 * u - r) / (.025 * u), 0, 1); dd = np.clip((-s_ax - .006 * u) / (.029 * u), 0, 1)
        inner = sm(hh) * sm(dd) if push else 0 * r
        return rv * (scale_r - 1)[:, None] - np.outer(.12 * u * inner * wz_used, Ax)
    wz = sm((s_ax + .17 * u) / (.15 * u))
    maw = fish0_map(r, wz)
    if plan in ('round', 'flat'):
        # Tube mouths. A radius-only map folds the near-cylindrical inner wall of the lip tube and
        # pulls the back of a lip torus into the opening. So: (1) unroll the cavity by geodesic
        # distance from its floor into a closed ellipsoid throat, with each ring's own centre giving
        # the angle; (2) beyond the crest, never let the lip's radius shrink again (torus back ->
        # cylinder behind the flare); (3) the skin under the tube's footprint goes out with it.
        from geo import mouth_geodesic
        # start on the lip's own floor when the lip is modelled apart from the head

        prefer = np.where((lab_o != main) & (size[lab_o] >= 30))[0]
        rho_o, _, _, st = mouth_geodesic(V, F, c, prefer=prefer); rho = rho_o[src]
        fin_ = np.isfinite(rho)
        frag = (size[lab] < 30) & ~eye_shell                 # loose duplicate triangles: they follow their host later
        bins = np.arange(.02, .14, .008); prof = [s_ax[(rho >= a_) & (rho < a_ + .012) & (r < .14) & ~frag] for a_ in bins]
        rho_rim = bins[int(np.argmax([q.mean() if len(q) > 2 else -9 for q in prof]))] + .006     # first guess, refined per angle below
        # ring centres along the cavity (smoothed), then directions about the local centre
        rb = np.arange(0, 1.6 * rho_rim + .012, .012); cens = []
        for a_ in rb:
            m_ = (rho >= a_) & (rho < a_ + .012) & ~frag
            cens.append(X[m_].mean(0) if m_.sum() >= 3 else np.full(3, np.nan))
        cens = np.array(cens); ok = ~np.isnan(cens[:, 0])
        cens = np.array([np.interp(rb, rb[ok], cens[ok, j]) for j in range(3)]).T
        cens = np.array([np.convolve(np.pad(cens[:, j], 1, mode='edge'), np.ones(3) / 3, 'valid') for j in range(3)]).T
        cen_v = np.array([np.interp(np.where(fin_, rho, 0), rb + .006, cens[:, j]) for j in range(3)]).T
        lv = (X - cen_v) - np.outer((X - cen_v) @ Ax, Ax); lr = np.linalg.norm(lv, axis=1)
        dirn = np.where((lr > 1e-6)[:, None], lv / np.maximum(lr, 1e-9)[:, None], rv / np.maximum(r, 1e-9)[:, None])
        # the crest is not equally far from the floor all round an off-centre cavity: find it per angle
        e1 = np.cross(Ax, [0, 1, 0]); e1 = e1 / np.linalg.norm(e1) if np.linalg.norm(e1) > 1e-6 else np.array([1., 0, 0]); e2 = np.cross(Ax, e1)
        theta = np.arctan2(dirn @ e2, dirn @ e1); NSEC = 16
        sec = ((theta + np.pi) / (2 * np.pi) * NSEC).astype(int) % NSEC
        rim_sec = np.full(NSEC, rho_rim)
        for k_ in range(NSEC):
            pr = [s_ax[(sec == k_) & (rho >= a_) & (rho < a_ + .012) & (r < .14) & ~frag] for a_ in bins]
            mv = [q.mean() if len(q) > 1 else -9 for q in pr]
            if max(mv) > -9: rim_sec[k_] = bins[int(np.argmax(mv))] + .006
        rim_sec = np.array([np.median(np.take(rim_sec, range(k_ - 1, k_ + 2), mode='wrap')) for k_ in range(NSEC)])
        rim_sec = np.array([np.mean(np.take(rim_sec, range(k_ - 1, k_ + 2), mode='wrap')) for k_ in range(NSEC)])
        ctr_ang = -np.pi + (np.arange(NSEC) + .5) * 2 * np.pi / NSEC
        rim_v = np.interp(theta, np.r_[ctr_ang - 2 * np.pi, ctr_ang, ctr_ang + 2 * np.pi], np.tile(rim_sec, 3))
        rho_rim = float(np.median(rim_sec))
        # a lip modelled apart: its back (inside the head) must not shrink back into the opening
        tube_shell = fin_ & ~body & ~eye_shell & ~frag
        host = ~frag
        ring_r = [r[(rho >= a_) & (rho < a_ + .012) & host].mean() for a_ in np.arange(rho_rim, rho_rim + .13, .012) if ((rho >= a_) & (rho < a_ + .012) & host).sum() > 2]
        r_tmax = max(ring_r) if tube_shell.any() else ring_r[0]
        s_floor = s_ax[src == st].min()
        r_used = r.copy(); wz_used = wz.copy()
        beyond = tube_shell & (rho > rim_v)
        r_used[beyond] = np.maximum(r[beyond], r_tmax); wz_used[beyond] = 1
        foot = ~fin_ & (s_ax > s_floor - .02) & ~eye_shell & tube_shell.any()
        wf = 1 - sm((r - r_tmax) / (.4 * r_tmax))
        r_used[foot] = np.maximum(r[foot], r_tmax * wf[foot] + r[foot] * (1 - wf[foot]))
        wz_used[foot] = np.maximum(wz[foot], wf[foot])
        tube_map = fish0_map(r_used, wz_used, push=False)
        maw = np.where((beyond | (foot & (wf > 0)))[:, None], tube_map, maw)
        # The head skin inside the lip tube's footprint would be stretched straight across the opening;
        # fold it back instead into a closed cup behind the gullet (rim on the lip cylinder, centre deep).
        foot_in = foot & (r < r_tmax)
        if foot_in.any():
            R_cyl = np.interp(r_tmax, rt, gt); f_ = r / r_tmax
            R_new = R_cyl * f_; A_new = s_ax - (.16 * u + .02) * np.clip(1 - f_, 0, 1) ** 1.5
            rdir = rv / np.maximum(r, 1e-9)[:, None]
            cup = rdir * (R_new - r)[:, None] + np.outer(A_new - s_ax, Ax)
            maw[foot_in] = cup[foot_in]
        old_t = X + maw; rel = old_t - C; A_old = rel @ Ax; R_old = np.linalg.norm(rel - np.outer(A_old, Ax), axis=1)
        crest = (np.abs(rho - rim_v) < .008) & host
        R_sec = np.array([R_old[crest & (sec == k_)].mean() if (crest & (sec == k_)).sum() else np.nan for k_ in range(NSEC)])
        A_sec = np.array([A_old[crest & (sec == k_)].mean() if (crest & (sec == k_)).sum() else np.nan for k_ in range(NSEC)])
        R_sec = np.where(np.isnan(R_sec), np.nanmean(R_sec), R_sec); A_sec = np.where(np.isnan(A_sec), np.nanmean(A_sec), A_sec)
        wrap = lambda v_: np.interp(theta, np.r_[ctr_ang - 2 * np.pi, ctr_ang, ctr_ang + 2 * np.pi], np.tile(v_, 3))
        R_c = wrap(R_sec) * .97; A_c = wrap(A_sec)
        # a horn-shaped gullet: wide at the lips, narrowing fast into the throat, so whatever the lips
        # roll back over always stays outside it
        t = np.clip(rho / rim_v, 0, 1)
        cust = C + dirn * (R_c * t ** 1.6)[:, None] + Ax[None, :] * (A_c - .13 * u * (1 - t))[:, None]
        wb = sm((rho / rim_v - .85) / .45); wb[~fin_] = 1
        maw = (1 - wb)[:, None] * (cust - X) + wb[:, None] * maw
        # loose duplicate triangles ride on the surface they were lying on
        hv = np.where(host & ~eye_shell)[0]; ht = cKDTree(X[hv])
        dd_, ii_ = ht.query(X[frag], k=4); ww_ = 1 / np.maximum(dd_, 1e-6); ww_ /= ww_.sum(1, keepdims=True)
        maw[frag] = (maw[hv[ii_]] * ww_[:, :, None]).sum(1)
        if report: print(f'   tube maw: rho_rim {rho_rim:.3f} [{rim_sec.min():.3f},{rim_sec.max():.3f}] r_tmax {r_tmax:.3f} tube_shell {tube_shell.sum()} foot {(foot & (wf > 0)).sum()} R_c {R_c.mean():.3f} A_c {A_c.mean():+.3f}')
    mask = np.ones(n)
    for e in eyes:
        ed = np.linalg.norm(X - e['center'], axis=1); mask *= sm((ed - 1.13 * e['radius']) / (1.0 * e['radius']))
    if plan in ('round', 'flat'): mask[tube_shell | frag & (np.linalg.norm(X - C, axis=1) < .2) | (fin_ & (rho < 1.3 * rim_v))] = 1     # the lips and throat always open fully
    maw *= (mask * ~eye_shell)[:, None]
    put('Alien_Mouth_Open', maw)
    # ---------------------------------------------------------------- morph into the shared egg
    egg = omat == 7; m1000 = names.index('To_Matched_1000')
    egg_final = ov[egg, :3] + os_[m1000, egg, :3]; egg_c = egg_final.mean(0)
    dirs = egg_final - egg_c; er = np.linalg.norm(dirs, axis=1); tree = cKDTree(dirs / er[:, None])
    q = P - egg_c; qr = np.linalg.norm(q, axis=1); qd = q / np.maximum(qr, 1e-6)[:, None]
    _, nn = tree.query(qd, k=6); R = er[nn].mean(1)
    target = egg_c + qd * (R * .88)[:, None]
    for i in range(8): shapes[names.index('To_Matched_%04d' % ((i + 1) * 125)), :, :3] = FRACS[i] * (target - P)
    # ---------------------------------------------------------------- smooth across welded seams + normals
    # Loose duplicate triangles (tiny shells lying on the surface) take their welded twin's motion exactly;
    # elsewhere UV-split copies of one point are averaged.
    loose = (size[lab] < 30) & ~eye_shell
    twin = np.bincount(wid[~loose], minlength=wid.max() + 1) > 0
    use = ~loose | ~twin[wid]
    for i in range(ns):
        D = shapes[i, :, :3]
        if np.abs(D).max() == 0: continue
        acc = np.zeros((wid.max() + 1, 3)); cnt = np.bincount(wid[use], minlength=wid.max() + 1)[:, None]
        np.add.at(acc, wid[use], D[use]); shapes[i, :, :3] = (acc / np.maximum(cnt, 1))[wid]
        shapes[i, :, 3:6] = normals(P + shapes[i, :, :3]) - N
    # ---------------------------------------------------------------- vertices
    rows = np.zeros((n, 18), np.float32)
    rows[:, 0:3] = P; rows[:, 3:6] = N
    rows[:, 6] = UV[:, 0] % 1; rows[:, 7] = .5 + .5 * (UV[:, 1] % 1)          # painted texture: top half of the atlas
    rows[:, 8] = 0; rows[:, 9] = np.where(eye_shell, .84, .55)                # metallic / smoothness like Fish0
    rows[:, 10] = 1; rows[:, 14] = 1                                          # Body
    T0, n0 = T.copy(), n
    if plan in ('round', 'flat'):
        # The gullet gets its own copies of the vertices, mapped onto a mouth-gradient strip in the atlas
        # (bottom half, beside the tongue swatch): the painted lining of a lip tube is tiny and some floor
        # pieces carry UVs scattered across the atlas, which smear body colour over the open throat.
        q = np.where(fin_, rho / rim_v, 9.0)
        inner_t = q[T].max(1) < GULLET_Q
        vids = np.unique(T[inner_t]); newidx = np.full(n, -1); newidx[vids] = n + np.arange(len(vids))
        extra_rows = rows[vids].copy()
        extra_rows[:, 6] = STRIP_U; extra_rows[:, 7] = STRIP_V0 + (STRIP_V1 - STRIP_V0) * np.clip(q[vids] / GULLET_Q, 0, 1)
        T = T.copy(); T[inner_t] = newidx[T[inner_t]]
        rows = np.concatenate([rows, extra_rows]); shapes = np.concatenate([shapes, shapes[:, vids]], axis=1); n = n + len(vids)
        if report: print(f'   gullet: {inner_t.sum()} triangles on the mouth strip ({len(vids)} vertex copies)')
    # ---------------------------------------------------------------- hidden parts from the approved Fish0 rig
    keep = np.where(np.isin(omat, [3, 6, 7]))[0]
    kept = ov[keep].copy(); kshapes = os_[:, keep].copy()
    mouth_old = np.array(d0['mouthAnchor']); Rm = rotation_from_z(Ax)
    mouth_new = to_game(C) - .012 * Ax
    head = np.isin(omat[keep], [3, 6])
    rel = kept[head, 0:3] - mouth_old
    new_rest = mouth_new + (rel @ Rm.T) * k
    kept[head, 0:3] = new_rest; kept[head, 3:6] = kept[head, 3:6] @ Rm.T
    for sname in names:
        si = names.index(sname)
        if sname == 'Alien_Mouth_Open': kshapes[si, head, :3] = (kshapes[si, head, :3] @ Rm.T) * k; kshapes[si, head, 3:6] = kshapes[si, head, 3:6] @ Rm.T
        elif sname.startswith('To_Matched'):
            old_final = ov[keep][head, :3] + os_[m1000, keep][head, :3]
            fr = FRACS[int(sname[-4:]) // 125 - 1]
            kshapes[si, head, :3] = fr * (old_final - new_rest)
        else: kshapes[si, head, :] = 0
    remap = -np.ones(len(ov), np.int64); remap[keep] = np.arange(len(keep)) + n
    keep_tris = otri[np.all(np.isin(otri, keep), axis=1)]
    all_v = np.concatenate([rows, kept]); all_t = np.concatenate([T, remap[keep_tris]])
    all_s = np.concatenate([shapes, kshapes], axis=1)
    # ---------------------------------------------------------------- json
    d = json.loads(json.dumps(d0))
    d['name'] = c['out']; d['atlas'] = c['out']; d['vertices'] = int(len(all_v)); d['triangles'] = int(len(all_t))
    d['parts'] = [c['out'] + '_Skin'] + [p for p in d0['parts'] if p.startswith(('Fish_Fang', 'Fish_Tongue', 'Fish_Matched'))]
    d['mouthAnchor'] = [round(float(v), 4) for v in mouth_new]
    d['throatAnchor'] = [round(float(v), 4) for v in mouth_new - .1526 * k * Ax]
    eye_out = []
    for e in eyes:
        pg = to_game(e['pupil']); eye_out += [round(float(v), 4) for v in list(pg) + list(e['axis'])]
    d['eyes'] = eye_out; d['eyeRadius'] = round(float(np.mean([e['radius'] for e in eyes]) * S * .8), 4) if eyes else 0
    d['mouthRadius'] = round(.21 * k, 4)
    if write:
        with open(os.path.join(OUT, c['out'] + '.cartoon'), 'wb') as f:
            f.write(struct.pack('<4i', len(all_v), all_t.size, ns, 1))
            f.write(all_v.astype('<f4').tobytes()); f.write(all_t.astype('<i4').tobytes())
            f.write(np.zeros(len(all_t), np.int32).astype('<i4').tobytes()); f.write(all_s.astype('<f4').tobytes())
        json.dump(d, open(os.path.join(OUT, c['out'] + '.json'), 'w'), separators=(',', ':'))
    if report:
        for sname in ['Swim_Left', 'Fin_Up', 'Swallow', 'Alien_Mouth_Open', 'To_Matched_1000']:
            D = shapes[names.index(sname), :, :3]; print(f'   {sname:17s} max {np.linalg.norm(D, axis=1).max():.3f}  active {(np.linalg.norm(D, axis=1) > 1e-4).sum()}')
        print('   bbox', P.min(0).round(3), P.max(0).round(3), 'mouth', d['mouthAnchor'], 'throat', d['throatAnchor'], 'eyes', len(eyes), 'eyeR', d['eyeRadius'])
    return dict(P=P, T=T0, N=N, shapes=shapes, names=names, UV=UV, tex=tex, eye_shell=eye_shell, eyes=eyes, json=d, all_v=all_v, all_t=all_t, all_s=all_s, n=n0,
                **({k_: v_ for k_, v_ in locals().items() if k_ in ('rho', 'rho_rim', 'rim_v', 'frag', 'tube_shell', 'foot', 'body', 'fin_')}))

def atlas(name, painted=None):
    """Atlas = the dilated painted texture in the top half + the approved Fish0 atlas bottom half
    (palette cells of the hidden fangs / tongue / midpoint egg)."""
    c = CONF[name]
    V, VT, F = load_obj(os.path.join(SRC, name, c['obj']))
    TT = []
    for face in F:
        for j in range(1, len(face) - 1): TT.append((face[0][1], face[j][1], face[j + 1][1]))
    img = painted if painted is not None else Image.open(os.path.join(SRC, name, c['tex'])).convert('RGB')
    filled, _ = dilate(img, VT, np.array(TT))
    old = Image.open(OLD + 'Fish0_Atlas.png').convert('RGB'); W, H = old.size
    out = old.copy(); out.paste(filled.resize((W, H // 2), Image.LANCZOS), (0, 0))
    if c['plan'] in ('round', 'flat'):
        # mouth-gradient strip for the gullet (see build): deep throat at STRIP_V0 -> lip pink at STRIP_V1
        a = np.asarray(out).copy(); x0, x1 = int((STRIP_U - .025) * W), int((STRIP_U + .025) * W)
        for y in range(int((1 - STRIP_V1 - .01) * H), int((1 - STRIP_V0 + .01) * H) + 1):
            v = 1 - y / H; t = np.clip((v - STRIP_V0) / (STRIP_V1 - STRIP_V0), 0, 1) ** .8
            a[y, x0:x1] = (np.array(MOUTH_DEEP) + (np.array(MOUTH_LIP) - np.array(MOUTH_DEEP)) * t).astype(np.uint8)
        out = Image.fromarray(a)
    out.save(os.path.join(OUT, c['out'] + '_Atlas.png'), optimize=True)
    return filled

if __name__ == '__main__':
    todo = sys.argv[1:] or list(CONF)
    for nm in todo: build(nm)
