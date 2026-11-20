# Emberwatch civic square

The south court is built around a market lane, a two-storey hearth inn, an open smithy, three sheltered stalls and a covered well. Red clay, oak, green shutters and local stone distinguish it from Highspire. The inn is an exterior facade. The smithy and market have open visitor space. The forge coals and well are static scenery.

## Unity

Place the **Emberwatch Civic Square** prefab in a scratch copy of Outpost-Expanded at **(28, 0, -29)** with zero rotation and scale 1. The mesh instances share a material and atlas. Four terrain-fitted stone footings and the sloped lane tiles are specific to this site. For other sites, use the loose models and fit their supports separately.

The court sits southeast of the starter apron. Keep Abe, the starter quests and the existing east approaches in place. A later placement pass should walk the complete connection to Abe, check mounted camera clearance, and review the retained trees before setting civilian routes or vendor markers.

## Blender

Export to a new directory with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/EmberwatchCivicSquare/build_square.py -- ./EmberwatchExport
```

The script generates the original geometry, 768 × 768 color atlas, editable Blender scene, FBX, GLB and render. The scene and kit use meters.
