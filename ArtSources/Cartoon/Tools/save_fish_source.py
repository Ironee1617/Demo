import bpy,json,struct,numpy as np
from pathlib import Path
R=Path(__file__).resolve().parent;E=R.parent/'Export'
bpy.ops.wm.read_factory_settings(use_empty=True)
d=json.loads((E/'Fish0.json').read_text())
with (E/'Fish0.cartoon').open('rb') as f:
 nv,ni,ns,nm=struct.unpack('<4i',f.read(16));v=np.frombuffer(f.read(nv*72),'<f4').reshape(nv,18);tri=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3);f.read(ni//3*4);shapes=np.frombuffer(f.read(),'<f4').reshape(ns,nv,6)
def xyz(p):return np.stack([p[...,0],-p[...,2],p[...,1]],axis=-1)
mesh=bpy.data.meshes.new('Rounded fish with narrow cheek opening');mesh.from_pydata(xyz(v[:,:3]).tolist(),[],tri.tolist());mesh.update();obj=bpy.data.objects.new('Fish_compact',mesh);bpy.context.collection.objects.link(obj)
uv=mesh.uv_layers.new(name='Painted atlas')
for loop in mesh.loops:uv.data[loop.index].uv=v[loop.vertex_index,6:8]
for face in mesh.polygons:face.use_smooth=True
obj.shape_key_add(name='Basis')
for i,name in enumerate(d['shapeNames']):obj.shape_key_add(name=name).data.foreach_set('co',xyz(v[:,:3]+shapes[i,:,:3]).ravel())
mat=bpy.data.materials.new('Painted fish');mat.use_nodes=True;tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(E/'Fish0_Atlas.png'));tex.image.pack();bs=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.45;mesh.materials.append(mat)
key=obj.data.shape_keys.key_blocks['Alien_Mouth_Open']
for frame,value in [(1,0),(17,1),(50,1),(67,0)]:key.value=value;key.keyframe_insert(data_path='value',frame=frame)
obj.data.shape_keys.animation_data.action.name='Rounded mouth opening - fixed eyes and soft sockets'
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=75;bpy.context.scene.render.fps=30;bpy.context.scene.frame_set(1)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(R.parent/'Source/fish_compact.blend'))
print('FISH_SOURCE_SAVED',flush=True)
