# Riftbound — NPC story and dialogue contracts

Authoring specification for Veyr and the March of Nine Hearths. **Design baseline: authoring this specification did not apply its dialogue, conditions, routines, quests or choices to runtime or the scene.** `Docs/Lore/RIFTBOUND_LORE_BIBLE.md` owns canon; this file maps it onto inspected existing actors and state. Existing rewards, combat outcomes, faction/race keys, actor IDs and saves remain unchanged.

## 1. Canon and character rules

The nine great anchors are Highspire, Frostpeak, Silverglen, Deathknell, Rustwater, Gor'Kresh, Duskwood, Ancient Grove and Wyrmcrest. Emberwatch is a road relay, not a secret tenth anchor; Greenhollow is a riding/work post serving the northern grove. The Witness Lattice prevents competing unrealized histories from physically overlapping. Its legitimate maintenance depends on freely witnessed agreements. The corrupt Tithe of Names feeds anchors by erasing testimony and memory. **Narrative erasure never deletes a character, save, player name, quest record or inventory.**

Chronology: First Binding412 years ago; Tithe introduced103 years ago; Ashfall61 years ago; Broken Toll18 years ago; Hollow Dusk3 weeks ago. No NPC who could not personally witness an event claims first-hand certainty. Written accounts can be unreliable; that is authored story, not permission to randomize objectives or break persistence.

The Hearthward Compact seeks public protection and central records but has used emergency debt and compulsory registration. The Ashen Pact defends clan/Returned/free-contract sovereignty but tolerates dangerous private obligations and reprisals. Neither faction is a racial morality category. The Roadward Truce permits cross-faction cooperative travel. Runtime `Alliance`/`Horde` and existing race keys stay compatible; narrative aliases do not authorize PvP or restrict the roster.

The Hollow Choir exploits forged witness records. Ordinary Returned are people with ordinary legal, family and work relationships. Hostile ghoul/undead encounter targets are specific threats; their `Undead` visual or shared `ghoul` target key is not proof that Returned civilians are evil.

Mara was Abe's sister. She died during the Seventh Wagon evacuation at the Broken Toll18 years ago. Nineteen people were officially recorded as lost; Selvek, a former ferrymaster and Abe's colleague, was among them and later Returned. The official ledger refuses to recognize his return. Selvek is currently an **Undead/Rogue** enemy visual; preserve it. His exclusion explains his toll-taking, not his violence. Existing captain-defeat and lockbox outcomes remain; no spare/recruit/negotiation branch is implemented by this document.

## 2. What the existing code actually supports

Inspected sources: `QuestGiver.cs`, `PlayerProgress.cs`, `PlayerController.cs`, `TownNPC.cs`, `NPCGossipWindow.cs`, `FactionQuestSystem.cs`, `ExplorationSites.cs`, `RiverReaverCamp.cs`, `PlayerServices.cs`, `WorldRegions.cs`, selected encounter scripts, current scene NPC/Enemy ID fields and `Docs/WORLD_ZONES.md`.

- `FactionQuestSystem` contains35 quest definitions. `giverNpc` and talk-objective `targetId` compare against `TownNPC.npcName` case-insensitively. **NPC names currently act as identity keys.** Cosmetic renaming without an alias/migration contract would break quests.
- Abe's three main missions are index-based: `QuestIndex`0,1,2; `QuestAccepted`, `QuestReady`, `QuestComplete`; names **The Forest Road**, **Stolen Supplies**, **The Ashwood Captain**. `PrototypeHUD.DrawQuestWindow` displays `PlayerProgress.QuestName` and `QuestDescription`. Editing only serialized `QuestGiver.introduction/progress/completion` will not replace those actual lines.
- Abe also gives the separate FactionQuest `alliance_highspire_dispatch`. Its availability is not automatically proof that all three main missions are complete; its current empty prerequisite must not be silently replaced by a new quest gate during a prose pass.
- Faction quest conditions available now include `ActiveQuestId`, `CurrentProgress`, `GetActiveQuest()`, `IsQuestCompleted(id)`, `HasAvailableQuest(npcName,player)` and `HasActiveQuestToComplete(npcName,player)`. Resolve against the intended local player/context, not another player's singleton or host progress.
- Elara is created by `ExplorationSites.EnsureCartographer` with `ExplorationSites.CartographerName = "Cartographer Elara"`. Her existing quests are `frontier_cartography` and `river_reaver_bounty`; the bounty requires completed cartography. Survey IDs are `westbank-overlook`, `bloodrock-vista`, `wyrmcrest-lookout`; objective group `frontier-vistas` is not a fourth physical marker.
- Selvek is an `EnemyController`, not a `TownNPC` quest giver. `westbank-reaver-captain` is the defeat key and `river-reaver-lockbox` is the cache key. Current target and reward rules stay authoritative.
- Rowan and Mira are existing merchants/services; no FactionQuest definition was found that gives them a story quest. Service greetings must not create objectives, purchases or quest credit merely by being displayed.
- TownNPC presently has one generic greeting/bark and broad name/role-based lore branches. A condition-driven content resolver and persistent conversation memory are **proposals**, not existing functionality. Do not introduce lore flags while pretending this is text-only.
- Player action selection prioritizes a nearby downed ally revive, then considers eligible interactions by distance. Keep current `[E]` eligibility/ranges and modal ownership; lore must not create another competing input path.

## 3. Identity and region registry

Keep each quoted key exactly as written below. Display aliases/role tags may be proposed separately, never substituted into saved/network keys in a content pass. Bosses/hostiles use `enemyId`; their nameplate text alone is not identity.

