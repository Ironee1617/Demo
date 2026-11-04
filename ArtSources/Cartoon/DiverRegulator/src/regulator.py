"""Divers: the regulator held in the mouth (2026-11-01). The old regulator hung at the throat on a hose from the
earpiece; it is removed (yellow: its shell is fused to the collar and earpiece, so the holes are capped and
painted like their surroundings) and a new one is modelled in front of the mouth, its hose running from the same
spot under the earpiece along the cheek into the regulator's side port. Every new part rides the Head bone."""
import numpy as np, json
from dload import load, load_uv2, atlas, face_colours, components, weld, hole_loops
from caps import context, fill_loop, split
from scipy.spatial import cKDTree

def normalize(v): return v/np.maximum(np.linalg.norm(v,axis=-1,keepdims=True),1e-12)

def grid_tris(nu,nv,wrap_u=True):
    T=[]
    for i in range(nu if wrap_u else nu-1):
        i2=(i+1)%nu
        for j in range(nv-1):
            a=i*nv+j; b=i2*nv+j; c=i2*nv+j+1; d=i*nv+j+1
            T+= [(a,b,c),(a,c,d)]
    return np.array(T)

def vnormals(V,T):
    fn=np.cross(V[T[:,1]]-V[T[:,0]],V[T[:,2]]-V[T[:,0]]); N=np.zeros_like(V)
    for k in range(3): np.add.at(N,T[:,k],fn)
    return normalize(N)

def superellipsoid(C,a,b,d,e1=.55,e2=.6,nu=40,nv=24):
    th=np.linspace(-np.pi,np.pi,nu,endpoint=False); ph=np.linspace(-np.pi/2,np.pi/2,nv)
    c=lambda w,m: np.sign(np.cos(w))*np.abs(np.cos(w))**m; s=lambda w,m: np.sign(np.sin(w))*np.abs(np.sin(w))**m
    TH,PH=np.meshgrid(th,ph,indexing='ij')
    V=np.stack([a*c(PH,e1)*c(TH,e2), b*s(PH,e1), d*c(PH,e1)*s(TH,e2)],-1).reshape(-1,3)+C
    T=grid_tris(nu,nv)
    # make faces point outward (theta runs x->z, latitude bottom->top)
    T=T[:,::-1] if np.dot(np.cross(V[T[0,1]]-V[T[0,0]],V[T[0,2]]-V[T[0,0]]),V[T[0]].mean(0)-C)<0 else T
    return V,T

def orient_radial(V,T,C,axis):
    """wind faces outward from the axis line (area-weighted vote over the faces that face sideways)"""
    axis=normalize(np.asarray(axis,float)); fn=np.cross(V[T[:,1]]-V[T[:,0]],V[T[:,2]]-V[T[:,0]]); cen=V[T].mean(1)
    rel=cen-C; rad=rel-np.outer(rel@axis,axis); rn=normalize(rad); side=np.abs(normalize(fn)@axis)<.5
    vote=(fn[side]*rn[side]).sum()
    return T if vote>=0 else T[:,::-1]

def revolve(C,profile,axis,nu=36):
    """profile: list of (radius, height) from the axis tip outward; revolved about axis through C"""
    axis=normalize(np.asarray(axis,float)); ref=np.array([0,1,0.]) if abs(axis[1])<.9 else np.array([1,0,0.])
    e1=normalize(np.cross(axis,ref)); e2=np.cross(axis,e1)
    th=np.linspace(0,2*np.pi,nu,endpoint=False); pr=np.asarray(profile,float)
    V=np.array([C+axis*h+(np.cos(t)*e1+np.sin(t)*e2)*r for t in th for r,h in pr])
    T=grid_tris(nu,len(pr))
    return V,orient_radial(V,T,C,axis)

