using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace SmartBeacons.Integration;

using Core;
using Persistence;

internal sealed class HudController
{
    private const float LayoutInterval = .10f;

    private readonly HudBindings bindings;
    private readonly GameInputAdapter input;
    private readonly PriorityStore priorities;
    private readonly ManualLogSource log;
    private readonly Dictionary<string, HudMarker> tracked = new(StringComparer.Ordinal);
    private readonly List<HudMarker> active = new();
    private readonly List<HudMarker> painted = new();
    private readonly List<HudMarker> ordered = new();
    private readonly List<string> expired = new();
    private readonly List<GroupPoint> points = new();
    private readonly Dictionary<string, Group> groupStates = new(StringComparer.Ordinal);
    private readonly FocusSelection focus = new();
    private readonly ScreenGroups groups = new();
    private readonly LabelLayout labels = new();
    private readonly MarkerBadges badges = new();
    private readonly HudFrameTime frameTime = new();
    private readonly Dictionary<string, ScreenPoint> labelOffsets = new(StringComparer.Ordinal);
    private uGUI_Pings owner;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private RectTransform canvasRoot;
    private int generation;
    private float nextLayout, nextDiagnostic;
    private bool layoutDirty = true, wasRevealing;
    private Vector2 screenSize;
    private float previousScale;
    private int collapsedGroups;
    private int focusedCount, priorityCount;

    public HudController(HudBindings bindings, GameInputAdapter input, PriorityStore priorities, ManualLogSource log)
    {
        this.bindings = bindings;
        this.input = input;
        this.priorities = priorities;
        this.log = log;
    }

    public void Restore()
    {
        Exception firstFailure = null;
        foreach (HudMarker marker in painted)
        {
            try { marker.Restore(bindings); }
            catch (Exception ex) { firstFailure ??= ex; }
        }
        painted.Clear();
        if (firstFailure != null)
            throw new InvalidOperationException("Failed to restore one or more navigation views.", firstFailure);
    }

    public void Clear()
    {
        try { Restore(); }
        finally { ClearState(); }
    }

    private void ClearState()
    {
        focus.Clear();
        groups.Clear();
        groupStates.Clear();
        labelOffsets.Clear();
        input?.Reset();
        badges.Clear();
        tracked.Clear();
        active.Clear();
        ordered.Clear();
        points.Clear();
        expired.Clear();
        owner = null;
        canvas = null;
        canvasScaler = null;
        canvasRoot = null;
        wasRevealing = false;
        layoutDirty = true;
    }

    public void SettingsChanged(bool activeNow)
    {
        if (!activeNow)
            Clear();
        else
        {
            input?.Reset();
            layoutDirty = true;
        }
    }

    public void Remove(string id)
    {
        if (id == null || !tracked.TryGetValue(id, out HudMarker marker))
            return;
        marker.Restore(bindings);
        badges.HideUnder(marker.IconTransform);
        tracked.Remove(id);
        layoutDirty = true;
    }

    private void SuspendPresentation()
    {
        badges.Begin();
        badges.End();
        focus.Clear();
        input?.Reset();
        layoutDirty = true;
    }