| Existing identity key | Type / region | Existing quest/state links |
| --- | --- | --- |
| `Abe` | QuestGiver; Emberwatch relay | MainQuestIndex0–2; `alliance_highspire_dispatch` |
| `Cartographer Elara` | Runtime-created TownNPC; Greenhollow | `frontier_cartography`, `river_reaver_bounty` |
| `Quartermaster Rowan` | Merchant/service; Emberwatch | Existing trade actions; no observed FactionQuest giver ID |
| `Provisioner Mira` | Merchant/service; Greenhollow | Existing trade actions; no observed FactionQuest giver ID |
| `westbank-reaver-captain` / Captain Selvek | Enemy; Reaver camp(-283,78) | `river_reaver_bounty`; `river-reaver-lockbox`; preserve Undead/Rogue |
| `Grand Duke Ronald` | TownNPC; Highspire | Target of `emberwatch_to_highspire`, `frostpeak_to_highspire`, `silverglen_to_highspire`, `alliance_highspire_dispatch`; gives `highspire_royal_council`, `highspire_foothills_convoy`, `highspire_duskwood_recon` |
| `Overlord Mor'gok` | TownNPC; Gor'Kresh | Target of `bloodrock_stronghold`, `deathknell_to_gorkresh`, `rustwater_to_gorkresh`; gives `gorkresh_war_forge`, `gorkresh_caldera_threat`, `gorkresh_wyrmcrest_scout`, `gorkresh_apex_clash` |
| `Shadow Priestess Alyssa` | TownNPC; Deathknell | Target `deathknell_shadow_rite`; giver `deathknell_to_gorkresh` |
| `Deathstalker Morzan` | TownNPC; Deathknell | `deathknell_ghouls`, `deathknell_cauldron`, `deathknell_shadow_rite` |
| `Boss Griznak` | TownNPC; Rustwater | `rustwater_scavengers`, `rustwater_machinery`, `rustwater_blast_check` |
| `Tinker Fizzlebang` | TownNPC; Rustwater | Target `rustwater_blast_check`; giver `rustwater_to_gorkresh` |
| `Thane Baelor Ironbeard` | TownNPC; Frostpeak | `frostpeak_troggs`, `frostpeak_forge`, `frostpeak_armory_check`; target `highspire_foothills_convoy` |
| `Blacksmith Torvald` | TownNPC; Emberwatch | Target `emberwatch_armory_check`; giver `emberwatch_to_highspire` |
| `Gundrak Stonehammer` | TownNPC; Frostpeak | Target `frostpeak_armory_check`; giver `frostpeak_to_highspire` |
| `Sunwarden Thalorien` | TownNPC; Silverglen | `silverglen_sprites`, `silverglen_attunement`, `silverglen_ley_counsel` |
| `Magister Kaelen` | TownNPC; Silverglen | Target `silverglen_ley_counsel`; giver `silverglen_to_highspire` |
| `Inquisitor Lucian` | TownNPC; Duskwood exterior | Target `highspire_duskwood_recon`; giver `duskwood_crypt_purge`, `alliance_wyrmcrest_summit` |
| `Captain Halford` | TownNPC; Emberwatch | `emberwatch_raider_cull`, `emberwatch_armory_check` |
| `Warlord Gorgash` | TownNPC; Bloodrock | `bloodrock_cull`, `bloodrock_munitions`, `bloodrock_warlord`, `bloodrock_stronghold` |
| `Blacksmith Garok` | TownNPC; Gor'Kresh | Target `gorkresh_war_forge` |
| `Archmage Karen` | TownNPC; Highspire | Target `highspire_royal_council` |
| `boss-malakar` / High Inquisitor Malakar | Enemy/encounter; crypt interior | Existing CathedralBossEncounter; no new dialogue quest proposed |
| `boss-abbot-vael` / Dread Abbot Vael | Enemy/encounter; crypt interior | Existing CathedralBossEncounter stage/death state |
| `vermithrax-boss` / Vermithrax the Ember Sovereign | Enemy/encounter; Wyrmcrest | `gorkresh_apex_clash`, `alliance_wyrmcrest_summit`; existing defeat outcome |
| `worldboss-gorgonax` / Gorgonax the Worldbreaker | Enemy/encounter; crater | Existing WorldBossEncounter; no new faction objective ID observed |

Region centres are navigation context, not exact NPC interaction points: Highspire(-200,-60), Frostpeak(-95,-90), Silverglen(-150,75), Deathknell(-240,180), Rustwater(240,150), Bloodrock(280,210), Gor'Kresh(340,290), Duskwood(-220,260), Greenhollow(-40,400), Ancient Grove(5,445), summit(250,510), crater(180,300). Resolve actual actor transforms for interaction/waypoints. Elara is created near `WorldRegions.Greenhollow + (-6,0,-3)`, not necessarily the zone-centre marker.

## 4. Character dossiers and authored lines

Lines are proposed English content. G=default greeting; P=existing active-state progress; A=after a relevant completed state/service result. Where no friendly conversation exists, P is combat/encounter copy and A is an identified surviving speaker's aftermath line. A dead hostile does not acquire a post-defeat conversation. No new deep-secret flag is assumed; late revelations are explicitly quarantined in section6.

### 01 — Abe, the roadwarden who stopped the wagon

**Role/need:** Retired roadwarden at Emberwatch's relay. Wants today's supply road safe and a version of the Seventh Wagon account he can bear to read. **Flaw/secret:** He obeyed the order to hold the Seventh Wagon18 years ago; his sister Mara died among those recorded lost. He has let the archive describe an administrative delay instead of his personal decision. **Knows:** road work, Selvek before the toll, the order he obeyed and his own guilt. **Does not know:** the full Lattice mechanism, Choir leadership or whether every lost person's testimony survived. **Voice:** short practical instructions; counts wheels and rations when anxious; never a prophetic lecture.

- G, no active main objective: “The road's gone quiet. Bad quiet. Three raiders are keeping our carts in the yard. Clear the way, then come back to me.”
- P, `QuestIndex==0 && QuestAccepted && !QuestReady`: “Three raiders. Keep your strength for the road home; a victory nobody returns from feeds no one.”
- P, index1 before/while recovering supplies: “Three marked crates, in the camp east of the road. Bring the stores back. Leave the reckoning to daylight.”
- P, index2 active: “The captain holds the ruined watchtower. Watch the heavy blow before it lands. I need you back, not brave on a memorial.”
- A, `QuestComplete`: “You brought the road back to us. I once kept a wagon waiting until there was no road left. I won't spend your good work calling myself innocent.”
- Separate available-dispatch line, `alliance_highspire_dispatch` actually eligible: “Highspire needs our report. Take the western highway to the Keep; let Ronald answer for what is written under his seal.”

**Work/interaction:** Seated/standing inspection of an existing dispatch book is a future ambient proposal; do not walk him off the quest/service apron or move the root. His pauses can suggest guilt without adding a new objective. **Co-op knowledge:** main-index lines are personal to the interacting player. Completion of one friend's three stages does not make Abe congratulate all clients. Mara/Seventh Wagon named revelation needs an approved explicit topic/flag; the current A line alludes without asserting that the player already investigated it.

### 02 — Cartographer Elara, keeper of contradictory roads

