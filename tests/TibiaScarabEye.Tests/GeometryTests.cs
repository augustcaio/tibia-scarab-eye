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
}
