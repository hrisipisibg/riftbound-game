# Riftbound combat refinement — source-based design and execution plan

Design baseline,16–17 November2026. No runtime, scene, asset, test or build changes were made while authoring this baseline. Inspected source at integration checkout0670215; shipped-runtime reference remains Combined02 source76696a7bff9e86f40a5c7fb90747fac5a290be8b. Build and test results remain tied to their recorded source and artifact identities. Canon authority: Docs/Lore/RIFTBOUND_LORE_BIBLE.md; NPC identity/voice contracts: Docs/Lore/NPC_STORY_CONTRACTS.md. Numbers labelled HYPOTHESIS are initial tuning candidates, not measured behavior or an instruction to change all values together.

## 1. What combat should express

Riftbound is a readable, target-based frontier RPG with active movement, guard, dodge and interrupt decisions. Retain the existing four kits and controls before adding combos, another class or a replacement framework. A clean fight should let a player answer: who is attacking, what will happen, when can I respond, did my response work, and why did this group fight me?

The Witness Lattice has nine anchors that prevent competing histories from occupying the same present. That gives encounters an original reason to exist, not permission to make every ordinary bandit a magical world-ending boss. Resource desperation, forged cargo records, corrupt toll collection and the Tithe of Names connect the first fights to the crisis at human scale. A raider steals food; a Reaver validates a forged levy; a bell-hexer disrupts a witness; Choir ritualists manufacture consent. The player learns the conflict through actions and recoverable evidence before a lore lecture.

Hearthward Compact/Ashen Pact are display/canon identities mapped to preserved legacy faction IDs. The Roadward Truce permits cross-faction co-op and shared hostile targets. No PvP, friendly fire, faction-locked party role or automatic hostility to civilians is added here. Warrior/Mage/Rogue/Paladin remain available under current compatibility rules; Ranger is deferred until the reference combat slice passes.

Desired moment-to-moment sequence: read intent → select/position → commit an action → see the contact resolve → choose recovery/defense → recover evidence or cargo. Damage numbers support this sequence; they do not replace it.

## 2. Evidence and open acceptance gates

Combined02 compiled and built with0 C# errors. Existing145 quest checks,41 gameplay checks and32 native spawn checks passed as recorded in the integration handoff. Those do not establish perfect combat feel or every authority edge case.

Only four of Combined02's ten Relay scenarios completed: UI flow, invalid code, resilience and combat. Authority was interrupted when the user stopped desktop automation; progression, late join, full acceptance, client leave/rejoin and capacity were not started. **Six remain incomplete/NOT RUN on the final candidate.** Candidate01's10/10 results are historical and must not be transferred to02. Preserve all existing assertions; finish these gates before describing online combat as fully accepted.

This document uses three labels:

- **OBSERVED_SOURCE:** an actual branch/constant/call relationship in the inspected source.
- **SOURCE_RISK:** behavior needing a bounded reproduction on current source; not a newly demonstrated exploit, proof that previous tests failed, or an excuse to rewrite systems.
- **HYPOTHESIS:** a proposed target to test, measure and revise.

No new gameplay, latency or performance test ran while writing this plan.

## 3. Concrete source inventory and drift

