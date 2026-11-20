# Westbank ferry landing

The south bank gained a cedar landing, a shore ramp, a moored skiff, a roadside shelter and a cargo hoist. The ferry landing is a place to explore on foot. The skiff is scenery. Mounts can still use the existing bridge.

## Unity placement

Place **Westbank Ferry Landing** at **(-270, 0, 41)** with zero rotation and unit scale in a copy of Outpost-Expanded. The prefab was fitted to the existing 3.25 m river surface. From the west end of the bridge, go south just east of the hoist, then follow the bank around the spur to the ramp. The editor fixture checks the bridge in both directions using foot and mount collision proxies. It checks the road-to-dock spur on foot.

The combined village preview build stages the prefab. The Willow Rest cache, bridge collision, river mesh, and terrain are authored independently.

## Editable source

Export into a new folder with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/WestbankFerryLanding/build_ferry_landing.py -- ./WestbankFerryLandingExport
```

The builder produces five original FBX and GLB models, a 768 × 768 atlas, a packed Blender scene and a display render.