def tube(path,r,seg=14,caps=True):
    path=np.asarray(path,float); tang=np.gradient(path,axis=0); tang=normalize(tang)
    ref=np.array([0,0,1.]) if abs(tang[0,2])<.9 else np.array([1,0,0.])
    n=normalize(np.cross(tang[0],ref)); frames=[]
    for t in tang:
        n=normalize(n-t*np.dot(n,t)); frames.append((n,np.cross(t,n)))
    th=np.linspace(0,2*np.pi,seg,endpoint=False)
    V=np.array([p+(np.cos(a)*f[0]+np.sin(a)*f[1])*r for a in th for p,f in zip(path,frames)])
    T=grid_tris(seg,len(path))
    if np.dot(np.cross(V[T[0,1]]-V[T[0,0]],V[T[0,2]]-V[T[0,0]]),V[T[0]].mean(0)-path[0])<0: T=T[:,::-1]
    if caps:
        L=len(path); base=len(V); V=np.vstack([V,path[0],path[-1]])
        for i in range(seg):
            i2=(i+1)%seg
            T=np.vstack([T,[base,i2*L,i*L],[base+1,i*L+L-1,i2*L+L-1]])
    return V,T

def bezier(P0,P1,P2,P3,n):
    t=np.linspace(0,1,n)[:,None]
    return (1-t)**3*P0+3*(1-t)**2*t*P1+3*(1-t)*t**2*P2+t**3*P3

def cap_hole(loop_w,w,P,N,keepT,uv,uv2,BI,BW,T,rings=4):
    """fill one hole: concentric rings toward the centre, relaxed; returns verts, tris, per-face uv source face"""
    # order the loop (welded ids) into a cycle
    wp={}; wn={}; worig={}
    for i,k in enumerate(w): wp.setdefault(k,P[i]); wn.setdefault(k,N[i]); worig.setdefault(k,i)
    L=list(loop_w); s=set(L)
    E={}
    for t in w[T[keepT]]:
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])):
            if a in s and b in s: E.setdefault((min(a,b),max(a,b)),[]).append((a,b))
    nb={k:[] for k in L}
    for (a,b),occ in E.items():
        if len(occ)==1: nb[a].append(b); nb[b].append(a)
    cyc=[L[0]]; prev=None
    while True:
        cur=cyc[-1]; nxt=[x for x in nb[cur] if x!=prev]
        if not nxt: break
        n=nxt[0]
        if n==cyc[0]: break
        prev=cur; cyc.append(n)
        if len(cyc)>len(L)+2: break
    B=np.array([wp[k] for k in cyc]); m=len(B); c=B.mean(0)
    V=[B]
    for r in range(1,rings): V.append(c+(B-c)*(1-r/rings))
    V=np.vstack(V+[c[None]])
    T_=[]
    for r in range(rings-1):
        for i in range(m):
            a=r*m+i; b=r*m+(i+1)%m; cc=(r+1)*m+(i+1)%m; d=(r+1)*m+i
            T_+=[(a,b,cc),(a,cc,d)]
    ctr=rings*m
    for i in range(m): T_.append(((rings-1)*m+i,(rings-1)*m+(i+1)%m,ctr))
    T_=np.array(T_)
    # relax the interior (boundary ring fixed): a smooth membrane
    nbrs=[set() for _ in range(len(V))]
    for t in T_:
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])): nbrs[a].add(b); nbrs[b].add(a)
    for _ in range(200):
        Vn=V.copy()
        for i in range(m,len(V)): Vn[i]=V[list(nbrs[i])].mean(0)
        V=Vn
    # orient like the surrounding surface
    navg=normalize(np.array([wn[k] for k in cyc]).mean(0))
    fn=np.cross(V[T_[:,1]]-V[T_[:,0]],V[T_[:,2]]-V[T_[:,0]]).sum(0)
    if np.dot(fn,navg)<0: T_=T_[:,::-1]
    src=np.array([worig[k] for k in cyc])   # original vertex per boundary point (weights/uv2)
    return V,T_,src

def blend_weights(X,S,SI,SW,k=6):
    """skin weights for new points: inverse-distance blend of the nearest source vertices, top four bones"""
    k=min(k,len(S)); d,ix=cKDTree(S).query(X,k=k); d=np.atleast_2d(d); ix=np.atleast_2d(ix)
    wt=1/np.maximum(d,1e-4)**2; wt/=wt.sum(1,keepdims=True)
    BI=np.zeros((len(X),4),np.int32); BW=np.zeros((len(X),4))
    for n in range(len(X)):
        acc={}
        for j in range(k):
            for b,x in zip(SI[ix[n,j]],SW[ix[n,j]]):
                if x>0: acc[int(b)]=acc.get(int(b),0)+x*wt[n,j]
        top=sorted(acc.items(),key=lambda t:-t[1])[:4]; tot=sum(v for _,v in top)
        for j,(b,v) in enumerate(top): BI[n,j]=b; BW[n,j]=v/tot
    return BI,BW

