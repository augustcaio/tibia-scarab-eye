using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

internal sealed class StoneButton : Button {
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        Theme.Frame(e.Graphics,ClientRectangle);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10),ForeColor,BackColor);
    }
}