| Area / inspected symbols | OBSERVED_SOURCE | Required reproduction or decision |
| --- | --- | --- |
| PlayerController.Attack | Coroutine snapshots victim, turns toward it, sets attackReady=duration+.08s, busyUntil=.78×duration; resolves after .45×duration. Launch range3.2m/contact3.5m and forward-dot>.1; cleave radius3.8m. | SOURCE_RISK: clip contact, action recovery and root-distance/facing may disagree. Record accepted action, normalized contact phase and actual victim at contact. |
| CastAbility1 | Warrior grants15 Rage and Paladin grants1 Holy Power before Attack validates a target; Rogue spends40 Energy before validation. | SOURCE_RISK: invalid targeting may generate or consume resources. Establish transaction semantics before changing resource amounts. |
| CastAbility2 | Eviscerate, Nova and Consecration call damage immediately rather than the melee coroutine contact phase. Nova is radius6.5m; Consecration5.5m; Eviscerate3.5m. | SOURCE_RISK: instant damage can precede the visible swing/cast; verify whether effects promise a field, freeze, slow or burst. |
| PlayerController.Hit | Local level/weapon/race/talent/shrine/critical calculation and hit feedback occur before a multiplayer request. Applies class debuffs after the request. | SOURCE_RISK: unconfirmed damage text, offline/online differences and local DoT ownership. Audit BuffSystem periodic target.Damage as well. |
| FireballSpell | Local homing visual, speed22m/s/lifetime3.5s; in multiplayer requests attack on visual impact. Sphere primitive has its collider removed; travel Update follows the target. | SOURCE_RISK: projectile can visually cross cover or lose a dead target; server final LOS differs from visible travel. Define the authoritative projectile/contact contract. |
| RiftboundCoopPlayer.ValidateAndExecuteAbilityOnServer | Class checks for named attacks; melee cooldown.35s/other attacks.8s; AOE cooldown keyed by ability+enemy; ranges30m fireball/8.5mAOE/5m other. Server damage has its own base table. | SOURCE_RISK: these are broad validation tolerances, not exact class cooldown/resource parity. Audit accepted IDs, class resources, one-cast AOE identity and failures before consuming cooldown. Existing authority passes cover their stated cases only. |
| ApplyServerAbilityHeal | Named class guards,8s heal-key cooldown, client-supplied amount clamped1..65; Arcane Ward grants a bounded movement allowance. | SOURCE_RISK: exact legal ability allowlist, heal amounts, resource costs, full-health rejection and client-side pre-heal must agree. Do not accept arbitrary IDs or trust client amounts as the damage/heal rule. |
| Interrupt | Player uses Mage25m/others4.5m and12s on success; ActionBar promises4s school lock. Server interrupt follows the generic5m/other-ability path; CastBarSystem sets interruptEndTime+.85s. | SOURCE_RISK: range/cooldown/lockout meaning differs. An interrupted cast is not proof of a four-second school lock; verify expiry and recast behavior. |
| Guard / dodge | PlayerUtilities guard is stationary/non-steering,50% local multiplier, max3s,6s cooldown after release. Dodge cooldown3.5s, movement .32s, local immunity .38s. | SOURCE_RISK: server incoming damage subtracts integer health before the owner invokes Combatant.Damage; server damage must resolve authoritative guard/dodge/armor consistently. |
| Combatant.Damage / CathedralBossEncounter | Damage clamps a positive incoming hit to at least1 after multiplier; dormant Vael sets incomingDamageMultiplier=0. | SOURCE_RISK: phase immunity may still take chip damage. Reproduce intended dormant/invulnerable semantics; do not blindly remove the global minimum without armor/DoT/phase/death regressions. |
| EnemyController | Aggro distance14m, home-target limit25m; return-home heal45%maxHP/s. Normal impact at .45×attackDuration; attack keeps facing target; heavy warning1.8s/radius4m/9s interval. | SOURCE_RISK: tracking through commitment, abrupt leash healing, home/cover/vertical edge cases and multiple enemies striking together. Preserve original homes/IDs until a reviewed encounter request. |
| Enemy remote hits | pendingDamage permits targetIsRemote OR ClearSightForTarget; remote ranged dispatch applies direct damage, local ranged uses EnemyProjectile with wall/player sweep. | SOURCE_RISK: remote targets may face different cover/travel/defense rules. Match host/local/client LOS and projectile outcome; do not simply disable authority to get parity. |
| Boss managers | Cathedral/Apex/WorldBoss use named references, local-player checks and direct local damage in inspected special-ability branches; existing dead/phase guards are present. | SOURCE_RISK: host-only execution, all eligible player selection, telegraph replication and late join need specific boss checks beyond general enemy replication. |
| CombatFeedback / CombatTextManager | Hit/Heal event feedback plus explicit damage text/SFX in several ability/debuff paths; hitflash clears property blocks after.12s. | SOURCE_RISK: duplicate numbers/sounds and clearing unrelated property-block tint. One event owner per authoritative outcome. |
| PlayerClassSystem descriptions | Contains original Alliance/Horde labels and descriptions such as Nova freeze, Ward shield and Eviscerate critical finisher. | SOURCE_RISK: display/canon/actual effect drift. Preserve ability IDs; the lore bible controls labels and the runtime adapter follows the agreed content contract. |

The first implementation stage must reproduce and fix exact-class cooldown/range/resource/death-guard parity before cosmetic combat timing. This is a bounded extension of existing handlers, not a new networking layer.

## 4. Four class identities and current arithmetic

These are source values at the inspected baseline, not balance recommendations. Local Hit adds3 damage per level above1 plus WeaponBonus, then race/talent/shrine multipliers and a random critical. Server attacks use separate bases plus4 per level; the inspected handler does not use the same full local formula. Compare recorded accepted outcomes with modifiers disabled first, then add one modifier at a time.

| Kit | Current local actions / resource | Inspected server attack base | Identity to preserve/refine |
| --- | --- | --- | --- |
| Warrior | Strike24, default duration1.2s,+15 Rage; Whirlwind38,20 Rage, radius3.8m, nominal5s cooldown modified by talent; Second Wind45 heal/12s. Rage decays4/s after its current out-of-combat condition. | Generic melee18; Whirlwind28. | Deliberate contact, cleave when enemies commit, spend earned Rage rather than permanent spin. A shielded foe can be repositioned; no mandatory shield item/system redesign. |
| Mage | Fireball36 plus its own level/weapon formula,15 Mana,1.25s ready/.6s busy,28m; Nova32,25 Mana,6s; Ward20 Mana/10s,7.5m forward move+40 heal. Mana regenerates8/s in inspected player code. | Fireball34; Nova24. | Space, sightline and tempo. Witness geometry offers exact trajectories; it does not justify firing through walls. Decide whether Ward is heal or shield and describe it honestly. |
| Rogue | Sinister Strike20,40 Energy,.55s attack; Eviscerate52,35 Energy,4s ready,.5s busy; Sprint20 Energy/10s,+65% speed for5s. Energy20/s plus talents. Local critical chance28% before shrine bonus. | Generic melee18; Eviscerate46. | Short commitments and movement windows; punish exposed recovery. Eviscerate is not automatically a guaranteed critical or combo-point spender merely because the description calls it a finisher. No new combo-point subsystem in the first slice. |
| Paladin | Crusader Strike26,1.1s,+1 Holy Power; Consecration40,2 Holy Power/6s, radius5.5m; Holy Light60,3 Holy Power/12s. | Generic melee18; Consecration24. | Earned oath-powered area control and recovery. Consecration's present burst must not pretend to be a persistent damaging floor unless duration/ticks/authority are implemented and tested. |