def harmonic_normals(V,TT,m,rimN):
    from scipy.sparse import lil_matrix, csr_matrix
    from scipy.sparse.linalg import spsolve
    n=len(V); nb=[set() for _ in range(n)]
    for t in TT:
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])): nb[a].add(b); nb[b].add(a)
    free=np.arange(m,n); fi={v:i for i,v in enumerate(free)}
    A=lil_matrix((len(free),len(free))); B=np.zeros((len(free),3))
    for v in free:
        i=fi[v]; A[i,i]=len(nb[v])
        for u in nb[v]:
            if u<m: B[i]+=rimN[u]
            else: A[i,fi[u]]-=1
    X=np.zeros((n,3)); X[:m]=rimN
    if len(free):
        A=csr_matrix(A)
        for k in range(3): X[m:,k]=spsolve(A,B[:,k])
    return normalize(X)

DARK=(66,58,62); CREAM=(242,214,196)
def circfit(pts):
    c0=pts.mean(0); _,_,vt=np.linalg.svd(pts-c0); e1,e2,n=vt
    x=(pts-c0)@e1; y=(pts-c0)@e2
    sol=np.linalg.lstsq(np.c_[2*x,2*y,np.ones(len(x))],x**2+y**2,rcond=None)[0]
    c=c0+sol[0]*e1+sol[1]*e2; return c,np.sqrt(sol[2]+sol[0]**2+sol[1]**2),n

def cap_all(name,P,N,T,keep,fc,UV,uv2):
    w,wp,cycles,worig,nb=context(P,T,keep)
    def paint(rim,target):
        rim=np.array([f for f in rim if f is not None]); d=np.abs(fc[rim]-np.array(target)).max(1); f=rim[np.argmin(d)]
        return UV[T[f]].mean(0),uv2[T[f]].mean(0)
    caps=[]; extra=[]
    def add(cyc,rim,mode,colour):
        cp=fill_loop(cyc,wp,nb,worig,mode); cp['mode']=mode; cp['colour']=colour; cp['uv'],cp['uv2']=paint(rim,colour); caps.append(cp)
    for cyc,rim in cycles:
        if len(cyc)<3: continue
        q=np.array([wp[k] for k in cyc]); ctr=q.mean(0)
        if name=='Diver' and np.linalg.norm(ctr-[-.042,.067,.488])<.03:
            # the collar's cream tube ran into the old regulator here: split its cut end off the rest of the hole
            c,r,n=circfit(q[np.abs(q[:,0]+.087)<.012])
            d=q-c; h=d@n; rad=np.linalg.norm(d-np.outer(h,n),axis=1); on=(np.abs(rad-r)<.012)&(np.abs(h)<.015)
            # longest cyclic run of points on the tube's end circle
            best=(0,0); L=len(q)
            for i0 in range(L):
                if on[i0] and not on[i0-1]:
                    j=i0
                    while on[(j+1)%L] and (j+1-i0)<L: j+=1
                    if j-i0>best[1]-best[0]: best=(i0,j)
            i0,i1=best; rot=lambda a: a[i0:]+a[:i0]
            cyc2=rot(list(cyc)); rim2=rot(list(rim)); j=i1-i0
            (ca,ra),(cb,rb)=split(cyc2,rim2,0,j)
            add(ca,ra,'harmonic',CREAM); add(cb,rb,'bilaplace',CREAM)   # the rest: the collar's underside, cream down to the neck
            extra+=fitting(c,r,n,P,T,keep,fc,UV,uv2)
        elif name=='Diver' and np.linalg.norm(ctr-[.096,.028,.405])<.02:
            # a thin cable ran into the old disc from the right; cut back past its flared tip, it now bends into
            # the neck (a short sleeve of cable diving under the suit)
            add(cyc,rim,'harmonic',DARK)
            c,r,n=circfit(q); ax=n if n[0]<0 else -n
            extra+=cable_end(c,min(r,.0095),ax,P,T,keep,fc,UV,uv2)
        else:
            # the clip ring, the earpiece stalk, the end of a cord: flat dark plugs
            add(cyc,rim,'harmonic',DARK)
    return caps,extra

