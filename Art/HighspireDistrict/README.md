# Highspire east ward

An original exterior district kit for Highspire's eastern arrival. The cobalt awnings mark trade services against limestone walls and slate roofs. The cart ramp takes visitors up to the guild hall. The fountain gives the lower ward a landmark and leaves the gate lane open.

## Unity placement

The Highspire East Ward prefab contains all eight modules, lane repeats, a shared 1536 × 1536 atlas, colliders and site-specific stone footings. In a scratch copy of the Highspire scene, place the prefab at **(-138, 12.30, -49)** with zero rotation and unit scale. The prefab was fitted around the existing terrain. For other sites, use individual modules and set footings to the local grade.

The gate opening and cart ramp use the playable exterior path. Doors and workshops are exterior set dressing. Keep the existing main gate, sentries and the tree at (-148.85, -39.45). The new ward connects conceptually to the east highway. Before promoting it, link the final cobbled lane to the existing road and walk the entire junction with the real camera and a mount.

## Source and export

Run Blender 5.2 with an empty output path.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/HighspireDistrict/build_district.py -- ./HighspireDistrictExport
```

This produces the packed Blender scene, two design renders, eight FBX files, eight GLB files and the shared color atlas. Geometry and textures are authored in the Blender source. Assign Highspire District.mat to the loose Unity FBX meshes. The district prefab already has material overrides.

## Next district slices

| Place | First district | Place in the world |
| --- | --- | --- |
| Emberwatch | Timber-and-stone inn, smithy and covered civic square | Keep Abe's entrance and open quest approach. Use the terraced side of the square for the forge and deliveries. |
| Gor'Kresh | Basalt forge courtyard and stepped guard market | Put training and quartermaster space where it is visible from the existing approach. Keep a broader lane for mounted travel. |
| Rustwater | Salvage yard, mill and raised plank alley | Let the river and workshops shape the lanes. Keep player traffic clear of crane and wheel set pieces. |
| Deathknell | Broken cloister, lower crypt yard and lantern stair | Use a lit ramp for the main route and treat the ruined stairs as a side passage. |
