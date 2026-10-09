using System.Collections.Generic;
using System.Drawing;
using TibiaScarabEye.UI;
using Xunit;

namespace TibiaScarabEye.Tests;

public class SelectorSquareTests
{
    private static readonly Size Source = new Size(640, 480);

    public static IEnumerable<object[]> DragEnds() => new[]
    {
        new object[] { new Point(500, 300) },
        new object[] { new Point(100, 300) },
        new object[] { new Point(500, 100) },
        new object[] { new Point(100, 100) },
        new object[] { new Point(2000, 2000) },
        new object[] { new Point(-2000, -2000) },
    };

    [Theory]
    [MemberData(nameof(DragEnds))]
    public void Square_StaysSquareInsideSourceInAnyDirection(Point end)
    {
        Rectangle square = Selector.Square(new Point(320, 240), end, Source);

        Assert.Equal(square.Width, square.Height);
        Assert.True(square.Width > 0);
        Assert.True(new Rectangle(Point.Empty, Source).Contains(square), "Square drag escaped source");
    }

    [Fact]
    public void Square_ClampsAtSourceBoundary()
    {
        Assert.Equal(new Rectangle(630, 470, 10, 10), Selector.Square(new Point(630, 470), new Point(700, 600), Source));
    }

    [Fact]
    public void Square_IsEmptyForClickWithoutDrag()
    {
        Assert.True(Selector.Square(new Point(20, 20), new Point(20, 20), Source).IsEmpty);
    }
}
