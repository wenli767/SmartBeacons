using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SmartBeacons.Core;
using SmartBeacons.Persistence;
using Xunit;

namespace SmartBeacons.Tests;

public sealed class LayoutAndPersistenceTests
{
    [Fact]
    public void ForcedCanvasPassesDoNotAdvanceHudTransitionsTwice()
    {
        var clock = new HudFrameTime();
        float opacity = 1f;
        const float delta = .025f;
        const float transitionSeconds = .2f;

        opacity = Number.MoveTowards(opacity, .25f, clock.Advance(20, delta) / transitionSeconds);
        Assert.Equal(.875f, opacity);
        opacity = Number.MoveTowards(opacity, .25f, clock.Advance(20, delta) / transitionSeconds);
        Assert.Equal(.875f, opacity);
        opacity = Number.MoveTowards(opacity, .25f, clock.Advance(21, delta) / transitionSeconds);
        Assert.Equal(.75f, opacity);
    }

    [Fact]
    public void GroupingUsesScreenOverlapAndExcludesUnavailableAndPriorityMarkers()
    {
        var groups = new ScreenGroups();
        groups.Update(new List<GroupPoint>
        {
            Point("near", 200f, 540f), Point("far", 215f, 550f), Point("separate", 260f, 600f),
            new("priority", new ScreenPoint(201f, 540f), eligible: false),
            new("disabled", new ScreenPoint(201f, 540f), eligible: false)
        }, 40f);
        Assert.Equal("far", groups.GroupOf("near"));
        Assert.Equal("far", groups.GroupOf("far"));
        Assert.Null(groups.GroupOf("separate"));
        Assert.Null(groups.GroupOf("disabled"));
        Assert.Null(groups.GroupOf("priority"));
    }

    [Fact]
    public void GroupIdentitySurvivesInputReorderingAndLeaveThreshold()
    {
        var groups = new ScreenGroups();
        groups.Update(new List<GroupPoint> { Point("B", 100f, 20f), Point("A", 120f, 20f) }, 40f);
        Assert.Equal("A", groups.GroupOf("B"));
        groups.Update(new List<GroupPoint> { Point("A", 144f, 20f), Point("B", 100f, 20f) }, 40f);
        Assert.Equal("A", groups.GroupOf("B"));
        groups.Update(new List<GroupPoint> { Point("B", 100f, 20f), Point("A", 150f, 20f) }, 40f);
        Assert.Null(groups.GroupOf("B"));
        groups.Update(new List<GroupPoint> { Point("A", 144f, 20f), Point("B", 100f, 20f) }, 40f);
        Assert.Null(groups.GroupOf("B"));
    }

    [Fact]
    public void LayoutHandlesEmptyMissingAndNonFiniteCandidates()
    {
        var groups = new ScreenGroups();
        groups.Update(new List<GroupPoint> { Point("A", 0f, 0f), Point("B", 0f, 0f) }, 40f);
        groups.Update(new List<GroupPoint>(), 40f);
        Assert.Null(groups.GroupOf("A"));

        groups.Update(new List<GroupPoint>
        {
            Point("A", float.NaN, 2f), Point("B", 2f, float.PositiveInfinity),
            Point(null, 2f, 2f), Point("C", 2f, 2f)
        }, 40f);
        Assert.Null(groups.GroupOf(null));
        Assert.Null(groups.GroupOf("C"));
    }

    [Fact]
    public void LargeCollectionKeepsDeterministicConnectedComponents()
    {
        var groups = new ScreenGroups();
        var points = new List<GroupPoint>();
        for (int i = 0; i < 1000; i++)
            points.Add(Point(i.ToString("0000"), (i / 50) * 80f, i % 50 * .1f));
        points.Reverse();
        groups.Update(points, 40f);
        Assert.Equal("0000", groups.GroupOf("0049"));
        Assert.Equal("0950", groups.GroupOf("0999"));
    }

    [Fact]
    public void LabelPlacementIsStableBoundedAndCanDeclineOverfullLayout()
    {
        var layout = new LabelLayout();
        var screen = new ScreenRect(0, 0, 640, 360);
        var label = new ScreenRect(280, 160, 68, 15);
        Assert.True(layout.Place(label, screen, default, 1f, out ScreenPoint first));
        Assert.True(layout.Place(label, screen, default, 1f, out ScreenPoint second));
        Assert.Equal(0f, first.Y);
        Assert.Equal(17f, second.Y);
        Assert.False(label.Overlaps(label.Move(second)));
        Assert.True(Math.Abs(second.X) <= 40f && Math.Abs(second.Y) <= 84f);
        layout.Clear();
        layout.Reserve(label);
        Assert.True(layout.Place(label, screen, second, 1f, out ScreenPoint stable));
        Assert.Equal(second.Y, stable.Y);
        layout.Clear();
        Assert.False(layout.Place(new ScreenRect(0, 0, 700, 20), screen, default, 1f, out _));
    }

