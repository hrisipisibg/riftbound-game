"""Build the original Emberwatch civic-square kit and its editable display scene."""
import math
import random
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector

out = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
out.mkdir(parents=True, exist_ok=False)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
rng = random.Random(1889)
palette = {
    "stone": (126, 115, 102), "beam": (102, 65, 36), "plaster": (218, 183, 128),
    "clay": (154, 70, 48), "shutters": (70, 105, 80), "amber": (243, 178, 75),
    "iron": (53, 53, 55), "ash": (64, 57, 52), "linen": (204, 184, 139),
}
keys = tuple(palette)
tile = 256
side = tile * 3

def texel(key, x, y, colors):
    noise = ((x * 39 ^ y * 109 ^ x * y * 19) % 19) - 9
    value = noise + 4 * math.sin(x * .09 + y * .05)
    if key in ("stone", "clay"):
        row = y // 28
        edge = min(y % 28, (x + row % 2 * 21) % 42)
        value += -33 if edge < 3 else 10 if edge < 6 else (row * 7 + x // 42 * 17) % 17 - 8
    if key in ("beam", "shutters"):
        value += 7 * math.sin(x * .7 + 2 * math.cos(y * .05))
    return bytes(max(0, min(255, round(c + value))) for c in colors[key])

def chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))

material = bpy.data.materials.new("Emberwatch timber and clay")
material.use_nodes = True
nodes = material.node_tree.nodes
image = nodes.new("ShaderNodeTexImage")
principled = nodes.get("Principled BSDF")
principled.inputs["Roughness"].default_value = .87
material.node_tree.links.new(image.outputs["Color"], principled.inputs["Base Color"])
parts, assets = [], {}

def use_atlas(filename, colors):
    pixels = b"".join(b"\0" + b"".join(texel(keys[y // tile * 3 + x // tile], x % tile, y % tile, colors)
                                           for x in range(side)) for y in range(side))
    atlas = out / filename
    atlas.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">2I5B", side, side, 8, 2, 0, 0, 0))
                      + chunk(b"IDAT", zlib.compress(pixels, 9)) + chunk(b"IEND", b""))
    texture = bpy.data.images.load(str(atlas))
    texture.pack()
    image.image = texture


