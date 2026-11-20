# Living Roads and Terraced Cities

Build a playable district and three roadside stops on the current four-tile map. The first delivery connects Emberwatch to Greenhollow through one recognizable camp, then extends the same principles to the eastern roads, Wyrmcrest and both faction capitals. Existing western camps, quest starts and named actors remain the reference points.

## 1. Walkable city elevations

| District | Layout and daily life | Traversal intent |
| --- | --- | --- |
| Emberwatch service court | Weighhouse, water trough, provision shed, scales and a covered loading bench. Keep the Abbey, central market, guards and Abe readable from the main square. | Start with a 1.2 m raised court and a 6 m cart ramp, 3.4 m wide. Add a separate stair shortcut once landing support and camera angles are known. Keep the west departure lane clear. |
| Highspire upper ward | Lower courier yard, ramp to the civic overlook, private doors for households and service steps around the back. Light the ramp and reserve a sheltered footway for vendors. | Survey the north-gate bends and castle camera before adding props. Preserve the west route and existing gates. |
| Gor’Kresh forge ledge | An earth-cut lower freight yard, red canvas shade, dark brace timber, ladder silhouettes and a broad ore ramp. Add forge bellows and workers bringing fuel from storage. | Build on a separate surveyed terrace, retain a direct mount loop through the main ward and keep stationary quests away from circulating workers. |
| Greenhollow post | Small timber additions and a tack shelter, rope drying racks, creek drainage and a provision queue. | Preserve the Greenhollow arrival footway and Ancient Grove approach. Use shallow platforms and short ramps near the wet ground. |

## 2. Camps and travel rhythm

Each stop needs a reason for being there, a landmark visible from its road, a sheltered place to stand and a clear way back. Keep uninterrupted rides between camps. Candidate centres use Unity X/Z metres and require terrain, vegetation and sightline surveys.

| Stop | Candidate | Place, purpose and story |
| --- | --- | --- |
| Lantern Rest | (2, 300), off the Greenhollow road | Lantern, roofed bench, cart ruts, a water barrel and a rain-stained tallyboard. A teamster waits for the next wagon. First addition. |
| Coilbreak Yard | (211, 188), Rustwater service spur | Ore sled, recovered boiler parts, windbreak and maintenance chalk marks. The camp explains the operation above Brass Hollows; access, rewards and the encounter use their existing rules. |
| Wyrmcrest Survey Refuge | (337, 323), lower approach bench | Stone wind wall, survey stakes, weather flags and a shelter with a view back down the valley. Leave the summit boss approach untouched. |
| Frostpeak relay | Foothill spur, survey pending | Timber stockade, a route marker that cuts through fog, sled loading and a sheltered worker pause. Retain a snow-to-grass transition along the approach. |
| Deathknell vigil | Approach to the Cathedral, survey pending | Two lamp stands, cracked masonry, a watch ledger and a flowing silhouette visible from the road. Preserve the dungeon approach and encounter space. |
| Western road | Existing Split-Seal Layby and Tallykeepers’ Rest | Give existing camps complementary cargo and route clues. Preserve the ford, bridge approaches, Reaver camp and quest interaction spaces. |

## 3. Civilian activity and co-op

Start with a porter between weighhouse and wagon, a runner between market and service court, and a groundskeeper looping the Abbey approach. A 2–5 second pause at meaningful stops keeps movement from looking mechanical. Keep named quest givers and merchants on their existing anchors. Use authored waypoints and verified heights. Keep walkers out of player actors, combat areas, bridge lanes and steep ramps. Decorative civilians ignore interaction and collision with players. Reuse existing locomotion and vary facing and pause times.

First release target is three walkers and two idle workers at Emberwatch, increased only after checking animation and frame time. Add a caravan arrival, town bell, forge activity and sheltered night shifts later. For anything shared in co-op, first define spawn ownership, reconnect behavior and what clients should see. Decorative paths may be local as long as they carry no quest, trade or combat state.

## 4. Models, textures and audio

