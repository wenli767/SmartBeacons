# Smart Beacons

Smart Beacons is a first-playthrough-friendly HUD mod for the original Steam release of Subnautica. It fades and shrinks existing navigation icons away from your view, shows names and distances as you look toward them, and combines overlapping icons into expandable groups. Discovery, exploration, and the PDA's beacon visibility controls continue to follow the game.

**Version 0.5.0-alpha.5** includes the fix for the first in-game HUD setup error and follow-up fixes for hotkey and fade timing. The new HUD integration still needs an in-game check.

## Install

- Use the original Subnautica **public** Steam branch on Windows. Install the [Subnautica BepInEx 5 pack](https://www.nexusmods.com/subnautica/mods/1108) and [Nautilus for original Subnautica, build 1.0.0.57 or newer](https://github.com/SubnauticaModding/Nautilus/releases/tag/1.0.0-pre.57).
- In Vortex, manage Subnautica with the [Subnautica Support extension](https://www.nexusmods.com/site/mods/202). Choose **Install From File**, select `SmartBeacons-v0.5.0-alpha.5.zip`, and enable and deploy it. The installed type should be **BepInEx Plugin**.
- For manual installation, extract the ZIP into the folder containing `Subnautica.exe`. Confirm that the only installed mod file is `Subnautica/BepInEx/plugins/SmartBeacons/SmartBeacons.dll`.

## Settings and controls

Open **Options → Mods → Smart Beacons** for appearance, focus and distance, grouping, context, categories, and diagnostics.

| Preset | Unfocused opacity | Unfocused icon scale | Focus angle | Distance fading |
|---|---:|---:|---:|---|
| Balanced | 25% | 65% | 15° | On |
| Subtle | 55% | 85% | 20° | Off |
| Minimal | 15% | 50% | 10° | On |
| Vanilla | Stock HUD | Stock HUD | Stock HUD | Stock HUD |
| Custom | Your settings | Your settings | Your settings | Your settings |

Balanced uses a 5° focus exit margin. Distance fading starts at 300 m and reaches 15% at 1,500 m. Editing an individual setting selects Custom. Presets and the reset button preserve each save's priorities and the game's input bindings.

- **Quick reveal** defaults to **Left Alt**. Hold it to show eligible on-screen labels and restore stock icon sizes while grouping and layout changes pause. Toggle mode is available under Mods. Menus, text entry, and pause suspend the action.
- **Toggle focused beacon priority** starts unbound. Bind it under **Options → Mod Input → Smart Beacons**, look toward a visible marker, and press it. You can bind either action on keyboard or controller. If one binding conflicts, Smart Beacons skips that binding and leaves the other bindings for that action available.
- Priority destinations retain a full stock icon scale and get a small star. Save normally to retain your changes. You can still hide any destination in the stock PDA Beacon Manager.
- Story and unrecognized or mod-added navigation categories default to Vanilla. **Hidden unless focused** reduces clutter while retaining focus, priority, and quick reveal overrides. Edge indicators keep the game's off-screen behavior.

## Compatibility and troubleshooting

Better Scanner Blips Remake uses a separate resource HUD. Beacon Color Picker and Easy Markers modify related visuals. See [compatibility notes](docs/COMPATIBILITY.md) for inspected integration points and the mods still awaiting live testing.

If the plugin fails to load, check the install path and Nautilus version, then find the first Smart Beacons or Harmony warning in `BepInEx/LogOutput.log`. Enable **Periodic HUD diagnostics** to log a sample at most every ten seconds. Share the relevant log lines and Steam build, along with the affected scenario.

To uninstall, remove the mod through Vortex or delete its own `BepInEx/plugins/SmartBeacons` folder. To remove its optional data as well, delete `BepInEx/config/com.smartbeacons.subnautica.cfg` and the `SmartBeacons.priorities.txt` sidecar in any affected save slot. The sidecar contains only Smart Beacons selections.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [PowerShell 7.2 or newer](https://learn.microsoft.com/powershell/scripting/install/installing-powershell). Run these commands from the repository root.

```powershell
pwsh -NoProfile -File scripts/Build.ps1
pwsh -NoProfile -File scripts/Package.ps1
```

The normal build uses the official modding references and places the ZIP in `artifacts/`. To compile the integration against your installed Steam game assemblies, pass the directory containing `Subnautica.exe`.

```powershell
pwsh -NoProfile -File scripts/Build.ps1 -GamePath 'C:\Program Files (x86)\Steam\steamapps\common\Subnautica'
```

The optional install script copies the Release DLL to your own game folder and supports `-WhatIf`. For smoke testing, follow the [in-game checklist](docs/TEST_PLAN.md). The [technical research](docs/TECHNICAL_RESEARCH.md) records the API and Steam build evidence.
