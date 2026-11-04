import numpy as np
def raycast(P,T,O,D):
    """front-most hit of rays O+tD; returns t, face, bary (u,v)"""
    A=P[T[:,0]];B=P[T[:,1]];C=P[T[:,2]]; e1=B-A; e2=C-A
    out_t=np.full(len(O),np.nan); out_f=np.full(len(O),-1); out_u=np.zeros(len(O)); out_v=np.zeros(len(O))
    for i,(o,d) in enumerate(zip(O,D)):
        pv=np.cross(d,e2); det=(e1*pv).sum(1); inv=1/np.where(np.abs(det)<1e-12,1e-12,det)
        tv=o-A; u=(tv*pv).sum(1)*inv; q=np.cross(tv,e1); v=(d[None]*q).sum(1)*inv; t=(e2*q).sum(1)*inv
        m=(u>=0)&(v>=0)&(u+v<=1)&(t>0)
        if m.any():
            k=np.where(m)[0][np.argmin(t[m])]; out_t[i]=t[k]; out_f[i]=k; out_u[i]=u[k]; out_v[i]=v[k]
    return out_t,out_f,out_u,out_v
