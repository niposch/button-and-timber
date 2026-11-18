"""Bake CC0 character walk cycles and houseplants from the original pack ZIPs."""
from array import array
from pathlib import Path
import math
import sys
from zipfile import ZipFile

import bpy
from mathutils import Vector


ART = Path(__file__).resolve().parents[1]
SCRATCH = ART.parent / ".godot" / "sprite-bake"
OUTPUT = ART / "Furniture"
SCRATCH.mkdir(parents=True, exist_ok=True)
OUTPUT.mkdir(exist_ok=True)
SIZE = 112
PEOPLE = ("Casual_Female", "Casual_Male", "Casual2_Female", "Casual2_Male",
          "OldClassy_Female", "OldClassy_Male", "Casual3_Female", "Casual3_Male")
SKIN_TONES = ("e2aa84", "be835d", "986046", "dba685",
              "a86d53", "e9bf9a", "c88964", "bc795a")


def setup(name, target, span, size):
    bpy.ops.scene.new(type="NEW")
    scene = bpy.context.scene
    scene.name = name
    camera = bpy.data.objects.new("Sprite camera", bpy.data.cameras.new("Orthographic"))
    scene.collection.objects.link(camera)
    camera.location = Vector((6, 6, math.sqrt(24))) + target
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = span
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 1.0
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_cavity = True
    shading.cavity_type = "WORLD"
    shading.cavity_ridge_factor = .45
    shading.cavity_valley_factor = .6
    return scene


def load_model(scene, filename, height):
    bpy.ops.import_scene.gltf(filepath=str(filename))
    imported = [obj for obj in scene.objects if obj != scene.camera]
    arm = next((obj for obj in imported if obj.type == "ARMATURE"), None)
    if arm:
        arm.animation_data.use_nla = False
        arm.animation_data.action = None
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in imported if obj.type == "MESH"
              for corner in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    root = bpy.data.objects.new("Ground pivot", None)
    scene.collection.objects.link(root)
    for obj in imported:
        if obj.parent is None:
            obj.parent = root
    center = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    scale = height / (high.z - low.z)
    root.scale = (scale, scale, scale)
    root.location = -center * scale
    return arm, root, center * scale


def linear_color(color):
    gamma = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return (*[c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
              for c in gamma], 1)


def apply_skin_tone(scene, color):
    scene.view_settings.exposure = .62
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.cavity_ridge_factor = .19
    scene.display.shading.cavity_valley_factor = .32
    palette = {"Skin": linear_color(color), "Face": linear_color("352725")}
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if material and (part := material.name.split(".")[0]) in palette:
                material.diffuse_color = palette[part]


def characters(archive):
    prefix = "glTF/"
    for number, model in enumerate(PEOPLE):
        scene = setup(model, Vector((0, 0, .54)), 2.23, SIZE)
        animations_before = set(bpy.data.actions)
        path = Path(archive.extract(prefix + model + ".gltf", SCRATCH))
        arm, pivot, center = load_model(scene, path, 1.12)
        apply_skin_tone(scene, SKIN_TONES[number])
        actions = {action.name.split(".")[0]: action for action in bpy.data.actions
                   if action not in animations_before}
        idle, walk = actions["Idle"], actions["Walk"]
        width = SIZE * 8
        pixels = array("f", [0]) * (width * SIZE * 4 * 4)
        image_path = SCRATCH / "frame.png"
        scene.render.filepath = str(image_path)
        for row in range(4):
            pivot.rotation_euler.z = math.pi * .5 * row
            pivot.location = -(pivot.rotation_euler.to_matrix() @ center)
            for col in range(8):
                action = idle if col == 0 else walk
                arm.animation_data.action = action
                start, end = action.frame_range
                scene.frame_set(round(start + (end - start) * ((col - 1) / 7 if col else 0)))
                bpy.ops.render.render(write_still=True)
                image = bpy.data.images.load(str(image_path), check_existing=False)
                frame = array("f", [0]) * (SIZE * SIZE * 4)
                image.pixels.foreach_get(frame)
                bpy.data.images.remove(image)
                for line in range(SIZE):
                    src = line * SIZE * 4
                    dst = (((3 - row) * SIZE + line) * width + col * SIZE) * 4
                    pixels[dst:dst + SIZE * 4] = frame[src:src + SIZE * 4]
        atlas = bpy.data.images.new(model, width, SIZE * 4, alpha=True)
        atlas.pixels.foreach_set(pixels)
        atlas.filepath_raw = str(OUTPUT / f"Person{number}.png")
        atlas.file_format = "PNG"
        atlas.save()
        bpy.data.images.remove(atlas)


def plants(archive):
    prefix = next(entry.rsplit("/", 1)[0] + "/" for entry in archive.namelist()
                  if entry.endswith("/gltf/cactus_small_A.gltf"))
    archive.extract(prefix + "furniturebits_texture.png", SCRATCH)
    for label, suffix in (("Plant", "small_A"), ("PlantTall", "medium_B")):
        model = f"cactus_{suffix}"
        for extension in (".gltf", ".bin"):
            archive.extract(prefix + model + extension, SCRATCH)
        scene = setup(label, Vector((0, 0, 0)), 1.5, 128)
        load_model(scene, SCRATCH / (prefix + model + ".gltf"), .55 if label == "Plant" else .75)
        scene.render.filepath = str(OUTPUT / (label + ".png"))
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    characters_zip, plants_zip = sys.argv[sys.argv.index("--") + 1:]
    with ZipFile(plants_zip) as furniture:
        plants(furniture)
    with ZipFile(characters_zip) as people:
        characters(people)
