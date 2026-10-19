"""Keep the extracted character hand, its original prop and rig together."""
import bpy,bmesh,json,struct,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parent;portable=(R.parent/'Export').is_dir();E=R.parent/'Export' if portable else R.parent/'Aquarium-CartoonReplacement-R1/Export';S=R.parent/'Source' if portable else R
def xyz(p):return np.stack([p[...,0],-p[...,2],p[...,1]],axis=-1)
def load(name):
 d=json.loads((E/(name+'.json')).read_text())
 with (E/(name+'.cartoon')).open('rb') as f:
  nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18);t=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3)
 return d,v,t
bpy.ops.wm.open_mainfile(filepath=str(S/'first_person_hands.blend'))
d,v,t=load('DiverHands');hand=bpy.data.objects['Extracted character hand'];hand.parent=None;hand.matrix_world=Matrix.Identity(4)
for mod in list(hand.modifiers):
 if mod.type=='ARMATURE':hand.modifiers.remove(mod)
for obj in list(bpy.data.objects):
 if obj.name.startswith(('DiverHands_Rig','HarpoonGun','Harpoon grip socket')):bpy.data.objects.remove(obj,do_unlink=True)
hand.data.vertices.foreach_set('co',xyz(v[:,:3]).astype(np.float32).ravel());hand.data.update()
bm=bmesh.new();bm.from_mesh(hand.data)
if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
assert bm.calc_volume(signed=True)>0,'Extracted Blender hand must face outward'
bm.to_mesh(hand.data);bm.free()
arm=bpy.data.armatures.new('Extracted character armature');rig=bpy.data.objects.new('DiverHands_Rig',arm);bpy.context.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for b in d['bones']:
 eb=arm.edit_bones.new(b['name']);eb.head=xyz(np.array(b['world']));child=next((q for q in d['bones'] if q['parent']==d['bones'].index(b)),None)
 eb.tail=xyz(np.array(child['world'])) if child and Vector(xyz(np.array(child['world']))-eb.head).length>.01 else eb.head+Vector((0,-.08,0))
 if b['parent']>=0:eb.parent=arm.edit_bones[d['bones'][b['parent']]['name']]
bpy.ops.object.mode_set(mode='OBJECT');hand.vertex_groups.clear()
for bi,b in enumerate(d['bones']):
 group=hand.vertex_groups.new(name=b['name'])
 for slot in range(4):
  for vi in np.where((v[:,14+slot].astype(int)==bi)&(v[:,10+slot]>0))[0]:group.add([int(vi)],float(v[vi,10+slot]),'ADD')
mod=hand.modifiers.new('Matched runtime armature','ARMATURE');mod.object=rig;hand.parent=rig
g,gv,gt=load('HarpoonGun');cross=np.cross(gv[gt[:,1],:3]-gv[gt[:,0],:3],gv[gt[:,2],:3]-gv[gt[:,0],:3])
if np.median(np.sum(cross*gv[gt,3:6].mean(1),axis=1))<0:gt=gt[:,::-1]
mesh=bpy.data.meshes.new('Original character prop');mesh.from_pydata(xyz(gv[:,:3]).tolist(),[],gt.tolist());mesh.update();gun=bpy.data.objects.new('HarpoonGun',mesh);bpy.context.collection.objects.link(gun)
uv=mesh.uv_layers.new(name='Painted atlas')
for loop in mesh.loops:uv.data[loop.index].uv=gv[loop.vertex_index,6:8]
for face in mesh.polygons:face.use_smooth=True
mat=bpy.data.materials.new('Harpoon painted cartoon');mat.use_nodes=True;im=mat.node_tree.nodes.new('ShaderNodeTexImage');im.image=bpy.data.images.load(str(E/'HarpoonGun_Atlas.png'));im.image.pack();bs=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(im.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.38;mesh.materials.append(mat)
handworld=np.array(next(b['world'] for b in d['bones'] if b['name']=='HandL'));origin=xyz(handworld+np.array(d['gunPosition']))
socket=bpy.data.objects.new('Harpoon grip socket',None);bpy.context.collection.objects.link(socket);socket.parent=rig;socket.parent_type='BONE';socket.parent_bone='HandL';socket.matrix_world=Matrix.Translation(Vector(origin));socket.scale=(d['gunScale'],)*3;gun.parent=socket
bpy.context.scene.unit_settings.system='METRIC';rig.show_in_front=True
bpy.context.view_layer.update();expected=xyz(gv[:,:3])*d['gunScale']+origin;actual=np.array([(gun.matrix_world@vert.co)[:] for vert in gun.data.vertices]);error=float(np.abs(expected-actual).max());assert error<1e-5,error
bpy.ops.wm.save_as_mainfile(filepath=str(S/'first_person_hands.blend'))
print('EXTRACTED_GRIP_ASSEMBLY_SAVED max_prop_error='+str(error))
