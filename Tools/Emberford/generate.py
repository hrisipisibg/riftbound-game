"""Build the Riftbound kit with blender -b --factory-startup --python generate.py -- --output PATH."""
import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from meshkit import Build, PALETTE, TEXTURES, make_materials, make_textures
from structures import (house, coursed_wall, arch, door, window, roof, debris, watchtower,
                        palisade, stash, bridge, weapon, banner as hanging_banner)
from characters import character, rig, animate

STATIC = {
    "Timber Cottage": lambda b,s: house(b,"house",s),
    "Wayfarer Inn": lambda b,s: house(b,"inn",s),
    "Emberford Smithy": lambda b,s: house(b,"smith",s),
    "Bell Chapel": lambda b,s: house(b,"chapel",s),
    "Briar Camp Hut": lambda b,s: house(b,"hut",s),
    "Briar Watchtower": watchtower, "Palisade": palisade,
    "Ashwater Bridge": bridge,
    "Stone Wall": lambda b,s: coursed_wall(b,4,.67,2.8,s),
    "Ruined Wall": lambda b,s: coursed_wall(b,4,.67,2.8,s,True),
    "Stone Arch": lambda b,s: arch(b,False,s),
    "Broken Arch": lambda b,s: arch(b,True,s),
    "Timber Door": lambda b,s: door(b),
    "Broken Door": lambda b,s: door(b,broken=True),
    "Lantern Window": lambda b,s: window(b),
    "Broken Window": lambda b,s: window(b,broken=True),
    "Slate Roof Module": lambda b,s: roof(b,3.8,3.4,.02,.98,s),
    "Collapsed Shed": lambda b,s: debris(b,s,True),
    "Rubble": debris,
    "Barrel": lambda b,s: stash(b,s,True),
    "Supply Crate": stash,
    "Paving Tile": lambda b,s: b.box((0,0,.07),(1.06,1.06,.15),"Pale Stone",.077),
    "Briar Banner": lambda b,s: standalone_banner(b),
    "Steel Sword": lambda b,s: weapon(b,"sword"),
    "Field Dagger": lambda b,s: weapon(b,"dagger"),
    "Tideglass Runeblade": lambda b,s: weapon(b,"runeblade"),
    "Gloam Crystal Staff": lambda b,s: weapon(b,"staff"),
    "Brute War Club": lambda b,s: weapon(b,"club"),
    "Fir Tree": lambda b,s: tree(b,s),
    "Moss Rock": lambda b,s: rock(b,s),
    "Street Lantern": lambda b,s: lantern(b)
}

ROSTER = {
    **{f"Emberwatch {k.capitalize()}":(k,False,"hero") for k in ("knight","mage","rogue","paladin")},
    **{f"Briar {k.capitalize()}":(k,True,"rival") for k in ("knight","mage","rogue","paladin")},
    "Briar Brute":("brute",True,"enemy"), "Cutpurse Goblin":("goblin",True,"enemy"),
    "Quarry Sentinel":("sentinel",True,"enemy"), "Gloam Wraith":("wraith",True,"enemy"),
    "Town Blacksmith":("smith",False,"npc"), "Town Merchant":("merchant",False,"npc"),
    "Town Scout":("scout",False,"npc")
}


def standalone_banner(b):
    b.beam((0,0,0),(0,0,2.55),.09,"Oak Edge")
    hanging_banner(b,0,0,2.49,"Banner Red")


def tree(b,seed):
    r=random.Random(seed)
    height=4.0+r.random()*1.3
    b.beam((0,0,0),(.05,.05,height-.35),.23,"Oak",.09,7)
    for i in range(4 if b.lod<2 else 3):
        z=height*.30+i*.13*height
        width=1.44-.26*i
        b.beam((.05,.04,z),(.03,-.12,z+height*.32),width,
               ("Grass Dark","Grass","Grass Light")[i%3],.10,10)


def rock(b,seed):
    r=random.Random(seed)
    for i in range(3 if b.lod==0 else 2 if b.lod==1 else 1):
        b.oval((i*.38,r.uniform(-.29,.29),.25-i*.036),
               (.55-i*.08,.41,.43-i*.05),"Slate" if i==1 else "Limestone",sides=8)
        if b.lod==0 and i!=1:
            b.oval((i*.38-.10,0,.59-i*.04),(.40,.29,.11),"Grass Dark",sides=7)


