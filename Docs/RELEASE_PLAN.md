# Riftbound release plan

This plan covers the diploma demonstration, competition build and a possible public game release. Milestones advance when their acceptance gates pass; no completion date is assumed.

## Release baseline

- Working branch: `cleanup/public-project-20261118`, an isolated combination of multiplayer source `953c1c950f36c4ed162c960d0fca4844754a8a26` and visual source `c4042b4834289635ccbcf6d1cef14238e538bf54`.
- The combined maintenance source is not yet a verified combined release. Record its final commit after cleanup and integration; do not label either input build as the combined candidate.
- Preserve finished branches, existing builds, current saves, stable gameplay IDs, asset GUIDs and authored terrain. Do not merge the release branch into another branch without review.
- Supported scope is a compact fantasy RPG with offline play and up to **four human players total**, including the host, in an online session.
- Online progression is session-only. Host departure ends the session; durable online characters, host migration and a persistent MMO world are not release promises.

### Current presentation findings

- The reviewed maintenance baseline is `f4ce09fcecb5a7ca4d3986f40a24aa584f3aea41`. Follow-up diagnostic changes do not constitute a newly verified combined runtime build.
- Native Idle/Ride samples cover 20 Humanoid character variants. The Human Mage remains upright in the isolated saddle bench; the reported severe tilt is **not reproduced** there. Actual local/observer transitions remain required before closure. Four Generic Undead variants need their separate rig-specific checks.
- The original contact fixture reproduced four unsupported actors: three summit enemies displaced by oversized platform capsules, and Lyra below terrain. Their names did not establish intentional flight.
- The localized scene repair now uses the existing dais/pit meshes for collision, retains the old disabled capsule components and positions the three summit actors on the dais and Lyra above terrain. Existing scene identities, X/Z positions, rotations, artwork and terrain remain preserved.
- After repair, the running-world contact fixture finds support for all 95 observed actors after scene reload. The three summit enemy motors remain grounded at their intended roots; the four repaired actors have estimated sole clearances of 0.024–0.031 m. Rendered contact captures were inspected.
- All 32 spawn checks and 145 quest checks pass, including the unchanged separate-Reaver-terrain and approach-grade assertions. These fixtures do not establish normal-input traversal, encounter feel or host/guest acceptance of the scene repair. The existing standalone predates this repair and must be rebuilt from the reviewed combined source.
- The contact fixture completed with advancing frames, but the editor emitted a search-index startup exception. No source compilation error was reported; do not describe the entire console as clean.

## Priorities

| Priority | Required outcome | Responsible area |
| --- | --- | --- |
| Must | Resolve the remote grounding API mismatch and verify the combined source compiles | Runtime |
| Must | Correct or explicitly resolve the Human Mage riding tilt before presenting mounted multiplayer | Animation |
| Must | Classify four unsupported world actors and preserve intentional flight | World + validation |
| Must | Finish the first quest chain and both reference travel corridors without blocking defects | Content + world |
| Must | Verify combat, mounting, death, saves and four-player session authority on the release candidate | Runtime + validation |
| Must | Package a reproducible Windows build with accurate controls, credits and known issues | Release |
| Must | Verify distribution rights and required attribution for every bundled asset | Content + release |
| Should | Improve backward gait, foot contact, seat fitting and transitions across the supported character set | Animation |
| Should | Refine readable lighting, spatial audio, impact feedback and compact UI at target resolutions | Presentation |
| Must | Complete fresh-machine installation and a real two-PC cross-network playthrough before advertising online acceptance | Validation |
| Later | Add distinct flying-mount art, more settlement activity and additional narrative content | Content + world |
| Later | Consider extra classes, subclasses, new zones, larger groups or additional online services | Design |

Paid providers, new backend services and a replacement asset family are not required for this release slice. Prefer the existing licensed assets and current systems.

## Gate 0 â€” lock the candidate scope

- Identify which features will actually appear in the diploma/competition demonstration and which remain future work.
- Freeze stable faction, race, class, NPC, ability, quest, enemy and chest identifiers. New display copy must not rename saved keys or change eligibility.
- Assign one editor per shared code, scene or prefab file. Review actual changes against the same source baseline before combining them.
- Establish a clean candidate checkout, supported Unity version, package lock, build settings and isolated validation profiles.
- Record known defects before testing, with severity, reproduction steps, owner area and acceptance condition. Keep failures and NOT RUN checks visible.

