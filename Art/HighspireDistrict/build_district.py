"""Procedural authoring source for Highspire's gate and market district."""
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
rng = random.Random(8311)

colors = {
    "limestone": (157, 153, 140), "trim": (187, 178, 150), "slate": (46, 69, 84),
    "oak": (95, 65, 47), "plaster": (206, 183, 145), "blue": (39, 67, 132),
    "brass": (208, 147, 64), "iron": (69, 75, 79), "glass": (227, 172, 71),
    "pavers": (131, 124, 109), "water": (57, 111, 118), "canvas": (218, 191, 142),
    "red": (120, 47, 40), "dark": (37, 32, 32), "moss": (100, 108, 76), "earth": (144, 115, 79),
}
tiles = tuple(colors)
size = 384
width = height = 4 * size

def rgb(key, x, y):
    grain = ((x * 39 ^ y * 109 ^ (x*y*19)) % 19) - 9
    wave = 3 * math.sin(x*.051+y*.011) + 5*math.cos(y*.027+x*.017)
    if key in ("limestone","slate","pavers"):
        rh, rw = (48,84) if key=="limestone" else (53,91)
        row = y//rh
        offset = x + row%2*(rw//2)
        edge = min(offset%rw, y%rh)
        wave += -32 if edge < 4 else 8 if edge < 8 else 0
        wave += ((row*5 + offset//rw*17)%13)-6
    if key == "oak":
        wave += -10 if (x + 3*math.sin(y*.035))%22 < 2 else 4
    if key in ("canvas","blue","red"):
        wave += 3 * math.sin(x*2.4)*math.cos(y*2.5)
    if key == "water":
        wave += 4 * math.sin(x*.095+3*math.sin(y*.042)) + 6*math.sin(y*.087+x*.04)
    return bytes(max(0,min(255,round(c+grain+wave))) for c in colors[key])

def chunk(kind, data):
    return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data))

data=[]
for y in range(height):
    data.append(b"\0"+b"".join(rgb(tiles[y//size*4+x//size],x%size,y%size) for x in range(width)))
atlas=out/"highspire_atlas.png"
atlas.write_bytes(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">2I5B",width,height,8,2,0,0,0))
                  +chunk(b"IDAT",zlib.compress(b"".join(data),9))+chunk(b"IEND",b""))
material = bpy.data.materials.new("Highspire district atlas")
material.use_nodes = True
nodes=material.node_tree.nodes
image=nodes.new("ShaderNodeTexImage")
image.image=bpy.data.images.load(str(atlas))
shader=nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value=.84
material.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])
parts=[]
assets={}

def finish(obj,key):
    obj.data.materials.append(material)
    tile=tiles.index(key)
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name="Atlas")
    for poly in obj.data.polygons:
        points=[obj.data.vertices[obj.data.loops[i].vertex_index].co for i in poly.loop_indices]
        flat=max(range(3),key=lambda axis:abs(poly.normal[axis]))
        a,b=[axis for axis in range(3) if axis!=flat]
        low_a,low_b=min(p[a] for p in points),min(p[b] for p in points)
        span_a=max(max(p[a] for p in points)-low_a,1e-4)
        span_b=max(max(p[b] for p in points)-low_b,1e-4)
        for i,p in zip(poly.loop_indices,points):
            uv.data[i].uv=((tile%4+.023+.954*(p[a]-low_a)/span_a)/4,
                           (3-tile//4+.023+.954*(p[b]-low_b)/span_b)/4)
    parts.append(obj)
    return obj

def raw(name, vertices, faces, key):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj,key)

def box(name, location, dimensions, key, bevel=.035):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    obj=bpy.context.object
    obj.name=name
    obj.scale=dimensions
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new("Cut edges","BEVEL")
        mod.width=bevel
        mod.segments=2 if bevel>.036 else 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj,key)

def beam(name,start,end,width,thickness,key="oak"):
    start,end=Vector(start),Vector(end)
    obj=box(name,(start+end)/2,(width,thickness,(end-start).length),key,.019)
    obj.rotation_euler=(end-start).to_track_quat("Z","Y").to_euler()
    return obj

