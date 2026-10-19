import bpy, os, traceback
out = os.path.join(os.path.expanduser("~"), "Documents", "Demo", "ArtSources", "Cartoon", "Source", "OctoAlienV2", "inspect_log2.txt")
f = open(out, "w", encoding="utf-8")
def w(*a): f.write(" ".join(str(x) for x in a) + "\n"); f.flush()
try:
    o = bpy.data.objects.get("OctoAlien"); me = o.data
    w("slots", len(o.material_slots), "mesh mats", len(me.materials))
    for i, m in enumerate(me.materials): w("mat", i, m)
    for m in bpy.data.materials:
        w("data mat", m.name, "users", m.users)
        nt = m.node_tree; w(" tree", nt)
        if nt:
            for n in nt.nodes: w("  node", n.bl_idname, n.name)
            for l in nt.links: w("  link", l.from_node.name, l.from_socket.name, "->", l.to_node.name, l.to_socket.name)
except Exception:
    w(traceback.format_exc())
f.close()
