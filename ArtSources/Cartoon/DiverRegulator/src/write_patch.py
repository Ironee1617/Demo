"""DREG v2 patch: the original mesh is checked (vertex count + position sum, triangle count + index sum), its
deleted triangles are listed, and the new vertices / triangles are appended after the original ones."""
import sys; sys.path.insert(0,'/home/claude/divers')
import numpy as np, pickle, struct, os
from dload import load
def tri_checksum(T):
    t=T.reshape(-1).astype(np.int64); return int((t*((np.arange(len(t))%13)+1)).sum())
def write(name,path):
    o=pickle.load(open('/tmp/reg_%s.pkl'%name,'rb')); M=load(name); T0=M['T']; n0=len(M['P'])
    assert o['orig']['nv']==n0
    # which original triangles survive (the build keeps T[keep] in order at the front)
    nkeep=None
    Tn=o['T']; keepset=set(map(tuple,Tn[:len(T0)].tolist()))
    # recover the keep mask by walking the original list in order
    j=0; keep=np.zeros(len(T0),bool)
    for i,t in enumerate(T0):
        if j<len(Tn) and tuple(Tn[j])==tuple(t): keep[i]=True; j+=1
    nk=keep.sum(); assert (Tn[:nk]==T0[keep]).all()
    deleted=np.where(~keep)[0].astype(np.int32); add=Tn[nk:]
    assert np.allclose(o['P'][:n0],M['P'],atol=1e-6) and np.allclose(o['UV'][:n0],M['UV'],atol=1e-6)
    P=o['P'][n0:]; N=o['N'][n0:]; UV=o['UV'][n0:]; UV2=o['UV2'][n0:]; BI=o['BI'][n0:]; BW=o['BW'][n0:]
    with open(path,'wb') as f:
        f.write(b'DREG'); f.write(struct.pack('<i',2))
        f.write(struct.pack('<idiq',n0,o['orig']['checksum'],len(T0),tri_checksum(T0)))
        f.write(struct.pack('<i',len(deleted))); f.write(deleted.astype('<i4').tobytes())
        f.write(struct.pack('<i',len(P)))
        for a,dt in ((P,'<f4'),(N,'<f4'),(UV,'<f4'),(UV2,'<f4'),(BI,'<i4'),(BW,'<f4')): f.write(np.ascontiguousarray(a).astype(dt).tobytes())
        f.write(struct.pack('<i',len(add))); f.write(add.astype('<i4').tobytes())
    print(name,'deleted',len(deleted),'added verts',len(P),'added tris',len(add),'bytes',os.path.getsize(path),'tri checksum',tri_checksum(T0))
os.makedirs('/mnt/user-data/outputs/DiverRegulator',exist_ok=True)
for n in ('Diver','Diver_Tracker','Diver_Drone'): write(n,'/mnt/user-data/outputs/DiverRegulator/%s.dreg'%n)