def lantern(b):
    b.box((0,0,.13),(.42,.44,.26),"Limestone",.065)
    b.beam((0,0,.21),(0,0,2.22),.085,"Oak Edge",.056,7)
    b.beam((0,0,1.92),(0,-.34,2.15),.061,"Iron",.047,6)
    b.box((0,-.34,1.88),(.24,.26,.34),"Amber",.041)
    b.box((0,-.34,2.07),(.33,.31,.092),"Iron",.035)
    b.box((0,-.34,1.68),(.29,.30,.087),"Iron",.024)


def slug(name):
    return name.replace(" ","_")


def triangle_count(obj):
    return sum(p.loop_total - 2 for p in obj.data.polygons)


def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    result=bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, add_leaf_bones=False,
        axis_forward="-Z",axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE",
        bake_anim=any(o.type=="ARMATURE" for o in objects),
        bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
        bake_anim_simplify_factor=.4, path_mode="STRIP")
    if "FINISHED" not in result or not path.is_file():
        raise RuntimeError("Failed to export " + str(path))


def make_asset(name,build,folder,library,seed,actor=None):
    sc=bpy.context.scene
    col=bpy.data.collections.new(name)
    sc.collection.children.link(col)
    meshes=[]
    counts=[]
    arm=rig(actor[0],name,col) if actor else None
    for level in range(3):
        b=Build(level)
        build(b,seed)
        obj=b.finish(slug(name)+"_LOD"+str(level),col)
        if arm:
            obj.parent=arm
            obj.modifiers.new("Skeleton",type="ARMATURE").object=arm
        meshes.append(obj)
        counts.append(triangle_count(obj))
    if not (counts[0]>=counts[1]>=counts[2]>0):
        raise RuntimeError(f"Non-monotonic LODs for {name}: {counts}")
    if arm:
        animate(arm)
    export_fbx(folder/(slug(name)+".fbx"), ([arm] if arm else [])+meshes)
    sc.collection.children.unlink(col)
    library.collection.children.link(col)
    # Space the asset browser apart. The FBX origin remains at the model's feet or base.
    index=len(library.collection.children)-1
    for obj in ([arm] if arm else meshes):
        obj.location=((index%7)*10, (index//7)*13,0)
    for obj in meshes[1:]:
        obj.hide_render=True
        obj.hide_viewport=True
    entry={"name":name,"file":slug(name)+".fbx",
           "kind":actor[2] if actor else ("weapon" if name in WEAPONS else "static"),
           "triangles":counts}
    # Model copies in display scenes use baked neutral mesh geometry.
    return entry,meshes[0].data


WEAPONS=("Steel Sword","Field Dagger","Tideglass Runeblade","Gloam Crystal Staff","Brute War Club")


def lake_line(x):
    return 6+3.8*math.sin(x*.088)


def elevation(x,y):
    distance=abs(y-lake_line(x))
    shoreline=max(0, min(1,(distance-2.5)/3.1))
    river=-1.55*(1-shoreline)
    hills=max(0,(abs(x)-24)*.075)+max(0,(y-28)*.125)
    return river+shoreline*(.20*math.sin(x*.24)*math.cos(y*.22)+hills)


def terrain_texture(folder, seed):
    size=768
    rng=random.Random(seed)
    pixels=[]
    grass=(.17,.23,.105)
    dry=(.24,.165,.085)
    sand=(.28,.217,.143)
    def smooth(a,b,v):
        t=max(0,min(1,(v-a)/(b-a)))
        return t*t*(3-2*t)
    for row in range(size):
        y=-40+93*row/(size-1)
        for col in range(size):
            x=-51+102*col/(size-1)
            noise=.52*math.sin(.28*x+.10*y)*math.cos(.21*y)+.23*math.sin(1.75*x+1.05*y)
            speck=rng.uniform(-.065,.065)
            river=1-smooth(3.5,5.8,abs(y-lake_line(x))+.52*noise)
            spine=(1-smooth(1.43,2.46,abs(x-.18*(y+13))+.48*noise)) * smooth(-34,-29,y) * (1-smooth(27,31,y))
            cross=(1-smooth(1.29,2.20,abs(y+13)+.45*noise))*smooth(-24,-22,x)*(1-smooth(20,23,x))
            trail=max(spine,cross)
            shade=(1+.18*noise+speck)
            for g,d,h in zip(grass,dry,sand):
                pixels.append((g*(1-trail)*(1-river)+d*trail*(1-river)+h*river)*shade)
            pixels.append(1)
    image=bpy.data.images.new("Riftbound_terrain",width=size,height=size,alpha=False)
    image.pixels.foreach_set(pixels)
    image.update()
    image.filepath_raw=str(folder/"terrain_albedo.png")
    image.file_format="PNG"
    image.save()
    image.pack()
    TEXTURES["terrain"]=image


def world_ground():
    b=Build()
    verts=[]
    for row in range(63):
        for col in range(69):
            x,y=-51+col*1.5,-40+row*1.5
            verts.append((x,y,elevation(x,y)))
    faces=[]
    for row in range(62):
        for col in range(68):
            i=row*69+col
            faces.append((i,i+1,i+70,i+69))
    b.mesh(verts,faces,"Meadow")
    land=b.finish("Emberford Ground_LOD0")
    for loop in land.data.loops:
        point=land.data.vertices[loop.vertex_index].co
        land.data.uv_layers.active.data[loop.index].uv=((point.x+51)/102,(point.y+40)/93)
    for poly in land.data.polygons:
        poly.use_smooth=True
    b=Build()
    edges=[]
    for j in range(68):
        x=-51+j*1.53
        y=lake_line(x)
        edges.extend(((x,y-3.3,-.73),(x,y+3.3,-.73)))
    b.mesh(edges,[(i,i+2,i+3,i+1) for i in range(0,len(edges)-2,2)],"River")
    for row in range(35):
        x=-49+row*2.8
        y=lake_line(x)+1.7*math.sin(row*2.07)
        b.box((x,y,-.715),(1.45+.5*math.sin(row),.081,.018),"Foam",.006,(0,0,.07*math.sin(row)))
    water=b.finish("Ashwater River_LOD0")
    b=Build()
    for a in range(5):
        for j in range(7):
            x=-9.9+j*1.0
            y=-10.75+a*.94
            b.box((x,y,elevation(x,y)+.062),(.96,.90,.123),"Pale Stone" if (a+j)%3 else "Limestone",
                  .053,(0,0,.02*sin2(a+j)))
    plaza=b.finish("Market Paving_LOD0")
    return [land,water,plaza]


def sin2(i):
    return math.sin(i*2.24)


def place(name, data, location, angle, scale, scene=None):
    obj=bpy.data.objects.new(name,data)
    (scene or bpy.context.scene).collection.objects.link(obj)
    obj.location=location
    obj.rotation_euler.z=math.radians(angle)
    obj.scale=(scale,scale,scale)
    return obj


def make_world(meshes,seed):
    rng=random.Random(seed)
    instances=[]
    def add(name,x,y,yaw=0,scale=1,z=None):
        z=elevation(x,y) if z is None else z
        place(name,meshes[name],(x,y,z),yaw,scale)
        instances.append({"asset":name,"position":[round(x,4),round(z,4),round(-y,4)],
                          "yaw":yaw,"scale":round(scale,3)})
    buildings=[
        ("Wayfarer Inn",-12,-7,8,1.14),("Bell Chapel",-4,-4,8,1.16),
        ("Emberford Smithy",9,-10,-13,1.30),("Timber Cottage",14,-9,9,1.14),
        ("Timber Cottage",-19,-14,67,1.16),("Timber Cottage",-15,-21,161,1.22),
        ("Timber Cottage",10,-21,158,1.08),("Timber Cottage",17,-18,-78,1.15),
        ("Briar Watchtower",9,21,-9,1.23),("Briar Watchtower",22,24,-18,1.20),
        ("Briar Camp Hut",12,29,-14,1.37),("Briar Camp Hut",19,32,20,1.48),("Briar Camp Hut",24,28,-43,1.25),
        ("Collapsed Shed",-21,13,25,1.18),("Broken Arch",-15,16,-11,1.38),
        ("Stone Arch",-22,20,22,1.33),("Ruined Wall",-25,18,-42,1.33),
        ("Ruined Wall",-16,19,-19,1.1),("Stone Wall",-25,11,138,1.20)]
    for name,x,y,yaw,scale in buildings:
        add(name,x,y,yaw,scale)
    add("Ashwater Bridge",2.5,lake_line(2.5),5,1.17,.14)
    for x in (8,12,20,24):
        add("Palisade",x,18.7,0)
    for y in (22,26,30):
        add("Palisade",26,y,90)
    for x,y in ((-11,-14),(-6,-14),(4,-14),(13,-13),(-3,-2),(7,-.4),
                (3,13),(7,17),(-17,-23),(3,-24)):
        add("Street Lantern",x,y,0,1.12)
    for x,y in ((10,18),(16,19),(24,21)):
        add("Briar Banner",x,y,-10,1.12)
    for name,x,y,ang in (("Town Merchant",-8.4,-8.5,20),("Town Blacksmith",7.1,-13,2),
                         ("Town Scout",-3.5,-13,-14),("Emberwatch Knight",3.4,-.9,-17),
                         ("Emberwatch Paladin",1.0,-2.1,-31),("Briar Brute",15.2,23.5,11),
                         ("Cutpurse Goblin",20,26,-12),("Briar Knight",12.9,20.8,7),
                         ("Gloam Wraith",-20,17,-7),("Quarry Sentinel",-22,22,16)):
        add(name,x,y,ang)
    for x,y in ((-13,-10),(-10.4,-10.1),(11,-12),(-18,-15),(20,25),(21,29),(11,27),(6,-8)):
        add("Supply Crate",x,y,-18,.89)
        add("Barrel",x+.95,y+.23,7,.86)
    for x,y in ((-22,11),(-15,14),(-20,19),(-27,18),(20,33)):
        add("Rubble",x,y,29,1.0)
    for ox,oy in ((-31,-23),(32,-15),(-33,30),(35,32),(-6,42)):
        for _ in range(13):
            x,y=ox+rng.uniform(-7,7),oy+rng.uniform(-6,6)
            if abs(y-lake_line(x))>6.4:
                add("Fir Tree",x,y,rng.randrange(360),rng.uniform(.76,1.65))
    for _ in range(164):
        x,y=rng.uniform(-47,47),rng.uniform(-35,49)
        if abs(y-lake_line(x))<6.8 or abs(x-.18*(y+13))<4:
            continue
        if any((x-bx)**2+(y-by)**2<24 for _,bx,by,_,_ in buildings):
            continue
        if -23<x<27 and -25<y<35 and rng.random()<.86:
            continue
        add("Fir Tree" if rng.random()<.73 else "Moss Rock",x,y,rng.randrange(360),rng.uniform(.67,1.57))
    return instances


def light_scene(scene, name, eye, target, size, resolution, sky_color):
    world=bpy.data.worlds.new(name+" Sky")
    scene.world=world
    world.use_nodes=True
    node=world.node_tree.nodes.get("Background")
    node.inputs["Color"].default_value=(*sky_color,1)
    node.inputs["Strength"].default_value=.55
    sun=bpy.data.lights.new("Raking sun","SUN")
    sun.energy=3.45
    sun.angle=math.radians(19)
    sun.color=(1,.84,.68)
    key=bpy.data.objects.new("Raking sun",sun)
    scene.collection.objects.link(key)
    key.rotation_euler=(.56,-.46,-.47)
    fill=bpy.data.lights.new("Cool fill","SUN")
    fill.energy=.78
    fill.color=(.58,.78,1)
    light=bpy.data.objects.new("Cool fill",fill)
    scene.collection.objects.link(light)
    light.rotation_euler=(.68,.67,2.19)
    cam=bpy.data.cameras.new(name)
    camera=bpy.data.objects.new(name,cam)
    scene.collection.objects.link(camera)
    camera.location=eye
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    cam.type="ORTHO"
    cam.ortho_scale=size
    cam.clip_end=300
    scene.camera=camera
    scene.render.engine="BLENDER_EEVEE"
    scene.render.resolution_x,scene.render.resolution_y=resolution
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.render.image_settings.file_format="PNG"


def make_roster(scene,meshes,names):
    for n in range(len(names)):
        x=(n%4-1.5)*2.57
        y=(n//4)*3.9
        place(names[n],meshes[names[n]],(x,y,.17),-9 if n%2 else 9,1,scene)
        b=Build()
        b.box((x,y,.02),(2.1,1.58,.22),"Slate",.125)
        b.finish("Display plinth",scene.collection)
    b=Build()
    b.box((0,2,-.22),(30,30,.25),"Black",.04)
    b.finish("Studio floor",scene.collection)


def make_weapon_gallery(meshes):
    scene=bpy.data.scenes.new("Armory")
    for i,name in enumerate(WEAPONS):
        x=(i-2)*.91
        place(name,meshes[name],(x,0,.15),0,1,scene)
        b=Build()
        b.box((x,0,.057),(.75,.64,.148),"Slate",.068)
        b.finish("Weapon stand",scene.collection)
    b=Build()
    b.box((0,1,-.10),(20,20,.18),"Black",.04)
    b.finish("Armory floor",scene.collection)
    light_scene(scene,"Armory",(.95,-9,3.64),(0,0,.91),5.50,(1475,880),(.46,.52,.67))
    return scene


def render(scene,path):
    scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True,scene=scene.name)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--output",required=True)
    ap.add_argument("--seed",type=int,default=4069)
    ap.add_argument("--no-render",action="store_true")
    args=ap.parse_args(sys.argv[sys.argv.index("--")+1:])
    root=Path(args.output).resolve()
    fbx=root/"FBX"; fbx.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.name="Emberford and the Briar Watch"
    scene.unit_settings.system="METRIC"
    library=bpy.data.scenes.new("Reusable Asset Library")
    make_textures(root/"Textures")
    terrain_texture(root/"Textures", args.seed)
    make_materials()
    assets,meshes=[],{}
    for i,(name,build) in enumerate(STATIC.items()):
        entry,mesh=make_asset(name,build,fbx,library,args.seed+i)
        assets.append(entry)
        meshes[name]=mesh
    for i,(name,info) in enumerate(ROSTER.items()):
        k,hostile,_=info
        entry,mesh=make_asset(name,lambda b,s: character(b,k,hostile),fbx,library,args.seed+i,info)
        assets.append(entry)
        meshes[name]=mesh
    ground=world_ground()
    export_fbx(fbx/"Emberford_Terrain.fbx",ground)
    placements=make_world(meshes,args.seed)
    light_scene(scene,"Emberford Overview",(48,-66,57),(0,7,0),100,(1760,1200),(.39,.50,.64))
    for label,names in (
        ("Faction Roster",list(ROSTER)[:8]),
        ("Enemies and Townsfolk",list(ROSTER)[8:])
    ):
        gallery=bpy.data.scenes.new(label)
        make_roster(gallery,meshes,names)
        light_scene(gallery,label,(2.7,-13,8.7),(0,1.70,.95),11.6,(1600,1080),(.46,.52,.67))
    armory=make_weapon_gallery(meshes)
    manifest={"seed":args.seed,"assets":assets,"instances":placements,
              "palette":[{"name":name,"color":color,"surface":surface or ""} for name,(color,surface) in PALETTE.items()]}
    (root/"KitManifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(root/"Emberford_Procedural_Kit.blend"))
    if not args.no_render:
        render(scene,root/"Emberford.png")
        render(armory,root/"Armory.png")
        render(bpy.data.scenes["Faction Roster"],root/"Faction_Roster.png")
        render(bpy.data.scenes["Enemies and Townsfolk"],root/"Enemies_and_Townsfolk.png")
        scene.camera.location=(19,-32,17.2)
        scene.camera.rotation_euler=(Vector((-4,-6,1.9))-scene.camera.location).to_track_quat("-Z","Y").to_euler()
        scene.camera.data.ortho_scale=45
        scene.render.resolution_x,scene.render.resolution_y=(1664,1120)
        render(scene,root/"Emberford_District.png")
    print(f"Generated {len(assets)} assets with three LODs each and {len(placements)} environment instances.")


if __name__=="__main__":
    main()
