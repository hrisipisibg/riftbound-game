"""Build the Riftbound civil architecture prototype in Blender 5.2."""
import bpy
import math
import struct
import zlib
from pathlib import Path
import sys

OUT = Path(sys.argv[sys.argv.index("--") + 1])
OUT.mkdir(parents=True, exist_ok=False)
bpy.ops.wm.read_factory_settings(use_empty=True)

SURFACES = ("stone", "plaster", "oak", "slate", "canvas", "iron", "amber", "red")
COLORS = [(121, 113, 98), (211, 181, 136), (99, 64, 43), (59, 94, 106),
          (211, 157, 93), (60, 60, 64), (250, 184, 66), (125, 56, 49)]
SIZE = 256

def pixel(index, x, y):
    # Broad courses and grain read at game distance; fine noise breaks flat fills.
    noise = ((x * 37 ^ y * 97 ^ (x * y * 11)) % 17) - 8
    if index in (0, 3):
        row = y // 32
        seam = (x + (row % 2) * 32) % 64
        pattern = -27 if y % 32 < 3 or seam < 3 else (8 if y % 32 > 28 else 0)
        pattern += ((row * 17 + (x + (row % 2) * 32) // 64 * 13) % 13) - 6
    elif index == 2:
        pattern = -11 if (x + round(2 * math.sin(y / 16))) % 23 < 2 else 0
    elif index == 4:
        pattern = 6 if (x // 32) % 2 == 0 else -5
    else:
        pattern = 0
    return bytes(max(0, min(255, channel + pattern + noise)) for channel in COLORS[index])

def chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data))

width, height = SIZE * 4, SIZE * 2
atlas = OUT / "civil_surfaces.png"
rows = []
for y in range(height):
    rows.append(b"\0" + b"".join(pixel(y // SIZE * 4 + x // SIZE, x % SIZE, y % SIZE)
                               for x in range(width)))
atlas.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">2I5B", width, height, 8, 2, 0, 0, 0))
                  + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))
material = bpy.data.materials.new("Civil surfaces")
material.use_nodes = True
texture = material.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = bpy.data.images.load(str(atlas))
shader = material.node_tree.nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value = .88
material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])

models = []

def solid(name, vertices, faces, surface):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    tile = SURFACES.index(surface)
    uv = mesh.uv_layers.new(name="Civil atlas")
    for polygon in mesh.polygons:
        points = [mesh.vertices[mesh.loops[i].vertex_index].co for i in polygon.loop_indices]
        axes = sorted(range(3), key=lambda axis: abs(polygon.normal[axis]))[:2]
        minimum = [min(point[axis] for point in points) for axis in axes]
        span = [max(point[axis] for point in points) - start for axis, start in zip(axes, minimum)]
        for i, point in zip(polygon.loop_indices, points):
            u, v = [(point[axis] - start) / max(length, .0001)
                    for axis, start, length in zip(axes, minimum, span)]
            uv.data[i].uv = ((tile % 4 + .018 + .964 * u) / 4,
                             (1 - tile // 4 + .018 + .964 * v) / 2)
    return obj

def prism(name, x, width, profile, surface):
    vertices = [(x + side * width / 2, y, z) for side in (-1, 1) for y, z in profile]
    count = len(profile)
    faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
    faces.extend((i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count))
    return solid(name, vertices, faces, surface)

def box(name, x, y, z, width, depth, height, surface):
    return prism(name, x, width, [(y-depth/2, z), (y+depth/2, z),
                                    (y+depth/2, z+height), (y-depth/2, z+height)], surface)

def package(name, old):
    parts = [obj for obj in bpy.context.scene.objects if obj not in old]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.export_scene.gltf(filepath=str(OUT / (name + ".glb")), use_selection=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name + ".fbx")), use_selection=True,
                             path_mode="COPY", embed_textures=True)
    models.append(obj)
    print("MODEL", name, "triangles", sum(len(poly.vertices) - 2 for poly in obj.data.polygons))
    return set(bpy.context.scene.objects)

