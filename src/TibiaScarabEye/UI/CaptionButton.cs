using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

internal enum CaptionGlyph { Minimize, Close }

// Botão da faixa superior da janela sem moldura: sem contorno, só um fundo que acende ao passar o mouse
// (vermelho da gema para fechar).
internal sealed class CaptionButton : Button {
    readonly CaptionGlyph glyph;
    bool hover;
    public CaptionButton(CaptionGlyph glyph) {
        this.glyph=glyph; FlatStyle=FlatStyle.Flat; Cursor=Cursors.Hand; TabStop=false; UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.Selectable,false);
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnMouseEnter(System.EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(System.EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.Clear(Theme.Background);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        if(hover) using(var fill=new SolidBrush(glyph==CaptionGlyph.Close?Theme.Danger:Theme.Raised)) using(var path=Theme.Rounded(new Rectangle(0,0,Width-1,Height-1),3)) g.FillPath(fill,path);
        int cx=Width/2, cy=Height/2, r=System.Math.Max(4,Height/5);
        using(var pen=new Pen(hover?Color.White:Theme.Ink,1.6f)) {
            if(glyph==CaptionGlyph.Minimize) g.DrawLine(pen,cx-r,cy+r/2,cx+r,cy+r/2);
            else { g.DrawLine(pen,cx-r+1,cy-r+1,cx+r-1,cy+r-1); g.DrawLine(pen,cx-r+1,cy+r-1,cx+r-1,cy-r+1); }
        }
    }
}
