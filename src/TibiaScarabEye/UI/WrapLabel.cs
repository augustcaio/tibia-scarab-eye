using System;
using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Label que informa a altura com quebra de linha para a largura oferecida pelo layout, para o texto refluir ao redimensionar.
internal sealed class WrapLabel : Label {
    const TextFormatFlags Flags=TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix;
    public WrapLabel() { AutoSize=true; UseMnemonic=false; }
    public override Size GetPreferredSize(Size proposedSize) {
        if(proposedSize.Width<=0 || proposedSize.Width>=int.MaxValue/2) return base.GetPreferredSize(proposedSize);
        var text=TextRenderer.MeasureText(Text,Font,new Size(proposedSize.Width,int.MaxValue),Flags);
        return new Size(Math.Min(text.Width,proposedSize.Width),text.Height+Padding.Vertical);
    }
    protected override void OnPaint(PaintEventArgs e) {
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Theme.Muted,Flags);
    }
}
