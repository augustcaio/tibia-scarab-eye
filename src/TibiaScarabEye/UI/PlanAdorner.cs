using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Desenha, acima da miniatura do jogo no planejador, o contorno de cada área. A grade de encaixe não é desenhada.
internal sealed class PlanAdorner : Adorner {
    internal struct Mark { public Rectangle Rect; public bool Selected; }
    Rectangle preview;
    Mark[] marks=new Mark[0];
    internal void Update(Rectangle preview,Mark[] marks) {
        this.preview=preview; this.marks=marks; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(preview.IsEmpty) return;
        var g=e.Graphics;
        g.SetClip(preview);
        foreach(var mark in marks) DrawMark(g,mark);
    }
    void DrawMark(Graphics g,Mark mark) {
        using(var shadow=new Pen(Color.Black,mark.Selected?5:4)) using(var border=new Pen(mark.Selected?Theme.Gold:Color.White,mark.Selected?3:2)) {
            g.DrawRectangle(shadow,mark.Rect);
            g.DrawRectangle(border,mark.Rect);
        }
    }
}
