from build_newfish import *
from scipy.sparse.csgraph import dijkstra
def mouth_geodesic(V, F, c, limit=.4, prefer=None):
    E = set()
    for face in F:
        idx = [vi for vi, ti in face]
        for j in range(len(idx)): a, b = idx[j], idx[(j + 1) % len(idx)]; E.add((min(a, b), max(a, b)))
    E = np.array(list(E)); w = np.linalg.norm(V[E[:, 0]] - V[E[:, 1]], axis=1)
    _, pid = np.unique(np.round(V, 5), axis=0, return_inverse=True); pid = pid.ravel()
    G = coo_matrix((w, (pid[E[:, 0]], pid[E[:, 1]])), shape=(pid.max() + 1,) * 2).tocsr()
    Ax = np.asarray(c['axis'], float); Ax /= np.linalg.norm(Ax); C = np.asarray(c['mouth'])
    dv = V - C; s = dv @ Ax; r = np.linalg.norm(dv - np.outer(s, Ax), axis=1)
    cand = np.where((r < .015) & (s > -.12))[0]
    if prefer is not None and np.isin(cand, prefer).any(): cand = cand[np.isin(cand, prefer)]
    st = cand[np.argmin(s[cand])]
    D = dijkstra(G, directed=False, indices=pid[st], limit=limit)[pid]
    return D, r, s, st
if __name__ == '__main__':
    for nm in sys.argv[1:]:
        c = CONF[nm]; V, VT, F = load_obj(os.path.join(SRC, nm, c['obj']))
        D, r, s, st = mouth_geodesic(V, F, c)
        # colour per vertex (first uv seen)
        tex = np.asarray(Image.open(os.path.join(SRC, nm, c['tex'])).convert('RGB')); th, tw = tex.shape[:2]
        col = np.zeros((len(V), 3))
        for face in F:
            for vi, ti in face:
                u, v = VT[ti]; col[vi] = tex[int((1 - v % 1) * (th - 1)), int((u % 1) * (tw - 1))]
        print(nm, 'start r', r[st].round(3), 's', s[st].round(3))
        for a in np.arange(0, .2, .012):
            m = (D >= a) & (D < a + .012)
            if m.sum(): print(f'  rho {a:.3f} n {m.sum():4d} r {r[m].mean():.3f} [{r[m].min():.3f},{r[m].max():.3f}] s {s[m].mean():+.3f} [{s[m].min():+.3f},{s[m].max():+.3f}] col {col[m].mean(0).astype(int)}')
