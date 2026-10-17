"""
Builder / Architect (建築師) 3D Model Generator for Blender.
Accurately implements character/builder.jpg based on Master Goal specifications:
- 3D Stylized Low-Poly Character (3.5-head tall male figure)
- Crisp faceted polygonal geometry, FLAT SHADING enabled
- Yellow construction hardhat with mounted protective safety goggles & yellow pencil tucked behind right ear
- Square-frame dark glasses with clear glass lenses
- Sculpted low-poly brown beard, mustache, and cheerful friendly expression
- Plaid shirt collar under beige tactical utility vest with flap pockets & shoulder epaulets
- Navy work jacket with rolled cuffs and leather utility belt
- Belt holding claw hammer, adjustable wrench, tape measure, and leather pouches
- Rolled architectural blueprint tube strapped diagonally across his back
- Left hand holds rolled-out blue blueprint schematic map and yellow wooden triangle ruler
- Right hand rests on Golden Survey Measuring Rod Staff:
  * Golden/yellow measuring ruler staff with black calibration tick marks
  * Interlocking brass/golden clockwork gear mechanism
  * Golden 6-sided die mounted at top
- Dark navy cargo work trousers with knee patches and heavy brown work boots
- Pivot at (0, 0, 0) at ground center, height ~ 1.83 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
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
    get_or_create_mat("BldSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("BldCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("BldNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("BldLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("BldTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("BldEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("BldEyeBlue", (0.12, 0.55, 0.85), rough=0.2)
    get_or_create_mat("BldPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("BldHairBrown", (0.28, 0.17, 0.10), rough=0.75)
    
    # Glasses & Goggles
    get_or_create_mat("BldGlassesFrame", (0.12, 0.13, 0.15), rough=0.4)
    get_or_create_mat("BldGlassesLens", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)
    get_or_create_mat("BldGoggleFrame", (0.16, 0.18, 0.22), rough=0.5)
    get_or_create_mat("BldGoggleLens", (0.15, 0.82, 0.92), rough=0.15, trans=0.6)

    # Yellow Construction Hardhat & Pencil
    get_or_create_mat("BldHardhatYellow", (0.96, 0.78, 0.12), rough=0.45)
    get_or_create_mat("BldHardhatDark", (0.18, 0.16, 0.14), rough=0.7)
    get_or_create_mat("BldPencilYellow", (0.95, 0.82, 0.14), rough=0.5)
    get_or_create_mat("BldPencilPink", (0.92, 0.45, 0.55), rough=0.7)
    get_or_create_mat("BldPencilBrass", (0.85, 0.65, 0.18), metal=0.88, rough=0.3)
    get_or_create_mat("BldPencilLead", (0.15, 0.15, 0.16), rough=0.5)

    # Clothing (Navy Jacket, Plaid Collar, Beige Tactical Vest)
    get_or_create_mat("BldJacketNavy", (0.14, 0.19, 0.28), rough=0.7)
    get_or_create_mat("BldJacketDark", (0.10, 0.13, 0.20), rough=0.75)
    get_or_create_mat("BldPlaidBase", (0.32, 0.36, 0.42), rough=0.7)
    get_or_create_mat("BldPlaidStripe", (0.75, 0.25, 0.20), rough=0.65)
    get_or_create_mat("BldVestBeige", (0.58, 0.52, 0.38), rough=0.75)
    get_or_create_mat("BldVestPocket", (0.48, 0.42, 0.28), rough=0.75)
    get_or_create_mat("BldVestTrim", (0.36, 0.30, 0.20), rough=0.7)

    # Trousers & Heavy Work Boots
    get_or_create_mat("BldPantsNavy", (0.16, 0.20, 0.26), rough=0.75)
    get_or_create_mat("BldKneePatch", (0.12, 0.15, 0.20), rough=0.8)
    get_or_create_mat("BldBootBrown", (0.32, 0.20, 0.12), rough=0.7)
    get_or_create_mat("BldBootDark", (0.18, 0.12, 0.08), rough=0.8)
    get_or_create_mat("BldBootSole", (0.08, 0.08, 0.08), rough=0.9)

    # Leather Utility Belt & Tools
    get_or_create_mat("BldBeltLeather", (0.32, 0.18, 0.10), rough=0.65)
    get_or_create_mat("BldBeltPouch", (0.40, 0.24, 0.14), rough=0.7)
    get_or_create_mat("BldBuckleSteel", (0.75, 0.77, 0.80), metal=0.9, rough=0.25)
    get_or_create_mat("BldToolSteel", (0.70, 0.72, 0.75), metal=0.9, rough=0.25)
    get_or_create_mat("BldWoodHandle", (0.48, 0.30, 0.16), rough=0.65)
    get_or_create_mat("BldTapeYellow", (0.95, 0.80, 0.12), rough=0.4)

    # Props: Blueprint Tube & Map & Triangle Ruler
    get_or_create_mat("BldTubeParchment", (0.85, 0.82, 0.75), rough=0.7)
    get_or_create_mat("BldTubeLeather", (0.35, 0.20, 0.12), rough=0.65)
    get_or_create_mat("BldBlueprintBlue", (0.16, 0.42, 0.82), rough=0.6)
    get_or_create_mat("BldBlueprintLine", (0.85, 0.92, 0.98), rough=0.5)
    get_or_create_mat("BldRulerYellow", (0.92, 0.65, 0.18), rough=0.55)

    # Survey Measuring Staff
    get_or_create_mat("BldScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("BldScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("BldRulerTick", (0.12, 0.12, 0.14), rough=0.5)

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
# 1. LEGS, NAVY CARGO TROUSERS & HEAVY WORK BOOTS
# -------------------------------------------------------------

def build_legs_and_boots():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Navy work cargo trousers thigh
        cylinder(f"LegThigh_{side}", "BldPantsNavy", (sx, 0.0, 0.44), 0.135, 0.32, segments=8)
        # Knee cuff & dark reinforcement patch
        cylinder(f"Knee_{side}", "BldPantsNavy", (sx, 0.01, 0.28), 0.125, 0.12, segments=8)
        box(f"KneePatch_{side}", "BldKneePatch", (sx, -0.10, 0.28), (0.13, 0.04, 0.12))
        
        # Cargo side pocket
        px = sx + (0.135 if sx > 0 else -0.135)
        box(f"CargoPocket_{side}", "BldPantsNavy", (px, 0.0, 0.42), (0.05, 0.14, 0.16), rot=(0, 0.1 if sx > 0 else -0.1, 0))
        box(f"CargoFlap_{side}", "BldKneePatch", (px, 0.0, 0.50), (0.055, 0.145, 0.04), rot=(0, 0.1 if sx > 0 else -0.1, 0))

        # Heavy Work Boot Shaft
        cylinder(f"BootShaft_{side}", "BldBootBrown", (sx, 0.0, 0.17), 0.115, 0.16, segments=8)
        
        # Boot Foot & Steel Toe Cap
        box(f"BootFoot_{side}", "BldBootBrown", (sx, -0.06, 0.08), (0.21, 0.30, 0.14))
        box(f"BootToe_{side}", "BldBootDark", (sx, -0.18, 0.07), (0.19, 0.10, 0.12))
        
        # Deep Tread Soles
        box(f"BootSole_{side}", "BldBootSole", (sx, -0.06, 0.02), (0.23, 0.34, 0.04))
        box(f"BootHeel_{side}", "BldBootSole", (sx, 0.05, 0.03), (0.23, 0.12, 0.06))
        # Boot Lacing Panel
        box(f"BootLace_{side}", "BldBootDark", (sx, -0.10, 0.13), (0.14, 0.12, 0.03))


# -------------------------------------------------------------
# 2. TORSO, JACKET, PLAID SHIRT & BEIGE TACTICAL VEST
# -------------------------------------------------------------

def build_torso_and_vest():
    # Pelvis / Lower Jacket Hem
    cylinder("Pelvis", "BldJacketNavy", (0.0, 0.0, 0.60), 0.26, 0.16, segments=10)
    
    # Leather Utility Tool Belt
    cylinder("UtilityBelt", "BldBeltLeather", (0.0, 0.0, 0.67), 0.28, 0.08, segments=12)
    box("BeltBuckle", "BldBuckleSteel", (0.0, -0.285, 0.67), (0.12, 0.03, 0.08))
    
    # Torso (Navy Work Jacket + Beige Tactical Vest)
    cylinder("TorsoLower", "BldJacketNavy", (0.0, -0.01, 0.88), 0.295, 0.36, segments=10, r2=0.275)
    cylinder("TorsoChest", "BldJacketNavy", (0.0, -0.02, 1.10), 0.28, 0.24, segments=10, r2=0.25)
    
    # Inner Plaid Shirt Neck & Collar V
    box("PlaidShirtCenter", "BldPlaidBase", (0.0, -0.23, 1.05), (0.14, 0.08, 0.28))
    box("PlaidStripe1", "BldPlaidStripe", (0.0, -0.24, 1.08), (0.03, 0.07, 0.20))
    # Folded Plaid Collar Wings
    box("PlaidCollar_L", "BldPlaidBase", (-0.06, -0.24, 1.20), (0.07, 0.04, 0.08), rot=(0.1, 0.2, -0.2))
    box("PlaidCollar_R", "BldPlaidBase", (0.06, -0.24, 1.20), (0.07, 0.04, 0.08), rot=(0.1, -0.2, 0.2))

    # Beige Tactical Utility Vest (Front panels)
    box("VestPanel_L", "BldVestBeige", (-0.14, -0.26, 0.95), (0.12, 0.06, 0.40), rot=(0, 0, -0.08))
    box("VestPanel_R", "BldVestBeige", (0.14, -0.26, 0.95), (0.12, 0.06, 0.40), rot=(0, 0, 0.08))
    # Vest Back Panel
    box("VestBack", "BldVestBeige", (0.0, 0.24, 0.96), (0.38, 0.06, 0.42))
    
    # Chest Flap Pockets on Vest
    box("ChestPocket_L", "BldVestPocket", (-0.14, -0.29, 1.04), (0.10, 0.04, 0.09))
    box("ChestPocketFlap_L", "BldVestTrim", (-0.14, -0.295, 1.09), (0.11, 0.045, 0.03))
    box("ChestPocket_R", "BldVestPocket", (0.14, -0.29, 1.04), (0.10, 0.04, 0.09))
    box("ChestPocketFlap_R", "BldVestTrim", (0.14, -0.295, 1.09), (0.11, 0.045, 0.03))
    
    # Lower Bellows Pockets on Vest
    box("LowerPocket_L", "BldVestPocket", (-0.15, -0.28, 0.82), (0.11, 0.05, 0.11))
    box("LowerPocketFlap_L", "BldVestTrim", (-0.15, -0.285, 0.88), (0.12, 0.055, 0.03))
    box("LowerPocket_R", "BldVestPocket", (0.15, -0.28, 0.82), (0.11, 0.05, 0.11))
    box("LowerPocketFlap_R", "BldVestTrim", (0.15, -0.285, 0.88), (0.12, 0.055, 0.03))

    # Shoulder Epaulets / Straps with brass rivets
    for s, sx in [("L", -0.24), ("R", 0.24)]:
        box(f"Epaulet_{s}", "BldVestTrim", (sx, 0.0, 1.22), (0.08, 0.18, 0.03), rot=(0, 0.2 if s=="R" else -0.2, 0))
        cylinder(f"EpauletRivet_{s}", "BldBuckleSteel", (sx, -0.06, 1.23), 0.015, 0.01, segments=6)


# -------------------------------------------------------------
# 3. UTILITY BELT TOOLS & BACKPACK BLUEPRINT TUBE
# -------------------------------------------------------------

def build_belt_tools_and_blueprint_tube():
    # 1. Left side belt tool pouch
    box("BeltPouch_L", "BldBeltPouch", (-0.28, -0.08, 0.65), (0.08, 0.14, 0.12), rot=(0, 0, 0.3))
    box("BeltPouchFlap_L", "BldBeltLeather", (-0.285, -0.08, 0.71), (0.085, 0.145, 0.03), rot=(0, 0, 0.3))
    
    # 2. Right side belt tool pouch
    box("BeltPouch_R", "BldBeltPouch", (0.28, -0.08, 0.65), (0.08, 0.14, 0.12), rot=(0, 0, -0.3))
    
    # 3. Steel Claw Hammer hanging on right belt loop
    # Hammer handle
    tube("HammerHandle", "BldWoodHandle", (0.24, -0.22, 0.68), (0.24, -0.22, 0.42), 0.016, segments=6)
    # Steel head & claws
    box("HammerHead", "BldToolSteel", (0.24, -0.22, 0.68), (0.04, 0.11, 0.04))
    box("HammerClaw", "BldToolSteel", (0.24, -0.17, 0.69), (0.035, 0.05, 0.025), rot=(0.4, 0, 0))
    # Belt leather loop holding hammer
    torus("HammerLoop", "BldBeltLeather", (0.24, -0.22, 0.67), 0.026, 0.008, rot=(math.pi/2, 0, 0))

    # 4. Adjustable Wrench hanging on front-left belt loop
    tube("WrenchHandle", "BldToolSteel", (-0.18, -0.26, 0.67), (-0.16, -0.26, 0.48), 0.014, segments=4)
    cylinder("WrenchHead", "BldToolSteel", (-0.18, -0.26, 0.67), 0.032, 0.02, segments=6, rot=(math.pi/2, 0, 0))
    box("WrenchJaw", "BldToolSteel", (-0.18, -0.26, 0.70), (0.05, 0.02, 0.04))

    # 5. Tape Measure on right hip
    box("TapeMeasureBody", "BldTapeYellow", (0.28, -0.18, 0.65), (0.06, 0.07, 0.07))
    cylinder("TapeBumper", "BldHardhatDark", (0.28, -0.18, 0.65), 0.025, 0.075, segments=8, rot=(0, math.pi/2, 0))
    box("TapeClip", "BldToolSteel", (0.25, -0.18, 0.66), (0.01, 0.03, 0.05))

    # 6. Diagonal Cross-Strap across chest for Blueprint Tube
    tube("CrossStrap", "BldBeltLeather", (-0.22, -0.24, 1.18), (0.22, 0.18, 0.68), 0.022, segments=4)
    cylinder("StrapBuckle", "BldBuckleSteel", (-0.02, -0.24, 0.95), 0.032, 0.015, segments=6, rot=(0.4, 0.8, 0))

    # 7. Rolled Blueprint Tube strapped diagonally across back
    tube_start = (-0.32, 0.32, 1.35)
    tube_end = (0.28, 0.28, 0.55)
    # Heavy parchment / leather cylindrical tube body
    tube("BlueprintTubeBody", "BldTubeParchment", tube_start, tube_end, 0.075, segments=10)
    # Leather end caps and reinforcement collars
    cylinder("TubeCapTop", "BldTubeLeather", tube_start, 0.082, 0.06, segments=10)
    cylinder("TubeCapBottom", "BldTubeLeather", tube_end, 0.082, 0.06, segments=10)
    # Rolled blueprint paper edge peeking out of top cap
    cylinder("PaperRollPeak", "BldBlueprintBlue", (tube_start[0], tube_start[1], tube_start[2] + 0.04), 0.068, 0.04, segments=8)
    # Leather straps securing the tube
    cylinder("TubeStrap1", "BldTubeLeather", (-0.12, 0.31, 1.08), 0.082, 0.03, segments=10)
    cylinder("TubeStrap2", "BldTubeLeather", (0.08, 0.29, 0.82), 0.082, 0.03, segments=10)


# -------------------------------------------------------------
# 4. ARMS, HANDS & PROPS (BLUEPRINT & TRIANGLE RULER)
# -------------------------------------------------------------

def build_arms_and_props():
    # --- RIGHT ARM (Resting on Golden Survey Staff) ---
    r_sh = (0.34, -0.02, 1.15)
    icosphere("ArmShoulder_R", "BldVestTrim", r_sh, 0.12, subdivisions=1)
    # Upper arm in navy jacket
    tube("UpperArm_R", "BldJacketNavy", r_sh, (0.46, -0.05, 0.94), 0.088, segments=8)
    icosphere("Elbow_R", "BldJacketNavy", (0.46, -0.05, 0.94), 0.085, subdivisions=1)
    # Forearm extending forward/down to grasp staff
    tube("Forearm_R", "BldJacketNavy", (0.46, -0.05, 0.94), (0.48, -0.22, 0.86), 0.080, segments=8)
    # Rolled-up jacket cuff
    cylinder("Cuff_R", "BldVestTrim", (0.48, -0.22, 0.86), 0.085, 0.04, segments=8)
    # Right Hand grasping staff top
    icosphere("Hand_R", "BldSkin", (0.49, -0.25, 0.84), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_R", "BldSkin", (0.45, -0.23, 0.85), (0.028, 0.04, 0.028), rot=(0.2, 0.2, 0.3))
    box("Fingers_R", "BldSkin", (0.50, -0.26, 0.83), (0.065, 0.04, 0.065), rot=(0, 0.2, 0.2))

    # --- LEFT ARM (Holding Open Blueprint & Triangle Ruler) ---
    l_sh = (-0.34, -0.02, 1.15)
    icosphere("ArmShoulder_L", "BldVestTrim", l_sh, 0.12, subdivisions=1)
    # Upper arm angled down & forward
    tube("UpperArm_L", "BldJacketNavy", l_sh, (-0.46, -0.08, 0.95), 0.088, segments=8)
    icosphere("Elbow_L", "BldJacketNavy", (-0.46, -0.08, 0.95), 0.085, subdivisions=1)
    # Forearm bent forward/inward to hold the blueprint roll
    tube("Forearm_L", "BldJacketNavy", (-0.46, -0.08, 0.95), (-0.32, -0.28, 0.88), 0.080, segments=8)
    # Rolled cuff
    cylinder("Cuff_L", "BldVestTrim", (-0.32, -0.28, 0.88), 0.085, 0.04, segments=8)
    # Left Hand holding blueprint
    icosphere("Hand_L", "BldSkin", (-0.30, -0.32, 0.88), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_L", "BldSkin", (-0.26, -0.30, 0.90), (0.028, 0.04, 0.028), rot=(0.2, -0.2, -0.3))
    box("Fingers_L", "BldSkin", (-0.32, -0.34, 0.87), (0.065, 0.04, 0.065), rot=(0, -0.2, -0.2))

    # --- HELD PROPS IN LEFT HAND ---
    # 1. ROLLED-OUT BLUE BLUEPRINT MAP
    bp_loc = (-0.24, -0.38, 0.89)
    bp_rot = (0.35, -0.15, 0.25)
    # Main unfolded parchment sheet
    box("BlueprintSheet", "BldBlueprintBlue", bp_loc, (0.32, 0.24, 0.012), rot=bp_rot)
    # Rolled edges of the blueprint
    cylinder("BPRoll_Top", "BldBlueprintBlue", (-0.22, -0.47, 0.96), 0.022, 0.33, segments=8, rot=(0.25, 0.9, 0))
    cylinder("BPRoll_Bottom", "BldBlueprintBlue", (-0.26, -0.28, 0.82), 0.022, 0.33, segments=8, rot=(0.25, 0.9, 0))
    # White Architectural Grid / CAD Lines on blueprint
    box("BPGridLine1", "BldBlueprintLine", (-0.24, -0.38, 0.898), (0.24, 0.008, 0.014), rot=bp_rot)
    box("BPGridLine2", "BldBlueprintLine", (-0.24, -0.38, 0.898), (0.008, 0.18, 0.014), rot=bp_rot)
    box("BPSchematicRect", "BldBlueprintLine", (-0.20, -0.36, 0.898), (0.12, 0.08, 0.014), rot=bp_rot)

    # 2. WOODEN DRAFTING TRIANGLE RULER
    tri_loc = (-0.38, -0.36, 0.96)
    tri_rot = (0.2, 0.4, -0.3)
    # Triangle frame legs (hypotenuse and two catheti)
    tube("TriLeg1", "BldRulerYellow", (-0.44, -0.34, 0.88), (-0.44, -0.34, 1.06), 0.014, segments=4)
    tube("TriLeg2", "BldRulerYellow", (-0.44, -0.34, 0.88), (-0.30, -0.37, 0.88), 0.014, segments=4)
    tube("TriHypotenuse", "BldRulerYellow", (-0.44, -0.34, 1.06), (-0.30, -0.37, 0.88), 0.014, segments=4)
    # Center cutout web plate
    box("TriWeb", "BldRulerYellow", tri_loc, (0.08, 0.01, 0.08), rot=tri_rot)


# -------------------------------------------------------------
# 5. HEAD, HAIR, BEARD, GLASSES & YELLOW HARDHAT WITH GOGGLES
# -------------------------------------------------------------

def build_head_and_hardhat():
    # Neck
    cylinder("Neck", "BldSkin", (0.0, -0.01, 1.28), 0.10, 0.12, segments=8)
    
    # Base Head (Stylized faceted low-poly cranium)
    icosphere("HeadBase", "BldSkin", (0.0, 0.0, 1.48), 0.23, scale=(1.12, 1.0, 1.04), subdivisions=2)
    # Defined Faceted Nose
    box("NoseBridge", "BldNose", (0.0, -0.225, 1.46), (0.045, 0.05, 0.07), rot=(0.2, 0, 0))
    icosphere("NoseTip", "BldNose", (0.0, -0.245, 1.43), 0.038, scale=(1.1, 0.9, 0.9), subdivisions=1)
    
    # Cheerful Cheeks & Smile
    for s in (-1, 1):
        icosphere(f"Cheek_{s}", "BldCheek", (s * 0.12, -0.18, 1.41), 0.055, scale=(1.0, 0.6, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "BldSkin", (s * 0.25, 0.01, 1.47), 0.045, scale=(0.5, 0.9, 1.2), subdivisions=1)
        # Eyes (Whites, Iris, Pupil)
        box(f"EyeWhite_{s}", "BldEyeWhite", (s * 0.09, -0.205, 1.51), (0.055, 0.02, 0.035))
        box(f"EyeIris_{s}", "BldEyeBlue", (s * 0.09, -0.218, 1.51), (0.032, 0.01, 0.032))
        box(f"EyePupil_{s}", "BldPupil", (s * 0.09, -0.225, 1.51), (0.016, 0.008, 0.016))
        # Eyebrows
        box(f"Eyebrow_{s}", "BldHairBrown", (s * 0.095, -0.21, 1.56), (0.075, 0.03, 0.025), rot=(0, 0, s * 0.12))

    # Mouth & Teeth (Warm friendly smile)
    box("MouthCavity", "BldLip", (0.0, -0.22, 1.34), (0.09, 0.02, 0.03))
    box("TeethUpper", "BldTeeth", (0.0, -0.226, 1.35), (0.075, 0.015, 0.015))

    # Sculpted Mustache & Beard
    # Full brown beard around jaw
    icosphere("BeardChin", "BldHairBrown", (0.0, -0.15, 1.30), 0.17, scale=(1.0, 0.95, 0.65), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"BeardJaw_{s}", "BldHairBrown", (s * 0.16, -0.06, 1.36), 0.09, scale=(0.7, 1.2, 1.4), subdivisions=1)
        # Handlebar/Neat mustache tabs
        tube(f"Mustache_{s}", "BldHairBrown", (0.0, -0.238, 1.39), (s * 0.11, -0.21, 1.365), 0.032, segments=5)
        tube(f"MustacheTip_{s}", "BldHairBrown", (s * 0.11, -0.21, 1.365), (s * 0.15, -0.18, 1.385), 0.024, segments=5, r2=0.01)

    # Hair at back and temples
    icosphere("HairBack", "BldHairBrown", (0.0, 0.10, 1.49), 0.21, scale=(1.12, 0.95, 0.85), subdivisions=1)

    # Square-Frame Glasses (Architect's signature dark spectacles)
    for s in (-1, 1):
        torus(f"GlassesFrame_{s}", "BldGlassesFrame", (s * 0.09, -0.235, 1.51), 0.052, 0.012, seg_major=4, seg_minor=4, rot=(math.pi/2, 0, math.pi/4))
        cylinder(f"GlassesLens_{s}", "BldGlassesLens", (s * 0.09, -0.235, 1.51), 0.046, 0.005, segments=4, rot=(math.pi/2, 0, math.pi/4))
        tube(f"GlassesTemple_{s}", "BldGlassesFrame", (s * 0.14, -0.23, 1.51), (s * 0.235, -0.04, 1.51), 0.008, segments=4)
    box("GlassesBridge", "BldGlassesFrame", (0.0, -0.24, 1.51), (0.045, 0.012, 0.012))

    # --- YELLOW CONSTRUCTION HARDHAT (幾何多邊形安全帽) ---
    hat_z = 1.62
    # Main Hardhat Dome (faceted crown)
    icosphere("HardhatDome", "BldHardhatYellow", (0.0, -0.01, hat_z), 0.27, scale=(1.08, 1.12, 0.72), subdivisions=1)
    # Hardhat Central Reinforcement Ridge (Top Crest)
    box("HardhatCrest", "BldHardhatYellow", (0.0, -0.01, hat_z + 0.18), (0.06, 0.46, 0.05), rot=(-0.05, 0, 0))
    # Hardhat Brim all around
    cylinder("HardhatBrim", "BldHardhatYellow", (0.0, -0.02, hat_z - 0.03), 0.31, 0.025, segments=12, rot=(0.06, 0, 0))
    # Front visor lip
    box("HardhatVisorLip", "BldHardhatYellow", (0.0, -0.28, hat_z - 0.02), (0.28, 0.12, 0.02), rot=(-0.14, 0, 0))
    # Black protective edge trim
    cylinder("HardhatTrim", "BldHardhatDark", (0.0, -0.02, hat_z - 0.045), 0.295, 0.015, segments=12, rot=(0.06, 0, 0))

    # Safety Goggles mounted on front of hardhat
    goggle_z = hat_z + 0.04
    goggle_y = -0.26
    # Goggle elastic strap around helmet
    cylinder("GoggleStrap", "BldHardhatDark", (0.0, -0.01, goggle_z), 0.28, 0.035, segments=12, rot=(0.06, 0, 0))
    # Dual rectangular goggle frames
    for s in (-1, 1):
        box(f"GoggleFrame_{s}", "BldGoggleFrame", (s * 0.09, goggle_y, goggle_z), (0.095, 0.04, 0.06), rot=(-0.12, 0, s * 0.08))
        box(f"GoggleLens_{s}", "BldGoggleLens", (s * 0.09, goggle_y - 0.02, goggle_z), (0.08, 0.01, 0.045), rot=(-0.12, 0, s * 0.08))
    box("GoggleBridge", "BldGoggleFrame", (0.0, goggle_y + 0.01, goggle_z), (0.04, 0.03, 0.03))

    # Yellow Architect Pencil tucked behind right ear / hardhat brim
    p_start = (0.26, -0.16, hat_z - 0.01)
    p_end = (0.28, 0.14, hat_z + 0.06)
    tube("PencilBody", "BldPencilYellow", p_start, p_end, 0.012, segments=6)
    tube("PencilTipWood", "BldSkin", p_start, (p_start[0], p_start[1] - 0.035, p_start[2] - 0.01), 0.012, segments=6, r2=0.0)
    cylinder("PencilLead", "BldPencilLead", (p_start[0], p_start[1] - 0.032, p_start[2] - 0.01), 0.004, 0.01, segments=4)
    # Brass ferrule & pink eraser at back of pencil
    cylinder("PencilFerrule", "BldPencilBrass", (p_end[0], p_end[1] + 0.015, p_end[2] + 0.005), 0.013, 0.02, segments=6)
    cylinder("PencilEraser", "BldPencilPink", (p_end[0], p_end[1] + 0.035, p_end[2] + 0.012), 0.012, 0.025, segments=6)


# -------------------------------------------------------------
# 6. GOLDEN SURVEY MEASURING ROD STAFF (多面體測量權杖)
# -------------------------------------------------------------

def build_survey_measuring_staff():
    sx = 0.49
    sy = -0.25
    top_z = 0.84
    
    # 1. Base steel ferrule tip at ground
    cylinder("StaffSpike", "BldToolSteel", (sx, sy, 0.03), 0.035, 0.06, segments=6, r2=0.01)
    cylinder("StaffBottomCollar", "BldScepterBrass", (sx, sy, 0.08), 0.042, 0.04, segments=8)
    
    # 2. Main Survey Measuring Ruler Bar (黃金測量標尺桿)
    tube("SurveyRod", "BldHardhatYellow", (sx, sy, 0.08), (sx, sy, top_z), 0.032, segments=4)
    # Metric calibration graduation ticks along the front of the ruler
    for i in range(12):
        tz = 0.14 + i * 0.055
        box(f"RulerTick_{i}", "BldRulerTick", (sx, sy - 0.033, tz), (0.042, 0.006, 0.012))

    # Upper brass collar below grip
    cylinder("StaffUpperCollar", "BldScepterBrass", (sx, sy, top_z + 0.02), 0.044, 0.04, segments=8)

    # 3. Clockwork Brass Mechanical Gear Assembly (精密齒輪機構)
    gear_z = top_z + 0.12
    # Primary large drive gear
    cylinder("MainGearHub", "BldScepterGold", (sx, sy, gear_z), 0.075, 0.025, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("MainGearAxle", "BldScepterBrass", (sx, sy - 0.015, gear_z), 0.040, 0.012, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.085 * math.cos(ang)
        tz = gear_z + 0.085 * math.sin(ang)
        box(f"MainGearTooth_{i}", "BldScepterGold", (tx, sy, tz), (0.02, 0.022, 0.02), rot=(0, -ang, 0))

    # Secondary interlocking small brass gear
    s_gx = sx - 0.075
    s_gz = gear_z - 0.055
    cylinder("SmallGearHub", "BldScepterBrass", (s_gx, sy + 0.01, s_gz), 0.045, 0.02, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(6):
        ang = i * math.pi / 3
        tx = s_gx + 0.052 * math.cos(ang)
        tz = s_gz + 0.052 * math.sin(ang)
        box(f"SmallGearTooth_{i}", "BldScepterBrass", (tx, sy + 0.01, tz), (0.015, 0.018, 0.015), rot=(0, -ang, 0))

    # 4. Golden 6-Sided Die mounted on top of the staff (多面體黃金骰子)
    die_z = gear_z + 0.15
    die_rot = (0.35, 0.35, 0.45)
    box("GoldenDie", "BldScepterGold", (sx, sy, die_z), (0.13, 0.13, 0.13), rot=die_rot)
    # Recessed dark pips
    box("DiePip1", "BldHardhatDark", (sx, sy - 0.07, die_z), (0.025, 0.01, 0.025), rot=die_rot)
    box("DiePip2", "BldHardhatDark", (sx - 0.035, sy - 0.06, die_z + 0.035), (0.02, 0.01, 0.02), rot=die_rot)
    box("DiePip3", "BldHardhatDark", (sx + 0.035, sy - 0.06, die_z - 0.035), (0.02, 0.01, 0.02), rot=die_rot)


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
    obj.name = "Builder"
    
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
    build_belt_tools_and_blueprint_tube()
    build_arms_and_props()
    build_head_and_hardhat()
    build_survey_measuring_staff()
    return finalize_and_export()


if __name__ == "__main__":
    generate_builder()
