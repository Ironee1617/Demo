import bpy, os
out = os.path.join(os.path.expanduser("~"), "Documents", "Demo", "ArtSources", "Cartoon", "Source", "OctoAlienV2", "inspect_log.txt")
with open(out, "w", encoding="utf-8") as f:
    o = bpy.data.objects.get("OctoAlien")
    f.write("obj %s\n" % o)
    me = o.data
    f.write("color attrs %s active %s render %s\n" % ([a.name + ":" + a.domain + ":" + a.data_type for a in me.color_attributes], me.color_attributes.active_color_name, me.color_attributes.render_color_index))
    f.write("materials %s slots %s\n" % ([m.name for m in me.materials], [s.material and s.material.name for s in o.material_slots]))
    for m in me.materials:
        f.write("use_nodes %s\n" % m.use_nodes)
        for n in m.node_tree.nodes:
            f.write("node %s %s\n" % (n.bl_idname, n.name))
        for l in m.node_tree.links:
            f.write("link %s.%s -> %s.%s\n" % (l.from_node.name, l.from_socket.name, l.to_node.name, l.to_socket.name))
    f.write("engine %s\n" % bpy.context.scene.render.engine)
