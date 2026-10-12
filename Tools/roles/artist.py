"""
Artist (藝術家) 3D Model Generator for Blender.
Accurately implements character/artist.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Tilted geometric beret (幾何貝雷帽) with paintbrushes tucked into the side
- Curly volumetric brown hair framing the face, round dark-rimmed glasses
- Navy turtleneck shirt and dark teal painter's apron with colorful paint splatters
- Canvas messenger bag with paintbrushes sticking out
- Left hand holds an artist palette with colorful paint dollops; right hand grips palette scepter
- Palette scepter (多面體藝術權杖頭) with gear mechanism and rainbow spectrum shaft
- High-top canvas sneakers with white toe caps and colorful paint marks
- Origin at (0, 0, 0) on ground, height ~ 1.82 units
- Exports to assets/models/artist.glb
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector, Euler, Matrix

OUT_DIR = r"C:\Users\User\Desktop\code\richman\assets\models"

parts = []
mats = {}

def reset_scene():
    parts.clear()
    mats.clear()
    for o in list(bpy.data.objects):
        if o.name.startswith("Artist") or o.name.startswith("part_") or o.name == "Artist":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Art") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Art"):
            bpy.data.materials.remove(m)

def get_or_create_mat(name, color, metal=0.0, rough=0.6, trans=0.0):
    if name in mats:
        return mats[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = tuple(color) + (1.0,)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if trans > 0.0:
        if "Transmission Weight" in bsdf.inputs:
            bsdf.inputs["Transmission Weight"].default_value = trans
        elif "Transmission" in bsdf.inputs:
            bsdf.inputs["Transmission"].default_value = trans
    mat.diffuse_color = tuple(color) + (1.0,)
    mats[name] = mat
    return mat

def setup_materials():
    # Skin & Face
    get_or_create_mat("ArtSkin", (0.92, 0.66, 0.52), rough=0.7)
    get_or_create_mat("ArtCheek", (0.88, 0.44, 0.38), rough=0.7)
    get_or_create_mat("ArtNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("ArtLip", (0.44, 0.14, 0.16), rough=0.6)
    get_or_create_mat("ArtTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("ArtEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("ArtEyeGreen", (0.12, 0.55, 0.42), rough=0.2)
    get_or_create_mat("ArtPupil", (0.03, 0.03, 0.04), rough=0.2)
    
    # Hair & Eyebrows (Warm wavy brown)
    get_or_create_mat("ArtHairBrown", (0.38, 0.22, 0.12), rough=0.75)
    
    # Clothing (Navy turtleneck, teal/navy apron, faded blue denim pants)
    get_or_create_mat("ArtTurtleDark", (0.08, 0.09, 0.13), rough=0.75)
    get_or_create_mat("ArtShirtNavy", (0.14, 0.18, 0.28), rough=0.75)
    get_or_create_mat("ArtApronTeal", (0.12, 0.20, 0.30), rough=0.75)
    get_or_create_mat("ArtApronPocket", (0.09, 0.14, 0.22), rough=0.75)
    get_or_create_mat("ArtPantsDenim", (0.22, 0.30, 0.40), rough=0.75)
    
    # Beret (Dark slate navy with wine red trim)
    get_or_create_mat("ArtBeretNavy", (0.12, 0.15, 0.24), rough=0.75)
    get_or_create_mat("ArtBeretBand", (0.35, 0.08, 0.14), rough=0.75)
    
    # Paint Splatter Colors (Red, Orange, Yellow, Green, Cyan, Blue, Purple)
    get_or_create_mat("ArtPaintRed", (0.85, 0.14, 0.14), rough=0.4)
    get_or_create_mat("ArtPaintOrange", (0.95, 0.50, 0.10), rough=0.4)
    get_or_create_mat("ArtPaintYellow", (0.95, 0.85, 0.12), rough=0.4)
    get_or_create_mat("ArtPaintGreen", (0.15, 0.70, 0.25), rough=0.4)
    get_or_create_mat("ArtPaintCyan", (0.10, 0.75, 0.85), rough=0.4)
    get_or_create_mat("ArtPaintBlue", (0.12, 0.38, 0.88), rough=0.4)
    get_or_create_mat("ArtPaintPurple", (0.55, 0.18, 0.75), rough=0.4)
    
    # Gear, Palette & Shoes
    get_or_create_mat("ArtCanvasBag", (0.72, 0.64, 0.50), rough=0.7)
    get_or_create_mat("ArtPaletteWood", (0.75, 0.55, 0.34), rough=0.5)
    get_or_create_mat("ArtBrushWood", (0.45, 0.28, 0.14), rough=0.6)
    get_or_create_mat("ArtBrushMetal", (0.65, 0.68, 0.72), metal=0.8, rough=0.3)
    get_or_create_mat("ArtSneakerCanvas", (0.16, 0.18, 0.24), rough=0.7)
    get_or_create_mat("ArtSneakerWhite", (0.88, 0.88, 0.86), rough=0.6)
    get_or_create_mat("ArtSneakerSole", (0.10, 0.10, 0.10), rough=0.85)
    
    # Scepter metals
    get_or_create_mat("ArtGold", (0.95, 0.76, 0.16), metal=0.92, rough=0.20)
    get_or_create_mat("ArtBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("ArtGlass", (0.85, 0.95, 1.0), metal=0.0, rough=0.1, trans=0.85)
    get_or_create_mat("ArtBlack", (0.04, 0.04, 0.05), rough=0.5)

def register_part(obj, mat_name):
    if mat_name in mats:
        if not obj.data.materials:
            obj.data.materials.append(mats[mat_name])
        else:
            obj.data.materials[0] = mats[mat_name]
    parts.append(obj)
    return obj

def add_mesh_obj(name, bm, mat_name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    return register_part(obj, mat_name)

def box(name, mat_name, loc, size, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.location = loc
    obj.scale = size
    obj.rotation_euler = rot
    return register_part(obj, mat_name)

def cylinder(name, mat_name, loc, r, d, segments=8, rot=(0, 0, 0), r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=segments, radius1=r, radius2=r if r2 is None else r2, depth=d)
    obj = bpy.context.object
    obj.name = name
    obj.location = loc
    obj.rotation_euler = rot
    return register_part(obj, mat_name)

def icosphere(name, mat_name, loc, r, scale=(1, 1, 1), subdivisions=1, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=r)
    obj = bpy.context.object
    obj.name = name
    obj.location = loc
    obj.scale = scale
    obj.rotation_euler = rot
    return register_part(obj, mat_name)

def torus(name, mat_name, loc, R, r, seg_major=8, seg_minor=4, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg_major, minor_segments=seg_minor, major_radius=R, minor_radius=r)
    obj = bpy.context.object
    obj.name = name
    obj.location = loc
    obj.rotation_euler = rot
    return register_part(obj, mat_name)

def tube(name, mat_name, p1, p2, r, segments=6, r2=None):
    p1 = Vector(p1)
    p2 = Vector(p2)
    delta = p2 - p1
    length = delta.length
    if length < 1e-5:
        length = 1e-4
    bpy.ops.mesh.primitive_cone_add(vertices=segments, radius1=r, radius2=r if r2 is None else r2, depth=length)
    obj = bpy.context.object
    obj.name = name
    obj.location = (p1 + p2) / 2
    obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    return register_part(obj, mat_name)

def slab(name, mat_name, p1, p2, width, thickness):
    p1 = Vector(p1)
    p2 = Vector(p2)
    delta = p2 - p1
    length = delta.length
    if length < 1e-5:
        length = 1e-4
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.location = (p1 + p2) / 2
    obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    obj.scale = (width, thickness, length)
    return register_part(obj, mat_name)

def loft(name, mat_name, rings, n=8):
    bm = bmesh.new()
    ring_verts = []
    for z, rx, ry, cy in rings:
        rv = []
        for k in range(n):
            angle = math.pi / n + 2 * math.pi * k / n
            x = rx * math.cos(angle)
            y = cy + ry * math.sin(angle)
            rv.append(bm.verts.new((x, y, z)))
        ring_verts.append(rv)
    bm.verts.ensure_lookup_table()
    for i in range(len(ring_verts) - 1):
        for k in range(n):
            v1 = ring_verts[i][k]
            v2 = ring_verts[i][(k + 1) % n]
            v3 = ring_verts[i + 1][(k + 1) % n]
            v4 = ring_verts[i + 1][k]
            bm.faces.new((v1, v2, v3, v4))
    bm.faces.new(ring_verts[0][::-1])
    bm.faces.new(ring_verts[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return add_mesh_obj(name, bm, mat_name)


# -------------------------------------------------------------
# BUILDER FUNCTIONS
# -------------------------------------------------------------

def build_legs_and_sneakers():
    for s in (-1, 1):
        x = s * 0.13
        # Casual rolled denim trousers
        leg_rings = [
            (0.18, 0.092, 0.092, 0.0),
            (0.35, 0.098, 0.098, 0.0),
            (0.55, 0.115, 0.115, 0.0)
        ]
        l = loft(f"Leg_{s}", "ArtPantsDenim", leg_rings, n=8)
        l.location.x = x
        
        # Rolled cuff at ankle
        torus(f"PantsCuff_{s}", "ArtPantsDenim", (x, 0.0, 0.18), 0.095, 0.016, seg_major=8, seg_minor=4)
        
        # High-top canvas sneakers with white toe cap and laces
        # Canvas body
        icosphere(f"SneakerUpper_{s}", "ArtSneakerCanvas", (x, -0.05, 0.072), 0.105, scale=(0.95, 1.65, 0.75), subdivisions=1)
        # White rubber toe cap
        icosphere(f"SneakerToe_{s}", "ArtSneakerWhite", (x, -0.125, 0.048), 0.068, scale=(1.05, 1.1, 0.65), subdivisions=1)
        # White foxing sole
        box(f"SneakerFoxing_{s}", "ArtSneakerWhite", (x, -0.04, 0.024), (0.19, 0.36, 0.032))
        box(f"SneakerSole_{s}", "ArtSneakerSole", (x, -0.04, 0.008), (0.195, 0.365, 0.016))
        # Sneaker paint splatter dots
        icosphere(f"SneakPaint_{s}_1", "ArtPaintRed", (x - 0.06, -0.05, 0.06), 0.014, subdivisions=1)
        icosphere(f"SneakPaint_{s}_2", "ArtPaintCyan", (x + 0.06, -0.02, 0.08), 0.014, subdivisions=1)


def build_torso_and_apron():
    # 1. Torso Base (Navy shirt & Dark Turtleneck)
    shirt_rings = [
        (0.44, 0.35, 0.28, 0.0),
        (0.58, 0.38, 0.31, -0.01),
        (0.76, 0.41, 0.33, -0.02),
        (0.96, 0.42, 0.34, -0.02),
        (1.10, 0.38, 0.30, -0.01),
        (1.20, 0.30, 0.22, 0.0),
        (1.26, 0.16, 0.13, 0.0)
    ]
    loft("ShirtTorsoMain", "ArtShirtNavy", shirt_rings, n=12)
    
    # Turtleneck collar
    cylinder("Turtleneck", "ArtTurtleDark", (0.0, -0.01, 1.25), 0.12, 0.08, segments=8)
    
    # 2. Painter's Apron (Teal/Navy apron front and bib)
    apron_bm = bmesh.new()
    apron_levels = [
        (0.48, 0.34, -0.30),
        (0.66, 0.38, -0.335),
        (0.86, 0.38, -0.35),
        (1.05, 0.32, -0.33),
        (1.18, 0.22, -0.27)
    ]
    apron_rows = []
    for z, w, y in apron_levels:
        p_row = [
            apron_bm.verts.new((-w * 0.5, y + 0.03, z)),
            apron_bm.verts.new((-w * 0.28, y + 0.01, z)),
            apron_bm.verts.new((0.0, y, z)),
            apron_bm.verts.new((w * 0.28, y + 0.01, z)),
            apron_bm.verts.new((w * 0.5, y + 0.03, z))
        ]
        apron_rows.append(p_row)
    apron_bm.verts.ensure_lookup_table()
    
    for i in range(len(apron_rows) - 1):
        for j in range(4):
            v1 = apron_rows[i][j]
            v2 = apron_rows[i][j + 1]
            v3 = apron_rows[i + 1][j + 1]
            v4 = apron_rows[i + 1][j]
            apron_bm.faces.new((v1, v2, v3, v4))
            
    bmesh.ops.recalc_face_normals(apron_bm, faces=apron_bm.faces)
    apron_obj = add_mesh_obj("ApronFront", apron_bm, "ArtApronTeal")
    
    mod = apron_obj.modifiers.new(name="ApronSolidify", type='SOLIDIFY')
    mod.thickness = 0.02
    bpy.context.view_layer.objects.active = apron_obj
    bpy.ops.object.modifier_apply(modifier="ApronSolidify")
    
    # Apron neck straps & waist tie
    slab("ApronStrap_L", "ArtApronPocket", (0.10, -0.25, 1.22), (0.11, -0.10, 1.25), 0.035, 0.015)
    slab("ApronStrap_R", "ArtApronPocket", (-0.10, -0.25, 1.22), (-0.11, -0.10, 1.25), 0.035, 0.015)
    # Front large patch pockets on apron
    box("ApronPocket1", "ArtApronPocket", (0.0, -0.36, 0.94), (0.16, 0.03, 0.14))
    box("ApronPocket2", "ArtApronPocket", (0.16, -0.34, 0.65), (0.11, 0.03, 0.10))
    
    # 3. Colorful Paint Splatters on the apron! (Distinct geometric faceted splotches)
    splatters = [
        ("ArtPaintRed", -0.12, 0.72, 0.045),
        ("ArtPaintYellow", 0.08, 0.76, 0.042),
        ("ArtPaintBlue", -0.18, 0.90, 0.048),
        ("ArtPaintGreen", 0.14, 0.56, 0.045),
        ("ArtPaintOrange", -0.06, 0.62, 0.040),
        ("ArtPaintPurple", 0.11, 1.08, 0.042),
        ("ArtPaintCyan", -0.08, 1.02, 0.038),
        ("ArtPaintYellow", 0.04, 0.88, 0.035),
        ("ArtPaintRed", 0.17, 0.92, 0.042),
        ("ArtPaintGreen", -0.14, 0.80, 0.040),
    ]
    for i, (c, x, z, s) in enumerate(splatters):
        box(f"PaintSplat_{i}", c, (x, -0.365, z), (s, 0.015, s), rot=(0.1, 0, 0.4 * i))
        
    # 4. Canvas Messenger Satchel on hip with brushes
    slab("MessengerStrap", "ArtCanvasBag", (0.28, -0.16, 1.24), (-0.30, -0.26, 0.60), 0.055, 0.02)
    box("MessengerBag", "ArtCanvasBag", (-0.32, -0.15, 0.60), (0.20, 0.14, 0.20), rot=(0, 0, 0.26))
    # Brushes sticking out of satchel
    tube("BagBrush1", "ArtBrushWood", (-0.30, -0.16, 0.68), (-0.28, -0.22, 0.84), 0.010, 4)
    cylinder("BagBrushTip1", "ArtPaintRed", (-0.28, -0.22, 0.84), 0.014, 0.03, segments=4)
    tube("BagBrush2", "ArtBrushWood", (-0.34, -0.15, 0.68), (-0.36, -0.20, 0.82), 0.010, 4)
    cylinder("BagBrushTip2", "ArtPaintCyan", (-0.36, -0.20, 0.82), 0.014, 0.03, segments=4)


def build_arms_and_hands():
    arm_r = 0.088
    
    # ------------------ RIGHT ARM (Holding Palette Scepter) ------------------
    sh_r = (-0.38, 0.0, 1.15)
    elbow_r = (-0.46, -0.06, 0.92)
    wrist_r = (-0.44, -0.15, 0.80)
    
    icosphere("Shoulder_R", "ArtShirtNavy", sh_r, 0.115, subdivisions=1)
    tube("UpperArm_R", "ArtShirtNavy", sh_r, elbow_r, arm_r, segments=8)
    icosphere("Elbow_R", "ArtShirtNavy", elbow_r, 0.09, subdivisions=1)
    tube("ForeArm_R", "ArtShirtNavy", elbow_r, wrist_r, arm_r - 0.005, segments=8)
    # Rolled up sleeve cuff
    torus("RolledCuff_R", "ArtApronPocket", wrist_r, 0.086, 0.016, seg_major=8, seg_minor=4)
    
    # Right hand gripping palette scepter
    icosphere("Hand_R_Palm", "ArtSkin", (-0.44, -0.16, 1.12), 0.065, scale=(1.1, 1.1, 0.65), subdivisions=1)
    icosphere("Hand_R_Thumb", "ArtSkin", (-0.39, -0.14, 1.08), 0.030, scale=(1.2, 0.8, 0.8), subdivisions=1)
    for i, dx in enumerate((-0.032, 0.0, 0.032)):
        icosphere(f"Hand_R_Finger_{i}", "ArtSkin", (-0.44 + dx, -0.21, 1.07), 0.025, scale=(0.8, 1.2, 0.8), subdivisions=1)
        
    # ------------------ LEFT ARM (Holding Artist Palette) ------------------
    sh_l = (0.38, 0.0, 1.15)
    elbow_l = (0.47, -0.07, 0.90)
    wrist_l = (0.34, -0.22, 0.82)
    
    icosphere("Shoulder_L", "ArtShirtNavy", sh_l, 0.115, subdivisions=1)
    tube("UpperArm_L", "ArtShirtNavy", sh_l, elbow_l, arm_r, segments=8)
    icosphere("Elbow_L", "ArtShirtNavy", elbow_l, 0.09, subdivisions=1)
    tube("ForeArm_L", "ArtShirtNavy", elbow_l, wrist_l, arm_r - 0.005, segments=8)
    torus("RolledCuff_L", "ArtApronPocket", wrist_l, 0.086, 0.016, seg_major=8, seg_minor=4)
    
    # Left hand supporting artist palette
    icosphere("Hand_L_Palm", "ArtSkin", (0.26, -0.26, 0.80), 0.055, scale=(1.1, 1.1, 0.7), subdivisions=1)
    icosphere("Hand_L_Thumb", "ArtSkin", (0.24, -0.23, 0.84), 0.026, subdivisions=1)
    
    # Artist Palette in left hand
    pal_x, pal_y, pal_z = 0.22, -0.32, 0.84
    cylinder("HandPalette", "ArtPaletteWood", (pal_x, pal_y, pal_z), 0.16, 0.022, segments=12, rot=(0.2, 0.1, -0.2))
    # Thumb hole
    cylinder("HandPalHole", "ArtBlack", (pal_x + 0.06, pal_y - 0.02, pal_z + 0.01), 0.028, 0.025, segments=8, rot=(0.2, 0.1, -0.2))
    # Colorful paint dollops on hand palette
    pal_paints = [
        ("ArtPaintRed", -0.08, 0.06, 0.025),
        ("ArtPaintBlue", -0.02, 0.10, 0.025),
        ("ArtPaintYellow", 0.07, 0.07, 0.025),
        ("ArtPaintGreen", -0.09, -0.04, 0.025),
        ("ArtPaintPurple", 0.08, -0.02, 0.025),
    ]
    for i, (c, dx, dy, r) in enumerate(pal_paints):
        cylinder(f"HandPalPaint_{i}", c, (pal_x + dx, pal_y + dy, pal_z + 0.02), r, 0.015, segments=8, rot=(0.2, 0.1, -0.2))


def build_head_and_face():
    head_center = (0.0, -0.03, 1.45)
    icosphere("HeadBase", "ArtSkin", head_center, 0.225, scale=(1.10, 1.02, 1.02), subdivisions=2)
    
    for s in (-1, 1):
        icosphere(f"CheekVol_{s}", "ArtSkin", (s * 0.14, -0.13, 1.40), 0.10, scale=(1.0, 0.9, 0.9), subdivisions=1)
        icosphere(f"CheekBlush_{s}", "ArtCheek", (s * 0.135, -0.185, 1.40), 0.055, scale=(1.1, 0.5, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "ArtSkin", (s * 0.25, -0.02, 1.45), 0.055, scale=(0.45, 0.9, 1.2), subdivisions=1)
        
    icosphere("Nose", "ArtNose", (0.0, -0.235, 1.42), 0.055, scale=(1.05, 0.95, 1.0), subdivisions=1)
    
    # Expressive green artist eyes
    for s in (-1, 1):
        eye_x = s * 0.090
        eye_y = -0.21
        eye_z = 1.495
        icosphere(f"EyeWhite_{s}", "ArtEyeWhite", (eye_x, eye_y, eye_z), 0.046, scale=(1.05, 0.55, 1.0), subdivisions=1)
        icosphere(f"EyeIris_{s}", "ArtEyeGreen", (eye_x, eye_y - 0.024, eye_z), 0.026, scale=(1.0, 0.5, 1.0), subdivisions=1)
        icosphere(f"EyePupil_{s}", "ArtPupil", (eye_x, eye_y - 0.035, eye_z), 0.013, scale=(1.0, 0.5, 1.0), subdivisions=1)
        slab(f"Eyebrow_{s}", "ArtHairBrown", (s * 0.045, -0.225, 1.565), (s * 0.15, -0.20, 1.575), 0.042, 0.025)
        
    # Round Artist Glasses
    for s in (-1, 1):
        gx = s * 0.090
        torus(f"GlassRim_{s}", "ArtBlack", (gx, -0.255, 1.495), 0.056, 0.007, seg_major=12, seg_minor=6, rot=(math.pi / 2, 0, 0))
        cylinder(f"GlassLens_{s}", "ArtGlass", (gx, -0.255, 1.495), 0.052, 0.004, segments=12, rot=(math.pi / 2, 0, 0))
        tube(f"GlassTemple_{s}", "ArtBlack", (s * 0.14, -0.245, 1.495), (s * 0.24, -0.03, 1.485), 0.007, 4)
    box("GlassBridge", "ArtBlack", (0.0, -0.255, 1.50), (0.035, 0.015, 0.012))
    
    # Cheerful Smile
    box("Teeth", "ArtTeeth", (0.0, -0.23, 1.35), (0.11, 0.02, 0.028))
    box("Mouth", "ArtLip", (0.0, -0.225, 1.33), (0.085, 0.02, 0.02))
    
    # Volumetric Wavy/Curly Brown Hair Locks
    # Hair mass back
    icosphere("HairBack", "ArtHairBrown", (0.0, 0.09, 1.48), 0.21, scale=(1.12, 0.98, 0.88), subdivisions=1)
    # Curly hair locks surrounding cheeks and forehead
    for k in range(16):
        ang = math.pi * 0.12 + k * (math.pi * 1.76 / 15)
        hx = 0.23 * math.cos(ang)
        hy = 0.20 * math.sin(ang) + 0.01
        hz = 1.42 + 0.04 * (k % 3)
        icosphere(f"HairCurl_{k}", "ArtHairBrown", (hx, hy, hz), 0.075, scale=(1.0, 1.0, 1.2), subdivisions=1)


def build_beret():
    # Tilted French Artist Beret (幾何貝雷帽)
    beret_z = 1.58
    # Beret band
    loft("BeretBand", "ArtBeretBand", [(beret_z, 0.25, 0.25, 0.0), (beret_z + 0.05, 0.26, 0.26, -0.01)], n=8)
    
    # Flaring puffy beret crown tilted stylishly to the side
    beret_bm = bmesh.new()
    b_rings = [
        (beret_z + 0.04, 0.28, 0.28, -0.01),
        (beret_z + 0.12, 0.35, 0.35, 0.02),
        (beret_z + 0.19, 0.31, 0.31, 0.04),
        (beret_z + 0.24, 0.16, 0.16, 0.05)
    ]
    loft("BeretCrown", "ArtBeretNavy", b_rings, n=10)
    # Beret top stalk / tab
    box("BeretStalk", "ArtBeretNavy", (0.02, 0.05, beret_z + 0.26), (0.03, 0.03, 0.05), rot=(0.2, 0.1, 0))
    
    # Paintbrushes tucked into the side of the beret
    tube("HatBrush1", "ArtBrushWood", (0.16, -0.16, beret_z + 0.02), (0.24, -0.18, beret_z + 0.22), 0.012, 4)
    cylinder("HatBrushFerrule1", "ArtBrushMetal", (0.23, -0.18, beret_z + 0.19), 0.014, 0.025, segments=4)
    cylinder("HatBrushTip1", "ArtPaintRed", (0.24, -0.18, beret_z + 0.22), 0.015, 0.035, segments=4)
    
    tube("HatBrush2", "ArtBrushWood", (0.12, -0.22, beret_z + 0.02), (0.18, -0.26, beret_z + 0.20), 0.012, 4)
    cylinder("HatBrushTip2", "ArtPaintYellow", (0.18, -0.26, beret_z + 0.20), 0.015, 0.035, segments=4)


def build_palette_scepter():
    cx = -0.44
    cy = -0.16
    
    # 1. Base Crystal / Ferrule
    cylinder("ScepterTip", "ArtGold", (cx, cy, 0.03), 0.028, 0.06, segments=8)
    cylinder("ScepterCrystal", "ArtPaintPurple", (cx, cy, 0.01), 0.022, 0.04, segments=6, r2=0.0)
    
    # 2. Rainbow Spectrum Shaft
    spectrum_colors = [
        "ArtPaintPurple", "ArtPaintBlue", "ArtPaintCyan", "ArtPaintGreen", "ArtPaintYellow", "ArtPaintOrange", "ArtPaintRed"
    ]
    seg_h = 0.82 / len(spectrum_colors)
    for i, c in enumerate(spectrum_colors):
        z_start = 0.06 + i * seg_h
        z_end = z_start + seg_h
        tube(f"SpectrumSeg_{i}", c, (cx, cy, z_start), (cx, cy, z_end), 0.024, segments=8)
        
    # 3. Brass Collar
    cylinder("ScepterCollar", "ArtBrass", (cx, cy, 0.89), 0.034, 0.035, segments=8)
    
    # 4. Steampunk Brass Gear Mechanism
    gear_r = 0.075
    gear_z = 0.96
    cylinder("GearHub", "ArtGold", (cx, cy - 0.02, gear_z), gear_r, 0.016, segments=12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        ang = k * math.pi / 4
        tx = cx + (gear_r + 0.01) * math.cos(ang)
        tz = gear_z + (gear_r + 0.01) * math.sin(ang)
        box(f"GearTooth_{k}", "ArtGold", (tx, cy - 0.02, tz), (0.018, 0.018, 0.018), rot=(0, -ang, 0))
        
    # 5. Wooden Palette Scepter Head (多面體藝術權杖頭)
    pal_z = 1.06
    cylinder("ScepterPalette", "ArtPaletteWood", (cx, cy - 0.01, pal_z), 0.155, 0.026, segments=12, rot=(math.pi / 2, 0, 0))
    # Thumb hole
    cylinder("ScepterPalHole", "ArtBlack", (cx + 0.05, cy - 0.025, pal_z - 0.03), 0.026, 0.028, segments=8, rot=(math.pi / 2, 0, 0))
    # 5 Colorful Paint Dollops
    paints = [
        ("ArtPaintRed", -0.08, 0.06),
        ("ArtPaintBlue", 0.0, 0.10),
        ("ArtPaintYellow", 0.08, 0.07),
        ("ArtPaintGreen", -0.10, -0.02),
        ("ArtPaintPurple", 0.10, 0.0)
    ]
    for i, (c, dx, dz) in enumerate(paints):
        cylinder(f"ScepterPaint_{i}", c, (cx + dx, cy - 0.025, pal_z + dz), 0.026, 0.016, segments=8, rot=(math.pi / 2, 0, 0))


# -------------------------------------------------------------
# FINISH, CHECK & EXPORT
# -------------------------------------------------------------

def finalize_and_export():
    print(f"Total parts created: {len(parts)}")
    for o in bpy.context.selected_objects:
        o.select_set(False)
        
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "Artist"
    obj.data.name = "Artist"
    
    bpy.ops.object.shade_flat()
    
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # Ground model so bottom of feet is at exactly z = 0.0
    z_min = min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= z_min
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold_edges = sum(1 for e in bm.edges if not e.is_manifold)
    face_count = len(bm.faces)
    vert_count = len(bm.verts)
    bm.free()
    
    dims = tuple(round(d, 3) for d in obj.dimensions)
    print(f"Artist finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "artist.glb")
    
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    
    bpy.ops.export_scene.gltf(
        filepath=glb_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True
    )
    print(f"Exported successfully to: {glb_path}")
    return obj, glb_path


def generate_artist():
    reset_scene()
    setup_materials()
    build_legs_and_sneakers()
    build_torso_and_apron()
    build_arms_and_hands()
    build_head_and_face()
    build_beret()
    build_palette_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_artist()
