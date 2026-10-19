"""Extract the existing character's holding hand. No sculpting or remeshing.

All visible vertices, triangles, UVs and material attributes come from Diver.
Only a hidden forearm cut is closed. One rigid change of coordinates aligns
the original held prop with the first-person camera.
"""
import bpy,bmesh,numpy as np,json,struct,hashlib
from pathlib import Path
from mathutils import Quaternion
R=Path(__file__).resolve().parent;portable=(R.parent/'Export/Diver.json').is_file()
E=R.parent/'Export' if portable else R.parent/'Aquarium-CartoonReplacement-R1/Export'
S=R.parent/'Source' if portable else R
meta=json.loads((E/'Diver.json').read_text());raw=(E/'Diver.cartoon').read_bytes()
nv,ni,ns,nm=struct.unpack_from('<4i',raw)
source=np.frombuffer(raw,'<f4',nv*18,16).reshape(nv,18)
tri=np.frombuffer(raw,'<i4',ni,16+nv*72).reshape(-1,3)
q=meta['gunRotation'];rotation=np.array(Quaternion((q[3],q[0],q[1],q[2])).to_matrix(),dtype=float)
source_hand=np.array(next(b['world'] for b in meta['bones'] if b['name']=='HandL'))
source_origin=source_hand+meta['gunPosition'];origin=np.array([.34,-.34,.83])
coords=(source[:,:3]-source_origin)@rotation+origin
arm_share=np.sum(source[:,10:14]*np.isin(source[:,14:18],[3,4,5]),axis=1)
part=meta['parts'].index('Diver_ContinuousWetsuit');start=sum(meta['partVertexCounts'][:part]);end=start+meta['partVertexCounts'][part]
keep=(source[:,0]>.38)&(source[:,1]>-.54)&(coords[:,2]>origin[2]-.50)&(np.arange(nv)>=start)&(np.arange(nv)<end)
hand_weight=np.sum(source[:,10:14]*(source[:,14:18]==5),axis=1)
assert np.all(keep[hand_weight>.5]),'Extraction must retain every original hand vertex'
kept_tri=tri[np.all(keep[tri],axis=1)]
used=np.unique(kept_tri);remap=np.full(nv,-1,np.int32);remap[used]=np.arange(len(used));t=remap[kept_tri]
v=source[used].copy();v[:,:3]=coords[used];v[:,3:6]=source[used,3:6]@rotation
# The armature follows the identical rigid change of coordinates.
bones=[]
def bone(name,parent,world):
 idx=next((i for i,b in enumerate(bones) if b['name']==parent),-1);p=np.array(world)
 local=p-np.array(bones[idx]['world']) if idx>=0 else p
 bones.append(dict(name=name,parent=idx,position=local.tolist(),rotation=[0,0,0,1],scale=[1,1,1],world=p.tolist()))
bone('Root',None,[0,0,0]);bone('Body','Root',[0,0,0])
for side in ['L','R']:
 for name,parent in [('UpperArm','Body'),('Forearm','UpperArm'+side),('Hand','Forearm'+side)]:
  old=next(b for b in meta['bones'] if b['name']==name+side)
  bone(name+side,parent,(np.array(old['world'])-source_origin)@rotation+origin)
v[:,10:14]=0;v[:,14:18]=0
for slot,old_id in enumerate([3,4,5]):
 v[:,10+slot]=(source[used,10:14]*(source[used,14:18]==old_id)).sum(1)
 v[:,14+slot]=slot+2
