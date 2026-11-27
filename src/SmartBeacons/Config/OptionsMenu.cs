using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Nautilus.Options;
using UnityEngine.UI;

namespace SmartBeacons.Config;

using Core;

internal sealed class OptionsMenu : ModOptions
{
    private static readonly string[] PresetNames = { "Vanilla", "Subtle", "Balanced", "Minimal", "Custom" };
    private static readonly string[] RevealNames = { "Hold", "Toggle" };
    private static readonly string[] BehaviorNames = { "Vanilla", "Smart", "Reduced", "Hidden unless focused" };
    private static readonly string[] CategoryNames =
        { "Player beacons", "Lifepods", "Seamoth", "Prawn Suit", "Cyclops", "Scanner cameras", "Story signals", "Other / modded signals" };

    private readonly ConfigFile config;
    private readonly ManualLogSource log;
    private readonly List<Func<bool>> writeEntries = new();
    private readonly List<Action<uGUI_TabbedControlsPanel, int, List<Action>>> buildControls = new();
    private bool ioWarning;

    public Settings Current { get; private set; } = new();
    public event Action Changed;

    public OptionsMenu(ConfigFile config, ManualLogSource log) : base("Smart Beacons")
    {
        this.config = config;
        this.log = log;
        config.SaveOnConfigSet = false;

        const string general = "Smart Beacons";
        Heading(general);
        Toggle(general, "Enabled", "Enable Smart Beacons", s => s.Enabled, (s, v) => s.Enabled = v);
        Choice(general, "Preset", "Preset", PresetNames, s => s.Preset, (s, v) => s.Preset = v, apply: (s, v) => s.ApplyPreset(v));
        Choice(general, "RevealMode", "Quick reveal mode", RevealNames, s => s.RevealMode, (s, v) => s.RevealMode = v);
        buildControls.Add((panel, tab, sync) => panel.AddButton(tab, "Reset Smart Beacons settings", () =>
            Change(() => Current = new Settings(), sync)));

        const string appearance = "Beacon appearance";
        Heading(appearance);
        Slider(appearance, "UnfocusedOpacity", "Unfocused opacity", s => s.UnfocusedOpacity, (s, v) => s.UnfocusedOpacity = v, 0, 100, 1, "{0:F0}%");
        Slider(appearance, "DistantOpacity", "Minimum distant opacity", s => s.DistantOpacity, (s, v) => s.DistantOpacity = v, 0, 100, 1, "{0:F0}%");
        Slider(appearance, "UnfocusedSize", "Unfocused icon size", s => s.UnfocusedSize, (s, v) => s.UnfocusedSize = v, 35, 100, 1, "{0:F0}%");
        Slider(appearance, "FocusedSize", "Focused icon size", s => s.FocusedSize, (s, v) => s.FocusedSize = v, 50, 150, 1, "{0:F0}%");
        Slider(appearance, "TransitionSeconds", "Fade transition", s => s.TransitionSeconds, (s, v) => s.TransitionSeconds = v, 0, 1, .05f, "{0:F2} s");
        Toggle(appearance, "FocusOnlyLabels", "Show labels only when focused", s => s.FocusOnlyLabels, (s, v) => s.FocusOnlyLabels = v);

        const string focus = "Focus and distance";
        Heading(focus);
        Slider(focus, "FocusAngle", "Focus angle", s => s.FocusAngle, (s, v) => s.FocusAngle = v, 3, 45, 1, "{0:F0}°");
        Slider(focus, "FocusHysteresis", "Extra angle to leave focus", s => s.FocusHysteresis, (s, v) => s.FocusHysteresis = v, 0, 15, 1, "{0:F0}°");
        Toggle(focus, "DistanceFade", "Distance fading", s => s.DistanceFade, (s, v) => s.DistanceFade = v);
        Slider(focus, "FadeStart", "Distance fading starts", s => s.FadeStart, (s, v) => s.FadeStart = v, 0, 2900, 50, "{0:F0} m");
        Slider(focus, "FadeEnd", "Distance fading ends", s => s.FadeEnd, (s, v) => s.FadeEnd = v, 50, 3000, 50, "{0:F0} m");

        const string grouping = "Grouping";
        Heading(grouping);
        Toggle(grouping, "Enabled", "Automatic grouping", s => s.Grouping, (s, v) => s.Grouping = v);
        Slider(grouping, "GroupPixels", "Grouping threshold at 1080p", s => s.GroupPixels, (s, v) => s.GroupPixels = v, 16, 96, 1, "{0:F0} px");
        Toggle(grouping, "ExpandGroups", "Expand focused groups", s => s.ExpandGroups, (s, v) => s.ExpandGroups = v);
        Toggle(grouping, "AvoidLabelOverlap", "Prevent label overlap", s => s.AvoidLabelOverlap, (s, v) => s.AvoidLabelOverlap = v);
        Toggle(grouping, "GroupCount", "Show number of grouped signals", s => s.GroupCount, (s, v) => s.GroupCount = v);

        const string context = "Context and categories";
        Heading(context);
        Toggle(context, "ReduceInBase", "Reduce clutter inside bases", s => s.ReduceInBase, (s, v) => s.ReduceInBase = v);
        Toggle(context, "ReduceInLifepod", "Reduce clutter inside lifepods", s => s.ReduceInLifepod, (s, v) => s.ReduceInLifepod = v);
        Toggle(context, "ReduceInVehicle", "Reduce clutter inside vehicles", s => s.ReduceInVehicle, (s, v) => s.ReduceInVehicle = v);
        for (int i = 0; i < CategoryNames.Length; i++)
        {
            int index = i;
            Choice(context, ((Category)i).ToString(), CategoryNames[i], BehaviorNames,
                s => s.Categories[index], (s, v) => s.Categories[index] = v);
        }

        const string advanced = "Advanced";
        Heading(advanced);
        Toggle(advanced, "Debug", "Debug logging", s => s.Debug, (s, v) => s.Debug = v);
        Toggle(advanced, "Diagnostics", "Periodic HUD diagnostics", s => s.Diagnostics, (s, v) => s.Diagnostics = v);
        Current.Validate();
        Persist(logCorrections: true);
    }

