using System;
using System.IO;
using TibiaScarabEye.Layouts;
using Xunit;

namespace TibiaScarabEye.Tests.Layouts;

public sealed class LayoutTests : IDisposable
{
    private readonly string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "tibiascarabeye-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void SaveAndLoad_RoundTripsRegionsAndObsIdentity()
    {
        string path = Path.Combine(directory, "layout.json");
        var spec = new RegionSpec { Name = "Vida e mana", X = .1, Y = .2, W = .3, H = .4, Left = -100, Top = -50, Width = 300, Height = 200, Opacity = 80, ObsId = "stable-test-region" };
        var layout = new Layout { SourceTitle = "Test window", ObsEnabled = true };
        layout.Regions.Add(spec);

        // Salvar duas vezes exercita a substituicao atomica de um arquivo existente.
        layout.Save(path);
        layout.Save(path);
        var loaded = Layout.Load(path);

        Assert.Equal(spec.Name, loaded.Regions[0].Name);
        Assert.Equal(80, loaded.Regions[0].Opacity);
        Assert.Equal(-100, loaded.Regions[0].Left);
        Assert.True(loaded.ObsEnabled);
        Assert.Equal(spec.ObsId, loaded.Regions[0].ObsId);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void LayerFlagsAndOrder_SurviveSaveAndLoad()
    {
        string path = Path.Combine(directory, "layers.json");
        var layout = new Layout();
        layout.Regions.Add(new RegionSpec { Name = "Baixo", X = .1, Y = .1, W = .1, H = .1, Width = 40, Height = 30, Hidden = true });
        layout.Regions.Add(new RegionSpec { Name = "Cima", X = .2, Y = .2, W = .1, H = .1, Width = 40, Height = 30, Locked = true });
        layout.Save(path);

        var loaded = Layout.Load(path);

        Assert.Equal(new[] { "Baixo", "Cima" }, loaded.Regions.ConvertAll(r => r.Name));
        Assert.Equal(new[] { true, false }, loaded.Regions.ConvertAll(r => r.Hidden));
        Assert.Equal(new[] { false, true }, loaded.Regions.ConvertAll(r => r.Locked));
    }

    [Fact]
    public void LayoutsFromOlderVersions_LoadVisibleAndUnlocked()
    {
        string path = Path.Combine(directory, "old.json");
        File.WriteAllText(path, "{\"Regions\":[{\"H\":0.1,\"Height\":30,\"Left\":10,\"Name\":\"Antiga\",\"ObsId\":\"x\",\"ObsTitle\":\"t\",\"Opacity\":100,\"Top\":10,\"W\":0.1,\"Width\":40,\"X\":0.1,\"Y\":0.1}],\"Version\":1}");

        var region = Layout.Load(path).Regions[0];

        Assert.False(region.Hidden);
        Assert.False(region.Locked);
    }
}
