using System;

namespace SmartBeacons.Core;

internal readonly struct ViewPoint
{
    public readonly float X, Y, Depth;

    public ViewPoint(float x, float y, float depth)
    {
        X = x;
        Y = y;
        Depth = depth;
    }

    public bool OnScreen => Number.Finite(X) && Number.Finite(Y) && Number.Finite(Depth) &&
        Depth > 0f && X >= 0f && X <= 1f && Y >= 0f && Y <= 1f;
}

internal readonly struct FocusCone
{
    private readonly float enterCos, exitCos;

    public FocusCone(float angle, float hysteresis)
    {
        enterCos = (float)Math.Cos(angle * Math.PI / 180d);
        exitCos = (float)Math.Cos((angle + hysteresis) * Math.PI / 180d);
    }

    public bool Contains(ViewPoint point, float alignment, bool wasFocused) =>
        point.OnScreen && Number.Finite(alignment) && alignment >= (wasFocused ? exitCos : enterCos);

    public static float Alignment(float forwardDot, float distance) =>
        distance > .001f && Number.Finite(distance)
            ? forwardDot / distance
            : float.NaN;
}

internal sealed class FocusSelection
{
    private string previous, best;
    private float bestAlignment;
    private bool retain;

    public string Selected => previous;

    public void Begin()
    {
        best = null;
        bestAlignment = float.NegativeInfinity;
        retain = false;
    }

    public void Consider(string id, ViewPoint point, float alignment, FocusCone cone)
    {
        if (string.IsNullOrEmpty(id))
            return;
        if (id == previous && cone.Contains(point, alignment, true))
            retain = true;
        if (!cone.Contains(point, alignment, false))
            return;
        if (alignment > bestAlignment || (alignment == bestAlignment && string.CompareOrdinal(id, best) < 0))
        {
            best = id;
            bestAlignment = alignment;
        }
    }

    public void Commit() => previous = retain ? previous : best;

    public void Clear()
    {
        previous = null;
        Begin();
    }
}

internal sealed class RevealState
{
    private bool toggled, armed;

    public bool Read(RevealMode mode, bool allowed, bool held, bool pressed)
    {
        if (!allowed)
        {
            Reset();
            return false;
        }

        if (!armed)
        {
            armed = !held;
            return false;
        }
        if (mode == RevealMode.Hold)
            return held;
        if (pressed)
            toggled = !toggled;
        return toggled;
    }

    public void Reset()
    {
        toggled = false;
        armed = false;
    }
}
