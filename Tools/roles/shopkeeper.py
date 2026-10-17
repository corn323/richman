"""
Shopkeeper / Merchant (店長 / 算盤商人) 3D Model Generator for Blender.
Accurately implements character/shopkeeper.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Burgundy newsboy flat cap (幾何小禮帽) with front visor & mounted golden jewelers loupe (放大鏡)
- Round gold wireframe spectacles / pince-nez with reflective glass lenses
- Curled merchant mustache, goatee beard, warm shrewd smile
- Forest-green tailored merchant coat with wide folded lapels & turn-back cuffs
- Crimson vest with vertical gold pinstripes and gold buttons
- Patterned gold/burgundy cravat tie & purple silk pocket square
- Left hand holds traditional wooden Abacus (精緻算盤) with counter beads
- Right hand rests on Polyhedral Balance Scale Scepter (多面體天平權杖):
  * Rainbow chromatic spectrum bar with calibration ticks
  * Faceted optical prism bottom spike
  * Interlocking brass gear and balance scale beam with two hanging brass pans
  * Golden polyhedral die at the summit
- Leather utility belt with coin pouch, hanging ledger book & brass keys
- Merchant travel pack with top blanket/scroll rolls
- Tailored charcoal trousers and Victorian lace-up leather boots
- Pivot at (0, 0, 0) at ground center, height ~ 1.82 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
- Exports to assets/models/shopkeeper.glb
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
        if o.name.startswith("Shopkeeper") or o.name.startswith("part_") or o.name == "Shopkeeper":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Shp") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Shp"):
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
    get_or_create_mat("ShpSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("ShpCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("ShpNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("ShpLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("ShpTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("ShpEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("ShpEyeBlue", (0.12, 0.55, 0.85), rough=0.2)
    get_or_create_mat("ShpPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("ShpHairBrown", (0.24, 0.15, 0.10), rough=0.75)
    
    # Spectacles & Loupe
    get_or_create_mat("ShpGoldWire", (0.95, 0.80, 0.22), metal=0.92, rough=0.20)
    get_or_create_mat("ShpGlass", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)
    
    # Newsboy Cap (Burgundy / Wine Red)
    get_or_create_mat("ShpHatBurgundy", (0.50, 0.10, 0.16), rough=0.7)
    get_or_create_mat("ShpHatDark", (0.35, 0.07, 0.11), rough=0.75)
    get_or_create_mat("ShpHatVisor", (0.40, 0.22, 0.14), rough=0.65)
    
    # Merchant Tailored Clothing
    get_or_create_mat("ShpCoatTeal", (0.16, 0.28, 0.28), rough=0.65)
    get_or_create_mat("ShpCoatDark", (0.10, 0.20, 0.20), rough=0.7)
    get_or_create_mat("ShpVestCrimson", (0.52, 0.10, 0.15), rough=0.7)
    get_or_create_mat("ShpGoldStripe", (0.95, 0.76, 0.16), metal=0.8, rough=0.3)
    get_or_create_mat("ShpCravatGold", (0.92, 0.62, 0.18), rough=0.55)
    get_or_create_mat("ShpPocketPurple", (0.55, 0.18, 0.65), rough=0.5)
    get_or_create_mat("ShpShirtWhite", (0.94, 0.94, 0.92), rough=0.6)
    
    # Trousers & Leather Boots
    get_or_create_mat("ShpPantsCharcoal", (0.14, 0.17, 0.22), rough=0.75)
    get_or_create_mat("ShpBootLeather", (0.32, 0.18, 0.10), rough=0.65)
    get_or_create_mat("ShpBootDark", (0.16, 0.10, 0.08), rough=0.8)
    get_or_create_mat("ShpBootSole", (0.08, 0.08, 0.08), rough=0.9)
    get_or_create_mat("ShpBeltLeather", (0.38, 0.22, 0.12), rough=0.6)
    get_or_create_mat("ShpBrassBuckle", (0.88, 0.70, 0.18), metal=0.88, rough=0.25)
    
    # Abacus (算盤)
    get_or_create_mat("ShpWoodFrame", (0.42, 0.24, 0.12), rough=0.6)
    get_or_create_mat("ShpWoodDark", (0.28, 0.15, 0.08), rough=0.65)
    get_or_create_mat("ShpBeadRed", (0.90, 0.18, 0.18), rough=0.4)
    get_or_create_mat("ShpBeadYellow", (0.95, 0.78, 0.15), rough=0.4)
    get_or_create_mat("ShpBeadCyan", (0.15, 0.75, 0.85), rough=0.4)
    
    # Backpack & Ledger
    get_or_create_mat("ShpPackBrown", (0.42, 0.26, 0.14), rough=0.7)
    get_or_create_mat("ShpScrollRoll", (0.88, 0.82, 0.70), rough=0.7)
    get_or_create_mat("ShpLedgerCover", (0.45, 0.15, 0.55), rough=0.6)
    get_or_create_mat("ShpLedgerPages", (0.92, 0.90, 0.85), rough=0.8)
    
    # Balance Scale Scepter & Rainbow Spectrum
    get_or_create_mat("ShpScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("ShpScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("ShpPrismPoint", (0.45, 0.20, 0.65), metal=0.3, rough=0.2)
    get_or_create_mat("ShpSpecRed", (0.95, 0.18, 0.18), rough=0.3)
    get_or_create_mat("ShpSpecOrange", (0.98, 0.48, 0.08), rough=0.3)
    get_or_create_mat("ShpSpecYellow", (0.98, 0.88, 0.10), rough=0.3)
    get_or_create_mat("ShpSpecGreen", (0.18, 0.85, 0.28), rough=0.3)
    get_or_create_mat("ShpSpecCyan", (0.15, 0.88, 0.98), rough=0.3)
    get_or_create_mat("ShpSpecBlue", (0.12, 0.35, 0.95), rough=0.3)
    get_or_create_mat("ShpSpecPurple", (0.65, 0.15, 0.85), rough=0.3)

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
# 1. LEGS, TROUSERS & VICTORIAN LEATHER BOOTS
# -------------------------------------------------------------

def build_legs_and_boots():
    for side, sx in [("L", -0.16), ("R", 0.16)]:
        # Tailored trouser thigh
        cylinder(f"LegThigh_{side}", "ShpPantsCharcoal", (sx, 0.0, 0.44), 0.13, 0.32, segments=8)
        # Knee crease
        cylinder(f"Knee_{side}", "ShpPantsCharcoal", (sx, 0.01, 0.28), 0.12, 0.12, segments=8)
        # Trouser cuff tucked into boots
        cylinder(f"PantCuff_{side}", "ShpPantsCharcoal", (sx, 0.0, 0.20), 0.125, 0.08, segments=8)

        # Victorian Boot Upper Shaft
        cylinder(f"BootShaft_{side}", "ShpBootLeather", (sx, 0.0, 0.14), 0.11, 0.16, segments=8)
        
        # Leather Foot
        box(f"BootFoot_{side}", "ShpBootLeather", (sx, -0.06, 0.08), (0.20, 0.29, 0.14))
        # Boot Cap / Toe
        box(f"BootToe_{side}", "ShpBootDark", (sx, -0.17, 0.07), (0.18, 0.10, 0.12))
        # Boot Sole
        box(f"BootSole_{side}", "ShpBootSole", (sx, -0.06, 0.02), (0.22, 0.33, 0.04))
        box(f"BootHeel_{side}", "ShpBootSole", (sx, 0.05, 0.035), (0.22, 0.12, 0.07))
        
        # Gold Brass Eyelets / Buckles on boot front
        for bz in [0.08, 0.12, 0.16]:
            cylinder(f"BootEyelet_{side}_{bz}", "ShpScepterGold", (sx, -0.13, bz), 0.012, 0.02, segments=6, rot=(math.pi/2, 0, 0))


# -------------------------------------------------------------
# 2. TORSO, COAT, PINSTRIPE VEST & CRAVAT TIE
# -------------------------------------------------------------

def build_torso_and_clothing():
    # Lower coat skirt / Pelvis
    cylinder("Pelvis", "ShpCoatTeal", (0.0, 0.0, 0.60), 0.26, 0.16, segments=10)
    
    # Leather Belt
    cylinder("LeatherBelt", "ShpBeltLeather", (0.0, 0.0, 0.67), 0.28, 0.08, segments=12)
    box("BeltBuckle", "ShpBrassBuckle", (0.0, -0.285, 0.67), (0.12, 0.03, 0.08))
    
    # Outer Teal Coat Torso
    cylinder("CoatTorso", "ShpCoatTeal", (0.0, -0.01, 0.88), 0.295, 0.36, segments=10, r2=0.275)
    cylinder("CoatChest", "ShpCoatTeal", (0.0, -0.02, 1.10), 0.28, 0.24, segments=10, r2=0.25)
    
    # Inner Crimson Vest (紅金直條紋背心)
    box("VestCenter", "ShpVestCrimson", (0.0, -0.23, 0.98), (0.16, 0.08, 0.38))
    
    # Vertical Gold Pinstripes on Vest (金色垂直細條紋)
    for px in [-0.055, 0.055]:
        box(f"VestStripe_{px}", "ShpGoldStripe", (px, -0.275, 0.98), (0.015, 0.02, 0.36))
    
    # Gold Vest Buttons down center
    for bz in [0.84, 0.92, 1.00]:
        cylinder(f"VestButton_{bz}", "ShpScepterGold", (0.0, -0.285, bz), 0.015, 0.012, segments=6, rot=(math.pi/2, 0, 0))

    # White Shirt Collar
    box("ShirtCollar_L", "ShpShirtWhite", (-0.06, -0.24, 1.22), (0.06, 0.04, 0.06), rot=(0, 0, -0.3))
    box("ShirtCollar_R", "ShpShirtWhite", (0.06, -0.24, 1.22), (0.06, 0.04, 0.06), rot=(0, 0, 0.3))

    # Paisley Gold Cravat / Ascot Tie at Neck
    box("CravatKnot", "ShpCravatGold", (0.0, -0.26, 1.22), (0.06, 0.05, 0.06))
    cylinder("CravatPin", "ShpScepterGold", (0.0, -0.288, 1.22), 0.012, 0.01, segments=6, rot=(math.pi/2, 0, 0))
    # Flowing Cravat Ribbons
    box("CravatDrape", "ShpCravatGold", (0.0, -0.265, 1.14), (0.08, 0.03, 0.12))

    # Folded Wide Coat Lapels framing the vest
    box("CoatLapel_L", "ShpCoatDark", (-0.14, -0.25, 1.02), (0.11, 0.06, 0.34), rot=(0, 0, -0.15))
    box("CoatLapel_R", "ShpCoatDark", (0.14, -0.25, 1.02), (0.11, 0.06, 0.34), rot=(0, 0, 0.15))

    # Turned up Coat Collar around neck
    cylinder("CoatCollar", "ShpCoatDark", (0.0, 0.04, 1.22), 0.25, 0.14, segments=10, r2=0.22)

    # Purple Pocket Square on right chest pocket (紫色方巾)
    box("ChestPocket", "ShpCoatDark", (-0.17, -0.26, 1.06), (0.09, 0.03, 0.08))
    box("PocketSquare", "ShpPocketPurple", (-0.17, -0.27, 1.10), (0.06, 0.02, 0.04), rot=(0, 0, 0.25))

    # Golden Brooch / Merchant Monogram Badge on left lapel
    cylinder("MerchantBrooch", "ShpScepterGold", (0.16, -0.26, 1.08), 0.026, 0.015, segments=8, rot=(math.pi/2, 0, 0))
    cylinder("BroochJewel", "ShpSpecCyan", (0.16, -0.27, 1.08), 0.014, 0.008, segments=6, rot=(math.pi/2, 0, 0))


# -------------------------------------------------------------
# 3. ACCESSORIES, COIN POUCH, LEDGER & BACKPACK
# -------------------------------------------------------------

def build_backpack_and_accessories():
    # Leather Coin Pouch on left hip
    box("CoinPouch_L", "ShpBeltLeather", (0.28, -0.06, 0.62), (0.12, 0.16, 0.15), rot=(0, 0, -0.2))
    box("CoinPouchFlap_L", "ShpBootDark", (0.285, -0.06, 0.68), (0.13, 0.165, 0.04), rot=(0, 0, -0.2))
    cylinder("CoinPouchButton", "ShpScepterGold", (0.33, -0.06, 0.65), 0.012, 0.02, segments=6, rot=(0, math.pi/2, 0))

    # Hanging Ledger Book & Brass Keys on right hip
    # Hanging Leather Ledger (帳本)
    box("LedgerCover", "ShpLedgerCover", (-0.28, -0.08, 0.52), (0.06, 0.15, 0.20), rot=(0, 0, 0.25))
    box("LedgerPages", "ShpLedgerPages", (-0.275, -0.08, 0.52), (0.045, 0.13, 0.18), rot=(0, 0, 0.25))
    box("LedgerStrap", "ShpBeltLeather", (-0.28, -0.08, 0.52), (0.065, 0.03, 0.205), rot=(0, 0, 0.25))
    
    # Hanging Brass Keys Ring
    tube("KeyRing", "ShpScepterGold", (-0.20, -0.24, 0.64), (-0.20, -0.25, 0.61), 0.014, segments=6)
    torus("KeyBow1", "ShpScepterGold", (-0.20, -0.25, 0.58), 0.018, 0.004, rot=(0, math.pi/2, 0))
    cylinder("KeyShaft1", "ShpScepterGold", (-0.20, -0.25, 0.52), 0.005, 0.10, segments=6)
    box("KeyBit1", "ShpScepterGold", (-0.20, -0.24, 0.49), (0.005, 0.016, 0.016))

    # Merchant Travel Backpack (皮革商旅行囊)
    bx, by, bz = 0.0, 0.28, 0.95
    box("BackpackMain", "ShpPackBrown", (bx, by, bz), (0.38, 0.22, 0.46))
    box("BackpackFlap", "ShpBootDark", (bx, by, bz + 0.18), (0.39, 0.23, 0.08))
    box("BackpackSide_L", "ShpPackBrown", (bx - 0.20, by, bz - 0.05), (0.07, 0.16, 0.24))
    box("BackpackSide_R", "ShpPackBrown", (bx + 0.20, by, bz - 0.05), (0.07, 0.16, 0.24))

    # Strapped Bedroll / Scrolls on top of backpack
    cylinder("Bedroll_Main", "ShpScrollRoll", (bx, by + 0.02, bz + 0.27), 0.07, 0.42, segments=10, rot=(0, math.pi/2, 0))
    cylinder("BedrollStrap_L", "ShpBeltLeather", (bx - 0.12, by + 0.02, bz + 0.27), 0.075, 0.025, segments=10, rot=(0, math.pi/2, 0))
    cylinder("BedrollStrap_R", "ShpBeltLeather", (bx + 0.12, by + 0.02, bz + 0.27), 0.075, 0.025, segments=10, rot=(0, math.pi/2, 0))


# -------------------------------------------------------------
# 4. ARMS, HANDS & TRADITIONAL ABACUS (精緻算盤)
# -------------------------------------------------------------

def build_arms_and_abacus():
    # --- RIGHT ARM (Holds Balance Scale Scepter) ---
    cylinder("Shoulder_R", "ShpCoatTeal", (-0.31, -0.02, 1.18), 0.11, 0.16, segments=8, rot=(0, 0.35, 0))
    # Turn-back coat cuff
    cylinder("CoatCuff_R", "ShpCoatDark", (-0.36, -0.05, 1.08), 0.115, 0.06, segments=8, rot=(0, 0.35, 0))
    tube("Forearm_R", "ShpCoatTeal", (-0.36, -0.05, 1.08), (-0.46, -0.22, 0.88), 0.085, segments=8)
    # White shirt cuff peeking out
    cylinder("ShirtCuff_R", "ShpShirtWhite", (-0.46, -0.22, 0.88), 0.082, 0.03, segments=8)
    # Hand resting firmly on scepter head
    box("Hand_R", "ShpSkin", (-0.47, -0.24, 0.86), (0.10, 0.11, 0.09), rot=(0.2, 0.1, -0.1))
    box("Thumb_R", "ShpSkin", (-0.44, -0.20, 0.87), (0.04, 0.06, 0.04), rot=(0.2, 0.1, -0.1))

    # --- LEFT ARM (Proudly holding the Wooden Abacus) ---
    cylinder("Shoulder_L", "ShpCoatTeal", (0.31, -0.02, 1.18), 0.11, 0.16, segments=8, rot=(0, -0.35, 0))
    cylinder("CoatCuff_L", "ShpCoatDark", (0.36, -0.05, 1.08), 0.115, 0.06, segments=8, rot=(0, -0.35, 0))
    # Forearm bent forward/upward to hold abacus
    tube("Forearm_L", "ShpCoatTeal", (0.36, -0.05, 1.08), (0.30, -0.28, 0.90), 0.085, segments=8)
    cylinder("ShirtCuff_L", "ShpShirtWhite", (0.30, -0.28, 0.90), 0.082, 0.03, segments=8)
    # Left Hand gripping bottom of abacus
    box("Hand_L", "ShpSkin", (0.26, -0.32, 0.89), (0.10, 0.10, 0.09), rot=(-0.25, 0.45, -0.5))

    # --- TRADITIONAL WOODEN ABACUS (東方經典木算盤) ---
    # Tilted naturally toward the viewer: held at x = 0.25, y = -0.36, z = 0.95
    ab_loc = (0.24, -0.38, 0.95)
    ab_rot = (-0.55, 0.32, -0.42)
    
    # 1. Outer Wooden Frame (Rectangular frame)
    box("AbacusFrame", "ShpWoodFrame", ab_loc, (0.24, 0.02, 0.34), rot=ab_rot)
    # Inner Cutout backing plate
    box("AbacusBacking", "ShpWoodDark", (ab_loc[0], ab_loc[1] - 0.006, ab_loc[2]), (0.20, 0.008, 0.30), rot=ab_rot)
    
    # 2. Partition Beam (橫樑分隔上下檔)
    box("AbacusBeam", "ShpWoodFrame", (ab_loc[0], ab_loc[1] - 0.010, ab_loc[2] + 0.04), (0.20, 0.014, 0.02), rot=ab_rot)

    # 3. Brass Counting Rods (銅檔桿)
    for k in [-0.07, -0.035, 0.0, 0.035, 0.07]:
        # Upper section rod
        box(f"AbacusRodUpper_{k}", "ShpScepterGold", (ab_loc[0] + k, ab_loc[1] - 0.010, ab_loc[2] + 0.09), (0.006, 0.006, 0.08), rot=ab_rot)
        # Lower section rod
        box(f"AbacusRodLower_{k}", "ShpScepterGold", (ab_loc[0] + k, ab_loc[1] - 0.010, ab_loc[2] - 0.05), (0.006, 0.006, 0.16), rot=ab_rot)
        
        # Colorful Counter Beads (算盤珠子)
        # Upper deck beads (1 or 2 beads)
        cylinder(f"BeadUpper_{k}", "ShpBeadRed", (ab_loc[0] + k, ab_loc[1] - 0.012, ab_loc[2] + 0.09), 0.014, 0.015, segments=8, rot=ab_rot)
        # Lower deck beads (3 beads stacked)
        cylinder(f"BeadLower1_{k}", "ShpBeadYellow", (ab_loc[0] + k, ab_loc[1] - 0.012, ab_loc[2] - 0.02), 0.014, 0.015, segments=8, rot=ab_rot)
        cylinder(f"BeadLower2_{k}", "ShpBeadYellow", (ab_loc[0] + k, ab_loc[1] - 0.012, ab_loc[2] - 0.05), 0.014, 0.015, segments=8, rot=ab_rot)
        cylinder(f"BeadLower3_{k}", "ShpBeadCyan", (ab_loc[0] + k, ab_loc[1] - 0.012, ab_loc[2] - 0.08), 0.014, 0.015, segments=8, rot=ab_rot)


# -------------------------------------------------------------
# 5. HEAD, FACE, SPECTACLES & NEWSBOY CAP WITH LOUPE
# -------------------------------------------------------------

def build_head_and_hat():
    # Neck
    cylinder("Neck", "ShpSkin", (0.0, 0.0, 1.25), 0.12, 0.14, segments=8)
    
    # Head Base
    icosphere("HeadBase", "ShpSkin", (0.0, -0.02, 1.44), 0.24, scale=(0.95, 1.05, 1.08), subdivisions=1)
    
    # Cheeks & Nose
    icosphere("Cheek_L", "ShpCheek", (-0.12, -0.19, 1.41), 0.05, subdivisions=1)
    icosphere("Cheek_R", "ShpCheek", (0.12, -0.19, 1.41), 0.05, subdivisions=1)
    box("NoseBridge", "ShpSkin", (0.0, -0.22, 1.45), (0.045, 0.06, 0.09))
    box("NoseTip", "ShpNose", (0.0, -0.25, 1.42), (0.055, 0.05, 0.05))
    
    # Eyes
    for side, ex in [("L", -0.08), ("R", 0.08)]:
        box(f"EyeWhite_{side}", "ShpEyeWhite", (ex, -0.21, 1.48), (0.055, 0.03, 0.04))
        cylinder(f"EyeIris_{side}", "ShpEyeBlue", (ex, -0.222, 1.48), 0.018, 0.01, segments=6, rot=(math.pi/2, 0, 0))
        cylinder(f"EyePupil_{side}", "ShpPupil", (ex, -0.226, 1.48), 0.010, 0.01, segments=6, rot=(math.pi/2, 0, 0))
        # Thin cultured eyebrow
        box(f"Eyebrow_{side}", "ShpHairBrown", (ex, -0.22, 1.52), (0.075, 0.03, 0.02), rot=(0, 0.15 if side == "L" else -0.15, 0))

    # Astute Merchant Smile & Teeth
    box("MouthCavity", "ShpLip", (0.0, -0.225, 1.34), (0.11, 0.03, 0.04))
    box("TeethUpper", "ShpTeeth", (0.0, -0.232, 1.35), (0.09, 0.02, 0.02))

    # Iconic Curled Merchant Mustache (優雅的捲曲翹鬍子)
    box("MustacheCenter", "ShpHairBrown", (0.0, -0.25, 1.39), (0.05, 0.04, 0.025))
    box("Mustache_L", "ShpHairBrown", (-0.06, -0.245, 1.385), (0.08, 0.04, 0.028), rot=(0, 0, 0.25))
    box("Mustache_R", "ShpHairBrown", (0.06, -0.245, 1.385), (0.08, 0.04, 0.028), rot=(0, 0, -0.25))
    # Curled mustache tips upward
    box("MustacheCurl_L", "ShpHairBrown", (-0.11, -0.24, 1.405), (0.035, 0.03, 0.03), rot=(0, 0, 0.7))
    box("MustacheCurl_R", "ShpHairBrown", (0.11, -0.24, 1.405), (0.035, 0.03, 0.03), rot=(0, 0, -0.7))

    # Goatee Beard / Chin Beard
    box("BeardGoatee", "ShpHairBrown", (0.0, -0.21, 1.29), (0.08, 0.08, 0.08))
    box("BeardChin", "ShpHairBrown", (0.0, -0.19, 1.28), (0.14, 0.12, 0.09))
    # Sideburns
    box("Sideburn_L", "ShpHairBrown", (-0.21, -0.04, 1.44), (0.04, 0.10, 0.12))
    box("Sideburn_R", "ShpHairBrown", (0.21, -0.04, 1.44), (0.04, 0.10, 0.12))
    # Hair in back
    box("HairBack", "ShpHairBrown", (0.0, 0.14, 1.37), (0.28, 0.12, 0.14))

    # --- ROUND GOLD WIRE SPECTACLES / PINCE-NEZ (復古金絲圓眼鏡) ---
    # Wire nose bridge
    tube("SpecBridge", "ShpGoldWire", (-0.03, -0.24, 1.48), (0.03, -0.24, 1.48), 0.006, segments=4)
    for side, gx in [("L", -0.08), ("R", 0.08)]:
        # Round Gold Rim
        torus(f"SpecRim_{side}", "ShpGoldWire", (gx, -0.242, 1.48), 0.040, 0.005, rot=(math.pi/2, 0, 0))
        # Clear Glass Lens
        cylinder(f"SpecLens_{side}", "ShpGlass", (gx, -0.242, 1.48), 0.036, 0.004, segments=8, rot=(math.pi/2, 0, 0))
        # Temple arm extending to ear
        tube(f"SpecTemple_{side}", "ShpGoldWire", (gx + (-0.04 if side == "L" else 0.04), -0.23, 1.48), (gx + (-0.12 if side == "L" else 0.12), -0.04, 1.48), 0.005, segments=4)

    # --- BURGUNDY NEWSBOY CAP (幾何小禮帽) ---
    # Cap Lower Band / Crown
    cylinder("CapCrownLower", "ShpHatBurgundy", (0.0, -0.01, 1.60), 0.26, 0.16, segments=10, r2=0.28)
    # Flared Newsboy Dome
    icosphere("CapCrownDome", "ShpHatBurgundy", (0.0, 0.02, 1.68), 0.27, scale=(1.05, 1.10, 0.55), subdivisions=1)
    # Center Cap Button
    cylinder("CapButton", "ShpHatDark", (0.0, 0.02, 1.76), 0.03, 0.02, segments=8)
    
    # Cap Visor / Peak (前伸帽簷)
    box("CapVisor", "ShpHatVisor", (0.0, -0.23, 1.60), (0.32, 0.18, 0.03), rot=(0.28, 0, 0))

    # --- MOUNTED GOLDEN JEWELERS LOUPE (側邊珠寶鑑定放大鏡) ---
    # Mounted on the left/front side of the cap brim: x = 0.15, y = -0.22, z = 1.65
    lx, ly, lz = 0.16, -0.22, 1.66
    # Gold mounting swivel bracket
    box("LoupeBracket", "ShpScepterGold", (lx + 0.03, ly, lz), (0.02, 0.03, 0.04), rot=(0, 0, 0.3))
    # Loupe Gold Rim
    torus("LoupeRim", "ShpScepterGold", (lx, ly - 0.02, lz), 0.045, 0.008, rot=(math.pi/2 - 0.2, 0, 0.2))
    # Loupe Magnifying Glass Lens
    cylinder("LoupeLens", "ShpGlass", (lx, ly - 0.02, lz), 0.040, 0.006, segments=8, rot=(math.pi/2 - 0.2, 0, 0.2))
    # Loupe Handle/Fold arm
    tube("LoupeHandle", "ShpScepterGold", (lx + 0.04, ly - 0.01, lz), (lx + 0.08, ly + 0.02, lz + 0.04), 0.007, segments=4)


# -------------------------------------------------------------
# 6. POLYHEDRAL BALANCE SCALE SCEPTER (多面體天平權杖)
# -------------------------------------------------------------

def build_balance_scepter():
    # Held in right hand, planted straight on the ground:
    # x = -0.48, y = -0.22, base at ground z = 0.0
    sx, sy = -0.48, -0.22

    # 1. Pointed Faceted Optical Prism Tip at bottom (底座光學稜鏡角錐)
    cylinder("PrismTip", "ShpPrismPoint", (sx, sy, 0.04), 0.045, 0.08, segments=6, r2=0.0)
    cylinder("PrismMount", "ShpScepterGold", (sx, sy, 0.09), 0.048, 0.03, segments=8)

    # 2. RAINBOW CHROMATIC SPECTRUM BAR (光譜色階柱)
    spectrum_mats = [
        "ShpSpecPurple",
        "ShpSpecBlue",
        "ShpSpecCyan",
        "ShpSpecGreen",
        "ShpSpecYellow",
        "ShpSpecOrange",
        "ShpSpecRed",
    ]
    band_h = 0.10
    for idx, smat in enumerate(spectrum_mats):
        bz = 0.16 + idx * band_h
        cylinder(f"SpectrumBand_{idx}", smat, (sx, sy, bz), 0.032, band_h, segments=8)
        box(f"SpecTick_{idx}", "ShpCoatDark", (sx, sy - 0.033, bz), (0.016, 0.005, 0.012))

    # Shaft top gold collar
    cylinder("ShaftTopCollar", "ShpScepterBrass", (sx, sy, 0.88), 0.045, 0.05, segments=8)

    # 3. Interlocking Brass Gear & Merchant Monogram Hub
    gear_z = 0.98
    cylinder("GearHub", "ShpScepterGold", (sx, sy, gear_z), 0.075, 0.025, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("MonogramCenter", "ShpScepterBrass", (sx, sy - 0.015, gear_z), 0.045, 0.010, segments=8, rot=(math.pi/2, 0, 0))
    # Gear Teeth
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.085 * math.cos(ang)
        tz = gear_z + 0.085 * math.sin(ang)
        box(f"GearTooth_{i}", "ShpScepterGold", (tx, sy, tz), (0.02, 0.022, 0.02), rot=(0, -ang, 0))

    # 4. HORIZONTAL BALANCE SCALE BEAM & DUAL SCALE PANS (象徵公平交易的天平)
    beam_z = gear_z + 0.07
    box("ScaleBeam", "ShpScepterGold", (sx, sy, beam_z), (0.26, 0.025, 0.02))
    cylinder("BeamPivotCap", "ShpScepterBrass", (sx, sy - 0.015, beam_z), 0.022, 0.02, segments=8, rot=(math.pi/2, 0, 0))

    # Left & Right Hanging Balance Scale Pans
    for side, bx in [("L", -0.11), ("R", 0.11)]:
        wx = sx + bx
        # Hanging chain rods
        tube(f"ScaleChain_{side}_1", "ShpScepterBrass", (wx, sy, beam_z), (wx - 0.025, sy, beam_z - 0.09), 0.005, segments=4)
        tube(f"ScaleChain_{side}_2", "ShpScepterBrass", (wx, sy, beam_z), (wx + 0.025, sy, beam_z - 0.09), 0.005, segments=4)
        # Conical Balance Scale Pan (黃金秤盤)
        cylinder(f"ScalePan_{side}", "ShpScepterGold", (wx, sy, beam_z - 0.095), 0.042, 0.018, segments=8, r2=0.015)
        # Gold Coin / Weight in Pan
        cylinder(f"ScaleWeight_{side}", "ShpScepterBrass", (wx, sy, beam_z - 0.085), 0.022, 0.015, segments=6)

    # 5. GOLDEN POLYHEDRAL DIE (多面體黃金骰子權杖頂)
    die_z = 1.15
    die_rot = (0.35, 0.35, 0.45)
    box("GoldenDie", "ShpScepterGold", (sx, sy, die_z), (0.13, 0.13, 0.13), rot=die_rot)
    # Die Pips
    box("DiePip1", "ShpHatBurgundy", (sx, sy - 0.07, die_z), (0.025, 0.01, 0.025), rot=die_rot)
    box("DiePip2", "ShpHatBurgundy", (sx - 0.035, sy - 0.06, die_z + 0.035), (0.02, 0.01, 0.02), rot=die_rot)
    box("DiePip3", "ShpHatBurgundy", (sx + 0.035, sy - 0.06, die_z - 0.035), (0.02, 0.01, 0.02), rot=die_rot)


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
    obj.name = "Shopkeeper"
    
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
    print(f"Shopkeeper finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "shopkeeper.glb")
    
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


def generate_shopkeeper():
    reset_scene()
    setup_materials()
    build_legs_and_boots()
    build_torso_and_clothing()
    build_backpack_and_accessories()
    build_arms_and_abacus()
    build_head_and_hat()
    build_balance_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_shopkeeper()
