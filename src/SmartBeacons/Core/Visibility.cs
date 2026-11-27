using System;

namespace SmartBeacons.Core;

internal readonly struct VisibilityInput
{
    public readonly bool Available, EnabledInGame, QuickReveal, Priority, Focused;
    public readonly Category Category;
    public readonly Location Location;
    public readonly float Distance;

    public VisibilityInput(bool available, bool enabledInGame, bool quickReveal, bool priority,
        bool focused, Category category, Location location, float distance)
    {
        Available = available;
        EnabledInGame = enabledInGame;
        QuickReveal = quickReveal;
        Priority = priority;
        Focused = focused;
        Category = category;
        Location = location;
        Distance = distance;
    }
}

internal enum Presentation { Vanilla, Reveal, Emphasized, Ordinary, Hidden, Unavailable }

internal readonly struct Visual
{
    public readonly Presentation Mode;
    public readonly float Opacity, Scale, LabelOpacity;

    public Visual(Presentation mode, float opacity = 1f, float scale = 1f, float labelOpacity = 1f)
    {
        Mode = mode;
        Opacity = opacity;
        Scale = scale;
        LabelOpacity = labelOpacity;
    }

    public bool CanGroup(bool priority, bool focused, bool expand) =>
        Mode is Presentation.Ordinary or Presentation.Emphasized && Opacity > 0f &&
        !priority && (!focused || expand);
}

internal static class Visibility
{
    public static Visual Evaluate(Settings settings, VisibilityInput ping)
    {
        if (!ping.Available || !ping.EnabledInGame)
            return new Visual(Presentation.Unavailable);

        Behavior behavior = settings.Categories[(int)ping.Category];
        if (!settings.Active || behavior == Behavior.Vanilla)
            return new Visual(Presentation.Vanilla);
        if (ping.QuickReveal)
            return new Visual(Presentation.Reveal);
        if (ping.Priority)
            return new Visual(Presentation.Emphasized);
        if (ping.Focused)
            return new Visual(Presentation.Emphasized, scale: settings.FocusedSize / 100f);
        if (behavior == Behavior.Hidden)
            return new Visual(Presentation.Hidden, opacity: 0f, labelOpacity: 0f);

        float context = ping.Location switch
        {
            Location.Base when settings.ReduceInBase => .55f,
            Location.Lifepod when settings.ReduceInLifepod => .55f,
            Location.Vehicle when settings.ReduceInVehicle &&
                ping.Category is not (Category.Seamoth or Category.Prawn or Category.Cyclops) => .55f,
            _ => 1f
        };

        float opacity = settings.UnfocusedOpacity / 100f;
        if (settings.DistanceFade)
        {
            float distantOpacity = Math.Min(opacity, settings.DistantOpacity / 100f);
            float fraction = (ping.Distance - settings.FadeStart) / (settings.FadeEnd - settings.FadeStart);
            opacity += (distantOpacity - opacity) * Number.SmoothStep(fraction);
        }
        if (behavior == Behavior.Reduced)
            opacity *= .6f;
        opacity *= context;
        return new Visual(Presentation.Ordinary, opacity, settings.UnfocusedSize / 100f,
            settings.FocusOnlyLabels ? 0f : opacity);
    }
}
