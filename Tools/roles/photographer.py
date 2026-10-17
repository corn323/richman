"""
Photographer (攝影師) 3D Model Generator for Blender.
Accurately implements character/potographer.jpg:
- Geometric low-poly style with crisp faceted surfaces
- Khaki safari bucket hat (幾何漁夫帽) with hatband & mounted protective goggles with cyan lenses
- Sports sunglasses with dark frames & cyan tinted lenses
- Neat chin-strap beard, mustache, confident smile showing teeth
- Olive-green multi-pocket photographer vest with orange webbing straps & D-rings
- Dark grey/green utility shirt with rolled-up sleeves
- Detailed DSLR camera hanging from neck strap on chest (lens, viewfinder, dials)
- Right hand grips/rests on Polyhedral Lens Scepter (多面體鏡頭權杖):
  * Rainbow chromatic spectrum bar (光譜色階柱) with calibration ticks
  * Bottom faceted optical prism tip
  * Golden camera lens aperture & balance scale weights
  * Top iridescent multifaceted chromatic prism cube
- Heavy camera backpack with mounted 3-legged professional folding tripod
- Belt with lens pouch and hanging brass key
- Khaki cargo pants with side bellows pockets and trekking hiking boots
- Pivot at (0, 0, 0) at ground center, height ~ 1.83 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
- Exports to assets/models/photographer.glb
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
        if o.name.startswith("Photographer") or o.name.startswith("part_") or o.name == "Photographer":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Ptg") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Ptg"):
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
    get_or_create_mat("PtgSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("PtgCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("PtgNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("PtgLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("PtgTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("PtgEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("PtgHairBrown", (0.30, 0.20, 0.12), rough=0.75)
    
    # Sunglasses
    get_or_create_mat("PtgShadesDark", (0.08, 0.09, 0.10), rough=0.4)
    get_or_create_mat("PtgShadesCyan", (0.16, 0.65, 0.85), rough=0.1, trans=0.78)

    # Safari Bucket Hat & Goggles
    get_or_create_mat("PtgHatKhaki", (0.58, 0.54, 0.38), rough=0.75)
    get_or_create_mat("PtgHatBand", (0.22, 0.25, 0.28), rough=0.7)
    get_or_create_mat("PtgGoggleFrame", (0.18, 0.20, 0.24), rough=0.5)
    get_or_create_mat("PtgGoggleLens", (0.15, 0.82, 0.92), rough=0.15, trans=0.6)
    
    # Clothing (Safari vest, shirt, cargo pants)
    get_or_create_mat("PtgVestOlive", (0.28, 0.38, 0.24), rough=0.7)
    get_or_create_mat("PtgVestPocket", (0.22, 0.30, 0.18), rough=0.7)
    get_or_create_mat("PtgWebbingOrange", (0.92, 0.46, 0.12), rough=0.55)
    get_or_create_mat("PtgShirtDark", (0.15, 0.20, 0.22), rough=0.8)
    get_or_create_mat("PtgCargoKhaki", (0.48, 0.44, 0.32), rough=0.75)
    get_or_create_mat("PtgGloveDark", (0.12, 0.13, 0.15), rough=0.7)
    get_or_create_mat("PtgGloveTrim", (0.35, 0.22, 0.14), rough=0.6)
    
    # Boots
    get_or_create_mat("PtgBootLeather", (0.32, 0.22, 0.14), rough=0.7)
    get_or_create_mat("PtgBootDark", (0.16, 0.14, 0.12), rough=0.8)
    get_or_create_mat("PtgBootSole", (0.08, 0.08, 0.08), rough=0.9)
    
    # Camera Equipment
    get_or_create_mat("PtgCameraBody", (0.12, 0.13, 0.15), rough=0.45)
    get_or_create_mat("PtgCameraLens", (0.06, 0.06, 0.07), metal=0.4, rough=0.3)
    get_or_create_mat("PtgLensGlass", (0.18, 0.85, 0.95), rough=0.1, emission=(0.18, 0.85, 0.95))
    get_or_create_mat("PtgCameraStrap", (0.10, 0.10, 0.12), rough=0.7)
    
    # Backpack & Heavy Tripod
    get_or_create_mat("PtgPackBrown", (0.38, 0.25, 0.15), rough=0.7)
    get_or_create_mat("PtgPackDark", (0.18, 0.16, 0.14), rough=0.75)
    get_or_create_mat("PtgTripodAlloy", (0.15, 0.16, 0.18), metal=0.85, rough=0.3)
    get_or_create_mat("PtgTripodKnob", (0.90, 0.48, 0.10), rough=0.5)
    get_or_create_mat("PtgBrassKey", (0.88, 0.70, 0.18), metal=0.9, rough=0.25)
    get_or_create_mat("PtgSteel", (0.70, 0.72, 0.75), metal=0.88, rough=0.25)
    
    # Optical Prism Scepter & Rainbow Spectrum
    get_or_create_mat("PtgScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("PtgScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("PtgPrismPoint", (0.45, 0.20, 0.65), metal=0.3, rough=0.2)
    get_or_create_mat("PtgSpecRed", (0.95, 0.18, 0.18), rough=0.3)
    get_or_create_mat("PtgSpecOrange", (0.98, 0.48, 0.08), rough=0.3)
    get_or_create_mat("PtgSpecYellow", (0.98, 0.88, 0.10), rough=0.3)
    get_or_create_mat("PtgSpecGreen", (0.18, 0.85, 0.28), rough=0.3)
    get_or_create_mat("PtgSpecCyan", (0.15, 0.88, 0.98), rough=0.3)
    get_or_create_mat("PtgSpecBlue", (0.12, 0.35, 0.95), rough=0.3)
    get_or_create_mat("PtgSpecPurple", (0.65, 0.15, 0.85), rough=0.3)
    get_or_create_mat("PtgSpecMagenta", (0.88, 0.15, 0.65), rough=0.3)

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
# 1. LEGS, CARGO PANTS & TREKKING BOOTS
# -------------------------------------------------------------

def build_legs_and_boots():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Cargo pants thigh
        cylinder(f"LegThigh_{side}", "PtgCargoKhaki", (sx, 0.0, 0.44), 0.135, 0.32, segments=8)
        # Knee cuff / tuck fold
        cylinder(f"Knee_{side}", "PtgCargoKhaki", (sx, 0.01, 0.28), 0.125, 0.12, segments=8)
        # Bellows cargo pocket on side of leg
        px = sx + (0.135 if sx > 0 else -0.135)
        box(f"CargoPocket_{side}", "PtgCargoKhaki", (px, 0.0, 0.42), (0.05, 0.14, 0.16), rot=(0, 0.1 if sx > 0 else -0.1, 0))
        box(f"CargoFlap_{side}", "PtgHatKhaki", (px, 0.0, 0.50), (0.055, 0.145, 0.04), rot=(0, 0.1 if sx > 0 else -0.1, 0))

        # Boot upper shaft
        cylinder(f"BootShaft_{side}", "PtgBootLeather", (sx, 0.0, 0.17), 0.115, 0.16, segments=8)
        
        # Trekking Boot Foot
        box(f"BootFoot_{side}", "PtgBootLeather", (sx, -0.06, 0.08), (0.21, 0.30, 0.14))
        # Boot Toe Guard
        box(f"BootToe_{side}", "PtgBootDark", (sx, -0.18, 0.07), (0.19, 0.10, 0.12))
        # Boot Sole with deep tread
        box(f"BootSole_{side}", "PtgBootSole", (sx, -0.06, 0.02), (0.23, 0.34, 0.04))
        box(f"BootHeel_{side}", "PtgBootSole", (sx, 0.05, 0.03), (0.23, 0.12, 0.06))
        # Ankle Laces / Buckle
        box(f"BootLace_{side}", "PtgBootDark", (sx, -0.10, 0.13), (0.14, 0.12, 0.03))


# -------------------------------------------------------------
# 2. TORSO, SHIRT, SAFARI VEST & CHEST DSLR CAMERA
# -------------------------------------------------------------

def build_torso_and_vest():
    # Pelvis / Lower vest skirt
    cylinder("Pelvis", "PtgCargoKhaki", (0.0, 0.0, 0.60), 0.26, 0.16, segments=10)
    
    # Leather utility belt
    cylinder("UtilityBelt", "PtgBootLeather", (0.0, 0.0, 0.67), 0.28, 0.08, segments=12)
    box("BeltBuckle", "PtgSteel", (0.0, -0.285, 0.67), (0.12, 0.03, 0.08))
    
    # Torso (Utility Shirt + Safari Vest)
    cylinder("VestTorso", "PtgVestOlive", (0.0, -0.01, 0.88), 0.295, 0.36, segments=10, r2=0.275)
    cylinder("VestChest", "PtgVestOlive", (0.0, -0.02, 1.10), 0.28, 0.24, segments=10, r2=0.25)
    
    # Inner dark shirt collar & chest visible down the center
    box("InnerShirt", "PtgShirtDark", (0.0, -0.23, 0.98), (0.14, 0.08, 0.38))
    
    # Left and Right Safari Vest panels
    box("VestPanel_L", "PtgVestOlive", (-0.13, -0.26, 0.98), (0.11, 0.06, 0.36), rot=(0, 0, -0.08))
    box("VestPanel_R", "PtgVestOlive", (0.13, -0.26, 0.98), (0.11, 0.06, 0.36), rot=(0, 0, 0.08))
    
    # Vest Webbing Straps (Iconic bright orange straps with D-rings)
    tube("Webbing_L", "PtgWebbingOrange", (-0.14, -0.27, 1.18), (-0.14, -0.28, 0.74), 0.018, segments=4)
    tube("Webbing_R", "PtgWebbingOrange", (0.14, -0.27, 1.18), (0.14, -0.28, 0.74), 0.018, segments=4)
    torus("DRing_L", "PtgSteel", (-0.14, -0.29, 1.02), 0.022, 0.005, rot=(math.pi/2, 0, 0))
    torus("DRing_R", "PtgSteel", (0.14, -0.29, 1.02), 0.022, 0.005, rot=(math.pi/2, 0, 0))

    # Upper Chest Flap Pockets
    box("ChestPocket_L", "PtgVestPocket", (-0.18, -0.26, 1.08), (0.10, 0.04, 0.11))
    box("ChestFlap_L", "PtgVestOlive", (-0.18, -0.27, 1.14), (0.11, 0.045, 0.03))
    box("ChestPocket_R", "PtgVestPocket", (0.18, -0.26, 1.08), (0.10, 0.04, 0.11))
    box("ChestFlap_R", "PtgVestOlive", (0.18, -0.27, 1.14), (0.11, 0.045, 0.03))

    # Lower Bellows Pockets
    box("LowerPocket_L", "PtgVestPocket", (-0.18, -0.27, 0.84), (0.11, 0.04, 0.13))
    box("LowerFlap_L", "PtgVestOlive", (-0.18, -0.28, 0.91), (0.12, 0.045, 0.035))
    box("LowerPocket_R", "PtgVestPocket", (0.18, -0.27, 0.84), (0.11, 0.04, 0.13))
    box("LowerFlap_R", "PtgVestOlive", (0.18, -0.28, 0.91), (0.12, 0.045, 0.035))

    # --- CHEST MOUNTED DSLR CAMERA (胸前懸掛單眼相機) ---
    # Camera neck strap looping around collar
    tube("CamStrap_L", "PtgCameraStrap", (-0.11, -0.16, 1.25), (-0.05, -0.32, 1.02), 0.015, segments=4)
    tube("CamStrap_R", "PtgCameraStrap", (0.11, -0.16, 1.25), (0.05, -0.32, 1.02), 0.015, segments=4)
    
    # Camera Body hanging on center chest (z = 0.96)
    cx, cy, cz = 0.0, -0.34, 0.96
    box("CameraBody", "PtgCameraBody", (cx, cy, cz), (0.18, 0.08, 0.12))
    # Right-side ergonomic hand grip
    box("CameraGrip", "PtgGloveDark", (cx + 0.07, cy - 0.02, cz), (0.045, 0.06, 0.11))
    # Viewfinder hump & Hot-shoe flash mount
    box("CameraViewfinder", "PtgCameraBody", (cx, cy, cz + 0.075), (0.06, 0.07, 0.04))
    box("CameraHotshoe", "PtgSteel", (cx, cy, cz + 0.098), (0.035, 0.035, 0.012))
    # Top mode dial & shutter button
    cylinder("CameraDial", "PtgSteel", (cx - 0.06, cy, cz + 0.065), 0.016, 0.015, segments=8)
    cylinder("ShutterBtn", "PtgTripodKnob", (cx + 0.065, cy - 0.02, cz + 0.065), 0.012, 0.015, segments=6)
    
    # Prominent Circular Camera Lens with cyan glass reflection
    cylinder("CameraLensBarrel", "PtgCameraLens", (cx, cy - 0.07, cz), 0.052, 0.08, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("CameraLensFrontRing", "PtgCameraBody", (cx, cy - 0.112, cz), 0.055, 0.015, segments=12, rot=(math.pi/2, 0, 0))
    # Cyan optical glass element inside lens
    cylinder("CameraLensGlass", "PtgLensGlass", (cx, cy - 0.116, cz), 0.042, 0.008, segments=10, rot=(math.pi/2, 0, 0))


# -------------------------------------------------------------
# 3. BELT POUCHES, HANGING BRASS KEY & BACKPACK TRIPOD
# -------------------------------------------------------------

def build_backpack_and_accessories():
    # Lens Pouch on right hip (x = -0.27, y = -0.06, z = 0.62)
    cylinder("LensPouch", "PtgCameraBody", (-0.28, -0.06, 0.62), 0.07, 0.18, segments=8)
    box("LensPouchCap", "PtgBootLeather", (-0.28, -0.06, 0.72), (0.15, 0.15, 0.04))
    
    # Hanging Brass Key on right hip (金色復古鑰匙)
    tube("KeyRing", "PtgBrassKey", (-0.18, -0.26, 0.65), (-0.18, -0.27, 0.62), 0.012, segments=6)
    torus("KeyBow", "PtgBrassKey", (-0.18, -0.27, 0.60), 0.020, 0.005, rot=(0, math.pi/2, 0))
    cylinder("KeyBitShaft", "PtgBrassKey", (-0.18, -0.27, 0.54), 0.006, 0.10, segments=6)
    box("KeyBit1", "PtgBrassKey", (-0.18, -0.26, 0.51), (0.006, 0.02, 0.02))
    box("KeyBit2", "PtgBrassKey", (-0.18, -0.26, 0.54), (0.006, 0.015, 0.015))

    # Accessory pouch on left hip
    box("BeltPouch_L", "PtgVestPocket", (0.27, -0.04, 0.63), (0.10, 0.15, 0.14), rot=(0, 0, -0.2))

    # Heavy Camera Backpack (背部專業攝影背包)
    bx, by, bz = 0.0, 0.28, 0.96
    box("BackpackMain", "PtgPackBrown", (bx, by, bz), (0.38, 0.24, 0.48))
    box("BackpackTopPocket", "PtgPackDark", (bx, by, bz + 0.20), (0.36, 0.22, 0.10))
    box("BackpackSide_L", "PtgPackDark", (bx - 0.20, by, bz - 0.06), (0.07, 0.18, 0.26))
    box("BackpackSide_R", "PtgPackDark", (bx + 0.20, by, bz - 0.06), (0.07, 0.18, 0.26))

    # --- PROFESSIONAL 3-LEGGED FOLDED CAMERA TRIPOD (重型折疊三腳架) ---
    # Mounted on the rear/side of the backpack (tx = 0.18, ty = 0.40, tz from 0.55 to 1.35)
    tx, ty = 0.18, 0.40
    
    # 3 Folded Telescoping Legs
    for idx, (lx, ly) in enumerate([(tx - 0.04, ty - 0.03), (tx + 0.04, ty - 0.03), (tx, ty + 0.04)]):
        # Upper Leg Section
        tube(f"TripodLegUpper_{idx}", "PtgTripodAlloy", (lx, ly, 1.20), (lx, ly, 0.88), 0.022, segments=6)
        # Leg lock clamp ring
        cylinder(f"TripodClamp1_{idx}", "PtgTripodKnob", (lx, ly, 0.88), 0.025, 0.02, segments=6)
        # Middle Leg Section
        tube(f"TripodLegMid_{idx}", "PtgTripodAlloy", (lx, ly, 0.88), (lx, ly, 0.68), 0.017, segments=6)
        cylinder(f"TripodClamp2_{idx}", "PtgTripodKnob", (lx, ly, 0.68), 0.020, 0.02, segments=6)
        # Lower Leg Section & Rubber Foot
        tube(f"TripodLegLow_{idx}", "PtgTripodAlloy", (lx, ly, 0.68), (lx, ly, 0.52), 0.013, segments=6)
        cylinder(f"TripodFoot_{idx}", "PtgBootSole", (lx, ly, 0.51), 0.016, 0.02, segments=6)

    # Tripod Pan-Tilt Fluid Head & Quick Release Plate
    cylinder("TripodHeadBase", "PtgTripodAlloy", (tx, ty, 1.24), 0.05, 0.06, segments=8)
    box("TripodQRPlate", "PtgTripodAlloy", (tx, ty, 1.28), (0.08, 0.08, 0.025))
    # Pan handle knob extending from tripod head
    tube("TripodPanHandle", "PtgTripodKnob", (tx, ty, 1.24), (tx + 0.08, ty + 0.08, 1.27), 0.010, segments=4)


# -------------------------------------------------------------
# 4. ARMS & GLOVED HANDS
# -------------------------------------------------------------

def build_arms_and_hands():
    # --- RIGHT ARM (Grips the Polyhedral Lens Scepter) ---
    # Shoulder / Rolled sleeve
    cylinder("Sleeve_R", "PtgShirtDark", (-0.31, -0.02, 1.18), 0.11, 0.16, segments=8, rot=(0, 0.35, 0))
    cylinder("SleeveRoll_R", "PtgVestOlive", (-0.36, -0.05, 1.08), 0.115, 0.06, segments=8, rot=(0, 0.35, 0))
    # Forearm extending downward to rest on scepter head
    tube("Forearm_R", "PtgSkin", (-0.36, -0.05, 1.08), (-0.46, -0.22, 0.88), 0.085, segments=8)
    # Fingerless glove cuff
    cylinder("GloveCuff_R", "PtgGloveTrim", (-0.46, -0.22, 0.88), 0.082, 0.04, segments=8)
    # Gloved Hand firmly gripping the scepter
    box("Hand_R", "PtgGloveDark", (-0.47, -0.24, 0.86), (0.11, 0.12, 0.10), rot=(0.2, 0.1, -0.1))
    box("Thumb_R", "PtgSkin", (-0.44, -0.20, 0.87), (0.04, 0.06, 0.04), rot=(0.2, 0.1, -0.1))

    # --- LEFT ARM (Resting naturally near hip / vest pocket) ---
    cylinder("Sleeve_L", "PtgShirtDark", (0.31, -0.02, 1.18), 0.11, 0.16, segments=8, rot=(0, -0.35, 0))
    cylinder("SleeveRoll_L", "PtgVestOlive", (0.36, -0.05, 1.08), 0.115, 0.06, segments=8, rot=(0, -0.35, 0))
    tube("Forearm_L", "PtgSkin", (0.36, -0.05, 1.08), (0.36, -0.14, 0.86), 0.085, segments=8)
    cylinder("GloveCuff_L", "PtgGloveTrim", (0.36, -0.14, 0.86), 0.082, 0.04, segments=8)
    box("Hand_L", "PtgGloveDark", (0.36, -0.16, 0.82), (0.10, 0.11, 0.09), rot=(-0.1, 0.2, -0.2))


# -------------------------------------------------------------
# 5. HEAD, FACE, SUNGLASSES & SAFARI BUCKET HAT
# -------------------------------------------------------------

def build_head_and_hat():
    # Neck
    cylinder("Neck", "PtgSkin", (0.0, 0.0, 1.25), 0.12, 0.14, segments=8)
    
    # Head Base
    icosphere("HeadBase", "PtgSkin", (0.0, -0.02, 1.44), 0.24, scale=(0.95, 1.05, 1.08), subdivisions=1)
    
    # Cheeks & Nose
    icosphere("Cheek_L", "PtgCheek", (-0.12, -0.19, 1.41), 0.05, subdivisions=1)
    icosphere("Cheek_R", "PtgCheek", (0.12, -0.19, 1.41), 0.05, subdivisions=1)
    box("NoseBridge", "PtgSkin", (0.0, -0.22, 1.45), (0.045, 0.06, 0.09))
    box("NoseTip", "PtgNose", (0.0, -0.25, 1.42), (0.055, 0.05, 0.05))
    
    # Confident Smile & Teeth
    box("MouthCavity", "PtgLip", (0.0, -0.225, 1.34), (0.11, 0.03, 0.04))
    box("TeethUpper", "PtgTeeth", (0.0, -0.232, 1.35), (0.09, 0.02, 0.02))

    # Neat Chin Beard & Mustache (修剪整齊的落腮鬍與八字鬍)
    box("Mustache_L", "PtgHairBrown", (-0.055, -0.245, 1.38), (0.07, 0.04, 0.03), rot=(0, 0, 0.2))
    box("Mustache_R", "PtgHairBrown", (0.055, -0.245, 1.38), (0.07, 0.04, 0.03), rot=(0, 0, -0.2))
    box("MustacheCenter", "PtgHairBrown", (0.0, -0.25, 1.39), (0.04, 0.04, 0.025))
    box("BeardChin", "PtgHairBrown", (0.0, -0.20, 1.28), (0.14, 0.12, 0.09))
    box("BeardJaw_L", "PtgHairBrown", (-0.13, -0.11, 1.33), (0.07, 0.16, 0.10), rot=(0, 0, -0.3))
    box("BeardJaw_R", "PtgHairBrown", (0.13, -0.11, 1.33), (0.07, 0.16, 0.10), rot=(0, 0, 0.3))
    # Hair in back
    box("HairBack", "PtgHairBrown", (0.0, 0.14, 1.37), (0.28, 0.12, 0.14))

    # --- SPORTS SUNGLASSES (戶外運動太陽眼鏡) ---
    box("ShadesBridge", "PtgShadesDark", (0.0, -0.245, 1.48), (0.05, 0.02, 0.015))
    box("ShadesTop", "PtgShadesDark", (0.0, -0.245, 1.51), (0.25, 0.02, 0.015))
    for side, gx in [("L", -0.085), ("R", 0.085)]:
        box(f"ShadesLens_{side}", "PtgShadesCyan", (gx, -0.243, 1.48), (0.095, 0.008, 0.055))
        tube(f"ShadesTemple_{side}", "PtgShadesDark", (gx + (-0.05 if side == "L" else 0.05), -0.23, 1.49), (gx + (-0.12 if side == "L" else 0.12), -0.04, 1.48), 0.008, segments=4)

    # --- SAFARI BUCKET HAT (幾何戶外漁夫帽) ---
    # Hat Crown Dome
    cylinder("HatCrownLower", "PtgHatKhaki", (0.0, -0.01, 1.62), 0.25, 0.16, segments=10, r2=0.22)
    cylinder("HatCrownTop", "PtgHatKhaki", (0.0, -0.01, 1.72), 0.21, 0.06, segments=10, r2=0.19)
    # Dark Hatband
    cylinder("HatBand", "PtgHatBand", (0.0, -0.01, 1.57), 0.258, 0.06, segments=10)
    
    # Sloping Wide Brim (漁夫帽下垂帽簷)
    # Truncated cone flared downward and outward
    cylinder("HatBrim", "PtgHatKhaki", (0.0, -0.01, 1.52), 0.36, 0.05, segments=12, r2=0.27)

    # --- MOUNTED PROTECTIVE GOGGLES (推至帽簷的風鏡) ---
    # Dark goggle strap wrapping around front of hatband
    cylinder("GoggleStrap", "PtgGoggleFrame", (0.0, -0.01, 1.63), 0.265, 0.04, segments=10)
    # Two Large Hexagonal Goggle Lenses on front of hat
    for side, gx in [("L", -0.09), ("R", 0.09)]:
        cylinder(f"GoggleBezel_{side}", "PtgGoggleFrame", (gx, -0.24, 1.65), 0.065, 0.03, segments=6, rot=(math.pi/2 - 0.2, 0, 0))
        cylinder(f"GoggleLens_{side}", "PtgGoggleLens", (gx, -0.252, 1.65), 0.052, 0.015, segments=6, rot=(math.pi/2 - 0.2, 0, 0))
    # Goggle bridge connector
    box("GoggleBridge", "PtgGoggleFrame", (0.0, -0.235, 1.65), (0.04, 0.02, 0.02), rot=(-0.2, 0, 0))


# -------------------------------------------------------------
# 6. POLYHEDRAL OPTICAL PRISM SCEPTER (多面體鏡頭權杖)
# -------------------------------------------------------------

def build_optical_scepter():
    # Held in right hand, planted straight on the ground:
    # x = -0.48, y = -0.22, base at ground z = 0.0
    sx, sy = -0.48, -0.22

    # 1. Pointed Faceted Optical Prism Tip at bottom (底座光學稜鏡角錐)
    cylinder("PrismTip", "PtgPrismPoint", (sx, sy, 0.04), 0.045, 0.08, segments=6, r2=0.0)
    cylinder("PrismMount", "PtgScepterGold", (sx, sy, 0.09), 0.048, 0.03, segments=8)

    # 2. RAINBOW CHROMATIC SPECTRUM BAR (光譜色階柱)
    # 7 distinct pure spectral bands stacked vertically from z = 0.11 to z = 0.81
    spectrum_mats = [
        "PtgSpecPurple",
        "PtgSpecBlue",
        "PtgSpecCyan",
        "PtgSpecGreen",
        "PtgSpecYellow",
        "PtgSpecOrange",
        "PtgSpecRed",
    ]
    band_h = 0.10
    for idx, smat in enumerate(spectrum_mats):
        bz = 0.16 + idx * band_h
        # Octagonal colored shaft segment
        cylinder(f"SpectrumBand_{idx}", smat, (sx, sy, bz), 0.032, band_h, segments=8)
        # Calibration / Focal-length graduation tick on front
        box(f"SpecTick_{idx}", "PtgCameraBody", (sx, sy - 0.033, bz), (0.016, 0.005, 0.012))

    # Shaft top metallic collar
    cylinder("ShaftTopCollar", "PtgScepterBrass", (sx, sy, 0.88), 0.045, 0.05, segments=8)

    # 3. Golden Camera Lens Aperture / Balance Scale (光圈鏡頭與天平支架)
    gear_z = 0.98
    # Center camera aperture iris disk
    cylinder("ApertureHub", "PtgScepterGold", (sx, sy, gear_z), 0.075, 0.025, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("ApertureIris", "PtgCameraBody", (sx, sy - 0.015, gear_z), 0.045, 0.010, segments=8, rot=(math.pi/2, 0, 0))
    cylinder("ApertureGlass", "PtgLensGlass", (sx, sy - 0.021, gear_z), 0.030, 0.006, segments=8, rot=(math.pi/2, 0, 0))

    # Horizontal Balance Scale Beam (天平橫樑)
    box("ScaleBeam", "PtgScepterGold", (sx, sy, gear_z + 0.06), (0.24, 0.025, 0.02))

    # Left & Right Hanging Balance Lens Weights (雙側天平鏡頭砝碼)
    for side, bx in [("L", -0.10), ("R", 0.10)]:
        wx = sx + bx
        # Hanging chain/rod
        tube(f"ScaleRod_{side}", "PtgScepterBrass", (wx, sy, gear_z + 0.06), (wx, sy, gear_z - 0.02), 0.006, segments=4)
        # Conical Lens Weight / Pan (圓錐鏡頭砝碼)
        cylinder(f"ScalePan_{side}", "PtgScepterGold", (wx, sy, gear_z - 0.03), 0.038, 0.04, segments=8, r2=0.01)
        cylinder(f"ScaleWeightLens_{side}", "PtgCameraBody", (wx, sy, gear_z - 0.055), 0.026, 0.02, segments=6)

    # 4. Interlocking Brass Gears under the Prism
    cylinder("GearWheel_1", "PtgScepterBrass", (sx + 0.04, sy + 0.01, gear_z - 0.05), 0.045, 0.016, segments=8, rot=(math.pi/2, 0, 0))

    # 5. IRIDESCENT CHROMATIC PRISM CUBE (多面體寶石稜鏡權杖頭)
    prism_z = 1.15
    prism_rot = (0.35, 0.35, 0.45)
    box("PrismCube", "PtgPrismPoint", (sx, sy, prism_z), (0.13, 0.13, 0.13), rot=prism_rot)
    # Iridescent faceted highlight planes on the prism
    box("PrismFacet1", "PtgSpecCyan", (sx, sy - 0.05, prism_z + 0.04), (0.07, 0.01, 0.07), rot=prism_rot)
    box("PrismFacet2", "PtgSpecMagenta", (sx - 0.04, sy - 0.05, prism_z - 0.03), (0.06, 0.01, 0.06), rot=prism_rot)
    box("PrismFacet3", "PtgSpecYellow", (sx + 0.04, sy - 0.05, prism_z), (0.06, 0.01, 0.06), rot=prism_rot)


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
    obj.name = "Photographer"
    
    # Clean up geometry & normals (do NOT run remove_doubles to preserve separate manifold shells)
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
    print(f"Photographer finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "photographer.glb")
    
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


def generate_photographer():
    reset_scene()
    setup_materials()
    build_legs_and_boots()
    build_torso_and_vest()
    build_backpack_and_accessories()
    build_arms_and_hands()
    build_head_and_hat()
    build_optical_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_photographer()
