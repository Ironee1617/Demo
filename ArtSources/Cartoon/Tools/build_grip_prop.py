"""FPS grip fitted to the broad sketch mitten; preserves the world gun."""
from pathlib import Path
import json,struct,numpy as np
R=Path(__file__).resolve().parent;portable=(R.parent/'Export').is_dir();E=R.parent/'Export' if portable else R.parent/'Aquarium-CartoonReplacement-R1/Export'
d=json.loads((E/'HarpoonGun.json').read_text())
with (E/'HarpoonGun.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18).copy();t=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3);groups=f.read(ni//3*4);shapes=f.read()
def smooth(a,b,x):
 s=np.clip((x-a)/(b-a),0,1);return s*s*(3-2*s)
start=0
for name,n in zip(d['parts'],d['partVertexCounts']):
 p=v[start:start+n,:3];start+=n
 if 'Grip' in name:
  p[:,0]*=.74;p[:,2]=p[:,2]*.40+.60*(-.028+.36*p[:,1])-.005;p[:,1]+=.025*smooth(.04,.095,p[:,1])
 elif 'TriggerGuard' in name:p[:,1]=p[:,1]*.95+.027;p[:,2]=(p[:,2]-.008)*1.32-.014
 elif 'Trigger' in name:p[:,1]+=.030;p[:,2]+=.030
 else:p[:,0]*=1.06;p[:,1]=.142+(p[:,1]-.142)*1.06+.025
# An open U guard uses the grip as its rear wall. The old closed loop left an
# extra bar passing through the sketch's small curled index finger.
assert d['parts'][-1]=='Harpoon_TriggerGuard'
base=sum(d['partVertexCounts'][:-1]);base_tri=sum(d['partTriangleCounts'][:-1]);template=v[base].copy()
control=np.array([[0,.118,.005],[0,.118,.085],[0,.102,.110],[0,.050,.110],[0,.028,.090],[0,.028,-.025]])
centers=[]
for u in np.linspace(0,len(control)-1,65):
 i=min(len(control)-2,int(u));f=u-i;a,b,c,e=[control[min(len(control)-1,max(0,k))] for k in [i-1,i,i+1,i+2]];centers.append(.5*(2*b+(-a+c)*f+(2*a-5*b+4*c-e)*f*f+(-a+3*b-3*c+e)*f**3))
centers=np.array(centers);tangent=np.gradient(centers,axis=0);tangent/=np.linalg.norm(tangent,axis=1,keepdims=True);gv=[];gt=[];sides=12
for i,(center,direction) in enumerate(zip(centers,tangent)):
 side=np.array([1,0,0]);up=np.cross(direction,side)
 for a in np.arange(sides)*2*np.pi/sides:
  row=template.copy();radial=side*np.cos(a)+up*np.sin(a);row[:3]=center+.006*radial;row[3:6]=radial;gv.append(row)
 if i:
  for j in range(sides):
   a=(i-1)*sides+j;b=(i-1)*sides+(j+1)%sides;gt.extend([[a,b,b+sides],[a,b+sides,a+sides]])
for ring,reverse in [(0,True),(len(centers)-1,False)]:
 row=template.copy();row[:3]=centers[ring];k=len(gv);gv.append(row)
 for j in range(sides):
  face=[ring*sides+j,ring*sides+(j+1)%sides,k];gt.append(face[::-1] if reverse else face)
old_cross=np.cross(v[t[:base_tri,1],:3]-v[t[:base_tri,0],:3],v[t[:base_tri,2],:3]-v[t[:base_tri,0],:3]);orientation=np.median(np.sum(old_cross*v[t[:base_tri],3:6].mean(1),axis=1))
gt=np.array(gt,np.int32)
if orientation<0:gt=gt[:,::-1]
v=np.r_[v[:base],np.array(gv,np.float32)];t=np.r_[t[:base_tri],gt+base]
d['partVertexCounts'][-1]=len(gv);d['partTriangleCounts'][-1]=len(gt);nv=len(v);ni=t.size;d['vertices']=nv;d['triangles']=len(t);groups=np.zeros(len(t),'<i4').tobytes();shapes=np.zeros((ns,nv,6),'<f4').tobytes()
unique,inv=np.unique(np.round(v[:,:3],6),axis=0,return_inverse=True);tt=inv[t];normal=np.zeros_like(unique);fn=np.cross(unique[tt[:,1]]-unique[tt[:,0]],unique[tt[:,2]]-unique[tt[:,0]]);fn/=np.maximum(np.linalg.norm(fn,axis=1,keepdims=True),1e-12)
for j in range(3):
 a=unique[tt[:,(j+1)%3]]-unique[tt[:,j]];b=unique[tt[:,(j+2)%3]]-unique[tt[:,j]];a/=np.maximum(np.linalg.norm(a,axis=1,keepdims=True),1e-12);b/=np.maximum(np.linalg.norm(b,axis=1,keepdims=True),1e-12);angle=np.arccos(np.clip(np.sum(a*b,axis=1),-1,1));np.add.at(normal,tt[:,j],fn*angle[:,None])
normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-12)
# Unity's clockwise export has normals opposite the mathematical cross product.
if np.median(np.sum(normal[inv]*v[:,3:6],axis=1))<0:normal=-normal
v[:,3:6]=normal[inv];d['name']='HarpoonGunFPS';d['firstPerson']=True
for key in ['muzzle','ropeExit']:
 p=d[key];p[0]*=1.06;p[1]=.142+(p[1]-.142)*1.06+.025
with (E/'HarpoonGunFPS.cartoon').open('wb') as f:f.write(struct.pack('<4i',nv,ni,ns,nm));f.write(v.tobytes());f.write(t.tobytes());f.write(groups);f.write(shapes)
(E/'HarpoonGunFPS.json').write_text(json.dumps(d,separators=(',',':')))
print('FPS_GRIP_PROP_EXPORTED')
