using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Lista de camadas do editor: uma linha por área, com olho (mostrar/esconder), cadeado (travar) e nome.
// Os itens são os RegionSpec das áreas, do topo para a base. Clicar no olho ou no cadeado não muda a seleção.
internal sealed class LayerList : ListBox {
    const int EyeX=8, LockX=34, TextX=62, IconWidth=20;
    public event Action<int> EyeClicked, LockClicked;
    public LayerList() {
        DrawMode=DrawMode.OwnerDrawFixed; SelectionMode=SelectionMode.MultiExtended;
        ItemHeight=26; IntegralHeight=false; BorderStyle=BorderStyle.FixedSingle;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        var spec=e.Index>=0?Items[e.Index] as RegionSpec:null;
        if(spec==null) return;
        var g=e.Graphics;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using(var back=new SolidBrush(selected?Color.FromArgb(92,82,58):Color.FromArgb(39,39,37))) g.FillRectangle(back,e.Bounds);
        Color ink=spec.Hidden?Color.FromArgb(120,120,112):Theme.Ink;
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        DrawEye(g,new Rectangle(e.Bounds.X+EyeX,e.Bounds.Y+7,16,12),!spec.Hidden,ink);
        DrawLock(g,new Rectangle(e.Bounds.X+LockX+2,e.Bounds.Y+5,11,16),spec.Locked);
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g,spec.Name,Font,new Rectangle(e.Bounds.X+TextX,e.Bounds.Y,e.Bounds.Width-TextX-4,e.Bounds.Height),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        using(var line=new Pen(Color.FromArgb(58,58,55))) g.DrawLine(line,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
    }
    static void DrawEye(Graphics g,Rectangle r,bool open,Color ink) {
        using(var pen=new Pen(ink,1.5f)) {
            g.DrawEllipse(pen,r);
            if(open) using(var pupil=new SolidBrush(ink)) g.FillEllipse(pupil,r.X+r.Width/2-3,r.Y+r.Height/2-3,6,6);
            else g.DrawLine(pen,r.Left-1,r.Bottom+1,r.Right+1,r.Top-1);
        }
    }
    static void DrawLock(Graphics g,Rectangle r,bool locked) {
        Color color=locked?Theme.Gold:Color.FromArgb(110,110,102);
        using(var pen=new Pen(color,1.5f)) {
            var body=new Rectangle(r.X,r.Y+7,r.Width,r.Height-7);
            if(locked) using(var fill=new SolidBrush(color)) g.FillRectangle(fill,body); else g.DrawRectangle(pen,body);
            g.DrawArc(pen,r.X+2,r.Y,r.Width-4,12,180,locked?180:140);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        int index=IndexFromPoint(e.Location);
        if(index>=0 && e.Button==MouseButtons.Left) {
            if(e.X>=EyeX && e.X<EyeX+IconWidth) { if(EyeClicked!=null) EyeClicked(index); return; }
            if(e.X>=LockX && e.X<LockX+IconWidth) { if(LockClicked!=null) LockClicked(index); return; }
        }
        base.OnMouseDown(e);
    }
}
