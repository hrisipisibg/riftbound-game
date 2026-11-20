"""Re-export the skinned combatants with corrected modeled weapon grips."""

import json
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from characters import character
from generate import ROSTER, make_asset
from meshkit import make_materials


def main():
    destination = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    library = bpy.data.scenes.new("Combatant library")
    make_materials()
    manifest_path = destination / "KitManifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for index, (name, spec) in enumerate(ROSTER.items()):
        kind, hostile, role = spec
        if role == "npc":
            continue
        asset, _ = make_asset(name, lambda mesh, _: character(mesh, kind, hostile),
                              destination / "FBX", library, manifest["seed"] + index, spec)
        entry = next(i for i, item in enumerate(manifest["assets"]) if item["file"] == asset["file"])
        manifest["assets"][entry] = asset
        print(name, asset["triangles"])
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
