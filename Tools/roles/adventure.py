"""
Adventure / Language Master (語言學家 / 冒險家) 3D Model Generator for Blender.
Accurately implements character/adventure.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Deerstalker hat (幾何獵帽) with dual visors, ear flaps tied on top with ribbon, and boom headset mic
- Silver hair and neatly trimmed silver goatee beard & mustache, rectangular glasses
- Explorer trench coat in olive khaki with storm capelet, plaid scarf, leather satchel & backpack
- Left hand holds an open book/dictionary; right hand grips the globe scepter
- Multi-language globe scepter (多面體多國語權杖) with gear mechanism and spectrum shaft
- Rugged leather explorer boots, dark trousers
- Origin at (0, 0, 0) on ground, height ~ 1.82 units
- Exports to assets/models/adventure.glb
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
        if o.name.startswith("Adventure") or o.name.startswith("part_") or o.name == "Adventure":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Adv") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Adv"):
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
    get_or_create_mat("AdvSkin", (0.92, 0.66, 0.52), rough=0.7)
    get_or_create_mat("AdvCheek", (0.88, 0.44, 0.38), rough=0.7)
    get_or_create_mat("AdvNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("AdvLip", (0.42, 0.14, 0.16), rough=0.6)
    get_or_create_mat("AdvTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("AdvEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("AdvEyeBlue", (0.10, 0.45, 0.82), rough=0.2)
    get_or_create_mat("AdvPupil", (0.03, 0.03, 0.04), rough=0.2)
    
    # Hair & Beard (Silver grey)
    get_or_create_mat("AdvHairGrey", (0.75, 0.77, 0.80), rough=0.8)
    
    # Clothing (Olive khaki trench coat, deep navy under-suit)
    get_or_create_mat("AdvCoatOlive", (0.34, 0.33, 0.22), rough=0.75)
    get_or_create_mat("AdvCoatDark", (0.24, 0.23, 0.16), rough=0.75)
    get_or_create_mat("AdvUnderNavy", (0.12, 0.17, 0.26), rough=0.75)
    get_or_create_mat("AdvPlaidScarf", (0.22, 0.32, 0.25), rough=0.7)
    get_or_create_mat("AdvPlaidStripe", (0.32, 0.30, 0.20), rough=0.7)
    
    # Leather & Gear
    get_or_create_mat("AdvLeather", (0.38, 0.22, 0.12), rough=0.5)
    get_or_create_mat("AdvLeatherDark", (0.24, 0.14, 0.08), rough=0.55)
    get_or_create_mat("AdvBootLeather", (0.32, 0.19, 0.10), rough=0.45)
    get_or_create_mat("AdvBootSole", (0.08, 0.07, 0.06), rough=0.85)
    
    # Book & Accessories
    get_or_create_mat("AdvBookCover", (0.16, 0.28, 0.38), rough=0.5)
    get_or_create_mat("AdvBookPages", (0.92, 0.90, 0.82), rough=0.6)
    get_or_create_mat("AdvBookSymbol", (0.10, 0.55, 0.60), rough=0.4)
    get_or_create_mat("AdvPurpleBook", (0.42, 0.15, 0.52), rough=0.5)
    get_or_create_mat("AdvGreenBook", (0.15, 0.45, 0.25), rough=0.5)
    
    # Scepter (Globe, spectrum shaft, gold & gears)
    get_or_create_mat("AdvGlobeOcean", (0.12, 0.42, 0.82), rough=0.3)
    get_or_create_mat("AdvGlobeLand", (0.18, 0.65, 0.26), rough=0.4)
    get_or_create_mat("AdvGold", (0.95, 0.76, 0.16), metal=0.92, rough=0.20)
    get_or_create_mat("AdvBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("AdvGlass", (0.85, 0.95, 1.0), metal=0.0, rough=0.1, trans=0.85)
    get_or_create_mat("AdvBlack", (0.04, 0.04, 0.05), rough=0.5)
    get_or_create_mat("AdvRed", (0.82, 0.15, 0.15), rough=0.5)
    get_or_create_mat("AdvOrange", (0.92, 0.50, 0.12), rough=0.5)
    get_or_create_mat("AdvYellow", (0.95, 0.85, 0.15), rough=0.5)
    get_or_create_mat("AdvCyan", (0.15, 0.75, 0.85), rough=0.5)
    get_or_create_mat("AdvPurpleLed", (0.65, 0.20, 0.85), rough=0.3)

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

def build_legs_and_boots():
    for s in (-1, 1):
        x = s * 0.13
        # Dark navy trousers
        leg_rings = [
            (0.18, 0.09, 0.09, 0.0),
            (0.36, 0.095, 0.095, 0.0),
            (0.55, 0.115, 0.115, 0.0)
        ]
        l = loft(f"Leg_{s}", "AdvUnderNavy", leg_rings, n=8)
        l.location.x = x
        
        # Explorer combat boots (rugged leather boot with thick sole)
        boot_rings = [
            (0.04, 0.10, 0.13, -0.02),
            (0.14, 0.095, 0.105, 0.0),
            (0.22, 0.095, 0.095, 0.0)
        ]
        b = loft(f"BootShaft_{s}", "AdvBootLeather", boot_rings, n=8)
        b.location.x = x
        
        # Boot toe & foot profile
        icosphere(f"BootFoot_{s}", "AdvBootLeather", (x, -0.06, 0.075), 0.105, scale=(0.95, 1.65, 0.75), subdivisions=1)
        icosphere(f"BootToe_{s}", "AdvBootLeather", (x, -0.13, 0.05), 0.068, scale=(1.05, 1.1, 0.65), subdivisions=1)
        # Thick lugged rubber sole
        box(f"BootSole_{s}", "AdvBootSole", (x, -0.04, 0.018), (0.19, 0.36, 0.035))
        # Boot cuff fold
        cylinder(f"BootCuff_{s}", "AdvLeatherDark", (x, 0.0, 0.22), 0.102, 0.03, segments=8)


def build_torso_and_outfit():
    # 1. Torso Base (Explorer Trench Coat)
    coat_rings = [
        (0.42, 0.36, 0.28, 0.0),     # Bottom coat hem (flaring around thighs)
        (0.56, 0.39, 0.31, -0.01),
        (0.74, 0.42, 0.34, -0.02),
        (0.94, 0.43, 0.35, -0.02),
        (1.08, 0.40, 0.32, -0.01),
        (1.20, 0.32, 0.24, 0.0),
        (1.26, 0.18, 0.14, 0.0)
    ]
    loft("CoatTorsoMain", "AdvCoatOlive", coat_rings, n=12)
    
    # 2. Under-vest / inner tunic visible in center
    vest_rings = [
        (0.55, 0.22, 0.18, -0.16),
        (0.78, 0.24, 0.20, -0.18),
        (1.02, 0.21, 0.18, -0.16),
        (1.18, 0.15, 0.14, -0.12)
    ]
    loft("InnerTunic", "AdvUnderNavy", vest_rings, n=8)
    for z in (0.64, 0.78, 0.92):
        icosphere(f"TunicBtn_{z}", "AdvGold", (0.0, -0.362, z), 0.016, subdivisions=1)
        
    # 3. Shoulder Storm Capelet (經典獵裝雙層風衣披肩)
    cape_rings = [
        (0.94, 0.41, 0.33, -0.02),
        (1.12, 0.38, 0.29, -0.01),
        (1.22, 0.30, 0.22, 0.0),
        (1.27, 0.16, 0.13, 0.0)
    ]
    loft("StormCapelet", "AdvCoatDark", cape_rings, n=10)
    
    # 4. Plaid Scarf wrapped around neck with tail
    loft("ScarfLoop", "AdvPlaidScarf", [(1.20, 0.18, 0.15, 0.0), (1.29, 0.16, 0.14, 0.0)], n=8)
    # Draping scarf ends
    slab("ScarfTail1", "AdvPlaidScarf", (-0.05, -0.18, 1.26), (-0.08, -0.32, 0.85), 0.09, 0.035)
    slab("ScarfTail2", "AdvPlaidStripe", (-0.05, -0.20, 1.15), (-0.075, -0.33, 0.98), 0.035, 0.04)
    
    # 5. Crossbody Leather Strap & Satchel
    # Diagonal strap from right shoulder to left hip
    slab("CrossStrap", "AdvLeather", (0.28, -0.18, 1.25), (-0.30, -0.28, 0.60), 0.055, 0.022)
    # Satchel Bag on left hip
    box("SatchelBag", "AdvLeather", (-0.33, -0.15, 0.60), (0.18, 0.14, 0.18), rot=(0, 0, 0.28))
    box("SatchelFlap", "AdvLeatherDark", (-0.33, -0.16, 0.65), (0.19, 0.15, 0.06), rot=(0, 0, 0.28))
    icosphere("SatchelBuckle", "AdvBrass", (-0.33, -0.23, 0.63), 0.016, subdivisions=1)
    
    # Books inside satchel / coat pockets
    box("PurpleBook", "AdvPurpleBook", (-0.31, -0.31, 0.72), (0.08, 0.035, 0.11), rot=(0, 0, 0.32))
    box("GreenBook", "AdvGreenBook", (0.28, -0.32, 0.70), (0.08, 0.035, 0.11), rot=(0, 0, -0.32))
    
    # 6. Explorer Backpack on back
    box("BackpackMain", "AdvLeather", (0.0, 0.36, 0.96), (0.40, 0.20, 0.46))
    box("BackpackPocket", "AdvLeatherDark", (0.0, 0.47, 0.82), (0.28, 0.08, 0.20))
    # Bedroll / rolled blankets strapped on top of backpack
    cylinder("BedrollRed", "AdvRed", (-0.09, 0.38, 1.21), 0.065, 0.18, segments=8, rot=(0, math.pi / 2, 0))
    cylinder("BedrollGreen", "AdvGreenBook", (0.09, 0.38, 1.21), 0.065, 0.18, segments=8, rot=(0, math.pi / 2, 0))
    # Straps holding bedroll
    for x in (-0.14, 0.0, 0.14):
        torus(f"BedrollStrap_{x}", "AdvLeatherDark", (x, 0.38, 1.21), 0.07, 0.008, seg_major=8, seg_minor=4, rot=(0, math.pi / 2, 0))


def build_arms_and_hands():
    arm_r = 0.088
    
    # ------------------ RIGHT ARM (Gripping Globe Cane) ------------------
    sh_r = (-0.38, 0.0, 1.15)
    elbow_r = (-0.46, -0.06, 0.92)
    wrist_r = (-0.44, -0.15, 0.80)
    
    icosphere("Shoulder_R", "AdvCoatOlive", sh_r, 0.115, subdivisions=1)
    tube("UpperArm_R", "AdvCoatOlive", sh_r, elbow_r, arm_r, segments=8)
    icosphere("Elbow_R", "AdvCoatOlive", elbow_r, 0.09, subdivisions=1)
    tube("ForeArm_R", "AdvCoatOlive", elbow_r, wrist_r, arm_r - 0.005, segments=8)
    tube("Cuff_R", "AdvCoatDark", wrist_r, (-0.44, -0.16, 0.74), 0.088, segments=8)
    
    # Right hand gripping top of globe scepter (globe center x=-0.44, y=-0.16, z=1.05)
    icosphere("Hand_R_Palm", "AdvSkin", (-0.44, -0.16, 1.14), 0.065, scale=(1.1, 1.1, 0.65), subdivisions=1)
    icosphere("Hand_R_Thumb", "AdvSkin", (-0.39, -0.14, 1.10), 0.030, scale=(1.2, 0.8, 0.8), subdivisions=1)
    for i, dx in enumerate((-0.032, 0.0, 0.032)):
        icosphere(f"Hand_R_Finger_{i}", "AdvSkin", (-0.44 + dx, -0.21, 1.09), 0.025, scale=(0.8, 1.2, 0.8), subdivisions=1)
        
    # ------------------ LEFT ARM (Holding Open Book in front) ------------------
    sh_l = (0.38, 0.0, 1.15)
    elbow_l = (0.46, -0.08, 0.90)
    wrist_l = (0.32, -0.24, 0.82)
    
    icosphere("Shoulder_L", "AdvCoatOlive", sh_l, 0.115, subdivisions=1)
    tube("UpperArm_L", "AdvCoatOlive", sh_l, elbow_l, arm_r, segments=8)
    icosphere("Elbow_L", "AdvCoatOlive", elbow_l, 0.09, subdivisions=1)
    tube("ForeArm_L", "AdvCoatOlive", elbow_l, wrist_l, arm_r - 0.005, segments=8)
    tube("Cuff_L", "AdvCoatDark", wrist_l, (0.28, -0.26, 0.80), 0.088, segments=8)
    
    # Left hand supporting open book
    icosphere("Hand_L_Palm", "AdvSkin", (0.24, -0.28, 0.78), 0.055, scale=(1.1, 1.1, 0.7), subdivisions=1)
    icosphere("Hand_L_Thumb", "AdvSkin", (0.22, -0.25, 0.83), 0.026, subdivisions=1)
    
    # Open Book / Dictionary held in hand
    bk_x, bk_y, bk_z = 0.22, -0.34, 0.84
    # Left page & cover
    box("BookLeftCover", "AdvBookCover", (bk_x - 0.055, bk_y, bk_z), (0.09, 0.14, 0.015), rot=(0.15, -0.3, 0.1))
    box("BookLeftPages", "AdvBookPages", (bk_x - 0.055, bk_y, bk_z + 0.012), (0.085, 0.13, 0.018), rot=(0.15, -0.3, 0.1))
    # Right page & cover
    box("BookRightCover", "AdvBookCover", (bk_x + 0.055, bk_y, bk_z), (0.09, 0.14, 0.015), rot=(0.15, 0.3, -0.1))
    box("BookRightPages", "AdvBookPages", (bk_x + 0.055, bk_y, bk_z + 0.012), (0.085, 0.13, 0.018), rot=(0.15, 0.3, -0.1))
    # Book spine
    box("BookSpine", "AdvBookCover", (bk_x, bk_y, bk_z - 0.01), (0.025, 0.14, 0.02), rot=(0.15, 0, 0))


def build_head_and_face():
    head_center = (0.0, -0.03, 1.45)
    icosphere("HeadBase", "AdvSkin", head_center, 0.225, scale=(1.10, 1.02, 1.02), subdivisions=2)
    
    for s in (-1, 1):
        icosphere(f"CheekVol_{s}", "AdvSkin", (s * 0.14, -0.13, 1.40), 0.10, scale=(1.0, 0.9, 0.9), subdivisions=1)
        icosphere(f"CheekBlush_{s}", "AdvCheek", (s * 0.135, -0.185, 1.40), 0.055, scale=(1.1, 0.5, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "AdvSkin", (s * 0.25, -0.02, 1.45), 0.055, scale=(0.45, 0.9, 1.2), subdivisions=1)
        
    icosphere("Nose", "AdvNose", (0.0, -0.235, 1.42), 0.055, scale=(1.05, 0.95, 1.0), subdivisions=1)
    
    # Friendly smiling eyes
    for s in (-1, 1):
        eye_x = s * 0.090
        eye_y = -0.21
        eye_z = 1.495
        icosphere(f"EyeWhite_{s}", "AdvEyeWhite", (eye_x, eye_y, eye_z), 0.046, scale=(1.05, 0.55, 1.0), subdivisions=1)
        icosphere(f"EyeIris_{s}", "AdvEyeBlue", (eye_x, eye_y - 0.024, eye_z), 0.026, scale=(1.0, 0.5, 1.0), subdivisions=1)
        icosphere(f"EyePupil_{s}", "AdvPupil", (eye_x, eye_y - 0.035, eye_z), 0.013, scale=(1.0, 0.5, 1.0), subdivisions=1)
        slab(f"Eyebrow_{s}", "AdvHairGrey", (s * 0.045, -0.225, 1.565), (s * 0.15, -0.20, 1.575), 0.042, 0.025)
        
    # Rectangular Modern Glasses
    for s in (-1, 1):
        gx = s * 0.090
        box(f"GlassRim_{s}", "AdvBlack", (gx, -0.255, 1.495), (0.09, 0.015, 0.065))
        box(f"GlassLens_{s}", "AdvGlass", (gx, -0.256, 1.495), (0.075, 0.006, 0.052))
        tube(f"GlassTemple_{s}", "AdvBlack", (s * 0.14, -0.245, 1.495), (s * 0.24, -0.03, 1.485), 0.007, 4)
    box("GlassBridge", "AdvBlack", (0.0, -0.255, 1.50), (0.03, 0.015, 0.012))
    
    # Silver Goatee Beard & Handlebar Mustache
    # Mustache
    icosphere("StacheCenter", "AdvHairGrey", (0.0, -0.255, 1.39), 0.048, scale=(1.5, 0.85, 0.95), subdivisions=1)
    for s in (-1, 1):
        p_start = (s * 0.02, -0.255, 1.39)
        p_mid = (s * 0.12, -0.24, 1.37)
        p_tip = (s * 0.18, -0.19, 1.40)
        tube(f"StacheWing1_{s}", "AdvHairGrey", p_start, p_mid, 0.038, segments=6, r2=0.030)
        tube(f"StacheWing2_{s}", "AdvHairGrey", p_mid, p_tip, 0.030, segments=6, r2=0.014)
    # Goatee chin beard
    icosphere("ChinBeard", "AdvHairGrey", (0.0, -0.16, 1.28), 0.11, scale=(1.05, 0.95, 0.75), subdivisions=1)
    for s in (-1, 1):
        tube(f"JawBeard_{s}", "AdvHairGrey", (s * 0.18, -0.06, 1.38), (0.0, -0.16, 1.27), 0.045, segments=6, r2=0.035)
        
    # Smile with white teeth
    box("Teeth", "AdvTeeth", (0.0, -0.23, 1.345), (0.10, 0.02, 0.025))
    box("Mouth", "AdvLip", (0.0, -0.225, 1.325), (0.08, 0.02, 0.02))
    
    # Silver Hair mass
    icosphere("HairBack", "AdvHairGrey", (0.0, 0.09, 1.48), 0.21, scale=(1.12, 0.98, 0.88), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"HairSide_{s}", "AdvHairGrey", (s * 0.22, 0.01, 1.48), 0.095, scale=(0.7, 1.1, 1.25), subdivisions=1)


def build_headset_mic():
    # Headset boom mic on left ear
    # Earpiece cup
    icosphere("HeadsetEar", "AdvBlack", (-0.26, -0.02, 1.46), 0.045, scale=(0.5, 1.0, 1.0), subdivisions=1)
    icosphere("HeadsetLed", "AdvPurpleLed", (-0.27, -0.02, 1.46), 0.018, subdivisions=1)
    # Boom arm curving forward to mouth
    tube("BoomArm1", "AdvBlack", (-0.26, -0.02, 1.44), (-0.23, -0.16, 1.36), 0.007, 4)
    tube("BoomArm2", "AdvBlack", (-0.23, -0.16, 1.36), (-0.17, -0.24, 1.34), 0.007, 4)
    # Mic tip
    icosphere("MicTip", "AdvPurpleLed", (-0.16, -0.25, 1.34), 0.018, subdivisions=1)


def build_deerstalker_hat():
    # Deerstalker Hat (幾何獵帽)
    # Crown
    cap_z = 1.57
    cap_rings = [
        (cap_z, 0.24, 0.24, -0.01),
        (cap_z + 0.06, 0.28, 0.28, -0.02),
        (cap_z + 0.14, 0.25, 0.25, -0.02),
        (cap_z + 0.20, 0.14, 0.14, -0.01)
    ]
    loft("HatCrown", "AdvCoatOlive", cap_rings, n=8)
    
    # Dual Visors (Front visor & Rear visor)
    # Front Visor
    front_bm = bmesh.new()
    fv_pts = [
        (-0.16, -0.22, cap_z + 0.01),
        (-0.12, -0.34, cap_z - 0.02),
        (0.0, -0.37, cap_z - 0.025),
        (0.12, -0.34, cap_z - 0.02),
        (0.16, -0.22, cap_z + 0.01),
        (0.13, -0.20, cap_z + 0.03),
        (0.0, -0.23, cap_z + 0.02),
        (-0.13, -0.20, cap_z + 0.03)
    ]
    thick = 0.022
    f_bot = [front_bm.verts.new(p) for p in fv_pts]
    f_top = [front_bm.verts.new((p[0], p[1], p[2] + thick)) for p in fv_pts]
    for i in range(len(fv_pts)):
        front_bm.faces.new((f_bot[i], f_bot[(i + 1) % len(fv_pts)], f_top[(i + 1) % len(fv_pts)], f_top[i]))
    front_bm.faces.new(f_bot[::-1])
    front_bm.faces.new(f_top)
    bmesh.ops.recalc_face_normals(front_bm, faces=front_bm.faces)
    add_mesh_obj("FrontVisor", front_bm, "AdvCoatDark")
    
    # Rear Visor (protecting neck)
    rear_bm = bmesh.new()
    rv_pts = [
        (-0.15, 0.18, cap_z + 0.01),
        (-0.10, 0.30, cap_z - 0.02),
        (0.0, 0.33, cap_z - 0.025),
        (0.10, 0.30, cap_z - 0.02),
        (0.15, 0.18, cap_z + 0.01),
        (0.12, 0.16, cap_z + 0.03),
        (0.0, 0.19, cap_z + 0.02),
        (-0.12, 0.16, cap_z + 0.03)
    ]
    r_bot = [rear_bm.verts.new(p) for p in rv_pts]
    r_top = [rear_bm.verts.new((p[0], p[1], p[2] + thick)) for p in rv_pts]
    for i in range(len(rv_pts)):
        rear_bm.faces.new((r_bot[i], r_bot[(i + 1) % len(rv_pts)], r_top[(i + 1) % len(rv_pts)], r_top[i]))
    rear_bm.faces.new(r_bot[::-1])
    rear_bm.faces.new(r_top)
    bmesh.ops.recalc_face_normals(rear_bm, faces=rear_bm.faces)
    add_mesh_obj("RearVisor", rear_bm, "AdvCoatDark")
    
    # Side Ear Flaps (tied up on top of the hat)
    for s in (-1, 1):
        slab(f"EarFlap_{s}", "AdvCoatOlive", (s * 0.27, -0.02, cap_z + 0.02), (s * 0.08, -0.02, cap_z + 0.20), 0.08, 0.03)
    # Ribbon Bow tying flaps together on crown peak
    box("RibbonKnot", "AdvBlack", (0.0, -0.02, cap_z + 0.22), (0.05, 0.05, 0.03))
    box("RibbonLoop_L", "AdvBlack", (-0.05, -0.02, cap_z + 0.23), (0.06, 0.035, 0.035), rot=(0, 0.3, 0))
    box("RibbonLoop_R", "AdvBlack", (0.05, -0.02, cap_z + 0.23), (0.06, 0.035, 0.035), rot=(0, -0.3, 0))


def build_globe_scepter():
    cx = -0.44
    cy = -0.16
    
    # 1. Base Crystal / Gold Ferrule
    cylinder("ScepterTip", "AdvGold", (cx, cy, 0.03), 0.028, 0.06, segments=8)
    cylinder("ScepterCrystal", "AdvCyan", (cx, cy, 0.01), 0.022, 0.04, segments=6, r2=0.0)
    
    # 2. Multi-language Rainbow / Spectrum Shaft
    spectrum_colors = [
        "AdvPurpleLed", "AdvCyan", "AdvGlobeOcean", "AdvGlobeLand", "AdvYellow", "AdvOrange", "AdvRed"
    ]
    seg_h = 0.82 / len(spectrum_colors)
    for i, c in enumerate(spectrum_colors):
        z_start = 0.06 + i * seg_h
        z_end = z_start + seg_h
        tube(f"SpectrumSeg_{i}", c, (cx, cy, z_start), (cx, cy, z_end), 0.024, segments=8)
        
    # 3. Brass Collar & Mounting Hub
    cylinder("ScepterCollar", "AdvBrass", (cx, cy, 0.89), 0.034, 0.035, segments=8)
    
    # 4. Steampunk Brass Gear surrounding Globe
    gear_r = 0.082
    gear_z = 0.98
    cylinder("GearHub", "AdvGold", (cx, cy - 0.02, gear_z), gear_r, 0.016, segments=12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        ang = k * math.pi / 4
        tx = cx + (gear_r + 0.01) * math.cos(ang)
        tz = gear_z + (gear_r + 0.01) * math.sin(ang)
        box(f"GearTooth_{k}", "AdvGold", (tx, cy - 0.02, tz), (0.018, 0.018, 0.018), rot=(0, -ang, 0))
        
    # 5. Stylized Low-Poly Globe (多面體地球儀)
    globe_z = 1.05
    icosphere("GlobeOcean", "AdvGlobeOcean", (cx, cy, globe_z), 0.095, subdivisions=2)
    # Continents / landmasses on globe
    for (dx, dy, dz, s) in [
        (0.04, -0.06, 0.03, 0.045),
        (-0.05, -0.05, -0.02, 0.042),
        (0.0, 0.06, 0.04, 0.048),
        (-0.04, 0.05, -0.03, 0.038)
    ]:
        icosphere(f"Landmass_{dx}_{dz}", "AdvGlobeLand", (cx + dx, cy + dy, globe_z + dz), s, scale=(1.1, 0.8, 1.1), subdivisions=1)


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
    obj.name = "Adventure"
    obj.data.name = "Adventure"
    
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
    print(f"Adventure finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "adventure.glb")
    
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


def generate_adventure():
    reset_scene()
    setup_materials()
    build_legs_and_boots()
    build_torso_and_outfit()
    build_arms_and_hands()
    build_head_and_face()
    build_headset_mic()
    build_deerstalker_hat()
    build_globe_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_adventure()