Local non-Rogue base critical chance is16%, multiplier1.8; Brute bonus1.15 is applied in inspected local/server damage paths, but other racial/gear/shrine effects need parity auditing. These are data points, not a guarantee every description is implemented. Freeze class IDs, saved equipment IDs and reward contracts. Fix source-of-truth duplication before tuning all classes to a spreadsheet.

## 5. Action contract: commitment without unresponsive controls

Extend the smallest existing helper/table first. A compact ability definition can hold legal class, cost/gain, range/shape, windup/contact/recovery, cooldown, interruption policy, presentation key and server result policy. Use preserved ability string IDs at compatibility boundaries. Do not introduce a second asset framework while scattered current tables are still authoritative in different places.

State contract:

1. **Intent:** input references ability/target and monotonically increasing local action sequence. Input is not damage authorization.
2. **Validate:** host checks living owner/target, legal ability/class, modal/state locks, exact cost/cooldown, accepted origin/range/LOS and encounter eligibility. Rejected requests spend nothing and produce no success feedback.
3. **Commit:** host creates action ID and immutable parameters, spends a valid cost once and snapshots the target/ground center. Whether an attack that was valid at launch then misses spends its cost is explicit; proposed default: yes. Generator gains occur on validated contact, not invalid input; confirm this design before changing current amounts.
4. **Windup:** animation/weapon/cast silhouette communicates the action. Target switch updates the selection ring for a future action; it does not redirect an already committed swing halfway through contact.
5. **Contact/active:** host evaluates the documented shape at the documented time, applies at most one direct result per action-target key, and publishes accepted damage/mitigation/interrupt outcomes. An AOE is one action with a bounded victim set, not a newly purchased cast for every target.
6. **Recovery:** the action has a readable safe-to-act point. A queue may retain the next eligible intent, not bypass recovery. Death, deserialization/scene exit and encounter reset cancel pending callbacks/action IDs safely.

Initial input policy HYPOTHESIS: one primary queue slot valid for120ms before recovery ends; latest eligible primary replaces the pending primary. Defensive intent is separately prioritized only where the cancel matrix permits. Drop queued attacks on death/modal opening/scene/session exit, target loss or explicit cancel. Holding primary may request the next legal primary, never one RPC per frame. Display queued state subtly on the existing slot; no extra permanent action panel.

Cancellation proposal to validate per clip: before commitment, cancel freely without cost; during committed windup, movement follows that ability's policy and a dodge may consume its own cooldown only if the action explicitly allows a cancel; after contact, retain a minimum recovery so dodge spam cannot erase every melee cost. Interrupt is independent only where the kit contract says so. Never allow client cancel messages to erase host-confirmed hits.

### Initial timing hypotheses

All proposed values below require native capture/frame timestamps and player trials; do not treat them as an already-approved balance patch.

| Element | Current source anchor | First test hypothesis | Accept/reject measurement |
| --- | --- | --- | --- |
| Basic Warrior |1.2s duration, contact540ms, busy936ms, ready1280ms | Windup350–450ms/contact≤100ms/recovery300–450ms; retain current damage until measured | Ten swings per rig: visible weapon contact vs accepted hit within80ms at60Hz capture; no earlier damage popup |
| Rogue basic |.55s duration/contact247.5ms/ready630ms | Windup180–240ms/recovery180–260ms; input queue120ms | Fast but distinguishable strokes; no double hit from held key or same action sequence |
| Paladin basic |1.1s/contact495ms | Windup350–450ms/recovery300–450ms | Clear weight without feet sliding during full lock; one resource gain per accepted contact |
| Fireball | Launch immediate in handler,.6s busy/1.25s ready | Release cue250–400ms into legal cast; preserve aim/LOS contract, cooldown initially1.25s | Projectile starts at actual hand/staff socket; no cast/VFX success on rejected request |
| Enemy small melee | Default1.2s/.45 impact; scene values vary | At least500–750ms readable windup, aim lock for final150–250ms | Player leaving declared shape before host contact avoids hit at0/80ms targetRTT |
| Tollwarden hook | New behavior proposal |900–1200ms line windup, bounded pull≤2m, recovery800ms | Walls/rails/steep edges stop pull; no pull across bridge or lethal cliff |
| Bell-hexer channel | Existing ranged cast bar duration mismatch |1.4–1.8s cast with release at its actual bar end | Interrupt cancels release exactly once; projectile travel remains visible to remote player |
| Champion heavy | Existing1.8s/radius4m/38 damage | Keep1.8s learning window first; declare radius/aim lock and recovery900–1200ms | Area marker equals damage area within0.15m; no tracking center after announced lock |
| Defensive input | Local dodge.32s movement/.38s immunity | Reuse durations first; host-authorized immunity must share a defined start/end | No retroactive immunity for an already resolved hit; authoritative/local health agrees after the outcome |
| Confirmation feedback | Multiple current emitters | One accepted direct-hit number/audio cue; DoT aggregation≤250ms | No duplicate outcome IDs or success numbers for a rejected action |

