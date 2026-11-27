namespace SmartBeacons.Core;

internal sealed class HudFrameTime
{
    private int lastFrame = -1;

    public float Advance(int frame, float unscaledDelta)
    {
        if (frame == lastFrame)
            return 0f;
        lastFrame = frame;
        return unscaledDelta;
    }
}
