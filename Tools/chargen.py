"""Low-poly character helpers for Blender (run inside Blender). Character faces -Y, Z up, feet at z=0."""
import bpy, bmesh, math, os
from mathutils import Vector

OUT = r"C:\Users\User\Desktop\code\richman\assets\models"
parts = []
mats = {}


def reset():
    parts.clear(); mats.clear()
    for o in list(bpy.data.objects):
        if o.type == 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def mat(name, c, metal=0.0, rough=0.7):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = tuple(c) + (1,)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = tuple(c) + (1,)
    mats[name] = m


def std_mats():
    mat("Skin", (0.86, 0.55, 0.42)); mat("Cheek", (0.9, 0.45, 0.38))
    mat("White", (0.93, 0.93, 0.93)); mat("Black", (0.04, 0.04, 0.05))
    mat("Gold", (0.95, 0.68, 0.18), 0.5, 0.4); mat("Brown", (0.30, 0.15, 0.07))
    mat("Leather", (0.35, 0.2, 0.1)); mat("Steel", (0.5, 0.52, 0.56), 0.6, 0.4)
    mat("Glass", (0.75, 0.92, 1.0), 0, 0.2); mat("Mouth", (0.55, 0.15, 0.15))
    mat("Teeth", (0.95, 0.95, 0.9))
    for n, c in (("Red", (0.85, 0.12, 0.12)), ("Orange", (0.95, 0.5, 0.1)), ("Yellow", (0.95, 0.85, 0.15)),
                 ("Green", (0.2, 0.7, 0.25)), ("Blue", (0.15, 0.45, 0.95)), ("Purple", (0.5, 0.2, 0.75))):
        mat(n, c)


def fin(o, m, loc=(0, 0, 0), rot=(0, 0, 0), sc=(1, 1, 1)):
    o.location = loc; o.rotation_euler = rot; o.scale = sc
    o.data.materials.append(mats[m]); parts.append(o); return o


def cube(m, loc, sc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1); return fin(bpy.context.object, m, loc, rot, sc)


def cyl(m, loc, r, d, v=8, sc=(1, 1, 1), rot=(0, 0, 0), r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=v, radius1=r, radius2=r if r2 is None else r2, depth=d)
    return fin(bpy.context.object, m, loc, rot, sc)


def ico(m, loc, r, sc=(1, 1, 1), sub=1, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r)
    return fin(bpy.context.object, m, loc, rot, sc)


def torus(m, loc, R, r, rot=(0, 0, 0), seg=8):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=4, major_radius=R, minor_radius=r)
    return fin(bpy.context.object, m, loc, rot)


def tube(m, a, b, r, v=6, r2=None):
    a = Vector(a); b = Vector(b); d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=v, radius1=r, radius2=r if r2 is None else r2, depth=d.length)
    return fin(bpy.context.object, m, (a + b) / 2, d.to_track_quat('Z', 'Y').to_euler())


def slab(m, a, b, w, t):
    a = Vector(a); b = Vector(b); d = b - a
    bpy.ops.mesh.primitive_cube_add(size=1); o = bpy.context.object
    return fin(o, m, (a + b) / 2, d.to_track_quat('Z', 'Y').to_euler(), (w, t, d.length))