Tuning order is correctness → contact readability → response → damage/TTK → extra spectacle. Avoid changing clip speed, duration, damage, enemy HP and camera shake in the same experiment.

## 6. Movement, animation, target and collision rules

- Keep existing CharacterController collision, base visual rotation preservation and calibrated stride work. Locomotion and attack phase must not fight to rotate the same visual child; explicitly return ownership on recovery/cancel/death.
- Imported Jog_Fwd_Loop/Sprint_Loop torso/foot distortion is an unresolved art/import task. Playback speed and visual yaw cannot be advertised as fixing a twisted skeleton. Record each race/rig/weapon combination at idle/run/backpedal/strafe/attack/guard/dodge; approve a focused retarget/import change separately, preserve original sources/GUIDs and compare captures.
- For the first slice retain in-place movement/collision. Do not enable root motion globally to hide foot sliding. Motion extracted from a new clip must be bounded by the same authoritative movement/terrain/collider rules, with no world clipping.
- Make range semantics explicit: horizontal reach against an eligible target collider, with a vertical tolerance and correct large-enemy surface, rather than arbitrary root-to-root3D distance for every shape. First record current behavior; only change the shared rule after class/server tests agree.
- Melee is an authored cone/sweep or targeted contact contract. It must not strike a target behind the character merely because a root is within a radius. Whirlwind/Nova may intentionally be radial and need matching presentation.
- Tab/click prefer alive hostile actors in the usable view, avoid noncombat townsfolk/party members, and explain out-of-range/blocked LOS without flashing error text every frame. Camera rotation alone must not retarget an already released strike. Target death clears or moves selection only under an explicit next-target policy.
- Keep safe arrival, escape and service approaches. No new enemy wall/arena rail may block the approved roads, mounts or camera. A hook/knockback uses swept collision and terrain checks; no teleporting a player through bridge rails, a closed doorway or another client.
- Preserve mounted action/damage dismount policy until its specific UX decision. Do not pass an uninterrupted mounted hostile-camp test by granting invulnerability or moving camp actors.

## 7. Defense and fairness contract

Guard and dodge must resolve in the host's damage pipeline alongside armor/resistance and phase immunity. An owner may present immediate input feedback, but a client-local invulnerableUntil or multiplier cannot be the authoritative defense. Send accepted defensive state/timestamps and publish the host's applied mitigation. Do not subtract raw network health and then independently apply a second differently mitigated local hit.

Keep Q's current stationary-guard/right-mouse-strafe behavior documented initially. Audit keyboard vs clickable guard action, UI modal gating, arrow-key movement and gamepad-equivalent input before claiming parity. Guard indicator distinguishes held, expired and recharging; it need not be another full-screen banner. Decide whether guard protects all directions or a front arc; initial proposal is a visible120-degree front arc with small/physical protection and clearly labelled unblockable ground effects. This is a design proposal, not current behavior.

Interrupt displays the actual cast identity, interruptibility and remaining host-synchronized time. Propose preserving12s ability cooldown until balance evidence changes it; resolve whether the existing advertised4s school lock is implemented or rewrite the description to actual interruption/stagger behavior. An interrupt arriving after a host-resolved cast gets a clear late result rather than pretending success. Boss uninterruptibility needs an icon/shape cue as well as color. Killing the caster cancels unlaunched effects; already released physical/projectile hazards follow an explicit rule and cannot silently damage after a reset.

Phase protection semantics are separate from flat armor: a dormant/invulnerable actor should either reject damage entirely or use a deliberate minimum-chip rule stated to the player. Reproduce Vael first. If the intended state is immunity, add an explicit gate/phase predicate with tests for positive armor mitigation, zero multipliers, DoT, lethal damage, repeated death notification and phase transition. Do not remove the minimum-one rule globally on the strength of one inspection.

## 8. Enemy intent, spacing and authority

Use small behavior profiles/adapters keyed by stable actor/archetype ID, not logic inferred from GameObject display names. A rename from Hexer to a canon title must not change the spell selected by string.Contains. All active player selection and projectile/damage decisions execute on the host in online mode; remote objects render replicated intent/outcomes. Offline uses the same logical action definitions through the existing offline path.

State vocabulary: unaware/job → notice → threaten/approach → windup → release → recovery → reposition → leash/return. Death/reset cancels pending releases. Ranged and melee actors use the same cover rule for local and remote targets; the existing targetIsRemote LOS bypass is a reproduction priority. A remote spell cannot become an unavoidable instant hit when the offline version is a dodgeable projectile.

