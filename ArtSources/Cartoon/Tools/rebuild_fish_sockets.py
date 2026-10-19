"""Replace decimated solidified eye collars with regular, smooth annuli.
Run refine_fish.py first; this script deliberately expects that clean baseline.
"""
from pathlib import Path
import bpy,struct,json,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parent;E=R.parent/'Export'
d=json.loads((E/'Fish0.json').read_text())
with (E/'Fish0.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18).copy();tri=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3).copy();groups=np.frombuffer(f.read(ni//3*4),'<i4').copy();s=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6).copy()
m=(v[:,6]*4).astype(int)+4*(v[:,7]*4).astype(int);u=.004*.62
tree=BVHTree.FromPolygons(v[:,:3].tolist(),tri[np.all((m==0)[tri],axis=1)].tolist(),all_triangles=True)
rimtri=tri[np.all((m==5)[tri],axis=1)]
oldrim=BVHTree.FromPolygons(v[:,:3].tolist(),rimtri.tolist(),all_triangles=True)
newpoints=[];newtri=[];angles=96;rows=8
for sign in [-1,1]:
 c=np.array([sign*87*u,(1.25+26*.004-1.42)*.62,105*u]);n=np.array([sign*.72,0,np.sqrt(1-.72**2)]);t=np.array([sign*np.sqrt(1-.72**2),0,-.72]);up=np.array([0,1,0]);offset=len(newpoints)
 for j in range(angles):
  a=2*np.pi*j/angles
  probe=c+t*(63.5*u*np.cos(a))+up*(71*u*np.sin(a))
  hit=tree.ray_cast(Vector(probe+n*2),Vector(-n),4)[0]
  assert hit is not None
  inner=c+n*(31*u*.28+.00025)+t*(51*u*np.sqrt(1-.28**2)*np.cos(a))+up*(57*u*np.sqrt(1-.28**2)*np.sin(a))
  outer=np.array(hit)+n*.0008
  for i in range(rows+1):
   r=i/rows;newpoints.append(inner*(1-r)+outer*r+n*.002*np.sin(np.pi*r))
 for j in range(angles):
  for i in range(rows):
   a=offset+j*(rows+1)+i;b=offset+((j+1)%angles)*(rows+1)+i
   for tr in [[a,b,b+1],[a,b+1,a+1]]:
    p=np.asarray([newpoints[k] for k in tr]);normal=np.cross(p[1]-p[0],p[2]-p[0])
    if normal@n<0:tr.reverse()
    newtri.append(tr)
p=np.asarray(newpoints);tt=np.asarray(newtri,np.int32)
newv=np.zeros((len(p),18),np.float32);newv[:,:3]=p;newv[:,6:8]=.375;newv[:,9]=.57;newv[:,10]=1;newv[:,14]=1
news=np.zeros((ns,len(p),6),np.float32)
# Transfer authored collapse targets onto new topology, retaining exactly fixed eyes.
for k,pt in enumerate(p):
 hit=oldrim.find_nearest(Vector(pt));ids=rimtri[hit[2]];a,b,c=v[ids,:3];ab=b-a;ac=c-a;ap=np.array(hit[0])-a
 d00=ab@ab;d01=ab@ac;d11=ac@ac;d20=ap@ab;d21=ap@ac;den=d00*d11-d01*d01
 wb=(d11*d20-d01*d21)/max(den,1e-20);wc=(d00*d21-d01*d20)/max(den,1e-20);w=np.clip([1-wb-wc,wb,wc],0,1);w/=w.sum()
 for i,name in enumerate(d['shapeNames']):
  if name.startswith('To_Matched_'):
   amount=int(name.rsplit('_',1)[-1])/1000;fade=amount*amount*(3-2*amount)
   target=np.sum((v[ids,:3]+s[i,ids,:3])*w[:,None],axis=0)
   news[i,k,:3]=target-pt+(pt-np.sum(v[ids,:3]*w[:,None],axis=0))*(1-fade)
def normals(points):
 normal=np.zeros_like(points);f=np.cross(points[tt[:,1]]-points[tt[:,0]],points[tt[:,2]]-points[tt[:,0]])
 for j in range(3):np.add.at(normal,tt[:,j],f)
 normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-12);return normal
newv[:,3:6]=normals(p)
for i in range(ns):news[i,:,3:6]=normals(p+news[i,:,:3])-newv[:,3:6]
keep=m!=5;mapping=np.cumsum(keep)-1;faces=np.all(keep[tri],axis=1);basecount=int(keep.sum())
v=np.concatenate([v[keep],newv]);s=np.concatenate([s[:,keep],news],axis=1)
tri=np.concatenate([mapping[tri[faces]],tt+basecount]);groups=np.concatenate([groups[faces],np.zeros(len(tt),np.int32)])
with (E/'Fish0.cartoon').open('wb') as f:
 f.write(struct.pack('<4i',len(v),tri.size,ns,nm));f.write(v.astype('<f4').tobytes());f.write(tri.astype('<i4').tobytes());f.write(groups.astype('<i4').tobytes());f.write(s.astype('<f4').tobytes())
d['vertices']=len(v);d['triangles']=len(tri);(E/'Fish0.json').write_text(json.dumps(d,separators=(',',':')))
print('FISH_SOCKET_TOPOLOGY',dict(vertices=len(v),triangles=len(tri),collar_vertices=len(p)),flush=True)
