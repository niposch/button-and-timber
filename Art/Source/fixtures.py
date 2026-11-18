"""Bake the shop decorations and workshop carts with the game's isometric camera."""
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from furniture import box, material, scene, ROOT, WOOD, EDGE, GOLD, CREAM, GREEN, TEAL, RED, IRON

LEAF = material("Moss leaf", "6a8b55")
CORAL = material("Petal coral", "d78079")
ORANGE = material("Pumpkin orange", "d88d4d")
WAX = material("Lantern glass", "edc879")


def round_prop(name, x, y, z, a, b, c, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, location=(x, y, z))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (a, b, c)
    obj.data.materials.append(mat)


def flowers():
    box("Planter", 0, 0, .16, .66, .76, .31, EDGE)
    box("Painted rim", 0, 0, .31, .70, .80, .08, CREAM)
    box("Soil", 0, 0, .355, .59, .66, .02, EDGE)
    for i, (x, y, h) in enumerate(((-.14, -.18, .65), (.12, -.14, .58), (-.10, .15, .74), (.16, .15, .62))):
        box("Stem", x, y, (h + .35) / 2, .03, .032, h - .35, LEAF, .005)
        round_prop("Leaf", x - .043, y, h - .15, .1, .046, .042, GREEN)
        for n in range(5):
            angle = n * math.tau / 5
            round_prop("Petal", x + .065 * math.cos(angle), y + .065 * math.sin(angle), h,
                       .061, .061, .038, CORAL if i % 2 == 0 else CREAM)
        round_prop("Flower center", x, y, h + .029, .042, .042, .022, GOLD)


def pennants():
    box("Stand", 0, 0, .073, .62, .76, .14, TEAL)
    for y in (-.33, .33):
        box("Pole", 0, y, .56, .075, .077, 1.02, WOOD)
        round_prop("Finial", 0, y, 1.088, .072, .072, .072, GOLD)
    for i, color in enumerate((RED, GOLD, GREEN, CREAM, RED)):
        y = -.26 + i * .13
        z = .94 + abs(y) * .22
        mesh = bpy.data.meshes.new("Pennant")
        mesh.from_pydata([(0, y - .057, z), (0, y + .057, z), (.022, y, z - .21)], [], [(0, 1, 2)])
        flag = bpy.data.objects.new("Cloth flag", mesh)
        bpy.context.scene.collection.objects.link(flag)
        flag.data.materials.append(color)
        flag.modifiers.new("Cloth thickness", "SOLIDIFY").thickness = .019


def pumpkins():
    box("Apple crate", 0, 0, .15, .71, .74, .28, WOOD)
    box("Dark rim", 0, 0, .279, .75, .78, .049, EDGE)
    for x, y, z, radius in ((-.12, -.12, .44, .24), (.16, .17, .398, .19)):
        for i in range(5):
            a = i * math.tau / 5
            round_prop("Pumpkin rib", x + .061 * math.cos(a), y + .061 * math.sin(a), z,
                       radius * .72, radius * .72, radius * .68, ORANGE)
        box("Twisted stem", x, y, z + radius * .78, .059, .063, .108, LEAF)


def lantern():
    box("Lantern plinth", 0, 0, .12, .60, .66, .23, GREEN)
    box("Iron base", 0, 0, .25, .48, .52, .08, TEAL)
    box("Amber glass", 0, 0, .506, .39, .43, .44, WAX)
    for x in (-.21, .21):
        for y in (-.23, .23):
            box("Window frame", x, y, .508, .045, .045, .49, TEAL)
    box("Snow cap", 0, 0, .76, .57, .61, .13, CREAM, .062)
    box("Handle", 0, 0, .89, .11, .21, .12, TEAL, .04)


def cart(tier):
    length = .70 + tier * .16
    paint = (WOOD, GREEN, TEAL)[tier]
    box("Deck", 0, 0, .255, .57, length, .12, paint)
    for x in (-.25, .25):
        for y in (-length * .34, length * .34):
            round_prop("Caster", x, y, .136, .092, .068, .1, TEAL)
            box("Caster bracket", x, y, .198, .08, .07, .12, IRON)
    for x in (-.21, .21):
        box("Handle stay", x, -length * .45, .46, .045, .045, .50, paint)
    box("Handle grip", 0, -length * .45, .71, .48, .067, .067, EDGE)
    for i in range(1 + tier):
        box("Parts crate", 0, .15, .386 + i * .13, .44 - i * .045, .39, .14, WOOD if i % 2 == 0 else CREAM)


if __name__ == "__main__":
    original = bpy.context.scene
    for label, prop in (("FlowerCrate", flowers), ("PennantStand", pennants),
                        ("PumpkinCrate", pumpkins), ("SnowLantern", lantern)):
        scene(label, prop, 2.35)
    for level in range(3):
        scene("Cart" + str(level), lambda tier=level: cart(tier), 2.1)
    bpy.data.scenes.remove(original)
    bpy.context.window.scene = bpy.data.scenes["FlowerCrate"]
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "Source/Fixtures.blend"), compress=True)