Spacing HYPOTHESES: two melee engagement slots around a solo target, one clearly visible heavy commitment at a time per local fight, and no more than two ordinary contact windows overlapping within150ms until playtests justify more. Caster seeks a6–10m pocket with a reachable flank rather than running indefinitely at attackRange boundary. Start with a bounded steering/separation adjustment in existing controllers; do not introduce a new AI framework/NavMesh bake across the whole world. A navigation failure returns to a valid home and reports a diagnostic instead of teleporting into the player.

Leash remains based on authored home and encounter ownership, not whichever party member most recently crossed a line. Warn/visually disengage before restoration; no invisible full heal while still hitting players. Clear harmful callbacks/debuff ownership on reset, and keep healing/reward suppression consistent. Four-player aggro changes cannot drag a settlement service into combat or permit off-route farming while a boss resets. Record current14m/25m values, actual collision and sightline outcomes before choosing new radii.

## 9. The reference encounter: three enemies, then one champion

First implement and accept one small reference fight before multiplying bosses or adding dozens of attacks. The proposed reference layout uses an off-road encounter pocket near the existing reference corridor; coordinates are selected only after current terrain/route/camera/service checks and specific scene ownership approval. Candidate layout HYPOTHESIS: roughly12×14m combat pocket, at least3m unobstructed retreat lane, no forced fight on the bridge deck, preserved mounted through-route and clear service/discovery aprons. These are preview targets, not measured placement facts.

| Actor / job | Behavior and readable tell | Player answer / failure recovery | Lore and content contract |
| --- | --- | --- | --- |
| Desperate road raider / obtain food | Steps out with an empty ration sack, one shouted demand, short single blade windup; after a miss exposes recovery instead of instantly snapping | Sidestep, guard or punish; it does not gain a new mechanic on each HP threshold | Show hunger/resource theft without absolving violence. Recover a ration tally with inconsistent levy marks; any surrender/nonlethal option is a later quest decision, not silently added reward logic |
| Reaver tollwarden / seize marked cargo | Visible shield posture and hook line; plants before the hook, drops shield/turn rate after the miss | Flank, interrupt its special if flagged, or break LOS; bounded pull stops at terrain/rails and never stuns into unavoidable cliff death | Shield carries a forged collection seal. Intent is cargo seizure and intimidation, not random spinning. Keep persistent enemy/loot IDs; no automatic camp recreation |
| Bell-hexer / disrupt a witness | Raises an identifiable bell, audible two-note preparation, thin marked projectile/channel lane; moves only during a defined reposition phase | Interrupt or use cover, then reach its recovery pocket; remote player gets the same release/LOS rule | The bell corrupts testimony locally. It does not turn every ordinary raider into a Choir member. A dropped bell inscription points to a broker, not the whole final revelation |

Teaching sequence: first raider alone teaches contact/recovery; add tollwarden in a separate staged trial; add bell-hexer; accept the three-role composition. This may be a development fixture before authoring permanent world encounters; it does not authorize new live spawns or quest rewards.

**Reaver champion slice: Captain Selvek, stable ID westbank-reaver-captain.** Canon makes him a Returned former ferrymaster, visually the existing Undead/Rogue: declared lost, then denied wages/testimony/restitution by a ledger that refuses his return. His grievance is real; violent toll collection is his choice, not a racial trait or automatic Choir loyalty. Existing RiverReaverCamp contains the captain and guards; preserve those actors, homes, lockbox linkage and defeat/reward contracts. Do not call the camp installer to generate a replacement. Design one shield/hook commitment and one existing-style heavy slam with clear recovery, not three new phase bars. A disputed tariff record and intact seized cargo connect the fight to denied recognition. At a health threshold he may take a more desperate stance with a stronger tell; a numeric threshold/HP buff waits for measured TTK. One clear line reveals the contradiction between a ferrymaster preserving passengers' names and taking cargo by force. Evidence advances the Tithe-of-Names investigation without claiming all Reavers serve the same master. Keep current defeat/lockbox outcomes; a surrender/nonlethal ending needs a separate approved quest/reward contract.

Acceptance for the slice: each class can defeat each role solo using its existing three core abilities plus universal defenses; the three-role group has a visible retreat and no unavoidable combined control chain; a mixed-faction party can join/leave/late-join without duplicate rewards or different target visibility. A failed attempt leaves services/roads usable. Champion lockbox cannot be opened twice and resets do not resurrect claimable rewards.

## 10. Bosses after the slice, tied to their duties

- **Malakar:** enforces the compulsory Perfect Witness. Telegraphs are ritual alignment and demanded testimony, not disconnected fire circles. A player breaks a declared channel/position rule; interruptibility matches the ritual shield state. Preserve existing phase ownership/death guards and review host/all-player selection before adding effects.
- **Vael:** falsified memorials protect the institution. Dormant phase is clearly protected, then the exposed falsification changes the encounter. Resolve the zero-multiplier chip-damage risk before designing a shielded phase. Phase transition is one authoritative event; dead callbacks cannot restart the ritual or award gold twice.
- **Vermithrax:** a summit guardian driven into conflict by Choir theft. Flame/cone and meteor language remains, but targets stolen anchor-bearing objects and protects its territory. Preserve current breath/meteor/enrage systems as audited source; add a readable story reason, actual host geometry and safe escape rather than more overlapping markers.
- **Gorgonax:** damaged pressure guardian, not another generic evil meteor caster. Prioritize pressure pulses, unstable vents and a predictable release lane. Existing stomp/meteor/enrage can be adapted only after visual/authority/reset review; do not silently change hazards while keeping misleading old cues.

