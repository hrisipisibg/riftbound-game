"""Mesh and palette helpers for the Riftbound procedural kit. Run with Blender Python."""
from math import cos, sin, pi, sqrt
from pathlib import Path
import random
import bpy
from mathutils import Vector, Euler, Matrix

PALETTE = {
    "Oak": ("674332", "wood"), "Oak Edge": ("996546", "wood"), "Dark Wood": ("332624", "wood"),
    "Plaster": ("c9a979", "stone"), "Limestone": ("8a9a91", "stone"), "Pale Stone": ("bfba9c", "stone"),
    "Basalt": ("4d5362", "stone"), "Slate": ("547182", "stone"), "Slate Edge": ("7695a3", "stone"),
    "Ember Tile": ("934a3f", "stone"), "Ember Edge": ("ad6450", "stone"),
    "Iron": ("536472", "metal"), "Steel": ("a5c0c5", "metal"), "Gold": ("ce9a4f", "metal"),
    "Black Iron": ("313345", "metal"), "Red Iron": ("85515d", "metal"), "Bone": ("ded0ac", "stone"),
    "Banner Teal": ("287a79", "fabric"), "Banner Red": ("883747", "fabric"), "Linen": ("c7b891", "fabric"),
    "Cloak Blue": ("394d76", "fabric"), "Cloak Violet": ("624265", "fabric"), "Leather": ("614036", "wood"),
    "Skin Warm": ("bb8060", None), "Skin Deep": ("9a634b", None), "Skin Ash": ("9698a5", None),
    "Goblin Skin": ("829752", None), "Brute Skin": ("897760", None), "Eye": ("ead9ad", None),
    "Black": ("292a31", None), "Hair": ("553731", None), "Silver Hair": ("b9b0a5", None),
    "Jade": ("3db7ad", "glow"), "Amber": ("ffae55", "glow"), "Violet": ("c35a9f", "glow"),
    "Grass": ("627553", None), "Grass Light": ("76865d", None), "Grass Dark": ("4d634c", None),
    "Meadow": ("ffffff", "terrain"), "Trail": ("9b8264", "stone"), "River": ("32768a", None), "Foam": ("89d2c3", None)
}
MATS = {}
TEXTURES = {}


