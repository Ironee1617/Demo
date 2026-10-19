"""Connected rounded tentacles with transported, constant-size sections."""
from pathlib import Path
import numpy as np,struct,json
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parent;W=R.parent;portable=(W/'Export').is_dir();E=W/'Export' if portable else R/'Export';B=W/'Source/BeforeSketchFix' if portable else R/'BeforeSketchFix'
d=json.loads((B/'Alien.json').read_text())
with (B/'Alien.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18).copy();tri=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3);f.read(ni//3*4);s=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6).copy()
mat=np.floor(v[:,6]*4).astype(int)+4*np.floor(v[:,7]*4).astype(int)
def ease(a,b,x):
 u=np.clip((x-a)/(b-a),0,1);return u*u*(3-2*u)
# Seat the globe directly under the existing eyelid dome. The previous globe
# was centered far behind the dome, leaving an open cavity around its sides.
eye=np.isin(mat,[5,6]);old=v[:,:3].copy();grid=np.linspace(-.5,1.,20001)
uncompressed_y=np.interp(old[eye,1],grid+.025-.115*ease(-.24,.08,grid),grid)
globe=old[eye].copy();globe[:,1]=uncompressed_y
center=(globe.max(0)+globe.min(0))/2;radii=(globe.max(0)-globe.min(0))/2
direction=(globe-center)/radii;direction/=np.linalg.norm(direction,axis=1,keepdims=True)
dome_radii=np.array([.3131,.2895,.160]);surface=direction*dome_radii
normal=direction/dome_radii;normal/=np.linalg.norm(normal,axis=1,keepdims=True)
v[eye,:3]=surface+np.array([0,.1858,.230])-.009*normal
oral_teeth=(mat==9)&(np.linalg.norm(old[:,[0,2]],axis=1)<.24)
v[oral_teeth,1]+=.065;v[oral_teeth,0]*=.86;v[oral_teeth,2]*=.86
change=v[:,:3]-old
for i,name in enumerate(d['shapeNames']):
 if name.startswith('To_Matched_'):s[i,:,:3]-=change*(int(name.rsplit('_',1)[-1])/1000)
 elif name.startswith('Blink_'):s[i,eye,:3]=0
# Angular eyelid samples follow the globe between keys, rather than cutting
# through it along four long straight interpolation chords.
lid_indices=np.where(np.isin(mat,[7,8]))[0]
params=[(0.,0.)]+[(theta,u) for theta in np.linspace(0,np.pi,65)[1:-1] for u in np.linspace(0,1,13)]+[(np.pi,0.)]
def fitted_lids(blink):
 result=[];opening=.982*np.cos(blink*np.pi/2)
 for side in [1,-1]:
  for inner_layer in [False,True]:
   for theta,u in params:
    x=.3131*np.cos(theta);y=side*.2895*np.sin(theta)*(1-(1-opening)*u)
    z=.160*np.sqrt(max(0,1-(x/.3131)**2-(y/.2895)**2))
    normal=np.array([x/.3131**2,y/.2895**2,z/.160**2]);normal/=max(np.linalg.norm(normal),1e-8)
    result.append(np.array([x,y+.1858,z+.230])-(.006*normal if inner_layer else 0))
 return np.array(result)
assert np.max(np.abs(fitted_lids(0)-v[lid_indices,:3]))<1e-6
for step in range(1,13):
 name='Blink_'+str(step*100//12) if step%3==0 else 'BlinkStep_'+str(step).zfill(2)
 if name not in d['shapeNames']:
  d['shapeNames'].append(name);s=np.concatenate([s,np.zeros((1,nv,6),np.float32)])
 i=d['shapeNames'].index(name);s[i,lid_indices,:3]=fitted_lids(step/12)-v[lid_indices,:3]
ns=len(d['shapeNames'])
d['mouthAnchor']=[0,-.243,0];d['throatAnchor']=[0,-.16,0]
p=v[:,:3].copy();cut=-.095
attrs=[row.copy() for row in v];states=[s[:,i].copy() for i in range(nv)];cache={};faces=[]
def intersection(a,b):
 key=tuple(sorted((a,b)))
 if key not in cache:
  f=(cut-attrs[a][1])/(attrs[b][1]-attrs[a][1]);cache[key]=len(attrs);attrs.append(attrs[a]*(1-f)+attrs[b]*f);states.append(states[a]*(1-f)+states[b]*f)
 return cache[key]
for face in tri:
 if np.all(mat[face]==1):
  out=[]
  for a,b in zip(face,np.roll(face,-1)):
   inside=attrs[a][1]>=cut
   if inside:out.append(a)
   if inside!=(attrs[b][1]>=cut):out.append(intersection(a,b))
  for j in range(1,len(out)-1):faces.append([out[0],out[j],out[j+1]])
 elif np.all(np.isin(mat[face],[10,11])):continue
 elif np.all(mat[face]==9) and np.linalg.norm(p[face].mean(0)[[0,2]])>.24:continue
 else:faces.append(face.tolist())
attrs=np.array(attrs);states=np.stack(states,1);faces=np.array(faces);used=np.unique(faces);mapping=np.zeros(len(attrs),int);mapping[used]=np.arange(len(used));faces=mapping[faces];attrs=attrs[used];states=states[:,used];basecount=len(attrs)
# Preserve the original face, eyelids, crest, ventral mouth and morph surface.
boundary=np.where(abs(attrs[:,1]-cut)<1e-6)[0];_,ix=np.unique(np.round(attrs[boundary,:3],6),axis=0,return_index=True);boundary=boundary[ix]
angle=np.mod(np.arctan2(attrs[boundary,0],-attrs[boundary,2]),2*np.pi);order=np.argsort(angle);boundary=boundary[order];angle=angle[order]
print('head boundary',len(boundary),flush=True)
allv=attrs.tolist();newfaces=[];sections=12;around=6*sections;top=[];bottom=[]
def vertex(pos,uv=(.875,.875)):
 row=np.zeros(18);row[:3]=pos;row[6:8]=uv;row[9]=.38;row[10]=1;row[14]=1;allv.append(row.tolist());return len(allv)-1
# Separate UV seams, while positions and normals remain welded. Interpolating
# between unrelated atlas cells would create a dark band around the neck.
boundary_old=boundary.copy();boundary=np.array([vertex(attrs[k,:3]) for k in boundary])
# A flared round collar is a shared surface, not overlapping separate shapes.
for y,radius,ring in [(-.190,.245,top),(-.245,.13,bottom)]:
 for j in range(around):
  theta=j*2*np.pi/around;ring.append(vertex([radius*np.sin(theta),y,-radius*np.cos(theta)]))
# Zipper the actual head rim into a regular collar ring.
i=j=0
while i<len(boundary) or j<around:
 a=int(boundary[i%len(boundary)]);b=top[j%around]
 nexta=angle[(i+1)%len(boundary)]+(2*np.pi if i+1>=len(boundary) else 0)
 nextb=(j+1)*2*np.pi/around
 if i>=len(boundary):newfaces.append([a,top[(j+1)%around],b]);j+=1
 elif j>=around or nexta<nextb:newfaces.append([a,int(boundary[(i+1)%len(boundary)]),b]);i+=1
 else:newfaces.append([a,top[(j+1)%around],b]);j+=1
# The lip and cavity are part of the same collar surface. Shared positions at
# atlas seams remain welded in every pose, including the opening/spit shape.
mouth_rings=[];previous=bottom;mouth_start=len(allv)
def mouth_ring(radius,y,material):
 uv=(((material%4)+.5)/4,((material//4)+.5)/4)
 return [vertex([radius*np.sin(j*2*np.pi/around),y,-radius*np.cos(j*2*np.pi/around)],uv) for j in range(around)]
previous=mouth_ring(.13,-.245,11);mouth_rings.append(previous)
for radius,y,material in [(.123,-.248,11),(.110,-.246,11),(.095,-.239,11),(.095,-.239,10),(.087,-.225,10),(.060,-.180,10),(.022,-.157,10)]:
 current=mouth_ring(radius,y,material);mouth_rings.append(current)
 if np.linalg.norm(np.array(allv)[current[0],:3]-np.array(allv)[previous[0],:3])>1e-6:
  for j in range(around):
   k=(j+1)%around;newfaces.extend([[previous[j],current[k],current[j]],[previous[j],previous[k],current[k]]])
 previous=current
mouth_cap=vertex([0,-.150,0],(.625,.625))
for j in range(around):newfaces.append([previous[j],previous[(j+1)%around],mouth_cap])
inner=mouth_rings[3];mouth_ids=np.arange(mouth_start,len(allv))
def smooth(a,b,x):
 u=np.clip((x-a)/(b-a),0,1);return u*u*(3-2*u)
def curve(a,b,c,e,t):return (1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*e
def rotation(a,b,x):
 axis=np.cross(a,b);sn=np.linalg.norm(axis);cs=np.dot(a,b)
 if sn<1e-8:return x.copy()
 axis/=sn;return x*cs+np.cross(axis,x)*sn+axis*np.sum(x*axis,axis=-1,keepdims=True)*(1-cs)
paths=[];rootrings=[];newids=[];restnorm=[];toothdata=[]
# The six petals are evenly attached; front pair travels below the side pair.
for limb in range(6):
 start=limb*sections;theta=(start+sections*.5)*2*np.pi/around;direction=np.array([np.sin(theta),0,-np.cos(theta)]);sign=1 if direction[0]>0 else -1
 front=direction[2]>.25;rear=direction[2]<-.25;lane=0 if front else 2 if rear else 1
 root=[top[(start+k)%around] for k in range(sections+1)]+[bottom[(start+sections-k)%around] for k in range(sections+1)];rootrings.append(root);rootpos=np.array(allv)[root,:3];a=rootpos.mean(0)
 end=direction*.80;end[1]=-.64 if lane!=1 else -.56;b=direction*.41;b[1]=-.29;c=direction*.71;c[1]=-.36
 target=np.array([sign*[.070,.130,.055][lane],[-.64,-.52,-.36][lane],[-1.25,-1.35,-1.17][lane]])
 # Photo reference: the head leads and the arms trail in one loose bundle.
 # Lower roots clear the ventral mouth immediately, without a deep U-shaped
 # scoop. Side roots provide clearance around the inner pair, then taper in.
 tb=a+np.array([sign*[.025,.24,.04][lane],[-.40,-.07,-.025][lane],[-.035,-.14,-.23][lane]])
 tc=np.array([sign*[.31,.56,.20][lane],[-.57,-.30,-.10][lane],[-.60,-.65,-.70][lane]])
 paths.append((a,b,c,end,tb,tc,target));ids=[root];N=40;M=len(root)
 for k in range(1,N+1):
  ids.append([vertex([0,0,0],(.75+(.02+.96*j/M)*.25,.75+(.02+.96*k/N)*.25)) for j in range(M)])
 for k in range(N):
  for j in range(M):
   jj=(j+1)%M;newfaces.extend([[ids[k][j],ids[k+1][jj],ids[k+1][j]],[ids[k][j],ids[k][jj],ids[k+1][jj]]])
 cap=vertex(end);newfaces.extend([[ids[-1][j],ids[-1][(j+1)%M],cap] for j in range(M)])
 newids.append((ids,cap,rootpos,direction))
 teeth=[]
 for fraction in [.30,.50,.70]:
  rings=[[vertex([0,0,0],(.375,.625)) for j in range(12)] for k in range(6)]
  for k in range(5):
   for j in range(12):
    n=(j+1)%12;newfaces.extend([[rings[k][j],rings[k+1][n],rings[k+1][j]],[rings[k][j],rings[k][n],rings[k+1][n]]])
  rootcap=vertex([0,0,0],(.375,.625));tipcap=vertex([0,0,0],(.375,.625))
  for j in range(12):
   n=(j+1)%12;newfaces.extend([[rings[0][j],rootcap,rings[0][n]],[rings[-1][j],rings[-1][n],tipcap]])
  teeth.append((round(fraction*N),rings,rootcap,tipcap))
 toothdata.append(teeth)
def meal_controls(a,b,c,e,direction,age):
 # Cup from idle, close around the entering meal, then curl the tips back
 # toward the ventral mouth. The thick sections retain their cross-section.
 stages=[(0,None),(.45,(.41,-.30,.70,-.40,.76,-.68)),
         (1.25,(.45,-.33,.70,-.55,.64,-.86)),
         (1.65,(.50,-.30,.80,-.82,.48,-.90)),
         (1.90,(.56,-.30,.70,-.92,.015,-.72)),
         (2.12,(.58,-.30,.72,-.88,-.020,-.56)),
         (2.28,(.58,-.30,.72,-.88,-.020,-.56)),(2.85,None),(3,None)]
 def control(data):
  if data is None:return np.array([b,c,e])
  return np.array([direction*radius+np.array([0,y,0]) for radius,y in zip(data[::2],data[1::2])])
 for (t0,p0),(t1,p1) in zip(stages,stages[1:]):
  if age<=t1:
   f=smooth(t0,t1,age);result=control(p0)*(1-f)+control(p1)*f
   return result
 return np.array([b,c,e])

def make_pose(amount,phase=None,capture=None,fan=None,captive=False):
 q=np.array(allv)[:,:3].copy()
 for limb,((a,b,c,e,tb,tc,te),(ids,cap,rootpos,direction)) in enumerate(zip(paths,newids)):
  times=np.linspace(0,1,len(ids));source_centers=[];target_centers=[];sign=1 if direction[0]>0 else -1
  if capture is not None:tb,tc,te=meal_controls(a,b,c,e,direction,capture)
  if fan is not None:
   fb=direction*.50+np.array([0,-.34,0]);fc=direction*.78+np.array([0,-.57,0]);fe=direction*.82+np.array([0,-.78,0])
   tb=b*(1-fan)+fb*fan;tc=c*(1-fan)+fc*fan;te=e*(1-fan)+fe*fan
  if captive:
   tb=direction*.34+np.array([0,-.42,0]);tc=direction*.37+np.array([0,-1.05,0]);te=direction*.18+np.array([0,-1.50,0])
  for t in times:
   if capture is not None:
    lag=[.00,.035,.07,.10,.055,.02][limb]
    delayed=max(0,capture-(lag+.16*t*t)*(1-smooth(.90,1.40,capture)))
    tb,tc,te=meal_controls(a,b,c,e,direction,delayed)
   target=curve(a,tb,tc,te,t)
   if capture is not None:
    follow=smooth(.32,.80,delayed)*(1-smooth(1.30,1.90,delayed))*smooth(.25,1,t)
    target[1]+=.028*follow*np.sin(9*delayed-4.5*t-limb*.4)
    tangent_side=np.cross(direction,[0,1,0]);target+=tangent_side*.020*follow*np.sin(8*delayed-4*t-limb*.55)
   source_centers.append(curve(a,b,c,e,t));target_centers.append(target)
  source_delta=np.diff(source_centers,axis=0);target_delta=np.diff(target_centers,axis=0);sl=np.linalg.norm(source_delta,axis=1);tl=np.linalg.norm(target_delta,axis=1);sd=source_delta/sl[:,None];td=target_delta/tl[:,None]
  if capture is not None:
   # Preserve rest arc length through the cup, closure, and inward tip push.
   # Both elongation and compression read as telescoping instead of bending.
   factor=float(sl.sum()/tl.sum());target_centers=a+(np.array(target_centers)-a)*factor
   target_delta=np.diff(target_centers,axis=0);tl=np.linalg.norm(target_delta,axis=1);td=target_delta/tl[:,None]
  axis=np.cross(sd,td);sn=np.linalg.norm(axis,axis=1);cs=np.clip(np.sum(sd*td,axis=1),-1,1);angle=np.arctan2(sn,cs)*amount;axis/=np.maximum(sn[:,None],1e-9)
  newdir=sd*np.cos(angle[:,None])+np.cross(axis,sd)*np.sin(angle[:,None])+axis*np.sum(sd*axis,axis=1)[:,None]*(1-np.cos(angle[:,None]));length=sl*(1-amount)+tl*amount
  centers=np.r_[a[None,:],a+np.cumsum(newdir*length[:,None],axis=0)]
  if phase is not None:
   envelope=smooth(.12,.92,times);angle=2*np.pi*(phase-.95*times)
   lag=[.00,.09,.16,.20,.12,.04][limb]
   distal=smooth(.38,1.,times);individual=angle-2*np.pi*lag
   centers[:,1]+=(.052*envelope*np.sin(angle)+.032*distal*np.sin(individual))*(.62 if amount==0 else 1)
   centers[:,0]+=(.014*envelope*np.sin(angle+np.pi/2)+.010*distal*np.sin(individual+np.pi/3))*(.62 if amount==0 else 1)
  ee=centers[-1];tangents=np.gradient(centers,axis=0);tangents/=np.linalg.norm(tangents,axis=1,keepdims=True)
  # Transport from the rest frame. Rebuilding the frame from world-up flips
  # its roll when a front root passes vertical, flattening between pose keys.
  rest_side=np.cross(sd[0],[0,1,0]);rest_side/=np.linalg.norm(rest_side)
  side=rotation(sd[0],tangents[0],rest_side);initial=tangents[0];root_offset=rootpos-a
  M=len(ids[0])
  for k in range(1,len(ids)):
   t=times[k];tangent=tangents[k];side=side-tangent*np.dot(side,tangent);side/=np.linalg.norm(side);up=np.cross(side,tangent)
   width=np.interp(t,[0,.12,.35,.6,.8,.94,.98,1],[.075,.105,.125,.115,.085,.050,.028,.003]);thick=np.interp(t,[0,.12,.35,.6,.8,.94,.98,1],[.06,.082,.095,.088,.065,.038,.022,.003])
   phi=np.linspace(-np.pi/2,np.pi*1.5,M,endpoint=False)
   oval=side[None,:]*(np.sin(phi)*width)[:,None]+up[None,:]*(np.cos(phi)*thick)[:,None]
   rotated=rotation(initial,tangent,root_offset);mix=smooth(0,.22,t);offset=rotated*(1-mix)+oval*mix
   q[ids[k]]=centers[k]+offset
   for station,rings,rootcap,tipcap in toothdata[limb]:
    if station!=k:continue
    tooth_axis=-up+.25*tangent;tooth_axis/=np.linalg.norm(tooth_axis);tooth_side=side-tooth_axis*np.dot(side,tooth_axis);tooth_side/=np.linalg.norm(tooth_side);other=np.cross(tooth_axis,tooth_side);base=centers[k]-up*thick*.87
    for ring,height,radius in zip(rings,[0,.007,.016,.029,.040,.047],[.016,.019,.017,.011,.004,.0007]):
     phi=np.arange(12)*2*np.pi/12;q[ring]=base+tooth_axis*height+radius*(tooth_side[None,:]*np.cos(phi[:,None])+other[None,:]*np.sin(phi[:,None]))
    q[rootcap]=base;q[tipcap]=base+tooth_axis*.048
  q[cap]=ee+tangents[-1]*.004
 return q
rest=make_pose(0);allv=np.array(allv);allv[:,:3]=rest
labels=np.full(len(allv),-1,int);progress=np.zeros(len(allv))
for limb,(ids,cap,_,_) in enumerate(newids):
 for k,ring in enumerate(ids[1:],1):labels[ring]=limb;progress[ring]=k/(len(ids)-1)
 labels[cap]=limb;progress[cap]=1
np.savez(R/'section-labels.npz',labels=labels,progress=progress)
# Correct new triangle winding using the adjoining surface and section normals.
newfaces=np.array(newfaces);unique,inv=np.unique(np.round(rest,6),axis=0,return_inverse=True)
# The tube ordering is outward. Head/collar normals are oriented consistently
# by connected topology in the Blender finishing step below.
faces=np.r_[faces,newfaces]
_,weld=np.unique(np.round(rest,6),axis=0,return_inverse=True);wf=weld[faces];valid=(wf[:,0]!=wf[:,1])&(wf[:,1]!=wf[:,2])&(wf[:,2]!=wf[:,0]);faces=faces[valid]
_,keep=np.unique(np.sort(weld[faces],axis=1),axis=0,return_index=True);faces=faces[np.sort(keep)]
wave_frames=16;grasp_frames=97;fan_frames=8;idle_frames=16
wave_end=ns+8+wave_frames
allstates=np.zeros((wave_end+grasp_frames+fan_frames+9+idle_frames,len(allv),6),np.float32);allstates[:ns,:basecount]=states
for i,name in enumerate(d['shapeNames']):
 if name.startswith('To_Matched_'):
  f=int(name.rsplit('_',1)[-1])/1000;center=np.array([0,.23,0]);allstates[i,basecount:,:3]=(center+(rest[basecount:]-center)*.001-rest[basecount:])*f
 elif name=='BellyOpen':
  rad=np.linalg.norm(rest[:,[0,2]],axis=1);influence=(1-smooth(.08,.24,rad))*(1-smooth(-.20,-.12,rest[:,1]))
  allstates[i,:,:3]=0
  allstates[i,:,0]=rest[:,0]*.6*influence;allstates[i,:,2]=rest[:,2]*.6*influence;allstates[i,:,1]=-.015*influence
for j in range(1,9):
 d['shapeNames'].append('Streamline_'+str(j*125).zfill(4));allstates[ns+j-1,:,:3]=make_pose(j/8)-rest
allstates[:ns,boundary]=allstates[:ns,boundary_old]
gathered=make_pose(1)
for j in range(wave_frames):
 d['shapeNames'].append('StreamWave_'+str(j).zfill(2));allstates[ns+8+j,:,:3]=make_pose(1,j/wave_frames)-gathered
for j in range(grasp_frames):
 d['shapeNames'].append('Grasp_'+str(j).zfill(2));allstates[wave_end+j,:,:3]=make_pose(1,capture=j*3/(grasp_frames-1))-rest
for j in range(1,fan_frames+1):
 d['shapeNames'].append('SpitFan_'+str(j*125).zfill(4));allstates[wave_end+grasp_frames+j-1,:,:3]=make_pose(1,fan=j/fan_frames)-rest
for j in range(1,9):
 d['shapeNames'].append('Captured_'+str(j*125).zfill(4));allstates[wave_end+grasp_frames+fan_frames+j-1,:,:3]=make_pose(j/8,captive=True)-rest
d['shapeNames'].append('CapturedCrest')
material=np.floor(allv[:,6]*4).astype(int)+4*np.floor(allv[:,7]*4).astype(int)
for material_id,sign in [(3,1),(4,-1)]:
 mask=material==material_id;points=rest[mask];base=points[points[:,1]<points[:,1].min()+.016].mean(0)
 angle=sign*np.pi/3;delta=points-base;turned=delta.copy()
 turned[:,0]=np.cos(angle)*delta[:,0]-np.sin(angle)*delta[:,1];turned[:,1]=np.sin(angle)*delta[:,0]+np.cos(angle)*delta[:,1]
 allstates[wave_end+grasp_frames+fan_frames+8,mask,:3]=base+turned-points
# Slow, staggered ripples on the relaxed spread pose, with transported
# sections instead of scaling or rotating unweighted legacy tentacle bones.
for j in range(idle_frames):
 d['shapeNames'].append('IdleWave_'+str(j).zfill(2))
 allstates[wave_end+grasp_frames+fan_frames+9+j,:,:3]=make_pose(0,j/idle_frames)-rest
# Keep new surfaces entirely on the body: the authored ring poses own bending.
allv[:,10:14]=[1,0,0,0];allv[:,14:18]=[1,0,0,0]
unique,inv=np.unique(np.round(rest,6),axis=0,return_inverse=True);tt=inv[faces];edges=np.unique(np.sort(np.r_[tt[:,[0,1]],tt[:,[1,2]],tt[:,[2,0]]],axis=1),axis=0);edges=np.r_[edges,edges[:,::-1]];degree=np.maximum(np.bincount(edges[:,0],minlength=len(unique)),1)
vertex_material=np.floor(allv[:,6]*4).astype(int)+4*np.floor(allv[:,7]*4).astype(int);allowed=np.zeros(len(unique));allowed[inv[np.where((np.arange(len(allv))>=basecount)&(vertex_material==15))[0]]]=1
# The existing face/socket and the ventral mouth opening are fixed boundaries.
# Only the new connecting collar relaxes; pulling the original eye rim would
# open a visible gap around the eyeball.
allowed[inv[:basecount]]=0;allowed[inv[boundary]]=0;allowed[inv[mouth_ids]]=0
blend=allowed*smooth(-.31,-.20,unique[:,1])*(1-smooth(-.09,-.01,unique[:,1]))*(1-smooth(.27,.36,np.linalg.norm(unique[:,[0,2]],axis=1)))
section_relax=np.ones(len(unique));section_relax[inv[labels>=0]]=1-smooth(.10,.22,progress[labels>=0]);blend*=section_relax
def polish(q):
 out=np.zeros_like(unique);np.add.at(out,inv,q);out/=np.bincount(inv)[:,None]
 for _ in range(28):
  neighbor=np.stack([np.bincount(edges[:,0],weights=out[edges[:,1],j],minlength=len(unique)) for j in range(3)],1)/degree[:,None];out+=.45*blend[:,None]*(neighbor-out)
 out[:,0]*=1+.045*blend;out[:,2]*=1+.045*blend
 return out[inv]
rawrest=rest.copy();rawgathered=gathered.copy();rest=polish(rest);gathered=polish(gathered);allv[:,:3]=rest
for i in range(len(allstates)):
 if ns+8<=i<wave_end:allstates[i,:,:3]=polish(rawgathered+allstates[i,:,:3])-gathered
 else:allstates[i,:,:3]=polish(rawrest+allstates[i,:,:3])-rest
def normals(q):
 unique,inv=np.unique(np.round(q,6),axis=0,return_inverse=True);tt=inv[faces];n=np.zeros_like(unique);cross=np.cross(unique[tt[:,1]]-unique[tt[:,0]],unique[tt[:,2]]-unique[tt[:,0]]);cross/=np.maximum(np.linalg.norm(cross,axis=1,keepdims=True),1e-12)
 # Angle weighting prevents the narrow zipper triangles at the neck from
 # introducing alternating dark facets along an otherwise welded surface.
 for j in range(3):
  a=unique[tt[:,(j+1)%3]]-unique[tt[:,j]];b=unique[tt[:,(j+2)%3]]-unique[tt[:,j]];a/=np.maximum(np.linalg.norm(a,axis=1,keepdims=True),1e-12);b/=np.maximum(np.linalg.norm(b,axis=1,keepdims=True),1e-12);angle=np.arccos(np.clip(np.sum(a*b,axis=1),-1,1));np.add.at(n,tt[:,j],cross*angle[:,None])
 n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-12);return n[inv]
allv[:,3:6]=normals(rest)
for i in range(len(allstates)):
 if ns+8<=i<wave_end:allstates[i,:,3:6]=normals(gathered+allstates[i,:,:3])-normals(gathered)
 else:allstates[i,:,3:6]=normals(rest+allstates[i,:,:3])-allv[:,3:6]
with (E/'Alien.cartoon').open('wb') as f:
 f.write(struct.pack('<4i',len(allv),faces.size,len(allstates),1));f.write(allv.astype('<f4').tobytes());f.write(faces.astype('<i4').tobytes());f.write(np.zeros(len(faces),'<i4').tobytes());f.write(allstates.astype('<f4').tobytes())
d['vertices']=len(allv);d['triangles']=len(faces);d['parts']=['Preserved face and ventral mouth','Connected collar and six rounded trailing arms'];d.pop('partVertexCounts',None);d.pop('partTriangleCounts',None);(E/'Alien.json').write_text(json.dumps(d,separators=(',',':')))
im=Image.open(B/'Alien_Atlas.png').convert('RGBA');size=im.width//4;ph=-np.pi/2+np.arange(size)/size*2*np.pi;pink=smooth(.1,.8,-np.cos(ph));rgb=np.array([243,224,232])[None,:]*(1-pink[:,None])+np.array([205,159,192])[None,:]*pink[:,None];pixels=np.ones((size,size,4),np.uint8)*255;pixels[:,:,:3]=rgb[None,:,:].astype(np.uint8);tile=Image.fromarray(pixels);draw=ImageDraw.Draw(tile)
for x,y,radius in [(.19,.3,.025),(.27,.57,.035),(.35,.76,.024),(.72,.35,.026),(.80,.65,.035)]:
 rr=radius*size;draw.ellipse((x*size-rr,y*size-rr,x*size+rr,y*size+rr),fill=(189,137,202,255))
im.paste(tile,(3*size,0));im.save(E/'Alien_Atlas.png')
(R/'rounded-topology.json').write_text(json.dumps(dict(vertices=len(allv),triangles=len(faces),section_count=40,section_vertices=26,round_section_size_preserved=True,head_boundary=len(boundary)),indent=2));print('ROUNDED_ALIEN_EXPORTED',len(allv),len(faces),flush=True)
