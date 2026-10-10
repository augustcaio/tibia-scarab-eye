using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Combo escura: o corpo vem do desenho por item; o botão de seta e o contorno são redesenhados por cima depois de cada
// pintura do sistema, porque o ComboBox não permite pintar esse trecho de outro jeito.
internal sealed class ThemedComboBox : ComboBox {
    const int WmPaint=0xF;
    public ThemedComboBox() {
        DrawMode=DrawMode.OwnerDrawFixed; FlatStyle=FlatStyle.Flat; BackColor=Theme.Surface; ForeColor=Theme.Ink; ItemHeight=20;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        bool hot=(e.State&DrawItemState.Selected)!=0 && (e.State&DrawItemState.ComboBoxEdit)==0;
        using(var back=new SolidBrush(hot?Theme.Teal:Theme.Surface)) e.Graphics.FillRectangle(back,e.Bounds);
        if(e.Index>=0) TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,Rectangle.Inflate(e.Bounds,-6,0),Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if(m.Msg==WmPaint) PaintChrome();
    }
    void PaintChrome() {
        using(var g=Graphics.FromHwnd(Handle)) {
            var r=ClientRectangle;
            int arrowWidth=Math.Max(22,SystemInformation.VerticalScrollBarWidth+4);
            var arrow=new Rectangle(r.Right-arrowWidth-1,r.Top+1,arrowWidth,r.Height-2);
            using(var fill=new SolidBrush(Enabled?Theme.Raised:Theme.Surface)) g.FillRectangle(fill,arrow);
            using(var divider=new Pen(Theme.Line)) g.DrawLine(divider,arrow.Left,arrow.Top,arrow.Left,arrow.Bottom-1);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            int cx=arrow.Left+arrow.Width/2, cy=arrow.Top+arrow.Height/2;
            using(var chevron=new Pen(Enabled?Theme.Gold:Theme.Muted,1.6f)) g.DrawLines(chevron,new[]{ new Point(cx-4,cy-2),new Point(cx,cy+2),new Point(cx+4,cy-2) });
            g.SmoothingMode=SmoothingMode.None;
            // a moldura interna do sistema (clara quando desabilitada) é coberta por um segundo fio na cor do campo
            using(var inside=new Pen(Theme.Surface)) g.DrawRectangle(inside,1,1,r.Width-3,r.Height-3);
            using(var border=new Pen(ContainsFocus||DroppedDown?Theme.GoldMid:Theme.Line)) g.DrawRectangle(border,0,0,r.Width-1,r.Height-1);
        }
    }
}
