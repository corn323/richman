"""
Fitness Trainer / Coach (健身教練) 3D Model Generator for Blender.
Accurately implements character/coach.jpg based on Master Goal specifications:
- 3D Stylized Low-Poly Character (3.5-head tall male figure)
- Crisp faceted polygonal geometry, FLAT SHADING enabled
- Athletic, muscular build with sculpted low-poly arms and shoulders
- Backwards blue baseball cap with orange sweatband & rear visor
- Stylish sports glasses / wraparound cyan visor & athletic bluetooth headset
- Short neat beard, mustache, and confident energetic athletic smile
- Blue hooded sleeveless athletic vest with orange chevron energy accents & center zipper
- Vibrant orange workout towel draped around neck and shoulders
- Muscular bare arms with athletic wristbands & purple crystal badge
- Blue athletic shorts with orange side stripes over dark compression leggings
- High-top athletic running sneakers with orange accents and aerodynamic soles
- Left hand holds sports gradient water bottle (or athletic waist holster)
- Right hand rests on Dumbbell Scepter (多面體槓片權杖頭):
  * Heavy barbell bumper base plate
  * Black & yellow textured measuring barbell rod
  * Horizontal dumbbell bar with dual heavy weight plates & brass lock collars
  * Steampunk brass/gold clockwork gears in center
  * Golden 6-sided polyhedral die at summit
- Pivot at (0, 0, 0) at ground center, height ~ 1.83 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
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
    # Skin & Face (Athletic healthy tan)
    get_or_create_mat("CchSkin", (0.92, 0.67, 0.52), rough=0.7)
    get_or_create_mat("CchCheek", (0.88, 0.45, 0.38), rough=0.7)
    get_or_create_mat("CchNose", (0.90, 0.58, 0.46), rough=0.7)
    get_or_create_mat("CchLip", (0.48, 0.18, 0.18), rough=0.6)
    get_or_create_mat("CchTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("CchEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("CchEyeBlue", (0.12, 0.55, 0.85), rough=0.2)
    get_or_create_mat("CchPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("CchHairBrown", (0.24, 0.15, 0.10), rough=0.75)
    
    # Sports Visor / Glasses
    get_or_create_mat("CchVisorFrame", (0.12, 0.20, 0.35), rough=0.4)
    get_or_create_mat("CchVisorLens", (0.16, 0.85, 0.95), rough=0.1, trans=0.75)
    get_or_create_mat("CchHeadset", (0.15, 0.16, 0.18), metal=0.6, rough=0.3)

    # Baseball Cap (Worn Backwards)
    get_or_create_mat("CchCapNavy", (0.12, 0.22, 0.38), rough=0.7)
    get_or_create_mat("CchCapDark", (0.08, 0.15, 0.26), rough=0.75)
    get_or_create_mat("CchCapVisor", (0.10, 0.18, 0.32), rough=0.65)
    get_or_create_mat("CchSweatbandOrange", (0.96, 0.48, 0.10), rough=0.6)

    # Athletic Apparel (Navy Hooded Sleeveless Vest, Chevron Accents)
    get_or_create_mat("CchVestNavy", (0.14, 0.24, 0.40), rough=0.7)
    get_or_create_mat("CchVestDark", (0.09, 0.16, 0.28), rough=0.75)
    get_or_create_mat("CchOrangeAccent", (0.96, 0.48, 0.10), rough=0.55)
    get_or_create_mat("CchZipperSteel", (0.75, 0.77, 0.80), metal=0.9, rough=0.25)
    get_or_create_mat("CchTowelOrange", (0.96, 0.48, 0.10), rough=0.8)
    get_or_create_mat("CchBadgePurple", (0.55, 0.18, 0.75), rough=0.3, metal=0.4)

    # Shorts & Compression Leggings
    get_or_create_mat("CchShortsNavy", (0.14, 0.24, 0.40), rough=0.7)
    get_or_create_mat("CchLeggingsDark", (0.12, 0.13, 0.16), rough=0.8)
    get_or_create_mat("CchKneePad", (0.18, 0.20, 0.25), rough=0.75)

    # Running Sneakers
    get_or_create_mat("CchSneakerNavy", (0.14, 0.24, 0.40), rough=0.65)
    get_or_create_mat("CchSneakerOrange", (0.96, 0.48, 0.10), rough=0.55)
    get_or_create_mat("CchSneakerSole", (0.92, 0.92, 0.90), rough=0.8)
    get_or_create_mat("CchSneakerDark", (0.10, 0.10, 0.12), rough=0.85)

    # Water Bottle & Wristbands
    get_or_create_mat("CchWristbandNavy", (0.12, 0.22, 0.38), rough=0.6)
    get_or_create_mat("CchBottleCyan", (0.16, 0.78, 0.92), rough=0.3)
    get_or_create_mat("CchBottleMagenta", (0.75, 0.18, 0.65), rough=0.3)
    get_or_create_mat("CchBottleCap", (0.15, 0.16, 0.18), rough=0.5)
    get_or_create_mat("CchBeltPouch", (0.28, 0.22, 0.16), rough=0.7)

    # Dumbbell Staff & Metallic Elements
    get_or_create_mat("CchBarbellBlack", (0.16, 0.18, 0.20), metal=0.75, rough=0.35)
    get_or_create_mat("CchScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("CchScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("CchYellowBar", (0.95, 0.82, 0.14), rough=0.45)
    get_or_create_mat("CchRodTick", (0.12, 0.12, 0.14), rough=0.5)

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
# 1. LEGS, COMPRESSION LEGGINGS & ATHLETIC SNEAKERS
# -------------------------------------------------------------

def build_legs_and_sneakers():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Dark compression leggings (lower leg)
        cylinder(f"LegTights_{side}", "CchLeggingsDark", (sx, 0.0, 0.32), 0.105, 0.28, segments=8)
        # Knee pads
        cylinder(f"KneePad_{side}", "CchKneePad", (sx, 0.01, 0.26), 0.115, 0.10, segments=8)
        box(f"KneeCap_{side}", "CchOrangeAccent", (sx, -0.09, 0.26), (0.11, 0.03, 0.09))
        
        # Athletic Gym Shorts (Navy with orange side stripe)
        cylinder(f"Shorts_{side}", "CchShortsNavy", (sx, 0.0, 0.46), 0.145, 0.24, segments=8)
        # Bright orange side stripe
        px = sx + (0.145 if sx > 0 else -0.145)
        box(f"ShortsStripe_{side}", "CchOrangeAccent", (px, 0.0, 0.46), (0.02, 0.14, 0.22), rot=(0, 0.08 if sx > 0 else -0.08, 0))

        # High-top Athletic Sneaker Shaft
        cylinder(f"SneakerShaft_{side}", "CchSneakerNavy", (sx, 0.0, 0.16), 0.11, 0.14, segments=8)
        # Rear orange heel tab
        box(f"HeelTab_{side}", "CchSneakerOrange", (sx, 0.10, 0.16), (0.05, 0.02, 0.08))
        
        # Sneaker Foot & Orange Toe Accent
        box(f"SneakerFoot_{side}", "CchSneakerNavy", (sx, -0.06, 0.08), (0.21, 0.28, 0.13))
        box(f"SneakerToe_{side}", "CchSneakerOrange", (sx, -0.17, 0.07), (0.19, 0.09, 0.11))
        
        # Aerodynamic Sneaker Sole (White midsole + dark tread)
        box(f"SneakerMidSole_{side}", "CchSneakerSole", (sx, -0.06, 0.03), (0.23, 0.32, 0.04))
        box(f"SneakerTread_{side}", "CchSneakerDark", (sx, -0.06, 0.012), (0.24, 0.33, 0.025))
        # Orange side wave on sole
        box(f"SoleWave_{side}", "CchOrangeAccent", (sx, -0.02, 0.045), (0.235, 0.12, 0.02))


# -------------------------------------------------------------
# 2. TORSO, HOODED ATHLETIC VEST, TOWEL & ACCESSORIES
# -------------------------------------------------------------

def build_torso_and_vest():
    # Pelvis / Lower Shorts Base
    cylinder("Pelvis", "CchShortsNavy", (0.0, 0.0, 0.58), 0.26, 0.16, segments=10)
    
    # Athletic utility belt
    cylinder("GymBelt", "CchCapDark", (0.0, 0.0, 0.65), 0.275, 0.07, segments=12)
    box("BeltBuckle", "CchZipperSteel", (0.0, -0.28, 0.65), (0.10, 0.025, 0.07))
    
    # Muscular Athletic V-Taper Torso (Defined waist, broad chest)
    # Lower waist
    cylinder("TorsoWaist", "CchVestNavy", (0.0, -0.01, 0.82), 0.275, 0.28, segments=10, r2=0.29)
    # Broad Muscular Chest
    cylinder("TorsoChest", "CchVestNavy", (0.0, -0.02, 1.05), 0.30, 0.26, segments=10, r2=0.31)
    
    # Orange Chevron Energy Graphics on Vest
    for s in (-1, 1):
        # Upper chest diagonal chevron
        tube(f"ChevronUpper_{s}", "CchOrangeAccent", (s * 0.04, -0.26, 1.16), (s * 0.18, -0.28, 0.98), 0.022, segments=4)
        # Lower rib diagonal chevron
        tube(f"ChevronLower_{s}", "CchOrangeAccent", (s * 0.26, -0.22, 0.88), (s * 0.08, -0.27, 0.76), 0.022, segments=4)

    # Center Metallic Zipper
    tube("VestZipper", "CchZipperSteel", (0.0, -0.26, 1.18), (0.0, -0.28, 0.69), 0.014, segments=4)
    cylinder("ZipperPull", "CchZipperSteel", (0.0, -0.29, 0.98), 0.018, 0.03, segments=6)

    # Purple Diamond Emblem on Left Chest
    box("ChestEmblem", "CchBadgePurple", (-0.14, -0.28, 1.05), (0.05, 0.02, 0.05), rot=(0, 0, 0.78))

    # Folded Hood on Vest Back
    cylinder("VestHoodRoll", "CchVestDark", (0.0, 0.22, 1.18), 0.24, 0.14, segments=8, rot=(0.3, 0, 0))

    # --- VIBRANT ORANGE WORKOUT TOWEL DRAPED OVER SHOULDERS ---
    # Towel passes behind neck and drapes down over both shoulders onto chest
    cylinder("TowelNeckBack", "CchTowelOrange", (0.0, 0.12, 1.25), 0.22, 0.10, segments=8, rot=(0.2, 0, 0))
    for s, sx in [("L", -0.22), ("R", 0.22)]:
        # Over shoulder
        box(f"TowelShoulder_{s}", "CchTowelOrange", (sx, -0.04, 1.22), (0.10, 0.26, 0.05), rot=(0, 0.15 if s=="R" else -0.15, 0))
        # Draping down in front of chest
        box(f"TowelFront_{s}", "CchTowelOrange", (sx, -0.24, 1.02), (0.09, 0.05, 0.34), rot=(0.08, 0, 0))
        # Towel bottom hem fringe
        box(f"TowelFringe_{s}", "CchTowelOrange", (sx, -0.25, 0.84), (0.095, 0.055, 0.03))


# -------------------------------------------------------------
# 3. WAIST PACK & SPORTS WATER BOTTLE
# -------------------------------------------------------------

def build_waist_accessories():
    # Athletic waist pack at rear-right
    box("GymWaistPack", "CchBeltPouch", (0.24, 0.12, 0.66), (0.10, 0.16, 0.11), rot=(0, 0, -0.4))
    
    # Sports Water Bottle in holster on right hip
    # Holster cage
    cylinder("BottleHolster", "CchCapDark", (0.29, -0.14, 0.64), 0.065, 0.14, segments=8)
    
    # Iridescent / Cyan-Magenta Sports Bottle
    cylinder("BottleLower", "CchBottleMagenta", (0.29, -0.14, 0.66), 0.055, 0.12, segments=8)
    cylinder("BottleUpper", "CchBottleCyan", (0.29, -0.14, 0.76), 0.052, 0.10, segments=8)
    cylinder("BottleNeck", "CchBottleCap", (0.29, -0.14, 0.83), 0.035, 0.04, segments=8)
    cylinder("BottleSpout", "CchOrangeAccent", (0.29, -0.14, 0.86), 0.018, 0.025, segments=6)


# -------------------------------------------------------------
# 4. SCULPTED MUSCULAR ARMS, WRISTBANDS & HANDS
# -------------------------------------------------------------

def build_muscular_arms():
    # --- RIGHT ARM (Resting on Dumbbell Staff) ---
    r_sh = (0.35, -0.02, 1.14)
    # Broad muscular deltoid
    icosphere("Deltoid_R", "CchSkin", r_sh, 0.13, scale=(1.1, 1.0, 1.1), subdivisions=1)
    # Muscular Bicep / Tricep Upper Arm
    tube("Bicep_R", "CchSkin", r_sh, (0.47, -0.06, 0.94), 0.105, segments=8)
    icosphere("BicepPeak_R", "CchSkin", (0.43, -0.08, 1.02), 0.095, scale=(1.1, 0.9, 1.0), subdivisions=1)
    # Elbow
    icosphere("Elbow_R", "CchSkin", (0.47, -0.06, 0.94), 0.085, subdivisions=1)
    # Muscular Forearm extending forward/down
    tube("Forearm_R", "CchSkin", (0.47, -0.06, 0.94), (0.49, -0.22, 0.86), 0.088, segments=8)
    # Athletic Sweatband on Wrist
    cylinder("Wristband_R", "CchWristbandNavy", (0.49, -0.22, 0.86), 0.086, 0.05, segments=8)
    box("WristGem_R", "CchBadgePurple", (0.52, -0.22, 0.86), (0.025, 0.035, 0.035), rot=(0, 0, 0.78))
    # Right Hand grasping dumbbell handle
    icosphere("Hand_R", "CchSkin", (0.50, -0.26, 0.84), 0.075, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_R", "CchSkin", (0.46, -0.24, 0.85), (0.03, 0.04, 0.03), rot=(0.2, 0.2, 0.3))
    box("Fingers_R", "CchSkin", (0.51, -0.27, 0.83), (0.068, 0.04, 0.068), rot=(0, 0.2, 0.2))

    # --- LEFT ARM (Holding Workout Water Bottle / Athletic Pose) ---
    l_sh = (-0.35, -0.02, 1.14)
    # Muscular Deltoid
    icosphere("Deltoid_L", "CchSkin", l_sh, 0.13, scale=(1.1, 1.0, 1.1), subdivisions=1)
    # Muscular Upper Arm
    tube("Bicep_L", "CchSkin", l_sh, (-0.46, -0.08, 0.95), 0.105, segments=8)
    icosphere("BicepPeak_L", "CchSkin", (-0.42, -0.10, 1.03), 0.095, scale=(1.1, 0.9, 1.0), subdivisions=1)
    # Elbow
    icosphere("Elbow_L", "CchSkin", (-0.46, -0.08, 0.95), 0.085, subdivisions=1)
    # Forearm bent forward/inward to athletic waist/chest
    tube("Forearm_L", "CchSkin", (-0.46, -0.08, 0.95), (-0.30, -0.26, 0.88), 0.088, segments=8)
    # Wristband
    cylinder("Wristband_L", "CchWristbandNavy", (-0.30, -0.26, 0.88), 0.086, 0.05, segments=8)
    box("WristGem_L", "CchBadgePurple", (-0.32, -0.28, 0.88), (0.025, 0.035, 0.035), rot=(0, 0, 0.78))
    # Left Hand firmly holding second hydration bottle
    icosphere("Hand_L", "CchSkin", (-0.26, -0.32, 0.88), 0.075, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_L", "CchSkin", (-0.22, -0.30, 0.90), (0.03, 0.04, 0.03), rot=(0.2, -0.2, -0.3))
    box("Fingers_L", "CchSkin", (-0.28, -0.34, 0.87), (0.068, 0.04, 0.068), rot=(0, -0.2, -0.2))

    # Handheld Sports Water Bottle (held in Left Hand)
    h_bot = (-0.24, -0.34, 0.88)
    cylinder("HandBottleLower", "CchBottleMagenta", (h_bot[0], h_bot[1], h_bot[2] - 0.04), 0.046, 0.10, segments=8, rot=(0.2, 0.2, 0))
    cylinder("HandBottleUpper", "CchBottleCyan", (h_bot[0], h_bot[1], h_bot[2] + 0.05), 0.044, 0.09, segments=8, rot=(0.2, 0.2, 0))
    cylinder("HandBottleCap", "CchBottleCap", (h_bot[0], h_bot[1], h_bot[2] + 0.11), 0.030, 0.03, segments=8, rot=(0.2, 0.2, 0))


# -------------------------------------------------------------
# 5. HEAD, HAIR, ATHLETIC SUNGLASSES & BACKWARDS CAP
# -------------------------------------------------------------

def build_head_and_cap():
    # Athletic Neck
    cylinder("Neck", "CchSkin", (0.0, -0.01, 1.28), 0.105, 0.12, segments=8)
    
    # Sculpted low-poly head
    icosphere("HeadBase", "CchSkin", (0.0, 0.0, 1.48), 0.23, scale=(1.12, 1.0, 1.04), subdivisions=2)
    # Strong Jaw & Chin
    box("JawChin", "CchSkin", (0.0, -0.16, 1.34), (0.14, 0.10, 0.08))
    # Defined Faceted Nose
    box("NoseBridge", "CchNose", (0.0, -0.225, 1.46), (0.045, 0.05, 0.07), rot=(0.2, 0, 0))
    icosphere("NoseTip", "CchNose", (0.0, -0.245, 1.43), 0.038, scale=(1.1, 0.9, 0.9), subdivisions=1)
    
    # Energetic Athletic Cheeks & Eyes
    for s in (-1, 1):
        icosphere(f"Cheek_{s}", "CchCheek", (s * 0.12, -0.18, 1.41), 0.055, scale=(1.0, 0.6, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "CchSkin", (s * 0.25, 0.01, 1.47), 0.045, scale=(0.5, 0.9, 1.2), subdivisions=1)
        # Eyes
        box(f"EyeWhite_{s}", "CchEyeWhite", (s * 0.09, -0.205, 1.51), (0.055, 0.02, 0.035))
        box(f"EyeIris_{s}", "CchEyeBlue", (s * 0.09, -0.218, 1.51), (0.032, 0.01, 0.032))
        box(f"EyePupil_{s}", "CchPupil", (s * 0.09, -0.225, 1.51), (0.016, 0.008, 0.016))
        # Athletic Eyebrows
        box(f"Eyebrow_{s}", "CchHairBrown", (s * 0.095, -0.21, 1.56), (0.075, 0.03, 0.025), rot=(0, 0, s * 0.12))

    # Bluetooth sports earbud on left ear
    cylinder("BluetoothEarbud", "CchHeadset", (-0.265, -0.04, 1.46), 0.022, 0.02, segments=6, rot=(0, math.pi/2, 0))
    tube("EarbudMic", "CchHeadset", (-0.265, -0.04, 1.46), (-0.24, -0.12, 1.42), 0.006, segments=4)

    # Confident Athletic Tooth Smile
    box("MouthCavity", "CchLip", (0.0, -0.22, 1.34), (0.10, 0.02, 0.035))
    box("TeethUpper", "CchTeeth", (0.0, -0.226, 1.352), (0.085, 0.015, 0.015))

    # Short Athletic Beard & Mustache
    icosphere("BeardChin", "CchHairBrown", (0.0, -0.15, 1.30), 0.15, scale=(1.0, 0.95, 0.65), subdivisions=1)
    for s in (-1, 1):
        icosphere(f"BeardJaw_{s}", "CchHairBrown", (s * 0.15, -0.06, 1.36), 0.08, scale=(0.7, 1.2, 1.4), subdivisions=1)
        tube(f"Mustache_{s}", "CchHairBrown", (0.0, -0.238, 1.39), (s * 0.10, -0.21, 1.37), 0.028, segments=5)

    # --- WRAPAROUND SPORTS SUNGLASSES / VISOR ---
    v_z = 1.51
    # Sleek dark sports frame
    box("VisorBridge", "CchVisorFrame", (0.0, -0.235, v_z), (0.045, 0.015, 0.015))
    for s in (-1, 1):
        box(f"VisorFrame_{s}", "CchVisorFrame", (s * 0.095, -0.235, v_z), (0.10, 0.02, 0.055), rot=(0, 0, s * 0.05))
        box(f"VisorLens_{s}", "CchVisorLens", (s * 0.095, -0.245, v_z), (0.088, 0.01, 0.044), rot=(0, 0, s * 0.05))
        tube(f"VisorTemple_{s}", "CchVisorFrame", (s * 0.14, -0.23, v_z), (s * 0.235, -0.04, v_z), 0.008, segments=4)

    # --- BASEBALL CAP (WORN BACKWARDS) ---
    cap_z = 1.62
    # Cap crown
    icosphere("CapCrown", "CchCapNavy", (0.0, 0.0, cap_z), 0.265, scale=(1.05, 1.10, 0.72), subdivisions=1)
    # Orange Sweatband around forehead
    cylinder("CapSweatband", "CchSweatbandOrange", (0.0, 0.0, cap_z - 0.04), 0.272, 0.04, segments=12)
    # Top Cap Button
    cylinder("CapTopButton", "CchOrangeAccent", (0.0, 0.0, cap_z + 0.19), 0.025, 0.015, segments=8)
    # Backwards pointing visor / bill
    box("CapRearVisor", "CchCapVisor", (0.0, 0.32, cap_z - 0.03), (0.28, 0.16, 0.02), rot=(-0.16, 0, 0))
    # Back adjustment strap & buckle
    box("CapStrap", "CchCapDark", (0.0, 0.25, cap_z - 0.02), (0.12, 0.03, 0.03))


# -------------------------------------------------------------
# 6. DUMBBELL SCEPTER WITH WEIGHT PLATES, GEARS & DIE
# -------------------------------------------------------------

def build_dumbbell_scepter():
    sx = 0.50
    sy = -0.26
    top_z = 0.86
    
    # 1. Heavy Base Barbell Bumper Plate at ground
    cylinder("BarbellBaseWeight", "CchBarbellBlack", (sx, sy, 0.035), 0.08, 0.05, segments=10)
    cylinder("BaseInnerCollar", "CchScepterBrass", (sx, sy, 0.075), 0.045, 0.03, segments=8)
    
    # 2. Barbell Shaft (Yellow & Black Textured Measurement Bar)
    tube("BarbellShaft", "CchYellowBar", (sx, sy, 0.08), (sx, sy, top_z), 0.032, segments=6)
    # Knurled grip rings / graduation ticks
    for i in range(10):
        tz = 0.15 + i * 0.065
        cylinder(f"KnurlRing_{i}", "CchBarbellBlack", (sx, sy, tz), 0.035, 0.015, segments=8)

    # 3. Horizontal Barbell Crossbar at Top
    bar_z = top_z + 0.10
    cylinder("BarbellTopCollar", "CchScepterBrass", (sx, sy, top_z + 0.02), 0.045, 0.04, segments=8)
    # Horizontal steel bar
    tube("DumbbellBar", "CchBarbellBlack", (sx - 0.18, sy, bar_z), (sx + 0.18, sy, bar_z), 0.020, segments=6)

    # 4. Heavy Barbell Weight Plates on Left & Right Ends
    for s, wx in [("L", sx - 0.15), ("R", sx + 0.15)]:
        # Heavy outer bumper plate
        cylinder(f"PlateOuter_{s}", "CchBarbellBlack", (wx, sy, bar_z), 0.085, 0.028, segments=12, rot=(0, math.pi/2, 0))
        # Inner smaller weight plate
        cylinder(f"PlateInner_{s}", "CchBarbellBlack", (wx + (0.025 if s=="L" else -0.025), sy, bar_z), 0.072, 0.022, segments=10, rot=(0, math.pi/2, 0))
        # Brass spin-lock collar
        cylinder(f"SpinLock_{s}", "CchScepterBrass", (wx + (-0.025 if s=="L" else 0.025), sy, bar_z), 0.035, 0.020, segments=8, rot=(0, math.pi/2, 0))

    # 5. Center Interlocking Steampunk Clockwork Gears
    cylinder("CenterGearHub", "CchScepterGold", (sx, sy - 0.01, bar_z), 0.072, 0.022, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("CenterGearPin", "CchScepterBrass", (sx, sy - 0.022, bar_z), 0.035, 0.012, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.082 * math.cos(ang)
        tz = bar_z + 0.082 * math.sin(ang)
        box(f"GearTooth_{i}", "CchScepterGold", (tx, sy - 0.01, tz), (0.018, 0.022, 0.018), rot=(0, -ang, 0))

    # 6. Polyhedral Golden 6-Sided Die at Summit
    die_z = bar_z + 0.16
    die_rot = (0.35, 0.35, 0.45)
    box("GoldenDie", "CchScepterGold", (sx, sy, die_z), (0.13, 0.13, 0.13), rot=die_rot)
    # Recessed dark pips
    box("DiePip1", "CchBarbellBlack", (sx, sy - 0.07, die_z), (0.025, 0.01, 0.025), rot=die_rot)
    box("DiePip2", "CchBarbellBlack", (sx - 0.035, sy - 0.06, die_z + 0.035), (0.02, 0.01, 0.02), rot=die_rot)
    box("DiePip3", "CchBarbellBlack", (sx + 0.035, sy - 0.06, die_z - 0.035), (0.02, 0.01, 0.02), rot=die_rot)


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
    obj.name = "Coach"
    
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
    build_legs_and_sneakers()
    build_torso_and_vest()
    build_waist_accessories()
    build_muscular_arms()
    build_head_and_cap()
    build_dumbbell_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_coach()
