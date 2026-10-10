using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Título de seção em serifa dourada com um fio que se apaga até a borda: separa grupos de controles sem desenhar caixas.
internal sealed class SectionHeader : Control {
    readonly Font font=new Font("Georgia",11f,FontStyle.Bold);
    public SectionHeader(string text) {
        Text=text; Height=28; Dock=DockStyle.Fill; Margin=new Padding(0,0,0,6);
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;
        Size size=TextRenderer.MeasureText(g,Text,font,new Size(int.MaxValue,Height),TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g,Text,font,new Rectangle(0,0,size.Width+2,Height),Theme.Gold,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        int x=size.Width+12, w=Width-x;
        if(w>8) using(var line=new LinearGradientBrush(new Rectangle(x,0,w,2),Color.FromArgb(170,Theme.GoldMid),Color.FromArgb(0,Theme.GoldMid),0f)) g.FillRectangle(line,x,Height/2,w,1);
    }
    protected override void Dispose(bool disposing) { if(disposing) font.Dispose(); base.Dispose(disposing); }
}
