# Three-Chimes Relay asset kit

This original kit turns a road stop into a working relay for the Greenhollow wagons. The bell tower is visible from the approach, the relayhouse receives loads through its stone arch, and the covered hoist and cargo racks tell the player where supplies change hands. The sign marks the junction. The bell, ladder and hoist are modeled set dressing.

## Models and layout

| Model | Intended use | Triangles |
| --- | --- | ---: |
| ThreeChimes_Relayhouse | Stone loading arch, slate eaves, shutters, lanterns and timber framing | 6,598 |
| ThreeChimes_BellTower | 6.9 m wayfinding silhouette with belfry, ladder, bell lip and clapper | 4,516 |
| ThreeChimes_LoadingBay | Canvas cargo station with pulley, ropes and stock racks | 3,412 |
| ThreeChimes_TrailSign | Trail directions and a hanging lantern | 404 |
| RelayRoad_Straight4m | 4 m wide × 4 m long through-lane | 432 |
| RelayRoad_Bend90_R5 | Quarter turn with 3/5/7 m inner/center/outer radii | 736 |
| RelayRoad_Ramp6m | 4 m wide, 1.2 m rise over 6 m, with stone sides | 826 |
| RelayRoad_Turnout6m | 6 m long pull-off that widens the 4 m lane to 6 m | 958 |
| RelayRoad_Junction4m | Three 4 m openings, one side spur | 1,328 |

The modules use metre scale and a shared 2048×2048 detail atlas for masonry, slate, oak, iron, brass, canvas, colours and ground surfaces. Original procedural artwork and mesh source live in build_kit.py. Run Blender 5.2 in batch mode with --python Art/ThreeChimes/build_kit.py -- followed by a new absolute output directory. It writes a packed .blend review scene, individual FBX and GLB exports, a PNG atlas and three renders. The FBX files and atlas under Assets/RPG/Art/ThreeChimes import into Unity.

## Road interfaces in Blender coordinates

Every joint is 4 m wide. The start line is X=−2..2 at Y=0. The straight ends at Y=4, the ramp and turnout at Y=6, and the junction continues through to Y=8. The junction's branch opening is X=5, Y=2..6. The bend turns from +Y to +X and ends at X=5, Y=3..7. The ramp ends 1.2 m higher than starts. Dress the shoulders after fitting a continuous drive surface to terrain. Empty spawn and walk lanes take priority over decorative props.

## Use in Riftbound

The Lantern Rest clearing is the first candidate for a Three-Chimes relay. Keep the north road clear and use independent foundations for the three structures. The in-memory Unity placement test renders a draft in context, drapes the entrance road over existing terrain, and checks the approach with foot and mount controller dimensions. Fixture checks do not cover actual input or entering building interiors.

The kit also supports a relay variant outside Gor’Kresh with darker cloth and signal timbers, or a small woodland toll stop made from the sign, turnout and loading bay. Survey each footprint separately. Place paths before cargo, keep the carriageway visible from the road, and start each evening focal point with one warm lamp.

The fitted tower and loading-bay exports extend their posts to meet the roofs. They are imported under the _Fitted filenames. The earlier art files remain available. The isolated Windows preview uses -includeThreeChimes together with -includeSettlementTrailheads and -includeSettlementActivity. It replaces the temporary Lantern Rest shelter with the relay, its approach and an updated atlas waypoint and notice. The entrance uses straights and a turnout. The bend, ramp and junction remain reusable modules for later placements.
### Regional surface pass

The regional Unity variants use existing Village plaster, rock trim and wood trim material references, with a second UV channel fitted to the correct timber and masonry bands. Road dirt and verges reuse the corresponding TerrainLayer albedos and normal maps. Road UVs follow world position and layer tile size across module joins. The original atlas remains the surface for the blue slate, canvas, metal, painted sign and other distinct features, with higher-resolution cloth and weathering detail. Original generated and existing project assets retain their source and license records.

The relayhouse, tower and loading bay have roughly 26–30% more triangles than their fitted predecessors. The added geometry is concentrated on the stone wedges, edges, bell and ridges. Road edge stones are sparser and irregular. Earlier Unity imports remain available. The regional playable build uses four new structure variants and a new straight and turnout. Other modules are available in the Blender exports.
