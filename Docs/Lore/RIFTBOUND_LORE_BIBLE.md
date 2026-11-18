# Riftbound — the March of Nine Hearths

Creative canon v1,16–17 November 2026. Written for the existing Riftbound map, characters and compact cooperative RPG. This is authored story/content, not a claim that dialogue, schedules or new quests are installed. NPC implementation contracts live in NPC_STORY_CONTRACTS.md; combat design lives in ../Design/COMBAT_REFINEMENT_PLAN.md.

## 1. The promise at the centre

**A promise can shelter someone—or own them.**

The frontier of Veyr is held together by agreements made visible: boundary stones, ferry tallies, marriage ribbons, burial lists, trading weights and the bells that announce a road is safe. Beneath that ordinary life sits the Witness Lattice, nine ancient anchors that prevent incompatible possibilities from occupying the same ground.

The danger begins with a practical contradiction. A cartographer measures the same crossing twice and gets two histories. A quartermaster receives three signed deliveries but only two wagons. A Returned ferryman can identify every passenger he carried, yet the official record insists he never existed. The frontier needs its roads reopened before it can understand why its records are lying.

Players become roadward adventurers: people willing to carry testimony across borders and take responsibility for what they find. They are useful before they are powerful. The first promise is to get a village's supplies through. The eventual question is who has the right to decide whose memories keep the world stable.

Tone: warm inhabited refuges, dangerous travel, political friction and discoveries earned through ordinary work. Bread ovens, repaired harnesses and a bench beside a difficult crossing matter because someone uses them. Humour comes from people and trades, not every goblin being an explosion joke or every elder reciting a prophecy.

## 2. What the Rift is—and what it cannot excuse

Veyr's physical world rests above the **Undertide**, the pressure of things that could have happened but did not. It is not an evil kingdom and has no universal dark ruler. A healthy boundary lets possibility remain possibility. A damaged boundary lets an alternative event leave an echo: a second bell stroke, a false door outline, an absent person's handwriting, a reflection carrying yesterday's injury.

The Lattice stabilizes places through witnessed continuity. Stone supplies a persistent reference, worked metal carries resonance, and freely given testimony binds one account to another. A witness does not have to be literate. A carved tally, sworn song or remembered route can be as valid as an archive page.

Four rules constrain magic and the plot:

1. **A witness cannot know what they did not experience.** Echoes are partial impressions, not omniscient surveillance or convenient evidence for every mystery.
2. **Consent creates a stable binding.** Coerced testimony can power an anchor briefly, but contradictions accumulate and the debt eventually returns somewhere else.
3. **Resonance moves a cost; it does not abolish one.** Large workings consume prepared materials and, when abused, fragments of lived recollection. Personal names are not magical passwords; it is the relationships and testimony attached to them that matter.
4. **An echo is evidence to interpret, not permission to rewrite any fact.** Canon changes need a documented cause. Broken quest flags, obsolete directions and disappearing saves are implementation defects, not manifestations of the Rift.

Everyday spellcasting uses small, recoverable resonance held in trained bodies, focuses and vows. Warriors cultivate control under pressure; rogues exploit attention and openings; mages shape measured resonance; paladins anchor protection in explicit commitments. These are disciplines, not moral alignments or faction-exclusive religions.

Health is an abstraction. Limited healing and emergency waystone recall do not mean anyone can resurrect any dead civilian. A prepared adventurer can sometimes be recalled from a failed expedition; the dead of an old disaster are a different matter. Existing respawn/reset rules remain gameplay rules until their narrative treatment is implemented. No memory cost may secretly delete player progress, equipment, names or saves.

## 3. History with consequences

Dates are relative to the present campaign; do not introduce a competing absolute calendar in individual NPC lines.

