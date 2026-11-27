using SmartBeacons.Core;
using Xunit;

namespace SmartBeacons.Tests;

public sealed class VisibilityTests
{
    private static Visual Evaluate(Settings settings, bool available = true, bool enabledInGame = true,
        bool reveal = false, bool priority = false, bool focused = false, Category category = Category.Beacon,
        Location location = Location.Outside, float distance = 0f) =>
        Visibility.Evaluate(settings, new VisibilityInput(available, enabledInGame, reveal, priority,
            focused, category, location, distance));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void UnavailableDestroyedOrManuallyDisabledSignalsWinOverEveryOverride(bool available, bool enabled)
    {
        var settings = new Settings();
        Assert.Equal(Presentation.Unavailable, Evaluate(settings, available, enabled, true, true, true).Mode);
        settings.ApplyPreset(Preset.Vanilla);
        Assert.Equal(Presentation.Unavailable, Evaluate(settings, available, enabled, true, true, true).Mode);
    }

    [Fact]
    public void VanillaAndDisabledSettingsPassThrough()
    {
        var settings = new Settings();
        settings.ApplyPreset(Preset.Vanilla);
        Assert.Equal(Presentation.Vanilla, Evaluate(settings, reveal: true, priority: true, focused: true).Mode);
        settings.ApplyPreset(Preset.Balanced);
        settings.Enabled = false;
        Assert.Equal(Presentation.Vanilla, Evaluate(settings, reveal: true, priority: true, focused: true).Mode);
    }

    [Theory]
    [InlineData(0f, .25f)]
    [InlineData(299f, .25f)]
    [InlineData(300f, .25f)]
    [InlineData(600f, .234375f)]
    [InlineData(900f, .2f)]
    [InlineData(1500f, .15f)]
    [InlineData(3000f, .15f)]
    [InlineData(float.NaN, .25f)]
    public void DistanceFadeHasFixedBoundariesAndSmoothInterpolation(float meters, float opacity)
    {
        Visual result = Evaluate(new Settings(), distance: meters);
        Assert.Equal(Presentation.Ordinary, result.Mode);
        Assert.Equal(opacity, result.Opacity, 5);
        Assert.Equal(.65f, result.Scale);
        Assert.Equal(0f, result.LabelOpacity);
    }

    [Fact]
    public void DistantFloorCannotBrightenAnUnfocusedSignal()
    {
        var settings = new Settings { UnfocusedOpacity = 10f, DistantOpacity = 20f };
        Assert.Equal(.10f, Evaluate(settings, distance: 2500f).Opacity);
        settings.UnfocusedOpacity = 0f;
        Assert.Equal(0f, Evaluate(settings, distance: 2500f).Opacity);
    }

    [Fact]
    public void DistanceFadeCanBeDisabledAndLabelsCanBeShown()
    {
        var settings = new Settings { DistanceFade = false, FocusOnlyLabels = false };
        Visual result = Evaluate(settings, distance: 8000f);
        Assert.Equal(.25f, result.Opacity);
        Assert.Equal(.25f, result.LabelOpacity);
    }

    [Fact]
    public void RevealAndPriorityOverrideFocusCategoryContextAndDistance()
    {
        var settings = new Settings { FocusedSize = 135f };
        settings.Categories[(int)Category.Beacon] = Behavior.Hidden;
        Visual reveal = Evaluate(settings, reveal: true, priority: true, focused: true, location: Location.Base, distance: 4000);
        Assert.Equal(Presentation.Reveal, reveal.Mode);
        Assert.Equal(1f, reveal.Scale);
        Assert.Equal(1f, reveal.LabelOpacity);
        Assert.False(reveal.CanGroup(priority: false, focused: false, expand: true));
        Visual priority = Evaluate(settings, priority: true, focused: true, location: Location.Base, distance: 4000);
        Assert.Equal(Presentation.Emphasized, priority.Mode);
        Assert.Equal(1f, priority.Opacity);
        Assert.Equal(1f, priority.Scale);
        Assert.Equal(1f, priority.LabelOpacity);
        Visual focus = Evaluate(settings, focused: true, location: Location.Base, distance: 4000);
        Assert.Equal(1.35f, focus.Scale);
        Assert.Equal(1f, focus.Opacity);
        Assert.Equal(Presentation.Hidden, Evaluate(settings).Mode);
    }

