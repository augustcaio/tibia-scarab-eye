using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

internal enum CaptionGlyph { Minimize, Close }

// Botão pequeno da faixa superior da janela sem moldura: mesma moldura de pedra dos botões, com ícone desenhado.
internal sealed class CaptionButton : Button {
    readonly CaptionGlyph glyph;
    bool hover;
    public CaptionButton(CaptionGlyph glyph) {
        this.glyph=glyph; FlatStyle=FlatStyle.Flat; BackColor=Theme.Surface; ForeColor=Theme.Ink; Cursor=Cursors.Hand; TabStop=false; UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.Selectable,false);
    }
    protected override void OnMouseEnter(System.EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(System.EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.Clear(hover?(glyph==CaptionGlyph.Close?Color.FromArgb(120,45,40):Color.FromArgb(88,88,83)):BackColor);
        Theme.Frame(g,ClientRectangle);
        int cx=Width/2, cy=Height/2, r=System.Math.Max(3,Height/6);
        using(var pen=new Pen(Theme.Ink,2)) {
            if(glyph==CaptionGlyph.Minimize) g.DrawLine(pen,cx-r,cy+r/2,cx+r,cy+r/2);
            else { g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.DrawLine(pen,cx-r,cy-r,cx+r,cy+r); g.DrawLine(pen,cx-r,cy+r,cx+r,cy-r); }
        }
    }
}