| When | Event | What remains in the present |
| --- | --- | --- |
|412 years ago | **The First Binding.** Nine communities stop an overlap disaster by witnessing one another's boundaries and building the anchors. | Rival peoples inherit parts of one shared engineering achievement; no faction owns the world's creation. |
|103 years ago | **The Tithe of Names.** Emergency anchor maintenance becomes an institution. Officials begin treating unclaimed testimony and burial records as a resource. | Missing names, duplicate seals and debt inherited by people who never consented. The full practice is not public knowledge. |
|61 years ago | **The Ashfall Winter.** Quarries and pressure works keep the region alive during a failed harvest. Reused anchor bronze enters ordinary industry. | Rustwater's expertise is indispensable; its machinery and contracts also conceal dangerous shortcuts. |
|18 years ago | **The Broken Toll.** Conflicting evacuation orders converge at the western crossing. The Seventh Wagon is held while a priority convoy passes; nineteen people are declared lost. | Abe's guilt, Selvek's contested identity, damaged trust in Highspire's records and the emergence of many Returned. |
|The following years | **The Roadward Settlements.** Refugees rebuild routes, memorials and mixed communities while both political blocs claim responsibility for keeping them safe. | Current villages are settlements with work and families, not empty military quest depots. |
|Three weeks ago | **The Hollow Dusk.** Several anchors sound a second note. Manifests acquire impossible matches and attacks concentrate on particular cargo. | The present shortage/raider crisis. Locals recognize symptoms without knowing the cause. |
|Now | **The Roadward Truce.** Compact and Pact permit mixed adventuring parties to protect public routes and investigate anchor failures. | Friendly cross-faction co-op makes sense without pretending the political conflict has vanished. |

The Seventh Wagon is important because it reveals the system at human scale. It is not secretly the origin of every character or every catastrophe. Other communities have independent histories and legitimate needs.

## 4. Two factions with reasons to exist

### The Hearthward Compact

Highspire's confederation promises that a road, a home and a person's legal identity will survive the death of a local ruler. Its strengths are coordinated relief, public works, shared measures and enforceable protection. Its institutional flaw is mistaking an official record for the person it should serve.

Grand Duke Ronald inherited an archive that paid for stability by excluding inconvenient testimony. He is not secretly delighted by suffering. He fears that publicly admitting how the anchors were maintained will trigger panic, invalidate land grants and undo the relief system that still feeds people. His cover-up protects some households while continuing the harm to others.

Compact disagreements are visible: Halford wants reliable patrols; Torvald wants workers paid before another levy; Elara wants evidence released; Lucian believes secrecy sometimes prevents a larger catastrophe. Their shared crest does not give them a shared conscience.

### The Ashen Pact

Gor'Kresh's confederation promises that no crown may erase a community because its account is inconvenient. Clans, Returned households and contract leagues bind themselves through witnesses they choose. Its strengths are mutual defence, portable identity and recognition of people excluded by old charters. Its flaw is allowing local obligations and retaliation to become coercion under another name.

Overlord Mor'gok remembers what official protection costs those who are never counted as citizens. He will cooperate over public routes, but will not surrender the Namewell's custodians or clan testimony to Highspire. He tolerates hard commanders whose reprisals endanger the very civilians he claims to protect. Gorgash sees every missing shipment as preparation for a western offensive; Alyssa sees proof that another kind of authority can exploit the dead as easily as a duke.

Pact membership does not make every crag raider an ally. Selvek's extortion harms Pact traders too. Griznak cannot make an unsafe contract just by declaring it free. Internal accountability is a playable subject, not a footnote.

### Compatibility and play policy

These are narrative/display names for original faction identities. Existing serialized `Alliance`/`Horde`, `Any`, race enums, quest faction filters and save keys remain unchanged until a reviewed presentation mapping exists. Do not search/replace code identifiers or rename NPC lookup keys.

Humans, dwarves, elves, orcs, goblins and Returned all exist in Veyr whether or not each is in the final playable roster. Two races per launch faction remains a supported-model/product decision. Origin influences perspective; it does not prescribe morality. The Roadward Truce permits friendly mixed parties. This canon does not add PvP, forced reputation grinding or restrictions on current saved characters.

## 5. The nine anchors and the existing map

An anchor is a historical function, not a promise that nine new glowing objects already exist. Use existing places and restrained approved set dressing. The compact game compresses distance; never claim a planned fifth terrain tile is present.

