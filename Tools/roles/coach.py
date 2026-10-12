"""
Coach / Fitness Trainer (健身教練) 3D Model Generator for Blender.
Accurately implements character/coach.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Backwards navy sports cap (幾何運動帽) with vibrant orange sweatband and rear visor
- Transparent cyan sports sunglasses, athletic stubble beard & confident smile
- Sleeveless navy athletic hoodie vest with bright orange geometric chevron accents & zipper
- Orange gym towel draped around neck, muscular low-poly arms with wristbands
- Left hand holds sports gradient water bottle; right hand grips dumbbell scepter
- Dumbbell scepter (多面體槓片權杖頭) with golden die, barbell weights, brass gears
- Navy athletic shorts with orange stripes, black compression tights, running sneakers
- Origin at (0, 0, 0) on ground, height ~ 1.82 units
- Exports to assets/models/coach.glb
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
        if o.name.startswith("Coach") or o.name.startswith("part_") or o.name == "Coach":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Cch") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Cch"):
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
    get_or_create_mat("CchSkin", (0.92, 0.66, 0.52), rough=0.7)
    get_or_create_mat("CchCheek", (0.88, 0.44, 0.38), rough=0.7)
    get_or_create_mat("CchNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("CchLip", (0.42, 0.14, 0.16), rough=0.6)
    get_or_create_mat("CchTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("CchEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("CchEyeBlue", (0.10, 0.45, 0.82), rough=0.2)
    get_or_create_mat("CchPupil", (0.03, 0.03, 0.04), rough=0.2)
    
    # Hair & Beard (Dark athletic brown)
    get_or_create_mat("CchHairBrown", (0.24, 0.15, 0.10), rough=0.75)
    
    # Clothing (Navy athletic fabric, bright energy orange)
    get_or_create_mat("CchNavyVest", (0.12, 0.18, 0.32), rough=0.7)
    get_or_create_mat("CchOrangeTrim", (0.98, 0.48, 0.08), rough=0.5)
    get_or_create_mat("CchTowelOrange", (0.95, 0.45, 0.06), rough=0.8)
    get_or_create_mat("CchTowelTrim", (0.15, 0.75, 0.85), rough=0.7)
    get_or_create_mat("CchBlackTights", (0.08, 0.08, 0.10), rough=0.8)
    
    # Sports Gear & Bottle
    get_or_create_mat("CchBottleCyan", (0.15, 0.55, 0.85), rough=0.3)
    get_or_create_mat("CchBottlePurple", (0.55, 0.20, 0.75), rough=0.3)
    get_or_create_mat("CchBottleCap", (0.10, 0.10, 0.12), rough=0.5)
    get_or_create_mat("CchFannyPack", (0.28, 0.18, 0.10), rough=0.6)
    get_or_create_mat("CchWristband", (0.14, 0.22, 0.42), rough=0.7)
    
    # Sunglasses & Headset
    get_or_create_mat("CchSunGlass", (0.25, 0.85, 0.95), metal=0.0, rough=0.1, trans=0.82)
    get_or_create_mat("CchGlassFrame", (0.12, 0.14, 0.18), rough=0.5)
    get_or_create_mat("CchHeadsetMic", (0.10, 0.10, 0.12), rough=0.5)
    get_or_create_mat("CchPurpleBadge", (0.55, 0.20, 0.75), rough=0.4)
    
    # Shoes & Scepter
    get_or_create_mat("CchSneakerNavy", (0.14, 0.20, 0.35), rough=0.6)
    get_or_create_mat("CchSneakerOrange", (0.98, 0.48, 0.08), rough=0.5)
    get_or_create_mat("CchSneakerSole", (0.10, 0.10, 0.12), rough=0.85)
    get_or_create_mat("CchGold", (0.95, 0.76, 0.16), metal=0.92, rough=0.20)
    get_or_create_mat("CchBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("CchSteel", (0.65, 0.68, 0.72), metal=0.85, rough=0.3)
    get_or_create_mat("CchBlackWeight", (0.10, 0.10, 0.12), rough=0.6)
    get_or_create_mat("CchYellowBar", (0.95, 0.82, 0.12), rough=0.4)
    get_or_create_mat("CchBlack", (0.04, 0.04, 0.05), rough=0.5)

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

def build_legs_and_shoes():
    for s in (-1, 1):
        x = s * 0.13
        # Black compression tights (lower leg)
        tights_rings = [
            (0.16, 0.088, 0.088, 0.0),
            (0.32, 0.092, 0.092, 0.0),
            (0.42, 0.098, 0.098, 0.0)
        ]
        l = loft(f"LegTights_{s}", "CchBlackTights", tights_rings, n=8)
        l.location.x = x
        
        # Athletic gym shorts (navy with orange stripe)
        shorts_rings = [
            (0.36, 0.125, 0.125, 0.0),
            (0.48, 0.135, 0.135, 0.0),
            (0.58, 0.150, 0.150, 0.0)
        ]
        sh = loft(f"Shorts_{s}", "CchNavyVest", shorts_rings, n=8)
        sh.location.x = x
        # Orange side stripe on shorts
        box(f"ShortsStripe_{s}", "CchOrangeTrim", (s * 0.22, 0.0, 0.46), (0.025, 0.22, 0.18))
        
        # Running Sneakers (navy with orange accents and white/black sole)
        icosphere(f"SneakerUpper_{s}", "CchSneakerNavy", (x, -0.05, 0.075), 0.105, scale=(0.95, 1.65, 0.75), subdivisions=1)
        icosphere(f"SneakerToe_{s}", "CchSneakerOrange", (x, -0.125, 0.048), 0.068, scale=(1.05, 1.1, 0.65), subdivisions=1)
        box(f"SneakerSole_{s}", "CchSneakerSole", (x, -0.04, 0.018), (0.19, 0.36, 0.035))


def build_torso_and_vest():
    # 1. Torso Base (Navy Athletic Sleeveless Hoodie Vest)
    vest_rings = [
        (0.48, 0.35, 0.28, 0.0),
        (0.62, 0.38, 0.31, -0.01),
        (0.80, 0.42, 0.34, -0.02),
        (0.98, 0.43, 0.35, -0.02),
        (1.12, 0.39, 0.31, -0.01),
        (1.20, 0.31, 0.23, 0.0),
        (1.26, 0.17, 0.13, 0.0)
    ]
    loft("AthleticTorsoMain", "CchNavyVest", vest_rings, n=12)
    
    # 2. Orange Energy Chevron Trims & Center Zipper
    for s in (-1, 1):
        slab(f"ChevronUpper_{s}", "CchOrangeTrim", (s * 0.05, -0.26, 1.22), (s * 0.18, -0.32, 1.02), 0.042, 0.025)
        slab(f"ChevronLower_{s}", "CchOrangeTrim", (s * 0.30, -0.24, 0.84), (s * 0.08, -0.38, 0.76), 0.052, 0.025)
    # Center zipper line
    slab("ZipperLine", "CchSteel", (0.0, -0.28, 1.20), (0.0, -0.38, 0.70), 0.018, 0.018)
    # Purple chest badge / logo
    box("ChestBadge", "CchPurpleBadge", (0.16, -0.37, 1.02), (0.055, 0.02, 0.055), rot=(0, 0, 0.78))
    
    # 3. Orange Gym Towel draped over neck & shoulders
    # Towel loop around back of neck
    loft("TowelLoop", "CchTowelOrange", [(1.20, 0.22, 0.18, 0.08), (1.29, 0.20, 0.16, 0.10)], n=8)
    # Front towel drape hanging down chest
    slab("TowelDrape", "CchTowelOrange", (-0.18, -0.22, 1.28), (-0.22, -0.34, 0.75), 0.095, 0.035)
    # Towel cyan edge stripe
    box("TowelStripe", "CchTowelTrim", (-0.22, -0.355, 0.78), (0.085, 0.01, 0.025))
    
    # 4. Athletic Waist Pack / Fanny Pack & Water Bottle Holster
    belt_z = 0.58
    torus("PackBelt", "CchOrangeTrim", (0.0, -0.01, belt_z), 0.37, 0.025, seg_major=12, seg_minor=4)
    box("FannyPack", "CchFannyPack", (0.28, -0.26, belt_z), (0.16, 0.10, 0.11), rot=(0, 0, 0.38))
    # Holstered bottle on left hip
    cylinder("HipBottle", "CchBottleCyan", (-0.28, -0.26, belt_z + 0.06), 0.045, 0.16, segments=8)
    cylinder("HipBottleCap", "CchBottleCap", (-0.28, -0.26, belt_z + 0.15), 0.035, 0.04, segments=8)


def build_arms_and_hands():
    arm_r = 0.095  # Muscular cut low-poly arms
    
    # ------------------ RIGHT ARM (Muscular Bare Arm gripping Dumbbell Scepter) ------------------
    sh_r = (-0.38, 0.0, 1.15)
    elbow_r = (-0.48, -0.06, 0.92)
    wrist_r = (-0.44, -0.15, 0.80)
    
    icosphere("Shoulder_R", "CchSkin", sh_r, 0.125, subdivisions=1)
    tube("Bicep_R", "CchSkin", sh_r, elbow_r, arm_r, segments=8)
    icosphere("Elbow_R", "CchSkin", elbow_r, 0.095, subdivisions=1)
    tube("ForeArm_R", "CchSkin", elbow_r, wrist_r, arm_r - 0.01, segments=8)
    # Athletic wristband
    torus("Wristband_R", "CchWristband", wrist_r, 0.082, 0.018, seg_major=8, seg_minor=4)
    
    # Right hand gripping dumbbell scepter
    icosphere("Hand_R_Palm", "CchSkin", (-0.44, -0.16, 1.12), 0.065, scale=(1.1, 1.1, 0.65), subdivisions=1)
    icosphere("Hand_R_Thumb", "CchSkin", (-0.39, -0.14, 1.08), 0.030, scale=(1.2, 0.8, 0.8), subdivisions=1)
    for i, dx in enumerate((-0.032, 0.0, 0.032)):
        icosphere(f"Hand_R_Finger_{i}", "CchSkin", (-0.44 + dx, -0.21, 1.07), 0.025, scale=(0.8, 1.2, 0.8), subdivisions=1)
        
    # ------------------ LEFT ARM (Holding Shaker Water Bottle) ------------------
    sh_l = (0.38, 0.0, 1.15)
    elbow_l = (0.48, -0.06, 0.92)
    wrist_l = (0.36, -0.22, 0.82)
    
    icosphere("Shoulder_L", "CchSkin", sh_l, 0.125, subdivisions=1)
    tube("Bicep_L", "CchSkin", sh_l, elbow_l, arm_r, segments=8)
    icosphere("Elbow_L", "CchSkin", elbow_l, 0.095, subdivisions=1)
    tube("ForeArm_L", "CchSkin", elbow_l, wrist_l, arm_r - 0.01, segments=8)
    torus("Wristband_L", "CchWristband", wrist_l, 0.082, 0.018, seg_major=8, seg_minor=4)
    
    # Left hand gripping shaker bottle
    icosphere("Hand_L_Palm", "CchSkin", (0.28, -0.26, 0.80), 0.055, scale=(1.1, 1.1, 0.7), subdivisions=1)
    
    # Sports Hydration Shaker Bottle held in left hand
    bot_x, bot_y, bot_z = 0.24, -0.32, 0.84
    cylinder("ShakerBody", "CchBottleCyan", (bot_x, bot_y, bot_z), 0.055, 0.18, segments=8, rot=(0.2, 0.1, -0.2))
    cylinder("ShakerGradient", "CchBottlePurple", (bot_x, bot_y, bot_z - 0.05), 0.054, 0.08, segments=8, rot=(0.2, 0.1, -0.2))
    cylinder("ShakerCap", "CchBottleCap", (bot_x, bot_y, bot_z + 0.10), 0.045, 0.04, segments=8, rot=(0.2, 0.1, -0.2))


def build_head_and_face():
    head_center = (0.0, -0.03, 1.45)
    icosphere("HeadBase", "CchSkin", head_center, 0.225, scale=(1.10, 1.02, 1.02), subdivisions=2)
    
    for s in (-1, 1):
        icosphere(f"CheekVol_{s}", "CchSkin", (s * 0.14, -0.13, 1.40), 0.10, scale=(1.0, 0.9, 0.9), subdivisions=1)
        icosphere(f"CheekBlush_{s}", "CchCheek", (s * 0.135, -0.185, 1.40), 0.055, scale=(1.1, 0.5, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "CchSkin", (s * 0.25, -0.02, 1.45), 0.055, scale=(0.45, 0.9, 1.2), subdivisions=1)
        
    icosphere("Nose", "CchNose", (0.0, -0.235, 1.42), 0.055, scale=(1.05, 0.95, 1.0), subdivisions=1)
    
    # Confident smiling eyes
    for s in (-1, 1):
        eye_x = s * 0.090
        eye_y = -0.21
        eye_z = 1.495
        icosphere(f"EyeWhite_{s}", "CchEyeWhite", (eye_x, eye_y, eye_z), 0.046, scale=(1.05, 0.55, 1.0), subdivisions=1)
        icosphere(f"EyeIris_{s}", "CchEyeBlue", (eye_x, eye_y - 0.024, eye_z), 0.026, scale=(1.0, 0.5, 1.0), subdivisions=1)
        icosphere(f"EyePupil_{s}", "CchPupil", (eye_x, eye_y - 0.035, eye_z), 0.013, scale=(1.0, 0.5, 1.0), subdivisions=1)
        slab(f"Eyebrow_{s}", "CchHairBrown", (s * 0.045, -0.225, 1.565), (s * 0.15, -0.20, 1.575), 0.042, 0.025)
        
    # Futuristic Sports Sunglasses / Visor
    for s in (-1, 1):
        gx = s * 0.090
        box(f"SunGlassLens_{s}", "CchSunGlass", (gx, -0.255, 1.495), (0.09, 0.015, 0.065))
        tube(f"SunGlassArm_{s}", "CchGlassFrame", (s * 0.14, -0.245, 1.495), (s * 0.24, -0.03, 1.485), 0.007, 4)
    box("SunGlassBridge", "CchGlassFrame", (0.0, -0.255, 1.50), (0.035, 0.015, 0.012))
    
    # Athletic Beard Stubble & Mustache
    icosphere("StacheCenter", "CchHairBrown", (0.0, -0.255, 1.39), 0.048, scale=(1.5, 0.85, 0.95), subdivisions=1)
    for s in (-1, 1):
        tube(f"JawStubble_{s}", "CchHairBrown", (s * 0.20, -0.06, 1.40), (0.0, -0.17, 1.28), 0.042, 6, 0.034)
    icosphere("ChinStubble", "CchHairBrown", (0.0, -0.165, 1.28), 0.105, scale=(1.05, 0.95, 0.8), subdivisions=1)
    
    # Confident Smile
    box("Teeth", "CchTeeth", (0.0, -0.23, 1.345), (0.11, 0.02, 0.028))
    box("Mouth", "CchLip", (0.0, -0.225, 1.325), (0.085, 0.02, 0.02))
    
    # Wireless Sports Earbud with Mic on left ear
    icosphere("EarbudCup", "CchHeadsetMic", (-0.26, -0.02, 1.45), 0.035, scale=(0.5, 1.0, 1.0), subdivisions=1)
    tube("EarbudMic", "CchHeadsetMic", (-0.26, -0.02, 1.44), (-0.22, -0.16, 1.38), 0.006, 4)


def build_backwards_cap():
    # Backwards Navy Sports Cap (幾何運動帽)
    cap_z = 1.57
    cap_rings = [
        (cap_z, 0.25, 0.25, -0.01),
        (cap_z + 0.07, 0.29, 0.29, -0.02),
        (cap_z + 0.15, 0.25, 0.25, -0.02),
        (cap_z + 0.22, 0.14, 0.14, -0.01)
    ]
    loft("CapDome", "CchNavyVest", cap_rings, n=8)
    
    # Orange Sweatband around cap base
    loft("OrangeSweatband", "CchOrangeTrim", [(cap_z + 0.01, 0.26, 0.26, -0.01), (cap_z + 0.07, 0.28, 0.28, -0.01)], n=8)
    
    # Backwards Visor (facing backwards over neck)
    rear_bm = bmesh.new()
    rv_pts = [
        (-0.16, 0.18, cap_z + 0.03),
        (-0.11, 0.32, cap_z - 0.01),
        (0.0, 0.36, cap_z - 0.015),
        (0.11, 0.32, cap_z - 0.01),
        (0.16, 0.18, cap_z + 0.03),
        (0.13, 0.16, cap_z + 0.05),
        (0.0, 0.19, cap_z + 0.04),
        (-0.13, 0.16, cap_z + 0.05)
    ]
    thick = 0.022
    r_bot = [rear_bm.verts.new(p) for p in rv_pts]
    r_top = [rear_bm.verts.new((p[0], p[1], p[2] + thick)) for p in rv_pts]
    for i in range(len(rv_pts)):
        rear_bm.faces.new((r_bot[i], r_bot[(i + 1) % len(rv_pts)], r_top[(i + 1) % len(rv_pts)], r_top[i]))
    rear_bm.faces.new(r_bot[::-1])
    rear_bm.faces.new(r_top)
    bmesh.ops.recalc_face_normals(rear_bm, faces=rear_bm.faces)
    add_mesh_obj("BackwardsVisor", rear_bm, "CchNavyVest")


def build_dumbbell_scepter():
    cx = -0.44
    cy = -0.16
    
    # 1. Base Rubber Bumper Foot
    cylinder("ScepterTip", "CchBlackWeight", (cx, cy, 0.03), 0.038, 0.06, segments=8)
    cylinder("ScepterBumper", "CchSteel", (cx, cy, 0.01), 0.042, 0.02, segments=8)
    
    # 2. Barbell Shaft with Yellow Knurling Grip
    tube("BarbellShaft", "CchSteel", (cx, cy, 0.05), (cx, cy, 0.88), 0.022, segments=8)
    tube("GripYellow", "CchYellowBar", (cx, cy, 0.30), (cx, cy, 0.78), 0.028, segments=8)
    
    # 3. Brass Collar & Mounting Hub
    cylinder("ScepterCollar", "CchBrass", (cx, cy, 0.89), 0.036, 0.035, segments=8)
    
    # 4. Steampunk Brass Gear Mechanism
    gear_r = 0.075
    gear_z = 0.96
    cylinder("GearHub", "CchGold", (cx, cy - 0.02, gear_z), gear_r, 0.016, segments=12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        ang = k * math.pi / 4
        tx = cx + (gear_r + 0.01) * math.cos(ang)
        tz = gear_z + (gear_r + 0.01) * math.sin(ang)
        box(f"GearTooth_{k}", "CchGold", (tx, cy - 0.02, tz), (0.018, 0.018, 0.018), rot=(0, -ang, 0))
        
    # 5. Dumbbell Barbell Head (多面體槓片權杖頭)
    db_z = 1.05
    # Central Barbell Bar
    tube("DumbbellBar", "CchSteel", (cx - 0.16, cy, db_z), (cx + 0.16, cy, db_z), 0.022, segments=8)
    # Left and Right Heavy Weight Plates
    for s in (-1, 1):
        # Outer large plate
        cylinder(f"PlateLarge_{s}", "CchBlackWeight", (cx + s * 0.13, cy, db_z), 0.075, 0.035, segments=10, rot=(0, math.pi / 2, 0))
        # Inner smaller plate
        cylinder(f"PlateSmall_{s}", "CchBlackWeight", (cx + s * 0.09, cy, db_z), 0.060, 0.035, segments=10, rot=(0, math.pi / 2, 0))
        # Steel barbell collar clip
        torus(f"BarbellClip_{s}", "CchSteel", (cx + s * 0.16, cy, db_z), 0.026, 0.008, seg_major=8, seg_minor=4, rot=(0, math.pi / 2, 0))
        
    # Center Golden Die atop dumbbell
    die_z = 1.15
    die_size = 0.115
    box("GoldenDie", "CchGold", (cx, cy, die_z), (die_size, die_size, die_size), rot=(0.15, 0.15, 0.35))


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
    obj.name = "Coach"
    obj.data.name = "Coach"
    
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
    print(f"Coach finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "coach.glb")
    
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


def generate_coach():
    reset_scene()
    setup_materials()
    build_legs_and_shoes()
    build_torso_and_vest()
    build_arms_and_hands()
    build_head_and_face()
    build_backwards_cap()
    build_dumbbell_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_coach()