- First prototype uses one 1024×512 color atlas for masonry, oak, plaster, slate, canvas, iron, amber and red accents. It includes a weighhouse, ramp terrace, shelter and waypost. The Blender generator, art direction and export instructions are in the settlement kit.
- Next make interchangeable retaining walls, stairs and corners, a posting board, trough, porch and service doors. Keep door height, pivots and architectural proportions consistent across module seams. Provide simple collision separately at Unity placement review.
- Use blue-slate roofs and ochre plaster at Emberwatch, granite and iron at Highspire, and red canvas with darker timber around the forge. Paint ground transitions where traffic would wear the surface. Add drainage, cleared foundations, wood piles and carts in service areas. Preserve cover in the wild.
- Place a small number of localized loops at visible sources such as forge bellows and creek drainage. Check voice and combat cues in-town. Use light falloff and warm entrance lights to reveal steps after dark without flooding adjacent roads.

## 5. Production and acceptance

1. Establish baseline on the current scene. Capture camera height, foliage, terrain grades, traffic lanes, quest fronts and lighting at each proposed site. Sample terrain plus actual physical support and map any affected tree or obstruction. Report geometry probes and input runs separately.
2. Assemble Emberwatch and Lantern Rest in a new additive review scene or isolated project copy. Place props from doorways outward. Give the ramp a collision surface and continuous lips, and protect quest approach and camera swing. Take matched eye-level views on foot and mounted before requesting production placement.
3. Apply reviewed batches in order. Keep terrain edits local, retain original terrain and serialized references, and compare protected trees, paint, details and seam heights before and after. Place civilians only after the authored routes have foot support and crossing clearance.
4. Make the settlements playable in sequence. Verify travel outbound and return with keyboard and mouse, on foot and on horse, waypoint correctness, two camera zooms, visible feet and hooves, shadows, and old-save load. Use real input through the test fixture for repeatable regression and report manual runs separately. Pair-test that cosmetic NPCs leave quest interaction usable from both peers.
5. Measure frame times and renderer counts at the market, in the wilderness and in co-op on the same machine/settings. Scale ambient actor counts and expensive lights against those results. Walk the new district at daytime and night, test 720p and 1080p, then produce a Windows candidate with exact source, receipts and a short Emberwatch → Lantern Rest → Greenhollow test route.

## Current implementation

The Blender kit is a preview with no scene placements. The geometry audit reached all 36 controller probe routes and flagged an on-foot camera squeeze at the Highspire start of the Westbank route. The first input run passed the bridge in both directions on foot and mounted, then stalled navigating the Highspire gate. The isolated gate reproduced the stall. The input fixture now aims camera yaw as well as actor heading at bends, matching the actual on-foot movement input basis.

### Site readings

Read-only 1 m terrain samples from the current expanded scene. Root count covers terrain-tree instances inside the footprint. Scene props, quest actors, road branches, physical support and sightlines still need placement checks.

| Candidate | Terrain height range | Steepest sample | Tree roots |
| --- | ---: | ---: | ---: |
| Emberwatch east court (25, −3) | −0.15–0.15 m | 6.8° | 0 |
| Lantern Rest (2, 300) | 7.56–9.08 m | 15.8° | 0 |
| Coilbreak Yard (211, 188) | 14.14–16.01 m | 11.3° | 0 |
| Wyrmcrest lower approach (337, 323) | 25.00–28.55 m | 25.1° | 0 |
| Wyrmcrest switchback edge (378, 373) | 14.53–34.25 m | 76.5° | 1 |
| Wyrmcrest lower bench (345, 350) | 18.47–31.54 m | 64.3° | 1 |

Reject the two cliff-edge candidates. Review a smaller stepped pad at the lower approach, or move the refuge farther downhill. Start art placement review at Emberwatch, and fit Lantern Rest's shelter and ground anchors to the existing grade rather than levelling the whole clearing.

### Verification results

