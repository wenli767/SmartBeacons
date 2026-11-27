# Changelog

## 0.5.0-alpha.4 — 2026-11-27

- Moved HUD styling to Subnautica's managed pre-canvas callbacks after Harmony failed to compile the patch for the vanilla ping update on build 1.22.83031.
- Added a log entry when the vanilla navigation HUD is found. Periodic diagnostics now show how many vanilla markers were available and how many Smart Beacons styled.

## 0.5.0-alpha.3 — 2026-11-27

- Kept working hotkey bindings active when another binding for the same action conflicts.
- Made each priority press usable once, even if the HUD updates twice in a frame.
- Cleared custom badges when the vanilla HUD update is skipped and prevented fading text from lingering past vanilla label visibility.

## 0.5.0-alpha.2 — 2026-11-27

- Fixed a save-loading stall caused by an oversized Smart Beacons priority file. The save now loads with priorities unavailable for that session and the file is preserved.
- Aligned the file-size limit with the largest set of UTF-8 priority IDs the mod can write.

## 0.5.0-alpha.1 — 2026-11-27

- Added reversible HUD fading, scaling, focus labels, distance and location-based behavior, and category overrides.
- Added overlap-based groups, count indicators, bounded group expansion, and label placement.
- Added Nautilus presets and settings, bindable quick reveal, priority stars and a focused-marker priority action.
- Added per-save priority sidecars, periodic diagnostics, build and package scripts, and GitHub Actions workflows.
- The initial downloadable build is an alpha pending a Windows in-game smoke test. The stock PDA controls remain in use and priority actions use the bindable hotkey.
