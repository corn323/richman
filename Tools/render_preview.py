"""
4-View Turnaround Preview Renderer for Blender Models.
Generates 772x772 composite preview image (Front, 3/4 Iso, Side, Back) with 4px dark border.
"""

import bpy
import math
import os
import subprocess
from mathutils import Vector

OUT_DIR = r"C:\Users\User\Desktop\code\richman\assets\models"
TEMP_DIR = r"C:\Users\User\AppData\Local\Temp\blender_previews"

def setup_render_environment():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 384
    scene.render.resolution_y = 384
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    
    # World background color
    if not scene.world:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg_node = scene.world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs["Color"].default_value = (0.247, 0.247, 0.247, 1.0)
        bg_node.inputs["Strength"].default_value = 1.0

    # Ensure Camera
    cam_obj = bpy.data.objects.get("PreviewCam")
    if not cam_obj:
        cam_data = bpy.data.cameras.new("PreviewCam")
        cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
        bpy.context.collection.objects.link(cam_obj)
    cam_obj.data.lens = 50.0
    scene.camera = cam_obj

    # 3-Point Lighting
    lights_config = [
        ("KeyLight", (3.0, -3.5, 4.0), 300.0, (1.0, 0.96, 0.90)),
        ("FillLight", (-3.5, -2.5, 2.5), 150.0, (0.85, 0.92, 1.0)),
        ("RimLight", (2.0, 3.5, 3.5), 350.0, (1.0, 0.98, 0.92)),
    ]
    for name, loc, power, col in lights_config:
        l_obj = bpy.data.objects.get(name)
        if not l_obj:
            l_data = bpy.data.lights.new(name, type='POINT')
            l_obj = bpy.data.objects.new(name, l_data)
            bpy.context.collection.objects.link(l_obj)
        l_obj.location = loc
        l_obj.data.energy = power
        l_obj.data.color = col

    return cam_obj


def look_at(cam_obj, target_pos):
    direction = Vector(target_pos) - cam_obj.location
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam_obj.rotation_euler = rot_quat.to_euler()


def render_character_preview(target_name):
    target_obj = bpy.data.objects.get(target_name)
    if not target_obj:
        print(f"Object {target_name} not found!")
        return None

    # Hide all other meshes during render
    saved_hide = {}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            saved_hide[o.name] = o.hide_render
            o.hide_render = (o.name != target_name)
        elif o.type == 'LIGHT' and o.name in ["Light"]:
            o.hide_render = True

    # Temporarily center target object at (0, 0, 0)
    orig_loc = target_obj.location.copy()
    target_obj.location = (0, 0, 0)

    cam_obj = setup_render_environment()
    target_center = (0.0, 0.0, 0.92)

    angles = [
        ("front", (0.0, -3.4, 0.95)),
        ("iso", (2.4, -2.4, 1.40)),
        ("side", (-3.4, 0.0, 0.95)),
        ("back", (0.0, 3.4, 0.95)),
    ]

    os.makedirs(TEMP_DIR, exist_ok=True)
    rendered_paths = {}

    scene = bpy.context.scene
    for label, cam_loc in angles:
        cam_obj.location = cam_loc
        look_at(cam_obj, target_center)
        out_path = os.path.join(TEMP_DIR, f"{target_name.lower()}_{label}.png")
        scene.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        rendered_paths[label] = out_path

    # Restore object locations & visibility
    target_obj.location = orig_loc
    for name, hide_val in saved_hide.items():
        o = bpy.data.objects.get(name)
        if o:
            o.hide_render = hide_val

    final_path = os.path.join(OUT_DIR, f"{target_name.lower()}_preview.png")

    # Composite into 772x772 with 4px black borders
    composite_py = f"""
from PIL import Image
composite = Image.new("RGBA", (772, 772), (20, 20, 20, 255))
im_front = Image.open(r'{rendered_paths["front"]}')
im_iso = Image.open(r'{rendered_paths["iso"]}')
im_side = Image.open(r'{rendered_paths["side"]}')
im_back = Image.open(r'{rendered_paths["back"]}')

composite.paste(im_front, (0, 0))
composite.paste(im_iso, (388, 0))
composite.paste(im_side, (0, 388))
composite.paste(im_back, (388, 388))

composite.save(r'{final_path}')
print("Composite successfully saved to:", r'{final_path}')
"""
    try:
        from PIL import Image
        composite = Image.new("RGBA", (772, 772), (20, 20, 20, 255))
        im_front = Image.open(rendered_paths["front"])
        im_iso = Image.open(rendered_paths["iso"])
        im_side = Image.open(rendered_paths["side"])
        im_back = Image.open(rendered_paths["back"])

        composite.paste(im_front, (0, 0))
        composite.paste(im_iso, (388, 0))
        composite.paste(im_side, (0, 388))
        composite.paste(im_back, (388, 388))
        composite.save(final_path)
    except ImportError:
        subprocess.run(["python", "-c", composite_py], check=True)

    print(f"Generated turnaround preview at: {final_path}")
    return final_path


if __name__ == "__main__":
    render_character_preview("Builder")