The corrected D3D11 Play Mode fixture reported 17 passes. It covered both directions on foot and mounted at the bridge, Highspire gate and Greenhollow arrival, the full north road outbound on foot and return mounted, and Willow Rest claim and isolated save/load. The last mounted Reaver case still failed after the player took damage and dismounted. PlayerController.OnDamaged explicitly dismounts the player. The geometry probe reached the camp. Qualify the ride to a safe staging point and test the hostile entry separately before signing off on that route. The Unity Editor search index also logged a startup exception, and the fixture continued.

The four FBX and four GLB files were reimported in Blender and verified for one material, textured UVs, ground-level pivots and 56–224 triangles per model. The art has not been checked in the Unity scene. Manual camera play, co-op and scene placement remain unverified.

### Placement preview

A temporary Unity edit-mode preview stages the Emberwatch court southeast of the market and Lantern Rest in the north-road clearing. It uses the currently shipped scene for context and writes only review evidence. The city layout was shifted south after the first render showed an authored pine crossing the candidate ramp. The revised ramp is centred at X/Z (28, −22). The weighhouse faces the loading area at (30.55, −15.27). Lantern Rest has its shelter opening toward the road, with a bench, barrel, post and stone footings.

The D3D11 render and physics review completed successfully. The new ramp had a 0.02 m toe transition and a 1.18 m rise. The ramp and Lantern Rest pull-off reached their endpoints in both directions with foot and mount controller probes (eight checks total). No existing solid collider bounds intersected the seven model placements. The rendered review showed the new texture atlas and intact trees and fence. This evidence covers preview placement and geometry. Manual camera motion, co-op and civilian routes still require checks before shipping.

Run the isolated editor entry point Riftbound.Editor.SettlementPlacementPreview.Run to reproduce the temporary models, geometry probes and D3D11 captures in .code-check/world-production/settlement-preview. The authored scene and terrain require their own reviewed placement change to ship the new sites.

The follow-up Play Mode fixture used the same temporary placements, the normal PlayerController and horse summon, a virtual keyboard, and an isolated save. All eight input cases passed with a grounded endpoint, the expected mount state and a 0.07–0.09 m controller-to-support gap. The horse summoned on the upper deck and descended under normal movement input. The fixture took a screenshot with the active orbit camera at each endpoint. Steering was scripted and the fixture teleported between cases. The route entry point is Riftbound.Editor.WorldRoutePlayModeCheck.RunSettlementVerification, with results under .code-check/world-production/settlement-play-*.

### Standalone review build

Use a separate project copy containing Assets, Packages and ProjectSettings, with the copied Player Settings product name set to **Riftbound Settlement Preview**. Run Riftbound.Editor.SettlementPreviewBuild.Run in a Unity batch and pass a unique -gameplayBuildName. The build callback adds the two temporary groups to the processed scene, limited to this explicit entry point. The source scene and protected terrain hashes are checked by the normal build harness.

Start a new solo character in the review build. Look southeast of Emberwatch's market for the slate-roof weighhouse and ramp, then follow the Greenhollow road north to the canvas shelter on the east side of the road. Use Z on clear ground to summon the horse. Leave the full build folder alongside the executable.

### Coilbreak Yard review

The salvage yard sits around X/Z (211, 188), roughly 26 m off Redridge Pass and 76 m east of the Brass Hollows entrance. The approach branches north from the road at (205, 162.73), passes the waypost, and reaches the yard at (207, 187). The shelter faces arrivals. Copper salvage coils, a cart, a covered anvil and sorting supplies give the stop a distinct job. The existing Hollows and Rustwater interactions remain separate.

Redridge through-lane geometry passed in both directions on foot and mounted, before and after placement. The spur passed another four geometry probes and four scripted Play Mode input cases using the normal player and horse with an isolated save. Every Play Mode endpoint was grounded. The D3D11 preview rendered the yard in its landscape. This work uses temporary groups and leaves manual multiplayer, navigation marking, and production scene placement open.

Run Riftbound.Editor.CoilbreakYardPreview.Run or Riftbound.Editor.WorldRoutePlayModeCheck.RunCoilbreakVerification to reproduce the checks. Pass -includeCoilbreakYard to the isolated settlement build entry point to include it alongside Emberwatch and Lantern Rest.

