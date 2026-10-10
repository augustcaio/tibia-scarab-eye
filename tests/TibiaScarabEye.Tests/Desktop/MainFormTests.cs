using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.UI;
using Xunit;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class MainFormTests
{
    // Janela principal isolada: presets numa pasta temporaria e um "Tibia" falso (SourceWindow) que os testes abrem e fecham,
    // para nunca tocar nos presets reais nem no jogo que estiver aberto.
    private sealed class Rig : IDisposable
    {
        public readonly SourceWindow Source = new SourceWindow();
        public readonly string PresetsPath = Path.Combine(Path.GetTempPath(), "tibiascarabeye-main-" + Guid.NewGuid().ToString("N") + ".json");
        public string Character = "Teste";
        public bool TibiaOpen = true;
        public readonly MainForm Main;

        public Rig(Action<PresetStore> seed = null, bool tibiaOpen = true)
        {
            TibiaOpen = tibiaOpen;
            if (seed != null) { var store = PresetStore.Open(PresetsPath); seed(store); store.Save(); }
            Main = new MainForm(PresetsPath, prefer => TibiaOpen ? new TibiaWindow { Handle = Source.Handle, Title = "Tibia - " + Character, Character = Character } : null);
            DesktopHarness.ShowOffscreen(Main);
        }

        public List<Overlay> Overlays => DesktopHarness.Field<List<Overlay>>(Main, "overlays");
        public StatusLamp Lamp => DesktopHarness.Field<StatusLamp>(Main, "lamp");
        public Preset Active => DesktopHarness.Field<Preset>(Main, "active");
        public ComboBox Presets => DesktopHarness.Field<ComboBox>(Main, "presets");
        public PresetStore Reopen() => PresetStore.Open(PresetsPath);
        public void Detect() => DesktopHarness.Call(Main, "DetectTibia");

        public RegionSpec Spec(string name, bool hidden = false) =>
            new RegionSpec { Name = name, X = .1, Y = .1, W = .2, H = .1, Left = DesktopHarness.Offscreen.X, Top = DesktopHarness.Offscreen.Y, Width = 120, Height = 40, Opacity = 100, Hidden = hidden };

        public void Dispose()
        {
            Main.Dispose();
            Source.Dispose();
            if (File.Exists(PresetsPath)) File.Delete(PresetsPath);
        }
    }

    private static RegionSpec Region(string name, double x = .1, int width = 60) =>
        new RegionSpec { Name = name, X = x, Y = .1, W = .1, H = .1, Width = width, Height = 40 };

    [WinFormsFact]
    public void MainForm_ConstructsAndRendersToBitmap()
    {
        using var rig = new Rig(store =>
        {
            store.Add("Combate", "Santizza", new[] { Region("Vida"), Region("Minimapa", .6, 106) });
            store.Add("Caça", "Santizza", new RegionSpec[0]);
        }, tibiaOpen: false);
        rig.Character = "Santizza";
        rig.TibiaOpen = true;
        // Os atalhos globais podem estar ocupados por outra instancia aberta; fixa o estado normal para a imagem da documentacao.
        DesktopHarness.SetField(rig.Main, "hotkey", true);
        DesktopHarness.SetField(rig.Main, "visibilityHotkey", true);
        rig.Detect();
        DesktopHarness.Call(rig.Main, "UpdateStatus");

        using var bitmap = DesktopHarness.RenderShaped(rig.Main);
        DesktopHarness.SaveArtifact(bitmap, "interface-preview.png");
    }

    // ----- O Tibia e o indicador -----

    [WinFormsFact]
    public void Lamp_FollowsTheTibiaWindowAndShowsTheCharacter()
    {
        using var rig = new Rig();
        Assert.Equal(LampState.Reading, rig.Lamp.State);
        Assert.Equal("Teste", rig.Lamp.Detail);

        rig.TibiaOpen = false;
        rig.Detect();
        Assert.Equal(LampState.Searching, rig.Lamp.State);
        Assert.Equal(IntPtr.Zero, DesktopHarness.Field<IntPtr>(rig.Main, "source"));

        rig.TibiaOpen = true; rig.Character = "Outro";
        rig.Detect();
        Assert.Equal(LampState.Reading, rig.Lamp.State);
        Assert.Equal("Outro", rig.Lamp.Detail);
        Assert.Equal(rig.Source.Handle, DesktopHarness.Field<IntPtr>(rig.Main, "source"));
    }

    [WinFormsFact]
    public void WithoutTibia_TheEditorAndTheOverlayActionsAreOff()
    {
        using var rig = new Rig(tibiaOpen: false);

        Assert.False(DesktopHarness.Field<Button>(rig.Main, "editor").Enabled);
        Assert.False(DesktopHarness.Field<Button>(rig.Main, "mode").Enabled);
        Assert.False(DesktopHarness.Field<Button>(rig.Main, "obsCapture").Enabled);
        Assert.Equal(LampState.Searching, rig.Lamp.State);

        rig.TibiaOpen = true; rig.Detect();
        Assert.True(DesktopHarness.Field<Button>(rig.Main, "editor").Enabled);
    }

    // ----- Presets por personagem, salvos sozinhos -----

    [WinFormsFact]
    public void TibiaOpening_LoadsTheCharactersLastPreset()
    {
        using var rig = new Rig(store =>
        {
            store.Add("Do outro", "Outro", new[] { Region("X") });
            var mine = store.Add("Meu", "Teste", new[] { Region("A"), Region("B", .3) });
            store.SetLastUsed("Teste", mine.Id);
        });

        Assert.Equal("Meu", rig.Active.Name);
        Assert.Equal(new[] { "A", "B" }, rig.Overlays.Select(o => o.Spec.Name));
        Assert.All(rig.Overlays, o => Assert.False(o.Visible));
        Assert.Equal(new[] { "Meu" }, rig.Presets.Items.Cast<object>().Select(i => i.ToString())); // o preset de outro personagem nao aparece
    }

    [WinFormsFact]
    public void ChangingCharacter_SavesTheCurrentPresetAndLoadsTheOthers()
    {
        using var rig = new Rig(store =>
        {
            store.Add("Um", "Um", new[] { Region("U") });
            store.Add("Dois", "Dois", new[] { Region("D1"), Region("D2", .3) });
        });
        rig.Character = "Um"; rig.Detect();
        Assert.Equal(new[] { "U" }, rig.Overlays.Select(o => o.Spec.Name));
        rig.Overlays[0].Spec.Name = "Renomeada"; // mudanca feita enquanto o personagem Um estava logado

        rig.Character = "Dois"; rig.Detect();

        Assert.Equal("Dois", rig.Active.Name);
        Assert.Equal(new[] { "D1", "D2" }, rig.Overlays.Select(o => o.Spec.Name));
        Assert.Equal("Renomeada", rig.Reopen().Presets.First(p => p.Name == "Um").Regions[0].Name);
    }

    [WinFormsFact]
    public void CreatingTheFirstArea_CreatesADefaultPresetAndAutosaves()
    {
        using var rig = new Rig();
        Assert.Null(rig.Active);
        Assert.Empty(rig.Reopen().Presets);

        DesktopHarness.Call(rig.Main, "EnsureActive");
        DesktopHarness.Call(rig.Main, "CreateOverlay", rig.Spec("Vida"));
        DesktopHarness.Call(rig.Main, "SaveActive");

        var saved = Assert.Single(rig.Reopen().Presets);
        Assert.Equal(("Padrão", "Teste"), (saved.Name, saved.Character));
        Assert.Equal("Vida", Assert.Single(saved.Regions).Name);
    }

    [WinFormsFact]
    public void SavingWithoutTheTibia_DoesNotWipeThePreset()
    {
        using var rig = new Rig(store => store.Add("Meu", "Teste", new[] { Region("A") }));
        rig.TibiaOpen = false; rig.Detect(); // o jogo fechou: as overlays saem, o preset fica

        DesktopHarness.Call(rig.Main, "SaveActive");

        Assert.Equal("A", Assert.Single(rig.Reopen().Presets.Single().Regions).Name);
        Assert.Empty(rig.Overlays);
    }

    [WinFormsFact]
    public void PickingAnotherPresetInTheList_SwitchesTheOverlays()
    {
        using var rig = new Rig(store =>
        {
            store.Add("Primeiro", "Teste", new[] { Region("P") });
            store.Add("Segundo", "Teste", new[] { Region("S1"), Region("S2", .3) });
        });
        Assert.Equal("Primeiro", rig.Active.Name);

        rig.Presets.SelectedIndex = 1;

        Assert.Equal("Segundo", rig.Active.Name);
        Assert.Equal(new[] { "S1", "S2" }, rig.Overlays.Select(o => o.Spec.Name));
        Assert.Equal(rig.Active.Id, DesktopHarness.Field<PresetStore>(rig.Main, "store").Pick("Teste").Id); // lembrado para este personagem
    }

    [WinFormsFact]
    public void DuplicatePreset_CopiesTheAreasWithFreshObsIdentity()
    {
        using var rig = new Rig(store => store.Add("Meu", "Teste", new[] { new RegionSpec { Name = "A", X = .1, Y = .1, W = .1, H = .1, Width = 60, Height = 40, ObsId = "fixo" } }));

        DesktopHarness.Call(rig.Main, "DuplicatePreset");

        Assert.Equal("Meu cópia", rig.Active.Name);
        var copy = Assert.Single(rig.Overlays);
        Assert.Equal("A", copy.Spec.Name);
        Assert.NotEqual("fixo", copy.Spec.ObsId);
        Assert.Equal(2, rig.Reopen().Presets.Count);
    }

    // ----- Camadas e modo jogo -----

    // A overlay nasce escondida: so o editor mostra onde ela ficara; ela aparece na tela apenas no modo jogo.
    [WinFormsFact]
    public void NewArea_StaysHiddenUntilPlayMode()
    {
        using var rig = new Rig();
        DesktopHarness.Call(rig.Main, "CreateOverlay", rig.Spec("Vida"));
        var overlay = rig.Overlays[0];

        DesktopHarness.Call(rig.Main, "TickSource");
        Assert.False(overlay.Visible, "Area recem-criada nao pode aparecer na tela antes do modo jogo");

        DesktopHarness.Call(rig.Main, "ToggleMode");
        Assert.True(overlay.Visible, "Modo jogo deve mostrar as areas");

        DesktopHarness.Call(rig.Main, "ToggleMode");
        Assert.False(overlay.Visible, "Voltar para a edicao deve esconder as areas");
    }

    // Camada escondida nunca aparece no modo jogo (nem no OBS, que segue Overlay.Visible); as demais aparecem.
    [WinFormsFact]
    public void HiddenLayer_StaysOffInPlayMode()
    {
        using var rig = new Rig();
        DesktopHarness.Call(rig.Main, "CreateOverlay", rig.Spec("Visivel"));
        DesktopHarness.Call(rig.Main, "CreateOverlay", rig.Spec("Escondida", hidden: true));

        DesktopHarness.Call(rig.Main, "ToggleMode");

        Assert.True(rig.Overlays[0].Visible, "A camada visivel deve aparecer no modo jogo");
        Assert.False(rig.Overlays[1].Visible, "A camada escondida nao pode aparecer");
    }

    // ----- Janela -----

    // Em qualquer largura/altura acima do minimo, nenhum controle pode ficar cortado pelo painel que o contem nem sobreposto a outro.
    [WinFormsTheory]
    [InlineData(0, 0)]
    [InlineData(300, 0)]
    [InlineData(0, 250)]
    [InlineData(500, 300)]
    public void MainForm_KeepsEveryControlInsideTheWindow(int extraWidth, int extraHeight)
    {
        using var rig = new Rig();
        var main = rig.Main;
        main.ClientSize = new Size(main.MinimumSize.Width - (main.Width - main.ClientSize.Width) + extraWidth,
                                   main.MinimumSize.Height - (main.Height - main.ClientSize.Height) + extraHeight);
        Application.DoEvents();

        using var bitmap = DesktopHarness.RenderShaped(main);
        DesktopHarness.SaveArtifact(bitmap, $"interface-{main.ClientSize.Width}x{main.ClientSize.Height}.png");

        AssertInsideParents(main);
    }

    // Sem moldura nativa: a janela e um retangulo, o arrasto pelo cabecalho e o redimensionamento pelas bordas dependem de WM_NCHITTEST.
    [WinFormsFact]
    public void MainForm_IsFramelessAndDraggableByTheHeader()
    {
        using var rig = new Rig();
        var main = rig.Main;

        Assert.Equal(FormBorderStyle.None, main.FormBorderStyle);
        Assert.Null(main.Region);
        var size = main.ClientSize;

        Assert.Equal(2, HitTest(main, size.Width / 2, 20));
        Assert.Equal(2, HitTest(main, 80, 40));
        Assert.Equal(1, HitTest(main, size.Width / 2, size.Height / 2));
        Assert.Equal(10, HitTest(main, 2, size.Height / 2));
        Assert.Equal(11, HitTest(main, size.Width - 2, size.Height / 2));
        Assert.Equal(15, HitTest(main, size.Width / 2, size.Height - 2));
        Assert.Equal(17, HitTest(main, size.Width - 2, size.Height - 2));
    }

    private static int HitTest(Form form, int x, int y)
    {
        var screen = form.PointToScreen(new Point(x, y));
        return (int)SendMessage(form.Handle, 0x84, IntPtr.Zero, (IntPtr)((screen.Y << 16) | (screen.X & 0xFFFF)));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    private static void AssertInsideParents(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            foreach (Control other in parent.Controls)
                Assert.True(child == other || !child.Bounds.IntersectsWith(other.Bounds), $"{child.GetType().Name} '{child.Text}' sobrepoe {other.GetType().Name} '{other.Text}' em {parent.GetType().Name}");
            Assert.True(child.Width > 0 && child.Height > 0, $"{child.GetType().Name} '{child.Text}' sem area visivel");
            Assert.True(new Rectangle(Point.Empty, parent.ClientSize).Contains(child.Bounds), $"{child.GetType().Name} '{child.Text}' cortado por {parent.GetType().Name}: {child.Bounds} em {parent.ClientSize}");
            AssertInsideParents(child);
        }
    }
}
