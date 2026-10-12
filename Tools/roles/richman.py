"""
Refined Richman (Host / 主持人) 3D Model Generator for Blender.
Accurately implements character/richman.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Flat cap (幾何多邊形帽子) in deep charcoal slate with prominent curved visor
- Silver hair, rich dark chestnut handlebar mustache, monocle with gold chain, cheerful smile with white teeth
- Tailored three-piece suit: deep charcoal slate coat framing rich burgundy vest in front
- Notched suit lapels, white pocket square on left breast, double-breasted gold buttons
- Center vertical gold buttons on burgundy vest, dark cravat & crisp shirt collar
- Left hand casually tucked into coat pocket; right hand gripping cane head
- Tailored charcoal trousers, rich brown Oxford dress shoes with darker sole & heel
- Steampunk walking cane with golden die, 8-tooth gear mechanism, dark wood shaft & gold ferrule
- Origin at (0, 0, 0) on ground, height ~ 1.82 units
- Exports to assets/models/richman.glb
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
        if o.name.startswith("Richman") or o.name.startswith("part_") or o.name == "Richman":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Rich") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Rich"):
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
    get_or_create_mat("RichSkin", (0.92, 0.65, 0.50), rough=0.7)
    get_or_create_mat("RichCheek", (0.88, 0.44, 0.38), rough=0.7)
    get_or_create_mat("RichNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("RichLip", (0.38, 0.10, 0.12), rough=0.6)
    get_or_create_mat("RichTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("RichEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("RichEyeBlue", (0.08, 0.40, 0.82), rough=0.2)
    get_or_create_mat("RichPupil", (0.03, 0.03, 0.04), rough=0.2)
    
    # Hair & Mustache
    get_or_create_mat("RichHairGrey", (0.50, 0.53, 0.56), rough=0.8)
    get_or_create_mat("RichStacheBrown", (0.20, 0.10, 0.05), rough=0.7)
    
    # Suit & Clothing (Deep charcoal slate with navy undertones, deep crimson burgundy vest)
    get_or_create_mat("RichCoatSlate", (0.08, 0.10, 0.13), rough=0.75)
    get_or_create_mat("RichCoatPocket", (0.05, 0.07, 0.09), rough=0.75)
    get_or_create_mat("RichLapelSlate", (0.06, 0.08, 0.11), rough=0.75)
    get_or_create_mat("RichPocketSquare", (0.96, 0.96, 0.96), rough=0.35)
    get_or_create_mat("RichVestBurgundy", (0.26, 0.03, 0.07), rough=0.65)
    get_or_create_mat("RichCravatDark", (0.04, 0.05, 0.07), rough=0.6)
    get_or_create_mat("RichShirtCollar", (0.90, 0.92, 0.94), rough=0.5)
    get_or_create_mat("RichPantsSlate", (0.07, 0.09, 0.12), rough=0.75)
    
    # Hat
    get_or_create_mat("RichHatSlate", (0.07, 0.09, 0.12), rough=0.75)
    get_or_create_mat("RichHatButton", (0.05, 0.07, 0.09), rough=0.75)
    
    # Shoes
    get_or_create_mat("RichShoeBrown", (0.20, 0.10, 0.05), rough=0.35)
    get_or_create_mat("RichShoeSole", (0.04, 0.03, 0.03), rough=0.85)
    
    # Accessories & Metals
    get_or_create_mat("RichGold", (0.95, 0.76, 0.16), metal=0.92, rough=0.20)
    get_or_create_mat("RichBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("RichWood", (0.12, 0.07, 0.04), rough=0.5)
    get_or_create_mat("RichGlass", (0.85, 0.95, 1.0), metal=0.0, rough=0.1, trans=0.88)
    get_or_create_mat("RichBlack", (0.03, 0.03, 0.04), rough=0.5)

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
        # Tailored trouser leg in dark charcoal slate
        leg_rings = [
            (0.12, 0.088, 0.088, 0.0),
            (0.32, 0.095, 0.095, 0.0),
            (0.55, 0.115, 0.115, 0.0)
        ]
        l = loft(f"Leg_{s}", "RichPantsSlate", leg_rings, n=8)
        l.location.x = x
        
        # Trouser cuff
        cylinder(f"Cuff_{s}", "RichPantsSlate", (x, 0.0, 0.115), 0.095, 0.03, segments=8)
        
        # Rich mahogany brown dress shoes
        icosphere(f"ShoeUpper_{s}", "RichShoeBrown", (x, -0.05, 0.068), 0.105, scale=(0.95, 1.65, 0.72), subdivisions=1)
        icosphere(f"ShoeToe_{s}", "RichShoeBrown", (x, -0.12, 0.045), 0.065, scale=(1.05, 1.1, 0.65), subdivisions=1)
        # Sole welt
        box(f"ShoeSole_{s}", "RichShoeSole", (x, -0.04, 0.014), (0.18, 0.35, 0.026))
        # Raised heel block
        box(f"ShoeHeel_{s}", "RichShoeSole", (x, 0.05, 0.024), (0.165, 0.13, 0.03))


def build_torso_and_vest():
    # 1. Torso Base (Dark Charcoal Overcoat Body)
    # The coat forms the main torso block
    coat_rings = [
        (0.42, 0.36, 0.28, 0.0),     # Bottom coat hem (flaring around thighs)
        (0.56, 0.39, 0.31, -0.01),
        (0.74, 0.42, 0.34, -0.02),
        (0.94, 0.43, 0.35, -0.02),
        (1.08, 0.40, 0.32, -0.01),
        (1.20, 0.32, 0.24, 0.0),
        (1.26, 0.18, 0.14, 0.0)      # Shoulder opening
    ]
    loft("CoatTorsoMain", "RichCoatSlate", coat_rings, n=12)
    
    # 2. Burgundy Vest Front Panel
    # In richman.jpg, the burgundy vest occupies the front chest & abdomen (between x = -0.16 and +0.16)
    # We build the vest as a curved front panel that sits slightly proud of the coat core:
    vest_bm = bmesh.new()
    vest_levels = [
        # (z, width, y_front)
        (0.52, 0.24, -0.31),  # Lower vest hem
        (0.66, 0.30, -0.345), # Belly center
        (0.84, 0.32, -0.36),  # Upper belly
        (1.02, 0.28, -0.34),  # Chest
        (1.15, 0.20, -0.28),  # V-neck top
    ]
    vest_rows = []
    for z, w, y in vest_levels:
        # 5 points across the front arc
        p_row = [
            vest_bm.verts.new((-w * 0.5, y + 0.04, z)),
            vest_bm.verts.new((-w * 0.28, y + 0.01, z)),
            vest_bm.verts.new((0.0, y, z)),
            vest_bm.verts.new((w * 0.28, y + 0.01, z)),
            vest_bm.verts.new((w * 0.5, y + 0.04, z))
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
    vest_obj = add_mesh_obj("VestFrontPanel", vest_bm, "RichVestBurgundy")
    
    # Give vest thickness
    mod = vest_obj.modifiers.new(name="VestSolidify", type='SOLIDIFY')
    mod.thickness = 0.02
    bpy.context.view_layer.objects.active = vest_obj
    bpy.ops.object.modifier_apply(modifier="VestSolidify")
    
    # 4 small gold buttons vertically down center of the burgundy vest
    for z in (0.62, 0.74, 0.86, 0.98):
        icosphere(f"VestBtn_{z}", "RichGold", (0.0, -0.362, z), 0.016, subdivisions=1)
        
    # 3. Shirt Collar & Dark Cravat (Tie)
    cylinder("NeckCollar", "RichShirtCollar", (0.0, -0.01, 1.25), 0.12, 0.08, segments=8)
    # Dark knotted cravat in the V-neck
    icosphere("CravatKnot", "RichCravatDark", (0.0, -0.25, 1.18), 0.042, scale=(1.2, 0.8, 1.0), subdivisions=1)
    slab("CravatDrape", "RichCravatDark", (0.0, -0.26, 1.15), (0.0, -0.30, 1.05), 0.060, 0.025)
    
    # 4. Tailored Notched Suit Lapels (西裝翻領)
    # The lapels fold back along the coat front edge from the collar down to mid-chest
    for s in (-1, 1):
        # Upper lapel section
        slab(f"LapelUp_{s}", "RichLapelSlate", (s * 0.10, -0.27, 1.22), (s * 0.18, -0.34, 1.06), 0.075, 0.035)
        # Notch triangle step
        icosphere(f"LapelNotch_{s}", "RichLapelSlate", (s * 0.19, -0.345, 1.05), 0.022, subdivisions=1)
        # Lower lapel section
        slab(f"LapelLow_{s}", "RichLapelSlate", (s * 0.17, -0.335, 1.03), (s * 0.15, -0.37, 0.86), 0.065, 0.035)
        
    # 5. Double-Breasted Coat Buttons (Gold) on coat flaps
    for z in (0.70, 0.84, 0.98):
        for s in (-1, 1):
            icosphere(f"CoatBtn_{s}_{z}", "RichGold", (s * 0.19, -0.375, z), 0.022, subdivisions=1)
            
    # 6. Pocket Square on Left Breast (Viewer's right, x > 0)
    box("BreastPocket", "RichCoatPocket", (0.24, -0.32, 1.04), (0.09, 0.03, 0.025), rot=(0, 0, -0.15))
    # Crisp white silk pocket square with two triangular peaks
    box("PocketSquare1", "RichPocketSquare", (0.23, -0.33, 1.075), (0.045, 0.015, 0.045), rot=(0.1, 0.15, -0.25))
    box("PocketSquare2", "RichPocketSquare", (0.26, -0.325, 1.068), (0.04, 0.015, 0.04), rot=(0.1, -0.15, 0.15))
    
    # 7. Lower Flap Pockets on Coat Sides
    for s in (-1, 1):
        box(f"FlapPocket_{s}", "RichCoatPocket", (s * 0.31, -0.29, 0.62), (0.14, 0.045, 0.05), rot=(0, 0, s * 0.32))
        icosphere(f"PocketBtn_{s}", "RichGold", (s * 0.31, -0.315, 0.62), 0.014, subdivisions=1)


def build_arms_and_hands():
    arm_r = 0.088
    
    # ------------------ LEFT ARM (Hand tucked into pocket) ------------------
    sh_l = (0.38, 0.0, 1.15)
    elbow_l = (0.49, -0.05, 0.90)
    wrist_l = (0.38, -0.20, 0.68)
    
    icosphere("Shoulder_L", "RichCoatSlate", sh_l, 0.115, subdivisions=1)
    tube("UpperArm_L", "RichCoatSlate", sh_l, elbow_l, arm_r, segments=8)
    icosphere("Elbow_L", "RichCoatSlate", elbow_l, 0.09, subdivisions=1)
    tube("ForeArm_L", "RichCoatSlate", elbow_l, wrist_l, arm_r - 0.005, segments=8)
    tube("Cuff_L", "RichCoatSlate", wrist_l, (0.35, -0.24, 0.64), 0.088, segments=8)
    icosphere("CuffBtn_L", "RichGold", (0.41, -0.20, 0.66), 0.015, subdivisions=1)
    # Hand entering the pocket slit
    icosphere("Hand_L", "RichSkin", (0.33, -0.25, 0.62), 0.065, scale=(0.9, 1.1, 0.8), subdivisions=1)
    
    # ------------------ RIGHT ARM (Holding cane) ------------------
    # Position cane comfortably closer at x = -0.42
    sh_r = (-0.38, 0.0, 1.15)
    elbow_r = (-0.45, -0.06, 0.92)
    wrist_r = (-0.43, -0.15, 0.80)
    
    icosphere("Shoulder_R", "RichCoatSlate", sh_r, 0.115, subdivisions=1)
    tube("UpperArm_R", "RichCoatSlate", sh_r, elbow_r, arm_r, segments=8)
    icosphere("Elbow_R", "RichCoatSlate", elbow_r, 0.09, subdivisions=1)
    tube("ForeArm_R", "RichCoatSlate", elbow_r, wrist_r, arm_r - 0.005, segments=8)
    tube("Cuff_R", "RichCoatSlate", wrist_r, (-0.42, -0.16, 0.74), 0.088, segments=8)
    icosphere("CuffBtn_R", "RichGold", (-0.38, -0.17, 0.76), 0.015, subdivisions=1)
    
    # Hand resting on cane die (die center at x=-0.42, y=-0.16, z=1.045)
    icosphere("Hand_R_Palm", "RichSkin", (-0.42, -0.16, 1.115), 0.065, scale=(1.1, 1.1, 0.65), subdivisions=1)
    icosphere("Hand_R_Thumb", "RichSkin", (-0.37, -0.14, 1.08), 0.030, scale=(1.2, 0.8, 0.8), subdivisions=1)
    for i, dx in enumerate((-0.032, 0.0, 0.032)):
        icosphere(f"Hand_R_Finger_{i}", "RichSkin", (-0.42 + dx, -0.21, 1.07), 0.025, scale=(0.8, 1.2, 0.8), subdivisions=1)


def build_head_and_face():
    head_center = (0.0, -0.03, 1.45)
    icosphere("HeadBase", "RichSkin", head_center, 0.225, scale=(1.10, 1.02, 1.02), subdivisions=2)
    
    for s in (-1, 1):
        icosphere(f"CheekVol_{s}", "RichSkin", (s * 0.14, -0.13, 1.40), 0.10, scale=(1.0, 0.9, 0.9), subdivisions=1)
        icosphere(f"CheekBlush_{s}", "RichCheek", (s * 0.135, -0.185, 1.40), 0.055, scale=(1.1, 0.5, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "RichSkin", (s * 0.25, -0.02, 1.45), 0.055, scale=(0.45, 0.9, 1.2), subdivisions=1)
        
    # Button nose
    icosphere("Nose", "RichNose", (0.0, -0.235, 1.42), 0.055, scale=(1.05, 0.95, 1.0), subdivisions=1)
    
    # Large expressive stylized cartoon eyes
    for s in (-1, 1):
        eye_x = s * 0.090
        eye_y = -0.21
        eye_z = 1.495
        # White sclera
        icosphere(f"EyeWhite_{s}", "RichEyeWhite", (eye_x, eye_y, eye_z), 0.046, scale=(1.05, 0.55, 1.0), subdivisions=1)
        # Sapphire blue iris
        icosphere(f"EyeIris_{s}", "RichEyeBlue", (eye_x, eye_y - 0.024, eye_z), 0.026, scale=(1.0, 0.5, 1.0), subdivisions=1)
        # Dark pupil
        icosphere(f"EyePupil_{s}", "RichPupil", (eye_x, eye_y - 0.035, eye_z), 0.013, scale=(1.0, 0.5, 1.0), subdivisions=1)
        # Thick dark brown eyebrows with gentleman arch
        slab(f"Eyebrow_{s}", "RichStacheBrown", (s * 0.045, -0.225, 1.565), (s * 0.15, -0.20, 1.575), 0.042, 0.025)
        
    # Glorious Voluminous Handlebar Mustache
    icosphere("StacheCenter", "RichStacheBrown", (0.0, -0.255, 1.39), 0.050, scale=(1.5, 0.85, 0.95), subdivisions=1)
    for s in (-1, 1):
        p_start = (s * 0.02, -0.255, 1.39)
        p_mid = (s * 0.13, -0.24, 1.37)
        p_tip = (s * 0.21, -0.19, 1.415)
        tube(f"StacheWing1_{s}", "RichStacheBrown", p_start, p_mid, 0.042, segments=6, r2=0.034)
        tube(f"StacheWing2_{s}", "RichStacheBrown", p_mid, p_tip, 0.034, segments=6, r2=0.014)
        icosphere(f"StacheTip_{s}", "RichStacheBrown", p_tip, 0.020, subdivisions=1)
        
    # Smile with white teeth
    box("Teeth", "RichTeeth", (0.0, -0.23, 1.342), (0.11, 0.02, 0.028))
    box("Mouth", "RichLip", (0.0, -0.225, 1.322), (0.085, 0.02, 0.02))
    
    # Silver-grey Hair
    icosphere("HairBack", "RichHairGrey", (0.0, 0.09, 1.48), 0.21, scale=(1.12, 0.98, 0.88), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"HairSide_{s}", "RichHairGrey", (s * 0.22, 0.01, 1.48), 0.095, scale=(0.7, 1.1, 1.25), subdivisions=1)
        slab(f"Sideburn_{s}", "RichHairGrey", (s * 0.22, -0.07, 1.48), (s * 0.22, -0.10, 1.38), 0.035, 0.04)


def build_monocle():
    # Monocle over character's right eye (viewer's left: x = -0.090, z = 1.495)
    mx = -0.090
    my = -0.256
    mz = 1.495
    m_radius = 0.054
    
    torus("MonocleRim", "RichGold", (mx, my, mz), m_radius, 0.007, seg_major=12, seg_minor=6, rot=(math.pi / 2, 0, 0))
    cylinder("MonocleGlass", "RichGlass", (mx, my, mz), m_radius * 0.92, 0.004, segments=12, rot=(math.pi / 2, 0, 0))
    
    hinge_x = mx - m_radius - 0.008
    hinge_y = my + 0.01
    icosphere("MonocleHinge", "RichGold", (hinge_x, hinge_y, mz - 0.01), 0.012, subdivisions=1)
    
    # Elegant flowing gold chain down to the coat lapel
    chain_pts = [
        (hinge_x, hinge_y, mz - 0.01),
        (hinge_x + 0.01, hinge_y + 0.02, mz - 0.08),
        (hinge_x + 0.025, hinge_y + 0.035, mz - 0.16),
        (-0.13, -0.22, 1.25),
        (-0.15, -0.27, 1.12)
    ]
    for i in range(len(chain_pts) - 1):
        tube(f"Chain_{i}", "RichGold", chain_pts[i], chain_pts[i + 1], 0.005, segments=4)


def build_flat_cap():
    # Authentic geometric flat cap / newsboy cap (幾何多邊形帽子)
    # Tilted forward, visor projecting over brow
    cap_z = 1.56
    cap_rings = [
        (cap_z, 0.24, 0.24, -0.01),
        (cap_z + 0.05, 0.29, 0.29, -0.03),
        (cap_z + 0.11, 0.27, 0.27, -0.04),
        (cap_z + 0.15, 0.16, 0.16, -0.03)
    ]
    loft("CapCrown", "RichHatSlate", cap_rings, n=8)
    cylinder("CapButton", "RichHatButton", (0.0, -0.03, cap_z + 0.16), 0.035, 0.02, segments=8)
    
    # Curved forward visor
    visor_bm = bmesh.new()
    v_pts = [
        (-0.17, -0.22, cap_z + 0.01),
        (-0.13, -0.35, cap_z - 0.02),
        (0.0, -0.38, cap_z - 0.025),
        (0.13, -0.35, cap_z - 0.02),
        (0.17, -0.22, cap_z + 0.01),
        (0.14, -0.20, cap_z + 0.03),
        (0.0, -0.23, cap_z + 0.02),
        (-0.14, -0.20, cap_z + 0.03)
    ]
    thick = 0.024
    bot_v = [visor_bm.verts.new(p) for p in v_pts]
    top_v = [visor_bm.verts.new((p[0], p[1], p[2] + thick)) for p in v_pts]
    n = len(v_pts)
    for i in range(n):
        visor_bm.faces.new((bot_v[i], bot_v[(i + 1) % n], top_v[(i + 1) % n], top_v[i]))
    visor_bm.faces.new(bot_v[::-1])
    visor_bm.faces.new(top_v)
    bmesh.ops.recalc_face_normals(visor_bm, faces=visor_bm.faces)
    add_mesh_obj("CapVisor", visor_bm, "RichHatSlate")


def build_cane():
    cx = -0.42
    cy = -0.16
    
    # 1. Base Gold Ferrule
    cylinder("CaneTip", "RichGold", (cx, cy, 0.03), 0.028, 0.06, segments=8)
    icosphere("CaneTipBall", "RichGold", (cx, cy, 0.01), 0.022, subdivisions=1)
    
    # 2. Polished Hardwood Shaft
    tube("CaneShaft", "RichWood", (cx, cy, 0.05), (cx, cy, 0.88), 0.022, segments=8)
    
    # 3. Brass Collar
    cylinder("CaneCollar", "RichBrass", (cx, cy, 0.89), 0.032, 0.035, segments=8)
    torus("CaneCollarRing", "RichGold", (cx, cy, 0.905), 0.034, 0.008, seg_major=8, seg_minor=4)
    
    # 4. Steampunk Gear Assembly (齒輪機構)
    gear_r = 0.065
    gear_z = 0.95
    cylinder("GearHub", "RichGold", (cx, cy - 0.02, gear_z), gear_r, 0.018, segments=12, rot=(math.pi / 2, 0, 0))
    for k in range(8):
        ang = k * math.pi / 4
        tx = cx + (gear_r + 0.01) * math.cos(ang)
        tz = gear_z + (gear_r + 0.01) * math.sin(ang)
        box(f"GearTooth_{k}", "RichGold", (tx, cy - 0.02, tz), (0.018, 0.018, 0.018), rot=(0, -ang, 0))
    cylinder("GearAxle", "RichBrass", (cx, cy - 0.03, gear_z), 0.022, 0.015, segments=6, rot=(math.pi / 2, 0, 0))
    
    gear2_r = 0.042
    gear2_x = cx + 0.075
    gear2_z = gear_z - 0.035
    cylinder("Gear2Hub", "RichBrass", (gear2_x, cy - 0.02, gear2_z), gear2_r, 0.015, segments=8, rot=(math.pi / 2, 0, 0))
    for k in range(6):
        ang = k * math.pi / 3 + 0.25
        tx = gear2_x + (gear2_r + 0.008) * math.cos(ang)
        tz = gear2_z + (gear2_r + 0.008) * math.sin(ang)
        box(f"Gear2Tooth_{k}", "RichBrass", (tx, cy - 0.02, tz), (0.014, 0.015, 0.014), rot=(0, -ang, 0))
        
    cylinder("DiePedestal", "RichGold", (cx, cy, 0.975), 0.045, 0.03, segments=8)
    
    # 5. Golden Die (黃金多面骰子)
    die_z = 1.045
    die_size = 0.125
    box("GoldenDie", "RichGold", (cx, cy, die_z), (die_size, die_size, die_size), rot=(0.15, 0.15, 0.35))
    
    # Pips (black dots)
    icosphere("DiePip_Front1", "RichBlack", (cx - 0.025, cy - 0.072, die_z + 0.025), 0.012, subdivisions=1)
    icosphere("DiePip_Front2", "RichBlack", (cx + 0.025, cy - 0.072, die_z - 0.025), 0.012, subdivisions=1)
    icosphere("DiePip_Side1", "RichBlack", (cx + 0.072, cy - 0.025, die_z + 0.03), 0.011, subdivisions=1)
    icosphere("DiePip_Side2", "RichBlack", (cx + 0.072, cy + 0.025, die_z - 0.03), 0.011, subdivisions=1)
    icosphere("DiePip_Side3", "RichBlack", (cx + 0.072, cy, die_z), 0.011, subdivisions=1)


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
    obj.name = "Richman"
    obj.data.name = "Richman"
    
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
    print(f"Richman finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "richman.glb")
    
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


def generate_richman():
    reset_scene()
    setup_materials()
    build_legs_and_shoes()
    build_torso_and_vest()
    build_arms_and_hands()
    build_head_and_face()
    build_monocle()
    build_flat_cap()
    build_cane()
    return finalize_and_export()


if __name__ == "__main__":
    generate_richman()
