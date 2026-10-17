using System;
using System.IO;
using TibiaScarabEye.Alerts;
using Xunit;

namespace TibiaScarabEye.Tests.Alerts;

public class AlertLogTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "tibiascarabeye-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

    [Fact]
    public void Write_AppendsTimestampedLinesAndCreatesTheFolder()
    {
        string path = Path.Combine(folder, "alerts.log");
        var log = new AlertLog(path, () => new DateTime(2026, 10, 16, 18, 30, 5, 123));

        log.Write("primeiro");
        log.Write("segundo");

        Assert.Equal(new[] { "2026-10-16 18:30:05.123  primeiro", "2026-10-16 18:30:05.123  segundo" }, File.ReadAllLines(path));
    }

    [Fact]
    public void ABigOldLogIsRestartedAndAWriteFailureNeverThrows()
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "alerts.log");
        File.WriteAllBytes(path, new byte[300 * 1024]);

        var log = new AlertLog(path);
        log.Write("novo");
        Assert.Single(File.ReadAllLines(path));

        // caminho impossivel (uma pasta no lugar do arquivo): so ignora
        string asFolder = Path.Combine(folder, "pasta.log");
        Directory.CreateDirectory(asFolder);
        new AlertLog(asFolder).Write("nao grava");
    }
}
