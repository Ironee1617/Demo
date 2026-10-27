"""Paint fixes on the delivered textures (applied before the atlas is assembled)."""
from build_newfish import *

def raster_uv(tri_uv, W, H):
    """Yield (ys, xs, bary(n,3)) for texels whose centres fall inside a UV triangle (v up)."""
    p = np.c_[tri_uv[:, 0] * W, (1 - tri_uv[:, 1]) * H]
    x0, x1 = int(max(0, np.floor(p[:, 0].min()))), int(min(W - 1, np.ceil(p[:, 0].max())))
    y0, y1 = int(max(0, np.floor(p[:, 1].min()))), int(min(H - 1, np.ceil(p[:, 1].max())))
    if x1 < x0 or y1 < y0: return None
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
    (ax, ay), (bx, by), (cx, cy) = p
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-12: return None
    w0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d; w1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d; w2 = 1 - w0 - w1
    ins = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
    if not ins.any(): return None
    return ys[ins].astype(int), xs[ins].astype(int), np.c_[w0[ins], w1[ins], w2[ins]]

def paint_mouth(name, b, deep=(84, 22, 44), lip=(192, 88, 112), full=.90, fade=1.0):
    """Line the gullet: everything inside the lip crest gets a mouth gradient (dark deep -> pink near the lips),
    fading into the original paint before the crest. Texels shared with any other triangle are left alone."""
    img = np.asarray(Image.open(os.path.join(SRC, name, CONF[name]['tex'])).convert('RGB')).astype(np.float32)
    H, W = img.shape[:2]
    q = np.where(np.isfinite(b['rho']), b['rho'] / b['rim_v'], 9.0)
    T = b['T']; UV = b['UV'] % 1
    tq = q[T].max(1); inner = tq < fade + .05
    lo = UV[T[inner]].reshape(-1, 2).min(0) - .002; hi = UV[T[inner]].reshape(-1, 2).max(0) + .002
    # texels used by other triangles (inside the same UV window)
    other = np.zeros((H, W), bool)
    tu = UV[T]; inwin = ~inner & (tu.max(1) >= lo).all(1) & (tu.min(1) <= hi).all(1)
    for i in np.where(inwin)[0]:
        rr = raster_uv(tu[i], W, H)
        if rr is not None: other[rr[0], rr[1]] = True
    acc = np.zeros((H, W)); qsum = np.zeros((H, W))
    for i in np.where(inner)[0]:
        rr = raster_uv(tu[i], W, H)
        if rr is None: continue
        ys, xs, bc = rr; qq = bc @ q[T[i]]
        acc[ys, xs] += 1; qsum[ys, xs] += qq
    m = (acc > 0) & ~other
    qv = qsum[m] / acc[m]
    t = np.clip(qv / full, 0, 1) ** .8
    col = np.array(deep)[None] + (np.array(lip) - np.array(deep))[None] * t[:, None]
    a = 1 - sm((qv - full) / (fade - full))
    img[m] = img[m] * (1 - a[:, None]) + col * a[:, None]
    print(f'   paint_mouth {name}: inner tris {inner.sum()} texels {m.sum()} blocked {((acc > 0) & other).sum()}')
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))

def repaint_clown_dorsal(img_arr, name='Clownfish', y0=.60, edge=.03, rim=.40):
    """The clownfish's dorsal fin is painted half solid black with a hard cut across the lobe. Paint it orange
    like its back half and keep only a thin black outline: the crest band of the lobe (surface facing up/forward,
    not sideways) near its top/front edge, which reads as an outline from the side like the other fins."""
    c = CONF[name]; V, VT, F = load_obj(os.path.join(SRC, name, c['obj']))
    img = img_arr.astype(np.float32); H, W = img.shape[:2]
    _, pid = np.unique(np.round(V, 5), axis=0, return_inverse=True); pid = pid.ravel()
    Nw = np.zeros((pid.max() + 1, 3))
    for face in F:
        vis = [vi for vi, ti in face]
        for j in range(1, len(vis) - 1):
            a_, b_, c_ = V[vis[0]], V[vis[j]], V[vis[j + 1]]; fn = np.cross(b_ - a_, c_ - a_)
            for vv in (vis[0], vis[j], vis[j + 1]): Nw[pid[vv]] += fn
    Nw /= np.maximum(np.linalg.norm(Nw, axis=1, keepdims=True), 1e-12); NV = Nw[pid]
    fin = V[:, 1] > y0; fv = V[fin]
    zb = np.linspace(fv[:, 2].min(), fv[:, 2].max(), 40); yb = np.linspace(y0, fv[:, 1].max(), 30)
    top = np.array([fv[np.abs(fv[:, 2] - z_) < .012, 1].max() if (np.abs(fv[:, 2] - z_) < .012).any() else y0 for z_ in zb])
    front = np.array([fv[np.abs(fv[:, 1] - y_) < .012, 2].max() if (np.abs(fv[:, 1] - y_) < .012).any() else fv[:, 2].max() for y_ in yb])
    orange = np.array([238., 123., 34.]); ink = np.array([52., 55., 66.])
    changed = 0; kept = 0
    for face in F:
        vis = [vi for vi, ti in face]
        if not all(fin[vis]): continue
        for j in range(1, len(face) - 1):
            tri = [face[0], face[j], face[j + 1]]
            uv = np.array([VT[ti] for vi, ti in tri]) % 1; P3 = np.array([V[vi] for vi, ti in tri]); N3 = np.array([NV[vi] for vi, ti in tri])
            rr = raster_uv(uv, W, H)
            if rr is None: continue
            ys, xs, bc = rr; p = bc @ P3; nn = bc @ N3; nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
            col = img[ys, xs]; dark = col.mean(1) < 100
            if not dark.any(): continue
            d_top = np.interp(p[:, 2], zb, top) - p[:, 1]; d_front = np.interp(p[:, 1], yb, front) - p[:, 2]
            margin = (np.minimum(d_top, d_front) < edge) & (np.abs(nn[:, 0]) < rim)
            fill = dark & ~margin; line = dark & margin
            img[ys[fill], xs[fill]] = orange; img[ys[line], xs[line]] = ink
            changed += fill.sum(); kept += line.sum()
    print(f'   clown dorsal: repainted {changed} texels, kept {kept} as outline')
    return img
