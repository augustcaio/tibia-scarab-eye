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
}
