"""Build the Three-Chimes mountain relay and modular track in Blender."""
import bpy
import math
import random
import struct
import sys
import zlib
from pathlib import Path
from mathutils import Vector

out = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
out.mkdir(parents=True, exist_ok=False)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
rng = random.Random(71492)

# One packed atlas. Colour, joints, grain and stains are authored procedurally.
palette = {
    "stone": (133, 125, 107), "plaster": (222, 184, 132), "oak": (96, 60, 37),
    "slate": (49, 81, 92), "iron": (62, 66, 68), "brass": (182, 124, 55),
    "dirt": (151, 107, 68), "moss": (105, 108, 66),
    "red": (153, 58, 43), "canvas": (216, 173, 101), "glass": (237, 165, 59),
    "dark": (34, 24, 23),
}
tiles = tuple(palette)
tile_width = 512
width, height = 2048, 2048

def colour(tile, x, y):
    if tile >= len(tiles):
        return b"\0\0\0"
    name = tiles[tile]
    x, y = x / 2, y / 2
    noise = ((int(x * 2) * 17 ^ int(y * 2) * 67 ^ int(x * y * 20)) % 17) - 8
    stains = 5 * math.sin(x * .036 + math.sin(y * .04)) + 4 * math.cos(y * .057)
    if name in ("stone", "slate"):
        joint = 32 if name == "stone" else 38
        xx = (x + (y // 32 % 2) * joint) % (joint * 2)
        yy = y % 32
        stains += -33 if xx < 3 or yy < 3 else (11 if yy >= 28 else 0)
    if name == "oak":
        grain = (x + 2 * math.sin(y / 16) + math.sin(y / 6)) % 11
        stains += -16 if grain < 1.25 else 2
    if name == "canvas":
        stains += 7 if x % 64 < 30 else -5
    if name == "canvas":
        stains += 3 * math.cos(x * 5.4) * math.cos(y * 6.2)
    if name == "dirt":
        stains += 4 * math.sin(x * .042) * math.cos(y * .032)
    if name == "plaster":
        stains += 6 * math.sin(x * .065 + y * .04) * math.sin(y * .1)
    if name == "brass":
        stains += 4 * math.sin(y * .085 + 2 * math.cos(x * .043))
    return bytes(max(0, min(255, round(c + noise + stains))) for c in palette[name])

def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))

pixels = []
for y in range(height):
    pixels.append(b"\0" + b"".join(colour(y // tile_width * 4 + x // tile_width, x % tile_width, y % tile_width)
                                     for x in range(width)))
atlas_path = out / "three_chimes_detail_atlas.png"
atlas_path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">2I5B", width, height, 8, 2, 0, 0, 0))
                       + chunk(b"IDAT", zlib.compress(b"".join(pixels), 9)) + chunk(b"IEND", b""))
material = bpy.data.materials.new("TC_atlas")
material.use_nodes = True
bsdf = material.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Roughness"].default_value = .82
tex = material.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(str(atlas_path))
material.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

regional_names = ("stone", "plaster", "oak", "dirt", "moss")
regional_materials = {}
for surface in regional_names:
    slot = material.copy()
    slot.name = "TC_" + surface
    regional_materials[surface] = slot

assets = {}
parts = []

