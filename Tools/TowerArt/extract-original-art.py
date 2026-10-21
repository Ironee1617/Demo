# Extracts the supplied files' setup transforms and named bone curves; no runtime or source image is modified.
from pathlib import Path
import json,struct,math,re,hashlib,shutil,uuid,os
import numpy as np
from PIL import Image
PROJECT=Path(__file__).resolve().parents[2]
os.chdir(PROJECT)
BASE=Path('Assets/Demos/Roguelike/RPGAsset');OUT=Path('Assets/Demos/Roguelike/Resources/Tower')
Path('Artifacts/TowerArt').mkdir(parents=True,exist_ok=True)
class Reader:
 def __init__(self,data,offset=0):self.data=data;self.p=offset;self.strings=[]
 def byte(self):v=self.data[self.p];self.p+=1;return v
 def integer(self):
  value=0;shift=0
  while True:
   b=self.byte();value|=(b&127)<<shift
   if b<128:return value
   shift+=7
   if shift>35:raise ValueError('Invalid variable integer')
 def number(self):value=struct.unpack_from('>f',self.data,self.p)[0];self.p+=4;return value
 def text(self):
  length=self.integer()
  if not length:return None
  length-=1;value=self.data[self.p:self.p+length].decode('utf-8');self.p+=length;return value
 def ref(self):
  ix=self.integer();return self.strings[ix-1] if ix else None
 def sequence(self):return [self.integer() for _ in range(4)]
def matrix(x,y,rotation,sx=1,sy=1,shearX=0,shearY=0):
 ax=math.radians(rotation+shearX);ay=math.radians(rotation+90+shearY)
 return np.array([[math.cos(ax)*sx,math.cos(ay)*sy,x],[math.sin(ax)*sx,math.sin(ay)*sy,y],[0,0,1.]])
def geometry(r,weighted,bones,slotbone):
 vertices=[];influences={};counts=[];bindBones=[];bindValues=[];n=r.integer()
 for _ in range(n):
  if weighted:
   point=np.zeros(3);count=r.integer();counts.append(count)
   for _ in range(count):
    bi=r.integer();x,y,w=r.number(),r.number(),r.number();bindBones.append(bi);bindValues.extend([x,y,w]);point+=bones[bi]['world']@np.array([x,y,1])*w;influences[bi]=influences.get(bi,0)+w
   vertices.append(point[:2])
  else:
   x,y=r.number(),r.number();counts.append(1);bindBones.append(slotbone);bindValues.extend([x,y,1]);vertices.append((bones[slotbone]['world']@np.array([x,y,1]))[:2])
 return np.array(vertices),influences,counts,bindBones,bindValues

def curves(r,dimensions,count,bytevalues=False):
 keys=[];time=r.number();values=[r.byte()/255 if bytevalues else r.number() for _ in range(dimensions)]
 for i in range(count):
  key={'time':time,'values':values,'curve':0,'controls':[]};keys.append(key)
  if i==count-1:break
  time2=r.number();nextValues=[r.byte()/255 if bytevalues else r.number() for _ in range(dimensions)];key['curve']=r.byte()
  if key['curve']==2:key['controls']=[[r.number() for _ in range(4)] for _ in range(dimensions)]
  elif key['curve'] not in (0,1):raise ValueError('Unknown curve')
  time,values=time2,nextValues
 return keys

def value_at(keys,time,dimension):
 if time<=keys[0]['time']:return keys[0]['values'][dimension]
 for i in range(len(keys)-1):
  a,b=keys[i],keys[i+1]
  if time>b['time']:continue
  if a['curve']==1:return a['values'][dimension]
  if a['curve']==0:
   t=(time-a['time'])/(b['time']-a['time']);return a['values'][dimension]+(b['values'][dimension]-a['values'][dimension])*t
  c=a['controls'][dimension];lo,hi=0.,1.
  for _ in range(15):
   t=(lo+hi)*.5;u=1-t;x=u*u*u*a['time']+3*u*u*t*c[0]+3*u*t*t*c[2]+t*t*t*b['time']
   if x<time:lo=t
   else:hi=t
  t=(lo+hi)*.5;u=1-t;return u*u*u*a['values'][dimension]+3*u*u*t*c[1]+3*u*t*t*c[3]+t*t*t*b['values'][dimension]
 return keys[-1]['values'][dimension]