    public void Update(uGUI_Pings hud, Settings settings)
    {
        if (!settings.Active)
            return;
        if (!hud)
        {
            if (!ReferenceEquals(owner, null))
                Clear();
            return;
        }
        Player player = Player.main;
        Camera camera = MainCamera.camera;
        if (!player || !camera || !hud.isActiveAndEnabled || !hud.pingCanvas ||
            !hud.canvasGroup || hud.canvasGroup.alpha <= .0001f || !bindings.IsVisible(hud))
        {
            SuspendPresentation();
            return;
        }

        if (!ReferenceEquals(hud, owner) || canvasRoot != hud.pingCanvas)
        {
            Clear();
            owner = hud;
            canvasRoot = hud.pingCanvas;
            Canvas nearest = canvasRoot.GetComponentInParent<Canvas>();
            canvas = nearest ? nearest.rootCanvas : null;
            canvasScaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;
            log.LogInfo("Smart Beacons acquired the vanilla navigation HUD.");
        }

        input?.Read(player, settings);
        Dictionary<string, uGUI_Ping> gameMarkers = bindings.Markers(hud);
        if (gameMarkers == null)
        {
            SuspendPresentation();
            return;
        }
        long start = settings.Diagnostics ? Stopwatch.GetTimestamp() : 0;
        Camera uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        float now = Time.unscaledTime;
        float screenScale = Screen.height / 1080f;
        float hudScale = canvas ? canvas.scaleFactor : 1f;
        bool canvasScalesWithScreen = canvasScaler &&
            canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize;
        float scale = Mathf.Clamp(canvasScalesWithScreen ? hudScale : hudScale * screenScale, .4f, 5f);
        var size = new Vector2(Screen.width, Screen.height);
        if (size != screenSize || !Mathf.Approximately(scale, previousScale))
        {
            layoutDirty = true;
            screenSize = size;
            previousScale = scale;
        }

        Location location = GetLocation(player);
        string previousFocus = focus.Selected;
        Collect(gameMarkers, camera, uiCamera, settings);
        if (input?.ConsumePriorityPress() == true)
            ToggleCurrentPriority(settings);
        bool reveal = input?.IsRevealing == true;
        bool layoutTick = layoutDirty || now >= nextLayout || reveal != wasRevealing || previousFocus != focus.Selected;
        layoutDirty = false;
        wasRevealing = reveal;
        Evaluate(settings, location, reveal);

        if (layoutTick)
            RefreshGroups(settings, reveal, scale);
        PresentGroups(settings, reveal, scale);
        if (layoutTick)
        {
            nextLayout = now + LayoutInterval;
            RefreshLabels(settings, reveal, uiCamera, scale, size);
        }
        Draw(settings, uiCamera, reveal);
        if (settings.Diagnostics && now >= nextDiagnostic)
        {
            nextDiagnostic = now + 10f;
            double elapsed = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            log.LogInfo($"HUD: {gameMarkers.Count} vanilla markers, {painted.Count} styled, {focusedCount} focused, {priorityCount} priority, " +
                $"{collapsedGroups} collapsed groups; sampled update {elapsed:F3} ms.");
        }
    }

    private void Collect(Dictionary<string, uGUI_Ping> gameMarkers, Camera camera, Camera uiCamera, Settings settings)
    {
        generation++;
        int seenCount = 0;
        active.Clear();
        focus.Begin();
        var cone = new FocusCone(settings.FocusAngle, settings.FocusHysteresis);
        Transform cameraTransform = camera.transform;
        Vector3 cameraPosition = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;
        foreach (var entry in gameMarkers)
        {
            string id = entry.Key;
            uGUI_Ping view = entry.Value;
            if (string.IsNullOrEmpty(id) || !view)
                continue;
            if (!tracked.TryGetValue(id, out HudMarker marker) || marker.View != view || !marker.Ping)
            {
                PingInstance ping = PingManager.Get(id);
                if (!ping)
                    continue;
                tracked[id] = marker = new HudMarker(id, view, ping);
                layoutDirty = true;
            }

            marker.LastSeen = generation;
            seenCount++;
            bool previouslyActive = marker.Point.Depth > 0f || marker.Meters > 0f;
            bool previouslyOnScreen = marker.Point.OnScreen;
            marker.Point = default;
            marker.Meters = 0f;
            if (!marker.Active(bindings))
            {
                if (previouslyActive) layoutDirty = true;
                marker.ResetMotion();
                continue;
            }

            Vector3 position = marker.Ping.GetPosition();
            Vector3 relative = position - cameraPosition;
            float squaredDistance = relative.sqrMagnitude;
            if (!Number.Finite(squaredDistance) || squaredDistance < .000001f)
            {
                if (previouslyActive) layoutDirty = true;
                continue;
            }
            marker.Meters = Mathf.Sqrt(squaredDistance);
            Vector3 viewport = camera.WorldToViewportPoint(position);
            marker.Point = new ViewPoint(viewport.x, viewport.y, viewport.z);
            marker.Screen = RectTransformUtility.WorldToScreenPoint(uiCamera, marker.IconTransform.position);
            marker.Alignment = FocusCone.Alignment(Vector3.Dot(forward, relative), marker.Meters);

            marker.Category = Classify(marker.Ping.pingType);
            marker.Priority = priorities.Contains(id);
            active.Add(marker);
            if (!previouslyActive || previouslyOnScreen != marker.Point.OnScreen)
                layoutDirty = true;
            Behavior category = settings.Categories[(int)marker.Category];
            if (category != Behavior.Vanilla)
                focus.Consider(id, marker.Point, marker.Alignment, cone);
        }
        focus.Commit();

        if (tracked.Count != seenCount)
        {
            expired.Clear();
            foreach (var marker in tracked)
            {
                if (marker.Value.LastSeen != generation)
                    expired.Add(marker.Key);
            }
            foreach (string id in expired)
            {
                tracked.Remove(id);
                layoutDirty = true;
            }
        }
    }