Exit gate: the feature list, combined source identity and validation matrix are agreed. Do not add optional content while a Must item remains unresolved.

## Gate 1 â€” close integration and presentation blockers

1. Connect remote player grounding through the implemented `GroundedVisual.BindObserver` API. The older contract names `SetObserverState`; reconcile the contract and actual caller instead of adding a second grounding framework.
2. Supply each peer's own alive, airborne and mounted state; rebind after model replacement. Missing observer context must remain safe and must not borrow local-player state.
3. Verify dismount, death and despawn restore the authored visual wrapper without clearing the nested correction, moving a network root or leaving an orphaned mount.
4. Reproduce the Human Mage `Ride` tilt with the actual rig, clip, saddle and scale. Review the smallest controller/model correction; do not apply a global rotation to all characters.
5. The `gryphon` mount currently uses horse artwork. Preserve its stable ID and unlock rules; either use accurately described existing artwork for this release or review an appropriate free, licensed replacement. Do not advertise wings or flight animation that the model lacks.
6. Review the two Ember Hatchlings, Outrider Lyra and Vermithrax the Ember Sovereign individually. Their support was unavailable in the prior visual probe; this is not proof of a placement defect. Preserve intentional flight and avoid lowering terrain or all models globally.

Exit gate: compile succeeds, focused changed-behavior checks pass and rendered local/observer captures show the intended mount, rider pose and grounded/airborne behavior.

## Gate 2 â€” make the first adventure dependable

- Start a fresh offline character through the title/character flow, then enter the world using normal input.
- Complete Abe's current sequence: three road raiders, three stolen supplies and the Ashwood Captain. Check offer, acceptance, progress, ready and completed states.
- Verify repeated interactions, enemy defeat, loot collection, quest turn-in and linked chests cannot duplicate rewards. Preserve existing counts, rewards and eligibility.
- Reach Rowan and Mira's services and Elara's survey/bounty flow without competing prompts, incorrect directions or private knowledge leaking between players.
- Traverse **Emberwatch â†’ Highspire â†’ Westbank â†’ Reaver camp** on foot and mounted. Include the bridge deck, camp approach, retreat and service access.
- Traverse **Emberwatch â†’ Greenhollow** on foot and mounted. Check road grades, stairs, terrain seams, obstacles and camera clearance in both directions.
- Preserve the protected terrain and vegetation, including the current 813-tree/15-prototype tile. Any placement repair needs a localized reviewed change and before/after evidence.

Exit gate: the first adventure is completable after a fresh start, after save/reload and after a recoverable failure. Document any optional content excluded from the demonstration.

## Gate 3 â€” combat, animation and interface acceptance

- Cover Warrior, Paladin, Mage and Rogue with representative races, then check remaining supported race/class combinations for rig or scale failures.
- Inspect idle, walk/run, backward movement, strafe, jump/land, attack/cast, guard/dodge, hit, death and respawn. Continuous locomotion must loop; one-shots must finish or cancel under the declared action rules.
- Match movement cadence to measured stride. Check feet, facing, weapon contact, recovery and mounted hips using actual gameplay video; isolated poses are supporting evidence only.
- Keep damage, defense, resources, cooldowns, LOS and reward resolution authoritative. Cosmetic animation events must not add damage or duplicate accepted impacts.
- Test target death, action interruption, rapid inputs, modal windows and target loss. Reject invalid actions without presenting a successful hit.
- Inspect compact party frames, group/session controls, ready checks and pings at the supported resolutions. Optional companions remain opt-in and distinct from human peers.
- Confirm keyboard/mouse controls, readable labels and warnings, reduced visual clutter, and no editor/debug graphics in the standalone.
- Listen to local and remote impact, cast, mount and ambient sounds. Check spatial distance, mix balance, abrupt loops and duplicate playback; test mute/volume controls.
- Review lighting, shadows, materials and ability effects in the actual build. A clean editor screenshot does not prove every shipped shader or texture is included.

Exit gate: no blocking input, animation, clarity or audio defect remains in the chosen demonstration path. Unsupported actions and art limitations are described accurately.

## Gate 4 â€” exact-candidate regression and online coverage