def rgb(hex_color):
    srgb = [int(hex_color[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return [c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in srgb]


def make_textures(folder):
    """Write repeatable sRGB albedo textures. The light/dark grain does not bake scene lighting."""
    folder.mkdir(parents=True, exist_ok=True)
    for kind in ("wood", "stone", "fabric", "metal"):
        size = 192
        buf = []
        rng = random.Random(kind)
        for y in range(size):
            v = y * 2 * pi / size
            for x in range(size):
                u = x * 2 * pi / size
                noise = rng.uniform(-.022, .022)
                if kind == "wood":
                    ridges = sin(11 * u + .95 * sin(2 * v + u) + .3 * sin(5 * v))
                    f = .81 + .10 * ridges + .05 * sin(29 * u + 2 * sin(v)) + .03 * sin(4 * v)
                elif kind == "stone":
                    n = sin(3 * u + 4 * v + sin(2 * v)) * cos(5 * v - 2 * u)
                    f = .86 + .084 * n + .044 * sin(13 * u + 9 * v) + noise
                elif kind == "fabric":
                    f = .87 + .06 * sin(32 * u) * sin(32 * v) + .03 * cos(10 * u - 8 * v)
                else:
                    f = .87 + .067 * sin(4 * u + 3 * v) * cos(7 * v) + .049 * sin(23 * v) + noise
                f = max(0, min(1, f))
                f = f / 12.92 if f <= .04045 else ((f+.055)/1.055)**2.4
                buf.extend((f, f, f, 1))
        img = bpy.data.images.new("Riftbound_" + kind, width=size, height=size, alpha=False)
        img.pixels.foreach_set(buf)
        img.update()
        img.filepath_raw = str(folder / (kind + "_albedo.png"))
        img.file_format = "PNG"
        img.save()
        img.pack()
        TEXTURES[kind] = img


def make_materials():
    for name, (hex_color, kind) in PALETTE.items():
        color = rgb(hex_color)
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = (*color, 1)
        b = mat.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (*color, 1)
        b.inputs["Roughness"].default_value = .31 if name == "River" else .5 if kind in ("metal", "glow") else .86
        b.inputs["Metallic"].default_value = .54 if kind == "metal" else 0
        if kind == "glow":
            b.inputs["Emission Color"].default_value = (*color, 1)
            b.inputs["Emission Strength"].default_value = .85
        if kind in TEXTURES:
            tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = TEXTURES[kind]
            mix = mat.node_tree.nodes.new("ShaderNodeMixRGB")
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1 if kind=="terrain" else .42
            mix.inputs[1].default_value = (*color, 1)
            mat.node_tree.links.new(tex.outputs["Color"], mix.inputs[2])
            mat.node_tree.links.new(mix.outputs["Color"], b.inputs["Base Color"])
        MATS[name] = mat


class Build:
    """Assemble one compact indexed mesh with material slots, UVs, and optional rigid bone weights."""
    def __init__(self, lod=0):
        self.lod = lod
        self.verts, self.faces, self.tags, self.weights = [], [], [], {}

    def mesh(self, verts, faces, mat, bone=None):
        offset = len(self.verts)
        self.verts.extend(verts)
        self.faces.extend(tuple(offset + i for i in face) for face in faces)
        self.tags.extend([mat] * len(faces))
        if bone:
            self.weights.setdefault(bone, []).extend(range(offset, offset + len(verts)))

    def ring_mesh(self, rings, mat, bone=None):
        count = len(rings[0])
        faces = [tuple(reversed(range(count)))]
        for row in range(len(rings)-1):
            i = row*count
            for col in range(count):
                n = (col+1) % count
                faces.append((i+col,i+n,i+n+count,i+col+count))
        i = (len(rings)-1)*count
        faces.append(tuple(range(i,i+count)))
        self.mesh([p for row in rings for p in row], faces, mat, bone)

    def box(self, center, size, mat, chamfer=.035, rot=(0,0,0), bone=None):
        x,y,z = [v/2 for v in size]
        c = min(chamfer, x*.44,y*.44,z*.44)
        transform = Euler(rot).to_matrix()
        if c <= .001 or self.lod >= 2:
            profile = [(-x,-y),(x,-y),(x,y),(-x,y)]
            rings = [[tuple(Vector(center)+transform@Vector((a,b,h))) for a,b in profile] for h in (-z,z)]
        else:
            rings = []
            for h,inset in ((-z,c*.55),(-z+c,0),(z-c,0),(z,c*.55)):
                a,b=x-inset,y-inset
                edge=c-inset*.45
                profile = [(-a+edge,-b),(a-edge,-b),(a,-b+edge),(a,b-edge),
                           (a-edge,b),(-a+edge,b),(-a,b-edge),(-a,-b+edge)]
                rings.append([tuple(Vector(center)+transform@Vector((i,j,h))) for i,j in profile])
        self.ring_mesh(rings, mat, bone)

    def beam(self, start, end, radius, mat, tip=None, sides=6, bone=None):
        a,b=Vector(start),Vector(end)
        direction=b-a
        if direction.length_squared < 1e-8:
            return
        rotation=direction.to_track_quat("Z","Y")
        tip=radius if tip is None else tip
        sides=max(4,sides-2*self.lod)
        rings=[]
        for center,r in ((a,radius),(b,tip)):
            rings.append([tuple(center+rotation@Vector((r*cos(2*pi*i/sides),r*sin(2*pi*i/sides),0))) for i in range(sides)])
        self.ring_mesh(rings,mat,bone)

    def oval(self, center, scale, mat, bone=None, sides=10):
        sides=max(5,sides-2*self.lod)
        steps=5-self.lod
        rings=[]
        for step in range(steps+1):
            angle=-pi*.5+.035+(pi-.07)*step/steps
            rings.append([(center[0]+scale[0]*cos(2*pi*i/sides)*cos(angle),
                           center[1]+scale[1]*sin(2*pi*i/sides)*cos(angle),
                           center[2]+scale[2]*sin(angle)) for i in range(sides)])
        self.ring_mesh(rings,mat,bone)

    def plate(self, outline, y, depth, mat, bone=None):
        # A shaped profile extruded along depth, used for shields, blades and banners.
        points=[(x,y+d,z) for d in (depth*.5,-depth*.5) for x,z in outline]
        n=len(outline)
        faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        self.mesh(points, faces, mat, bone)

    def finish(self,label, collection=None):
        m=bpy.data.meshes.new(label)
        m.from_pydata(self.verts, [], self.faces)
        m.update()
        o=bpy.data.objects.new(label,m)
        (collection or bpy.context.scene.collection).objects.link(o)
        slots={}
        for name in self.tags:
            if name not in slots:
                slots[name]=len(slots)
                m.materials.append(MATS[name])
        uv=m.uv_layers.new(name="Tile UV")
        for poly,name in zip(m.polygons,self.tags):
            poly.material_index=slots[name]
            dominant=max(range(3),key=lambda axis:abs(poly.normal[axis]))
            u,v=[axis for axis in range(3) if axis!=dominant]
            if name in ("Oak","Dark Wood","Oak Edge","Leather") and v!=2:
                u,v=v,u
            for loop_index in poly.loop_indices:
                point=m.vertices[m.loops[loop_index].vertex_index].co
                uv.data[loop_index].uv=(point[u]*.73,point[v]*.73)
        for name,indices in self.weights.items():
            o.vertex_groups.new(name=name).add(indices,1.0,"REPLACE")
        return o