    [Fact]
    public void GroupsExcludePriorityAndUnavailablePingsAndRespectExpansionSetting()
    {
        var settings = new Settings();
        Assert.False(Evaluate(settings, priority: true).CanGroup(priority: true, focused: false, expand: true));
        Assert.False(Evaluate(settings, enabledInGame: false).CanGroup(priority: false, focused: false, expand: true));
        Assert.False(Evaluate(settings, focused: true).CanGroup(priority: false, focused: true, expand: false));
        Assert.True(Evaluate(settings, focused: true).CanGroup(priority: false, focused: true, expand: true));
        Assert.True(Evaluate(settings).CanGroup(priority: false, focused: false, expand: false));
    }

    [Fact]
    public void StoryAndUnknownCategoriesDefaultToTheirVanillaPresentation()
    {
        var settings = new Settings();
        Assert.Equal(Presentation.Vanilla, Evaluate(settings, category: Category.Other, reveal: true).Mode);
        Assert.Equal(Presentation.Vanilla, Evaluate(settings, category: Category.Story, priority: true).Mode);
        settings.Categories[(int)Category.Other] = Behavior.Reduced;
        Assert.Equal(.15f, Evaluate(settings, category: Category.Other).Opacity, 5);
    }

    [Fact]
    public void ContextOptionsAreIndependentAndProtectVehicleNavigation()
    {
        var settings = new Settings();
        Assert.Equal(.1375f, Evaluate(settings, location: Location.Base).Opacity, 5);
        Assert.Equal(.1375f, Evaluate(settings, location: Location.Lifepod).Opacity, 5);
        Assert.Equal(.25f, Evaluate(settings, location: Location.Vehicle).Opacity);
        settings.ReduceInBase = false;
        settings.ReduceInVehicle = true;
        Assert.Equal(.25f, Evaluate(settings, location: Location.Base).Opacity);
        Assert.Equal(.1375f, Evaluate(settings, location: Location.Lifepod).Opacity, 5);
        Assert.Equal(.1375f, Evaluate(settings, location: Location.Vehicle).Opacity, 5);
        Assert.Equal(.25f, Evaluate(settings, location: Location.Vehicle, category: Category.Cyclops).Opacity);
    }

    [Fact]
    public void ValidationRepairsNaNEnumAndInvertedDistanceRange()
    {
        var settings = new Settings
        {
            Preset = (Preset)(-1), RevealMode = (RevealMode)999, FocusAngle = float.NaN,
            UnfocusedOpacity = -10f, DistantOpacity = float.PositiveInfinity,
            FadeStart = 9000f, FadeEnd = 20f, GroupPixels = 0f
        };
        settings.Categories[(int)Category.Cyclops] = (Behavior)99;
        settings.Validate();
        Assert.Equal(Preset.Balanced, settings.Preset);
        Assert.Equal(RevealMode.Hold, settings.RevealMode);
        Assert.Equal(15f, settings.FocusAngle);
        Assert.Equal(0f, settings.UnfocusedOpacity);
        Assert.Equal(15f, settings.DistantOpacity);
        Assert.Equal(2900f, settings.FadeStart);
        Assert.Equal(2950f, settings.FadeEnd);
        Assert.Equal(16f, settings.GroupPixels);
        Assert.Equal(Behavior.Vanilla, settings.Categories[(int)Category.Cyclops]);
    }

    [Theory]
    [InlineData(1, 55f, 85f, 20f, false)]
    [InlineData(2, 25f, 65f, 15f, true)]
    [InlineData(3, 15f, 50f, 10f, true)]
    public void PresetsSetSpecifiedDefaultsAndPreserveIndependentOptions(int preset, float opacity,
        float scale, float focus, bool distanceFade)
    {
        var settings = new Settings { Grouping = false, FocusOnlyLabels = false, ReduceInBase = false };
        settings.ApplyPreset((Preset)preset);
        Assert.Equal(opacity, settings.UnfocusedOpacity);
        Assert.Equal(scale, settings.UnfocusedSize);
        Assert.Equal(focus, settings.FocusAngle);
        Assert.Equal(distanceFade, settings.DistanceFade);
        Assert.True(settings.Grouping);
        Assert.True(settings.FocusOnlyLabels);
        Assert.False(settings.ReduceInBase);
        settings.ApplyPreset(Preset.Custom);
        Assert.Equal(opacity, settings.UnfocusedOpacity);
    }
}
