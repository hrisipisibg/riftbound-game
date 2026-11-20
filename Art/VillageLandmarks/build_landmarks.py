"""Original dressing for Emberwatch's supply lane and Greenhollow's rest stop."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, out, rng, scene


def bench(x, y, z=0):
    for offset in (-.69, .69):
        box("Bench trestle", (x + offset, y, z + .28), (.16, .48, .56), "beam")
    box("Bench seat", (x, y, z + .54), (1.8, .54, .095), "beam")
    box("Bench back", (x, y + .23, z + .81), (1.82, .10, .19), "shutters")
    for offset in (-.71, .71):
        beam("Seat brace", (x + offset, y + .22, z + .30), (x + offset, y + .22, z + .87), .10)


def cargo(x, y, z, size, key="beam"):
    box("Slatted cargo", (x, y, z + size * .4), (size, .82 * size, .8 * size), key, .047)
    for offset in (-.35, .35):
        box("Cargo iron strap", (x + offset * size, y, z + size * .4), (.095, .86 * size, .83 * size), "iron")


def canopy(width, depth, height, clay=False):
    for x in (-width, width):
        for y in (-depth, depth):
            box("Stone pier", (x, y, .12), (.43, .43, .24), "stone")
            beam("Canopy post", (x, y, .19), (x, y, height), .19)
            beam("Knee brace", (x, y, height - .62), (x * .69, y, height - .12), .12)
    for y in (-depth, depth):
        beam("Eave rail", (-width - .16, y, height), (width + .16, y, height), .19)
    for i in range(-4, 5):
        x = i * width / 4
        beam("Cross rafter", (x, -depth - .25, height + .13), (x, depth + .25, height + .13), .10)
    if clay:
        for sign in (-1, 1):
            panel = box("Sloping tile cover", (0, sign * depth * .57, height + .36),
                        (2 * width + .58, depth * 1.33, .12), "clay")
            panel.rotation_euler.x = -sign * math.atan2(.47, depth * 1.24)
        box("Roof ridge", (0, 0, height + .62), (2 * width + .65, .20, .16), "clay")
    else:
        for i in range(5):
            x = (i - 2) * width * .42
            for sign in (-1, 1):
                awning = box("Striped canvas", (x, sign * depth * .52, height + .22),
                             (width * .43, depth * 1.17, .052), "shutters" if i % 2 == 0 else "linen", .014)
                awning.rotation_euler.x = -sign * .13
        for y in (-depth - .28, depth + .28):
            box("Canvas hem", (0, y, height + .065), (2.11 * width, .063, .17), "shutters")


# Farm cart with iron rims, raised footboard, hay and lashed goods.
box("Wagon chassis", (0, 0, .73), (1.55, 2.54, .17), "beam")
for i in range(7):
    box("Wagon deck slat", ((i - 3) * .21, 0, .86), (.20, 2.60, .105), "beam")
for x in (-.79, .79):
    box("Wagon sideboard", (x, .09, 1.13), (.105, 2.53, .49), "shutters")
    for y in (-1.04, -.33, .42, 1.13):
        box("Wagon stake", (x, y, 1.15), (.18, .15, .68), "beam")
    beam("Drawbar", (x * .55, -1.1, .73), (x * .75, -2.46, .54), .12)
for y in (-.82, .88):
    beam("Iron axle", (-1.04, y, .50), (1.04, y, .50), .105, "iron")
    for x in (-.99, .99):
        # An open spoked wheel, with a segmented iron tyre.
        for i in range(12):
            a = i * math.tau / 12
            b = (i + 1) * math.tau / 12
            p = (x, y + .50 * math.cos(a), .515 + .50 * math.sin(a))
            q = (x, y + .50 * math.cos(b), .515 + .50 * math.sin(b))
            beam("Iron wheel rim", p, q, .105, "iron")
            if i % 2 == 0:
                beam("Wagon spoke", (x, y, .515), p, .075)
        hub = cylinder("Axle hub", (x, y, .515), .13, .22, "beam")
        hub.rotation_euler.y = math.pi * .5
cargo(-.36, .48, .93, .59)
cargo(.33, .68, .93, .66, "linen")
cargo(.02, -.39, .93, .69, "linen")
for x in (-.2, .24):
    beam("Tie-down", (x, -.76, 1.41), (x, .97, 1.49), .04, "iron")
export("SupplyWagon", "Emberwatch")

# Loading shed: room to stand under the roof, stock at the rear.
canopy(1.65, 1.21, 2.8, clay=True)
box("Loading deck", (0, 0, .095), (3.52, 2.76, .19), "stone")
cargo(-.99, .67, .20, .70)
cargo(-.99, .67, .76, .48)
cargo(-.30, .69, .20, .59)
for x in (.42, 1.03):
    cylinder("Bound supply barrel", (x, .70, .52), .29, .65, "beam")
    for z in (.32, .69):
        cylinder("Barrel band", (x, .70, z), .303, .074, "iron")
export("LoadingShed", "Emberwatch")

# Village news board with different-sized pinned parchments.
for x in (-.65, .65):
    box("Board stone base", (x, 0, .12), (.37, .42, .24), "stone")
    beam("Notice post", (x, 0, .21), (x, 0, 2.27), .15)
box("Notice backing", (0, 0, 1.56), (1.68, .12, 1.04), "beam")
for x, z, width, height in [(-.47, 1.64, .30, .55), (-.04, 1.76, .44, .37),
                             (.44, 1.68, .32, .49), (.09, 1.30, .38, .26)]:
    box("Pinned notice", (x, -.075, z), (width, .021, height), "linen", .007)
    box("Brass pin", (x, -.092, z + height * .33), (.037, .021, .037), "amber", .005)
    for i in range(3):
        box("Ink line", (x, -.090, z + .049 - i * .053), (width * (.72 - i * .12), .009, .015), "ash", .001)
for sign in (-1, 1):
    cap = box("Noticeboard roof", (0, sign * .23, 2.24), (2.02, .62, .11), "clay")
    cap.rotation_euler.x = -sign * .27
export("RoofedNoticeBoard", "Emberwatch")

# Raised herb beds with loose spacing and shaped foliage.
for x in (-.69, .69):
    box("Tilled garden bed", (x, 0, .22), (1.24, 2.51, .35), "ash")
    for edge in (-.59, .59):
        box("Garden bed rail", (x + edge, 0, .25), (.13, 2.62, .50), "beam")
    for end in (-1.25, 1.25):
        box("Garden end board", (x, end, .25), (1.23, .13, .50), "beam")
    for row in range(4):
        y = -.87 + row * .56
        for side in (-1, 1):
            px = x + side * .255 + rng.uniform(-.045, .045)
            for sign in (-1, 1):
                leaf = box("Bunched herb leaf", (px + sign * .071, y, .49), (.28, .11, .055),
                           "shutters", .023, segments=1)
                leaf.rotation_euler.y = -sign * .6
            box("Plant tip", (px, y, .52), (.078, .095, .19), "shutters", .022, segments=1)
export("HerbBeds", "Emberwatch")

# Roadside cover and seats. Visitors enter across a level stone floor.
canopy(1.82, 1.32, 2.71)
box("Rest court floor", (0, 0, .085), (3.95, 2.94, .17), "stone")
bench(0, .96, .17)
box("Traveler's table", (.77, -.02, .91), (1.20, .63, .11), "beam")
for x in (.32, 1.22):
    beam("Table leg", (x, -.02, .16), (x, -.02, .89), .14)
cylinder("Drinking crock", (.50, -.03, 1.06), .11, .27, "clay")
export("RestShelter", "Greenhollow")

# Tiny honey stall with recognizable stacked hive boxes and jars.
canopy(1.26, .88, 2.41)
box("Honey counter", (0, .17, 1.02), (2.32, .69, .15), "beam")
for x in (-.84, .84):
    beam("Counter leg", (x, .23, 0), (x, .23, .98), .13)
for i in range(5):
    x = -.87 + i * .43
    cylinder("Honey jar", (x, .08, 1.20), .118, .25, "amber")
    cylinder("Wax lid", (x, .08, 1.33), .122, .046, "linen")
for z in (.17, .38, .59):
    box("Apiary box", (.88, .64, z), (.53, .43, .19), "linen")
    box("Hive entrance", (.88, .40, z - .05), (.23, .045, .05), "ash")
export("HoneyStand", "Greenhollow")

# Route marker and lantern. Signs are decorative world art.
box("Trail marker footing", (0, 0, .105), (.61, .59, .21), "stone")
beam("Marker post", (0, 0, .18), (0, 0, 2.68), .17)
for sign, z in [(-1, 2.24), (1, 1.89)]:
    box("Trail direction board", (sign * .29, -.03, z), (.96, .13, .24), "shutters")
    tip = box("Styled sign tip", (sign * .77, -.03, z), (.179, .125, .179), "shutters")
    tip.rotation_euler.y = math.pi * .25
    box("Sign painted line", (sign * .24, -.102, z), (.53, .02, .035), "linen", .005)
beam("Lantern hook", (0, 0, 1.53), (0, -.45, 1.53), .068, "iron")
box("Trail lamp frame", (0, -.46, 1.26), (.24, .22, .33), "iron")
box("Trail lamp glass", (0, -.47, 1.27), (.175, .237, .20), "amber")
export("TrailLantern", "Greenhollow")

for name, x, y, yaw in [
    ("SupplyWagon", -2.6, -1.35, -24), ("LoadingShed", -3.3, 2.9, 0),
    ("RoofedNoticeBoard", 2.8, -1.8, -14), ("HerbBeds", 2.6, 1.4, -10),
    ("RestShelter", 32.5, 1.5, 0), ("HoneyStand", 37.0, 1.2, -8),
    ("TrailLantern", 34.3, -1.4, -16),
]:
    assets[name].location = (x, y, 0)
    assets[name].rotation_euler.z = math.radians(yaw)

bpy.ops.mesh.primitive_plane_add(size=150, location=(14, 0, -.025))
earth = bpy.data.materials.new("Warm dirt")
earth.diffuse_color = (.28, .23, .17, 1)
bpy.context.object.data.materials.append(earth)
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = "ORTHO"
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.color_type = "TEXTURE"
shading.light = "STUDIO"
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = "BOTH"
scene.render.resolution_x = 1440
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100


def frame(name, target_x, scale):
    camera.location = (target_x + 9, -13, 11)
    camera.rotation_euler = (Vector((target_x, .5, 1.1)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale
    scene.render.filepath = str(out / name)


frame("emberwatch-supply-lane.png", -.4, 12.3)
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Village-Landmarks.blend"))
bpy.ops.render.render(write_still=True)
frame("greenhollow-rest-stop.png", 34.7, 10.2)
bpy.ops.render.render(write_still=True)
