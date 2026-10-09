using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using TibiaScarabEye.Interop;

namespace TibiaScarabEye.UI;

// Desenha, acima da miniatura do jogo no planejador, a grade e o contorno de cada área.
// Janela layered com alpha por pixel: a grade é translúcida e deixa o jogo visível por baixo.
internal sealed class PlanAdorner : Adorner {
    internal struct Mark { public Rectangle Rect; public bool Selected; }
    const int MajorEvery=4;
    static readonly Color Minor=Color.FromArgb(70,235,235,222), Major=Color.FromArgb(125,235,235,222);
    Rectangle preview;
    double cell;
    Mark[] marks=new Mark[0];
    Bitmap frame;
    internal void Update(Rectangle preview,double cell,Mark[] marks) {
        this.preview=preview; this.cell=cell; this.marks=marks; Render();
    }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); Render(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Render(); }
    protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); Render(); }
    void Render() {
        if(!IsHandleCreated || !Visible || Width<1 || Height<1) return;
        if(frame==null || frame.Size!=ClientSize) { if(frame!=null) frame.Dispose(); frame=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb); }
        using(var g=Graphics.FromImage(frame)) {
            g.Clear(Color.Transparent);
            if(!preview.IsEmpty) {
                g.SetClip(preview);
                DrawGrid(g);
                foreach(var mark in marks) DrawMark(g,mark);
            }
        }
        Native.PresentLayered(Handle,frame,Location);
    }
    void DrawGrid(Graphics g) {
        if(cell<=0) return;
        // Com a janela do jogo reduzida na prévia, a célula pode ficar menor que 4 px: desenha uma linha a cada N células
        // para a grade nunca virar um borrão. O encaixe continua por célula.
        double step=cell*Math.Ceiling(4/cell);
        using(var minor=new Pen(Minor)) using(var major=new Pen(Major)) {
            int i=0;
            for(double x=preview.Left;x<=preview.Right+0.5;x+=step,i++) g.DrawLine(i%MajorEvery==0?major:minor,(int)Math.Round(x),preview.Top,(int)Math.Round(x),preview.Bottom);
            i=0;
            for(double y=preview.Top;y<=preview.Bottom+0.5;y+=step,i++) g.DrawLine(i%MajorEvery==0?major:minor,preview.Left,(int)Math.Round(y),preview.Right,(int)Math.Round(y));
        }
    }
    void DrawMark(Graphics g,Mark mark) {
        using(var shadow=new Pen(Color.Black,mark.Selected?5:4)) using(var border=new Pen(mark.Selected?Theme.Gold:Color.White,mark.Selected?3:2)) {
            g.DrawRectangle(shadow,mark.Rect);
            g.DrawRectangle(border,mark.Rect);
        }
    }
    protected override void Dispose(bool disposing) { if(disposing && frame!=null) frame.Dispose(); base.Dispose(disposing); }
}