def read_clip(data,pos,bones,name,strings):
 r=Reader(data,pos+len(name)+1);r.strings=strings;total=r.integer();slots=r.integer();assert total<2000 and slots<100
 for _ in range(slots):
  r.integer()
  for _ in range(r.integer()):
   kind=r.byte();count=r.integer();assert count<1000
   if kind==0:
    for _ in range(count):r.number();r.ref()
   else:
    dims={1:4,2:3,3:7,4:6,5:1}[kind];r.integer();curves(r,dims,count,True)
 tracks=[]
 for _ in range(r.integer()):
  bone=r.integer();assert bone<len(bones)
  for _ in range(r.integer()):
   kind,count=r.byte(),r.integer();assert count<1000
   if kind==10:
    for _ in range(count):r.number();r.byte()
    continue
   r.integer();dims=2 if kind in (1,4,7) else 1
   tracks.append({'bone':bone,'kind':kind,'keys':curves(r,dims,count)})
 duration=max((t['keys'][-1]['time'] for t in tracks),default=.5)
 frames=[];framecount=max(2,math.ceil(duration*24)+1)
 for index in range(framecount):
  time=min(duration,index/24);pose=[[b['x'],b['y'],b['rotation'],b['scaleX'],b['scaleY'],b['shearX'],b['shearY']] for b in bones]
  for track in tracks:
   b=track['bone'];k=track['kind'];keys=track['keys'];v=value_at(keys,time,0)
   if k==0:pose[b][2]+=v
   elif k==1:pose[b][0]+=v;pose[b][1]+=value_at(keys,time,1)
   elif k==2:pose[b][0]+=v
   elif k==3:pose[b][1]+=v
   elif k==4:pose[b][3]*=v;pose[b][4]*=value_at(keys,time,1)
   elif k==5:pose[b][3]*=v
   elif k==6:pose[b][4]*=v
   elif k==7:pose[b][5]+=v;pose[b][6]+=value_at(keys,time,1)
   elif k==8:pose[b][5]+=v
   elif k==9:pose[b][6]+=v
  frames.append({'time':round(time,5),'pose':[round(v,5) for p in pose for v in p]})
 return {'name':name,'duration':round(duration,5),'frames':frames,'boneTrackCount':len(tracks)}

