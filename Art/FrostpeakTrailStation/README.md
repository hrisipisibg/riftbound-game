# Frostpeak trail station

A granite pass forge, sloped trail terrace, windbreak shelter, copper lantern mileposts, and packed supplies sit beside the west spur. Snow-free slate, stone, and copper match the existing foothills terrain. The terrace has a clear center for mounts and gradual ramps at both ends.

## Unity placement

Place **Frostpeak Trail Station** at **(-112, 0, -109)** with zero rotation and unit scale in a copy of Outpost-Expanded. The model offsets already include the 16 m terrain height. Approach from the Frostpeak spur on the southwest side of the village, west of the wagon. The preview leaves the original Great Smithy, starter actors and wagon in place.

The editor preview checks the spur and terrace in both directions on foot and with a mount collision proxy. The forge and windbreak entrances have separate walking checks. The combined village preview includes this prefab.

## Editable source

Export into a clean output location with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/FrostpeakTrailStation/build_trail_station.py -- ./FrostpeakTrailStationExport
```

The script produces the packed Blender scene, five FBX and GLB pairs, an original 768 × 768 atlas and a display render.
