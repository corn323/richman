"""
Richman / Host (大富翁 / 主持人) 3D Model Generator for Blender.
Accurately implements character/richman.jpg based on Master Goal specifications:
- 3D Stylized Low-Poly Character (3.5-head tall male figure)
- Crisp faceted polygonal geometry, FLAT SHADING enabled
- Grey newsboy flat cap (幾何多邊形帽子) with curved front visor
- Refined grey/silver hair, thick dark chestnut handlebar mustache, cheerful smile
- Golden monocle over right eye with hanging gold chain
- Tailored three-piece suit: dark slate grey suit coat, rich burgundy vest, white pocket square
- Crisp white shirt collar and dark cravat/necktie, golden coat & vest buttons
- Left hand holds ornate golden pocket watch (with watch face & stem loop)
- Right hand rests on Elegant Steampunk Walking Cane (多面體權杖頭):
  * Polished mahogany wood shaft with golden ferrule tip
  * Clockwork brass/golden gears mechanism
  * Golden 6-sided polyhedral die at summit with recessed pips
- Tailored dark slate trousers and polished brown Oxford leather dress shoes
- Pivot at (0, 0, 0) at ground center, height ~ 1.82 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
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

def get_or_create_mat(name, color, metal=0.0, rough=0.6, trans=0.0, emission=None):
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
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = tuple(emission) + (1.0,)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 2.0
        elif "Emission" in bsdf.inputs:
            bsdf.inputs["Emission"].default_value = tuple(emission) + (1.0,)
    mat.diffuse_color = tuple(color) + (1.0,)
    mats[name] = mat
    return mat

def setup_materials():
    # Skin & Face
    get_or_create_mat("RichSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("RichCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("RichNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("RichLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("RichTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("RichEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("RichEyeBlue", (0.12, 0.55, 0.85), rough=0.2)
    get_or_create_mat("RichPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("RichHairGrey", (0.72, 0.74, 0.76), rough=0.7)
    get_or_create_mat("RichMustache", (0.28, 0.16, 0.10), rough=0.75)
    
    # Golden Monocle
    get_or_create_mat("RichMonocleGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("RichMonocleLens", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)
    get_or_create_mat("RichChainGold", (0.88, 0.70, 0.18), metal=0.9, rough=0.25)

    # Newsboy Cap (Grey / Slate)
    get_or_create_mat("RichHatSlate", (0.24, 0.27, 0.32), rough=0.7)
    get_or_create_mat("RichHatDark", (0.16, 0.18, 0.22), rough=0.75)
    get_or_create_mat("RichHatVisor", (0.20, 0.23, 0.28), rough=0.65)

    # Tailored Three-Piece Suit
    get_or_create_mat("RichCoatSlate", (0.22, 0.25, 0.30), rough=0.65)
    get_or_create_mat("RichCoatLapel", (0.16, 0.19, 0.24), rough=0.7)
    get_or_create_mat("RichPocketSquare", (0.95, 0.95, 0.95), rough=0.5)
    get_or_create_mat("RichVestBurgundy", (0.48, 0.10, 0.16), rough=0.65)
    get_or_create_mat("RichVestTrim", (0.36, 0.08, 0.12), rough=0.7)
    get_or_create_mat("RichShirtWhite", (0.95, 0.95, 0.95), rough=0.6)
    get_or_create_mat("RichTieNavy", (0.12, 0.15, 0.22), rough=0.6)
    get_or_create_mat("RichGoldButton", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)

    # Trousers & Oxford Shoes
    get_or_create_mat("RichPantsSlate", (0.20, 0.23, 0.28), rough=0.7)
    get_or_create_mat("RichShoeBrown", (0.32, 0.18, 0.10), rough=0.6)
    get_or_create_mat("RichShoeDark", (0.18, 0.10, 0.08), rough=0.75)
    get_or_create_mat("RichShoeSole", (0.08, 0.08, 0.08), rough=0.9)

    # Pocket Watch & Walking Cane
    get_or_create_mat("RichWatchGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("RichWatchFace", (0.94, 0.93, 0.88), rough=0.3)
    get_or_create_mat("RichCaneWood", (0.28, 0.15, 0.08), rough=0.55)
    get_or_create_mat("RichCaneGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("RichCaneBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)

def register_part(obj, mat_name):
    if mat_name in mats:
        if not obj.data.materials:
            obj.data.materials.append(mats[mat_name])
        else:
            obj.data.materials[0] = mats[mat_name]
    parts.append(obj)
    return obj

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


# -------------------------------------------------------------
# 1. LEGS, TAILORED TROUSERS & OXFORD LEATHER SHOES
# -------------------------------------------------------------

def build_legs_and_shoes():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Tailored dress trousers thigh & calf
        cylinder(f"LegThigh_{side}", "RichPantsSlate", (sx, 0.0, 0.44), 0.13, 0.32, segments=8)
        cylinder(f"LegCalf_{side}", "RichPantsSlate", (sx, 0.01, 0.28), 0.12, 0.14, segments=8)
        # Crisp trouser crease fold
        box(f"TrouserCrease_{side}", "RichPantsSlate", (sx, -0.09, 0.34), (0.02, 0.04, 0.28))
        # Trouser cuff
        cylinder(f"TrouserCuff_{side}", "RichCoatLapel", (sx, 0.0, 0.17), 0.115, 0.04, segments=8)

        # Polished Mahogany Brown Oxford Shoe
        box(f"ShoeFoot_{side}", "RichShoeBrown", (sx, -0.06, 0.08), (0.20, 0.29, 0.13))
        box(f"ShoeToeCap_{side}", "RichShoeDark", (sx, -0.17, 0.07), (0.18, 0.09, 0.11))
        # Dark leather sole & raised heel
        box(f"ShoeSole_{side}", "RichShoeSole", (sx, -0.06, 0.02), (0.22, 0.32, 0.035))
        box(f"ShoeHeel_{side}", "RichShoeSole", (sx, 0.05, 0.035), (0.21, 0.12, 0.06))
        # Shoe Laces tab
        box(f"ShoeLace_{side}", "RichShoeDark", (sx, -0.08, 0.135), (0.12, 0.10, 0.025))


# -------------------------------------------------------------
# 2. TORSO, COAT, BURGUNDY VEST, SHIRT & CRAVAT
# -------------------------------------------------------------

def build_torso_and_vest():
    # Pelvis & Suit Coat Tail Hem
    cylinder("CoatTail", "RichCoatSlate", (0.0, 0.0, 0.58), 0.275, 0.18, segments=10)
    
    # Lower Torso (Waist / Coat & Vest Core)
    cylinder("TorsoWaist", "RichCoatSlate", (0.0, -0.01, 0.82), 0.295, 0.32, segments=10, r2=0.29)
    # Chest
    cylinder("TorsoChest", "RichCoatSlate", (0.0, -0.02, 1.08), 0.29, 0.26, segments=10, r2=0.26)
    
    # Rich Burgundy Vest Front
    box("VestFront", "RichVestBurgundy", (0.0, -0.24, 0.88), (0.22, 0.08, 0.38))
    # Vest bottom pointed tabs (waistcoat points)
    box("VestPoint_L", "RichVestBurgundy", (-0.06, -0.245, 0.67), (0.08, 0.06, 0.06), rot=(0, 0, -0.4))
    box("VestPoint_R", "RichVestBurgundy", (0.06, -0.245, 0.67), (0.08, 0.06, 0.06), rot=(0, 0, 0.4))

    # Vest Center Gold Buttons
    for z in [0.75, 0.84, 0.93, 1.02]:
        cylinder(f"VestButton_{z}", "RichGoldButton", (0.0, -0.285, z), 0.014, 0.01, segments=6, rot=(math.pi/2, 0, 0))

    # Crisp White Dress Shirt Collar in V-neck
    box("ShirtCollarCenter", "RichShirtWhite", (0.0, -0.22, 1.15), (0.12, 0.08, 0.18))
    box("CollarWing_L", "RichShirtWhite", (-0.05, -0.23, 1.20), (0.06, 0.04, 0.06), rot=(0.1, 0.2, -0.2))
    box("CollarWing_R", "RichShirtWhite", (0.05, -0.23, 1.20), (0.06, 0.04, 0.06), rot=(0.1, -0.2, 0.2))

    # Dark Navy Necktie / Cravat
    tube("CravatTie", "RichTieNavy", (0.0, -0.235, 1.22), (0.0, -0.25, 1.04), 0.024, segments=4)
    cylinder("TieKnot", "RichTieNavy", (0.0, -0.238, 1.19), 0.025, 0.03, segments=6)

    # Suit Coat Lapels (Framing the burgundy vest)
    box("Lapel_L", "RichCoatLapel", (-0.16, -0.25, 1.04), (0.10, 0.06, 0.34), rot=(0, 0, -0.1))
    box("Lapel_R", "RichCoatLapel", (0.16, -0.25, 1.04), (0.10, 0.06, 0.34), rot=(0, 0, 0.1))
    # Notched Lapel Upper Flaps
    box("LapelUpper_L", "RichCoatLapel", (-0.15, -0.23, 1.18), (0.08, 0.05, 0.09), rot=(0.1, 0, -0.2))
    box("LapelUpper_R", "RichCoatLapel", (0.15, -0.23, 1.18), (0.08, 0.05, 0.09), rot=(0.1, 0, 0.2))

    # White Handkerchief Pocket Square on Left Breast Pocket
    box("BreastPocket", "RichCoatLapel", (-0.16, -0.27, 1.08), (0.08, 0.02, 0.04))
    box("PocketSquare", "RichPocketSquare", (-0.16, -0.275, 1.11), (0.06, 0.015, 0.03), rot=(0.1, 0, 0.2))

    # Coat Waist Flap Pockets
    box("CoatPocket_L", "RichCoatLapel", (-0.22, -0.24, 0.76), (0.11, 0.04, 0.08))
    box("CoatPocket_R", "RichCoatLapel", (0.22, -0.24, 0.76), (0.11, 0.04, 0.08))

    # Double-Breasted Gold Buttons on Coat
    for z in [0.82, 0.94]:
        cylinder(f"CoatButton_L_{z}", "RichGoldButton", (-0.13, -0.275, z), 0.016, 0.01, segments=6, rot=(math.pi/2, 0, 0))
        cylinder(f"CoatButton_R_{z}", "RichGoldButton", (0.13, -0.275, z), 0.016, 0.01, segments=6, rot=(math.pi/2, 0, 0))


# -------------------------------------------------------------
# 3. ARMS, HANDS, GOLDEN POCKET WATCH & CANE REST
# -------------------------------------------------------------

def build_arms_and_watch():
    # --- RIGHT ARM (Resting gently on Cane Summit) ---
    r_sh = (0.34, -0.02, 1.14)
    icosphere("Shoulder_R", "RichCoatSlate", r_sh, 0.12, subdivisions=1)
    # Upper Arm in suit jacket
    tube("UpperArm_R", "RichCoatSlate", r_sh, (0.46, -0.05, 0.94), 0.085, segments=8)
    icosphere("Elbow_R", "RichCoatSlate", (0.46, -0.05, 0.94), 0.082, subdivisions=1)
    # Forearm extending to rest on cane
    tube("Forearm_R", "RichCoatSlate", (0.46, -0.05, 0.94), (0.48, -0.22, 0.86), 0.078, segments=8)
    # Jacket Cuff & Gold Cufflinks
    cylinder("JacketCuff_R", "RichCoatLapel", (0.48, -0.22, 0.86), 0.082, 0.04, segments=8)
    cylinder("Cufflink_R", "RichGoldButton", (0.52, -0.22, 0.86), 0.010, 0.015, segments=6, rot=(0, math.pi/2, 0))
    # Right Hand resting gracefully over cane head
    icosphere("Hand_R", "RichSkin", (0.49, -0.25, 0.84), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_R", "RichSkin", (0.45, -0.23, 0.85), (0.028, 0.04, 0.028), rot=(0.2, 0.2, 0.3))
    box("Fingers_R", "RichSkin", (0.50, -0.26, 0.83), (0.065, 0.04, 0.065), rot=(0, 0.2, 0.2))

    # --- LEFT ARM (Holding Golden Pocket Watch) ---
    l_sh = (-0.34, -0.02, 1.14)
    icosphere("Shoulder_L", "RichCoatSlate", l_sh, 0.12, subdivisions=1)
    # Upper Arm
    tube("UpperArm_L", "RichCoatSlate", l_sh, (-0.46, -0.08, 0.95), 0.085, segments=8)
    icosphere("Elbow_L", "RichCoatSlate", (-0.46, -0.08, 0.95), 0.082, subdivisions=1)
    # Forearm bent forward/up holding pocket watch
    tube("Forearm_L", "RichCoatSlate", (-0.46, -0.08, 0.95), (-0.32, -0.26, 0.90), 0.078, segments=8)
    cylinder("JacketCuff_L", "RichCoatLapel", (-0.32, -0.26, 0.90), 0.082, 0.04, segments=8)
    cylinder("Cufflink_L", "RichGoldButton", (-0.36, -0.26, 0.90), 0.010, 0.015, segments=6, rot=(0, math.pi/2, 0))
    # Left Hand presenting pocket watch
    icosphere("Hand_L", "RichSkin", (-0.28, -0.32, 0.90), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_L", "RichSkin", (-0.24, -0.30, 0.92), (0.028, 0.04, 0.028), rot=(0.2, -0.2, -0.3))
    box("Fingers_L", "RichSkin", (-0.30, -0.34, 0.89), (0.065, 0.04, 0.065), rot=(0, -0.2, -0.2))

    # --- ORNATE GOLDEN POCKET WATCH ---
    w_pos = (-0.26, -0.38, 0.92)
    w_rot = (0.25, 0.35, 0.0)
    # Round golden case
    cylinder("WatchCase", "RichWatchGold", w_pos, 0.055, 0.018, segments=12, rot=w_rot)
    # White watch face with clock markings
    cylinder("WatchFace", "RichWatchFace", (w_pos[0], w_pos[1] - 0.01, w_pos[2]), 0.046, 0.005, segments=12, rot=w_rot)
    # Golden top stem loop & crown
    cylinder("WatchStem", "RichWatchGold", (w_pos[0], w_pos[1], w_pos[2] + 0.06), 0.012, 0.02, segments=6, rot=w_rot)
    torus("WatchRing", "RichWatchGold", (w_pos[0], w_pos[1], w_pos[2] + 0.075), 0.018, 0.004, rot=w_rot)
    # Watch chain connecting to vest
    tube("WatchChainVest", "RichChainGold", (w_pos[0], w_pos[1], w_pos[2] + 0.07), (-0.05, -0.27, 0.85), 0.005, segments=4)


# -------------------------------------------------------------
# 4. HEAD, SILVER HAIR, MUSTACHE, MONOCLE & NEWSBOY CAP
# -------------------------------------------------------------

def build_head_and_hat():
    # Neck
    cylinder("Neck", "RichSkin", (0.0, -0.01, 1.28), 0.10, 0.12, segments=8)
    
    # Stylized faceted cranium
    icosphere("HeadBase", "RichSkin", (0.0, 0.0, 1.48), 0.23, scale=(1.12, 1.0, 1.04), subdivisions=2)
    # Strong rounded nose
    box("NoseBridge", "RichNose", (0.0, -0.225, 1.46), (0.045, 0.05, 0.07), rot=(0.2, 0, 0))
    icosphere("NoseTip", "RichNose", (0.0, -0.245, 1.43), 0.042, scale=(1.1, 0.9, 0.9), subdivisions=1)
    
    # Cheerful rosy cheeks & eyes
    for s in (-1, 1):
        icosphere(f"Cheek_{s}", "RichCheek", (s * 0.12, -0.18, 1.41), 0.058, scale=(1.0, 0.6, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "RichSkin", (s * 0.25, 0.01, 1.47), 0.045, scale=(0.5, 0.9, 1.2), subdivisions=1)
        # Eyes
        box(f"EyeWhite_{s}", "RichEyeWhite", (s * 0.09, -0.205, 1.51), (0.055, 0.02, 0.035))
        box(f"EyeIris_{s}", "RichEyeBlue", (s * 0.09, -0.218, 1.51), (0.032, 0.01, 0.032))
        box(f"EyePupil_{s}", "RichPupil", (s * 0.09, -0.225, 1.51), (0.016, 0.008, 0.016))
        # Eyebrows (Distinguished grey)
        box(f"Eyebrow_{s}", "RichHairGrey", (s * 0.095, -0.21, 1.56), (0.075, 0.03, 0.025), rot=(0, 0, s * 0.12))

    # Distinguished Silver Hair on sides and back
    icosphere("HairBack", "RichHairGrey", (0.0, 0.10, 1.48), 0.21, scale=(1.14, 0.95, 0.85), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"HairSide_{s}", "RichHairGrey", (s * 0.21, 0.02, 1.48), 0.095, scale=(0.6, 1.2, 1.2), subdivisions=1)

    # Cheerful Dignified Smile showing teeth
    box("MouthCavity", "RichLip", (0.0, -0.22, 1.34), (0.09, 0.02, 0.03))
    box("TeethUpper", "RichTeeth", (0.0, -0.226, 1.35), (0.075, 0.015, 0.015))

    # Magnificent Sculpted Handlebar Mustache (Rich dark chestnut)
    for s in (-1, 1):
        tube(f"MustacheMain_{s}", "RichMustache", (0.0, -0.238, 1.39), (s * 0.12, -0.21, 1.37), 0.034, segments=6)
        # Curved handlebar tips curling upward
        tube(f"MustacheCurled_{s}", "RichMustache", (s * 0.12, -0.21, 1.37), (s * 0.17, -0.18, 1.405), 0.028, segments=5, r2=0.012)
    icosphere("MustacheCenter", "RichMustache", (0.0, -0.24, 1.395), 0.032, scale=(1.3, 0.7, 0.8), subdivisions=1)

    # --- GOLDEN MONOCLE OVER RIGHT EYE ---
    m_eye = (0.09, -0.236, 1.51)
    torus("MonocleRim", "RichMonocleGold", m_eye, 0.048, 0.008, seg_major=8, seg_minor=4, rot=(math.pi/2, 0, 0))
    cylinder("MonocleLens", "RichMonocleLens", m_eye, 0.044, 0.005, segments=8, rot=(math.pi/2, 0, 0))
    # Monocle gold chain hanging down to vest
    tube("MonocleChain1", "RichChainGold", (0.13, -0.23, 1.48), (0.16, -0.20, 1.30), 0.005, segments=4)
    tube("MonocleChain2", "RichChainGold", (0.16, -0.20, 1.30), (0.10, -0.26, 1.05), 0.005, segments=4)

    # --- GREY NEWSBOY CAP / FLAT CAP (幾何多邊形帽子) ---
    cap_z = 1.62
    # Newsboy Cap Crown (faceted 8-panel drape)
    icosphere("CapCrown", "RichHatSlate", (0.0, -0.01, cap_z), 0.28, scale=(1.10, 1.15, 0.68), subdivisions=1)
    # Top Cap Button
    cylinder("CapButton", "RichHatDark", (0.0, 0.0, cap_z + 0.18), 0.025, 0.015, segments=8)
    # Front visor lip
    box("CapVisor", "RichHatVisor", (0.0, -0.28, cap_z - 0.02), (0.28, 0.14, 0.022), rot=(-0.14, 0, 0))
    # Cap band trim
    cylinder("CapTrim", "RichHatDark", (0.0, -0.01, cap_z - 0.04), 0.285, 0.025, segments=12, rot=(0.04, 0, 0))


# -------------------------------------------------------------
# 5. ELEGANT STEAMPUNK WALKING CANE (多面體權杖頭)
# -------------------------------------------------------------

def build_walking_cane():
    sx = 0.49
    sy = -0.25
    top_z = 0.84
    
    # 1. Golden Ferrule Tip at ground
    cylinder("CaneSpike", "RichCaneGold", (sx, sy, 0.03), 0.028, 0.05, segments=6, r2=0.01)
    cylinder("CaneBottomRing", "RichCaneBrass", (sx, sy, 0.065), 0.034, 0.03, segments=8)
    
    # 2. Polished Mahogany Wooden Cane Shaft
    tube("CaneShaft", "RichCaneWood", (sx, sy, 0.07), (sx, sy, top_z), 0.026, segments=8)
    
    # Upper Brass & Gold Collar below grip
    cylinder("CaneTopCollar", "RichCaneGold", (sx, sy, top_z + 0.02), 0.042, 0.04, segments=8)

    # 3. Clockwork Steampunk Gears Assembly
    gear_z = top_z + 0.11
    # Main Brass Gear Hub
    cylinder("CaneGearHub", "RichCaneGold", (sx, sy, gear_z), 0.072, 0.024, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("CaneGearAxle", "RichCaneBrass", (sx, sy - 0.015, gear_z), 0.038, 0.012, segments=8, rot=(math.pi/2, 0, 0))
    # 8 Gear Teeth
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.082 * math.cos(ang)
        tz = gear_z + 0.082 * math.sin(ang)
        box(f"GearTooth_{i}", "RichCaneGold", (tx, sy, tz), (0.018, 0.022, 0.018), rot=(0, -ang, 0))

    # Secondary Interlocking Small Clockwork Gear
    s_gx = sx - 0.068
    s_gz = gear_z - 0.052
    cylinder("SmallGearHub", "RichCaneBrass", (s_gx, sy + 0.008, s_gz), 0.042, 0.018, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(6):
        ang = i * math.pi / 3
        tx = s_gx + 0.048 * math.cos(ang)
        tz = s_gz + 0.048 * math.sin(ang)
        box(f"SmallTooth_{i}", "RichCaneBrass", (tx, sy + 0.008, tz), (0.014, 0.018, 0.014), rot=(0, -ang, 0))

    # 4. Polyhedral Golden 6-Sided Die at Summit
    die_z = gear_z + 0.15
    die_rot = (0.35, 0.35, 0.45)
    box("GoldenDie", "RichCaneGold", (sx, sy, die_z), (0.13, 0.13, 0.13), rot=die_rot)
    # Recessed dark pips
    box("DiePip1", "RichCoatLapel", (sx, sy - 0.07, die_z), (0.025, 0.01, 0.025), rot=die_rot)
    box("DiePip2", "RichCoatLapel", (sx - 0.035, sy - 0.06, die_z + 0.035), (0.02, 0.01, 0.02), rot=die_rot)
    box("DiePip3", "RichCoatLapel", (sx + 0.035, sy - 0.06, die_z - 0.035), (0.02, 0.01, 0.02), rot=die_rot)


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
    
    obj = bpy.context.active_object
    obj.name = "Richman"
    
    # Clean up geometry & normals
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
    build_arms_and_watch()
    build_head_and_hat()
    build_walking_cane()
    return finalize_and_export()


if __name__ == "__main__":
    generate_richman()