**Role/need:** Cartographer at Greenhollow, comparing manifest routes to physical survey marks. Wants independently witnessed observations she can distinguish from copied records. **Flaw/secret:** She can mistake corroboration for certainty and distrust a true recollection that has no map mark. She has noticed incompatible dates but has not proved who forged them. **Knows:** the three survey sites, local manifest discrepancies, the Reaver threat and the publicly disputed declaration that Selvek was lost; the proposed aftermath reflects her own record comparison, not a newly awarded player evidence item. **Does not know:** Abe's private confession, Selvek's full Returned history or the Choir's entire plan. **Voice:** precise questions, corrections and qualified claims; never pretends the map is infallible.

- G: “A copied map repeats its author's mistake. Tell me what you find with your own eyes.”
- P, active `frontier_cartography`: “Westbank Overlook, Bloodrock Vista, Wyrmcrest Lookout. Earlier discoveries count. Three observations, not three guesses.”
- A, cartography completed and bounty available: “Your marks agree with the land. The river report does not: Selvek's toll threatens the crossing. Stop him, then bring me an account I can actually verify.”
- P, active `river_reaver_bounty` not ready: “The camp is on the western bank, north of Westbank crossing. Selvek commands it. Keep a clear way back down the trail.”
- A, bounty completed: “The crossing has a chance to breathe. A dead man on a ledger and a living threat on the bank were both Selvek. Neither fact erases the other.”

**Work/interaction:** Future map/rubbing comparison at the existing cartographer position; no random roaming onto the arrival path or spawning additional permanent maps/markers. Keep the three existing survey/cache IDs and lockbox rule. **Co-op knowledge:** discovered/completed observations and reward eligibility remain individual. Her personal map-copy theory is public; Mara's fate is not disclosed merely because another player defeated Selvek.

### 03 — Quartermaster Rowan, the ledger that never balances

**Role/need:** Emberwatch quartermaster. Needs enough stock to survive a delayed convoy and enough honest receipts to refuse another emergency requisition. **Flaw/secret:** Rounds a shortage down in public to prevent panic, thereby making later reports look dishonest. **Knows:** recent shortages, unit prices and damaged seals; not who ordered the Tithe or why histories overlap. **Voice:** spare, dry arithmetic, occasional tired kindness.

- G: “You can buy what is here. The ledger lists a little more than the shelves do.”
- P, service open/ordinary repeat greeting, no new quest: “If you're heading out, count your potions now. I can't mend an empty bottle from the other end of the road.”
- A, only after a confirmed authorized purchase: “Count it before you go. A mark on my page isn't the same as a thing in your hand.”

**Work/interaction:** Inventory/check-crate gestures may later reuse existing props; no item automatically granted, loss deducted or “shortage” mechanic applied. Preserve `Merchant.npcName`, prices/caps and3m trade eligibility. **Co-op knowledge:** a receipt belongs to the buyer; rejected/full-inventory transactions get existing failure feedback, never this success line. No persistent “helped Rowan” flag exists.

### 04 — Provisioner Mira, trust measured in meals

**Role/need:** Greenhollow provisioner. Keeps road crews and returned travellers supplied; needs promises that can be fulfilled without trapping workers in debt. **Flaw/secret:** Carries unpaid small accounts herself and avoids confronting a neighbour who repeatedly abuses that trust. **Knows:** workers, passing caravans, unusual settlement orders and useful routes; does not know the exact origin of the forged memorials and will not expose a frightened customer's identity as gossip. **Voice:** warm, concrete and firm about boundaries; no cheerful cartel slogans.

- G: “A road crew can't live on assurances. Sit if you need a moment; trade when you're ready.”
- P, ordinary service conversation: “Elara checks the route marks. I check that the people sent to find them have something to eat.”
- A, confirmed authorized purchase: “That's yours. If someone calls it a favour later, show them the receipt.”

**Work/interaction:** Future supply-sorting only within her existing service footprint. No new sit/rest buff, meal item, credit system or feeding objective is implied. Preserve her merchant role and clear approach from the arrival footway. **Co-op knowledge:** shop state and purchases are individual; greeting can be shared public lore, not proof the whole party paid or earned trust.

### 05 — Captain Selvek, returned but not received

**Role/need:** Former ferrymaster, now River Reaver leader. Wants recognition of his return and compensation for years officially treated as non-existent. **Flaw/secret:** Converts exclusion into a claimed right to collect violent tolls. He remembers the held Seventh Wagon and Abe, but cannot prove what happened to every passenger. **Knows:** his own return, ferry work and local complicity. **Does not know:** the entire Choir network; he is not secretly its universal mastermind. **Voice:** controlled toll-house formalities that become bitter when recognition is refused.

- G/aggro proposal, no gossip menu: “Crossing charge. Don't ask which book grants me the right. Yours says I never came back.”
- P/combat bark proposal: “Nineteen names closed. Mine still breathing. Pay attention.”
- A, spoken by Elara after the player's existing bounty completion: “Selvek was refused a place among the living. He made other people pay for that refusal. We must record both.”

**Work/interaction:** Keep existing `Undead`/`Rogue`, captain ID, guards, home/leash and defeat-gated cache. No parley button, surrender threshold, nonlethal flag, recruitment or new lockbox testimony item. A future toll/manifest prop requires a world/content contract and cannot block the repaired trail. **Co-op knowledge:** barks disclose public hostility; detailed Broken Toll recognition is optional authored revelation, never granted to all late joiners from one player's private dialogue. Defeat is existing authoritative state, not a choice memory.

### 06 — Grand Duke Ronald, protection with a sealed exception

**Role/need:** Highspire civic protector; needs the archive and food routes stable. **Flaw/secret:** Conceals emergency archive abuses as necessities, fearing disclosure will destroy public trust faster than reform can rebuild it. **Knows:** approved emergency procedures and his own signatures; not every falsified local record. **Voice:** considerate public language with carefully passive descriptions of responsibility.

- G: “Highspire is answerable for the roads it claims to protect. Tell me where that protection has failed.”
- P, active Highspire council/convoy/recon step: “Bring the report from the named witness. A reassuring summary cannot replace it.”
- A, `highspire_duskwood_recon` completed: “Lucian's account is entered. I will not call the record complete merely because the seal is mine.”

**Work/knowledge:** Future clerk/dispatch inspections stay off the gate/well route; no throne displacement. The player sees only their eligible report lines. Specific archive-order disclosure requires a new reviewed knowledge condition, not generic completed-faction progress.