### Gor’Kresh trailhead and combined review

The first Wyrmcrest draft serves as a lower trailhead shelter inside Gor’Kresh's eastern palisade at (371, 314). The slate roof, wind walls, bench and red survey pennant mark a rest stop ahead of the climb. The separate upper-mountain refuge still needs a site.

The original switchback centreline near (347.5, 314) runs into the War-Hall tower and Wind Rider Vark. The preview approaches east of them through (352, 314). That bypass and the spur passed eight controller geometry checks. The spur ends outside the shelter so riders can turn in the open. The original centreline obstruction remains.

The combined review build includes Emberwatch's service court, Lantern Rest, Coilbreak Yard and the Gor’Kresh Trailhead Shelter. In the editor, all 16 scripted Play Mode traversal cases passed with all four placements present, four cases per site covering both directions on foot and mounted. The packaged build booted and contains all four groups. Manual standalone traversal and multiplayer are still pending.

Use -includeSettlementTrailheads with Riftbound.Editor.SettlementPreviewBuild.Run for the combined build. Run Riftbound.Editor.WorldRoutePlayModeCheck.RunFullSettlementVerification for the combined input check.

## Playtest sequence and next expansion

1. **Wayfinding and activity.** Use M to select a settlement waypoint. Each stop has a local notice about the road, salvage work or the climb ahead. The Emberwatch court has one decorative porter walking a surveyed loop. Review notice prompts, map legibility, gait and clearance before placing additional walkers.
2. **Brass Hollows solo loop.** Start at Tinker Fizzlebang, follow the entrance waypoint and test the teaching vent before engaging the sentry. Check that telegraphs are readable while moving, Veyl leaves time to counter, and the service exit works after the pump shuts down. Verify re-entry and a load from an earlier stage with a test save.
3. **Westbank and Reaver approach.** Measure a mounted staging point before aggro. Verify the ride to that point and approach on foot from there. Keep the existing damage-triggered dismount rule.
4. **Settlements and terrain.** Apply reviewed placements only after playtest feedback and scene review. Survey an upper refuge beyond Gor’Kresh rather than expanding the trailhead onto a bluff. Define one new walking loop per settlement, then profile frame time and show evidence that actors stay clear of mounts, doors and quest givers.
5. **Co-op and release.** Run host/guest checks for the same world layout, non-interfering cosmetic actors, combat animations and reconnect. Brass Hollows remains a solo encounter. Include smoke results, manual outcomes, save limits and a pinned Windows candidate in the release notes.

Preview 04 combines the four sites, four atlas waypoints and readable notices, one walking porter, and the revised solo Brass Hollows encounter. The published preview workflow adds -includeSettlementActivity alongside -includeSettlementTrailheads. The combined Editor fixture reports 22 passes, including all 16 route traversals, four E reads, the porter completing 10 loops, and M opening the atlas with the settlement destination resolving through the waypoint API. The atlas was captured at 1280×720. The released Hollows and civilian fixtures also passed when rerun on the combined branch (35 and 15 checks). The Windows candidate booted with 0 build errors and 343 warnings. Manual standalone input, walking animation quality and co-op remain open.

### Three-Chimes Relay art kit

The candidate Lantern Rest expansion pairs a stone-arched depot, signal tower, hoist and cargo bay with a connected entrance path. The new modular kit includes 4 m straights, a 90-degree bend, ramp, turnout, junction and lantern sign. See [kit dimensions and fit](../Art/ThreeChimes/README.md). The Unity preview is temporary. Production placement and normal-input checks require a follow-up through the world review workflow.

The playable Three-Chimes candidate replaces the simple Lantern Rest group only in the isolated preview build. It adds the fitted depot, bell tower, loading bay, waypost and terrain-draped approach, and keeps the existing north road, preview save identity and waypoint location. Use M and choose Three-Chimes Relay. The bell, ladder and hoist are set dressing. The route test uses real player input with scripted steering. Standalone boot and packaged scene data are checked separately.
