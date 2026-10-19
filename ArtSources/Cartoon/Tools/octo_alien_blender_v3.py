# Octopus alien - Blender build script (Blender 4.x / 5.x).
# Builds head, eye, antenna, neck skirt, 6 hooked arms with cream teeth on the
# undersides and a round toothed mouth underneath, fuses everything with a voxel
# remesh into ONE welded mesh, paints vertex colours and renders a turnaround.
import bpy, bmesh, math, os, traceback
import numpy as np
from mathutils import Vector, kdtree

OUT = os.path.join(os.path.expanduser("~"), "Documents", "Demo", "ArtSources", "Cartoon", "Source", "OctoAlienV2")
os.makedirs(OUT, exist_ok=True)
LOG = open(os.path.join(OUT, "build_log.txt"), "w", encoding="utf-8")
def log(*a):
    LOG.write(" ".join(str(x) for x in a) + "\n"); LOG.flush(); print(*a)

VOXEL = 0.0065
def B(p):  # model space (y up, +z = face) -> Blender (z up, face toward -Y)
    return Vector((p[0], -p[2], p[1]))

def catmull(points, n):
    P = np.asarray(points, float); P = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]]); out = []
    for i in range(1, len(P) - 2):
        for t in np.linspace(0, 1, n, endpoint=False):
            p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-2]); return np.array(out)

def link(obj):
    bpy.context.scene.collection.objects.link(obj); return obj

def ellipsoid(name, c, r, rot_z_deg=0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1, location=B(c))
    o = bpy.context.active_object; o.name = name
    o.scale = (r[0], r[2], r[1])                         # model (x,y,z) radii -> Blender (x,-z,y)
    o.rotation_euler = (0, -math.radians(rot_z_deg), 0)  # model z-rotation (tilt sideways) -> Blender y-rotation
    return o

def torus(name, c, axis, R, r):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=64, minor_segments=16, location=B(c))
    o = bpy.context.active_object; o.name = name
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(B(axis).normalized())
    return o

def tube(name, pts, radii):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = 1.0; cu.bevel_resolution = 6; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, q, rr in zip(sp.points, pts, radii):
        v = B(q); p.co = (v.x, v.y, v.z, 1); p.radius = float(rr)
    o = link(bpy.data.objects.new(name, cu)); return o

def cone(name, base, direction, h, r):
    d = np.asarray(direction, float); d /= np.linalg.norm(d)
    mid = np.asarray(base) + d * h * .5
    bpy.ops.mesh.primitive_cone_add(vertices=14, radius1=r, radius2=r * .12, depth=h, location=B(mid))
    o = bpy.context.active_object; o.name = name
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(B(d).normalized())
    return o

def arm_frame(pts, j):
    T = pts[min(j + 1, len(pts) - 1)] - pts[max(j - 1, 0)]; T /= np.linalg.norm(T)
    S = np.cross(T, [0, 1, 0]); S /= np.linalg.norm(S)
    N = np.cross(S, T); N /= np.linalg.norm(N)
    return T, S, N

