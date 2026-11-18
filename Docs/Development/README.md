# Development

Use Unity6000.6.2f1 and the package versions pinned under Packages. Open the expanded world scene for the current game. Avoid running world-generation/import-repair menus on an authored scene.

## Build

The Riftbound editor menu contains Windows build commands. The multiplayer presentation build methods are `Riftbound.Editor.CoopPrefabSetup.BuildMultiplayerPolish01` and `BuildMultiplayerPolish02`. They write complete standalone folders under Builds; distribute the whole folder rather than the executable alone. Existing build directories are protected from accidental replacement by the build helpers.

## Source and assets

Runtime code is under Assets/RPG/Scripts; editor checks and preparation tools are under Assets/RPG/Editor. Scene placement, terrain, prefab identity and `.meta` GUIDs are part of the authored game. Do not recreate actors or regenerate terrain to repair a local issue.

Keep character save IDs, faction/race/quest/enemy/chest keys and serialized references stable. Offline saves and session-only online progress are separate. Cosmetic animation/grounding must not change collision roots, hit authority or rewards.

## Isolated validation

Use a disposable checkout, temporary progress files and unique evidence/build folders. Before first batch startup create an ignored `AnimationFixResult.txt` note stating that automatic repair is suppressed: the legacy first-run hook otherwise can perform a broad import repair. Do not treat that marker as proof a repair ran.

Read-only world audit helpers require an ignored `.coordination/world-validation-context.md` file in the isolated checkout. Write-capable world tools require a separate local edit context, and terrain repair requires a local scene-release record; those markers are deliberately not distributed. Do not manufacture authorization to bypass the guards. See [validation](../VALIDATION.md) for public check entrypoints and acceptance boundaries.

Commit only source/assets/documentation needed for a change. Local workspace notes and generated diagnostics are excluded by Git. Preserve third-party licensing and factual validation limitations.