v[:,10:14]/=v[:,10:14].sum(1,keepdims=True)
# The exported body repeats some seam vertices; join coincident copies only.
# This changes indexing, not the modeled hand surface.
_,first,inverse=np.unique(np.round(v[:,:3],6),axis=0,return_index=True,return_inverse=True)
v=v[first];used=used[first];t=inverse[t]
# Close the hidden forearm opening with a new center, preserving source faces.
directed=np.concatenate([t[:,[0,1]],t[:,[1,2]],t[:,[2,0]]])
_,inverse,edge_counts=np.unique(np.sort(directed,1),axis=0,return_inverse=True,return_counts=True)
assert edge_counts.max()==2,'Nonmanifold source hand'
boundary=directed[edge_counts[inverse]==1];assert len(boundary)>0
links={int(a):int(b) for a,b in boundary};cursor=int(boundary[0,0]);visited=[]
while cursor not in visited:
 visited.append(cursor);cursor=links[cursor]
assert len(visited)==len(boundary),'Unexpected extra cut boundary'
center=v[np.unique(boundary)].mean(0);center[3:6]=[0,0,-1];center[10:14]=[0,1,0,0];center[14:18]=[2,3,4,0]
cap=np.array([[b,a,len(v)] for a,b in boundary],np.int32)
t=np.concatenate([t,cap]);v=np.vstack([v,center])
bpy.ops.wm.read_factory_settings(use_empty=True)
me=bpy.data.meshes.new('Exact character hand surface');me.from_pydata(v[:,:3],[],t);me.update()
edges=np.sort(np.concatenate([t[:,[0,1]],t[:,[1,2]],t[:,[2,0]]]),1)
_,counts=np.unique(edges,axis=0,return_counts=True)
print('EXTRACTION_TOPOLOGY',len(kept_tri),len(t),len(v),len(boundary),np.unique(counts,return_counts=True),flush=True)
assert np.all(counts==2),'Open extraction'
recovered=(v[:-1,:3]-origin)@rotation.T+source_origin
error=float(np.max(np.linalg.norm(recovered-source[used,:3],axis=1)))
assert error<1e-6,error
rest_hand=np.array(next(b['world'] for b in bones if b['name']=='HandL'))
d=dict(name='DiverHands',kind='Diver',firstPerson=True,bones=bones,shapeNames=['Metamorphosis'],atlas='Diver',materials=meta['materials'],vertices=len(v),triangles=len(t),parts=['Original character holding hand and wrist'],partVertexCounts=[len(v)],partTriangleCounts=[len(t)],mouthAnchor=[0,0,0],throatAnchor=[0,0,0],gunPosition=(origin-rest_hand).tolist(),gunRotation=[0,0,0,1],gunScale=meta['gunScale'])
with (E/'DiverHands.cartoon').open('wb') as f:
 f.write(struct.pack('<4i',len(v),t.size,1,1));f.write(v.astype('<f4').tobytes());f.write(t.astype('<i4').tobytes());f.write(np.zeros(len(t),'<i4').tobytes());f.write(np.zeros((len(v),6),'<f4').tobytes())
(E/'DiverHands.json').write_text(json.dumps(d,separators=(',',':')))
report=dict(source='Diver.cartoon',source_sha256=hashlib.sha256(raw).hexdigest(),source_vertex_indices=used.tolist(),vertices=len(v),original_triangles=len(kept_tri),hidden_cap_triangles=len(t)-len(kept_tri),max_source_position_error=error,nonmanifold_edges=int(np.sum(counts!=2)),shape_edited=False)
(S/'extraction-validation.json').write_text(json.dumps(report,indent=2))
hand=bpy.data.objects.new('Extracted character hand',me);bpy.context.collection.objects.link(hand)
for face in me.polygons:face.use_smooth=True
uv=me.uv_layers.new(name='Original character atlas')
for loop in me.loops:uv.data[loop.index].uv=v[loop.vertex_index,6:8]
mat=bpy.data.materials.new('Original navy wetsuit');mat.use_nodes=True
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(E/'Diver_Atlas.png'));tex.image.pack()
bs=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.54;me.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=str(S/'first_person_hands.blend'))
print('CHARACTER_HAND_EXTRACTED '+json.dumps({k:v for k,v in report.items() if k!='source_vertex_indices'}),flush=True)
