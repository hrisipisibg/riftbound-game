"""Build Frostpeak's original high-pass station and granite trail terrace."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Frostpeak granite and copper"
use_atlas("frostpeak_atlas.png", {
    "stone": (133, 138, 143), "beam": (91, 63, 47), "plaster": (189, 194, 197),
    "clay": (71, 88, 101), "shutters": (58, 108, 113), "amber": (255, 167, 65),
    "iron": (62, 66, 75), "ash": (53, 52, 57), "linen": (164, 97, 59),
})


def roof(width, length, eave, rise):
    angle = math.atan2(rise, length)
    slope = math.hypot(length, rise)
    for side in (-1, 1):
        for i in range(6):
            x = -width + (i + .5) * width / 3
            panel = box("Blue slate roof pan", (x, side * length / 2, eave + rise / 2),
                        (width / 3 + .015, slope + .13, .14), "clay")
            panel.rotation_euler.x = -side * angle
            beam("Raised copper joint", (x + width / 6 - .036, side * length, eave + .07),
                 (x + width / 6 - .036, 0, eave + rise + .07), .066, "linen")
        beam("Heavy carved fascia", (-width - .13, side * length, eave),
             (width + .13, side * length, eave), .19, "beam")
    box("Folded copper ridge", (0, 0, eave + rise + .075), (2 * width + .26, .28, .17), "linen", .04)


def pier(x, y, height, width):
    box("Double-cut granite footing", (x, y, .16), (width + .27, width + .32, .32), "plaster", .075)
    rows = math.ceil((height - .30) / .43)
    for row in range(rows):
        low = .30 + row * .43
        top = min(height, low + .43)
        box("Rock-faced stone course", (x, y, (low + top) / 2),
            (width - (row % 2) * .04, width, top - low), "stone", .058)
    box("Upper capital", (x, y, height + .075), (width + .15, width + .14, .15), "plaster", .042)


def candle_cage(x, y, z):
    box("Copper lamp bottom", (x, y, z - .18), (.33, .32, .10), "linen")
    box("Amber glass", (x, y, z), (.24, .235, .30), "amber")
    for sx in (-1, 1):
        for sy in (-1, 1):
            beam("Thin lamp frame", (x + sx * .138, y + sy * .137, z - .16),
                 (x + sx * .137, y + sy * .137, z + .16), .046, "iron")
    box("Beaten lamp top", (x, y, z + .19), (.35, .34, .10), "linen")


# Mountain forge with shielded hearth, open bay and angled stone shoulders.
box("Forge deck", (0, 0, .145), (7.32, 5.23, .29), "stone", .064)
box("Granite wind wall", (0, 2.13, 1.60), (6.74, .48, 2.76), "stone", .07)
for x in (-3.06, 3.06):
    pier(x, -2.00, 2.74, .53)
    pier(x, 1.94, 2.74, .53)
    box("Low wind break", (x, .75, .80), (.41, 2.19, 1.03), "stone", .06)
    beam("Main bay knee", (x, -1.99, 2.22), (x * .73, -1.99, 2.97), .18)
    candle_cage(x, -2.32, 1.72)
roof(3.58, 2.5, 3.06, 1.03)
box("Squared chimney", (1.52, 1.47, 3.06), (1.39, 1.20, 3.89), "ash", .066)
box("Chimney copper collar", (1.52, 1.47, 4.65), (1.48, 1.30, .15), "linen")
box("Flared granite chimney cap", (1.52, 1.47, 5.05), (1.59, 1.40, .26), "stone", .062)
box("Black hearth surround", (1.51, .74, .86), (2.03, .41, 1.24), "iron", .083)
box("Banked coal face", (1.51, .505, .85), (1.47, .11, .60), "amber", .060)
box("Hearth apron stone", (1.50, .09, .445), (1.83, .80, .28), "clay")
box("Anvil pedestal", (-1.54, .03, .57), (.64, .60, .58), "beam")
box("Anvil head", (-1.54, .03, .95), (.94, .40, .23), "iron", .058)
box("Anvil horn", (-2.06, .03, .98), (.29, .20, .13), "iron", .042)
export("PassForge", "Frostpeak")

# A separate roofed waystop with a windbreak, bench and supplies.
box("Waystop footing", (0, 0, .11), (5.36, 3.55, .22), "stone", .05)
for x in (-2.33, 2.33):
    for y in (-1.48, 1.42):
        pier(x, y, 2.50, .47)
    beam("Waystop roof brace", (x, -1.48, 1.98), (x * .71, -1.48, 2.59), .14)
box("Low rear screen", (0, 1.32, .86), (4.65, .31, 1.30), "stone", .055)
roof(2.61, 1.72, 2.70, .73)
for x in (-1.65, -.35, .98):
    box("Bench stone leg", (x, .87, .39), (.21, .51, .40), "stone")
box("Warm oak seat", (-.34, .88, .63), (3.15, .53, .11), "beam")
box("Carved seat back", (-.34, 1.12, .86), (3.15, .12, .25), "shutters")
cylinder("Sealed supply cask", (1.64, .74, .57), .30, .67, "beam")
for z in (.32, .77):
    cylinder("Supply cask hoop", (1.64, .74, z), .306, .069, "iron")
export("WindbreakShelter", "Frostpeak")

# A 4.4 m corridor rises to a granite deck between low retaining walls.
profile = [(-5.6, .018), (-2.12, .86), (2.12, .86), (5.6, .018)]
verts = [(x, y, z) for y, z in profile for x in (-2.28, 2.28)]
mesh("Continuous granite trail surface", verts,
     [(i*2, i*2+1, i*2+3, i*2+2) for i in range(3)], "stone")
for x in (-2.23, 2.23):
    for (a, h), (b, j) in zip(profile, profile[1:]):
        beam("Retaining ledge coping", (x, a, h + .03), (x, b, j + .03), .17, "plaster")
    for y in (-1.59, -.53, .53, 1.59):
        box("Tall retaining block", (x, y, .422), (.35, 1.05, .84), "stone", .038)
for y in (-1.72, -.86, 0, .86, 1.72):
    for x in (-1.74, 1.74):
        box("Paving iron inset stud", (x, y, .867), (.065, .08, .021), "linen", .008)
export("TrailTerrace", "Frostpeak")

# A carved pass marker combines wayfinding art and a local lantern.
box("Milepost base", (0, 0, .149), (.83, .64, .298), "stone", .073)
box("Carved granite waystone", (0, 0, .94), (.54, .47, 1.40), "plaster", .077)
carve = box("Bronze diamond inlay", (0, -.247, 1.24), (.29, .04, .29), "linen", .019)
carve.rotation_euler.y = math.pi / 4
for i, length in enumerate((.27, .33, .24)):
    box("Incised route line", (0, -.245, .98 - i * .10), (length, .032, .025), "ash", .006)
beam("Milepost lamp bracket", (0, 0, 1.66), (0, -.39, 1.89), .096, "iron")
candle_cage(0, -.40, 1.64)
export("PassMilepost", "Frostpeak")

# Copper-bound storage tucked next to the workshops.
box("Crate landing pad", (0, 0, .095), (2.28, 1.36, .19), "stone")
for x, y, z, s in [(-.52, .13, .49, .61), (.44, .15, .56, .74), (-.52, .11, .93, .42)]:
    box("Packed high-pass crate", (x, y, z), (s, s * .79, s), "beam", .053)
    for offset in (-.30, .30):
        box("Copper crate strap", (x + offset*s, y - s*.406, z), (.085, .035, s * .93), "linen", .014)
export("PackedSupplies", "Frostpeak")

for name, at, yaw in [
    ("PassForge", (-5.9, 2.4, 0), -12), ("TrailTerrace", (0, .5, 0), 0),
    ("WindbreakShelter", (5.0, 2.6, 0), 12), ("PassMilepost", (-2.98, -4.3, 0), -14),
    ("PackedSupplies", (4.95, -.95, 0), 12),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(yaw)
other = assets["PassMilepost"].copy()
bpy.context.collection.objects.link(other)
other.location = (3.1, -3.8, 0)

bpy.ops.mesh.primitive_plane_add(size=150, location=(0, 0, -.023))
rock = bpy.data.materials.new("Pass ground")
rock.diffuse_color = (.255, .27, .29, 1)
bpy.context.object.data.materials.append(rock)
bpy.ops.object.camera_add(location=(13, -21, 13))
camera = bpy.context.object
camera.rotation_euler = (Vector((-.2, 1, 1.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 24.2
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.color_type = "TEXTURE"
shading.light = "STUDIO"
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = "BOTH"
scene.render.resolution_x, scene.render.resolution_y = 1500, 1040
scene.render.resolution_percentage = 100
scene.render.filepath = str(out / "frostpeak-trail-station.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Frostpeak-Trail-Station.blend"))
bpy.ops.render.render(write_still=True)