def finish_mesh(obj, surface):
    obj.data.materials.clear()
    obj.data.materials.append(regional_materials.get(surface, material))
    index = tiles.index(surface)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Atlas")
    uv.name = "Atlas"
    metres = obj.data.uv_layers.new(name="RegionalMetres", do_init=False)
    obj.data.uv_layers.active_index = 0
    for face in obj.data.polygons:
        face.material_index = 0
        vertices = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        axes = [a for a in range(3) if a != max(range(3), key=lambda k: abs(face.normal[k]))]
        origin = [min(v[a] for v in vertices) for a in axes]
        size = [max(.001, max(v[a] for v in vertices) - low) for a, low in zip(axes, origin)]
        for loop, v in zip(face.loop_indices, vertices):
            a, b = [(v[axis] - low) / extent for axis, low, extent in zip(axes, origin, size)]
            uv.data[loop].uv = ((index % 4 + .018 + .964 * a) / 4,
                                (3 - index // 4 + .018 + .964 * b) / 4)
            if surface == "oak":
                along, across = (a, b) if size[0] >= size[1] else (b, a)
                # Village WoodTrim is an atlas. Keep the whole face in the dark timber band.
                metres.data[loop].uv = (.025 + .95 * along, .405 + .255 * across)
            elif surface == "stone":
                # Use the masonry courses, below the atlas's smooth trim panels.
                metres.data[loop].uv = ((v[axes[0]] - origin[0]) / 2.0, .026 + .345 * b)
            else:
                metres.data[loop].uv = tuple((v[axis] - low) / 2.1 for axis, low in zip(axes, origin))
    parts.append(obj)
    return obj

def block(label, pos, size, surface, bevel=.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.name = label
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Worn edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2 if bevel >= .029 else 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish_mesh(obj, surface)

def beam(label, a, b, width, depth, surface="oak", bevel=.02):
    a, b = Vector(a), Vector(b)
    obj = block(label, (a+b)/2, (width, depth, (b-a).length), surface, bevel)
    obj.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()
    return obj

def mesh(label, verts, faces, surface):
    data = bpy.data.meshes.new(label)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(label, data)
    bpy.context.collection.objects.link(obj)
    return finish_mesh(obj, surface)

def lathe(label, pos, profile, surface, sides=24):
    verts = [(radius*math.cos(a*math.tau/sides), radius*math.sin(a*math.tau/sides), z)
             for z, radius in profile for a in range(sides)]
    faces = [(j*sides+i, j*sides+(i+1)%sides, (j+1)*sides+(i+1)%sides, (j+1)*sides+i)
             for j in range(len(profile)-1) for i in range(sides)]
    obj = mesh(label, verts, faces, surface)
    obj.location = pos
    return obj

def roof(cx, cy, z, half_length, half_span, rise):
    slope = math.atan2(rise, half_span)
    for side in (-1, 1):
        panel = block("Slate roof", (cx, cy+side*half_span/2, z+rise/2),
                      (2*half_length, math.hypot(half_span, rise), .17), "slate", .03)
        panel.rotation_euler.x = -side*slope
        for row in range(2):
            y = side * (half_span - .31 - row*.43)
            top = z + rise*(1-abs(y)/half_span)
            n = math.floor(2*half_length/.54)
            for j in range(n):
                x = cx-half_length+.28+j*.54
                shingle = block("Cut eave shingle", (x,cy+y,top+.155+row*.026), (.51,.58,.105), "slate", .012)
                shingle.rotation_euler.x = -side*slope
        for end in (-1,1):
            beam("Roof verge", (cx+end*half_length,cy,z+rise+.13),
                 (cx+end*half_length,cy+side*half_span,z+.10), .17,.16, "oak", .02)
    block("Ridge cap", (cx,cy,z+rise+.11), (2*half_length+.15,.29,.18), "slate", .04)
    for j in range(1, int(half_length * 3)):
        x = cx - half_length + j * .68
        block("Ridge joint", (x,cy,z+rise+.19),(.15,.28,.10),"slate",.028)

def crate(x,y,z, scale=1):
    block("Cargo crate", (x,y,z+.33*scale), (.69*scale,.62*scale,.66*scale), "oak", .025)
    for sign in (-1,1):
        for face in (-1,1):
            block("Crate binding", (x+sign*.22*scale,y+face*.319*scale,z+.33*scale),
                  (.09*scale,.042*scale,.62*scale), "iron", .008)

def export(name):
    # Bake model-local mesh as a single asset with an origin at ground level.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    active = parts[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    active.name = active.data.name = name
    bpy.ops.export_scene.gltf(filepath=str(out / (name + ".glb")), use_selection=True)
    bpy.ops.export_scene.fbx(filepath=str(out / (name + ".fbx")), use_selection=True,
                             path_mode="COPY", embed_textures=True)
    triangles = sum(len(face.vertices)-2 for face in active.data.polygons)
    print(f"ASSET {name}: {triangles} triangles", flush=True)
    assets[name] = active
    parts.clear()

# Storehouse with an actual recessed arched loading opening and stone buttresses.
block("Dressed stone plinth", (0,0,.19), (7.7,5.0,.38), "stone", .06)
block("Recessed shop shadow", (0,1.15,1.48), (2.5,.18,2.4), "dark")
block("Rear wall", (0,2.14,1.73), (7.27,.30,2.8), "plaster", .035)
for side in (-1,1):
    block("End wall", (side*3.57,0,1.73), (.30,4.44,2.8), "plaster", .035)
    block("Front wall wing", (side*2.39,-2.12,1.73),(2.37,.32,2.8), "plaster",.03)
    block("Stone arch pier", (side*1.10,-2.27,1.01), (.43,.57,1.55), "stone",.035)
    for x in (1.50, 3.42):
        block("Framing post", (side*x,-2.34,1.78), (.25,.24,2.88), "oak", .026)
    block("Inset shutter", (side*2.48,-2.30,1.95), (1.03,.16,.97), "dark", .035)
    for dx in (-.29,0,.29):
        block("Red shutter boards", (side*2.48+dx,-2.41,1.95),(.26,.09,.82),"red",.018)
    beam("Diagonal wall brace", (side*1.52,-2.38,2.74), (side*2.1,-2.38,2.38), .13,.13)
    for end in (-1,1):
        block("Corner shoulder", (side*3.71,end*2.18,.67),(.38,.43,1.20),"stone",.035)
block("Arched bay lintel wall", (0,-2.1,2.95), (2.53,.31,.42), "plaster",.025)
# Radial wedges replace the square prototype blocks around the loading arch.
for i in range(13):
    start = i * math.pi / 13 + .017
    stop = (i + 1) * math.pi / 13 - .017
    outer = 1.24 if i != 6 else 1.31
    profile = [(r * math.cos(t), 1.73 + .78 * r * math.sin(t)) for r, t in
               ((.88,start),(outer,start),(outer,stop),(.88,stop))]
    verts = [(x,y,z) for y in (-2.58,-2.04) for x,z in profile]
    faces = [(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
    mesh("Cut arch voussoir", verts, faces, "stone")
block("Front sill", (0,-2.63,.11), (2.76,.62,.22), "stone", .04)
block("Transom beam", (0,-2.34,3.08), (7.75,.29,.23), "oak", .025)
for x in (-3.66,3.66):
    verts = [(x,-2.2,3.15),(x,2.2,3.15),(x,0,4.76)]
    face = (0,1,2) if x>0 else (2,1,0)
    mesh("Plastered gable",verts,[face],"plaster")
    beam("Gable kingpost", (x,0,3.12),(x,0,4.77),.17,.19)
roof(0,0,3.18,4.08,2.53,1.72)
block("Tapered stone chimney", (2.4,.78,4.43),(.58,.64,1.88), "stone", .045)
block("Chimney crown", (2.4,.78,5.43),(.78,.83,.19), "stone", .045)
for x in (-1.28,1.28):
    block("Lantern back", (x,-2.62,1.88), (.20,.15,.32), "iron",.02)
    block("Lantern glass", (x,-2.73,1.86), (.17,.15,.23), "glass",.022)
    block("Lantern cap", (x,-2.73,2.00), (.23,.21,.08), "iron",.02)
export("ThreeChimes_Relayhouse")

# Open bell tower, full-shaped bell lip and clapper, flare braces, climb ladder.
block("Tower foundation", (0,0,.18), (3.07,3.07,.36), "stone", .08)
block("Setback tower course", (0,0,.40), (2.75,2.75,.16), "stone", .06)
for x in (-1.05,1.05):
    for y in (-1.05,1.05):
        block("Stone pier foot", (x,y,.65), (.61,.61,.37), "stone", .045)
        beam("Tower frame", (x*1.06,y*1.06,.78),(x*.91,y*.91,5.72), .31,.31)
    for direction in (-1,1):
        beam("Tower cross brace", (x,-direction*1.02,1.25),(x,direction*.98,3.45),.18,.16)
for y in (-1.04,1.04):
    beam("Tower collar beam", (-1.37,y,3.81),(1.37,y,3.81),.22,.27)
block("Belfry decking", (0,0,4.0), (2.78,2.76,.15), "oak", .027)
for x in (-1.25,1.25):
    block("Belfry railing", (x,0,4.52), (.15,2.50,.16), "oak",.017)
    for y in (-.57,0,.57):
        block("Rail baluster", (x,y,4.24),(.11,.11,.5),"oak",.015)
for x in (-.64,-.12):
    beam("Ladder stile", (x,-1.82,.30),(x,-1.30,4.06), .12,.10)
for j in range(11):
    h=.47+j*.317
    beam("Ladder rung",(-.69,-1.82+.137*h,h),(-.06,-1.82+.137*h,h),.075,.085)
beam("Bell axle", (-1.0,0,5.17),(1.0,0,5.17), .18,.18, "oak")
lathe("Cast bell", (0,0,4.42), [(0,.55),(.13,.51),(.20,.40),(.51,.26),(.65,.16),(.68,.07)], "brass")
lathe("Bell lip", (0,0,4.365), [(0,.50),(.055,.57),(.12,.55),(.14,.52)], "brass")
lathe("Iron clapper", (0,0,4.23), [(0,.05),(.09,.095),(.17,.063),(.49,.031)], "iron", 10)
beam("Bell pull", (.50,-.18,5.05),(.55,-.18,.98), .045,.047, "canvas", .002)
roof(0,0,5.35,1.74,1.73,.87)
beam("Tower finial", (0,0,6.17),(0,0,6.90),.11,.11,"iron",.01)
block("Red signal banner", (0,-1.18,3.17), (.69,.07,1.16), "red",.034)
beam("Banner rod", (-.51,-1.17,3.78),(.51,-1.17,3.78),.10,.10,"iron",.015)
export("ThreeChimes_BellTower")

# Loading bay with racks, faired canopy, hoist and cargo.
for x in (-2.42,2.42):
    for y in (-1.35,1.35):
        block("Loading foot", (x,y,.14), (.49,.49,.28), "stone", .05)
        block("Loading post", (x,y,1.48),(.23,.23,2.41),"oak",.045)
    beam("Side angle brace", (x,-1.35,1.68),(x,-.57,2.56),.15,.15)
block("Pallet floor", (0,0,.16), (4.91,2.94,.30), "oak",.04)
block("Stock rack", (0,1.10,1.06), (4.75,.54,.16),"oak",.04)
for x in (-1.44,-.64,1.02,1.80):
    crate(x, .97, 1.14, .75)
for x in (-1.8,-.95,1.78):
    crate(x,-.11,.32,rng.uniform(.81,1.12))
for y in (-1.41,1.41):
    block("Loading canopy beam", (0,y,2.61), (5.15,.19,.22),"oak",.03)
for d in (-1,1):
    panel=block("Stitched striped canvas", (0,d*.81,2.84),(5.32,1.71,.095),"canvas",.02)
    panel.rotation_euler.x=-d*.24
    for x in (-1.65,0,1.65):
        beam("Canvas seam",(x,0,3.10),(x,d*1.63,2.70),.066,.065,"red",.011)
lathe("Loading rope pulley", (.25,-1.43,2.28), [(-.1,.20),(0,.24),(.105,.20)],"iron",12)
beam("Hoist rope", (.25,-1.43,2.29),(.25,-1.43,1.09),.047,.05,"canvas",.004)
beam("Raised loading arm", (1.9,-1.39,2.6),(1.9,-2.32,2.58),.18,.17)
export("ThreeChimes_LoadingBay")

# Continuous surfaces; detail stones sit clear of connector edges.
def stone_detail(x, y, z, width, depth):
    obj = block("Edge paving stone", (x,y,z+.022),(width,depth,.072),"stone",.022)
    obj.rotation_euler.z = rng.uniform(-.40,.40)

def path_strip(points, label):
    samples = len(points)
    bands = [-2,-1.49,-.60,.61,1.53,2]
    for b,(lo,hi) in enumerate(zip(bands,bands[1:])):
        verts=[]
        for x,y,nx,ny,z in points:
            verts.extend([(x+nx*lo,y+ny*lo,z), (x+nx*hi,y+ny*hi,z)])
        faces=[(2*k,2*k+1,2*k+3,2*k+2) for k in range(samples-1)]
        mesh(label,verts,faces,"moss" if b in (0,4) else "dirt")
    for i,(x,y,nx,ny,z) in enumerate(points[1:-1]):
        for side in (-1,1):
            if rng.random() > .32:
                offset=side*rng.uniform(1.43,1.88)
                stone_detail(x+offset*nx,y+offset*ny,z,rng.uniform(.21,.36),rng.uniform(.23,.41))

path_strip([(0,i*.5,1,0,.025) for i in range(9)], "Straight lane")
export("RelayRoad_Straight4m")
path_strip([(5-5*math.cos(a),5*math.sin(a),math.cos(a),-math.sin(a),.025)
            for a in [i*math.pi/24 for i in range(13)]], "Quarter-turn lane")
export("RelayRoad_Bend90_R5")
path_strip([(0,i*.5,1,0,.025+i*.1) for i in range(13)], "Graded lane")
# Dressed cheeks taper down to the low connector.
for x in (-2,2):
    mesh("Graded stone verge", [(x,0,0),(x,6,0),(x,6,1.225)],[(0,1,2) if x>0 else (2,1,0)],"stone")
export("RelayRoad_Ramp6m")

# A 6m widened turnout narrows to the same 4m at both connection ends.
path_strip([(0,i*.5,1,0,.025) for i in range(13)], "Through carriageway")
mesh("Lay-by paved shoulder",[(2,0,.025),(4,1.2,.025),(4,4.8,.025),(2,6,.025)],[(0,1,2,3)],"stone")
for j in range(6):
    stone_detail(3.83,1.55+j*.54,.025,.30,.38)
export("RelayRoad_Turnout6m")

# The junction has 4 m interfaces and rounded inside corners.
verts = [(-2,0,.025),(2,0,.025),(2,1.72,.025),(2.32,2,.025),(5,2,.025),
         (5,6,.025),(2.32,6,.025),(2,6.28,.025),(2,8,.025),(-2,8,.025)]
mesh("Three-way junction surface", verts, [tuple(range(len(verts)))], "dirt")
for y in [i*.52+.35 for i in range(15)]:
    stone_detail(-1.76,y,.025,.28,.30)
    if y < 1.45 or y > 6.6:
        stone_detail(1.78,y,.025,.26,.30)
for x in (2.62,3.15,3.68,4.21,4.62):
    for y in (2.26,5.74):
        stone_detail(x,y,.025,.33,.24)
export("RelayRoad_Junction4m")

block("Marker plinth", (0,0,.12), (.55,.54,.24), "stone", .065)
block("Weathered post", (0,0,1.37), (.13,.15,2.35), "oak", .017)
for j, direction in enumerate((1,-1)):
    z=2.19-j*.41
    profile=[(-.47,-.14),(.43,-.14),(.67,0),(.43,.14),(-.47,.14)]
    verts=[(direction*x,y,z+h) for y in (-.062,.062) for x,h in profile]
    faces=[(0,1,2,3,4),(9,8,7,6,5)]
    faces.extend((k,k+5,(k+1)%5+5,(k+1)%5) for k in range(5))
    if direction<0:
        faces=[f[::-1] for f in faces]
    mesh("Ochre trail arrow",verts,faces,"red" if j else "oak")
    block("Sign iron pin",(-.08*direction,-.079,z),(.077,.047,.077),"iron",.010)
beam("Lantern bracket", (0,0,1.37),(-.32,-.11,1.37),.06,.06,"iron",.008)
block("Marker lantern glass",(-.32,-.12,1.20),(.15,.15,.20),"glass",.023)
block("Marker lantern lid",(-.32,-.12,1.33),(.21,.21,.07),"iron",.017)
export("ThreeChimes_TrailSign")

# A composed review scene: stock yard, loading door, signal tower and connected road.
def stage(asset, location, rotation=0):
    obj=assets[asset]
    obj.location=location
    obj.rotation_euler.z=rotation
    return obj

stage("ThreeChimes_Relayhouse",(-3.1,3.20,0))
stage("ThreeChimes_BellTower",(3.40,3.25,0))
stage("ThreeChimes_LoadingBay",(8.40,-5.15,0), math.radians(168))
stage("RelayRoad_Straight4m", (0,-9.15,0))
stage("RelayRoad_Bend90_R5",(0,-5.15,0))
stage("RelayRoad_Turnout6m",(5,-.15,0),math.radians(-90))
# Display the rise module as an upper spur with a separately placed support.
stage("RelayRoad_Ramp6m",(-8.45,-4.30,0))
stage("RelayRoad_Junction4m",(-25,-4.5,0))
stage("ThreeChimes_TrailSign",(-2.77,-5.45,0))
block("Ramp display support",(-8.45,2.36,.59),(4.20,1.32,1.19),"stone",.05)
mesh("Packed relay courtyard", [(-7.4,-3.7,-.018),(-1.8,-5.1,-.018),(4.5,-5.1,-.018),(10.6,-2,-.018),(9.8,2.9,-.018),(5.6,6,-.018),(-6.7,6.2,-.018)],[(0,1,2,3,4,5,6)],"dirt")
for pos in ((-.85,.83,0),(.12,.98,0),(4.50,1.15,0),(-5.67,6.16,0)):
    crate(*pos,.83)
for x,y,h in ((-7.25,2.7,1), (8.9,2.3,1.3),(-2.38,-8.2,.65)):
    block("Low milestone", (x,y,h*.45), (.56,.59,h*.9), "stone", .05)
    block("Moss cap", (x,y,h*.93), (.58,.59,.12),"moss", .04)

bpy.ops.mesh.primitive_plane_add(size=180)
ground=bpy.context.object
ground.name="Review ground"
ground.location.z=-.04
ground_mat=bpy.data.materials.new("Dry highland ground")
ground_mat.diffuse_color=(.33,.27,.19,1)
ground.data.materials.append(ground_mat)

bpy.ops.object.camera_add()
camera=bpy.context.object
camera.data.type="ORTHO"
scene.camera=camera
scene.render.engine="BLENDER_WORKBENCH"
scene.display.shading.color_type="TEXTURE"
scene.display.shading.light="STUDIO"
scene.display.shading.studiolight_rotate_z=.65
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.cavity_type="BOTH"
scene.display.shading.curvature_ridge_factor=1.12
scene.display.shading.curvature_valley_factor=1.05
scene.display.shading.background_type="WORLD"
if scene.world is None:
    scene.world=bpy.data.worlds.new("Review sky")
scene.world.color=(.16,.20,.23)
scene.render.resolution_x=1600
scene.render.resolution_y=1080
scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"

def view(file, eye, focus, scale):
    camera.location=eye
    camera.rotation_euler=(Vector(focus)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale
    scene.render.filepath=str(out/file)

view("three-chimes-overview.png",(12,-23,17),(1,-1.0,1.9),25.7)
tex.image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out/"Three-Chimes-Relay.blend"))
bpy.ops.render.render(write_still=True)
view("three-chimes-detail.png",(9,-17,7.5),(.8,1.6,2.55),17.8)
bpy.ops.render.render(write_still=True)
view("three-chimes-path-junction.png",(-31,-14,13),(-24,-.2,0),11.9)
bpy.ops.render.render(write_still=True)
