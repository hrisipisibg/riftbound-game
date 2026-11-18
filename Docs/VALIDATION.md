# Validation

Use an isolated checkout and unique evidence paths. Compile after source/package changes. Never replace a failed assertion with a weaker expectation to obtain a pass.

## Unity entrypoints

Run batch methods with `-batchmode -projectPath <checkout> -executeMethod <method> -logFile <ignored evidence file>`. PlayMode methods manage their own exit; omit `-quit`. Run one Unity process per checkout.

| Method | Scope |
| --- | --- |
| Riftbound.Editor.CombatRefinementChecks.VerifyParity | Combat authority/parity, selected narrative and multiplayer/group behavior |
| Riftbound.Editor.Gp01Diagnostics.RunPlayModeVerify | Gameplay diagnostics with isolated progress |
| Riftbound.Editor.QuestRouteSmokeTest.Run | Quest/state/reward/navigation regression |
| Riftbound.Editor.CombinedSpawnVerification.Run | Native terrain/collider spawn checks; requires local read-only context |
| Riftbound.Editor.MountVisualChecks.Run |16focused native cadence/state/binding checks |
| Riftbound.Editor.GroundedVisualChecks.Run |19native contact/root/rebinding/support fixtures |
| Riftbound.Editor.PresentationAssetAudit.Run | Prefab/rig/clip and sampled-pose inventory |
| Riftbound.Editor.PresentationWorldProbe.Run | Running-world contact observation with explicit synthetic idle/title fixture and scene reload |
| Riftbound.Editor.RiderPoseAudit.Run | Idle/Ride samples and optional GPU bench renders for Humanoid resource characters; reports excluded rigs |
| Riftbound.Editor.SupportSurfaceAudit.Run | Saved-scene summit/Lyra bounds and rays, scene hash preservation, and actual Reaver terrain reference/vegetation counts |
| Riftbound.Editor.SupportSurfaceAudit.RunBoth | Saved baseline plus an unsaved, temporary two-platform/four-actor repair preview with bounded native motor steps |

GPU rendering methods require a real graphics device; omit `-nographics`. Native physics, sampled poses, rendered captures, scripted movement, headless online sessions and manual two-PC play are distinct evidence categories.

The support preview does not save the scene. `RunCandidate`/`RunBoth` are preparation methods for the original enabled-capsule baseline and deliberately reject the already-repaired scene; use `Run` to inspect the saved repair. Successful preview motor steps establish a candidate for review, not permission to apply it or proof of encounter/traversal acceptance. `ClosestPoint` containment is reported only for supported primitive/convex shapes; a terrain or non-convex mesh result is not classified as containment.

## Manual acceptance

Test each relevant class/race in offline and host/guest roles. Cover run/walk/backward/strafe/jump, attacks/casts/guard/dodge, death/respawn, mount/dismount and optional companions. Inspect feet, rider hips, actual mount mesh/materials and full action transitions.

Test late join, leaving/rejoining, four-player capacity, ready yes/no/timeout, invalid requests and host departure. Verify no duplicate rewards and no offline-save mutation. Traverse both reference corridors, including bridge deck vs underlying terrain, stairs, slopes, seams and camera clearance.

Record exact source/build hashes and actual results. Prior branch passes do not establish acceptance of a newly combined build. Current integration still needs remote grounding API hookup and a newly verified combined standalone; manual two-PC acceptance is not claimed here.