def extract(index):
 name=f'cha_{index:03}';source=BASE/'unity'/name/(name+'.skel.bytes');data=source.read_bytes();r=Reader(data,8)
 version=r.text();bounds=[r.number() for _ in range(5)];extra=r.byte();assert version=='4.2.43'
 if extra:r.number();r.text();r.text()
 r.strings=[r.text() for _ in range(r.integer())];bones=[]
 for i in range(r.integer()):
  nameBone=r.text();parent=r.integer() if i else -1;rot,x,y,sx,sy,shx,shy,length=[r.number() for _ in range(8)];inherit=r.integer();r.byte()
  if extra:r.p+=4;r.text();r.byte()
  world=matrix(x,y,rot,sx,sy,shx,shy)
  if parent>=0:world=bones[parent]['world']@world
  bones.append({'name':nameBone,'parent':parent,'x':x,'y':y,'rotation':rot,'scaleX':sx,'scaleY':sy,'shearX':shx,'shearY':shy,'inherit':inherit,'world':world})
 slots=[]
 for _ in range(r.integer()):
  slot=r.text();bone=r.integer();r.p+=8;attachment=r.ref();r.integer()
  if extra:r.byte()
  slots.append({'name':slot,'bone':bone,'attachment':attachment})
 assert [r.integer() for _ in range(4)]==[0,0,0,0]
 parts=[];errors=[];legMin=0;visible=[];manifest=[]
 for _ in range(r.integer()):
  slotIndex=r.integer();slot=slots[slotIndex]
  for _ in range(r.integer()):
   attachment=r.ref();flags=r.byte();attachName=r.ref() if flags&8 else attachment;kind=flags&7
   if kind not in (0,2):raise ValueError((index,kind))
   path=r.ref() if flags&16 else attachName
   if flags&32:r.p+=4
   if flags&64:r.sequence()
   if kind==0:
    angle=r.number() if flags&128 else 0;x,y,sx,sy,w,h=[r.number() for _ in range(6)]
    region=matrix(x,y,angle,sx,sy);counts=[1]*4;bindBones=[slot['bone']]*4;bindValues=[]
    for vx,vy in [(-w/2,h/2),(-w/2,-h/2),(w/2,-h/2),(w/2,h/2)]:
     q=region@np.array([vx,vy,1]);bindValues.extend([float(q[0]),float(q[1]),1])
    uv=np.array([[0,0,1],[0,1,1],[1,1,1],[1,0,1]]);triangles=[0,1,2,2,3,0]
    trans=bones[slot['bone']]['world']@region;fit=np.array([[trans[0,0]*w,-trans[0,1]*h,trans[0,2]-trans[0,0]*w/2+trans[0,1]*h/2],[trans[1,0]*w,-trans[1,1]*h,trans[1,2]-trans[1,0]*w/2+trans[1,1]*h/2]]).T;influences={}
   else:
    hull=r.integer();vertices,influences,counts,bindBones,bindValues=geometry(r,bool(flags&128),bones,slot['bone']);uv=np.array([[r.number(),r.number(),1] for _ in range(len(vertices))])
    triangles=[r.integer() for _ in range((len(vertices)*2-hull-2)*3)]
    if extra:[r.integer() for _ in range(r.integer())];r.number();r.number()
    fit=np.linalg.lstsq(uv,vertices,rcond=None)[0];errors.append(float(abs(uv@fit-vertices).max()))
   png=BASE/'spine'/name/(path+'.png');assert png.exists(),png
   im=Image.open(png).convert('RGBA');box=im.getchannel('A').getbbox();u=(box[0]+box[2])/(2*im.width);v=(box[1]+box[3])/(2*im.height)
   center=np.array([u,v,1])@fit;right=fit[0]/im.width;up=-fit[1]/im.height
   bone=slot['bone']
   if slot['name'] in ('wingL','wingR') and influences:bone=max(influences,key=influences.get)
   inverse=np.linalg.inv(bones[bone]['world']);local=inverse@np.array([center[0],center[1],1]);lr=inverse[:2,:2]@right;lu=inverse[:2,:2]@up
   scaleX=float(np.linalg.norm(lr));scaleY=float(np.linalg.norm(lu))*(1 if np.linalg.det(np.stack([lr,lu],axis=1))>=0 else -1);angle=math.degrees(math.atan2(lr[1],lr[0]))
   corners=[np.array([uu,vv,1])@fit for uu in [box[0]/im.width,box[2]/im.width] for vv in [box[1]/im.height,box[3]/im.height]];visible.extend(corners)
   if path in ('legr','legl'):legMin=min(legMin,min(c[1] for c in corners))
   if attachment==slot['attachment']:
    parts.append({'name':slot['name'],'texture':path,'bone':bone,'x':round(float(local[0]),5),'y':round(float(local[1]),5),'angle':round(angle,5),'scaleX':round(scaleX,5),'scaleY':round(scaleY,5),'order':slotIndex,'counts':counts,'bindBones':bindBones,'bindValues':[round(float(v),6) for v in bindValues],'uv':[round(float(v),7) for p in uv[:,:2] for v in p],'triangles':triangles})
   dest=OUT/'Actors'/name/png.name
   dest.parent.mkdir(parents=True,exist_ok=True)
   if not dest.exists():
    shutil.copyfile(png,dest);meta=Path(str(png)+'.meta').read_text(encoding='utf-8-sig');meta=re.sub(r'guid: [a-f0-9]+','guid: '+uuid.uuid4().hex,meta,count=1)
    for key,value in {'enableMipMap':'0','nPOTScale':'0','filterMode':'0','wrapU':'1','wrapV':'1','wrapW':'1','textureCompression':'0','spriteMode':'1','spritePixelsToUnits':'1','spriteGenerateFallbackPhysicsShape':'0','spriteMeshType':'0','alignment':'9','alphaIsTransparency':'1','textureType':'8','maxTextureSize':'256'}.items():meta=re.sub(r'(?m)^(\s*'+key+r':) .*$',r'\g<1> '+value,meta)
    meta=re.sub(r'spritePivot: \{x: [^,]+, y: [^}]+\}',f'spritePivot: {{x: {u:.8f}, y: {1-v:.8f}}}',meta);Path(str(dest)+'.meta').write_text(meta,encoding='utf-8')
   manifest.append({'source':str(png).replace('\\','/'),'runtime':str(dest).replace('\\','/'),'sha256':hashlib.sha256(png.read_bytes()).hexdigest()})
 clips=[]
 for clipName in ['atk','idle','run','die']:
  pattern=bytes([len(clipName)+1])+clipName.encode();offset=data.find(pattern,r.p)
  if offset>=0:clips.append(read_clip(data,offset,bones,clipName,r.strings))
 rawbones=[{k:round(v,6) if isinstance(v,float) else v for k,v in b.items() if k!='world'} for b in bones]
 arr=np.array(visible);center=(arr.min(axis=0)+arr.max(axis=0))/2
 return {'index':index,'sourceVersion':version,'groundOffset':round(-legMin,5),'portraitX':round(float(center[0]),4),'portraitY':round(float(center[1])-legMin,4),'portraitSize':round(float(max(np.ptp(arr,axis=0)))+6,4),'bones':rawbones,'parts':parts,'clips':clips,'maxSetupFitError':round(max(errors,default=0),6)},manifest

