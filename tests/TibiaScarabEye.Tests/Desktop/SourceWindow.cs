using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.Tests.Desktop;

// Janela falsa que faz o papel do cliente do Tibia como origem das miniaturas DWM.
internal sealed class SourceWindow : IDisposable
{
    private readonly Form form;

    public SourceWindow(string title = "Tibia validation source")
    {
        form = new Form
        {
            Text = title,
            Size = new Size(640, 480),
            BackColor = Color.CornflowerBlue,
            ShowInTaskbar = false,
        };
        DesktopHarness.ShowOffscreen(form);
    }

    public IntPtr Handle => form.Handle;

    public void Dispose() => form.Dispose();
}
