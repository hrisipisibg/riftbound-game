"""Original sandstone lookout and supply yard for the Bloodrock trail."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EmberwatchCivicSquare"))
from build_square import assets, beam, box, cylinder, export, material, mesh, out, scene, use_atlas

material.name = "Bloodrock sandstone and hide"
use_atlas("bloodrock_atlas.png", {
    "stone": (143, 91, 68), "beam": (105, 68, 44), "plaster": (180, 125, 82),
    "clay": (132, 71, 51), "shutters": (124, 51, 37), "amber": (240, 155, 53),
    "iron": (67, 58, 60), "ash": (57, 44, 38), "linen": (193, 154, 100),
})


def lance_post(x, y, base, height):
    beam("Tapered redwood timber", (x, y, base), (x, y, base + height - .13), .23)
    verts = [(x-.119, y-.119, base+height-.18), (x+.119, y-.119, base+height-.18),
             (x+.119, y+.119, base+height-.18), (x-.119, y+.119, base+height-.18),
             (x, y, base+height+.06)]
    mesh("Hewn timber cap", verts, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)], "beam")
    box("Post wrap", (x, y, base + height - .28), (.248, .248, .096), "iron", .018)


# Humans can walk up a continuous 20-degree ramp onto a stone-and-wood viewpoint.
# The open ramp is on the south side. The sandstone landing is 2.15 m above the yard.
levels = [(-7.15, .02), (-1.22, 2.15), (2.38, 2.15)]
verts = [(x, y, z) for y, z in levels for x in (-1.50, 1.50)]
mesh("Continuous lookout floor", verts, [(0, 1, 3, 2), (2, 3, 5, 4)], "beam")
box("Dressed sandstone base", (0, .68, .38), (3.24, 3.62, .76), "stone", .085)
box("Inset sandstone wall", (0, .68, 1.36), (3.03, 3.54, 1.37), "stone", .079)
box("Redstone deck edge", (0, .65, 2.055), (3.16, 3.62, .174), "plaster", .043)
for x in (-1.48, 1.48):
    beam("Broad ramp stringer", (x, -7.12, .04), (x, -1.22, 2.15), .16)
    for i, y in enumerate((-5.6, -3.37, -1.18, .75, 2.25)):
        z = min(2.15, .02 + (y + 7.15) * (2.15-.02)/5.93)
        lance_post(x, y, z - .095, 1.22)
        if i == 0:
            continue
        old_y = (-5.6, -3.37, -1.18, .75, 2.25)[i-1]
        old_z = min(2.15, .02 + (old_y + 7.15) * (2.15-.02)/5.93)
        beam("Lookout edge rail", (x, old_y, old_z + .89), (x, y, z + .89), .127)
        beam("Lookout lower spar", (x, old_y, old_z + .45), (x, y, z + .45), .092)
    for y in (-4.7, -3.0, -1.34):
        z = .02 + (y + 7.15) * (2.15-.02)/5.93
        beam("Grounded ramp pile", (x*.88, y, -.02), (x*.88, y, z), .23)
for z in (2.62, 3.04):
    beam("Overlook viewing rail", (-1.50, 2.26, z), (1.50, 2.26, z), .13)
for y in (-6.65, -5.95, -5.25, -4.55, -3.85, -3.15, -2.45, -1.75):
    z = .02 + (y + 7.15) * (2.15-.02)/5.93
    seam = box("Recessed plank join", (0, y, z), (2.78, .030, .008), "ash", bevel=0)
    seam.rotation_euler.x = math.atan2(2.13, 5.93)
for y in (-.8, -.12, .56, 1.24, 1.92):
    box("Lookout board join", (0, y, 2.151), (2.78, .018, .006), "ash", bevel=0)
export("VistaLookout", "Bloodrock")

# A shade tent pitched on sandstone shoes, with cargo stacked behind the open bay.
box("Paved supplies apron", (0, 0, .108), (5.76, 4.0, .216), "stone", .068)
for x in (-2.44, 2.44):
    for y in (-1.58, 1.59):
        box("Heavy post shoe", (x, y, .24), (.63, .59, .35), "stone", .051)
        beam("Outward sloping tent post", (x, y, .41), (x*.85, y, 2.87), .24)
        beam("Shade knee brace", (x, y, 2.11), (x*.64, y, 2.83), .142)
for side in (-1, 1):
    for i in range(3):
        x = -.04 + (i-1) * 1.74
        cloth = box("Pitch-stitched hide panel", (x, side*.94, 3.15), (1.73, 2.06, .092),
                    "shutters" if i != 1 else "linen", .023)
        cloth.rotation_euler.x = -side*.24
    beam("Long shade fascia", (-2.63, side*1.93, 2.88), (2.63, side*1.93, 2.88), .17)
beam("High tent ridge", (-2.70, 0, 3.44), (2.70, 0, 3.44), .175)
box("Back windbreak timber", (0, 1.53, .95), (4.38, .18, 1.17), "beam")
for y, z in ((.85,.57), (.82,1.0)):
    box("Sealed travel crate", (-1.20, y, z), (.78,.65,.49), "beam", .052)
    for x in (-1.45, -.95):
        box("Heavy cargo strap", (x, y-.33, z), (.09,.042,.48), "iron")
for x in (.51, 1.52):
    cylinder("Ration cask", (x, 1.09, .62), .30, .79, "beam")
    for z in (.38, .82):
        cylinder("Ration cask band", (x, 1.09, z), .308, .07, "iron")
export("TrailSupplyStand", "Bloodrock")

# Hide banner and a low lantern on a squared roadside marker.
box("Broad trail marker footing", (0, 0, .13), (.79,.70,.26), "stone", .072)
lance_post(0, 0, .21, 3.25)
beam("Bronze crossbar", (-.65, 0, 2.84), (.65, 0, 2.84), .104, "linen")
box("Looped crimson banner", (0, -.11, 2.29), (.96,.071,.98), "shutters", .022)
for x in (-.33, .33):
    hem = box("Cut hide end", (x, -.11, 1.78), (.32,.070,.21), "shutters", .022)
    hem.rotation_euler.y = math.copysign(.31,x)
emblem = box("Sandstone triangle marker", (0, -.156, 2.33), (.31,.034,.31), "linen", .018)
emblem.rotation_euler.y = math.pi/4
beam("Hanging lantern peg", (0,0,1.51), (0,-.37,1.57), .064, "iron")
box("Trail lantern case", (0,-.36,1.32), (.26,.27,.32), "iron")
box("Warm glass plate", (0,-.50,1.32), (.20,.028,.24), "amber")
export("OutpostBanner", "Bloodrock")

# A low bench with a stone water basin. Sits away from the through route.
box("Rest bay floor", (0,0,.107), (3.42,2.44,.214), "stone", .064)
for x in (-1.35,-.19,.97):
    box("Bench stone leg", (x,.66,.36), (.31,.48,.35), "stone")
box("Redwood bench plank", (-.20,.66,.57), (2.67,.56,.13), "beam")
box("Bench back", (-.20,.91,.87), (2.68,.11,.29), "beam")
cylinder("Water crock body", (1.04,-.51,.40), .33,.43,"plaster")
cylinder("Water crock collar", (1.04,-.51,.63), .36,.10,"linen")
cylinder("Blue-green water top", (1.04,-.51,.687), .28,.018,"iron")
box("Folded hide on seat", (-.68,.62,.66), (.68,.54,.07), "shutters")
export("RestBay", "Bloodrock")

for name, at, angle in [
    ("VistaLookout", (1.7,1.2,0),0), ("TrailSupplyStand", (-5.4,1.0,0),-19),
    ("RestBay", (-3.6,-3.0,0),12), ("OutpostBanner",(4.3,-5.6,0),0),
]:
    assets[name].location = at
    assets[name].rotation_euler.z = math.radians(angle)
second = assets["OutpostBanner"].copy()
bpy.context.collection.objects.link(second)
second.location = (-.75,-6.1,0)

bpy.ops.mesh.primitive_plane_add(size=150, location=(0,0,-.021))
ground = bpy.data.materials.new("Redridge ground")
ground.diffuse_color = (.31,.23,.19,1)
bpy.context.object.data.materials.append(ground)
bpy.ops.object.camera_add(location=(12,-18,14))
camera = bpy.context.object
camera.rotation_euler = (Vector((-.5,-.8,1.25)) - camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 23.0
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
scene.render.filepath = str(out / "bloodrock-lookout.png")
bpy.ops.wm.save_as_mainfile(filepath=str(out / "Bloodrock-Lookout.blend"))
bpy.ops.render.render(write_still=True)
