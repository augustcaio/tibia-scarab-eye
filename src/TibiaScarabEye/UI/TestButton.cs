using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TibiaScarabEye.Alerts;

namespace TibiaScarabEye.UI;

// Botão só com ícone que toca um alerta para teste: triângulo (equipado), exclamação (aviso dos 1:30) e X (retirado).
// Mesma moldura de pedra dos botões e dos botões da faixa do topo.
internal sealed class TestButton : Button {
    readonly AlertKind kind;
    bool hover;
    public TestButton(AlertKind kind,string tip) {
        this.kind=kind; FlatStyle=FlatStyle.Flat; BackColor=Theme.Surface; ForeColor=Theme.Ink; Cursor=Cursors.Hand; UseVisualStyleBackColor=false;
        Margin=Padding.Empty; AccessibleName=tip; AccessibleDescription=tip;
    }
    public AlertKind Kind { get { return kind; } }
    protected override Size DefaultSize { get { return new Size(LogicalToDeviceUnits(28),LogicalToDeviceUnits(28)); } }
    protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.Clear(hover?Color.FromArgb(88,88,83):BackColor);
        Theme.Frame(g,ClientRectangle);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        int cx=Width/2, cy=Height/2, r=Math.Max(4,Height/5);
        Color ink=Enabled?Theme.Ink:Theme.Muted;
        if(kind==AlertKind.Equipped) {
            using(var brush=new SolidBrush(Theme.Gold)) g.FillPolygon(brush,new[]{new Point(cx-r/2,cy-r),new Point(cx-r/2,cy+r),new Point(cx+r,cy)});
        } else if(kind==AlertKind.Warning) {
            using(var pen=new Pen(Color.FromArgb(234,170,48),Math.Max(2,Height/12f)) { StartCap=LineCap.Round, EndCap=LineCap.Round }) using(var dot=new SolidBrush(Color.FromArgb(234,170,48))) {
                g.DrawLine(pen,cx,cy-r,cx,cy+r/4);
                g.FillEllipse(dot,cx-pen.Width/2f,cy+r/2f,pen.Width,pen.Width);
            }
        } else {
            using(var pen=new Pen(ink,2)) { g.DrawLine(pen,cx-r,cy-r,cx+r,cy+r); g.DrawLine(pen,cx-r,cy+r,cx+r,cy-r); }
        }
        if(Focused) ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6),ForeColor,BackColor);
    }
}