def paint(obj, key):
    index = keys.index(key)
    obj.data.materials.append(material)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
    uv.name = "UVMap"
    for poly in obj.data.polygons:
        a, b = [axis for axis in range(3) if axis != max(range(3), key=lambda k: abs(poly.normal[k]))]
        coords = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in poly.loop_indices]
        lo_a, lo_b = min(v[a] for v in coords), min(v[b] for v in coords)
        d_a = max(max(v[a] for v in coords) - lo_a, .0001)
        d_b = max(max(v[b] for v in coords) - lo_b, .0001)
        for loop, vert in zip(poly.loop_indices, coords):
            uv.data[loop].uv = ((index % 3 + .025 + .95 * (vert[a] - lo_a) / d_a) / 3,
                                (2 - index // 3 + .025 + .95 * (vert[b] - lo_b) / d_b) / 3)
    parts.append(obj)
    return obj

def box(name, at, dims, key, bevel=.035, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    paint(obj, key)
    if bevel:
        modifier = obj.modifiers.new("Worn arris", "BEVEL")
        modifier.width = min(bevel, min(dims) * .35)
        modifier.segments = segments
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

def beam(name, a, b, width, key="beam"):
    a, b = Vector(a), Vector(b)
    obj = box(name, (a + b) * .5, (width, width, (b - a).length), key)
    obj.rotation_euler = (b - a).to_track_quat("Z", "Y").to_euler()

def mesh(name, vertices, faces, key):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return paint(obj, key)

def cylinder(name, at, radius, depth, key, sides=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=at)
    obj = bpy.context.object
    obj.name = name
    return paint(obj, key)

def roof(width, length, eave, rise, center_y=0):
    # Ridge runs along X. Each slope has exposed fascia and individual verge tiles.
    angle = math.atan2(rise, length)
    slope = math.hypot(length, rise)
    for sign in (-1, 1):
        panel = box("Clay roof slope", (0, center_y + sign * length * .5, eave + rise * .5),
                    (width * 2, slope, .13), "clay")
        panel.rotation_euler.x = -sign * angle
        beam("Eave fascia", (-width, center_y + sign * length, eave),
             (width, center_y + sign * length, eave), .19)
        for x in (-width, width):
            beam("Rake fascia", (x, center_y + sign * length, eave), (x, center_y, eave + rise), .17)
        for i in range(math.floor(width * 4)):
            x = -width + (i + .5) * .5
            tile_piece = box("Lapped eave tile", (x, center_y + sign * (length - .11), eave + .13),
                             (.46, .30, .078), "clay")
            tile_piece.rotation_euler.x = -sign * angle
    box("Ridge capping", (0, center_y, eave + rise + .08), (2 * width + .12, .25, .18), "clay")
    for sign in (-1, 1):
        x = sign * (width - .32)
        face = (0, 1, 2) if sign > 0 else (2, 1, 0)
        mesh("Closed plaster gable", [(x, center_y - length, eave), (x, center_y + length, eave),
             (x, center_y, eave + rise)], [face], "plaster")

def window(x, y, z):
    box("Recessed frame", (x, y, z), (.90, .15, 1.12), "beam")
    box("Amber leaded glass", (x, y - .085, z), (.67, .032, .88), "amber")
    for offset in (-.48, .48):
        box("Green shutter", (x + offset, y - .105, z), (.22, .08, .92), "shutters")
    box("Sill", (x, y - .14, z - .56), (1.14, .31, .105), "stone")
    box("Center muntin", (x, y - .11, z), (.061, .035, .90), "beam")
    box("Horizontal muntin", (x, y - .11, z), (.67, .035, .060), "beam")

def export(name, prefix="Emberwatch"):
    if image.image is None:
        use_atlas("emberwatch_atlas.png", palette)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    model = bpy.context.object
    model.name = model.data.name = prefix + "_" + name
    if model.data.validate(verbose=True):
        raise ValueError(f"Invalid mesh: {model.name}")
    bpy.ops.export_scene.fbx(filepath=str(out / (model.name + ".fbx")), use_selection=True,
                             path_mode="COPY", embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(out / (model.name + ".glb")), use_selection=True)
    print(f"ASSET {model.name} {sum(len(p.vertices) - 2 for p in model.data.polygons)} triangles", flush=True)
    assets[name] = model
    parts.clear()

if __name__ == "__main__":
    # Two-storey inn with a plaster jetty, braces, shuttered windows and projecting hatch.
    box("Stone plinth", (0, 0, .21), (8.6, 6.3, .42), "stone")
    box("Masonry ground floor", (0, 0, 1.43), (8.15, 5.85, 2.52), "stone")
    box("Jettied plaster floor", (0, -.22, 3.68), (8.62, 6.38, 2.08), "plaster")
    for z in (2.68, 4.67):
        box("Full-width bressummer", (0, -.22, z), (8.78, 6.46, .24), "beam")
    for x in (-4.25, -2.43, 0, 2.43, 4.25):
        beam("Front stud", (x, -3.43, 2.75), (x, -3.43, 4.7), .18)
    for x in (-4.03, 4.03):
        direction = 1 if x < 0 else -1
        beam("Jetty brace", (x, -3.49, 2.85), (x + direction * 1.28, -3.49, 4.52), .17)
    for x in (-2.83, -1.16, 1.16, 2.83):
        window(x, -3.49, 3.77)
    for x in (-2.62, 2.62):
        window(x, -2.99, 1.67)
    box("Oak entrance door", (0, -3.04, 1.40), (1.56, .12, 2.01), "beam", .09)
    box("Door threshold", (0, -3.11, .25), (1.91, .56, .26), "stone")
    beam("Inn sign hanger", (1.2, -3.36, 3.02), (1.2, -4.34, 3.02), .12)
    box("Hanging inn sign", (1.2, -4.18, 2.63), (.075, .83, .47), "shutters")
    box("Sign brass emblem", (1.25, -4.18, 2.64), (.02, .30, .19), "amber")
    roof(4.68, 3.47, 4.82, 1.77, -.22)
    box("Chimney", (2.75, 1.27, 5.95), (.88, .92, 2.18), "stone")
    box("Chimney cap", (2.75, 1.27, 7.07), (1.08, 1.06, .17), "ash")
    export("HearthInn")

    # Open forge with a closed rear chimney and an accessible front bay.
    box("Smithy raised floor", (0, 0, .14), (6.9, 5.15, .28), "stone")
    box("Forge back wall", (0, 2.22, 1.57), (6.55, .47, 2.82), "stone")
    for x in (-3.10, 3.10):
        beam("Smithy post", (x, -2.23, .27), (x, -2.23, 3.28), .30)
        beam("Rafter brace", (x, -2.23, 2.15), (x * .74, -2.23, 3.10), .16)
    beam("Lintel", (-3.38, -2.23, 3.13), (3.38, -2.23, 3.13), .27)
    roof(3.63, 2.72, 3.3, 1.18)
    box("Forge chimney breast", (1.86, 1.70, 2.79), (1.81, 1.22, 5.02), "ash", .085)
    box("Forge chimney cap", (1.86, 1.70, 5.30), (2.04, 1.45, .20), "stone")
    box("Hearth firebrick", (1.86, .89, .69), (1.75, 1.22, .17), "stone")
    box("Banked forge coals", (1.86, .82, .80), (1.36, .87, .11), "amber")
    cylinder("Anvil stump", (-1.81, -.25, .63), .39, .70, "beam")
    box("Anvil foot", (-1.81, -.25, 1.00), (.87, .4, .11), "iron")
    box("Anvil waist", (-1.81, -.25, 1.12), (.42, .27, .22), "iron")
    box("Anvil face", (-1.81, -.25, 1.26), (1.04, .34, .11), "iron")
    box("Quench trough", (2.31, -1.7, .44), (1.29, .62, .35), "beam")
    export("OpenSmithy")

    # Three sheltered market bays with an unobstructed front aisle.
    box("Market floor", (0, 0, .10), (8.8, 4.23, .20), "stone")
    for x in (-4.11, -1.37, 1.37, 4.11):
        for y in (-1.85, 1.83):
            box("Pier stone shoe", (x, y, .28), (.42, .42, .17), "stone")
            beam("Market upright", (x, y, .33), (x, y, 3.11), .24)
        if x < 4:
            beam("Market knee brace", (x, -1.85, 2.41), (x + .54, -1.85, 3.01), .15)
    beam("Market front header", (-4.38, -1.85, 3.07), (4.38, -1.85, 3.07), .25)
    roof(4.58, 2.30, 3.21, .98)
    for index, x in enumerate((-2.74, 0, 2.74)):
        box("Vendor counter top", (x, 1.09, 1.04), (1.98, .83, .15), "beam")
        for sign in (-1, 1):
            beam("Counter trestle", (x + sign * .69, 1.1, .2), (x + sign * .73, 1.1, 1.04), .14)
        for j in range(3):
            box("Store goods", (x + (j - 1) * .55, 1.18, 1.25), (.39, .44, .29), "linen" if j == index else "beam")
        box("Hanging trade valance", (x, -2.15, 2.81), (2.32, .075, .42), "shutters" if index != 1 else "linen")
    export("MarketShelter")

    # Landmark and seating. The well is closed by iron bars across the top.
    cylinder("Well footing", (0, 0, .11), 1.27, .22, "stone", 14)
    cylinder("Well wall", (0, 0, .52), 1.02, .79, "stone", 14)
    cylinder("Cap stones", (0, 0, .97), 1.13, .19, "stone", 14)
    cylinder("Dark well mouth", (0, 0, 1.071), .83, .022, "ash", 14)
    for i in range(-4, 5):
        half = math.sqrt(.8 ** 2 - (i * .17) ** 2)
        beam("Iron well grate", (-half, i * .17, 1.105), (half, i * .17, 1.105), .034, "iron")
    for x in (-.99, .99):
        beam("Well mast", (x, 0, .86), (x, 0, 2.81), .18)
    beam("Well windlass", (-1.09, 0, 2.35), (1.09, 0, 2.35), .12, "iron")
    roof(1.43, 1.0, 2.82, .69)
    export("MarketWell")

    # Rounded stone pavers on a thin collision deck.
    box("Square lane base", (0, 0, .043), (4.14, 4.0, .086), "stone", .014)
    for row in range(8):
        for col in range(6):
            x = -1.68 + col * .67
            y = -1.75 + row * .5
            height = rng.uniform(.026, .044)
            box("Dressed paver", (x, y, .084), (.643, .474, height), "stone", .034, segments=1)
    export("SquareLane4m")

    for name, pos, yaw in [
        ("HearthInn", (-6.4, 4.7, 0), -13), ("OpenSmithy", (7, 1.0, 0), 20),
        ("MarketShelter", (1.8, 8.9, 0), 0), ("MarketWell", (-1.9, .5, 0), 0),
        ("SquareLane4m", (0, -5.05, 0), 0),
    ]:
        assets[name].location = pos
        assets[name].rotation_euler.z = math.radians(yaw)
    for x, y in ((0, -1.05), (3.8, -3.5), (-4.1, -4.7)):
        paving = assets["SquareLane4m"].copy()
        bpy.context.collection.objects.link(paving)
        paving.location = (x, y, 0)
    bpy.ops.mesh.primitive_plane_add(size=180, location=(0, 0, -.015))
    ground = bpy.data.materials.new("Packed courtyard earth")
    ground.diffuse_color = (.24, .184, .125, 1)
    bpy.context.object.data.materials.append(ground)
    bpy.ops.object.camera_add(location=(15, -22, 17))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, 2, 1.6)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 31.8
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.render.resolution_x, scene.render.resolution_y = 1560, 1040
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(out / "emberwatch-civic-square.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "Emberwatch-Civic-Square.blend"))
    bpy.ops.render.render(write_still=True)