| Evidence category | Required coverage | Limit |
| --- | --- | --- |
| Source/asset inspection | IDs, GUIDs, resource reachability, clip/rig bindings and protected asset hashes | Does not prove gameplay |
| Native fixtures | Changed mount cadence, grounding, support, cancellation and authority cases | Does not prove normal input or visual feel |
| Offline normal input | Fresh/load character, first quests, services, combat, death, mounts and both routes | Record class/race, route and actual build |
| Headless session tests | Join/reject, authority, progression, late join, ready checks, leave/rejoin, host exit and capacity | Two local processes are not a two-PC cross-network test |
| Rendered host/guest | Both perspectives for movement, combat, real mounts, rider fitting, death and late join | Inspect captures; flags alone cannot prove correct artwork |
| Manual two-PC play | Same complete candidate on separate PCs over a real connection | Required before claiming two-PC cross-network acceptance |

- Run the existing quest, gameplay, spawn and relevant combat suites against the final candidate, using their actual assertions and expected coverage. Investigate failures or unexpected skips; do not lower expectations to turn them green.
- Prior records include 145 quest, 41 gameplay, 32 spawn checks and scoped native presentation tests. These are historical baselines, not automatic passes for this combined release.
- Admit four players total and reject a fifth. Exercise leader permissions, role changes, ready yes/no/timeout, stale replies and disconnect cleanup.
- Verify late join while mounted, during combat and during a ready check. Repeat with host and guest roles reversed.
- Use isolated profiles and invite codes. Confirm online activity does not overwrite offline progression or another player's personal quest state.
- Observe a fixed representative scene with two and four players; record frame-time distribution, obvious allocation spikes and effect/audio density. Report the PC/settings and test conditions rather than inventing a universal performance claim.

Exit gate: all Must scenarios pass on the exact candidate, or a release decision explicitly limits the supported scope. A diploma-only offline demonstration must not be presented as verified online multiplayer.

## Gate 5 â€” content, rights and documentation freeze

- Check every model, texture, sound, font, icon and dependency for redistribution rights. Retain required licenses, credits and provenance records; unresolved rights block public distribution of the affected material.
- Verify authored NPC copy against the lore bible, current quest states and actual service/hostility behavior. Unknown or unsupported conditions need safe fallback text.
- Review the public README, gameplay/online setup, controls, roadmap and known issues against the shipped build. Remove stale instructions and promises, not genuine limitations.
- Include a short diploma/competition explanation of the implemented architecture, gameplay loop, testing approach and remaining work. Capture screenshots/video from the actual candidate.
- Freeze optional features once acceptance testing begins. Any later code, package, scene or asset change creates a new candidate and requires the affected checks again.

Exit gate: the public feature list, credits and demonstration material match the release, with no unsupported claims or missing required attribution.

## Gate 6 â€” package and release

- Build a uniquely named Windows candidate from the recorded clean source/configuration. Keep earlier candidates intact.
- Record the source commit, Unity version, build configuration, executable and managed/runtime assembly hashes, content/version identity and validation summary.
- Distribute the **complete output folder**, including the executable, matching `_Data` directory and accompanying Unity runtime files. Do not ship only the `.exe`.
- Test the packaged archive after extraction on a fresh location and, preferably, a second PC without the Unity editor. Verify launch, inputs, audio, saves, exit and online setup where supported.
- Include controls, minimum tested hardware/settings, online/session limitations, known issues and credits. Keep saves, caches, authentication data and private invite codes out of the package.
- Tag the reviewed release source and publish the matching archive/checksums through the chosen distribution channel. Verify the uploaded archive is the artifact that was tested.

Exit gate: a player can install, launch and follow the intended first-adventure path using only the released files and instructions.

## Demonstration and rollback

- Rehearse a short reliable sequence: launch â†’ choose character â†’ Abe quest/combat â†’ mount/travel â†’ one service or exploration objective. Demonstrate online features only within their verified scope.
- Keep a separate prepared demonstration profile and a clean-start profile. Do not depend on modifying real player saves or a live connection for the entire presentation.
- Retain the previous working full build, source tag, settings and save-format compatibility notes. Test how to return to it before publishing an update.
- If a blocking release defect appears, stop distributing the faulty candidate, keep diagnostic evidence and publish a corrected version or clearly identified previous build. Never overwrite old evidence or silently reuse a version name for different binaries.
- Classify post-release reports by build, platform, offline/host/guest role, race/class and reproduction steps. Fix regressions before reopening Later features.

Related documents: [Gameplay](GAMEPLAY.md), [Online setup](ONLINE_SETUP.md), [Validation](VALIDATION.md), [World layout](WORLD_ZONES.md) and [Roadmap](ROADMAP.md).
