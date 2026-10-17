"""
Adventurer / Language Master (冒險家 / 語言大師) 3D Model Generator for Blender.
Accurately implements character/adventure.jpg based on Master Goal specifications:
- 3D Stylized Low-Poly Character (3.5-head tall male figure)
- Crisp faceted polygonal geometry, FLAT SHADING enabled
- Plaid deerstalker hat (幾何獵帽) with tied top ribbon, front & rear brims, and communications boom mic
- Clear framed glasses, refined silver-grey hair, trimmed goatee beard & mustache, warm expression
- Olive-grey explorer trench coat with shoulder storm capelet & folded lapel collar
- Plaid scarf wrapped around neck over dark teal sweater
- Leather travel backpack with colorful field journals/books sticking out
- Leather messenger bag with diagonal cross strap
- Left hand holds open travel passport / notebook
- Right hand rests on Multi-national Language Staff (多面體多國語權杖頭):
  * Faceted optical crystal prism bottom tip
  * Rainbow chromatic spectrum bar with calibration ticks
  * Interlocking clockwork steampunk brass/golden gears
  * Miniature 3D globe with ocean blue & green land facets in golden meridian ring
- Dark charcoal cargo trousers and rugged brown leather hiking boots
- Pivot at (0, 0, 0) at ground center, height ~ 1.83 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
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
    get_or_create_mat("AdvSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("AdvCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("AdvNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("AdvLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("AdvTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("AdvEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("AdvEyeBlue", (0.12, 0.55, 0.85), rough=0.2)
    get_or_create_mat("AdvPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("AdvHairGrey", (0.75, 0.77, 0.79), rough=0.7)
    get_or_create_mat("AdvBeardGrey", (0.70, 0.72, 0.75), rough=0.75)
    
    # Glasses & Headset
    get_or_create_mat("AdvGlassFrame", (0.15, 0.18, 0.22), rough=0.4)
    get_or_create_mat("AdvGlassLens", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)
    get_or_create_mat("AdvHeadset", (0.14, 0.16, 0.18), metal=0.7, rough=0.3)
    get_or_create_mat("AdvMicTip", (0.45, 0.18, 0.65), rough=0.4)

    # Deerstalker Hat
    get_or_create_mat("AdvHatOlive", (0.26, 0.30, 0.22), rough=0.7)
    get_or_create_mat("AdvHatDark", (0.16, 0.18, 0.14), rough=0.75)
    get_or_create_mat("AdvHatVisor", (0.22, 0.25, 0.18), rough=0.65)
    get_or_create_mat("AdvHatRibbon", (0.14, 0.12, 0.10), rough=0.6)

    # Explorer Trench Coat & Inner Clothing
    get_or_create_mat("AdvCoatOlive", (0.24, 0.28, 0.22), rough=0.65)
    get_or_create_mat("AdvCoatLapel", (0.18, 0.22, 0.16), rough=0.7)
    get_or_create_mat("AdvSweaterTeal", (0.12, 0.24, 0.28), rough=0.75)
    get_or_create_mat("AdvScarfBase", (0.15, 0.24, 0.26), rough=0.7)
    get_or_create_mat("AdvScarfStripe", (0.92, 0.62, 0.16), rough=0.6)
    get_or_create_mat("AdvGoldBtn", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)

    # Trousers & Hiking Boots
    get_or_create_mat("AdvPantsDark", (0.16, 0.20, 0.26), rough=0.75)
    get_or_create_mat("AdvBootLeather", (0.32, 0.20, 0.12), rough=0.65)
    get_or_create_mat("AdvBootDark", (0.18, 0.12, 0.08), rough=0.8)
    get_or_create_mat("AdvBootSole", (0.08, 0.08, 0.08), rough=0.9)

    # Backpack & Messenger Satchel
    get_or_create_mat("AdvPackBrown", (0.38, 0.24, 0.14), rough=0.7)
    get_or_create_mat("AdvPackDark", (0.22, 0.15, 0.10), rough=0.75)
    get_or_create_mat("AdvBookRed", (0.75, 0.18, 0.18), rough=0.6)
    get_or_create_mat("AdvBookTeal", (0.18, 0.55, 0.65), rough=0.6)
    get_or_create_mat("AdvBookPages", (0.92, 0.90, 0.85), rough=0.8)
    get_or_create_mat("AdvPassportCover", (0.20, 0.40, 0.35), rough=0.55)

    # Multi-national Language Staff
    get_or_create_mat("AdvScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("AdvScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("AdvPrismPoint", (0.45, 0.20, 0.65), metal=0.3, rough=0.2)
    get_or_create_mat("AdvGlobeOcean", (0.18, 0.55, 0.85), rough=0.3)
    get_or_create_mat("AdvGlobeLand", (0.22, 0.70, 0.28), rough=0.4)
    get_or_create_mat("AdvGlobeRing", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("AdvSpecRed", (0.95, 0.18, 0.18), rough=0.3)
    get_or_create_mat("AdvSpecOrange", (0.98, 0.48, 0.08), rough=0.3)
    get_or_create_mat("AdvSpecYellow", (0.98, 0.88, 0.10), rough=0.3)
    get_or_create_mat("AdvSpecGreen", (0.18, 0.85, 0.28), rough=0.3)
    get_or_create_mat("AdvSpecCyan", (0.15, 0.88, 0.98), rough=0.3)
    get_or_create_mat("AdvSpecBlue", (0.12, 0.35, 0.95), rough=0.3)
    get_or_create_mat("AdvSpecPurple", (0.65, 0.15, 0.85), rough=0.3)

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
# 1. LEGS, TROUSERS & HIKING BOOTS
# -------------------------------------------------------------

def build_legs_and_boots():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Cargo explorer trousers
        cylinder(f"LegThigh_{side}", "AdvPantsDark", (sx, 0.0, 0.44), 0.13, 0.32, segments=8)
        cylinder(f"LegKnee_{side}", "AdvPantsDark", (sx, 0.01, 0.28), 0.12, 0.12, segments=8)
        
        # Heavy Leather Hiking Boot Shaft
        cylinder(f"BootShaft_{side}", "AdvBootLeather", (sx, 0.0, 0.17), 0.115, 0.16, segments=8)
        cylinder(f"BootCuff_{side}", "AdvCoatLapel", (sx, 0.0, 0.23), 0.12, 0.035, segments=8)
        
        # Hiking Boot Foot & Dark Toe Guard
        box(f"BootFoot_{side}", "AdvBootLeather", (sx, -0.06, 0.08), (0.21, 0.30, 0.14))
        box(f"BootToe_{side}", "AdvBootDark", (sx, -0.18, 0.07), (0.19, 0.10, 0.12))
        
        # Deep Lugged Rubber Sole & Heel
        box(f"BootSole_{side}", "AdvBootSole", (sx, -0.06, 0.02), (0.23, 0.34, 0.04))
        box(f"BootHeel_{side}", "AdvBootSole", (sx, 0.05, 0.03), (0.23, 0.12, 0.06))
        # Boot Laces panel
        box(f"BootLace_{side}", "AdvBootDark", (sx, -0.10, 0.13), (0.14, 0.12, 0.03))


# -------------------------------------------------------------
# 2. TORSO, TRENCH COAT, STORM CAPELET & SCARF
# -------------------------------------------------------------

def build_torso_and_coat():
    # Pelvis & Coat Lower Skirt
    cylinder("Pelvis", "AdvCoatOlive", (0.0, 0.0, 0.58), 0.275, 0.18, segments=10)
    
    # Leather Belt around waist
    cylinder("WaistBelt", "AdvBootLeather", (0.0, 0.0, 0.67), 0.285, 0.08, segments=12)
    box("BeltBuckle", "AdvScepterBrass", (0.0, -0.285, 0.67), (0.11, 0.03, 0.08))
    
    # Torso (Trench Coat Body)
    cylinder("TorsoWaist", "AdvCoatOlive", (0.0, -0.01, 0.84), 0.295, 0.32, segments=10, r2=0.28)
    cylinder("TorsoChest", "AdvCoatOlive", (0.0, -0.02, 1.08), 0.285, 0.26, segments=10, r2=0.26)
    
    # Inner Teal Sweater visible in neck opening
    box("InnerSweater", "AdvSweaterTeal", (0.0, -0.23, 1.06), (0.14, 0.08, 0.24))
    cylinder("SweaterButton1", "AdvGoldBtn", (0.0, -0.275, 1.00), 0.012, 0.01, segments=6, rot=(math.pi/2, 0, 0))
    cylinder("SweaterButton2", "AdvGoldBtn", (0.0, -0.275, 0.92), 0.012, 0.01, segments=6, rot=(math.pi/2, 0, 0))

    # Trench Coat Front Panels & Folded Lapels
    box("CoatPanel_L", "AdvCoatOlive", (-0.14, -0.25, 0.95), (0.12, 0.06, 0.40), rot=(0, 0, -0.08))
    box("CoatPanel_R", "AdvCoatOlive", (0.14, -0.25, 0.95), (0.12, 0.06, 0.40), rot=(0, 0, 0.08))
    box("Lapel_L", "AdvCoatLapel", (-0.16, -0.26, 1.10), (0.09, 0.05, 0.18), rot=(0.1, 0.1, -0.2))
    box("Lapel_R", "AdvCoatLapel", (0.16, -0.26, 1.10), (0.09, 0.05, 0.18), rot=(0.1, -0.1, 0.2))

    # Trench Coat Flap Pockets
    box("CoatPocket_L", "AdvCoatLapel", (-0.22, -0.24, 0.76), (0.11, 0.04, 0.09))
    box("CoatPocket_R", "AdvCoatLapel", (0.22, -0.24, 0.76), (0.11, 0.04, 0.09))

    # Shoulder Storm Capelet (經典雙層風衣防雨披肩)
    cylinder("StormCapelet", "AdvCoatLapel", (0.0, -0.01, 1.15), 0.32, 0.14, segments=12, r2=0.28)
    
    # Plaid / Checkered Scarf wrapped around neck
    torus("ScarfWrap", "AdvScarfBase", (0.0, -0.01, 1.25), 0.18, 0.045, seg_major=10, seg_minor=4)
    # Scarf hanging tail in front
    box("ScarfTail", "AdvScarfBase", (0.06, -0.26, 1.12), (0.09, 0.035, 0.22), rot=(0.1, 0, 0.1))
    box("ScarfStripe1", "AdvScarfStripe", (0.06, -0.278, 1.16), (0.092, 0.01, 0.025), rot=(0.1, 0, 0.1))
    box("ScarfStripe2", "AdvScarfStripe", (0.06, -0.278, 1.06), (0.092, 0.01, 0.025), rot=(0.1, 0, 0.1))


# -------------------------------------------------------------
# 3. BACKPACK WITH FIELD BOOKS & MESSENGER SATCHEL
# -------------------------------------------------------------

def build_backpack_and_satchel():
    # 1. Diagonal Cross-Strap across chest for Satchel
    tube("SatchelStrap", "AdvPackDark", (-0.22, -0.25, 1.20), (0.24, 0.16, 0.66), 0.020, segments=4)
    cylinder("SatchelBuckle", "AdvScepterBrass", (0.02, -0.25, 0.94), 0.030, 0.015, segments=6, rot=(0.4, 0.8, 0))

    # Leather Messenger Satchel at right hip
    box("MessengerSatchel", "AdvPackBrown", (0.28, -0.06, 0.66), (0.10, 0.22, 0.18), rot=(0, 0, -0.2))
    box("SatchelFlap", "AdvPackDark", (0.285, -0.06, 0.74), (0.105, 0.225, 0.04), rot=(0, 0, -0.2))

    # 2. Leather Travel Backpack on Back
    bp_loc = (0.0, 0.28, 0.98)
    box("BackpackBody", "AdvPackBrown", bp_loc, (0.34, 0.18, 0.40))
    box("BackpackPocketLower", "AdvPackDark", (0.0, 0.38, 0.86), (0.24, 0.08, 0.16))
    box("BackpackTopFlap", "AdvPackDark", (0.0, 0.28, 1.18), (0.35, 0.19, 0.05))

    # 3. Colorful Field Journals / Books sticking out from top of backpack
    # Red Language Lexicon
    box("BookRedCover", "AdvBookRed", (-0.08, 0.28, 1.25), (0.045, 0.14, 0.14), rot=(0, 0.2, 0.1))
    box("BookRedPages", "AdvBookPages", (-0.08, 0.28, 1.25), (0.04, 0.13, 0.135), rot=(0, 0.2, 0.1))
    # Teal Atlas / Phrasebook
    box("BookTealCover", "AdvBookTeal", (0.08, 0.29, 1.24), (0.045, 0.14, 0.13), rot=(0, -0.15, -0.1))
    box("BookTealPages", "AdvBookPages", (0.08, 0.29, 1.24), (0.04, 0.13, 0.125), rot=(0, -0.15, -0.1))


# -------------------------------------------------------------
# 4. ARMS, HANDS & OPEN PASSPORT / NOTEBOOK
# -------------------------------------------------------------

def build_arms_and_passport():
    # --- RIGHT ARM (Resting on Multi-national Language Staff) ---
    r_sh = (0.34, -0.02, 1.14)
    icosphere("Shoulder_R", "AdvCoatLapel", r_sh, 0.12, subdivisions=1)
    # Upper Arm in trench coat
    tube("UpperArm_R", "AdvCoatOlive", r_sh, (0.46, -0.05, 0.94), 0.088, segments=8)
    icosphere("Elbow_R", "AdvCoatOlive", (0.46, -0.05, 0.94), 0.085, subdivisions=1)
    # Forearm extending to rest on staff
    tube("Forearm_R", "AdvCoatOlive", (0.46, -0.05, 0.94), (0.48, -0.22, 0.86), 0.080, segments=8)
    cylinder("CoatCuff_R", "AdvCoatLapel", (0.48, -0.22, 0.86), 0.085, 0.04, segments=8)
    # Right Hand resting on Globe Scepter top
    icosphere("Hand_R", "AdvSkin", (0.49, -0.25, 0.84), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_R", "AdvSkin", (0.45, -0.23, 0.85), (0.028, 0.04, 0.028), rot=(0.2, 0.2, 0.3))
    box("Fingers_R", "AdvSkin", (0.50, -0.26, 0.83), (0.065, 0.04, 0.065), rot=(0, 0.2, 0.2))

    # --- LEFT ARM (Holding Open Travel Passport / Notebook) ---
    l_sh = (-0.34, -0.02, 1.14)
    icosphere("Shoulder_L", "AdvCoatLapel", l_sh, 0.12, subdivisions=1)
    # Upper Arm
    tube("UpperArm_L", "AdvCoatOlive", l_sh, (-0.46, -0.08, 0.95), 0.088, segments=8)
    icosphere("Elbow_L", "AdvCoatOlive", (-0.46, -0.08, 0.95), 0.085, subdivisions=1)
    # Forearm bent forward holding book
    tube("Forearm_L", "AdvCoatOlive", (-0.46, -0.08, 0.95), (-0.32, -0.26, 0.90), 0.080, segments=8)
    cylinder("CoatCuff_L", "AdvCoatLapel", (-0.32, -0.26, 0.90), 0.085, 0.04, segments=8)
    # Left Hand presenting notebook
    icosphere("Hand_L", "AdvSkin", (-0.28, -0.32, 0.90), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_L", "AdvSkin", (-0.24, -0.30, 0.92), (0.028, 0.04, 0.028), rot=(0.2, -0.2, -0.3))
    box("Fingers_L", "AdvSkin", (-0.30, -0.34, 0.89), (0.065, 0.04, 0.065), rot=(0, -0.2, -0.2))

    # --- OPEN TRAVEL PASSPORT / EXPEDITION NOTEBOOK ---
    nb_pos = (-0.26, -0.38, 0.91)
    nb_rot = (0.28, 0.35, -0.15)
    # Left & Right open pages
    box("PassportCover_L", "AdvPassportCover", (nb_pos[0] - 0.06, nb_pos[1], nb_pos[2]), (0.12, 0.15, 0.015), rot=(0.28, 0.20, -0.3))
    box("PassportPages_L", "AdvBookPages", (nb_pos[0] - 0.06, nb_pos[1] - 0.005, nb_pos[2] + 0.008), (0.11, 0.14, 0.012), rot=(0.28, 0.20, -0.3))
    box("PassportCover_R", "AdvPassportCover", (nb_pos[0] + 0.06, nb_pos[1], nb_pos[2]), (0.12, 0.15, 0.015), rot=(0.28, 0.45, 0.0))
    box("PassportPages_R", "AdvBookPages", (nb_pos[0] + 0.06, nb_pos[1] - 0.005, nb_pos[2] + 0.008), (0.11, 0.14, 0.012), rot=(0.28, 0.45, 0.0))
    # Golden Visa Stamp / Language Glyph on right page
    box("VisaStamp", "AdvGoldBtn", (nb_pos[0] + 0.06, nb_pos[1] - 0.012, nb_pos[2] + 0.012), (0.035, 0.035, 0.006), rot=(0.28, 0.45, 0.0))


# -------------------------------------------------------------
# 5. HEAD, SILVER HAIR, BEARD, GLASSES & DEERSTALKER HAT
# -------------------------------------------------------------

def build_head_and_hat():
    # Neck
    cylinder("Neck", "AdvSkin", (0.0, -0.01, 1.28), 0.10, 0.12, segments=8)
    
    # Stylized faceted cranium
    icosphere("HeadBase", "AdvSkin", (0.0, 0.0, 1.48), 0.23, scale=(1.12, 1.0, 1.04), subdivisions=2)
    # Defined nose
    box("NoseBridge", "AdvNose", (0.0, -0.225, 1.46), (0.045, 0.05, 0.07), rot=(0.2, 0, 0))
    icosphere("NoseTip", "AdvNose", (0.0, -0.245, 1.43), 0.040, scale=(1.1, 0.9, 0.9), subdivisions=1)
    
    # Cheeks & Eyes
    for s in (-1, 1):
        icosphere(f"Cheek_{s}", "AdvCheek", (s * 0.12, -0.18, 1.41), 0.056, scale=(1.0, 0.6, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "AdvSkin", (s * 0.25, 0.01, 1.47), 0.045, scale=(0.5, 0.9, 1.2), subdivisions=1)
        # Eyes
        box(f"EyeWhite_{s}", "AdvEyeWhite", (s * 0.09, -0.205, 1.51), (0.055, 0.02, 0.035))
        box(f"EyeIris_{s}", "AdvEyeBlue", (s * 0.09, -0.218, 1.51), (0.032, 0.01, 0.032))
        box(f"EyePupil_{s}", "AdvPupil", (s * 0.09, -0.225, 1.51), (0.016, 0.008, 0.016))
        # Eyebrows
        box(f"Eyebrow_{s}", "AdvHairGrey", (s * 0.095, -0.21, 1.56), (0.075, 0.03, 0.025), rot=(0, 0, s * 0.12))

    # Distinguished Silver Hair on sides and back
    icosphere("HairBack", "AdvHairGrey", (0.0, 0.10, 1.48), 0.21, scale=(1.14, 0.95, 0.85), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"HairSide_{s}", "AdvHairGrey", (s * 0.21, 0.02, 1.48), 0.095, scale=(0.6, 1.2, 1.2), subdivisions=1)

    # Warm Dignified Smile showing teeth
    box("MouthCavity", "AdvLip", (0.0, -0.22, 1.34), (0.09, 0.02, 0.03))
    box("TeethUpper", "AdvTeeth", (0.0, -0.226, 1.35), (0.075, 0.015, 0.015))

    # Sculpted Silver Goatee Beard & Mustache
    icosphere("BeardGoatee", "AdvBeardGrey", (0.0, -0.16, 1.29), 0.14, scale=(0.85, 0.95, 0.70), subdivisions=1)
    for s in (-1, 1):
        tube(f"Mustache_{s}", "AdvBeardGrey", (0.0, -0.238, 1.39), (s * 0.11, -0.21, 1.37), 0.030, segments=5)

    # Clear Rectangular Framed Glasses
    for s in (-1, 1):
        box(f"GlassesFrame_{s}", "AdvGlassFrame", (s * 0.095, -0.235, 1.51), (0.10, 0.02, 0.055))
        box(f"GlassesLens_{s}", "AdvGlassLens", (s * 0.095, -0.245, 1.51), (0.088, 0.01, 0.044))
        tube(f"GlassesTemple_{s}", "AdvGlassFrame", (s * 0.14, -0.23, 1.51), (s * 0.235, -0.04, 1.51), 0.008, segments=4)
    box("GlassesBridge", "AdvGlassFrame", (0.0, -0.235, 1.51), (0.045, 0.015, 0.015))

    # Communications Boom Headset on Left Ear
    cylinder("HeadsetEarpiece", "AdvHeadset", (-0.265, -0.02, 1.48), 0.025, 0.02, segments=6, rot=(0, math.pi/2, 0))
    tube("BoomMicArm", "AdvHeadset", (-0.265, -0.02, 1.48), (-0.20, -0.18, 1.38), 0.006, segments=4)
    icosphere("BoomMicTip", "AdvMicTip", (-0.20, -0.18, 1.38), 0.012, subdivisions=1)

    # --- PLAID DEERSTALKER HAT (幾何獵帽) ---
    hat_z = 1.62
    # Crown of deerstalker hat (6-sided peaked dome)
    cylinder("DeerstalkerCrown", "AdvHatOlive", (0.0, -0.01, hat_z), 0.28, 0.22, segments=8, r2=0.18)
    cylinder("CrownTopRibbon", "AdvHatRibbon", (0.0, -0.01, hat_z + 0.12), 0.04, 0.03, segments=6)
    
    # Front visor brim
    box("FrontVisor", "AdvHatVisor", (0.0, -0.28, hat_z - 0.04), (0.28, 0.16, 0.022), rot=(-0.14, 0, 0))
    # Rear neck shade visor brim
    box("RearVisor", "AdvHatVisor", (0.0, 0.26, hat_z - 0.04), (0.28, 0.16, 0.022), rot=(0.14, 0, 0))

    # Earflaps folded and tied over the top with ribbon
    for s in (-1, 1):
        box(f"FoldedEarflap_{s}", "AdvHatOlive", (s * 0.12, -0.01, hat_z + 0.06), (0.06, 0.16, 0.12), rot=(0, s * 0.25, 0))
    box("TiedBowRibbon", "AdvHatRibbon", (0.0, -0.01, hat_z + 0.14), (0.10, 0.04, 0.03))


# -------------------------------------------------------------
# 6. MULTI-NATIONAL LANGUAGE STAFF (多面體多國語權杖頭)
# -------------------------------------------------------------

def build_language_staff():
    sx = 0.49
    sy = -0.25
    top_z = 0.84
    
    # 1. Faceted Optical Crystal Prism Bottom Spike
    cylinder("PrismTip", "AdvPrismPoint", (sx, sy, 0.04), 0.035, 0.08, segments=6, r2=0.01)
    cylinder("PrismMount", "AdvScepterBrass", (sx, sy, 0.09), 0.044, 0.03, segments=8)
    
    # 2. Rainbow Chromatic Spectrum Bar
    spectrum_mats = [
        "AdvSpecPurple",
        "AdvSpecBlue",
        "AdvSpecCyan",
        "AdvSpecGreen",
        "AdvSpecYellow",
        "AdvSpecOrange",
        "AdvSpecRed",
    ]
    band_h = 0.10
    for idx, smat in enumerate(spectrum_mats):
        bz = 0.14 + idx * band_h
        cylinder(f"SpectrumBand_{idx}", smat, (sx, sy, bz), 0.032, band_h, segments=8)
        box(f"SpecTick_{idx}", "AdvHatDark", (sx, sy - 0.033, bz), (0.016, 0.005, 0.012))

    # Upper Golden Collar
    cylinder("StaffUpperCollar", "AdvScepterBrass", (sx, sy, 0.86), 0.045, 0.04, segments=8)

    # 3. Interlocking Steampunk Clockwork Gears Mechanism
    gear_z = 0.96
    cylinder("MainGearHub", "AdvScepterGold", (sx, sy, gear_z), 0.075, 0.024, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("MainGearAxle", "AdvScepterBrass", (sx, sy - 0.015, gear_z), 0.040, 0.012, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.085 * math.cos(ang)
        tz = gear_z + 0.085 * math.sin(ang)
        box(f"MainGearTooth_{i}", "AdvScepterGold", (tx, sy, tz), (0.02, 0.022, 0.02), rot=(0, -ang, 0))

    # Small secondary gear
    s_gx = sx - 0.072
    s_gz = gear_z - 0.052
    cylinder("SmallGearHub", "AdvScepterBrass", (s_gx, sy + 0.008, s_gz), 0.042, 0.018, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(6):
        ang = i * math.pi / 3
        tx = s_gx + 0.048 * math.cos(ang)
        tz = s_gz + 0.048 * math.sin(ang)
        box(f"SmallTooth_{i}", "AdvScepterBrass", (tx, sy + 0.008, tz), (0.014, 0.018, 0.014), rot=(0, -ang, 0))

    # 4. Miniature 3D Globe with Golden Meridian Ring at Summit
    globe_z = gear_z + 0.16
    # Golden Meridian Ring around globe
    torus("GlobeMeridianRing", "AdvGlobeRing", (sx, sy, globe_z), 0.09, 0.012, seg_major=12, seg_minor=4, rot=(0, 0.4, 0))
    # Ocean Blue Base Globe Sphere
    icosphere("GlobeOcean", "AdvGlobeOcean", (sx, sy, globe_z), 0.078, subdivisions=2)
    # Green Continental Facets
    box("ContinentEuroAsia", "AdvGlobeLand", (sx, sy - 0.05, globe_z + 0.02), (0.045, 0.035, 0.045), rot=(0.2, 0.3, 0.1))
    box("ContinentAmericas", "AdvGlobeLand", (sx - 0.04, sy + 0.03, globe_z + 0.01), (0.035, 0.045, 0.06), rot=(0.1, -0.4, 0.2))
    box("ContinentAfrica", "AdvGlobeLand", (sx + 0.03, sy - 0.04, globe_z - 0.02), (0.035, 0.03, 0.04), rot=(0.2, 0.1, -0.3))
    # Golden Language Glyph Tiles on Ring
    box("GlyphTile1", "AdvScepterGold", (sx - 0.08, sy, globe_z + 0.05), (0.025, 0.015, 0.025), rot=(0, 0.4, 0))
    box("GlyphTile2", "AdvScepterGold", (sx + 0.08, sy, globe_z - 0.05), (0.025, 0.015, 0.025), rot=(0, 0.4, 0))


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
    obj.name = "Adventure"
    
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
    build_torso_and_coat()
    build_backpack_and_satchel()
    build_arms_and_passport()
    build_head_and_hat()
    build_language_staff()
    return finalize_and_export()


if __name__ == "__main__":
    generate_adventure()