    private void ToggleCurrentPriority(Settings settings)
    {
        string id = focus.Selected;
        if (id == null || !tracked.TryGetValue(id, out HudMarker marker) || marker.Ping.Id != id ||
            settings.Categories[(int)marker.Category] == Behavior.Vanilla || !priorities.Toggle(id))
            return;
        marker.Priority = priorities.Contains(id);
        layoutDirty = true;
        if (settings.Debug)
            log.LogDebug($"Focused navigation signal priority changed to {marker.Priority}.");
    }

    private void Evaluate(Settings settings, Location location, bool reveal)
    {
        focusedCount = 0;
        priorityCount = 0;
        foreach (HudMarker marker in active)
        {
            bool focused = marker.Id == focus.Selected;
            var ping = new VisibilityInput(true, marker.Ping.visible, reveal, marker.Priority,
                focused, marker.Category, location, marker.Meters);
            marker.Visual = Visibility.Evaluate(settings, ping);
            marker.Expanded = marker.Collapsed = false;
            marker.Spread = default;
            marker.GroupCount = 0;
            if (focused) focusedCount++;
            if (marker.Priority) priorityCount++;
        }
    }

    private bool EligibleForGroup(HudMarker marker, Settings settings) =>
        marker.Visual.CanGroup(marker.Priority, marker.Id == focus.Selected, settings.ExpandGroups) &&
        marker.Point.OnScreen && marker.HasVisibleIcon(bindings) &&
        Number.Finite(marker.Screen.x) && Number.Finite(marker.Screen.y);

    private void RefreshGroups(Settings settings, bool reveal, float scale)
    {
        if (reveal || !settings.Grouping)
        {
            groups.Clear();
            return;
        }
        points.Clear();
        foreach (HudMarker marker in active)
        {
            if (EligibleForGroup(marker, settings))
                points.Add(new GroupPoint(marker.Id, new ScreenPoint(marker.Screen.x, marker.Screen.y)));
        }
        groups.Update(points, settings.GroupPixels * scale);
    }

    private void PresentGroups(Settings settings, bool reveal, float scale)
    {
        groupStates.Clear();
        collapsedGroups = 0;
        if (reveal || !settings.Grouping)
            return;

        ordered.Clear();
        foreach (HudMarker marker in active)
        {
            if (!EligibleForGroup(marker, settings))
                continue;
            string groupId = groups.GroupOf(marker.Id);
            if (groupId == null)
                continue;
            ordered.Add(marker);
            groupStates.TryGetValue(groupId, out Group group);
            if (group.First == null || string.CompareOrdinal(marker.Id, group.First) < 0)
                group.First = marker.Id;
            group.Count++;
            group.Expanded |= marker.Id == focus.Selected;
            groupStates[groupId] = group;
        }

        // Sorting by persistent id keeps the ordering independent of HUD pooling and camera motion.
        ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        foreach (HudMarker marker in ordered)
        {
            string id = groups.GroupOf(marker.Id);
            Group group = groupStates[id];
            if (group.Count < 2)
                continue;
            if (group.Expanded && settings.ExpandGroups)
            {
                marker.Expanded = true;
                marker.Spread = Spread(group.Placed, group.Count, scale);
                marker.Visual = new Visual(Presentation.Emphasized, scale: settings.FocusedSize / 100f);
                group.Placed++;
                groupStates[id] = group;
            }
            else if (marker.Id == group.First)
            {
                marker.GroupCount = group.Count;
                collapsedGroups++;
            }
            else
            {
                marker.Collapsed = true;
            }
        }
    }

    private static ScreenPoint Spread(int index, int count, float scale)
    {
        if (count == 2)
            return new ScreenPoint((index == 0 ? -1f : 1f) * 13f * scale, 0f);
        if (index >= 15)
            return default;
        int columns = 3;
        float x = (index % columns - 1) * 23f * scale;
        int row = index / columns;
        float y = ((row + 1) / 2) * (row % 2 == 0 ? -17f : 17f) * scale;
        return new ScreenPoint(x, y);
    }