| Place / anchor | Function and local life | Present fracture / useful story |
| --- | --- | --- |
| **Highspire / Crown Bell** | Coordinates road charters, granaries and public testimony. The plaza well is where notices are heard and disputed. | A sealed record does not match what relief workers remember. Court security is needed; obedience alone is not a solution. |
| **Frostpeak / Deep Anvil** | Maintains anchor alloys and honest measures. Smith families teach apprentices through repaired tools. | Old bronze was requisitioned without replacement. A forge quest should concern material duty and workers, not only a sword upgrade. |
| **Silverglen / Silver Root** | Living roots preserve seasonal continuity; keepers witness change instead of freezing the forest forever. | Thalorien wants protective isolation; Kaelen wants careful measurement shared beyond the grove. Purification can heal or erase. |
| **Deathknell / Namewell** | Custodians preserve Returned testimony and consent. Mourning and new lives coexist. | Some memories belong to multiple voices. Alyssa refuses to sell certainty she cannot honestly provide. |
| **Rustwater / Pressure Wheel** | Converts pressure into useful work; mechanics keep water and trade moving. | Deferred maintenance and a forged allocation contract make an industrial fault look like sabotage. Fixing machinery has civic stakes. |
| **Gor'Kresh / Ash Hearth** | Holds chosen obligations and public restitution between clans. | Private vengeance is being presented as defence of the Pact. Mor'gok must decide what solidarity actually demands. |
| **Duskwood / Witness Vault** | Stores burial testimony and records of unresolved loss. Cathedral and crypts were places of care before militarization. | Malakar and Vael turn incomplete memorials into compulsory evidence for the Hollow Choir. |
| **Ancient Grove / Living Witness** | Holds continuity that cannot be carried on paper: routes, migration and relationships between settlements. Greenhollow is its working supply/riding post. | Elara's surveys reveal a distributed pattern. The answer needs multiple sites, not one magical map table. |
| **Wyrmcrest / Summit Crown** | Releases excess resonance safely through the mountain; Vermithrax is its long-lived guardian, not its owner. | Stolen anchor components make the guardian treat travellers as another extraction party. The summit is reachable by the existing switchback route. |

**Emberwatch** is a relay and roadward station, not a tenth great anchor. Its significance comes from keeping promises between places. Abe's tasks begin with a safe road, a paid delivery and a witnessed return.

**Westbank and Willow Rest** are where different accounts physically meet. A supported cache, repaired crossing and practical resting place should communicate relief work. The cache remains `willow-rest-cache`; scenery must never conceal access or add a duplicate reward.

**Camp Bloodrock** protects the eastern approach and concentrates Pact anxiety about missing cargo. **Redridge** is a working pass, not an endless combat corridor. **Gorgonax's crater** is an old pressure/quarry failure whose guardian cannot distinguish extraction crews from rescuers.

**Stormglass Reach** is future geography. Its proposed beacon/quarry dispute should grow from the pressure system once the present campaign works. Nothing in an NPC's current directions should send a player into that unbuilt region.

## 6. People who carry the first mystery

Detailed voices, dialogue states and implementation keys are in NPC_STORY_CONTRACTS.md. The following facts govern their relationship; do not accidentally give everyone the same secret.

- **Abe:** retired roadwarden, practical, protective, ashamed of following the order that held the Seventh Wagon. His sister Mara was among the nineteen lost. He can recognize a familiar seal and a bad patrol plan; he cannot explain the Lattice's ultimate origin or name the Choir at the opening.
- **Captain Selvek:** a Returned former ferrymaster, visually the existing Undead/Rogue. He was among those declared lost. The official ledger's refusal to recognize his return left wages, testimony and restitution inaccessible. He now takes tolls by force. His grievance is real and his violence is a choice. Existing captain-defeat/lockbox outcomes remain the implemented contract; a future surrender branch needs its own rules.
- **Cartographer Elara:** treats a measurement as a promise someone else must be able to check. Her surveys connect the stolen cargo to anchor maintenance. She has professional courage, not universal supernatural insight. She asks witnesses before assigning blame.
- **Quartermaster Rowan:** worries about bread, blankets and whether a signature actually means a wagon arrived. His willingness to expose discrepancies costs him standing with his superiors. He does not secretly run every caravan in the world.
- **Provisioner Mira:** earns trust through fair supplies and remembered debts. She knows which settlements are buying unusual quantities; she will not disclose a frightened customer's identity as gossip. Her service remains useful after a story beat ends.
- **Inquisitor Lucian:** genuinely stopped an earlier outbreak and believes withheld information can save lives. He recognizes that Malakar's methods are wrong while struggling to admit how his own institution supplied them.

