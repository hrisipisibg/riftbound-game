# Village landmarks

Emberwatch's supply lane extends the civic square with a loaded spoked wagon, a working-yard shelter, an herb garden and a roofed notice board. Greenhollow has a covered traveler stop with a bench and table, a honey stall, and a lantern trail marker. The wagon is parked scenery. The notice and honey displays are decorative.

## Unity preview

| Prefab | Position | Context |
| --- | --- | --- |
| Emberwatch Supply Lane | (43, 0, -40) | East of the Emberwatch Civic Square prefab at (28, 0, -29) |
| Greenhollow Rest Stop | (-33, 0, 377) | Southwest shoulder of the Greenhollow arrival road |

Use a scratch copy of Outpost-Expanded, zero rotation and unit scale for both prefabs. The prefab roots use these coordinates even though some children sit much higher. Their child offsets and any footings match the current terrain. Keep the existing route and village actors. Both prefabs share the civic square's atlas.

## Editable source

Run Blender 5.2 with a new output directory.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/VillageLandmarks/build_landmarks.py -- ./VillageLandmarksExport
```

The builder produces seven FBX and GLB models, one packed Blender scene and renders of the two groups. The group renders show the models on a display floor. The Unity editor preview stages the authored terrain.
