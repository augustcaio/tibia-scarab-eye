using System.Drawing;
using System.Windows.Forms;

namespace TibiaScarabEye.UI;

// Lista escura de linhas com a seleção em verde-azulado e uma barra dourada à esquerda. Quem herda desenha o conteúdo da linha.
internal class ThemedListBox : ListBox {
    protected const int TextInset=12;
    public ThemedListBox() {
        DrawMode=DrawMode.OwnerDrawFixed; ItemHeight=28; IntegralHeight=false; BorderStyle=BorderStyle.FixedSingle;
        BackColor=Theme.Surface; ForeColor=Theme.Ink;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        if(e.Index<0) return;
        var g=e.Graphics;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using(var back=new SolidBrush(selected?Theme.Teal:Theme.Surface)) g.FillRectangle(back,e.Bounds);
        if(selected) using(var bar=new SolidBrush(Theme.GoldMid)) g.FillRectangle(bar,e.Bounds.Left,e.Bounds.Top,3,e.Bounds.Height);
        using(var line=new Pen(Color.FromArgb(28,40,43))) g.DrawLine(line,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
        DrawRow(g,e.Bounds,e.Index,selected);
    }
    protected virtual void DrawRow(Graphics g,Rectangle bounds,int index,bool selected) {
        TextRenderer.DrawText(g,Items[index].ToString(),Font,new Rectangle(bounds.X+TextInset,bounds.Y,bounds.Width-TextInset-6,bounds.Height),Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
}
