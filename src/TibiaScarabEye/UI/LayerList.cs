using System;
using System.Drawing;
using System.Windows.Forms;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Lista de camadas do editor: uma linha por área, com olho (mostrar/esconder), cadeado (travar) e nome.
// Os itens são os RegionSpec das áreas, do topo para a base. Clicar no olho ou no cadeado não muda a seleção.
internal sealed class LayerList : ThemedListBox {
    const int EyeX=10, LockX=36, TextX=64, IconWidth=20;
    public event Action<int> EyeClicked, LockClicked;
    public LayerList() { SelectionMode=SelectionMode.MultiExtended; ItemHeight=28; }
    protected override void DrawRow(Graphics g,Rectangle bounds,int index,bool selected) {
        var spec=Items[index] as RegionSpec;
        if(spec==null) return;
        Color ink=spec.Hidden?Color.FromArgb(104,118,118):Theme.Ink;
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        DrawEye(g,new Rectangle(bounds.X+EyeX,bounds.Y+9,16,12),!spec.Hidden,ink);
        DrawLock(g,new Rectangle(bounds.X+LockX+2,bounds.Y+6,11,16),spec.Locked);
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.Default;
        TextRenderer.DrawText(g,spec.Name,Font,new Rectangle(bounds.X+TextX,bounds.Y,bounds.Width-TextX-6,bounds.Height),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    static void DrawEye(Graphics g,Rectangle r,bool open,Color ink) {
        using(var pen=new Pen(ink,1.5f)) {
            g.DrawEllipse(pen,r);
            if(open) using(var pupil=new SolidBrush(ink)) g.FillEllipse(pupil,r.X+r.Width/2-3,r.Y+r.Height/2-3,6,6);
            else g.DrawLine(pen,r.Left-1,r.Bottom+1,r.Right+1,r.Top-1);
        }
    }
    static void DrawLock(Graphics g,Rectangle r,bool locked) {
        Color color=locked?Theme.Gold:Color.FromArgb(88,104,106);
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