def loft(m, rings, n=8):
    bm = bmesh.new(); rs = []
    for z, rx, ry, cy in rings:
        rs.append([bm.verts.new((rx * math.cos(math.pi / n + 2 * math.pi * k / n),
                                 cy + ry * math.sin(math.pi / n + 2 * math.pi * k / n), z)) for k in range(n)])
    for i in range(len(rs) - 1):
        for k in range(n):
            bm.faces.new((rs[i][k], rs[i][(k + 1) % n], rs[i + 1][(k + 1) % n], rs[i + 1][k]))
    bm.faces.new(rs[0][::-1]); bm.faces.new(rs[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("l"); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("l", me); bpy.context.collection.objects.link(o)
    o.data.materials.append(mats[m]); parts.append(o); return o


# ------------------------------------------------------------------ body
def legs(pants, shoe, sole="Black", baggy=0.0, boots=False, knee=None):
    for s in (-1, 1):
        o = loft(pants, [(0.09, .095 + baggy, .095 + baggy, 0), (0.3, .085 + baggy, .085 + baggy, 0), (0.55, .105 + baggy, .105 + baggy, 0)])
        o.location.x = s * 0.12
        if boots:
            loft(shoe, [(0.0, .1, .12, -0.02), (0.2, .095, .105, 0)]).location.x = s * 0.12
        ico(shoe, (s * 0.12, -0.05, 0.065), 0.1, (0.95, 1.6, 0.7), 1)
        cube(sole, (s * 0.12, -0.03, 0.012), (0.17, 0.34, 0.025))
        if knee:
            ico(knee, (s * 0.12, -0.04, 0.4), 0.09, (1, 0.9, 1.0))


def torso(coat, vest=None, bulk=1.0, trim=None, length=0.0):
    r = [(0.4 - length, .33 * bulk, .25, 0), (0.55, .37 * bulk, .29, -.01), (0.82, .41 * bulk, .34, -.03),
         (1.03, .39 * bulk, .31, -.02), (1.18, .32 * bulk, .24, 0), (1.26, .17, .13, 0)]
    loft(coat, r)
    if vest:
        v = []
        for z, rx, ry, cy in [(0.58, .17, .29, -.01), (0.82, .19, .34, -.03), (1.03, .17, .31, -.02), (1.2, .12, .22, 0)]:
            fr = ry * 0.924 + 0.025; v.append((z, rx, fr / 2, cy - fr / 2))
        loft(vest, v)
    cyl("Skin", (0, -0.01, 1.29), 0.1, 0.08, 8)


def arms(sleeve, cuff, glove=None, bare=False, hip_right=True, cane_x=-0.5):
    sk = glove or "Skin"
    sl = "Skin" if bare else sleeve
    rr = 0.095 if bare else 0.085
    sh = (-0.38, 0, 1.12)
    ico(sleeve if not bare else "Skin", sh, 0.11 if bare else 0.1)
    tube(sl, sh, (-0.47, -0.1, 0.93), rr, 8)
    if bare: ico("Skin", (-0.43, -0.06, 1.02), 0.1)
    tube(cuff, (-0.47, -0.1, 0.93), (-0.5, -0.14, 0.89), 0.075, 8)
    ico(sk, (-0.5, -0.15, 0.87), 0.075, (1, 1, 0.9))
    sh = (0.38, 0, 1.12)
    ico(sleeve if not bare else "Skin", sh, 0.11 if bare else 0.1)
    tube(sl, sh, (0.52, -0.02, 0.9), rr, 8)
    tube(sl, (0.52, -0.02, 0.9), (0.42, -0.12, 0.74), rr - 0.005, 8)
    if bare: ico("Skin", (0.46, -0.0, 1.0), 0.1)
    tube(cuff, (0.42, -0.12, 0.74), (0.4, -0.14, 0.7), 0.07, 8)
    ico(sk, (0.39, -0.16, 0.66), 0.07)


def head(hair=None, hair_sides=True, beard=None, stache=None, eye="Blue", brow="Stache", smile=False, ears=True):
    ico("Skin", (0, 0, 1.47), 0.22, (1.12, 1.0, 1.02), 2)
    ico("Skin", (0, -0.21, 1.43), 0.055, (1, 1, 1.1))
    for s in (-1, 1):
        ico("Cheek", (s * 0.12, -0.17, 1.4), 0.06, (1, 0.6, 1))
        if ears: ico("Skin", (s * 0.245, 0.0, 1.46), 0.05, (0.5, 1, 1.3))
        ico("White", (s * 0.085, -0.2, 1.5), 0.04, (1, 0.5, 1))
        ico(eye, (s * 0.085, -0.225, 1.5), 0.022, (1, 0.5, 1))
        ico("Black", (s * 0.085, -0.236, 1.5), 0.01, (1, 0.5, 1))
        slab(brow, (s * 0.04, -0.215, 1.565), (s * 0.14, -0.195, 1.57 + (0.01 if s > 0 else 0)), 0.035, 0.03)
        if hair and hair_sides:
            ico(hair, (s * 0.215, 0.03, 1.5), 0.1, (0.7, 1.1, 1.3))
    if hair:
        ico(hair, (0, 0.11, 1.5), 0.2, (1.12, 0.9, 0.8))
    if beard:
        ico(beard, (0, -0.09, 1.26), 0.18, (1.0, 0.95, 0.55))
        for s in (-1, 1):
            ico(beard, (s * 0.19, -0.04, 1.37), 0.07, (0.6, 1.3, 1.6))
    if stache:
        for s in (-1, 1):
            tube(stache, (0, -0.235, 1.405), (s * 0.1, -0.222, 1.38), 0.026, 5)
            tube(stache, (s * 0.1, -0.222, 1.38), (s * 0.16, -0.19, 1.4), 0.022, 5, 0.012)
        ico(stache, (0, -0.237, 1.41), 0.03, (1.4, 0.7, 0.8))
    if smile:
        cube("Teeth", (0, -0.22, 1.345), (0.1, 0.02, 0.03))
        cube("Mouth", (0, -0.215, 1.325), (0.08, 0.02, 0.02))
    else:
        cube("Mouth", (0, -0.215, 1.35), (0.07, 0.02, 0.02))


def glasses(frame="Black", lens="Glass", sq=False, z=1.5, r=0.058):
    for s in (-1, 1):
        torus(frame, (s * 0.085, -0.248, z), r, 0.012, (math.pi / 2, 0, 0), 4 if sq else 8)
        cyl(lens, (s * 0.085, -0.248, z), r * 0.95, 0.006, 4 if sq else 8, rot=(math.pi / 2, 0, math.pi / 4 if sq else 0))
        tube(frame, (s * 0.14, -0.235, z), (s * 0.235, -0.04, z + 0.0), 0.008, 4)
    cube(frame, (0, -0.25, z), (0.04, 0.012, 0.012))


def shades(z=1.5):
    for s in (-1, 1):
        cube("Black", (s * 0.095, -0.245, z), (0.12, 0.02, 0.07))
        cube("Glass", (s * 0.095, -0.256, z), (0.1, 0.005, 0.05))
        tube("Black", (s * 0.15, -0.235, z), (s * 0.235, -0.04, z), 0.009, 4)
    cube("Black", (0, -0.25, z + 0.01), (0.04, 0.015, 0.015))


# ------------------------------------------------------------------ hats
def dome(m, z0=1.6, h=0.23, r=0.26, bulge=0.285):
    loft(m, [(z0, r, r, 0), (z0 + 0.06, bulge, bulge, 0), (z0 + 0.16, r - 0.03, r - 0.03, 0), (z0 + h, 0.14, 0.14, 0)])


def brim_front(m, z=1.65, w=0.36, d=0.2, y=-0.3, back=False):
    cube(m, (0, -y if back else y, z), (w, d, 0.03), (0.12 if back else -0.12, 0, 0))


# ------------------------------------------------------------------ cane
def cane_shaft(kind="wood", cx=-0.5, cy=-0.15, top=0.95):
    if kind == "rainbow":
        cols = ["Red", "Orange", "Yellow", "Green", "Blue", "Purple"]
        n = len(cols); seg = (top - 0.06) / n
        for i, c in enumerate(cols):
            tube(c, (cx, cy, 0.06 + i * seg), (cx, cy, 0.06 + (i + 1) * seg), 0.035, 6)
        cyl("Gold", (cx, cy, 0.97), 0.045, 0.04, 8)
        cyl("Purple", (cx, cy, 0.03), 0.035, 0.06, 6, r2=0.0, rot=(math.pi, 0, 0))
    elif kind == "ruler":
        tube("Yellow", (cx, cy, 0.03), (cx, cy, top), 0.035, 4)
        for i in range(10):
            cube("Black", (cx, cy - 0.036, 0.1 + i * 0.085), (0.04, 0.01, 0.012))
        cyl("Steel", (cx, cy, 0.03), 0.04, 0.05, 4)
        cyl("Gold", (cx, cy, 0.97), 0.045, 0.04, 8)
    elif kind == "tech":
        tube("Steel", (cx, cy, 0.03), (cx, cy, top), 0.035, 6)
        for i in range(8):
            cube("Cyan", (cx, cy - 0.034, 0.1 + i * 0.1), (0.045, 0.008, 0.012))
        cyl("Gold", (cx, cy, 0.97), 0.045, 0.04, 8)
    elif kind == "bar":
        tube("Black", (cx, cy, 0.05), (cx, cy, top), 0.03, 6)
        tube("Yellow", (cx, cy, 0.3), (cx, cy, 0.8), 0.036, 6)
        cyl("Steel", (cx, cy, 0.03), 0.09, 0.05, 8)
        cyl("Gold", (cx, cy, 0.97), 0.045, 0.04, 8)
    else:
        tube("Brown", (cx, cy, 0.03), (cx, cy, top), 0.024, 6)
        cyl("Gold", (cx, cy, 0.01), 0.03, 0.04, 6)
        cyl("Gold", (cx, cy, 0.9), 0.04, 0.03, 8)


def gear(cx, cy, gx, gz, gr, m="Gold"):
    cyl(m, (gx, cy - 0.05, gz), gr, 0.02, 12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        a = k * math.pi / 4
        cube(m, (gx + (gr + 0.01) * math.cos(a), cy - 0.05, gz + (gr + 0.01) * math.sin(a)), (0.02, 0.02, 0.02), (0, -a, 0))
    cyl("Brown", (gx, cy - 0.062, gz), gr * 0.35, 0.01, 6, rot=(math.pi / 2, 0, 0))


def die(cx, cy, z, m="Gold", size=0.12):
    cube(m, (cx, cy, z), (size, size, size), (0.3, 0.3, 0.5))
    ico("Black", (cx, cy - 0.085, z + 0.01), 0.015); ico("Black", (cx + 0.04, cy - 0.07, z + 0.06), 0.015)
    ico("Black", (cx - 0.04, cy - 0.07, z - 0.04), 0.015)


def scale_head(cx=-0.5, cy=-0.15, z=1.05):
    tube("Gold", (cx, cy, z - 0.1), (cx, cy, z + 0.06), 0.02, 6)
    slab("Gold", (cx - 0.15, cy, z + 0.06), (cx + 0.15, cy, z + 0.06), 0.02, 0.02)
    for s in (-1, 1):
        cyl("Gold", (cx + s * 0.15, cy, z - 0.01), 0.05, 0.015, 8)
        tube("Gold", (cx + s * 0.15, cy, z + 0.06), (cx + s * 0.1, cy, z - 0.01), 0.004, 4)
        tube("Gold", (cx + s * 0.15, cy, z + 0.06), (cx + s * 0.2, cy, z - 0.01), 0.004, 4)
    gear(cx, cy, cx, z - 0.12, 0.05)
    die(cx, cy, z + 0.14, "Gold", 0.09)


# ------------------------------------------------------------------ finish
def finish(name, export=True):
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    o = bpy.context.object; o.name = name; o.data.name = name
    bpy.ops.object.shade_flat()
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.005)
    bpy.ops.object.mode_set(mode='OBJECT')
    zmin = min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices: v.co.z -= zmin
    o.data.update(); o.location = (0, 0, 0)
    bm = bmesh.new(); bm.from_mesh(o.data)
    nm = sum(1 for e in bm.edges if not e.is_manifold); nf = len(bm.faces); bm.free()
    if export:
        os.makedirs(OUT, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name.lower() + ".glb"), export_format='GLB',
                                  use_selection=True, export_yup=True, export_apply=True)
    return "%s dims=%s faces=%d nonmanifold=%d" % (name, tuple(round(x, 2) for x in o.dimensions), nf, nm)