def cable_end(c,r,ax,P,T,keep,fc,UV,uv2):
    """the cut cable continues a little toward the throat, then bends back and ends inside the neck"""
    ax=normalize(ax); back=np.array([0,0,-1.]); down=np.array([0,-1.,0])
    path=bezier(c-ax*.002,c+ax*.012,c+ax*.022+back*.012,c+ax*.026+back*.034+down*.004,16)
    V,TT=tube(path,r*.98,seg=12,caps=True)
    cand=np.where(keep)[0]; cen=P[T[cand]].mean(1); m=np.linalg.norm(cen-c,axis=1)<.06; cand=cand[m]
    f=cand[np.argmin(np.abs(fc[cand]-np.array(DARK)).max(1)+np.linalg.norm(P[T[cand]].mean(1)-c,axis=1)*.01)]
    vs=np.unique(T[keep]); bones=vs[np.linalg.norm(P[vs]-c,axis=1)<.05]
    return [(V,TT,(UV[T[f]].mean(0),uv2[T[f]].mean(0)),bones)]

def fitting(c,r,n,P,T,keep,fc,UV,uv2):
    """an inflator-style end on the cream tube: dark crimp over the cut end, short dark body, grey push button"""
    ax=-n if n[0]<0 else n      # pointing out of the tube (+x)
    vs=np.unique(T[keep]); V=P[vs]; d=V-c; h=d@ax; rad=np.linalg.norm(d-np.outer(h,ax),axis=1)
    near=(h>-.02)&(h<.002)&(rad<r*1.6)
    rc=r*1.13
    def face_uv(target):
        cand=np.where(keep)[0]; cen=P[T[cand]].mean(1); m=np.linalg.norm(cen-c,axis=1)<.25; cand=cand[m]
        f=cand[np.argmin(np.abs(fc[cand]-np.array(target)).max(1)+np.linalg.norm(P[T[cand]].mean(1)-c,axis=1)*.01)]
        return UV[T[f]].mean(0),uv2[T[f]].mean(0)
    dark=face_uv(DARK); grey=face_uv((150,150,158))
    bones=vs[np.linalg.norm(V-c,axis=1)<.06]
    crimp=revolve(c,[(r*.97,-.024),(rc*.95,-.024),(rc,-.020),(rc,.003),(rc*.94,.007),(r*.8,.007)],ax,nu=40)
    body=revolve(c,[(0,.056),(r*.5,.055),(r*.75,.050),(r*.88,.042),(r*.92,.032),(r*.92,.004),(r*.86,-.004)],ax,nu=40)
    btn=revolve(c+ax*.054,[(0,.008),(r*.42,.0075),(r*.5,.005),(r*.52,-.003)],ax,nu=28)
    return [(crimp[0],crimp[1],dark,bones),(body[0],body[1],dark,bones),(btn[0],btn[1],grey,bones)]

