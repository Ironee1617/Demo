"""Fill the holes left after deleting faces: one fan of concentric rings per hole, faired with a bi-Laplacian
so it continues the curvature of the surface around it, wound like its neighbours, and painted per face with
the colour of the nearest face along the hole's rim (never with something seen through the hole)."""
import numpy as np
from collections import defaultdict
from scipy.sparse import lil_matrix, csr_matrix
from scipy.sparse.linalg import lsqr
from dload import weld

def boundary_cycles(P,T,keep):
    w=weld(P)
    cnt=defaultdict(int); cnt0=defaultdict(int); owner={}
    for f,t in enumerate(w[T]):
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])):
            k=(min(a,b),max(a,b)); cnt0[k]+=1
            if keep[f]: cnt[k]+=1; owner[(a,b)]=f
    bd=[(a,b) for (a,b),f in owner.items() if cnt[(min(a,b),max(a,b))]==1 and cnt0[(min(a,b),max(a,b))]>=2]
    nxt=defaultdict(list)
    for a,b in bd: nxt[a].append(b)
    wp={}
    for i,k in enumerate(w): wp.setdefault(k,P[i])
    used=set(); cycles=[]
    for a,b in bd:
        if (a,b) in used: continue
        cyc=[a]; used.add((a,b)); prev=a; cur=b
        while cur!=a:
            cands=[x for x in nxt[cur] if (cur,x) not in used]
            if not cands: cyc=None; break
            n=cands[0]; used.add((cur,n)); cyc.append(cur); prev=cur; cur=n
        if not cyc: continue
        rim=[owner[(cyc[i],cyc[(i+1)%len(cyc)])] for i in range(len(cyc))]
        todo=[(cyc,rim)]
        while todo:   # holes touching at one vertex: split the walk into its loops
            c,r=todo.pop(); seen={}; cut=None
            for i,k in enumerate(c):
                if k in seen: cut=(seen[k],i); break
                seen[k]=i
            if cut is None: cycles.append((c,r)); continue
            i,j=cut; todo+=[(c[i:j],r[i:j]),(c[:i]+c[j:],r[:i]+r[j:])]
    return w,wp,cycles

def split(cyc,rim,i,j):
    """cut one hole along the chord cyc[i]-cyc[j] (i<j): two holes sharing that chord"""
    return (cyc[i:j+1],rim[i:j]+[None]),(cyc[j:]+cyc[:i+1],rim[j:]+rim[:i]+[None])

def fill_loop(cyc,wp,nb,worig,mode='bilaplace',rings=None):
    """cyc: welded ids in the kept faces' edge order (the cap runs the other way). mode: bilaplace (continues the
    curvature round it), harmonic (a taut, flat membrane), none"""
    cyc=cyc[::-1]
    B=np.array([wp[k] for k in cyc]); m=len(B)
    # clip sharp convex corners first (a jagged rim would fold the first ring over itself)
    loop=list(range(m)); ears=[]
    npoly=np.zeros(3)
    for i in range(m): a,b=B[i],B[(i+1)%m]; npoly+=np.array([(a[1]-b[1])*(a[2]+b[2]),(a[2]-b[2])*(a[0]+b[0]),(a[0]-b[0])*(a[1]+b[1])])
    npoly/=np.linalg.norm(npoly)+1e-15
    changed=True
    while changed and len(loop)>6:
        changed=False
        for j in range(len(loop)):
            i0,i1,i2=loop[j-1],loop[j],loop[(j+1)%len(loop)]
            u=B[i0]-B[i1]; v=B[i2]-B[i1]
            ang=np.degrees(np.arccos(np.clip(np.dot(u,v)/(np.linalg.norm(u)*np.linalg.norm(v)+1e-15),-1,1)))
            convex=np.dot(np.cross(B[i1]-B[i0],B[i2]-B[i1]),npoly)>0
            if convex and ang<55:
                ears.append((i0,i1,i2)); loop.pop(j); changed=True; break
    L=np.array(loop); ml=len(L); c=B[L].mean(0)
    R=rings or int(np.clip(round(ml/9),3,12))
    V=np.vstack([B]+[c+(B[L]-c)*(1-r/R) for r in range(1,R)]+[c[None]])
    def rv(r,i): return L[i%ml] if r==0 else m+(r-1)*ml+(i%ml)
    TT=list(ears)
    for r in range(R-1):
        for i in range(ml):
            a=rv(r,i); b=rv(r,i+1); cc=rv(r+1,i+1); d=rv(r+1,i)
            TT+=[(a,b,cc),(a,cc,d)]
    ctr=len(V)-1
    for i in range(ml): TT.append((rv(R-1,i),rv(R-1,i+1),ctr))
    TT=np.array(TT)
    capnb=[set() for _ in range(len(V))]
    for t in TT:
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])): capnb[a].add(b); capnb[b].add(a)
    if mode!='none':
        free=np.arange(m,len(V))
        if mode=='harmonic':
            outer=[]; X=V.copy(); rows=[(i,list(capnb[i])) for i in free]
        else:
            outer=sorted({x for k in cyc for x in nb[k]}-set(cyc)); oidx={k:i for i,k in enumerate(outer)}
            X=np.vstack([V]+([np.array([wp[k] for k in outer])] if outer else []))
            pos={k:i for i,k in enumerate(cyc)}
            rows=[]
            for i in range(len(V)):
                nbs=set(capnb[i])
                if i<m: nbs|={len(V)+oidx[x] for x in nb[cyc[i]] if x in oidx}|{pos[x] for x in nb[cyc[i]] if x in pos}
                rows.append((i,sorted(nbs)))
        Lm=lil_matrix((len(rows),len(X)))
        for r,(i,nbs) in enumerate(rows):
            Lm[r,i]=1.0
            for j in nbs: Lm[r,j]-=1.0/len(nbs)
        Lm=csr_matrix(Lm); fixed=np.setdiff1d(np.arange(len(X)),free)
        A=Lm[:,free]; b=-(Lm[:,fixed]@X[fixed])
        for k in range(3): X[free,k]=lsqr(A,b[:,k],atol=1e-12,btol=1e-12,iter_lim=50000)[0]
        V=X[:len(V)]
    return dict(V=V,T=TT,m=m,src=np.array([worig[k] for k in cyc]),cyc=cyc)

def context(P,T,keep):
    w,wp,cycles=boundary_cycles(P,T,keep)
    worig={}
    for i,k in enumerate(w): worig.setdefault(k,i)
    nb=defaultdict(set)
    for t in w[T[keep]]:
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])): nb[a].add(b); nb[b].add(a)
    return w,wp,cycles,worig,nb