def ring(name,cx,cy,sections,key,count=12,rotation=math.pi/8):
    verts=[(cx+r*math.cos(rotation+i*math.tau/count),cy+r*math.sin(rotation+i*math.tau/count),z)
           for z,r in sections for i in range(count)]
    faces=[(j*count+i,j*count+(i+1)%count,(j+1)*count+(i+1)%count,(j+1)*count+i)
           for j in range(len(sections)-1) for i in range(count)]
    faces += [tuple(range(count-1,-1,-1)), tuple(range(len(verts)-count,len(verts)))]
    return raw(name,verts,faces,key)

def arch(label,cx,y,spring,rx,rz,thick,depth,key="trim",segments=13):
    for j in range(segments):
        a=j*math.pi/segments+.012
        b=(j+1)*math.pi/segments-.012
        lip=thick*(1.18 if j==segments//2 else 1)
        face=[(cx+(rx+d)*math.cos(t),spring+(rz+d)*math.sin(t))
              for d,t in ((0,a),(lip,a),(lip,b),(0,b))]
        verts=[(x,y+h,z) for h in (-depth/2,depth/2) for x,z in face]
        raw(label,verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],key)

def window(x,y,z,width=.72,height=1.18,key="glass"):
    box("Recessed window frame",(x,y,z),(width+.19,.15,height+.19),"dark",.038)
    box("Window panes",(x,y-.09,z),(width,.075,height),key,.02)
    box("Window central mullion",(x,y-.143,z),(.08,.075,height+.02),"iron",.014)
    box("Window cross rail",(x,y-.148,z-.10),(width,.07,.077),"iron",.015)
    box("Limestone sill",(x,y-.13,z-height/2-.11),(width+.28,.32,.14),"trim",.038)

def hanging_lantern(x,y,z):
    beam("Lantern steel hook",(x,y+.18,z+.22),(x,y-.09,z+.22),.05,.05,"iron")
    box("Lantern glass",(x,y-.08,z),(.13,.13,.27),"glass",.023)
    for h in (-.15,.14):
        box("Lantern cap",(x,y-.08,z+h),(.21,.20,.065),"iron",.019)

def gable_roof(cx,cy,eave,half_width,half_length,rise):
    slope=math.atan2(rise,half_length)
    for direction in (-1,1):
        roof=box("Slate roof plane",(cx,cy+direction*half_length/2,eave+rise/2),
                 (half_width*2, math.hypot(half_length,rise),.20),"slate",.035)
        roof.rotation_euler.x=-direction*slope
        for side in (-1,1):
            beam("Roof coping",(cx+side*half_width,cy,eave+rise+.11),(cx+side*half_width,cy+direction*half_length,eave+.12),
                 .19,.17,"oak")
        for nx in range(int(half_width*3.8)):
            x=cx-half_width+.23+nx*.52
            edge=box("Shaped eave tile",(x,cy+direction*(half_length-.24),eave+.27),
                     (.45,.48,.085),"slate",.013)
            edge.rotation_euler.x=-direction*slope
    box("Lead ridge seam",(cx,cy,eave+rise+.15),(half_width*2+.07,.25,.13),"slate",.04)

def banner(x,y,z,width=1.0,height=2.0):
    verts=[]
    for j in range(7):
        u=j/6
        for s in (-1,1):
            verts.append((x+s*width*.5,y-.085*math.sin(u*math.pi),z-height*u+.11*abs(s)*(u**6)))
    faces=[(2*i,2*i+2,2*i+3,2*i+1) for i in range(6)]
    raw("Cobalt district banner",verts,faces+[(d,c,b,a) for a,b,c,d in faces],"blue")
    beam("Banner pole",(x-width*.62,y,z+.04),(x+width*.62,y,z+.04),.089,.09,"brass")
    box("Banner gold hem",(x,y-.045,z-height+.13),(width,.035,.06),"brass",.008)

