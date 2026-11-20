# Village tour

The **Riftbound Village Tour** Windows build has a test character profile and a fast-travel menu for the remodel locations.

1. Go through the title screen and enter the world offline.
2. Stand on foot outside combat, close any other game windows, and press **F8**.
3. Choose an arrival point and play with the normal movement and mount controls.
4. Use **Return to start** to go back to where the tour began. **F8**, **Esc**, and **Close** dismiss the menu.

The tour covers both Emberwatch stops, Highspire, Greenhollow, Gor’Kresh, Rustwater, Deathknell, Silverglen, Frostpeak, Westbank, and Bloodrock. The separate product name gives the tour its own character roster, adventure saves, and preferences.

## Build the tour

Use a separate Unity project copy with **Riftbound Village Tour** as its product name. Run `Riftbound.Editor.VillageRemodelPreviewBuild.Run` in batch mode with a unique `-gameplayBuildName`. The builder stages the eleven prefab placements and adds the tour menu during packaging. Only the dedicated tour build gets the menu.