### 07 — Overlord Mor'gok, consent with an unpaid consequence

**Role/need:** Gor'Kresh leader protecting clan, Returned and free-contract sovereignty. **Flaw/secret:** Defends consent publicly while tolerating reprisal raids by allies he fears to alienate. **Knows:** clan obligations, forge pressure and caldera/summit threats; not whether every claimed debt was freely agreed. **Voice:** direct questions and reciprocal obligations; never “the Horde wants war” by default.

- G: “No clan speaks your promise for you. Tell me what you can do, and what you expect in return.”
- P, active existing Gor'Kresh objective: “A vow does not make the caldera safe. Finish the work we actually agreed.”
- A, `gorkresh_apex_clash` completed: “You ended the summit threat. I will hear the cost before anyone names it a triumph.”

**Work/knowledge:** Existing capital role/root retained. Receiving one emissary dispatch does not reveal another player's faction route. Reprisal complicity is later topic-gated lore, not an immediate alignment/hostility switch.

### 08 — Shadow Priestess Alyssa, custody without ownership

**Role/need:** Returned memory custodian at Deathknell. Helps people distinguish chosen testimony from imposed recollection. **Flaw/secret:** Withholds painful fragments for fear the listener cannot bear them; protection can become another denial of consent. **Knows:** local Returned accounts and custodian practice, not a supernatural perfect truth. **Voice:** asks permission; distinguishes “remembered” from “certain.”

- G: “You may tell me what you remember. You may also decline.”
- P, `deathknell_to_gorkresh` active: “Take our account to Mor'gok in Gor'Kresh. It is testimony, not permission to speak for everyone here.”
- A, that quest completed: “Our words arrived without being made into an oath we did not give. That matters.”

**Work/knowledge:** Quiet listening ritual is proposed ambience; no compulsory player-memory edit or class change. Personal testimony remains individual. She does not treat all Returned encounters as corruption or claim Mara's memory is stored in an existing item.

### 09 — Deathstalker Morzan, the guard at an open door

**Role/need:** Deathknell security; needs civilians safe from hostile remains and opportunists. **Flaw/secret:** Uses secrecy and pre-emptive suspicion, undermining the freedom he protects. **Knows:** patrol incidents and which threats are attacking now; cannot classify moral worth by body/race. **Voice:** quiet, specific threat descriptions; no romantic praise of undeath.

- G: “The person at the door gets a name before they get a verdict.”
- P, active `deathknell_ghouls`/`deathknell_cauldron`: “Stop the creatures attacking the road. Leave the people sheltering behind it alone.”
- A, `deathknell_shadow_rite` completed: “Alyssa has heard you. I can guard a threshold; I cannot decide what you must remember.”

**Work/knowledge:** Any future patrol stays within a bounded apron and returns for interaction. Existing defeat objectives are not retargeted at civilians. Security findings are public, private testimonies are not party-wide knowledge.

### 10 — Boss Griznak, a signature is not a worker

**Role/need:** Rustwater contractor balancing orders and maintenance wages. **Flaw/secret:** Writes aggressive completion clauses and then pays overtime privately when reality breaks them. **Knows:** contracts and missing shipments; not all causes of anchor pressure. **Voice:** brisk clause-by-clause speech, practical costs, no “time is money” caricature.

- G: “Before you agree, read the part about who repairs what breaks.”
- P, `rustwater_blast_check` active: “Fizzlebang signs the machinery manifest. I sign the delivery contract. Don't let anyone exchange one for the other.”
- A, that quest completed: “One signed manifest. Good. Now the capital can argue about quantities with facts.”

**Work/knowledge:** Proposed board/ledger animation creates no auction transaction. Known contract trouble is public; unpaid worker debt is later optional lore. Existing scavenger targets/rewards remain intact.

### 11 — Tinker Fizzlebang, maintenance before spectacle

**Role/need:** Rustwater engineer who must keep the anchor machinery repairable. **Flaw/secret:** Hides a temporary bypass because admitting it would halt food-and-ore transport. **Knows:** load, materials and test results; not the whole Tithe scheme. **Voice:** literal failure modes and dry corrections; accidents have consequences, not punchlines.

- G: “It's running. That is a condition, not a guarantee.”
- P, `rustwater_to_gorkresh` active: “Take the manifest through Bloodrock to Gor'Kresh. No extra promise on my signature.”
- A, that quest completed: “Delivered as specified. Now we can repair what the next contract assumes already works.”

**Work/knowledge:** Inspection routines must not spawn explosions or damage service users. Technical bypass details remain topic-gated, not broadcast to a newcomer by ambient bark. No new bomb item or sabotage choice is applied.

### 12 — Thane Baelor Ironbeard, the oath of material duty

**Role/need:** Frostpeak leader responsible for shelter, metal and forge-anchor work. **Flaw/secret:** Measures duty in output and can overlook the injured worker making it possible. **Knows:** clan promises and material shortages; not every central archive use of the iron. **Voice:** weight, heat and shared labour rather than generic drinking jokes.

- G: “An oath weighs what people must carry to keep it.”
- P, `frostpeak_armory_check` active: “Gundrak has the inspection. A bright edge is no proof the metal will hold.”
- A, `highspire_foothills_convoy` completed: “You carried the request honestly. I owe my workers an answer just as honest.”

**Work/knowledge:** Future forge oversight remains beside current infrastructure; no automatic tribute deduction. Clan accounts are public; personal work injuries are not a global party quest flag.

### 13 — Blacksmith Torvald, a useful edge

**Role/need:** Emberwatch smith, connecting the village's safety to usable equipment and a truthful dispatch. **Flaw/secret:** Repairs unsuitable requisitioned gear instead of challenging the order that sent it. **Knows:** actual wear, local supply quality and sealed dispatch destination; not Ronald's complete archive policy. **Voice:** terse tactile observation, then one actionable instruction.

- G: “Show me where it catches. A clean blade can still be a bad tool.”
- P, `emberwatch_to_highspire` active: “Take the western road to Highspire. Ronald needs the dispatch; you need room to get there in one piece.”
- A, that quest completed: “The message reached the Keep. Perhaps the next load of iron will reach us.”

**Work/knowledge:** Hammer/rest loops are proposed and cannot play through active conversation. No unsolicited repair fee, temper bonus or item grant. A player's dispatch completion changes only their response.

