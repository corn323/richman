"""
Artist (藝術家) 3D Model Generator for Blender.
Accurately implements character/artist.jpg based on Master Goal specifications:
- 3D Stylized Low-Poly Character (3.5-head tall figure)
- Crisp faceted polygonal geometry, FLAT SHADING enabled
- Tilted dark beret hat with metallic badge and paintbrushes tucked behind the band
- Round dark-framed spectacles with clear reflective glass lenses
- Stylized voluminous wavy/curly brown hair framing the face, creative cheerful smile
- Dark paint-splattered artist apron with pockets over dark charcoal turtleneck sweater
- Bright multi-colored paint splatters across apron, sleeves, and sneakers
- Canvas messenger shoulder bag draped across body
- Left hand holds authentic wooden oval paint palette with colorful oil paint dollops & thumb hole
- Right hand rests on Artistic Scepter (多面體藝術權杖頭):
  * Bottom faceted crystal optical prism tip
  * Rainbow chromatic spectrum bar with calibration ticks
  * Interlocking clockwork steampunk brass/golden gears
  * Golden/wooden artist paint palette crest at summit with paint gems
- Slate blue-grey rolled-cuff studio trousers and paint-splattered high-top canvas sneakers
- Pivot at (0, 0, 0) at ground center, height ~ 1.82 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
- Exports to assets/models/artist.glb
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
        if o.name.startswith("Artist") or o.name.startswith("part_") or o.name == "Artist":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Art") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Art"):
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
    get_or_create_mat("ArtSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("ArtCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("ArtNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("ArtLip", (0.48, 0.18, 0.18), rough=0.6)
    get_or_create_mat("ArtTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("ArtEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("ArtEyeGreen", (0.15, 0.65, 0.45), rough=0.2)
    get_or_create_mat("ArtPupil", (0.04, 0.04, 0.05), rough=0.2)
    get_or_create_mat("ArtHairBrown", (0.34, 0.20, 0.12), rough=0.75)
    
    # Round Glasses
    get_or_create_mat("ArtGlassFrame", (0.14, 0.16, 0.18), rough=0.4)
    get_or_create_mat("ArtGlassLens", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)

    # Tilted Beret & Paintbrushes
    get_or_create_mat("ArtBeretNavy", (0.16, 0.20, 0.28), rough=0.7)
    get_or_create_mat("ArtBeretDark", (0.10, 0.14, 0.20), rough=0.75)
    get_or_create_mat("ArtBadgeGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("ArtBadgeGreen", (0.20, 0.75, 0.35), rough=0.4)
    get_or_create_mat("ArtBrushWood", (0.48, 0.30, 0.16), rough=0.65)
    get_or_create_mat("ArtBrushSilver", (0.75, 0.77, 0.80), metal=0.9, rough=0.25)
    get_or_create_mat("ArtBrushTipRed", (0.85, 0.20, 0.20), rough=0.5)
    get_or_create_mat("ArtBrushTipCyan", (0.18, 0.70, 0.85), rough=0.5)

    # Clothing: Paint-Splattered Apron & Turtleneck Sweater
    get_or_create_mat("ArtApronDark", (0.16, 0.22, 0.28), rough=0.7)
    get_or_create_mat("ArtPocketDark", (0.12, 0.16, 0.22), rough=0.75)
    get_or_create_mat("ArtSweaterCharcoal", (0.14, 0.16, 0.20), rough=0.8)
    get_or_create_mat("ArtPantsSlate", (0.24, 0.30, 0.36), rough=0.75)
    get_or_create_mat("ArtPantsCuff", (0.20, 0.25, 0.30), rough=0.8)

    # Paint Splatter Color Blocks
    get_or_create_mat("ArtSplatterRed", (0.92, 0.20, 0.20), rough=0.4)
    get_or_create_mat("ArtSplatterYellow", (0.96, 0.85, 0.15), rough=0.4)
    get_or_create_mat("ArtSplatterCyan", (0.18, 0.82, 0.95), rough=0.4)
    get_or_create_mat("ArtSplatterGreen", (0.20, 0.80, 0.30), rough=0.4)
    get_or_create_mat("ArtSplatterPurple", (0.65, 0.20, 0.80), rough=0.4)
    get_or_create_mat("ArtSplatterOrange", (0.96, 0.50, 0.12), rough=0.4)
    get_or_create_mat("ArtSplatterWhite", (0.94, 0.94, 0.94), rough=0.4)

    # Canvas Sneakers
    get_or_create_mat("ArtSneakerDark", (0.16, 0.18, 0.22), rough=0.7)
    get_or_create_mat("ArtSneakerWhite", (0.92, 0.92, 0.90), rough=0.7)
    get_or_create_mat("ArtSneakerSole", (0.86, 0.86, 0.84), rough=0.8)

    # Messenger Canvas Bag
    get_or_create_mat("ArtBagCanvas", (0.70, 0.64, 0.52), rough=0.7)
    get_or_create_mat("ArtBagStrap", (0.58, 0.52, 0.40), rough=0.75)

    # Handheld Wooden Paint Palette
    get_or_create_mat("ArtPaletteWood", (0.68, 0.46, 0.28), rough=0.6)
    get_or_create_mat("ArtPaletteDark", (0.48, 0.30, 0.16), rough=0.65)

    # Artistic Scepter
    get_or_create_mat("ArtScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("ArtScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("ArtPrismPoint", (0.45, 0.20, 0.65), metal=0.3, rough=0.2)
    get_or_create_mat("ArtSpecRed", (0.95, 0.18, 0.18), rough=0.3)
    get_or_create_mat("ArtSpecOrange", (0.98, 0.48, 0.08), rough=0.3)
    get_or_create_mat("ArtSpecYellow", (0.98, 0.88, 0.10), rough=0.3)
    get_or_create_mat("ArtSpecGreen", (0.18, 0.85, 0.28), rough=0.3)
    get_or_create_mat("ArtSpecCyan", (0.15, 0.88, 0.98), rough=0.3)
    get_or_create_mat("ArtSpecBlue", (0.12, 0.35, 0.95), rough=0.3)
    get_or_create_mat("ArtSpecPurple", (0.65, 0.15, 0.85), rough=0.3)

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
# 1. LEGS, ROLLED STUDIO TROUSERS & CANVAS SNEAKERS
# -------------------------------------------------------------

def build_legs_and_sneakers():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Studio trousers thigh & calf
        cylinder(f"LegThigh_{side}", "ArtPantsSlate", (sx, 0.0, 0.44), 0.13, 0.32, segments=8)
        cylinder(f"LegCalf_{side}", "ArtPantsSlate", (sx, 0.01, 0.28), 0.12, 0.14, segments=8)
        # Rolled cuff at ankle
        cylinder(f"TrouserCuff_{side}", "ArtPantsCuff", (sx, 0.0, 0.18), 0.122, 0.05, segments=8)

        # High-top Canvas Sneaker Shaft
        cylinder(f"SneakerShaft_{side}", "ArtSneakerDark", (sx, 0.0, 0.14), 0.108, 0.12, segments=8)
        
        # Sneaker Foot & White Rubber Toe Cap
        box(f"SneakerFoot_{side}", "ArtSneakerDark", (sx, -0.06, 0.075), (0.20, 0.28, 0.12))
        box(f"SneakerToe_{side}", "ArtSneakerWhite", (sx, -0.165, 0.065), (0.18, 0.09, 0.10))
        
        # Rubber Midsole & Tread
        box(f"SneakerMidSole_{side}", "ArtSneakerSole", (sx, -0.06, 0.025), (0.22, 0.31, 0.035))
        box(f"SneakerTread_{side}", "ArtSneakerDark", (sx, -0.06, 0.010), (0.23, 0.32, 0.02))
        
        # White Laces & Tongue
        box(f"SneakerLace_{side}", "ArtSneakerWhite", (sx, -0.09, 0.125), (0.12, 0.10, 0.025))

        # Paint splatters on sneakers
        box(f"ShoeSplat1_{side}", "ArtSplatterYellow", (sx - 0.06, -0.10, 0.06), (0.02, 0.02, 0.02), rot=(0.2, 0.1, 0.3))
        box(f"ShoeSplat2_{side}", "ArtSplatterCyan", (sx + 0.05, -0.04, 0.07), (0.02, 0.02, 0.02), rot=(0.1, 0.4, 0.2))


# -------------------------------------------------------------
# 2. TORSO, TURTLENECK SWEATER & PAINT-SPLATTERED APRON
# -------------------------------------------------------------

def build_torso_and_apron():
    # Pelvis & Apron Lower Skirt
    cylinder("Pelvis", "ArtApronDark", (0.0, 0.0, 0.58), 0.27, 0.18, segments=10)
    
    # Waist Belt / Apron Tie Band
    cylinder("ApronTieBand", "ArtPocketDark", (0.0, 0.0, 0.67), 0.28, 0.07, segments=12)
    # Tie knot on back
    box("ApronBowBack", "ArtPocketDark", (0.0, 0.28, 0.67), (0.10, 0.04, 0.05))

    # Torso (Turtleneck Base + Apron Body)
    cylinder("TorsoWaist", "ArtApronDark", (0.0, -0.01, 0.84), 0.285, 0.32, segments=10, r2=0.28)
    cylinder("TorsoChest", "ArtApronDark", (0.0, -0.02, 1.08), 0.28, 0.26, segments=10, r2=0.25)
    
    # Dark Turtleneck Collar in Neck Opening
    cylinder("TurtleneckCollar", "ArtSweaterCharcoal", (0.0, -0.01, 1.25), 0.12, 0.10, segments=8)
    
    # Front Apron Bib / Panel
    box("ApronBib", "ArtApronDark", (0.0, -0.25, 0.96), (0.24, 0.06, 0.38))
    # Apron Shoulder Straps
    tube("ApronStrap_L", "ArtPocketDark", (-0.12, -0.24, 1.15), (-0.14, 0.16, 1.18), 0.020, segments=4)
    tube("ApronStrap_R", "ArtPocketDark", (0.12, -0.24, 1.15), (0.14, 0.16, 1.18), 0.020, segments=4)

    # Apron Front Pockets (with tucked pencils/brushes)
    box("ApronPocket_L", "ArtPocketDark", (-0.12, -0.28, 0.78), (0.10, 0.03, 0.11))
    box("ApronPocket_R", "ArtPocketDark", (0.12, -0.28, 0.78), (0.10, 0.03, 0.11))
    tube("PocketPencil1", "ArtBrushWood", (-0.10, -0.29, 0.88), (-0.08, -0.28, 0.74), 0.008, segments=4)
    tube("PocketBrush1", "ArtBrushWood", (0.10, -0.29, 0.89), (0.08, -0.28, 0.74), 0.008, segments=4)

    # --- COLORFUL GEOMETRIC PAINT SPLATTERS ON APRON ---
    splatters = [
        ("Splat_Red1", "ArtSplatterRed", (-0.06, -0.285, 0.98), (0.035, 0.012, 0.035), (0.2, 0.1, 0.4)),
        ("Splat_Yel1", "ArtSplatterYellow", (0.07, -0.285, 1.04), (0.040, 0.012, 0.030), (-0.1, 0.3, -0.2)),
        ("Splat_Cyan1", "ArtSplatterCyan", (-0.08, -0.285, 0.72), (0.035, 0.012, 0.040), (0.3, -0.2, 0.1)),
        ("Splat_Grn1", "ArtSplatterGreen", (0.04, -0.285, 0.86), (0.030, 0.012, 0.030), (0.1, 0.2, 0.3)),
        ("Splat_Pur1", "ArtSplatterPurple", (-0.02, -0.285, 1.12), (0.028, 0.012, 0.028), (-0.2, 0.1, 0.5)),
        ("Splat_Orn1", "ArtSplatterOrange", (0.09, -0.285, 0.74), (0.035, 0.012, 0.035), (0.2, 0.3, -0.1)),
        ("Splat_Wht1", "ArtSplatterWhite", (-0.03, -0.285, 0.65), (0.025, 0.012, 0.025), (0.1, -0.1, 0.2)),
    ]
    for name, mat, loc, size, rot in splatters:
        box(name, mat, loc, size, rot=rot)


# -------------------------------------------------------------
# 3. CANVAS MESSENGER SHOULDER BAG
# -------------------------------------------------------------

def build_messenger_bag():
    # Diagonal Canvas Strap from right shoulder to left hip
    tube("BagStrap", "ArtBagStrap", (0.22, -0.22, 1.20), (-0.26, 0.12, 0.66), 0.022, segments=4)
    cylinder("BagBuckle", "ArtBadgeGold", (-0.02, -0.24, 0.95), 0.030, 0.015, segments=6, rot=(0.4, 0.8, 0))

    # Canvas Messenger Bag at left hip
    box("CanvasBagBody", "ArtBagCanvas", (-0.28, -0.04, 0.65), (0.10, 0.22, 0.18), rot=(0, 0, 0.25))
    box("CanvasBagFlap", "ArtBagStrap", (-0.285, -0.04, 0.73), (0.105, 0.225, 0.04), rot=(0, 0, 0.25))
    # Brushes peeking out of bag
    tube("BagBrush1", "ArtBrushWood", (-0.28, -0.06, 0.82), (-0.28, -0.06, 0.68), 0.010, segments=4)
    cylinder("BagBrushTip1", "ArtBrushTipRed", (-0.28, -0.06, 0.84), 0.010, 0.025, segments=6)


# -------------------------------------------------------------
# 4. ARMS, HANDS, WOODEN PALETTE & SCEPTER GRIP
# -------------------------------------------------------------

def build_arms_and_palette():
    # --- RIGHT ARM (Resting on Palette Scepter) ---
    r_sh = (0.34, -0.02, 1.14)
    icosphere("Shoulder_R", "ArtApronDark", r_sh, 0.12, subdivisions=1)
    # Upper arm in dark turtleneck
    tube("UpperArm_R", "ArtSweaterCharcoal", r_sh, (0.46, -0.05, 0.94), 0.085, segments=8)
    icosphere("Elbow_R", "ArtSweaterCharcoal", (0.46, -0.05, 0.94), 0.082, subdivisions=1)
    # Forearm extending to rest on staff
    tube("Forearm_R", "ArtSweaterCharcoal", (0.46, -0.05, 0.94), (0.48, -0.22, 0.86), 0.078, segments=8)
    cylinder("SleeveCuff_R", "ArtApronDark", (0.48, -0.22, 0.86), 0.082, 0.04, segments=8)
    # Right Hand resting on scepter head
    icosphere("Hand_R", "ArtSkin", (0.49, -0.25, 0.84), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_R", "ArtSkin", (0.45, -0.23, 0.85), (0.028, 0.04, 0.028), rot=(0.2, 0.2, 0.3))
    box("Fingers_R", "ArtSkin", (0.50, -0.26, 0.83), (0.065, 0.04, 0.065), rot=(0, 0.2, 0.2))

    # --- LEFT ARM (Holding Wooden Artist Palette) ---
    l_sh = (-0.34, -0.02, 1.14)
    icosphere("Shoulder_L", "ArtApronDark", l_sh, 0.12, subdivisions=1)
    # Upper arm
    tube("UpperArm_L", "ArtSweaterCharcoal", l_sh, (-0.46, -0.08, 0.95), 0.085, segments=8)
    icosphere("Elbow_L", "ArtSweaterCharcoal", (-0.46, -0.08, 0.95), 0.082, subdivisions=1)
    # Forearm bent forward holding palette
    tube("Forearm_L", "ArtSweaterCharcoal", (-0.46, -0.08, 0.95), (-0.32, -0.26, 0.90), 0.078, segments=8)
    cylinder("SleeveCuff_L", "ArtApronDark", (-0.32, -0.26, 0.90), 0.082, 0.04, segments=8)
    # Left Hand gripping palette through thumb hole
    icosphere("Hand_L", "ArtSkin", (-0.28, -0.32, 0.90), 0.072, scale=(1.0, 1.1, 0.9), subdivisions=1)
    box("Thumb_L", "ArtSkin", (-0.25, -0.32, 0.92), (0.028, 0.04, 0.028), rot=(0.2, -0.2, -0.3))

    # --- WOODEN ARTIST PAINT PALETTE (精緻木製調色盤) ---
    pal_pos = (-0.26, -0.38, 0.92)
    pal_rot = (0.28, 0.35, -0.15)
    # Oval shaped wooden palette board
    cylinder("PaletteBoard", "ArtPaletteWood", pal_pos, 0.14, 0.015, segments=12, rot=pal_rot)
    # Thumb hole cutout
    cylinder("ThumbHoleRim", "ArtPaletteDark", (pal_pos[0] + 0.05, pal_pos[1] + 0.02, pal_pos[2]), 0.032, 0.018, segments=8, rot=pal_rot)
    
    # Colorful oil paint dollops arranged around palette periphery
    paint_dollops = [
        ("DollopRed", "ArtSplatterRed", (pal_pos[0] - 0.08, pal_pos[1] - 0.04, pal_pos[2] + 0.03)),
        ("DollopOrange", "ArtSplatterOrange", (pal_pos[0] - 0.09, pal_pos[1], pal_pos[2] + 0.05)),
        ("DollopYellow", "ArtSplatterYellow", (pal_pos[0] - 0.06, pal_pos[1] + 0.05, pal_pos[2] + 0.07)),
        ("DollopGreen", "ArtSplatterGreen", (pal_pos[0], pal_pos[1] + 0.06, pal_pos[2] + 0.07)),
        ("DollopCyan", "ArtSplatterCyan", (pal_pos[0] + 0.05, pal_pos[1] + 0.04, pal_pos[2] + 0.05)),
        ("DollopPurple", "ArtSplatterPurple", (pal_pos[0] - 0.03, pal_pos[1] - 0.07, pal_pos[2] + 0.01)),
        ("DollopWhite", "ArtSplatterWhite", (pal_pos[0] + 0.02, pal_pos[1] - 0.06, pal_pos[2] + 0.01)),
    ]
    for dname, dmat, dloc in paint_dollops:
        icosphere(dname, dmat, dloc, 0.020, scale=(1.0, 1.0, 0.6), subdivisions=1, rot=pal_rot)


# -------------------------------------------------------------
# 5. HEAD, WAVY CURLY HAIR, ROUND GLASSES & BERET
# -------------------------------------------------------------

def build_head_and_beret():
    # Neck
    cylinder("Neck", "ArtSkin", (0.0, -0.01, 1.28), 0.10, 0.12, segments=8)
    
    # Base Cranium
    icosphere("HeadBase", "ArtSkin", (0.0, 0.0, 1.48), 0.23, scale=(1.12, 1.0, 1.04), subdivisions=2)
    # Nose
    box("NoseBridge", "ArtNose", (0.0, -0.225, 1.46), (0.042, 0.05, 0.068), rot=(0.2, 0, 0))
    icosphere("NoseTip", "ArtNose", (0.0, -0.245, 1.43), 0.038, scale=(1.1, 0.9, 0.9), subdivisions=1)
    
    # Cheerful expressive cheeks & smile
    for s in (-1, 1):
        icosphere(f"Cheek_{s}", "ArtCheek", (s * 0.12, -0.18, 1.41), 0.056, scale=(1.0, 0.6, 0.9), subdivisions=1)
        icosphere(f"Ear_{s}", "ArtSkin", (s * 0.25, 0.01, 1.47), 0.045, scale=(0.5, 0.9, 1.2), subdivisions=1)
        # Eyes
        box(f"EyeWhite_{s}", "ArtEyeWhite", (s * 0.09, -0.205, 1.51), (0.055, 0.02, 0.035))
        box(f"EyeIris_{s}", "ArtEyeGreen", (s * 0.09, -0.218, 1.51), (0.032, 0.01, 0.032))
        box(f"EyePupil_{s}", "ArtPupil", (s * 0.09, -0.225, 1.51), (0.016, 0.008, 0.016))
        # Eyebrows
        box(f"Eyebrow_{s}", "ArtHairBrown", (s * 0.095, -0.21, 1.56), (0.075, 0.03, 0.025), rot=(0, 0, s * 0.12))

    # Cheerful Creative Smile showing white teeth
    box("MouthCavity", "ArtLip", (0.0, -0.22, 1.34), (0.09, 0.02, 0.032))
    box("TeethUpper", "ArtTeeth", (0.0, -0.226, 1.352), (0.078, 0.015, 0.015))

    # Voluminous Wavy / Curly Brown Hair Framing Face
    icosphere("HairBack", "ArtHairBrown", (0.0, 0.09, 1.47), 0.22, scale=(1.15, 0.95, 0.9), subdivisions=1)
    for s in (-1, 1):
        # Curly hair tufts on temples and cheeks
        icosphere(f"HairCurl1_{s}", "ArtHairBrown", (s * 0.22, -0.05, 1.46), 0.10, scale=(0.8, 1.1, 1.1), subdivisions=1)
        icosphere(f"HairCurl2_{s}", "ArtHairBrown", (s * 0.21, 0.04, 1.38), 0.09, scale=(0.8, 1.1, 1.1), subdivisions=1)
        icosphere(f"HairCurl3_{s}", "ArtHairBrown", (s * 0.14, -0.16, 1.36), 0.075, scale=(1.0, 0.8, 0.9), subdivisions=1)

    # Round Dark-Framed Spectacles
    for s in (-1, 1):
        torus(f"GlassesFrame_{s}", "ArtGlassFrame", (s * 0.09, -0.235, 1.51), 0.052, 0.010, seg_major=10, seg_minor=4, rot=(math.pi/2, 0, 0))
        cylinder(f"GlassesLens_{s}", "ArtGlassLens", (s * 0.09, -0.235, 1.51), 0.046, 0.005, segments=8, rot=(math.pi/2, 0, 0))
        tube(f"GlassesTemple_{s}", "ArtGlassFrame", (s * 0.14, -0.23, 1.51), (s * 0.235, -0.04, 1.51), 0.008, segments=4)
    box("GlassesBridge", "ArtGlassFrame", (0.0, -0.235, 1.51), (0.045, 0.012, 0.012))

    # --- TILTED ARTIST BERET (幾何貝雷帽) ---
    beret_z = 1.63
    # Main slanted beret crown (tilted stylishly to right side)
    icosphere("BeretCrown", "ArtBeretNavy", (0.04, -0.02, beret_z), 0.30, scale=(1.20, 1.15, 0.55), subdivisions=1, rot=(0.15, 0.25, -0.1))
    # Beret Center Stalk / Tip
    cylinder("BeretStalk", "ArtBeretDark", (0.06, -0.02, beret_z + 0.16), 0.015, 0.035, segments=6, rot=(0.15, 0.25, -0.1))
    # Lower fitted beret headband
    cylinder("BeretBand", "ArtBeretDark", (0.0, 0.0, beret_z - 0.06), 0.275, 0.04, segments=12, rot=(0.08, 0.12, 0))

    # Metallic Triangle Emblem / Badge on Beret Front
    box("BeretBadgeTriangle", "ArtBadgeGold", (-0.14, -0.23, beret_z + 0.04), (0.045, 0.015, 0.045), rot=(-0.1, 0.3, 0.78))
    box("BeretBadgeGem", "ArtBadgeGreen", (-0.14, -0.24, beret_z + 0.04), (0.025, 0.010, 0.025), rot=(-0.1, 0.3, 0.78))

    # Paintbrushes tucked behind beret band on side
    b1_start = (-0.18, -0.15, beret_z + 0.08)
    b1_end = (-0.24, 0.08, beret_z + 0.24)
    tube("TuckedBrush1_Shaft", "ArtBrushWood", b1_start, b1_end, 0.010, segments=6)
    cylinder("TuckedBrush1_Ferrule", "ArtBrushSilver", (-0.22, 0.02, beret_z + 0.20), 0.012, 0.025, segments=6)
    cylinder("TuckedBrush1_Tip", "ArtBrushTipCyan", (-0.24, 0.08, beret_z + 0.24), 0.010, 0.03, segments=6, r2=0.002)

    b2_start = (-0.15, -0.18, beret_z + 0.06)
    b2_end = (-0.20, 0.05, beret_z + 0.21)
    tube("TuckedBrush2_Shaft", "ArtBrushWood", b2_start, b2_end, 0.009, segments=6)
    cylinder("TuckedBrush2_Tip", "ArtBrushTipRed", (-0.20, 0.05, beret_z + 0.21), 0.009, 0.028, segments=6, r2=0.002)


# -------------------------------------------------------------
# 6. ARTISTIC SCEPTER WITH PALETTE CREST & SPECTRUM POLE
# -------------------------------------------------------------

def build_artistic_scepter():
    sx = 0.49
    sy = -0.25
    top_z = 0.84
    
    # 1. Faceted Optical Crystal Prism Bottom Spike
    cylinder("PrismTip", "ArtPrismPoint", (sx, sy, 0.04), 0.035, 0.08, segments=6, r2=0.01)
    cylinder("PrismMount", "ArtScepterBrass", (sx, sy, 0.09), 0.044, 0.03, segments=8)
    
    # 2. Rainbow Chromatic Spectrum Bar
    spectrum_mats = [
        "ArtSpecPurple",
        "ArtSpecBlue",
        "ArtSpecCyan",
        "ArtSpecGreen",
        "ArtSpecYellow",
        "ArtSpecOrange",
        "ArtSpecRed",
    ]
    band_h = 0.10
    for idx, smat in enumerate(spectrum_mats):
        bz = 0.14 + idx * band_h
        cylinder(f"SpectrumBand_{idx}", smat, (sx, sy, bz), 0.032, band_h, segments=8)
        box(f"SpecTick_{idx}", "ArtBeretDark", (sx, sy - 0.033, bz), (0.016, 0.005, 0.012))

    # Upper Golden Collar
    cylinder("StaffUpperCollar", "ArtScepterBrass", (sx, sy, 0.86), 0.045, 0.04, segments=8)

    # 3. Interlocking Steampunk Clockwork Gears Mechanism
    gear_z = 0.96
    cylinder("MainGearHub", "ArtScepterGold", (sx, sy, gear_z), 0.075, 0.024, segments=12, rot=(math.pi/2, 0, 0))
    cylinder("MainGearAxle", "ArtScepterBrass", (sx, sy - 0.015, gear_z), 0.040, 0.012, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + 0.085 * math.cos(ang)
        tz = gear_z + 0.085 * math.sin(ang)
        box(f"MainGearTooth_{i}", "ArtScepterGold", (tx, sy, tz), (0.02, 0.022, 0.02), rot=(0, -ang, 0))

    # Small secondary gear
    s_gx = sx - 0.072
    s_gz = gear_z - 0.052
    cylinder("SmallGearHub", "ArtScepterBrass", (s_gx, sy + 0.008, s_gz), 0.042, 0.018, segments=8, rot=(math.pi/2, 0, 0))
    for i in range(6):
        ang = i * math.pi / 3
        tx = s_gx + 0.048 * math.cos(ang)
        tz = s_gz + 0.048 * math.sin(ang)
        box(f"SmallTooth_{i}", "ArtScepterBrass", (tx, sy + 0.008, tz), (0.014, 0.018, 0.014), rot=(0, -ang, 0))

    # 4. Golden Palette Shape Crest with Colorful Paint Gems at Summit
    crest_z = gear_z + 0.16
    # Palette crest disc
    cylinder("CrestPaletteDisc", "ArtScepterGold", (sx, sy, crest_z), 0.085, 0.020, segments=10, rot=(math.pi/2, 0, 0))
    cylinder("CrestThumbHole", "ArtScepterBrass", (sx + 0.035, sy - 0.012, crest_z), 0.022, 0.024, segments=8, rot=(math.pi/2, 0, 0))
    
    # Colorful Paint Gems on Crest
    crest_gems = [
        ("CrestGemRed", "ArtSplatterRed", (sx - 0.04, sy - 0.015, crest_z + 0.04)),
        ("CrestGemYellow", "ArtSplatterYellow", (sx, sy - 0.015, crest_z + 0.055)),
        ("CrestGemCyan", "ArtSplatterCyan", (sx + 0.04, sy - 0.015, crest_z + 0.04)),
        ("CrestGemGreen", "ArtSplatterGreen", (sx - 0.05, sy - 0.015, crest_z)),
        ("CrestGemPurple", "ArtSplatterPurple", (sx - 0.03, sy - 0.015, crest_z - 0.04)),
    ]
    for gname, gmat, gloc in crest_gems:
        icosphere(gname, gmat, gloc, 0.018, subdivisions=1)


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
    obj.name = "Artist"
    
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
    print(f"Artist finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "artist.glb")
    
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


def generate_artist():
    reset_scene()
    setup_materials()
    build_legs_and_sneakers()
    build_torso_and_apron()
    build_messenger_bag()
    build_arms_and_palette()
    build_head_and_beret()
    build_artistic_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_artist()
