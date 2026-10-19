"""Compress the neck, retain the exact shared morph, and author the lower maw."""
from pathlib import Path
import numpy as np,json,struct
R=Path(__file__).resolve().parent;portable=(R.parent/'Export/Alien.json').is_file();E=R.parent/'Export' if portable else R.parent/'Aquarium-CartoonReplacement-R1/Export'
source=R.parent/'Source/PrePolish' if portable else R/'BeforeExport';d=json.loads((source/'Alien.json').read_text())
with (source/'Alien.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18).copy();t=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3);groups=np.frombuffer(f.read(ni//3*4),'<i4');shapes=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6).copy()
def ss(a,b,x):
 q=np.clip((x-a)/(b-a),0,1);return q*q*(3-2*q)
def compact(p):
 q=p.copy();q[:,1]+=.025-.115*ss(-.24,.08,p[:,1]);return q
def normals(p):
 # Weld UV duplicates for a smooth continuous surface normal.
 points,inv=np.unique(np.round(p,6),axis=0,return_inverse=True);tri=inv[t];n=np.zeros_like(points);face=np.cross(points[tri[:,1]]-points[tri[:,0]],points[tri[:,2]]-points[tri[:,0]])
 for j in range(3):np.add.at(n,tri[:,j],face)
 n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-12);return n[inv]
base=v[:,:3].copy();new=compact(base);nn=normals(new);oldnormal=v[:,3:6].copy()
material=np.floor(v[:,6]*4).astype(int)+np.floor(v[:,7]*4).astype(int)*4
for i,name in enumerate(d['shapeNames']):
 target=base+shapes[i,:,:3]
 fold=int(name.rsplit('_',1)[1])/1000 if name.startswith('To_Matched_') else 0
 # The final midpoint must remain bit-identical; intermediate surfaces relax
 # back to it without changing topology or UV correspondence.
 target+=(compact(target)-target)*(1-fold)
 if name.startswith('Blink_'):
  blink=int(name.split('_')[1])/100
  target[np.isin(material,[5,6]),2]-=.018*blink
  for mat,offset in [(7,.006),(8,-.006)]:
   ids=material==mat;q=target[ids];rad=(q[:,0]/.3131)**2+((q[:,1]-.1858)/.2895)**2
   target[ids,2]=.230+.160*np.sqrt(np.maximum(0,1-rad))+offset
 targetnorm=normals(target)
 if name.startswith('Blink_'):
  # The old reduced eyelids inherited corrugated normals from near-coincident
  # inner/outer source sheets. Their opening now stays on one smooth dome.
  for mat,sign in [(7,1),(8,-1)]:
   ids=material==mat;q=target[ids];normal=np.stack([q[:,0]/(.3131**2),(q[:,1]-.1858)/(.2895**2),(q[:,2]-.230)/(.160**2)],1)
   normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-8);targetnorm[ids]=normal*sign
 shapes[i,:,:3]=target-new;shapes[i,:,3:]=targetnorm-nn
v[:,:3]=new;v[:,3:6]=nn
for b in d['bones']:
 b['world']=compact(np.array([b['world']],float))[0].tolist() if b['name'] not in ['Root','Body'] else b['world']
for b in d['bones']:
 b['position']=(np.array(b['world'])-(np.array(d['bones'][b['parent']]['world']) if b['parent']>=0 else 0)).tolist()
