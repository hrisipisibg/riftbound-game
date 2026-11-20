# Rustwater salvage yard

The northwest apron has a sheet-metal sorting workshop, a rotating ore drum, a gantry crane, sorted scrap bins, and a raised service crossing with a ramp at each end. The drum rotation is visual. A separate fixed volume supplies collision. The yard is built on the Redridge shoulder, where it joins the existing Rustwater road.

## Unity placement

Place the **Rustwater Salvage Yard** prefab at **(224, 0, 171)** with zero rotation and unit scale in a copy of Outpost-Expanded. Individual model heights are fitted to the existing 14 m terrain. The prefab does not change quest actors, combat spawns, terrain, or the starter road. The combined village preview build stages it automatically.

The preview tool verifies the Redridge connection and crossing in both directions on foot and using a mount collision proxy. It also checks the workshop apron on foot. Test live controls and camera near the rail and around the work areas before permanent placement.

## Editable source

Export to a new directory with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/RustwaterSalvageYard/build_salvage_yard.py -- ./RustwaterSalvageYardExport
```

The builder produces the editable Blender scene, six FBX and GLB models, a 768 × 768 atlas and a display render.
