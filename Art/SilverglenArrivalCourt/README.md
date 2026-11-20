# Silverglen arrival court

The gateward trail shoulder gained a reading pavilion, a crescent bench, a blue-flower herb basin, two copper-ribbed trail beacons, and a broad paved lane with compass inlays. The court sits southeast of the original Moonwell. The Moonwell, starter actors, inn, and fence remain in their authored positions.

## Unity placement

Place **Silverglen Arrival Court** at **(-130, 0, 55)** in a copy of Outpost-Expanded with zero rotation and unit scale. The 15 m terrain height is already in the prefab's child offsets. From the Silverglen Trail east of the inn, head south past the east fence to reach the court.

The editor fixture checks the trail-to-court lane on foot and with a mount collision proxy in both directions. It separately checks the pavilion threshold and the herb court on foot. The combined village preview build includes this prefab.

## Editable source

Export into a new directory with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/SilverglenArrivalCourt/build_arrival_court.py -- ./SilverglenArrivalCourtExport
```

The builder produces five original FBX and GLB models, a 768 × 768 atlas, a packed Blender scene and an art render.
