"""
Builder / Architect (建築師) 3D Model Generator for Blender.
Accurately implements character/builder.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Yellow safety hardhat (幾何多邊形安全帽) with goggles on front and pencil tucked on side
- Full brown beard and mustache, rectangular dark-rimmed glasses
- Navy work jacket with khaki multi-pocket utility vest, plaid collar
- Heavy utility tool belt with hammer, wrench, tool pouches, brass buckle
- Rolled architectural blueprint cylinder strapped across back
- Left hand holds yellow triangle ruler and open blue blueprint; right hand grips survey ruler scepter
- Survey measuring scepter (多面體測量權杖頭) with prism cube, brass gears, and survey ruler rod
- Navy work trousers with knee patches, steel-toe work boots
- Origin at (0, 0, 0) on ground, height ~ 1.82 units
- Exports to assets/models/builder.glb
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
        if o.name.startswith("Builder") or o.name.startswith("part_") or o.name == "Builder":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Bld") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Bld"):
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
    get_or_create_mat("BldSkin", (0.92, 0.66, 0.52), rough=0.7)
    get_or_create_mat("BldCheek", (0.88, 0.44, 0.38), rough=0.7)
    get_or_create_mat("BldNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("BldLip", (0.42, 0.14, 0.16), rough=0.6)
    get_or_create_mat("BldTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("BldEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("BldEyeBlue", (0.10, 0.45, 0.82), rough=0.2)
    get_or_create_mat("BldPupil", (0.03, 0.03, 0.04), rough=0.2)
    
    # Hair & Beard (Warm chestnut brown)
    get_or_create_mat("BldHairBrown", (0.28, 0.16, 0.09), rough=0.75)
    
    # Clothing (Navy work jacket, khaki utility vest, plaid shirt, navy cargo pants)
    get_or_create_mat("BldJacketNavy", (0.14, 0.20, 0.30), rough=0.75)
    get_or_create_mat("BldVestKhaki", (0.62, 0.52, 0.38), rough=0.75)
    get_or_create_mat("BldPlaidCollar", (0.22, 0.25, 0.28), rough=0.7)
    get_or_create_mat("BldPantsNavy", (0.12, 0.16, 0.24), rough=0.75)
    get_or_create_mat("BldKneePatch", (0.18, 0.24, 0.34), rough=0.75)
    
    # Hardhat & Goggles (Safety Yellow/Gold)
    get_or_create_mat("BldHardhatYellow", (0.95, 0.72, 0.12), rough=0.35)
    get_or_create_mat("BldGoggleFrame", (0.18, 0.20, 0.24), rough=0.6)
    get_or_create_mat("BldGoggleLens", (0.40, 0.85, 0.95), metal=0.0, rough=0.1, trans=0.85)
    get_or_create_mat("BldPencilWood", (0.85, 0.45, 0.15), rough=0.5)
    get_or_create_mat("BldPencilLead", (0.05, 0.05, 0.05), rough=0.5)
    
    # Leather & Tool belt
    get_or_create_mat("BldLeatherBelt", (0.35, 0.20, 0.10), rough=0.5)
    get_or_create_mat("BldLeatherDark", (0.24, 0.14, 0.08), rough=0.55)
    get_or_create_mat("BldSteel", (0.65, 0.68, 0.72), metal=0.85, rough=0.3)
    get_or_create_mat("BldHammerWood", (0.85, 0.50, 0.15), rough=0.5)
    
    # Boots & Blueprint
    get_or_create_mat("BldBootBrown", (0.28, 0.16, 0.09), rough=0.45)
    get_or_create_mat("BldBootSole", (0.08, 0.07, 0.06), rough=0.85)
    get_or_create_mat("BldBlueprintBlue", (0.15, 0.40, 0.75), rough=0.6)
    get_or_create_mat("BldBlueprintLine", (0.85, 0.92, 1.0), rough=0.5)
    get_or_create_mat("BldTriangleYellow", (0.95, 0.80, 0.15), rough=0.4)
    get_or_create_mat("BldBlueprintRoll", (0.88, 0.86, 0.80), rough=0.6)
    
    # Scepter (Prism cube, gears, survey ruler)
    get_or_create_mat("BldGold", (0.95, 0.76, 0.16), metal=0.92, rough=0.20)
    get_or_create_mat("BldBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("BldRulerYellow", (0.95, 0.82, 0.12), rough=0.4)
    get_or_create_mat("BldBlack", (0.04, 0.04, 0.05), rough=0.5)

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
        # Navy cargo trousers
        leg_rings = [
            (0.18, 0.092, 0.092, 0.0),
            (0.35, 0.098, 0.098, 0.0),
            (0.55, 0.115, 0.115, 0.0)
        ]
        l = loft(f"Leg_{s}", "BldPantsNavy", leg_rings, n=8)
        l.location.x = x
        
        # Knee reinforcement patch
        box(f"KneePatch_{s}", "BldKneePatch", (x, -0.09, 0.36), (0.11, 0.025, 0.13))
        
        # Heavy work boots (thick sole, steel toe cap)
        icosphere(f"BootFoot_{s}", "BldBootBrown", (x, -0.06, 0.075), 0.105, scale=(0.95, 1.65, 0.75), subdivisions=1)
        icosphere(f"BootToe_{s}", "BldBootBrown", (x, -0.13, 0.05), 0.068, scale=(1.05, 1.1, 0.65), subdivisions=1)
        # Heavy sole welt
        box(f"BootSole_{s}", "BldBootSole", (x, -0.04, 0.018), (0.19, 0.36, 0.035))


def build_torso_and_vest():
    # 1. Torso Base (Navy Work Jacket)
    jacket_rings = [
        (0.44, 0.36, 0.28, 0.0),
        (0.58, 0.39, 0.31, -0.01),
        (0.76, 0.42, 0.34, -0.02),
        (0.96, 0.43, 0.35, -0.02),
        (1.10, 0.39, 0.31, -0.01),
        (1.20, 0.31, 0.23, 0.0),
        (1.26, 0.17, 0.13, 0.0)
    ]
    loft("JacketTorsoMain", "BldJacketNavy", jacket_rings, n=12)
    
    # Plaid shirt collar in neck V
    cylinder("PlaidNeck", "BldPlaidCollar", (0.0, -0.01, 1.25), 0.12, 0.08, segments=8)
    slab("CollarTab_L", "BldPlaidCollar", (0.04, -0.16, 1.25), (0.10, -0.21, 1.18), 0.035, 0.02)
    slab("CollarTab_R", "BldPlaidCollar", (-0.04, -0.16, 1.25), (-0.10, -0.21, 1.18), 0.035, 0.02)
    
    # 2. Khaki Multi-pocket Utility Vest
    vest_bm = bmesh.new()
    vest_levels = [
        (0.50, 0.36, -0.30),
        (0.68, 0.40, -0.335),
        (0.88, 0.40, -0.35),
        (1.06, 0.34, -0.33),
        (1.18, 0.24, -0.27)
    ]
    vest_rows = []
    for z, w, y in vest_levels:
        p_row = [
            vest_bm.verts.new((-w * 0.5, y + 0.03, z)),
            vest_bm.verts.new((-w * 0.28, y + 0.01, z)),
            vest_bm.verts.new((0.0, y, z)),
            vest_bm.verts.new((w * 0.28, y + 0.01, z)),
            vest_bm.verts.new((w * 0.5, y + 0.03, z))
        ]
        vest_rows.append(p_row)
    vest_bm.verts.ensure_lookup_table()
    
    for i in range(len(vest_rows) - 1):
        for j in range(4):
            v1 = vest_rows[i][j]
            v2 = vest_rows[i][j + 1]
            v3 = vest_rows[i + 1][j + 1]
            v4 = vest_rows[i + 1][j]
            vest_bm.faces.new((v1, v2, v3, v4))
            
    bmesh.ops.recalc_face_normals(vest_bm, faces=vest_bm.faces)
    vest_obj = add_mesh_obj("UtilityVest", vest_bm, "BldVestKhaki")
    
    mod = vest_obj.modifiers.new(name="VestSolidify", type='SOLIDIFY')
    mod.thickness = 0.022
    bpy.context.view_layer.objects.active = vest_obj
    bpy.ops.object.modifier_apply(modifier="VestSolidify")
    
    # Vest Chest Pockets (左右各兩個多功能立體口袋)
    for s in (-1, 1):
        # Upper chest pocket
        box(f"VestPocketUp_{s}", "BldVestKhaki", (s * 0.14, -0.36, 1.02), (0.11, 0.035, 0.10))
        box(f"PocketFlapUp_{s}", "BldJacketNavy", (s * 0.14, -0.375, 1.06), (0.115, 0.025, 0.025))
        # Lower vest pocket
        box(f"VestPocketLow_{s}", "BldVestKhaki", (s * 0.16, -0.34, 0.72), (0.12, 0.035, 0.11))
        box(f"PocketFlapLow_{s}", "BldJacketNavy", (s * 0.16, -0.355, 0.76), (0.125, 0.025, 0.025))
        
    # 3. Heavy Utility Tool Belt (工具皮帶與金屬掛件)
    belt_z = 0.58
    torus("ToolBeltLoop", "BldLeatherBelt", (0.0, -0.01, belt_z), 0.38, 0.032, seg_major=12, seg_minor=6)
    # Heavy Brass Buckle
    box("BeltBuckle", "BldBrass", (0.0, -0.385, belt_z), (0.11, 0.03, 0.08))
    box("BeltProng", "BldSteel", (0.0, -0.395, belt_z), (0.03, 0.02, 0.06))
    
    # Left & Right Tool Pouches on Belt
    box("ToolPouch_R", "BldLeatherDark", (-0.28, -0.28, belt_z - 0.04), (0.14, 0.12, 0.14), rot=(0, 0, -0.35))
    box("ToolPouch_L", "BldLeatherDark", (0.28, -0.28, belt_z - 0.04), (0.14, 0.12, 0.14), rot=(0, 0, 0.35))
    
    # Hammer hanging in hammer loop on right hip
    torus("HammerLoop", "BldSteel", (-0.18, -0.33, belt_z - 0.02), 0.04, 0.008, seg_major=8, seg_minor=4)
    tube("HammerHandle", "BldHammerWood", (-0.18, -0.33, belt_z + 0.02), (-0.18, -0.33, belt_z - 0.24), 0.016, 6)
    box("HammerHead", "BldSteel", (-0.18, -0.33, belt_z + 0.03), (0.11, 0.04, 0.045))
    
    # Wrench hanging on left hip
    slab("WrenchShaft", "BldSteel", (0.18, -0.33, belt_z), (0.18, -0.33, belt_z - 0.18), 0.025, 0.012)
    cylinder("WrenchHead", "BldSteel", (0.18, -0.33, belt_z - 0.18), 0.026, 0.012, segments=6)
    
    # 4. Rolled Blueprint Cylinder strapped across the back
    cylinder("BlueprintRoll", "BldBlueprintRoll", (0.14, 0.36, 1.05), 0.085, 0.65, segments=8, rot=(0.1, 0.45, 0.0))
    cylinder("BlueprintCap1", "BldLeatherDark", (0.14, 0.36, 1.05), 0.09, 0.66, segments=8, rot=(0.1, 0.45, 0.0))
    # Blueprint carry strap
    slab("BlueprintStrap", "BldLeatherBelt", (0.24, -0.16, 1.25), (-0.25, 0.28, 0.65), 0.05, 0.02)


def build_arms_and_hands():
    arm_r = 0.088
    
    # ------------------ RIGHT ARM (Holding Survey Scepter) ------------------
    sh_r = (-0.38, 0.0, 1.15)
    elbow_r = (-0.46, -0.06, 0.92)
    wrist_r = (-0.44, -0.15, 0.80)
    
    icosphere("Shoulder_R", "BldJacketNavy", sh_r, 0.115, subdivisions=1)
    tube("UpperArm_R", "BldJacketNavy", sh_r, elbow_r, arm_r, segments=8)
    icosphere("Elbow_R", "BldJacketNavy", elbow_r, 0.09, subdivisions=1)
    tube("ForeArm_R", "BldJacketNavy", elbow_r, wrist_r, arm_r - 0.005, segments=8)
    torus("JacketCuff_R", "BldKneePatch", wrist_r, 0.086, 0.016, seg_major=8, seg_minor=4)
    
    # Right hand gripping survey scepter
    icosphere("Hand_R_Palm", "BldSkin", (-0.44, -0.16, 1.12), 0.065, scale=(1.1, 1.1, 0.65), subdivisions=1)
    icosphere("Hand_R_Thumb", "BldSkin", (-0.39, -0.14, 1.08), 0.030, scale=(1.2, 0.8, 0.8), subdivisions=1)
    for i, dx in enumerate((-0.032, 0.0, 0.032)):
        icosphere(f"Hand_R_Finger_{i}", "BldSkin", (-0.44 + dx, -0.21, 1.07), 0.025, scale=(0.8, 1.2, 0.8), subdivisions=1)
        
    # ------------------ LEFT ARM (Holding Triangle Ruler & Blueprint) ------------------
    sh_l = (0.38, 0.0, 1.15)
    elbow_l = (0.47, -0.07, 0.90)
    wrist_l = (0.32, -0.24, 0.82)
    
    icosphere("Shoulder_L", "BldJacketNavy", sh_l, 0.115, subdivisions=1)
    tube("UpperArm_L", "BldJacketNavy", sh_l, elbow_l, arm_r, segments=8)
    icosphere("Elbow_L", "BldJacketNavy", elbow_l, 0.09, subdivisions=1)
    tube("ForeArm_L", "BldJacketNavy", elbow_l, wrist_l, arm_r - 0.005, segments=8)
    torus("JacketCuff_L", "BldKneePatch", wrist_l, 0.086, 0.016, seg_major=8, seg_minor=4)
    
    # Left hand supporting blueprint and triangle ruler
    icosphere("Hand_L_Palm", "BldSkin", (0.24, -0.28, 0.78), 0.055, scale=(1.1, 1.1, 0.7), subdivisions=1)
    icosphere("Hand_L_Thumb", "BldSkin", (0.22, -0.25, 0.83), 0.026, subdivisions=1)
    
    # Open Architectural Blueprint in hands
    bp_x, bp_y, bp_z = 0.20, -0.34, 0.82
    box("BlueprintSheet", "BldBlueprintBlue", (bp_x, bp_y, bp_z), (0.24, 0.18, 0.015), rot=(0.25, 0.15, -0.1))
    box("BlueprintGrid", "BldBlueprintLine", (bp_x, bp_y, bp_z + 0.01), (0.20, 0.14, 0.016), rot=(0.25, 0.15, -0.1))
    
    # Yellow Triangle Ruler (三角板) held in hand
    slab("TriangleRuler1", "BldTriangleYellow", (bp_x - 0.06, bp_y - 0.04, bp_z + 0.02), (bp_x + 0.06, bp_y - 0.04, bp_z + 0.02), 0.02, 0.01)
    slab("TriangleRuler2", "BldTriangleYellow", (bp_x - 0.06, bp_y - 0.04, bp_z + 0.02), (bp_x - 0.06, bp_y + 0.08, bp_z + 0.02), 0.02, 0.01)
    slab("TriangleRuler3", "BldTriangleYellow", (bp_x - 0.06, bp_y + 0.08, bp_z + 0.02), (bp_x + 0.06, bp_y - 0.04, bp_z + 0.02), 0.02, 0.01)


def build_head_and_face():
    head_center = (0.0, -0.03, 1.45)
    icosphere("HeadBase", "BldSkin", head_center, 0.225, scale=(1.10, 1.02, 1.02), subdivisions=2)
    
    for s in (-1, 1):
        icosphere(f"CheekVol_{s}", "BldSkin", (s * 0.14, -0.13, 1.40), 0.10, scale=(1.0, 0.9, 0.9), subdivisions=1)
        icosphere(f"CheekBlush_{s}", "BldCheek", (s * 0.135, -0.185, 1.40), 0.055, scale=(1.1, 0.5, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "BldSkin", (s * 0.25, -0.02, 1.45), 0.055, scale=(0.45, 0.9, 1.2), subdivisions=1)
        
    icosphere("Nose", "BldNose", (0.0, -0.235, 1.42), 0.055, scale=(1.05, 0.95, 1.0), subdivisions=1)
    
    # Eyes
    for s in (-1, 1):
        eye_x = s * 0.090
        eye_y = -0.21
        eye_z = 1.495
        icosphere(f"EyeWhite_{s}", "BldEyeWhite", (eye_x, eye_y, eye_z), 0.046, scale=(1.05, 0.55, 1.0), subdivisions=1)
        icosphere(f"EyeIris_{s}", "BldEyeBlue", (eye_x, eye_y - 0.024, eye_z), 0.026, scale=(1.0, 0.5, 1.0), subdivisions=1)
        icosphere(f"EyePupil_{s}", "BldPupil", (eye_x, eye_y - 0.035, eye_z), 0.013, scale=(1.0, 0.5, 1.0), subdivisions=1)
        slab(f"Eyebrow_{s}", "BldHairBrown", (s * 0.045, -0.225, 1.565), (s * 0.15, -0.20, 1.575), 0.042, 0.025)
        
    # Rectangular Safety Glasses
    for s in (-1, 1):
        gx = s * 0.090
        box(f"GlassRim_{s}", "BldBlack", (gx, -0.255, 1.495), (0.09, 0.015, 0.065))
        box(f"GlassLens_{s}", "BldGoggleLens", (gx, -0.256, 1.495), (0.075, 0.006, 0.052))
        tube(f"GlassTemple_{s}", "BldBlack", (s * 0.14, -0.245, 1.495), (s * 0.24, -0.03, 1.485), 0.007, 4)
    box("GlassBridge", "BldBlack", (0.0, -0.255, 1.50), (0.03, 0.015, 0.012))
    
    # Full Trimmed Brown Beard & Mustache
    icosphere("StacheCenter", "BldHairBrown", (0.0, -0.255, 1.39), 0.048, scale=(1.5, 0.85, 0.95), subdivisions=1)
    for s in (-1, 1):
        tube(f"StacheWing_{s}", "BldHairBrown", (s * 0.02, -0.255, 1.39), (s * 0.16, -0.22, 1.38), 0.038, 6, 0.024)
        tube(f"JawBeard_{s}", "BldHairBrown", (s * 0.20, -0.06, 1.40), (0.0, -0.18, 1.28), 0.048, 6, 0.038)
    icosphere("ChinBeard", "BldHairBrown", (0.0, -0.17, 1.28), 0.115, scale=(1.05, 0.95, 0.8), subdivisions=1)
    
    # Smile with white teeth
    box("Teeth", "BldTeeth", (0.0, -0.23, 1.345), (0.10, 0.02, 0.025))
    box("Mouth", "BldLip", (0.0, -0.225, 1.325), (0.08, 0.02, 0.02))
    
    # Hair Back
    icosphere("HairBack", "BldHairBrown", (0.0, 0.09, 1.48), 0.21, scale=(1.12, 0.98, 0.88), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"HairSide_{s}", "BldHairBrown", (s * 0.22, 0.01, 1.48), 0.095, scale=(0.7, 1.1, 1.25), subdivisions=1)


def build_hardhat():
    # Yellow Geometric Safety Hardhat (幾何多邊形安全帽)
    hat_z = 1.57
    # Hardhat dome
    hat_rings = [
        (hat_z, 0.25, 0.25, -0.01),
        (hat_z + 0.07, 0.29, 0.29, -0.02),
        (hat_z + 0.15, 0.25, 0.25, -0.02),
        (hat_z + 0.22, 0.14, 0.14, -0.01)
    ]
    loft("HardhatDome", "BldHardhatYellow", hat_rings, n=8)
    
    # Center reinforcement ridge on hardhat
    slab("HardhatRidge", "BldHardhatYellow", (0.0, -0.26, hat_z + 0.08), (0.0, 0.26, hat_z + 0.08), 0.05, 0.03)
    
    # Hardhat front visor
    visor_bm = bmesh.new()
    v_pts = [
        (-0.16, -0.22, hat_z + 0.01),
        (-0.12, -0.34, hat_z - 0.01),
        (0.0, -0.37, hat_z - 0.015),
        (0.12, -0.34, hat_z - 0.01),
        (0.16, -0.22, hat_z + 0.01),
        (0.13, -0.20, hat_z + 0.03),
        (0.0, -0.23, hat_z + 0.02),
        (-0.13, -0.20, hat_z + 0.03)
    ]
    thick = 0.024
    bot_v = [visor_bm.verts.new(p) for p in v_pts]
    top_v = [visor_bm.verts.new((p[0], p[1], p[2] + thick)) for p in v_pts]
    for i in range(len(v_pts)):
        visor_bm.faces.new((bot_v[i], bot_v[(i + 1) % len(v_pts)], top_v[(i + 1) % len(v_pts)], top_v[i]))
    visor_bm.faces.new(bot_v[::-1])
    visor_bm.faces.new(top_v)
    bmesh.ops.recalc_face_normals(visor_bm, faces=visor_bm.faces)
    add_mesh_obj("HardhatVisor", visor_bm, "BldHardhatYellow")
    
    # Protective Goggles mounted on hardhat forehead
    box("GoggleStrap", "BldBlack", (0.0, -0.01, hat_z + 0.08), (0.58, 0.58, 0.035))
    for s in (-1, 1):
        g_x = s * 0.10
        box(f"HatGoggleFrame_{s}", "BldGoggleFrame", (g_x, -0.29, hat_z + 0.08), (0.09, 0.03, 0.065))
        box(f"HatGoggleLens_{s}", "BldGoggleLens", (g_x, -0.30, hat_z + 0.08), (0.075, 0.015, 0.05))
        
    # Carpenter Pencil tucked on right side of hardhat
    tube("PencilShaft", "BldPencilWood", (0.24, -0.16, hat_z + 0.04), (0.28, -0.12, hat_z + 0.18), 0.012, 4)
    cylinder("PencilTip", "BldPencilLead", (0.28, -0.12, hat_z + 0.18), 0.012, 0.025, segments=4, r2=0.0)


def build_survey_scepter():
    cx = -0.44
    cy = -0.16
    
    # 1. Base Metal Survey Foot / Ferrule
    cylinder("ScepterTip", "BldSteel", (cx, cy, 0.03), 0.032, 0.06, segments=4)
    cylinder("ScepterFoot", "BldBrass", (cx, cy, 0.01), 0.036, 0.02, segments=8)
    
    # 2. Surveyor Measuring Ruler Shaft (Yellow with Black Tick Marks)
    # Square ruler rod
    box("RulerShaft", "BldRulerYellow", (cx, cy, 0.48), (0.048, 0.048, 0.84))
    # Black survey tick marks along shaft
    for k in range(10):
        mz = 0.12 + k * 0.08
        box(f"RulerTick_{k}", "BldBlack", (cx, cy - 0.025, mz), (0.035, 0.005, 0.016))
        
    # 3. Brass Collar & Mounting Hub
    cylinder("ScepterCollar", "BldBrass", (cx, cy, 0.90), 0.036, 0.035, segments=8)
    
    # 4. Steampunk Brass Gear Mechanism
    gear_r = 0.075
    gear_z = 0.96
    cylinder("GearHub", "BldGold", (cx, cy - 0.02, gear_z), gear_r, 0.016, segments=12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        ang = k * math.pi / 4
        tx = cx + (gear_r + 0.01) * math.cos(ang)
        tz = gear_z + (gear_r + 0.01) * math.sin(ang)
        box(f"GearTooth_{k}", "BldGold", (tx, cy - 0.02, tz), (0.018, 0.018, 0.018), rot=(0, -ang, 0))
        
    # 5. Survey Theodolite Prism Cube (多面體測量權杖頭)
    prism_z = 1.06
    box("PrismCube", "BldSteel", (cx, cy, prism_z), (0.12, 0.12, 0.12), rot=(0.15, 0.15, 0.35))
    box("PrismGoldCore", "BldGold", (cx, cy, prism_z + 0.04), (0.07, 0.07, 0.05), rot=(0.15, 0.15, 0.35))


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
    obj.name = "Builder"
    obj.data.name = "Builder"
    
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
    print(f"Builder finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "builder.glb")
    
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


def generate_builder():
    reset_scene()
    setup_materials()
    build_legs_and_boots()
    build_torso_and_vest()
    build_arms_and_hands()
    build_head_and_face()
    build_hardhat()
    build_survey_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_builder()
