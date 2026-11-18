# Expanded world layout

> Planning update, 2026-11-16: **Stormglass Reach** is a proposed addition north of the current map. That fifth tile is design-only and is not present in this scene. Existing cache, bridge and settlement repairs are authored content. Never rerun the generator below over those edits.

The new world is authored into **Outpost-Expanded.unity**. The original Updated scene and its terrain remain available. Four connected 384-metre heightfield tiles cover **768 × 768 metres**, from x=-320 to 448 and z=-160 to 608. The original terrain covered 240 × 380 metres; the old gameplay boundary was approximately 160 × 300 metres.

## Zone plan

| Region | New centre (x,z) | Ground elevation | Role |
|---|---:|---:|---|
| Emberwatch / Ashwood | (0,0..100) | Existing heights | Alliance introduction and Abe's complete quest chain |
| Highspire Keep | (-200,-60) | 12m | Alliance capital and western trail junction |
| Frostpeak foothills | (-95,-90) | 16m | Dwarf starting area |
| Silverglen Sanctum | (-150,75) | 15m | Elf starting grove |
| Deathknell Hollow | (-240,180) | 12m | Undead starting area |
| Duskwood Cathedral | (-220,260) | 18m | Dungeon exterior and portal entrance |
| Rustwater Scrapworks | (240,150) | 14m | Goblin starting area |
| Camp Bloodrock | (280,210) | 18m | Horde camp and access to eastern zones |
| Gor'Kresh Stronghold | (340,290) | 25m | Horde capital |
| Gorgonax crater | (180,300) | 22m | Optional world-boss clearing |
| Greenhollow Post | (-40,400) | 13m | Northern riding post and provisioner |
| Ancient Grove | (5,445) | Existing shifted heights | Northern scouting objective |
| Wyrmcrest Summit | (250,510) | 75m | Apex encounter on a real mountain, accessible by switchbacks |
| Westbank / Willow Rest | (-285,50..60) | 6m road | New river crossing and exploration cache |

The western crypt and the first three Abe missions retain their original positions. The underground Scarlet Crypts interior remains at (-400,-40,200), with its exit updated to the relocated cathedral exterior.

## Connections

- Emberwatch connects west to Highspire, Frostpeak and the Westbank river bridge.
- A separate forest trail leads to Silverglen.
- The north road branches toward Duskwood/Deathknell and the Greenhollow valley.
- The old captain trail becomes the start of the long eastbound Redridge pass through Rustwater to Bloodrock.
- Eastern roads connect Bloodrock, Gor'Kresh, the crater and a continuous mountain switchback route.
- A valley crossing connects Greenhollow with the eastern crater.

Road points and elevations are defined once in `WorldExpansionLayout.Routes`. Terrain painting follows those paths. Existing terrain samples are retained beneath the original quest route and translated beneath moved settlements, keeping their buildings and actors aligned. The summit uses a real 75-metre terrain plateau beneath its existing dais.

## Terrain, scenery and validation

The terrain reuses the existing grass/dirt/rock PBR layers and blends downloaded forest floor, rocky highland and summit snow layers. Four tiles use matching world-space height functions and connected neighbours. Old inner-map barriers are disabled; new boundaries sit at the expanded world edge. A 260-metre flowing western river has a physical bridge and a persistent cache. New nature instances are seeded and capped to a modest renderer budget; core settlements and roads are kept clear.

Use `Riftbound.Editor.WorldPresentationSetup.BatchGenerate` in an isolated project copy to configure textures, generate terrain and finish the scene. Do not regenerate over manual scene edits. `WorldExpansionResult.txt` records movement deltas, flight stations, terrain coverage/layers, seam clearance, road grades and dungeon preservation.

Validated 2026-10-31: four tiles, maximum measured seam difference 0.0000m, authored road grades at most37 degrees, original underground interior preserved. New scenery:323 instances /334 renderers. The startup/quest/save regression passes76 assertions, including solo startup, explicit companion call/dismiss, packaged audio, terrain sampling and old-save coordinate migration. GPU edit-mode overview/Bloodrock previews show imported textures; gameplay traversal and audio listening remain manual checks.