    private void RefreshLabels(Settings settings, bool reveal, Camera uiCamera, float scale, Vector2 size)
    {
        if (reveal || !settings.AvoidLabelOverlap)
        {
            labelOffsets.Clear();
            return;
        }
        labels.Clear();
        ordered.Clear();
        var screen = new ScreenRect(2f, 2f, size.x - 4f, size.y - 4f);
        foreach (HudMarker marker in active)
        {
            if (!marker.CanShowLabels || marker.Collapsed)
                continue;
            if (marker.Visual.Mode == Presentation.Vanilla)
            {
                if (marker.View.GetTextAlpha() > .05f && marker.TryLabelRect(uiCamera, out ScreenRect stock))
                    labels.Reserve(stock.Pad(2f * scale));
            }
            else if (marker.Visual.LabelOpacity > 0f)
                ordered.Add(marker);
        }
        ordered.Sort((a, b) =>
        {
            int rank = LabelRank(b).CompareTo(LabelRank(a));
            return rank != 0 ? rank : string.CompareOrdinal(a.Id, b.Id);
        });

        expired.Clear();
        foreach (var offset in labelOffsets)
        {
            if (!tracked.ContainsKey(offset.Key))
                expired.Add(offset.Key);
        }
        foreach (string id in expired)
            labelOffsets.Remove(id);

        foreach (HudMarker marker in ordered)
        {
            if (!marker.TryLabelRect(uiCamera, out ScreenRect bounds))
            {
                labelOffsets.Remove(marker.Id);
                continue;
            }
            var padded = bounds.Move(marker.Spread).Pad(2f * scale);
            labelOffsets.TryGetValue(marker.Id, out ScreenPoint preferred);
            if (labels.Place(padded, screen, preferred, scale, out ScreenPoint offset))
                labelOffsets[marker.Id] = offset;
            else if (marker.Priority || marker.Id == focus.Selected)
            {
                labelOffsets.Remove(marker.Id);
                labels.Reserve(padded);
            }
            else
                labelOffsets[marker.Id] = new ScreenPoint(float.PositiveInfinity, 0f);
        }
    }

    private static int LabelRank(HudMarker marker) => marker.Priority ? 4 :
        marker.Visual.Mode == Presentation.Emphasized && !marker.Expanded ? 3 : marker.Expanded ? 2 : 1;

    private void Draw(Settings settings, Camera uiCamera, bool reveal)
    {
        badges.Begin();
        // Unity can render the canvases again after a forced layout in the same frame.
        float dt = frameTime.Advance(Time.frameCount, Time.unscaledDeltaTime);
        foreach (HudMarker marker in active)
        {
            marker.LabelOffset = settings.AvoidLabelOverlap && !reveal &&
                labelOffsets.TryGetValue(marker.Id, out ScreenPoint offset) ? offset : default;
            if (marker.Visual.Mode is Presentation.Vanilla or Presentation.Unavailable)
            {
                marker.ResetMotion();
                continue;
            }
            painted.Add(marker); // Also restore the current marker if any subsequent call throws.
            marker.Apply(bindings, uiCamera, settings, dt);
            if (marker.Priority && marker.Point.OnScreen && !reveal && marker.CurrentIconAlpha > .01f)
                badges.Draw(marker, 0, bindings.IconColor(marker.View));
            else if (marker.GroupCount > 1 && settings.GroupCount && marker.CurrentIconAlpha > .01f)
                badges.Draw(marker, marker.GroupCount, bindings.IconColor(marker.View));
        }
        badges.End();
    }

    private static Location GetLocation(Player player)
    {
        if (player.currentEscapePod)
            return Location.Lifepod;
        if (player.GetVehicle())
            return Location.Vehicle;
        SubRoot sub = player.currentSub;
        if (!sub)
            return Location.Outside;
        if (sub.isBase)
            return Location.Base;
        return sub.isCyclops ? Location.Vehicle : Location.Outside;
    }

    private static Category Classify(PingType type) => type switch
    {
        PingType.Beacon => Category.Beacon,
        PingType.Lifepod => Category.Lifepod,
        PingType.Seamoth => Category.Seamoth,
        PingType.Exosuit => Category.Prawn,
        PingType.Cyclops => Category.Cyclops,
        PingType.MapRoomCamera => Category.Camera,
        PingType.Signal or PingType.Sunbeam or PingType.Rocket => Category.Story,
        _ => Category.Other
    };

    private struct Group
    {
        public string First;
        public int Count, Placed;
        public bool Expanded;
    }
}
