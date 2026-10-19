import bpy,json,struct,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
from math import sin,pi,radians
R=Path(__file__).resolve().parent;portable=(R.parent/'Export/Alien.json').is_file();E=R.parent/'Export' if portable else R/'Export'
bpy.ops.wm.read_factory_settings(use_empty=True)
d=json.loads((E/'Alien.json').read_text())
with (E/'Alien.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18);t=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3);f.read(ni//3*4);shapes=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6)
def xyz(p):return np.stack([p[...,0],-p[...,2],p[...,1]],axis=-1)
mesh=bpy.data.meshes.new('Compact continuous alien');mesh.from_pydata(xyz(v[:,:3]).tolist(),[],t.tolist());mesh.update();obj=bpy.data.objects.new('Alien_compact',mesh);bpy.context.collection.objects.link(obj)
uv=mesh.uv_layers.new(name='Painted atlas')
for loop in mesh.loops:uv.data[loop.index].uv=v[loop.vertex_index,6:8]
for p in mesh.polygons:p.use_smooth=True
obj.shape_key_add(name='Basis')
for i,name in enumerate(d['shapeNames']):obj.shape_key_add(name=name).data.foreach_set('co',xyz(v[:,:3]+shapes[i,:,:3]).ravel())
mat=bpy.data.materials.new('Original painted cartoon');mat.use_nodes=True;nodes=mat.node_tree.nodes;im=nodes.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(E/'Alien_Atlas.png'));im.image.pack();bs=nodes.get('Principled BSDF');mat.node_tree.links.new(im.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.45;mesh.materials.append(mat)
arm=bpy.data.armatures.new('Alien 20 bone rig');rig=bpy.data.objects.new('Alien_rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for i,b in enumerate(d['bones']):
 eb=arm.edit_bones.new(b['name']);eb.head=xyz(np.array(b['world']));child=next((q for q in d['bones'] if q['parent']==i),None)
 eb.tail=xyz(np.array(child['world'])) if child and Vector(xyz(np.array(child['world']))-eb.head).length>.01 else eb.head+Vector((0,0,.08))
 if b['parent']>=0:eb.parent=arm.edit_bones[d['bones'][b['parent']]['name']]
bpy.ops.object.mode_set(mode='OBJECT')
for i,b in enumerate(d['bones']):
 vg=obj.vertex_groups.new(name=b['name'])
 for j in range(4):
  for idx in np.where((v[:,14+j].astype(int)==i)&(v[:,10+j]>0))[0]:vg.add([int(idx)],float(v[idx,10+j]),'ADD')
mod=obj.modifiers.new('Continuous tentacle rig','ARMATURE');mod.object=rig;obj.parent=rig
mod=obj.modifiers.new('Weld UV seams for smooth preview','WELD');mod.merge_threshold=.00001
keys=obj.data.shape_keys.key_blocks;keys['Streamline_1000'].value=1
for frame in range(1,74):
 phase=(frame-1)/72*16;k=int(phase)%16;fraction=phase-int(phase)
 for i in range(16):
  key=keys['StreamWave_'+str(i).zfill(2)];key.value=(1-fraction if i==k else fraction if i==(k+1)%16 else 0);key.keyframe_insert(data_path='value',frame=frame)
 keys['Streamline_1000'].keyframe_insert(data_path='value',frame=frame)
if obj.data.shape_keys.animation_data and obj.data.shape_keys.animation_data.action:obj.data.shape_keys.animation_data.action.name='Octopus trailing swim with staggered tip waves'
def smooth(a,b,t):
 x=np.clip((t-a)/(b-a),0,1);return x*x*(3-2*x)
def blank():
 for key in keys:
  if key.name!='Basis':key.value=0
def blink(value):
 for j in range(1,13):
  name='Blink_'+str(j*100//12) if j%3==0 else 'BlinkStep_'+str(j).zfill(2)
  keys[name].value=max(0,1-abs(value*12-j))
body=rig.pose.bones['Body'];body.rotation_mode='QUATERNION';pivot=body.bone.head_local
rig.rotation_mode='QUATERNION'
def capture_time(age):
 def h(t,a,b,da,db):return (2*t**3-3*t*t+1)*a+(t**3-2*t*t+t)*da+(-2*t**3+3*t*t)*b+(t**3-t*t)*db
 if age<=.85:return h(age/.85,0,.45,.85*.53,.85*.72)
 if age<=2.38:return h((age-.85)/1.53,.45,1.98,1.53*.72,1.53*1.25)
 return h(min(1,(age-2.38)/.62),1.98,3,.62*1.25,.62*1.65)
def body_pose(angle,scale=(1,1,1)):
 body.matrix=Matrix.Translation(pivot)@Matrix.Rotation(radians(angle),4,'X')@Matrix.Translation(-pivot)@body.bone.matrix_local
 body.scale=scale
def record(frame):
 for key in keys:
  if key.name!='Basis':key.keyframe_insert(data_path='value',frame=frame)
 for channel in ['rotation_quaternion','location','scale']:body.keyframe_insert(data_path=channel,frame=frame)
 rig.keyframe_insert(data_path='rotation_quaternion',frame=frame)
body_pose(0);body.keyframe_insert(data_path='rotation_quaternion',frame=1);body.keyframe_insert(data_path='rotation_quaternion',frame=73)
for action,start,end in [('Capture and pull',91,181),('Captured struggle and fold',201,264),('Expulsion fan and recoil',281,317)]:
 bpy.context.scene.timeline_markers.new(action,frame=start)
 blank();body_pose(0);rig.rotation_quaternion=Quaternion();record(start-1)
 for frame in range(start,end+1):
  age=(frame-start)/30;blank()
  if action=='Capture and pull':
   raw_age=age;age=capture_time(age)
   at=min(96,age*32);j=min(95,int(at));f=at-j;keys['Grasp_'+str(j).zfill(2)].value=1-f;keys['Grasp_'+str(j+1).zfill(2)].value=f
   body_pose(0)
   # Forward target example; Unity computes the direction from the live prey.
   rig.rotation_quaternion=Quaternion((1,0,0),radians(-90)*smooth(0,.72,raw_age)*(1-smooth(2.45,2.95,raw_age)))
   keys['BellyOpen'].value=smooth(.45,.90,age)*(1-smooth(1.98,2.65,age))
  elif action=='Captured struggle and fold':
   fold=smooth(.16,.72,age);struggle=smooth(.04,.20,age)*(1-smooth(.82,1.30,age))
   for j in range(1,9):keys['Captured_'+str(j*125).zfill(4)].value=max(0,1-abs(fold*8-j))
   keys['CapturedCrest'].value=smooth(.24,.68,age)
   blink(smooth(.58,1.10,age)*.92);body_pose(sin(age*19)*3*struggle,(1-.04*struggle,1-.02*struggle,1+.04*struggle))
  else:
   spit=smooth(0,.10,age)*(1-smooth(.70,1.2,age));body_pose(-58*spit,(1+.08*spit,1-.07*spit,1-.035*spit));keys['BellyOpen'].value=spit
   for j in range(1,9):keys['SpitFan_'+str(j*125).zfill(4)].value=max(0,1-abs(spit*8-j))
  record(frame)
 bpy.context.scene.timeline_markers.new(action+' end',frame=end)
bpy.context.scene.timeline_markers.new('Octopus swimming loop',frame=1)
bpy.context.scene.timeline_markers.new('Idle tentacle ripple',frame=341)
for frame in range(341,414):
 blank();body_pose(0);rig.rotation_quaternion=Quaternion()
 phase=(frame-341)/72*16;k=int(phase)%16;fraction=phase-int(phase)
 for i in range(16):keys['IdleWave_'+str(i).zfill(2)].value=(1-fraction if i==k else fraction if i==(k+1)%16 else 0)
 record(frame)
obj.data.shape_keys.animation_data.action.name='Idle - Swim - Close and feed into mouth - Captured - Expel'
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=413;scene.render.fps=30;scene.frame_set(91)
scene.use_preview_range=True;scene.frame_preview_start=91;scene.frame_preview_end=181
for name,key in [('Lower mouth','mouthAnchor'),('Throat','throatAnchor')]:
 marker=bpy.data.objects.new(name,None);marker.empty_display_size=.06;marker.location=xyz(np.array(d[key]));bpy.context.collection.objects.link(marker)
for selected in bpy.context.selected_objects:selected.select_set(False)
obj.select_set(True);bpy.context.view_layer.objects.active=obj
obj.active_shape_key_index=keys.find('Grasp_48')
if bpy.context.window and bpy.data.workspaces.get('Animation'):bpy.context.window.workspace=bpy.data.workspaces['Animation']
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_floor=False
   space.region_3d.view_location=(0,.2,.1);space.region_3d.view_distance=3.5
   space.region_3d.view_perspective='ORTHO';space.region_3d.view_rotation=Vector((3,-5,2)).to_track_quat('Z','Y')
  elif area.type=='PROPERTIES':area.spaces.active.context='DATA'
  elif area.type=='DOPESHEET_EDITOR' and area.height>100:area.spaces.active.dopesheet.filter_fcurve_name='Grasp'
bpy.ops.wm.save_as_mainfile(filepath=str((R.parent/'Source' if portable else R)/'alien_mouth_assist_r15.blend'))
print('ALIEN_SOURCE_SAVED',flush=True)
