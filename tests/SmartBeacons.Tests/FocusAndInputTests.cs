using System;
using SmartBeacons.Core;
using Xunit;

namespace SmartBeacons.Tests;

public sealed class FocusAndInputTests
{
    private static readonly ViewPoint Center = new(.5f, .5f, 1f);
    private readonly FocusCone cone = new(15f, 5f);
    private static float Cos(double degrees) => (float)Math.Cos(degrees * Math.PI / 180d);

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(14, true, true)]
    [InlineData(17, false, true)]
    [InlineData(21, false, false)]
    public void FocusConeUsesEnterAndExitAngles(double angle, bool enters, bool stays)
    {
        Assert.Equal(enters, cone.Contains(Center, Cos(angle), false));
        Assert.Equal(stays, cone.Contains(Center, Cos(angle), true));
    }

    [Theory]
    [InlineData(.5f, .5f, -1f)]
    [InlineData(.5f, .5f, 0f)]
    [InlineData(.5f, 1.1f, 1f)]
    [InlineData(float.NaN, .5f, 1f)]
    [InlineData(.5f, float.PositiveInfinity, 1f)]
    public void BehindOffscreenAndInvalidViewPointsCannotReceiveFocus(float x, float y, float depth)
    {
        Assert.False(cone.Contains(new ViewPoint(x, y, depth), 1f, true));
    }

    [Fact]
    public void AlignmentReturnsUnitDirectionCosine()
    {
        Assert.Equal(.6f, FocusCone.Alignment(3f, 5f));
        Assert.Equal(-1f, FocusCone.Alignment(-5f, 5f));
        Assert.True(float.IsNaN(FocusCone.Alignment(1f, 0f)));
        Assert.False(cone.Contains(Center, float.NaN, true));
    }

    [Fact]
    public void SingleFocusKeepsPreviousUntilExitAndUsesDeterministicTies()
    {
        var selection = new FocusSelection();
        selection.Begin();
        selection.Consider(null, Center, 1f, cone);
        selection.Consider("bravo", Center, Cos(10), cone);
        selection.Consider("alpha", Center, Cos(10), cone);
        selection.Commit();
        Assert.Equal("alpha", selection.Selected);

        selection.Begin();
        selection.Consider("bravo", Center, 1f, cone);
        selection.Consider("alpha", Center, Cos(17), cone);
        selection.Commit();
        Assert.Equal("alpha", selection.Selected);

        selection.Begin();
        selection.Consider("bravo", Center, 1f, cone);
        selection.Consider("alpha", Center, Cos(21), cone);
        selection.Commit();
        Assert.Equal("bravo", selection.Selected);
        selection.Clear();
        Assert.Null(selection.Selected);
    }

    [Fact]
    public void HoldDoesNotReactivateFromAKeyHeldThroughAMenu()
    {
        var key = new RevealState();
        Assert.False(key.Read(RevealMode.Hold, true, false, false));
        Assert.True(key.Read(RevealMode.Hold, true, true, true));
        Assert.False(key.Read(RevealMode.Hold, false, true, false));
        Assert.False(key.Read(RevealMode.Hold, true, true, false));
        Assert.False(key.Read(RevealMode.Hold, true, false, false));
        Assert.True(key.Read(RevealMode.Hold, true, true, true));
        Assert.False(key.Read(RevealMode.Hold, true, false, false));
    }

    [Fact]
    public void ToggleChangesOnPressAndClearsInBlockedContext()
    {
        var key = new RevealState();
        key.Read(RevealMode.Toggle, true, false, false);
        Assert.True(key.Read(RevealMode.Toggle, true, true, true));
        Assert.True(key.Read(RevealMode.Toggle, true, false, false));
        Assert.False(key.Read(RevealMode.Toggle, true, true, true));
        Assert.True(key.Read(RevealMode.Toggle, true, true, true));
        Assert.False(key.Read(RevealMode.Toggle, false, false, false));
        Assert.False(key.Read(RevealMode.Toggle, true, false, false));
    }
}
