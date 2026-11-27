using System;
using System.Collections.Generic;

namespace SmartBeacons.Core;

internal readonly struct ScreenPoint
{
    public readonly float X, Y;
    public ScreenPoint(float x, float y) { X = x; Y = y; }
    public bool Valid => Number.Finite(X) && Number.Finite(Y);
}

internal readonly struct GroupPoint
{
    public readonly string Id;
    public readonly ScreenPoint Point;
    public readonly bool Eligible;

    public GroupPoint(string id, ScreenPoint point, bool eligible = true)
    {
        Id = id;
        Point = point;
        Eligible = eligible;
    }
}

internal sealed class ScreenGroups
{
    private Dictionary<string, string> previous = new(StringComparer.Ordinal);
    private Dictionary<string, string> current = new(StringComparer.Ordinal);
    private readonly List<GroupPoint> sorted = new();
    private int[] parents = Array.Empty<int>();
    private int[] sizes = Array.Empty<int>();

    public string GroupOf(string id) => current.TryGetValue(id, out string group) ? group : null;

    public void Clear()
    {
        previous.Clear();
        current.Clear();
        sorted.Clear();
    }

    public void Update(List<GroupPoint> points, float enterPixels)
    {
        (previous, current) = (current, previous);
        current.Clear();
        sorted.Clear();
        foreach (GroupPoint point in points)
        {
            if (point.Eligible && point.Id != null && point.Point.Valid)
                sorted.Add(point);
        }
        sorted.Sort((a, b) =>
        {
            int compare = a.Point.X.CompareTo(b.Point.X);
            return compare != 0 ? compare : string.CompareOrdinal(a.Id, b.Id);
        });
        if (parents.Length < sorted.Count)
        {
            int capacity = Math.Max(sorted.Count, parents.Length * 2);
            parents = new int[capacity];
            sizes = new int[capacity];
        }

        float leavePixels = enterPixels * 1.22f;
        float enterSquared = enterPixels * enterPixels;
        float leaveSquared = leavePixels * leavePixels;
        for (int i = 0; i < sorted.Count; i++)
        {
            parents[i] = i;
            sizes[i] = 1;
        }
        for (int i = 0; i < sorted.Count; i++)
        {
            GroupPoint a = sorted[i];
            previous.TryGetValue(a.Id, out string previousGroup);
            for (int j = i + 1; j < sorted.Count; j++)
            {
                GroupPoint b = sorted[j];
                float dx = b.Point.X - a.Point.X;
                if (dx > leavePixels)
                    break;
                float dy = b.Point.Y - a.Point.Y;
                float squared = dx * dx + dy * dy;
                bool retained = squared <= leaveSquared && previousGroup != null &&
                    previous.TryGetValue(b.Id, out string bGroup) && previousGroup == bGroup;
                if (squared <= enterSquared || retained)
                    Join(i, j);
            }
        }
        for (int i = 0; i < sorted.Count; i++)
        {
            int root = Root(i);
            if (sizes[root] >= 2)
                current[sorted[i].Id] = sorted[root].Id;
        }
    }

    private int Root(int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    private void Join(int a, int b)
    {
        int first = Root(a);
        int second = Root(b);
        if (first == second)
            return;
        if (string.CompareOrdinal(sorted[first].Id, sorted[second].Id) > 0)
            (first, second) = (second, first);
        parents[second] = first;
        sizes[first] += sizes[second];
    }
}

internal readonly struct ScreenRect
{
    public readonly float Left, Bottom, Width, Height;
    public ScreenRect(float left, float bottom, float width, float height)
    {
        Left = left;
        Bottom = bottom;
        Width = width;
        Height = height;
    }

    public float Right => Left + Width;
    public float Top => Bottom + Height;
    public ScreenRect Move(ScreenPoint offset) => new(Left + offset.X, Bottom + offset.Y, Width, Height);
    public ScreenRect Pad(float pixels) => new(Left - pixels, Bottom - pixels, Width + pixels * 2f, Height + pixels * 2f);
    public bool Overlaps(ScreenRect other) =>
        Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;

    public ScreenRect Union(ScreenRect other)
    {
        float left = Math.Min(Left, other.Left);
        float bottom = Math.Min(Bottom, other.Bottom);
        return new ScreenRect(left, bottom, Math.Max(Right, other.Right) - left,
            Math.Max(Top, other.Top) - bottom);
    }
}

internal sealed class LabelLayout
{
    private readonly List<ScreenRect> occupied = new();

    public void Clear() => occupied.Clear();
    public void Reserve(ScreenRect rect) => occupied.Add(rect);

    public bool Place(ScreenRect rect, ScreenRect screen, ScreenPoint preferred, float scale, out ScreenPoint offset)
    {
        if (preferred.Valid && Math.Abs(preferred.X) <= 46f * scale && Math.Abs(preferred.Y) <= 96f * scale &&
            TryReserve(rect.Move(preferred), screen))
        {
            offset = preferred;
            return true;
        }

        float column = 40f * scale;
        float step = Math.Min(42f * scale, Math.Max(16f * scale, rect.Height + 2f * scale));
        for (int row = 0; row <= 4; row++)
        {
            float y = ((row + 1) / 2) * (row % 2 == 0 ? -step : step);
            for (int side = 0; side < 3; side++)
            {
                offset = new ScreenPoint(side == 0 ? 0f : side == 1 ? column : -column, y);
                if (TryReserve(rect.Move(offset), screen))
                    return true;
            }
        }
        offset = default;
        return false;
    }

    private bool TryReserve(ScreenRect rect, ScreenRect screen)
    {
        if (!Number.Finite(rect.Left) || !Number.Finite(rect.Bottom) ||
            !Number.Finite(rect.Width) || !Number.Finite(rect.Height) ||
            rect.Width <= 0f || rect.Height <= 0f ||
            rect.Left < screen.Left || rect.Bottom < screen.Bottom || rect.Right > screen.Right || rect.Top > screen.Top)
            return false;
        foreach (ScreenRect other in occupied)
        {
            if (rect.Overlaps(other))
                return false;
        }
        occupied.Add(rect);
        return true;
    }
}
