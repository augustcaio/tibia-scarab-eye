using System.Drawing;
using System.IO;
using TibiaScarabEye.Layouts;
using Xunit;

namespace TibiaScarabEye.Tests.Layouts;

public class RegionSpecTests
{
    private static RegionSpec Sample() => new RegionSpec { Name = "Vida e mana", X = .1, Y = .2, W = .3, H = .4, Left = -100, Top = -50, Width = 300, Height = 200, Opacity = 80 };

    [Fact]
    public void Crop_MapsNormalizedRegionToPixels()
    {
        var spec = Sample();
        spec.Validate();

        Assert.Equal(new Rectangle(100, 100, 300, 200), spec.Crop(new Size(1000, 500)));
    }

    [Fact]
    public void Validate_RejectsNonFiniteCrop()
    {
        var spec = Sample();
        spec.W = double.NaN;

        Assert.Throws<InvalidDataException>(spec.Validate);
    }
}
