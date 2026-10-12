"""
Engineer (工程師) 3D Model Generator for Blender.
Accurately implements character/engineer.jpg:
- Geometric low-poly style with clean faceted surfaces
- Peaked work cap / hardhat with hexagonal metallic plate & illuminated cyan LED lamp
- Heavy communication headset / ear protection muffs with gold rings & dark headband
- HUD glasses with dark rectangular frames & transparent cyan HUD lenses
- Trimmed brown full beard, mustache, confident warm smile
- Slate-grey utility hooded jacket with chest pockets, sleeve patches & cyan circuit traces on left chest
- Dark plaid flannel shirt inside with open collar
- Crossbody strap, heavy brown leather tool belt with brass buckle & pouches
- Hanging tools: orange-handle hammer and adjustable steel wrench
- Technical backpack with comms antenna & cyan LED beacon
- Right hand rests on Polyhedral Tech Scepter (多面體科技權杖):
  * Metallic shaft with glowing cyan ladder LED display
  * Interlocking brass/gold mechanical gears and lattice cage
  * Golden polyhedral die (D6) at the top
- Left hand holds high-tech blueprint tablet with glowing display
- Charcoal cargo pants and steel-toe work boots
- Pivot at (0, 0, 0) at ground center, height ~ 1.82 units
- 100% Manifold geometry (0 non-manifold edges), outward normals, clean UVs
- Exports to assets/models/engineer.glb
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
        if o.name.startswith("Engineer") or o.name.startswith("part_") or o.name == "Engineer":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.name.startswith("Eng") or m.name.startswith("part_"):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.name.startswith("Eng"):
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
    get_or_create_mat("EngSkin", (0.92, 0.68, 0.54), rough=0.7)
    get_or_create_mat("EngCheek", (0.88, 0.46, 0.40), rough=0.7)
    get_or_create_mat("EngNose", (0.90, 0.60, 0.48), rough=0.7)
    get_or_create_mat("EngLip", (0.45, 0.18, 0.18), rough=0.6)
    get_or_create_mat("EngTeeth", (0.96, 0.96, 0.94), rough=0.3)
    get_or_create_mat("EngEyeWhite", (0.95, 0.95, 0.95), rough=0.2)
    get_or_create_mat("EngEyeCyan", (0.12, 0.68, 0.88), rough=0.2)
    get_or_create_mat("EngPupil", (0.04, 0.04, 0.05), rough=0.2)
    
    # Hair & Beard
    get_or_create_mat("EngHairBrown", (0.28, 0.18, 0.12), rough=0.75)
    
    # Clothing (Slate grey jacket, flannel, cargo pants)
    get_or_create_mat("EngJacketSlate", (0.22, 0.27, 0.35), rough=0.65)
    get_or_create_mat("EngJacketDark", (0.14, 0.18, 0.24), rough=0.7)
    get_or_create_mat("EngPlaidDark", (0.16, 0.18, 0.20), rough=0.8)
    get_or_create_mat("EngPlaidStripe", (0.26, 0.29, 0.32), rough=0.8)
    get_or_create_mat("EngPantsSlate", (0.16, 0.20, 0.26), rough=0.75)
    get_or_create_mat("EngGloveDark", (0.12, 0.13, 0.16), rough=0.7)
    get_or_create_mat("EngGloveTrim", (0.40, 0.44, 0.48), rough=0.6)
    
    # Boots
    get_or_create_mat("EngBootDark", (0.12, 0.14, 0.18), rough=0.8)
    get_or_create_mat("EngBootSole", (0.07, 0.07, 0.08), rough=0.9)
    get_or_create_mat("EngBootSteel", (0.48, 0.52, 0.56), metal=0.8, rough=0.35)
    
    # Cap & Headset
    get_or_create_mat("EngCapSlate", (0.22, 0.27, 0.35), rough=0.65)
    get_or_create_mat("EngCapPeak", (0.15, 0.19, 0.25), rough=0.65)
    get_or_create_mat("EngHeadsetDark", (0.10, 0.12, 0.15), rough=0.5)
    get_or_create_mat("EngHeadsetGold", (0.90, 0.70, 0.20), metal=0.9, rough=0.25)
    get_or_create_mat("EngPlateGrey", (0.35, 0.38, 0.42), metal=0.7, rough=0.4)
    
    # Glowing Circuit / HUD / Lamp
    get_or_create_mat("EngNeonCyan", (0.15, 0.90, 1.00), rough=0.1, emission=(0.15, 0.90, 1.00))
    get_or_create_mat("EngHUDGlass", (0.20, 0.85, 0.95), rough=0.1, trans=0.82)
    get_or_create_mat("EngGlassFrame", (0.08, 0.09, 0.11), rough=0.4)
    
    # Leather Gear & Belt & Tools
    get_or_create_mat("EngLeatherBrown", (0.34, 0.20, 0.12), rough=0.6)
    get_or_create_mat("EngBrassBuckle", (0.88, 0.68, 0.18), metal=0.88, rough=0.28)
    get_or_create_mat("EngSteelTool", (0.70, 0.72, 0.75), metal=0.88, rough=0.25)
    get_or_create_mat("EngOrangeTool", (0.96, 0.48, 0.10), rough=0.5)
    get_or_create_mat("EngBackpack", (0.18, 0.23, 0.30), rough=0.65)
    
    # Tablet
    get_or_create_mat("EngTabletBody", (0.12, 0.14, 0.18), rough=0.4)
    get_or_create_mat("EngTabletScreen", (0.10, 0.40, 0.60), rough=0.2)
    get_or_create_mat("EngTabletBlue", (0.30, 0.85, 1.00), rough=0.2, emission=(0.30, 0.85, 1.00))
    
    # Scepter
    get_or_create_mat("EngScepterGold", (0.96, 0.78, 0.18), metal=0.92, rough=0.20)
    get_or_create_mat("EngScepterBrass", (0.85, 0.65, 0.18), metal=0.85, rough=0.28)
    get_or_create_mat("EngScepterShaft", (0.12, 0.14, 0.18), metal=0.5, rough=0.3)

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
# 1. LEGS, CARGO PANTS & INDUSTRIAL BOOTS
# -------------------------------------------------------------

def build_legs_and_boots():
    for side, sx in [("L", -0.165), ("R", 0.165)]:
        # Cargo pants upper leg
        cylinder(f"LegThigh_{side}", "EngPantsSlate", (sx, 0.0, 0.44), 0.13, 0.32, segments=8)
        # Knee cuff / reinforcement fold
        cylinder(f"Knee_{side}", "EngPantsSlate", (sx, 0.01, 0.28), 0.125, 0.12, segments=8)
        # Cargo flap pocket on outer thigh
        px = sx + (0.13 if sx > 0 else -0.13)
        box(f"CargoPocket_{side}", "EngJacketDark", (px, 0.0, 0.42), (0.04, 0.14, 0.16), rot=(0, 0.1 if sx > 0 else -0.1, 0))
        box(f"CargoFlap_{side}", "EngPantsSlate", (px, 0.0, 0.50), (0.045, 0.145, 0.04), rot=(0, 0.1 if sx > 0 else -0.1, 0))

        # Shin / Boot shaft
        cylinder(f"BootShaft_{side}", "EngBootDark", (sx, 0.0, 0.17), 0.115, 0.16, segments=8)
        
        # Heavy Boot Foot
        box(f"BootFoot_{side}", "EngBootDark", (sx, -0.06, 0.08), (0.21, 0.30, 0.14))
        # Steel Toe Cap
        box(f"BootToe_{side}", "EngBootSteel", (sx, -0.18, 0.07), (0.19, 0.10, 0.12))
        # Boot Sole
        box(f"BootSole_{side}", "EngBootSole", (sx, -0.06, 0.02), (0.23, 0.34, 0.04))
        # Boot Heel
        box(f"BootHeel_{side}", "EngBootSole", (sx, 0.05, 0.03), (0.23, 0.12, 0.06))
        # Ankle buckle strap
        box(f"BootStrap_{side}", "EngLeatherBrown", (sx, -0.02, 0.13), (0.22, 0.22, 0.03))


# -------------------------------------------------------------
# 2. TORSO, JACKET, FLANNEL SHIRT & POCKETS
# -------------------------------------------------------------

def build_torso_and_jacket():
    # Pelvis / Lower jacket skirt
    cylinder("Pelvis", "EngPantsSlate", (0.0, 0.0, 0.60), 0.26, 0.16, segments=10)
    
    # Heavy leather tool belt
    cylinder("ToolBelt", "EngLeatherBrown", (0.0, 0.0, 0.67), 0.28, 0.08, segments=12)
    # Heavy brass belt buckle
    box("BeltBuckle", "EngBrassBuckle", (0.0, -0.285, 0.67), (0.13, 0.03, 0.09))
    box("BeltProng", "EngSteelTool", (0.0, -0.295, 0.67), (0.03, 0.02, 0.06))
    
    # Utility Jacket Torso
    cylinder("JacketTorso", "EngJacketSlate", (0.0, -0.01, 0.88), 0.295, 0.36, segments=10, r2=0.275)
    cylinder("JacketChest", "EngJacketSlate", (0.0, -0.02, 1.10), 0.28, 0.24, segments=10, r2=0.25)
    
    # Inner dark plaid flannel shirt visible down the center chest
    box("FlannelShirt", "EngPlaidDark", (0.0, -0.23, 0.98), (0.15, 0.08, 0.38))
    # Flannel vertical button placket
    box("FlannelPlacket", "EngPlaidStripe", (0.0, -0.275, 0.98), (0.04, 0.02, 0.36))
    for bz in [0.86, 0.94, 1.02, 1.10]:
        cylinder(f"FlannelButton_{bz}", "EngPlateGrey", (0.0, -0.288, bz), 0.012, 0.01, segments=6, rot=(math.pi/2, 0, 0))
    
    # Jacket Lapels / Front panels framing the flannel shirt
    box("JacketLapel_L", "EngJacketSlate", (-0.12, -0.26, 0.98), (0.10, 0.06, 0.36), rot=(0, 0, -0.08))
    box("JacketLapel_R", "EngJacketSlate", (0.12, -0.26, 0.98), (0.10, 0.06, 0.36), rot=(0, 0, 0.08))
    
    # Turned up jacket hood/collar around back and neck
    cylinder("HoodCollar", "EngJacketDark", (0.0, 0.06, 1.21), 0.26, 0.16, segments=10, r2=0.23)
    
    # Right chest flap pocket
    box("ChestPocket_R", "EngJacketDark", (0.15, -0.26, 1.04), (0.11, 0.04, 0.13))
    box("ChestPocketFlap_R", "EngJacketSlate", (0.15, -0.27, 1.11), (0.12, 0.045, 0.035))
    cylinder("PocketButton_R", "EngPlateGrey", (0.15, -0.295, 1.10), 0.012, 0.01, segments=6, rot=(math.pi/2, 0, 0))
    
    # Left chest pocket & iconic GLOWING CYAN CIRCUIT PATTERN (幾何電路紋路)
    box("ChestPocket_L", "EngJacketDark", (-0.15, -0.26, 1.04), (0.11, 0.04, 0.13))
    box("ChestPocketFlap_L", "EngJacketSlate", (-0.15, -0.27, 1.11), (0.12, 0.045, 0.035))
    
    # Cyan circuit traces on left chest above/around pocket
    circuit_traces = [
        # (x, y, z, w, d, h)
        (-0.16, -0.28, 1.18, 0.14, 0.015, 0.012),   # horizontal main trace
        (-0.22, -0.275, 1.15, 0.012, 0.015, 0.07),  # vertical drop trace
        (-0.10, -0.275, 1.20, 0.012, 0.015, 0.05),  # vertical rise trace
        (-0.06, -0.27, 1.22, 0.08, 0.015, 0.012),   # top horizontal branch
        (-0.16, -0.28, 1.14, 0.05, 0.015, 0.012),   # small lower branch
    ]
    for idx, (cx, cy, cz, cw, cd, ch) in enumerate(circuit_traces):
        box(f"CircuitLine_{idx}", "EngNeonCyan", (cx, cy, cz), (cw, cd, ch))
    
    # Circuit connection nodes (square solder pads)
    nodes = [
        (-0.22, -0.28, 1.18),
        (-0.10, -0.28, 1.18),
        (-0.10, -0.275, 1.22),
        (-0.02, -0.27, 1.22),
        (-0.22, -0.275, 1.11),
    ]
    for idx, (nx, ny, nz) in enumerate(nodes):
        box(f"CircuitPad_{idx}", "EngNeonCyan", (nx, ny, nz), (0.022, 0.018, 0.022))


# -------------------------------------------------------------
# 3. CROSSBODY STRAP, BACKPACK & HANGING TOOLS
# -------------------------------------------------------------

def build_backpack_and_tools():
    # Crossbody leather strap from left shoulder (x=-0.22, z=1.24) to right hip (x=0.22, z=0.68)
    tube("CrossStrap", "EngLeatherBrown", (-0.22, -0.18, 1.24), (0.22, -0.26, 0.68), 0.04, segments=6)
    box("StrapBuckle", "EngBrassBuckle", (0.0, -0.285, 0.96), (0.06, 0.03, 0.07), rot=(0, 0, -0.65))

    # Tool pouch on left hip (holds small components)
    box("ToolPouch_L", "EngLeatherBrown", (-0.26, -0.06, 0.62), (0.10, 0.16, 0.15), rot=(0, 0, 0.2))
    box("PouchFlap_L", "EngLeatherBrown", (-0.265, -0.06, 0.68), (0.11, 0.165, 0.04), rot=(0, 0, 0.2))
    box("PouchBuckle_L", "EngBrassBuckle", (-0.315, -0.06, 0.65), (0.02, 0.04, 0.04), rot=(0, 0, 0.2))

    # Tool holster on right hip with hammer & wrench
    box("ToolHolster_R", "EngLeatherBrown", (0.27, -0.04, 0.63), (0.10, 0.16, 0.12), rot=(0, 0, -0.2))
    
    # 1. Orange-handled Hammer (鐵鎚)
    hx, hy, hz = 0.29, -0.08, 0.52
    cylinder("HammerHandle", "EngOrangeTool", (hx, hy, hz), 0.022, 0.26, segments=6)
    box("HammerGripBlack", "EngGloveDark", (hx, hy, hz - 0.07), (0.05, 0.05, 0.09))
    box("HammerHead", "EngSteelTool", (hx, hy, hz + 0.13), (0.06, 0.14, 0.06), rot=(0, 0, 0.3))
    cylinder("HammerClaw", "EngSteelTool", (hx - 0.03, hy + 0.05, hz + 0.13), 0.02, 0.06, segments=4, rot=(0.5, 0, 0))

    # 2. Adjustable Steel Wrench (銀色板手)
    wx, wy, wz = 0.28, 0.05, 0.54
    cylinder("WrenchHandle", "EngSteelTool", (wx, wy, wz), 0.018, 0.22, segments=6, rot=(0.1, 0, 0.3))
    box("WrenchHead", "EngSteelTool", (wx + 0.01, wy, wz + 0.11), (0.06, 0.03, 0.07), rot=(0.1, 0, 0.3))
    box("WrenchJawCut", "EngGloveDark", (wx + 0.01, wy, wz + 0.13), (0.03, 0.04, 0.04), rot=(0.1, 0, 0.3))

    # Technical Backpack (背部工程設備背包)
    bx, by, bz = 0.0, 0.28, 0.95
    box("BackpackMain", "EngBackpack", (bx, by, bz), (0.38, 0.22, 0.46))
    box("BackpackTopFlap", "EngJacketDark", (bx, by, bz + 0.20), (0.39, 0.23, 0.08))
    box("BackpackPocket_L", "EngJacketDark", (bx - 0.20, by, bz - 0.05), (0.07, 0.16, 0.24))
    box("BackpackPocket_R", "EngJacketDark", (bx + 0.20, by, bz - 0.05), (0.07, 0.16, 0.24))
    
    # Cyan battery status bar on backpack rear
    box("BackpackStatusBar", "EngNeonCyan", (bx, by + 0.115, bz), (0.18, 0.015, 0.05))
    
    # Comms Antenna on backpack left top corner
    ax, ay, az = -0.15, 0.26, 1.18
    cylinder("AntennaBase", "EngPlateGrey", (ax, ay, az), 0.03, 0.05, segments=8)
    tube("AntennaMast", "EngSteelTool", (ax, ay, az), (ax - 0.04, ay + 0.02, az + 0.48), 0.014, segments=6)
    # Glowing cyan antenna LED tip
    icosphere("AntennaTip", "EngNeonCyan", (ax - 0.04, ay + 0.02, az + 0.49), 0.024, subdivisions=1)


# -------------------------------------------------------------
# 4. ARMS, GLOVED HANDS & BLUEPRINT TABLET
# -------------------------------------------------------------

def build_arms_and_hands():
    # Shoulder pads / epaulets
    box("Shoulder_R", "EngJacketDark", (-0.32, -0.02, 1.20), (0.16, 0.18, 0.09), rot=(0, 0.25, 0))
    box("Shoulder_L", "EngJacketDark", (0.32, -0.02, 1.20), (0.16, 0.18, 0.09), rot=(0, -0.25, 0))
    
    # Arm sleeve utility pocket on right arm
    box("SleevePocket_R", "EngJacketDark", (-0.37, -0.02, 1.10), (0.04, 0.11, 0.13), rot=(0, 0.25, 0))
    
    # --- RIGHT ARM (Holds Tech Scepter) ---
    # Upper arm angled forward/downward
    tube("ArmUpper_R", "EngJacketSlate", (-0.31, -0.02, 1.18), (-0.42, -0.10, 0.95), 0.10, segments=8)
    # Elbow sleeve fold
    cylinder("Elbow_R", "EngJacketDark", (-0.42, -0.10, 0.95), 0.095, 0.08, segments=8)
    # Forearm extending to rest firmly on top of scepter
    tube("Forearm_R", "EngJacketSlate", (-0.42, -0.10, 0.95), (-0.46, -0.22, 0.88), 0.085, segments=8)
    # Glove cuff
    cylinder("GloveCuff_R", "EngGloveTrim", (-0.46, -0.22, 0.88), 0.08, 0.04, segments=8)
    # Gloved Right Hand (gripping/resting on top of scepter shaft & gear bracket)
    box("Hand_R", "EngGloveDark", (-0.47, -0.24, 0.86), (0.11, 0.12, 0.10), rot=(0.2, 0.1, -0.1))
    box("Thumb_R", "EngGloveDark", (-0.44, -0.20, 0.87), (0.04, 0.06, 0.05), rot=(0.2, 0.1, -0.1))
    
    # --- LEFT ARM (Holding High-Tech Blueprint Tablet) ---
    # Upper arm angled down and forward
    tube("ArmUpper_L", "EngJacketSlate", (0.31, -0.02, 1.18), (0.38, -0.12, 0.94), 0.10, segments=8)
    # Elbow fold
    cylinder("Elbow_L", "EngJacketDark", (0.38, -0.12, 0.94), 0.095, 0.08, segments=8)
    # Forearm angled inward to support the tablet
    tube("Forearm_L", "EngJacketSlate", (0.38, -0.12, 0.94), (0.32, -0.28, 0.90), 0.085, segments=8)
    # Glove cuff
    cylinder("GloveCuff_L", "EngGloveTrim", (0.32, -0.28, 0.90), 0.08, 0.04, segments=8)
    # Gloved Left Hand
    box("Hand_L", "EngGloveDark", (0.28, -0.32, 0.89), (0.11, 0.10, 0.09), rot=(-0.25, 0.45, -0.5))

    # --- HIGH-TECH BLUEPRINT TABLET (科技藍圖平板電腦) ---
    # Held by left hand, tilted up toward face (~35 deg pitch, ~20 deg roll)
    tab_loc = (0.26, -0.36, 0.94)
    tab_rot = (-0.55, 0.32, -0.42)
    # Tablet Bezel / Body
    box("TabletBody", "EngTabletBody", tab_loc, (0.26, 0.016, 0.34), rot=tab_rot)
    # Glowing Blueprint Screen
    box("TabletScreen", "EngTabletScreen", (tab_loc[0], tab_loc[1] - 0.009, tab_loc[2]), (0.23, 0.006, 0.30), rot=tab_rot)
    # Architectural Blueprint Grid / Vector Schematics on screen
    box("TabletBPLine1", "EngTabletBlue", (tab_loc[0] - 0.03, tab_loc[1] - 0.012, tab_loc[2] + 0.04), (0.14, 0.004, 0.01), rot=tab_rot)
    box("TabletBPLine2", "EngTabletBlue", (tab_loc[0] + 0.04, tab_loc[1] - 0.012, tab_loc[2] - 0.02), (0.10, 0.004, 0.01), rot=tab_rot)
    box("TabletBPRect", "EngTabletBlue", (tab_loc[0] - 0.02, tab_loc[1] - 0.012, tab_loc[2] - 0.03), (0.09, 0.004, 0.09), rot=tab_rot)
    # Cyan status LED indicator on tablet frame
    box("TabletStatusLED", "EngNeonCyan", (tab_loc[0] + 0.10, tab_loc[1] - 0.012, tab_loc[2] + 0.13), (0.015, 0.005, 0.015), rot=tab_rot)


# -------------------------------------------------------------
# 5. HEAD, FACE, BEARD & HIGH-TECH HUD GLASSES
# -------------------------------------------------------------

def build_head_and_face():
    # Neck
    cylinder("Neck", "EngSkin", (0.0, 0.0, 1.25), 0.12, 0.14, segments=8)
    
    # Head Base
    icosphere("HeadBase", "EngSkin", (0.0, -0.02, 1.44), 0.24, scale=(0.95, 1.05, 1.08), subdivisions=1)
    
    # Cheeks & Nose
    icosphere("Cheek_L", "EngCheek", (-0.12, -0.19, 1.41), 0.05, subdivisions=1)
    icosphere("Cheek_R", "EngCheek", (0.12, -0.19, 1.41), 0.05, subdivisions=1)
    box("NoseBridge", "EngSkin", (0.0, -0.22, 1.45), (0.045, 0.06, 0.09))
    box("NoseTip", "EngNose", (0.0, -0.25, 1.42), (0.055, 0.05, 0.05))
    
    # Eyes
    for side, ex in [("L", -0.08), ("R", 0.08)]:
        box(f"EyeWhite_{side}", "EngEyeWhite", (ex, -0.21, 1.48), (0.055, 0.03, 0.04))
        cylinder(f"EyeIris_{side}", "EngEyeCyan", (ex, -0.222, 1.48), 0.018, 0.01, segments=6, rot=(math.pi/2, 0, 0))
        cylinder(f"EyePupil_{side}", "EngPupil", (ex, -0.226, 1.48), 0.010, 0.01, segments=6, rot=(math.pi/2, 0, 0))
        # Eyebrow
        box(f"Eyebrow_{side}", "EngHairBrown", (ex, -0.22, 1.52), (0.075, 0.03, 0.025), rot=(0, 0.1 if side == "L" else -0.1, 0))
    
    # Confident Smile & Teeth
    box("MouthCavity", "EngLip", (0.0, -0.225, 1.34), (0.11, 0.03, 0.04))
    box("TeethUpper", "EngTeeth", (0.0, -0.232, 1.35), (0.09, 0.02, 0.02))
    
    # Mustache (豐滿八字鬍)
    box("Mustache_L", "EngHairBrown", (-0.055, -0.245, 1.38), (0.07, 0.04, 0.03), rot=(0, 0, 0.2))
    box("Mustache_R", "EngHairBrown", (0.055, -0.245, 1.38), (0.07, 0.04, 0.03), rot=(0, 0, -0.2))
    box("MustacheCenter", "EngHairBrown", (0.0, -0.25, 1.39), (0.04, 0.04, 0.025))

    # Geometric Full Beard wrapping around the chin & jawline
    box("BeardChin", "EngHairBrown", (0.0, -0.19, 1.28), (0.16, 0.14, 0.10))
    box("BeardChinTip", "EngHairBrown", (0.0, -0.22, 1.29), (0.12, 0.06, 0.07))
    box("BeardJaw_L", "EngHairBrown", (-0.14, -0.11, 1.33), (0.08, 0.18, 0.12), rot=(0, 0, -0.3))
    box("BeardJaw_R", "EngHairBrown", (0.14, -0.11, 1.33), (0.08, 0.18, 0.12), rot=(0, 0, 0.3))
    # Sideburns connecting to hair
    box("Sideburn_L", "EngHairBrown", (-0.21, -0.04, 1.44), (0.04, 0.10, 0.12))
    box("Sideburn_R", "EngHairBrown", (0.21, -0.04, 1.44), (0.04, 0.10, 0.12))
    # Hair peeking out behind neck
    box("HairBack", "EngHairBrown", (0.0, 0.14, 1.37), (0.28, 0.12, 0.14))

    # --- HIGH-TECH HUD GLASSES (幾何戰術HUD眼鏡) ---
    # Black modern frames
    box("HUDFrameBridge", "EngGlassFrame", (0.0, -0.245, 1.48), (0.05, 0.02, 0.015))
    box("HUDFrameTop", "EngGlassFrame", (0.0, -0.245, 1.51), (0.24, 0.02, 0.015))
    for side, gx in [("L", -0.085), ("R", 0.085)]:
        box(f"HUDBorderOuter_{side}", "EngGlassFrame", (gx + (-0.05 if side == "L" else 0.05), -0.245, 1.48), (0.015, 0.02, 0.055))
        box(f"HUDBorderBottom_{side}", "EngGlassFrame", (gx, -0.245, 1.45), (0.10, 0.02, 0.015))
        # Semi-transparent glowing cyan HUD lenses
        box(f"HUDLens_{side}", "EngHUDGlass", (gx, -0.243, 1.48), (0.09, 0.008, 0.05))
        # Digital reticle/circuit grid inside HUD
        box(f"HUDGrid_{side}", "EngNeonCyan", (gx + (0.02 if side == "L" else -0.02), -0.246, 1.48), (0.03, 0.004, 0.01))
        # Temple arms extending back to ears
        tube(f"HUDTemple_{side}", "EngGlassFrame", (gx + (-0.05 if side == "L" else 0.05), -0.23, 1.49), (gx + (-0.12 if side == "L" else 0.12), -0.04, 1.48), 0.008, segments=4)


# -------------------------------------------------------------
# 6. WORK CAP & INTEGRATED HEADSET / EAR-MUFFS
# -------------------------------------------------------------

def build_headset_and_cap():
    # Cap main dome (fitted snugly on head)
    cylinder("CapBase", "EngCapSlate", (0.0, -0.01, 1.59), 0.255, 0.18, segments=10, r2=0.235)
    icosphere("CapCrown", "EngCapSlate", (0.0, 0.02, 1.66), 0.23, scale=(1.0, 1.05, 0.65), subdivisions=1)
    
    # Cap Peak / Front Visor (鴨舌遮陽帽簷)
    # Extends forward with slight downward slope
    box("CapVisor", "EngCapPeak", (0.0, -0.23, 1.60), (0.32, 0.18, 0.03), rot=(0.28, 0, 0))
    box("CapVisorTrim", "EngJacketDark", (0.0, -0.30, 1.57), (0.30, 0.04, 0.03), rot=(0.28, 0, 0))

    # Front Cap Hexagonal Plate & Glowing Cyan LED Headlamp (六角金屬底座與頭燈)
    box("CapHexMount", "EngPlateGrey", (0.0, -0.22, 1.66), (0.11, 0.03, 0.11), rot=(-0.25, 0, 0))
    cylinder("CapLampRing", "EngHeadsetGold", (0.0, -0.238, 1.66), 0.045, 0.025, segments=8, rot=(math.pi/2 - 0.25, 0, 0))
    # Illuminated Cyan Hexagonal Lens
    cylinder("CapLampLens", "EngNeonCyan", (0.0, -0.248, 1.66), 0.036, 0.015, segments=6, rot=(math.pi/2 - 0.25, 0, 0))

    # --- HEAVY COMMUNICATION HEADSET / EAR-PROTECTION MUFFS (幾何耳機) ---
    # Top Headband spanning across the cap from ear to ear
    tube("Headband_L", "EngHeadsetDark", (-0.24, 0.0, 1.48), (-0.16, 0.0, 1.72), 0.028, segments=6)
    tube("Headband_Top", "EngHeadsetDark", (-0.16, 0.0, 1.72), (0.16, 0.0, 1.72), 0.028, segments=6)
    tube("Headband_R", "EngHeadsetDark", (0.16, 0.0, 1.72), (0.24, 0.0, 1.48), 0.028, segments=6)
    
    # Headband adjustment notch brackets
    box("BandBracket_L", "EngPlateGrey", (-0.20, 0.0, 1.62), (0.04, 0.06, 0.05), rot=(0, 0.4, 0))
    box("BandBracket_R", "EngPlateGrey", (0.20, 0.0, 1.62), (0.04, 0.06, 0.05), rot=(0, -0.4, 0))

    # Earmuff Ear-cups on both sides
    for side, ex in [("L", -0.26), ("R", 0.26)]:
        # Inner ear cushion
        cylinder(f"MuffCushion_{side}", "EngHeadsetDark", (ex, 0.0, 1.46), 0.09, 0.04, segments=8, rot=(0, math.pi/2, 0))
        # Outer Gold/Brass Metallic Ring
        cylinder(f"MuffGoldRing_{side}", "EngHeadsetGold", (ex + (-0.025 if side == "L" else 0.025), 0.0, 1.46), 0.082, 0.025, segments=8, rot=(0, math.pi/2, 0))
        # Outer Metallic Housing Center Cap
        cylinder(f"MuffOuterCap_{side}", "EngPlateGrey", (ex + (-0.04 if side == "L" else 0.04), 0.0, 1.46), 0.065, 0.02, segments=8, rot=(0, math.pi/2, 0))
        # Cyan power status pip on headset cup
        box(f"MuffLED_{side}", "EngNeonCyan", (ex + (-0.052 if side == "L" else 0.052), 0.0, 1.46), (0.01, 0.02, 0.02))


# -------------------------------------------------------------
# 7. POLYHEDRAL TECH SCEPTER (多面體科技權杖)
# -------------------------------------------------------------

def build_tech_scepter():
    # Held in right hand, planted straight on the ground:
    # x = -0.48, y = -0.22, base at ground z = 0.0, height to die top ~ 1.25
    sx, sy = -0.48, -0.22
    
    # 1. Base Foot / Metallic Ground Spike
    cylinder("ScepterBaseRing", "EngScepterGold", (sx, sy, 0.02), 0.045, 0.04, segments=8)
    cylinder("ScepterRubberFoot", "EngGloveDark", (sx, sy, 0.005), 0.04, 0.015, segments=8)

    # 2. Main Metallic Shaft (Dark slate alloy)
    shaft_len = 0.88
    shaft_z = 0.48
    cylinder("ScepterShaft", "EngScepterShaft", (sx, sy, shaft_z), 0.030, shaft_len, segments=8)
    # Shaft decorative metal rings
    for rz in [0.24, 0.48, 0.72]:
        cylinder(f"ShaftRing_{rz}", "EngScepterBrass", (sx, sy, rz), 0.034, 0.02, segments=8)

    # 3. GLOWING CYAN LADDER LED PROGRESS DISPLAY (科技光柱數據刻度)
    # Segmented glowing cyan ticks on the front face of the shaft
    for k in range(16):
        lz = 0.16 + k * 0.045
        box(f"ScepterLED_{k}", "EngNeonCyan", (sx, sy - 0.031, lz), (0.018, 0.006, 0.020))

    # 4. Upper Support Collar & Gear Assembly Bracket
    collar_z = 0.94
    cylinder("ScepterCollar", "EngScepterBrass", (sx, sy, collar_z), 0.05, 0.05, segments=8)
    box("ScepterBracket", "EngPlateGrey", (sx, sy, collar_z + 0.04), (0.08, 0.08, 0.03))

    # 5. Interlocking Brass/Gold Mechanical Gears (機械齒輪組)
    # Main Lower Gear (8-toothed cog)
    gear1_r = 0.075
    gear1_z = 1.01
    cylinder("Gear1_Hub", "EngScepterGold", (sx, sy, gear1_z), gear1_r, 0.022, segments=12, rot=(math.pi/2, 0, 0))
    for i in range(8):
        ang = i * math.pi / 4
        tx = sx + (gear1_r + 0.01) * math.cos(ang)
        tz = gear1_z + (gear1_r + 0.01) * math.sin(ang)
        box(f"Gear1_Tooth_{i}", "EngScepterGold", (tx, sy, tz), (0.02, 0.022, 0.02), rot=(0, -ang, 0))

    # Secondary Smaller Interlocking Gear (offset slightly)
    gear2_r = 0.048
    gear2_x = sx + 0.06
    gear2_z = 1.07
    cylinder("Gear2_Hub", "EngScepterBrass", (gear2_x, sy + 0.01, gear2_z), gear2_r, 0.018, segments=10, rot=(math.pi/2, 0, 0))
    for i in range(6):
        ang = i * math.pi / 3
        tx = gear2_x + (gear2_r + 0.008) * math.cos(ang)
        tz = gear2_z + (gear2_r + 0.008) * math.sin(ang)
        box(f"Gear2_Tooth_{i}", "EngScepterBrass", (tx, sy + 0.01, tz), (0.016, 0.018, 0.016), rot=(0, -ang, 0))

    # 6. Tech Lattice Struts / Cage supporting the Floating Die
    cage_z = 1.08
    for ang in [0, math.pi/2, math.pi, 3*math.pi/2]:
        cx = sx + 0.045 * math.cos(ang)
        cy = sy + 0.045 * math.sin(ang)
        tube(f"CageStrut_{ang}", "EngSteelTool", (cx, cy, collar_z + 0.05), (sx + 0.03 * math.cos(ang), sy + 0.03 * math.sin(ang), cage_z + 0.06), 0.008, segments=4)

    # 7. GOLDEN POLYHEDRAL DIE (多面體黃金骰子權杖頭)
    die_z = 1.18
    # 45-degree isometric tilt for dynamic low-poly silhouette
    die_rot = (0.35, 0.35, 0.45)
    box("GoldenDie", "EngScepterGold", (sx, sy, die_z), (0.13, 0.13, 0.13), rot=die_rot)
    
    # Inset contrasting dark pips on faces of the die
    # Face 1 pip (top/front)
    box("DiePip_Center", "EngGloveDark", (sx, sy - 0.07, die_z), (0.025, 0.01, 0.025), rot=die_rot)
    # Face 2 pips
    box("DiePip_L", "EngGloveDark", (sx - 0.035, sy - 0.06, die_z + 0.035), (0.02, 0.01, 0.02), rot=die_rot)
    box("DiePip_R", "EngGloveDark", (sx + 0.035, sy - 0.06, die_z - 0.035), (0.02, 0.01, 0.02), rot=die_rot)


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
    obj.name = "Engineer"
    
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
    print(f"Engineer finished: Dimensions={dims}, Verts={vert_count}, Faces={face_count}, NonManifoldEdges={non_manifold_edges}")
    
    os.makedirs(OUT_DIR, exist_ok=True)
    glb_path = os.path.join(OUT_DIR, "engineer.glb")
    
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


def generate_engineer():
    reset_scene()
    setup_materials()
    build_legs_and_boots()
    build_torso_and_jacket()
    build_backpack_and_tools()
    build_arms_and_hands()
    build_head_and_face()
    build_headset_and_cap()
    build_tech_scepter()
    return finalize_and_export()


if __name__ == "__main__":
    generate_engineer()