actors=[];allfiles=[]
for index in list(range(15))+[16]:
 actor,files=extract(index);actors.append(actor);allfiles+=files
 print(index,'parts',len(actor['parts']),'bones',len(actor['bones']),'clips',[(c['name'],c['duration'],c['boneTrackCount']) for c in actor['clips']],'fitError',actor['maxSetupFitError'],'portraitSize',actor['portraitSize'])
output=OUT/'ArtData.json';output.write_text(json.dumps({'sampleRate':24,'actors':actors},ensure_ascii=False,separators=(',',':')),encoding='utf-8')
meta=Path(str(output)+'.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
# Audit every copied pixel PNG, including unused source alternatives retained with the rig.
items=[]
for runtime in sorted((OUT/'Actors').rglob('*.png')):
 source=BASE/'spine'/runtime.parent.name/runtime.name
 digest=hashlib.sha256(source.read_bytes()).hexdigest()
 assert digest==hashlib.sha256(runtime.read_bytes()).hexdigest(),f'Runtime copy differs from original: {runtime}'
 items.append({'source':source.as_posix(),'runtime':runtime.as_posix(),'sha256':digest,'bytes':source.stat().st_size})
for index,name in enumerate(['Stage_0_0','Stage_0_1','Stage_0_2','Stage_1_Boss','Stage_0_Boss']):
 source=BASE/'mobIcon'/(name+'.png');runtime=OUT/'Monsters'/f'monster_{index}.png'
 digest=hashlib.sha256(source.read_bytes()).hexdigest()
 assert digest==hashlib.sha256(runtime.read_bytes()).hexdigest(),runtime
 items.append({'source':source.as_posix(),'runtime':runtime.as_posix(),'sha256':digest,'bytes':source.stat().st_size})
skeletons=[]
for index in list(range(15))+[16]:
 source=BASE/'unity'/f'cha_{index:03}'/f'cha_{index:03}.skel.bytes'
 skeletons.append({'source':source.as_posix(),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
manifest={'imagePolicy':'All active character and monster PNGs are independent, byte-identical copies. No new character image files are generated. Portraits are runtime camera captures of these same original weighted meshes.','sampleRate':24,'sourceFormat':'Spine 4.2.43','artDataSha256':hashlib.sha256(output.read_bytes()).hexdigest(),'files':items,'skeletons':skeletons}
for file in [OUT/'ArtSources.json',Path('Artifacts/TowerArt/source-manifest.json')]:
 file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
meta=Path(str(OUT/'ArtSources.json')+'.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('ArtData',output.stat().st_size,'bytes;',len(items),'unchanged original PNG copies;',len(skeletons),'original skeletons verified.')
