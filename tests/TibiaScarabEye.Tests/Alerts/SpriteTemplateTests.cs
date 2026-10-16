using System.Drawing;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class SpriteTemplateTests
{
    private static readonly Rectangle Whole = new Rectangle(0, 0, 200, 150);

    [Theory]
    [InlineData(PlasmaItem.Collar)]
    [InlineData(PlasmaItem.Ring)]
    public void EmbeddedSprites_Load32PixelsWideWithTheTopRowsOnly(PlasmaItem item)
    {
        var template = Frames.Template(item);

        // so as 22 linhas de cima contam: embaixo o jogo escreve o tempo restante
        Assert.Equal((32, SpriteTemplate.SpriteRows), (template.Width, template.Height));
        Assert.InRange(template.OpaquePixels, 64, 32 * SpriteTemplate.SpriteRows - 1); // o fundo transparente nao conta
    }

    [Theory]
    [InlineData(PlasmaItem.Collar, 0, 0)]
    [InlineData(PlasmaItem.Collar, 57, 31)]
    [InlineData(PlasmaItem.Ring, 168, 118)] // encostado no canto inferior direito
    [InlineData(PlasmaItem.Ring, 3, 90)]
    public void FindsTheSpriteWhereverItIsInTheArea(PlasmaItem item, int x, int y)
    {
        var frame = Frames.With(200, 150, (item, new Point(x, y)));

        Assert.True(Frames.Template(item).Find(frame, Whole));
    }

    [Fact]
    public void DoesNotFindItInAnEmptyFrame()
    {
        Assert.False(Frames.Template(PlasmaItem.Collar).Find(Frames.Empty(), Whole));
        Assert.False(Frames.Template(PlasmaItem.Ring).Find(Frames.Empty(), Whole));
    }

    [Fact]
    public void TheCollarAndTheRingAreNotMistakenForEachOther()
    {
        var onlyRing = Frames.With(200, 150, (PlasmaItem.Ring, new Point(40, 40)));
        var onlyCollar = Frames.With(200, 150, (PlasmaItem.Collar, new Point(40, 40)));

        Assert.False(Frames.Template(PlasmaItem.Collar).Find(onlyRing, Whole));
        Assert.False(Frames.Template(PlasmaItem.Ring).Find(onlyCollar, Whole));
    }

    [Fact]
    public void BothItemsInTheSameFrameAreFoundSeparately()
    {
        var frame = Frames.With(200, 150, (PlasmaItem.Collar, new Point(10, 10)), (PlasmaItem.Ring, new Point(100, 80)));

        Assert.True(Frames.Template(PlasmaItem.Collar).Find(frame, Whole));
        Assert.True(Frames.Template(PlasmaItem.Ring).Find(frame, Whole));
    }

    [Fact]
    public void OnlyLooksInsideTheGivenAreas()
    {
        var frame = Frames.With(200, 150, (PlasmaItem.Ring, new Point(120, 90)));
        var template = Frames.Template(PlasmaItem.Ring);

        Assert.False(template.Find(frame, new Rectangle(0, 0, 80, 60)));
        Assert.True(template.Find(frame, new[] { new Rectangle(0, 0, 80, 60), new Rectangle(110, 80, 60, 60) }));
    }

    [Fact]
    public void ASlightlyOffAreaStillFindsTheSlot()
    {
        // Area de 34x34 (um slot) deslocada 2 px do sprite: a busca olha um pouco alem da borda.
        var frame = Frames.With(200, 150, (PlasmaItem.Collar, new Point(100, 60)));

        Assert.True(Frames.Template(PlasmaItem.Collar).Find(frame, new Rectangle(102, 62, 34, 34)));
    }

    [Fact]
    public void ToleratesSmallColorDifferences()
    {
        var frame = Noisy(Frames.With(200, 150, (PlasmaItem.Ring, new Point(30, 30))), 10);

        Assert.True(Frames.Template(PlasmaItem.Ring).Find(frame, Whole));
    }

    [Fact]
    public void TheTimerTextOverTheBottomOfTheItemDoesNotHideIt()
    {
        var frame = Frames.WithTimers(60, 50, (PlasmaItem.Collar, new Point(5, 3), "1m28"), (PlasmaItem.Ring, new Point(5, 3 + 0), null));
        Assert.True(Frames.Template(PlasmaItem.Collar).FindIn(Frames.WithTimers(60, 50, (PlasmaItem.Collar, new Point(5, 3), "30m"))));
        Assert.True(Frames.Template(PlasmaItem.Collar).FindIn(Frames.WithTimers(60, 50, (PlasmaItem.Collar, new Point(5, 3), "1m58"))));
    }

    [Fact]
    public void Locate_GivesTheBestPositionInsideTheTolerance()
    {
        var frame = Frames.With(120, 80, (PlasmaItem.Ring, new Point(47, 21)));
        Point at;

        Assert.True(Frames.Template(PlasmaItem.Ring).Locate(frame, out at));
        Assert.Equal(new Point(47, 21), at);
        Assert.False(Frames.Template(PlasmaItem.Collar).Locate(frame, out at));
    }

    [Fact]
    public void AreaSmallerThanTheSpriteFindsNothing()
    {
        var frame = Frames.With(200, 150, (PlasmaItem.Ring, new Point(30, 30)));

        Assert.False(Frames.Template(PlasmaItem.Ring).Find(frame, new Rectangle(30, 30, 20, 20)));
    }

    // Soma e subtrai alternadamente 'amount' em cada canal, como uma paleta ou compressao diferente faria.
    private static PixelBuffer Noisy(PixelBuffer frame, int amount)
    {
        var bytes = (byte[])frame.Bgra.Clone();
        for (int i = 0; i < bytes.Length; i += 4)
            for (int c = 0; c < 3; c++)
                bytes[i + c] = (byte)System.Math.Clamp(bytes[i + c] + (((i / 4) + c) % 2 == 0 ? amount : -amount), 0, 255);
        return new PixelBuffer(frame.Width, frame.Height, bytes);
    }
}
