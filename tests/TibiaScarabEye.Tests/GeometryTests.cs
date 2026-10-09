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
    public void SnapToGrid_MovesToNearestLineStartingAtTheGridCorner(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(new Point(expectedX, expectedY), Geometry.SnapToGrid(new Point(x, y), new Rectangle(0, 0, 640, 480), 32));
    }

    [Fact]
    public void SnapToGrid_AnchorsLinesAtTheMapCorner()
    {
        Assert.Equal(new Point(110, 70), Geometry.SnapToGrid(new Point(115, 66), new Rectangle(14, 6, 400, 300), 32));
    }

    [Fact]
    public void SnapToGrid_WithoutGridKeepsLocation()
    {
        Assert.Equal(new Point(7, 9), Geometry.SnapToGrid(new Point(7, 9), Rectangle.Empty, 32));
        Assert.Equal(new Point(7, 9), Geometry.SnapToGrid(new Point(7, 9), new Rectangle(0, 0, 100, 100), 0));
    }

    [Theory]
    [InlineData(-30, -5, 0, 0)]
    [InlineData(600, 470, 440, 380)]
    [InlineData(50, 60, 50, 60)]
    public void ClampInside_KeepsRectangleInsideBounds(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(new Rectangle(expectedX, expectedY, 200, 100), Geometry.ClampInside(new Rectangle(x, y, 200, 100), new Size(640, 480)));
    }

    [Fact]
    public void ClampInside_RectangleLargerThanBoundsSitsAtOrigin()
    {
        Assert.Equal(new Rectangle(0, 0, 900, 700), Geometry.ClampInside(new Rectangle(40, 40, 900, 700), new Size(640, 480)));
    }
}
