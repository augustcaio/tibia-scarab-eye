using System;
using System.IO;
using System.Linq;
using System.Text;
using TibiaScarabEye.Alerts;
using TibiaScarabEye.Obs;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class ObsSightTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "tibiascarabeye-sight-" + Guid.NewGuid().ToString("N"));

    public ObsSightTests() { Directory.CreateDirectory(folder); }

    public void Dispose() { Directory.Delete(folder, true); }

    // Monta o arquivo como o script Lua do OBS o grava.
    private static byte[] Build(long heartbeat, params (int w, int h, byte fill)[] areas)
    {
        using var stream = new MemoryStream();
        void Text(string text) { var bytes = Encoding.ASCII.GetBytes(text); stream.Write(bytes, 0, bytes.Length); }
        Text("TSEYE1\n");
        Text($"{heartbeat} abc123 {areas.Length}\n");
        foreach (var (w, h, fill) in areas)
        {
            Text($"{w} {h}\n");
            var pixels = new byte[w * h * 4];
            Array.Fill(pixels, fill);
            stream.Write(pixels, 0, pixels.Length);
        }
        return stream.ToArray();
    }

    [Fact]
    public void Parse_ReadsHeaderAndEveryArea()
    {
        var sight = ObsSight.Parse(Build(1792173245, (40, 30, 7), (34, 34, 9)));

        Assert.Equal(1792173245, sight.Heartbeat);
        Assert.Equal("abc123", sight.Session);
        Assert.Equal(new[] { (40, 30), (34, 34) }, sight.Areas.Select(a => (a.Width, a.Height)));
        Assert.All(sight.Areas[0].Bgra.Take(40 * 30 * 4), b => Assert.Equal(7, b));
        Assert.Equal(9, sight.Areas[1].Bgra[34 * 34 * 4 - 1]);
    }

    [Fact]
    public void Parse_AcceptsAnEmptyList()
    {
        Assert.Empty(ObsSight.Parse(Build(100)).Areas);
    }

    [Theory]
    [InlineData("OUTRO\n1 a 0\n")]
    [InlineData("TSEYE1\nnao-e-numero a 0\n")]
    [InlineData("TSEYE1\n1 a 31\n")]
    [InlineData("TSEYE1\n1 a 1\n513 10\n")]
    [InlineData("TSEYE1\n1 a 1\n0 10\n")]
    [InlineData("TSEYE1\n1 a 1\n10 10\nfaltam os bytes")]
    public void Parse_RejectsMalformedFiles(string content)
    {
        Assert.Throws<InvalidDataException>(() => ObsSight.Parse(Encoding.ASCII.GetBytes(content)));
    }

    [Fact]
    public void Parse_RejectsATruncatedImage()
    {
        var bytes = Build(1, (20, 20, 1));

        Assert.Throws<InvalidDataException>(() => ObsSight.Parse(bytes.Take(bytes.Length - 5).ToArray()));
    }

    [Fact]
    public void Reader_ReturnsTheAreasWhileTheHeartbeatIsFresh()
    {
        string path = Path.Combine(folder, "obs-sight.bin");
        File.WriteAllBytes(path, Build(1000, (34, 34, 5)));
        long now = 1002;
        var reader = new ObsSightReader(path, () => now);

        Assert.Single(reader.Read());

        now = 1000 + ObsSightReader.MaxAgeSeconds + 1; // o script parou de atualizar
        Assert.Null(reader.Read());
    }

    [Fact]
    public void Reader_PicksUpTheNewFileWhenTheScriptReplacesIt()
    {
        string path = Path.Combine(folder, "obs-sight.bin");
        File.WriteAllBytes(path, Build(1000, (34, 34, 5)));
        long now = 1000;
        var reader = new ObsSightReader(path, () => now);
        Assert.Single(reader.Read());

        File.WriteAllBytes(path, Build(1001, (34, 34, 5), (40, 40, 6)));
        now = 1001;

        Assert.Equal(2, reader.Read().Count);
    }

    [Fact]
    public void Reader_KeepsTheLastGoodReadWhileTheFileIsMissingForAMoment()
    {
        string path = Path.Combine(folder, "obs-sight.bin");
        File.WriteAllBytes(path, Build(1000, (34, 34, 5)));
        long now = 1000;
        var reader = new ObsSightReader(path, () => now);
        Assert.Single(reader.Read());

        File.Delete(path); // o script troca o arquivo: remove e renomeia
        now = 1001;

        Assert.Single(reader.Read());
    }

    [Fact]
    public void Reader_IgnoresACorruptFileAndKeepsTheLastGoodOne()
    {
        string path = Path.Combine(folder, "obs-sight.bin");
        File.WriteAllBytes(path, Build(1000, (34, 34, 5)));
        long now = 1000;
        var reader = new ObsSightReader(path, () => now);
        Assert.Single(reader.Read());

        File.WriteAllText(path, "lixo");

        Assert.Single(reader.Read());
    }

    [Fact]
    public void Reader_WithoutAnyFileHasNoImages()
    {
        Assert.Null(new ObsSightReader(Path.Combine(folder, "nao-existe.bin"), () => 1).Read());
    }

    [Fact]
    public void ASpriteInAnAreaImageFromObsIsRecognised()
    {
        // O caminho todo do lado do programa: arquivo do OBS -> imagens das areas -> reconhecimento do sprite.
        var frame = Frames.With(40, 40, (PlasmaItem.Collar, new System.Drawing.Point(4, 4)));
        var bytes = Build(1000, (40, 40, 0)).ToList();
        int header = Encoding.ASCII.GetBytes("TSEYE1\n1000 abc123 1\n40 40\n").Length;
        bytes.RemoveRange(header, 40 * 40 * 4);
        bytes.InsertRange(header, frame.Bgra.Take(40 * 40 * 4));
        string path = Path.Combine(folder, "obs-sight.bin");
        File.WriteAllBytes(path, bytes.ToArray());

        var areas = new ObsSightReader(path, () => 1000).Read();

        Assert.True(Frames.Template(PlasmaItem.Collar).Find(areas));
        Assert.False(Frames.Template(PlasmaItem.Ring).Find(areas));
    }
}
