"""Original Deathknell masonry, iron and glass kit in meters."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Deathknell slate and moss"
use_atlas("deathknell_atlas.png", {
    "stone": (98, 96, 110), "beam": (75, 62, 81), "plaster": (127, 119, 146),
    "clay": (96, 81, 107), "shutters": (88, 108, 82), "amber": (167, 241, 110),
    "iron": (56, 59, 72), "ash": (52, 46, 61), "linen": (160, 119, 96),
})


def column(x, y, height, width=.71):
    box("Carved base plinth", (x, y, .20), (width + .29, .93, .40), "stone", .065)
    box("Inset lower collar", (x, y, .44), (width + .18, .79, .15), "plaster", .042)
    for i in range(math.ceil((height - .54) / .47)):
        low = .52 + i * .47
        top = min(low + .47, height)
        if top > low:
            box("Ashlar column course", (x, y, (low + top) * .5),
                (width - (i % 2) * .046, .70, top - low + .007), "stone", .034)
    box("Flared capital", (x, y, height + .08), (width + .27, .83, .20), "plaster", .052)


def arch_ring(name, y, radius, spring, rise, depth, first=0, last=20):
    def point(i, outer, far):
        # Uneven masonry seams are small enough to read as joints at player height.
        t = i / 20
        x = radius * (2 * t - 1)
        z = spring + rise * math.sqrt(max(0, 1 - abs(2 * t - 1)))
        if outer:
            x += .38 * (2 * t - 1)
            z += .39 * math.sqrt(max(0, 1 - abs(2 * t - 1)))
        return x, y + depth * (.5 if far else -.5), z

    for i in range(first, last):
        # Vertices trace the stone face and its extruded back.
        a, b = i + .016, i + .984
        verts = [point(a, False, False), point(a, True, False), point(b, True, False), point(b, False, False),
                 point(a, False, True), point(a, True, True), point(b, True, True), point(b, False, True)]
        mesh(name, verts, [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
                           (2, 3, 7, 6), (3, 0, 4, 7)], "plaster" if i % 5 == 0 else "stone")


def cage(x, y, z):
    box("Lantern lower casting", (x, y, z - .20), (.34, .34, .11), "iron")
    box("Milk-green lantern glass", (x, y, z), (.256, .256, .33), "amber", .023)
    for dx in (-.147, .147):
        for dy in (-.147, .147):
            beam("Iron cage rib", (x + dx, y + dy, z - .17), (x + dx * .74, y + dy * .74, z + .19), .048, "iron")
    box("Lantern upper cap", (x, y, z + .21), (.32, .32, .12), "iron", .031)


# The opening stays wide and tall enough for the mount proxy.
for x in (-2.73, 2.73):
    column(x, 0, 2.81, .89)
    box("Buttress spur", (x, .245, 1.37), (.46, 1.26, 2.41), "clay", .044)
    box("Emblem bracket", (x, -.50, 1.90), (.42, .40, .10), "iron")
    cage(x, -.65, 1.67)
arch_ring("Pointed passage stone", 0, 2.25, 2.79, 2.05, .70)
box("Highest carved stone", (0, -.040, 5.23), (.35, .80, .34), "plaster")
export("WatchArch", "Deathknell")

# Side gallery with a missing crown and stopped footing.
for x, h in [(-2.42, 2.33), (2.42, 2.33)]:
    column(x, 0, h, .56)
arch_ring("Surviving gallery voussoir", 0, 2.16, 2.30, 1.28, .50, 0, 7)
arch_ring("Surviving gallery voussoir", 0, 2.16, 2.30, 1.26, .50, 16, 20)
for i, h in enumerate((.49, .43, .35, .29, .23)):
    box("Fallen side wall slab", (-1.94 + i * .72, .19, h * .5), (.69, .66, h), "stone")
for x, y, z, angle in [(-2.6, -.7, .15, .3), (-1.5, .84, .19, -.6), (.91, .76, .16, .7), (2.21, -.60, .17, -.24)]:
    rock = box("Cleared masonry fragment", (x, y, z), (.64, .46, .29), "plaster", .083)
    rock.rotation_euler.z = angle
export("BrokenArcade", "Deathknell")

# Low memorial on a stone pad with space on both sides.
box("Cenotaph platform", (0, 0, .13), (3.16, 2.36, .26), "stone", .057)
box("Inscribed stone foot", (0, .23, .34), (2.37, 1.46, .24), "plaster", .041)
box("Tomb chest", (0, .25, .71), (2.16, 1.28, .63), "stone", .065)
box("Chamfered tomb lid", (0, .25, 1.08), (2.26, 1.38, .21), "plaster", .067)
box("Memorial stele", (0, .69, 1.37), (.98, .30, .69), "clay", .049)
box("Inset seal", (0, .519, 1.35), (.62, .067, .37), "ash", .028)
for i, length in enumerate((.43, .37, .46)):
    box("Shallow carved script", (0, .477, 1.426 - .069 * i), (length, .018, .023), "linen", .006)
for x, y in [(-.91, -.48), (-.76, -.39), (.85, -.49)]:
    cylinder("Wax stump", (x, y, .38), .065, .19, "linen")
    cylinder("Small wick", (x, y, .482), .017, .035, "amber", 8)
export("Cenotaph", "Deathknell")

# Two gentle grades meet the level central bed. Mesh normals face upward.
levels = [(-5.0, .018), (-1.85, .72), (1.85, .72), (5.0, .018)]
verts = [(x, y, z) for y, z in levels for x in (-2.25, 2.25)]
mesh("Clipped stone ramp", verts, [(i*2, i*2+1, i*2+3, i*2+2) for i in range(3)], "stone")
for x in (-2.22, 2.22):
    for (a, h), (b, j) in zip(levels, levels[1:]):
        beam("Approach curb", (x, a, h + .03), (x, b, j + .03), .15, "clay")
    for y in (-1.64, 0, 1.64):
        box("Retaining wall pilaster", (x, y, .46), (.42, .50, .91), "stone")
    box("Covered retaining wall", (x, 0, .35), (.29, 3.7, .63), "stone")
export("CloisterRamp", "Deathknell")

# Freestanding path lantern with a curved cantilever.
box("Lamppost grave slab", (0, 0, .123), (.67, .66, .246), "stone", .064)
beam("Slender wrought post", (0, 0, .20), (0, 0, 2.62), .13, "iron")
for i in range(6):
    a, b = i * math.pi / 6, (i + 1) * math.pi / 6
    beam("Bent lamp arm", (0, -.29 + .29 * math.cos(a), 2.59 + .29 * math.sin(a)),
         (0, -.29 + .29 * math.cos(b), 2.59 + .29 * math.sin(b)), .075, "iron")
beam("Lantern hanger", (0, -.58, 2.59), (0, -.58, 2.41), .050, "iron")
cage(0, -.58, 2.22)
export("CloisterLantern", "Deathknell")

for name, at, angle in [
    ("WatchArch", (0, -3.65, 0), 0), ("BrokenArcade", (-5.85, 2.8, 0), -27),
    ("Cenotaph", (5.25, 3.2, 0), 12), ("CloisterRamp", (0, 2.15, 0), 0),
    ("CloisterLantern", (4.44, -2.2, 0), -24),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(angle)
other = assets["CloisterLantern"].copy()
bpy.context.collection.objects.link(other)
other.location = (-4.08, -.45, 0)
other.rotation_euler.z = math.radians(77)

bpy.ops.mesh.primitive_plane_add(size=140, location=(0, 0, -.018))
ground = bpy.data.materials.new("Hollow ground")
ground.diffuse_color = (.23, .22, .25, 1)
bpy.context.object.data.materials.append(ground)
bpy.ops.object.camera_add(location=(12, -22, 11.2))
camera = bpy.context.object
camera.rotation_euler = (Vector((.0, .8, 1.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 23.9
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
scene.render.filepath = str(out / "deathknell-cloister.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Deathknell-Cloister.blend"))
bpy.ops.render.render(write_still=True)
