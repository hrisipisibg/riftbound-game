"""Original Rustwater sorting yard and a raised service crossing."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Rustwater zinc and verdigris"
use_atlas("rustwater_atlas.png", {
    "stone": (108, 100, 90), "beam": (96, 73, 52), "plaster": (166, 135, 85),
    "clay": (102, 110, 99), "shutters": (51, 108, 97), "amber": (244, 165, 58),
    "iron": (68, 76, 79), "ash": (51, 49, 45), "linen": (184, 111, 64),
})


def plate(name, at, dims, key):
    box(name, at, dims, key, .028)
    for sign in (-1, 1):
        x = at[0] + sign * (dims[0] * .5 - .105)
        box("Square rivet head", (x, at[1] - dims[1] * .54, at[2]), (.078, .049, .078), "iron", .012)


def axial_cylinder(name, at, radius, depth, key, sides=16):
    obj = cylinder(name, at, radius, depth, key, sides)
    obj.rotation_euler.y = math.pi / 2
    return obj


def lamp(x, y, z):
    box("Lantern frame", (x, y, z), (.28, .28, .39), "iron", .037)
    for offset in (-.147, .147):
        box("Amber lens", (x, y + offset, z), (.21, .024, .24), "amber", .011)


# Offset sheet-metal workshop with a visible front sorting station.
box("Sorting floor", (0, 0, .13), (7.5, 5.32, .26), "stone")
for x in (-3.35, 3.35):
    for y in (-2.31, 2.30):
        box("Workshop footing", (x, y, .30), (.49, .55, .43), "stone")
        beam("Raked steel column", (x, y, .46), (x * .96, y, 3.33), .23, "iron")
        beam("Rocking frame brace", (x, y, 2.48), (x * .74, y, 3.24), .15, "shutters")
for i in range(7):
    x = (i - 3) * 1.08
    roof = box("Mismatched galvanized roof panel", (x, .02, 3.57), (1.07, 5.52, .13),
               ("clay", "shutters", "clay", "iron")[i % 4])
    roof.rotation_euler.x = .12
    beam("Raised standing seam", (x + .50, -2.68, 3.32), (x + .50, 2.73, 3.97), .073, "iron")
for x in (-2.3, 0, 2.3):
    plate("Flanged back sheet", (x, 2.31, 1.47), (2.25, .15, 2.47), "iron")
    plate("Copper patch", (x + .18, 2.19, 1.85), (1.16, .055, .64), "linen")
for x in (-2.31, 2.31):
    box("Sorting work table", (x, .99, 1.05), (1.63, .88, .17), "beam")
    for dx in (-.58, .58):
        beam("Table trestle", (x + dx, .99, .29), (x + dx, .99, 1.04), .16)
    for j in range(3):
        box("Sorted ingot sample", (x - .48 + j * .46, .98, 1.21), (.39, .25, .14),
            "iron" if j % 2 else "linen")
plate("Workshop lintel sign", (0, -2.37, 3.14), (2.85, .14, .45), "shutters")
for x in (-2.8, 2.8):
    lamp(x, -2.3, 2.48)
export("SortingWorkshop", "Rustwater")

# Ore drum. Center the asset on the axle so it can be animated independently.
for x in (-.63, .63):
    axial_cylinder("Drum end collar", (x, 0, 0), .90, .17, "iron")
    axial_cylinder("Vertical copper inset", (x * 1.14, 0, 0), .68, .041, "linen")
for i in range(12):
    theta = i * math.tau / 12
    y, z = math.cos(theta) * .81, math.sin(theta) * .81
    beam("Long drum stave", (-.62, y, z), (.62, y, z), .153, "shutters" if i % 3 else "iron")
    axial_cylinder("Flange rivet", (.75, y * .89, z * .89), .046, .055, "iron", 8)
axial_cylinder("Axle", (0, 0, 0), .14, 2.09, "iron")
export("SortingDrum", "Rustwater")

# Drum frame and gravity chute. The rotor slot is centered at (0,0,1.47).
box("Drum skid", (0, 0, .14), (3.30, 2.70, .28), "stone")
for x in (-.96, .96):
    for y in (-.71, .71):
        beam("Skid diagonal", (x * 1.34, y, .30), (x, 0, 1.49), .17, "iron")
    box("Axle saddle", (x, 0, 1.42), (.25, .34, .32), "linen")
box("Stamped ore bin", (0, .73, .55), (1.37, .67, .38), "iron")
chute = box("Ore delivery chute", (0, .76, .90), (1.27, 1.14, .12), "shutters")
chute.rotation_euler.x = -.40
export("DrumFrame", "Rustwater")

# Gantry crane with angled truss, gear train, and an unoccupied hook zone.
box("Crane platform", (0, 0, .135), (3.65, 3.28, .27), "stone")
for x in (-1.29, 1.29):
    for y in (.52, 1.25):
        box("Crane foot casting", (x, y, .30), (.47, .54, .32), "iron")
    beam("Crane mast", (x, .99, .35), (x, .81, 4.27), .25, "iron")
    beam("Hoist jib", (x, 1.1, 4.28), (x, -1.65, 4.18), .20, "shutters")
    beam("Jib brace", (x, .76, 2.62), (x, -.89, 4.18), .16, "iron")
    for z in (1.32, 2.36, 3.39):
        plate("Mast band", (x, .74, z), (.37, .21, .13), "linen")
    for y in (.64, -.09, -.82):
        beam("Jib diagonal", (x, y, 4.18), (x, y + .38, 4.67), .105, "iron")
    beam("Jib upper chord", (x, 1.14, 4.68), (x, -1.66, 4.58), .16, "iron")
beam("Outer headstock", (-1.42, -1.62, 4.18), (1.42, -1.62, 4.18), .20, "iron")
axial_cylinder("Hoist spool", (0, -.55, 4.33), .29, 1.26, "linen")
axial_cylinder("Winch gear", (1.41, .38, 1.53), .39, .16, "iron")
for i in range(12):
    angle = i * math.tau / 12
    tooth = box("Winch gear tooth", (1.41, .38 + .40 * math.cos(angle), 1.53 + .40 * math.sin(angle)),
                (.20, .13, .12), "iron", .012)
    tooth.rotation_euler.x = angle
beam("Hook line", (0, -1.64, 4.12), (0, -1.64, 3.02), .045, "iron")
for i in range(6):
    a = i * math.pi / 5
    b = (i + 1) * math.pi / 5
    beam("Rounded hoist hook", (.20 * math.cos(a), -1.64, 2.98 - .20 * math.sin(a)),
         (.20 * math.cos(b), -1.64, 2.98 - .20 * math.sin(b)), .086, "iron")
export("SalvageCrane", "Rustwater")

# Traversable boardwalk, 3.8 m between rails with passive slopes at both ends.
cross_section = [(-5.6, .02), (-2.2, .73), (2.2, .73), (5.6, .02)]
verts = [(x, y, z) for y, z in cross_section for x in (-2.03, 2.03)]
faces = [(i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2) for i in range(len(cross_section) - 1)]
mesh("Continuous walking surface", verts, faces, "beam")
for (start, start_height), (end, end_height) in zip(cross_section, cross_section[1:]):
    slope = (end_height - start_height) / (end - start)
    for i in range(1, round((end - start) / .49)):
        y = start + i * .49
        seam = box("Recessed board seam", (0, y, start_height + (y - start) * slope),
                   (3.88, .023, .007), "ash", bevel=0)
        seam.rotation_euler.x = math.atan(slope)
for y in (-2.12, -.71, .71, 2.12):
    for x in (-1.80, 1.80):
        box("Deck pier", (x, y, .34), (.28, .30, .68), "stone", .023)
for x in (-2.01, 2.01):
    for (a, h), (b, j) in zip(cross_section, cross_section[1:]):
        beam("Side skirt", (x, a, max(.04, h - .10)), (x, b, max(.04, j - .10)), .15, "shutters")
    for y in (-2.14, 0, 2.14):
        beam("Rail post", (x, y, .64), (x, y, 1.61), .13, "iron")
    beam("Upper handrail", (x, -2.14, 1.56), (x, 2.14, 1.56), .14, "shutters")
    beam("Lower handrail", (x, -2.14, 1.14), (x, 2.14, 1.14), .10, "iron")
for y in (-2.0, -1.33, -.66, 0, .66, 1.33, 2.0):
    for x in (-1.76, 1.76):
        box("Secured plank pin", (x, y, .738), (.055, .055, .016), "iron", .008)
export("ServiceCrossing", "Rustwater")

# Sorted scrap bins and bent copper pipe as a recognizable unloading marker.
for x in (-.71, .71):
    plate("Scrap bin front", (x, -.46, .37), (1.22, .13, .63), "shutters")
    plate("Scrap bin rear", (x, .46, .37), (1.22, .13, .63), "iron")
    for dx in (-.57, .57):
        box("Scrap bin side", (x + dx, 0, .37), (.13, .94, .63), "iron")
    for i in range(4):
        scrap = box("Metal offcut", (x + (i - 1.5) * .23, (i % 2 - .5) * .45, .64 + i * .057),
                    (.38, .59, .12), "linen" if i % 2 else "iron")
        scrap.rotation_euler.z = i * .64
for a, b in [((-1.25, .18, .74), (-1.25, .18, 1.30)), ((-1.25, .18, 1.30), (-.69, .18, 1.30)),
             ((-.69, .18, 1.30), (-.50, .18, 1.03))]:
    beam("Recovered copper pipe", a, b, .13, "linen")
export("SortedScrapBins", "Rustwater")

for name, at, yaw in [
    ("SortingWorkshop", (-6.4, 3.25, 0), -7), ("DrumFrame", (-5.3, -1.35, 0), 0),
    ("SortingDrum", (-5.3, -1.35, 1.47), 0), ("SalvageCrane", (5.45, 2.65, 0), -18),
    ("ServiceCrossing", (0, 2.5, 0), 0), ("SortedScrapBins", (5.45, -.72, 0), -15),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(yaw)

bpy.ops.mesh.primitive_plane_add(size=160, location=(0, 0, -.015))
earth = bpy.data.materials.new("Rustwater earth")
earth.diffuse_color = (.32, .25, .20, 1)
bpy.context.object.data.materials.append(earth)
bpy.ops.object.camera_add(location=(15, -23, 15.5))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 1.7, 1.4)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 26
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.color_type = "TEXTURE"
shading.light = "STUDIO"
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = "BOTH"
scene.render.resolution_x, scene.render.resolution_y = 1560, 1040
scene.render.resolution_percentage = 100
scene.render.filepath = str(out / "rustwater-salvage-yard.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Rustwater-Salvage-Yard.blend"))
bpy.ops.render.render(write_still=True)
