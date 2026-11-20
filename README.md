# Riftbound

A 3D fantasy RPG built with Unity for a diploma project and competition. Explore connected settlements, complete quests, choose a race and class, and play solo or in an online party of up to four players.

## Run the project

1. Install Git LFS and run `git lfs install`.
2. Clone the repository, then run `git lfs pull`.
3. Open the project in Unity Hub using **Unity6000.6.2f1**.
4. Open `Assets/RPG/Scenes/Outpost-Expanded.unity` and press Play.
5. Select a character and enter the world. Use the Game tab for keyboard and mouse input.

## Play

| Action | Control |
| --- | --- |
| Move / camera / jump | WASD / mouse / Space |
| Slow walk | C |
| Interact / select target | E / Tab |
| Core abilities |1â€“4 |
| Summon or dismiss mount / select mount | Z / V |
| Inventory / quest journal / map | I / J / M |
| Group menu / companions | O / G |
| Map ping / ready check | F6 / F7 |
| Close panel / settings | Escape |

Characters start solo; companions are optional. Online parties use a private invite code, shared only with the people joining. Online progress is currently session-only and does not replace offline character saves. Host migration is not supported.

The current world includes Emberwatch, Highspire, Westbank, Greenhollow and the Reaver camp. Four classes and six races have character variants; animation and mount presentation are still being refined.

## Documentation

- [Gameplay](Docs/GAMEPLAY.md)
- [World zones](Docs/WORLD_ZONES.md)
- [Lore](Docs/Lore/RIFTBOUND_LORE_BIBLE.md)
- [Online setup](Docs/ONLINE_SETUP.md)
- [Development and builds](Docs/Development/README.md)
- [Validation](Docs/VALIDATION.md)
- [Roadmap](Docs/ROADMAP.md)
- [Release plan](Docs/RELEASE_PLAN.md)

## Contributing

Use a separate checkout and branch per task. Keep models and textures in Git LFS, commit Unity `.meta` files with their assets, and coordinate scene/prefab ownership before editing. Builds, caches, saves and local workspace records are excluded from Git.

## Assets

Attribution and source records remain under `Assets/RPG/`, including [character asset records](Assets/RPG/CHARACTER_ASSETS.md). Some imported assets still need source/license review before public redistribution. Existing license and attribution files must stay with their assets.