### 14 — Gundrak Stonehammer, the mark beneath the polish

**Role/need:** Frostpeak armorer who signs inspected loads. **Flaw/secret:** Once accepted a flawed batch under pressure and now repeats tests to exhaustion. **Knows:** material provenance and inspection marks; not whether an archive copied a witness signature later. **Voice:** patient, procedural, insists on checking the hidden surface.

- G: “Turn it over. The maker's mark tells me less than the cracks around it.”
- P, `frostpeak_to_highspire` active: “Carry the clan's account to Ronald. Don't call it tribute on our behalf.”
- A, that quest completed: “Then our account stands as given. My mark covers the work, not every use they make of it.”

**Work/knowledge:** Proposed material sorting uses existing work area; do not obstruct anvil/service access. Earlier flawed-batch confession is later optional lore without a new collectible or reward.

### 15 — Sunwarden Thalorien, keeping the forest legible

**Role/need:** Silverglen witness-grove steward. Wants stable paths and reliable living witness marks through protective isolation; Kaelen argues for careful measurement shared outside. **Flaw/secret:** Removes disruptive signs too readily, mistaking neat continuity for health. **Knows:** local disturbances and grove practice; not which erased story should be restored. **Voice:** seasonal precision and cautious stewardship; avoids universal “purity.”

- G: “The grove changes every day. It is the changes that leave no trace I distrust.”
- P, `silverglen_ley_counsel` active: “Show Kaelen what you observed. Tell him which marks you did not understand.”
- A, that quest completed: “We disagree about the old scars. At least we can still point to the same tree.”

**Work/knowledge:** No tree removal/rotation or grove-regeneration command follows a line about pruning. Combat sprite/anomaly objectives retained; ordinary inhabitants are not “impurities.” Personal observations do not advance another player's quest.

### 16 — Magister Kaelen, the value of an unresolved mark

**Role/need:** Silverglen scholar arguing that contradictions must be preserved long enough to understand. **Flaw/secret:** Can value an illuminating anomaly more than nearby people's immediate safety. **Knows:** comparative witness readings and evidence of disturbance toward Duskwood; not a total chronology of the lattice. **Voice:** clear hypotheses and one counterexample, not jargon-heavy exposition.

- G: “If two records disagree, copying the tidier one does not settle the matter.”
- P, `silverglen_to_highspire` active: “Take our warning south to Highspire. Distinguish what you saw from what I suspect.”
- A, that quest completed: “Ronald has the account. Whether he preserves the disagreement is another question.”

**Work/knowledge:** Reading/measurement at existing scholar station, no new floating VFX field in the service lane. Technical doubts public; a player's unrevealed memory or main quest is never inferred from party membership.

### 17 — Inquisitor Lucian, the protective witness who signed

**Role/need:** Duskwood protector containing the crypt threat. **Flaw/secret:** Helped certify burial records under emergency authority and buried his doubts with the names. **Knows:** local access, Malakar/Vael's public teachings and records he signed; not whether destruction of an anchor can be safely reversed. **Voice:** controlled care, increasingly concrete responsibility instead of “the institution made errors.”

- G: “The living outside these walls come first. The record of how we failed them comes next. Both are my duty.”
- P, `duskwood_crypt_purge` active: “The portal leads beneath the cathedral. Stop the hostile horrors there; a Returned face is not your target by itself.”
- A, that quest completed: “The halls are quieter. I will not write that every name inside them has been answered for.”
- P, existing summit quest active: “The marked switchbacks reach Wyrmcrest. The summit guardian is hostile. Do not mistake the necessity of surviving it for proof that our story was right.”

**Work/knowledge:** No new inquisitorial purity test, forced confession or PvP rule. Preserve `Inquisitor Lucian`, legacy `duskwood_crypt_purge` and `alliance_wyrmcrest_summit`. Burial complicity is later gated disclosure, not automatic reward text for every newcomer.

### 18 — Captain Halford, patrols instead of slogans

**Role/need:** Emberwatch watch captain. Needs safe shifts and proof a patrol returned. **Flaw/secret:** Lets a dispatch read “road secure” too soon to keep relief coming. **Knows:** immediate raider reports and Torvald's readiness; not Abe's full Broken Toll account. **Voice:** plain orders, named people and return times.

- G: “Tell someone which road you're taking. We cannot search every ditch at once.”
- P, `emberwatch_armory_check` active: “Torvald at the smithy. Get the armory report before you leave.”
- A, `emberwatch_raider_cull` completed: “Fewer raiders, not a safe world. You've bought the next cart a chance.”

**Work/knowledge:** Bounded patrol may later be proposed; he must remain reachable for turn-ins. No false “all enemies gone” global claim from a personal3-target objective. Truce travellers receive civil treatment regardless of legacy race key.

### 19 — Warlord Gorgash, the frontier's promised protection

**Role/need:** Bloodrock field commander protecting caravans and local consent. **Flaw/secret:** Calls retaliatory violence protection even when those paying the cost never agreed. **Knows:** frontier attackers, munitions shortage and the road to Gor'Kresh; not all stronghold negotiations. **Voice:** forceful but specific, asks who benefits and evades who pays.

- G: “Protection is a promise. Out here, people expect to see who keeps it.”
- P, `bloodrock_stronghold` active: “Take your account northeast to Mor'gok. Leave out neither our losses nor what we did in return.”
- A, `bloodrock_warlord` completed: “The outcast chief is down. That gives us time to decide whether the next patrol learns anything.”

**Work/knowledge:** No new attack order against the other player faction. Existing hostile encounter targets retained. His admission is public response to that player's objective; not proof a newcomer consented to a clan oath.

### 20 — Blacksmith Garok, obligations that can be repaired

**Role/need:** Gor'Kresh smith with practical responsibility for anchor hardware and field tools. **Flaw/secret:** Treats repairability as an answer to moral misuse; a machine can work while the contract is wrong. **Knows:** forge components/material duty; not whether every signature authorizing them was free. **Voice:** direct shop-floor metaphors, little ceremony.

- G: “Bring me a thing that can be mended. For the rest, bring the person who made the promise.”
- P, target of active `gorkresh_war_forge`: “Inspection first. If Mor'gok wants a guarantee, he can hear its limits too.”
- A, that quest completed: “You've seen the forge. Now ask what the finished work is being asked to carry.”

