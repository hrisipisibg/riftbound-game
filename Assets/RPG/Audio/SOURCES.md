# Audio sources and playback

Imported 2026-10-31 from Kenney's official free download pages:

- [RPG Audio](https://kenney.nl/assets/rpg-audio), Kenney Vleugels: knife slices, coin handling, footsteps and book/page foley.
- [Impact Sounds](https://kenney.nl/assets/impact-sounds), Kenney Vleugels: three punch/body impacts and two light metal impacts.
- [UI Audio](https://kenney.nl/assets/ui-audio), Kenney Vleugels: mouse/button clicks.

All three author pages identify the packs as **Creative Commons Zero (CC0)**. Original pack license files are retained beside this note. No paid assets or downloaded preview mixes are included. The 18 original OGG files are unmodified and total 197,310 bytes.

## Runtime mapping

- `AudioEffects`: recorded click, coin and page variations; recorded weapon swishes; movement-distance footsteps while grounded and on foot.
- `GameSound`: recorded body-hit variations and metal blocks. Magic/heal/rune cues retain original procedural synthesis, with softer attacks, filtered noise and smooth release tails.
- Quest completion, discovery and level-up use overlapping bell-like notes rather than hard note switches.
- UI playback is limited to three voices. World playback is limited to eight spatial voices, four starts per frame, and per-cue cooldowns.
- World playback is suppressed during the character menu, pause and the short initial load guard. UI clicks remain available in menus. Teleports and mount movement do not emit walking footsteps.
- The existing master AudioListener setting remains authoritative. The existing SFX, music and ambience controls now scale their corresponding playback paths.
- Zone music resolves the zone name through `ZoneManager.GetZoneName`, so it follows expanded-world boundaries rather than the old coordinate thresholds. Both crossfade voices pause in menus. Zone changes during a fade are picked up afterward.
- Environmental wind and musical cues are generated locally by project code; they are not downloaded recordings.

## Validation

Check the OGG assets import, then test clicks/quest hand-ins, melee hits, held Q blocks, coin loot, walking/running, mounting, pausing and character selection. Confirm SFX/music/ambience sliders and master mute. Runtime listening and final Unity compilation are handled by the integrating build; no claim of subjective audio acceptance is made by this source note.