old = set()
box("Foundation", 0, 0, 0, 5.2, 4.2, .40, "stone")
box("Plastered hall", 0, 0, .40, 4.8, 3.8, 2.36, "plaster")
for x in (-2.36, 2.36):
    for y in (-1.86, 1.86):
        box("Corner oak", x, y, .34, .24, .24, 2.34, "oak")
for y in (-1.96, 1.96):
    box("Beam", 0, y, 2.47, 5.03, .18, .23, "oak")
prism("Gabled roof", 0, 5.45, [(-2.24,2.70),(2.24,2.70),(0,4.17)], "slate")
box("Document office door", -.54, -1.95, .41, 1.03, .15, 1.83, "oak")
for x in (-1.62, .67, 1.65):
    box("Shutter frame", x, -1.95, 1.15, .62, .17, .72, "oak")
    box("Deep shutter", x, -2.05, 1.23, .44, .11, .51, "red")
box("Chimney", 1.35, .75, 2.9, .62, .62, 1.52, "stone")
box("Chimney lip", 1.35, .75, 4.39, .78, .78, .14, "stone")
box("Entrance step", -.54, -2.34, 0, 1.56, .58, .2, "stone")
old = package("Emberwatch_Weighhouse", old)

box("Raised deck", 0, 1.85, 0, 5, 3.7, 1.12, "stone")
box("Paved cap", 0, 1.85, 1.12, 5, 3.7, .08, "stone")
prism("Cart access", 0, 3.4, [(-6,0),(0,0),(0,1.2)], "stone")
for x in (-2.38, 2.38):
    box("Low retaining lip", x, 1.95, 1.2, .24, 3.45, .40, "stone")
old = package("Emberwatch_ServiceTerrace", old)

for x in (-1.57, 1.57):
    for y in (-1.05, 1.05):
        box("Trestle foot", x, y, 0, .36, .36, .16, "stone")
        box("Awning support", x, y, .16, .19, .19, 2.22, "oak")
prism("Canvas", 0, 3.75, [(-1.44,2.23),(1.44,2.23),(1.44,2.32),(0,2.72),(-1.44,2.32)], "canvas")
box("Back windbreak", 0, 1.05, .38, 3.2, .14, 1.13, "oak")
for x in (-1.16, .9):
    box("Supply crate", x, .54, 0, .68, .64, .58, "oak")
    box("Crate brace", x, .21, .04, .09, .07, .50, "iron")
old = package("LanternRest_RoadShelter", old)

box("Plinth", 0, 0, 0, .5, .5, .19, "stone")
box("Lantern post", 0, 0, .19, .18, .18, 1.98, "oak")
box("Swing bracket", .20, 0, 2.03, .56, .11, .11, "iron")
box("Lantern glass", .42, 0, 1.65, .27, .26, .28, "amber")
box("Lantern lid", .42, 0, 1.93, .34, .34, .09, "iron")
package("LanternRest_Waypost", old)

for obj, location in zip(models, [(0,1,0), (6,2,0), (-5,-2,0), (-2,-3,0)]):
    obj.location = location

from mathutils import Vector
bpy.ops.mesh.primitive_plane_add(size=200)
bpy.context.object.location.z = -.025
ground = bpy.data.materials.new("Backdrop")
ground.diffuse_color = (.19, .22, .22, 1)
bpy.context.object.data.materials.append(ground)
bpy.ops.object.camera_add(location=(12, -19, 13))
camera = bpy.context.object
camera.rotation_euler = (Vector((.7, 0, 1.35)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 21
scene = bpy.context.scene
scene.camera = camera
if scene.world is None:
    scene.world = bpy.data.worlds.new("World")
scene.world.color = (.24, .24, .24)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.render.resolution_x = 1400
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.filepath = str(OUT / "settlement-kit.png")
texture.image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "settlement-kit.blend"))
bpy.ops.render.render(write_still=True)
