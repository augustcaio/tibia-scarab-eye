using System;
using System.IO;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class AlertSettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "tibiascarabeye-alerts-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

    [Fact]
    public void MissingFile_StartsWithEverythingOn()
    {
        var settings = AlertSettings.Load(Path.Combine(folder, "alerts.json"));

        Assert.True(settings.IsEnabled(PlasmaItem.Collar));
        Assert.True(settings.IsEnabled(PlasmaItem.Ring));
    }

    [Fact]
    public void SaveThenLoad_KeepsEachSwitch()
    {
        string path = Path.Combine(folder, "alerts.json");
        var settings = new AlertSettings();
        settings.Set(PlasmaItem.Collar, false);

        settings.Save(path);
        var loaded = AlertSettings.Load(path);

        Assert.False(loaded.IsEnabled(PlasmaItem.Collar));
        Assert.True(loaded.IsEnabled(PlasmaItem.Ring));
    }

    [Fact]
    public void CorruptFile_FallsBackToTheDefaults()
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "alerts.json");
        File.WriteAllText(path, "{ isto nao e json");

        var settings = AlertSettings.Load(path);

        Assert.True(settings.CollarEnabled && settings.RingEnabled);
    }
}

public class FileAlertSoundTests : IDisposable
{
    private readonly string user = Path.Combine(Path.GetTempPath(), "tibiascarabeye-snd-u-" + Guid.NewGuid().ToString("N"));
    private readonly string app = Path.Combine(Path.GetTempPath(), "tibiascarabeye-snd-a-" + Guid.NewGuid().ToString("N"));

    public FileAlertSoundTests() { Directory.CreateDirectory(user); Directory.CreateDirectory(app); }

    public void Dispose() { Directory.Delete(user, true); Directory.Delete(app, true); }

    [Theory]
    [InlineData(PlasmaItem.Collar, AlertKind.Equipped, "collar-on.wav")]
    [InlineData(PlasmaItem.Collar, AlertKind.Warning, "collar-warning.wav")]
    [InlineData(PlasmaItem.Collar, AlertKind.Removed, "collar-off.wav")]
    [InlineData(PlasmaItem.Ring, AlertKind.Equipped, "ring-on.wav")]
    [InlineData(PlasmaItem.Ring, AlertKind.Warning, "ring-warning.wav")]
    [InlineData(PlasmaItem.Ring, AlertKind.Removed, "ring-off.wav")]
    public void FileNamesFollowTheDocumentedConvention(PlasmaItem item, AlertKind kind, string expected)
    {
        Assert.Equal(expected, FileAlertSound.FileName(item, kind));
    }

    [Fact]
    public void TheUserFolderWinsOverTheInstalledOne()
    {
        File.WriteAllBytes(Path.Combine(app, "ring-warning.wav"), new byte[1]);
        var sound = new FileAlertSound(user, app);
        Assert.Equal(Path.Combine(app, "ring-warning.wav"), sound.Find(PlasmaItem.Ring, AlertKind.Warning));

        File.WriteAllBytes(Path.Combine(user, "ring-warning.wav"), new byte[1]);

        Assert.Equal(Path.Combine(user, "ring-warning.wav"), sound.Find(PlasmaItem.Ring, AlertKind.Warning));
        Assert.Null(sound.Find(PlasmaItem.Collar, AlertKind.Equipped));
    }

    // Os sons que acompanham o programa: todos presentes e em WAV PCM (o SoundPlayer nao toca WAV de ponto flutuante).
    [Theory]
    [InlineData("collar-on.wav")]
    [InlineData("collar-warning.wav")]
    [InlineData("collar-off.wav")]
    [InlineData("ring-on.wav")]
    [InlineData("ring-warning.wav")]
    [InlineData("ring-off.wav")]
    [InlineData("plasmas-warning.wav")]
    public void TheShippedSoundsExistAndArePcmWavs(string name)
    {
        string path = new FileAlertSound(FileAlertSound.AppFolder).Find(name);

        Assert.True(path != null, "Faltou " + name + " na pasta sounds ao lado do programa.");
        var header = File.ReadAllBytes(path).AsSpan(0, 44).ToArray();
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(header, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(header, 8, 4));
        Assert.Equal(1, BitConverter.ToUInt16(header, 20)); // 1 = PCM
        Assert.Equal(16, BitConverter.ToUInt16(header, 34));
    }
}
