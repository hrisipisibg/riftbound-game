"""Original Westbank riverside kit, including a shore ramp and a moored skiff."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Westbank cedar and willow"
use_atlas("westbank_atlas.png", {
    "stone": (114, 122, 115), "beam": (110, 79, 52), "plaster": (179, 153, 112),
    "clay": (74, 94, 83), "shutters": (67, 109, 73), "amber": (239, 170, 75),
    "iron": (66, 69, 68), "ash": (59, 54, 45), "linen": (171, 123, 64),
})


def post(x, y, low, high, crust=False):
    box("Squared cedar pile", (x, y, (low + high) / 2), (.18, .20, high - low), "beam", .042)
    box("Iron pile cap", (x, y, high + .018), (.202, .222, .060), "iron", .013)
    if crust:
        box("Weathered river band", (x, y, -.65), (.196, .212, .24), "shutters", .023)


# A landing with open shore end, three visible timber trestles and low rails.
box("Continuous landing deck", (0, 0, -.105), (4.42, 5.6, .21), "beam", .015)
for i in range(15):
    y = -2.61 + i * .371
    box("Dressed landing board", (0, y, -.036), (4.30, .356, .077), "beam", .023, 1)
for y in (-2.06, 0, 2.08):
    for x in (-2.02, 2.02):
        post(x, y, -3.45, .93, crust=True)
        beam("Pile knee diagonal", (x, y, -1.11), (x * .70, y, -.12), .17)
    beam("Transverse pile cap", (-2.15, y, -.22), (2.15, y, -.22), .26)
for x in (-2.09, 2.09):
    beam("Outer dock runner", (x, -2.8, -.19), (x, 2.8, -.19), .18)
    beam("Landward hand rail", (x, -2.05, .83), (x, .6, .83), .13)
    # The outboard bay has raised posts and a lower rail above the water.
    beam("Mooring-bay rail", (x, .59, .47), (x, 2.59, .47), .10)
    post(x, 2.62, -.23, .97)
beam("Outboard safety rail", (-2.1, 2.62, .85), (2.1, 2.62, .85), .13)
beam("Outboard lower rail", (-2.1, 2.62, .47), (2.1, 2.62, .47), .11)
for x in (-1.72, 1.72):
    box("Mooring cleat stem", (x, 1.72, .14), (.095, .09, .28), "iron")
    box("Mooring cleat arms", (x, 1.72, .27), (.30, .087, .075), "iron")
export("LandingDock", "Westbank")

# Landward end y=-3.6 is high. Scale only the height to match the sampled shore.
verts = [(-2.12, -3.6, 1.54), (2.12, -3.6, 1.54), (2.12, 3.6, 0), (-2.12, 3.6, 0)]
mesh("Unbroken ramp deck", verts, [(0, 1, 2, 3)], "beam")
angle = math.atan2(1.54, 7.2)
for i in range(18):
    y = -3.40 + i * .40
    z = 1.54 * (3.6 - y) / 7.2
    tread = box("Seamed ramp board", (0, y, z - .016), (4.15, .385, .040), "beam", .016, 1)
    tread.rotation_euler.x = -angle
for x in (-2.08, 2.08):
    beam("Outer ramp stringer", (x, -3.6, 1.45), (x, 3.6, -.09), .18)
    beam("Ramp coping", (x, -3.6, 1.58), (x, 3.6, .06), .13)
    for y in (-1.7, .55, 2.7):
        top = 1.54 * (3.6 - y) / 7.2
        post(x, y, top - 2.6, top + .82)
    beam("Long shore handrail", (x, -2.2, 2.00), (x, 3.18, .88), .12)
export("ShoreRamp", "Westbank")

# Shelter on the dry apron, with stock benches and a lantern for late arrivals.
box("Shelter stone apron", (0, 0, .125), (6.43, 3.98, .25), "stone", .054)
for x in (-2.74, 2.74):
    for y in (-1.66, 1.65):
        box("Masonry post shoe", (x, y, .25), (.42, .43, .30), "stone")
        beam("Shelter upright", (x, y, .31), (x, y, 2.99), .20)
    beam("Front brace", (x, -1.66, 2.25), (x * .73, -1.66, 2.93), .125)
box("Breakwater rear wall", (0, 1.65, .75), (5.48, .24, 1.12), "stone")
box("Oak service counter", (0, .84, 1.00), (3.55, .68, .15), "beam")
for x in (-1.45, 1.45):
    beam("Counter legs", (x, .84, .25), (x, .84, 1.01), .14)
for i in range(6):
    x = -2.56 + i * 1.02
    roof = box("Split shake roof pan", (x, 0, 3.02), (1.01, 4.02, .16), "clay")
    roof.rotation_euler.x = -.13
    beam("Mossed roof seam", (x + .49, -1.95, 3.34), (x + .49, 1.95, 2.84), .063)
beam("Front fascia", (-3.13, -1.96, 3.28), (3.13, -1.96, 3.28), .18)
for x in (-1.85, 1.85):
    beam("Lantern hanger", (x, -1.67, 3.18), (x, -1.67, 2.74), .060, "iron")
    box("Lantern brass housing", (x, -1.67, 2.59), (.28, .24, .35), "iron")
    box("Lantern amber face", (x, -1.803, 2.61), (.19, .042, .24), "amber")
for x in (-.95, .95):
    box("Wrapped parcel", (x, .94, 1.15), (.62, .52, .16), "linen")
    box("Parcel dark band", (x, .94, 1.23), (.10, .53, .035), "iron")
export("RopewayShelter", "Westbank")

# Small cargo hoist with rope drum, storage pile and a visible pulley.
box("Hoist base", (0, 0, .13), (3.06, 2.48, .26), "stone")
for x in (-1.08, 1.08):
    for y in (-.59, .65):
        beam("Timber hoist tripod", (x, y, .24), (x * .55, .28, 2.86), .19)
    beam("Horizontal hoist boom", (x * .55, .62, 2.91), (x * .55, -1.04, 2.91), .15)
    beam("Boom knee", (x * .72, .28, 1.96), (x * .55, -.81, 2.85), .143)
beam("Jib head beam", (-.79, -1.01, 2.93), (.79, -1.01, 2.93), .18)
pulley = cylinder("Capped pulley", (0, -1.04, 2.77), .25, .105, "iron")
pulley.rotation_euler.y = math.pi / 2
beam("Hoist rope", (0, -1.04, 2.56), (0, -1.04, 1.75), .039, "linen")
for i in range(5):
    a, b = i * math.pi / 5, (i + 1) * math.pi / 5
    beam("Curved hook", (.12 * math.cos(a), -1.04, 1.78 - .12 * math.sin(a)),
         (.12 * math.cos(b), -1.04, 1.78 - .12 * math.sin(b)), .053, "iron")
for x in (-.80, -.14, .53):
    box("Roped river crate", (x, .49, .54), (.57, .48, .57), "beam", .045)
    box("Crate binding", (x, .228, .54), (.12, .049, .58), "iron")
export("CargoHoist", "Westbank")

# Open skiff. No physics or transportation on the decorative hull.
stations = [(-2.42, .09, .44), (-1.65, .62, .32), (-.48, .91, .27),
            (.91, .86, .25), (1.94, .63, .33), (2.13, .55, .36)]
verts = []
for y, width, top in stations:
    verts += [(-width, y, top), (-width * .72, y, -.49), (width * .72, y, -.49), (width, y, top)]
faces = [(0, 1, 2, 3), (23, 22, 21, 20)]
for i in range(5):
    a, b = i * 4, (i + 1) * 4
    faces.extend((a+j, b+j, b+j+1, a+j+1) for j in range(3))
mesh("Tapered plank hull", verts, faces, "shutters")
inside = [(side * width * .89, y, .063) for y, width, _ in stations for side in (-1, 1)]
mesh("Dry inner floor liner", inside,
     [(i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2) for i in range(len(stations) - 1)], "beam")
for side in (-1, 1):
    for (p, w, h), (q, v, j) in zip(stations, stations[1:]):
        beam("Bent copper-trim gunwale", (w * side, p, h), (v * side, q, j), .090, "linen")
for y, width in [(-1.15, 1.27), (-.12, 1.58), (.98, 1.49)]:
    box("Skiff thwart seat", (0, y, .15), (width, .26, .095), "beam")
    box("Skiff flooring plank", (0, y, -.36), (width * .76, .97, .08), "beam")
box("Stowed cargo", (.19, 1.38, -.035), (.53, .43, .45), "beam", .052)
beam("Wooden oar shaft", (-.61, -1.43, .33), (-.12, .84, .35), .073, "beam")
blade = box("Oar blade", (-.66, -1.65, .345), (.23, .63, .067), "linen", .035)
blade.rotation_euler.z = -.19
export("WillowSkiff", "Westbank")

for name, at, yaw in [
    ("LandingDock", (0, 3.8, 0), 0), ("ShoreRamp", (0, -2.6, 0), 0),
    ("RopewayShelter", (-5.0, -8.72, 1.54), -.20), ("CargoHoist", (4.05, -8.2, 1.54), .18),
    ("WillowSkiff", (3.47, 4.05, -.57), -.16),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = yaw

# Studio bank and water make the shoreline assembly easy to inspect in Blender.
def display_box(name, at, dims, color):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dims
    tint = bpy.data.materials.new(name)
    tint.diffuse_color = (*color, 1)
    obj.data.materials.append(tint)


display_box("Display bank", (0, -14.2, -.30), (100, 16, 3.68), (.29, .29, .22))
display_box("Display water", (0, 37, -.72), (140, 87, .22), (.19, .32, .35))
bpy.ops.object.camera_add(location=(13, -15, 17))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, -2.35, 1.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 27
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
scene.render.filepath = str(out / "westbank-ferry-landing.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Westbank-Ferry-Landing.blend"))
bpy.ops.render.render(write_still=True)
