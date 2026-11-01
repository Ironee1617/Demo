import bpy, math, os, json
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
if os.path.basename(ROOT)=='source':ROOT=os.path.dirname(ROOT)
OUT = os.path.join(ROOT, 'export')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

def material(name, color, metal, rough):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    return m
steel=material('Brushed marine steel',(.34,.46,.48),.72,.34)
edge=material('Machined silver edges',(.58,.68,.68),.8,.27)
dark=material('Pipe interior',(.055,.13,.15),.45,.48)
rubber=material('EPDM seal',(.018,.032,.035),0,.69)
bronze=material('Nickel bronze screws',(.52,.42,.25),.74,.3)
marks=material('Inlaid flow markers',(.68,.78,.73),.25,.45)

def finish(o,mat,bevel=.008):
    o.data.materials.append(mat)
    if bevel:
        m=o.modifiers.new('Machined edge radii','BEVEL');m.width=bevel;m.segments=2
        m.affect='EDGES';m.limit_method='ANGLE'
        bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons:p.use_smooth=True
    o.data.set_sharp_from_angle(angle=math.radians(40))
    m=o.modifiers.new('Area weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True;m.weight=50
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
    return o

def profile(name,points,mat,n=96,bevel=0):
    vs=[];fs=[]
    for r,z in points:
        for j in range(n):
            a=2*math.pi*j/n;vs.append((r*math.cos(a),r*math.sin(a),z))
    for i in range(len(points)):
        for j in range(n):fs.append((i*n+j,((i+1)%len(points))*n+j,((i+1)%len(points))*n+(j+1)%n,i*n+(j+1)%n))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,mat,bevel)

def cube(name,loc,size,mat,bev=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name
    o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,mat,bev)

def cylinder(name,r,depth,loc,mat,verts=64,bevel=.005):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=loc)
    o=bpy.context.object;o.name=name;return finish(o,mat,bevel)