try:
    # ---------------------------------------------------------------- clean scene
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for b in list(block):
            if b.users == 0: block.remove(b)
    parts = []

    # ---------------------------------------------------------------- head, neck, eye, antenna
    P = {"head_rx": 0.42183683845280073, "head_ry": 0.39380604543677855, "head_rz": 0.42540578184721023, "neck_r": 0.13396276955960115, "neck_y": -0.09675349337606957, "skirt_r": 0.18324650662393038, "ant_x": 0.072, "ant_y": 0.8738060454367785, "ant_rx": 0.085, "ant_ry": 0.13, "ant_tilt": 22, "arm_r0": 0.19427805784269706, "arm_r1": 0.020904139360410866, "arm_taper": 0.807679342156948, "arm_y0": -0.1499938889629447, "reach_f": 0.7805161663067647, "reach_s": 0.797263945857433, "reach_b": 0.7482347680754764, "low_f": -0.4589708024877239, "low_s": -0.25, "low_b": -0.44906267697600705, "lift_f": 0.14826288400604362, "lift_s": 0.12, "lift_b": 0.012068511052250654, "curl_f": 0.0607, "curl_s": 0.0923, "curl_b": -0.0273, "hook_f": 0.1314, "hook_s": 0.0013, "hook_b": 0.0045, "tooth": 0.03573071462767859, "elev": 10.392023798734106, "az_q": 53.63280854959442}
    HEAD_C = np.array([0, .31, 0.0])
    parts.append(ellipsoid("Head", HEAD_C, (P["head_rx"], P["head_ry"], P["head_rz"])))
    parts.append(ellipsoid("Neck", (0, P["neck_y"], 0), (P["neck_r"], .14, P["neck_r"])))
    parts.append(ellipsoid("Skirt", (0, P["arm_y0"], 0), (P["skirt_r"], .08, P["skirt_r"])))
    EYE_C = np.array([0, .30, 0.0]); EK = P["head_rx"] / .37; ez = P["head_rz"] * .60
    parts.append(ellipsoid("EyeBulge", EYE_C + (0, 0, ez), (.275 * EK, .275 * EK, .135 * EK)))
    parts.append(torus("EyeRim", EYE_C + (0, 0, ez + .075 * EK), (0, 0, 1), .258 * EK, .045 * EK))
    top = .31 + P["head_ry"]
    stalk = tube("AntennaStalk", np.array([[0, top - .05, -.01], [0, (top + P["ant_y"]) / 2, -.01], [0, P["ant_y"] - P["ant_ry"] * .4, -.01]]), [.06, .05, .045]); parts.append(stalk)
    for side in (-1, 1):
        parts.append(ellipsoid("AntennaLobe" + ("L" if side < 0 else "R"), (P["ant_x"] * side, P["ant_y"], -.01), (P["ant_rx"], P["ant_ry"], P["ant_rx"] * .8), rot_z_deg=P["ant_tilt"] * side))

    # ---------------------------------------------------------------- arms + teeth (fitted centrelines)
    def arm_centreline(group, theta, mirror):
        reach, low, lift, curl = P["reach_" + group], P["low_" + group], P["lift_" + group], P["curl_" + group] * mirror
        ctrl = [(.12, 0, P["arm_y0"]), (.30 * reach / .8, 0, P["arm_y0"] + (low - P["arm_y0"]) * .55),
                (.52 * reach / .8, .15 * curl, low), (.70 * reach / .8, .45 * curl, low + .25 * lift),
                (.80 * reach / .8, .8 * curl, low + .75 * lift), (.80 * reach / .8 - P["hook_" + group], 1.1 * curl, low + 1.2 * lift + .5 * abs(P["hook_" + group]))]
        c = catmull(ctrl, 12)
        u = np.array([math.sin(theta), 0, math.cos(theta)]); lat = np.array([math.cos(theta), 0, -math.sin(theta)])
        return np.array([u * r + lat * l + np.array([0, y, 0]) for r, l, y in c])
    ARMS, TEETH = [], []
    i = 0
    for group, deg in (("f", 30), ("s", 90), ("b", 150)):
        for mirror in (1, -1):
            pts = arm_centreline(group, math.radians(deg * mirror), mirror)
            s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]); s /= s[-1]
            rad = 1.15 * (P["arm_r1"] + (P["arm_r0"] - P["arm_r1"]) * (1 - s) ** P["arm_taper"]); rad[-3:] *= np.array([.92, .8, .6])
            ARMS.append((pts, s, rad))
            arm_obj = tube("Arm%d" % i, pts, rad); parts.append(arm_obj); i += 1
            bpy.ops.object.select_all(action='DESELECT'); arm_obj.select_set(True); bpy.context.view_layer.objects.active = arm_obj
            bpy.ops.object.convert(target='MESH')
            arm_obj = bpy.context.active_object; parts[-1] = arm_obj
            Ns = np.array([arm_frame(pts, j)[2] for j in range(len(pts))])
            for v in arm_obj.data.vertices:
                q = np.array([v.co.x, v.co.z, -v.co.y]); j = int(np.argmin(((pts - q) ** 2).sum(1)))
                off = q - pts[j]; q = q + Ns[j] * (off @ Ns[j]) * (.85 - 1)          # slightly flattened, as fitted
                v.co = B(q)
            for j in range(len(pts)):
                if not (.22 < s[j] < .90) or j % 8: continue
                T, S, N = arm_frame(pts, j); size = 1 - .45 * (s[j] - .3) / .63
                for side in (-1, 1):
                    base = pts[j] + rad[j] * (.85 * side * S - .62 * N)
                    d = .35 * side * S - 1.0 * N + .1 * T; d /= np.linalg.norm(d)
                    th = P["tooth"] * 2.0 * size; TEETH.append((base, d, th, th * .5))
                    parts.append(cone("ArmTooth", base - d * .02, d, th + .02, th * .5))

    # ---------------------------------------------------------------- mouth (underside)
    MOUTH_C = np.array([0, -.14, 0])
    parts.append(torus("MouthLip", MOUTH_C, (0, 1, 0), .13, .05))
    MOUTH_TEETH = []
    for k in range(12):
        a = 2 * math.pi * k / 12; radial = np.array([math.sin(a), 0, math.cos(a)])
        base = MOUTH_C + radial * .12 + np.array([0, -.008, 0]); d = -radial * .9 - np.array([0, .35, 0]); d /= np.linalg.norm(d)
        MOUTH_TEETH.append((base, d))
        parts.append(cone("MouthTooth", base - d * .012, d, .07, .024))
    log("parts", len(parts))

    # ---------------------------------------------------------------- fuse into one mesh
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    body = bpy.context.active_object; body.name = "OctoAlien"
    m = body.modifiers.new("Fuse", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = VOXEL; m.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=m.name)
    log("after remesh", len(body.data.vertices), "verts")
    # soften the blends between parts (fillets), keep volume
    sm = body.modifiers.new("Fillet", 'SMOOTH'); sm.factor = .5; sm.iterations = 4
    bpy.ops.object.modifier_apply(modifier=sm.name); log("smoothed")
    # carve the mouth cavity
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1, location=B(MOUTH_C + (0, .005, 0)))
    cav = bpy.context.active_object; cav.scale = (.11, .11, .10)
    bpy.context.view_layer.objects.active = body; body.select_set(True)
    bm_mod = body.modifiers.new("MouthCavity", 'BOOLEAN'); bm_mod.operation = 'DIFFERENCE'; bm_mod.object = cav; bm_mod.solver = 'EXACT'
    for solver in ('MANIFOLD', 'FLOAT', 'FAST'):
        try: bm_mod.solver = solver; break
        except Exception: pass
    log("boolean solver", bm_mod.solver)
    bpy.ops.object.modifier_apply(modifier=bm_mod.name)
    bpy.data.objects.remove(cav, do_unlink=True)
    m2 = body.modifiers.new("Refuse", 'REMESH'); m2.mode = 'VOXEL'; m2.voxel_size = VOXEL; m2.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=m2.name)
    sm2 = body.modifiers.new("Polish", 'SMOOTH'); sm2.factor = .5; sm2.iterations = 2
    bpy.ops.object.modifier_apply(modifier=sm2.name)
    me = body.data
    for p in me.polygons: p.use_smooth = True
    log("final", len(me.vertices), "verts", len(me.polygons), "faces")

    # ---------------------------------------------------------------- vertex colours
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    nb = np.empty(n * 3, np.float32); me.vertices.foreach_get("normal", nb); nb = nb.reshape(-1, 3)
    V = np.stack([co[:, 0], co[:, 2], -co[:, 1]], 1); Nn = np.stack([nb[:, 0], nb[:, 2], -nb[:, 1]], 1)   # back to model space
    def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float) / 255
    SKIN, SKIN_TOP, UNDER, SPOT = hexc('#f1ddd6'), hexc('#f6e7e1'), hexc('#d99aa6'), hexc('#bf8fd9')
    TOOTH, SCLERA, IRIS_OUT, IRIS_IN, PUPIL = hexc('#f4ead2'), hexc('#f7f0ea'), hexc('#5b3478'), hexc('#3a1f52'), hexc('#160b22')
    CAVITY, LIP = hexc('#4a1a2a'), hexc('#d98f9c')
    def blend(col, target, w): return col * (1 - w[:, None]) + target * w[:, None]
    def nearest(points, queries):
        kd = kdtree.KDTree(len(points))
        for k, p in enumerate(points): kd.insert(Vector(p), k)
        kd.balance(); d = np.empty(len(queries)); ix = np.empty(len(queries), int)
        for q in range(len(queries)):
            _, k, dist = kd.find(Vector(queries[q])); d[q] = dist; ix[q] = k
        return d, ix
    top = np.clip(V[:, 1] - .2, 0, .5)
    col = blend(np.tile(SKIN, (n, 1)), SKIN_TOP, top)
    samples, frames = [], []
    for pts, s, rad in ARMS:
        for j in range(len(pts)):
            T, S, N = arm_frame(pts, j); samples.append(pts[j]); frames.append((N, rad[j], s[j]))
    dist, idx = nearest(samples, V)
    armN = np.array([frames[k][0] for k in idx]); armR = np.array([frames[k][1] for k in idx]); armS = np.array([frames[k][2] for k in idx])
    on_arm = (dist < armR * 1.6 + .02) & (V[:, 1] < .05)
    under = np.clip((-(Nn * armN).sum(1) - .05) / .35, 0, 1) * on_arm * np.clip((armS - .05) / .15, 0, 1)
    col = blend(col, UNDER, under)
    tooth_pts = []
    for base, d, h, r in TEETH: tooth_pts += [base + d * h * f for f in (.3, .6, .9)]
    for base, d in MOUTH_TEETH: tooth_pts += [base + d * .06 * f for f in (.3, .7)]
    tdist, _ = nearest(tooth_pts, V); tooth = np.clip((.026 - tdist) / .01, 0, 1)
    mr = np.hypot(V[:, 0], V[:, 2]); below = V[:, 1] < -.06
    lip = below * np.clip((.20 - mr) / .04, 0, 1); cavity = below * np.clip((.125 - mr) / .015, 0, 1) * (V[:, 1] > -.165)
    col = blend(col, LIP, lip); col = blend(col, CAVITY, cavity)
    er = np.hypot(V[:, 0] - EYE_C[0], V[:, 1] - EYE_C[1])
    rng = np.random.default_rng(7)
    eligible = np.where((under < .1) & (tooth < .1) & (lip < .1) & ~((er < .32 * EK) & (V[:, 2] > .15)) & (V[:, 1] > -.5) & (V[:, 1] < .7))[0]
    centres = V[rng.choice(eligible, 60, replace=False)]; radii = rng.uniform(.018, .058, len(centres))
    spot = np.zeros(n)
    sd, si = nearest(centres, V)
    spot = np.clip((radii[si] - sd) / .006, 0, 1) * (1 - under) * (1 - tooth)
    col = blend(col, SPOT, spot)
    front = V[:, 2] > ez + .03
    sclera = front * np.clip((.232 * EK - er) / .008, 0, 1); iris = front * np.clip((.158 * EK - er) / .006, 0, 1); pupil = front * np.clip((.080 * EK - er) / .02, 0, 1)
    hl = front * np.clip((.03 * EK - np.hypot(V[:, 0] + .06 * EK, V[:, 1] - EYE_C[1] - .065 * EK)) / .006, 0, 1)
    col = blend(col, SCLERA, sclera)
    g = np.clip(er / (.158 * EK), 0, 1)[:, None]; col = blend(col, IRIS_IN * (1 - g) + IRIS_OUT * g, iris)
    col = blend(col, PUPIL, pupil); col = blend(col, np.ones(3), hl); col = blend(col, TOOTH, tooth)
    rgba = np.hstack([np.clip(col, 0, 1), np.ones((n, 1))]).astype(np.float32)
    attr = me.color_attributes.new("Col", 'BYTE_COLOR', 'POINT')
    attr.data.foreach_set("color_srgb", rgba.ravel())
    me.color_attributes.active_color = attr
    try: me.color_attributes.render_color_index = me.color_attributes.active_color_index
    except Exception: pass

    # ---------------------------------------------------------------- material (colour attribute, soft clay)
    mat = bpy.data.materials.new("OctoSkin"); mat.use_nodes = True
    nodes = mat.node_tree.nodes; bsdf = nodes.get("Principled BSDF")
    vc = nodes.new("ShaderNodeAttribute"); vc.attribute_type = 'GEOMETRY'; vc.attribute_name = "Col"
    mat.node_tree.links.new(vc.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = .45
    for key in ("Subsurface Weight", "Subsurface"):
        if key in bsdf.inputs: bsdf.inputs[key].default_value = .08; break
    me.materials.clear(); me.materials.append(mat)
    for poly in me.polygons: poly.material_index = 0
    body.location = (0, 0, .66)                      # stand the arm tips on the ground plane

    # ---------------------------------------------------------------- turnaround renders (Workbench, colour attribute)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 900; sc.render.resolution_y = 800
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'VERTEX'
    sc.display.shading.show_cavity = True; sc.display.shading.cavity_type = 'WORLD'
    sc.display.shading.show_shadows = False; sc.display.shading.show_specular_highlight = True
    sc.render.film_transparent = False
    try:
        sc.world = sc.world or bpy.data.worlds.new("World"); sc.world.color = (1, 1, 1)
    except Exception: pass
    cam_data = bpy.data.cameras.new("Cam"); cam_data.lens = 85
    cam = link(bpy.data.objects.new("Cam", cam_data)); sc.camera = cam
    target = Vector((0, 0, .62))
    views = {"front": 0, "left34": -50, "right34": 50, "back": 180, "side": 90}
    for name, az in views.items():
        a = math.radians(az); dist = 5.2
        cam.location = target + Vector((dist * math.sin(a), -dist * math.cos(a), 1.0))
        cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = (target - cam.location).to_track_quat('-Z', 'Y')
        sc.render.filepath = os.path.join(OUT, "render_%s.png" % name); bpy.ops.render.render(write_still=True)
    cam.location = target + Vector((0, 0, -4.8)); cam.rotation_quaternion = (target - cam.location).to_track_quat('-Z', 'Y')
    sc.render.filepath = os.path.join(OUT, "render_bottom.png"); bpy.ops.render.render(write_still=True)

    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.4
    E = math.radians(P["elev"])
    for name, az, el in (("match_front", 0, E), ("match_q", P["az_q"], E), ("match_back", 180, E), ("match_bottom", 0, None)):
        a = math.radians(az)
        if el is None: cam.location = target + Vector((0, 0, -5))
        else: cam.location = target + Vector((5 * math.cos(el) * math.sin(a), -5 * math.cos(el) * math.cos(a), 5 * math.sin(el)))
        cam.rotation_quaternion = (target - cam.location).to_track_quat('-Z', 'Y')
        sc.render.filepath = os.path.join(OUT, name + ".png"); bpy.ops.render.render(write_still=True)
    cam.data.type = 'PERSP'
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48
    try: sc.cycles.use_denoising = True
    except Exception: pass
    world = sc.world; world.use_nodes = True; bg = world.node_tree.nodes.get("Background")
    if bg: bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = .55
    key = link(bpy.data.objects.new("Key", bpy.data.lights.new("Key", 'AREA'))); key.data.energy = 650; key.data.size = 4
    key.location = (-3, -4, 5); key.rotation_mode = 'QUATERNION'; key.rotation_quaternion = (target - key.location).to_track_quat('-Z', 'Y')
    for name, az, el in (("beauty_front", 0, 1.0), ("beauty_left34", -45, 1.2), ("beauty_bottom", 0, None)):
        a = math.radians(az)
        cam.location = target + (Vector((0, 0, -4.8)) if el is None else Vector((5.2 * math.sin(a), -5.2 * math.cos(a), el)))
        cam.rotation_quaternion = (target - cam.location).to_track_quat('-Z', 'Y')
        sc.render.filepath = os.path.join(OUT, name + ".png"); bpy.ops.render.render(write_still=True); log("rendered", name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "octo_alien_v2.blend"))
    log("DONE", OUT)
except Exception:
    log("ERROR\n" + traceback.format_exc())
finally:
    LOG.close()
