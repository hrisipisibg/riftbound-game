# The Roads That Remember

Build a cohesive 20–30 minute adventure across the existing frontier. The player starts with Abe, follows a disputed shipment through Westbank, then heads east to the Brass Hollows to shut down a siphon. Make the adventure work from a fresh character and a reloaded save before expanding the map.

## First pass — make the new version playable

- Integrate the latest frontier source with the existing movement and combat fixes. Check camera steering, walk/run, jumps, queued abilities, cooldowns, and mount transitions together.
- Retain quest, encounter, faction and profile IDs. Follow existing solo and online save boundaries. A quest advertised for co-op must have a working completion path for host and guest.
- Walk the western loop from Rowan through Split-Seal Layby and Tallykeepers’ Rest. Show the current objective in the journal and on the map, then give the return trip a visible payoff.
- Make the Brass Hollows a short, readable dungeon. Give each room a clear purpose. Teach the steam tell in a safe first room, combine it with a fight in the next, then let Foreman Veyl test it.
- Give Veyl a readable windup, a committed strike, and a recovery window. Keep encounter outcomes authoritative and reward turn-in idempotent.

## Art and atmosphere

- Use the available CC0 medieval kit for a compact group of crates, grain sacks and lanterns along the western route. Keep the existing road clearance and terrain data. Preview fit before placement.
- Use warm lanterns and indigo cloth for western stops, then cold shale and hot brass for the Hollows. Players should recognize which area they are entering from silhouettes and lighting.
- Give the dungeon entrance, final chamber and return to the surface distinct audio beats. Reuse existing sound assets and preserve headroom for combat cues.
- Keep the HUD sparse. Show the active objective, target tell and action feedback when useful. Evaluate the atlas at 1280×720 and 1920×1080.

## Release checks

- Verify fresh start, quest acceptance, combat, both turn-ins, reload, and no duplicate reward.
- Verify on-foot and mounted travel along both western and eastern approaches. Include turning, camera clearance and stepping off ledges.
- Verify the dungeon’s entry, exit, death, retreat and reset paths. Test host and guest separately before promoting online dungeon support.
- Build a new Windows candidate and keep the prior playable build. Record what was tested on the exact source.
- Expand next into Lantern Rest, Coilbreak Yard and the summit lead-in after this route plays end to end.

## Adventure Guide and Quest Journal

The journal should answer two questions when a player returns to Riftbound. What was I doing, and where can I go next? Use existing progress and world locations so the guide stays accurate as the frontier grows. Keep J as the entry point, M for the full atlas, and Shift+J for encounters.

### First release

| View | Contents | Player action |
| --- | --- | --- |
| Overview | Current campaign step, accepted frontier tasks, and a short list of nearby or available adventures | Open a task or deliberately track its destination |
| Quest Journal | Abe's chapter chain, the western road shipment, and Brass Hollows. Group active, ready to turn in, and completed work | Read the objective, select Track, or open the atlas |
| Explore | Route cards for Emberwatch–Greenhollow, the western road, Redridge–Rustwater, and the Wyrmcrest approach | Read a travel tip and pin a supported destination |
| Encounters | Existing encounter journal reached from the guide | Read abilities and rewards in the current encounter interface |

Use live quest properties to resolve objectives and availability. Keep quest stages, unlocks, reward rules, and encounter ownership in the systems that already control them. Read existing world anchors and placed waystops for locations and omit buttons when a site is absent. Brass Hollows and the western quest show their current solo availability. A route card can describe a path before a player has accepted a quest. Quest details should show concrete steps and objectives, with completion states sourced from progress.

### Route card content

- **North Road.** Leave Emberwatch for Greenhollow. Show Three-Chimes Relay if the playtest scene includes it and use the live notice anchor for its waypoint. Add a short travel note about the pull-off and the bell tower.
- **The Missing Shipment.** Start with Rowan, then point to Split-Seal Layby, Tallykeepers’ Rest and Westbank. Show the live western objective once accepted.
- **Into the Brass Hollows.** Point players to Rustwater and explain that Tinker Fizzlebang starts the solo mine quest. Use the current entrance location only when the mine exists. Describe the steam warning and recovery windows after the appropriate guide entry is available.
- **Climb to Wyrmcrest.** Use the Gor’Kresh trailhead and existing summit anchors as distinct stops. Describe the lower shelter as an optional stop, and keep encounter details in the encounter journal.

### Build order

1. Inventory the current journal rows and the game properties each reads. Capture every existing feature in a mapping before changing its presentation. Replace raw coordinates with named in-game destinations.
2. Build the three journal views with a shared list and detail layout. Keep reading and navigation usable at 1280×720 and 1920×1080. Use the current UI style helpers, allow longer text to scroll, and keep actions tied to the selected entry.
3. Add route content and live destination resolution. Use the existing quest focus methods and the existing map pin API. Reading, filtering and switching views should leave the current pin alone. When tracking replaces a pin, label that action before it fires.
4. Follow up after playtest with live faction errands, region recommendations, and encounter links on relevant entries. Introduce discovery tracking or rewards only through a separately reviewed progression change.

Acceptance covers a fresh character, a live active quest changing to ready, both frontier quests active, missing preview-only sites, an existing manual map pin, offline and co-op availability, reload, and both target resolutions. Quest, map, chat and escape controls must keep their established behavior. Compile the exact branch and run the acceptance cases during integration before building a Windows candidate.
