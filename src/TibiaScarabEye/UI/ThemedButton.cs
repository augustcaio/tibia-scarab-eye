using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Botão plano de cantos levemente arredondados. Principal: ouro dos arabescos com texto escuro, para a ação que o usuário
// provavelmente quer; secundário: superfície elevada com fio de contorno. Passar o mouse acende o contorno em ouro.
internal sealed class ThemedButton : Button {
    bool hover, pressed;
    public bool Primary { get; set; }
    public ThemedButton() {
        FlatStyle=FlatStyle.Flat; Cursor=Cursors.Hand; UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed=true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        g.Clear(Theme.Background);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        var bounds=new Rectangle(0,0,Width-1,Height-1);
        Color border, text;
        Brush fill;
        if(!Enabled) { fill=new SolidBrush(Theme.Surface); border=Color.FromArgb(34,46,50); text=Color.FromArgb(96,110,110); }
        else if(Primary) {
            Color top=pressed?Theme.GoldMid:hover?Color.FromArgb(247,214,134):Theme.Gold, bottom=pressed?Theme.GoldDark:hover?Theme.GoldMid:Color.FromArgb(214,142,52);
            fill=new LinearGradientBrush(bounds,top,bottom,90f); border=Theme.GoldDark; text=Color.FromArgb(24,18,10);
        } else {
            fill=new SolidBrush(pressed?Theme.Surface:hover?Color.FromArgb(40,56,61):Theme.Raised); border=hover?Theme.GoldMid:Theme.Line; text=Theme.Ink;
        }
        using(fill) using(var path=Theme.Rounded(bounds,3)) using(var pen=new Pen(border)) {
            g.FillPath(fill,path); g.DrawPath(pen,path);
        }
        if(Focused && ShowFocusCues) using(var ring=new Pen(Color.FromArgb(120,Theme.Gold)) { DashStyle=DashStyle.Dot }) g.DrawRectangle(ring,3,3,Width-7,Height-7);
        using(var font=new Font(Font,Primary?FontStyle.Bold:FontStyle.Regular))
            TextRenderer.DrawText(g,Text,font,new Rectangle(4,0,Width-8,Height),text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
}
