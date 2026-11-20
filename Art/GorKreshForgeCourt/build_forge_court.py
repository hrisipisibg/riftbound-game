"""Original Gor'Kresh kit in meters, with a charcoal-and-rust atlas."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Gor'Kresh charcoal and rust"
use_atlas("gorkresh_atlas.png", {
    "stone": (83, 74, 69), "beam": (93, 53, 36), "plaster": (147, 119, 91),
    "clay": (111, 54, 40), "shutters": (130, 45, 33), "amber": (255, 135, 38),
    "iron": (63, 64, 68), "ash": (41, 36, 37), "linen": (213, 175, 110),
})


def octagon(name, x, y, levels, key):
    sides = 8
    verts = [(x + r * math.cos(i * math.tau / sides + math.pi / 8),
              y + r * math.sin(i * math.tau / sides + math.pi / 8), z)
             for z, r in levels for i in range(sides)]
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(levels) - 1):
        for i in range(sides):
            a = j * sides + i
            b = j * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(len(verts) - sides, len(verts))))
    return mesh(name, verts, faces, key)


def lantern(x, y, z):
    box("Iron lantern case", (x, y, z), (.31, .32, .42), "iron")
    for offset in (-.168, .168):
        box("Amber inset", (x, y + offset, z), (.226, .020, .30), "amber")
        box("Iron center bar", (x, y + offset * 1.06, z), (.031, .021, .32), "iron")
    box("Sloped lantern cap", (x, y, z + .23), (.37, .36, .087), "iron")


def timber_cover(width, depth, height, ridge):
    for x in (-width, width):
        for y in (-depth, depth):
            box("Basalt pier shoe", (x, y, .25), (.56, .56, .50), "stone")
            beam("Raked timber post", (x, y, .45), (x * .92, y, height), .25)
            beam("Corner brace", (x, y, height - .63), (x * .67, y, height - .12), .15)
    for sign in (-1, 1):
        long = math.hypot(depth, ridge)
        panel = box("Iron roof deck", (0, sign * depth / 2, height + ridge / 2),
                    (2 * width + .51, long + .14, .13), "clay")
        panel.rotation_euler.x = -sign * math.atan2(ridge, depth)
        beam("Roof fascia", (-width - .28, sign * depth, height),
             (width + .28, sign * depth, height), .17, "iron")
        for i in range(-3, 4):
            x = i * width / 3
            beam("Raised roof rib", (x, sign * (depth + .075), height + .052),
                 (x, 0, height + ridge + .075), .073, "iron")
    box("Ridge capping", (0, 0, height + ridge + .04), (2 * width + .58, .27, .16), "iron")


# The chimney is visible across the quarter. The forecourt has an open apron.
box("Furnace stone terrace", (0, 0, .165), (7.8, 5.96, .33), "stone")
box("Furnace rear retaining wall", (0, 2.60, 1.02), (7.52, .42, 1.45), "stone")
octagon("Furnace main body", .42, .62, [(.33, 2.28), (.52, 2.34), (2.35, 1.86), (3.2, 1.30),
        (3.65, .85), (5.62, .73), (5.87, .90)], "stone")
octagon("Masonry belt at shoulder", .42, .62, [(2.13, 1.94), (2.33, 1.96)], "iron")
octagon("Chimney neck collar", .42, .62, [(4.53, .83), (4.68, .84)], "iron")
octagon("Mouth of chimney", .42, .62, [(5.89, .94), (5.99, .91)], "ash")
box("Furnace mouth recess", (.42, -1.46, 1.30), (1.82, .23, 1.32), "ash", .07)
box("Banked glow", (.42, -1.60, 1.16), (1.38, .072, .75), "amber", .084)
for i in range(7):
    angle = i * math.pi / 6
    obj = box("Mouth arch voussoir", (.42 + math.cos(angle) * 1.06, -1.71, 1.44 + math.sin(angle) * .48),
              (.37, .35, .28), "iron", .047)
    obj.rotation_euler.y = -angle * .60
for x in (-.56, 1.40):
    box("Mouth side jamb", (x, -1.70, 1.0), (.30, .33, .92), "iron")
for i in range(3):
    box("Slag tray rail", (.42 + (i - 1) * .37, -2.0, .61), (.13, .95, .16), "iron")
box("Slag tray base", (.42, -2.02, .50), (1.35, .99, .13), "ash")
for x in (-3.16, 3.16):
    lantern(x, -2.22, .63)
export("KilnFurnace", "GorKresh")

# Roofed loading shed with repaired crates, ingot stacks and tool racks.
timber_cover(3.20, 1.76, 3.26, .75)
box("Quartermaster stone floor", (0, 0, .145), (6.92, 4.02, .29), "stone")
box("Rear heavy plank screen", (0, 1.72, 1.50), (6.31, .16, 2.58), "beam")
for i in range(6):
    for j in range(2):
        x = -2.46 + i * .37
        box("Stacked raw ingot", (x + j * .11, 1.11, .38 + j * .14),
            (.31, .49 - j * .07, .13), "iron")
for x in (1.23, 2.37):
    box("Supply crate", (x, 1.08, .65), (.95, .82, .72), "beam")
    for dx in (-.29, .29):
        box("Crate band", (x + dx, 1.08, .66), (.10, .859, .73), "iron")
    box("Red supply seal", (x, .645, .70), (.25, .03, .3), "shutters")
box("Front unloading bench", (-.89, .22, .83), (1.76, .73, .13), "beam")
for dx in (-.73, .73):
    beam("Bench trestle", (-.89 + dx, .22, .26), (-.89 + dx, .22, .82), .16)
export("QuartermasterShed", "GorKresh")

# An open arch marks the court without narrowing the 4.6 m clear center.
for x in (-2.80, 2.80):
    box("Way arch basalt plinth", (x, 0, .29), (.96, 1.12, .58), "stone")
    beam("Inward leaning arch post", (x, 0, .48), (x * .88, 0, 4.01), .40)
    box("Arch post iron shoe", (x * .98, 0, .79), (.49, .52, .36), "iron")
    beam("Outrigger strut", (x * 1.15, 0, .59), (x * .92, 0, 2.97), .19)
    lantern(x * .94, -.35, 2.23)
beam("Iron crosshead", (-2.82, 0, 3.98), (2.82, 0, 3.98), .40, "iron")
beam("Raised center brace", (-2.69, 0, 4.08), (0, 0, 4.72), .25)
beam("Raised center brace", (2.69, 0, 4.08), (0, 0, 4.72), .25)
flag = box("Forged red crest", (0, -.21, 4.23), (.63, .14, .63), "shutters")
flag.rotation_euler.y = math.pi / 4
for x in (-1.60, 1.60):
    mesh("V-cut banner", [(x - .43, -.27, 3.93), (x + .43, -.27, 3.93),
         (x + .43, -.285, 3.23), (x, -.31, 3.34), (x - .43, -.285, 3.23)],
         [(0, 4, 3, 2, 1)], "shutters")
export("ForgeArch", "GorKresh")

# Blacksmith working nook, set aside from the mounted lane.
box("Anvil dais", (0, 0, .13), (3.39, 2.57, .26), "stone")
octagon("Anvil block", 0, 0, [(.23, .62), (.97, .50)], "beam")
box("Anvil base plate", (0, 0, 1.00), (.94, .60, .13), "iron")
box("Anvil waist", (0, 0, 1.18), (.54, .44, .28), "iron")
box("Anvil face and horn", (0, 0, 1.38), (1.35, .50, .22), "iron", .083)
box("Quench trough", (1.12, .53, .47), (.65, 1.12, .45), "beam")
box("Trough water surface", (1.12, .53, .704), (.48, .94, .04), "iron")
for i in range(3):
    beam("Long forge tongs", (-1.19 + i * .12, .85, .35), (-1.28 + i * .15, 1.01, 1.25), .058, "iron")
export("AnvilStation", "GorKresh")

# Waist-high charcoal bin and spare fuel wood.
box("Charcoal box front", (0, -.48, .49), (1.93, .15, .88), "beam")
box("Charcoal box back", (0, .47, .49), (1.93, .15, .88), "beam")
for x in (-.89, .89):
    box("Charcoal box sides", (x, 0, .49), (.15, 1.09, .88), "beam")
    box("Iron rim strap", (x, -.53, .49), (.13, .075, .88), "iron")
for i in range(7):
    lump = box("Charcoal chunk", ((i % 4 - 1.5) * .39, (i // 4 - .5) * .42, .89),
               (.44, .35, .29), "ash", .092)
    lump.rotation_euler.z = i * .81
export("CharcoalBin", "GorKresh")

for name, at, yaw in [
    ("ForgeArch", (0, -4.0, 0), 0), ("KilnFurnace", (-6.5, 3.4, 0), -13),
    ("QuartermasterShed", (6.9, 3.2, 0), 14), ("AnvilStation", (-5.0, -2.4, 0), -15),
    ("CharcoalBin", (-9.6, .9, 0), 12),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(yaw)

bpy.ops.mesh.primitive_plane_add(size=160, location=(0, 0, -.02))
earth = bpy.data.materials.new("Packed red earth")
earth.diffuse_color = (.33, .22, .15, 1)
bpy.context.object.data.materials.append(earth)
bpy.ops.object.camera_add(location=(15, -22, 15))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 1, 1.85)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 26.8
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.color_type = "TEXTURE"
shading.light = "STUDIO"
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = "BOTH"
scene.render.resolution_x, scene.render.resolution_y = 1580, 1080
scene.render.resolution_percentage = 100
scene.render.filepath = str(out / "gorkresh-forge-court.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "GorKresh-Forge-Court.blend"))
bpy.ops.render.render(write_still=True)
