# Changelog

## 0.5.0-alpha.2 — 2026-11-27

- Fixed a save-loading stall caused by an oversized Smart Beacons priority file. The save now loads with priorities unavailable for that session and the file is preserved.
- Aligned the file-size limit with the largest set of UTF-8 priority IDs the mod can write.

## 0.5.0-alpha.1 — 2026-11-27

- Added reversible HUD fading, scaling, focus labels, distance and location-based behavior, and category overrides.
- Added overlap-based groups, count indicators, bounded group expansion, and label placement.
- Added Nautilus presets and settings, bindable quick reveal, priority stars and a focused-marker priority action.
- Added per-save priority sidecars, periodic diagnostics, build and package scripts, and GitHub Actions workflows.
- The initial downloadable build is an alpha pending a Windows in-game smoke test. The stock PDA controls remain in use and priority actions use the bindable hotkey.