    public override void BuildModOptions(uGUI_TabbedControlsPanel panel, int tab, IReadOnlyCollection<OptionItem> options)
    {
        var refresh = new List<Action>();
        foreach (var build in buildControls)
            build(panel, tab, refresh);
    }

    private void Heading(string title) => buildControls.Add((panel, tab, sync) => panel.AddHeading(tab, title));

    private void Toggle(string section, string key, string label, Func<Settings, bool> get, Action<Settings, bool> set)
    {
        Bind(section, key, label, get, set);
        buildControls.Add((panel, tab, sync) =>
        {
            Toggle toggle = panel.AddToggleOption(tab, label, get(Current),
                value => Edit(set, value, sync));
            sync.Add(() => { if (toggle) toggle.SetIsOnWithoutNotify(get(Current)); });
        });
    }

    private void Slider(string section, string key, string label, Func<Settings, float> get, Action<Settings, float> set,
        float min, float max, float step, string format)
    {
        float defaultValue = get(Current);
        Bind(section, key, label, get, set);
        buildControls.Add((panel, tab, sync) =>
        {
            var row = panel.AddSliderOption(tab, label, get(Current), min, max, defaultValue, step,
                value => Edit(set, value, sync), SliderLabelMode.Float, format);
            Slider slider = row ? row.GetComponentInChildren<Slider>(true) : null;
            if (slider)
                sync.Add(() => { if (slider) slider.value = get(Current); });
        });
    }

    private void Choice<T>(string section, string key, string label, string[] names,
        Func<Settings, T> get, Action<Settings, T> set, Action<Settings, T> apply = null) where T : struct, Enum
    {
        Bind(section, key, label, get, set);
        buildControls.Add((panel, tab, sync) =>
        {
            var row = panel.AddDropdownOption(tab, label, names, Convert.ToInt32(get(Current)),
                index => Edit(apply ?? set, (T)Enum.ToObject(typeof(T), index), sync, apply != null));
            var dropdown = row ? row.GetComponentInChildren<TMPro.TMP_Dropdown>(true) : null;
            var legacyDropdown = row ? row.GetComponentInChildren<Dropdown>(true) : null;
            sync.Add(() =>
            {
                int index = Convert.ToInt32(get(Current));
                if (dropdown) dropdown.SetValueWithoutNotify(index);
                else if (legacyDropdown) legacyDropdown.SetValueWithoutNotify(index);
            });
        });
    }

    private void Bind<T>(string section, string key, string label, Func<Settings, T> get, Action<Settings, T> set)
    {
        ConfigEntry<T> entry = config.Bind(section, key, get(Current), label);
        set(Current, entry.Value);
        writeEntries.Add(() =>
        {
            T value = get(Current);
            if (EqualityComparer<T>.Default.Equals(entry.Value, value))
                return false;
            entry.Value = value;
            return true;
        });
    }

    private bool refreshing;

    private void Edit<T>(Action<Settings, T> set, T value, List<Action> sync, bool preset = false)
    {
        if (refreshing)
            return;
        Change(() =>
        {
            set(Current, value);
            if (!preset)
                Current.Preset = Preset.Custom;
        }, sync);
    }

    private void Change(Action apply, List<Action> sync)
    {
        apply();
        Current.Validate();
        Persist();
        refreshing = true;
        try
        {
            foreach (Action refresh in sync)
                refresh();
        }
        finally
        {
            refreshing = false;
            Changed?.Invoke();
        }
    }

    private void Persist(bool logCorrections = false)
    {
        bool changed = false;
        foreach (var write in writeEntries)
            changed |= write();
        if (logCorrections && changed)
            log.LogWarning("Smart Beacons corrected invalid configuration values.");

        try
        {
            config.Save();
            ioWarning = false;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            if (!ioWarning) log.LogWarning($"Could not save Smart Beacons settings: {ex.Message}");
            ioWarning = true;
        }
    }
}
