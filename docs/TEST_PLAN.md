# In-game checklist

## First-pass smoke test

1. Deploy the ZIP in Vortex, launch the game, and confirm **Options → Mods → Smart Beacons** appears. Load an existing save. Check `BepInEx/LogOutput.log` if the section is missing.
2. Place or use three beacons, with two on nearly the same sightline and one apart. Confirm ordinary icons shrink and fade, the nearby pair groups, and looking toward each reveals its name and distance. Look away to observe the transition and group collapse.
3. Hold **Left Alt** to reveal eligible pings. Hide a beacon in the stock PDA manager, close the PDA, and verify that quick reveal still leaves that beacon hidden.
4. Bind **Toggle focused beacon priority** in **Mod Input**, look toward a visible beacon and mark it. Check its star and full presentation. Rename and recolor it, save, reload the same slot and check its priority again.
5. Switch off **Enable Smart Beacons** and verify that opacity, size, labels, placement and HUD decorations return to stock behavior. Check the BepInEx log for errors.

## Follow-up checks

- Enter and exit a seabase, the lifepod, Seamoth, Prawn Suit, Cyclops and Map Room camera view. Verify context settings separately and check stock navigation while using a vehicle.
- Confirm Scanner Room resource markers behave as before with and without Better Scanner Blips Remake. Recolor a marker if a color mod is installed and check that tint survives all Smart Beacons transitions.
- Test Hold and Toggle through the PDA, menus, rename fields, pause and the console. Rebind each action on a controller and check the PDA's navigation and stock bulk visibility toggle.
- If Mod Input allows a conflicting assignment, give Quick Reveal a working keyboard binding and a conflicting controller binding. Confirm the keyboard still works and the conflicting control keeps its game action. Rebind the controller control and confirm both devices work.
- Load a second save, then return to the first. Include two beacons with the same name, and verify priorities follow the original markers across renames and save switches.
- At 1080p, 1440p, 4K and ultrawide sizes, test dense markers and changes to HUD scaling. Compare 20, 50 and 100 enabled signals with and without the plugin. Use the Unity profiler if available and record the build and mod list with any performance results.

Automated logic tests use synthetic camera, visibility and layout data. They cover policy and persistence and do not load Unity. Game rendering, input hooks and Vortex deployment require the smoke test above.
