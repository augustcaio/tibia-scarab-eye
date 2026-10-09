using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace TibiaScarabEye.Tests;

public class GeometryTests
{
    [Fact]
    public void Fit_PreservesAspectRatioAndCentersInsideArea()
    {
        Rectangle fitted = Geometry.Fit(new Size(400, 200), new Rectangle(10, 20, 300, 300));

        Assert.Equal(new Rectangle(10, 95, 300, 150), fitted);
    }

    [Theory]
    [InlineData(213, 177, 224, 192)]
    [InlineData(100, 100, 96, 96)]
    [InlineData(0, 0, 0, 0)]
    public void SnapToGrid_MovesToNearestLineStartingAtTheWindowCorner(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(new Point(expectedX, expectedY), Geometry.SnapToGrid(new Point(x, y), 32));
    }

    [Fact]
    public void SnapToGrid_WithoutValidCellKeepsLocation()
    {
        Assert.Equal(new Point(7, 9), Geometry.SnapToGrid(new Point(7, 9), 0));
    }

    [Theory]
    [InlineData(-30, -5, 0, 0)]
    [InlineData(600, 470, 440, 380)]
    [InlineData(50, 60, 50, 60)]
    public void ClampInside_KeepsRectangleInsideBounds(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(new Rectangle(expectedX, expectedY, 200, 100), Geometry.ClampInside(new Rectangle(x, y, 200, 100), new Size(640, 480)));
    }

    [Theory]
    [InlineData(1, 1000, 34)]
    [InlineData(34, 1000, 34)]
    [InlineData(50, 1000, 34)]
    [InlineData(52, 1000, 70)]
    [InlineData(70, 1000, 70)]
    [InlineData(100, 1000, 106)]
    [InlineData(106, 1000, 106)]
    [InlineData(400, 1000, 394)]
    public void SnapToSlots_PicksTheNearestBlockOfAdjacentSlots(int length, int max, int expected)
    {
        Assert.Equal(expected, Geometry.SnapToSlots(length, max));
    }

    [Theory]
    [InlineData(400, 100, 70)]
    [InlineData(400, 34, 34)]
    [InlineData(400, 20, 20)]
    public void SnapToSlots_NeverExceedsTheRoomLeft(int length, int max, int expected)
    {
        Assert.Equal(expected, Geometry.SnapToSlots(length, max));
    }

    [Fact]
    public void ClampInside_RectangleLargerThanBoundsSitsAtOrigin()
    {
        Assert.Equal(new Rectangle(0, 0, 900, 700), Geometry.ClampInside(new Rectangle(40, 40, 900, 700), new Size(640, 480)));
    }

    [Fact]
    public void SlotBlock_SnapsEachSideAndGrowsInTheDragDirection()
    {
        var source = new Size(640, 480);
        Assert.Equal(new Rectangle(100, 100, 142, 70), Geometry.SlotBlock(new Point(100, 100), new Point(250, 160), source, false));
        Assert.Equal(new Rectangle(158, 266, 142, 34), Geometry.SlotBlock(new Point(300, 300), new Point(150, 290), source, false));
    }

    [Fact]
    public void SlotBlock_SquareUsesTheLongestSide()
    {
        Assert.Equal(new Rectangle(100, 100, 142, 142), Geometry.SlotBlock(new Point(100, 100), new Point(250, 130), new Size(640, 480), true));
    }

    [Fact]
    public void SlotBlock_IsEmptyWithoutDragAndNeverLeavesTheSource()
    {
        var source = new Size(640, 480);
        Assert.True(Geometry.SlotBlock(new Point(20, 20), new Point(20, 20), source, false).IsEmpty);
        Assert.Equal(new Rectangle(630, 470, 10, 10), Geometry.SlotBlock(new Point(630, 470), new Point(700, 600), source, false));
    }

    private static readonly Rectangle[] Pair = { new Rectangle(10, 10, 20, 20), new Rectangle(50, 40, 30, 10) };

    [Theory]
    [InlineData((int)Geometry.AlignKind.Left, 10, 10, 10, 40)]
    [InlineData((int)Geometry.AlignKind.Right, 60, 10, 50, 40)]
    [InlineData((int)Geometry.AlignKind.CenterH, 35, 10, 30, 40)]
    [InlineData((int)Geometry.AlignKind.Top, 10, 10, 50, 10)]
    [InlineData((int)Geometry.AlignKind.Bottom, 10, 30, 50, 40)]
    [InlineData((int)Geometry.AlignKind.MiddleV, 10, 20, 50, 25)]
    public void Align_UsesTheBoundsOfTheWholeSelection(int kind, int ax, int ay, int bx, int by)
    {
        var aligned = Geometry.Align(Pair, (Geometry.AlignKind)kind);

        Assert.Equal(new Point(ax, ay), aligned[0].Location);
        Assert.Equal(new Point(bx, by), aligned[1].Location);
        Assert.Equal(Pair[0].Size, aligned[0].Size);
    }

    [Fact]
    public void Distribute_KeepsTheEndsAndEvensTheGapsWhateverTheInputOrder()
    {
        var spread = Geometry.Distribute(new[] { new Rectangle(100, 0, 10, 10), new Rectangle(0, 0, 10, 10), new Rectangle(30, 0, 10, 10) }, true);

        Assert.Equal(new[] { 100, 0, 50 }, new[] { spread[0].X, spread[1].X, spread[2].X });
    }

    [Fact]
    public void Distribute_NeedsThreeOrMoreRectangles()
    {
        Assert.Equal(Pair, Geometry.Distribute(Pair, true));
    }

    [Fact]
    public void SnapToEdges_PullsEdgesTogetherWithinTheThresholdAndReportsGuides()
    {
        var vertical = new List<int>();
        var horizontal = new List<int>();

        Point delta = Geometry.SnapToEdges(new Rectangle(100, 100, 50, 50), new[] { new Rectangle(153, 50, 40, 40) }, 6, vertical, horizontal);

        Assert.Equal(new Point(3, 0), delta);
        Assert.Equal(new[] { 153 }, vertical);
        Assert.Empty(horizontal);
    }

    [Fact]
    public void SnapToEdges_ReportsGuidesForAlreadyCenteredAreasAndIgnoresFarOnes()
    {
        var vertical = new List<int>();
        var horizontal = new List<int>();

        Assert.Equal(Point.Empty, Geometry.SnapToEdges(new Rectangle(0, 0, 40, 40), new[] { new Rectangle(10, 100, 20, 10) }, 6, vertical, horizontal));
        Assert.Equal(new[] { 20 }, vertical);

        vertical.Clear();
        Assert.Equal(Point.Empty, Geometry.SnapToEdges(new Rectangle(0, 0, 40, 40), new[] { new Rectangle(300, 300, 20, 10) }, 6, vertical, horizontal));
        Assert.Empty(vertical);
    }
}