No ancestry is a diagnosis of corruption. The Returned are people with varied loyalties, work and hopes. Enemy hostility comes from affiliation, action and encounter state, not the presence of an Undead model.

## 7. Adversaries with readable intentions

The **Hollow Choir** promises an end to conflict by making one compulsory account of the world. Its members do not all cackle about destruction. Some lost families to contradictory orders; some profit from unquestionable records; some are frightened caretakers who have confused stillness with peace. Their system removes the very consent that makes a binding stable.

**Malakar** seeks the Perfect Witness: a ritual archive that cannot be contradicted because dissenting testimony is consumed. His crimes are deliberate. **Abbot Vael** protects a memorial institution built on falsified lists; he calls uncertainty disrespect for the dead. Their relationship explains different encounter intentions—coercive channels and defensive preservation—without contradicting existing phase/death guards or current boss outcomes.

**Vermithrax** remembers communities that no longer appear on maps. The theft at the Summit Crown has made him treat every armed ascent as another attempt to strip the mountain. Players can learn why he attacks without the current defeat fight becoming secretly nonlethal. A future parley/repair ending requires an explicit new outcome contract; current loot/victory IDs remain intact.

**Gorgonax** is an overdriven quarry/pressure guardian. Its vents and dangerous sweeps should be readable consequences of stored pressure. This does not authorize destructive terrain deformation or new combat mechanics merely because the story mentions them.

Enemy combat language should reveal intention: raiders rush and withdraw for cargo; Reaver tollwardens brace a crossing and punish a careless frontal approach; bellhexers sustain disruptive channels; Choir acolytes protect ritual lines; machinery vents before discharge. A visual shape, animation and sound should communicate danger together. Lore is not an excuse for invisible attacks or unreadable damage.

The local name **Scarlet Crypts** comes from the red lacquer once used to seal burial testimony. It does not imply another game's church, crusade or cosmology. Existing location and encounter identifiers stay compatible while their original institutional history is established here.

## 8. Reveal the story at the scale of play

| Layer | What the player can establish | Existing/proposed delivery boundary |
| --- | --- | --- |
| **A safe road** | Supplies are missing, attacks are organized and a familiar seal appears twice. | Rewrite current Abe/merchant copy around actual existing objectives; no new kill quotas/rewards required. |
| **Three accounts** | Westbank, Bloodrock and Wyrmcrest lookout observations contradict one official manifest. | Existing `frontier_cartography` markers remain its actual target set. Story explains why comparison matters; do not silently substitute Ancient Grove or move markers. |
| **The ferryman's name** | Selvek's exclusion and the Seventh Wagon connect present coercion with an old institution. | Existing `river_reaver_bounty`, `westbank-reaver-captain`, `river-reaver-lockbox` remain valid. Optional future evidence/readable is a new proposed slice. |
| **The disputed archive** | Both factions possess useful evidence and protect different interests; neither can solve the crisis alone. | Expand current faction introductions/Highspire dispatch carefully after exact quest graph audit. |
| **The compulsory witness** | Duskwood's ritual is using testimony without consent. | Existing cathedral/crypt encounters first need coherent dialogue, telegraphs and correct directions; new branching outcomes deferred. |
| **Who keeps the promise?** | Repairing the Summit Crown requires a political decision about future testimony and maintenance. | Future climax: consent-based public oversight versus distributed local custody, each with costs. No final ending/global phasing is implemented by writing this bible. |

The first20–30-minute slice resolves a local promise and opens a larger question. It must not require a player to read the whole history before accepting Abe's task. Use optional questions, a short evidence summary and environmental details. Do not replace play with unskippable exposition.

