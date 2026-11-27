# Implementation and release gates

| Phase | Delivered in this alpha |
|---|---|
| Research and build | Steam branch research, Nautilus and Vortex sources, pinned references, solution and CI |
| Core HUD | Vanilla view tracking and restoration, focus, opacity, icon scale, label reveal and distance logic |
| Configuration | Options, presets, input, validation and transitions |
| Grouping | Screen overlap, stable representatives, expansion, group badges and bounded label layout |
| Advanced | Hotkey-based priority, sidecars, context, and category controls |
| PDA | Stock bulk visibility retained. New priority widgets and custom filters deferred pending game UI and controller validation |
| Packaging | Release DLL and plugin-only Vortex archive. Manual verification is a separate release gate |

Before calling a build stable, complete the smoke test against the current public Steam branch and document the exact game, Nautilus and optional mod versions tested. Use the detailed checklist to investigate regressions, controller issues or visual layout problems.
