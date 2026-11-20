# Bloodrock trail lookout

The southeast shoulder gained a sandstone lookout with a walkable timber ramp, a hide-roof supply stand, trail banners, and a rest bay with a bench and water crock. The platform stands about 2.16 m above the ground. Mounts have a separate through lane at yard level.

## Unity placement

Place **Bloodrock Trail Lookout** at **(294, 0, 184.5)** with zero rotation and unit scale in a copy of Outpost-Expanded. The child offsets match the existing 18 m plateau. The preview leaves the existing camp fence, flight master, wagon, quest actors, patrols and Vista discovery in their original locations.

From camp, skirt the east end of the south fence to find the lookout. A foot route runs west from the south end of the yard to the existing Vista marker. The editor fixture exercises both sides of that approach. It uses a foot and mount collision proxy in the through lane and a foot proxy on the climb and Vista link.

## Editable source

Export into a new output folder with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/BloodrockLookout/build_lookout.py -- ./BloodrockLookoutExport
```

The builder produces four FBX and GLB pairs, one 768 × 768 atlas, the packed Blender scene and a display render.
