"""Export the rebuilt tower meshes and the capital landmark buildings with Blender."""

from pathlib import Path
import json
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate import make_asset
from meshkit import Build, make_materials
from structures import watchtower, coursed_wall, roof, door, window, banner


def stronghold(b, seed, timber):
    width, depth = (11.1, 7.9) if timber else (10.7, 8.2)
    eaves = 4.85 if timber else 5.65
    front = -depth * .5
    wall = "Dark Wood" if timber else "Limestone"
    edge = "Oak Edge" if timber else "Pale Stone"
    standard = "Banner Red" if timber else "Banner Teal"
    b.box((0, 0, .24), (width + .85, depth + .78, .49), "Basalt", .15)
    b.box((0, 0, eaves * .5), (width, depth, eaves), wall, .11)
    if not timber and b.lod == 0:
        for sign in (-1, 1):
            coursed_wall(b, width, .30, eaves, seed + sign, center=(0, sign * depth * .5, 0))
            coursed_wall(b, depth, .29, eaves, seed + sign * 3,
                         center=(sign * width * .5, 0, 0), angle=1.57079632679)
    elif timber:
        for z in (.61, 1.51, 2.33, 3.24, 4.05):
            for y in (front, -front):
                b.box((0, y, z), (width + .14, .22, .20), "Oak", .048)

    rise = 2.32 if timber else 2.55
    roof(b, width + 1.1, depth + 1.10, eaves, rise, seed, timber)
    b.mesh([(-width * .5, front, eaves - .08), (width * .5, front, eaves - .08), (0, front, eaves + rise),
            (-width * .5, -front, eaves - .08), (width * .5, -front, eaves - .08), (0, -front, eaves + rise)],
           [(0, 1, 2), (5, 4, 3)], wall)
    for y in (front, -front):
        b.beam((0, y, eaves), (0, y, eaves + rise - .1), .12, edge, sides=5)
    if not timber:
        # Entry pavilion stands out from the nave and makes the main door legible from the approach.
        b.box((0, front - .38, 2.3), (4.1, 1.36, 4.35), "Pale Stone", .15)
        porch = Build(b.lod)
        roof(porch, 4.65, 2.21, 4.1, 1.64, seed + 2)
        offset = len(b.verts)
        b.verts.extend((x, y + front - .41, z) for x, y, z in porch.verts)
        b.faces.extend(tuple(offset + index for index in face) for face in porch.faces)
        b.tags.extend(porch.tags)
        b.box((0, front - .87, 3.72), (3.65, .46, .42), "Slate Edge", .09)
    for side in (-1, 1):
        x = side * width * .39
        if timber:
            b.beam((x, front - .78, .06), (x * .98, front - .40, eaves + .58), .31, "Oak Edge", .16)
            b.beam((x, front - .9, eaves * .59), (x * 1.06, front - .95, eaves + .91), .13, "Bone", .025)
        else:
            b.box((x, front - .1, eaves * .39), (.95, 1.04, eaves * .78), edge, .1)
            b.box((x, front - .10, eaves * .77), (1.17, 1.22, .47), "Pale Stone", .1)
        window(b, side * width * .27, front - .21, 2.45, 1.14, 1.58, skin=edge)
        banner(b, side * width * .23, front - .35, eaves * .86, standard, 1.36)
    door(b, 0, front - (1.14 if not timber else .15), .31, 2.22, 3.34)
    for row in range(3):
        b.box((0, front - 1.57 + .26 * row, .11 + .16 * row),
              (3.85 - .28 * row, .72, .19), "Limestone", .057)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    destination = Path(args[0]).resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    library = bpy.data.scenes.new("Architecture library")
    make_materials()
    path = destination / "KitManifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    builds = {
        "Briar Watchtower": watchtower,
        "Highspire Masonry Tower": lambda b, s: watchtower(b, s, stone=True),
        "Highspire Citadel Hall": lambda b, s: stronghold(b, s, timber=False),
        "Briar War Hall": lambda b, s: stronghold(b, s, timber=True),
    }
    for index, (name, builder) in enumerate(builds.items()):
        entry, _ = make_asset(name, builder, destination / "FBX", library, manifest["seed"] + 128 + index)
        matches = [i for i, old in enumerate(manifest["assets"]) if old["name"] == name]
        if matches:
            manifest["assets"][matches[0]] = entry
        else:
            manifest["assets"].append(entry)
        print(name, entry["triangles"])
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
