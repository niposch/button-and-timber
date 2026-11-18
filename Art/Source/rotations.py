"""Render the three extra quarter-turn views of every placeable furnishing.

Run from the repository root:
  blender --background --python Art/Source/rotations.py
The unrotated sprites come from furniture.py and fixtures.py.
"""
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from furniture import display, register, scene
from fixtures import flowers, pennants, pumpkins, lantern

if __name__ == "__main__":
    initial = bpy.context.scene
    for turn in (1, 2, 3):
        scene("Shelf", display, turn=turn)
        scene("Table", lambda: display(True), turn=turn)
        scene("Register", register, turn=turn)
        for label, prop in (("FlowerCrate", flowers), ("PennantStand", pennants),
                            ("PumpkinCrate", pumpkins), ("SnowLantern", lantern)):
            scene(label, prop, 2.35, turn)
    bpy.data.scenes.remove(initial)
