using System;
using System.IO;
using System.Linq;
using TibiaScarabEye.Layouts;
using Xunit;

namespace TibiaScarabEye.Tests.Layouts;

public sealed class PresetStoreTests : IDisposable
{
    private readonly string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "tibiascarabeye-presets-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string File_(string name) => Path.Combine(directory, name);

    private static RegionSpec Region(string name, string obsId = null) =>
        new RegionSpec { Name = name, X = .1, Y = .1, W = .1, H = .1, Width = 40, Height = 30, ObsId = obsId, ObsTitle = obsId == null ? null : "t " + obsId };

    [Fact]
    public void SaveAndOpen_RoundTripsPresetsLayersAndLastUsed()
    {
        string path = File_("presets.json");
        var store = PresetStore.Open(path);
        var preset = store.Add("Combate", "Santizza", new[] { Region("Vida", "id-1"), Region("Mana", "id-2") });
        preset.Regions[1].Hidden = true;
        store.SetLastUsed("Santizza", preset.Id);
        store.Save();

        var reopened = PresetStore.Open(path);

        var loaded = Assert.Single(reopened.Presets);
        Assert.Equal(("Combate", "Santizza"), (loaded.Name, loaded.Character));
        Assert.Equal(new[] { "Vida", "Mana" }, loaded.Regions.Select(r => r.Name));
        Assert.Equal(new[] { "id-1", "id-2" }, loaded.Regions.Select(r => r.ObsId));
        Assert.True(loaded.Regions[1].Hidden);
        Assert.Same(loaded, reopened.Pick("santizza"));
    }

    [Fact]
    public void MissingFile_StartsEmptyAndACorruptOneIsKeptAsABackup()
    {
        string path = File_("presets.json");
        Assert.Empty(PresetStore.Open(path).Presets);

        File.WriteAllText(path, "isto nao e json");
        Assert.Empty(PresetStore.Open(path).Presets);
        Assert.True(File.Exists(path + ".corrompido"));
    }

    [Fact]
    public void Names_AreUniquePerScope()
    {
        var store = PresetStore.Open(File_("presets.json"));
        store.Add("Combate", "Santizza", Array.Empty<RegionSpec>());

        Assert.Equal("combate (2)", store.Add("combate", "Santizza", Array.Empty<RegionSpec>()).Name);
        Assert.Equal("Combate", store.Add("Combate", "Outro", Array.Empty<RegionSpec>()).Name);
        Assert.Equal("Combate", store.Add("Combate", null, Array.Empty<RegionSpec>()).Name);
        Assert.Equal("Preset", store.Add("   ", null, Array.Empty<RegionSpec>()).Name);
    }

    [Fact]
    public void Pick_PrefersTheLastUsedThenOneOfTheCharacterThenAGlobalOne()
    {
        var store = PresetStore.Open(File_("presets.json"));
        var global = store.Add("Todos", null, Array.Empty<RegionSpec>());
        var other = store.Add("De outro", "Outro", Array.Empty<RegionSpec>());
        var mine = store.Add("Meu", "Santizza", Array.Empty<RegionSpec>());
        var second = store.Add("Meu 2", "Santizza", Array.Empty<RegionSpec>());

        Assert.Same(mine, store.Pick("Santizza"));
        store.SetLastUsed("Santizza", second.Id);
        Assert.Same(second, store.Pick("Santizza"));
        store.SetLastUsed("Santizza", other.Id); // o ultimo usado nao serve para este personagem
        Assert.Same(mine, store.Pick("Santizza"));
        Assert.Same(global, store.Pick("Desconhecido"));
        Assert.Null(PresetStore.Open(File_("vazio.json")).Pick("Santizza"));
    }

    [Fact]
    public void Remove_ForgetsTheLastUsedEntry()
    {
        var store = PresetStore.Open(File_("presets.json"));
        var preset = store.Add("A", "Santizza", Array.Empty<RegionSpec>());
        store.SetLastUsed("Santizza", preset.Id);

        store.Remove(preset);

        Assert.Empty(store.Presets);
        Assert.Null(store.Pick("Santizza"));
    }

    [Fact]
    public void Rename_CanMoveAPresetBetweenACharacterAndEveryone()
    {
        var store = PresetStore.Open(File_("presets.json"));
        store.Add("Fixo", null, Array.Empty<RegionSpec>());
        var preset = store.Add("Meu", "Santizza", Array.Empty<RegionSpec>());

        store.Rename(preset, "Fixo", null);

        Assert.Equal(("Fixo (2)", null), (preset.Name, preset.Character));
        Assert.True(preset.IsGlobal);
    }

    [Fact]
    public void ExportAndImport_BringPresetsBackWithNewIdentityAndNoNameClash()
    {
        var source = PresetStore.Open(File_("a.json"));
        var preset = source.Add("Combate", "Santizza", new[] { Region("Vida", "id-1") });
        string exported = File_("export.json");
        source.Export(exported, new[] { preset });

        var target = PresetStore.Open(File_("b.json"));
        target.Add("Combate", "Santizza", Array.Empty<RegionSpec>());
        int imported = target.Import(exported);

        Assert.Equal(1, imported);
        Assert.Equal(2, target.Presets.Count);
        var added = target.Presets[1];
        Assert.Equal(("Combate (2)", "Santizza"), (added.Name, added.Character));
        Assert.NotEqual(preset.Id, added.Id);
        Assert.Equal("Vida", added.Regions[0].Name);
        Assert.NotEqual("id-1", added.Regions[0].ObsId); // posicoes do OBS nao sao compartilhadas
        Assert.Contains("Combate (2)", PresetStore.Open(File_("b.json")).Presets.Select(p => p.Name)); // importar ja salva
    }

    [Fact]
    public void Import_AcceptsALayoutSavedByOlderVersions()
    {
        var layout = new Layout();
        layout.Regions.Add(Region("Minimapa"));
        string old = File_("Meu layout.json");
        layout.Save(old);

        var store = PresetStore.Open(File_("presets.json"));
        Assert.Equal(1, store.Import(old));

        var preset = Assert.Single(store.Presets);
        Assert.Equal("Meu layout", preset.Name);
        Assert.True(preset.IsGlobal);
        Assert.Equal("Minimapa", Assert.Single(preset.Regions).Name);
    }

    [Fact]
    public void Import_RejectsInvalidFilesWithoutChangingTheStore()
    {
        var store = PresetStore.Open(File_("presets.json"));
        store.Add("Existente", null, Array.Empty<RegionSpec>());
        string notJson = File_("lixo.json");
        File.WriteAllText(notJson, "nada a ver");
        string badRegion = File_("ruim.json");
        File.WriteAllText(badRegion, "{\"Version\":1,\"Presets\":[{\"Name\":\"X\",\"Regions\":[{\"Name\":\"\",\"W\":0}]}]}");

        Assert.Throws<InvalidDataException>(() => store.Import(notJson));
        Assert.Throws<InvalidDataException>(() => store.Import(badRegion));
        Assert.Single(store.Presets);
    }

    [Fact]
    public void Add_RefusesToPassTheLimit()
    {
        var store = PresetStore.Open(File_("presets.json"));
        for (int i = 0; i < PresetStore.MaxPresets; i++) store.Add("P" + i, null, Array.Empty<RegionSpec>());

        Assert.Throws<InvalidOperationException>(() => store.Add("A mais", null, Array.Empty<RegionSpec>()));
    }
}
