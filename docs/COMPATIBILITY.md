# Compatibility

| Integration | Evidence | Live status |
|---|---|---|
| Steam original Subnautica public branch | Build 20241558 is the target. The official GameLibs reference pack compiles the plugin. | Game launch and actual asset layout pending. |
| Performance Booster | Its Nexus description covers performance. Source for the current published release was unavailable for this review. Smart Beacons touches the navigation HUD and has no graphics or streaming integration. | Unverified together. |
| Better Scanner Blips Remake | Published source patches `uGUI_ResourceTracker.UpdateBlips`, `uGUI_ResourceTracker.OnDestroy`, and `ResourceTracker.Register`. Smart Beacons works only with `uGUI_Pings`. Map Room camera navigation pings are a distinct category from resource blips. | Source surfaces are separate. Live compatibility pending. |
| CustomBeacons (2025) | Nexus describes custom icons and colors. Smart Beacons uses existing sprites and RGB. Public patch source was unavailable for this review. | Unverified together. |
| Beacon Color Picker | Source patches `uGUI_Pings.OnAdd`, `OnColor`, and PDA row controls. Smart Beacons patches the update, removal and teardown path. | Related presentation is shared. Recoloring during active fades needs a live check. |
| Easy Markers | Source patches text update and PDA listing or controls. Smart Beacons uses the existing text components and leaves PDA rows untouched. | Unverified together. |
| Unknown or modded navigation types | Categories default to Vanilla. Users can opt in through Other. | New enum registrations and game-mod combinations need runtime checks. |

[Performance Booster](https://www.nexusmods.com/subnautica/mods/389) · [Better Scanner Blips Remake source](https://github.com/Ungeziefi/Subnautica-Mods/tree/8c442445a4ff03d3994d9138422e05377b0c650f/New%20Mods/Better%20Scanner%20Blips%20Remake) · [CustomBeacons](https://www.nexusmods.com/subnautica/mods/2609) · [Beacon Color Picker](https://www.nexusmods.com/subnautica/mods/3050) · [Easy Markers](https://github.com/appendixis/Subnautica-EasyMarkers-Mod)

Known alpha limitations include keyboard and controller bindings and text reveal requiring in-game validation. PDA priority controls and category filtering were deferred. There is no diagnostic overlay. CPU overhead and HUD scaling behavior have yet to be measured in the game.
