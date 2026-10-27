"""Small z-buffered, textured, back-face-aware rasterizer for offline review (numpy)."""
import numpy as np
from PIL import Image

def look(eye, target, up=(0, 1, 0)):
    eye = np.asarray(eye, float); f = np.asarray(target, float) - eye; f /= np.linalg.norm(f)
    r = np.cross(f, up); r /= np.linalg.norm(r); u = np.cross(r, f)
    return eye, r, u, f

def raster(P, T, UV, tex, cam, size=(480, 400), fov=30, light=(-.4, .7, .6), mark_back=True, bg=(235, 238, 242), nrm=None, back_col=None):
    """P: (n,3) positions, T: (m,3), UV: (n,2) in [0,1] with v up, tex: HxWx3 uint8 array."""
    eye, r, u, f = cam; W, H = size
    rel = P - eye; xc = rel @ r; yc = rel @ u; zc = rel @ f
    fl = .5 * H / np.tan(np.radians(fov) / 2)
    sx = W / 2 + fl * xc / np.maximum(zc, 1e-4); sy = H / 2 - fl * yc / np.maximum(zc, 1e-4)
    img = np.zeros((H, W, 3), np.float32); img[:] = bg; zb = np.full((H, W), np.inf, np.float32)
    th, tw = tex.shape[:2]
    L = np.asarray(light, float); L /= np.linalg.norm(L)
    tp = P[T]; fn = np.cross(tp[:, 1] - tp[:, 0], tp[:, 2] - tp[:, 0]); fnn = np.linalg.norm(fn, axis=1); fn = fn / np.maximum(fnn, 1e-12)[:, None]
    facing = (fn * (tp.mean(1) - eye)).sum(1) < 0
    if nrm is None: nrm = np.zeros_like(P); np.add.at(nrm, T[:, 0], fn); np.add.at(nrm, T[:, 1], fn); np.add.at(nrm, T[:, 2], fn); nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
    for i in range(len(T)):
        a, b, c = T[i]
        if zc[a] <= 1e-3 or zc[b] <= 1e-3 or zc[c] <= 1e-3 or fnn[i] < 1e-14: continue
        x0, x1 = int(max(0, np.floor(min(sx[a], sx[b], sx[c])))), int(min(W - 1, np.ceil(max(sx[a], sx[b], sx[c]))))
        y0, y1 = int(max(0, np.floor(min(sy[a], sy[b], sy[c])))), int(min(H - 1, np.ceil(max(sy[a], sy[b], sy[c]))))
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        d = (sy[b] - sy[c]) * (sx[a] - sx[c]) + (sx[c] - sx[b]) * (sy[a] - sy[c])
        if abs(d) < 1e-12: continue
        w0 = ((sy[b] - sy[c]) * (xs - sx[c]) + (sx[c] - sx[b]) * (ys - sy[c])) / d
        w1 = ((sy[c] - sy[a]) * (xs - sx[c]) + (sx[a] - sx[c]) * (ys - sy[c])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        if not inside.any(): continue
        # perspective-correct depth / attributes
        iz = w0 / zc[a] + w1 / zc[b] + w2 / zc[c]; z = 1 / iz
        yy, xx = ys[inside].astype(int), xs[inside].astype(int); zz = z[inside]
        closer = zz < zb[yy, xx]
        if not closer.any(): continue
        yy, xx, zz = yy[closer], xx[closer], zz[closer]
        b0 = (w0[inside][closer] / zc[a]) * zz; b1 = (w1[inside][closer] / zc[b]) * zz; b2 = 1 - b0 - b1
        zb[yy, xx] = zz
        if facing[i] or not mark_back:
            uv = b0[:, None] * UV[a] + b1[:, None] * UV[b] + b2[:, None] * UV[c]
            tx = np.clip((uv[:, 0] % 1) * (tw - 1), 0, tw - 1).astype(int); ty = np.clip((1 - uv[:, 1] % 1) * (th - 1), 0, th - 1).astype(int)
            col = tex[ty, tx].astype(np.float32)
            n_ = b0[:, None] * nrm[a] + b1[:, None] * nrm[b] + b2[:, None] * nrm[c]
            n_ /= np.maximum(np.linalg.norm(n_, axis=1, keepdims=True), 1e-9)
            sh = .42 + .58 * np.clip(n_ @ L, 0, 1)
            img[yy, xx] = col * sh[:, None]
        else:
            img[yy, xx] = (255, 0, 40) if back_col is None else back_col[i]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
