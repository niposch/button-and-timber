"""Bake original workshop equipment using the furniture sprite camera."""
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from furniture import box, scene, ROOT, WOOD, CREAM, GREEN, TEAL, RED, INK, IRON, GOLD


def table():
    for x in (-.28, .28):
        for y in (-.37, .37):
            box("Leg", x, y, .29, .085, .085, .58, TEAL)
    box("Maple top", 0, 0, .583, .78, 1.00, .105, WOOD)


def sewing():
    table()
    box("Machine base", -.04, 0, .675, .51, .68, .079, GREEN)
    box("Upright", -.09, -.236, .83, .28, .18, .27, CREAM)
    box("Sewing arm", -.09, -.026, .954, .28, .60, .15, CREAM, .052)
    box("Presser foot", -.09, .247, .747, .07, .07, .18, IRON, .006)
    box("Spool", -.09, -.163, 1.066, .091, .098, .085, RED, .018)
    box("Flywheel", -.088, -.338, .934, .21, .077, .18, GREEN, .034)
    box("Cloth", .069, .198, .67, .38, .33, .03, RED, .008, .12)


def assembly():
    table()
    box("Weighted plate", -.034, 0, .661, .54, .63, .08, IRON)
    box("Pedestal", -.225, -.15, .9, .103, .11, .54, INK)
    box("Drill motor", -.185, .017, 1.145, .27, .42, .19, GREEN, .048)
    box("Chuck", -.188, .138, 1.001, .105, .10, .14, IRON, .018)
    box("Bit", -.188, .138, .883, .029, .028, .13, GOLD, .003)
    box("Handle", .009, -.064, 1.13, .22, .029, .029, IRON, .007)
    box("Grip", .13, -.064, 1.132, .061, .067, .065, RED, .015)
    box("Vice", .035, .069, .75, .26, .31, .10, TEAL)


def finishing():
    table()
    box("Protective mat", .044, .001, .647, .59, .65, .026, GREEN, .008)
    for index, color in enumerate((RED, GOLD, TEAL)):
        y = -.313 + .206 * index
        box("Paint pot", -.24, y, .734, .144, .14, .18, color, .038)
        box("Pot lid", -.24, y, .833, .155, .154, .032, CREAM, .01)
    box("Paint roller", .207, .254, .691, .071, .24, .083, CREAM, .014)
    box("Roller grip", .243, .07, .674, .061, .16, .038, RED, .009)


if __name__ == "__main__":
    original = bpy.context.scene
    for label, machine in (("Sewing", sewing), ("Assembly", assembly), ("Finishing", finishing)):
        scene(label, machine, 2.55)
    bpy.data.scenes.remove(original)
    bpy.context.window.scene = bpy.data.scenes["Sewing"]
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "Source/Workshop.blend"), compress=True)
