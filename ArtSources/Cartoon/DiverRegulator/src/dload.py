import numpy as np, json
U='/mnt/user-data/uploads/D:--Demo/Temp/ClaudeQA/diverreg/'
A='/mnt/user-data/uploads/D:--Demo/Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment/'
def load(name):
    b=open(U+name+'.raw','rb').read(); nv,nt=np.frombuffer(b,'<i4',2,0); o=8
    P=np.frombuffer(b,'<f4',nv*3,o).reshape(-1,3).astype(float); o+=nv*12
    N=np.frombuffer(b,'<f4',nv*3,o).reshape(-1,3).astype(float); o+=nv*12
    UV=np.frombuffer(b,'<f4',nv*2,o).reshape(-1,2).astype(float); o+=nv*8
    rec=np.frombuffer(b,np.dtype([('i','<i4',4),('w','<f4',4)]),nv,o); o+=nv*32
    T=np.frombuffer(b,'<i4',nt,o).reshape(-1,3).copy()
    info=json.load(open(U+name+'.json'))
    return dict(P=P,N=N,UV=UV,BI=rec['i'].copy(),BW=rec['w'].astype(float),T=T,bones=info['bones'],bind=np.array(info['bind']).reshape(-1,4,4).transpose(0,2,1) if False else np.array(info['bind']),tex=info['tex'])
def atlas(name):
    from PIL import Image
    f={'Diver':'Diver_Atlas.png','Diver_Tracker':'Diver_Tracker_Atlas.png','Diver_Drone':'Diver_Drone_Atlas.png'}[name]
    return np.asarray(Image.open(A+f).convert('RGB')).astype(float)
def weld(P,eps=1e-5):
    key=np.round(P/eps).astype(np.int64); _,w=np.unique(key,axis=0,return_inverse=True); return w.ravel()
def components(P,T):
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    w=weld(P); n=w.max()+1; r=np.r_[T[:,0],T[:,1],T[:,2]]; c=np.r_[T[:,1],T[:,2],T[:,0]]
    _,lab=connected_components(coo_matrix((np.ones(len(r)),(w[r],w[c])),shape=(n,n)),directed=False)
    return lab[w]   # per vertex
def render(M,cols,eye,target,up=(0,1,0),size=(800,800),fov=30,light=(0.3,0.5,1)):
    from PIL import Image
    P,T=M['P'],M['T']
    f=np.array(target,float)-np.array(eye,float); f/=np.linalg.norm(f); r=np.cross(f,up); r/=np.linalg.norm(r); u=np.cross(r,f)
    rel=P-np.array(eye,float); x=rel@r; y=rel@u; z=rel@f
    W,H=size; fl=H/2/np.tan(np.radians(fov/2)); sx=W/2+x/z*fl; sy=H/2-y/z*fl
    img=np.zeros((H,W,3))+np.array([30,50,60]); zb=np.full((H,W),np.inf)
    L=np.array(light,float); L/=np.linalg.norm(L)
    fn=np.cross(P[T[:,1]]-P[T[:,0]],P[T[:,2]]-P[T[:,0]]); fn/=np.linalg.norm(fn,axis=1,keepdims=True)+1e-12
    vis=(fn@(-f))>0
    shade=np.clip(.35+.65*np.abs(fn@L),0,1)
    order=np.where(vis&(z[T].min(1)>.01))[0]
    for k in order:
        t=T[k]; xs=sx[t]; ys=sy[t]; zs=z[t]
        x0=int(max(0,np.floor(xs.min()))); x1=int(min(W-1,np.ceil(xs.max()))); y0=int(max(0,np.floor(ys.min()))); y1=int(min(H-1,np.ceil(ys.max())))
        if x1<x0 or y1<y0: continue
        gx,gy=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
        d=(xs[1]-xs[0])*(ys[2]-ys[0])-(xs[2]-xs[0])*(ys[1]-ys[0])
        if abs(d)<1e-9: continue
        l1=((gx-xs[0])*(ys[2]-ys[0])-(xs[2]-xs[0])*(gy-ys[0]))/d; l2=((xs[1]-xs[0])*(gy-ys[0])-(gx-xs[0])*(ys[1]-ys[0]))/d; l0=1-l1-l2
        m=(l0>=0)&(l1>=0)&(l2>=0)
        if not m.any(): continue
        zz=l0*zs[0]+l1*zs[1]+l2*zs[2]
        yy,xx=np.where(m); zz=zz[m]; Y=yy+y0; X=xx+x0
        ok=zz<zb[Y,X]; Y,X,zz=Y[ok],X[ok],zz[ok]; zb[Y,X]=zz
        c=cols[k] if cols.ndim==2 and len(cols)==len(T) else cols[t].mean(0)
        img[Y,X]=c*shade[k]
    return Image.fromarray(np.clip(img,0,255).astype(np.uint8))
def face_colours(M,at):
    H,W=at.shape[:2]; uv=M['UV'][M['T']].mean(1)
    return at[((1-uv[:,1])*H).astype(int).clip(0,H-1),(uv[:,0]*W).astype(int).clip(0,W-1)]
def load_uv2(name):
    b=open(U+name+'.uv2','rb').read(); n=np.frombuffer(b,'<i4',1,0)[0]
    return np.frombuffer(b,'<f4',n*2,4).reshape(-1,2).astype(float)
def hole_loops(P,T,keep):
    w=weld(P)
    def bed(mask):
        E={}
        for t in w[T[mask]]:
            for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])):
                k=(min(a,b),max(a,b)); E[k]=E.get(k,0)+1
        return {k for k,v in E.items() if v==1}
    new=bed(keep)-bed(np.ones(len(T),bool))
    from collections import defaultdict
    adj=defaultdict(list)
    for a,b in new: adj[a].append(b); adj[b].append(a)
    seen=set(); loops=[]
    for v in adj:
        if v in seen: continue
        comp=[];stack=[v];seen.add(v)
        while stack:
            x=stack.pop(); comp.append(x)
            for y in adj[x]:
                if y not in seen: seen.add(y); stack.append(y)
        loops.append(comp)
    return loops,w,new