    [Fact]
    public void PrioritiesFollowIdsAcrossRenamesAndStayIsolatedBySave()
    {
        using var temp = new SaveDirectories();
        var priorities = new PriorityStore();
        // Both game beacons can be named "Home". Only the serialized id enters the store.
        string firstHomeId = "guid-for-first-home";
        string secondHomeId = "guid-for-second-home";
        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.True(priorities.Toggle(firstHomeId));
        Assert.False(priorities.Contains(secondHomeId));
        priorities.Save(temp.Second);
        Assert.False(Directory.Exists(temp.Second));
        priorities.Save(temp.First);
        Assert.True(priorities.TryLoad(temp.Second, out _));
        Assert.False(priorities.Contains(firstHomeId));
        Assert.True(priorities.Toggle(secondHomeId));
        priorities.Save(temp.Second);
        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.True(priorities.Contains(firstHomeId));
        Assert.False(priorities.Contains(secondHomeId));

        Assert.True(priorities.Toggle(firstHomeId));
        priorities.Save(temp.First);
        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.False(priorities.Contains(firstHomeId));
        Assert.True(priorities.TryLoad(temp.Second, out _));
        Assert.True(priorities.Contains(secondHomeId));
    }

    [Fact]
    public void InvalidIdsAndAnInactiveSaveCannotBeWritten()
    {
        using var temp = new SaveDirectories();
        var priorities = new PriorityStore();
        Assert.False(priorities.Toggle("ok"));
        Assert.True(priorities.TryLoad(temp.First, out _));
        foreach (string id in new[] { null, "", " ", "with\nnewline", new string('x', 257) })
        {
            Assert.False(priorities.Toggle(id));
            Assert.False(priorities.Contains(id));
        }
        priorities.Save(temp.First);
        Assert.False(Directory.Exists(temp.First));
    }

    [Fact]
    public void OversizedSidecarLeavesPrioritiesEmptyAndPreservesTheFile()
    {
        using var temp = new SaveDirectories();
        var priorities = new PriorityStore();
        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.True(priorities.Toggle("first-save-beacon"));
        priorities.Save(temp.First);

        const long oversizedLength = 4 * 1024 * 1024;
        Directory.CreateDirectory(temp.Second);
        string file = Path.Combine(temp.Second, "SmartBeacons.priorities.txt");
        using (FileStream stream = File.Create(file))
            stream.SetLength(oversizedLength);

        Assert.False(priorities.TryLoad(temp.Second, out string warning));
        Assert.Contains("exceeds the size limit", warning);
        Assert.False(priorities.Ready);
        Assert.False(priorities.Contains("first-save-beacon"));
        Assert.False(priorities.Toggle("second-save-beacon"));
        priorities.Save(temp.Second);
        Assert.Equal(oversizedLength, new FileInfo(file).Length);

        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.True(priorities.Contains("first-save-beacon"));
    }

    [Fact]
    public void MaximumLengthUtf8IdsRoundTripBeyondTheOldFileLimit()
    {
        using var temp = new SaveDirectories();
        var priorities = new PriorityStore();
        Assert.True(priorities.TryLoad(temp.First, out _));
        var ids = new List<string>();
        string suffix = new('海', 252);
        for (int i = 0; i < 4096; i++)
        {
            string id = i.ToString("D4", CultureInfo.InvariantCulture) + suffix;
            ids.Add(id);
            Assert.True(priorities.Toggle(id));
        }

        priorities.Save(temp.First);
        Assert.True(new FileInfo(Path.Combine(temp.First, "SmartBeacons.priorities.txt")).Length > 2 * 1024 * 1024);
        Assert.True(priorities.TryLoad(temp.First, out _));
        Assert.All(ids, id => Assert.True(priorities.Contains(id)));
    }

    private static GroupPoint Point(string id, float x, float y) => new(id, new ScreenPoint(x, y));

    private sealed class SaveDirectories : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "SmartBeacons-" + Guid.NewGuid());
        public string First => Path.Combine(root, "slot0000");
        public string Second => Path.Combine(root, "slot0001");

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
