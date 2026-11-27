using System;

namespace SmartBeacons.Core;

internal enum Preset { Vanilla, Subtle, Balanced, Minimal, Custom }
internal enum RevealMode { Hold, Toggle }
internal enum Behavior { Vanilla, Smart, Reduced, Hidden }
internal enum Category { Beacon, Lifepod, Seamoth, Prawn, Cyclops, Camera, Story, Other }
internal enum Location { Outside, Base, Lifepod, Vehicle }

internal sealed class Settings
{
    public bool Enabled = true;
    public Preset Preset = Preset.Balanced;
    public RevealMode RevealMode = RevealMode.Hold;
    public float UnfocusedOpacity = 25f;
    public float DistantOpacity = 15f;
    public float UnfocusedSize = 65f;
    public float FocusedSize = 100f;
    public float TransitionSeconds = 0.20f;
    public bool FocusOnlyLabels = true;
    public float FocusAngle = 15f;
    public float FocusHysteresis = 5f;
    public bool DistanceFade = true;
    public float FadeStart = 300f;
    public float FadeEnd = 1500f;
    public bool Grouping = true;
    public float GroupPixels = 40f;
    public bool ExpandGroups = true;
    public bool AvoidLabelOverlap = true;
    public bool GroupCount = true;
    public bool ReduceInBase = true;
    public bool ReduceInLifepod = true;
    public bool ReduceInVehicle;
    public bool Debug = false;
    public bool Diagnostics = false;
    public Behavior[] Categories =
    {
        Behavior.Smart, Behavior.Smart, Behavior.Smart, Behavior.Smart,
        Behavior.Smart, Behavior.Smart, Behavior.Vanilla, Behavior.Vanilla
    };

    public bool Active => Enabled && Preset != Preset.Vanilla;

    public void ApplyPreset(Preset next)
    {
        Preset = next;
        if (next is Preset.Custom or Preset.Vanilla)
            return;

        (UnfocusedOpacity, UnfocusedSize, FocusAngle, DistanceFade) = next switch
        {
            Preset.Subtle => (55f, 85f, 20f, false),
            Preset.Minimal => (15f, 50f, 10f, true),
            _ => (25f, 65f, 15f, true)
        };
        Grouping = true;
        FocusOnlyLabels = true;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(Preset), Preset)) Preset = Preset.Balanced;
        if (!Enum.IsDefined(typeof(RevealMode), RevealMode)) RevealMode = RevealMode.Hold;
        UnfocusedOpacity = Number.Clamp(UnfocusedOpacity, 0f, 100f, 25f);
        DistantOpacity = Number.Clamp(DistantOpacity, 0f, 100f, 15f);
        UnfocusedSize = Number.Clamp(UnfocusedSize, 35f, 100f, 65f);
        FocusedSize = Number.Clamp(FocusedSize, 50f, 150f, 100f);
        TransitionSeconds = Number.Clamp(TransitionSeconds, 0f, 1f, .2f);
        FocusAngle = Number.Clamp(FocusAngle, 3f, 45f, 15f);
        FocusHysteresis = Number.Clamp(FocusHysteresis, 0f, 15f, 5f);
        FadeStart = Number.Clamp(FadeStart, 0f, 2900f, 300f);
        FadeEnd = Number.Clamp(FadeEnd, FadeStart + 50f, 3000f, Math.Max(1500f, FadeStart + 50f));
        GroupPixels = Number.Clamp(GroupPixels, 16f, 96f, 40f);
        for (int i = 0; i < Categories.Length; i++)
        {
            if (!Enum.IsDefined(typeof(Behavior), Categories[i]))
                Categories[i] = Behavior.Vanilla;
        }
    }
}

internal static class Number
{
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float Clamp(float value, float min, float max, float fallback) =>
        Finite(value) ? Math.Max(min, Math.Min(max, value)) : fallback;

    public static float MoveTowards(float from, float to, float maxDelta) =>
        Math.Abs(to - from) <= maxDelta ? to : from + Math.Sign(to - from) * maxDelta;

    public static float SmoothStep(float fraction)
    {
        float t = Clamp(fraction, 0f, 1f, 0f);
        return t * t * (3f - 2f * t);
    }
}