**Work/knowledge:** Existing forge position and talk credit unchanged. No automatic new recipe, crafting profession or material delivery quest. Technical talk may be shared; the player's quest reward remains personal/server-authorized.

### 21 — Archmage Karen, the archive's honest calculation

**Role/need:** Highspire arcanist balancing anchor stability and public safety. **Flaw/secret:** Her emergency models assign costs to people whose consent is not represented by the model. **Knows:** current loads, risk estimates and council reports; not the true content of every erased testimony. **Voice:** accessible explanation followed by uncertainty, no borrowed magical-order names.

- G: “A calculation can warn us. It cannot volunteer someone else to pay.”
- P, target of active `highspire_royal_council`: “I can show the strain. The council must answer for the remedy.”
- A, that quest completed: “You have my report. Keep its margin of doubt with the rest.”

**Work/knowledge:** Table/reading routine only as later world proposal. No new anchor-repair ability or perfect memory-retrieval service. Confidential council figures are not emitted as ambient barks to all passers-by.

### 22 — High Inquisitor Malakar, the Perfect Witness

**Role/need:** Existing crypt antagonist. Wants one compulsory Perfect Witness to end contradictions by eliminating the right to disagree. **Flaw/secret:** His promised certainty requires destroying the freely offered testimony that makes the Lattice legitimate. **Knows:** coercive witness practice and his own forged records; cannot genuinely know all unrealized histories. **Voice:** serene imperatives; offers safety while removing choice.

- G/encounter-start proposal: “You have suffered enough uncertainty. Kneel, and your account will be made correct.”
- P/active-cast bark proposal: “One record. One witness. No more doubt.”
- A, proposed Lucian aftermath after existing authoritative boss defeat: “He called obedience a cure. Do not let our account call it consent.”

**Work/knowledge:** Existing enemy `boss-malakar`, phase/death guards and rewards preserved. No conversation requiring surrender, forced character-memory deletion, renamed spell/ability IDs or new nonlethal ending. Encounter barks are shared public events; confidential player history must not be improvised into them.

### 23 — Dread Abbot Vael, guardian of the false memorial

**Role/need:** Existing second crypt antagonist defending memorial records he knows are falsified. **Flaw/secret:** Prefers a consoling official account to relatives' incompatible testimony, calling dissent cruelty. **Knows:** altered local memorials and the practices he defends; not everyone's private grief. **Voice:** intimate, solicitous language that becomes possessive.

- G/activation proposal: “Those names have rested long enough. Why would you make their families lose them twice?”
- P/phase-active bark proposal: “The stone is settled. It is you who will not let them rest.”
- A, proposed Lucian aftermath after `boss-abbot-vael` defeat: “A memorial can carry a lie as carefully as a truth. We still owe the names a hearing.”

**Work/knowledge:** No duplicate phase activation or cast after death for narrative timing. Existing defeat/loot remains. Do not convert the memorial into an implemented read/collect quest or declare all crypt horrors free citizens; those are separate authored distinctions.

### 24 — Vermithrax, the last summit witness

**Role/need:** Living guardian of Wyrmcrest's great anchor. Needs stolen witness material returned and the summit's continuity protected. **Flaw/secret:** Treats approaching mortals as another theft risk and lashes out indiscriminately after Choir intrusion. **Knows:** summit history and the theft it directly witnessed; not each traveller's faction guilt. **Voice:** sensory, ancient but comprehensible; refuses ceremonial hero flattery.

- G/aggro proposal: “Another hand on the stair. Another promise that it comes empty.”
- P/enraged bark proposal: “You take the witness and leave the wound.”
- A, proposed Elara/Lucian aftermath after existing `vermithrax-boss` defeat: “The guardian is dead. We have ended a danger, not proved that every step to it was just.”

**Work/knowledge:** Retain existing living model/boss, hostile encounter, summit switchback access and defeat objectives/rewards. No “only wings can reach it,” dragon-riding unlock, cleanse/spare option or ending that requires a new outcome flag. The theft is lore; no existing stolen-witness item is asserted.

### 25 — Gorgonax, pressure without a language

**Role/need:** Damaged quarry/pressure guardian in the crater, not a tenth great anchor. Its old work was to channel dangerous load safely. **Flaw/secret:** Damage and forged maintenance instructions now turn pressure relief into destructive cycles. **Knows:** no reliable human-scale political narrative; it responds to old directives and physical strain. **Voice:** fractured mechanical duty phrases; not an omniscient villain speech.

- G/activation proposal: “LOAD UNWITNESSED. RELIEF REQUIRED.”
- P/active-phase bark proposal: “KEEP THE CHANNEL OPEN.”
- A, proposed Fizzlebang aftermath after existing `worldboss-gorgonax` defeat: “It kept doing the task after the people responsible stopped checking what the task meant.”

**Work/knowledge:** Existing world boss mechanics/death/rewards retained; no automatic repaired-friendly state, mining buff or new pressure puzzle. Shared telegraphs may communicate immediate danger; a first-time player does not gain workshop secrets merely from entering the crater.

## 5. Proposed dialogue schema, state precedence and knowledge scopes

This is a design contract for a small resolver over existing fields, **not a new runtime framework or committed asset schema**. Implement the first slice before deciding whether data needs ScriptableObjects or existing plain definitions. A dialogue display must never advance rewards or defeat flags as a side effect.

```yaml
dialogue_entry:             # proposed content record, not an installed schema
  content_key: abe.main0.offer       # prose identifier only; not a new quest/save ID
  npc_identity: Abe                 # exact existing key
  channel: quest_body              # quest_body / gossip / bark / encounter / aftermath
  scope: interacting_player        # public / interacting_player / host_encounter
  conditions: existing predicates only, explicitly listed in implementation ticket
  priority: 70
  text: authored line
  effects: none                    # rewards and actions keep existing validated paths
  fallback: current safe generic greeting
  provenance: dossier + actual source fields + reviewed canon version
```

