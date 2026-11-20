# Deathknell cloister

The cloister adds a pointed watch arch, a broken side arcade, a raised stone lane, two low green lanterns and a cenotaph. The lane crosses the southern apron below the Watch House. The memorial is a small side stop. Both ends of the raised lane have gentle ramps.

## Unity placement

Place **Deathknell Cloister** at **(-242.5, 0, 156.8)** with zero rotation and unit scale in a copy of Outpost-Expanded. The prefab's child height offsets fit the 12 m plateau. Keep the cemetery fence and existing starter actors. The entrance path passes east of the Watch House, around x=-235, before turning west along the cloister.

The editor preview checks the ramp and north connector in both directions on foot and using a mount collision proxy. The combined village preview build includes this prefab. The two new lanterns use short-range point lights with shadows disabled.

## Editable source

Export into a new folder with Blender 5.2.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python Art/DeathknellCloister/build_cloister.py -- ./DeathknellCloisterExport
```

The builder generates five original FBX and GLB models, a 768 × 768 atlas, an editable Blender scene and a display render.
