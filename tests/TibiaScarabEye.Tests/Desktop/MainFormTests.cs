using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.UI;
using Xunit;

namespace TibiaScarabEye.Tests.Desktop;

[Trait("Category", "Desktop")]
public class MainFormTests
{
    [WinFormsFact]
    public void MainForm_ConstructsAndRendersToBitmap()
    {
        using var main = new MainForm();
        DesktopHarness.ShowOffscreen(main);

        // A lista real ignora janelas do proprio processo; fixa um item para que a captura de tela
        // da documentacao nao mostre o titulo de outra janela do desktop.
        var windows = DesktopHarness.Field<ComboBox>(main, "windows");
        windows.Items.Clear();
        windows.Items.Add(new WindowItem { Handle = main.Handle, Title = "Tibia - Personagem" });
        windows.SelectedIndex = 0;

        // Os atalhos globais podem estar ocupados por outra instancia aberta; fixa o estado normal para a imagem da documentacao.
        DesktopHarness.SetField(main, "hotkey", true);
        DesktopHarness.SetField(main, "visibilityHotkey", true);
        DesktopHarness.Call(main, "UpdateStatus");

        using var bitmap = DesktopHarness.RenderShaped(main);
        DesktopHarness.SaveArtifact(bitmap, "interface-preview.png");
        main.Close();
    }

    // A overlay nasce escondida: so o planejador mostra onde ela ficara; ela aparece na tela apenas no modo jogo.
    [WinFormsFact]
    public void NewArea_StaysHiddenUntilPlayMode()
    {
        using var source = new SourceWindow();
        using var main = new MainForm();
        DesktopHarness.ShowOffscreen(main);
        DesktopHarness.SetField(main, "source", source.Handle);
        var spec = new TibiaScarabEye.Layouts.RegionSpec { Name = "Vida", X = .1, Y = .1, W = .2, H = .1, Left = DesktopHarness.Offscreen.X, Top = DesktopHarness.Offscreen.Y, Width = 120, Height = 40, Opacity = 100 };
        DesktopHarness.Call(main, "CreateOverlay", spec);
        var overlay = DesktopHarness.Field<System.Collections.Generic.List<Overlay>>(main, "overlays")[0];

        DesktopHarness.Call(main, "TickSource");
        Assert.False(overlay.Visible, "Area recem-criada nao pode aparecer na tela antes do modo jogo");

        DesktopHarness.Call(main, "ToggleMode");
        Assert.True(overlay.Visible, "Modo jogo deve mostrar as areas");

        DesktopHarness.Call(main, "ToggleMode");
        Assert.False(overlay.Visible, "Voltar para a edicao deve esconder as areas");
        main.Close();
    }

    // Camada escondida nunca aparece no modo jogo (nem no OBS, que segue Overlay.Visible); as demais aparecem.
    [WinFormsFact]
    public void HiddenLayer_StaysOffInPlayMode()
    {
        using var source = new SourceWindow();
        using var main = new MainForm();
        DesktopHarness.ShowOffscreen(main);
        DesktopHarness.SetField(main, "source", source.Handle);
        RegionSpecFactory(main, "Visivel", hidden: false);
        RegionSpecFactory(main, "Escondida", hidden: true);
        var overlays = DesktopHarness.Field<System.Collections.Generic.List<Overlay>>(main, "overlays");

        DesktopHarness.Call(main, "ToggleMode");

        Assert.True(overlays[0].Visible, "A camada visivel deve aparecer no modo jogo");
        Assert.False(overlays[1].Visible, "A camada escondida nao pode aparecer");
        main.Close();
    }

    private static void RegionSpecFactory(MainForm main, string name, bool hidden)
    {
        var spec = new TibiaScarabEye.Layouts.RegionSpec { Name = name, X = .1, Y = .1, W = .2, H = .1, Left = DesktopHarness.Offscreen.X, Top = DesktopHarness.Offscreen.Y, Width = 120, Height = 40, Opacity = 100, Hidden = hidden };
        DesktopHarness.Call(main, "CreateOverlay", spec);
    }

    // Em qualquer largura/altura acima do minimo, nenhum controle pode ficar cortado pelo painel que o contem nem sobreposto a outro.
    [WinFormsTheory]
    [InlineData(0, 0)]
    [InlineData(300, 0)]
    [InlineData(0, 250)]
    [InlineData(500, 300)]
    public void MainForm_KeepsEveryControlInsideTheWindow(int extraWidth, int extraHeight)
    {
        using var main = new MainForm();
        DesktopHarness.ShowOffscreen(main);
        main.ClientSize = new Size(main.MinimumSize.Width - (main.Width - main.ClientSize.Width) + extraWidth,
                                   main.MinimumSize.Height - (main.Height - main.ClientSize.Height) + extraHeight);
        Application.DoEvents();

        using var bitmap = DesktopHarness.RenderShaped(main);
        DesktopHarness.SaveArtifact(bitmap, $"interface-{main.ClientSize.Width}x{main.ClientSize.Height}.png");

        AssertInsideParents(main);
        main.Close();
    }

    // Sem moldura nativa: o formato (painel + logo), o arrasto pelo topo e o redimensionamento pelas bordas dependem do Region e de WM_NCHITTEST.
    [WinFormsFact]
    public void MainForm_IsFramelessWithEmblemCutout()
    {
        using var main = new MainForm();
        DesktopHarness.ShowOffscreen(main);

        Assert.Equal(FormBorderStyle.None, main.FormBorderStyle);
        var size = main.ClientSize;
        Assert.False(main.Region.IsVisible(new Point(4, 4)), "canto superior esquerdo deve ser vazio");
        Assert.True(main.Region.IsVisible(new Point(82, 50)), "o emblema do escaravelho deve fazer parte da janela");
        Assert.False(main.Region.IsVisible(new Point(size.Width / 2, 20)), "o topo central e vazio: o emblema fica no canto esquerdo");
        Assert.True(main.Region.IsVisible(new Point(4, size.Height - 4)), "o painel deve fazer parte da janela");

        Assert.Equal(2, HitTest(main, size.Width / 2, 20));
        Assert.Equal(1, HitTest(main, size.Width / 2, size.Height / 2));
        Assert.Equal(10, HitTest(main, 2, size.Height / 2));
        Assert.Equal(11, HitTest(main, size.Width - 2, size.Height / 2));
        Assert.Equal(15, HitTest(main, size.Width / 2, size.Height - 2));
        Assert.Equal(17, HitTest(main, size.Width - 2, size.Height - 2));
        main.Close();
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
