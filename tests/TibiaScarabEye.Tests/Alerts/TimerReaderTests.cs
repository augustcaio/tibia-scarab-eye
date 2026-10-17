using System.Drawing;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class TimerReaderTests
{
    private static TimerReading ReadUnder(PlasmaItem item, string text, int width = 48, int height = 40)
    {
        var area = Frames.WithTimers(width, height, (item, new Point(2, 1), text));
        Point at;
        Assert.True(Frames.Template(item).Locate(area, out at));
        return TimerReader.Read(area, at);
    }

    [Theory]
    [InlineData("1m28", 88, true)]
    [InlineData("1m30", 90, true)]
    [InlineData("1m05", 65, true)]
    [InlineData("1m47", 107, true)]
    [InlineData("1m58", 118, true)]
    [InlineData("30m", 1800, false)]
    [InlineData("11m", 660, false)]
    [InlineData("2m", 120, false)]
    public void ReadsWhatTheGameWritesUnderTheItem(string text, int seconds, bool exact)
    {
        var reading = ReadUnder(PlasmaItem.Collar, text);

        Assert.True(reading.Valid, text);
        Assert.Equal(text, reading.Text);
        Assert.Equal(seconds, reading.Seconds);
        Assert.Equal(exact, reading.Exact);
    }

    [Fact]
    public void ReadsTheRingTooAndNothingWhenThereIsNoText()
    {
        Assert.Equal(88, ReadUnder(PlasmaItem.Ring, "1m28").Seconds);
        Assert.False(ReadUnder(PlasmaItem.Ring, null).Valid);
    }

    [Fact]
    public void ACharacterTheFontTableDoesNotHaveIsNotGuessed()
    {
        // Faltam os desenhos do 6 e do 9: "1m3" sem o ultimo digito nao pode virar "1m30" nem "1m3x".
        var area = Frames.WithTimers(48, 40, (PlasmaItem.Collar, new Point(2, 1), "1m3"));
        Point at;
        Frames.Template(PlasmaItem.Collar).Locate(area, out at);

        Assert.False(TimerReader.Read(area, at).Valid);
    }

    [Fact]
    public void TheTextOfTheNeighbourSlotDoesNotMixIn()
    {
        // Uma area larga que cobre o colar e o anel lado a lado: cada um le so o seu.
        var area = Frames.WithTimers(80, 40, (PlasmaItem.Collar, new Point(2, 1), "1m28"), (PlasmaItem.Ring, new Point(42, 1), "1m47"));
        Point collarAt, ringAt;
        Assert.True(Frames.Template(PlasmaItem.Collar).Locate(area, out collarAt));
        Assert.True(Frames.Template(PlasmaItem.Ring).Locate(area, out ringAt));

        Assert.Equal(88, TimerReader.Read(area, collarAt).Seconds);
        Assert.Equal(107, TimerReader.Read(area, ringAt).Seconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("m")]
    [InlineData("1m2")]
    [InlineData("1m234")]
    [InlineData("1m60")]
    [InlineData("10m30")]
    [InlineData("0m10")]
    [InlineData("12")]
    [InlineData("1m2m")]
    public void Parse_RejectsShapesTheGameDoesNotUse(string text)
    {
        Assert.False(TimerReader.Parse(text).Valid);
    }

    [Fact]
    public void ARealCaptureOfTheSlotIsRead()
    {
        // As linhas de texto de "1m28" como saem do jogo (capturadas de um cliente real), com um brilho do sprite a mais no topo.
        string[] rows =
        {
            " .  ..    #          ....   ....      ",
            " . .##.    ..       .####. .####.     ",
            " ..###.. ......... .##..##.##..##.    ",
            " . .##. .#####.###. ....##.##..##.    ",
            " . .##. .##..##..##....##. .####.     ",
            " . .##. .##..##..##. .##...##..##.    ",
            " . .##. .##..##..##..##. ..##..##.    ",
            " . .##. .##..##..##.##.....##..##.    ",
            " ..####..##..##..##.######..####.     ",
        };
        // '#' e '.' do dump sao a luz do texto e o contorno; o resto e o fundo do slot.
        var bytes = new byte[38 * 40 * 4];
        for (int i = 0; i < bytes.Length; i += 4) { bytes[i] = 28; bytes[i + 1] = 60; bytes[i + 2] = 30; bytes[i + 3] = 255; }
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < 38; x++)
            {
                char c = x < rows[y].Length ? rows[y][x] : ' ';
                int at = ((22 + y - 1) * 38 + x) * 4;
                if (c == '#') { bytes[at] = 205; bytes[at + 1] = 208; bytes[at + 2] = 205; }
                else if (c == '.') { bytes[at] = 2; bytes[at + 1] = 4; bytes[at + 2] = 2; }
            }
        var area = new PixelBuffer(38, 40, bytes);

        var reading = TimerReader.Read(area, new Point(1, 1));

        Assert.Equal("1m28", reading.Text);
        Assert.Equal(88, reading.Seconds);
    }
}
