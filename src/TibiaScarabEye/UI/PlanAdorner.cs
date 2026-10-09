using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Desenha, acima da miniatura do jogo no planejador, a grade do mapa e o contorno de cada área.
internal sealed class PlanAdorner : Adorner {
    internal struct Mark { public Rectangle Rect; public bool Selected; }
    Rectangle preview, grid;
    double cell;
    Mark[] marks=new Mark[0];
    internal void Update(Rectangle preview,Rectangle grid,double cell,Mark[] marks) {
        this.preview=preview; this.grid=grid; this.cell=cell; this.marks=marks; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(preview.IsEmpty) return;
        var g=e.Graphics;
        g.SetClip(preview);
        DrawGrid(g);
        foreach(var mark in marks) DrawMark(g,mark);
    }
    void DrawGrid(Graphics g) {
        Rectangle area=Rectangle.Intersect(grid,preview);
        if(area.IsEmpty) return;
        g.SetClip(area);
        // Linhas opacas: a janela usa chave de cor, então transparência no traço deixaria franjas coloridas.
        // Com a janela do jogo reduzida na prévia, a célula pode ficar menor que 4 px: desenha uma linha a cada N células
        // para a grade nunca virar um borrão. O encaixe continua por célula.
        if(cell>0) {
            double step=cell*Math.Ceiling(4/cell);
            using(var pen=new Pen(Color.FromArgb(212,213,199)) { DashStyle=DashStyle.Dot }) {
                for(double x=grid.Left;x<=grid.Right+0.5;x+=step) g.DrawLine(pen,(int)Math.Round(x),grid.Top,(int)Math.Round(x),grid.Bottom);
                for(double y=grid.Top;y<=grid.Bottom+0.5;y+=step) g.DrawLine(pen,grid.Left,(int)Math.Round(y),grid.Right,(int)Math.Round(y));
            }
        }
        g.SetClip(preview);
        using(var border=new Pen(Theme.Gold,2)) g.DrawRectangle(border,grid.X,grid.Y,grid.Width-1,grid.Height-1);
    }
    void DrawMark(Graphics g,Mark mark) {
        using(var shadow=new Pen(Color.Black,mark.Selected?5:4)) using(var border=new Pen(mark.Selected?Theme.Gold:Color.White,mark.Selected?3:2)) {
            g.DrawRectangle(shadow,mark.Rect);
            g.DrawRectangle(border,mark.Rect);
        }
    }
}