Co-op facts have separate scopes. A player may know what their personal quest record establishes; a party may share an accepted event; an NPC may know a public local fact. One advanced guest cannot automatically overwrite a new player's journal or force a campaign ending. Online remains session-only until a separate persistence design is delivered; this fiction does not claim otherwise.

## 9. Evidence objects ready for later staging

These are authored content concepts, not spawned loot or approved placement edits. Use existing licensed props only after review; provide optional readable text without forcing collect-and-return filler.

| Object | Short authored text | What it establishes / does not establish |
| --- | --- | --- |
| Seventh Wagon tally | “Nineteen names. One wagon. Hold until the priority seal passes.” | A decision and count; not who ultimately forged the order. |
| Rowan's receipt | “Received three. Unloaded two. Do not balance this column for me.” | Present discrepancy and Rowan's voice; not proof he knows the Choir. |
| Mira's harness tag | “Repaired twice. Owner paid once. Return it when the road is safe.” | Human stakes and a standing relationship; no hidden purchase obligation. |
| Elara's map margin | “A route is not true because I drew it. Ask the person who walked it.” | Her method and why independent survey matters. |
| Namewell token | “Returned does not mean restored. Ask what I consent to remember.” | Agency of the Returned; no racial curse mechanic. |
| Rustwater maintenance notice | “The wheel is not broken. Its safety work was sold as spare capacity.” | A concrete industrial conflict; no blanket condemnation of goblins. |
| Unsealed memorial page | “Leave this line open. We do not yet know.” | Honest uncertainty as a positive practice; not an omniscient plot reveal. |

## 10. Writing and implementation rules

1. Preserve current stable quest/enemy/chest/race/faction IDs, actor references, rewards, saves and normal service access. New display terminology is not a data migration.
2. Replace copied universe-specific organizations and generic race stereotypes with the original institutions above. Do not mechanically replace words inside keys, asset paths or code identifiers.
3. Give every major NPC a job, a desire, a limit, an opinion and something they do not know. Lines must react to actual state, not invented flags silently treated as true.
4. Mark dialogue as confirmed fact, official claim, local belief or deliberate lie. A character's suspicion is not global canon. Secrets need a reveal condition and evidence provenance.
5. Keep greetings short, optional lore deeper and quest instructions operationally accurate. Directions come from live location contracts, not hard-coded obsolete coordinates. Never excuse a wrong waypoint as Rift fiction.
6. Service NPCs remain reachable while working. Pause ambient tasks for interaction, give precedence to dialogue/combat safety, and resume predictably. Do not add a24-hour simulation framework for three convincing routines.
7. Public movement/availability and shared events require host authority. Local cosmetic idle gestures and personal dialogue do not grant rewards or mutate another player's progression. All new memory flags need explicit persistence scope.
8. A new boss story must respect the shipped encounter outcome. Parley, arrest, spared enemies or faction consequences are future mechanics until their server/save/quest contracts exist.
9. Art direction follows material causes: archive seals, repaired timber, honest measures, weathered witnesses and practical work. Do not cover the map in glowing Rift particles or identical crates.
10. Player-visible text cannot promise content, durable online persistence, map regions or choices that are not present. Lore depth should increase coherence, not multiply unfinished systems.

## 11. Reference slice and implementation boundaries

Narrative design covers canon, NPC voice and state-aware content, regional clues and content validation. Runtime development covers dialogue/state adapters, combat, enemy behavior, networking and regression checks. Scene, prefab and terrain changes require a reviewed scope and one editor at a time. FactionQuestSystem.cs combines definitions and runtime behavior; supply narrative changes through the shared content contract rather than editing quest logic concurrently.

First reference slice: Abe → Rowan/Mira → Elara's existing survey → Selvek's existing bounty. Preserve the initial tutorial and all quest eligibility/reward behavior. Implement a small state-aware dialogue catalog for those five identities, then one readable raider/guard/hexer encounter using existing assets. Do not start new regions/classes or rewrite all NPC systems before this slice works offline and in co-op.