Choir acolytes, when the later ritual slice is approved, protect one identifiable ritual line rather than chaining generic AOE attacks. A visible sustained channel feeds a declared protection state, interrupt/displacement/line-breaking follows one explicit counter rule, and restoration has a recovery window. Start with one acolyte and one protected target in a development fixture. Do not add simultaneous invulnerable healers, mandatory class-specific dispels or hidden channels before the three-role reference fight passes.

Each boss must have a one-page contract: trigger, host authority, eligible targets, tells, impact volumes, interrupt/guard rules, phase transition, wipe/reset/death/late-join behavior, reward owner and save/session policy. No new boss arena or global timing pause is authorized by this document.

## 11. Presentation, camera and accessibility

Damage feedback follows confirmed outcomes. One authoritative result owns the number, impact SFX and hit flash; predicted local launch cues are distinguishable and never show an unconfirmed critical/damage amount. Aggregate rapid DoT text within a proposed250ms window; cap ordinary clutter while keeping lethal/interrupt/telegraph information legible. Avoid a floating sentence, chat spam and screen banner on every hit.

Create a shared visual grammar using existing licensed assets: physical blade arc, shield/hook line, bell witness-disruption, oath/ward effect and ritual protection each have distinct shape/motion/sound, not only red vs green. Telegraphed floor geometry conforms to actual supported terrain and reports matching radius/cone. Hidden authoring lights/gizmos must not become visible light-marker meshes; review actual game rendering before deleting a legitimate light.

Audio HYPOTHESES: one launch and one confirmed contact cue per action, maximum6 simultaneous close combat voices before quiet duplicate impacts are culled, hostile warning priority over incidental barks. Record distance attenuation in the actual build; do not assume every sound is spatial. Interrupt success, shield impact, ordinary hit and immunity must sound different. Bells must signal a mechanic rather than constantly competing with dialogue. No provider purchase/download in this plan.

Keep camera target framing, collision and player steering stable during combat. Proposed optional camera impulse is local-only and independently reduced/disabled for accessibility. Do not call Time.timeScale=0, pause the whole network simulation or freeze other players for cinematic hitstop. Any future tiny local pose/visual hold is presentation-only and must not change authoritative timers, input acceptance or projectile travel; defer until the slice already feels correct.

Input/accessibility acceptance: existing keyboard/mouse and clickable buttons agree; remappable actions and a clear controller focus path are separate scoped tasks, not claimed present. Offer optional hold/toggle guard, reduced shake/flashes, stable ground marker outlines, readable cast text, non-color interruptibility and adjustable combat-number density. A setting cannot hide the only warning of a lethal mechanic. Test1280×720 as well as the user's normal resolution, with UI scale and left/right handed remaps once implemented.

## 12. Balance measurements before gear inflation

Baseline trials use fixed level1 then current cap, fixed admitted weapon/armor IDs, no shrine/talent buffs initially, one race at a time, and all four classes. Record actual legal cast counts, cost/gain, direct/DoT damage, healing/mitigation, damage taken, casts rejected, dodge/interrupt use, fight duration and whether death/reset occurred. Add race, weapon, talent and shrine modifiers individually; never fit an unexplained live DPS sample to a false base value.

Initial HYPOTHESES: an ordinary equal-level enemy takes roughly3–5 clean primary contacts/5–8s under intended resource cadence; an unguarded ordinary hit costs8–12% of baseline player health, heavy25–35%; a solo champion lasts35–55s with several meaningful defenses rather than an inflated sponge. Record current outcomes first; these targets may be rejected. Player skill and resource waiting affect the result, so publish sample size/range, not only an average.

For groups, test an initial HP scale1+.65×(players−1) against the same role mix; do not silently apply it. It must not multiply ordinary damage into unavoidable one-shots, scale rewards repeatedly on join, or change current save IDs. Evaluate whether all targets need scaling at all. No class should be required to cheese a specific racial bonus to clear the reference slice. Gear upgrades should make a measured difference without skipping the taught mechanic; preserve gold/XP/drop values until a separate economy balance packet.

## 13. Network scenarios and measurable acceptance

0/80/150/250ms are **target RTT scenarios, not observed measurements**. Record realized latency/jitter/loss and the tool/configuration used; do not call a parameter set proof of real delay. Start with stable RTT, then add a separately declared jitter/loss profile. Same-machine Relay is not two-PC cross-network acceptance.