Conceptual knowledge tiers: **public**(roads/truce/current visible danger), **personal progress**(this player's accepted/completed objectives), **private testimony**(explicitly witnessed optional conversation), **encounter public**(a current shared cast/defeat). There is currently no general `HasHeardMara`, `MetSelvekBefore`, `WitnessTrust`, `PerfectWitnessChoice` or party lore database. Do not call these implemented flags. If deeper disclosures need persistence, propose narrow stable keys, save migration and host/local scope first, then obtain their implementation scope.

| Priority | Candidate condition | Safe behavior |
| --- | --- | --- |
| 100 | Dead player, hostile encounter, out of range, incompatible modal or invalid actor | Keep existing interaction denial/hostile behavior. No lore panel through combat, no stuck cursor or new resurrection route. |
| 90 | Exact active quest ready at this NPC / target, using existing eligibility | Ready/turn-in text takes precedence; existing authorized completion path alone pays rewards. |
| 80 | Exact active talk objective targets this NPC | Show current target-specific response, not a broad faction-history paragraph. Talking uses current progress logic exactly once. |
| 70 | Exact active objective belongs to this giver and is not ready | Show actual progress and relevant direction; do not expose later reveals. |
| 60 | Existing accepted/completed personal state permits an authored aftermath | Choose the latest relevant response without resurrecting/resetting actors or announcing another player's victory. |
| 50 | Existing available quest, actual faction/prerequisite rules met | Offer existing quest text. Narrative faction alias does not change `Alliance`/`Horde` eligibility. |
| 40 | Explicitly selected public lore topic | Short, optional answer tied to the dossier; no copied world names or unsolicited spoiler dump. |
| 30 | Directions requested | Resolve actual current actor/region route; fail usefully if missing. Never silently pin an unrelated old coordinate. |
| 10 | No higher condition / unknown content | Stable safe greeting, service options and Back/Close. Display has no progression effects. |

Tie-break: exact NPC+exact quest beats faction/role match; explicit selected topic beats unsolicited bark; deterministic authored order resolves equal entries. No `name.Contains("Ronald") || role.Contains("Highspire")` shortcut that turns every Highspire worker into Ronald. Keep service actions available where eligible, and preserve revive priority over nearby gossip. Tooltip/objective/body/nameplate must agree on the same existing actor and quest.

Co-op: host-authoritative actions remain host-authoritative. Each client reads their own eligible quest state; a host's completion, optional testimony or faction identity must not replace a client's. Shared encounter barks may be replicated once; they do not grant private journal entries. Late join receives current encounter/world state without replaying every private confession or paying old rewards. An optional future shared story topic requires explicit acceptance/knowledge policy; Roadward Truce is not blanket lore synchronization.

Ambient constraints for every dossier: no new procedural patrols in the first text slice; no movement while an interaction is active; bounded work loops return to the authored anchor; no collisions added to roads/service/mount aprons. Barks need cooldown/distance/priority and silence during dialogue/casts, not an unbounded OnGUI spam path. Existing light/camera/animation/performance conditions remain meaningful. Routine animation and props require a separate world/asset change contract.

## 6. First playable story slice — five-character contract

The first implementation should make the existing adventure feel connected without adding quest IDs, rewards, compulsory cutscenes or another menu. **Scope approval here is an authoring proposal only; no implementation has been performed.**

| Slice | Existing trigger/state | Content and knowledge | Explicit non-change |
| --- | --- | --- | --- |
| NS01 Abe's road | Main `QuestIndex`0–2, accepted/ready/complete; separate `alliance_highspire_dispatch` kept distinct | Replace actual `PlayerProgress.QuestDescription` branches with short practical road/supply/captain lines. Post-main completion alludes to a held wagon. An optional public topic may explain the Broken Toll without a new reward | No new Mara-investigation gate, kill count, main-index change, or requirement to finish the main chain before an already eligible faction dispatch |
| NS02 Rowan's shortages | Actual nearby merchant/trade success or failure | Establish mismatch between shelves and ledger in greeting; use success line only after authorized purchase. Direct to Abe with existing actor resolution | No debt mechanic, prices/caps changed, mandatory purchase or extra supplies granted |
| NS03 Elara's survey | `frontier_cartography`;3 existing SurveyIds; readiness/completion | Contrast observed marks with copied manifest. Text guides through the existing three sites and returns to actual Elara | No extra survey marker, automatic discovery, fourth proof or modified reward |
| NS04 Mira's worker trust | Existing Greenhollow service/context | Explain why road crews depend on reliable material promises; separate Elara's surveying from Mira's provisioning | No trust currency, food buff, rest ability, unpaid-account save flag or automatically shared purchase |
| NS05 Selvek's toll | Existing Reaver bounty eligibility, captain hostility/defeat, lockbox | Humanize Returned ferrymaster through one optional encounter bark and Elara aftermath; mention the refused record only when appropriate | No spare/recruit outcome, camp recreation, faction hostility rewrite, new testimony item or changed defeat/lockbox flag |

Revelation order: present a physical problem(first road/supplies)→show a mundane record discrepancy(Rowan)→teach independent observation(Elara)→show the human cost(Mira/Returned)→make the toll conflict morally legible(Selvek). The player need not read optional lore to find the objective. Abe's full responsibility, Mara's name/relationship and the exact19-person account should be optional authored revelations tied to an approved future knowledge contract; do not dump all three in the first greeting.

Suggested future optional topic text, **not a current implemented state**:

- Abe, “What happened at the Broken Toll?”: “I was told to hold the seventh wagon. My sister Mara was aboard. By the time I refused the order, there was nobody left to hear the refusal.”
- Abe, “Selvek says he came back.”: “He did. We worked that crossing together. I knew his voice before I let a clerk tell me what the ledger allowed.”
- Elara, after relevant authorized testimony is actually known: “A missing person and a false entry are different facts. We will not repair the second by pretending the first never hurt.”

These lines must not require the player to have killed Selvek if their topic prerequisites imply meeting him alive. For now no friendly meeting exists; do not write “as you promised him” or “you spared him.” Mara does not appear as a new quest actor or secret player-relative.

## 7. Concrete prose and direction corrections

Current text contains borrowed terminology and stale coordinate claims. This list is an implementation task list, not a code change. Preserve all programmatic names/quest/item IDs until an explicit compatibility migration exists.

| Current source / problem | Proposed replacement / rule | Compatibility and validation |
| --- | --- | --- |
| `NPCGossipWindow.GetNPCLore`: Ronald mentions Aethelgard, Kirin Tor, three-hundred-year history and Alliance lion ideology | “Highspire's archive is one of the Nine Hearths. The Compact owes its people shelter, honest accounts and roads that return them home. Those duties are older than the current council.” | Use Veyr/First Binding412-year canon only where relevant; do not replace `Alliance` enum/filter keys or NPC identity strings |
| Same function, Mor'gok broad role branch says Forsaken and frames every Pact citizen as a war stereotype | “Gor'Kresh is bound by negotiated obligations among clans, Returned and free workshops. A promise freely made protects us; a debt named without consent can break us.” | `Undead` remains visual/save key; Returned is narrative people-name. Exact Mor'gok entry precedes general capital fallback |
| Same function, Morzan/Alyssa generic Forsaken/Light/Shadow binary | Use their distinct custodian/security dossiers; ordinary Returned are not monster categories | Do not change hostile encounter targeting or race eligibility during prose replacement |
| Same function, Griznak/Fizzlebang Bilgewater Cartel and reckless-profit caricature | Rustwater freely signed repair/delivery contracts; contractor/engineer disagreement over acceptance versus safe operation | Keep `Boss Griznak`, `Tinker Fizzlebang`, quest keys and existing commerce paths |
| `FactionQuestSystem.silverglen_attunement`: “eternal light of Silvermoon”; “Moonwell” lore in other text | Silverglen's living witness-grove and maintained witness pool, with Thalorien/Kaelen's disagreement | Keep `silverglen_attunement`, target keys/counts/rewards; no assertion the encounter became a new ritual mechanic |
| `rustwater_to_gorkresh`: title “Bilgewater Contract: Gor'Kresh” and “Horde Warlord” prose | Proposed display title “The Rustwater Delivery”; freely agreed manifest carried through Bloodrock to Mor'gok | Do not rename `rustwater_to_gorkresh` or talk target |
| `deathknell_to_gorkresh`: “Warchief binds us” | Alyssa's witnessed account to the Pact council/Overlord; consent, not racial servitude | Existing next/prerequisite/reward/faction keys unchanged |
| Generic Scarlet Crusade religion/enemy provenance in gossip/encounter journal/boss barks | Duskwood's memorial institution, falsified burial records, Malakar's Perfect Witness doctrine and Vael's protection of false consolation | Legacy Scarlet-labelled IDs/items/interior/encounter display names require a separate alias audit; no broad search/replace |
| `GetNPCThreats` crypt coordinate(-55,215) | “The portal is on the Duskwood cathedral grounds.” Expanded destination resolves `WorldRegions.Cathedral` near(-220,260); underground interior remains(-400,-40,200) | Exterior entry, interior encounter and safe return are distinct destinations; use actual portal transform when available |
| `GetNPCThreats` Bloodrock(55,215), summit(0,48,225), “without wings” access claim | Bloodrock current centre(280,210), Wyrmcrest(250,510); “The marked mountain switchbacks lead to the summit. Prepare before you climb.” | Centre is orientation context, not arbitrary teleport/interaction coordinate. Existing mounted/foot trail remains advertised; no flight requirement invented |
| Gossip direction-button fallback flight(2,10)/mailbox(8.5,6.5) if no nearby service found | Prefer the actual nearby service transform; otherwise give an unavailable/no-local-service response or a correctly named known destination | Do not silently pin an Emberwatch fallback while talking in Rustwater/Greenhollow. Existing portal/summit buttons already branch through WorldRegions; retain correct behavior |
| `highspire_duskwood_recon` prose directs across the Willowmere bridge | Direct north toward Deathknell/Duskwood on the actual authored route; optional Westbank/Reaver excursion is separate | Westbank crossing near(-255,50) is not required to reach cathedral(-220,260); verify current route meshes/map instead of inventing another bridge |
| Static broad territory lore and old “Amber Vale” continent claims | “The March of Nine Hearths in Veyr”; local road/settlement terminology from the lore bible | Do not rename scene groups/map keys/save migration coordinates as part of text work |

Threat directions should teach a safe return route and relevant preparation, not raw spoiler coordinates in every greeting. When a coordinate is displayed for diagnostics it must identify expanded versus legacy layout. Missing route evidence is marked unresolved; lore cannot certify an untested path.

## 8. Safe implementation packets and acceptance

| Packet / owner | Concrete work | Done evidence / boundary |
| --- | --- | --- |
| NPC00 / Narrative design | Review these25 dossiers against lore bible, source key registry and region layout; approve the five-character slice | No unresolved name/state contradiction; authored lines tagged public/personal/private/encounter and implemented/proposed distinction retained |
| NPC01 / Runtime development | Wire first-slice text to the actual existing main/faction/service response paths; keep exact actor/quest keys | Compile; Abe0/1/2 offer/progress/ready/complete and separate dispatch tested; Elara survey/bounty prerequisite/turn-in/dedup intact; no effect from merely viewing lore |
| NPC02 / Runtime development | Replace unsafe broad lore routing with exact-key+condition precedence for first-slice actors only | Rowan does not speak as Ronald; unknown NPC gets safe fallback; close/back/out-of-range/death/service behavior retained; no new framework unless current definitions cannot support the narrow scope |
| NPC03 / Content/runtime contract | Correct copied proper nouns and misleading directions in the approved text list | Review changed strings and existing keys separately; map/quest target coordinates still agree with authored routes; no whole-file replacement or global rename |
| NPC04 / QA integration | Test player-specific knowledge and current online authority | Two clients at different main/faction stages get correct lines; one turn-in pays once; host progress does not overwrite client lines; late join gets present world state without private confession replay |
| NPC05 / World design, coordinated review | Preview modest work/record props and bounded routines for the existing NPCs | Separate scene/prefab approval, exact assets/transforms/provenance/support/clearances and native before/after. No NPC root moved, duplicate actor created or service path blocked |
| NPC06 / Design+gameplay, later | Propose explicit optional testimony/knowledge storage only if first slice proves useful | Define stable new lore keys, source/recipient/host/local scope, save migration and reset/spoiler rules before coding. No real save/identity erasure or new ending flags smuggled into dialogue |

Required test matrix: clean/new character; each existing Abe stage unaccepted/active/ready; fully completed main chain; separate faction dispatch eligibility; Elara unavailable survey/active/ready/completed and bounty unavailable/active/ready/completed; repeated turn-in; service success/rejection; out-of-range/closing/reopening; mixed-stage host/client and late join. Preserve existing145 quest and32 independent spawn regressions, relevant gameplay/Relay authority checks and source/build identities. New narrative predicates need meaningful behavior tests, not tests that merely compare authored string constants.

No tests or native screenshots were run for this authoring document, and no story dialogue is advertised as implemented. Current main/faction state remains the source of gameplay truth; deeper lore is a bounded content proposal until wired, verified and delivered in a new build.