def build(name):
    M=load(name); P,N,UV,BI,BW,T=M['P'],M['N'],M['UV'],M['BI'],M['BW'],M['T']; uv2=load_uv2(name)
    head=M['bones'].index('Head'); at=atlas(name); fc=face_colours(M,at)
    info=json.load(open('/tmp/hose_info.json'))[name]
    repaint=np.zeros(len(T),bool)
    if name=='Diver':
        lab=np.load('/tmp/yel_lab.npy'); hose=lab==2; disc=lab==28
        # (and the flared tip of the thin cable that ran into the old disc)
        dele=hose|disc|((lab==21)&(P[T].mean(1)[:,0]<.095))
        # slivers of the old disc's dark rim stay fused to the collar's underside: repainted cream (copied onto new
        # vertices with the collar's colour), otherwise they show as dark cracks round the cap
        repaint=np.isin(lab,[67,68,69,74,94,95,96,103,104,173])
        mouth=dict(y=.205,zf=.515,s=1.12,r=.026)
    elif name=='Diver_Drone':
        seg=np.load('/tmp/seg_Diver_Drone.npy'); hose=seg==11; disc=np.isin(seg,[16,17]); dele=hose|disc|(seg==46)
        mouth=dict(y=.212,zf=.385,s=.95,r=.023)
    else:
        lab=components(P,T); fl=lab[T[:,0]]; hose=fl==24; disc=np.isin(fl,[16,18]); dele=hose|disc
        mouth=dict(y=.305,zf=.247,s=1.0,r=.022)
    keep=~dele
    # colours: black from the old hose, grey from the old regulator's face
    def face_uv(mask,target):
        cand=np.where(mask)[0]; d=np.abs(fc[cand]-np.array(target)).max(1); f=cand[np.argmin(d)]
        return UV[T[f]].mean(0), uv2[T[f]].mean(0)
    black_uv,black_uv2=face_uv(hose,(62,57,62))
    grey_cands=disc&(fc.mean(1)>80)
    grey_uv,grey_uv2=face_uv(grey_cands if grey_cands.any() else disc,(150,150,158))
    parts=[]   # (V,T,uv,uv2)
    # ---- holes left by the old shell: each capped with a membrane in one colour taken from its rim
    caps,extra=cap_all(name,P,N,T,keep,fc,UV,uv2)
    # ---- the regulator
    s=mouth['s']; r=mouth['r']
    a,b,d=.085*s,.052*s,.042*s
    C=np.array([0,mouth['y'],mouth['zf']-.018*s+d])
    body=superellipsoid(C,a,b,d)
    R=.044*s; front=C+np.array([0,0,d-.006*s])
    button=revolve(front,[(0,.019*s),(.35*R,.0185*s),(.7*R,.016*s),(.92*R,.010*s),(R,.003*s),(R,-.004*s)],(0,0,1))
    ring=revolve(front,[(R*.97,.012*s),(R*1.06,.0135*s),(R*1.15,.011*s),(R*1.2,.004*s),(R*1.2,-.004*s)],(0,0,1))
    tee=superellipsoid(C+np.array([0,-b*.92,d*.15]),.075*s,.02*s,.034*s,e1=.7,e2=.7,nu=32,nv=14)
    port_c=C+np.array([a*.8,0,0]); port_end=C+np.array([a+.028*s,0,0])
    port=revolve(port_c,[(0,.028*s+a*.2),(r*1.25,.028*s+a*.2),(r*1.3,.02*s),(r*1.3,0)],(1,0,0),nu=20)
    # ---- the hose: from the old hose's end under the earpiece, along the cheek, into the side port
    A=np.array(info['top']); dA=normalize(np.array(info['dir'])); Q=port_end
    ch=np.linalg.norm(Q-A)
    path=bezier(A-dA*.01,A+dA*ch*.35,Q+np.array([1,0,0])*ch*.38,Q,48)
    # keep it off the face: push samples out along the nearest surface normal
    hv=np.unique(T[keep]); hv=hv[(P[hv,1]>mouth['y']-.25)&(P[hv,2]>-.2)]
    tree=cKDTree(P[hv])
    for it in range(30):
        _,ix=tree.query(path); q=P[hv[ix]]; nq=normalize(N[hv[ix]])
        gap=((path-q)*nq).sum(1); need=r+.006-gap
        push=np.where(need>0,need,0)[:,None]*nq
        push[:2]=0; push[-3:]=0
        path=path+push
        sm=path.copy(); sm[1:-1]=(path[:-2]+2*path[1:-1]+path[2:])/4; sm[:2]=path[:2]; sm[-3:]=path[-3:]; path=sm
    hoseV,hoseT=tube(path,r)
    for (V,TT),col in ((body,'black'),(button,'grey'),(ring,'black'),(tee,'black'),(port,'black'),((hoseV,hoseT),'black')):
        parts.append((V,TT,col))
    # ---- assemble: original vertices stay (unused ones too, so indices never move), new ones appended
    Vs=[P];Ns=[N];UVs=[UV];UV2s=[uv2];BIs=[BI];BWs=[BW];Ts=[T[keep&~repaint]]; base=len(P)
    hw_i=np.zeros(4,int); hw_i[0]=head; hw_w=np.array([1.,0,0,0])
    for V,TT,col in parts:
        n=len(V); Vs.append(V); Ns.append(vnormals(V,TT)); Ts.append(TT+base)
        u,u2=(black_uv,black_uv2) if col=='black' else (grey_uv,grey_uv2)
        UVs.append(np.tile(u,(n,1))); UV2s.append(np.tile(u2,(n,1))); BIs.append(np.tile(hw_i,(n,1))); BWs.append(np.tile(hw_w,(n,1)))
        base+=n
    if repaint.any():
        # repainted faces: the same corners on new vertices, coloured like the collar
        f=np.where(repaint)[0]; idx=T[f].reshape(-1); n=len(idx)
        cu,cu2=[cp for cp in caps if cp['colour']==CREAM][0]['uv'],[cp for cp in caps if cp['colour']==CREAM][0]['uv2']
        Vs.append(P[idx]);Ns.append(N[idx]);UVs.append(np.tile(cu,(n,1)));UV2s.append(np.tile(cu2,(n,1)));BIs.append(BI[idx]);BWs.append(BW[idx])
        Ts.append(np.arange(n).reshape(-1,3)+base); base+=n
    for cp in caps:
        V,TT,src,m=cp['V'],cp['T'],cp['src'],cp['m']
        Vf=V[TT].reshape(-1,3)
        # normals: the rim keeps the original surface's, the inside blends them (a plug shades like the surface
        # round it instead of showing its outline); blended with the cap's own shape a little
        Nh=harmonic_normals(V,TT,m,normalize(N[src])); Ng=vnormals(V,TT)
        Nc=normalize(Nh*.8+Ng*.2); Nc[:m]=normalize(N[src])
        Nf=Nc[TT].reshape(-1,3)
        n=len(Vf); Uf=np.tile(cp['uv'],(n,1)); U2f=np.tile(cp['uv2'],(n,1))
        Bi,Bw=blend_weights(Vf,P[src],BI[src],BW[src])
        Vs.append(Vf);Ns.append(Nf);UVs.append(Uf);UV2s.append(U2f);BIs.append(Bi);BWs.append(Bw)
        Ts.append(np.arange(n).reshape(-1,3)+base); base+=n
    for V,TT,(u,u2),bones in extra:
        n=len(V); Vs.append(V); Ns.append(vnormals(V,TT)); Ts.append(TT+base)
        UVs.append(np.tile(u,(n,1))); UV2s.append(np.tile(u2,(n,1)))
        Bi,Bw=blend_weights(V,P[bones],BI[bones],BW[bones]); BIs.append(Bi); BWs.append(Bw); base+=n
    out=dict(P=np.vstack(Vs),N=np.vstack(Ns),UV=np.vstack(UVs),UV2=np.vstack(UV2s),BI=np.vstack(BIs).astype(np.int32),BW=np.vstack(BWs),T=np.vstack(Ts).astype(np.int32),
             bones=M['bones'],orig=dict(nv=len(P),checksum=float(np.round(P.astype(np.float32).astype(float).sum(),3))),
             caps=len(caps),path=path,C=C,mouth=mouth)
    return out

def write_raw(out,path):
    import struct
    with open(path,'wb') as f:
        nv=len(out['P']); nt=out['T'].size
        f.write(b'DREG'); f.write(struct.pack('<iiid',1,out['orig']['nv'],nv,out['orig']['checksum'])); f.write(struct.pack('<i',nt))
        f.write(out['P'].astype('<f4').tobytes()); f.write(out['N'].astype('<f4').tobytes()); f.write(out['UV'].astype('<f4').tobytes()); f.write(out['UV2'].astype('<f4').tobytes())
        f.write(out['BI'].astype('<i4').tobytes()); f.write(out['BW'].astype('<f4').tobytes()); f.write(out['T'].astype('<i4').tobytes())