for key in ['mouthAnchor','throatAnchor']:d[key]=compact(np.array([d[key]],float))[0].tolist()
maw=new.copy();offset=0;mouth=np.array(d['mouthAnchor']);axis=np.array(d['mouthAnchor'])-np.array(d['throatAnchor']);axis/=np.linalg.norm(axis)
delta=new-mouth;axial=delta@axis;radial=delta-axial[:,None]*axis;radius=np.linalg.norm(radial,axis=1)
influence=(1-ss(.14,.30,radius))*(1-ss(.05,.14,np.abs(axial)))
maw+=radial*(.70*influence[:,None])+axis*(.014*influence[:,None])
shapes=np.concatenate([shapes,np.concatenate([maw-new,normals(maw)-nn],axis=1)[None]],axis=0);d['shapeNames'].append('BellyOpen')
# Rebuild the two eyelid sheets parametrically. A transferred, nearly collapsed
# ring had crossing strips at full closure; a shared meridian closes cleanly.
oldlid=np.isin(material,[7,8]);lidbase=v[oldlid,:3].copy();oldstates=[lidbase+s[oldlid,:3] for s in shapes]
lidv=[];lidn=[];liduv=[];lidtri=[];lidshapes=[[] for _ in d['shapeNames']]
for side in [1,-1]:
 params=[(0.,0.)]+[(theta,u) for theta in np.linspace(0,np.pi,65)[1:-1] for u in np.linspace(0,1,13)]+[(np.pi,0.)]
 count=len(params);start=len(lidv)
 def sheet(blink,inner):
  coords=[];ns=[]
  for theta,u in params:
   x=.3131*np.cos(theta);y=side*.2895*np.sin(theta)*(1-(.018+.982*blink)*u)
   z=.160*np.sqrt(max(0,1-(x/.3131)**2-(y/.2895)**2))
   normal=np.array([x/.3131**2,y/.2895**2,z/.160**2]);normal/=max(np.linalg.norm(normal),1e-8)
   p=np.array([x,y+.1858,z+.230])-(.006*normal if inner else 0)
   coords.append(p);ns.append(normal*(-1 if inner else 1))
  return np.array(coords),np.array(ns)
 for inner in [False,True]:
  p,n=sheet(0,inner);lidv.extend(p);lidn.extend(n);mat=8 if inner else 7;liduv.extend([[((mat%4)+.5)/4,((mat//4)+.5)/4]]*count)
  for k,name in enumerate(d['shapeNames']):
   if name.startswith('Blink_'):q,qn=sheet(int(name.split('_')[1])/100,inner)
   elif name.startswith('To_Matched_'):
    affine=np.linalg.lstsq(np.c_[lidbase,np.ones(len(lidbase))],oldstates[k],rcond=None)[0];q=np.c_[p,np.ones(len(p))]@affine;qn=n
   else:q,qn=p,n
   lidshapes[k].extend(np.concatenate([q-p,qn-n],axis=1))
 # Front patches run from the rim toward the closing meridian.
 surf=[]
 for j in range(62):
  for u in range(12):
   a=1+j*13+u;b=a+13;surf.extend([[a,b,b+1],[a,b+1,a+1]])
 for u in range(12):surf.extend([[0,1+u,2+u],[count-1,count-14+u+1,count-14+u]])
 for inner in [0,1]:
  for tr in surf:
   a=np.array(tr)+start+inner*count
   if (side<0)^bool(inner):a=a[::-1]
   lidtri.append(a)
 # Outer rim and opening edge are joined to the inset inner surface.
 boundary=[0]+[1+j*13 for j in range(63)]+[count-1]+[1+j*13+12 for j in range(62,-1,-1)]+[0]
 for a,b in zip(boundary[:-1],boundary[1:]):
  quad=[start+a,start+b,start+b+count,start+a+count]
  if side<0:quad=quad[::-1]
  lidtri.extend([[quad[0],quad[1],quad[2]],[quad[0],quad[2],quad[3]]])
keep=~oldlid;mapping=np.full(nv,-1,int);mapping[keep]=np.arange(keep.sum());retained=np.all(keep[t],axis=1);oldnv=int(keep.sum())
lv=np.zeros((len(lidv),18),np.float32);lv[:,:3]=lidv;lv[:,3:6]=lidn;lv[:,6:8]=liduv;lv[:,9]=.55;lv[:,10]=1;lv[:,14]=1
v=np.concatenate([v[keep],lv]);t=np.concatenate([mapping[t[retained]],np.array(lidtri)+oldnv]).astype('<i4');groups=np.concatenate([groups[retained],np.zeros(len(lidtri),'<i4')]);shapes=np.concatenate([shapes[:,keep],np.array(lidshapes,dtype=np.float32)],axis=1)
nv=len(v);ni=t.size;d['vertices']=nv;d['triangles']=len(t)
with (E/'Alien.cartoon').open('wb') as f:
 f.write(struct.pack('<4i',nv,ni,len(shapes),nm));f.write(v.astype('<f4').tobytes());f.write(t.tobytes());f.write(groups.tobytes());f.write(shapes.astype('<f4').tobytes())
(E/'Alien.json').write_text(json.dumps(d,separators=(',',':')))
report=dict(vertices=nv,triangles=len(t),neck_compression=.115,lower_mouth_shape=True,mouth_anchor=d['mouthAnchor'])
(R/'alien-geometry.json').write_text(json.dumps(report,indent=2));print(report)
