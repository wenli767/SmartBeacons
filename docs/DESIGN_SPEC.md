# Design decisions

## Visibility precedence

1. Require a visible vanilla HUD owner, a drawable existing view, and a live, initialized ping.
2. Honor the ping's native manual visibility flag.
3. Pass through when disabled, using the Vanilla preset, or when a category is explicitly set to Vanilla.
4. Quick reveal shows eligible on-screen labels, uses the current stock size, and skips group and label layout changes.
5. Priority gives full opacity to eligible on-screen icons and labels, keeps stock scale, and excludes the marker from groups.
6. Focus gives full eligible opacity and uses the configured focused size. Only one marker gets primary focus.
7. Apply the category's Smart, Reduced, or Hidden-unless-focused behavior.
8. Apply the optional location multiplier.
9. Interpolate ordinary opacity over the configured distance range.
10. Set the ordinary icon scale and label policy and interpolate appearance with unscaled time.
11. Group overlapping icons and lay out labels without changing their world positions.

Priority and quick reveal leave vanilla edge indications intact. Label overlap avoidance can suppress lower-ranked ordinary and expanded labels when bounded offsets run out. Priority and the primary focused label keep their text in that case, so two exceptionally dense priority labels can still overlap.

## Placement and lifetime

A single existing navigation view supplies the collapsed image. A pooled text badge displays the number of members. A 10 Hz screen-space overlap pass uses a sorted sweep and connected components. Stable IDs break ties, and established groups use a 22% larger separation threshold. Membership is checked against visibility on every HUD update. Expansion distributes markers within a small local area and label placement tries a bounded set of offsets. Screen positions come from the actual vanilla rectangles and the grouping threshold uses the HUD canvas scale.

The plugin restores properties that still match its last write before the original update runs. This keeps vanilla interpolation and captures the latest native presentation even when colors or labels change. Feature disable, HUD teardown, and save changes clear cached layout and pooled objects. An unexpected HUD integration error logs once and reverts to vanilla behavior for the running session. Input and persistence have their own error handling.

Priorities live in the game's save workspace so a newly created or different slot starts with its own sidecar state. IDs come from serialized `PingInstance.Id`. Changes commit with the normal save operation. Names and mutable colors are never used as identity.
