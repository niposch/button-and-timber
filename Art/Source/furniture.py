"""Render the furniture sprites with Blender from the repository root."""
from pathlib import Path
import math
import sys

import bpy


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Furniture"
OUT.mkdir(exist_ok=True)


def material(name, color):
    value = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    value = [(c / 12.92 if c < .04045 else ((c + .055) / 1.055) ** 2.4) for c in value]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*value, 1)
    return mat


WOOD = material("Honeyed maple", "b8875d")
EDGE = material("Walnut trim", "9b6148")
GOLD = material("Brass", "e7ba6d")
CREAM = material("Ivory", "e4c79a")
GREEN = material("Enamel sage", "63877c")
TEAL = material("Dark teal", "365659")
RED = material("Painted coral", "b46955")
IRON = material("Blue steel", "7f9d9c")
INK = material("Ink", "354648")


def box(name, x, y, z, sx, sy, sz, mat, bevel=.025, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler.z = angle
    obj.data.materials.append(mat)
    if bevel:
        edge = obj.modifiers.new("Soft worn edges", "BEVEL")
        edge.width = bevel
        edge.segments = 2
        obj.modifiers.new("Face normals", "WEIGHTED_NORMAL")
    return obj


def display(table=False):
    height = .58 if table else .67
    for x in (-.32, .32):
        for y in (-.36, .36):
            box("Maple leg", x, y, height / 2, .08, .08, height, EDGE)
            box("Painted foot", x, y, .07, .087, .087, .12, TEAL)
    box("Display surface", 0, 0, height, .84, .94, .1, RED if table else GREEN)
    box("Maple inset", 0, 0, height + .052, .71, .81, .017, WOOD, .012)
    if not table:
        box("Lower shelf", 0, 0, .22, .74, .82, .085, WOOD)
        box("Back", -.30, 0, .48, .075, .85, .79, WOOD)
        box("Back cap", -.3, 0, .887, .13, .92, .06, GREEN)
        for index in range(6):
            box("Peg", -.254, -.33 + index * .132, .78, .025, .026, .025, GOLD, .004)
    box("Ticket holder", .425, 0, height, .022, .22, .086, EDGE, .006)
    box("Paper ticket", .439, 0, height, .012, .172, .056, CREAM, .002)


def register():
    box("Kickboard", 0, 0, .08, .73, .86, .16, TEAL)
    box("Counter body", 0, 0, .365, .73, .87, .62, GREEN)
    box("Maple top", 0, 0, .69, .86, .96, .085, WOOD)
    for y in (-.265, -.13, .005, .14, .275):
        box("Fluting", .372, y, .32, .035, .045, .49, CREAM, .008)
    box("Till drawer", -.015, .15, .776, .40, .43, .09, TEAL)
    box("Till keypad", -.015, .15, .839, .32, .33, .05, IRON)
    for x in (-.09, 0, .09):
        for y in (.075, .16, .245):
            box("Till key", x, y, .876, .037, .037, .02, CREAM, .007)
    box("Display bezel", -.175, .15, .894, .075, .34, .15, TEAL)
    box("Display glass", -.134, .15, .908, .015, .248, .075, GOLD, .007)
    box("Coin dish", .075, -.228, .755, .19, .26, .043, RED)
    box("Dish liner", .075, -.228, .78, .14, .21, .009, CREAM, .003)


def workbench():
    for x in (-.335, .335):
        for y in (-.91, .91):
            box("Leg", x, y, .31, .105, .115, .62, EDGE)
    box("Apron", 0, 0, .55, .7, 1.96, .21, EDGE)
    box("Maple worktop", 0, 0, .695, .99, 2.17, .15, WOOD)
    box("Drawer", .361, .25, .57, .033, .64, .132, WOOD, .013)
    box("Drawer pull", .387, .25, .568, .047, .19, .03, GOLD, .012)
    box("Cutting mat", -.07, -.52, .783, .66, .64, .018, GREEN, .008, -.07)
    for i in range(5):
        box("Mat rule", -.29 + i * .115, -.52, .794, .009, .55, .003, CREAM, .001)
    box("Tray", -.17, .7, .81, .39, .44, .08, TEAL, .022)
    for i in range(3):
        box("Offcut", -.17 + i * .085, .7, .869, .061, .23, .052, CREAM, .007, i * .15)


def cabinet():
    box("Plinth", 0, 0, .065, .78, .96, .13, TEAL)
    box("Cabinet", 0, 0, .46, .74, .9, .82, GREEN)
    box("Wood cap", 0, 0, .886, .81, .97, .086, WOOD)
    for y in (-.23, .23):
        box("Door", .381, y, .458, .057, .421, .61, GREEN)
        box("Trim inset", .417, y, .455, .018, .297, .475, TEAL, .018)
        box("Brass pull", .438, y * .43, .48, .037, .034, .091, GOLD, .014)


def tool(index):
    if index == 0:
        box("Saw blade", 0, 0, .23, .22, .8, .046, IRON, .013)
        for i in range(6):
            box("Saw tooth", .113, -.319 + i * .109, .228, .077, .067, .033, IRON, .005, math.pi / 4)
        box("Saw handle", 0, -.47, .245, .29, .28, .11, RED)
        box("Handle grip", 0, -.562, .29, .22, .1, .11, WOOD)
    elif index == 1:
        box("Mallet handle", 0, -.098, .17, .111, .87, .1, WOOD)
        box("Mallet head", 0, .287, .23, .55, .31, .31, EDGE, .055)
        for x in (-.222, .222):
            box("Mallet bands", x, .287, .23, .064, .31, .31, GOLD)
    elif index == 2:
        box("Brush handle", 0, -.18, .17, .15, .68, .1, GREEN)
        box("Brush ferrule", 0, .18, .174, .26, .16, .106, GOLD)
        for i in range(5):
            box("Bristles", -.1 + i * .05, .35, .166, .048, .25, .077, CREAM)
        box("Paint dipped tip", 0, .43, .175, .245, .082, .079, RED)
    else:
        box("Needle", 0, .2, .21, .052, .66, .043, IRON, .011, .22)
        for y in (-.32, -.04):
            box("Spool cap", .02, y, .225, .32, .065, .25, CREAM, .037)
        box("Spool thread", .02, -.18, .225, .27, .26, .21, RED, .034)
        for i in range(5):
            box("Thread ridges", .155, -.28 + i * .047, .225, .028, .018, .163, EDGE, .006)


def scene(name, source, span=2.9):
    bpy.ops.scene.new(type="NEW")
    current = bpy.context.scene
    current.name = name
    source()
    cam = bpy.data.objects.new("Isometric sprite camera", bpy.data.cameras.new("Isometric"))
    current.collection.objects.link(cam)
    cam.location = (6, 6, math.sqrt(24))
    cam.rotation_euler = (-cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span
    current.camera = cam
    current.render.engine = "BLENDER_WORKBENCH"
    current.render.film_transparent = True
    current.render.image_settings.color_mode = "RGBA"
    current.render.resolution_x = 192
    current.render.resolution_y = 192
    current.render.resolution_percentage = 100
    current.render.filepath = str(OUT / (name + ".png"))
    shading = current.display.shading
    shading.light = "STUDIO"
    shading.studiolight_rotate_z = .45
    shading.color_type = "MATERIAL"
    shading.show_cavity = True
    shading.cavity_type = "BOTH"
    shading.cavity_ridge_factor = .7
    shading.cavity_valley_factor = 1.15
    shading.show_shadows = True
    current.view_settings.view_transform = "Standard"
    current.view_settings.exposure = .72
    bpy.ops.render.render(write_still=True)
    current.render.filepath = "//../Furniture/" + name + ".png"


if __name__ == "__main__":
    initial = bpy.context.scene
    scene("Shelf", display)
    scene("Table", lambda: display(True))
    scene("Register", register)
    scene("Bench", workbench, 3.4)
    scene("Cabinet", cabinet)
    for idx, label in enumerate(("Saw", "Mallet", "Brush", "Needle")):
        scene(label, lambda i=idx: tool(i), 1.48)
    bpy.data.scenes.remove(initial)
    if "--save-source" in sys.argv:
        bpy.context.window.scene = bpy.data.scenes["Shelf"]
        bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "Source" / "Furniture.blend"), compress=True)
