# Riftbound map plan — The Roads That Remember

**Design proposal for Outpost-Expanded.** Seven destinations on the current 768 × 768 m map, with three regional questlines and one optional summit lead-in. Coordinates are candidate camp centers in Unity world metres `(x, z)`. Sample the authored terrain for elevation and shift a camp within its local site if the approach, slope, or clearance requires it.

## What the player does

The frontier feels busier when the roads carry people, goods, and consequences. Start with visible work: recover supplies, bring a signed tally home, clear a dangerous approach. Let the player discover that the records disagree only after seeing who depends on them. Each chain has a home NPC, an outward journey that mixes combat and interaction, and a return that changes a visible detail in the world.

| Site | Placement and silhouette | Quest use |
| --- | --- | --- |
| **1. Split-Seal Layby** | `(-94, -25)`, north of Highspire highway. Broken wagon, blue tarpaulin, spilled grain. | Scout the ambush, beat two raiders, recover the waybill. The route itself stays clear. |
| **2. Tallykeepers’ Rest** | `(-219, 29)`, east of the Westbank approach. Ledger table, drying lines, lantern frame. | Compare the wagon seal with a voluntary ferry tally. A bench and lit lantern become the landmark after turn-in. |
| **3. Lantern Rest** | `(-99, 194)`, east of Duskwood road on the way to the Deathknell branch. Warm watch hut, memorial ribbons. | Find a missing patrol pouch at a nearby overlook and listen to a witness before returning it. |
| **4. Redridge Weighpost** | `(156, 145)`, just south of the pass between Ashwood and Rustwater. Counterweight crane, salt crates. | Inspect a loaded axle and compare actual cargo weight with its signed manifest. |
| **5. Coilbreak Yard** | `(210, 184)`, north of the pass near Rustwater. Bronze piping and a pressure gauge. | Recover a regulator from two or three scavengers. Read a pulse, move clear, then operate a release wheel. |
| **6. Valley Relay** | `(68, 409)`, north of the Greenhollow valley crossing. Survey pennants and a low signal mast. | Bring an independent reading to Elara and learn where the Wyrmcrest pressure is coming from. |
| **7. Crownline Shelter** | `(349, 433)`, a candidate bench beside the upper switchbacks. Rope racks, windbreak, red pennant. | Recover a marked bronze fitting from an optional elite patrol. Get a readable view of the last switchback. |

## Questlines

### A. Two Seals, One Load

**Rowan → Split-Seal Layby → Tallykeepers’ Rest → Rowan.** Offer after Abe’s Stolen Supplies turn-in. Rowan needs to pay for a grain wagon that was never delivered. At the layby, the player chooses whether to pull one raider away or fight both. The waybill carries two authentic-looking seals for the same load. At Westbank, a ferry worker offers their own tally and remembers a wagon with one broken wheel. The two observations narrow the discrepancy without establishing which office caused it.

Reward a modest shipment stipend and XP. Restock Rowan’s visible grain sacks as the payoff. Keep the existing Willow Rest cache, bridge, and Selvek bounty separate.

### B. A Name in the Rain

**Lantern Rest → a nearby patrol overlook → Lantern Rest.** Halford can point players toward the watch hut, and its watchkeeper offers the quest on arrival from either direction. A patrol pouch was left behind during a fight near the Deathknell branch. Recover it, clearing a small ambush if needed. A Returned witness explains that the patrol passed despite what the roll says. The player records only what the witness agrees to share. The completed memorial adds a blank brass tag awaiting the patrol’s return, with an optional conversation about the Broken Toll.

Reward XP and a starter defensive gear option. Keep the existing Duskwood recon and Scarlet Crypts entrance available on their current terms.

### C. Pressure on the Pass

**Fizzlebang → Redridge Weighpost → Coilbreak Yard → Valley Relay → Elara.** Offer when the player reaches Rustwater. The manifest’s weight is wrong because an old bronze assembly went through as river ballast. Track it to Coilbreak, retrieve the regulator, and vent the pressure in a short encounter that uses Riftbound’s existing ground telegraph language. Elara’s relay reading confirms the pressure is moving through several linked sites. Neither a forged signature nor one failed gauge identifies who redirected the shipment.

Reward XP and a choice between two existing gear tiers appropriate to the region. The post-quest yard shows a repaired gauge and quieter pipes. This route stays outside Gorgonax’s boss clearing.

### Optional summit lead-in — The Weight of Bronze

**Elara → Crownline Shelter → existing Wyrmcrest approach.** Offer after Pressure on the Pass and Elara’s existing survey. Recover a marked part from a patrol before the shelter. The part shows that someone removed an anchor component. It leaves Vermithrax’s motives open and gives the player a reason to follow the switchbacks. The existing summit encounter retains its own access and outcome.

## Placement and rollout

1. **Build the western slice first.** Place Split-Seal Layby and Tallykeepers’ Rest and wire Two Seals, One Load. This proves the camp → clue → witness → turn-in loop in a compact route.
2. **Fill the two long travel corridors.** Add Lantern Rest and the Redridge sites. Build one readable encounter per stop, with a recovery or witness objective at the next stop. Reserve open approaches for mounted travel and keep starter areas, flight landings, and patrol paths clear.
3. **Add the optional summit leg.** Survey the upper bench on foot before placing Crownline Shelter. Keep the existing path through Wyrmcrest Lookout free of props and hostile pulls.
4. **Wire navigation and rewards.** Build atlas pins and the quest arrow from the same placed actor, interactable, or camp transforms. Show the camp as the broad destination before arrival, then point at the journal’s current objective. Route camp quests with separate stable IDs so they fit alongside the existing main and faction chains. Award XP through existing progression and tune after playing an ordinary route.
5. **Acceptance before expanding.** Walk each spur on foot and mounted. Confirm a player can see the landmark from the road, reach it without a jump or flight requirement, finish and reload without duplicate loot, and follow the arrow back to the right NPC. Check mixed-progress groups before sharing encounters in co-op.

### Source notes

- [Current roads and settlements](../WORLD_ZONES.md)
- [The March of Nine Hearths](../Lore/RIFTBOUND_LORE_BIBLE.md)
- [NPC knowledge and quest constraints](../Lore/NPC_STORY_CONTRACTS.md)
- [Gameplay and travel loops](../GAMEPLAY.md)
