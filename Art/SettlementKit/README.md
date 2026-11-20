# Settlement kit

Original stylized architecture prototype for Riftbound. One Blender Python source generates the weighhouse, service terrace, road shelter, waypost, original color atlas and a review scene. The script uses procedural geometry and textures without third-party source artwork.

Run Blender 5.2 in background with --python Art/SettlementKit/build_kit.py -- followed by an absolute, unused output directory. Exports include each model as FBX and GLB, the atlas PNG, one packed .blend file and a rendered PNG. Models use metre units and a base pivot. The terrace is 1.2 m tall with a nominal 6 m ramp (11.3 degrees). Each mesh has one material and a UV atlas. The amber glass surface needs a separate Unity lighting pass if used as a real light.

Unity imports the four FBX files and atlas from Assets/RPG/Art/SettlementKit. The placement preview assigns the atlas to a temporary URP material and adds temporary collision. See Docs/SETTLEMENT_EXPANSION_PLAN.md for the in-context results.
