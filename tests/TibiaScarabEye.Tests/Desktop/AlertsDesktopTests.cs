using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using TibiaScarabEye.Alerts;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.Tests.Alerts;
using TibiaScarabEye.UI;
using Xunit;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class AlertsDesktopTests
{
    private sealed class FakeSound : IAlertSound
    {
        public readonly List<(PlasmaItem item, AlertKind kind)> Played = new();
        public int Combined;
        public void Play(PlasmaItem item, AlertKind kind) => Played.Add((item, kind));
        public void PlayCombinedWarning() => Combined++;
    }

    // Janela principal isolada: presets e alertas em arquivos temporarios, um "Tibia" falso, som e relogio falsos, e uma leitura
    // da tela que devolve um quadro com o sprite de 'Showing' dentro da area recortada (ou sem sprite nenhum).
    private sealed class Rig : IDisposable
    {
        public readonly SourceWindow Source = new SourceWindow();
        public readonly string PresetsPath = Path.Combine(Path.GetTempPath(), "tibiascarabeye-alerts-p-" + Guid.NewGuid().ToString("N") + ".json");
        public readonly string AlertsPath = Path.Combine(Path.GetTempPath(), "tibiascarabeye-alerts-a-" + Guid.NewGuid().ToString("N") + ".json");
        public readonly FakeSound Sound = new FakeSound();
        public TimeSpan Now = TimeSpan.Zero;
        public PlasmaItem? Showing;
        public MainForm Main;

        public Rig(bool withArea = true)
        {
            hasArea = withArea;
            if (withArea)
            {
                var store = PresetStore.Open(PresetsPath);
                store.Add("Meu", "Teste", new[] { new RegionSpec { Name = "Equipamento", X = .1, Y = .1, W = .3, H = .3, Width = 120, Height = 90, Opacity = 100 } });
                store.Save();
            }
            Main = Open();
        }

        public MainForm Open()
        {
            var main = new MainForm(PresetsPath,
                prefer => new TibiaWindow { Handle = Source.Handle, Title = "Tibia - Teste", Character = "Teste" },
                AlertsPath, Sound, Sight, () => Now);
            DesktopHarness.ShowOffscreen(main);
            return main;
        }

        // O que o script do OBS grava: a imagem de cada area do preset (aqui, uma area de 120x90 com o sprite em (6,6)); sem areas, lista vazia;
        // 'ObsOff' simula o OBS fechado (sem imagens recentes).
        public bool ObsOff;
        private readonly bool hasArea;
        private IReadOnlyList<PixelBuffer> Sight()
        {
            if (ObsOff) return null;
            if (!hasArea) return new PixelBuffer[0];
            return new[] { Showing == null ? Frames.Empty(120, 90) : Frames.With(120, 90, (Showing.Value, new Point(6, 6))) };
        }

        // Duas leituras equivalem a um segundo de jogo para o monitor.
        public void Tick(int times = 1)
        {
            for (int i = 0; i < times; i++) { DesktopHarness.Call(Main, "TickAlerts"); Now += TimeSpan.FromMilliseconds(500); }
        }

        public Dictionary<PlasmaItem, AlertRow> Rows => DesktopHarness.Field<Dictionary<PlasmaItem, AlertRow>>(Main, "alertRows");

        public void Dispose()
        {
            Main.Dispose();
            Source.Dispose();
            foreach (var path in new[] { PresetsPath, AlertsPath }) if (File.Exists(path)) File.Delete(path);
        }
    }

    [WinFormsFact]
    public void TheWindowShowsBothItemsWithTheirSpriteAndASwitchThatStartsOn()
    {
        using var rig = new Rig();

        Assert.Equal(new[] { PlasmaItem.Collar, PlasmaItem.Ring }, rig.Rows.Keys.OrderBy(k => k));
        Assert.Equal("Collar of Red Plasma", rig.Rows[PlasmaItem.Collar].Title);
        Assert.Equal("Ring of Red Plasma", rig.Rows[PlasmaItem.Ring].Title);
        Assert.All(rig.Rows.Values, row => Assert.True(row.Checked));
        Assert.All(rig.Rows.Values, row => Assert.True(row.Visible));
    }

    [WinFormsFact]
    public void ASwitchIsSavedAndComesBackOnTheNextStart()
    {
        using var rig = new Rig();

        rig.Rows[PlasmaItem.Collar].Switch.Checked = false;

        Assert.False(AlertSettings.Load(rig.AlertsPath).IsEnabled(PlasmaItem.Collar));
        Assert.True(AlertSettings.Load(rig.AlertsPath).IsEnabled(PlasmaItem.Ring));
        rig.Main.Dispose();
        rig.Main = rig.Open();
        Assert.False(rig.Rows[PlasmaItem.Collar].Checked);
        Assert.True(rig.Rows[PlasmaItem.Ring].Checked);
    }

    [WinFormsFact]
    public void ClickingTheSwitchTurnsTheAlertOffAndOn()
    {
        using var rig = new Rig();
        var toggle = rig.Rows[PlasmaItem.Ring].Switch;

        toggle.PerformClick();
        Assert.False(rig.Rows[PlasmaItem.Ring].Checked);
        toggle.PerformClick();
        Assert.True(rig.Rows[PlasmaItem.Ring].Checked);
    }

    [WinFormsFact]
    public void TheSpriteInTheCroppedAreaPlaysTheEquippedAlertAndThenTheWarning()
    {
        using var rig = new Rig();
        rig.Showing = PlasmaItem.Ring;

        rig.Tick(4);

        Assert.Equal(new[] { (PlasmaItem.Ring, AlertKind.Equipped) }, rig.Sound.Played);
        Assert.StartsWith("Equipado", rig.Rows[PlasmaItem.Ring].Detail);
        Assert.Equal("Não equipado", rig.Rows[PlasmaItem.Collar].Detail);

        rig.Now += new TimeSpan(0, 28, 30);
        rig.Tick();

        Assert.Equal((PlasmaItem.Ring, AlertKind.Warning), rig.Sound.Played.Last());
        Assert.Equal(2, rig.Sound.Played.Count);
        Assert.Contains("restam 1:", rig.Rows[PlasmaItem.Ring].Detail);
    }

    [WinFormsFact]
    public void EachRowHasATestIconPerAlertAndItPlaysEvenWithTheSwitchOff()
    {
        using var rig = new Rig();
        rig.Rows[PlasmaItem.Ring].Switch.Checked = false;
        var buttons = rig.Rows[PlasmaItem.Ring].TestButtons;

        Assert.Equal(new[] { AlertKind.Equipped, AlertKind.Warning, AlertKind.Removed }, buttons.Select(b => b.Kind));
        foreach (var button in buttons) button.PerformClick();

        Assert.Equal(new[] { (PlasmaItem.Ring, AlertKind.Equipped), (PlasmaItem.Ring, AlertKind.Warning), (PlasmaItem.Ring, AlertKind.Removed) }, rig.Sound.Played);
        Assert.All(rig.Rows.Values.SelectMany(r => r.TestButtons), b => Assert.True(b.Right <= rig.Rows[PlasmaItem.Ring].Switch.Left && b.Width > 0));
    }

    [WinFormsFact]
    public void TheRowMenuPlaysTheCombinedWarning()
    {
        using var rig = new Rig();
        var menu = rig.Rows[PlasmaItem.Collar].ContextMenuStrip;

        var item = menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text.StartsWith("Ouvir o aviso dos dois"));
        ((System.Windows.Forms.ToolStripMenuItem)item).PerformClick();

        Assert.Equal(1, rig.Sound.Combined);
    }

    [WinFormsFact]
    public void TheSwitchOffSilencesThatItemOnly()
    {
        using var rig = new Rig();
        rig.Rows[PlasmaItem.Ring].Switch.Checked = false;
        rig.Showing = PlasmaItem.Ring;

        rig.Tick(4);

        Assert.Empty(rig.Sound.Played);
        Assert.Equal("Alerta desligado", rig.Rows[PlasmaItem.Ring].Detail);
    }

    [WinFormsFact]
    public void WithoutAnAreaTheRowsAskForOne()
    {
        using var rig = new Rig(withArea: false);
        rig.Showing = PlasmaItem.Collar;

        rig.Tick(4);

        Assert.Empty(rig.Sound.Played);
        Assert.Equal("Crie uma área sobre o slot do item", rig.Rows[PlasmaItem.Collar].Detail);
    }

    [WinFormsFact]
    public void ZeroingTheCountMakesItANewItem()
    {
        using var rig = new Rig();
        rig.Showing = PlasmaItem.Collar;
        rig.Tick(4);
        rig.Now += TimeSpan.FromMinutes(12);
        rig.Tick();

        DesktopHarness.Field<PlasmaMonitor>(rig.Main, "plasma").Restart(PlasmaItem.Collar);
        DesktopHarness.Call(rig.Main, "RefreshAlertRows");

        Assert.Contains("restam 30:00", rig.Rows[PlasmaItem.Collar].Detail);
    }

    [WinFormsFact]
    public void WithoutImagesFromObsTheRowsExplainAndTheCountKeepsRunning()
    {
        using var rig = new Rig();
        rig.Showing = PlasmaItem.Ring;
        rig.Tick(4);
        Assert.StartsWith("Equipado", rig.Rows[PlasmaItem.Ring].Detail);

        rig.ObsOff = true;
        rig.Now += new TimeSpan(0, 28, 30);
        rig.Tick();

        Assert.Equal((PlasmaItem.Ring, AlertKind.Warning), rig.Sound.Played.Last()); // o aviso sai mesmo sem imagens
        Assert.StartsWith("Equipado", rig.Rows[PlasmaItem.Ring].Detail);
        Assert.Equal("Sem imagem do OBS: abra o OBS com o script", rig.Rows[PlasmaItem.Collar].Detail);
    }
}
