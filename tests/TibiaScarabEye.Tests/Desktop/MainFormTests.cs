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

        using var bitmap = new Bitmap(main.Width, main.Height);
        main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, main.Size));
        DesktopHarness.SaveArtifact(bitmap, "interface-preview.png");
        main.Close();
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

        using var bitmap = new Bitmap(main.Width, main.Height);
        main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, main.Size));
        DesktopHarness.SaveArtifact(bitmap, $"interface-{main.ClientSize.Width}x{main.ClientSize.Height}.png");

        AssertInsideParents(main);
        main.Close();
    }

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
