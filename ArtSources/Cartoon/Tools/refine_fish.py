from pathlib import Path
import struct,json,numpy as np
R=Path(__file__).resolve().parent;E=R.parent/'Export';B=R.parent/'Source/BeforeFluidMotionR14'
d=json.loads((B/'Fish0.json').read_text())
with (B/'Fish0.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18).copy();tri=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3).copy();groups=f.read(ni//3*4);s=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6).copy()
index=d['shapeNames'].index('Alien_Mouth_Open');rest=v[:,:3];amount=.22
material=(v[:,6]*4).astype(int)+4*(v[:,7]*4).astype(int)
def smooth(a,b,x):
 u=np.clip((x-a)/(b-a),0,1);return u*u*(3-2*u)
unit=.004*.62
b=np.stack([rest[:,0]/unit,470-rest[:,2]/unit,540-(rest[:,1]/.62+1.42-1.25)/.004],axis=1)
weights=[];eye_deltas=[];socket_support=[]
for sign in [-1,1]:
 c=np.array([sign*87,365,514.]);target=np.array([sign*165,395,455.]);offset=b-c
 n=np.array([sign*.72,-np.sqrt(1-.72**2),0]);tangent=np.array([sign*np.sqrt(1-.72**2),.72,0])
 distance=np.sqrt((offset@tangent/56.2)**2+(offset[:,2]/63.2)**2)
 weights.append((1-smooth(1.05,1.7,distance))*(1-smooth(60,110,abs(offset@n)))*(b[:,0]*sign>0))
 socket_support.append((1-smooth(1.25,1.9,distance))*(1-smooth(60,110,abs(offset@n)))*(b[:,0]*sign>0))
 delta=target+offset*.76-b;eye_deltas.append(np.stack([delta[:,0],-delta[:,2],-delta[:,1]],axis=1)*unit)
correction=(eye_deltas[0]*weights[0][:,None]+eye_deltas[1]*weights[1][:,None])/np.maximum(1,weights[0]+weights[1])[:,None]
q=rest+s[index,:,:3]*amount
q[material==0]-=correction[material==0]*amount
support=np.maximum(socket_support[0],socket_support[1])*(material==0)
q=rest+(q-rest)*(1-support[:,None])
eye=np.isin(material,[2,5]);q[eye]=rest[eye]
def normals(points):
 unique,inv=np.unique(np.round(points,6),axis=0,return_inverse=True);tt=inv[tri];n=np.zeros_like(unique)
 face=np.cross(unique[tt[:,1]]-unique[tt[:,0]],unique[tt[:,2]]-unique[tt[:,0]])
 face/=np.maximum(np.linalg.norm(face,axis=1,keepdims=True),1e-12)
 for j in range(3):
  a=unique[tt[:,(j+1)%3]]-unique[tt[:,j]];b=unique[tt[:,(j+2)%3]]-unique[tt[:,j]]
  a/=np.maximum(np.linalg.norm(a,axis=1,keepdims=True),1e-12);b/=np.maximum(np.linalg.norm(b,axis=1,keepdims=True),1e-12)
  np.add.at(n,tt[:,j],face*np.arccos(np.clip(np.sum(a*b,axis=1),-1,1))[:,None])
 n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-12);return n[inv]
s[index,:,:3]=q-rest
shading=normals(q)*(1-support[:,None])+v[:,3:6]*support[:,None]
shading/=np.maximum(np.linalg.norm(shading,axis=1,keepdims=True),1e-12)
s[index,:,3:6]=shading-v[:,3:6]
# Preserve eye shading as well as its surface throughout mouth opening.
s[index,eye,3:6]=0
with (E/'Fish0.cartoon').open('wb') as f:
 f.write(struct.pack('<4i',nv,ni,ns,nm));f.write(v.astype('<f4').tobytes());f.write(tri.astype('<i4').tobytes());f.write(groups);f.write(s.astype('<f4').tobytes())
d['throatAnchor'][2]=.30
(E/'Fish0.json').write_text(json.dumps(d,separators=(',',':')))
report=dict(eye_mouth_displacement=float(np.max(abs(s[index,eye,:3]))),eye_swallow_displacement=float(np.max(abs(s[d['shapeNames'].index('Swallow'),eye,:3]))),mouth_radius=float(abs(q[material==4,0]).max()),body_width=float(np.ptp(q[material==0,0])))
(R/'fish-eye-validation.json').write_text(json.dumps(report,indent=2));print(report)
