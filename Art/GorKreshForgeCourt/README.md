# Gor'Kresh forge court

The south apron has a charcoal kiln, supply shed, red-banner arch, anvil and quench station, and a charcoal bin. The arch leaves the center open for mounts. The coal glow and banners are static. The design keeps the bonfire and the existing lodges as the main landmarks.

## Unity placement

Place **GorKresh Forge Court** at **(340, 0, 269)** in a copy of Outpost-Expanded with zero rotation and unit scale. Model height offsets are fitted to the site's 25 m terrain. The full village-remodel preview includes this court alongside Highspire, Emberwatch and Greenhollow.

To build the combined preview from a separate project named **Riftbound Settlement Preview**, open the same Unity version in batch mode and use the editor method Riftbound.Editor.VillageRemodelPreviewBuild.Run with a unique -gameplayBuildName. The preview builder adds the prefabs to the build scene before packaging. The ordinary build flow uses the authored scene.

## Editable source

Export into a new directory with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/GorKreshForgeCourt/build_forge_court.py -- ./GorKreshForgeCourtExport
```

The builder produces the packed Blender scene, five FBX and GLB pairs, an original 768 × 768 atlas, and a display render.
