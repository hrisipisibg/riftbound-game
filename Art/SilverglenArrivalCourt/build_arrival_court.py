"""Build the original Silverglen arrival kit."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Silverglen pearl and teal"
use_atlas("silverglen_atlas.png", {
    "stone": (169, 167, 153), "beam": (101, 77, 66), "plaster": (214, 206, 175),
    "clay": (61, 107, 112), "shutters": (57, 117, 89), "amber": (117, 208, 230),
    "iron": (72, 83, 108), "ash": (86, 74, 66), "linen": (187, 151, 82),
})


def rings(name, x, y, profile, key, sides=16):
    vertices = [(x + radius * math.cos(i * math.tau / sides), y + radius * math.sin(i * math.tau / sides), z)
                for z, radius in profile for i in range(sides)]
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(profile) - 1):
        for i in range(sides):
            a, b = j * sides + i, j * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(len(vertices) - sides, len(vertices))))
    return mesh(name, vertices, faces, key)


def polar(radius, angle, height):
    return (radius * math.cos(angle), radius * math.sin(angle), height)


# Pavilion with an eight-leaf roof, brass seams and an open reading space.
rings("Stone pavilion foundation", 0, 0, [(0, 3.48), (.14, 3.5), (.28, 3.42)], "stone", 8)
rings("Cream lip", 0, 0, [(.19, 3.51), (.29, 3.5), (.33, 3.4)], "plaster", 8)
for angle in (-math.pi * .14, math.pi * .14, math.pi * .40, math.pi * .60,
              math.pi * .86, math.pi * 1.14):
    x, y, _ = polar(2.77, angle, 0)
    rings("Flared column foot", x, y, [(.30, .25), (.47, .23), (.58, .16)], "linen", 8)
    beam("Tapering stone column", (x, y, .54), (x * .92, y * .92, 3.15), .18, "plaster")
    beam("Curved roof knee", (x * .92, y * .92, 2.62), (x * .71, y * .71, 3.20), .125, "linen")
for leaf in range(8):
    a = leaf * math.tau / 8
    b = a + math.tau / 8
    mid = (a + b) * .5
    # Six vertices give each leaf a shallow curve and a broad shadow line.
    top = [polar(.30, a, 5.04), polar(1.54, a, 4.15), polar(3.44, a, 3.13),
           polar(3.68, mid, 3.34), polar(3.44, b, 3.13), polar(1.54, b, 4.15),
           polar(.30, b, 5.04), polar(1.60, mid, 4.34)]
    vertices = top + [(x, y, z - .11) for x, y, z in top]
    faces = [(i, (i + 1) % 7, 7) for i in range(7)]
    direction = Vector(vertices[faces[0][1]]) - Vector(vertices[faces[0][0]])
    to_center = Vector(vertices[7]) - Vector(vertices[0])
    if direction.cross(to_center).z < 0:
        faces = [tuple(reversed(face)) for face in faces]
    boundary = [(face[0], face[1]) if face[-1] == 7 else (face[1], face[2]) for face in faces]
    faces += [tuple(index + 8 for index in reversed(face)) for face in faces]
    faces.extend((b1, a1, a1 + 8, b1 + 8) for a1, b1 in boundary)
    mesh("Folded teal roof petal", vertices, faces, "clay" if leaf % 2 == 0 else "iron")
    for radius_a, height_a, radius_b, height_b in [(.32, 5.03, 1.54, 4.17), (1.54, 4.17, 3.45, 3.17)]:
        beam("Raised copper roof seam", polar(radius_a, a, height_a), polar(radius_b, a, height_b), .063, "linen")
rings("Apex base", 0, 0, [(4.89, .37), (5.11, .34), (5.28, .21), (5.60, .065)], "linen", 12)
for x in (-1.34, -.62):
    box("Reading bench footing", (x, 1.24, .51), (.22, .48, .37), "plaster")
box("Reading bench seat", (-.98, 1.25, .76), (1.30, .53, .17), "beam")
box("Field guide table", (.85, .95, 1.07), (1.25, .71, .15), "beam")
for x in (.37, 1.33):
    beam("Table leg", (x, .95, .34), (x, .95, 1.07), .14)
for i, key in enumerate(("linen", "shutters", "iron")):
    box("Field guide", (.56 + .31 * i, .93 + .07 * (i % 2), 1.17), (.25, .34, .052), key, .012)
export("ReadingPavilion", "Silverglen")

# Low crescent bench with fitted stone supports. The curved seat is a solid mesh.
def crescent(label, inner, outer, low, high, key):
    count = 10
    verts = [polar(r, math.radians(23 + 14 * i), z)
             for i in range(count) for r, z in [(inner, low), (outer, low), (inner, high), (outer, high)]]
    faces = [(0, 2, 3, 1)]
    for i in range(count - 1):
        a, b = i * 4, (i + 1) * 4
        faces += [(a, a+1, b+1, b), (a+2, b+2, b+3, a+3),
                  (a, b, b+2, a+2), (a+1, a+3, b+3, b+1)]
    faces.append((b, b+1, b+3, b+2))
    return mesh(label, verts, faces, key)


for angle in (33, 84, 137):
    x, y, _ = polar(1.32, math.radians(angle), 0)
    box("Crescent bench stone support", (x, y, .21), (.34, .44, .42), "stone", .047)
crescent("Thin stone seat", 1.05, 1.58, .40, .56, "plaster")
crescent("Inlaid seat edge", 1.52, 1.60, .49, .56, "linen")
crescent("Timber hand rest", 1.57, 1.67, .67, .78, "beam")
for angle in (30, 88, 144):
    x, y, _ = polar(1.57, math.radians(angle), 0)
    beam("Arm support", (x, y, .52), (x, y, .73), .105, "linen")
export("CrescentBench", "Silverglen")

# Herb basin with blue flowers and leaf layers, kept low for the approach sightline.
rings("Herb bowl", 0, 0, [(0, 1.17), (.14, 1.21), (.49, 1.36), (.58, 1.40), (.63, 1.35)], "stone")
rings("Fine copper band", 0, 0, [(.53, 1.388), (.59, 1.401)], "linen")
rings("Dark planting soil", 0, 0, [(.58, 1.19), (.642, 1.19)], "ash")
for i in range(8):
    a = i * math.tau / 8
    root, top = polar(.64, a, .62), polar(.79, a + .13, 1.06 + .07 * (i % 3))
    beam("Flower stem", root, top, .044, "shutters")
    for side in (-1, 1):
        leaf = box("Long herb leaf", polar(.76, a + side * .21, .77), (.32, .13, .058), "shutters", .025, 1)
        leaf.rotation_euler = (0, -.40, a + side * .90)
    for petal in range(5):
        theta = petal * math.tau / 5
        x, y, z = top
        bloom = box("Blue petal", (x + .09 * math.cos(theta), y + .09 * math.sin(theta), z),
                    (.155, .09, .035), "amber", .024, 1)
        bloom.rotation_euler.z = theta
export("HerbBasin", "Silverglen")

# A ground-set trail beacon with copper ribs around a faceted blue glass head.
rings("Trail beacon lower plinth", 0, 0, [(0, .45), (.13, .47), (.23, .37)], "stone", 8)
rings("Tapered lantern shaft", 0, 0, [(.21, .24), (.38, .21), (1.50, .135), (1.68, .23)], "plaster", 8)
rings("Glass beacon", 0, 0, [(1.60, .18), (1.77, .28), (2.03, .24), (2.22, .07)], "amber", 8)
for i in range(6):
    angle = i * math.tau / 6
    beam("Rising copper leaf", polar(.18, angle, 1.59), polar(.28, angle + .16, 1.83), .048, "linen")
    beam("Folded copper leaf", polar(.28, angle + .16, 1.83), polar(.16, angle + .30, 2.10), .048, "linen")
export("TrailBeacon", "Silverglen")

# Walkable inlay slab. The narrow seams sit less than a centimeter above the stone.
box("Arrival paving", (0, 0, .045), (4.1, 3.0, .09), "stone", .024)
for y in (-1.0, 0, 1.0):
    for x in (-1.51, -.50, .51, 1.52):
        box("Cushion edge ashlar", (x, y, .084), (.985, .968, .029), "plaster", .037, 1)
for a, b in [((-1.19, 0, .10), (0, .86, .10)), ((0, .86, .10), (1.19, 0, .10)),
             ((1.19, 0, .10), (0, -.86, .10)), ((0, -.86, .10), (-1.19, 0, .10))]:
    beam("Copper wayfinding inlay", a, b, .022, "linen")
rings("Blue compass center", 0, 0, [(.088, .25), (.112, .25)], "clay", 8)
export("InlayPaving", "Silverglen")

for name, at, yaw in [
    ("ReadingPavilion", (-4.75, 3.30, 0), 0), ("HerbBasin", (4.0, 3.7, 0), 0),
    ("CrescentBench", (4.0, 1.8, 0), 180), ("TrailBeacon", (-2.7, -3.65, 0), 0),
    ("InlayPaving", (0, -3.85, 0), 0),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(yaw)
for name, x, y in [("TrailBeacon", 2.74, -3.67), ("InlayPaving", 0, -.85), ("InlayPaving", 0, 2.15)]:
    instance = assets[name].copy()
    bpy.context.collection.objects.link(instance)
    instance.location = (x, y, 0)

bpy.ops.mesh.primitive_plane_add(size=130, location=(0, 0, -.016))
soil = bpy.data.materials.new("Open grove ground")
soil.diffuse_color = (.26, .30, .24, 1)
bpy.context.object.data.materials.append(soil)
bpy.ops.object.camera_add(location=(11, -21, 13))
camera = bpy.context.object
camera.rotation_euler = (Vector((-.6, .8, 1.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 20
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
scene.render.filepath = str(out / "silverglen-arrival.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Silverglen-Arrival-Court.blend"))
bpy.ops.render.render(write_still=True)