def cut(o,tool):
    bpy.context.view_layer.objects.active=o;m=o.modifiers.new('True cut opening','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=tool
    bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(tool,do_unlink=True)

def join(objects,name,origin=(0,0,0)):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    bpy.context.scene.cursor.location=origin;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o

base=profile('Fixed_Flange',[(.65,-.23),(.65,.052),(.69,.08),(.99,.08),(1.07,.012),(1.08,-.035),(1.06,-.22)],steel)
for i in range(6):
    a=math.tau*i/6+math.pi/6
    cut(base,cylinder('Threaded base socket',.036,.5,(.845*math.cos(a),.845*math.sin(a),0),steel,32,0))
finish(base,steel,.003)
rim=profile('Bright rim',[(1.023,.037),(1.025,.052),(1.053,.026),(1.052,.014)],edge)
base=join([base,rim],'Fixed_Flange')
pipe=profile('Pipe_Throat',[(.65,.045),(.65,-2.6),(.72,-2.6),(.72,.045)],dark,96,0)
pipe_rings=[]
for z in [-.32,-1.1,-1.88]:
    pipe_rings.append(profile('Recessed pipe joint',[(.647,z+.018),(.642,z),(.647,z-.018),(.66,z-.018),(.66,z+.018)],steel,96,0))
pipe=join([pipe]+pipe_rings,'Pipe_Throat')
seal=profile('Seal',[(.651,.081),(.654,.098),(.686,.098),(.69,.081)],rubber,96,0)

lid=profile('Lid_Ring',[(.655,.099),(.655,.163),(.675,.178),(.947,.178),(.986,.139),(.982,.099)],steel,128,0)
for i in range(6):
    a=math.tau*i/6+math.pi/6;x,y=.845*math.cos(a),.845*math.sin(a)
    tool=cylinder('bolt bore',.041,.3,(x,y,.13),rubber,48,0);cut(lid,tool)
finish(lid,steel,.006)
parts=[lid]
parts.append(profile('Lid inner machined lip',[(.652,.145),(.652,.172),(.669,.183),(.679,.177),(.668,.163)],edge))
parts.append(profile('Outer concentric engraving',[(.937,.178),(.937,.180),(.942,.180),(.942,.178)],dark,128,0))
for i in range(-8,9):
    x=i*.073;half=math.sqrt(max(0,.658**2-(abs(x)+.018)**2))
    parts.append(cube('Bevelled grille slat',(x,0,.137),(.034,half*2,.065),edge,.007))
for y in [-.29,.29]:
    half=math.sqrt(.654**2-y*y)
    parts.append(cube('Underside reinforcement',(0,y,.105),(half*2,.034,.034),steel,.006))
# Small recessed directional chevrons; no painted fake holes or extra fasteners.
for a in [math.pi/2,3*math.pi/2]:
    for offset in [-1,1]:
        o=cube('Flow chevron',(offset*.022,.78*math.sin(a),.181),(.016,.063,.003),marks,.001)
        o.rotation_euler.z=offset*.56*(1 if a<math.pi else -1);parts.append(o)
lid=join(parts,'Lid')

screws=[]
for i in range(6):
    a=math.tau*i/6+math.pi/6;x,y=.845*math.cos(a),.845*math.sin(a)
    washer=profile('Captive washer',[(.042,.176),(.042,.19),(.112,.19),(.112,.176)],edge,48,0)
    head=cylinder('Hex head',.103,.082,(0,0,.229),bronze,6,0)
    tool=cube('Straight screwdriver slot',(0,0,.271),(.028,.24,.061),rubber,0);cut(head,tool)
    finish(head,bronze,.005)
    shaft=cylinder('Screw shaft',.034,.21,(0,0,.085),steel,24,.002)
    thread=[]
    for z in [.005,.033,.061,.089,.117]:
        thread.append(profile('Thread crest',[(.034,z-.006),(.040,z),(.034,z+.006)],steel,24,0))
    screw=join([washer,head,shaft]+thread,'Screw_%02d'%(i+1),(0,0,.176))
    screw.location=(x,y,.176);screw.rotation_euler.z=a+.2+(i%3)*.12
    screws.append(screw)

bottom=cylinder('Pipe_Shadow_End',.649,.01,(0,0,-2.6),rubber,96,0)
# Independent objects preserve removable lid and all six screws for future interactions.
root=bpy.data.objects.new('ReturnDrain',None);bpy.context.collection.objects.link(root)
for o in [base,pipe,seal,lid,bottom]+screws:o.parent=root
anchors={'Opening':(0,0,.06),'LidLift':(0,0,.14),'PipeExit':(0,0,-2.4)}
for name,at in anchors.items():
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=root;o.location=at

bpy.context.scene.unit_settings.system='METRIC'
for o in bpy.data.objects:
    if o.type!='MESH':continue
    mats=list(o.data.materials); used=[]; remap={}
    for p in o.data.polygons:
        old=p.material_index;m=mats[old]
        if m not in used:used.append(m)
        remap[old]=used.index(m)
    indices=[remap[p.material_index] for p in o.data.polygons]
    o.data.materials.clear()
    for m in used:o.data.materials.append(m)
    for p,i in zip(o.data.polygons,indices):p.material_index=i
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'ReturnDrain.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'ReturnDrain.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
    apply_unit_scale=True,bake_space_transform=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
    mesh_smooth_type='FACE',use_mesh_modifiers=True)
report={o.name:{'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'vertices':len(o.data.vertices)} for o in bpy.data.objects if o.type=='MESH'}
with open(os.path.join(OUT,'mesh-report.json'),'w') as f:json.dump(report,f,indent=2)
with open(os.path.join(OUT,'material-slots.json'),'w') as f:
    json.dump({'meshes':[{'name':o.name,'materials':[m.name for m in o.data.materials]} for o in bpy.data.objects if o.type=='MESH']},f,indent=2)
print('DRAIN_MODEL_OK',sum(d['triangles'] for d in report.values()))
