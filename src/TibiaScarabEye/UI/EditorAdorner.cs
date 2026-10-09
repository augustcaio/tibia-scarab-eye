using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Desenha, acima da miniatura do jogo no editor de áreas: o contorno de cada overlay, o recorte de origem da área
// selecionada (tracejado) e o retângulo que está sendo desenhado para criar uma área.
internal sealed class EditorAdorner : Adorner {
    internal struct Mark { public Rectangle Rect; public bool Selected; }
    static readonly Color SourceColor=Color.FromArgb(0,210,255);
    Rectangle preview, source, drawing;
    Mark[] marks=new Mark[0];
    internal void Update(Rectangle preview,Mark[] marks,Rectangle source,Rectangle drawing) {
        this.preview=preview; this.marks=marks; this.source=source; this.drawing=drawing; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(preview.IsEmpty) return;
        var g=e.Graphics;
        g.SetClip(preview);
        foreach(var mark in marks) if(!mark.Selected) Outline(g,mark.Rect,Color.White,2,4,DashStyle.Solid);
        foreach(var mark in marks) if(mark.Selected) Outline(g,mark.Rect,Theme.Gold,3,5,DashStyle.Solid);
        if(!source.IsEmpty) Outline(g,source,SourceColor,2,4,DashStyle.Dash);
        if(!drawing.IsEmpty) Outline(g,drawing,Theme.Gold,3,5,DashStyle.Solid);
    }
    static void Outline(Graphics g,Rectangle rect,Color color,int width,int shadowWidth,DashStyle style) {
        using(var shadow=new Pen(Color.Black,shadowWidth)) using(var border=new Pen(color,width) { DashStyle=style }) {
            g.DrawRectangle(shadow,rect);
            g.DrawRectangle(border,rect);
        }
    }
}