| Scenario | Required outcome / receipt |
| --- | --- |
| All four classes, legal/illegal intent | Exact legal ability ID/class/cost/cooldown/range; rejected/unknown/duplicate requests cause zero resource, HP, cooldown or reward change unless the documented commit policy already spent a valid action |
| Two inputs near recovery boundary | At most one queued legal next action; no lost valid buffered press at0/80ms targetRTT; no out-of-order duplicate at150/250ms. Log accepted action IDs and timestamps |
| AOE hits several actors | One cost/cooldown transaction, one damage result per eligible victim; no resubmission to the same enemy for extra damage; dead/ineligible/behind-cover targets follow the declared shape |
| Ranged cover / projectile | Local host and remote client produce the same valid-LOS/impact outcome; no hidden direct hit on a remote player when a comparable local projectile hits a wall |
| Guard/dodge at boundary | Host timestamps define the result, authoritative/local HP converges after the published outcome, no retroactive undo of confirmed damage, no client-only permanent immunity |
| Interrupt boundary / phase immunity | One successful interrupt, late/uninterruptible result distinguished; no spell completes twice after interruption/death/reset; dormant Vael follows the agreed immunity semantics |
| Target dies/disconnects mid-action | Pending target reference safely cancels/resolves under contract; no corpse damage, endless cast, stale revive, duplicate XP/gold or stuck movement lock |
| Late join during warning/phase | Actor HP/state/current action/remaining warning and protection are correct for the joining client; no full-health phantom or invisible damage window |
| Host exit / scene reset | Existing session-ended policy, all owned transient effects/actions clear, offline saves remain untouched; no hidden host migration or persistence claim |
| High RTT | Local intent feedback remains immediate; no premature success damage number; host resolves legality. Report added perceived delay and design limits rather than trusting forged client timestamps to make every dodge win |

Initial acceptance thresholds HYPOTHESES: zero duplicate authoritative action-target outcomes across100 repeated direct and50 AOE attempts; no accepted attack from a dead owner or to a dead target; exact resource/cooldown agreement after each acknowledged action; zero Rigidbody/CharacterController/terrain penetration attributable to dodge/hook/Blink in the bounded fixture; no callback after death/reset except explicitly released hazards. Every timing figure includes simulation/capture rate and tolerance. Headless assertions supplement, not replace, native capture and human feel.

Do not add arbitrary client health rollback or accept client-reported hit results. Any lag compensation later needs a separately reviewed bounded history, server eligibility rules and replay-safe identifiers; it is not a shortcut in this pass.

## 14. Implementation stages and responsibilities

These stages were **PLANNED** at the design baseline, apart from the source inventory; this document does not establish their completion. Runtime development covers combat, networking, the NPC adapter and the whole FactionQuestSystem.cs file. Narrative and world design supply catalog data, canon, encounter briefs, visual diagnostics and reviewed placements. Keep one editor per shared runtime file and agree one catalog/type contract before implementing adapters. Model/import changes require a separate review. Existing quest rewards, resource and ability IDs, and serialized references are compatibility contracts.