def export(name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj=bpy.context.object
    obj.name=obj.data.name=name
    bpy.ops.export_scene.gltf(filepath=str(out/(name+".glb")),use_selection=True)
    bpy.ops.export_scene.fbx(filepath=str(out/(name+".fbx")),use_selection=True,path_mode="COPY",embed_textures=True)
    print(f"ASSET {name}: {sum(len(p.vertices)-2 for p in obj.data.polygons)} triangles",flush=True)
    assets[name]=obj
    parts.clear()

# Gate frontage. The central passage is open all the way through.
for x in (-4.58,4.58):
    box("Battered tower base",(x,0,.39),(3.34,3.88,.78),"limestone",.10)
    box("Tower wall shaft",(x,0,3.86),(2.94,3.28,6.2),"limestone",.095)
    for z in (1.0,3.07,6.75,7.02):
        box("Tower stringcourse",(x,0,z),(3.17,3.51,.19),"trim",.055)
    for j in range(5):
        for side in (-1,1):
            box("Corner quoin",(x+side*1.35,-1.69,1.16+j*1.04),(.31,.26,.49),"trim",.047)
    for z in (2.38,5.18): window(x,-1.7,z,.49,.96,"dark")
    ring("Faceted slate spire",x,0,[(7.07,2.31),(7.28,2.34),(8.35,1.39),(9.35,.56),(9.78,.08)],"slate",8)
    ring("Brass finial",x,0,[(9.7,.1),(9.99,.15),(10.2,.05)],"brass",10)
    hanging_lantern(x,-2.05,1.89)
    banner(x,-1.77,5.0,.95,1.85)
for y in (-1.43,1.43):
    arch("Gate arch stone",0,y,3.22,2.91,1.66,.35,.74,segments=17)
    box("Arch crown parapet",(0,y,5.66),(6.96,.66,1.07),"limestone",.05)
    for j in range(7):
        box("Crenel cap",(-3.02+j*1.01,y,6.40),(.56,.71,.45),"trim",.048)
for x in (-3.14,3.14):
    box("Gate pier",(x,0,1.62),(.59,3.76,3.24),"limestone",.05)
box("Gate upper crossing",(0,0,5.48),(6.3,3.39,.78),"limestone",.06)
box("Gate crest tablet",(0,-1.82,5.54),(1.18,.26,.58),"blue",.065)
box("Crest gilt bar",(0,-1.98,5.54),(.74,.075,.11),"brass",.018)
export("Highspire_Gatehouse")

# Twin-level merchant guild hall, projecting window bay, entrance portico.
box("Guild stone base",(0,0,.21),(8.46,5.72,.42),"limestone",.064)
box("Stone lower hall",(0,.08,1.75),(8.05,5.11,2.76),"limestone",.065)
box("Upper plaster hall",(0,0,3.96),(8.4,5.33,1.88),"plaster",.051)
for z in (3.24,4.83):
    box("Coped floor edge",(0,0,z),(8.6,5.55,.23),"trim",.052)
for x in (-3.93,-2.17,2.17,3.93):
    box("Upper oak frame",(x,-2.72,3.98),(.19,.20,1.68),"oak",.03)
for x in (-2.99,2.99):
    window(x,-2.62,1.87,.76,1.22,"dark")
    window(x,-2.74,3.95,.85,1.20)
box("Entrance backing",(0,-2.58,1.48),(1.8,.15,2.31),"dark",.06)
arch("Trade hall carved arch",0,-2.74,1.84,.88,.71,.21,.33,segments=11)
for x in (-1.0,1.0): box("Door pilaster",(x,-2.79,1.17),(.24,.33,1.48),"trim",.04)
for step in range(3):
    box("Guild stair",(0,-3.22+step*.19,.055+step*.052),(2.48,1.03-step*.20,.11+step*.105),"trim",.026)
for x in (-1.57,1.57): hanging_lantern(x,-2.98,1.82)
box("Projecting bay sill",(0,-2.95,3.35),(2.13,.72,.21),"trim",.045)
box("Oriel window bay",(0,-2.95,3.95),(1.95,.78,1.21),"oak",.049)
for x in (-.62,0,.62): window(x,-3.36,3.94,.41,.8)
box("Oriel copper canopy",(0,-2.98,4.63),(2.23,.95,.18),"slate",.043)
gable_roof(0,0,4.91,4.48,2.99,1.71)
box("Chimney stack",(2.51,1.22,6.02),(.71,.84,2.13),"limestone",.051)
box("Chimney cap",(2.51,1.22,7.12),(.88,1.03,.23),"trim",.061)
banner(2.1,-2.86,3.97,.67,1.44)
export("Highspire_GuildHall")

# Tanners' and farriers' workshop block with a work-facing awning and cargo.
box("Workshop plinth",(0,0,.14),(6.28,4.9,.28),"limestone",.06)
box("Workshop lower wall",(0,.06,1.32),(6.0,4.51,2.11),"limestone",.05)
box("Workshop upper overhang",(0,-.05,2.83),(6.24,4.7,1.08),"plaster",.055)
for x in (-2.87,-1.14,1.14,2.87):
    box("Painted workshop timber",(x,-2.45,2.88),(.19,.2,1.20),"oak",.021)
for x in (-2.1,1.94): window(x,-2.46,2.89,.53,.61,"dark")
box("Workshop door",(0,-2.27,1.24),(1.45,.19,1.94),"oak",.051)
arch("Shallow work door arch",0,-2.41,1.6,.72,.53,.15,.20,segments=9)
gable_roof(0,0,3.43,3.36,2.63,1.23)
awning=box("Oxblood workshop cover",(0,-2.91,2.51),(4.31,1.19,.11),"red",.025)
awning.rotation_euler.x=math.radians(12)
for x in (-1.88,1.88):
    beam("Awning iron support",(x,-2.24,2.26),(x,-3.36,2.55),.1,.09,"oak")
    hanging_lantern(x,-2.68,1.86)
for x,y in ((1.64,-2.92),(2.34,-2.9),(-2.23,-2.87)):
    ring("Banded workshop barrel",x,y,[(0,.29),(.16,.36),(.57,.38),(.80,.31)],"oak",16)
    for h in (.17,.65): ring("Barrel iron band",x,y,[(h,.366),(h+.055,.368)],"iron",16)
export("Highspire_Workshop")

# Three stall bays remain separate from through traffic.
box("Market gallery foundation",(0,0,.16),(8.9,3.3,.32),"limestone",.06)
for x in (-4.19,-1.4,1.4,4.19):
    for y in (-1.29,1.29):
        box("Portico column base",(x,y,.36),(.57,.57,.37),"trim",.048)
        box("Portico oak column",(x,y,1.61),(.23,.23,2.26),"oak",.024)
for y in (-1.35,1.35):
    box("Gallery bearing beam",(0,y,2.79),(8.85,.32,.29),"oak",.043)
for j,x in enumerate((-2.78,0,2.78)):
    roof=box("Market striped canopy",(x,-.07,3.02),(2.86,3.33,.145),"blue" if j!=1 else "canvas",.025)
    roof.rotation_euler.x=math.radians(8)
    box("Market scallop hem",(x,-1.69,2.72),(2.73,.085,.29),"blue" if j!=1 else "canvas",.023)
    box("Counter",(x,.66,1.12),(2.02,.68,.15),"oak",.04)
    for side in (-1,1):
        box("Counter support",(x+side*.77,.67,.73),(.14,.59,.72),"oak",.025)
    for k in range(3):
        box("Market cargo",(x-.61+k*.61,.74,1.38),(.46,.43,.38),"oak" if k!=1 else "red",.049)
        box("Cargo fastener",(x-.61+k*.61,.508,1.37),(.06,.04,.30),"iron",.018)
export("Highspire_MarketLoggia")

# Basin, inner water face, central column and bronze cap.
ring("Fountain tier",0,0,[(0,1.64),(.15,1.67),(.23,1.58),(.29,1.47)],"trim",12)
ring("Solid basin base",0,0,[(.22,1.43),(.57,1.42),(.66,1.50),(.78,1.52),(.80,1.28)],"limestone",16)
verts=[(r*math.cos(i*math.tau/20),r*math.sin(i*math.tau/20),z)
       for r,z in ((1.49,.77),(1.27,.77),(1.23,.66)) for i in range(20)]
raw("Basin rolled rim",verts,[(j*20+i,j*20+(i+1)%20,(j+1)*20+(i+1)%20,(j+1)*20+i)
                              for j in range(2) for i in range(20)],"trim")
ring("Still fountain water",0,0,[(.804,1.265),(.811,1.265)],"water",24)
ring("Fountain pier",0,0,[(.77,.38),(.89,.44),(1.09,.26),(1.69,.21),(1.78,.38),(1.91,.48)],"trim",12)
ring("Fountain crown",0,0,[(1.91,.38),(2.04,.24),(2.22,.10),(2.29,.037)],"brass",12)
export("Highspire_Fountain")

# Modular terrace supports and 4.4m-wide ramp. The seam at Y=0 is level.
box("Raised market substructure",(0,2.65,.59),(8.2,5.30,1.18),"limestone",.061)
box("Raised stone deck",(0,2.65,1.195),(8.26,5.30,.098),"pavers",.024)
for x in (-3.99,3.99):
    box("Parapet sill",(x,2.69,1.33),(.23,4.94,.17),"trim",.025)
    for j in range(6):
        box("Balustrade pillar",(x,.58+j*.78,1.57),(.19,.22,.47),"limestone",.024)
    box("Parapet hand rail",(x,2.53,1.81),(.31,4.25,.15),"trim",.039)
export("Highspire_MarketTerrace")
raw("4.4 m cart ramp",
    [(-2.2,-6.5,0),(2.2,-6.5,0),(-2.2,0,0),(2.2,0,0),(-2.2,0,1.244),(2.2,0,1.244)],
    [(0,1,5,4),(2,4,5,3),(0,4,2),(1,3,5),(0,2,3,1)],"pavers")
for x in (-2.28,2.28):
    beam("Stone ramp coping",(x,-6.5,.055),(x,0,1.31),.15,.12,"trim")
export("Highspire_CartRamp")
raw("Cobbled lane", [(-2.2,0,.024),(2.2,0,.024),(2.2,4,.024),(-2.2,4,.024)], [(0,1,2,3)],"pavers")
for y in (.51,1.49,2.44,3.38):
    for x in (-2.28,2.28):
        obj=box("Road kerb",(x,y,.073),(.23,rng.uniform(.82,.98),.13),"trim",.036)
        obj.rotation_euler.z=rng.uniform(-.04,.04)
export("Highspire_CobbledLane4m")

def stage(name,point,yaw=0):
    obj=assets[name]
    obj.location, obj.rotation_euler.z=point,yaw

stage("Highspire_Gatehouse",(0,-7.5,0))
stage("Highspire_GuildHall",(-7.4,8.05,1.244))
stage("Highspire_Workshop",(9.1,1.8,0),math.radians(-90))
stage("Highspire_MarketLoggia",(3.3,9.7,0))
stage("Highspire_Fountain",(0.0,1.25,0))
stage("Highspire_MarketTerrace",(-7.4,3.1,0))
stage("Highspire_CartRamp",(-7.4,3.1,0))
stage("Highspire_CobbledLane4m",(0,-14.5,0))
for y in (-10.5,-6.5,-2.5):
    extra=assets["Highspire_CobbledLane4m"].copy()
    bpy.context.collection.objects.link(extra)
    extra.location=(0,y,0)
# Plaza paving under the whole square, below individual modules.
box("Market square",(.24,2.68,-.075),(25.65,18.8,.14),"pavers",.08)
box("Guild uphill foundation",(-7.4,7.53,.613),(8.64,6.0,1.226),"limestone",.067)
for x,y in ((-9.2,4),(6.2,7.3),(8.1,-2.5)):
    box("Street stone trough",(x,y,.26),(1.28,.73,.52),"trim",.08)
    box("Trough dark surface",(x,y,.519),(1.08,.51,.022),"water",.026)
for x in (-8.2,8.2):
    for j in range(4):
        box("Merchant square stone post",(x+j*.88*(1 if x>0 else -1),-5.24,.37),(.39,.39,.74),"limestone",.06)

bpy.ops.mesh.primitive_plane_add(size=200)
ground=bpy.context.object
ground.location.z=-.155
mat=bpy.data.materials.new("Highspire ground")
mat.diffuse_color=(.245,.25,.25,1)
ground.data.materials.append(mat)
bpy.ops.object.camera_add()
camera=bpy.context.object
camera.data.type="ORTHO"
scene.camera=camera
scene.render.engine="BLENDER_WORKBENCH"
scene.display.shading.color_type="TEXTURE"
scene.display.shading.light="STUDIO"
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.cavity_type="BOTH"
scene.display.shading.curvature_ridge_factor=1.14
scene.display.shading.curvature_valley_factor=1.14
scene.display.shading.background_type="WORLD"
if scene.world is None:
    scene.world=bpy.data.worlds.new("Slate sky")
scene.world.color=(.18,.22,.26)
scene.render.resolution_x=1740
scene.render.resolution_y=1160
scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"

def view(name, eye, focus, scale):
    camera.location=eye
    camera.rotation_euler=(Vector(focus)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale
    scene.render.filepath=str(out/name)

view("highspire-district.png",(23,-30,25),(0,.8,2.55),38.7)
image.image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(out/"Highspire-Gate-District.blend"))
bpy.ops.render.render(write_still=True)
view("highspire-street.png",(-13,-16,9),(0,1.0,3.05),30.2)
bpy.ops.render.render(write_still=True)