| Packet | Priority / owner | Smallest concrete deliverable | Dependency and measured acceptance |
| --- | --- | --- | --- |
| CF00 | P0 / Runtime | Complete finalCombined02's six incomplete online cases, preserve source/build identity and existing checks | No green transfer from01; actual exits/assertions and manual two-PC NOT RUN boundary recorded |
| CF01 | P0 / Runtime | Bounded parity repro matrix for exactly four kits: IDs, class, range, cost/gain, cooldown, alive state and heal amount | SOURCE_RISK reproduction before fix; one shared legal definition/validation path where feasible, existing145/41 and relevant authority cases remain intact |
| CF02 | P0 / Runtime | Correct only demonstrated authority/parity defects, including unknown IDs, invalid resource transactions, healing and replay guard | CF01; host resources/cooldowns/HP agree, invalid/dead/duplicate requests have documented zero effects. No blanket values change |
| CF03 | P0 / Runtime | Reproduce dormant-phase immunity and damage-floor semantics with direct/DoT/armor/lethal cases | Preserve Combatant floor unless evidence supports an explicit phase gate; Vael protection/transition/death tests and no duplicate rewards |
| CF04 | P0 / Runtime | Match remote/local enemy LOS, authoritative projectile travel and guard/dodge mitigation | CF01–03; cover and boundary cases at declaredRTT, no direct remote bypass or client-authoritative damage |
| CF05 | P1 / Animation + runtime | Capture one rig/kit action timeline and specify contact/recovery definition | Source clip/import issue separated from runtime; matched captures and accepted-hit timestamps, no new asset download |
| CF06 | P1 / Runtime | One-slot bounded input buffer and explicit action/cancel ownership for one primary attack | CF02/05;100 repeated inputs, death/modal/target loss cleanup; no recovery bypass |
| CF07 | P1 / Runtime | Expand accepted action IDs/phase/contact contract to existing four kits | CF06; no frame-event client damage authority; one AOE transaction and one result per victim |
| CF08 | P1 / Runtime | Reconcile interrupt range/cooldown/castbar/school-lock descriptions | CF02/07; Mage/melee, late cast, uninterruptible, repeated interrupt and death outcomes match UI |
| CF09 | P1 / Runtime | Readable enemy commitment, fixed contact geometry and bounded aim-lock/recovery | CF04/07; misses truly miss; current homes/leash preserved until encounter review |
| CF10 | P1 / Runtime | Bounded spacing/reposition/leash cleanup on the three reference roles | CF09; no stuck oscillation, pile-up strike burst, invisible heal while attacking or service chase |
| CF11 | P1 / World design | Exact three-role encounter placement/asset/camera/route preview and stable-ID contract | Canon/NPC plan +sceneownership; no shared scene save until scoped review/approval, terrain/813trees protection retained |
| CF12 | P1 / Runtime | Three-role development fixture/adapters using existing actor archetypes | CF04/08–11; raider →warden →hexer →combined; no string-name mechanics or new quest rewards |
| CF13 | P1 / Both, separate files | CaptainSelvek champion content/runtime slice | CF12; existing captain/guards/lockbox IDs retained, no camp regeneration; solo4kits/group/reset/reward dedup accepted |
| CF14 | P1 / Runtime | Outcome-owned sound/VFX/numbers and property-block-safe flash | CF07; one accepted result gives one impact package, rejected actions show no success; inspect native density and audio |
| CF15 | P1 / Animation design + asset owner | Focused locomotion/attack rig/import correction with originals preserved | CF05; per-rig contacts/feet/facing checked, no global root-motion flip; source models not replaced without review |
| CF16 | P1 / Runtime | Controller/click/key/modal/guard usability and reduced-effects options | CF06/14; same legal action across input methods; all warnings readable without color/shake |
| CF17 | P1 / Both | Fixed-baseline4class/race/gear/resource balance sheet from actual trials | CF13–16; publish samples and modifiers, keep IDs/rewards; only one tuning dimension changed per iteration |
| CF18 | P1 / Runtime + narrative | NPC/combat display adapter for legal names, hostile intent barks, effect text and faction labels | Canon and NPC content contract first; legacy ability/faction IDs and noncombat service targeting unchanged. Extend the existing narrative adapter rather than creating a competing module |
| CF19 | P2 / Runtime | Host-owned boss action/phase/reset/late-join contract before added attacks | CF03/13; no direct local-only boss damage in online path; all eligible players warned, duplicate phase/death/reward guards |
| CF20 | P2 / Both | Malakar/Vael ritual mechanics aligned to approved lore, then Vermithrax/Gorgonax duties | CF19 +specific content/scene approval; one boss at a time, visual/authority/reset/2–4player acceptance before next |
| CF21 | P1 / Runtime | Latency0/80/150/250RTT target matrix with action timestamps and actual network conditions | CF02/07/13; no forged timestamp trust, lost confirmed outcomes or overclaim of cross-network acceptance |
| CF22 | P1 / Both | Reference fight rendered performance/readability comparison | Same cameras/settings/party/enemy count; record p95 frame time/draw/FX/audio as available, no headless-FPS marketing; avoid per-hit object/material leaks |
| CF23 | P1 / Integrator + user acceptance | New unique combined build and small player-test script, exact source/checks/NOT RUN handoff | Existing145/41/spawn32 +changed online/encounter cases; manualfirstadventure/true2PC still labelled until run; no main merge without authorization |

Suggested order: CF00 → CF01 → reproduced fixesCF02/03/04 → CF05/06/07 → CF08/09/10; CF11/15 can be prepared independently of runtime changes. ThenCF12/13 reference slice → CF14/16/17/18 → CF21/22/23. CF19/20 are blocked on reference quality; Ranger, extra skill bars, threat meters, new raid bosses and PvP are outside this pass.

## 15. Implementation and verification guidelines

1. Establish the exact source/build baseline and close or explicitly report incomplete online coverage. Reproduce class cooldown, range, resource and death-guard differences with one legal and one rejected case per ability before changing handlers.
2. Fix demonstrated parity defects before cosmetic timing. Preserve IDs, amounts and rewards unless the correction itself requires a reviewed change. Keep existing offline and authority assertions; report incomplete manual and latency checks.
3. Agree the canon/NPC identity mapping, service/hostility contract and shared catalog schema before wiring the existing dialogue and action-description surfaces. Keep FactionQuestSystem.cs under one runtime editor and supply narrative changes through external data. Preserve cross-faction RoadwardTruce cooperation; display names must not select enemy mechanics.
4. Prepare the three-role/Selvek encounter brief against existing IDs, terrain, roads, vegetation, camera and service approaches. Scene or placement changes require reviewed scope and exclusive editing. Preserve camp actors, reward linkages and protected terrain; do not regenerate the world.
5. Keep model, importer, controller and prefab changes separate from runtime validation changes so each can be reviewed and reverted independently.
6. After two unchanged failures, gather new evidence before retrying. After ten minutes without new evidence, investigate a different hypothesis. Do not weaken an assertion to make a retry pass.
7. For each completed stage record the reproduced issue, focused change, changed measurements, regressions actually run and remaining checks. Distinguish source inspection, synthetic fixtures, rendered captures, normal input and real online play.

No builds or tests ran while authoring this design baseline. Historical validation results above remain scoped to their original source/build; they are not new passes for later implementation.
